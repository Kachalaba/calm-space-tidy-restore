using System;
using System.Collections.Generic;
using System.Reflection;
using CalmSpace.Demo;
using CalmSpace.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CalmSpace.Editor
{
    /// <summary>
    /// Creates the portrait demo shell in the generated single scene. All
    /// references are serialized at authoring time so runtime code performs no
    /// hierarchy searches.
    /// </summary>
    internal static class CalmSpaceDemoSceneBuilder
    {
        private const string ThemeCatalogPath =
            "Assets/CalmSpace/Config/DemoThemeCatalog.asset";
        private static readonly Color Ink =
            new Color(0.035f, 0.045f, 0.055f, 1f);
        private static readonly Color Paper =
            new Color(1f, 0.97f, 0.91f, 1f);
        private static readonly Color SecondaryPaper =
            new Color(0.76f, 0.82f, 0.76f, 1f);
        private static readonly Color Coral =
            new Color(1f, 0.47f, 0.40f, 1f);
        private static readonly Color SagePanel =
            new Color(0.09f, 0.16f, 0.14f, 0.96f);

        public static DemoThemeCatalog CreateOrUpdateThemeCatalog()
        {
            DemoThemeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<DemoThemeCatalog>(
                    ThemeCatalogPath);
            if (catalog == null)
            {
                catalog =
                    ScriptableObject.CreateInstance<DemoThemeCatalog>();
                AssetDatabase.CreateAsset(catalog, ThemeCatalogPath);
            }

            SetPrivateField(
                catalog,
                "_palettes",
                ThemePalette.CreateBuiltInPalettes());
            SetPrivateField(catalog, "_defaultPaletteIndex", 0);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        public static DemoExperienceController CreateDemoUi(
            Transform servicesRoot,
            Camera camera,
            Renderer[] boardRenderers,
            Sprite menuBackground,
            Sprite roundedSprite)
        {
            if (servicesRoot == null)
            {
                throw new ArgumentNullException(nameof(servicesRoot));
            }

            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (menuBackground == null || roundedSprite == null)
            {
                throw new ArgumentException(
                    "The demo UI requires its background and rounded sprite.");
            }

            EnsureEventSystem();
            Font font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            if (font == null)
            {
                throw new InvalidOperationException(
                    "Unity's built-in runtime font is unavailable.");
            }

            var uiRoot = new GameObject(
                "Demo UI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = uiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 20;

            CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2400f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            var primaryTexts = new List<Text>(32);
            var secondaryTexts = new List<Text>(24);
            var accentImages = new List<Image>(8);
            var panelImages = new List<Image>(16);
            var localizedTexts =
                new List<
                    DemoExperienceController.LocalizedTextBinding>(20);

            CanvasGroup home = CreateScreen(uiRoot.transform, "Home");
            Image homeWash;
            BuildHomeScreen(
                home.transform,
                menuBackground,
                roundedSprite,
                font,
                primaryTexts,
                secondaryTexts,
                accentImages,
                panelImages,
                localizedTexts,
                out Button playButton,
                out Button levelsButton,
                out Button musicButton,
                out Button languageButton,
                out Text homeProgressText,
                out Text musicButtonText,
                out Text languageButtonText,
                out Image musicStateImage,
                out DemoExperienceController.ThemeButtonBinding[]
                    themeBindings,
                out homeWash);

            CanvasGroup levelSelect =
                CreateScreen(uiRoot.transform, "Level Select");
            BuildLevelSelectScreen(
                levelSelect.transform,
                menuBackground,
                roundedSprite,
                font,
                primaryTexts,
                secondaryTexts,
                panelImages,
                localizedTexts,
                out Button levelBackButton,
                out DemoExperienceController.LevelButtonBinding[]
                    levelBindings);

            CanvasGroup hud = CreateScreen(
                uiRoot.transform,
                "Gameplay HUD");
            BuildHudScreen(
                hud.transform,
                roundedSprite,
                font,
                primaryTexts,
                secondaryTexts,
                panelImages,
                out Button hudHomeButton,
                out Text hudLevelNameText,
                out Text hudProgressText);

            CanvasGroup completion =
                CreateScreen(uiRoot.transform, "Completion");
            BuildCompletionScreen(
                completion.transform,
                roundedSprite,
                font,
                primaryTexts,
                secondaryTexts,
                accentImages,
                panelImages,
                localizedTexts,
                out Button completionHomeButton,
                out Button nextButton,
                out Text completionTitleText,
                out Text completionBodyText,
                out Text nextButtonText);

            CanvasGroup loading =
                CreateScreen(uiRoot.transform, "Loading");
            BuildLoadingOverlay(
                loading.transform,
                roundedSprite,
                font,
                primaryTexts,
                localizedTexts);

            LevelThemeApplicator applicator =
                servicesRoot.gameObject.AddComponent<
                    LevelThemeApplicator>();
            SetObjectReference(applicator, "_camera", camera);
            SetPrivateField(
                applicator,
                "_boardRenderers",
                boardRenderers ?? Array.Empty<Renderer>());

            DemoExperienceController experience =
                uiRoot.AddComponent<DemoExperienceController>();
            SetObjectReference(experience, "_homeScreen", home);
            SetObjectReference(
                experience,
                "_levelSelectScreen",
                levelSelect);
            SetObjectReference(experience, "_hudScreen", hud);
            SetObjectReference(
                experience,
                "_completionScreen",
                completion);
            SetObjectReference(
                experience,
                "_loadingOverlay",
                loading);
            SetObjectReference(experience, "_playButton", playButton);
            SetObjectReference(
                experience,
                "_levelsButton",
                levelsButton);
            SetObjectReference(
                experience,
                "_levelSelectBackButton",
                levelBackButton);
            SetObjectReference(
                experience,
                "_hudHomeButton",
                hudHomeButton);
            SetObjectReference(
                experience,
                "_completionHomeButton",
                completionHomeButton);
            SetObjectReference(experience, "_nextButton", nextButton);
            SetObjectReference(
                experience,
                "_musicButton",
                musicButton);
            SetObjectReference(
                experience,
                "_languageButton",
                languageButton);
            SetObjectReference(
                experience,
                "_homeProgressText",
                homeProgressText);
            SetObjectReference(
                experience,
                "_hudLevelNameText",
                hudLevelNameText);
            SetObjectReference(
                experience,
                "_hudProgressText",
                hudProgressText);
            SetObjectReference(
                experience,
                "_completionTitleText",
                completionTitleText);
            SetObjectReference(
                experience,
                "_completionBodyText",
                completionBodyText);
            SetObjectReference(
                experience,
                "_nextButtonText",
                nextButtonText);
            SetObjectReference(
                experience,
                "_musicButtonText",
                musicButtonText);
            SetObjectReference(
                experience,
                "_languageButtonText",
                languageButtonText);
            SetObjectReference(
                experience,
                "_levelThemeApplicator",
                applicator);
            SetObjectReference(
                experience,
                "_backgroundWash",
                homeWash);
            SetObjectReference(
                experience,
                "_musicStateImage",
                musicStateImage);
            SetPrivateField(experience, "_levelButtons", levelBindings);
            SetPrivateField(experience, "_themeButtons", themeBindings);
            SetPrivateField(
                experience,
                "_localizedTexts",
                localizedTexts.ToArray());
            SetPrivateField(
                experience,
                "_accentImages",
                accentImages.ToArray());
            SetPrivateField(
                experience,
                "_panelImages",
                panelImages.ToArray());
            SetPrivateField(
                experience,
                "_primaryTexts",
                primaryTexts.ToArray());
            SetPrivateField(
                experience,
                "_secondaryTexts",
                secondaryTexts.ToArray());
            EditorUtility.SetDirty(experience);
            return experience;
        }

        private static void BuildHomeScreen(
            Transform screen,
            Sprite background,
            Sprite rounded,
            Font font,
            List<Text> primaryTexts,
            List<Text> secondaryTexts,
            List<Image> accentImages,
            List<Image> panelImages,
            List<DemoExperienceController.LocalizedTextBinding>
                localizedTexts,
            out Button playButton,
            out Button levelsButton,
            out Button musicButton,
            out Button languageButton,
            out Text progressText,
            out Text musicText,
            out Text languageText,
            out Image musicStateImage,
            out DemoExperienceController.ThemeButtonBinding[]
                themeBindings,
            out Image backgroundWash)
        {
            CreateBackground(screen, background);
            backgroundWash = CreateImage(
                screen,
                "Theme Wash",
                null,
                new Color(0.035f, 0.06f, 0.055f, 0.26f),
                false);
            Stretch(backgroundWash.rectTransform);

            RectTransform safe =
                CreateSafeArea(screen, "Home Safe Area");

            Text eyebrow = CreateText(
                safe,
                "Eyebrow",
                "TIDY  ·  RESTORE  ·  BREATHE",
                font,
                28,
                FontStyle.Bold,
                TextAnchor.UpperLeft,
                Coral);
            SetAnchored(
                eyebrow.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(72f, -108f),
                new Vector2(830f, 52f));
            eyebrow.rectTransform.pivot = new Vector2(0f, 1f);
            primaryTexts.Add(eyebrow);
            AddLocalizedText(
                localizedTexts,
                eyebrow,
                DemoTextKey.HomeEyebrow);

            Text title = CreateText(
                safe,
                "Title",
                "CALM\nSPACE",
                font,
                112,
                FontStyle.Bold,
                TextAnchor.UpperLeft,
                Paper);
            title.lineSpacing = 0.78f;
            SetAnchored(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(68f, -174f),
                new Vector2(760f, 250f));
            title.rectTransform.pivot = new Vector2(0f, 1f);
            primaryTexts.Add(title);

            Text subtitle = CreateText(
                safe,
                "Subtitle",
                "Small rituals. Satisfying order.",
                font,
                34,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                SecondaryPaper);
            SetAnchored(
                subtitle.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(74f, -438f),
                new Vector2(800f, 62f));
            subtitle.rectTransform.pivot = new Vector2(0f, 1f);
            secondaryTexts.Add(subtitle);
            AddLocalizedText(
                localizedTexts,
                subtitle,
                DemoTextKey.HomeSubtitle);

            ButtonVisual music = CreateButton(
                safe,
                "Music",
                "♪   Sound on",
                rounded,
                font,
                28,
                SagePanel);
            SetAnchored(
                music.Rect,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-64f, -92f),
                new Vector2(260f, 86f));
            music.Rect.pivot = new Vector2(1f, 1f);
            music.Button.navigation =
                new Navigation { mode = Navigation.Mode.None };
            musicButton = music.Button;
            musicText = music.Label;
            panelImages.Add(music.Background);
            primaryTexts.Add(music.Label);

            musicStateImage = CreateImage(
                music.Rect,
                "Music State",
                rounded,
                new Color(0.47f, 0.78f, 0.65f, 1f),
                false);
            SetAnchored(
                musicStateImage.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(28f, 0f),
                new Vector2(16f, 16f));
            musicStateImage.rectTransform.pivot =
                new Vector2(0f, 0.5f);

            ButtonVisual language = CreateButton(
                safe,
                "Language",
                "EN",
                rounded,
                font,
                25,
                SagePanel);
            SetAnchored(
                language.Rect,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-64f, -196f),
                new Vector2(160f, 68f));
            language.Rect.pivot = new Vector2(1f, 1f);
            language.Button.navigation =
                new Navigation { mode = Navigation.Mode.None };
            languageButton = language.Button;
            languageText = language.Label;
            panelImages.Add(language.Background);
            primaryTexts.Add(language.Label);

            Image card = CreateImage(
                safe,
                "Home Card",
                rounded,
                SagePanel,
                false);
            SetAnchored(
                card.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 62f),
                new Vector2(940f, 830f));
            card.rectTransform.pivot = new Vector2(0.5f, 0f);
            panelImages.Add(card);

            Text ritual = CreateText(
                card.rectTransform,
                "Ritual Label",
                "YOUR QUIET RITUAL",
                font,
                25,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                Coral);
            SetAnchored(
                ritual.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 742f),
                new Vector2(810f, 46f));
            primaryTexts.Add(ritual);
            AddLocalizedText(
                localizedTexts,
                ritual,
                DemoTextKey.HomeRitualLabel);

            progressText = CreateText(
                card.rectTransform,
                "Progress",
                "0 / 6 spaces restored",
                font,
                34,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                Paper);
            SetAnchored(
                progressText.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 680f),
                new Vector2(810f, 60f));
            primaryTexts.Add(progressText);

            Text mood = CreateText(
                card.rectTransform,
                "Mood Label",
                "CHOOSE A MOOD",
                font,
                23,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                SecondaryPaper);
            SetAnchored(
                mood.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 592f),
                new Vector2(810f, 44f));
            secondaryTexts.Add(mood);
            AddLocalizedText(
                localizedTexts,
                mood,
                DemoTextKey.HomeMoodLabel);

            themeBindings =
                new DemoExperienceController.ThemeButtonBinding[3];
            for (var index = 0; index < themeBindings.Length; index++)
            {
                ButtonVisual theme = CreateButton(
                    card.rectTransform,
                    "Theme " + (index + 1),
                    index == 0
                        ? "Quiet Sage"
                        : index == 1
                            ? "Blue Hour"
                            : "Soft Sunset",
                    rounded,
                    font,
                    22,
                    new Color(0.15f, 0.22f, 0.20f, 1f));
                float x = (index - 1) * 272f;
                SetAnchored(
                    theme.Rect,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(x, 466f),
                    new Vector2(250f, 136f));
                panelImages.Add(theme.Background);
                primaryTexts.Add(theme.Label);

                theme.Label.alignment = TextAnchor.MiddleRight;
                theme.Label.rectTransform.offsetMin =
                    new Vector2(82f, 8f);
                theme.Label.rectTransform.offsetMax =
                    new Vector2(-14f, -8f);

                Image selected = CreateImage(
                    theme.Rect,
                    "Selected",
                    rounded,
                    new Color(1f, 1f, 1f, 0.20f),
                    false);
                Stretch(
                    selected.rectTransform,
                    new Vector2(-7f, -7f),
                    new Vector2(7f, 7f));
                selected.transform.SetAsFirstSibling();

                Image swatch = CreateImage(
                    theme.Rect,
                    "Swatch",
                    rounded,
                    index == 0
                        ? Coral
                        : index == 1
                            ? new Color(0.40f, 0.84f, 0.82f, 1f)
                            : new Color(1f, 0.62f, 0.41f, 1f),
                    false);
                SetAnchored(
                    swatch.rectTransform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(22f, 0f),
                    new Vector2(54f, 76f));
                swatch.rectTransform.pivot =
                    new Vector2(0f, 0.5f);

                var binding =
                    new DemoExperienceController.ThemeButtonBinding();
                SetPrivateField(binding, "_button", theme.Button);
                SetPrivateField(binding, "_swatch", swatch);
                SetPrivateField(binding, "_label", theme.Label);
                SetPrivateField(
                    binding,
                    "_selectedRoot",
                    selected.gameObject);
                themeBindings[index] = binding;
            }

            ButtonVisual play = CreateButton(
                card.rectTransform,
                "Play",
                "Begin restoring",
                rounded,
                font,
                38,
                Coral);
            SetAnchored(
                play.Rect,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 230f),
                new Vector2(810f, 150f));
            playButton = play.Button;
            accentImages.Add(play.Background);
            primaryTexts.Add(play.Label);
            AddLocalizedText(
                localizedTexts,
                play.Label,
                DemoTextKey.PlayButton);

            ButtonVisual levels = CreateButton(
                card.rectTransform,
                "Open Level Select",
                "Choose a space",
                rounded,
                font,
                32,
                new Color(0.15f, 0.23f, 0.20f, 1f));
            SetAnchored(
                levels.Rect,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 68f),
                new Vector2(810f, 118f));
            levelsButton = levels.Button;
            panelImages.Add(levels.Background);
            primaryTexts.Add(levels.Label);
            AddLocalizedText(
                localizedTexts,
                levels.Label,
                DemoTextKey.ChooseSpaceButton);
        }

        private static void BuildLevelSelectScreen(
            Transform screen,
            Sprite background,
            Sprite rounded,
            Font font,
            List<Text> primaryTexts,
            List<Text> secondaryTexts,
            List<Image> panelImages,
            List<DemoExperienceController.LocalizedTextBinding>
                localizedTexts,
            out Button backButton,
            out DemoExperienceController.LevelButtonBinding[]
                bindings)
        {
            CreateBackground(screen, background);
            Image wash = CreateImage(
                screen,
                "Dark Wash",
                null,
                new Color(0.025f, 0.04f, 0.045f, 0.64f),
                false);
            Stretch(wash.rectTransform);

            RectTransform safe =
                CreateSafeArea(screen, "Level Safe Area");
            ButtonVisual back = CreateButton(
                safe,
                "Back",
                "‹",
                rounded,
                font,
                52,
                SagePanel);
            SetAnchored(
                back.Rect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(62f, -72f),
                new Vector2(112f, 96f));
            back.Rect.pivot = new Vector2(0f, 1f);
            backButton = back.Button;
            panelImages.Add(back.Background);
            primaryTexts.Add(back.Label);

            Text title = CreateText(
                safe,
                "Title",
                "Choose a space",
                font,
                62,
                FontStyle.Bold,
                TextAnchor.UpperLeft,
                Paper);
            SetAnchored(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(72f, -212f),
                new Vector2(860f, 94f));
            title.rectTransform.pivot = new Vector2(0f, 1f);
            primaryTexts.Add(title);
            AddLocalizedText(
                localizedTexts,
                title,
                DemoTextKey.LevelSelectTitle);

            Text subtitle = CreateText(
                safe,
                "Subtitle",
                "A little order, one ritual at a time.",
                font,
                29,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                SecondaryPaper);
            SetAnchored(
                subtitle.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(74f, -308f),
                new Vector2(900f, 54f));
            subtitle.rectTransform.pivot = new Vector2(0f, 1f);
            secondaryTexts.Add(subtitle);
            AddLocalizedText(
                localizedTexts,
                subtitle,
                DemoTextKey.LevelSelectSubtitle);

            bindings =
                new DemoExperienceController.LevelButtonBinding[6];
            for (var index = 0; index < bindings.Length; index++)
            {
                var row = index / 2;
                var column = index % 2;
                ButtonVisual card = CreateButton(
                    safe,
                    "Level " + (index + 1),
                    string.Empty,
                    rounded,
                    font,
                    30,
                    SagePanel);
                float x = column == 0 ? -226f : 226f;
                float y = 570f - row * 274f;
                SetAnchored(
                    card.Rect,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(x, y),
                    new Vector2(424f, 232f));
                panelImages.Add(card.Background);
                card.Label.gameObject.SetActive(false);

                Text number = CreateText(
                    card.Rect,
                    "Number",
                    (index + 1).ToString("00"),
                    font,
                    30,
                    FontStyle.Bold,
                    TextAnchor.UpperLeft,
                    Coral);
                SetAnchored(
                    number.rectTransform,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(28f, -26f),
                    new Vector2(100f, 44f));
                number.rectTransform.pivot =
                    new Vector2(0f, 1f);

                Text name = CreateText(
                    card.Rect,
                    "Name",
                    "Level",
                    font,
                    32,
                    FontStyle.Bold,
                    TextAnchor.LowerLeft,
                    Paper);
                SetAnchored(
                    name.rectTransform,
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(28f, 27f),
                    new Vector2(330f, 94f));
                name.rectTransform.pivot =
                    new Vector2(0f, 0f);

                GameObject lockRoot =
                    CreateUiObject("Locked", card.Rect);
                Stretch(lockRoot.GetComponent<RectTransform>());
                Image lockWash = lockRoot.AddComponent<Image>();
                lockWash.sprite = rounded;
                lockWash.type = Image.Type.Sliced;
                lockWash.color =
                    new Color(0.02f, 0.03f, 0.04f, 0.50f);
                lockWash.raycastTarget = false;
                Text lockText = CreateText(
                    lockRoot.transform,
                    "Lock Label",
                    "LOCKED",
                    font,
                    22,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    SecondaryPaper);
                Stretch(lockText.rectTransform);
                AddLocalizedText(
                    localizedTexts,
                    lockText,
                    DemoTextKey.Locked);

                GameObject completedRoot =
                    CreateUiObject("Completed", card.Rect);
                RectTransform completedRect =
                    completedRoot.GetComponent<RectTransform>();
                SetAnchored(
                    completedRect,
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(-22f, -22f),
                    new Vector2(64f, 64f));
                completedRect.pivot = new Vector2(1f, 1f);
                Image completedImage =
                    completedRoot.AddComponent<Image>();
                completedImage.sprite = rounded;
                completedImage.type = Image.Type.Sliced;
                completedImage.color =
                    new Color(0.47f, 0.78f, 0.65f, 1f);
                completedImage.raycastTarget = false;
                Text check = CreateText(
                    completedRoot.transform,
                    "Check",
                    "✓",
                    font,
                    32,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    Ink);
                Stretch(check.rectTransform);

                var binding =
                    new DemoExperienceController.LevelButtonBinding();
                SetPrivateField(binding, "_button", card.Button);
                SetPrivateField(binding, "_numberText", number);
                SetPrivateField(binding, "_titleText", name);
                SetPrivateField(
                    binding,
                    "_lockRoot",
                    lockRoot);
                SetPrivateField(
                    binding,
                    "_completedRoot",
                    completedRoot);
                SetPrivateField(
                    binding,
                    "_cardImage",
                    card.Background);
                bindings[index] = binding;
            }
        }

        private static void BuildHudScreen(
            Transform screen,
            Sprite rounded,
            Font font,
            List<Text> primaryTexts,
            List<Text> secondaryTexts,
            List<Image> panelImages,
            out Button homeButton,
            out Text levelName,
            out Text progress)
        {
            RectTransform safe =
                CreateSafeArea(screen, "HUD Safe Area");
            Image bar = CreateImage(
                safe,
                "Top Bar",
                rounded,
                new Color(0.08f, 0.14f, 0.13f, 0.93f),
                false);
            SetAnchored(
                bar.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -42f),
                new Vector2(940f, 154f));
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            panelImages.Add(bar);

            ButtonVisual home = CreateButton(
                bar.rectTransform,
                "Home",
                "⌂",
                rounded,
                font,
                42,
                new Color(0.16f, 0.24f, 0.21f, 1f));
            SetAnchored(
                home.Rect,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(20f, 0f),
                new Vector2(112f, 112f));
            home.Rect.pivot = new Vector2(0f, 0.5f);
            homeButton = home.Button;
            panelImages.Add(home.Background);
            primaryTexts.Add(home.Label);

            levelName = CreateText(
                bar.rectTransform,
                "Level Name",
                "Soft Blocks",
                font,
                34,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                Paper);
            SetAnchored(
                levelName.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(162f, 0f),
                new Vector2(510f, 84f));
            levelName.rectTransform.pivot =
                new Vector2(0f, 0.5f);
            primaryTexts.Add(levelName);

            progress = CreateText(
                bar.rectTransform,
                "Progress",
                "0 / 3",
                font,
                29,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                SecondaryPaper);
            SetAnchored(
                progress.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-26f, 0f),
                new Vector2(220f, 74f));
            progress.rectTransform.pivot =
                new Vector2(1f, 0.5f);
            secondaryTexts.Add(progress);
        }

        private static void BuildCompletionScreen(
            Transform screen,
            Sprite rounded,
            Font font,
            List<Text> primaryTexts,
            List<Text> secondaryTexts,
            List<Image> accentImages,
            List<Image> panelImages,
            List<DemoExperienceController.LocalizedTextBinding>
                localizedTexts,
            out Button homeButton,
            out Button nextButton,
            out Text title,
            out Text body,
            out Text nextText)
        {
            Image dim = CreateImage(
                screen,
                "Dim",
                null,
                new Color(0.02f, 0.025f, 0.03f, 0.72f),
                true);
            Stretch(dim.rectTransform);

            RectTransform safe =
                CreateSafeArea(screen, "Completion Safe Area");
            Image card = CreateImage(
                safe,
                "Completion Card",
                rounded,
                SagePanel,
                true);
            SetAnchored(
                card.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 74f),
                new Vector2(940f, 720f));
            card.rectTransform.pivot = new Vector2(0.5f, 0f);
            panelImages.Add(card);

            Text done = CreateText(
                card.rectTransform,
                "Done",
                "RESTORED",
                font,
                24,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                Coral);
            SetAnchored(
                done.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 626f),
                new Vector2(810f, 44f));
            primaryTexts.Add(done);
            AddLocalizedText(
                localizedTexts,
                done,
                DemoTextKey.CompletionBadge);

            title = CreateText(
                card.rectTransform,
                "Title",
                "Space restored",
                font,
                54,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                Paper);
            SetAnchored(
                title.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 542f),
                new Vector2(810f, 80f));
            primaryTexts.Add(title);

            body = CreateText(
                card.rectTransform,
                "Body",
                "Everything is back in its place.",
                font,
                30,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                SecondaryPaper);
            SetAnchored(
                body.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 416f),
                new Vector2(810f, 118f));
            secondaryTexts.Add(body);

            ButtonVisual next = CreateButton(
                card.rectTransform,
                "Next",
                "Next space",
                rounded,
                font,
                36,
                Coral);
            SetAnchored(
                next.Rect,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 220f),
                new Vector2(810f, 146f));
            nextButton = next.Button;
            nextText = next.Label;
            accentImages.Add(next.Background);
            primaryTexts.Add(next.Label);

            ButtonVisual home = CreateButton(
                card.rectTransform,
                "Home",
                "Back home",
                rounded,
                font,
                30,
                new Color(0.15f, 0.23f, 0.20f, 1f));
            SetAnchored(
                home.Rect,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 66f),
                new Vector2(810f, 108f));
            homeButton = home.Button;
            panelImages.Add(home.Background);
            primaryTexts.Add(home.Label);
            AddLocalizedText(
                localizedTexts,
                home.Label,
                DemoTextKey.BackHome);
        }

        private static void BuildLoadingOverlay(
            Transform screen,
            Sprite rounded,
            Font font,
            List<Text> primaryTexts,
            List<DemoExperienceController.LocalizedTextBinding>
                localizedTexts)
        {
            Image dim = CreateImage(
                screen,
                "Dim",
                null,
                new Color(0.02f, 0.03f, 0.035f, 0.80f),
                true);
            Stretch(dim.rectTransform);

            Image pill = CreateImage(
                screen,
                "Loading Pill",
                rounded,
                SagePanel,
                true);
            SetAnchored(
                pill.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(620f, 150f));
            Text loadingText = CreateText(
                pill.rectTransform,
                "Loading Text",
                "One calm moment…",
                font,
                31,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Paper);
            Stretch(loadingText.rectTransform);
            primaryTexts.Add(loadingText);
            AddLocalizedText(
                localizedTexts,
                loadingText,
                DemoTextKey.Loading);
        }

        private static void CreateBackground(
            Transform parent,
            Sprite sprite)
        {
            Image background = CreateImage(
                parent,
                "Atelier Background",
                sprite,
                Color.white,
                false);
            Stretch(background.rectTransform);
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
        }

        private static CanvasGroup CreateScreen(
            Transform parent,
            string name)
        {
            GameObject root = CreateUiObject(name, parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            Stretch(rect);
            return root.AddComponent<CanvasGroup>();
        }

        private static RectTransform CreateSafeArea(
            Transform parent,
            string name)
        {
            GameObject root = CreateUiObject(name, parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            Stretch(rect);
            root.AddComponent<SafeAreaFitter>();
            return rect;
        }

        private static ButtonVisual CreateButton(
            Transform parent,
            string name,
            string label,
            Sprite rounded,
            Font font,
            int fontSize,
            Color color)
        {
            GameObject root = CreateUiObject(name, parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            Image image = root.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = true;

            Button button = root.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation =
                new Navigation { mode = Navigation.Mode.None };
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor =
                    new Color(1f, 1f, 1f, 0.92f),
                pressedColor =
                    new Color(0.78f, 0.78f, 0.78f, 1f),
                selectedColor = Color.white,
                disabledColor =
                    new Color(0.45f, 0.45f, 0.45f, 0.65f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            Text text = CreateText(
                rect,
                "Label",
                label,
                font,
                fontSize,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Paper);
            Stretch(
                text.rectTransform,
                new Vector2(18f, 8f),
                new Vector2(-18f, -8f));
            return new ButtonVisual(button, image, text, rect);
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            Font font,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Color color)
        {
            GameObject root = CreateUiObject(name, parent);
            Text text = root.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            return text;
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Sprite sprite,
            Color color,
            bool raycastTarget)
        {
            GameObject root = CreateUiObject(name, parent);
            Image image = root.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            image.type =
                sprite == null
                    ? Image.Type.Simple
                    : Image.Type.Sliced;
            return image;
        }

        private static GameObject CreateUiObject(
            string name,
            Transform parent)
        {
            var root = new GameObject(
                name,
                typeof(RectTransform));
            root.transform.SetParent(parent, false);
            return root;
        }

        private static void EnsureEventSystem()
        {
            var eventSystemObject =
                new GameObject("Event System");
            eventSystemObject.AddComponent<EventSystem>();
            InputSystemUIInputModule module =
                eventSystemObject.AddComponent<
                    InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private static void Stretch(
            RectTransform rect,
            Vector2? offsetMin = null,
            Vector2? offsetMax = null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin ?? Vector2.zero;
            rect.offsetMax = offsetMax ?? Vector2.zero;
        }

        private static void SetAnchored(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void AddLocalizedText(
            List<DemoExperienceController.LocalizedTextBinding> bindings,
            Text text,
            DemoTextKey key)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var binding =
                new DemoExperienceController.LocalizedTextBinding();
            SetPrivateField(binding, "_text", text);
            SetPrivateField(binding, "_key", key);
            bindings.Add(binding);
        }

        private static void SetObjectReference(
            Object target,
            string propertyName,
            Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property =
                serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new MissingFieldException(
                    target.GetType().FullName,
                    propertyName);
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(
                    target.GetType().FullName,
                    fieldName);
            }

            field.SetValue(target, value);
            if (target is Object unityObject)
            {
                EditorUtility.SetDirty(unityObject);
            }
        }

        private readonly struct ButtonVisual
        {
            public ButtonVisual(
                Button button,
                Image background,
                Text label,
                RectTransform rect)
            {
                Button = button;
                Background = background;
                Label = label;
                Rect = rect;
            }

            public Button Button { get; }

            public Image Background { get; }

            public Text Label { get; }

            public RectTransform Rect { get; }
        }
    }
}
