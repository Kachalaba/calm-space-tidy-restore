using System;
using UnityEngine;

namespace CalmSpace.Demo
{
    /// <summary>
    /// Immutable, validated view of the locally persisted demo progress.
    /// A compact bit mask is sufficient for the deliberately small demo and
    /// keeps save migration deterministic.
    /// </summary>
    public readonly struct DemoProgressSnapshot :
        IEquatable<DemoProgressSnapshot>
    {
        public DemoProgressSnapshot(
            int highestUnlockedLevelIndex,
            int completedLevelMask,
            string selectedThemeId,
            bool musicEnabled)
        {
            HighestUnlockedLevelIndex = highestUnlockedLevelIndex;
            CompletedLevelMask = completedLevelMask;
            SelectedThemeId = selectedThemeId ?? string.Empty;
            MusicEnabled = musicEnabled;
        }

        public int HighestUnlockedLevelIndex { get; }

        public int CompletedLevelMask { get; }

        public string SelectedThemeId { get; }

        public bool MusicEnabled { get; }

        public bool Equals(DemoProgressSnapshot other)
        {
            return
                HighestUnlockedLevelIndex ==
                    other.HighestUnlockedLevelIndex &&
                CompletedLevelMask == other.CompletedLevelMask &&
                string.Equals(
                    SelectedThemeId,
                    other.SelectedThemeId,
                    StringComparison.Ordinal) &&
                MusicEnabled == other.MusicEnabled;
        }

        public override bool Equals(object obj)
        {
            return obj is DemoProgressSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = HighestUnlockedLevelIndex;
                hashCode =
                    (hashCode * 397) ^ CompletedLevelMask;
                hashCode =
                    (hashCode * 397) ^
                    (SelectedThemeId != null
                        ? SelectedThemeId.GetHashCode()
                        : 0);
                hashCode =
                    (hashCode * 397) ^ MusicEnabled.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(
            DemoProgressSnapshot left,
            DemoProgressSnapshot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            DemoProgressSnapshot left,
            DemoProgressSnapshot right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Pure progression rules. Keeping validation independent from PlayerPrefs
    /// makes corrupt-save recovery and unlock behavior straightforward to test.
    /// </summary>
    public static class DemoProgressRules
    {
        public const int MaximumTrackedLevels = 30;

        public static DemoProgressSnapshot CreateDefault(
            int levelCount,
            string defaultThemeId)
        {
            var normalizedCount = NormalizeLevelCount(levelCount);
            return new DemoProgressSnapshot(
                normalizedCount > 0 ? 0 : -1,
                0,
                NormalizeThemeId(defaultThemeId, string.Empty),
                true);
        }

        public static DemoProgressSnapshot Normalize(
            DemoProgressSnapshot snapshot,
            int levelCount,
            string defaultThemeId)
        {
            var normalizedCount = NormalizeLevelCount(levelCount);
            var validMask = GetValidMask(normalizedCount);
            var completedMask =
                snapshot.CompletedLevelMask & validMask;

            var highestUnlocked = normalizedCount > 0
                ? Mathf.Clamp(
                    snapshot.HighestUnlockedLevelIndex,
                    0,
                    normalizedCount - 1)
                : -1;

            if (normalizedCount > 0 && completedMask != 0)
            {
                var highestCompleted =
                    FindHighestCompletedIndex(
                        completedMask,
                        normalizedCount);
                var unlockFromCompletion =
                    Mathf.Min(
                        highestCompleted + 1,
                        normalizedCount - 1);
                highestUnlocked =
                    Mathf.Max(
                        highestUnlocked,
                        unlockFromCompletion);
            }

            return new DemoProgressSnapshot(
                highestUnlocked,
                completedMask,
                NormalizeThemeId(
                    snapshot.SelectedThemeId,
                    defaultThemeId),
                snapshot.MusicEnabled);
        }

        public static DemoProgressSnapshot MarkCompleted(
            DemoProgressSnapshot snapshot,
            int levelIndex,
            int levelCount,
            string defaultThemeId)
        {
            var normalized =
                Normalize(snapshot, levelCount, defaultThemeId);
            var normalizedCount = NormalizeLevelCount(levelCount);
            if (levelIndex < 0 || levelIndex >= normalizedCount)
            {
                return normalized;
            }

            var completedMask =
                normalized.CompletedLevelMask | (1 << levelIndex);
            var highestUnlocked =
                Mathf.Max(
                    normalized.HighestUnlockedLevelIndex,
                    Mathf.Min(
                        levelIndex + 1,
                        normalizedCount - 1));

            return new DemoProgressSnapshot(
                highestUnlocked,
                completedMask,
                normalized.SelectedThemeId,
                normalized.MusicEnabled);
        }

        public static DemoProgressSnapshot WithSelectedTheme(
            DemoProgressSnapshot snapshot,
            string themeId,
            int levelCount,
            string defaultThemeId)
        {
            var normalized =
                Normalize(snapshot, levelCount, defaultThemeId);
            return new DemoProgressSnapshot(
                normalized.HighestUnlockedLevelIndex,
                normalized.CompletedLevelMask,
                NormalizeThemeId(themeId, defaultThemeId),
                normalized.MusicEnabled);
        }

        public static DemoProgressSnapshot WithMusicEnabled(
            DemoProgressSnapshot snapshot,
            bool enabled,
            int levelCount,
            string defaultThemeId)
        {
            var normalized =
                Normalize(snapshot, levelCount, defaultThemeId);
            return new DemoProgressSnapshot(
                normalized.HighestUnlockedLevelIndex,
                normalized.CompletedLevelMask,
                normalized.SelectedThemeId,
                enabled);
        }

        public static bool IsLevelUnlocked(
            DemoProgressSnapshot snapshot,
            int levelIndex,
            int levelCount)
        {
            var normalizedCount = NormalizeLevelCount(levelCount);
            return
                levelIndex >= 0 &&
                levelIndex < normalizedCount &&
                levelIndex <= snapshot.HighestUnlockedLevelIndex;
        }

        public static bool IsLevelCompleted(
            DemoProgressSnapshot snapshot,
            int levelIndex,
            int levelCount)
        {
            var normalizedCount = NormalizeLevelCount(levelCount);
            return
                levelIndex >= 0 &&
                levelIndex < normalizedCount &&
                (snapshot.CompletedLevelMask &
                    (1 << levelIndex)) != 0;
        }

        public static int CountCompleted(
            DemoProgressSnapshot snapshot,
            int levelCount)
        {
            var remaining =
                snapshot.CompletedLevelMask &
                GetValidMask(NormalizeLevelCount(levelCount));
            var count = 0;
            while (remaining != 0)
            {
                remaining &= remaining - 1;
                count++;
            }

            return count;
        }

        public static int GetRecommendedLevel(
            DemoProgressSnapshot snapshot,
            int levelCount)
        {
            var normalizedCount = NormalizeLevelCount(levelCount);
            if (normalizedCount <= 0)
            {
                return -1;
            }

            var highestUnlocked =
                Mathf.Clamp(
                    snapshot.HighestUnlockedLevelIndex,
                    0,
                    normalizedCount - 1);
            for (var index = 0; index <= highestUnlocked; index++)
            {
                if (!IsLevelCompleted(
                        snapshot,
                        index,
                        normalizedCount))
                {
                    return index;
                }
            }

            return highestUnlocked;
        }

        public static int NormalizeLevelCount(int levelCount)
        {
            return Mathf.Clamp(
                levelCount,
                0,
                MaximumTrackedLevels);
        }

        private static int GetValidMask(int normalizedLevelCount)
        {
            return normalizedLevelCount <= 0
                ? 0
                : (1 << normalizedLevelCount) - 1;
        }

        private static int FindHighestCompletedIndex(
            int completedMask,
            int normalizedLevelCount)
        {
            for (var index = normalizedLevelCount - 1;
                 index >= 0;
                 index--)
            {
                if ((completedMask & (1 << index)) != 0)
                {
                    return index;
                }
            }

            return -1;
        }

        private static string NormalizeThemeId(
            string candidate,
            string fallback)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }

            return string.IsNullOrWhiteSpace(fallback)
                ? string.Empty
                : fallback.Trim();
        }
    }

    public interface IDemoProgressStore
    {
        event Action<DemoProgressSnapshot> ProgressChanged;

        bool IsInitialized { get; }

        int LevelCount { get; }

        DemoProgressSnapshot Current { get; }

        void Initialize(int levelCount, string defaultThemeId);

        bool IsLevelUnlocked(int levelIndex);

        bool IsLevelCompleted(int levelIndex);

        void MarkLevelCompleted(int levelIndex);

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

        private const int CurrentSchemaVersion = 1;

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
            LevelCount =
                DemoProgressRules.NormalizeLevelCount(levelCount);
            _defaultThemeId =
                string.IsNullOrWhiteSpace(defaultThemeId)
                    ? string.Empty
                    : defaultThemeId.Trim();

            var loaded = LoadOrCreateDefault();
            Current = DemoProgressRules.Normalize(
                loaded,
                LevelCount,
                _defaultThemeId);
            IsInitialized = true;

            if (loaded != Current)
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

        private DemoProgressSnapshot LoadOrCreateDefault()
        {
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
                    serialized.version != CurrentSchemaVersion)
                {
                    return fallback;
                }

                return new DemoProgressSnapshot(
                    serialized.highestUnlockedLevelIndex,
                    serialized.completedLevelMask,
                    serialized.selectedThemeId,
                    serialized.musicEnabled);
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
                musicEnabled = Current.MusicEnabled
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
        }
    }
}
