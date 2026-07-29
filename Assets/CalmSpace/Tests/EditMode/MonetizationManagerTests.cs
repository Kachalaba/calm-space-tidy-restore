using System;
using System.Reflection;
using System.Threading;
using CalmSpace.Core;
using CalmSpace.Monetization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace CalmSpace.Tests.EditMode
{
    public sealed class MonetizationManagerTests
    {
        [Test]
        public void PublicMonetizationContractIsRewardedOnly()
        {
            Assembly runtimeAssembly =
                typeof(IMonetizationManager).Assembly;

            Assert.That(
                runtimeAssembly.GetType(
                    "CalmSpace.Monetization.InterstitialAdRequest"),
                Is.Null);
            Assert.That(
                runtimeAssembly.GetType(
                    "CalmSpace.Monetization.InterstitialAdResult"),
                Is.Null);
            Assert.That(
                runtimeAssembly.GetType(
                    "CalmSpace.Monetization.AdFormat"),
                Is.Null);

            MethodInfo[] managerMethods =
                typeof(IMonetizationManager).GetMethods();
            for (var index = 0;
                 index < managerMethods.Length;
                 index++)
            {
                Assert.That(
                    managerMethods[index].Name,
                    Does.Not.Contain("Interstitial"));
            }

            MethodInfo[] providerMethods =
                typeof(IRewardedAdProvider).GetMethods();
            for (var index = 0;
                 index < providerMethods.Length;
                 index++)
            {
                Assert.That(
                    providerMethods[index].Name,
                    Does.Not.Contain("Interstitial"));
            }
        }

        [Test]
        public void UnavailableRestorePreservesCachedRelaxPass()
        {
            var cached = new PersistentMonetizationState(
                schemaVersion: 1,
                relaxPassOwned: true,
                verifiedAtUnixSeconds: 123);
            Harness harness = CreateHarness(
                ProviderEntitlementStatus.Unavailable,
                PersistentStateLoadResult.Found(cached));

            harness.Manager.InitializeAsync().GetAwaiter().GetResult();

            Assert.That(harness.Manager.HasRelaxPass, Is.True);
            Assert.That(
                harness.Manager.Snapshot.RelaxPassOwned,
                Is.True);
            Assert.That(
                harness.Manager.Snapshot.EntitlementVerification,
                Is.EqualTo(EntitlementVerification.Unavailable));
            Assert.That(harness.Store.SaveCalls, Is.Zero);
        }

        [Test]
        public void AuthoritativeRestoreCanClearCachedRelaxPass()
        {
            var cached = new PersistentMonetizationState(
                schemaVersion: 1,
                relaxPassOwned: true,
                verifiedAtUnixSeconds: 123);
            Harness harness = CreateHarness(
                ProviderEntitlementStatus.VerifiedNotEntitled,
                PersistentStateLoadResult.Found(cached));

            harness.Manager.InitializeAsync().GetAwaiter().GetResult();

            Assert.That(harness.Manager.HasRelaxPass, Is.False);
            Assert.That(
                harness.Manager.Snapshot.EntitlementVerification,
                Is.EqualTo(
                    EntitlementVerification.VerifiedNotEntitled));
            Assert.That(harness.Store.SaveCalls, Is.EqualTo(1));
            Assert.That(
                harness.Store.LastSavedState.RelaxPassOwned,
                Is.False);
        }

        [Test]
        public void RewardedPresentationIsBlockedUntilInitialized()
        {
            Harness harness = CreateHarness(
                ProviderEntitlementStatus.VerifiedNotEntitled,
                PersistentStateLoadResult.Missing());
            var callbacks = new RecordingRewardCallbacks();

            RewardedAdResult result = harness.Manager
                .ShowRewardedAsync(CreateHintRequest(), callbacks)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.Outcome, Is.EqualTo(AdShowOutcome.Blocked));
            Assert.That(
                result.BlockReason,
                Is.EqualTo(AdBlockReason.NotInitialized));
            Assert.That(result.RewardEarned, Is.False);
            Assert.That(callbacks.ApplyCalls, Is.Zero);
            Assert.That(callbacks.CompletionCalls, Is.EqualTo(1));
            Assert.That(harness.Ads.ShowCalls, Is.Zero);
        }

        [Test]
        public void GameplayAndDragLeasesBlockRewardedProviderCalls()
        {
            Harness harness = CreateHarness(
                ProviderEntitlementStatus.VerifiedNotEntitled,
                PersistentStateLoadResult.Missing());
            harness.Manager.InitializeAsync().GetAwaiter().GetResult();
            var callbacks = new RecordingRewardCallbacks();

            Assert.That(
                harness.Activity.TryEnterGameplay(
                    out IDisposable gameplayLease),
                Is.True);
            RewardedAdResult duringGameplay = harness.Manager
                .ShowRewardedAsync(CreateHintRequest(), callbacks)
                .GetAwaiter()
                .GetResult();
            gameplayLease.Dispose();

            Assert.That(
                duringGameplay.BlockReason,
                Is.EqualTo(AdBlockReason.GameplayActive));

            Assert.That(
                harness.Activity.TryEnterDrag(
                    out IDisposable dragLease),
                Is.True);
            RewardedAdResult duringDrag = harness.Manager
                .ShowRewardedAsync(CreateHintRequest(), callbacks)
                .GetAwaiter()
                .GetResult();
            dragLease.Dispose();

            Assert.That(
                duringDrag.BlockReason,
                Is.EqualTo(AdBlockReason.DragActive));
            Assert.That(harness.Ads.ShowCalls, Is.Zero);
            Assert.That(callbacks.ApplyCalls, Is.Zero);
            Assert.That(callbacks.CompletionCalls, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateProviderSignalsApplyRewardExactlyOnce()
        {
            Harness harness = CreateHarness(
                ProviderEntitlementStatus.VerifiedNotEntitled,
                PersistentStateLoadResult.Missing());
            harness.Manager.InitializeAsync().GetAwaiter().GetResult();
            harness.Ads.EmitReward = true;
            harness.Ads.EmitDuplicateReward = true;
            var callbacks = new RecordingRewardCallbacks();

            RewardedAdResult result = harness.Manager
                .ShowRewardedAsync(
                    new RewardedAdRequest(
                        "instant-hint",
                        RewardedBenefitKind.InstantSolutionHint,
                        2),
                    callbacks)
                .GetAwaiter()
                .GetResult();

            Assert.That(
                result.Outcome,
                Is.EqualTo(AdShowOutcome.Completed));
            Assert.That(result.RewardEarned, Is.True);
            Assert.That(callbacks.ApplyCalls, Is.EqualTo(1));
            Assert.That(callbacks.CompletionCalls, Is.EqualTo(1));
            Assert.That(
                callbacks.LastReward.Benefit,
                Is.EqualTo(
                    RewardedBenefitKind.InstantSolutionHint));
            Assert.That(callbacks.LastReward.Amount, Is.EqualTo(2));
            Assert.That(harness.Ads.ShowCalls, Is.EqualTo(1));
            Assert.That(harness.Activity.IsAdActive, Is.False);
        }

        [Test]
        public void PersistRejectedRewardIsNotReportedAsEarned()
        {
            Harness harness = CreateHarness(
                ProviderEntitlementStatus.VerifiedNotEntitled,
                PersistentStateLoadResult.Missing());
            harness.Manager.InitializeAsync().GetAwaiter().GetResult();
            harness.Ads.EmitReward = true;
            var callbacks = new RecordingRewardCallbacks
            {
                ApplyResult = false
            };

            RewardedAdResult result = harness.Manager
                .ShowRewardedAsync(
                    new RewardedAdRequest(
                        "extra-decor",
                        RewardedBenefitKind.ExtraRoomDecor),
                    callbacks)
                .GetAwaiter()
                .GetResult();

            Assert.That(
                result.Outcome,
                Is.EqualTo(AdShowOutcome.Completed));
            Assert.That(result.RewardEarned, Is.False);
            Assert.That(callbacks.ApplyCalls, Is.EqualTo(1));
            Assert.That(callbacks.CompletionCalls, Is.EqualTo(1));
        }

        [TestCase(ProviderAdOutcome.Failed)]
        [TestCase(ProviderAdOutcome.Closed)]
        [TestCase(ProviderAdOutcome.Cancelled)]
        [TestCase(ProviderAdOutcome.Unavailable)]
        public void NonCompletedProviderOutcomeNeverAppliesReward(
            ProviderAdOutcome providerOutcome)
        {
            Harness harness = CreateHarness(
                ProviderEntitlementStatus.VerifiedNotEntitled,
                PersistentStateLoadResult.Missing());
            harness.Manager.InitializeAsync().GetAwaiter().GetResult();
            harness.Ads.EmitReward = true;
            harness.Ads.Outcome = providerOutcome;
            var callbacks = new RecordingRewardCallbacks();

            RewardedAdResult result = harness.Manager
                .ShowRewardedAsync(CreateHintRequest(), callbacks)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.RewardEarned, Is.False);
            Assert.That(callbacks.ApplyCalls, Is.Zero);
            Assert.That(callbacks.CompletionCalls, Is.EqualTo(1));
            Assert.That(harness.Activity.IsAdActive, Is.False);
        }

        [Test]
        public void RelaxPassCanSuppressOptionalRewardedPlacements()
        {
            Harness harness = CreateHarness(
                ProviderEntitlementStatus.VerifiedEntitled,
                PersistentStateLoadResult.Missing(),
                allowRewardedForRelaxPassOwners: false);
            harness.Manager.InitializeAsync().GetAwaiter().GetResult();
            var callbacks = new RecordingRewardCallbacks();

            RewardedAdResult result = harness.Manager
                .ShowRewardedAsync(CreateHintRequest(), callbacks)
                .GetAwaiter()
                .GetResult();

            Assert.That(harness.Manager.HasRelaxPass, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(AdShowOutcome.Blocked));
            Assert.That(
                result.BlockReason,
                Is.EqualTo(AdBlockReason.EntitlementSuppressed));
            Assert.That(harness.Ads.ShowCalls, Is.Zero);
            Assert.That(callbacks.CompletionCalls, Is.EqualTo(1));
        }

        [Test]
        public void PurchasedRelaxPassIsCachedAfterAuthoritativeSuccess()
        {
            Harness harness = CreateHarness(
                ProviderEntitlementStatus.VerifiedNotEntitled,
                PersistentStateLoadResult.Missing());
            harness.RelaxPass.PurchaseStatus =
                ProviderPurchaseStatus.Purchased;
            harness.Manager.InitializeAsync().GetAwaiter().GetResult();
            int savesBeforePurchase = harness.Store.SaveCalls;

            ProviderRelaxPassPurchaseResult result = harness.Manager
                .PurchaseRelaxPassAsync()
                .GetAwaiter()
                .GetResult();

            Assert.That(
                result.Status,
                Is.EqualTo(ProviderPurchaseStatus.Purchased));
            Assert.That(harness.Manager.HasRelaxPass, Is.True);
            Assert.That(
                harness.Store.SaveCalls,
                Is.EqualTo(savesBeforePurchase + 1));
            Assert.That(
                harness.Store.LastSavedState.RelaxPassOwned,
                Is.True);
        }

        [Test]
        public void NoOpProvidersNeverAuthorizeOrDisplayAnything()
        {
            var ads = new NoOpRewardedAdProvider();
            var relaxPass = new NoOpRelaxPassEntitlementProvider();
            var observer = new RecordingProviderObserver();

            Assert.That(ads.IsReady("instant-hint"), Is.False);
            Assert.That(
                ads.InitializeAsync(default)
                    .GetAwaiter()
                    .GetResult(),
                Is.EqualTo(ProviderInitializationStatus.Unavailable));
            Assert.That(
                ads.ShowAsync(
                        new ProviderRewardedAdRequest("instant-hint"),
                        observer,
                        default)
                    .GetAwaiter()
                    .GetResult()
                    .Outcome,
                Is.EqualTo(ProviderAdOutcome.Unavailable));
            Assert.That(observer.OpenCalls, Is.Zero);
            Assert.That(observer.RewardCalls, Is.Zero);
            Assert.That(
                relaxPass.RestoreRelaxPassAsync(default)
                    .GetAwaiter()
                    .GetResult()
                    .Status,
                Is.EqualTo(ProviderEntitlementStatus.Unavailable));
        }

        private static RewardedAdRequest CreateHintRequest()
        {
            return new RewardedAdRequest(
                "instant-hint",
                RewardedBenefitKind.InstantSolutionHint);
        }

        private static Harness CreateHarness(
            ProviderEntitlementStatus entitlementStatus,
            PersistentStateLoadResult loadResult,
            bool allowRewardedForRelaxPassOwners = true)
        {
            var activity = new PresentationActivityCoordinator();
            var ads = new FakeRewardedAdProvider();
            var relaxPass =
                new FakeRelaxPassProvider(entitlementStatus);
            var store = new FakeStateStore(loadResult);
            var manager = new MonetizationManager(
                ads,
                relaxPass,
                store,
                activity,
                new MonetizationOptions(
                    allowRewardedForRelaxPassOwners));

            return new Harness(
                manager,
                activity,
                ads,
                relaxPass,
                store);
        }

        private sealed class Harness
        {
            public Harness(
                MonetizationManager manager,
                PresentationActivityCoordinator activity,
                FakeRewardedAdProvider ads,
                FakeRelaxPassProvider relaxPass,
                FakeStateStore store)
            {
                Manager = manager;
                Activity = activity;
                Ads = ads;
                RelaxPass = relaxPass;
                Store = store;
            }

            public MonetizationManager Manager { get; }

            public PresentationActivityCoordinator Activity { get; }

            public FakeRewardedAdProvider Ads { get; }

            public FakeRelaxPassProvider RelaxPass { get; }

            public FakeStateStore Store { get; }
        }

        private sealed class FakeRelaxPassProvider :
            IRelaxPassEntitlementProvider
        {
            private readonly ProviderEntitlementStatus _restoreStatus;

            public FakeRelaxPassProvider(
                ProviderEntitlementStatus restoreStatus)
            {
                _restoreStatus = restoreStatus;
            }

            public ProviderPurchaseStatus PurchaseStatus { get; set; } =
                ProviderPurchaseStatus.Unavailable;

            public UniTask<ProviderEntitlementRestoreResult>
                RestoreRelaxPassAsync(
                    CancellationToken cancellationToken)
            {
                return UniTask.FromResult(
                    new ProviderEntitlementRestoreResult(
                        _restoreStatus,
                        null));
            }

            public UniTask<ProviderRelaxPassPurchaseResult>
                PurchaseRelaxPassAsync(
                    CancellationToken cancellationToken)
            {
                return UniTask.FromResult(
                    new ProviderRelaxPassPurchaseResult(
                        cancellationToken.IsCancellationRequested
                            ? ProviderPurchaseStatus.Cancelled
                            : PurchaseStatus,
                        null));
            }
        }

        private sealed class FakeStateStore : IMonetizationStateStore
        {
            private readonly PersistentStateLoadResult _loadResult;

            public FakeStateStore(
                PersistentStateLoadResult loadResult)
            {
                _loadResult = loadResult;
            }

            public int SaveCalls { get; private set; }

            public PersistentMonetizationState LastSavedState
            {
                get;
                private set;
            }

            public UniTask<PersistentStateLoadResult> LoadAsync(
                CancellationToken cancellationToken)
            {
                return UniTask.FromResult(_loadResult);
            }

            public UniTask<PersistentStateSaveResult> SaveAsync(
                PersistentMonetizationState state,
                CancellationToken cancellationToken)
            {
                SaveCalls++;
                LastSavedState = state;
                return UniTask.FromResult(
                    cancellationToken.IsCancellationRequested
                        ? PersistentStateSaveResult.Cancelled()
                        : PersistentStateSaveResult.Succeeded());
            }
        }

        private sealed class FakeRewardedAdProvider :
            IRewardedAdProvider
        {
            public int ShowCalls { get; private set; }

            public bool Ready { get; set; } = true;

            public bool EmitReward { get; set; }

            public bool EmitDuplicateReward { get; set; }

            public ProviderAdOutcome Outcome { get; set; } =
                ProviderAdOutcome.Completed;

            public event Action AvailabilityChanged;

            public UniTask<ProviderInitializationStatus> InitializeAsync(
                CancellationToken cancellationToken)
            {
                return UniTask.FromResult(
                    Ready
                        ? ProviderInitializationStatus.Ready
                        : ProviderInitializationStatus.Unavailable);
            }

            public bool IsReady(string placementId)
            {
                return Ready;
            }

            public UniTask<ProviderAdResult> ShowAsync(
                ProviderRewardedAdRequest request,
                IProviderRewardedAdSessionObserver observer,
                CancellationToken cancellationToken)
            {
                ShowCalls++;
                observer.OnOpened();

                if (EmitReward)
                {
                    observer.OnRewardEarned();
                    if (EmitDuplicateReward)
                    {
                        observer.OnRewardEarned();
                    }
                }

                return UniTask.FromResult(
                    new ProviderAdResult(
                        cancellationToken.IsCancellationRequested
                            ? ProviderAdOutcome.Cancelled
                            : Outcome,
                        null));
            }
        }

        private sealed class RecordingRewardCallbacks :
            IRewardedAdCallbacks
        {
            public bool ApplyResult { get; set; } = true;

            public int ApplyCalls { get; private set; }

            public int CompletionCalls { get; private set; }

            public RewardGrant LastReward { get; private set; }

            public bool TryApplyReward(RewardGrant reward)
            {
                ApplyCalls++;
                LastReward = reward;
                return ApplyResult;
            }

            public void OnCompleted(RewardedAdResult result)
            {
                CompletionCalls++;
            }
        }

        private sealed class RecordingProviderObserver :
            IProviderRewardedAdSessionObserver
        {
            public int OpenCalls { get; private set; }

            public int RewardCalls { get; private set; }

            public void OnOpened()
            {
                OpenCalls++;
            }

            public void OnRewardEarned()
            {
                RewardCalls++;
            }
        }
    }
}
