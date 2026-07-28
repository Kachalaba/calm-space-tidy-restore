using System;
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
        CleaningPrompt = 17
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

        string GetThemeName(ThemePalette palette);

        string FormatHomeProgress(int completed, int total);

        string FormatCompletionBody(LevelDefinition definition);

        string FormatCleaningProgress(int percentage);
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

        public string FormatHomeProgress(
            int completed,
            int total)
        {
            EnsureInitialized();
            return CurrentLocale == DemoLocale.Ukrainian
                ? "Відновлено: " + completed + " із " + total
                : completed + " / " + total + " spaces restored";
        }

        public string FormatCompletionBody(
            LevelDefinition definition)
        {
            EnsureInitialized();
            if (definition == null)
            {
                return Get(DemoTextKey.CompletionFallbackBody);
            }

            var levelName = GetLevelName(definition);
            return CurrentLocale == DemoLocale.Ukrainian
                ? "«" + levelName + "» — знову в гармонії."
                : levelName + " feels calm again.";
        }

        public string FormatCleaningProgress(int percentage)
        {
            EnsureInitialized();
            var clamped = Mathf.Clamp(percentage, 0, 100);
            return CurrentLocale == DemoLocale.Ukrainian
                ? "Очищено: " + clamped + "%"
                : clamped + "% clean";
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
