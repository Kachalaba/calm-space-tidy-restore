using System;
using UnityEngine;

namespace CalmSpace.Demo
{
    /// <summary>
    /// Read-only bridge for the pre-encrypted PlayerPrefs profile. The encrypted
    /// store is the only writer; this reader exists solely for one-time import.
    /// </summary>
    public sealed class PlayerPrefsDemoProgressStore
    {
        public const string DefaultPlayerPrefsKey =
            "calmspace.demo.progress.v1";

        private const int LegacySchemaVersion = 1;
        private const int CurrentSchemaVersion = 2;

        private readonly string _playerPrefsKey;

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

        public bool TryRead(
            int levelCount,
            string defaultThemeId,
            int completionReward,
            out DemoProgressSnapshot snapshot)
        {
            snapshot = default;
            if (!PlayerPrefs.HasKey(_playerPrefsKey))
            {
                return false;
            }

            try
            {
                string json = PlayerPrefs.GetString(
                    _playerPrefsKey,
                    string.Empty);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                SerializedState serialized =
                    JsonUtility.FromJson<SerializedState>(json);
                if (serialized == null ||
                    (serialized.version != LegacySchemaVersion &&
                     serialized.version != CurrentSchemaVersion))
                {
                    return false;
                }

                var versionOne = new SecureProfileV1Dto
                {
                    version = SecureProfileCodec.VersionOne,
                    highestUnlockedLevelIndex =
                        serialized.highestUnlockedLevelIndex,
                    completedLevelMask =
                        serialized.completedLevelMask,
                    selectedThemeId = serialized.selectedThemeId,
                    musicEnabled = serialized.musicEnabled,
                    cozyTokens = serialized.version ==
                        CurrentSchemaVersion
                            ? serialized.calmPoints
                            : 0,
                    rewardedLevelMask = serialized.version ==
                        CurrentSchemaVersion
                            ? serialized.rewardedLevelMask
                            : 0,
                    ownedDecorationMask = serialized.version ==
                        CurrentSchemaVersion
                            ? serialized.ownedDecorationMask
                            : 1,
                    selectedDecorationIndex = serialized.version ==
                        CurrentSchemaVersion
                            ? serialized.selectedDecorationIndex
                            : 0
                };
                snapshot = LegacyProfileV1Migration.Migrate(
                    versionOne,
                    levelCount,
                    defaultThemeId);
                if (serialized.version == LegacySchemaVersion)
                {
                    snapshot =
                        DemoProgressRules.ReconcileCompletionRewards(
                            snapshot,
                            levelCount,
                            defaultThemeId,
                            completionReward);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Calm Space ignored an unreadable legacy profile: " +
                    exception.GetType().Name);
                snapshot = default;
                return false;
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
