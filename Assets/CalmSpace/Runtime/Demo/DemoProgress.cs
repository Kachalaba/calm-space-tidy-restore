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
            : this(
                highestUnlockedLevelIndex,
                completedLevelMask,
                selectedThemeId,
                musicEnabled,
                0,
                0,
                1,
                0)
        {
        }

        public DemoProgressSnapshot(
            int highestUnlockedLevelIndex,
            int completedLevelMask,
            string selectedThemeId,
            bool musicEnabled,
            int calmPoints,
            int rewardedLevelMask,
            int ownedDecorationMask,
            int selectedDecorationIndex)
        {
            HighestUnlockedLevelIndex = highestUnlockedLevelIndex;
            CompletedLevelMask = completedLevelMask;
            SelectedThemeId = selectedThemeId ?? string.Empty;
            MusicEnabled = musicEnabled;
            CalmPoints = calmPoints;
            RewardedLevelMask = rewardedLevelMask;
            OwnedDecorationMask = ownedDecorationMask;
            SelectedDecorationIndex = selectedDecorationIndex;
        }

        public int HighestUnlockedLevelIndex { get; }

        public int CompletedLevelMask { get; }

        public string SelectedThemeId { get; }

        public bool MusicEnabled { get; }

        public int CalmPoints { get; }

        public int RewardedLevelMask { get; }

        public int OwnedDecorationMask { get; }

        public int SelectedDecorationIndex { get; }

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
                MusicEnabled == other.MusicEnabled &&
                CalmPoints == other.CalmPoints &&
                RewardedLevelMask == other.RewardedLevelMask &&
                OwnedDecorationMask == other.OwnedDecorationMask &&
                SelectedDecorationIndex ==
                    other.SelectedDecorationIndex;
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
                hashCode = (hashCode * 397) ^ CalmPoints;
                hashCode = (hashCode * 397) ^ RewardedLevelMask;
                hashCode = (hashCode * 397) ^ OwnedDecorationMask;
                hashCode =
                    (hashCode * 397) ^ SelectedDecorationIndex;
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
        public const int MaximumTrackedDecorations = 4;

        private const int StarterDecorationMask = 1;
        private const int ValidDecorationMask =
            (1 << MaximumTrackedDecorations) - 1;

        public static DemoProgressSnapshot CreateDefault(
            int levelCount,
            string defaultThemeId)
        {
            var normalizedCount = NormalizeLevelCount(levelCount);
            return new DemoProgressSnapshot(
                normalizedCount > 0 ? 0 : -1,
                0,
                NormalizeThemeId(defaultThemeId, string.Empty),
                true,
                0,
                0,
                StarterDecorationMask,
                0);
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
            var rewardedLevelMask =
                snapshot.RewardedLevelMask &
                completedMask &
                validMask;
            var ownedDecorationMask =
                (snapshot.OwnedDecorationMask &
                 ValidDecorationMask) |
                StarterDecorationMask;
            var selectedDecorationIndex =
                NormalizeSelectedDecorationIndex(
                    snapshot.SelectedDecorationIndex,
                    ownedDecorationMask);

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
                snapshot.MusicEnabled,
                Mathf.Max(0, snapshot.CalmPoints),
                rewardedLevelMask,
                ownedDecorationMask,
                selectedDecorationIndex);
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
                normalized.MusicEnabled,
                normalized.CalmPoints,
                normalized.RewardedLevelMask,
                normalized.OwnedDecorationMask,
                normalized.SelectedDecorationIndex);
        }

        public static DemoProgressSnapshot CompleteLevelWithReward(
            DemoProgressSnapshot snapshot,
            int levelIndex,
            int levelCount,
            string defaultThemeId,
            int rewardAmount)
        {
            var normalized =
                Normalize(snapshot, levelCount, defaultThemeId);
            var normalizedCount = NormalizeLevelCount(levelCount);
            if (levelIndex < 0 || levelIndex >= normalizedCount)
            {
                return normalized;
            }

            DemoProgressSnapshot completed = MarkCompleted(
                normalized,
                levelIndex,
                normalizedCount,
                defaultThemeId);
            if (rewardAmount <= 0)
            {
                return completed;
            }

            var rewardBit = 1 << levelIndex;
            if ((completed.RewardedLevelMask & rewardBit) != 0)
            {
                return completed;
            }

            return new DemoProgressSnapshot(
                completed.HighestUnlockedLevelIndex,
                completed.CompletedLevelMask,
                completed.SelectedThemeId,
                completed.MusicEnabled,
                SaturatingAdd(
                    completed.CalmPoints,
                    rewardAmount),
                completed.RewardedLevelMask | rewardBit,
                completed.OwnedDecorationMask,
                completed.SelectedDecorationIndex);
        }

        public static DemoProgressSnapshot ReconcileCompletionRewards(
            DemoProgressSnapshot snapshot,
            int levelCount,
            string defaultThemeId,
            int rewardAmount)
        {
            var reconciled =
                Normalize(snapshot, levelCount, defaultThemeId);
            if (rewardAmount <= 0)
            {
                return reconciled;
            }

            var completedMask = reconciled.CompletedLevelMask;
            for (var levelIndex = 0;
                 levelIndex < NormalizeLevelCount(levelCount);
                 levelIndex++)
            {
                if ((completedMask & (1 << levelIndex)) == 0)
                {
                    continue;
                }

                reconciled = CompleteLevelWithReward(
                    reconciled,
                    levelIndex,
                    levelCount,
                    defaultThemeId,
                    rewardAmount);
            }

            return reconciled;
        }

        public static bool TryPurchaseAndSelectDecoration(
            DemoProgressSnapshot snapshot,
            int decorationIndex,
            int cost,
            int levelCount,
            string defaultThemeId,
            out DemoProgressSnapshot result)
        {
            var normalized =
                Normalize(snapshot, levelCount, defaultThemeId);
            result = normalized;
            if (!IsValidDecorationIndex(decorationIndex) ||
                cost < 0)
            {
                return false;
            }

            var decorationBit = 1 << decorationIndex;
            if ((normalized.OwnedDecorationMask &
                 decorationBit) != 0)
            {
                result = WithSelectedDecoration(
                    normalized,
                    decorationIndex);
                return true;
            }

            if (normalized.CalmPoints < cost)
            {
                return false;
            }

            result = new DemoProgressSnapshot(
                normalized.HighestUnlockedLevelIndex,
                normalized.CompletedLevelMask,
                normalized.SelectedThemeId,
                normalized.MusicEnabled,
                normalized.CalmPoints - cost,
                normalized.RewardedLevelMask,
                normalized.OwnedDecorationMask |
                    decorationBit,
                decorationIndex);
            return true;
        }

        public static bool TrySelectDecoration(
            DemoProgressSnapshot snapshot,
            int decorationIndex,
            int levelCount,
            string defaultThemeId,
            out DemoProgressSnapshot result)
        {
            var normalized =
                Normalize(snapshot, levelCount, defaultThemeId);
            result = normalized;
            if (!IsValidDecorationIndex(decorationIndex) ||
                !IsDecorationOwned(
                    normalized,
                    decorationIndex))
            {
                return false;
            }

            result = WithSelectedDecoration(
                normalized,
                decorationIndex);
            return true;
        }

        public static bool IsDecorationOwned(
            DemoProgressSnapshot snapshot,
            int decorationIndex)
        {
            return
                IsValidDecorationIndex(decorationIndex) &&
                (snapshot.OwnedDecorationMask &
                 (1 << decorationIndex)) != 0;
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
                normalized.MusicEnabled,
                normalized.CalmPoints,
                normalized.RewardedLevelMask,
                normalized.OwnedDecorationMask,
                normalized.SelectedDecorationIndex);
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
                enabled,
                normalized.CalmPoints,
                normalized.RewardedLevelMask,
                normalized.OwnedDecorationMask,
                normalized.SelectedDecorationIndex);
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

        private static bool IsValidDecorationIndex(
            int decorationIndex)
        {
            return
                decorationIndex >= 0 &&
                decorationIndex < MaximumTrackedDecorations;
        }

        private static int NormalizeSelectedDecorationIndex(
            int selectedDecorationIndex,
            int ownedDecorationMask)
        {
            if (IsValidDecorationIndex(selectedDecorationIndex) &&
                (ownedDecorationMask &
                 (1 << selectedDecorationIndex)) != 0)
            {
                return selectedDecorationIndex;
            }

            for (var index = 0;
                 index < MaximumTrackedDecorations;
                 index++)
            {
                if ((ownedDecorationMask & (1 << index)) != 0)
                {
                    return index;
                }
            }

            return 0;
        }

        private static DemoProgressSnapshot WithSelectedDecoration(
            DemoProgressSnapshot snapshot,
            int decorationIndex)
        {
            return new DemoProgressSnapshot(
                snapshot.HighestUnlockedLevelIndex,
                snapshot.CompletedLevelMask,
                snapshot.SelectedThemeId,
                snapshot.MusicEnabled,
                snapshot.CalmPoints,
                snapshot.RewardedLevelMask,
                snapshot.OwnedDecorationMask,
                decorationIndex);
        }

        private static int SaturatingAdd(int value, int addition)
        {
            if (addition <= 0)
            {
                return Mathf.Max(0, value);
            }

            var sum = (long)Mathf.Max(0, value) + addition;
            return sum >= int.MaxValue
                ? int.MaxValue
                : (int)sum;
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
