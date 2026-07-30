using System;
using System.Collections.Generic;
using CalmSpace.Levels;

namespace CalmSpace.Workshop
{
    public enum WorkshopCatalogValidationCode
    {
        Valid = 0,
        MissingChapter = 1,
        MissingBeat = 2,
        DuplicateBeatId = 3,
        DuplicateZoneIndex = 4,
        StageGap = 5,
        InvalidMemoryLink = 6,
        MissingFinale = 7,
        MultipleFinales = 8,
        LevelJoinFailed = 9,
        InvalidDecorLink = 10,
        InvalidDailyCareUnlock = 11,
        ZoneIndexOutOfRange = 12
    }

    public readonly struct WorkshopCatalogValidationResult
    {
        internal WorkshopCatalogValidationResult(
            WorkshopCatalogValidationCode code,
            int invalidStageIndex,
            string stableId)
        {
            _isInitialized = true;
            Code = code;
            InvalidStageIndex = invalidStageIndex;
            StableId = stableId ?? string.Empty;
        }

        private readonly bool _isInitialized;

        public bool IsValid =>
            _isInitialized &&
            Code == WorkshopCatalogValidationCode.Valid;

        public WorkshopCatalogValidationCode Code { get; }

        public int InvalidStageIndex { get; }

        public string StableId { get; }
    }

    /// <summary>
    /// Validates workshop metadata without changing the ordinary level
    /// catalog. Every join is resolved through chapter id plus stage index.
    /// </summary>
    public static class WorkshopCatalogValidator
    {
        public static WorkshopCatalogValidationResult ValidateChapter(
            LevelCatalog levels,
            LivingWorkshopCatalog workshop,
            string chapterId)
        {
            string normalizedChapterId = Normalize(chapterId);
            if (levels == null ||
                string.IsNullOrEmpty(normalizedChapterId) ||
                !levels.TryGetRestorationChapter(
                    normalizedChapterId,
                    out var chapter))
            {
                return Invalid(
                    WorkshopCatalogValidationCode.MissingChapter,
                    -1,
                    normalizedChapterId);
            }

            if (workshop == null || workshop.Count == 0)
            {
                return Invalid(
                    WorkshopCatalogValidationCode.MissingBeat,
                    0,
                    normalizedChapterId);
            }

            var stageBeats =
                new WorkshopBeatDefinition[chapter.StageCount];
            var beatIds =
                new HashSet<string>(StringComparer.Ordinal);
            var zoneIndices = new HashSet<int>();
            int matchingBeatCount = 0;
            int finaleCount = 0;
            WorkshopBeatDefinition firstFinale = null;
            WorkshopBeatDefinition firstOffFinale = null;

            for (var catalogIndex = 0;
                 catalogIndex < workshop.Count;
                 catalogIndex++)
            {
                if (!workshop.TryGetBeat(
                        catalogIndex,
                        out var beat))
                {
                    return Invalid(
                        WorkshopCatalogValidationCode.MissingBeat,
                        beat != null && beat.StageIndex >= 0
                            ? beat.StageIndex
                            : catalogIndex,
                        beat?.BeatId ?? normalizedChapterId);
                }

                if (!string.Equals(
                        beat.ChapterId,
                        normalizedChapterId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                matchingBeatCount++;
                if (beat.StageIndex < 0 ||
                    beat.StageIndex >= chapter.StageCount)
                {
                    return Invalid(
                        WorkshopCatalogValidationCode.StageGap,
                        beat.StageIndex,
                        beat.BeatId);
                }

                if (beat.ZoneIndex < 0 ||
                    beat.ZoneIndex >= chapter.StageCount)
                {
                    return Invalid(
                        WorkshopCatalogValidationCode
                            .ZoneIndexOutOfRange,
                        beat.StageIndex,
                        beat.BeatId);
                }

                if (!beat.IsValid)
                {
                    return Invalid(
                        WorkshopCatalogValidationCode.MissingBeat,
                        beat.StageIndex,
                        beat.BeatId);
                }

                WorkshopBeatContract canonicalBeat = default;
                bool hasCanonicalBeat =
                    string.Equals(
                        normalizedChapterId,
                        WorkshopContentIds.CozyWorkshopChapterId,
                        StringComparison.Ordinal) &&
                    WorkshopContentIds.TryGetCozyWorkshopBeat(
                        beat.StageIndex,
                        out canonicalBeat);
                if (!beatIds.Add(beat.BeatId))
                {
                    return Invalid(
                        WorkshopCatalogValidationCode
                            .DuplicateBeatId,
                        beat.StageIndex,
                        beat.BeatId);
                }

                if (hasCanonicalBeat &&
                    (!string.Equals(
                        beat.BeatId,
                        canonicalBeat.BeatId,
                        StringComparison.Ordinal) ||
                     !string.Equals(
                        beat.TitleTextKey,
                        canonicalBeat.TitleTextKey,
                        StringComparison.Ordinal) ||
                     !string.Equals(
                        beat.ResultTextKey,
                        canonicalBeat.ResultTextKey,
                        StringComparison.Ordinal)))
                {
                    return Invalid(
                        WorkshopCatalogValidationCode.MissingBeat,
                        beat.StageIndex,
                        canonicalBeat.BeatId);
                }

                if (!zoneIndices.Add(beat.ZoneIndex))
                {
                    return Invalid(
                        WorkshopCatalogValidationCode
                            .DuplicateZoneIndex,
                        beat.StageIndex,
                        beat.BeatId);
                }

                if (hasCanonicalBeat &&
                    !string.Equals(
                        beat.MemoryId,
                        canonicalBeat.MemoryId,
                        StringComparison.Ordinal))
                {
                    return Invalid(
                        WorkshopCatalogValidationCode
                            .InvalidMemoryLink,
                        beat.StageIndex,
                        string.IsNullOrEmpty(beat.MemoryId)
                            ? canonicalBeat.MemoryId
                            : beat.MemoryId);
                }

                if (!string.IsNullOrEmpty(beat.MemoryId) &&
                    !WorkshopContentIds.IsKnownMemoryId(
                        beat.MemoryId))
                {
                    return Invalid(
                        WorkshopCatalogValidationCode
                            .InvalidMemoryLink,
                        beat.StageIndex,
                        beat.MemoryId);
                }

                if (hasCanonicalBeat &&
                    !string.Equals(
                        beat.UnlockedDecorSlotId,
                        canonicalBeat.UnlockedDecorSlotId,
                        StringComparison.Ordinal))
                {
                    return Invalid(
                        WorkshopCatalogValidationCode
                            .InvalidDecorLink,
                        beat.StageIndex,
                        string.IsNullOrEmpty(
                            beat.UnlockedDecorSlotId)
                            ? canonicalBeat.UnlockedDecorSlotId
                            : beat.UnlockedDecorSlotId);
                }

                if (hasCanonicalBeat &&
                    beat.UnlocksDailyCare !=
                    canonicalBeat.UnlocksDailyCare)
                {
                    return Invalid(
                        WorkshopCatalogValidationCode
                            .InvalidDailyCareUnlock,
                        beat.StageIndex,
                        WorkshopContentIds.FamilyTeaDailyCareId);
                }

                if (stageBeats[beat.StageIndex] != null)
                {
                    return Invalid(
                        WorkshopCatalogValidationCode.StageGap,
                        beat.StageIndex,
                        beat.BeatId);
                }

                stageBeats[beat.StageIndex] = beat;
                if (!beat.IsFinale)
                {
                    continue;
                }

                finaleCount++;
                if (firstFinale == null)
                {
                    firstFinale = beat;
                }

                if (beat.StageIndex != chapter.StageCount - 1 &&
                    firstOffFinale == null)
                {
                    firstOffFinale = beat;
                }
            }

            if (matchingBeatCount < chapter.StageCount)
            {
                return Invalid(
                    WorkshopCatalogValidationCode.MissingBeat,
                    FindMissingStage(stageBeats),
                    normalizedChapterId);
            }

            for (var stageIndex = 0;
                 stageIndex < stageBeats.Length;
                 stageIndex++)
            {
                if (stageBeats[stageIndex] == null)
                {
                    return Invalid(
                        WorkshopCatalogValidationCode.StageGap,
                        stageIndex,
                        normalizedChapterId);
                }
            }

            if (finaleCount > 1)
            {
                WorkshopBeatDefinition duplicateFinale =
                    firstOffFinale ?? firstFinale;
                return Invalid(
                    WorkshopCatalogValidationCode.MultipleFinales,
                    duplicateFinale.StageIndex,
                    duplicateFinale.BeatId);
            }

            if (finaleCount == 0 ||
                firstFinale.StageIndex != chapter.StageCount - 1)
            {
                return Invalid(
                    WorkshopCatalogValidationCode.MissingFinale,
                    chapter.StageCount - 1,
                    normalizedChapterId);
            }

            for (var stageIndex = 0;
                 stageIndex < stageBeats.Length;
                 stageIndex++)
            {
                WorkshopBeatDefinition beat =
                    stageBeats[stageIndex];
                if (!levels.TryFindRestorationStage(
                        beat.ChapterId,
                        beat.StageIndex,
                        out _,
                        out _))
                {
                    return Invalid(
                        WorkshopCatalogValidationCode.LevelJoinFailed,
                        beat.StageIndex,
                        beat.BeatId);
                }
            }

            return new WorkshopCatalogValidationResult(
                WorkshopCatalogValidationCode.Valid,
                -1,
                normalizedChapterId);
        }

        public static bool TryResolveMetaAction(
            LevelCatalog levels,
            LivingWorkshopCatalog workshop,
            string chapterId,
            int stageIndex,
            out WorkshopBeatDefinition beat)
        {
            beat = null;
            WorkshopCatalogValidationResult result =
                ValidateChapter(
                    levels,
                    workshop,
                    chapterId);
            if (!result.IsValid ||
                !workshop.TryFindBeat(
                    chapterId,
                    stageIndex,
                    out var resolvedBeat))
            {
                return false;
            }

            beat = resolvedBeat;
            return true;
        }

        private static WorkshopCatalogValidationResult Invalid(
            WorkshopCatalogValidationCode code,
            int stageIndex,
            string stableId)
        {
            return new WorkshopCatalogValidationResult(
                code,
                stageIndex,
                stableId);
        }

        private static int FindMissingStage(
            IReadOnlyList<WorkshopBeatDefinition> stageBeats)
        {
            for (var index = 0;
                 index < stageBeats.Count;
                 index++)
            {
                if (stageBeats[index] == null)
                {
                    return index;
                }
            }

            return 0;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
