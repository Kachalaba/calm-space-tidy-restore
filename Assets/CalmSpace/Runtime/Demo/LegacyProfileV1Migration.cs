using System;
using CalmSpace.Workshop;

namespace CalmSpace.Demo
{
    public static class LegacyProfileV1Migration
    {
        private const string DefaultWorkbenchDecorationId =
            "soft-fern";
        private const string DefaultWarmLightDecorationId =
            "linen-shade";

        public static DemoProgressSnapshot Migrate(
            SecureProfileV1Dto source,
            int levelCount,
            string defaultThemeId)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            string[] owned = CreateOwnedDecorationIds(
                source.ownedDecorationMask);
            DecorationSelection[] selections =
                CreateSelections(
                    source.ownedDecorationMask,
                    source.selectedDecorationIndex);
            string[] seenRoomRevealIds =
                CreateSeenRoomRevealIds(
                    source.completedLevelMask,
                    levelCount);
            PendingPresentationEntry[] pending =
                HasCompletedWorkshop(source.completedLevelMask)
                    ? new[]
                    {
                        PendingPresentationEntry.Finale(
                            WorkshopContentIds
                                .CozyWorkshopChapterId,
                            requiresExplicitLaunch: true)
                    }
                    : Array.Empty<PendingPresentationEntry>();

            var migrated = new DemoProgressSnapshot(
                source.highestUnlockedLevelIndex,
                source.completedLevelMask,
                source.selectedThemeId,
                source.musicEnabled,
                source.cozyTokens,
                source.rewardedLevelMask,
                owned,
                selections,
                seenRoomRevealIds,
                Array.Empty<string>(),
                Array.Empty<string>(),
                pending,
                0,
                0);
            return DemoProgressRules.Normalize(
                migrated,
                levelCount,
                defaultThemeId);
        }

        private static string[] CreateOwnedDecorationIds(
            int ownedDecorationMask)
        {
            var result = new string[6];
            var count = 0;
            AppendUnique(
                result,
                ref count,
                DefaultWorkbenchDecorationId);
            AppendUnique(
                result,
                ref count,
                DefaultWarmLightDecorationId);

            for (var index = 0; index < 4; index++)
            {
                if ((ownedDecorationMask & (1 << index)) == 0)
                {
                    continue;
                }

                AppendUnique(
                    result,
                    ref count,
                    DemoProgressSnapshot.GetLegacyDecorationId(index));
            }

            Array.Resize(ref result, count);
            return result;
        }

        private static DecorationSelection[] CreateSelections(
            int ownedDecorationMask,
            int selectedDecorationIndex)
        {
            var result = new DecorationSelection[3];
            result[0] = new DecorationSelection(
                WorkshopContentIds.WorkbenchAccentDecorSlotId,
                DefaultWorkbenchDecorationId);
            result[1] = new DecorationSelection(
                WorkshopContentIds.WarmLightDecorSlotId,
                DefaultWarmLightDecorationId);
            var count = 2;

            if (selectedDecorationIndex < 0 ||
                selectedDecorationIndex >= 4 ||
                (ownedDecorationMask &
                 (1 << selectedDecorationIndex)) == 0)
            {
                Array.Resize(ref result, count);
                return result;
            }

            string selectedId =
                DemoProgressSnapshot.GetLegacyDecorationId(
                    selectedDecorationIndex);
            string mappedSlotId =
                selectedDecorationIndex == 2
                    ? WorkshopContentIds.WarmLightDecorSlotId
                    : WorkshopContentIds
                        .WorkbenchAccentDecorSlotId;
            int mappedIndex =
                string.Equals(
                    mappedSlotId,
                    WorkshopContentIds.WarmLightDecorSlotId,
                    StringComparison.Ordinal)
                    ? 1
                    : 0;
            result[mappedIndex] =
                new DecorationSelection(
                    mappedSlotId,
                    selectedId);
            result[count++] =
                new DecorationSelection(
                    DemoProgressSnapshot.LegacyDecorationSlotId,
                    selectedId);
            Array.Resize(ref result, count);
            return result;
        }

        private static string[] CreateSeenRoomRevealIds(
            int completedLevelMask,
            int levelCount)
        {
            int stageCount = Math.Min(
                DemoProgressRules.NormalizeLevelCount(levelCount),
                WorkshopContentIds.CozyWorkshopBeatCount);
            var result = new string[stageCount];
            var count = 0;
            for (var stageIndex = 0;
                 stageIndex < stageCount;
                 stageIndex++)
            {
                if ((completedLevelMask & (1 << stageIndex)) == 0 ||
                    !WorkshopContentIds.TryGetCozyWorkshopBeat(
                        stageIndex,
                        out WorkshopBeatContract beat))
                {
                    continue;
                }

                result[count++] = beat.BeatId;
            }

            Array.Resize(ref result, count);
            return result;
        }

        private static bool HasCompletedWorkshop(
            int completedLevelMask)
        {
            return (completedLevelMask & 0xFF) == 0xFF;
        }

        private static void AppendUnique(
            string[] values,
            ref int count,
            string value)
        {
            for (var index = 0; index < count; index++)
            {
                if (string.Equals(
                        values[index],
                        value,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }

            values[count++] = value;
        }
    }
}
