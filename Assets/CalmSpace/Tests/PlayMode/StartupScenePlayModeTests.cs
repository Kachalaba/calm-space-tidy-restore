using System.Collections;
using System.Reflection;
using CalmSpace.Cleaning;
using CalmSpace.Demo;
using CalmSpace.Input;
using CalmSpace.Levels;
using CalmSpace.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CalmSpace.Tests.PlayMode
{
    public sealed class StartupScenePlayModeTests
    {
        [SetUp]
        public void ClearDemoProgress()
        {
            PlayerPrefs.DeleteKey(
                PlayerPrefsDemoProgressStore.DefaultPlayerPrefsKey);
            PlayerPrefs.SetInt(
                DemoLocalizationService.DefaultPlayerPrefsKey,
                (int)DemoLocale.English);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void RemoveDemoProgress()
        {
            PlayerPrefs.DeleteKey(
                PlayerPrefsDemoProgressStore.DefaultPlayerPrefsKey);
            PlayerPrefs.DeleteKey(
                DemoLocalizationService.DefaultPlayerPrefsKey);
        }

        [UnityTest]
        public IEnumerator MainSceneStartsOnMenuWithoutLoadingLevel()
        {
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                "Main",
                LoadSceneMode.Single);
            Assert.That(sceneLoad, Is.Not.Null);

            while (!sceneLoad.isDone)
            {
                yield return null;
            }

            DemoExperienceController experience = null;
            for (int frame = 0;
                 frame < 300 &&
                 (experience == null || !experience.IsInitialized);
                 frame++)
            {
                experience =
                    Object.FindFirstObjectByType<
                        DemoExperienceController>(
                    FindObjectsInactive.Include);
                yield return null;
            }

            Assert.That(experience, Is.Not.Null);
            Assert.That(experience.IsInitialized, Is.True);
            Assert.That(experience.CurrentLevelIndex, Is.EqualTo(-1));
            Assert.That(
                Object.FindFirstObjectByType<LevelBase>(
                    FindObjectsInactive.Include),
                Is.Null);
            Assert.That(
                Object.FindFirstObjectByType<DragInputRouter>(),
                Is.Not.Null);
            DemoRoomPresenter room =
                Object.FindFirstObjectByType<DemoRoomPresenter>(
                    FindObjectsInactive.Include);
            Assert.That(room, Is.Not.Null);
            Assert.That(room.IsVisible, Is.True);
            Assert.That(room.DecorationCount, Is.EqualTo(4));
            Assert.That(room.SelectedDecorationIndex, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LanguageButtonSwitchesMenuToUkrainian()
        {
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                "Main",
                LoadSceneMode.Single);
            while (!sceneLoad.isDone)
            {
                yield return null;
            }

            DemoExperienceController experience = null;
            for (var frame = 0;
                 frame < 300 &&
                 (experience == null || !experience.IsInitialized);
                 frame++)
            {
                experience =
                    Object.FindFirstObjectByType<
                        DemoExperienceController>();
                yield return null;
            }

            Assert.That(experience, Is.Not.Null);
            Assert.That(experience.IsInitialized, Is.True);

            FieldInfo languageButtonField =
                typeof(DemoExperienceController).GetField(
                    "_languageButton",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            FieldInfo languageTextField =
                typeof(DemoExperienceController).GetField(
                    "_languageButtonText",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            FieldInfo progressTextField =
                typeof(DemoExperienceController).GetField(
                    "_homeProgressText",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            FieldInfo playButtonField =
                typeof(DemoExperienceController).GetField(
                    "_playButton",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            var languageButton =
                languageButtonField?.GetValue(experience) as Button;
            var languageText =
                languageTextField?.GetValue(experience) as Text;
            var progressText =
                progressTextField?.GetValue(experience) as Text;
            var playButton =
                playButtonField?.GetValue(experience) as Button;
            Assert.That(languageButton, Is.Not.Null);
            Assert.That(languageText, Is.Not.Null);
            Assert.That(progressText, Is.Not.Null);
            Assert.That(playButton, Is.Not.Null);

            languageButton.onClick.Invoke();
            yield return null;

            Assert.That(languageText.text, Is.EqualTo("УКР"));
            Assert.That(
                progressText.text,
                Is.EqualTo("Відновлено: 0 із 8"));
            Assert.That(
                playButton.GetComponentInChildren<Text>().text,
                Is.EqualTo("Почати відновлення"));
            Assert.That(
                PlayerPrefs.GetInt(
                    DemoLocalizationService.DefaultPlayerPrefsKey),
                Is.EqualTo((int)DemoLocale.Ukrainian));
        }

        [UnityTest]
        public IEnumerator PlayButtonFlowLoadsFirstAddressableLevel()
        {
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                "Main",
                LoadSceneMode.Single);
            while (!sceneLoad.isDone)
            {
                yield return null;
            }

            DemoExperienceController experience = null;
            for (var frame = 0;
                 frame < 300 &&
                 (experience == null || !experience.IsInitialized);
                 frame++)
            {
                experience =
                    Object.FindFirstObjectByType<
                        DemoExperienceController>();
                yield return null;
            }

            Assert.That(experience, Is.Not.Null);
            Assert.That(experience.IsInitialized, Is.True);

            UniTask<bool> playTask =
                experience.PlayRecommendedLevelAsync();
            float timeoutAt = Time.realtimeSinceStartup + 10f;
            while (
                playTask.Status == UniTaskStatus.Pending &&
                Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(
                playTask.Status,
                Is.Not.EqualTo(UniTaskStatus.Pending));
            Assert.That(
                playTask.GetAwaiter().GetResult(),
                Is.True);

            FittingLevel level =
                Object.FindFirstObjectByType<FittingLevel>();
            Assert.That(level, Is.Not.Null);
            Assert.That(level.State, Is.EqualTo(LevelState.Active));
            Assert.That(level.ItemCount, Is.EqualTo(3));
            Assert.That(level.Definition.LevelId, Is.EqualTo(
                "01-soft-blocks"));
            Assert.That(experience.CurrentLevelIndex, Is.Zero);
            DemoRoomPresenter room =
                Object.FindFirstObjectByType<DemoRoomPresenter>(
                    FindObjectsInactive.Include);
            Assert.That(room, Is.Not.Null);
            Assert.That(
                room.IsVisible,
                Is.False,
                "The decorative room must not cover gameplay.");
        }

        [UnityTest]
        public IEnumerator ScrewLevelHudExplainsHoldGesture()
        {
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                "Main",
                LoadSceneMode.Single);
            while (!sceneLoad.isDone)
            {
                yield return null;
            }

            DemoExperienceController experience = null;
            for (var frame = 0;
                 frame < 300 &&
                 (experience == null || !experience.IsInitialized);
                 frame++)
            {
                experience =
                    Object.FindFirstObjectByType<
                        DemoExperienceController>();
                yield return null;
            }

            Assert.That(experience, Is.Not.Null);
            Assert.That(experience.IsInitialized, Is.True);

            IDemoProgressStore progress =
                GetPrivateField<IDemoProgressStore>(
                    experience,
                    "_progressStore");
            for (var index = 0; index < 4; index++)
            {
                progress.MarkLevelCompleted(index);
            }

            UniTask<bool> playTask =
                experience.PlayLevelAsync(4);
            float timeoutAt = Time.realtimeSinceStartup + 10f;
            while (
                playTask.Status == UniTaskStatus.Pending &&
                Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(
                playTask.Status,
                Is.Not.EqualTo(UniTaskStatus.Pending));
            Assert.That(
                playTask.GetAwaiter().GetResult(),
                Is.True);

            Text hudProgress =
                GetPrivateField<Text>(
                    experience,
                    "_hudProgressText");
            Assert.That(
                hudProgress.text,
                Does.Contain("Hold a screw to turn it out"));
        }

        [UnityTest]
        public IEnumerator EveryDemoAddressableLevelInitializes()
        {
            string[] levelIds =
            {
                "01-soft-blocks",
                "02-pebble-pairs",
                "03-tea-drawer",
                "04-color-shelf",
                "05-fastener-tray",
                "06-fresh-surface",
                "07-cabinet-hinge",
                "08-dusty-window"
            };
            LevelType[] expectedTypes =
            {
                LevelType.Fitting,
                LevelType.Sorting,
                LevelType.Fitting,
                LevelType.Sorting,
                LevelType.ScrewPuzzle,
                LevelType.Cleaning,
                LevelType.ScrewPuzzle,
                LevelType.Cleaning
            };

            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                "Main",
                LoadSceneMode.Single);
            while (!sceneLoad.isDone)
            {
                yield return null;
            }

            DemoExperienceController experience = null;
            float initializationTimeout =
                Time.realtimeSinceStartup + 10f;
            while (
                (experience == null || !experience.IsInitialized) &&
                Time.realtimeSinceStartup < initializationTimeout)
            {
                experience =
                    Object.FindFirstObjectByType<
                        DemoExperienceController>();
                yield return null;
            }

            Assert.That(experience, Is.Not.Null);
            Assert.That(experience.IsInitialized, Is.True);

            FieldInfo flowField =
                typeof(DemoExperienceController).GetField(
                    "_levelFlow",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(flowField, Is.Not.Null);
            var flow =
                flowField.GetValue(experience) as
                    ILevelFlowController;
            Assert.That(flow, Is.Not.Null);

            for (var index = 0; index < levelIds.Length; index++)
            {
                UniTask<bool> loadTask =
                    flow.LoadLevelAsync(levelIds[index]);
                float loadTimeout =
                    Time.realtimeSinceStartup + 10f;
                while (
                    loadTask.Status == UniTaskStatus.Pending &&
                    Time.realtimeSinceStartup < loadTimeout)
                {
                    yield return null;
                }

                Assert.That(
                    loadTask.Status,
                    Is.Not.EqualTo(UniTaskStatus.Pending),
                    "Timed out loading " + levelIds[index]);
                Assert.That(
                    loadTask.GetAwaiter().GetResult(),
                    Is.True,
                    levelIds[index]);
                Assert.That(flow.CurrentLevel, Is.Not.Null);
                Assert.That(
                    flow.CurrentLevel.Definition.LevelId,
                    Is.EqualTo(levelIds[index]));
                Assert.That(
                    flow.CurrentLevel.SupportedType,
                    Is.EqualTo(expectedTypes[index]));
                Assert.That(
                    flow.CurrentLevel.State,
                    Is.EqualTo(LevelState.Active));

                if (expectedTypes[index] == LevelType.Cleaning)
                {
                    // Staged levels hold a cleaner per pass, so ask the
                    // level which surface the player is on rather than
                    // assuming one lives on the root.
                    var cleaningLevel =
                        flow.CurrentLevel as CleaningLevel;
                    Assert.That(
                        cleaningLevel,
                        Is.Not.Null,
                        levelIds[index]);
                    RenderTextureCleaner cleaner =
                        cleaningLevel.ActiveCleaner;
                    if (cleaningLevel.StageCount > 0 &&
                        cleaningLevel.ActiveTool ==
                            CleaningToolKind.Hands)
                    {
                        Assert.That(
                            cleaner,
                            Is.Null,
                            levelIds[index] +
                            " must keep its cleaner locked while " +
                            "debris remains.");
                    }
                    else
                    {
                        Assert.That(
                            cleaner,
                            Is.Not.Null,
                            levelIds[index]);
                        Assert.That(
                            cleaner.IsInitialized,
                            Is.True,
                            levelIds[index]);
                    }
                }
            }

            UniTask unloadTask = flow.UnloadCurrentLevelAsync();
            float unloadTimeout = Time.realtimeSinceStartup + 10f;
            while (
                unloadTask.Status == UniTaskStatus.Pending &&
                Time.realtimeSinceStartup < unloadTimeout)
            {
                yield return null;
            }

            Assert.That(
                unloadTask.Status,
                Is.Not.EqualTo(UniTaskStatus.Pending));
            Assert.That(flow.CurrentLevel, Is.Null);
        }

        [UnityTest]
        public IEnumerator LastLevelCompletionShowsOneHomeAction()
        {
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                "Main",
                LoadSceneMode.Single);
            while (!sceneLoad.isDone)
            {
                yield return null;
            }

            DemoExperienceController experience = null;
            float initializationTimeout =
                Time.realtimeSinceStartup + 10f;
            while (
                (experience == null || !experience.IsInitialized) &&
                Time.realtimeSinceStartup < initializationTimeout)
            {
                experience =
                    Object.FindFirstObjectByType<
                        DemoExperienceController>();
                yield return null;
            }

            Assert.That(experience, Is.Not.Null);
            Assert.That(experience.IsInitialized, Is.True);

            FieldInfo progressField =
                typeof(DemoExperienceController).GetField(
                    "_progressStore",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(progressField, Is.Not.Null);
            var progressStore =
                progressField.GetValue(experience) as
                    IDemoProgressStore;
            Assert.That(progressStore, Is.Not.Null);

            FieldInfo catalogField =
                typeof(DemoExperienceController).GetField(
                    "_levelCatalog",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(catalogField, Is.Not.Null);
            var catalog =
                catalogField.GetValue(experience) as LevelCatalog;
            Assert.That(catalog, Is.Not.Null);

            // Derived from the catalog so growing the level set does not
            // silently stop testing the real last level.
            int lastLevelIndex = catalog.Count - 1;
            for (var index = 0; index < lastLevelIndex; index++)
            {
                progressStore.MarkLevelCompleted(index);
            }

            UniTask<bool> loadTask =
                experience.PlayLevelAsync(lastLevelIndex);
            float loadTimeout =
                Time.realtimeSinceStartup + 10f;
            while (
                loadTask.Status == UniTaskStatus.Pending &&
                Time.realtimeSinceStartup < loadTimeout)
            {
                yield return null;
            }

            Assert.That(
                loadTask.Status,
                Is.Not.EqualTo(UniTaskStatus.Pending));
            Assert.That(
                loadTask.GetAwaiter().GetResult(),
                Is.True);

            LevelBase level =
                Object.FindFirstObjectByType<CleaningLevel>();
            Assert.That(level, Is.Not.Null);

            MethodInfo completionMethod =
                typeof(DemoExperienceController).GetMethod(
                    "HandleLevelCompleted",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(completionMethod, Is.Not.Null);
            completionMethod.Invoke(
                experience,
                new object[] { level });

            FieldInfo completionScreenField =
                typeof(DemoExperienceController).GetField(
                    "_completionScreen",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(completionScreenField, Is.Not.Null);
            var completionScreen =
                completionScreenField.GetValue(experience) as
                    CanvasGroup;

            float presentationTimeout =
                Time.realtimeSinceStartup + 3f;
            while (
                (completionScreen == null ||
                 !completionScreen.gameObject.activeSelf ||
                 !completionScreen.interactable) &&
                Time.realtimeSinceStartup < presentationTimeout)
            {
                yield return null;
            }

            Assert.That(completionScreen, Is.Not.Null);
            Assert.That(
                completionScreen.gameObject.activeSelf,
                Is.True);

            FieldInfo nextButtonField =
                typeof(DemoExperienceController).GetField(
                    "_nextButton",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            FieldInfo homeButtonField =
                typeof(DemoExperienceController).GetField(
                    "_completionHomeButton",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            FieldInfo nextTextField =
                typeof(DemoExperienceController).GetField(
                    "_nextButtonText",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(nextButtonField, Is.Not.Null);
            Assert.That(homeButtonField, Is.Not.Null);
            Assert.That(nextTextField, Is.Not.Null);

            var nextButton =
                nextButtonField.GetValue(experience) as Button;
            var homeButton =
                homeButtonField.GetValue(experience) as Button;
            var nextText =
                nextTextField.GetValue(experience) as Text;
            Assert.That(nextButton, Is.Not.Null);
            Assert.That(homeButton, Is.Not.Null);
            Assert.That(nextText, Is.Not.Null);
            Assert.That(nextButton.gameObject.activeSelf, Is.True);
            Assert.That(homeButton.gameObject.activeSelf, Is.False);
            Assert.That(nextText.text, Is.EqualTo("Back home"));
        }

        [UnityTest]
        public IEnumerator RoomPurchaseAndSelectionPersistAcrossReload()
        {
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(
                "Main",
                LoadSceneMode.Single);
            while (!sceneLoad.isDone)
            {
                yield return null;
            }

            DemoExperienceController experience = null;
            for (var frame = 0;
                 frame < 300 &&
                 (experience == null || !experience.IsInitialized);
                 frame++)
            {
                experience =
                    Object.FindFirstObjectByType<
                        DemoExperienceController>();
                yield return null;
            }

            Assert.That(experience, Is.Not.Null);
            Assert.That(experience.IsInitialized, Is.True);

            IDemoProgressStore progress =
                GetPrivateField<IDemoProgressStore>(
                    experience,
                    "_progressStore");
            DemoDecorationCatalog catalog =
                GetPrivateField<DemoDecorationCatalog>(
                    experience,
                    "_decorationCatalog");
            DemoExperienceController.DecorationButtonBinding[]
                buttons =
                    GetPrivateField<
                        DemoExperienceController
                            .DecorationButtonBinding[]>(
                        experience,
                        "_decorationButtons");
            Text currency =
                GetPrivateField<Text>(
                    experience,
                    "_roomCurrencyText");
            Assert.That(progress, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(buttons, Has.Length.EqualTo(4));
            Assert.That(currency, Is.Not.Null);
            Assert.That(currency.text, Is.EqualTo("CALM TOKENS · 0"));

            Assert.That(
                progress.CompleteLevelAndReward(
                    0,
                    catalog.CompletionReward),
                Is.EqualTo(15));
            Assert.That(
                progress.CompleteLevelAndReward(
                    1,
                    catalog.CompletionReward),
                Is.EqualTo(15));
            yield return null;

            Assert.That(progress.Current.CalmPoints, Is.EqualTo(30));
            Assert.That(buttons[1].Button.interactable, Is.True);
            buttons[1].Button.onClick.Invoke();
            yield return null;

            Assert.That(progress.Current.CalmPoints, Is.EqualTo(10));
            Assert.That(
                progress.Current.OwnedDecorationMask,
                Is.EqualTo(0b0011),
                "One button press must buy only that decoration.");
            Assert.That(
                progress.Current.SelectedDecorationIndex,
                Is.EqualTo(1));

            DemoRoomPresenter room =
                Object.FindFirstObjectByType<DemoRoomPresenter>(
                    FindObjectsInactive.Include);
            Assert.That(room, Is.Not.Null);
            Assert.That(room.SelectedDecorationIndex, Is.EqualTo(1));

            AsyncOperation reload = SceneManager.LoadSceneAsync(
                "Main",
                LoadSceneMode.Single);
            while (!reload.isDone)
            {
                yield return null;
            }

            experience = null;
            for (var frame = 0;
                 frame < 300 &&
                 (experience == null || !experience.IsInitialized);
                 frame++)
            {
                experience =
                    Object.FindFirstObjectByType<
                        DemoExperienceController>();
                yield return null;
            }

            Assert.That(experience, Is.Not.Null);
            progress = GetPrivateField<IDemoProgressStore>(
                experience,
                "_progressStore");
            room = Object.FindFirstObjectByType<DemoRoomPresenter>(
                FindObjectsInactive.Include);
            Assert.That(progress.Current.CalmPoints, Is.EqualTo(10));
            Assert.That(
                progress.Current.SelectedDecorationIndex,
                Is.EqualTo(1));
            Assert.That(room.SelectedDecorationIndex, Is.EqualTo(1));
            Assert.That(room.IsVisible, Is.True);
        }

        private static T GetPrivateField<T>(
            object target,
            string fieldName)
            where T : class
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(target) as T;
        }
    }
}
