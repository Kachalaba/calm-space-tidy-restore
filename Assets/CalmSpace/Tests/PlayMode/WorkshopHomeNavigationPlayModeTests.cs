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

        [Test]
        public void AwaitPropagatesAlreadyFaultedUniTasks()
        {
            var expected =
                new System.InvalidOperationException("await sentinel");
            UniTask task = UniTask.FromException(expected);
            System.Exception actual = CaptureAwaitFailure(Await(task));
            if (actual == null)
            {
                CaptureTaskFailure(task);
            }

            Assert.That(actual, Is.SameAs(expected));

            var genericExpected = new System.InvalidOperationException(
                "generic await sentinel");
            UniTask<bool> genericTask =
                UniTask.FromException<bool>(genericExpected);
            System.Exception genericActual =
                CaptureAwaitFailure(Await(genericTask));
            if (genericActual == null)
            {
                CaptureTaskFailure(genericTask);
            }

            Assert.That(genericActual, Is.SameAs(genericExpected));
        }

        [UnityTest]
        public IEnumerator PrimaryIntentEmitsOneStableWorkshopRequest()
        {
            yield return LoadMain();
            WorkshopHomeController home = FindHome();
            var audio = new RecordingAudio();
            SetPrivateField(home, "_audioService", audio);
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
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.EqualTo(1));

            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            IDemoProgressStore store = GetPrivateField<IDemoProgressStore>(
                experience,
                "_progressStore");
            for (var index = 0; index < 8; index++)
            {
                store.MarkLevelCompleted(index);
            }

            home.View.PrimaryButton.onClick.Invoke();
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.EqualTo(1),
                "An unavailable primary action must stay silent.");
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
        public IEnumerator MusicWriteFailureDoesNotPublishStateUiOrAnalytics()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            WorkshopHomeController home = FindHome();
            IDemoProgressStore persisted =
                GetPrivateField<IDemoProgressStore>(
                    experience,
                    "_progressStore");
            var store = new FailOnceMusicPreferenceStore(persisted);
            SetPrivateField(home, "_store", store);
            SetPrivateField(experience, "_progressStore", store);
            var analytics = new RecordingAnalytics();
            SetPrivateField(experience, "_analytics", analytics);
            IBackgroundMusicService music =
                GetPrivateField<IBackgroundMusicService>(home, "_music");

            home.View.SettingsButton.onClick.Invoke();
            yield return null;
            Text label = GetPrivateField<Text>(
                home.View,
                "_settingsMusicLabel");
            bool enabledBefore = music.IsEnabled;
            string labelBefore = label.text;
            DemoProgressSnapshot persistedBefore = persisted.Current;

            home.View.SettingsMusicButton.onClick.Invoke();
            yield return null;

            Assert.That(store.MusicWriteAttempts, Is.EqualTo(1));
            Assert.That(music.IsEnabled, Is.EqualTo(enabledBefore));
            Assert.That(label.text, Is.EqualTo(labelBefore));
            Assert.That(persisted.Current, Is.EqualTo(persistedBefore));
            Assert.That(
                analytics.Count(ProductEventKind.MusicChanged),
                Is.Zero);

            home.View.SettingsMusicButton.onClick.Invoke();
            yield return null;

            Assert.That(store.MusicWriteAttempts, Is.EqualTo(2));
            Assert.That(music.IsEnabled, Is.EqualTo(!enabledBefore));
            Assert.That(label.text, Is.Not.EqualTo(labelBefore));
            Assert.That(
                persisted.Current.MusicEnabled,
                Is.EqualTo(!enabledBefore));
            Assert.That(
                analytics.Count(ProductEventKind.MusicChanged),
                Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CatalogRequestUsesTypedCatalogSourceAndStableLookup()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            Assert.That(experience, Is.Not.Null);
            var analytics = new RecordingAnalytics();
            SetPrivateField(experience, "_analytics", analytics);
            WorkshopHomeController home = FindHome();
            home.View.CatalogButton.onClick.Invoke();
            CanvasGroup levelSelect = GetPrivateField<CanvasGroup>(
                experience,
                "_levelSelectScreen");
            yield return WaitForScreen(levelSelect);
            yield return WaitForNavigationIdle(experience);

            DemoExperienceController.LevelButtonBinding[] entries =
                GetPrivateField<
                    DemoExperienceController.LevelButtonBinding[]>(
                    experience,
                    "_levelButtons");
            Assert.That(entries, Is.Not.Empty);
            Assert.That(entries[0].Button.interactable, Is.True);
            entries[0].Button.onClick.Invoke();
            yield return WaitForCurrentLevel(experience, 0);

            Assert.That(experience.CurrentLevelIndex, Is.Zero);
            ProductAnalyticsEvent started =
                analytics.Find(ProductEventKind.LevelStarted);
            Assert.That(started.LevelId, Is.EqualTo("01-soft-blocks"));
            Assert.That(started.LaunchSource,
                Is.EqualTo(LevelLaunchSource.Catalog));
        }

        [UnityTest]
        public IEnumerator HomeTaskTitleTracksLiveLocaleAndCompleteCopy()
        {
            yield return LoadMain();
            WorkshopHomeController home = FindHome();
            Text taskTitle = GetPrivateField<Text>(home.View, "_taskTitle");
            IDemoLocalizationService localization =
                GetPrivateField<IDemoLocalizationService>(
                    home,
                    "_localization");

            Assert.That(taskTitle.text, Is.EqualTo("Clear the passage"));
            localization.SelectLocale(DemoLocale.Ukrainian);
            yield return null;
            Assert.That(taskTitle.text, Is.EqualTo("Звільнити прохід"));
            localization.SelectLocale(DemoLocale.Russian);
            yield return null;
            Assert.That(taskTitle.text, Is.EqualTo("Освободить проход"));

            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            IDemoProgressStore store = GetPrivateField<IDemoProgressStore>(
                experience,
                "_progressStore");
            for (var index = 0; index < 8; index++)
            {
                store.MarkLevelCompleted(index);
            }

            UniTask prepare = home.PrepareEntryAsync(
                WorkshopHomeEntryReason.ExplicitWorkshopView,
                default);
            yield return Await(prepare);
            Assert.That(taskTitle.text,
                Is.EqualTo("Мастерская готова встретить всех."));
            Assert.That(home.View.PrimaryButton.gameObject.activeSelf,
                Is.False);
            Assert.That(home.View.ActiveHotspotCount, Is.Zero);
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
            var recommendedResult = true;
            yield return Await(
                recommended,
                value => recommendedResult = value);
            Assert.That(recommendedResult, Is.False);
            Assert.That(experience.CurrentLevelIndex, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator AllCompleteWithInvalidMetadataCannotReplayLevelEight()
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

            LevelCatalog levels = GetPrivateField<LevelCatalog>(
                experience,
                "_levelCatalog");
            LivingWorkshopCatalog workshop =
                GetPrivateField<LivingWorkshopCatalog>(
                    experience,
                    "_livingWorkshopCatalog");
            WorkshopTextCatalog emptyText =
                ScriptableObject.CreateInstance<WorkshopTextCatalog>();
            try
            {
                var unavailable = new WorkshopRuntimeAvailability(
                    levels,
                    workshop,
                    emptyText);
                Assert.That(unavailable.HomeMetaAvailable, Is.False);
                SetAvailabilityIfSupported(experience, unavailable);
                SetPrivateField(
                    experience,
                    "_workshopFlowCoordinator",
                    new ScriptedWorkshopFlow());

                UniTask<bool> play =
                    experience.PlayRecommendedLevelAsync();
                var played = true;
                yield return Await(play, value => played = value);

                Assert.That(played, Is.False);
                Assert.That(experience.CurrentLevelIndex, Is.EqualTo(-1));
            }
            finally
            {
                Object.Destroy(emptyText);
            }
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
            var played = true;
            yield return Await(play, value => played = value);

            Assert.That(played, Is.False);
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
        public IEnumerator MidUnloadCancellationAfterReleaseRecoversCleanHome()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            yield return LoadFirstLevel(experience);
            InvokeCompletion(experience);
            yield return WaitForCompletionStatus(
                experience,
                ProfileMutationStatus.Applied);
            CanvasGroup completion = GetPrivateField<CanvasGroup>(
                experience,
                "_completionScreen");
            yield return WaitForScreen(completion);
            LevelBase level = GetPrivateField<LevelBase>(
                experience,
                "_boundLevel");
            var cancelling = new CancellingUnloadLevelFlow(
                level,
                0,
                releaseBeforeCancel: true);
            SetPrivateField(experience, "_levelFlow", cancelling);

            UniTask returning = experience.ReturnToWorkshopAsync(
                WorkshopHomeEntryReason.ReturnFromLevel);
            yield return Await(returning);

            Assert.That(cancelling.CurrentLevel, Is.Null);
            Assert.That(experience.CurrentLevelIndex, Is.EqualTo(-1));
            Assert.That(
                GetPrivateField<LevelBase>(experience, "_boundLevel"),
                Is.Null);
            CanvasGroup home = GetPrivateField<CanvasGroup>(
                experience,
                "_homeScreen");
            Assert.That(home.gameObject.activeSelf, Is.True);
            Assert.That(home.interactable, Is.True);
            Assert.That(completion.gameObject.activeSelf, Is.False);
            Assert.That(CountInteractiveScreens(), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MidUnloadCancellationWhileOwnedRestoresBoundHud()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            yield return LoadFirstLevel(experience);
            LevelBase level = GetPrivateField<LevelBase>(
                experience,
                "_boundLevel");
            var cancelling = new CancellingUnloadLevelFlow(
                level,
                0,
                releaseBeforeCancel: false);
            SetPrivateField(experience, "_levelFlow", cancelling);

            UniTask returning = experience.ReturnToWorkshopAsync(
                WorkshopHomeEntryReason.ReturnFromLevel);
            yield return Await(returning);

            Assert.That(cancelling.CurrentLevel, Is.SameAs(level));
            Assert.That(experience.CurrentLevelIndex, Is.Zero);
            Assert.That(
                GetPrivateField<LevelBase>(experience, "_boundLevel"),
                Is.SameAs(level));
            CanvasGroup hud = GetPrivateField<CanvasGroup>(
                experience,
                "_hudScreen");
            Assert.That(hud.gameObject.activeSelf, Is.True);
            Assert.That(hud.interactable, Is.True);
            Assert.That(CountInteractiveScreens(), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CancelledAndFailedHomeTransitionsDoNotRepeatWorkshopViewed()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            WorkshopHomeController home = FindHome();
            var analytics = new RecordingAnalytics();
            SetPrivateField(home, "_analytics", analytics);
            ILevelFlowController realFlow =
                GetPrivateField<ILevelFlowController>(
                    experience,
                    "_levelFlow");
            var request = new LevelLaunchRequest(
                "01-soft-blocks",
                0,
                LevelLaunchSource.Workshop,
                "cozy-workshop",
                "cozy-workshop.clear-passage");

            UniTask<bool> cancelled = experience.PlayLevelAsync(
                request,
                new System.Threading.CancellationToken(true));
            yield return Await(cancelled);
            Assert.That(
                analytics.Count(ProductEventKind.WorkshopViewed),
                Is.Zero,
                "A pre-cancelled launch never hid the workshop.");

            SetPrivateField(experience, "_levelFlow",
                new CountingFailingLevelFlow());
            UniTask<bool> failed = experience.PlayLevelAsync(request);
            yield return Await(failed);
            Assert.That(
                analytics.Count(ProductEventKind.WorkshopViewed),
                Is.Zero,
                "A failed load restored the same visible workshop.");

            SetPrivateField(experience, "_levelFlow", realFlow);
            MethodInfo showCatalog =
                typeof(DemoExperienceController).GetMethod(
                    "ShowLevelSelectAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showCatalog, Is.Not.Null);
            UniTask cancelledCatalog = (UniTask)showCatalog.Invoke(
                experience,
                new object[]
                {
                    new System.Threading.CancellationToken(true)
                });
            yield return Await(cancelledCatalog);
            Assert.That(
                analytics.Count(ProductEventKind.WorkshopViewed),
                Is.Zero,
                "A cancelled catalog fade never hid the workshop.");
        }

        [UnityTest]
        public IEnumerator SuccessfulCatalogRoundTripEmitsOneWorkshopViewed()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            WorkshopHomeController home = FindHome();
            var analytics = new RecordingAnalytics();
            SetPrivateField(home, "_analytics", analytics);

            home.View.CatalogButton.onClick.Invoke();
            CanvasGroup catalog = GetPrivateField<CanvasGroup>(
                experience,
                "_levelSelectScreen");
            yield return WaitForScreen(catalog);
            yield return WaitForNavigationIdle(experience);
            Button back = GetPrivateField<Button>(
                experience,
                "_levelSelectBackButton");
            back.onClick.Invoke();
            CanvasGroup homeScreen = GetPrivateField<CanvasGroup>(
                experience,
                "_homeScreen");
            yield return WaitForScreen(homeScreen);
            yield return WaitForNavigationIdle(experience);

            Assert.That(
                analytics.Count(ProductEventKind.WorkshopViewed),
                Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SuccessfulLevelRoundTripEmitsOneWorkshopViewed()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            WorkshopHomeController home = FindHome();
            var analytics = new RecordingAnalytics();
            SetPrivateField(home, "_analytics", analytics);

            home.View.PrimaryButton.onClick.Invoke();
            yield return WaitForCurrentLevel(experience, 0);
            yield return WaitForNavigationIdle(experience);
            UniTask returning = experience.ReturnToWorkshopAsync(
                WorkshopHomeEntryReason.ReturnFromLevel);
            yield return Await(returning);

            Assert.That(
                analytics.Count(ProductEventKind.WorkshopViewed),
                Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FailedLevelLoadShowsLocalizedVisibleRetryAndRetainsExactRequest()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            ILevelFlowController realFlow =
                GetPrivateField<ILevelFlowController>(
                    experience,
                    "_levelFlow");
            var failing = new CountingFailingLevelFlow();
            SetPrivateField(experience, "_levelFlow", failing);
            var analytics = new RecordingAnalytics();
            SetPrivateField(experience, "_analytics", analytics);
            IWorkshopHomeRecovery realRecovery =
                GetPrivateField<IWorkshopHomeRecovery>(
                    experience,
                    "_workshopHomeRecovery");
            var capturingRecovery =
                new CapturingWorkshopHomeRecovery(realRecovery);
            SetPrivateField(
                experience,
                "_workshopHomeRecovery",
                capturingRecovery);
            var request = new LevelLaunchRequest(
                "01-soft-blocks",
                0,
                LevelLaunchSource.Workshop,
                "cozy-workshop",
                "cozy-workshop.clear-passage");

            UniTask<bool> play = experience.PlayLevelAsync(request);
            var played = true;
            yield return Await(play, value => played = value);

            WorkshopHomeController home = FindHome();
            var audio = new RecordingAudio();
            SetPrivateField(home, "_audioService", audio);
            Assert.That(played, Is.False);
            Assert.That(home.View.BottomSheet.IsOpen, Is.True);
            Assert.That(home.View.BottomSheet.ActionButton.gameObject.activeSelf,
                Is.True);
            Assert.That(home.View.VisibleActionControlsHaveBindings, Is.True);
            Assert.That(CountInteractiveScreens(), Is.EqualTo(1));
            Transform recoveryRoot =
                home.View.BottomSheet.transform.Find("Recovery Content");
            Assert.That(recoveryRoot, Is.Not.Null);
            Text title = recoveryRoot.Find("Failure Title")
                .GetComponent<Text>();
            Text actionLabel = home.View.BottomSheet.ActionButton
                .GetComponentInChildren<Text>(true);
            Assert.That(title.text,
                Is.EqualTo("This space needs one calm moment."));
            Assert.That(actionLabel.text, Is.EqualTo("Try again"));
            Assert.That(actionLabel.fontSize, Is.GreaterThanOrEqualTo(24));
            Assert.That(
                home.View.BottomSheet.ActionButton
                    .GetComponent<RectTransform>().sizeDelta.x,
                Is.GreaterThanOrEqualTo(600f));
            Assert.That(
                home.View.BottomSheet.ActionButton.targetGraphic.color.a,
                Is.GreaterThanOrEqualTo(0.8f));
            Assert.That(home.View.SettingsMusicButton.gameObject.activeSelf,
                Is.False);
            Assert.That(home.View.SettingsLocaleButton.gameObject.activeSelf,
                Is.False);
            Assert.That(home.View.SettingsHapticButton.gameObject.activeSelf,
                Is.False);

            IDemoLocalizationService localization =
                GetPrivateField<IDemoLocalizationService>(
                    home,
                    "_localization");
            localization.SelectLocale(DemoLocale.Ukrainian);
            yield return null;
            Assert.That(title.text,
                Is.EqualTo("Цьому простору потрібна спокійна мить."));
            Assert.That(actionLabel.text,
                Is.EqualTo("Спробувати ще раз"));
            localization.SelectLocale(DemoLocale.Russian);
            yield return null;
            Assert.That(title.text,
                Is.EqualTo("Этому пространству нужна спокойная минута."));
            Assert.That(actionLabel.text,
                Is.EqualTo("Попробовать снова"));

            home.View.BottomSheet.ActionButton.onClick.Invoke();
            yield return WaitForLoadCalls(failing, 2);
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.EqualTo(1));
            Assert.That(failing.LoadCallCount, Is.EqualTo(2),
                "Replacing the recovery action must not multiply listeners.");
            Assert.That(capturingRecovery.RetryCount, Is.EqualTo(1));
            Assert.That(capturingRecovery.LastRetry.LevelId,
                Is.EqualTo(request.LevelId));
            Assert.That(capturingRecovery.LastRetry.LevelIndex,
                Is.EqualTo(request.LevelIndex));
            Assert.That(capturingRecovery.LastRetry.Source,
                Is.EqualTo(request.Source));
            Assert.That(capturingRecovery.LastRetry.ChapterId,
                Is.EqualTo(request.ChapterId));
            Assert.That(capturingRecovery.LastRetry.BeatId,
                Is.EqualTo(request.BeatId));
            yield return WaitForOpenSheet(home.View.BottomSheet);
            yield return WaitForNavigationIdle(experience);

            SetPrivateField(experience, "_levelFlow", realFlow);
            home.View.BottomSheet.ActionButton.onClick.Invoke();
            yield return WaitForCurrentLevel(experience, 0);
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.EqualTo(2),
                "Each accepted recovery retry emits one UI cue.");
            ProductAnalyticsEvent started =
                analytics.Find(ProductEventKind.LevelStarted);
            Assert.That(started.LevelId, Is.EqualTo(request.LevelId));
            Assert.That(started.LevelIndex, Is.EqualTo(request.LevelIndex));
            Assert.That(started.LaunchSource, Is.EqualTo(request.Source));
        }

        [UnityTest]
        public IEnumerator PersistFailureRetriesTheSameCompletionCommand()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            yield return LoadLevelAt(experience, 7);
            var coordinator = new ScriptedWorkshopFlow(
                ProfileMutationStatus.PersistFailed,
                ProfileMutationStatus.Applied);
            var analytics = new RecordingAnalytics();
            var audio = new RecordingAudio();
            SetPrivateField(experience, "_workshopFlowCoordinator", coordinator);
            SetPrivateField(experience, "_analytics", analytics);
            SetPrivateField(experience, "_audioService", audio);

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
            Assert.That(analytics.Count(ProductEventKind.MemoryUnlocked),
                Is.Zero);
            Assert.That(analytics.Count(ProductEventKind.ChapterCompleted),
                Is.Zero);
            Assert.That(audio.Count(AsmrAudioCue.LevelComplete), Is.Zero,
                "A completion that did not persist must stay silent.");

            yield return WaitForButton(retry);

            retry.onClick.Invoke();
            yield return WaitForCompletionStatus(
                experience,
                ProfileMutationStatus.Applied);

            Assert.That(coordinator.CompleteCallCount, Is.EqualTo(2));
            Assert.That(coordinator.AllCommandsMatch, Is.True);
            Assert.That(analytics.Count(ProductEventKind.LevelCompleted),
                Is.EqualTo(1));
            Assert.That(analytics.Count(ProductEventKind.MemoryUnlocked),
                Is.EqualTo(1));
            Assert.That(analytics.Count(ProductEventKind.ChapterCompleted),
                Is.EqualTo(1));
            Assert.That(audio.Count(AsmrAudioCue.LevelComplete), Is.EqualTo(1),
                "The successful retry emits completion once.");
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.EqualTo(1),
                "The accepted save retry emits one UI cue.");

            retry.onClick.Invoke();
            yield return null;
            Assert.That(audio.Count(AsmrAudioCue.UiTap), Is.EqualTo(1),
                "A stale retry callback is rejected and stays silent.");
        }

        [UnityTest]
        public IEnumerator AppliedStageTwoEmitsOneGatedMemoryUnlockedEvent()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            yield return LoadLevelAt(experience, 1);
            var coordinator = new ScriptedWorkshopFlow(
                ProfileMutationStatus.Applied,
                ProfileMutationStatus.Applied);
            var analytics = new RecordingAnalytics();
            SetPrivateField(experience, "_workshopFlowCoordinator", coordinator);
            SetPrivateField(experience, "_analytics", analytics);

            InvokeCompletion(experience);
            InvokeCompletion(experience);
            yield return null;

            Assert.That(analytics.Count(ProductEventKind.MemoryUnlocked),
                Is.EqualTo(1));
            ProductAnalyticsEvent unlocked =
                analytics.Find(ProductEventKind.MemoryUnlocked);
            Assert.That(unlocked.BeatId,
                Is.EqualTo("cozy-workshop.pebble-shelf"));
            Assert.That(unlocked.ItemId,
                Is.EqualTo("family.summer-trail-stones"));
            Assert.That(analytics.Count(ProductEventKind.ChapterCompleted),
                Is.Zero);
        }

        [UnityTest]
        public IEnumerator AppliedStageEightEmitsOneGatedMemoryAndChapterEvent()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            yield return LoadLevelAt(experience, 7);
            var coordinator = new ScriptedWorkshopFlow(
                ProfileMutationStatus.Applied,
                ProfileMutationStatus.Applied);
            var analytics = new RecordingAnalytics();
            SetPrivateField(experience, "_workshopFlowCoordinator", coordinator);
            SetPrivateField(experience, "_analytics", analytics);

            InvokeCompletion(experience);
            InvokeCompletion(experience);
            yield return null;

            Assert.That(analytics.Count(ProductEventKind.MemoryUnlocked),
                Is.EqualTo(1));
            Assert.That(analytics.Count(ProductEventKind.ChapterCompleted),
                Is.EqualTo(1));
            ProductAnalyticsEvent unlocked =
                analytics.Find(ProductEventKind.MemoryUnlocked);
            ProductAnalyticsEvent chapter =
                analytics.Find(ProductEventKind.ChapterCompleted);
            Assert.That(unlocked.BeatId,
                Is.EqualTo("cozy-workshop.open-window"));
            Assert.That(unlocked.ItemId,
                Is.EqualTo("family.open-windows"));
            Assert.That(chapter.ChapterId, Is.EqualTo("cozy-workshop"));
            Assert.That(chapter.BeatId,
                Is.EqualTo("cozy-workshop.open-window"));
        }

        [UnityTest]
        public IEnumerator InvalidWorkshopTextSuppressesCompletionMetaAnalytics()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            yield return LoadLevelAt(experience, 7);
            LevelCatalog levels = GetPrivateField<LevelCatalog>(
                experience,
                "_levelCatalog");
            LivingWorkshopCatalog workshop =
                GetPrivateField<LivingWorkshopCatalog>(
                    experience,
                    "_livingWorkshopCatalog");
            WorkshopTextCatalog emptyText =
                ScriptableObject.CreateInstance<WorkshopTextCatalog>();
            var availability = new WorkshopRuntimeAvailability(
                levels,
                workshop,
                emptyText);
            SetAvailabilityIfSupported(experience, availability);
            var coordinator = new ScriptedWorkshopFlow(
                ProfileMutationStatus.Applied);
            var analytics = new RecordingAnalytics();
            SetPrivateField(experience, "_workshopFlowCoordinator", coordinator);
            SetPrivateField(experience, "_analytics", analytics);

            InvokeCompletion(experience);
            yield return WaitForCompletionStatus(
                experience,
                ProfileMutationStatus.Applied);

            Assert.That(coordinator.CompleteCallCount, Is.EqualTo(1));
            Assert.That(analytics.Count(ProductEventKind.LevelCompleted),
                Is.EqualTo(1));
            Assert.That(analytics.Count(ProductEventKind.MemoryUnlocked),
                Is.Zero);
            Assert.That(analytics.Count(ProductEventKind.ChapterCompleted),
                Is.Zero);
            Object.Destroy(emptyText);
        }

        [UnityTest]
        public IEnumerator PartialInvalidWorkshopSuppressesResolvableBeatAnalytics()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            yield return LoadLevelAt(experience, 7);
            LevelCatalog levels = GetPrivateField<LevelCatalog>(
                experience,
                "_levelCatalog");
            LivingWorkshopCatalog validWorkshop =
                GetPrivateField<LivingWorkshopCatalog>(
                    experience,
                    "_livingWorkshopCatalog");
            Assert.That(validWorkshop.TryFindBeat(
                "cozy-workshop",
                7,
                out WorkshopBeatDefinition finaleBeat), Is.True);
            var partial =
                ScriptableObject.CreateInstance<LivingWorkshopCatalog>();
            SetPrivateField(
                partial,
                "_beats",
                new[] { finaleBeat });
            IWorkshopTextService textService =
                GetPrivateField<IWorkshopTextService>(
                    experience,
                    "_workshopText");
            WorkshopTextCatalog text =
                GetPrivateField<WorkshopTextCatalog>(
                    textService,
                    "_catalog");
            var availability = new WorkshopRuntimeAvailability(
                levels,
                partial,
                text);
            Assert.That(availability.HomeMetaAvailable, Is.False);
            SetPrivateField(experience, "_livingWorkshopCatalog", partial);
            SetAvailabilityIfSupported(experience, availability);
            var coordinator = new ScriptedWorkshopFlow(
                ProfileMutationStatus.Applied);
            var analytics = new RecordingAnalytics();
            SetPrivateField(experience, "_workshopFlowCoordinator", coordinator);
            SetPrivateField(experience, "_analytics", analytics);

            InvokeCompletion(experience);
            yield return WaitForCompletionStatus(
                experience,
                ProfileMutationStatus.Applied);

            Assert.That(coordinator.CompleteCallCount, Is.EqualTo(1));
            Assert.That(analytics.Count(ProductEventKind.LevelCompleted),
                Is.EqualTo(1));
            Assert.That(analytics.Count(ProductEventKind.MemoryUnlocked),
                Is.Zero,
                "A locally resolvable beat cannot override invalid authoring policy.");
            Assert.That(analytics.Count(ProductEventKind.ChapterCompleted),
                Is.Zero);
            Object.Destroy(partial);
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
            yield return LoadLevelAt(experience, 7);
            var coordinator = new ScriptedWorkshopFlow(
                ProfileMutationStatus.AlreadyApplied);
            var analytics = new RecordingAnalytics();
            var audio = new RecordingAudio();
            SetPrivateField(experience, "_workshopFlowCoordinator", coordinator);
            SetPrivateField(experience, "_analytics", analytics);
            SetPrivateField(experience, "_audioService", audio);

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
            Assert.That(analytics.Count(ProductEventKind.MemoryUnlocked),
                Is.Zero);
            Assert.That(analytics.Count(ProductEventKind.ChapterCompleted),
                Is.Zero);
            Assert.That(audio.Count(AsmrAudioCue.LevelComplete), Is.EqualTo(1),
                "A successful replay still receives one completion cue.");
        }

        [UnityTest]
        public IEnumerator InvalidCompletionLogsOnceAndKeepsHudStable()
        {
            yield return LoadMain();
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>();
            yield return LoadLevelAt(experience, 7);
            var coordinator = new ScriptedWorkshopFlow(
                ProfileMutationStatus.Invalid,
                ProfileMutationStatus.Invalid);
            var analytics = new RecordingAnalytics();
            SetPrivateField(experience, "_workshopFlowCoordinator", coordinator);
            SetPrivateField(experience, "_analytics", analytics);

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
            Assert.That(analytics.Count(ProductEventKind.MemoryUnlocked),
                Is.Zero);
            Assert.That(analytics.Count(ProductEventKind.ChapterCompleted),
                Is.Zero);
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
            yield return LoadLevelAt(experience, 0);
        }

        private static IEnumerator LoadLevelAt(
            DemoExperienceController experience,
            int levelIndex)
        {
            IDemoProgressStore store = GetPrivateField<IDemoProgressStore>(
                experience,
                "_progressStore");
            for (var index = 0; index < levelIndex; index++)
            {
                if (!store.IsLevelCompleted(index))
                {
                    store.MarkLevelCompleted(index);
                }
            }

            UniTask<bool> play = experience.PlayLevelAsync(levelIndex);
            var played = false;
            yield return Await(play, value => played = value);
            Assert.That(played, Is.True);
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

        private static IEnumerator WaitForScreen(CanvasGroup screen)
        {
            float timeout = Time.realtimeSinceStartup + 10f;
            while ((!screen.gameObject.activeInHierarchy ||
                    !screen.interactable ||
                    !screen.blocksRaycasts) &&
                Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(screen.gameObject.activeInHierarchy, Is.True);
            Assert.That(screen.interactable, Is.True);
            Assert.That(screen.blocksRaycasts, Is.True);
        }

        private static IEnumerator WaitForCurrentLevel(
            DemoExperienceController experience,
            int expectedIndex)
        {
            float timeout = Time.realtimeSinceStartup + 10f;
            while (experience.CurrentLevelIndex != expectedIndex &&
                Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(experience.CurrentLevelIndex,
                Is.EqualTo(expectedIndex));
        }

        private static IEnumerator WaitForNavigationIdle(
            DemoExperienceController experience)
        {
            float timeout = Time.realtimeSinceStartup + 10f;
            while (experience.IsNavigating &&
                Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(experience.IsNavigating, Is.False);
        }

        private static IEnumerator WaitForLoadCalls(
            CountingFailingLevelFlow flow,
            int expectedCount)
        {
            float timeout = Time.realtimeSinceStartup + 10f;
            while (flow.LoadCallCount < expectedCount &&
                Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(flow.LoadCallCount, Is.EqualTo(expectedCount));
        }

        private static IEnumerator WaitForOpenSheet(WorkshopBottomSheet sheet)
        {
            float timeout = Time.realtimeSinceStartup + 10f;
            while (!sheet.IsOpen && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(sheet.IsOpen, Is.True);
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

            UniTaskStatus status = task.Status;
            if (status == UniTaskStatus.Faulted ||
                status == UniTaskStatus.Canceled)
            {
                task.GetAwaiter().GetResult();
            }

            Assert.That(status, Is.EqualTo(UniTaskStatus.Succeeded));
            task.GetAwaiter().GetResult();
        }

        private static System.Exception CaptureAwaitFailure(
            IEnumerator routine)
        {
            try
            {
                routine.MoveNext();
                return null;
            }
            catch (System.Exception exception)
            {
                return exception;
            }
        }

        private static void CaptureTaskFailure(UniTask task)
        {
            try
            {
                task.GetAwaiter().GetResult();
            }
            catch
            {
                // The regression assertion owns the original exception.
            }
        }

        private static void CaptureTaskFailure(UniTask<bool> task)
        {
            try
            {
                task.GetAwaiter().GetResult();
            }
            catch
            {
                // The regression assertion owns the original exception.
            }
        }

        private static IEnumerator Await(
            UniTask<bool> task,
            System.Action<bool> onSucceeded = null)
        {
            float timeout = Time.realtimeSinceStartup + 10f;
            while (task.Status == UniTaskStatus.Pending &&
                Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            UniTaskStatus status = task.Status;
            if (status == UniTaskStatus.Faulted ||
                status == UniTaskStatus.Canceled)
            {
                task.GetAwaiter().GetResult();
            }

            Assert.That(status, Is.EqualTo(UniTaskStatus.Succeeded));
            bool result = task.GetAwaiter().GetResult();
            onSucceeded?.Invoke(result);
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

        private static void SetAvailabilityIfSupported(
            DemoExperienceController experience,
            WorkshopRuntimeAvailability availability)
        {
            FieldInfo field = typeof(DemoExperienceController).GetField(
                "_workshopAvailability",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(experience, availability);
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

            public ProductAnalyticsEvent Find(ProductEventKind kind)
            {
                foreach (ProductAnalyticsEvent analyticsEvent in _events)
                {
                    if (analyticsEvent.Kind == kind)
                    {
                        return analyticsEvent;
                    }
                }

                Assert.Fail("Missing analytics event " + kind + ".");
                return default;
            }
        }

        private sealed class RecordingAudio : ICategorizedAsmrAudioService
        {
            private readonly List<AsmrAudioCue> _cues =
                new List<AsmrAudioCue>();

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

        private sealed class FailOnceMusicPreferenceStore : IDemoProgressStore
        {
            private readonly IDemoProgressStore _inner;
            private bool _failNextMusicWrite = true;

            public FailOnceMusicPreferenceStore(IDemoProgressStore inner)
            {
                _inner = inner;
            }

            public event System.Action<DemoProgressSnapshot> ProgressChanged
            {
                add => _inner.ProgressChanged += value;
                remove => _inner.ProgressChanged -= value;
            }

            public bool IsInitialized => _inner.IsInitialized;
            public int LevelCount => _inner.LevelCount;
            public DemoProgressSnapshot Current => _inner.Current;
            public int MusicWriteAttempts { get; private set; }

            public ProfileInitializationResult Initialize(
                int levelCount,
                string defaultThemeId,
                int completionReward) =>
                _inner.Initialize(
                    levelCount,
                    defaultThemeId,
                    completionReward);

            public ProfileMutationResult<LevelCompletionMutation> CompleteLevel(
                CompleteLevelCommand command) =>
                _inner.CompleteLevel(command);

            public ProfileMutationResult<PresentationMutation>
                MarkPresentationSeen(PendingPresentationEntry presentation) =>
                    _inner.MarkPresentationSeen(presentation);

            public ProfileMutationResult<MemoryMutation> MarkMemoryViewed(
                string memoryId) =>
                    _inner.MarkMemoryViewed(memoryId);

            public ProfileMutationResult<DecorationMutation>
                PurchaseAndSelectDecoration(
                    string slotId,
                    string decorationId,
                    int cost) =>
                    _inner.PurchaseAndSelectDecoration(
                        slotId,
                        decorationId,
                        cost);

            public ProfileMutationResult<DecorationMutation>
                GrantAndSelectDecoration(
                    string slotId,
                    string decorationId,
                    DecorationGrantSource source) =>
                    _inner.GrantAndSelectDecoration(
                        slotId,
                        decorationId,
                        source);

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
                bool enabled)
            {
                MusicWriteAttempts++;
                if (_failNextMusicWrite)
                {
                    _failNextMusicWrite = false;
                    return new ProfileMutationResult<PreferenceMutation>(
                        ProfileMutationStatus.PersistFailed,
                        _inner.Current,
                        new PreferenceMutation(false));
                }

                return _inner.SetMusicEnabled(enabled);
            }

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

        private sealed class CountingFailingLevelFlow : ILevelFlowController
        {
            public int LoadCallCount { get; private set; }
            public LevelBase CurrentLevel => null;
            public int CurrentLevelIndex => -1;
            public bool IsLoading => false;
            public UniTask<bool> LoadFirstLevelAsync(
                System.Threading.CancellationToken cancellationToken = default)
                => UniTask.FromResult(false);
            public UniTask<bool> LoadLevelAsync(
                string levelId,
                System.Threading.CancellationToken cancellationToken = default)
            {
                LoadCallCount++;
                return UniTask.FromResult(false);
            }
            public UniTask<bool> LoadNextLevelAsync(
                System.Threading.CancellationToken cancellationToken = default)
                => UniTask.FromResult(false);
            public UniTask UnloadCurrentLevelAsync(
                System.Threading.CancellationToken cancellationToken = default)
                => UniTask.CompletedTask;
        }

        private sealed class CapturingWorkshopHomeRecovery :
            IWorkshopHomeRecovery
        {
            private readonly IWorkshopHomeRecovery _inner;

            public CapturingWorkshopHomeRecovery(
                IWorkshopHomeRecovery inner)
            {
                _inner = inner;
            }

            public int RetryCount { get; private set; }
            public LevelLaunchRequest LastRetry { get; private set; }

            public void ShowLoadFailure(
                LevelLaunchRequest request,
                System.Action<LevelLaunchRequest> retry)
            {
                _inner.ShowLoadFailure(
                    request,
                    retried =>
                    {
                        RetryCount++;
                        LastRetry = retried;
                        retry(retried);
                    });
            }
        }

        private sealed class CancellingUnloadLevelFlow : ILevelFlowController
        {
            private readonly bool _releaseBeforeCancel;

            public CancellingUnloadLevelFlow(
                LevelBase currentLevel,
                int currentLevelIndex,
                bool releaseBeforeCancel)
            {
                CurrentLevel = currentLevel;
                CurrentLevelIndex = currentLevelIndex;
                _releaseBeforeCancel = releaseBeforeCancel;
            }

            public LevelBase CurrentLevel { get; private set; }
            public int CurrentLevelIndex { get; private set; }
            public bool IsLoading { get; private set; }

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

            public async UniTask UnloadCurrentLevelAsync(
                System.Threading.CancellationToken cancellationToken = default)
            {
                IsLoading = true;
                await UniTask.Yield();
                if (_releaseBeforeCancel)
                {
                    CurrentLevel = null;
                    CurrentLevelIndex = -1;
                }

                IsLoading = false;
                throw new System.OperationCanceledException(
                    "Controlled cancellation after unload boundary.");
            }
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
