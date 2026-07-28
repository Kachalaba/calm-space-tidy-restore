using System;
using System.Threading;
using CalmSpace.Core;
using Cysharp.Threading.Tasks;

namespace CalmSpace.Monetization
{
    /// <summary>
    /// Provider-neutral monetization policy. Receipt/store restoration remains
    /// authoritative; the encrypted local state is only a conservative cache.
    /// </summary>
    public sealed class MonetizationManager : IMonetizationManager
    {
        private const int CurrentStateSchemaVersion = 1;

        private readonly IAdProvider _adProvider;
        private readonly INoAdsEntitlementProvider _entitlementProvider;
        private readonly IMonetizationStateStore _stateStore;
        private readonly IPresentationActivityCoordinator
            _activityCoordinator;
        private readonly IMonotonicClock _clock;
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
        private bool _cachedLifetimeNoAds;
        private EntitlementVerification _entitlementVerification =
            EntitlementVerification.Unverified;
        private bool _suppressInterruptiveAds = true;
        private int _interruptiveAdReservations;
        private double _lastInterstitialOpenedAtSeconds;
        private ProviderEntitlementRestoreResult _lastRestoreResult =
            new ProviderEntitlementRestoreResult(
                ProviderEntitlementStatus.Unavailable,
                null);
        private int _providerAvailabilityNotificationQueued;

        public MonetizationManager(
            IAdProvider adProvider,
            INoAdsEntitlementProvider entitlementProvider,
            IMonetizationStateStore stateStore,
            IPresentationActivityCoordinator activityCoordinator,
            IMonotonicClock clock,
            MonetizationOptions options)
        {
            _adProvider = adProvider ??
                throw new ArgumentNullException(nameof(adProvider));
            _entitlementProvider = entitlementProvider ??
                throw new ArgumentNullException(
                    nameof(entitlementProvider));
            _stateStore = stateStore ??
                throw new ArgumentNullException(nameof(stateStore));
            _activityCoordinator = activityCoordinator ??
                throw new ArgumentNullException(
                    nameof(activityCoordinator));
            _clock = clock ??
                throw new ArgumentNullException(nameof(clock));
            _options = options ??
                throw new ArgumentNullException(nameof(options));

            _adProvider.AvailabilityChanged +=
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
                        _cachedLifetimeNoAds,
                        _entitlementVerification,
                        _suppressInterruptiveAds);
                }
            }
        }

        public bool IsNoAds
        {
            get
            {
                lock (_stateSync)
                {
                    return _cachedLifetimeNoAds;
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

                    _lastInterstitialOpenedAtSeconds =
                        ReadMonotonicTimeFailClosed();
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

                ProviderInitializationStatus initializationStatus;
                try
                {
                    initializationStatus =
                        await _adProvider.InitializeAsync(
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
                    initializationStatus =
                        ProviderInitializationStatus.Unavailable;
                }

                ProviderEntitlementRestoreResult restoreResult =
                    await RestoreFromProviderCoreAsync(
                        cancellationToken);

                lock (_stateSync)
                {
                    _adProviderStatus = initializationStatus;
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

        public async UniTask<InterstitialAdResult>
            TryShowInterstitialAsync(
                InterstitialAdRequest request,
                CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (request == null)
            {
                return BlockedInterstitial(
                    AdBlockReason.InvalidRequest);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return new InterstitialAdResult(
                    AdShowOutcome.Cancelled,
                    AdBlockReason.Cancelled,
                    null);
            }

            MonetizationSnapshot snapshot = Snapshot;
            if (!snapshot.IsInitialized)
            {
                return BlockedInterstitial(
                    AdBlockReason.NotInitialized);
            }

            if (snapshot.SuppressInterruptiveAds)
            {
                return BlockedInterstitial(
                    AdBlockReason.EntitlementSuppressed);
            }

            if (!IsProviderReady(
                AdFormat.Interstitial,
                request.PlacementId))
            {
                return BlockedInterstitial(
                    AdBlockReason.ProviderNotReady);
            }

            if (!IsInterstitialCooldownElapsed())
            {
                return BlockedInterstitial(
                    AdBlockReason.Cooldown);
            }

            if (!_activityCoordinator.TryEnterAd(
                out IDisposable adLease,
                out AdBlockReason activityBlockReason))
            {
                return BlockedInterstitial(activityBlockReason);
            }

            var policyReservation = false;
            try
            {
                snapshot = Snapshot;
                if (snapshot.SuppressInterruptiveAds)
                {
                    return BlockedInterstitial(
                        AdBlockReason.EntitlementSuppressed);
                }

                if (!IsInterstitialCooldownElapsed())
                {
                    return BlockedInterstitial(
                        AdBlockReason.Cooldown);
                }

                if (!IsProviderReady(
                    AdFormat.Interstitial,
                    request.PlacementId))
                {
                    return BlockedInterstitial(
                        AdBlockReason.ProviderNotReady);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return new InterstitialAdResult(
                        AdShowOutcome.Cancelled,
                        AdBlockReason.Cancelled,
                        null);
                }

                if (!TryReserveInterruptiveAd())
                {
                    return BlockedInterstitial(
                        AdBlockReason.EntitlementSuppressed);
                }

                policyReservation = true;
                var observer = new InterstitialSessionObserver(
                    this);
                ProviderAdResult providerResult;
                try
                {
                    providerResult = await _adProvider.ShowAsync(
                        new ProviderAdRequest(
                            AdFormat.Interstitial,
                            request.PlacementId),
                        observer,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    await UniTask.SwitchToMainThread();
                    return new InterstitialAdResult(
                        AdShowOutcome.Cancelled,
                        AdBlockReason.Cancelled,
                        null);
                }
                catch (Exception exception)
                {
                    await UniTask.SwitchToMainThread();
                    return new InterstitialAdResult(
                        AdShowOutcome.Failed,
                        AdBlockReason.None,
                        exception.GetType().Name);
                }

                await UniTask.SwitchToMainThread();
                return new InterstitialAdResult(
                    MapAdOutcome(providerResult.Outcome),
                    AdBlockReason.None,
                    providerResult.ProviderMessage);
            }
            finally
            {
                if (policyReservation)
                {
                    ReleaseInterruptiveAdReservation();
                }

                adLease.Dispose();
            }
        }

        public UniTask<InterstitialAdResult>
            TryShowInterstitialBetweenLevelsAsync(
                string placementId,
                CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(placementId))
            {
                return UniTask.FromResult(
                    BlockedInterstitial(
                        AdBlockReason.InvalidRequest));
            }

            return TryShowInterstitialAsync(
                new InterstitialAdRequest(placementId),
                cancellationToken);
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
                    BlockedRewarded(
                        AdBlockReason.InvalidRequest));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return CompleteWithoutSession(
                    callbacks,
                    new RewardedAdResult(
                        AdShowOutcome.Cancelled,
                        AdBlockReason.Cancelled,
                        false,
                        null));
            }

            MonetizationSnapshot snapshot = Snapshot;
            if (!snapshot.IsInitialized)
            {
                return CompleteWithoutSession(
                    callbacks,
                    BlockedRewarded(
                        AdBlockReason.NotInitialized));
            }

            if (snapshot.CachedLifetimeNoAds &&
                !_options.AllowRewardedForNoAdsOwners)
            {
                return CompleteWithoutSession(
                    callbacks,
                    BlockedRewarded(
                        AdBlockReason.EntitlementSuppressed));
            }

            if (!IsProviderReady(
                AdFormat.Rewarded,
                request.PlacementId))
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
                if (snapshot.CachedLifetimeNoAds &&
                    !_options.AllowRewardedForNoAdsOwners)
                {
                    return CompleteWithoutSession(
                        callbacks,
                        BlockedRewarded(
                            AdBlockReason.EntitlementSuppressed));
                }

                if (!IsProviderReady(
                    AdFormat.Rewarded,
                    request.PlacementId))
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
                        new RewardedAdResult(
                            AdShowOutcome.Cancelled,
                            AdBlockReason.Cancelled,
                            false,
                            null));
                }

                observer = new RewardedSessionObserver(
                    callbacks,
                    new RewardGrant(
                        request.RewardId,
                        request.Amount));

                ProviderAdResult providerResult;
                try
                {
                    providerResult = await _adProvider.ShowAsync(
                        new ProviderAdRequest(
                            AdFormat.Rewarded,
                            request.PlacementId,
                            request.RewardId,
                            request.Amount),
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
                if (observer != null)
                {
                    observer.CompleteIfNeeded();
                }

                adLease.Dispose();
            }
        }

        public UniTask<RewardedAdResult> TryShowRewardedAsync(
            RewardedAdRequest request,
            IRewardedAdCallbacks callbacks,
            CancellationToken cancellationToken = default)
        {
            return ShowRewardedAsync(
                request,
                callbacks,
                cancellationToken);
        }

        public async UniTask<ProviderNoAdsPurchaseResult>
            PurchaseLifetimeNoAdsAsync(
                CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await InitializeAsync(cancellationToken);
            await _entitlementGate.WaitAsync(cancellationToken);
            var availabilityChanged = false;
            ProviderNoAdsPurchaseResult result;

            try
            {
                try
                {
                    result =
                        await _entitlementProvider
                            .PurchaseLifetimeNoAdsAsync(
                                cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    result = new ProviderNoAdsPurchaseResult(
                        ProviderPurchaseStatus.Cancelled,
                        null);
                }
                catch (Exception exception)
                {
                    result = new ProviderNoAdsPurchaseResult(
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
                    availabilityChanged = true;
                }
            }
            finally
            {
                _entitlementGate.Release();
            }

            if (availabilityChanged)
            {
                await UniTask.SwitchToMainThread();
                RaiseAvailabilityChanged();
            }

            return result;
        }

        public UniTask<ProviderNoAdsPurchaseResult>
            PurchaseNoAdsAsync(
                CancellationToken cancellationToken = default)
        {
            return PurchaseLifetimeNoAdsAsync(cancellationToken);
        }

        public async UniTask<ProviderEntitlementRestoreResult>
            RestoreLifetimeNoAdsAsync(
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
                await RestoreFromProviderCoreAsync(cancellationToken);
            lock (_stateSync)
            {
                _lastRestoreResult = result;
            }

            await UniTask.SwitchToMainThread();
            RaiseAvailabilityChanged();
            return result;
        }

        public UniTask<ProviderEntitlementRestoreResult>
            RestoreNoAdsAsync(
                CancellationToken cancellationToken = default)
        {
            return RestoreLifetimeNoAdsAsync(cancellationToken);
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

            _adProvider.AvailabilityChanged -=
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
                        await _entitlementProvider
                            .RestoreLifetimeNoAdsAsync(
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
                    case ProviderEntitlementStatus
                        .VerifiedEntitled:
                        ApplyVerifiedEntitlement(true);
                        await PersistAuthoritativeStateAsync(
                            true,
                            cancellationToken);
                        break;

                    case ProviderEntitlementStatus
                        .VerifiedNotEntitled:
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
            bool lifetimeNoAds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PersistentStateSaveResult saveResult =
                await _stateStore.SaveAsync(
                    new PersistentMonetizationState(
                        CurrentStateSchemaVersion,
                        lifetimeNoAds,
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
                _cachedLifetimeNoAds =
                    loadResult.HasState &&
                    loadResult.State.SchemaVersion ==
                    CurrentStateSchemaVersion &&
                    loadResult.State.LifetimeNoAds;
                _entitlementVerification =
                    EntitlementVerification.Unverified;
                _suppressInterruptiveAds = true;
            }
        }

        private void ApplyVerifiedEntitlement(bool lifetimeNoAds)
        {
            lock (_stateSync)
            {
                _cachedLifetimeNoAds = lifetimeNoAds;
                _entitlementVerification = lifetimeNoAds
                    ? EntitlementVerification.VerifiedEntitled
                    : EntitlementVerification.VerifiedNotEntitled;
                _suppressInterruptiveAds = lifetimeNoAds;
            }
        }

        private bool TryReserveInterruptiveAd()
        {
            lock (_stateSync)
            {
                if (_suppressInterruptiveAds || _isDisposed)
                {
                    return false;
                }

                _interruptiveAdReservations++;
                return true;
            }
        }

        private void ReleaseInterruptiveAdReservation()
        {
            lock (_stateSync)
            {
                if (_interruptiveAdReservations > 0)
                {
                    _interruptiveAdReservations--;
                }
            }
        }

        private void ApplyUnavailableEntitlement()
        {
            lock (_stateSync)
            {
                _entitlementVerification =
                    EntitlementVerification.Unavailable;
                _suppressInterruptiveAds = true;
            }
        }

        private bool IsInterstitialCooldownElapsed()
        {
            double now = ReadMonotonicTimeFailClosed();
            double lastOpened;
            lock (_stateSync)
            {
                lastOpened = _lastInterstitialOpenedAtSeconds;
            }

            if (!IsFinite(now) ||
                !IsFinite(lastOpened) ||
                now < lastOpened)
            {
                return false;
            }

            return now - lastOpened >=
                _options.InterstitialCooldown.TotalSeconds;
        }

        private void MarkInterstitialOpened()
        {
            double now = ReadMonotonicTimeFailClosed();
            lock (_stateSync)
            {
                if (!IsFinite(now) ||
                    !IsFinite(_lastInterstitialOpenedAtSeconds) ||
                    now < _lastInterstitialOpenedAtSeconds)
                {
                    // A clock failure at the actual open boundary must poison
                    // this run rather than make a second ad immediately
                    // eligible after the clock recovers.
                    _lastInterstitialOpenedAtSeconds = double.NaN;
                    return;
                }

                _lastInterstitialOpenedAtSeconds = now;
            }
        }

        private double ReadMonotonicTimeFailClosed()
        {
            try
            {
                return _clock.NowSeconds;
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        private bool IsProviderReady(
            AdFormat format,
            string placementId)
        {
            try
            {
                return _adProvider.IsReady(format, placementId);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        private static InterstitialAdResult BlockedInterstitial(
            AdBlockReason reason)
        {
            return new InterstitialAdResult(
                AdShowOutcome.Blocked,
                reason,
                null);
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

        private static RewardedAdResult CompleteWithoutSession(
            IRewardedAdCallbacks callbacks,
            RewardedAdResult result)
        {
            if (callbacks != null)
            {
                try
                {
                    callbacks.OnCompleted(result);
                }
                catch (Exception)
                {
                    // Consumer callbacks cannot compromise presentation
                    // cleanup or cause a duplicate completion.
                }
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
                    ref _providerAvailabilityNotificationQueued,
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
                ref _providerAvailabilityNotificationQueued,
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
                    // One observer must not prevent other observers from
                    // receiving the provider availability transition.
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

        private sealed class InterstitialSessionObserver :
            IProviderAdSessionObserver
        {
            private readonly MonetizationManager _owner;
            private int _opened;

            public InterstitialSessionObserver(
                MonetizationManager owner)
            {
                _owner = owner;
            }

            public void OnOpened()
            {
                if (Interlocked.Exchange(ref _opened, 1) == 0)
                {
                    _owner.MarkInterstitialOpened();
                }
            }

            public void OnRewardEarned()
            {
            }
        }

        private sealed class RewardedSessionObserver :
            IProviderAdSessionObserver
        {
            private readonly object _sync = new object();
            private readonly IRewardedAdCallbacks _callbacks;
            private readonly RewardGrant _reward;
            private bool _rewardSignalReceived;
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
                    if (_completed || _rewardSignalReceived)
                    {
                        return;
                    }

                    _rewardSignalReceived = true;
                }
            }

            public RewardedAdResult Complete(
                AdShowOutcome outcome,
                AdBlockReason blockReason,
                string providerMessage)
            {
                IRewardedAdCallbacks callbacks;
                RewardedAdResult result;
                var grantReward = false;

                lock (_sync)
                {
                    if (_completed)
                    {
                        return _completedResult;
                    }

                    grantReward =
                        outcome == AdShowOutcome.Completed &&
                        _rewardSignalReceived;
                    result = new RewardedAdResult(
                        outcome,
                        blockReason,
                        grantReward,
                        providerMessage);
                    _completedResult = result;
                    _completed = true;
                    callbacks = _callbacks;
                }

                if (callbacks == null)
                {
                    return result;
                }

                if (grantReward)
                {
                    try
                    {
                        callbacks.OnRewardEarned(_reward);
                    }
                    catch (Exception)
                    {
                        // The authoritative signal remains consumed once.
                    }
                }

                try
                {
                    callbacks.OnCompleted(result);
                }
                catch (Exception)
                {
                    // Completion is still considered delivered once.
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
