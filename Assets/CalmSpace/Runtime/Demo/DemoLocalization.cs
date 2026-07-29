using System;
using CalmSpace.Cleaning;
using CalmSpace.Levels;
using UnityEngine;

namespace CalmSpace.Demo
{
    public enum DemoLocale
    {
        English = 0,
        Ukrainian = 1
    }

    public enum DemoTextKey
    {
        HomeEyebrow = 0,
        HomeSubtitle = 1,
        HomeRitualLabel = 2,
        HomeMoodLabel = 3,
        PlayButton = 4,
        ChooseSpaceButton = 5,
        LevelSelectTitle = 6,
        LevelSelectSubtitle = 7,
        Locked = 8,
        CompletionBadge = 9,
        CompletionTitle = 10,
        CompletionFallbackBody = 11,
        NextSpace = 12,
        BackHome = 13,
        Loading = 14,
        SoundOn = 15,
        SoundOff = 16,
        CleaningPrompt = 17,
        ScrewPrompt = 18,
        ToolHands = 19,
        ToolCloth = 20,
        ToolSponge = 21,
        ToolSqueegee = 22,
        HomeDecorLabel = 23,
        DecorationBuy = 24,
        DecorationOwned = 25,
        DecorationSelected = 26,
        Undo = 27,
        RestorationStageComplete = 28,
        RestorationComplete = 29,
        ContinueRestoration = 30
    }

    public interface IDemoLocalizationService
    {
        event Action<DemoLocale> LocaleChanged;

        bool IsInitialized { get; }

        DemoLocale CurrentLocale { get; }

        string CurrentLocaleShortLabel { get; }

        void Initialize();

        bool SelectLocale(DemoLocale locale);

        void ToggleLocale();

        string Get(DemoTextKey key);

        string GetLevelName(LevelDefinition definition);

        string GetRestorationChapterName(string chapterId);

        string GetThemeName(ThemePalette palette);

        string GetDecorationName(
            DemoDecorationDefinition decoration);

        string FormatHomeProgress(int completed, int total);

        string FormatRoomCurrency(int amount);

        string FormatDecorationCost(int cost);

        string FormatCompletionReward(int amount);

        string FormatCompletionBody(
            LevelDefinition definition,
            bool includeRestorationContext = true);

        string FormatRestorationStageTitle(
            LevelDefinition definition,
            bool includeRestorationContext = true);

        string FormatCleaningProgress(int percentage);

        string FormatStageProgress(
            CleaningToolKind tool,
            int stage,
            int stageCount,
            int percentage);
    }

    /// <summary>
    /// Compact, allocation-free lookup table for the two demo languages.
    /// Stable level and theme identifiers remain authoring data while only
    /// their player-facing names are localized.
    /// </summary>
    public static class DemoLocalizationTable
    {
        public static string Get(
            DemoLocale locale,
            DemoTextKey key)
        {
            switch (locale)
            {
                case DemoLocale.Ukrainian:
                    return GetUkrainian(key);
                case DemoLocale.English:
                default:
                    return GetEnglish(key);
            }
        }

        public static string GetLevelName(
            DemoLocale locale,
            string levelId,
            string fallback)
        {
            if (locale != DemoLocale.Ukrainian)
            {
                return fallback ?? string.Empty;
            }

            switch (levelId)
            {
                case "01-soft-blocks":
                    return "М’які блоки";
                case "02-pebble-pairs":
                    return "Пари камінців";
                case "03-tea-drawer":
                    return "Чайна шухляда";
                case "04-color-shelf":
                    return "Кольорова полиця";
                case "05-fastener-tray":
                    return "Таця з кріпленнями";
                case "06-fresh-surface":
                    return "Чиста поверхня";
                case "07-cabinet-hinge":
                    return "Завіса шафи";
                case "08-dusty-window":
                    return "Запилене вікно";
                default:
                    return fallback ?? string.Empty;
            }
        }

        public static string GetThemeName(
            DemoLocale locale,
            string themeId,
            string fallback)
        {
            if (locale != DemoLocale.Ukrainian)
            {
                return fallback ?? string.Empty;
            }

            switch (themeId)
            {
                case "sage":
                    return "Тиха шавлія";
                case "ocean":
                    return "Синя година";
                case "sunset":
                    return "Ніжний захід";
                default:
                    return fallback ?? string.Empty;
            }
        }

        public static string GetRestorationChapterName(
            DemoLocale locale,
            string chapterId)
        {
            switch (chapterId)
            {
                case "cozy-workshop":
                    return locale == DemoLocale.Ukrainian
                        ? "Затишна майстерня"
                        : "Cozy Workshop";
                default:
                    return chapterId ?? string.Empty;
            }
        }

        public static string GetDecorationName(
            DemoLocale locale,
            string decorationId,
            string fallback)
        {
            if (locale != DemoLocale.Ukrainian)
            {
                return fallback ?? string.Empty;
            }

            switch (decorationId)
            {
                case "soft-fern":
                    return "Ніжна папороть";
                case "river-stones":
                    return "Річкове каміння";
                case "warm-lantern":
                    return "Теплий ліхтар";
                case "clay-vase":
                    return "Глиняна ваза";
                default:
                    return fallback ?? string.Empty;
            }
        }

        private static string GetEnglish(DemoTextKey key)
        {
            switch (key)
            {
                case DemoTextKey.HomeEyebrow:
                    return "TIDY  ·  RESTORE  ·  BREATHE";
                case DemoTextKey.HomeSubtitle:
                    return "Small rituals. Satisfying order.";
                case DemoTextKey.HomeRitualLabel:
                    return "YOUR QUIET RITUAL";
                case DemoTextKey.HomeMoodLabel:
                    return "CHOOSE A MOOD";
                case DemoTextKey.PlayButton:
                    return "Begin restoring";
                case DemoTextKey.ChooseSpaceButton:
                    return "Choose a space";
                case DemoTextKey.LevelSelectTitle:
                    return "Choose a space";
                case DemoTextKey.LevelSelectSubtitle:
                    return "A little order, one ritual at a time.";
                case DemoTextKey.Locked:
                    return "LOCKED";
                case DemoTextKey.CompletionBadge:
                    return "RESTORED";
                case DemoTextKey.CompletionTitle:
                    return "Space restored";
                case DemoTextKey.CompletionFallbackBody:
                    return "Everything is back in its place.";
                case DemoTextKey.NextSpace:
                    return "Next space";
                case DemoTextKey.BackHome:
                    return "Back home";
                case DemoTextKey.Loading:
                    return "One calm moment…";
                case DemoTextKey.SoundOn:
                    return "Sound on";
                case DemoTextKey.SoundOff:
                    return "Sound off";
                case DemoTextKey.CleaningPrompt:
                    return "Gently clean the surface";
                case DemoTextKey.ScrewPrompt:
                    return "Hold a screw to turn it out";
                case DemoTextKey.ToolHands:
                    return "Clearing";
                case DemoTextKey.ToolCloth:
                    return "Cloth";
                case DemoTextKey.ToolSponge:
                    return "Sponge";
                case DemoTextKey.ToolSqueegee:
                    return "Squeegee";
                case DemoTextKey.HomeDecorLabel:
                    return "YOUR CALM COLLECTION";
                case DemoTextKey.DecorationBuy:
                    return "BUY";
                case DemoTextKey.DecorationOwned:
                    return "OWNED";
                case DemoTextKey.DecorationSelected:
                    return "SELECTED";
                case DemoTextKey.Undo:
                    return "Undo";
                case DemoTextKey.RestorationStageComplete:
                    return "Restoration step complete";
                case DemoTextKey.RestorationComplete:
                    return "Restoration complete";
                case DemoTextKey.ContinueRestoration:
                    return "Continue restoring";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(key),
                        key,
                        "Unsupported demo localization key.");
            }
        }

        private static string GetUkrainian(DemoTextKey key)
        {
            switch (key)
            {
                case DemoTextKey.HomeEyebrow:
                    return "УПОРЯДКУЙ  ·  ВІДНОВИ  ·  ВИДИХНИ";
                case DemoTextKey.HomeSubtitle:
                    return "Малі ритуали. Приємний лад.";
                case DemoTextKey.HomeRitualLabel:
                    return "ТВІЙ ТИХИЙ РИТУАЛ";
                case DemoTextKey.HomeMoodLabel:
                    return "ОБЕРИ НАСТРІЙ";
                case DemoTextKey.PlayButton:
                    return "Почати відновлення";
                case DemoTextKey.ChooseSpaceButton:
                    return "Обрати простір";
                case DemoTextKey.LevelSelectTitle:
                    return "Обрати простір";
                case DemoTextKey.LevelSelectSubtitle:
                    return "Трохи ладу — один ритуал за раз.";
                case DemoTextKey.Locked:
                    return "ЗАКРИТО";
                case DemoTextKey.CompletionBadge:
                    return "ВІДНОВЛЕНО";
                case DemoTextKey.CompletionTitle:
                    return "Лад відновлено";
                case DemoTextKey.CompletionFallbackBody:
                    return "Усе знову на своєму місці.";
                case DemoTextKey.NextSpace:
                    return "Наступний простір";
                case DemoTextKey.BackHome:
                    return "На головну";
                case DemoTextKey.Loading:
                    return "Мить спокою…";
                case DemoTextKey.SoundOn:
                    return "Звук увімкнено";
                case DemoTextKey.SoundOff:
                    return "Звук вимкнено";
                case DemoTextKey.CleaningPrompt:
                    return "Ніжно очисть поверхню";
                case DemoTextKey.ScrewPrompt:
                    return "Утримуй гвинт, щоб викрутити";
                case DemoTextKey.ToolHands:
                    return "Прибирання";
                case DemoTextKey.ToolCloth:
                    return "Серветка";
                case DemoTextKey.ToolSponge:
                    return "Губка";
                case DemoTextKey.ToolSqueegee:
                    return "Водозгін";
                case DemoTextKey.HomeDecorLabel:
                    return "ТВОЯ КОЛЕКЦІЯ СПОКОЮ";
                case DemoTextKey.DecorationBuy:
                    return "ПРИДБАТИ";
                case DemoTextKey.DecorationOwned:
                    return "ПРИДБАНО";
                case DemoTextKey.DecorationSelected:
                    return "ОБРАНО";
                case DemoTextKey.Undo:
                    return "Скасувати";
                case DemoTextKey.RestorationStageComplete:
                    return "Етап відновлення завершено";
                case DemoTextKey.RestorationComplete:
                    return "Відновлення завершено";
                case DemoTextKey.ContinueRestoration:
                    return "Продовжити відновлення";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(key),
                        key,
                        "Unsupported demo localization key.");
            }
        }
    }

    /// <summary>
    /// Owns the current demo language. A dedicated PlayerPrefs key keeps the
    /// preference backward-compatible with the existing progress schema.
    /// </summary>
    public sealed class DemoLocalizationService :
        IDemoLocalizationService
    {
        public const string DefaultPlayerPrefsKey =
            "calmspace.demo.locale.v1";

        private readonly string _playerPrefsKey;
        private readonly SystemLanguage _systemLanguage;

        public DemoLocalizationService()
            : this(
                DefaultPlayerPrefsKey,
                Application.systemLanguage)
        {
        }

        public DemoLocalizationService(
            string playerPrefsKey,
            SystemLanguage systemLanguage)
        {
            if (string.IsNullOrWhiteSpace(playerPrefsKey))
            {
                throw new ArgumentException(
                    "A non-empty PlayerPrefs key is required.",
                    nameof(playerPrefsKey));
            }

            _playerPrefsKey = playerPrefsKey;
            _systemLanguage = systemLanguage;
        }

        public event Action<DemoLocale> LocaleChanged;

        public bool IsInitialized { get; private set; }

        public DemoLocale CurrentLocale { get; private set; }

        public string CurrentLocaleShortLabel =>
            CurrentLocale == DemoLocale.Ukrainian
                ? "УКР"
                : "EN";

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            CurrentLocale = ResolveInitialLocale();
            IsInitialized = true;
        }

        public bool SelectLocale(DemoLocale locale)
        {
            EnsureInitialized();
            if (locale != DemoLocale.English &&
                locale != DemoLocale.Ukrainian)
            {
                return false;
            }

            if (CurrentLocale == locale)
            {
                return true;
            }

            CurrentLocale = locale;
            SaveCurrent();
            LocaleChanged?.Invoke(CurrentLocale);
            return true;
        }

        public void ToggleLocale()
        {
            SelectLocale(
                CurrentLocale == DemoLocale.English
                    ? DemoLocale.Ukrainian
                    : DemoLocale.English);
        }

        public string Get(DemoTextKey key)
        {
            EnsureInitialized();
            return DemoLocalizationTable.Get(CurrentLocale, key);
        }

        public string GetLevelName(LevelDefinition definition)
        {
            EnsureInitialized();
            if (definition == null)
            {
                return string.Empty;
            }

            return DemoLocalizationTable.GetLevelName(
                CurrentLocale,
                definition.LevelId,
                definition.DisplayName);
        }

        public string GetRestorationChapterName(string chapterId)
        {
            EnsureInitialized();
            return DemoLocalizationTable.GetRestorationChapterName(
                CurrentLocale,
                chapterId);
        }

        public string GetThemeName(ThemePalette palette)
        {
            EnsureInitialized();
            if (palette == null)
            {
                return string.Empty;
            }

            return DemoLocalizationTable.GetThemeName(
                CurrentLocale,
                palette.Id,
                palette.DisplayName);
        }

        public string GetDecorationName(
            DemoDecorationDefinition decoration)
        {
            EnsureInitialized();
            if (decoration == null)
            {
                return string.Empty;
            }

            return DemoLocalizationTable.GetDecorationName(
                CurrentLocale,
                decoration.Id,
                decoration.DisplayName);
        }

        public string FormatHomeProgress(
            int completed,
            int total)
        {
            EnsureInitialized();
            return CurrentLocale == DemoLocale.Ukrainian
                ? "Відновлено: " + completed + " із " + total
                : completed + " / " + total + " spaces restored";
        }

        public string FormatRoomCurrency(int amount)
        {
            EnsureInitialized();
            var normalized = Mathf.Max(0, amount);
            return CurrentLocale == DemoLocale.Ukrainian
                ? "ЖЕТОНИ ЗАТИШКУ · " + normalized
                : "COZY TOKENS · " + normalized;
        }

        public string FormatDecorationCost(int cost)
        {
            EnsureInitialized();
            var normalized = Mathf.Max(0, cost);
            return CurrentLocale == DemoLocale.Ukrainian
                ? normalized + " ЖЕТОНІВ ЗАТИШКУ"
                : normalized + " TOKENS";
        }

        public string FormatCompletionReward(int amount)
        {
            EnsureInitialized();
            var normalized = Mathf.Max(0, amount);
            return CurrentLocale == DemoLocale.Ukrainian
                ? "+" + normalized + " ЖЕТОНІВ ЗАТИШКУ"
                : "+" + normalized + " COZY TOKENS";
        }

        public string FormatCompletionBody(
            LevelDefinition definition,
            bool includeRestorationContext = true)
        {
            EnsureInitialized();
            if (definition == null)
            {
                return Get(DemoTextKey.CompletionFallbackBody);
            }

            if (includeRestorationContext &&
                definition.TryGetRestorationStage(out var stage))
            {
                string chapterName =
                    GetRestorationChapterName(stage.ChapterId);
                if (stage.IsFinalStage)
                {
                    return CurrentLocale == DemoLocale.Ukrainian
                        ? "«" + chapterName +
                          "» — повністю відновлено."
                        : chapterName + " is fully restored.";
                }

                return CurrentLocale == DemoLocale.Ukrainian
                    ? chapterName + " · етап " +
                      stage.StageNumber + " з " +
                      stage.StageCount + " завершено."
                    : chapterName + " · stage " +
                      stage.StageNumber + " of " +
                      stage.StageCount + " complete.";
            }

            string levelName = GetLevelName(definition);
            return CurrentLocale == DemoLocale.Ukrainian
                ? "«" + levelName + "» — знову в гармонії."
                : levelName + " feels calm again.";
        }

        public string FormatRestorationStageTitle(
            LevelDefinition definition,
            bool includeRestorationContext = true)
        {
            EnsureInitialized();
            if (definition == null ||
                !includeRestorationContext ||
                !definition.TryGetRestorationStage(out var stage))
            {
                return GetLevelName(definition);
            }

            string chapterName =
                GetRestorationChapterName(stage.ChapterId);
            string levelName = GetLevelName(definition);
            return chapterName + "\n" +
                   stage.StageNumber + "/" +
                   stage.StageCount + " · " +
                   levelName;
        }

        public string FormatCleaningProgress(int percentage)
        {
            EnsureInitialized();
            var clamped = Mathf.Clamp(percentage, 0, 100);
            return CurrentLocale == DemoLocale.Ukrainian
                ? "Очищено: " + clamped + "%"
                : clamped + "% clean";
        }

        public string FormatStageProgress(
            CleaningToolKind tool,
            int stage,
            int stageCount,
            int percentage)
        {
            EnsureInitialized();
            var clampedCount = Mathf.Max(1, stageCount);
            var clampedStage = Mathf.Clamp(stage, 1, clampedCount);
            var clampedPercentage = Mathf.Clamp(percentage, 0, 100);
            var toolName = Get(GetToolKey(tool));

            return CurrentLocale == DemoLocale.Ukrainian
                ? toolName + " · крок " + clampedStage + " з " +
                  clampedCount + " · " + clampedPercentage + "%"
                : toolName + " · step " + clampedStage + " of " +
                  clampedCount + " · " + clampedPercentage + "%";
        }

        private static DemoTextKey GetToolKey(CleaningToolKind tool)
        {
            switch (tool)
            {
                case CleaningToolKind.Cloth:
                    return DemoTextKey.ToolCloth;
                case CleaningToolKind.Sponge:
                    return DemoTextKey.ToolSponge;
                case CleaningToolKind.Squeegee:
                    return DemoTextKey.ToolSqueegee;
                default:
                    return DemoTextKey.ToolHands;
            }
        }

        private DemoLocale ResolveInitialLocale()
        {
            if (PlayerPrefs.HasKey(_playerPrefsKey))
            {
                var stored = PlayerPrefs.GetInt(
                    _playerPrefsKey,
                    (int)DemoLocale.English);
                if (stored == (int)DemoLocale.English ||
                    stored == (int)DemoLocale.Ukrainian)
                {
                    return (DemoLocale)stored;
                }
            }

            return _systemLanguage == SystemLanguage.Ukrainian
                ? DemoLocale.Ukrainian
                : DemoLocale.English;
        }

        private void SaveCurrent()
        {
            try
            {
                PlayerPrefs.SetInt(
                    _playerPrefsKey,
                    (int)CurrentLocale);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Calm Space could not persist the demo language: " +
                    exception.Message);
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "Initialize the demo localization service before use.");
            }
        }
    }
}
