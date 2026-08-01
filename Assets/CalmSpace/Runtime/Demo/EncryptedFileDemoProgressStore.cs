using System;
using System.IO;
using System.Security.Cryptography;
using CalmSpace.Monetization;
using CalmSpace.Workshop;
using UnityEngine;

namespace CalmSpace.Demo
{
    /// <summary>
    /// The single writable player-profile owner. Candidate snapshots become
    /// visible only after their authenticated atomic write has completed.
    /// </summary>
    public sealed class EncryptedFileDemoProgressStore :
        IDemoProgressStore
    {
        public const string DefaultMigrationMarker =
            "calmspace.demo.secure-profile-migrated.v1";

        private const long MaximumProtectedFileBytes = 64 * 1024;

        private readonly string _filePath;
        private readonly string _temporaryPath;
        private readonly string _backupPath;
        private readonly string _recoveryStagingPath;
        private readonly IAuthenticatedDataProtector _protector;
        private readonly byte[] _associatedData;
        private readonly string _legacyPlayerPrefsKey;
        private readonly string _migrationMarker;
        private readonly IProfileFileSystem _fileSystem;
        private readonly SecureProfileCodec _codec;
        private string _defaultThemeId = string.Empty;
        private ProfileInitializationResult _initializationResult;

        public EncryptedFileDemoProgressStore(
            string filePath,
            IAuthenticatedDataProtector protector,
            byte[] associatedData,
            string legacyPlayerPrefsKey =
                PlayerPrefsDemoProgressStore.DefaultPlayerPrefsKey,
            string migrationMarker = DefaultMigrationMarker,
            IProfileFileSystem fileSystem = null,
            SecureProfileCodec codec = null)
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

            _filePath = Path.GetFullPath(filePath);
            _temporaryPath = _filePath + ".tmp";
            _backupPath = _filePath + ".bak";
            _recoveryStagingPath = _filePath + ".recovery";
            _associatedData = (byte[])associatedData.Clone();
            _legacyPlayerPrefsKey =
                legacyPlayerPrefsKey ?? string.Empty;
            _migrationMarker =
                migrationMarker ?? DefaultMigrationMarker;
            _fileSystem =
                fileSystem ?? new SystemProfileFileSystem();
            _codec = codec ?? new SecureProfileCodec();
        }

        public event Action<DemoProgressSnapshot> ProgressChanged;

        public bool IsInitialized { get; private set; }

        public int LevelCount { get; private set; }

        public DemoProgressSnapshot Current { get; private set; }

        public ProfileInitializationResult Initialize(
            int levelCount,
            string defaultThemeId)
        {
            return Initialize(
                levelCount,
                defaultThemeId,
                DemoDecorationCatalog.DefaultCompletionReward);
        }

        public ProfileInitializationResult Initialize(
            int levelCount,
            string defaultThemeId,
            int completionReward)
        {
            if (IsInitialized)
            {
                return _initializationResult;
            }

            LevelCount =
                DemoProgressRules.NormalizeLevelCount(levelCount);
            _defaultThemeId =
                string.IsNullOrWhiteSpace(defaultThemeId)
                    ? string.Empty
                    : defaultThemeId.Trim();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            DevelopmentProfileFixtureInjector
                .TryInjectFromAndroidLaunchExtra(
                    _filePath,
                    _protector,
                    _associatedData,
                    _fileSystem);
#endif

            CandidateReadResult primary =
                ReadCandidate(
                    _filePath,
                    ProfileLoadSource.Primary);
            ProfileInitializationResult result =
                ResolvePrimary(
                    primary,
                    completionReward);
            if (result.IsReady ||
                IsTerminal(result.LoadStatus))
            {
                return result;
            }

            CandidateReadResult temporary =
                ReadCandidate(
                    _temporaryPath,
                    ProfileLoadSource.Temporary);
            result = ResolveRecoveryCandidate(temporary);
            if (result.IsReady ||
                IsTerminal(result.LoadStatus))
            {
                return result;
            }

            CandidateReadResult backup =
                ReadCandidate(
                    _backupPath,
                    ProfileLoadSource.Backup);
            result = ResolveRecoveryCandidate(backup);
            if (result.IsReady ||
                IsTerminal(result.LoadStatus))
            {
                return result;
            }

            if (CanImportLegacy(primary, temporary, backup) &&
                TryImportLegacy(
                    completionReward,
                    out DemoProgressSnapshot imported))
            {
                return PersistAndPublishInitialization(
                    imported,
                    ProfileLoadStatus.LoadedV1,
                    ProfileLoadSource.LegacyPlayerPrefs,
                    wasMigrated: true);
            }

            ProfileLoadStatus exhaustedStatus =
                SelectExhaustedStatus(
                    primary,
                    temporary,
                    backup);
            DemoProgressSnapshot fallback =
                DemoProgressRules.CreateDefault(
                    LevelCount,
                    _defaultThemeId);
            return PersistAndPublishInitialization(
                fallback,
                exhaustedStatus,
                ProfileLoadSource.Default,
                wasMigrated: false);
        }

        public ProfileMutationResult<LevelCompletionMutation>
            CompleteLevel(CompleteLevelCommand command)
        {
            EnsureInitialized();
            return Commit(
                DemoProgressRules.CompleteLevel(
                    Current,
                    command,
                    LevelCount));
        }

        public ProfileMutationResult<PresentationMutation>
            MarkPresentationSeen(
                PendingPresentationEntry presentation)
        {
            EnsureInitialized();
            return Commit(
                DemoProgressRules.MarkPresentationSeen(
                    Current,
                    presentation));
        }

        public ProfileMutationResult<MemoryMutation> MarkMemoryViewed(
            string memoryId)
        {
            EnsureInitialized();
            return Commit(
                DemoProgressRules.MarkMemoryViewed(
                    Current,
                    memoryId));
        }

        public ProfileMutationResult<DecorationMutation>
            PurchaseAndSelectDecoration(
                string slotId,
                string decorationId,
                int cost)
        {
            EnsureInitialized();
            return Commit(
                DemoProgressRules.PurchaseDecoration(
                    Current,
                    slotId,
                    decorationId,
                    cost));
        }

        public ProfileMutationResult<DecorationMutation>
            GrantAndSelectDecoration(
                string slotId,
                string decorationId,
                DecorationGrantSource source)
        {
            EnsureInitialized();
            return Commit(
                DemoProgressRules.GrantDecoration(
                    Current,
                    slotId,
                    decorationId,
                    source));
        }

        public ProfileMutationResult<DecorationMutation>
            SelectDecoration(
                string slotId,
                string decorationId)
        {
            EnsureInitialized();
            if (!Current.OwnsDecoration(decorationId))
            {
                return new ProfileMutationResult<DecorationMutation>(
                    ProfileMutationStatus.Invalid,
                    Current,
                    new DecorationMutation(
                        slotId,
                        decorationId,
                        0,
                        Current.CozyTokens));
            }

            return Commit(
                DemoProgressRules.GrantDecoration(
                    Current,
                    slotId,
                    decorationId,
                    DecorationGrantSource.Migration));
        }

        public ProfileMutationResult<DailyCareMutation>
            CompleteDailyCare(
                string careId,
                int utcDayKey,
                int rewardAmount,
                string unlockedMemoryId)
        {
            EnsureInitialized();
            if (!string.IsNullOrEmpty(unlockedMemoryId) &&
                !string.Equals(
                    unlockedMemoryId,
                    WorkshopContentIds.TeaPostcardMemoryId,
                    StringComparison.Ordinal))
            {
                return new ProfileMutationResult<DailyCareMutation>(
                    ProfileMutationStatus.Invalid,
                    Current,
                    new DailyCareMutation(
                        careId,
                        utcDayKey,
                        0,
                        Current.CozyTokens,
                        Current.CompletedDailyCareCount,
                        string.Empty));
            }

            return Commit(
                DemoProgressRules.CompleteDailyCare(
                    Current,
                    careId,
                    utcDayKey,
                    rewardAmount));
        }

        public ProfileMutationResult<PreferenceMutation>
            SetSelectedTheme(string themeId)
        {
            EnsureInitialized();
            return Commit(
                DemoProgressRules.SetPreferences(
                    Current,
                    themeId,
                    Current.MusicEnabled));
        }

        public ProfileMutationResult<PreferenceMutation>
            SetMusicEnabled(bool enabled)
        {
            EnsureInitialized();
            return Commit(
                DemoProgressRules.SetPreferences(
                    Current,
                    Current.SelectedThemeId,
                    enabled));
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
            if (!TryGetLegacyBeatId(
                    levelIndex,
                    out string beatId))
            {
                return;
            }

            CompleteLevel(
                new CompleteLevelCommand(
                    beatId,
                    levelIndex,
                    0,
                    Array.Empty<PendingPresentationEntry>()));
        }

        public int CompleteLevelAndReward(
            int levelIndex,
            int rewardAmount)
        {
            EnsureInitialized();
            if (!TryGetLegacyBeatId(
                    levelIndex,
                    out string beatId))
            {
                return 0;
            }

            ProfileMutationResult<LevelCompletionMutation> result =
                CompleteLevel(
                    new CompleteLevelCommand(
                        beatId,
                        levelIndex,
                        rewardAmount,
                        Array.Empty<PendingPresentationEntry>()));
            return result.Status == ProfileMutationStatus.Applied
                ? result.Payload.RewardAmount
                : 0;
        }

        public bool TryPurchaseAndSelectDecoration(
            int decorationIndex,
            int cost)
        {
            EnsureInitialized();
            if (!TryGetLegacyDecoration(
                    decorationIndex,
                    out string slotId,
                    out string decorationId))
            {
                return false;
            }

            ProfileMutationResult<DecorationMutation> purchase =
                DemoProgressRules.PurchaseDecoration(
                    Current,
                    slotId,
                    decorationId,
                    cost);
            if (purchase.Status == ProfileMutationStatus.Invalid)
            {
                return false;
            }

            ProfileMutationResult<DecorationMutation> legacySelection =
                DemoProgressRules.GrantDecoration(
                    purchase.Snapshot,
                    DemoProgressSnapshot.LegacyDecorationSlotId,
                    decorationId,
                    DecorationGrantSource.Migration);
            ProfileMutationStatus status =
                purchase.Status == ProfileMutationStatus.Applied ||
                legacySelection.Status == ProfileMutationStatus.Applied
                    ? ProfileMutationStatus.Applied
                    : ProfileMutationStatus.AlreadyApplied;
            var mutation = new DecorationMutation(
                slotId,
                decorationId,
                purchase.Payload.TokenDelta,
                legacySelection.Snapshot.CozyTokens);

            return Commit(
                    new ProfileMutationResult<DecorationMutation>(
                        status,
                        legacySelection.Snapshot,
                        mutation))
                .IsSuccess;
        }

        public bool TrySelectDecoration(int decorationIndex)
        {
            EnsureInitialized();
            if (!TryGetLegacyDecoration(
                    decorationIndex,
                    out string slotId,
                    out string decorationId))
            {
                return false;
            }

            if (!Current.OwnsDecoration(decorationId))
            {
                return false;
            }

            ProfileMutationResult<DecorationMutation> workshopSelection =
                DemoProgressRules.GrantDecoration(
                    Current,
                    slotId,
                    decorationId,
                    DecorationGrantSource.Migration);
            ProfileMutationResult<DecorationMutation> legacySelection =
                DemoProgressRules.GrantDecoration(
                    workshopSelection.Snapshot,
                    DemoProgressSnapshot.LegacyDecorationSlotId,
                    decorationId,
                    DecorationGrantSource.Migration);
            ProfileMutationStatus status =
                workshopSelection.Status == ProfileMutationStatus.Applied ||
                legacySelection.Status == ProfileMutationStatus.Applied
                    ? ProfileMutationStatus.Applied
                    : ProfileMutationStatus.AlreadyApplied;
            var mutation = new DecorationMutation(
                slotId,
                decorationId,
                0,
                legacySelection.Snapshot.CozyTokens);

            return Commit(
                    new ProfileMutationResult<DecorationMutation>(
                        status,
                        legacySelection.Snapshot,
                        mutation))
                .IsSuccess;
        }

        private ProfileInitializationResult ResolvePrimary(
            CandidateReadResult candidate,
            int completionReward)
        {
            if (!candidate.HasSnapshot)
            {
                return Failure(candidate.Status, candidate.Source);
            }

            DemoProgressSnapshot normalized =
                DemoProgressRules.Normalize(
                    candidate.Snapshot,
                    LevelCount,
                    _defaultThemeId);
            bool needsPersist =
                candidate.Status == ProfileLoadStatus.LoadedV1 ||
                normalized != candidate.Snapshot;
            if (needsPersist)
            {
                return PersistAndPublishInitialization(
                    normalized,
                    candidate.Status,
                    candidate.Source,
                    candidate.Status ==
                        ProfileLoadStatus.LoadedV1);
            }

            return PublishInitialization(
                normalized,
                candidate.Status,
                candidate.Source,
                wasMigrated: false,
                wasPersisted: false);
        }

        private ProfileInitializationResult ResolveRecoveryCandidate(
            CandidateReadResult candidate)
        {
            if (!candidate.HasSnapshot)
            {
                return Failure(candidate.Status, candidate.Source);
            }

            DemoProgressSnapshot normalized =
                DemoProgressRules.Normalize(
                    candidate.Snapshot,
                    LevelCount,
                    _defaultThemeId);
            string stagingPath =
                candidate.Source == ProfileLoadSource.Temporary
                    ? _recoveryStagingPath
                    : _temporaryPath;
            return PersistAndPublishInitialization(
                normalized,
                candidate.Status,
                candidate.Source,
                candidate.Status == ProfileLoadStatus.LoadedV1,
                stagingPath);
        }

        private ProfileInitializationResult
            PersistAndPublishInitialization(
                DemoProgressSnapshot snapshot,
                ProfileLoadStatus loadStatus,
                ProfileLoadSource source,
                bool wasMigrated,
                string stagingPath = null)
        {
            if (!TryPersist(
                    snapshot,
                    stagingPath ?? _temporaryPath))
            {
                return Failure(ProfileLoadStatus.IoError, source);
            }

            if (source == ProfileLoadSource.Temporary)
            {
                TryDeleteFile(_temporaryPath);
            }

            MarkLegacyImportConsumed();
            return PublishInitialization(
                snapshot,
                loadStatus,
                source,
                wasMigrated,
                wasPersisted: true);
        }

        private ProfileInitializationResult PublishInitialization(
            DemoProgressSnapshot snapshot,
            ProfileLoadStatus loadStatus,
            ProfileLoadSource source,
            bool wasMigrated,
            bool wasPersisted)
        {
            Current = snapshot;
            IsInitialized = true;
            _initializationResult =
                new ProfileInitializationResult(
                    true,
                    loadStatus,
                    source,
                    wasMigrated,
                    wasPersisted,
                    snapshot);
            return _initializationResult;
        }

        private static ProfileInitializationResult Failure(
            ProfileLoadStatus status,
            ProfileLoadSource source)
        {
            return new ProfileInitializationResult(
                false,
                status,
                source,
                false,
                false,
                default);
        }

        private CandidateReadResult ReadCandidate(
            string path,
            ProfileLoadSource source)
        {
            byte[] protectedData = null;
            byte[] plaintext = null;
            try
            {
                if (_fileSystem.GetFilePresence(path) ==
                    ProfileFilePresence.Missing)
                {
                    return CandidateReadResult.Failed(
                        ProfileLoadStatus.Missing,
                        source);
                }

                long length = _fileSystem.GetFileLength(path);
                if (length <= 0L ||
                    length > MaximumProtectedFileBytes)
                {
                    return CandidateReadResult.Failed(
                        ProfileLoadStatus.InvalidData,
                        source);
                }

                protectedData = _fileSystem.ReadAllBytes(path);
                DataUnprotectStatus protectionStatus =
                    _protector.TryUnprotect(
                        protectedData,
                        _associatedData,
                        out plaintext);
                if (protectionStatus != DataUnprotectStatus.Success ||
                    plaintext == null)
                {
                    return CandidateReadResult.Failed(
                        MapProtectionFailure(protectionStatus),
                        source);
                }

                ProfileLoadStatus decodeStatus =
                    _codec.TryDecode(
                        plaintext,
                        out SecureProfileV1Dto versionOne,
                        out DemoProgressSnapshot versionTwo);
                if (decodeStatus == ProfileLoadStatus.LoadedV1)
                {
                    return CandidateReadResult.Valid(
                        decodeStatus,
                        source,
                        LegacyProfileV1Migration.Migrate(
                            versionOne,
                            LevelCount,
                            _defaultThemeId));
                }

                if (decodeStatus == ProfileLoadStatus.LoadedV2)
                {
                    return CandidateReadResult.Valid(
                        decodeStatus,
                        source,
                        versionTwo);
                }

                return CandidateReadResult.Failed(
                    decodeStatus,
                    source);
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException ||
                      exception is InvalidOperationException ||
                      exception is NotSupportedException ||
                      exception is CryptographicException)
            {
                Debug.LogWarning(
                    "Calm Space could not read an encrypted profile candidate: " +
                    exception.GetType().Name);
                return CandidateReadResult.Failed(
                    exception is CryptographicException
                        ? ProfileLoadStatus.AuthenticationFailed
                        : ProfileLoadStatus.IoError,
                    source);
            }
            finally
            {
                Clear(protectedData);
                Clear(plaintext);
            }
        }

        private bool TryPersist(DemoProgressSnapshot snapshot)
        {
            return TryPersist(snapshot, _temporaryPath);
        }

        private bool TryPersist(
            DemoProgressSnapshot snapshot,
            string stagingPath)
        {
            byte[] plaintext = null;
            byte[] protectedData = null;
            try
            {
                plaintext = _codec.EncodeV2(snapshot);
                protectedData = _protector.Protect(
                    plaintext,
                    _associatedData);
                if (protectedData == null ||
                    protectedData.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Profile protection returned no data.");
                }

                string directory =
                    Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    _fileSystem.CreateDirectory(directory);
                }

                _fileSystem.WriteAllBytesAndFlush(
                    stagingPath,
                    protectedData);
                if (_fileSystem.GetFilePresence(_filePath) ==
                    ProfileFilePresence.Exists)
                {
                    _fileSystem.Replace(
                        stagingPath,
                        _filePath,
                        _backupPath);
                }
                else
                {
                    _fileSystem.Move(
                        stagingPath,
                        _filePath);
                }

                return true;
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException ||
                      exception is InvalidOperationException ||
                      exception is NotSupportedException ||
                      exception is CryptographicException)
            {
                TryDeleteFile(stagingPath);
                Debug.LogWarning(
                    "Calm Space could not persist the encrypted profile: " +
                    exception.GetType().Name);
                return false;
            }
            finally
            {
                Clear(plaintext);
                Clear(protectedData);
            }
        }

        private ProfileMutationResult<TPayload> Commit<TPayload>(
            ProfileMutationResult<TPayload> candidate)
        {
            if (candidate.Status != ProfileMutationStatus.Applied)
            {
                return candidate;
            }

            if (!TryPersist(candidate.Snapshot))
            {
                return new ProfileMutationResult<TPayload>(
                    ProfileMutationStatus.PersistFailed,
                    Current,
                    candidate.Payload);
            }

            Current = candidate.Snapshot;
            ProgressChanged?.Invoke(Current);
            return candidate;
        }

        private bool TryImportLegacy(
            int completionReward,
            out DemoProgressSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrEmpty(_legacyPlayerPrefsKey) ||
                PlayerPrefs.GetInt(_migrationMarker, 0) != 0)
            {
                return false;
            }

            return new PlayerPrefsDemoProgressStore(
                    _legacyPlayerPrefsKey)
                .TryRead(
                    LevelCount,
                    _defaultThemeId,
                    completionReward,
                    out snapshot);
        }

        private static bool CanImportLegacy(
            CandidateReadResult primary,
            CandidateReadResult temporary,
            CandidateReadResult backup)
        {
            return
                primary.Status == ProfileLoadStatus.Missing &&
                temporary.Status == ProfileLoadStatus.Missing &&
                backup.Status == ProfileLoadStatus.Missing;
        }

        private void MarkLegacyImportConsumed()
        {
            try
            {
                PlayerPrefs.SetInt(_migrationMarker, 1);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Calm Space could not persist the legacy import marker: " +
                    exception.GetType().Name);
            }
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                if (_fileSystem.GetFilePresence(path) ==
                    ProfileFilePresence.Exists)
                {
                    _fileSystem.Delete(path);
                }
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException)
            {
                // A stale temporary is authenticated before the next recovery.
            }
        }

        private static ProfileLoadStatus SelectExhaustedStatus(
            CandidateReadResult primary,
            CandidateReadResult temporary,
            CandidateReadResult backup)
        {
            if (primary.Status != ProfileLoadStatus.Missing)
            {
                return primary.Status;
            }

            if (temporary.Status != ProfileLoadStatus.Missing)
            {
                return temporary.Status;
            }

            return backup.Status;
        }

        private static bool IsTerminal(ProfileLoadStatus status)
        {
            return
                status == ProfileLoadStatus.UnsupportedVersion ||
                status == ProfileLoadStatus.IoError;
        }

        private static ProfileLoadStatus MapProtectionFailure(
            DataUnprotectStatus status)
        {
            switch (status)
            {
                case DataUnprotectStatus.AuthenticationFailed:
                    return ProfileLoadStatus.AuthenticationFailed;
                case DataUnprotectStatus.UnsupportedVersion:
                    // Protector envelope versions are not authenticated.
                    // Only SecureProfileCodec can return terminal future
                    // schema status after authenticated unprotection.
                    return ProfileLoadStatus.InvalidData;
                default:
                    return ProfileLoadStatus.InvalidData;
            }
        }

        private static bool TryGetLegacyBeatId(
            int levelIndex,
            out string beatId)
        {
            if (!WorkshopContentIds.TryGetCozyWorkshopBeat(
                    levelIndex,
                    out WorkshopBeatContract beat))
            {
                beatId = string.Empty;
                return false;
            }

            beatId = beat.BeatId;
            return true;
        }

        private static bool TryGetLegacyDecoration(
            int decorationIndex,
            out string slotId,
            out string decorationId)
        {
            decorationId =
                DemoProgressSnapshot.GetLegacyDecorationId(
                    decorationIndex);
            if (string.IsNullOrEmpty(decorationId))
            {
                slotId = string.Empty;
                return false;
            }

            slotId = decorationIndex == 2
                ? WorkshopContentIds.WarmLightDecorSlotId
                : WorkshopContentIds.WorkbenchAccentDecorSlotId;
            return true;
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "Initialize the encrypted demo profile before use.");
            }
        }

        private static void Clear(byte[] bytes)
        {
            if (bytes != null)
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }

        private readonly struct CandidateReadResult
        {
            private CandidateReadResult(
                ProfileLoadStatus status,
                ProfileLoadSource source,
                DemoProgressSnapshot snapshot,
                bool hasSnapshot)
            {
                Status = status;
                Source = source;
                Snapshot = snapshot;
                HasSnapshot = hasSnapshot;
            }

            public ProfileLoadStatus Status { get; }

            public ProfileLoadSource Source { get; }

            public DemoProgressSnapshot Snapshot { get; }

            public bool HasSnapshot { get; }

            public static CandidateReadResult Valid(
                ProfileLoadStatus status,
                ProfileLoadSource source,
                DemoProgressSnapshot snapshot)
            {
                return new CandidateReadResult(
                    status,
                    source,
                    snapshot,
                    true);
            }

            public static CandidateReadResult Failed(
                ProfileLoadStatus status,
                ProfileLoadSource source)
            {
                return new CandidateReadResult(
                    status,
                    source,
                    default,
                    false);
            }
        }
    }
}
