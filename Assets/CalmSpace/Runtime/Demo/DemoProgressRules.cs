using System;
using UnityEngine;

namespace CalmSpace.Demo
{
    public static partial class DemoProgressRules
    {
        private const string TeaPostcardMemoryId =
            "family.tea-postcard-03";

        public static ProfileMutationResult<LevelCompletionMutation>
            CompleteLevel(
                DemoProgressSnapshot snapshot,
                CompleteLevelCommand command,
                int levelCount)
        {
            int normalizedLevelCount =
                NormalizeLevelCount(levelCount);
            if (!IsValidStableId(command.LevelId) ||
                command.LevelIndex < 0 ||
                command.LevelIndex >= normalizedLevelCount ||
                command.RewardAmount < 0 ||
                !HasValidPresentations(command))
            {
                return Invalid(
                    snapshot,
                    new LevelCompletionMutation(
                        false,
                        0,
                        snapshot.CozyTokens));
            }

            int completionBit = 1 << command.LevelIndex;
            if ((snapshot.CompletedLevelMask & completionBit) != 0)
            {
                return AlreadyApplied(
                    snapshot,
                    new LevelCompletionMutation(
                        false,
                        0,
                        snapshot.CozyTokens));
            }

            int highestUnlocked =
                Math.Max(
                    snapshot.HighestUnlockedLevelIndex,
                    Math.Min(
                        command.LevelIndex + 1,
                        normalizedLevelCount - 1));
            int tokenBalance =
                SaturatingAdd(
                    snapshot.CozyTokens,
                    command.RewardAmount);
            PendingPresentationEntry[] pending =
                AppendCommandPresentations(snapshot, command);
            DemoProgressSnapshot next = Rebuild(
                snapshot,
                highestUnlocked,
                snapshot.CompletedLevelMask | completionBit,
                snapshot.SelectedThemeId,
                snapshot.MusicEnabled,
                tokenBalance,
                snapshot.RewardedLevelMask | completionBit,
                snapshot.CopyOwnedDecorationIds(),
                snapshot.CopyDecorationSelections(),
                snapshot.CopySeenRoomRevealIds(),
                snapshot.CopyViewedMemoryIds(),
                snapshot.CopySeenFinaleIds(),
                pending,
                snapshot.LastDailyCareUtcDayKey,
                snapshot.CompletedDailyCareCount);

            return Applied(
                next,
                new LevelCompletionMutation(
                    true,
                    command.RewardAmount,
                    tokenBalance));
        }

        public static ProfileMutationResult<PresentationMutation>
            MarkPresentationSeen(
                DemoProgressSnapshot snapshot,
                PendingPresentationEntry presentation)
        {
            if (!IsValidStableId(presentation.StableId) ||
                (presentation.Kind !=
                    PendingPresentationKind.RoomReveal &&
                 presentation.Kind !=
                    PendingPresentationKind.Finale))
            {
                return Invalid(
                    snapshot,
                    new PresentationMutation(presentation));
            }

            bool alreadySeen =
                presentation.Kind ==
                    PendingPresentationKind.RoomReveal
                    ? snapshot.HasSeenRoomReveal(
                        presentation.StableId)
                    : snapshot.HasSeenFinale(
                        presentation.StableId);
            if (alreadySeen)
            {
                return AlreadyApplied(
                    snapshot,
                    new PresentationMutation(presentation));
            }

            if (!snapshot.TryGetPendingPresentation(
                    0,
                    out PendingPresentationEntry head) ||
                head != presentation)
            {
                return Invalid(
                    snapshot,
                    new PresentationMutation(presentation));
            }

            PendingPresentationEntry[] pending =
                RemovePendingAt(snapshot, 0);
            string[] roomRevealIds =
                snapshot.CopySeenRoomRevealIds();
            string[] finaleIds = snapshot.CopySeenFinaleIds();
            if (presentation.Kind ==
                PendingPresentationKind.RoomReveal)
            {
                roomRevealIds =
                    AppendId(
                        roomRevealIds,
                        presentation.StableId);
            }
            else
            {
                finaleIds =
                    AppendId(
                        finaleIds,
                        presentation.StableId);
            }

            DemoProgressSnapshot next = Rebuild(
                snapshot,
                snapshot.HighestUnlockedLevelIndex,
                snapshot.CompletedLevelMask,
                snapshot.SelectedThemeId,
                snapshot.MusicEnabled,
                snapshot.CozyTokens,
                snapshot.RewardedLevelMask,
                snapshot.CopyOwnedDecorationIds(),
                snapshot.CopyDecorationSelections(),
                roomRevealIds,
                snapshot.CopyViewedMemoryIds(),
                finaleIds,
                pending,
                snapshot.LastDailyCareUtcDayKey,
                snapshot.CompletedDailyCareCount);
            return Applied(
                next,
                new PresentationMutation(presentation));
        }

        public static ProfileMutationResult<MemoryMutation>
            MarkMemoryViewed(
                DemoProgressSnapshot snapshot,
                string memoryId)
        {
            if (!IsValidStableId(memoryId))
            {
                return Invalid(
                    snapshot,
                    new MemoryMutation(memoryId, false));
            }

            if (snapshot.HasViewedMemory(memoryId))
            {
                return AlreadyApplied(
                    snapshot,
                    new MemoryMutation(memoryId, false));
            }

            string[] viewedMemoryIds =
                AppendId(
                    snapshot.CopyViewedMemoryIds(),
                    memoryId);
            PendingPresentationEntry[] pending =
                RemovePendingMemory(snapshot, memoryId);
            DemoProgressSnapshot next = Rebuild(
                snapshot,
                snapshot.HighestUnlockedLevelIndex,
                snapshot.CompletedLevelMask,
                snapshot.SelectedThemeId,
                snapshot.MusicEnabled,
                snapshot.CozyTokens,
                snapshot.RewardedLevelMask,
                snapshot.CopyOwnedDecorationIds(),
                snapshot.CopyDecorationSelections(),
                snapshot.CopySeenRoomRevealIds(),
                viewedMemoryIds,
                snapshot.CopySeenFinaleIds(),
                pending,
                snapshot.LastDailyCareUtcDayKey,
                snapshot.CompletedDailyCareCount);
            return Applied(
                next,
                new MemoryMutation(memoryId, true));
        }

        public static ProfileMutationResult<DecorationMutation>
            PurchaseDecoration(
                DemoProgressSnapshot snapshot,
                string slotId,
                string decorationId,
                int cost)
        {
            if (!IsValidStableId(slotId) ||
                !IsValidStableId(decorationId) ||
                cost < 0)
            {
                return Invalid(
                    snapshot,
                    CreateDecorationPayload(
                        snapshot,
                        slotId,
                        decorationId,
                        0));
            }

            bool isOwned =
                snapshot.OwnsDecoration(decorationId);
            bool isSelected =
                snapshot.TryGetSelectedDecoration(
                    slotId,
                    out string selectedId) &&
                string.Equals(
                    selectedId,
                    decorationId,
                    StringComparison.Ordinal);
            if (isOwned && isSelected)
            {
                return AlreadyApplied(
                    snapshot,
                    CreateDecorationPayload(
                        snapshot,
                        slotId,
                        decorationId,
                        0));
            }

            if (!isOwned && snapshot.CozyTokens < cost)
            {
                return Invalid(
                    snapshot,
                    CreateDecorationPayload(
                        snapshot,
                        slotId,
                        decorationId,
                        0));
            }

            int tokenDelta = isOwned ? 0 : -cost;
            return ApplyDecoration(
                snapshot,
                slotId,
                decorationId,
                tokenDelta);
        }

        public static ProfileMutationResult<DecorationMutation>
            GrantDecoration(
                DemoProgressSnapshot snapshot,
                string slotId,
                string decorationId,
                DecorationGrantSource source)
        {
            if (!IsValidStableId(slotId) ||
                !IsValidStableId(decorationId) ||
                !Enum.IsDefined(
                    typeof(DecorationGrantSource),
                    source))
            {
                return Invalid(
                    snapshot,
                    CreateDecorationPayload(
                        snapshot,
                        slotId,
                        decorationId,
                        0));
            }

            if (snapshot.OwnsDecoration(decorationId) &&
                snapshot.TryGetSelectedDecoration(
                    slotId,
                    out string selectedId) &&
                string.Equals(
                    selectedId,
                    decorationId,
                    StringComparison.Ordinal))
            {
                return AlreadyApplied(
                    snapshot,
                    CreateDecorationPayload(
                        snapshot,
                        slotId,
                        decorationId,
                        0));
            }

            return ApplyDecoration(
                snapshot,
                slotId,
                decorationId,
                0);
        }

        public static ProfileMutationResult<DailyCareMutation>
            CompleteDailyCare(
                DemoProgressSnapshot snapshot,
                string careId,
                int utcDayKey,
                int rewardAmount)
        {
            if (!IsValidStableId(careId) ||
                utcDayKey < 0 ||
                rewardAmount < 0 ||
                (snapshot.LastDailyCareUtcDayKey > 0 &&
                 utcDayKey <
                    snapshot.LastDailyCareUtcDayKey))
            {
                return Invalid(
                    snapshot,
                    CreateDailyCarePayload(
                        snapshot,
                        careId,
                        utcDayKey,
                        0,
                        string.Empty));
            }

            if (snapshot.CompletedDailyCareCount > 0 &&
                utcDayKey ==
                    snapshot.LastDailyCareUtcDayKey)
            {
                return AlreadyApplied(
                    snapshot,
                    CreateDailyCarePayload(
                        snapshot,
                        careId,
                        utcDayKey,
                        0,
                        string.Empty));
            }

            int completionCount =
                snapshot.CompletedDailyCareCount ==
                    int.MaxValue
                    ? int.MaxValue
                    : snapshot.CompletedDailyCareCount + 1;
            int tokenBalance =
                SaturatingAdd(
                    snapshot.CozyTokens,
                    rewardAmount);
            string unlockedMemoryId = string.Empty;
            PendingPresentationEntry[] pending =
                snapshot.CopyPendingPresentations();
            if (completionCount == 3 &&
                !snapshot.HasViewedMemory(
                    TeaPostcardMemoryId) &&
                !HasPendingMemory(
                    snapshot,
                    TeaPostcardMemoryId))
            {
                pending =
                    AppendPresentation(
                        pending,
                        PendingPresentationEntry.Memory(
                            TeaPostcardMemoryId));
                unlockedMemoryId = TeaPostcardMemoryId;
            }

            DemoProgressSnapshot next = Rebuild(
                snapshot,
                snapshot.HighestUnlockedLevelIndex,
                snapshot.CompletedLevelMask,
                snapshot.SelectedThemeId,
                snapshot.MusicEnabled,
                tokenBalance,
                snapshot.RewardedLevelMask,
                snapshot.CopyOwnedDecorationIds(),
                snapshot.CopyDecorationSelections(),
                snapshot.CopySeenRoomRevealIds(),
                snapshot.CopyViewedMemoryIds(),
                snapshot.CopySeenFinaleIds(),
                pending,
                utcDayKey,
                completionCount);
            return Applied(
                next,
                new DailyCareMutation(
                    careId,
                    utcDayKey,
                    rewardAmount,
                    tokenBalance,
                    completionCount,
                    unlockedMemoryId));
        }

        public static ProfileMutationResult<PreferenceMutation>
            SetPreferences(
                DemoProgressSnapshot snapshot,
                string selectedThemeId,
                bool musicEnabled)
        {
            if (!IsValidStableId(selectedThemeId))
            {
                return Invalid(
                    snapshot,
                    new PreferenceMutation(false));
            }

            string normalizedThemeId = selectedThemeId.Trim();
            if (string.Equals(
                    snapshot.SelectedThemeId,
                    normalizedThemeId,
                    StringComparison.Ordinal) &&
                snapshot.MusicEnabled == musicEnabled)
            {
                return AlreadyApplied(
                    snapshot,
                    new PreferenceMutation(false));
            }

            DemoProgressSnapshot next = Rebuild(
                snapshot,
                snapshot.HighestUnlockedLevelIndex,
                snapshot.CompletedLevelMask,
                normalizedThemeId,
                musicEnabled,
                snapshot.CozyTokens,
                snapshot.RewardedLevelMask,
                snapshot.CopyOwnedDecorationIds(),
                snapshot.CopyDecorationSelections(),
                snapshot.CopySeenRoomRevealIds(),
                snapshot.CopyViewedMemoryIds(),
                snapshot.CopySeenFinaleIds(),
                snapshot.CopyPendingPresentations(),
                snapshot.LastDailyCareUtcDayKey,
                snapshot.CompletedDailyCareCount);
            return Applied(
                next,
                new PreferenceMutation(true));
        }

        private static ProfileMutationResult<TPayload> Applied<TPayload>(
            DemoProgressSnapshot snapshot,
            TPayload payload)
        {
            return new ProfileMutationResult<TPayload>(
                ProfileMutationStatus.Applied,
                snapshot,
                payload);
        }

        private static ProfileMutationResult<TPayload>
            AlreadyApplied<TPayload>(
                DemoProgressSnapshot snapshot,
                TPayload payload)
        {
            return new ProfileMutationResult<TPayload>(
                ProfileMutationStatus.AlreadyApplied,
                snapshot,
                payload);
        }

        private static ProfileMutationResult<TPayload> Invalid<TPayload>(
            DemoProgressSnapshot snapshot,
            TPayload payload)
        {
            return new ProfileMutationResult<TPayload>(
                ProfileMutationStatus.Invalid,
                snapshot,
                payload);
        }

        private static ProfileMutationResult<DecorationMutation>
            ApplyDecoration(
                DemoProgressSnapshot snapshot,
                string slotId,
                string decorationId,
                int tokenDelta)
        {
            string[] owned = snapshot.CopyOwnedDecorationIds();
            if (!snapshot.OwnsDecoration(decorationId))
            {
                owned = AppendId(owned, decorationId);
            }

            DecorationSelection[] selections =
                SetSelection(
                    snapshot.CopyDecorationSelections(),
                    slotId,
                    decorationId);
            int tokenBalance =
                tokenDelta < 0
                    ? snapshot.CozyTokens + tokenDelta
                    : snapshot.CozyTokens;
            DemoProgressSnapshot next = Rebuild(
                snapshot,
                snapshot.HighestUnlockedLevelIndex,
                snapshot.CompletedLevelMask,
                snapshot.SelectedThemeId,
                snapshot.MusicEnabled,
                tokenBalance,
                snapshot.RewardedLevelMask,
                owned,
                selections,
                snapshot.CopySeenRoomRevealIds(),
                snapshot.CopyViewedMemoryIds(),
                snapshot.CopySeenFinaleIds(),
                snapshot.CopyPendingPresentations(),
                snapshot.LastDailyCareUtcDayKey,
                snapshot.CompletedDailyCareCount);
            return Applied(
                next,
                new DecorationMutation(
                    slotId,
                    decorationId,
                    tokenDelta,
                    tokenBalance));
        }

        private static DecorationMutation CreateDecorationPayload(
            DemoProgressSnapshot snapshot,
            string slotId,
            string decorationId,
            int tokenDelta)
        {
            return new DecorationMutation(
                slotId,
                decorationId,
                tokenDelta,
                snapshot.CozyTokens);
        }

        private static DailyCareMutation CreateDailyCarePayload(
            DemoProgressSnapshot snapshot,
            string careId,
            int utcDayKey,
            int rewardAmount,
            string unlockedMemoryId)
        {
            return new DailyCareMutation(
                careId,
                utcDayKey,
                rewardAmount,
                snapshot.CozyTokens,
                snapshot.CompletedDailyCareCount,
                unlockedMemoryId);
        }

        private static DemoProgressSnapshot Rebuild(
            DemoProgressSnapshot snapshot,
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
            return new DemoProgressSnapshot(
                highestUnlockedLevelIndex,
                completedLevelMask,
                selectedThemeId,
                musicEnabled,
                cozyTokens,
                rewardedLevelMask,
                ownedDecorationIds,
                decorationSelections,
                seenRoomRevealIds,
                viewedMemoryIds,
                seenFinaleIds,
                pendingPresentations,
                lastDailyCareUtcDayKey,
                completedDailyCareCount);
        }

        private static bool HasValidPresentations(
            CompleteLevelCommand command)
        {
            int previousKind = -1;
            for (var index = 0;
                 index < command.PresentationCount;
                 index++)
            {
                if (!command.TryGetPresentation(
                        index,
                        out PendingPresentationEntry entry) ||
                    !IsValidStableId(entry.StableId) ||
                    (entry.Kind !=
                        PendingPresentationKind.RoomReveal &&
                     entry.Kind != PendingPresentationKind.Memory &&
                     entry.Kind != PendingPresentationKind.Finale) ||
                    (int)entry.Kind < previousKind)
                {
                    return false;
                }

                previousKind = (int)entry.Kind;
            }

            return true;
        }

        private static PendingPresentationEntry[]
            AppendCommandPresentations(
                DemoProgressSnapshot snapshot,
                CompleteLevelCommand command)
        {
            PendingPresentationEntry[] result =
                snapshot.CopyPendingPresentations();
            for (var index = 0;
                 index < command.PresentationCount;
                 index++)
            {
                command.TryGetPresentation(
                    index,
                    out PendingPresentationEntry entry);
                if (!HasHandledPresentation(snapshot, entry) &&
                    !ContainsPresentation(result, entry))
                {
                    result = AppendPresentation(result, entry);
                }
            }

            return result;
        }

        private static bool HasHandledPresentation(
            DemoProgressSnapshot snapshot,
            PendingPresentationEntry entry)
        {
            switch (entry.Kind)
            {
                case PendingPresentationKind.RoomReveal:
                    return snapshot.HasSeenRoomReveal(
                        entry.StableId);
                case PendingPresentationKind.Memory:
                    return snapshot.HasViewedMemory(
                        entry.StableId);
                case PendingPresentationKind.Finale:
                    return snapshot.HasSeenFinale(entry.StableId);
                default:
                    return false;
            }
        }

        private static bool ContainsPresentation(
            PendingPresentationEntry[] presentations,
            PendingPresentationEntry candidate)
        {
            if (presentations == null)
            {
                return false;
            }

            for (var index = 0;
                 index < presentations.Length;
                 index++)
            {
                if (presentations[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPendingMemory(
            DemoProgressSnapshot snapshot,
            string memoryId)
        {
            for (var index = 0;
                 index < snapshot.PendingPresentationCount;
                 index++)
            {
                snapshot.TryGetPendingPresentation(
                    index,
                    out PendingPresentationEntry entry);
                if (entry.Kind ==
                        PendingPresentationKind.Memory &&
                    string.Equals(
                        entry.StableId,
                        memoryId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static PendingPresentationEntry[] RemovePendingAt(
            DemoProgressSnapshot snapshot,
            int removedIndex)
        {
            var result =
                new PendingPresentationEntry[
                    snapshot.PendingPresentationCount - 1];
            var destinationIndex = 0;
            for (var index = 0;
                 index < snapshot.PendingPresentationCount;
                 index++)
            {
                if (index == removedIndex)
                {
                    continue;
                }

                snapshot.TryGetPendingPresentation(
                    index,
                    out result[destinationIndex]);
                destinationIndex++;
            }

            return result;
        }

        private static PendingPresentationEntry[]
            RemovePendingMemory(
                DemoProgressSnapshot snapshot,
                string memoryId)
        {
            var matchCount = 0;
            for (var index = 0;
                 index < snapshot.PendingPresentationCount;
                 index++)
            {
                snapshot.TryGetPendingPresentation(
                    index,
                    out PendingPresentationEntry entry);
                if (entry.Kind ==
                        PendingPresentationKind.Memory &&
                    string.Equals(
                        entry.StableId,
                        memoryId,
                        StringComparison.Ordinal))
                {
                    matchCount++;
                }
            }

            if (matchCount == 0)
            {
                return snapshot.CopyPendingPresentations();
            }

            var result =
                new PendingPresentationEntry[
                    snapshot.PendingPresentationCount -
                    matchCount];
            var destinationIndex = 0;
            for (var index = 0;
                 index < snapshot.PendingPresentationCount;
                 index++)
            {
                snapshot.TryGetPendingPresentation(
                    index,
                    out PendingPresentationEntry entry);
                if (entry.Kind ==
                        PendingPresentationKind.Memory &&
                    string.Equals(
                        entry.StableId,
                        memoryId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                result[destinationIndex++] = entry;
            }

            return result;
        }

        private static string[] AppendId(
            string[] source,
            string value)
        {
            int sourceLength = source?.Length ?? 0;
            var result = new string[sourceLength + 1];
            if (sourceLength > 0)
            {
                Array.Copy(source, result, sourceLength);
            }

            result[sourceLength] = value;
            return result;
        }

        private static PendingPresentationEntry[] AppendPresentation(
            PendingPresentationEntry[] source,
            PendingPresentationEntry value)
        {
            int sourceLength = source?.Length ?? 0;
            var result =
                new PendingPresentationEntry[sourceLength + 1];
            if (sourceLength > 0)
            {
                Array.Copy(source, result, sourceLength);
            }

            result[sourceLength] = value;
            return result;
        }

        private static DecorationSelection[] SetSelection(
            DecorationSelection[] source,
            string slotId,
            string decorationId)
        {
            int sourceLength = source?.Length ?? 0;
            for (var index = 0; index < sourceLength; index++)
            {
                if (!string.Equals(
                        source[index].SlotId,
                        slotId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var replaced =
                    new DecorationSelection[sourceLength];
                Array.Copy(source, replaced, sourceLength);
                replaced[index] =
                    new DecorationSelection(
                        slotId,
                        decorationId);
                return replaced;
            }

            var appended =
                new DecorationSelection[sourceLength + 1];
            if (sourceLength > 0)
            {
                Array.Copy(source, appended, sourceLength);
            }

            appended[sourceLength] =
                new DecorationSelection(slotId, decorationId);
            return appended;
        }

        private static string[] NormalizeOwnedDecorationIds(
            DemoProgressSnapshot snapshot)
        {
            string[] source = snapshot.CopyOwnedDecorationIds();
            string[] normalized = Array.Empty<string>();
            for (var index = 0; index < source.Length; index++)
            {
                string decorationId = source[index];
                if (IsValidStableId(decorationId) &&
                    !ContainsId(normalized, decorationId))
                {
                    normalized =
                        AppendId(
                            normalized,
                            decorationId.Trim());
                }
            }

            if (!ContainsId(normalized, "soft-fern"))
            {
                normalized = AppendId(normalized, "soft-fern");
            }

            return normalized;
        }

        private static DecorationSelection[]
            NormalizeDecorationSelections(
                DemoProgressSnapshot snapshot,
                string[] ownedDecorationIds)
        {
            DecorationSelection[] source =
                snapshot.CopyDecorationSelections();
            DecorationSelection[] normalized =
                Array.Empty<DecorationSelection>();
            for (var index = 0; index < source.Length; index++)
            {
                string slotId = source[index].SlotId;
                string decorationId =
                    source[index].DecorationId;
                if (!IsValidStableId(slotId) ||
                    !IsValidStableId(decorationId) ||
                    !ContainsId(
                        ownedDecorationIds,
                        decorationId))
                {
                    continue;
                }

                normalized =
                    SetSelection(
                        normalized,
                        slotId.Trim(),
                        decorationId.Trim());
            }

            if (normalized.Length == 0)
            {
                normalized =
                    new[]
                    {
                        new DecorationSelection(
                            DemoProgressSnapshot
                                .LegacyDecorationSlotId,
                            "soft-fern")
                    };
            }

            return normalized;
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

        private static bool IsValidStableId(string stableId)
        {
            return !string.IsNullOrWhiteSpace(stableId);
        }
    }

    /// <summary>
    /// Pure progression rules. Keeping validation independent from PlayerPrefs
    /// makes corrupt-save recovery and unlock behavior straightforward to test.
    /// </summary>
    public static partial class DemoProgressRules
    {
        public const int MaximumTrackedLevels = 30;
        public const int MaximumTrackedDecorations = 4;

        private const int StarterDecorationMask = 1;

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

            string[] ownedDecorationIds =
                NormalizeOwnedDecorationIds(snapshot);
            DecorationSelection[] decorationSelections =
                NormalizeDecorationSelections(
                    snapshot,
                    ownedDecorationIds);
            return Rebuild(
                snapshot,
                highestUnlocked,
                completedMask,
                NormalizeThemeId(
                    snapshot.SelectedThemeId,
                    defaultThemeId),
                snapshot.MusicEnabled,
                Mathf.Max(0, snapshot.CalmPoints),
                rewardedLevelMask,
                ownedDecorationIds,
                decorationSelections,
                snapshot.CopySeenRoomRevealIds(),
                snapshot.CopyViewedMemoryIds(),
                snapshot.CopySeenFinaleIds(),
                snapshot.CopyPendingPresentations(),
                Mathf.Max(0, snapshot.LastDailyCareUtcDayKey),
                Mathf.Max(0, snapshot.CompletedDailyCareCount));
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

            return Rebuild(
                normalized,
                highestUnlocked,
                completedMask,
                normalized.SelectedThemeId,
                normalized.MusicEnabled,
                normalized.CalmPoints,
                normalized.RewardedLevelMask,
                normalized.CopyOwnedDecorationIds(),
                normalized.CopyDecorationSelections(),
                normalized.CopySeenRoomRevealIds(),
                normalized.CopyViewedMemoryIds(),
                normalized.CopySeenFinaleIds(),
                normalized.CopyPendingPresentations(),
                normalized.LastDailyCareUtcDayKey,
                normalized.CompletedDailyCareCount);
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

            return Rebuild(
                completed,
                completed.HighestUnlockedLevelIndex,
                completed.CompletedLevelMask,
                completed.SelectedThemeId,
                completed.MusicEnabled,
                SaturatingAdd(
                    completed.CalmPoints,
                    rewardAmount),
                completed.RewardedLevelMask | rewardBit,
                completed.CopyOwnedDecorationIds(),
                completed.CopyDecorationSelections(),
                completed.CopySeenRoomRevealIds(),
                completed.CopyViewedMemoryIds(),
                completed.CopySeenFinaleIds(),
                completed.CopyPendingPresentations(),
                completed.LastDailyCareUtcDayKey,
                completed.CompletedDailyCareCount);
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

            result = PurchaseDecoration(
                    normalized,
                    DemoProgressSnapshot.LegacyDecorationSlotId,
                    DemoProgressSnapshot.GetLegacyDecorationId(
                        decorationIndex),
                    cost)
                .Snapshot;
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
            return Rebuild(
                normalized,
                normalized.HighestUnlockedLevelIndex,
                normalized.CompletedLevelMask,
                NormalizeThemeId(themeId, defaultThemeId),
                normalized.MusicEnabled,
                normalized.CalmPoints,
                normalized.RewardedLevelMask,
                normalized.CopyOwnedDecorationIds(),
                normalized.CopyDecorationSelections(),
                normalized.CopySeenRoomRevealIds(),
                normalized.CopyViewedMemoryIds(),
                normalized.CopySeenFinaleIds(),
                normalized.CopyPendingPresentations(),
                normalized.LastDailyCareUtcDayKey,
                normalized.CompletedDailyCareCount);
        }

        public static DemoProgressSnapshot WithMusicEnabled(
            DemoProgressSnapshot snapshot,
            bool enabled,
            int levelCount,
            string defaultThemeId)
        {
            var normalized =
                Normalize(snapshot, levelCount, defaultThemeId);
            return Rebuild(
                normalized,
                normalized.HighestUnlockedLevelIndex,
                normalized.CompletedLevelMask,
                normalized.SelectedThemeId,
                enabled,
                normalized.CalmPoints,
                normalized.RewardedLevelMask,
                normalized.CopyOwnedDecorationIds(),
                normalized.CopyDecorationSelections(),
                normalized.CopySeenRoomRevealIds(),
                normalized.CopyViewedMemoryIds(),
                normalized.CopySeenFinaleIds(),
                normalized.CopyPendingPresentations(),
                normalized.LastDailyCareUtcDayKey,
                normalized.CompletedDailyCareCount);
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

        private static DemoProgressSnapshot WithSelectedDecoration(
            DemoProgressSnapshot snapshot,
            int decorationIndex)
        {
            DecorationSelection[] selections =
                SetSelection(
                    snapshot.CopyDecorationSelections(),
                    DemoProgressSnapshot.LegacyDecorationSlotId,
                    DemoProgressSnapshot.GetLegacyDecorationId(
                        decorationIndex));
            return Rebuild(
                snapshot,
                snapshot.HighestUnlockedLevelIndex,
                snapshot.CompletedLevelMask,
                snapshot.SelectedThemeId,
                snapshot.MusicEnabled,
                snapshot.CalmPoints,
                snapshot.RewardedLevelMask,
                snapshot.CopyOwnedDecorationIds(),
                selections,
                snapshot.CopySeenRoomRevealIds(),
                snapshot.CopyViewedMemoryIds(),
                snapshot.CopySeenFinaleIds(),
                snapshot.CopyPendingPresentations(),
                snapshot.LastDailyCareUtcDayKey,
                snapshot.CompletedDailyCareCount);
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

}
