using System;
using CalmSpace.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CalmSpace.Editor
{
    internal readonly struct WorkshopHomeBuildResult
    {
        public WorkshopHomeBuildResult(
            Button legacyPlay,
            Button legacyCatalog,
            Button legacyMusic,
            Button legacyLanguage,
            Text progress,
            Text currency,
            Text legacyMusicText,
            Text legacyLanguageText,
            Image musicState,
            Image backgroundWash,
            WorkshopHomeController controller,
            Text[] primaryTexts,
            Text[] secondaryTexts,
            Image[] panels,
            Image[] accents)
        {
            LegacyPlay = legacyPlay;
            LegacyCatalog = legacyCatalog;
            LegacyMusic = legacyMusic;
            LegacyLanguage = legacyLanguage;
            Progress = progress;
            Currency = currency;
            LegacyMusicText = legacyMusicText;
            LegacyLanguageText = legacyLanguageText;
            MusicState = musicState;
            BackgroundWash = backgroundWash;
            Controller = controller;
            PrimaryTexts = primaryTexts;
            SecondaryTexts = secondaryTexts;
            Panels = panels;
            Accents = accents;
        }

        public Button LegacyPlay { get; }
        public Button LegacyCatalog { get; }
        public Button LegacyMusic { get; }
        public Button LegacyLanguage { get; }
        public Text Progress { get; }
        public Text Currency { get; }
        public Text LegacyMusicText { get; }
        public Text LegacyLanguageText { get; }
        public Image MusicState { get; }
        public Image BackgroundWash { get; }
        public WorkshopHomeController Controller { get; }
        public Text[] PrimaryTexts { get; }
        public Text[] SecondaryTexts { get; }
        public Image[] Panels { get; }
        public Image[] Accents { get; }
    }

    /// <summary>
    /// Deterministic Task 8 source for the transparent living-home shell.
    /// The center remains owned by the room; this builder creates chrome only.
    /// </summary>
    internal static class CalmSpaceWorkshopSceneBuilder
    {
        private static readonly Color Paper =
            new Color(1f, 0.97f, 0.91f, 1f);
        private static readonly Color Secondary =
            new Color(0.76f, 0.82f, 0.76f, 1f);
        private static readonly Color Panel =
            new Color(0.09f, 0.16f, 0.14f, 0.94f);
        private static readonly Color Coral =
            new Color(1f, 0.47f, 0.40f, 1f);

        public static bool TryBuildHome(
            Transform screen,
            Sprite rounded,
            Font font,
            DemoRoomPresenter roomPresenter,
            out WorkshopHomeBuildResult result)
        {
            if (screen == null || rounded == null || font == null ||
                roomPresenter == null)
            {
                result = default;
                return false;
            }

            Image wash = CreateImage(screen, "Workshop Theme Wash", null,
                new Color(0.02f, 0.06f, 0.05f, 0.08f), false);
            Stretch(wash.rectTransform);

            RectTransform roomRoot = Rect("Workshop Room Center", screen);
            Stretch(roomRoot);
            roomRoot.SetAsFirstSibling();
            BuildStaticRoomFallback(roomRoot, rounded);
            SetObjectReference(roomPresenter, "_roomRoot", roomRoot.gameObject);
            SetArray(roomPresenter, "_decorationRoots", Array.Empty<GameObject>());

            RectTransform safe = Rect("Workshop Safe Area", screen);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            Image topBar = CreateImage(safe, "Compact Top Bar", rounded, Panel, false);
            Anchor(topBar.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0f, -38f), new Vector2(940f, 142f));
            topBar.rectTransform.pivot = new Vector2(0.5f, 1f);

            Text currency = Text(topBar.rectTransform, "Cozy Tokens",
                "COZY TOKENS · 0", font, 24, TextAnchor.MiddleLeft, Paper);
            Anchor(currency.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(34f, 0f), new Vector2(520f, 82f));
            currency.rectTransform.pivot = new Vector2(0f, 0.5f);

            Button settings = Button(topBar.rectTransform, "Settings", "⚙",
                rounded, font, 38, new Color(0.16f, 0.24f, 0.21f, 1f),
                out Text settingsLabel);
            Anchor(settings.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f), new Vector2(-24f, 0f),
                new Vector2(108f, 92f));
            settings.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);

            GameObject album = Root("Album Capability", topBar.rectTransform);
            GameObject daily = Root("Daily Care Capability", topBar.rectTransform);
            GameObject decor = Root("Decor Capability", topBar.rectTransform);
            GameObject relax = Root("Relax Pass Capability", topBar.rectTransform);
            album.SetActive(false);
            daily.SetActive(false);
            decor.SetActive(false);
            relax.SetActive(false);

            Button hotspot = Button(safe, "Active Restoration Hotspot", string.Empty,
                rounded, font, 1, new Color(1f, 1f, 1f, 0.001f), out _);
            Anchor(hotspot.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 120f),
                new Vector2(620f, 720f));

            Image taskCard = CreateImage(safe, "Workshop Task Card", rounded, Panel, true);
            Anchor(taskCard.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0f, 42f), new Vector2(940f, 320f));
            taskCard.rectTransform.pivot = new Vector2(0.5f, 0f);

            Text progress = Text(taskCard.rectTransform, "Workshop Progress",
                "0 / 8", font, 25, TextAnchor.MiddleLeft, Secondary);
            Anchor(progress.rectTransform, new Vector2(0f, 1f),
                new Vector2(38f, -36f), new Vector2(300f, 54f));
            progress.rectTransform.pivot = new Vector2(0f, 1f);

            Text taskTitle = Text(taskCard.rectTransform, "Task Title",
                "Clear the passage", font, 30, TextAnchor.UpperLeft, Paper);
            Anchor(taskTitle.rectTransform, new Vector2(0f, 1f),
                new Vector2(38f, -92f), new Vector2(850f, 72f));
            taskTitle.rectTransform.pivot = new Vector2(0f, 1f);
            taskTitle.resizeTextForBestFit = true;
            taskTitle.resizeTextMinSize = 24;
            taskTitle.resizeTextMaxSize = 30;

            Button primary = Button(taskCard.rectTransform, "Primary Task", "Start",
                rounded, font, 32, Coral, out Text primaryLabel);
            Anchor(primary.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0f, 98f),
                new Vector2(850f, 96f));

            Button catalog = Button(taskCard.rectTransform, "Catalog", "All spaces",
                rounded, font, 25, new Color(0.15f, 0.23f, 0.20f, 1f),
                out Text catalogLabel);
            Anchor(catalog.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0f, 26f),
                new Vector2(850f, 44f));

            RectTransform sheetRect = Rect("Settings Bottom Sheet", safe);
            Image sheetImage = sheetRect.gameObject.AddComponent<Image>();
            sheetImage.sprite = rounded;
            sheetImage.type = Image.Type.Sliced;
            sheetImage.color = Panel;
            sheetImage.raycastTarget = true;
            Anchor(sheetRect, new Vector2(0.5f, 0f), new Vector2(0f, 42f),
                new Vector2(940f, 520f));
            sheetRect.pivot = new Vector2(0.5f, 0f);
            CanvasGroup sheetGroup = sheetRect.gameObject.AddComponent<CanvasGroup>();

            Button close = Button(sheetRect, "Close Settings", "×", rounded, font,
                38, new Color(0.16f, 0.24f, 0.21f, 1f), out _);
            Anchor(close.GetComponent<RectTransform>(), new Vector2(1f, 1f),
                new Vector2(-24f, -24f), new Vector2(82f, 72f));
            close.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);

            RectTransform settingsContent = Rect("Settings Content", sheetRect);
            Stretch(settingsContent);
            Button music = SettingsButton(settingsContent, "Music Setting", rounded, font,
                330f, out Text musicLabel);
            Button locale = SettingsButton(settingsContent, "Locale Setting", rounded, font,
                220f, out Text localeLabel);
            Button haptic = SettingsButton(settingsContent, "Haptic Setting", rounded, font,
                110f, out Text hapticLabel);

            RectTransform recoveryContent = Rect(
                "Recovery Content",
                sheetRect);
            Stretch(recoveryContent);
            Text recoveryTitle = Text(
                recoveryContent,
                "Failure Title",
                "This space needs one calm moment.",
                font,
                32,
                TextAnchor.MiddleCenter,
                Paper);
            Anchor(recoveryTitle.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0f, -150f), new Vector2(820f, 128f));
            Button sheetAction = Button(
                recoveryContent,
                "Sheet Action",
                "Try again",
                rounded,
                font,
                30,
                Coral,
                out Text sheetActionLabel);
            Anchor(sheetAction.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 92f),
                new Vector2(820f, 104f));
            sheetAction.gameObject.SetActive(false);
            recoveryContent.gameObject.SetActive(false);

            WorkshopBottomSheet bottomSheet =
                sheetRect.gameObject.AddComponent<WorkshopBottomSheet>();
            bottomSheet.Configure(
                sheetGroup,
                close,
                sheetAction,
                settingsContent.gameObject,
                recoveryContent.gameObject,
                recoveryTitle,
                sheetActionLabel);

            WorkshopHomeView view = screen.gameObject.AddComponent<WorkshopHomeView>();
            view.Configure(primary, catalog, settings, hotspot, album, decor, daily,
                relax, primaryLabel, taskTitle, progress, bottomSheet);
            view.ConfigureSettings(music, locale, haptic, musicLabel, localeLabel,
                hapticLabel);
            view.ConfigureChrome(catalogLabel);
            WorkshopHomeController controller =
                screen.gameObject.AddComponent<WorkshopHomeController>();
            SetObjectReference(controller, "_view", view);

            RectTransform legacy = Rect("Hidden Legacy Home Controls", screen);
            legacy.gameObject.SetActive(false);
            Button legacyPlay = Button(legacy, "Legacy Play", string.Empty, rounded,
                font, 1, Color.clear, out _);
            Button legacyCatalog = Button(legacy, "Legacy Catalog", string.Empty,
                rounded, font, 1, Color.clear, out _);
            Button legacyMusic = Button(legacy, "Legacy Music", string.Empty, rounded,
                font, 1, Color.clear, out Text legacyMusicText);
            Button legacyLanguage = Button(legacy, "Legacy Language", string.Empty,
                rounded, font, 1, Color.clear, out Text legacyLanguageText);
            Image musicState = CreateImage(legacy, "Legacy Music State", rounded,
                Color.clear, false);

            result = new WorkshopHomeBuildResult(
                legacyPlay, legacyCatalog, legacyMusic, legacyLanguage,
                progress, currency, legacyMusicText, legacyLanguageText,
                musicState, wash, controller,
                new[] { currency, taskTitle, primaryLabel, settingsLabel },
                new[] { progress },
                new[] { topBar, taskCard, sheetImage },
                new[] { primary.GetComponent<Image>() });
            return true;
        }

        private static void BuildStaticRoomFallback(
            RectTransform roomRoot,
            Sprite rounded)
        {
            Image paper = CreateImage(
                roomRoot,
                "Generated Paper Wall",
                null,
                new Color(0.78f, 0.71f, 0.58f, 1f),
                false);
            Stretch(paper.rectTransform);

            Image floor = CreateImage(
                roomRoot,
                "Generated Wood Floor",
                null,
                new Color(0.24f, 0.15f, 0.10f, 1f),
                false);
            floor.rectTransform.anchorMin = Vector2.zero;
            floor.rectTransform.anchorMax = new Vector2(1f, 0.38f);
            floor.rectTransform.offsetMin = Vector2.zero;
            floor.rectTransform.offsetMax = Vector2.zero;

            Image window = CreateImage(
                roomRoot,
                "Generated Quiet Window",
                rounded,
                new Color(0.50f, 0.68f, 0.66f, 0.72f),
                false);
            Anchor(window.rectTransform, new Vector2(0.72f, 0.66f),
                Vector2.zero, new Vector2(250f, 380f));

            Image workbench = CreateImage(
                roomRoot,
                "Generated Wooden Workbench",
                rounded,
                new Color(0.36f, 0.21f, 0.12f, 1f),
                false);
            Anchor(workbench.rectTransform, new Vector2(0.42f, 0.42f),
                Vector2.zero, new Vector2(580f, 190f));

            Image shelf = CreateImage(
                roomRoot,
                "Generated Empty Shelf",
                rounded,
                new Color(0.31f, 0.18f, 0.11f, 1f),
                false);
            Anchor(shelf.rectTransform, new Vector2(0.30f, 0.68f),
                Vector2.zero, new Vector2(360f, 54f));
        }

        private static Button SettingsButton(
            Transform parent,
            string name,
            Sprite rounded,
            Font font,
            float y,
            out Text label)
        {
            Button button = Button(parent, name, name, rounded, font, 26,
                new Color(0.15f, 0.23f, 0.20f, 1f), out label);
            Anchor(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                new Vector2(0f, y), new Vector2(820f, 88f));
            return button;
        }

        private static Button Button(
            Transform parent, string name, string value, Sprite rounded,
            Font font, int fontSize, Color color, out Text label)
        {
            GameObject root = Root(name, parent);
            Image image = root.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            Button button = root.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            label = Text(root.transform, "Label", value, font, fontSize,
                TextAnchor.MiddleCenter, Paper);
            Stretch(label.rectTransform, new Vector2(16f, 6f),
                new Vector2(-16f, -6f));
            return button;
        }

        private static Text Text(
            Transform parent, string name, string value, Font font,
            int fontSize, TextAnchor alignment, Color color)
        {
            Text text = Root(name, parent).AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Image CreateImage(
            Transform parent, string name, Sprite sprite, Color color,
            bool raycast)
        {
            Image image = Root(name, parent).AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite == null ? Image.Type.Simple : Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            return Root(name, parent).GetComponent<RectTransform>();
        }

        private static GameObject Root(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            return root;
        }

        private static void Anchor(
            RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(
            RectTransform rect, Vector2? min = null, Vector2? max = null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = min ?? Vector2.zero;
            rect.offsetMax = max ?? Vector2.zero;
        }

        private static void SetObjectReference(
            UnityEngine.Object target, string fieldName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetArray(
            UnityEngine.Object target, string fieldName, GameObject[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
