using System;
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
        public void CooldownBelowThreeMinutesIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MonetizationOptions(
                    TimeSpan.FromSeconds(179.999d),
                    allowRewardedForNoAdsOwners: true));
        }

        [Test]
        public void UnavailableEntitlementFailsClosed()
        {
            var harness = CreateHarness(
                ProviderEntitlementStatus.Unavailable,
                PersistentStateLoadResult.Missing());

            harness.Manager.InitializeAsync().GetAwaiter().GetResult();

            Assert.That(
                harness.Manager.Snapshot.SuppressInterruptiveAds,
                Is.True);
            Assert.That(
                harness.Manager.Snapshot.EntitlementVerification,
                Is.EqualTo(EntitlementVerification.Unavailable));
        }

        [Test]
        public void UnavailableRestoreNeverClearsCachedOwnership()
        {
            var state = new PersistentMonetizationState(
                schemaVersion: 1,
                lifetimeNoAds: true,
                verifiedAtUnixSeconds: 123);
            var harness = CreateHarness(
                ProviderEntitlementStatus.Unavailable,
                PersistentStateLoadResult.Found(state));

            harness.Manager.InitializeAsync().GetAwaiter().GetResult();

            Assert.That(harness.Manager.Snapshot.CachedLifetimeNoAds, Is.True);
            Assert.That(
                harness.Manager.Snapshot.SuppressInterruptiveAds,
                Is.True);
            Assert.That(harness.Store.SaveCalls, Is.Zero);
        }

        [Test]
        public void InterstitialUsesInclusiveThreeMinuteCooldown()
        {
            var harness = CreateHarness(
                ProviderEntitlementStatus.VerifiedNotEntitled,
                PersistentStateLoadResult.Missing());
            harness.Clock.NowSeconds = 10d;
            harness.Manager.InitializeAsync().GetAwaiter().GetResult();

            harness.Clock.NowSeconds = 189.999d;
            var early = harness.Manager
                .TryShowInterstitialAsync(
                    new InterstitialAdRequest("between-levels"))
                .GetAwaiter()
                .GetResult();

            Assert.That(early.Outcome, Is.EqualTo(AdShowOutcome.Blocked));
            Assert.That(early.BlockReason, Is.EqualTo(AdBlockReason.Cooldown));
            Assert.That(harness.Ads.ShowCalls, Is.Zero);

            harness.Clock.NowSeconds = 190d;
            var first = harness.Manager
                .TryShowInterstitialAsync(
                    new InterstitialAdRequest("between-levels"))
                .GetAwaiter()
                .GetResult();

            Assert.That(first.Outcome, Is.EqualTo(AdShowOutcome.Completed));
            Assert.That(harness.Ads.ShowCalls, Is.EqualTo(1));

            harness.Clock.NowSeconds = 369.999d;
            var secondEarly = harness.Manager
                .TryShowInterstitialAsync(
                    new InterstitialAdRequest("between-levels"))
                .GetAwaiter()
                .GetResult();

            Assert.That(
                secondEarly.BlockReason,
                Is.EqualTo(AdBlockReason.Cooldown));

            harness.Clock.NowSeconds = 370d;
            var second = harness.Manager
                .TryShowInterstitialAsync(
                    new InterstitialAdRequest("between-levels"))
                .GetAwaiter()
                .GetResult();

            Assert.That(second.Outcome, Is.EqualTo(AdShowOutcome.Completed));
            Assert.That(harness.Ads.ShowCalls, Is.EqualTo(2));
        }

        [Test]
        public void GameplayAndDragLeasesBlockProviderCalls()
        {
            var harness = CreateHarness(
                ProviderEntitlementStatus.VerifiedNotEntitled,
                PersistentStateLoadResult.Missing());
            harness.Manager.InitializeAsync().GetAwaiter().GetResult();
            harness.Clock.NowSeconds = 180d;

            Assert.That(
                harness.Activity.TryEnterGameplay(out var gameplay),
                Is.True);
            var duringGameplay = harness.Manager
                .TryShowInterstitialAsync(
                    new InterstitialAdRequest("between-levels"))
                .GetAwaiter()
                .GetResult();
            Assert.That(
                duringGameplay.BlockReason,
                Is.EqualTo(AdBlockReason.GameplayActive));
            gameplay.Dispose();

            Assert.That(harness.Activity.TryEnterDrag(out var drag), Is.True);
            var duringDrag = harness.Manager
                .TryShowInterstitialAsync(
                    new InterstitialAdRequest("between-levels"))
                .GetAwaiter()
                .GetResult();
            Assert.That(
                duringDrag.BlockReason,
                Is.EqualTo(AdBlockReason.DragActive));
            drag.Dispose();

            Assert.That(harness.Ads.ShowCalls, Is.Zero);
        }

        [Test]
        public void ClockFailureAtOpenPoisonsInterstitialCooldown()
        {
            var harness = CreateHarness(
                ProviderEntitlementStatus.VerifiedNotEntitled,
                PersistentStateLoadResult.Missing());
            harness.Manager.InitializeAsync().GetAwaiter().GetResult();
            harness.Clock.NowSeconds = 180d;
            harness.Clock.ThrowOnReadNumber =
                harness.Clock.ReadCount + 3;

            var first = harness.Manager
                .TryShowInterstitialAsync(
                    new InterstitialAdRequest("between-levels"))
                .GetAwaiter()
                .GetResult();

            Assert.That(first.Outcome, Is.EqualTo(AdShowOutcome.Completed));

            harness.Clock.ThrowOnReadNumber = 0;
            harness.Clock.NowSeconds = 181d;
            var second = harness.Manager
                .TryShowInterstitialAsync(
                    new InterstitialAdRequest("between-levels"))
                .GetAwaiter()
                .GetResult();

            Assert.That(second.Outcome, Is.EqualTo(AdShowOutcome.Blocked));
            Assert.That(second.BlockReason, Is.EqualTo(AdBlockReason.Cooldown));
            Assert.That(harness.Ads.ShowCalls, Is.EqualTo(1));
        }

        [Test]
        public void RewardIsGrantedOnceOnlyAfterProviderSignal()
        {
            var harness = CreateHarness(
                ProviderEntitlementStatus.VerifiedNotEntitled,
                PersistentStateLoadResult.Missing());
            harness.Manager.InitializeAsync().GetAwaiter().GetResult();
            harness.Ads.EmitReward = true;
            harness.Ads.EmitDuplicateReward = true;
            var callbacks = new RecordingRewardCallbacks();

            var result = harness.Manager
                .ShowRewardedAsync(
                    new RewardedAdRequest("hint", "hint-token", 1),
                    callbacks)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.Outcome, Is.EqualTo(AdShowOutcome.Completed));
            Assert.That(result.RewardEarned, Is.True);
            Assert.That(callbacks.RewardCalls, Is.EqualTo(1));
            Assert.That(callbacks.CompletionCalls, Is.EqualTo(1));
        }

        [TestCase(ProviderAdOutcome.Failed)]
        [TestCase(ProviderAdOutcome.Closed)]
        [TestCase(ProviderAdOutcome.Cancelled)]
        public void NonCompletedRewardedOutcomeNeverGrantsReward(
            ProviderAdOutcome providerOutcome)
        {
            var harness = CreateHarness(
                ProviderEntitlementStatus.VerifiedNotEntitled,
                PersistentStateLoadResult.Missing());
            harness.Manager.InitializeAsync().GetAwaiter().GetResult();
            harness.Ads.EmitReward = true;
            harness.Ads.EmitDuplicateReward = true;
            harness.Ads.Outcome = providerOutcome;
            var callbacks = new RecordingRewardCallbacks();

            var result = harness.Manager
                .ShowRewardedAsync(
                    new RewardedAdRequest("hint", "hint-token", 1),
                    callbacks)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.RewardEarned, Is.False);
            Assert.That(callbacks.RewardCalls, Is.Zero);
            Assert.That(callbacks.CompletionCalls, Is.EqualTo(1));
        }

        [Test]
        public void NoOpProvidersNeverAuthorizeOrDisplayAnything()
        {
            var ads = new NoOpAdProvider();
            var entitlements = new NoOpNoAdsEntitlementProvider();

            Assert.That(ads.IsReady(AdFormat.Rewarded, "hint"), Is.False);
            Assert.That(
                ads.InitializeAsync(default).GetAwaiter().GetResult(),
                Is.EqualTo(ProviderInitializationStatus.Unavailable));
            Assert.That(
                entitlements
                    .RestoreLifetimeNoAdsAsync(default)
                    .GetAwaiter()
                    .GetResult()
                    .Status,
                Is.EqualTo(ProviderEntitlementStatus.Unavailable));
        }

        private static Harness CreateHarness(
            ProviderEntitlementStatus entitlementStatus,
            PersistentStateLoadResult loadResult)
        {
            var clock = new FakeClock();
            var activity = new PresentationActivityCoordinator();
            var ads = new FakeAdProvider();
            var entitlements = new FakeEntitlementProvider(entitlementStatus);
            var store = new FakeStateStore(loadResult);
            var options = new MonetizationOptions(
                TimeSpan.FromSeconds(180),
                allowRewardedForNoAdsOwners: true);
            var manager = new MonetizationManager(
                ads,
                entitlements,
                store,
                activity,
                clock,
                options);

            return new Harness(
                manager,
                clock,
                activity,
                ads,
                store);
        }

        private sealed class Harness
        {
            public Harness(
                MonetizationManager manager,
                FakeClock clock,
                PresentationActivityCoordinator activity,
                FakeAdProvider ads,
                FakeStateStore store)
            {
                Manager = manager;
                Clock = clock;
                Activity = activity;
                Ads = ads;
                Store = store;
            }

            public MonetizationManager Manager { get; }

            public FakeClock Clock { get; }

            public PresentationActivityCoordinator Activity { get; }

            public FakeAdProvider Ads { get; }

            public FakeStateStore Store { get; }
        }

        private sealed class FakeClock : IMonotonicClock
        {
            private double _nowSeconds;

            public int ReadCount { get; private set; }

            public int ThrowOnReadNumber { get; set; }

            public double NowSeconds
            {
                get
                {
                    ReadCount++;
                    if (ThrowOnReadNumber == ReadCount)
                    {
                        throw new InvalidOperationException(
                            "Synthetic clock failure.");
                    }

                    return _nowSeconds;
                }
                set => _nowSeconds = value;
            }
        }

        private sealed class FakeEntitlementProvider :
            INoAdsEntitlementProvider
        {
            private readonly ProviderEntitlementStatus _status;

            public FakeEntitlementProvider(
                ProviderEntitlementStatus status)
            {
                _status = status;
            }

            public UniTask<ProviderEntitlementRestoreResult>
                RestoreLifetimeNoAdsAsync(
                    CancellationToken cancellationToken)
            {
                return UniTask.FromResult(
                    new ProviderEntitlementRestoreResult(_status, null));
            }

            public UniTask<ProviderNoAdsPurchaseResult>
                PurchaseLifetimeNoAdsAsync(
                    CancellationToken cancellationToken)
            {
                return UniTask.FromResult(
                    new ProviderNoAdsPurchaseResult(
                        ProviderPurchaseStatus.Unavailable,
                        null));
            }
        }

        private sealed class FakeStateStore : IMonetizationStateStore
        {
            private readonly PersistentStateLoadResult _loadResult;

            public FakeStateStore(PersistentStateLoadResult loadResult)
            {
                _loadResult = loadResult;
            }

            public int SaveCalls { get; private set; }

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
                return UniTask.FromResult(
                    PersistentStateSaveResult.Succeeded());
            }
        }

        private sealed class FakeAdProvider : IAdProvider
        {
            public int ShowCalls { get; private set; }

            public bool EmitReward { get; set; }

            public bool EmitDuplicateReward { get; set; }

            public ProviderAdOutcome Outcome { get; set; } =
                ProviderAdOutcome.Completed;

            public event Action AvailabilityChanged;

            public UniTask<ProviderInitializationStatus> InitializeAsync(
                CancellationToken cancellationToken)
            {
                return UniTask.FromResult(
                    ProviderInitializationStatus.Ready);
            }

            public bool IsReady(AdFormat format, string placementId)
            {
                return true;
            }

            public UniTask<ProviderAdResult> ShowAsync(
                ProviderAdRequest request,
                IProviderAdSessionObserver observer,
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
                        Outcome,
                        null));
            }
        }

        private sealed class RecordingRewardCallbacks :
            IRewardedAdCallbacks
        {
            public int RewardCalls { get; private set; }

            public int CompletionCalls { get; private set; }

            public void OnRewardEarned(RewardGrant reward)
            {
                RewardCalls++;
            }

            public void OnCompleted(RewardedAdResult result)
            {
                CompletionCalls++;
            }
        }
    }
}
