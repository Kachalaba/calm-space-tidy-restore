using System;
using UnityEngine;

namespace CalmSpace.Demo
{
    public interface IDemoProgressStore
    {
        event Action<DemoProgressSnapshot> ProgressChanged;

        bool IsInitialized { get; }

        int LevelCount { get; }

        DemoProgressSnapshot Current { get; }

        void Initialize(int levelCount, string defaultThemeId);

        void Initialize(
            int levelCount,
            string defaultThemeId,
            int completionReward);

        bool IsLevelUnlocked(int levelIndex);

        bool IsLevelCompleted(int levelIndex);

        void MarkLevelCompleted(int levelIndex);

        int CompleteLevelAndReward(
            int levelIndex,
            int rewardAmount);

        bool TryPurchaseAndSelectDecoration(
            int decorationIndex,
            int cost);

        bool TrySelectDecoration(int decorationIndex);

        void SetSelectedTheme(string themeId);

        void SetMusicEnabled(bool enabled);
    }

    /// <summary>
    /// Small local profile store for non-sensitive demo preferences and
    /// progression. Monetization entitlements intentionally remain in their
    /// separate authenticated store.
    /// </summary>
    public sealed class PlayerPrefsDemoProgressStore :
        IDemoProgressStore
    {
        public const string DefaultPlayerPrefsKey =
            "calmspace.demo.progress.v1";

        private const int LegacySchemaVersion = 1;
        private const int CurrentSchemaVersion = 2;

        private readonly string _playerPrefsKey;
        private string _defaultThemeId = string.Empty;

        public PlayerPrefsDemoProgressStore()
            : this(DefaultPlayerPrefsKey)
        {
        }

        public PlayerPrefsDemoProgressStore(string playerPrefsKey)
        {
            if (string.IsNullOrWhiteSpace(playerPrefsKey))
            {
                throw new ArgumentException(
                    "A non-empty PlayerPrefs key is required.",
                    nameof(playerPrefsKey));
            }

            _playerPrefsKey = playerPrefsKey;
        }

        public event Action<DemoProgressSnapshot> ProgressChanged;

        public bool IsInitialized { get; private set; }

        public int LevelCount { get; private set; }

        public DemoProgressSnapshot Current { get; private set; }

        public void Initialize(
            int levelCount,
            string defaultThemeId)
        {
            Initialize(
                levelCount,
                defaultThemeId,
                DemoDecorationCatalog.DefaultCompletionReward);
        }

        public void Initialize(
            int levelCount,
            string defaultThemeId,
            int completionReward)
        {
            LevelCount =
                DemoProgressRules.NormalizeLevelCount(levelCount);
            _defaultThemeId =
                string.IsNullOrWhiteSpace(defaultThemeId)
                    ? string.Empty
                    : defaultThemeId.Trim();

            DemoProgressSnapshot loaded =
                LoadOrCreateDefault(out var loadedSchemaVersion);
            DemoProgressSnapshot normalized =
                DemoProgressRules.Normalize(
                    loaded,
                    LevelCount,
                    _defaultThemeId);
            Current =
                DemoProgressRules.ReconcileCompletionRewards(
                    normalized,
                    LevelCount,
                    _defaultThemeId,
                    completionReward);
            IsInitialized = true;

            if (loadedSchemaVersion != CurrentSchemaVersion ||
                loaded != Current)
            {
                SaveCurrent();
            }
        }

        public bool IsLevelUnlocked(int levelIndex)
        {
            EnsureInitialized();
            return DemoProgressRules.IsLevelUnlocked(
                Current,
                levelIndex,
                LevelCount);
        }

        public bool IsLevelCompleted(int levelIndex)
        {
            EnsureInitialized();
            return DemoProgressRules.IsLevelCompleted(
                Current,
                levelIndex,
                LevelCount);
        }

        public void MarkLevelCompleted(int levelIndex)
        {
            EnsureInitialized();
            Apply(
                DemoProgressRules.MarkCompleted(
                    Current,
                    levelIndex,
                    LevelCount,
                    _defaultThemeId));
        }

        public int CompleteLevelAndReward(
            int levelIndex,
            int rewardAmount)
        {
            EnsureInitialized();
            var previousPoints = Current.CalmPoints;
            DemoProgressSnapshot next =
                DemoProgressRules.CompleteLevelWithReward(
                    Current,
                    levelIndex,
                    LevelCount,
                    _defaultThemeId,
                    rewardAmount);
            Apply(next);
            return Mathf.Max(
                0,
                next.CalmPoints - previousPoints);
        }

        public bool TryPurchaseAndSelectDecoration(
            int decorationIndex,
            int cost)
        {
            EnsureInitialized();
            bool succeeded =
                DemoProgressRules.TryPurchaseAndSelectDecoration(
                    Current,
                    decorationIndex,
                    cost,
                    LevelCount,
                    _defaultThemeId,
                    out DemoProgressSnapshot next);
            if (succeeded)
            {
                Apply(next);
            }

            return succeeded;
        }

        public bool TrySelectDecoration(int decorationIndex)
        {
            EnsureInitialized();
            bool succeeded =
                DemoProgressRules.TrySelectDecoration(
                    Current,
                    decorationIndex,
                    LevelCount,
                    _defaultThemeId,
                    out DemoProgressSnapshot next);
            if (succeeded)
            {
                Apply(next);
            }

            return succeeded;
        }

        public void SetSelectedTheme(string themeId)
        {
            EnsureInitialized();
            Apply(
                DemoProgressRules.WithSelectedTheme(
                    Current,
                    themeId,
                    LevelCount,
                    _defaultThemeId));
        }

        public void SetMusicEnabled(bool enabled)
        {
            EnsureInitialized();
            Apply(
                DemoProgressRules.WithMusicEnabled(
                    Current,
                    enabled,
                    LevelCount,
                    _defaultThemeId));
        }

        private DemoProgressSnapshot LoadOrCreateDefault(
            out int loadedSchemaVersion)
        {
            loadedSchemaVersion = 0;
            var fallback = DemoProgressRules.CreateDefault(
                LevelCount,
                _defaultThemeId);
            if (!PlayerPrefs.HasKey(_playerPrefsKey))
            {
                return fallback;
            }

            try
            {
                var json = PlayerPrefs.GetString(
                    _playerPrefsKey,
                    string.Empty);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return fallback;
                }

                var serialized =
                    JsonUtility.FromJson<SerializedState>(json);
                if (serialized == null ||
                    (serialized.version != LegacySchemaVersion &&
                     serialized.version != CurrentSchemaVersion))
                {
                    return fallback;
                }

                loadedSchemaVersion = serialized.version;
                if (serialized.version == LegacySchemaVersion)
                {
                    return new DemoProgressSnapshot(
                        serialized.highestUnlockedLevelIndex,
                        serialized.completedLevelMask,
                        serialized.selectedThemeId,
                        serialized.musicEnabled);
                }

                return new DemoProgressSnapshot(
                    serialized.highestUnlockedLevelIndex,
                    serialized.completedLevelMask,
                    serialized.selectedThemeId,
                    serialized.musicEnabled,
                    serialized.calmPoints,
                    serialized.rewardedLevelMask,
                    serialized.ownedDecorationMask,
                    serialized.selectedDecorationIndex);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Calm Space ignored an unreadable demo progress save: " +
                    exception.Message);
                return fallback;
            }
        }

        private void Apply(DemoProgressSnapshot next)
        {
            if (next == Current)
            {
                return;
            }

            Current = next;
            SaveCurrent();
            ProgressChanged?.Invoke(Current);
        }

        private void SaveCurrent()
        {
            var serialized = new SerializedState
            {
                version = CurrentSchemaVersion,
                highestUnlockedLevelIndex =
                    Current.HighestUnlockedLevelIndex,
                completedLevelMask = Current.CompletedLevelMask,
                selectedThemeId = Current.SelectedThemeId,
                musicEnabled = Current.MusicEnabled,
                calmPoints = Current.CalmPoints,
                rewardedLevelMask = Current.RewardedLevelMask,
                ownedDecorationMask =
                    Current.OwnedDecorationMask,
                selectedDecorationIndex =
                    Current.SelectedDecorationIndex
            };

            try
            {
                PlayerPrefs.SetString(
                    _playerPrefsKey,
                    JsonUtility.ToJson(serialized));
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Calm Space could not persist demo progress: " +
                    exception.Message);
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "Initialize the demo progress store before use.");
            }
        }

        [Serializable]
        private sealed class SerializedState
        {
            public int version;
            public int highestUnlockedLevelIndex;
            public int completedLevelMask;
            public string selectedThemeId;
            public bool musicEnabled;
            public int calmPoints;
            public int rewardedLevelMask;
            public int ownedDecorationMask;
            public int selectedDecorationIndex;
        }
    }
}
