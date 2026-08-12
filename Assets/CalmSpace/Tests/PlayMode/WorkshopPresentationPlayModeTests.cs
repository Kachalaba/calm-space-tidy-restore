using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using CalmSpace.Audio;
using CalmSpace.Demo;
using CalmSpace.Levels;
using CalmSpace.UI;
using CalmSpace.Workshop;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CalmSpace.Tests.PlayMode
{
    /// <summary>
    /// The minimal reveal loop: a first completion queues one room reveal in
    /// the persisted profile, coming home plays it, an interrupted reveal
    /// stays pending, and a successful one retires exactly once.
    /// </summary>
    public sealed class WorkshopPresentationPlayModeTests
    {
        private const string ProfileFileName = "player-profile-v1.bin";
        private const string FirstRevealBeatId =
            "cozy-workshop.clear-passage";
        private const string UnavailableMemoryId =
            "legacy.unavailable-memory";
        private const string InvalidRevealDiagnostic =
            "Workshop reveal cozy-workshop.clear-passage could not be " +
            "retired because its persisted presentation state is invalid; " +
            "the reveal remains queued for recovery.";

        private int _invalidRevealDiagnosticCount;

        /// <summary>
        /// The reveal queue lives in the persisted profile, so each test needs
        /// a clean profile to be order independent and repeatable.
        /// </summary>
        [SetUp]
        public void ResetPersistedProfile()
        {
            // The store restores from its .bak companion, so a partial reset
            // would silently carry a completed chapter into the next test.
            string path = System.IO.Path.Combine(
                Application.persistentDataPath, ProfileFileName);
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string candidate = path + suffix;
                if (System.IO.File.Exists(candidate))
                {
                    System.IO.File.Delete(candidate);
                }
            }

            _invalidRevealDiagnosticCount = 0;
            Application.logMessageReceived += CountInvalidRevealDiagnostic;
        }

        [TearDown]
        public void StopRecordingDiagnostics()
        {
            Application.logMessageReceived -= CountInvalidRevealDiagnostic;
        }

        [UnityTest]
        public IEnumerator FirstCompletionRevealsTheZoneAndRetiresItOnce()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            IDemoProgressStore store = Store(experience);
            yield return WaitForRoom(home);

            yield return CompleteFirstLevel(experience);

            Assert.That(
                PendingRevealCount(store),
                Is.EqualTo(1),
                "Completing a level must queue exactly one room reveal.");

            yield return ReturnHome(experience, home);
            yield return WaitForRevealStart(home);

            WorkshopRoomPresenter room = Room(home);
            Assert.That(
                room.IsRevealPlaying,
                Is.True,
                "Coming home must start the pending reveal.");

            yield return WaitForRevealEnd(home, 12f);

            Assert.That(
                PendingRevealCount(store),
                Is.Zero,
                "A completed reveal must be marked seen exactly once.");
            Assert.That(
                store.Current.CompletedLevelMask & 1,
                Is.Not.Zero,
                "The permanent restored state must survive the reveal.");
        }

        [UnityTest]
        public IEnumerator InterruptedRevealStaysPendingAndReplaysOnNextEntry()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            var audio = new RecordingAudio();
            SetPrivateField(home, "_audioService", audio);
            IDemoProgressStore store = Store(experience);
            yield return WaitForRoom(home);

            yield return CompleteFirstLevel(experience);
            yield return ReturnHome(experience, home);
            yield return WaitForRevealStart(home);
            Assert.That(
                audio.Count(AsmrAudioCue.RoomReveal),
                Is.EqualTo(1));

            // Leaving the workshop mid-reveal persists nothing.
            home.NotifyHidden();
            yield return WaitForRevealEnd(home, 5f, false);

            Assert.That(
                PendingRevealCount(store),
                Is.EqualTo(1),
                "A cancelled reveal must never be marked seen.");

            WorkshopRoomPresenter room = Room(home);
            for (var index = 0; index < room.BeatCount; index++)
            {
                Assert.That(
                    room.GetBeat(index).BeforeGroup.alpha,
                    Is.EqualTo(0f).Within(0.001f),
                    "Cancellation must remove every temporary overlay.");
            }

            yield return Await(home.NotifyVisibleAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));
            yield return WaitForRevealStart(home);
            Assert.That(
                audio.Count(AsmrAudioCue.RoomReveal),
                Is.EqualTo(2),
                "Replaying an interrupted visual starts one fresh cue.");
            Assert.That(
                Room(home).IsRevealPlaying,
                Is.True,
                "The next entry must replay the unfinished reveal.");

            yield return WaitForRevealEnd(home, 12f);
            Assert.That(PendingRevealCount(store), Is.Zero);
        }

        [UnityTest]
        public IEnumerator
            MidRevealHideThenImmediateReentryRestoresProjectionAndReplaysPendingHeadOnce()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            var audio = new RecordingAudio();
            SetPrivateField(home, "_audioService", audio);
            yield return WaitForRoom(home);

            WorkshopRoomPresenter room = Room(home);
            var frames = new ControlledRevealFrameDriver();
            room.RevealFrameDriver = frames.WaitForNextFrameAsync;
            var store = new InMemoryProgressStore(
                CreatePendingSnapshot(
                    0b1,
                    PendingPresentationEntry.RoomReveal(
                        FirstRevealBeatId)),
                WorkshopContentIds.CozyWorkshopBeatCount);
            ReplaceProgressStore(experience, home, store);

            home.NotifyHidden();
            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));
            yield return Await(home.NotifyVisibleAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));

            Assert.That(frames.WaitCount, Is.EqualTo(1));
            Assert.That(audio.Count(AsmrAudioCue.RoomReveal), Is.EqualTo(1));
            Assert.That(room.IsRevealPlaying, Is.True);

            frames.Advance(WorkshopRoomPresenter.RevealSeconds / 2f);

            WorkshopRoomBeatBinding firstBeat = room.GetBeat(0);
            Assert.That(frames.WaitCount, Is.EqualTo(2));
            Assert.That(
                firstBeat.BeforeGroup.alpha,
                Is.EqualTo(0.5f).Within(0.0001f),
                "Half of the reveal duration must produce a true mid-reveal " +
                "overlay, not a boundary sample.");

            home.NotifyHidden();
            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));
            yield return Await(home.NotifyVisibleAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));

            Assert.That(room.IsVisible, Is.True);
            Assert.That(frames.WaitCount, Is.EqualTo(2));
            Assert.That(
                GetPrivateField<bool>(home, "_revealPlaying"),
                Is.True,
                "Immediate re-entry must occur before cancellation settles.");

            frames.ReleaseCancelledFrame();

            Assert.That(
                frames.WaitCount,
                Is.EqualTo(3),
                "A visible home must start a new controlled reveal-frame wait " +
                "after the cancelled continuation settles.");
            Assert.That(frames.PendingCount, Is.EqualTo(1));
            Assert.That(audio.Count(AsmrAudioCue.RoomReveal), Is.EqualTo(2));
            Assert.That(room.IsRevealPlaying, Is.True);

            frames.Advance(WorkshopRoomPresenter.RevealSeconds);

            Assert.That(frames.WaitCount, Is.EqualTo(3));
            Assert.That(frames.PendingCount, Is.Zero);
            Assert.That(store.RetirementAttemptCount, Is.EqualTo(1));
            Assert.That(store.Current.SeenRoomRevealCount, Is.EqualTo(1));
            Assert.That(
                store.Current.HasSeenRoomReveal(FirstRevealBeatId),
                Is.True);
            Assert.That(PendingRevealCount(store), Is.Zero);
            Assert.That(home.View.BottomSheet.IsOpen, Is.False);
            Assert.That(room.IsVisible, Is.True);
            Assert.That(
                GetPrivateField<GameObject>(room, "_ambientRoot")
                    .activeInHierarchy,
                Is.True);

            IWorkshopProgressProjector projector =
                GetPrivateField<IWorkshopProgressProjector>(
                    home,
                    "_projector");
            Assert.That(
                projector.TryProject(
                    store.Current,
                    out WorkshopProgressProjection projection),
                Is.True);
            Assert.That(projection.CompletedBeatCount, Is.EqualTo(1));
            Assert.That(projection.RestoredZoneMask, Is.EqualTo(0b1));
            Assert.That(projection.IsComplete, Is.False);
            Assert.That(
                WorkshopContentIds.TryGetCozyWorkshopBeat(
                    1,
                    out WorkshopBeatContract nextBeat),
                Is.True);
            Assert.That(
                projection.ActiveHotspotBeatId,
                Is.EqualTo(nextBeat.BeatId));
            Assert.That(
                GetPrivateField<CanvasGroup>(room, "_finaleGroup").alpha,
                Is.EqualTo(0f).Within(0.0001f));

            var activeHotspotCount = 0;
            var interactableHotspotCount = 0;
            for (var index = 0; index < room.BeatCount; index++)
            {
                WorkshopRoomBeatBinding binding = room.GetBeat(index);
                Assert.That(
                    binding.RestoredGroup.alpha,
                    Is.EqualTo(binding.ZoneIndex == 0 ? 1f : 0f)
                        .Within(0.0001f));
                Assert.That(
                    binding.BeforeGroup.alpha,
                    Is.EqualTo(0f).Within(0.0001f));
                Assert.That(binding.RestoredGroup.interactable, Is.False);
                Assert.That(binding.RestoredGroup.blocksRaycasts, Is.False);
                Assert.That(binding.BeforeGroup.interactable, Is.False);
                Assert.That(binding.BeforeGroup.blocksRaycasts, Is.False);

                if (!binding.Hotspot.gameObject.activeInHierarchy)
                {
                    continue;
                }

                activeHotspotCount++;
                Assert.That(binding.BeatId, Is.EqualTo(nextBeat.BeatId));
                if (binding.Hotspot.interactable)
                {
                    interactableHotspotCount++;
                }
            }

            Assert.That(activeHotspotCount, Is.EqualTo(1));
            Assert.That(interactableHotspotCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RevealFrameFaultKeepsPendingHeadWithoutAutomaticReplay()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            var audio = new RecordingAudio();
            SetPrivateField(home, "_audioService", audio);
            yield return WaitForRoom(home);

            WorkshopRoomPresenter room = Room(home);
            var frames = new ControlledRevealFrameDriver();
            room.RevealFrameDriver = frames.WaitForNextFrameAsync;
            var store = new InMemoryProgressStore(
                CreatePendingSnapshot(
                    0b1,
                    PendingPresentationEntry.RoomReveal(
                        FirstRevealBeatId)),
                WorkshopContentIds.CozyWorkshopBeatCount);
            ReplaceProgressStore(experience, home, store);

            home.NotifyHidden();
            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));
            yield return Await(home.NotifyVisibleAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));

            Assert.That(frames.WaitCount, Is.EqualTo(1));
            Assert.That(audio.Count(AsmrAudioCue.RoomReveal), Is.EqualTo(1));

            LogAssert.Expect(
                LogType.Exception,
                new Regex("Controlled reveal frame failure"));
            frames.Fail(new InvalidOperationException(
                "Controlled reveal frame failure."));

            Assert.That(
                frames.WaitCount,
                Is.EqualTo(1),
                "A frame fault is not controller cancellation and must not " +
                "start another reveal wait.");
            Assert.That(frames.PendingCount, Is.Zero);
            Assert.That(audio.Count(AsmrAudioCue.RoomReveal), Is.EqualTo(1));
            Assert.That(
                GetPrivateField<bool>(home, "_revealPlaying"),
                Is.False);
            Assert.That(room.IsRevealPlaying, Is.False);
            Assert.That(store.RetirementAttemptCount, Is.Zero);
            Assert.That(store.Current.SeenRoomRevealCount, Is.Zero);
            Assert.That(PendingRevealCount(store), Is.EqualTo(1));
            AssertFreshHead(
                store,
                PendingPresentationEntry.RoomReveal(FirstRevealBeatId));
            Assert.That(room.GetBeat(0).BeforeGroup.alpha, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator
            ForeignRevealCancellationDuringImmediateReentryDoesNotReplay()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            var audio = new RecordingAudio();
            SetPrivateField(home, "_audioService", audio);
            yield return WaitForRoom(home);

            WorkshopRoomPresenter room = Room(home);
            var frames = new ControlledRevealFrameDriver();
            room.RevealFrameDriver = frames.WaitForNextFrameAsync;
            var store = new InMemoryProgressStore(
                CreatePendingSnapshot(
                    0b1,
                    PendingPresentationEntry.RoomReveal(
                        FirstRevealBeatId)),
                WorkshopContentIds.CozyWorkshopBeatCount);
            ReplaceProgressStore(experience, home, store);

            home.NotifyHidden();
            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));
            yield return Await(home.NotifyVisibleAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));

            Assert.That(frames.WaitCount, Is.EqualTo(1));
            Assert.That(audio.Count(AsmrAudioCue.RoomReveal), Is.EqualTo(1));

            home.NotifyHidden();
            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));
            yield return Await(home.NotifyVisibleAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));

            using var foreignCancellation = new CancellationTokenSource();
            foreignCancellation.Cancel();
            frames.ReleaseCancelledFrame(foreignCancellation.Token);

            Assert.That(
                frames.WaitCount,
                Is.EqualTo(1),
                "Only cancellation from the exact controller token may " +
                "restart the pending reveal.");
            Assert.That(frames.PendingCount, Is.Zero);
            Assert.That(audio.Count(AsmrAudioCue.RoomReveal), Is.EqualTo(1));
            Assert.That(
                GetPrivateField<bool>(home, "_revealPlaying"),
                Is.False);
            Assert.That(store.RetirementAttemptCount, Is.Zero);
            Assert.That(store.Current.SeenRoomRevealCount, Is.Zero);
            AssertFreshHead(
                store,
                PendingPresentationEntry.RoomReveal(FirstRevealBeatId));
        }

        [UnityTest]
        public IEnumerator MissingRevealVisualStaysSilentAndShowsFallback()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            var audio = new RecordingAudio();
            SetPrivateField(home, "_audioService", audio);
            yield return WaitForRoom(home);
            yield return CompleteFirstLevel(experience);

            var missingRoomObject = new GameObject("Missing reveal room");
            var missingRoom =
                missingRoomObject.AddComponent<WorkshopRoomPresenter>();
            missingRoom.Configure(
                WorkshopContentIds.CozyWorkshopChapterId,
                missingRoomObject,
                null,
                null,
                Array.Empty<WorkshopRoomBeatBinding>());
            SetPrivateField(home, "_room", missingRoom);

            LogAssert.Expect(
                LogType.Warning,
                "Workshop room has no authored visual for beat " +
                "cozy-workshop.clear-passage; the room stays usable.");
            yield return ReturnHome(experience, home);
            yield return WaitForRevealEnd(home, 2f, false);

            Assert.That(
                audio.Count(AsmrAudioCue.RoomReveal),
                Is.Zero,
                "A missing visual is a fallback, not a reveal playback.");
            Assert.That(
                GetPrivateField<string>(home, "_revealFallbackBeatId"),
                Is.EqualTo("cozy-workshop.clear-passage"));
            Assert.That(home.View.BottomSheet.IsOpen, Is.True);

            home.View.BottomSheet.ActionButton.onClick.Invoke();
            yield return null;
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.EqualTo(1),
                "Skipping an available reveal fallback emits one UI cue.");
        }

        [UnityTest]
        public IEnumerator NullRoomLoadOffersRevealFallbackWithoutConsumingQueue()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            IDemoProgressStore store = Store(experience);
            yield return WaitForRoom(home);
            yield return CompleteFirstLevel(experience);

            var loader = new ControlledRoomLoader();
            ReplaceRoomLoader(home, loader);
            yield return ReturnHome(experience, home);

            loader.CompleteWithNull();
            yield return WaitForRevealFallback(home, 2f);

            AssertRevealFallbackRetainsHead(home, store);
            yield return RetireRevealFallbackAndExposeNextLevel(
                experience, home, store);
        }

        [UnityTest]
        public IEnumerator FailedRoomLoadOffersRevealFallbackWithoutConsumingQueue()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            IDemoProgressStore store = Store(experience);
            yield return WaitForRoom(home);
            yield return CompleteFirstLevel(experience);

            var loader = new ControlledRoomLoader();
            ReplaceRoomLoader(home, loader);
            yield return ReturnHome(experience, home);

            LogAssert.Expect(
                LogType.Warning,
                "The illustrated workshop room is unavailable; the " +
                "localized task card remains in use. Controlled room " +
                "load failure.");
            loader.Fail(new InvalidOperationException(
                "Controlled room load failure."));
            yield return WaitForRevealFallback(home, 2f);

            AssertRevealFallbackRetainsHead(home, store);
            yield return RetireRevealFallbackAndExposeNextLevel(
                experience, home, store);
        }

        [UnityTest]
        public IEnumerator TimedOutRoomLoadOffersRevealFallbackWithoutConsumingQueue()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            IDemoProgressStore store = Store(experience);
            yield return WaitForRoom(home);
            yield return CompleteFirstLevel(experience);

            var loader = new ControlledRoomLoader();
            ReplaceRoomLoader(home, loader);
            SetPrivateField(
                home,
                "_roomLoadTimeout",
                TimeSpan.FromMilliseconds(50));
            yield return ReturnHome(experience, home);
            yield return WaitForRevealFallback(home, 2f);

            AssertRevealFallbackRetainsHead(home, store);
            Assert.That(
                loader.UnloadCallCount,
                Is.EqualTo(1),
                "Timeout must await the loader lifecycle boundary so a late " +
                "physical load cannot be adopted or cached.");
            Assert.That(loader.PhysicalLoadPending, Is.False);
            yield return RetireRevealFallbackAndExposeNextLevel(
                experience, home, store);
        }

        [UnityTest]
        public IEnumerator PersistFailedRevealCanBeRetriedInTheSameHomeSession()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            IDemoProgressStore persistedStore = Store(experience);
            var store = new PresentationRetirementStore(
                persistedStore,
                PresentationRetirementBehavior.FailOnce);
            SetPrivateField(home, "_store", store);
            yield return WaitForRoom(home);

            yield return CompleteFirstLevel(experience);
            yield return ReturnHome(experience, home);
            yield return WaitForRevealEnd(home, 14f);
            yield return WaitForRevealFallback(home, 2f);

            AssertRevealFallbackRetainsHead(home, persistedStore);
            yield return RetireRevealFallbackAndExposeNextLevel(
                experience, home, persistedStore);
        }

        [UnityTest]
        public IEnumerator InvalidRevealRetirementRetainsTheHeadAndRecoverySurface()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            IDemoProgressStore persistedStore = Store(experience);
            var store = new PresentationRetirementStore(
                persistedStore,
                PresentationRetirementBehavior.AlwaysInvalid);
            SetPrivateField(home, "_store", store);
            yield return WaitForRoom(home);

            yield return CompleteFirstLevel(experience);
            yield return ReturnHome(experience, home);
            yield return WaitForRevealEnd(home, 14f);
            yield return WaitForRevealFallback(home, 2f);

            AssertRevealFallbackRetainsHead(home, persistedStore);
            Assert.That(_invalidRevealDiagnosticCount, Is.EqualTo(1));

            home.View.BottomSheet.ActionButton.onClick.Invoke();
            yield return null;
            yield return null;

            AssertRevealFallbackRetainsHead(home, persistedStore);
            Assert.That(
                _invalidRevealDiagnosticCount,
                Is.EqualTo(1),
                "Repeated recovery attempts must not spam diagnostics.");
        }

        [UnityTest]
        public IEnumerator MigratedExplicitFinaleSurvivesAutomaticHomePreparation()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            IWorkshopFlowCoordinator flow = Flow(experience);
            DemoProgressSnapshot migrated = LegacyProfileV1Migration.Migrate(
                new SecureProfileV1Dto
                {
                    version = 1,
                    highestUnlockedLevelIndex = 7,
                    completedLevelMask = 0xFF,
                    selectedThemeId = "sage",
                    musicEnabled = true,
                    ownedDecorationMask = 1,
                    selectedDecorationIndex = 0
                },
                WorkshopContentIds.CozyWorkshopBeatCount,
                "sage");
            var store = new InMemoryProgressStore(
                migrated,
                WorkshopContentIds.CozyWorkshopBeatCount);
            SetPrivateField(home, "_store", store);
            SetPrivateField(flow, "_store", store);

            home.NotifyHidden();
            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ColdStart,
                default));

            Assert.That(store.Current.PendingPresentationCount, Is.EqualTo(1));
            Assert.That(
                store.Current.TryGetPendingPresentation(
                    0, out PendingPresentationEntry finale),
                Is.True);
            Assert.That(finale.Kind, Is.EqualTo(PendingPresentationKind.Finale));
            Assert.That(finale.RequiresExplicitLaunch, Is.True);
            Assert.That(
                flow.GetRecommendedAction()?.Kind,
                Is.EqualTo(
                    WorkshopRecommendedActionKind.OpenCompletedWorkshop));
        }

        [UnityTest]
        public IEnumerator RevealFallbackCannotBeDismissedOrReplacedBySettings()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            IDemoProgressStore store = Store(experience);
            yield return WaitForRoom(home);
            yield return CompleteFirstLevel(experience);

            var loader = new ControlledRoomLoader();
            ReplaceRoomLoader(home, loader);
            yield return ReturnHome(experience, home);
            loader.CompleteWithNull();
            yield return WaitForRevealFallback(home, 2f);

            AssertRevealFallbackRetainsHead(home, store);
            string title = RecoveryTitle(home);
            home.View.SettingsButton.onClick.Invoke();
            yield return null;

            AssertRevealFallbackRetainsHead(home, store);
            AssertRecoveryModeWasNotReplaced(home, title);
            AssertRecoveryCloseUnavailable(home);
        }

        [UnityTest]
        public IEnumerator MemoryCardCannotBeDismissedOrReplacedBySettings()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            yield return WaitForRoom(home);

            DemoProgressSnapshot snapshot = CreatePendingSnapshot(
                0b11,
                PendingPresentationEntry.Memory(
                    WorkshopContentIds.SummerTrailStonesMemoryId));
            var store = new InMemoryProgressStore(
                snapshot,
                WorkshopContentIds.CozyWorkshopBeatCount);
            ReplaceProgressStore(experience, home, store);
            home.NotifyHidden();
            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));
            yield return Await(home.NotifyVisibleAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));
            yield return null;

            Assert.That(
                MemoryCardId(home),
                Is.EqualTo(WorkshopContentIds.SummerTrailStonesMemoryId));
            Assert.That(home.View.BottomSheet.IsOpen, Is.True);
            string title = RecoveryTitle(home);
            home.View.SettingsButton.onClick.Invoke();
            yield return null;

            Assert.That(
                MemoryCardId(home),
                Is.EqualTo(WorkshopContentIds.SummerTrailStonesMemoryId));
            Assert.That(store.Current.PendingPresentationCount, Is.EqualTo(1));
            AssertRecoveryModeWasNotReplaced(home, title);
            AssertRecoveryCloseUnavailable(home);
        }

        [UnityTest]
        public IEnumerator LoadFailureCannotBeDismissedOrReplacedBySettings()
        {
            yield return LoadMain();
            WorkshopHomeController home = FindHome();
            var retryCount = 0;
            ((IWorkshopHomeRecovery)home).ShowLoadFailure(
                default,
                _ => retryCount++);
            yield return null;

            Assert.That(
                GetPrivateField<bool>(home, "_hasLoadFailure"),
                Is.True);
            string title = RecoveryTitle(home);
            home.View.SettingsButton.onClick.Invoke();
            yield return null;

            Assert.That(
                GetPrivateField<bool>(home, "_hasLoadFailure"),
                Is.True);
            Assert.That(retryCount, Is.Zero);
            AssertRecoveryModeWasNotReplaced(home, title);
            AssertRecoveryCloseUnavailable(home);
        }

        [UnityTest]
        public IEnumerator PrepareEntryKeepsImmediateNullRoomFallbackVisible()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            yield return WaitForRoom(home);

            var store = new InMemoryProgressStore(
                CreatePendingSnapshot(
                    0b1,
                    PendingPresentationEntry.RoomReveal(
                        FirstRevealBeatId)),
                WorkshopContentIds.CozyWorkshopBeatCount);
            ReplaceProgressStore(experience, home, store);
            var loader = new ControlledRoomLoader();
            loader.CompleteWithNull();
            ReplaceRoomLoader(home, loader);

            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));

            AssertRevealFallbackRetainsHead(home, store);
            AssertRecoveryCloseUnavailable(home);
        }

        [UnityTest]
        public IEnumerator PrepareEntryKeepsImmediateThrowRoomFallbackVisible()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            yield return WaitForRoom(home);

            var store = new InMemoryProgressStore(
                CreatePendingSnapshot(
                    0b1,
                    PendingPresentationEntry.RoomReveal(
                        FirstRevealBeatId)),
                WorkshopContentIds.CozyWorkshopBeatCount);
            ReplaceProgressStore(experience, home, store);
            var loader = new ControlledRoomLoader();
            loader.Fail(new InvalidOperationException(
                "Immediate room failure."));
            ReplaceRoomLoader(home, loader);

            LogAssert.Expect(
                LogType.Warning,
                "The illustrated workshop room is unavailable; the " +
                "localized task card remains in use. Immediate room " +
                "failure.");
            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));

            AssertRevealFallbackRetainsHead(home, store);
            AssertRecoveryCloseUnavailable(home);
        }

        [UnityTest]
        public IEnumerator SettingsSheetRetainsDismissibleCloseButton()
        {
            yield return LoadMain();
            WorkshopHomeController home = FindHome();

            home.View.SettingsButton.onClick.Invoke();
            yield return null;

            Button close = CloseButton(home);
            Assert.That(home.View.BottomSheet.IsOpen, Is.True);
            Assert.That(close.gameObject.activeSelf, Is.True);
            Assert.That(close.interactable, Is.True);

            close.onClick.Invoke();
            yield return null;
            Assert.That(home.View.BottomSheet.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator PersistFailedFinaleRetirementOffersRetryWithoutRepeatingAutomatically()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            var audio = new RecordingAudio();
            SetPrivateField(home, "_audioService", audio);
            yield return WaitForRoom(home);

            PendingPresentationEntry finale =
                PendingPresentationEntry.Finale(
                    WorkshopContentIds.CozyWorkshopChapterId);
            DemoProgressSnapshot snapshot = CreatePendingSnapshot(
                0xFF,
                finale,
                PendingPresentationEntry.Memory(UnavailableMemoryId),
                PendingPresentationEntry.RoomReveal(FirstRevealBeatId));
            var store = new InMemoryProgressStore(
                snapshot,
                WorkshopContentIds.CozyWorkshopBeatCount,
                ProfileMutationStatus.PersistFailed);
            ReplaceProgressStore(experience, home, store);
            home.NotifyHidden();

            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));

            AssertQueueRecoveryRetainsHead(home, store, finale);
            Assert.That(store.RetirementAttemptCount, Is.EqualTo(1));

            string recoveryTitle = RecoveryTitle(home);
            home.View.SettingsButton.onClick.Invoke();
            yield return null;
            AssertRecoveryModeWasNotReplaced(home, recoveryTitle);
            Assert.That(store.RetirementAttemptCount, Is.EqualTo(1));

            IDemoLocalizationService localization =
                GetPrivateField<IDemoLocalizationService>(
                    home,
                    "_localization");
            IWorkshopTextService text =
                GetPrivateField<IWorkshopTextService>(home, "_text");
            DemoLocale originalLocale = localization.CurrentLocale;
            DemoLocale refreshedLocale = originalLocale == DemoLocale.Ukrainian
                ? DemoLocale.Russian
                : DemoLocale.Ukrainian;
            Assert.That(localization.SelectLocale(refreshedLocale), Is.True);
            yield return null;
            Assert.That(
                RecoveryTitle(home),
                Is.EqualTo(text.Get("load.failure.title")));
            Assert.That(
                RecoveryActionLabel(home),
                Is.EqualTo(text.Get("load.failure.retry")),
                "Locale refresh must update the active retry in place.");
            Assert.That(store.RetirementAttemptCount, Is.EqualTo(1));
            Assert.That(localization.SelectLocale(originalLocale), Is.True);
            yield return null;

            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));
            AssertQueueRecoveryRetainsHead(home, store, finale);
            Assert.That(
                store.RetirementAttemptCount,
                Is.EqualTo(1),
                "Preparation must not repeat a mutation awaiting player retry.");

            home.View.BottomSheet.ActionButton.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(store.RetirementAttemptCount, Is.EqualTo(2));
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.EqualTo(1));
            AssertFreshHead(
                store,
                PendingPresentationEntry.Memory(UnavailableMemoryId));
            AssertQueueRecoveryRetainsHead(
                home,
                store,
                PendingPresentationEntry.Memory(UnavailableMemoryId));

            home.View.BottomSheet.ActionButton.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(store.RetirementAttemptCount, Is.EqualTo(3));
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.EqualTo(2));
            AssertFreshHead(
                store,
                PendingPresentationEntry.RoomReveal(FirstRevealBeatId));
        }

        [UnityTest]
        public IEnumerator PersistFailedUnavailableMemoryRetirementOffersRetry()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            var audio = new RecordingAudio();
            SetPrivateField(home, "_audioService", audio);
            yield return WaitForRoom(home);

            PendingPresentationEntry memory =
                PendingPresentationEntry.Memory(UnavailableMemoryId);
            DemoProgressSnapshot snapshot = CreatePendingSnapshot(
                0b1,
                memory,
                PendingPresentationEntry.RoomReveal(FirstRevealBeatId));
            var store = new InMemoryProgressStore(
                snapshot,
                WorkshopContentIds.CozyWorkshopBeatCount,
                ProfileMutationStatus.PersistFailed);
            ReplaceProgressStore(experience, home, store);
            home.NotifyHidden();

            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));

            AssertQueueRecoveryRetainsHead(home, store, memory);
            Assert.That(store.RetirementAttemptCount, Is.EqualTo(1));

            home.View.BottomSheet.ActionButton.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(store.RetirementAttemptCount, Is.EqualTo(2));
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.EqualTo(1));
            AssertFreshHead(
                store,
                PendingPresentationEntry.RoomReveal(FirstRevealBeatId));
        }

        [UnityTest]
        public IEnumerator InvalidUnpresentableRetirementFailsClosedWithRetry()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            var audio = new RecordingAudio();
            SetPrivateField(home, "_audioService", audio);
            yield return WaitForRoom(home);

            PendingPresentationEntry finale =
                PendingPresentationEntry.Finale(
                    WorkshopContentIds.CozyWorkshopChapterId);
            var store = new InMemoryProgressStore(
                CreatePendingSnapshot(0xFF, finale),
                WorkshopContentIds.CozyWorkshopBeatCount,
                ProfileMutationStatus.Invalid,
                ProfileMutationStatus.Invalid,
                ProfileMutationStatus.Invalid);
            ReplaceProgressStore(experience, home, store);
            home.NotifyHidden();

            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));
            AssertQueueRecoveryRetainsHead(home, store, finale);
            Assert.That(store.RetirementAttemptCount, Is.EqualTo(1));

            home.NotifyHidden();
            AssertFreshHead(store, finale);
            Assert.That(home.View.BottomSheet.IsOpen, Is.False);
            Assert.That(
                GetPrivateField<bool>(home, "_hasQueueRecovery"),
                Is.False,
                "Hiding clears transient recovery state, not its queue head.");
            Assert.That(store.RetirementAttemptCount, Is.EqualTo(1));
            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));
            AssertQueueRecoveryRetainsHead(home, store, finale);
            Assert.That(store.RetirementAttemptCount, Is.EqualTo(2));

            home.View.BottomSheet.ActionButton.onClick.Invoke();
            yield return null;
            yield return null;

            AssertQueueRecoveryRetainsHead(home, store, finale);
            Assert.That(store.RetirementAttemptCount, Is.EqualTo(3));
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StaleQueueRecoveryCallbackIsSilent()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            var audio = new RecordingAudio();
            SetPrivateField(home, "_audioService", audio);
            yield return WaitForRoom(home);

            PendingPresentationEntry finale =
                PendingPresentationEntry.Finale(
                    WorkshopContentIds.CozyWorkshopChapterId);
            var store = new InMemoryProgressStore(
                CreatePendingSnapshot(0xFF, finale),
                WorkshopContentIds.CozyWorkshopBeatCount,
                ProfileMutationStatus.PersistFailed);
            ReplaceProgressStore(experience, home, store);
            home.NotifyHidden();
            yield return Await(home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ReturnFromLevel,
                default));
            AssertQueueRecoveryRetainsHead(home, store, finale);

            PendingPresentationEntry fresh =
                PendingPresentationEntry.RoomReveal(FirstRevealBeatId);
            store.ReplaceCurrent(CreatePendingSnapshot(0b1, fresh));
            home.View.BottomSheet.ActionButton.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(store.RetirementAttemptCount, Is.EqualTo(1));
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.Zero);
            AssertFreshHead(store, fresh);
        }

        [UnityTest]
        public IEnumerator ReplayingACompletedLevelQueuesNothingNew()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            IDemoProgressStore store = Store(experience);
            yield return WaitForRoom(home);

            yield return CompleteFirstLevel(experience);
            yield return ReturnHome(experience, home);
            yield return WaitForRevealEnd(home, 14f);
            Assert.That(PendingRevealCount(store), Is.Zero);

            int tokensAfterFirst = store.Current.CozyTokens;

            yield return CompleteFirstLevel(experience);

            Assert.That(
                PendingRevealCount(store),
                Is.Zero,
                "Replaying a completed level must not queue another reveal.");
            Assert.That(
                store.Current.CozyTokens,
                Is.EqualTo(tokensAfterFirst),
                "Replaying a completed level must not pay out again.");
        }

        /// <summary>
        /// Every restored beat has an authored, localized result line. It is
        /// the only story the shipped demo tells, so the completion screen
        /// must show it rather than the generic progress sentence.
        /// </summary>
        [UnityTest]
        public IEnumerator CompletionShowsTheAuthoredBeatLine()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            yield return WaitForRoom(home);

            yield return CompleteLevel(experience, 0);

            var body = GetPrivateField<UnityEngine.UI.Text>(
                experience, "_completionBodyText");
            Assert.That(body, Is.Not.Null);

            var text = GetPrivateField<IWorkshopTextService>(
                experience, "_workshopText");
            WorkshopContentIds.TryGetCozyWorkshopBeat(
                0, out WorkshopBeatContract beat);
            string expected = text.Get(beat.ResultTextKey);

            Assert.That(
                expected,
                Is.Not.EqualTo(beat.ResultTextKey),
                "The beat result key must resolve to authored copy.");
            Assert.That(
                body.text,
                Is.EqualTo(expected),
                "The completion screen must show the authored beat line.");
        }

        /// <summary>
        /// The real Addressable room must become visibly more restored after
        /// each completed stage. This catches a projection, presenter, or
        /// hotspot regression that would leave the player home with a stale
        /// workshop despite the profile recording chapter progress.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryStageReturnsHomeWithCumulativeVisibleRoomRestoration()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            IDemoProgressStore store = Store(experience);
            yield return WaitForRoom(home);

            int stages = WorkshopContentIds.CozyWorkshopBeatCount;
            for (var levelIndex = 0; levelIndex < stages; levelIndex++)
            {
                yield return CompleteLevel(experience, levelIndex);

                bool isLast = levelIndex == stages - 1;
                if (isLast)
                {
                    Assert.That(
                        TryGetPendingPresentation(
                            store,
                            PendingPresentationKind.Finale,
                            out PendingPresentationEntry finale),
                        Is.True,
                        "Stage 8 must queue its finale before home prepares.");
                    Assert.That(
                        finale.RequiresExplicitLaunch,
                        Is.False,
                        "A live stage-8 finale must retain the automatic " +
                        "static-home policy.");
                }

                yield return ReturnHome(experience, home);
                yield return WaitForRevealEnd(home, 14f);

                if (levelIndex == 4 || levelIndex == 7)
                {
                    string expectedMemoryId = levelIndex == 4
                        ? WorkshopContentIds.FixEverythingMemoryId
                        : WorkshopContentIds.OpenWindowsMemoryId;
                    yield return AssertMemoryOpensAndClosesOnce(
                        home,
                        store,
                        expectedMemoryId,
                        levelIndex);
                }
                else
                {
                    yield return CloseMemoryCardIfOpen(home);
                }

                Assert.That(
                    PendingRevealCount(store),
                    Is.Zero,
                    "Stage " + levelIndex +
                    " left a room reveal stuck in the queue.");
                Assert.That(
                    store.Current.PendingPresentationCount,
                    Is.Zero,
                    "Stage " + levelIndex +
                    " left a presentation this build cannot show at the " +
                    "head of the queue.");

                AssertSettledHomeShowsCumulativeRestoration(
                    experience,
                    home,
                    levelIndex,
                    stages);

                WorkshopRecommendedAction? action =
                    Flow(experience).GetRecommendedAction();
                Assert.That(
                    action.HasValue,
                    Is.True,
                    "Stage " + levelIndex + " left the home with no action.");
                Assert.That(
                    action.Value.Kind,
                    Is.EqualTo(isLast
                        ? WorkshopRecommendedActionKind.OpenCompletedWorkshop
                        : WorkshopRecommendedActionKind.StartLevel),
                    "Unexpected home action after stage " + levelIndex + ".");
                Assert.That(
                    home.View.PrimaryButton.gameObject.activeSelf,
                    Is.EqualTo(!isLast),
                    "Start availability is wrong after stage " + levelIndex +
                    ".");
            }

            IWorkshopProgressProjector projector =
                GetPrivateField<IWorkshopProgressProjector>(
                    home,
                    "_projector");
            Assert.That(
                projector.TryProject(
                    store.Current,
                    out WorkshopProgressProjection finalProjection),
                Is.True,
                "The complete profile must still project a workshop room.");
            Assert.That(finalProjection.CompletedBeatCount, Is.EqualTo(8));
            Assert.That(finalProjection.RestoredZoneMask, Is.EqualTo(0xFF));
            Assert.That(finalProjection.IsComplete, Is.True);
            Assert.That(finalProjection.ActiveHotspotBeatId, Is.Empty);
            Assert.That(
                store.Current.CompletedLevelMask & 0xFF,
                Is.EqualTo(0xFF),
                "All eight stages must be recorded complete.");
            Assert.That(store.Current.PendingPresentationCount, Is.Zero);
            Assert.That(
                GetPrivateField<int>(experience, "_currentLevelIndex"),
                Is.LessThan(0),
                "The chapter finale must not auto-start another level.");
        }

        /// <summary>
        /// Stage 2 uncovers the trail-stones memory. It must be shown, must
        /// survive leaving the workshop without closing it, and must retire
        /// only when the player closes the card.
        /// </summary>
        [UnityTest]
        public IEnumerator RestoredZoneShowsItsFamilyMemory()
        {
            yield return LoadMain();
            DemoExperienceController experience = FindExperience();
            WorkshopHomeController home = FindHome();
            var audio = new RecordingAudio();
            SetPrivateField(home, "_audioService", audio);
            IDemoProgressStore store = Store(experience);
            yield return WaitForRoom(home);

            yield return CompleteLevel(experience, 0);
            yield return ReturnHome(experience, home);
            yield return WaitForRevealEnd(home, 14f);
            yield return CloseMemoryCardIfOpen(home);

            // Stage 2 is the first beat that carries a memory.
            yield return CompleteLevel(experience, 1);
            yield return ReturnHome(experience, home);
            yield return WaitForRevealEnd(home, 14f);

            Assert.That(
                MemoryCardId(home),
                Is.EqualTo(WorkshopContentIds.SummerTrailStonesMemoryId),
                "Restoring the pebble shelf must show its family memory.");
            Assert.That(
                home.View.BottomSheet.IsOpen,
                Is.True,
                "The memory card must be on screen.");
            Assert.That(
                home.View.PrimaryButton.gameObject.activeSelf,
                Is.False,
                "The primary action must be suppressed behind the card.");

            var text = GetPrivateField<IWorkshopTextService>(
                experience, "_workshopText");
            WorkshopContentIds.TryGetMemoryTextKey(
                WorkshopContentIds.SummerTrailStonesMemoryId,
                out string key);
            Assert.That(
                text.Get(key),
                Is.Not.EqualTo(key),
                "The memory must resolve to authored copy.");

            // Leaving without closing keeps the memory pending.
            home.NotifyHidden();
            Assert.That(
                store.Current.HasViewedMemory(
                    WorkshopContentIds.SummerTrailStonesMemoryId),
                Is.False,
                "Leaving the workshop must not consume the memory.");

            yield return Await(home.NotifyVisibleAsync(
                WorkshopHomeEntryReason.ReturnFromLevel, default));
            yield return null;
            Assert.That(
                MemoryCardId(home),
                Is.EqualTo(WorkshopContentIds.SummerTrailStonesMemoryId),
                "The unclosed memory must be offered again.");

            yield return CloseMemoryCardIfOpen(home);

            Assert.That(
                store.Current.HasViewedMemory(
                    WorkshopContentIds.SummerTrailStonesMemoryId),
                Is.True,
                "Closing the card must retire the memory exactly once.");
            Assert.That(
                store.Current.PendingPresentationCount,
                Is.Zero,
                "The queue must be empty after the memory is closed.");
            Assert.That(
                home.View.PrimaryButton.gameObject.activeSelf,
                Is.True,
                "The next level must be offered again.");
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.EqualTo(1),
                "Closing an offered memory is one accepted primary action.");

            yield return CloseMemoryCardIfOpen(home);
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.EqualTo(1),
                "No memory card means no accepted action and no extra cue.");
        }

        private static string MemoryCardId(WorkshopHomeController home)
        {
            return GetPrivateField<string>(home, "_memoryCardId");
        }

        private static string RecoveryTitle(WorkshopHomeController home)
        {
            Text title = GetPrivateField<Text>(
                home.View.BottomSheet,
                "_recoveryTitleLabel");
            return title == null ? string.Empty : title.text;
        }

        private static Button CloseButton(WorkshopHomeController home)
        {
            return GetPrivateField<Button>(
                home.View.BottomSheet,
                "_closeButton");
        }

        private static string RecoveryActionLabel(
            WorkshopHomeController home)
        {
            Text action = GetPrivateField<Text>(
                home.View.BottomSheet,
                "_actionLabel");
            return action == null ? string.Empty : action.text;
        }

        private static void AssertRecoveryModeWasNotReplaced(
            WorkshopHomeController home,
            string expectedTitle)
        {
            WorkshopBottomSheet sheet = home.View.BottomSheet;
            GameObject settings = GetPrivateField<GameObject>(
                sheet,
                "_settingsContent");
            GameObject recovery = GetPrivateField<GameObject>(
                sheet,
                "_recoveryContent");
            Assert.That(sheet.IsOpen, Is.True);
            Assert.That(settings.activeSelf, Is.False);
            Assert.That(recovery.activeSelf, Is.True);
            Assert.That(RecoveryTitle(home), Is.EqualTo(expectedTitle));
        }

        private static void AssertRecoveryCloseUnavailable(
            WorkshopHomeController home)
        {
            Button close = CloseButton(home);
            Assert.That(
                close.gameObject.activeSelf,
                Is.False,
                "Recovery must not expose an X that can discard its action.");
            Assert.That(close.interactable, Is.False);
        }

        private static void AssertQueueRecoveryRetainsHead(
            WorkshopHomeController home,
            InMemoryProgressStore store,
            PendingPresentationEntry expectedHead)
        {
            AssertFreshHead(store, expectedHead);
            Assert.That(
                home.View.BottomSheet.IsOpen,
                Is.True,
                "A failed automatic retirement needs an explicit retry.");
            Assert.That(
                home.View.BottomSheet.ActionButton.gameObject.activeSelf,
                Is.True);
            Assert.That(
                home.View.BottomSheet.ActionButton.interactable,
                Is.True);
            Assert.That(
                home.View.PrimaryButton.gameObject.activeSelf,
                Is.False,
                "Primary progression stays suppressed behind the queue head.");
            AssertRecoveryCloseUnavailable(home);
        }

        private static void AssertFreshHead(
            IDemoProgressStore store,
            PendingPresentationEntry expectedHead)
        {
            Assert.That(
                store.Current.TryGetPendingPresentation(
                    0,
                    out PendingPresentationEntry head),
                Is.True);
            Assert.That(head, Is.EqualTo(expectedHead));
        }

        private static DemoProgressSnapshot CreatePendingSnapshot(
            int completedLevelMask,
            params PendingPresentationEntry[] pending)
        {
            return new DemoProgressSnapshot(
                highestUnlockedLevelIndex: 7,
                completedLevelMask: completedLevelMask,
                selectedThemeId: "sage",
                musicEnabled: true,
                cozyTokens: 0,
                rewardedLevelMask: completedLevelMask,
                ownedDecorationIds: Array.Empty<string>(),
                decorationSelections: Array.Empty<DecorationSelection>(),
                seenRoomRevealIds: Array.Empty<string>(),
                viewedMemoryIds: Array.Empty<string>(),
                seenFinaleIds: Array.Empty<string>(),
                pendingPresentations: pending,
                lastDailyCareUtcDayKey: 0,
                completedDailyCareCount: 0);
        }

        private static void ReplaceProgressStore(
            DemoExperienceController experience,
            WorkshopHomeController home,
            IDemoProgressStore store)
        {
            SetPrivateField(home, "_store", store);
            SetPrivateField(Flow(experience), "_store", store);
        }

        private void CountInvalidRevealDiagnostic(
            string condition,
            string stackTrace,
            LogType type)
        {
            if (type == LogType.Warning &&
                string.Equals(
                    condition,
                    InvalidRevealDiagnostic,
                    StringComparison.Ordinal))
            {
                _invalidRevealDiagnosticCount++;
            }
        }

        private static void ReplaceRoomLoader(
            WorkshopHomeController home,
            IWorkshopRoomLoader loader)
        {
            SetPrivateField(home, "_room", null);
            SetPrivateField(home, "_roomUnavailable", false);
            SetPrivateField(home, "_roomLoading", false);
            SetPrivateField(home, "_roomLoader", loader);
        }

        private static IEnumerator WaitForRevealFallback(
            WorkshopHomeController home,
            float seconds)
        {
            float timeout = Time.realtimeSinceStartup + seconds;
            while (string.IsNullOrEmpty(GetPrivateField<string>(
                       home, "_revealFallbackBeatId")) &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
        }

        private static void AssertRevealFallbackRetainsHead(
            WorkshopHomeController home,
            IDemoProgressStore store)
        {
            Assert.That(
                GetPrivateField<string>(home, "_revealFallbackBeatId"),
                Is.EqualTo(FirstRevealBeatId),
                "The pending reveal must have an immediate recovery action.");
            Assert.That(home.View.BottomSheet.IsOpen, Is.True);
            Assert.That(
                home.View.PrimaryButton.gameObject.activeSelf,
                Is.False,
                "Start must stay suppressed while the reveal is pending.");
            Assert.That(store.Current.PendingPresentationCount, Is.EqualTo(1));
            Assert.That(
                store.Current.TryGetPendingPresentation(
                    0, out PendingPresentationEntry head),
                Is.True);
            Assert.That(head.Kind, Is.EqualTo(PendingPresentationKind.RoomReveal));
            Assert.That(head.StableId, Is.EqualTo(FirstRevealBeatId));
        }

        private static IEnumerator RetireRevealFallbackAndExposeNextLevel(
            DemoExperienceController experience,
            WorkshopHomeController home,
            IDemoProgressStore store)
        {
            home.View.BottomSheet.ActionButton.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(
                store.Current.PendingPresentationCount,
                Is.Zero,
                "Recovery must retire exactly the persisted queue head.");
            Assert.That(
                Flow(experience).GetRecommendedAction()?.Kind,
                Is.EqualTo(WorkshopRecommendedActionKind.StartLevel));
            Assert.That(
                home.View.PrimaryButton.gameObject.activeSelf,
                Is.True,
                "The next level must be exposed after recovery succeeds.");
        }

        private static bool TryGetPendingPresentation(
            IDemoProgressStore store,
            PendingPresentationKind kind,
            out PendingPresentationEntry presentation)
        {
            DemoProgressSnapshot snapshot = store.Current;
            for (var index = 0;
                 index < snapshot.PendingPresentationCount;
                 index++)
            {
                if (snapshot.TryGetPendingPresentation(
                        index, out PendingPresentationEntry candidate) &&
                    candidate.Kind == kind)
                {
                    presentation = candidate;
                    return true;
                }
            }

            presentation = default;
            return false;
        }

        private static IEnumerator AssertMemoryOpensAndClosesOnce(
            WorkshopHomeController home,
            IDemoProgressStore store,
            string expectedMemoryId,
            int levelIndex)
        {
            Assert.That(
                MemoryCardId(home),
                Is.EqualTo(expectedMemoryId),
                "Stage " + levelIndex + " must open its family memory.");
            Assert.That(home.View.BottomSheet.IsOpen, Is.True);
            Assert.That(
                home.View.BottomSheet.ActionButton.gameObject.activeSelf,
                Is.True);
            Assert.That(home.View.BottomSheet.ActionButton.interactable, Is.True);
            Assert.That(store.Current.HasViewedMemory(expectedMemoryId), Is.False);

            home.View.BottomSheet.ActionButton.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(MemoryCardId(home), Is.Empty);
            Assert.That(
                store.Current.HasViewedMemory(expectedMemoryId),
                Is.True,
                "Closing stage " + levelIndex + " memory must retire it.");
            Assert.That(home.View.BottomSheet.IsOpen, Is.False);
        }

        private static void AssertSettledHomeShowsCumulativeRestoration(
            DemoExperienceController experience,
            WorkshopHomeController home,
            int completedStageIndex,
            int stageCount)
        {
            CanvasGroup homeScreen = GetPrivateField<CanvasGroup>(
                experience,
                "_homeScreen");
            Assert.That(
                GetPrivateField<CanvasGroup>(experience, "_activeScreen"),
                Is.SameAs(homeScreen),
                "Stage " + completedStageIndex + " did not return to Main.");
            Assert.That(homeScreen.gameObject.activeInHierarchy, Is.True);
            Assert.That(homeScreen.interactable, Is.True);
            Assert.That(
                GetPrivateField<LevelBase>(experience, "_boundLevel"),
                Is.Null,
                "No level may remain active after returning home.");
            Assert.That(experience.CurrentLevelIndex, Is.LessThan(0));

            WorkshopRoomPresenter room = Room(home);
            Assert.That(room.IsVisible, Is.True);
            Assert.That(room.BeatCount, Is.EqualTo(stageCount));

            var activeHotspotCount = 0;
            var interactableHotspotCount = 0;
            string activeHotspotBeatId = string.Empty;
            for (var beatIndex = 0; beatIndex < room.BeatCount; beatIndex++)
            {
                WorkshopRoomBeatBinding binding = room.GetBeat(beatIndex);
                Assert.That(binding, Is.Not.Null);
                Assert.That(binding.RestoredGroup, Is.Not.Null);
                Assert.That(binding.BeforeGroup, Is.Not.Null);
                Assert.That(binding.Hotspot, Is.Not.Null);
                Assert.That(
                    binding.RestoredGroup.alpha,
                    Is.EqualTo(
                        binding.ZoneIndex <= completedStageIndex ? 1f : 0f)
                        .Within(0.001f),
                    "Stage " + completedStageIndex + " restored layer " +
                    binding.ZoneIndex + " is wrong.");
                Assert.That(
                    binding.BeforeGroup.alpha,
                    Is.EqualTo(0f).Within(0.001f),
                    "Stage " + completedStageIndex + " left a dirty overlay " +
                    "visible for beat " + beatIndex + ".");

                if (binding.Hotspot.gameObject.activeInHierarchy)
                {
                    activeHotspotCount++;
                    activeHotspotBeatId = binding.BeatId;
                    if (binding.Hotspot.interactable)
                    {
                        interactableHotspotCount++;
                    }
                }
            }

            bool isFinalStage = completedStageIndex == stageCount - 1;
            Assert.That(activeHotspotCount, Is.EqualTo(isFinalStage ? 0 : 1));
            Assert.That(
                interactableHotspotCount,
                Is.EqualTo(isFinalStage ? 0 : 1),
                "Only the next restoration hotspot may accept input.");
            if (!isFinalStage)
            {
                Assert.That(
                    WorkshopContentIds.TryGetCozyWorkshopBeat(
                        completedStageIndex + 1,
                        out WorkshopBeatContract nextBeat),
                    Is.True);
                Assert.That(activeHotspotBeatId, Is.EqualTo(nextBeat.BeatId));
            }
        }

        private static IEnumerator CloseMemoryCardIfOpen(
            WorkshopHomeController home)
        {
            for (var guard = 0; guard < 4; guard++)
            {
                if (string.IsNullOrEmpty(MemoryCardId(home)))
                {
                    yield break;
                }

                home.View.BottomSheet.ActionButton.onClick.Invoke();
                yield return null;
                yield return null;
            }
        }

        private static IWorkshopFlowCoordinator Flow(
            DemoExperienceController experience)
        {
            return GetPrivateField<IWorkshopFlowCoordinator>(
                experience, "_workshopFlowCoordinator");
        }

        private static IEnumerator CompleteFirstLevel(
            DemoExperienceController experience)
        {
            yield return CompleteLevel(experience, 0);
        }

        private static IEnumerator CompleteLevel(
            DemoExperienceController experience,
            int levelIndex)
        {
            // Navigation may still be settling from the previous transition,
            // so ask until the level actually opens.
            var started = false;
            float startTimeout = Time.realtimeSinceStartup + 20f;
            while (!started && Time.realtimeSinceStartup < startTimeout)
            {
                UniTask<bool> play = experience.PlayLevelAsync(levelIndex);
                yield return Await(play);
                started = play.GetAwaiter().GetResult();
                if (!started)
                {
                    yield return null;
                }
            }

            Assert.That(
                started,
                Is.True,
                "Level " + levelIndex + " never opened.");

            LevelBase level = GetPrivateField<LevelBase>(
                experience, "_boundLevel");
            MethodInfo completed =
                typeof(DemoExperienceController).GetMethod(
                    "HandleLevelCompleted",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(completed, Is.Not.Null);
            completed.Invoke(experience, new object[] { level });

            float timeout = Time.realtimeSinceStartup + 8f;
            while (experience.LastCompletionStatus ==
                       ProfileMutationStatus.Invalid &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
        }

        private static IEnumerator ReturnHome(
            DemoExperienceController experience,
            WorkshopHomeController home)
        {
            float timeout = Time.realtimeSinceStartup + 25f;
            while (!GetPrivateField<bool>(home, "_visible") &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return Await(experience.ReturnToWorkshopAsync(
                    WorkshopHomeEntryReason.ReturnFromLevel));
                yield return null;
            }

            Assert.That(
                GetPrivateField<bool>(home, "_visible"),
                Is.True,
                "The workshop never became visible again.");
        }

        private static int PendingRevealCount(IDemoProgressStore store)
        {
            DemoProgressSnapshot snapshot = store.Current;
            var count = 0;
            for (var index = 0;
                 index < snapshot.PendingPresentationCount;
                 index++)
            {
                if (snapshot.TryGetPendingPresentation(
                        index, out PendingPresentationEntry pending) &&
                    pending.Kind == PendingPresentationKind.RoomReveal)
                {
                    count++;
                }
            }

            return count;
        }

        private static WorkshopRoomPresenter Room(WorkshopHomeController home)
        {
            var room = GetPrivateField<WorkshopRoomPresenter>(home, "_room");
            Assert.That(
                room,
                Is.Not.Null,
                "The illustrated room never loaded in this test run.");
            return room;
        }

        private static IEnumerator WaitForRoom(WorkshopHomeController home)
        {
            float timeout = Time.realtimeSinceStartup + 15f;
            while (GetPrivateField<WorkshopRoomPresenter>(home, "_room") ==
                       null &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(
                GetPrivateField<WorkshopRoomPresenter>(home, "_room"),
                Is.Not.Null,
                "The Addressable room prefab did not load.");
        }

        private static IEnumerator WaitForRevealStart(
            WorkshopHomeController home)
        {
            float timeout = Time.realtimeSinceStartup + 8f;
            while (!GetPrivateField<bool>(home, "_revealPlaying") &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(
                GetPrivateField<bool>(home, "_revealPlaying"),
                Is.True,
                "The reveal never started.");
        }

        private static IEnumerator WaitForRevealEnd(
            WorkshopHomeController home,
            float seconds,
            bool requireRevealStart = true)
        {
            if (requireRevealStart)
            {
                yield return WaitForRevealStart(home);
            }

            float timeout = Time.realtimeSinceStartup + seconds;
            while (GetPrivateField<bool>(home, "_revealPlaying") &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(
                GetPrivateField<bool>(home, "_revealPlaying"),
                Is.False,
                "The reveal never settled.");

            // Let the completion continuation retire the presentation.
            for (var frame = 0; frame < 5; frame++)
            {
                yield return null;
            }
        }

        private static DemoExperienceController FindExperience()
        {
            var experience =
                Object.FindFirstObjectByType<DemoExperienceController>(
                    FindObjectsInactive.Include);
            Assert.That(experience, Is.Not.Null);
            return experience;
        }

        private static WorkshopHomeController FindHome()
        {
            var home = Object.FindFirstObjectByType<WorkshopHomeController>(
                FindObjectsInactive.Include);
            Assert.That(home, Is.Not.Null);
            return home;
        }

        private static IDemoProgressStore Store(
            DemoExperienceController experience)
        {
            return GetPrivateField<IDemoProgressStore>(
                experience, "_progressStore");
        }

        private static T GetPrivateField<T>(object target, string name)
        {
            FieldInfo field = null;
            for (Type type = target.GetType();
                 type != null && field == null;
                 type = type.BaseType)
            {
                field = type.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
            }

            Assert.That(field, Is.Not.Null, "Missing field " + name);
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(
            object target,
            string name,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing field " + name);
            field.SetValue(target, value);
        }

        private enum PresentationRetirementBehavior
        {
            FailOnce,
            AlwaysInvalid
        }

        private sealed class PresentationRetirementStore : IDemoProgressStore
        {
            private readonly IDemoProgressStore _inner;
            private readonly PresentationRetirementBehavior _behavior;
            private bool _hasFailed;

            public PresentationRetirementStore(
                IDemoProgressStore inner,
                PresentationRetirementBehavior behavior)
            {
                _inner = inner;
                _behavior = behavior;
            }

            public event Action<DemoProgressSnapshot> ProgressChanged
            {
                add => _inner.ProgressChanged += value;
                remove => _inner.ProgressChanged -= value;
            }

            public bool IsInitialized => _inner.IsInitialized;
            public int LevelCount => _inner.LevelCount;
            public DemoProgressSnapshot Current => _inner.Current;

            public ProfileInitializationResult Initialize(
                int levelCount,
                string defaultThemeId,
                int completionReward) =>
                _inner.Initialize(
                    levelCount, defaultThemeId, completionReward);

            public ProfileMutationResult<LevelCompletionMutation> CompleteLevel(
                CompleteLevelCommand command) =>
                _inner.CompleteLevel(command);

            public ProfileMutationResult<PresentationMutation>
                MarkPresentationSeen(PendingPresentationEntry presentation)
            {
                if (_behavior ==
                    PresentationRetirementBehavior.AlwaysInvalid)
                {
                    return new ProfileMutationResult<PresentationMutation>(
                        ProfileMutationStatus.Invalid,
                        _inner.Current,
                        default);
                }

                if (!_hasFailed)
                {
                    _hasFailed = true;
                    return new ProfileMutationResult<PresentationMutation>(
                        ProfileMutationStatus.PersistFailed,
                        _inner.Current,
                        default);
                }

                return _inner.MarkPresentationSeen(presentation);
            }

            public ProfileMutationResult<MemoryMutation> MarkMemoryViewed(
                string memoryId) =>
                _inner.MarkMemoryViewed(memoryId);

            public ProfileMutationResult<DecorationMutation>
                PurchaseAndSelectDecoration(
                    string slotId,
                    string decorationId,
                    int cost) =>
                _inner.PurchaseAndSelectDecoration(
                    slotId, decorationId, cost);

            public ProfileMutationResult<DecorationMutation>
                GrantAndSelectDecoration(
                    string slotId,
                    string decorationId,
                    DecorationGrantSource source) =>
                _inner.GrantAndSelectDecoration(
                    slotId, decorationId, source);

            public ProfileMutationResult<DecorationMutation> SelectDecoration(
                string slotId,
                string decorationId) =>
                _inner.SelectDecoration(slotId, decorationId);

            public ProfileMutationResult<DailyCareMutation> CompleteDailyCare(
                string careId,
                int utcDayKey,
                int rewardAmount,
                string unlockedMemoryId) =>
                _inner.CompleteDailyCare(
                    careId,
                    utcDayKey,
                    rewardAmount,
                    unlockedMemoryId);

            public ProfileMutationResult<PreferenceMutation> SetSelectedTheme(
                string themeId) =>
                _inner.SetSelectedTheme(themeId);

            public ProfileMutationResult<PreferenceMutation> SetMusicEnabled(
                bool enabled) =>
                _inner.SetMusicEnabled(enabled);

            public bool IsLevelUnlocked(int levelIndex) =>
                _inner.IsLevelUnlocked(levelIndex);

            public bool IsLevelCompleted(int levelIndex) =>
                _inner.IsLevelCompleted(levelIndex);

            public void MarkLevelCompleted(int levelIndex) =>
                _inner.MarkLevelCompleted(levelIndex);

            public int CompleteLevelAndReward(
                int levelIndex,
                int rewardAmount) =>
                _inner.CompleteLevelAndReward(levelIndex, rewardAmount);

            public bool TryPurchaseAndSelectDecoration(
                int decorationIndex,
                int cost) =>
                _inner.TryPurchaseAndSelectDecoration(decorationIndex, cost);

            public bool TrySelectDecoration(int decorationIndex) =>
                _inner.TrySelectDecoration(decorationIndex);
        }

        private sealed class InMemoryProgressStore : IDemoProgressStore
        {
            private readonly ProfileMutationStatus[] _retirementStatuses;
            private int _retirementStatusIndex;

            public InMemoryProgressStore(
                DemoProgressSnapshot snapshot,
                int levelCount,
                params ProfileMutationStatus[] retirementStatuses)
            {
                Current = snapshot;
                LevelCount = levelCount;
                _retirementStatuses =
                    retirementStatuses ??
                    Array.Empty<ProfileMutationStatus>();
            }

            public event Action<DemoProgressSnapshot> ProgressChanged;
            public bool IsInitialized => true;
            public int LevelCount { get; }
            public DemoProgressSnapshot Current { get; private set; }
            public int RetirementAttemptCount { get; private set; }

            public ProfileMutationResult<PresentationMutation>
                MarkPresentationSeen(PendingPresentationEntry presentation)
            {
                RetirementAttemptCount++;
                if (TryTakeRetirementStatus(out ProfileMutationStatus status))
                {
                    return new ProfileMutationResult<PresentationMutation>(
                        status,
                        Current,
                        default);
                }

                ProfileMutationResult<PresentationMutation> result =
                    DemoProgressRules.MarkPresentationSeen(
                        Current, presentation);
                if (result.IsSuccess)
                {
                    Current = result.Snapshot;
                    ProgressChanged?.Invoke(Current);
                }

                return result;
            }

            public ProfileMutationResult<MemoryMutation> MarkMemoryViewed(
                string memoryId)
            {
                RetirementAttemptCount++;
                if (TryTakeRetirementStatus(out ProfileMutationStatus status))
                {
                    return new ProfileMutationResult<MemoryMutation>(
                        status,
                        Current,
                        default);
                }

                ProfileMutationResult<MemoryMutation> result =
                    DemoProgressRules.MarkMemoryViewed(Current, memoryId);
                if (result.IsSuccess)
                {
                    Current = result.Snapshot;
                    ProgressChanged?.Invoke(Current);
                }

                return result;
            }

            public void ReplaceCurrent(DemoProgressSnapshot snapshot)
            {
                Current = snapshot;
            }

            private bool TryTakeRetirementStatus(
                out ProfileMutationStatus status)
            {
                if (_retirementStatusIndex >= _retirementStatuses.Length)
                {
                    status = default;
                    return false;
                }

                status = _retirementStatuses[_retirementStatusIndex++];
                return true;
            }

            public ProfileInitializationResult Initialize(
                int levelCount,
                string defaultThemeId,
                int completionReward) =>
                throw new NotSupportedException();

            public ProfileMutationResult<LevelCompletionMutation> CompleteLevel(
                CompleteLevelCommand command) =>
                throw new NotSupportedException();

            public ProfileMutationResult<DecorationMutation>
                PurchaseAndSelectDecoration(
                    string slotId,
                    string decorationId,
                    int cost) =>
                throw new NotSupportedException();

            public ProfileMutationResult<DecorationMutation>
                GrantAndSelectDecoration(
                    string slotId,
                    string decorationId,
                    DecorationGrantSource source) =>
                throw new NotSupportedException();

            public ProfileMutationResult<DecorationMutation> SelectDecoration(
                string slotId,
                string decorationId) =>
                throw new NotSupportedException();

            public ProfileMutationResult<DailyCareMutation> CompleteDailyCare(
                string careId,
                int utcDayKey,
                int rewardAmount,
                string unlockedMemoryId) =>
                throw new NotSupportedException();

            public ProfileMutationResult<PreferenceMutation> SetSelectedTheme(
                string themeId) =>
                throw new NotSupportedException();

            public ProfileMutationResult<PreferenceMutation> SetMusicEnabled(
                bool enabled) =>
                throw new NotSupportedException();

            public bool IsLevelUnlocked(int levelIndex) =>
                DemoProgressRules.IsLevelUnlocked(
                    Current, levelIndex, LevelCount);

            public bool IsLevelCompleted(int levelIndex) =>
                DemoProgressRules.IsLevelCompleted(
                    Current, levelIndex, LevelCount);

            public void MarkLevelCompleted(int levelIndex) =>
                throw new NotSupportedException();

            public int CompleteLevelAndReward(
                int levelIndex,
                int rewardAmount) =>
                throw new NotSupportedException();

            public bool TryPurchaseAndSelectDecoration(
                int decorationIndex,
                int cost) =>
                throw new NotSupportedException();

            public bool TrySelectDecoration(int decorationIndex) =>
                throw new NotSupportedException();
        }

        private sealed class ControlledRoomLoader : IWorkshopRoomLoader
        {
            private readonly UniTaskCompletionSource<WorkshopRoomPresenter>
                _completion =
                    new UniTaskCompletionSource<WorkshopRoomPresenter>();

            public bool IsLoaded => false;
            public WorkshopRoomPresenter Presenter => null;
            public int UnloadCallCount { get; private set; }
            public bool PhysicalLoadPending =>
                _completion.Task.Status == UniTaskStatus.Pending;

            public UniTask<WorkshopRoomPresenter> LoadAsync(
                string chapterId,
                Transform parent,
                CancellationToken cancellationToken)
            {
                // Caller cancellation is intentionally waiter-only. The home
                // must use UnloadAsync to stop the physical load lifecycle.
                return _completion.Task;
            }

            public void CompleteWithNull()
            {
                _completion.TrySetResult(null);
            }

            public void Fail(Exception exception)
            {
                _completion.TrySetException(exception);
            }

            public UniTask UnloadAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                UnloadCallCount++;
                _completion.TrySetCanceled(cancellationToken);
                return UniTask.CompletedTask;
            }

            public void Dispose()
            {
            }
        }

        private sealed class ControlledRevealFrameDriver
        {
            private readonly Queue<PendingRevealFrame> _pending =
                new Queue<PendingRevealFrame>();

            public int WaitCount { get; private set; }
            public int PendingCount => _pending.Count;

            public UniTask<float> WaitForNextFrameAsync(
                CancellationToken cancellationToken)
            {
                WaitCount++;
                var frame = new PendingRevealFrame(cancellationToken);
                _pending.Enqueue(frame);
                return frame.Completion.Task;
            }

            public void Advance(float delta)
            {
                PendingRevealFrame frame = TakePending();
                Assert.That(
                    frame.CancellationToken.IsCancellationRequested,
                    Is.False,
                    "A cancelled reveal frame must be released explicitly.");
                frame.Completion.TrySetResult(delta);
            }

            public void ReleaseCancelledFrame()
            {
                PendingRevealFrame frame = TakePending();
                Assert.That(
                    frame.CancellationToken.IsCancellationRequested,
                    Is.True,
                    "The controlled frame must observe cancellation first.");
                frame.Completion.TrySetCanceled(frame.CancellationToken);
            }

            public void ReleaseCancelledFrame(
                CancellationToken cancellationToken)
            {
                PendingRevealFrame frame = TakePending();
                Assert.That(
                    frame.CancellationToken.IsCancellationRequested,
                    Is.True,
                    "The controller frame must observe its own cancellation.");
                Assert.That(
                    cancellationToken,
                    Is.Not.EqualTo(frame.CancellationToken),
                    "The regression needs distinct cancellation provenance.");
                frame.Completion.TrySetCanceled(cancellationToken);
            }

            public void Fail(Exception exception)
            {
                PendingRevealFrame frame = TakePending();
                Assert.That(
                    frame.CancellationToken.IsCancellationRequested,
                    Is.False,
                    "A reveal fault must remain distinct from cancellation.");
                frame.Completion.TrySetException(exception);
            }

            private PendingRevealFrame TakePending()
            {
                Assert.That(
                    _pending.Count,
                    Is.GreaterThan(0),
                    "No controlled reveal frame is waiting.");
                return _pending.Dequeue();
            }

            private sealed class PendingRevealFrame
            {
                public PendingRevealFrame(
                    CancellationToken cancellationToken)
                {
                    CancellationToken = cancellationToken;
                }

                public CancellationToken CancellationToken { get; }
                public UniTaskCompletionSource<float> Completion { get; } =
                    new UniTaskCompletionSource<float>();
            }
        }

        private sealed class RecordingAudio : ICategorizedAsmrAudioService
        {
            private readonly System.Collections.Generic.List<AsmrAudioCue>
                _cues = new System.Collections.Generic.List<AsmrAudioCue>();

            public bool IsAvailable => true;

            public void PlaySnap(Vector3 worldPosition)
            {
                PlayCue(AsmrAudioCue.Placement, worldPosition);
            }

            public void PlayCue(AsmrAudioCue cue, Vector3 worldPosition)
            {
                _cues.Add(cue);
            }

            public int Count(AsmrAudioCue cue)
            {
                var count = 0;
                for (var index = 0; index < _cues.Count; index++)
                {
                    if (_cues[index] == cue)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private static IEnumerator LoadMain()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "Main", LoadSceneMode.Single);
            while (!load.isDone)
            {
                yield return null;
            }

            DemoExperienceController experience = null;
            float timeout = Time.realtimeSinceStartup + 10f;
            while ((experience == null || !experience.IsInitialized) &&
                   Time.realtimeSinceStartup < timeout)
            {
                experience =
                    Object.FindFirstObjectByType<DemoExperienceController>(
                        FindObjectsInactive.Include);
                yield return null;
            }

            Assert.That(experience, Is.Not.Null);
            Assert.That(experience.IsInitialized, Is.True);
        }

        private static IEnumerator Await(UniTask task)
        {
            float timeout = Time.realtimeSinceStartup + 15f;
            while (task.Status == UniTaskStatus.Pending &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
        }

        private static IEnumerator Await(UniTask<bool> task)
        {
            float timeout = Time.realtimeSinceStartup + 15f;
            while (task.Status == UniTaskStatus.Pending &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
        }
    }
}
