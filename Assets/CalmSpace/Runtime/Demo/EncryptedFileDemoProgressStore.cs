using System;
using System.IO;
using System.Text;
using CalmSpace.Monetization;
using UnityEngine;

namespace CalmSpace.Demo
{
    /// <summary>
    /// Authenticated, encrypted player profile for level unlocks, Cozy Tokens,
    /// and room inventory. Every mutation is persisted before it becomes
    /// visible, so a failed write cannot spend tokens or unlock an item only
    /// in memory.
    /// </summary>
    public sealed class EncryptedFileDemoProgressStore :
        IDemoProgressStore
    {
        public const string DefaultMigrationMarker =
            "calmspace.demo.secure-profile-migrated.v1";

        private const int CurrentSchemaVersion = 1;
        private const int MaximumProtectedFileBytes = 64 * 1024;

        private readonly string _filePath;
        private readonly string _temporaryPath;
        private readonly string _backupPath;
        private readonly IAuthenticatedDataProtector _protector;
        private readonly byte[] _associatedData;
        private readonly string _legacyPlayerPrefsKey;
        private readonly string _migrationMarker;
        private string _defaultThemeId = string.Empty;

        public EncryptedFileDemoProgressStore(
            string filePath,
            IAuthenticatedDataProtector protector,
            byte[] associatedData,
            string legacyPlayerPrefsKey =
                PlayerPrefsDemoProgressStore.DefaultPlayerPrefsKey,
            string migrationMarker = DefaultMigrationMarker)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "A profile file path is required.",
                    nameof(filePath));
            }

            _protector = protector ??
                throw new ArgumentNullException(nameof(protector));
            if (associatedData == null ||
                associatedData.Length == 0)
            {
                throw new ArgumentException(
                    "Associated data is required.",
                    nameof(associatedData));
            }

            _filePath = filePath;
            _temporaryPath = filePath + ".tmp";
            _backupPath = filePath + ".bak";
            _associatedData =
                (byte[])associatedData.Clone();
            _legacyPlayerPrefsKey =
                legacyPlayerPrefsKey ??
                string.Empty;
            _migrationMarker = migrationMarker ??
                DefaultMigrationMarker;
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

            bool hasSecureFile =
                File.Exists(_filePath) ||
                File.Exists(_temporaryPath) ||
                File.Exists(_backupPath);
            DemoProgressSnapshot loaded;
            bool loadedSecure = TryLoadSecure(out loaded);

            if (!loadedSecure)
            {
                loaded = ShouldImportLegacy(hasSecureFile)
                    ? ImportLegacy(completionReward)
                    : DemoProgressRules.CreateDefault(
                        LevelCount,
                        _defaultThemeId);
            }

            Current = DemoProgressRules.ReconcileCompletionRewards(
                DemoProgressRules.Normalize(
                    loaded,
                    LevelCount,
                    _defaultThemeId),
                LevelCount,
                _defaultThemeId,
                completionReward);
            IsInitialized = true;

            if (TryPersist(Current))
            {
                PlayerPrefs.SetInt(_migrationMarker, 1);
                PlayerPrefs.Save();
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
            TryApply(
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
            int previousTokens = Current.CozyTokens;
            DemoProgressSnapshot next =
                DemoProgressRules.CompleteLevelWithReward(
                    Current,
                    levelIndex,
                    LevelCount,
                    _defaultThemeId,
                    rewardAmount);
            return TryApply(next)
                ? Mathf.Max(
                    0,
                    next.CozyTokens - previousTokens)
                : 0;
        }

        public bool TryPurchaseAndSelectDecoration(
            int decorationIndex,
            int cost)
        {
            EnsureInitialized();
            if (!DemoProgressRules.TryPurchaseAndSelectDecoration(
                    Current,
                    decorationIndex,
                    cost,
                    LevelCount,
                    _defaultThemeId,
                    out DemoProgressSnapshot next))
            {
                return false;
            }

            return TryApply(next);
        }

        public bool TrySelectDecoration(int decorationIndex)
        {
            EnsureInitialized();
            if (!DemoProgressRules.TrySelectDecoration(
                    Current,
                    decorationIndex,
                    LevelCount,
                    _defaultThemeId,
                    out DemoProgressSnapshot next))
            {
                return false;
            }

            return TryApply(next);
        }

        public void SetSelectedTheme(string themeId)
        {
            EnsureInitialized();
            TryApply(
                DemoProgressRules.WithSelectedTheme(
                    Current,
                    themeId,
                    LevelCount,
                    _defaultThemeId));
        }

        public void SetMusicEnabled(bool enabled)
        {
            EnsureInitialized();
            TryApply(
                DemoProgressRules.WithMusicEnabled(
                    Current,
                    enabled,
                    LevelCount,
                    _defaultThemeId));
        }

        private bool TryApply(DemoProgressSnapshot next)
        {
            if (next == Current)
            {
                return true;
            }

            if (!TryPersist(next))
            {
                return false;
            }

            Current = next;
            ProgressChanged?.Invoke(Current);
            return true;
        }

        private bool TryLoadSecure(
            out DemoProgressSnapshot snapshot)
        {
            if (TryLoadCandidate(_filePath, out snapshot))
            {
                return true;
            }

            if (TryLoadCandidate(_temporaryPath, out snapshot) ||
                TryLoadCandidate(_backupPath, out snapshot))
            {
                TryPersist(snapshot);
                return true;
            }

            snapshot = default;
            return false;
        }

        private bool TryLoadCandidate(
            string path,
            out DemoProgressSnapshot snapshot)
        {
            snapshot = default;
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var file = new FileInfo(path);
                if (file.Length <= 0L ||
                    file.Length > MaximumProtectedFileBytes)
                {
                    return false;
                }

                byte[] protectedData = File.ReadAllBytes(path);
                DataUnprotectStatus status =
                    _protector.TryUnprotect(
                        protectedData,
                        _associatedData,
                        out byte[] plaintext);
                Array.Clear(
                    protectedData,
                    0,
                    protectedData.Length);
                if (status != DataUnprotectStatus.Success ||
                    plaintext == null)
                {
                    return false;
                }

                try
                {
                    string json = Encoding.UTF8.GetString(plaintext);
                    SecureProfileState state =
                        JsonUtility.FromJson<SecureProfileState>(
                            json);
                    if (state == null ||
                        state.version != CurrentSchemaVersion)
                    {
                        return false;
                    }

                    snapshot = new DemoProgressSnapshot(
                        state.highestUnlockedLevelIndex,
                        state.completedLevelMask,
                        state.selectedThemeId,
                        state.musicEnabled,
                        state.cozyTokens,
                        state.rewardedLevelMask,
                        state.ownedDecorationMask,
                        state.selectedDecorationIndex);
                    return true;
                }
                finally
                {
                    Array.Clear(plaintext, 0, plaintext.Length);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Calm Space ignored an unreadable secure profile: " +
                    exception.GetType().Name);
                return false;
            }
        }

        private bool TryPersist(DemoProgressSnapshot snapshot)
        {
            byte[] plaintext = null;
            byte[] protectedData = null;
            try
            {
                string directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var state = new SecureProfileState
                {
                    version = CurrentSchemaVersion,
                    highestUnlockedLevelIndex =
                        snapshot.HighestUnlockedLevelIndex,
                    completedLevelMask =
                        snapshot.CompletedLevelMask,
                    selectedThemeId = snapshot.SelectedThemeId,
                    musicEnabled = snapshot.MusicEnabled,
                    cozyTokens = snapshot.CozyTokens,
                    rewardedLevelMask =
                        snapshot.RewardedLevelMask,
                    ownedDecorationMask =
                        snapshot.OwnedDecorationMask,
                    selectedDecorationIndex =
                        snapshot.SelectedDecorationIndex
                };
                plaintext = Encoding.UTF8.GetBytes(
                    JsonUtility.ToJson(state));
                protectedData = _protector.Protect(
                    plaintext,
                    _associatedData);
                File.WriteAllBytes(_temporaryPath, protectedData);

                if (File.Exists(_filePath))
                {
                    File.Copy(
                        _filePath,
                        _backupPath,
                        true);
                    File.Delete(_filePath);
                }

                File.Move(_temporaryPath, _filePath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Calm Space could not persist the secure profile: " +
                    exception.GetType().Name);
                return false;
            }
            finally
            {
                if (plaintext != null)
                {
                    Array.Clear(plaintext, 0, plaintext.Length);
                }

                if (protectedData != null)
                {
                    Array.Clear(
                        protectedData,
                        0,
                        protectedData.Length);
                }
            }
        }

        private bool ShouldImportLegacy(bool hasSecureFile)
        {
            return
                !hasSecureFile &&
                !string.IsNullOrEmpty(_legacyPlayerPrefsKey) &&
                PlayerPrefs.GetInt(_migrationMarker, 0) == 0 &&
                PlayerPrefs.HasKey(_legacyPlayerPrefsKey);
        }

        private DemoProgressSnapshot ImportLegacy(
            int completionReward)
        {
            var legacy =
                new PlayerPrefsDemoProgressStore(
                    _legacyPlayerPrefsKey);
            legacy.Initialize(
                LevelCount,
                _defaultThemeId,
                completionReward);
            return legacy.Current;
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "Initialize the secure demo profile before use.");
            }
        }

        [Serializable]
        private sealed class SecureProfileState
        {
            public int version;
            public int highestUnlockedLevelIndex;
            public int completedLevelMask;
            public string selectedThemeId;
            public bool musicEnabled;
            public int cozyTokens;
            public int rewardedLevelMask;
            public int ownedDecorationMask;
            public int selectedDecorationIndex;
        }
    }
}
