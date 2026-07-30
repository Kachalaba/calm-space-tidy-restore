using System;

namespace CalmSpace.Demo
{
    public enum ProfileMutationStatus
    {
        Applied = 0,
        AlreadyApplied = 1,
        PersistFailed = 2,
        Invalid = 3
    }

    public readonly struct ProfileMutationResult<TPayload>
    {
        public ProfileMutationResult(
            ProfileMutationStatus status,
            DemoProgressSnapshot snapshot,
            TPayload payload)
        {
            Status = status;
            Snapshot = snapshot;
            Payload = payload;
        }

        public ProfileMutationStatus Status { get; }

        public DemoProgressSnapshot Snapshot { get; }

        public TPayload Payload { get; }

        public bool IsSuccess =>
            Status == ProfileMutationStatus.Applied ||
            Status == ProfileMutationStatus.AlreadyApplied;
    }

    public enum PendingPresentationKind
    {
        RoomReveal = 0,
        Memory = 1,
        Finale = 2
    }

    public readonly struct PendingPresentationEntry :
        IEquatable<PendingPresentationEntry>
    {
        private PendingPresentationEntry(
            PendingPresentationKind kind,
            string stableId,
            bool requiresExplicitLaunch)
        {
            Kind = kind;
            StableId = stableId ?? string.Empty;
            RequiresExplicitLaunch = requiresExplicitLaunch;
        }

        public PendingPresentationKind Kind { get; }

        public string StableId { get; }

        public bool RequiresExplicitLaunch { get; }

        public static PendingPresentationEntry RoomReveal(string beatId)
        {
            return new PendingPresentationEntry(
                PendingPresentationKind.RoomReveal,
                beatId,
                false);
        }

        public static PendingPresentationEntry Memory(string memoryId)
        {
            return new PendingPresentationEntry(
                PendingPresentationKind.Memory,
                memoryId,
                false);
        }

        public static PendingPresentationEntry Finale(
            string chapterId,
            bool requiresExplicitLaunch = false)
        {
            return new PendingPresentationEntry(
                PendingPresentationKind.Finale,
                chapterId,
                requiresExplicitLaunch);
        }

        public bool Equals(PendingPresentationEntry other)
        {
            return
                Kind == other.Kind &&
                string.Equals(
                    StableId,
                    other.StableId,
                    StringComparison.Ordinal) &&
                RequiresExplicitLaunch ==
                    other.RequiresExplicitLaunch;
        }

        public override bool Equals(object obj)
        {
            return obj is PendingPresentationEntry other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Kind;
                hashCode =
                    (hashCode * 397) ^
                    (StableId != null ? StableId.GetHashCode() : 0);
                hashCode =
                    (hashCode * 397) ^
                    RequiresExplicitLaunch.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(
            PendingPresentationEntry left,
            PendingPresentationEntry right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            PendingPresentationEntry left,
            PendingPresentationEntry right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct DecorationSelection
    {
        public DecorationSelection(
            string slotId,
            string decorationId)
        {
            SlotId = slotId ?? string.Empty;
            DecorationId = decorationId ?? string.Empty;
        }

        public string SlotId { get; }

        public string DecorationId { get; }
    }

    public enum DecorationGrantSource
    {
        Rewarded = 0,
        RelaxPass = 1,
        Migration = 2
    }

    public readonly struct LevelCompletionMutation
    {
        public LevelCompletionMutation(
            bool firstCompletion,
            int rewardAmount,
            int tokenBalance)
        {
            FirstCompletion = firstCompletion;
            RewardAmount = rewardAmount;
            TokenBalance = tokenBalance;
        }

        public bool FirstCompletion { get; }

        public int RewardAmount { get; }

        public int TokenBalance { get; }
    }

    public readonly struct PresentationMutation
    {
        public PresentationMutation(
            PendingPresentationEntry presentation)
        {
            Presentation = presentation;
        }

        public PendingPresentationEntry Presentation { get; }
    }

    public readonly struct MemoryMutation
    {
        public MemoryMutation(string memoryId, bool firstView)
        {
            MemoryId = memoryId ?? string.Empty;
            FirstView = firstView;
        }

        public string MemoryId { get; }

        public bool FirstView { get; }
    }

    public readonly struct DecorationMutation
    {
        public DecorationMutation(
            string slotId,
            string decorationId,
            int tokenDelta,
            int tokenBalance)
        {
            SlotId = slotId ?? string.Empty;
            DecorationId = decorationId ?? string.Empty;
            TokenDelta = tokenDelta;
            TokenBalance = tokenBalance;
        }

        public string SlotId { get; }

        public string DecorationId { get; }

        public int TokenDelta { get; }

        public int TokenBalance { get; }
    }

    public readonly struct DailyCareMutation
    {
        public DailyCareMutation(
            string careId,
            int utcDayKey,
            int rewardAmount,
            int tokenBalance,
            int completionCount,
            string unlockedMemoryId)
        {
            CareId = careId ?? string.Empty;
            UtcDayKey = utcDayKey;
            RewardAmount = rewardAmount;
            TokenBalance = tokenBalance;
            CompletionCount = completionCount;
            UnlockedMemoryId = unlockedMemoryId ?? string.Empty;
        }

        public string CareId { get; }

        public int UtcDayKey { get; }

        public int RewardAmount { get; }

        public int TokenBalance { get; }

        public int CompletionCount { get; }

        public string UnlockedMemoryId { get; }
    }

    public readonly struct PreferenceMutation
    {
        public PreferenceMutation(bool changed)
        {
            Changed = changed;
        }

        public bool Changed { get; }
    }

    public enum ProfileLoadStatus
    {
        Missing = 0,
        LoadedV1 = 1,
        LoadedV2 = 2,
        AuthenticationFailed = 3,
        InvalidData = 4,
        IoError = 5,
        UnsupportedVersion = 6
    }

    public enum ProfileLoadSource
    {
        None = 0,
        Primary = 1,
        Temporary = 2,
        Backup = 3,
        LegacyPlayerPrefs = 4,
        Default = 5
    }

    public readonly struct ProfileInitializationResult
    {
        public ProfileInitializationResult(
            bool isReady,
            ProfileLoadStatus loadStatus,
            ProfileLoadSource source,
            bool wasMigrated,
            bool wasPersisted,
            DemoProgressSnapshot snapshot)
        {
            IsReady = isReady;
            LoadStatus = loadStatus;
            Source = source;
            WasMigrated = wasMigrated;
            WasPersisted = wasPersisted;
            Snapshot = snapshot;
        }

        public bool IsReady { get; }

        public ProfileLoadStatus LoadStatus { get; }

        public ProfileLoadSource Source { get; }

        public bool WasMigrated { get; }

        public bool WasPersisted { get; }

        public DemoProgressSnapshot Snapshot { get; }
    }

    public readonly struct CompleteLevelCommand
    {
        private readonly PendingPresentationEntry[] _presentations;

        public CompleteLevelCommand(
            string levelId,
            int levelIndex,
            int rewardAmount,
            PendingPresentationEntry[] presentations)
        {
            LevelId = levelId ?? string.Empty;
            LevelIndex = levelIndex;
            RewardAmount = rewardAmount;
            _presentations = CopyPresentations(presentations);
        }

        public string LevelId { get; }

        public int LevelIndex { get; }

        public int RewardAmount { get; }

        public int PresentationCount =>
            _presentations?.Length ?? 0;

        public bool TryGetPresentation(
            int index,
            out PendingPresentationEntry entry)
        {
            if (_presentations == null ||
                index < 0 ||
                index >= _presentations.Length)
            {
                entry = default;
                return false;
            }

            entry = _presentations[index];
            return true;
        }

        private static PendingPresentationEntry[] CopyPresentations(
            PendingPresentationEntry[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<PendingPresentationEntry>();
            }

            var copy =
                new PendingPresentationEntry[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }

    public readonly partial struct DemoProgressSnapshot
    {
        internal const string LegacyDecorationSlotId =
            "legacy-room";

        private static readonly string[] EmptyIds =
            Array.Empty<string>();
        private static readonly PendingPresentationEntry[]
            EmptyPresentations =
                Array.Empty<PendingPresentationEntry>();

        private readonly string[] _seenRoomRevealIds;
        private readonly string[] _viewedMemoryIds;
        private readonly string[] _seenFinaleIds;
        private readonly PendingPresentationEntry[]
            _pendingPresentations;
        private readonly string[] _ownedDecorationIds;
        private readonly DecorationSelection[]
            _decorationSelections;

        public DemoProgressSnapshot(
            int highestUnlockedLevelIndex,
            int completedLevelMask,
            string selectedThemeId,
            bool musicEnabled,
            int cozyTokens,
            int rewardedLevelMask,
            string[] ownedDecorationIds,
            DecorationSelection[] decorationSelections,
            string[] seenRoomRevealIds,
            string[] viewedMemoryIds,
            string[] seenFinaleIds,
            PendingPresentationEntry[] pendingPresentations,
            int lastDailyCareUtcDayKey,
            int completedDailyCareCount)
        {
            HighestUnlockedLevelIndex =
                highestUnlockedLevelIndex;
            CompletedLevelMask = completedLevelMask;
            SelectedThemeId = selectedThemeId ?? string.Empty;
            MusicEnabled = musicEnabled;
            CozyTokens = cozyTokens;
            RewardedLevelMask = rewardedLevelMask;

            _ownedDecorationIds = CopyIds(ownedDecorationIds);
            _decorationSelections =
                CopySelections(decorationSelections);
            _seenRoomRevealIds = CopyIds(seenRoomRevealIds);
            _viewedMemoryIds = CopyIds(viewedMemoryIds);
            _seenFinaleIds = CopyIds(seenFinaleIds);
            _pendingPresentations =
                CopyPresentations(pendingPresentations);

            LastDailyCareUtcDayKey =
                lastDailyCareUtcDayKey;
            CompletedDailyCareCount =
                completedDailyCareCount;
            OwnedDecorationMask =
                CreateLegacyOwnedDecorationMask(
                    _ownedDecorationIds);
            SelectedDecorationIndex =
                CreateLegacySelectedDecorationIndex(
                    _decorationSelections,
                    OwnedDecorationMask);
        }

        public int SeenRoomRevealCount =>
            _seenRoomRevealIds?.Length ?? 0;

        public int ViewedMemoryCount =>
            _viewedMemoryIds?.Length ?? 0;

        public int SeenFinaleCount =>
            _seenFinaleIds?.Length ?? 0;

        public int PendingPresentationCount =>
            _pendingPresentations?.Length ?? 0;

        public int OwnedDecorationCount =>
            _ownedDecorationIds?.Length ?? 0;

        public int DecorationSelectionCount =>
            _decorationSelections?.Length ?? 0;

        public int LastDailyCareUtcDayKey { get; }

        public int CompletedDailyCareCount { get; }

        public bool HasSeenRoomReveal(string beatId)
        {
            return ContainsId(_seenRoomRevealIds, beatId);
        }

        public bool HasViewedMemory(string memoryId)
        {
            return ContainsId(_viewedMemoryIds, memoryId);
        }

        public bool HasSeenFinale(string chapterId)
        {
            return ContainsId(_seenFinaleIds, chapterId);
        }

        public bool OwnsDecoration(string decorationId)
        {
            return ContainsId(_ownedDecorationIds, decorationId);
        }

        public bool TryGetSelectedDecoration(
            string slotId,
            out string decorationId)
        {
            if (_decorationSelections != null &&
                !string.IsNullOrEmpty(slotId))
            {
                for (var index = 0;
                     index < _decorationSelections.Length;
                     index++)
                {
                    DecorationSelection selection =
                        _decorationSelections[index];
                    if (string.Equals(
                            selection.SlotId,
                            slotId,
                            StringComparison.Ordinal))
                    {
                        decorationId =
                            selection.DecorationId ??
                            string.Empty;
                        return true;
                    }
                }
            }

            decorationId = string.Empty;
            return false;
        }

        public bool TryGetPendingPresentation(
            int index,
            out PendingPresentationEntry entry)
        {
            if (_pendingPresentations == null ||
                index < 0 ||
                index >= _pendingPresentations.Length)
            {
                entry = default;
                return false;
            }

            entry = _pendingPresentations[index];
            return true;
        }

        internal string[] CopySeenRoomRevealIds()
        {
            return CopyIds(_seenRoomRevealIds);
        }

        internal string[] CopyViewedMemoryIds()
        {
            return CopyIds(_viewedMemoryIds);
        }

        internal string[] CopySeenFinaleIds()
        {
            return CopyIds(_seenFinaleIds);
        }

        internal PendingPresentationEntry[]
            CopyPendingPresentations()
        {
            return CopyPresentations(_pendingPresentations);
        }

        internal string[] CopyOwnedDecorationIds()
        {
            return CopyIds(_ownedDecorationIds);
        }

        internal DecorationSelection[] CopyDecorationSelections()
        {
            return CopySelections(_decorationSelections);
        }

        private bool EqualsV2(DemoProgressSnapshot other)
        {
            return
                LastDailyCareUtcDayKey ==
                    other.LastDailyCareUtcDayKey &&
                CompletedDailyCareCount ==
                    other.CompletedDailyCareCount &&
                IdArraysEqual(
                    _seenRoomRevealIds,
                    other._seenRoomRevealIds) &&
                IdArraysEqual(
                    _viewedMemoryIds,
                    other._viewedMemoryIds) &&
                IdArraysEqual(
                    _seenFinaleIds,
                    other._seenFinaleIds) &&
                PresentationArraysEqual(
                    _pendingPresentations,
                    other._pendingPresentations) &&
                IdArraysEqual(
                    _ownedDecorationIds,
                    other._ownedDecorationIds) &&
                SelectionArraysEqual(
                    _decorationSelections,
                    other._decorationSelections);
        }

        private int GetV2HashCode()
        {
            unchecked
            {
                var hashCode = LastDailyCareUtcDayKey;
                hashCode =
                    (hashCode * 397) ^
                    CompletedDailyCareCount;
                hashCode = HashIds(hashCode, _seenRoomRevealIds);
                hashCode = HashIds(hashCode, _viewedMemoryIds);
                hashCode = HashIds(hashCode, _seenFinaleIds);
                hashCode =
                    HashPresentations(
                        hashCode,
                        _pendingPresentations);
                hashCode = HashIds(hashCode, _ownedDecorationIds);
                hashCode =
                    HashSelections(
                        hashCode,
                        _decorationSelections);
                return hashCode;
            }
        }

        private static string[] CreateLegacyOwnedDecorationIds(
            int ownedDecorationMask)
        {
            var count = 0;
            for (var index = 0; index < 4; index++)
            {
                if ((ownedDecorationMask & (1 << index)) != 0)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return EmptyIds;
            }

            var result = new string[count];
            var destinationIndex = 0;
            for (var index = 0; index < 4; index++)
            {
                if ((ownedDecorationMask & (1 << index)) == 0)
                {
                    continue;
                }

                result[destinationIndex++] =
                    GetLegacyDecorationId(index);
            }

            return result;
        }

        private static DecorationSelection[]
            CreateLegacyDecorationSelections(
                int ownedDecorationMask,
                int selectedDecorationIndex)
        {
            if (selectedDecorationIndex < 0 ||
                selectedDecorationIndex >= 4 ||
                (ownedDecorationMask &
                 (1 << selectedDecorationIndex)) == 0)
            {
                return Array.Empty<DecorationSelection>();
            }

            return new[]
            {
                new DecorationSelection(
                    LegacyDecorationSlotId,
                    GetLegacyDecorationId(
                        selectedDecorationIndex))
            };
        }

        private static int CreateLegacyOwnedDecorationMask(
            string[] ownedDecorationIds)
        {
            var mask = 0;
            if (ownedDecorationIds == null)
            {
                return mask;
            }

            for (var index = 0;
                 index < ownedDecorationIds.Length;
                 index++)
            {
                int decorationIndex =
                    GetLegacyDecorationIndex(
                        ownedDecorationIds[index]);
                if (decorationIndex >= 0)
                {
                    mask |= 1 << decorationIndex;
                }
            }

            return mask;
        }

        private static int CreateLegacySelectedDecorationIndex(
            DecorationSelection[] selections,
            int ownedDecorationMask)
        {
            if (selections != null)
            {
                for (var index = 0;
                     index < selections.Length;
                     index++)
                {
                    if (!string.Equals(
                            selections[index].SlotId,
                            LegacyDecorationSlotId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int legacySelectedIndex =
                        GetLegacyDecorationIndex(
                            selections[index].DecorationId);
                    if (legacySelectedIndex >= 0 &&
                        (ownedDecorationMask &
                         (1 << legacySelectedIndex)) != 0)
                    {
                        return legacySelectedIndex;
                    }
                }

                for (var index = 0;
                     index < selections.Length;
                     index++)
                {
                    int selectedIndex =
                        GetLegacyDecorationIndex(
                            selections[index].DecorationId);
                    if (selectedIndex >= 0 &&
                        (ownedDecorationMask &
                         (1 << selectedIndex)) != 0)
                    {
                        return selectedIndex;
                    }
                }
            }

            for (var index = 0; index < 4; index++)
            {
                if ((ownedDecorationMask & (1 << index)) != 0)
                {
                    return index;
                }
            }

            return 0;
        }

        internal static string GetLegacyDecorationId(int index)
        {
            switch (index)
            {
                case 0:
                    return "soft-fern";
                case 1:
                    return "river-stones";
                case 2:
                    return "warm-lantern";
                case 3:
                    return "clay-vase";
                default:
                    return string.Empty;
            }
        }

        private static int GetLegacyDecorationIndex(string decorationId)
        {
            if (string.Equals(
                    decorationId,
                    "soft-fern",
                    StringComparison.Ordinal))
            {
                return 0;
            }

            if (string.Equals(
                    decorationId,
                    "river-stones",
                    StringComparison.Ordinal))
            {
                return 1;
            }

            if (string.Equals(
                    decorationId,
                    "warm-lantern",
                    StringComparison.Ordinal))
            {
                return 2;
            }

            return string.Equals(
                    decorationId,
                    "clay-vase",
                    StringComparison.Ordinal)
                ? 3
                : -1;
        }

        private static bool ContainsId(
            string[] values,
            string candidate)
        {
            if (values == null || string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (string.Equals(
                        values[index],
                        candidate,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] CopyIds(string[] source)
        {
            if (source == null || source.Length == 0)
            {
                return EmptyIds;
            }

            var copy = new string[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static DecorationSelection[] CopySelections(
            DecorationSelection[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<DecorationSelection>();
            }

            var copy = new DecorationSelection[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static PendingPresentationEntry[] CopyPresentations(
            PendingPresentationEntry[] source)
        {
            if (source == null || source.Length == 0)
            {
                return EmptyPresentations;
            }

            var copy =
                new PendingPresentationEntry[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static bool IdArraysEqual(
            string[] left,
            string[] right)
        {
            int leftLength = left?.Length ?? 0;
            if (leftLength != (right?.Length ?? 0))
            {
                return false;
            }

            for (var index = 0; index < leftLength; index++)
            {
                if (!string.Equals(
                        left[index],
                        right[index],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PresentationArraysEqual(
            PendingPresentationEntry[] left,
            PendingPresentationEntry[] right)
        {
            int leftLength = left?.Length ?? 0;
            if (leftLength != (right?.Length ?? 0))
            {
                return false;
            }

            for (var index = 0; index < leftLength; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SelectionArraysEqual(
            DecorationSelection[] left,
            DecorationSelection[] right)
        {
            int leftLength = left?.Length ?? 0;
            if (leftLength != (right?.Length ?? 0))
            {
                return false;
            }

            for (var index = 0; index < leftLength; index++)
            {
                if (!string.Equals(
                        left[index].SlotId,
                        right[index].SlotId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        left[index].DecorationId,
                        right[index].DecorationId,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static int HashIds(int hashCode, string[] values)
        {
            if (values == null)
            {
                return hashCode;
            }

            unchecked
            {
                for (var index = 0; index < values.Length; index++)
                {
                    hashCode =
                        (hashCode * 397) ^
                        (values[index] != null
                            ? values[index].GetHashCode()
                            : 0);
                }

                return hashCode;
            }
        }

        private static int HashPresentations(
            int hashCode,
            PendingPresentationEntry[] values)
        {
            if (values == null)
            {
                return hashCode;
            }

            unchecked
            {
                for (var index = 0; index < values.Length; index++)
                {
                    hashCode =
                        (hashCode * 397) ^
                        values[index].GetHashCode();
                }

                return hashCode;
            }
        }

        private static int HashSelections(
            int hashCode,
            DecorationSelection[] values)
        {
            if (values == null)
            {
                return hashCode;
            }

            unchecked
            {
                for (var index = 0; index < values.Length; index++)
                {
                    hashCode =
                        (hashCode * 397) ^
                        (values[index].SlotId != null
                            ? values[index].SlotId.GetHashCode()
                            : 0);
                    hashCode =
                        (hashCode * 397) ^
                        (values[index].DecorationId != null
                            ? values[index].DecorationId.GetHashCode()
                            : 0);
                }

                return hashCode;
            }
        }
    }

    /// <summary>
    /// Immutable, validated view of the locally persisted demo progress.
    /// A compact bit mask is sufficient for the deliberately small demo and
    /// keeps save migration deterministic.
    /// </summary>
    public readonly partial struct DemoProgressSnapshot :
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
            : this(
                highestUnlockedLevelIndex,
                completedLevelMask,
                selectedThemeId,
                musicEnabled,
                calmPoints,
                rewardedLevelMask,
                CreateLegacyOwnedDecorationIds(ownedDecorationMask),
                CreateLegacyDecorationSelections(
                    ownedDecorationMask,
                    selectedDecorationIndex),
                EmptyIds,
                EmptyIds,
                EmptyIds,
                EmptyPresentations,
                0,
                0)
        {
        }

        public int HighestUnlockedLevelIndex { get; }

        public int CompletedLevelMask { get; }

        public string SelectedThemeId { get; }

        public bool MusicEnabled { get; }

        public int CozyTokens { get; }

        /// <summary>
        /// v2 save migration alias. New product code should use CozyTokens.
        /// </summary>
        public int CalmPoints => CozyTokens;

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
                    other.SelectedDecorationIndex &&
                EqualsV2(other);
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
                hashCode = (hashCode * 397) ^ GetV2HashCode();
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

}
