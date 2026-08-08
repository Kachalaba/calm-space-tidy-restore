using System;
using System.Collections;
using System.Reflection;
using CalmSpace.Demo;
using CalmSpace.Levels;
using CalmSpace.UI;
using CalmSpace.Workshop;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
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
            IDemoProgressStore store = Store(experience);
            yield return WaitForRoom(home);

            yield return CompleteFirstLevel(experience);
            yield return ReturnHome(experience, home);
            yield return WaitForRevealStart(home);

            // Leaving the workshop mid-reveal persists nothing.
            home.NotifyHidden();
            yield return WaitForRevealEnd(home, 5f);

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
                Room(home).IsRevealPlaying,
                Is.True,
                "The next entry must replay the unfinished reveal.");

            yield return WaitForRevealEnd(home, 12f);
            Assert.That(PendingRevealCount(store), Is.Zero);
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
        /// Walks the whole chapter. Stages 2 and 5 also queue a Memory and
        /// stage 8 queues a Finale; this build ships neither the Album nor a
        /// finale sequence, so those entries must still leave the queue. A
        /// presentation stuck at the head blocks every later recommendation
        /// and would strand the player on a home screen with no next level.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryStageKeepsTheChapterPlayable()
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
                yield return ReturnHome(experience, home);
                yield return WaitForRevealEnd(home, 14f);

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

                bool isLast = levelIndex == stages - 1;
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

            Assert.That(
                store.Current.CompletedLevelMask & 0xFF,
                Is.EqualTo(0xFF),
                "All eight stages must be recorded complete.");
            Assert.That(
                GetPrivateField<int>(experience, "_currentLevelIndex"),
                Is.LessThan(0),
                "The chapter finale must not auto-start another level.");
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
        }

        private static IEnumerator WaitForRevealEnd(
            WorkshopHomeController home,
            float seconds)
        {
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
