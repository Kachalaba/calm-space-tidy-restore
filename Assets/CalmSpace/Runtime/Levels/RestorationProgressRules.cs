using System;
using System.Collections.Generic;

namespace CalmSpace.Levels
{
    /// <summary>
    /// Immutable chapter context for one authored level. Stages remain regular
    /// Addressable levels, so memory release, Undo ownership, and completion
    /// persistence keep their existing lifecycle.
    /// </summary>
    public readonly struct RestorationStageInfo :
        IEquatable<RestorationStageInfo>
    {
        public RestorationStageInfo(
            string chapterId,
            int stageIndex,
            int stageCount)
        {
            if (string.IsNullOrWhiteSpace(chapterId))
            {
                throw new ArgumentException(
                    "A restoration chapter id is required.",
                    nameof(chapterId));
            }

            if (stageCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stageCount));
            }

            if (stageIndex < 0 || stageIndex >= stageCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stageIndex));
            }

            ChapterId = chapterId.Trim();
            StageIndex = stageIndex;
            StageCount = stageCount;
        }

        public string ChapterId { get; }

        public int StageIndex { get; }

        public int StageCount { get; }

        public int StageNumber => StageIndex + 1;

        public bool IsFirstStage => StageIndex == 0;

        public bool IsFinalStage =>
            StageIndex == StageCount - 1;

        public bool Equals(RestorationStageInfo other)
        {
            return
                string.Equals(
                    ChapterId,
                    other.ChapterId,
                    StringComparison.Ordinal) &&
                StageIndex == other.StageIndex &&
                StageCount == other.StageCount;
        }

        public override bool Equals(object obj)
        {
            return
                obj is RestorationStageInfo other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode =
                    ChapterId != null
                        ? ChapterId.GetHashCode()
                        : 0;
                hashCode = (hashCode * 397) ^ StageIndex;
                hashCode = (hashCode * 397) ^ StageCount;
                return hashCode;
            }
        }
    }

    /// <summary>
    /// Pure rules for grouping independently loaded levels into a coherent
    /// restoration journey. Invalid authoring fails closed and is presented as
    /// an ordinary standalone level.
    /// </summary>
    public static class RestorationProgressRules
    {
        public static bool TryCreateStage(
            LevelDefinition definition,
            out RestorationStageInfo stage)
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(
                    definition.RestorationChapterId) ||
                definition.RestorationStageCount <= 0 ||
                definition.RestorationStageIndex < 0 ||
                definition.RestorationStageIndex >=
                    definition.RestorationStageCount)
            {
                stage = default;
                return false;
            }

            stage = new RestorationStageInfo(
                definition.RestorationChapterId,
                definition.RestorationStageIndex,
                definition.RestorationStageCount);
            return true;
        }

        public static bool IsContinuation(
            LevelDefinition current,
            LevelDefinition next)
        {
            if (!TryCreateStage(current, out var currentStage) ||
                !TryCreateStage(next, out var nextStage))
            {
                return false;
            }

            return
                string.Equals(
                    currentStage.ChapterId,
                    nextStage.ChapterId,
                    StringComparison.Ordinal) &&
                currentStage.StageCount == nextStage.StageCount &&
                nextStage.StageIndex ==
                    currentStage.StageIndex + 1;
        }

        /// <summary>
        /// Ensures every authored chapter is one contiguous, complete sequence.
        /// This keeps duplicated stage metadata deterministic without adding a
        /// second catalog or changing Addressable ownership.
        /// </summary>
        public static bool IsCatalogSequenceValid(
            IReadOnlyList<LevelDefinition> definitions,
            out int invalidIndex)
        {
            invalidIndex = -1;
            if (definitions == null)
            {
                return false;
            }

            var chapterIds =
                new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0;
                 index < definitions.Count;
                 index++)
            {
                LevelDefinition definition = definitions[index];
                if (!TryCreateStage(definition, out var stage))
                {
                    if (definition != null &&
                        (!string.IsNullOrWhiteSpace(
                             definition.RestorationChapterId) ||
                         definition.RestorationStageCount != 0 ||
                         definition.RestorationStageIndex != 0))
                    {
                        invalidIndex = index;
                        return false;
                    }

                    continue;
                }

                if (stage.IsFirstStage &&
                    !chapterIds.Add(stage.ChapterId))
                {
                    invalidIndex = index;
                    return false;
                }

                int chapterStart = index - stage.StageIndex;
                if (chapterStart < 0 ||
                    chapterStart + stage.StageCount >
                        definitions.Count)
                {
                    invalidIndex = index;
                    return false;
                }

                for (var offset = 0;
                     offset < stage.StageCount;
                     offset++)
                {
                    int candidateIndex = chapterStart + offset;
                    if (!TryCreateStage(
                            definitions[candidateIndex],
                            out var candidate) ||
                        !string.Equals(
                            candidate.ChapterId,
                            stage.ChapterId,
                            StringComparison.Ordinal) ||
                        candidate.StageIndex != offset ||
                        candidate.StageCount != stage.StageCount)
                    {
                        invalidIndex = candidateIndex;
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
