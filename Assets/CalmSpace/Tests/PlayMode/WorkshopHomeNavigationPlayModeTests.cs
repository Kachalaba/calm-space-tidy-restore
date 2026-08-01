using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CalmSpace.Analytics;
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

namespace CalmSpace.Tests.PlayMode
{
    public sealed class WorkshopHomeNavigationPlayModeTests
    {
        [SetUp]
        public void ClearProgress()
        {
            DeleteSecureDemoProgress();
            PlayerPrefs.SetInt(
                DemoLocalizationService.DefaultPlayerPrefsKey,
                (int)DemoLocale.English);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void RemoveProgress()
        {
            DeleteSecureDemoProgress();
            PlayerPrefs.DeleteKey(
                DemoLocalizationService.DefaultPlayerPrefsKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator PrimaryIntentEmitsOneStableWorkshopRequest()
        {
            yield return LoadMain();
            WorkshopHomeController home = FindHome();
            var requestCount = 0;
            LevelLaunchRequest captured = default;
            home.LevelLaunchRequested += request =>
            {
                requestCount++;
                captured = request;
            };

            home.View.PrimaryButton.onClick.Invoke();
            yield return null;

            Assert.That(requestCount, Is.EqualTo(1));
            Assert.That(captured.Source, Is.EqualTo(LevelLaunchSource.Workshop));
            Assert.That(captured.LevelId, Is.EqualTo("01-soft-blocks"));
            Assert.That(captured.LevelIndex, Is.Zero);
            Assert.That(captured.ChapterId, Is.EqualTo("cozy-workshop"));
            Assert.That(captured.BeatId, Is.EqualTo("cozy-workshop.clear-passage"));
        }

        [UnityTest]
        public IEnumerator HomeLifecycleAndNavigationListenersAreCardinal()
        {
            yield return LoadMain();
            WorkshopHomeController home = FindHome();
            var analytics = new RecordingAnalytics();
            SetPrivateField(home, "_analytics", analytics);
            var catalogRequests = 0;
            home.CatalogRequested += () => catalogRequests++;

            home.NotifyHidden();
            UniTask visible = home.NotifyVisibleAsync(
                WorkshopHomeEntryReason.ExplicitWorkshopView,
                default);
            yield return Await(visible);
            visible = home.NotifyVisibleAsync(
                WorkshopHomeEntryReason.ExplicitWorkshopView,
                default);
            yield return Await(visible);
            home.View.CatalogButton.onClick.Invoke();
            yield return null;

            Assert.That(
                analytics.Count(ProductEventKind.WorkshopViewed),
                Is.EqualTo(1));
            Assert.That(catalogRequests, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SettingsControlsStayCardinalAndOperateRealServices()
        {
            yield return LoadMain();
            WorkshopHomeController home = FindHome();
            UniTask initialized = home.InitializeAsync(default);
            yield return Await(initialized);
            initialized = home.InitializeAsync(default);
            yield return Await(initialized);
            IBackgroundMusicService music =
                GetPrivateField<IBackgroundMusicService>(home, "_music");
            IDemoLocalizationService localization =
                GetPrivateField<IDemoLocalizationService>(
                    home,
                    "_localization");
            bool musicBefore = music.IsEnabled;

            home.View.SettingsButton.onClick.Invoke();
            home.View.SettingsMusicButton.onClick.Invoke();
            home.View.SettingsLocaleButton.onClick.Invoke();
            home.View.SettingsHapticButton.onClick.Invoke();
            yield return null;

            Assert.That(home.View.BottomSheet.IsOpen, Is.True);
            Assert.That(music.IsEnabled, Is.EqualTo(!musicBefore),
                "One click must toggle music exactly once.");
            Assert.That(localization.CurrentLocale,
                Is.EqualTo(DemoLocale.Ukrainian),
                "One click must advance locale exactly once.");
            Assert.That(home.View.VisibleActionControlsHaveBindings, Is.True);
        }

        [UnityTest]
        public IEnumerator CatalogRequestUsesTypedCatalogSourceAndStableLookup()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            Assert.That(experience, Is.Not.Null);

            var request = new LevelLaunchRequest(
                "01-soft-blocks",
                0,
                LevelLaunchSource.Catalog,
                string.Empty,
                string.Empty);
            UniTask<bool> play = experience.PlayLevelAsync(request);
            yield return Await(play);

            Assert.That(play.GetAwaiter().GetResult(), Is.True);
            Assert.That(experience.CurrentLevelIndex, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CompletedWorkshopHasNoActiveLevelHotspot()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            IDemoProgressStore store = GetPrivateField<IDemoProgressStore>(
                experience,
                "_progressStore");
            for (var index = 0; index < 8; index++)
            {
                store.MarkLevelCompleted(index);
            }

            WorkshopHomeController home = FindHome();
            UniTask prepare = home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ExplicitWorkshopView,
                default);
            yield return Await(prepare);

            Assert.That(home.View.ActiveHotspotCount, Is.Zero);
            Assert.That(home.View.PrimaryButton.gameObject.activeSelf, Is.False);
            Assert.That(experience.CurrentLevelIndex, Is.EqualTo(-1));

            UniTask<bool> recommended =
                experience.PlayRecommendedLevelAsync();
            yield return Await(recommended);
            Assert.That(recommended.GetAwaiter().GetResult(), Is.False);
            Assert.That(experience.CurrentLevelIndex, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator CancelledLaunchRecoversOneInteractiveScreen()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            var cancelled = new System.Threading.CancellationToken(true);
            var request = new LevelLaunchRequest(
                "01-soft-blocks",
                0,
                LevelLaunchSource.Workshop,
                "cozy-workshop",
                "cozy-workshop.clear-passage");

            UniTask<bool> play = experience.PlayLevelAsync(request, cancelled);
            yield return Await(play);

            Assert.That(play.GetAwaiter().GetResult(), Is.False);
            CanvasGroup[] groups = Object.FindObjectsByType<CanvasGroup>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var interactive = 0;
            foreach (CanvasGroup group in groups)
            {
                if (group.gameObject.activeInHierarchy &&
                    group.interactable && group.blocksRaycasts)
                {
                    interactive++;
                }
            }

            Assert.That(interactive, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FailedLevelLoadReturnsHomeWithOneRetryAction()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            SetPrivateField(experience, "_levelFlow", new FailingLevelFlow());
            var request = new LevelLaunchRequest(
                "01-soft-blocks",
                0,
                LevelLaunchSource.Workshop,
                "cozy-workshop",
                "cozy-workshop.clear-passage");

            UniTask<bool> play = experience.PlayLevelAsync(request);
            yield return Await(play);

            WorkshopHomeController home = FindHome();
            Assert.That(play.GetAwaiter().GetResult(), Is.False);
            Assert.That(home.View.BottomSheet.IsOpen, Is.True);
            Assert.That(home.View.BottomSheet.ActionButton.gameObject.activeSelf,
                Is.True);
            Assert.That(home.View.VisibleActionControlsHaveBindings, Is.True);
            Assert.That(CountInteractiveScreens(), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PersistFailureRetriesTheSameCompletionCommand()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            yield return LoadFirstLevel(experience);
            var coordinator = new ScriptedWorkshopFlow(
                ProfileMutationStatus.PersistFailed,
                ProfileMutationStatus.Applied);
            var analytics = new RecordingAnalytics();
            SetPrivateField(experience, "_workshopFlowCoordinator", coordinator);
            SetPrivateField(experience, "_analytics", analytics);

            InvokeCompletion(experience);
            yield return WaitForCompletionStatus(
                experience,
                ProfileMutationStatus.PersistFailed);

            Button retry = GetPrivateField<Button>(
                experience,
                "_retrySaveButton");
            Button returnWithout = GetPrivateField<Button>(
                experience,
                "_returnWithoutSavingButton");
            Assert.That(retry.gameObject.activeSelf, Is.True);
            Assert.That(returnWithout.gameObject.activeSelf, Is.True);
            Assert.That(analytics.Count(ProductEventKind.LevelCompleted),
                Is.Zero);

            yield return WaitForButton(retry);

            retry.onClick.Invoke();
            yield return WaitForCompletionStatus(
                experience,
                ProfileMutationStatus.Applied);

            Assert.That(coordinator.CompleteCallCount, Is.EqualTo(2));
            Assert.That(coordinator.AllCommandsMatch, Is.True);
            Assert.That(analytics.Count(ProductEventKind.LevelCompleted),
                Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReturnWithoutSavingDoesNotRepeatOrMutateCompletion()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            yield return LoadFirstLevel(experience);
            var coordinator = new ScriptedWorkshopFlow(
                ProfileMutationStatus.PersistFailed);
            SetPrivateField(experience, "_workshopFlowCoordinator", coordinator);

            InvokeCompletion(experience);
            yield return WaitForCompletionStatus(
                experience,
                ProfileMutationStatus.PersistFailed);
            Button returnWithout = GetPrivateField<Button>(
                experience,
                "_returnWithoutSavingButton");
            yield return WaitForButton(returnWithout);
            returnWithout.onClick.Invoke();

            float timeout = Time.realtimeSinceStartup + 10f;
            while (experience.CurrentLevelIndex >= 0 &&
                Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(coordinator.CompleteCallCount, Is.EqualTo(1));
            Assert.That(experience.CurrentLevelIndex, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator AlreadyAppliedShowsReplayWithoutReward()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            yield return LoadFirstLevel(experience);
            var coordinator = new ScriptedWorkshopFlow(
                ProfileMutationStatus.AlreadyApplied);
            var analytics = new RecordingAnalytics();
            SetPrivateField(experience, "_workshopFlowCoordinator", coordinator);
            SetPrivateField(experience, "_analytics", analytics);

            InvokeCompletion(experience);
            yield return WaitForCompletionStatus(
                experience,
                ProfileMutationStatus.AlreadyApplied);

            Text reward = GetPrivateField<Text>(
                experience,
                "_completionRewardText");
            Assert.That(reward.gameObject.activeSelf, Is.False);
            Assert.That(analytics.Count(ProductEventKind.LevelCompleted),
                Is.EqualTo(1));
            Assert.That(analytics.Last.Flag, Is.False);
        }

        [UnityTest]
        public IEnumerator InvalidCompletionLogsOnceAndKeepsHudStable()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            yield return LoadFirstLevel(experience);
            var coordinator = new ScriptedWorkshopFlow(
                ProfileMutationStatus.Invalid,
                ProfileMutationStatus.Invalid);
            SetPrivateField(experience, "_workshopFlowCoordinator", coordinator);

            LogAssert.Expect(
                LogType.Error,
                "Calm Space ignored an invalid workshop completion.");
            InvokeCompletion(experience);
            InvokeCompletion(experience);
            yield return null;

            CanvasGroup hud = GetPrivateField<CanvasGroup>(
                experience,
                "_hudScreen");
            Assert.That(hud.gameObject.activeSelf, Is.True);
            Assert.That(hud.interactable, Is.True);
            Assert.That(coordinator.CompleteCallCount, Is.EqualTo(2));
        }

        private static WorkshopHomeController FindHome()
        {
            WorkshopHomeController home =
                Object.FindFirstObjectByType<WorkshopHomeController>(
                    FindObjectsInactive.Include);
            Assert.That(home, Is.Not.Null);
            Assert.That(home.View, Is.Not.Null);
            return home;
        }

        private static IEnumerator LoadFirstLevel(
            DemoExperienceController experience)
        {
            UniTask<bool> play = experience.PlayLevelAsync(0);
            yield return Await(play);
            Assert.That(play.GetAwaiter().GetResult(), Is.True);
        }

        private static void InvokeCompletion(
            DemoExperienceController experience)
        {
            LevelBase level = GetPrivateField<LevelBase>(
                experience,
                "_boundLevel");
            MethodInfo method = typeof(DemoExperienceController).GetMethod(
                "HandleLevelCompleted",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(experience, new object[] { level });
        }

        private static IEnumerator WaitForCompletionStatus(
            DemoExperienceController experience,
            ProfileMutationStatus expected)
        {
            float timeout = Time.realtimeSinceStartup + 5f;
            while (experience.LastCompletionStatus != expected &&
                Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(experience.LastCompletionStatus, Is.EqualTo(expected));
        }

        private static IEnumerator WaitForButton(Button button)
        {
            float timeout = Time.realtimeSinceStartup + 5f;
            while ((!button.gameObject.activeInHierarchy ||
                    !button.interactable) &&
                Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(button.gameObject.activeInHierarchy, Is.True);
            Assert.That(button.interactable, Is.True);
        }

        private static int CountInteractiveScreens()
        {
            GameObject ui = GameObject.Find("Demo UI");
            Assert.That(ui, Is.Not.Null);
            var count = 0;
            for (var index = 0; index < ui.transform.childCount; index++)
            {
                CanvasGroup group = ui.transform.GetChild(index)
                    .GetComponent<CanvasGroup>();
                if (group != null && group.gameObject.activeSelf &&
                    group.interactable && group.blocksRaycasts)
                {
                    count++;
                }
            }

            return count;
        }

        private static IEnumerator LoadMain()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "Main",
                LoadSceneMode.Single);
            while (!load.isDone)
            {
                yield return null;
            }

            DemoExperienceController experience = null;
            float timeout = Time.realtimeSinceStartup + 10f;
            while ((experience == null || !experience.IsInitialized) &&
                Time.realtimeSinceStartup < timeout)
            {
                experience = Object.FindFirstObjectByType<
                    DemoExperienceController>(FindObjectsInactive.Include);
                yield return null;
            }

            Assert.That(experience, Is.Not.Null);
            Assert.That(experience.IsInitialized, Is.True);
        }

        private static IEnumerator Await(UniTask task)
        {
            float timeout = Time.realtimeSinceStartup + 10f;
            while (task.Status == UniTaskStatus.Pending &&
                Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
        }

        private static IEnumerator Await(UniTask<bool> task)
        {
            float timeout = Time.realtimeSinceStartup + 10f;
            while (task.Status == UniTaskStatus.Pending &&
                Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
        }

        private static void DeleteSecureDemoProgress()
        {
            string path = Path.Combine(
                Application.persistentDataPath,
                "player-profile-v1.bin");
            Delete(path);
            Delete(path + ".tmp");
            Delete(path + ".bak");
            PlayerPrefs.DeleteKey(
                EncryptedFileDemoProgressStore.DefaultMigrationMarker);
        }

        private static void Delete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static T GetPrivateField<T>(object target, string fieldName)
            where T : class
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(target) as T;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class RecordingAnalytics : IProductAnalytics
        {
            private readonly List<ProductAnalyticsEvent> _events =
                new List<ProductAnalyticsEvent>();

            public ProductAnalyticsEvent Last =>
                _events.Count == 0 ? default : _events[_events.Count - 1];

            public void Track(in ProductAnalyticsEvent analyticsEvent)
            {
                _events.Add(analyticsEvent);
            }

            public int Count(ProductEventKind kind)
            {
                var count = 0;
                foreach (ProductAnalyticsEvent analyticsEvent in _events)
                {
                    if (analyticsEvent.Kind == kind)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private sealed class FailingLevelFlow : ILevelFlowController
        {
            public LevelBase CurrentLevel => null;
            public int CurrentLevelIndex => -1;
            public bool IsLoading => false;
            public UniTask<bool> LoadFirstLevelAsync(
                System.Threading.CancellationToken cancellationToken = default)
                => UniTask.FromResult(false);
            public UniTask<bool> LoadLevelAsync(
                string levelId,
                System.Threading.CancellationToken cancellationToken = default)
                => UniTask.FromResult(false);
            public UniTask<bool> LoadNextLevelAsync(
                System.Threading.CancellationToken cancellationToken = default)
                => UniTask.FromResult(false);
            public UniTask UnloadCurrentLevelAsync(
                System.Threading.CancellationToken cancellationToken = default)
                => UniTask.CompletedTask;
        }

        private sealed class ScriptedWorkshopFlow : IWorkshopFlowCoordinator
        {
            private readonly Queue<ProfileMutationStatus> _statuses;
            private string _levelId;
            private int _levelIndex;
            private int _reward;

            public ScriptedWorkshopFlow(params ProfileMutationStatus[] statuses)
            {
                _statuses = new Queue<ProfileMutationStatus>(statuses);
            }

            public int CompleteCallCount { get; private set; }
            public bool AllCommandsMatch { get; private set; } = true;
            public WorkshopRecommendedAction? GetRecommendedAction() => null;
            public WorkshopRecommendedAction? GetExplicitWorkshopAction() => null;
            public bool TryCreateLaunchRequest(
                WorkshopRecommendedAction action,
                out LevelLaunchRequest request)
            {
                request = default;
                return false;
            }

            public ProfileMutationResult<LevelCompletionMutation> CompleteLevel(
                string levelId,
                int levelIndex,
                int rewardAmount)
            {
                CompleteCallCount++;
                if (CompleteCallCount == 1)
                {
                    _levelId = levelId;
                    _levelIndex = levelIndex;
                    _reward = rewardAmount;
                }
                else
                {
                    AllCommandsMatch &= _levelId == levelId &&
                        _levelIndex == levelIndex && _reward == rewardAmount;
                }

                ProfileMutationStatus status = _statuses.Count == 0
                    ? ProfileMutationStatus.Invalid
                    : _statuses.Dequeue();
                bool first = status == ProfileMutationStatus.Applied;
                return new ProfileMutationResult<LevelCompletionMutation>(
                    status,
                    default,
                    new LevelCompletionMutation(
                        first,
                        first ? rewardAmount : 0,
                        first ? rewardAmount : 0));
            }
        }
    }
}
