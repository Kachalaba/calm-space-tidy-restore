using System;
using System.Threading;
using CalmSpace.Core;
using Cysharp.Threading.Tasks;

namespace CalmSpace.Monetization
{
    /// <summary>
    /// Rewarded-only monetization policy. Fullscreen presentation is possible
    /// solely after an explicit player action, and the activity coordinator
    /// rejects it while gameplay or a drag owns the screen.
    /// </summary>
    public sealed class MonetizationManager : IMonetizationManager
    {
        private const int CurrentStateSchemaVersion = 1;

        private readonly IRewardedAdProvider _rewardedAdProvider;
        private readonly IRelaxPassEntitlementProvider
            _relaxPassProvider;
        private readonly IMonetizationStateStore _stateStore;
        private readonly IPresentationActivityCoordinator
            _activityCoordinator;
        private readonly MonetizationOptions _options;
        private readonly object _stateSync = new object();
        private readonly SemaphoreSlim _initializationGate =
            new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _entitlementGate =
            new SemaphoreSlim(1, 1);

        private bool _isInitialized;
        private bool _isDisposed;
        private ProviderInitializationStatus _adProviderStatus =
            ProviderInitializationStatus.Unavailable;
        private bool _relaxPassOwned;
        private EntitlementVerification _entitlementVerification =
            EntitlementVerification.Unverified;
        private ProviderEntitlementRestoreResult _lastRestoreResult =
            new ProviderEntitlementRestoreResult(
                ProviderEntitlementStatus.Unavailable,
                null);
        private int _availabilityNotificationQueued;

        public MonetizationManager(
            IRewardedAdProvider rewardedAdProvider,
            IRelaxPassEntitlementProvider relaxPassProvider,
            IMonetizationStateStore stateStore,
            IPresentationActivityCoordinator activityCoordinator,
            MonetizationOptions options)
        {
            _rewardedAdProvider = rewardedAdProvider ??
                throw new ArgumentNullException(
                    nameof(rewardedAdProvider));
            _relaxPassProvider = relaxPassProvider ??
                throw new ArgumentNullException(
                    nameof(relaxPassProvider));
            _stateStore = stateStore ??
                throw new ArgumentNullException(nameof(stateStore));
            _activityCoordinator = activityCoordinator ??
                throw new ArgumentNullException(
                    nameof(activityCoordinator));
            _options = options ??
                throw new ArgumentNullException(nameof(options));

            _rewardedAdProvider.AvailabilityChanged +=
                HandleProviderAvailabilityChanged;
        }

        public event Action AvailabilityChanged;

        public MonetizationSnapshot Snapshot
        {
            get
            {
                lock (_stateSync)
                {
                    return new MonetizationSnapshot(
                        _isInitialized,
                        _adProviderStatus,
                        _relaxPassOwned,
                        _entitlementVerification);
                }
            }
        }

        public bool HasRelaxPass
        {
            get
            {
                lock (_stateSync)
                {
                    return _relaxPassOwned;
                }
            }
        }

        public async UniTask InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await _initializationGate.WaitAsync(cancellationToken);
            try
            {
                lock (_stateSync)
                {
                    if (_isInitialized)
                    {
                        return;
                    }
                }

                PersistentStateLoadResult loadResult;
                try
                {
                    loadResult = await _stateStore.LoadAsync(
                        cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    loadResult = PersistentStateLoadResult.Failed(
                        PersistentStateLoadStatus.IoError,
                        exception.GetType().Name);
                }

                ApplyCachedState(loadResult);

                ProviderInitializationStatus adStatus;
                try
                {
                    adStatus =
                        await _rewardedAdProvider.InitializeAsync(
                            cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    adStatus =
                        ProviderInitializationStatus.Unavailable;
                }

                ProviderEntitlementRestoreResult restoreResult =
                    await RestoreFromProviderCoreAsync(
                        cancellationToken);

                lock (_stateSync)
                {
                    _adProviderStatus = adStatus;
                    _lastRestoreResult = restoreResult;
                    _isInitialized = true;
                }
            }
            finally
            {
                _initializationGate.Release();
            }

            await UniTask.SwitchToMainThread();
            RaiseAvailabilityChanged();
        }

        public async UniTask<RewardedAdResult> ShowRewardedAsync(
            RewardedAdRequest request,
            IRewardedAdCallbacks callbacks,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (request == null)
            {
                return CompleteWithoutSession(
                    callbacks,
                    BlockedRewarded(AdBlockReason.InvalidRequest));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return CompleteWithoutSession(
                    callbacks,
                    CancelledRewarded());
            }

            MonetizationSnapshot snapshot = Snapshot;
            if (!snapshot.IsInitialized)
            {
                return CompleteWithoutSession(
                    callbacks,
                    BlockedRewarded(AdBlockReason.NotInitialized));
            }

            if (snapshot.RelaxPassOwned &&
                !_options.AllowRewardedForRelaxPassOwners)
            {
                return CompleteWithoutSession(
                    callbacks,
                    BlockedRewarded(
                        AdBlockReason.EntitlementSuppressed));
            }

            if (!IsProviderReady(request.PlacementId))
            {
                return CompleteWithoutSession(
                    callbacks,
                    BlockedRewarded(
                        AdBlockReason.ProviderNotReady));
            }

            if (!_activityCoordinator.TryEnterAd(
                    out IDisposable adLease,
                    out AdBlockReason activityBlockReason))
            {
                return CompleteWithoutSession(
                    callbacks,
                    BlockedRewarded(activityBlockReason));
            }

            RewardedSessionObserver observer = null;
            try
            {
                snapshot = Snapshot;
                if (snapshot.RelaxPassOwned &&
                    !_options.AllowRewardedForRelaxPassOwners)
                {
                    return CompleteWithoutSession(
                        callbacks,
                        BlockedRewarded(
                            AdBlockReason.EntitlementSuppressed));
                }

                if (!IsProviderReady(request.PlacementId))
                {
                    return CompleteWithoutSession(
                        callbacks,
                        BlockedRewarded(
                            AdBlockReason.ProviderNotReady));
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return CompleteWithoutSession(
                        callbacks,
                        CancelledRewarded());
                }

                observer = new RewardedSessionObserver(
                    callbacks,
                    new RewardGrant(
                        request.Benefit,
                        request.Amount));

                ProviderAdResult providerResult;
                try
                {
                    providerResult =
                        await _rewardedAdProvider.ShowAsync(
                            new ProviderRewardedAdRequest(
                                request.PlacementId),
                            observer,
                            cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    await UniTask.SwitchToMainThread();
                    return observer.Complete(
                        AdShowOutcome.Cancelled,
                        AdBlockReason.Cancelled,
                        null);
                }
                catch (Exception exception)
                {
                    await UniTask.SwitchToMainThread();
                    return observer.Complete(
                        AdShowOutcome.Failed,
                        AdBlockReason.None,
                        exception.GetType().Name);
                }

                await UniTask.SwitchToMainThread();
                return observer.Complete(
                    MapAdOutcome(providerResult.Outcome),
                    AdBlockReason.None,
                    providerResult.ProviderMessage);
            }
            finally
            {
                observer?.CompleteIfNeeded();
                adLease.Dispose();
            }
        }

        public async UniTask<ProviderRelaxPassPurchaseResult>
            PurchaseRelaxPassAsync(
                CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await InitializeAsync(cancellationToken);
            await _entitlementGate.WaitAsync(cancellationToken);
            var changed = false;
            ProviderRelaxPassPurchaseResult result;

            try
            {
                try
                {
                    result =
                        await _relaxPassProvider
                            .PurchaseRelaxPassAsync(
                                cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    result = new ProviderRelaxPassPurchaseResult(
                        ProviderPurchaseStatus.Cancelled,
                        null);
                }
                catch (Exception exception)
                {
                    result = new ProviderRelaxPassPurchaseResult(
                        ProviderPurchaseStatus.Failed,
                        exception.GetType().Name);
                }

                if (result.Status ==
                        ProviderPurchaseStatus.Purchased ||
                    result.Status ==
                        ProviderPurchaseStatus.AlreadyOwned)
                {
                    ApplyVerifiedEntitlement(true);
                    await PersistAuthoritativeStateAsync(
                        true,
                        cancellationToken);
                    changed = true;
                }
            }
            finally
            {
                _entitlementGate.Release();
            }

            if (changed)
            {
                await UniTask.SwitchToMainThread();
                RaiseAvailabilityChanged();
            }

            return result;
        }

        public async UniTask<ProviderEntitlementRestoreResult>
            RestoreRelaxPassAsync(
                CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            bool wasInitialized = Snapshot.IsInitialized;
            await InitializeAsync(cancellationToken);
            if (!wasInitialized)
            {
                lock (_stateSync)
                {
                    return _lastRestoreResult;
                }
            }

            ProviderEntitlementRestoreResult result =
                await RestoreFromProviderCoreAsync(
                    cancellationToken);
            lock (_stateSync)
            {
                _lastRestoreResult = result;
            }

            await UniTask.SwitchToMainThread();
            RaiseAvailabilityChanged();
            return result;
        }

        public void Dispose()
        {
            lock (_stateSync)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
            }

            _rewardedAdProvider.AvailabilityChanged -=
                HandleProviderAvailabilityChanged;
        }

        private async UniTask<ProviderEntitlementRestoreResult>
            RestoreFromProviderCoreAsync(
                CancellationToken cancellationToken)
        {
            await _entitlementGate.WaitAsync(cancellationToken);
            try
            {
                ProviderEntitlementRestoreResult result;
                try
                {
                    result =
                        await _relaxPassProvider
                            .RestoreRelaxPassAsync(
                                cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    result =
                        new ProviderEntitlementRestoreResult(
                            ProviderEntitlementStatus.Unavailable,
                            exception.GetType().Name);
                }

                switch (result.Status)
                {
                    case ProviderEntitlementStatus.VerifiedEntitled:
                        ApplyVerifiedEntitlement(true);
                        await PersistAuthoritativeStateAsync(
                            true,
                            cancellationToken);
                        break;
                    case ProviderEntitlementStatus.VerifiedNotEntitled:
                        ApplyVerifiedEntitlement(false);
                        await PersistAuthoritativeStateAsync(
                            false,
                            cancellationToken);
                        break;
                    default:
                        ApplyUnavailableEntitlement();
                        break;
                }

                return result;
            }
            finally
            {
                _entitlementGate.Release();
            }
        }

        private async UniTask PersistAuthoritativeStateAsync(
            bool relaxPassOwned,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PersistentStateSaveResult saveResult =
                await _stateStore.SaveAsync(
                    new PersistentMonetizationState(
                        CurrentStateSchemaVersion,
                        relaxPassOwned,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    cancellationToken);
            if (saveResult.Status ==
                    PersistentStateSaveStatus.Cancelled ||
                cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    cancellationToken);
            }
        }

        private void ApplyCachedState(
            PersistentStateLoadResult loadResult)
        {
            lock (_stateSync)
            {
                _relaxPassOwned =
                    loadResult.HasState &&
                    loadResult.State.SchemaVersion ==
                        CurrentStateSchemaVersion &&
                    loadResult.State.RelaxPassOwned;
                _entitlementVerification =
                    EntitlementVerification.Unverified;
            }
        }

        private void ApplyVerifiedEntitlement(bool relaxPassOwned)
        {
            lock (_stateSync)
            {
                _relaxPassOwned = relaxPassOwned;
                _entitlementVerification = relaxPassOwned
                    ? EntitlementVerification.VerifiedEntitled
                    : EntitlementVerification.VerifiedNotEntitled;
            }
        }

        private void ApplyUnavailableEntitlement()
        {
            lock (_stateSync)
            {
                _entitlementVerification =
                    EntitlementVerification.Unavailable;
            }
        }

        private bool IsProviderReady(string placementId)
        {
            try
            {
                return _rewardedAdProvider.IsReady(placementId);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static RewardedAdResult BlockedRewarded(
            AdBlockReason reason)
        {
            return new RewardedAdResult(
                AdShowOutcome.Blocked,
                reason,
                false,
                null);
        }

        private static RewardedAdResult CancelledRewarded()
        {
            return new RewardedAdResult(
                AdShowOutcome.Cancelled,
                AdBlockReason.Cancelled,
                false,
                null);
        }

        private static RewardedAdResult CompleteWithoutSession(
            IRewardedAdCallbacks callbacks,
            RewardedAdResult result)
        {
            if (callbacks == null)
            {
                return result;
            }

            try
            {
                callbacks.OnCompleted(result);
            }
            catch (Exception)
            {
                // A consumer cannot compromise presentation cleanup.
            }

            return result;
        }

        private static AdShowOutcome MapAdOutcome(
            ProviderAdOutcome outcome)
        {
            switch (outcome)
            {
                case ProviderAdOutcome.Completed:
                    return AdShowOutcome.Completed;
                case ProviderAdOutcome.Closed:
                    return AdShowOutcome.Closed;
                case ProviderAdOutcome.Failed:
                    return AdShowOutcome.Failed;
                case ProviderAdOutcome.Cancelled:
                    return AdShowOutcome.Cancelled;
                default:
                    return AdShowOutcome.Unavailable;
            }
        }

        private void HandleProviderAvailabilityChanged()
        {
            if (Interlocked.Exchange(
                    ref _availabilityNotificationQueued,
                    1) != 0)
            {
                return;
            }

            UniTask.Post(
                DispatchProviderAvailabilityChanged,
                PlayerLoopTiming.Update);
        }

        private void DispatchProviderAvailabilityChanged()
        {
            Interlocked.Exchange(
                ref _availabilityNotificationQueued,
                0);
            lock (_stateSync)
            {
                if (_isDisposed)
                {
                    return;
                }
            }

            RaiseAvailabilityChanged();
        }

        private void RaiseAvailabilityChanged()
        {
            Action handler = AvailabilityChanged;
            if (handler == null)
            {
                return;
            }

            foreach (Action subscriber in
                     handler.GetInvocationList())
            {
                try
                {
                    subscriber();
                }
                catch (Exception)
                {
                    // One observer cannot starve the others.
                }
            }
        }

        private void ThrowIfDisposed()
        {
            lock (_stateSync)
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(
                        nameof(MonetizationManager));
                }
            }
        }

        private sealed class RewardedSessionObserver :
            IProviderRewardedAdSessionObserver
        {
            private readonly object _sync = new object();
            private readonly IRewardedAdCallbacks _callbacks;
            private readonly RewardGrant _reward;
            private bool _rewardSignalReceived;
            private bool _completing;
            private bool _completed;
            private RewardedAdResult _completedResult;

            public RewardedSessionObserver(
                IRewardedAdCallbacks callbacks,
                RewardGrant reward)
            {
                _callbacks = callbacks;
                _reward = reward;
            }

            public void OnOpened()
            {
            }

            public void OnRewardEarned()
            {
                lock (_sync)
                {
                    if (!_completed)
                    {
                        _rewardSignalReceived = true;
                    }
                }
            }

            public RewardedAdResult Complete(
                AdShowOutcome outcome,
                AdBlockReason blockReason,
                string providerMessage)
            {
                bool shouldApply;
                lock (_sync)
                {
                    while (_completing && !_completed)
                    {
                        Monitor.Wait(_sync);
                    }

                    if (_completed)
                    {
                        return _completedResult;
                    }

                    _completing = true;
                    shouldApply =
                        outcome == AdShowOutcome.Completed &&
                        _rewardSignalReceived &&
                        _callbacks != null;
                }

                var applied = false;
                if (shouldApply)
                {
                    try
                    {
                        applied =
                            _callbacks.TryApplyReward(_reward);
                    }
                    catch (Exception)
                    {
                        applied = false;
                    }
                }

                var result = new RewardedAdResult(
                    outcome,
                    blockReason,
                    applied,
                    providerMessage);
                lock (_sync)
                {
                    _completedResult = result;
                    _completed = true;
                    _completing = false;
                    Monitor.PulseAll(_sync);
                }

                if (_callbacks != null)
                {
                    try
                    {
                        _callbacks.OnCompleted(result);
                    }
                    catch (Exception)
                    {
                        // Completion remains delivered once.
                    }
                }

                return result;
            }

            public void CompleteIfNeeded()
            {
                Complete(
                    AdShowOutcome.Failed,
                    AdBlockReason.None,
                    null);
            }
        }
    }
}
