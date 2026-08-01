using System;
using CalmSpace.Demo;
using CalmSpace.Levels;

namespace CalmSpace.Workshop
{
    /// <summary>
    /// Converts the immutable profile into workshop presentation state. This
    /// never retains a projection, so each caller observes the current store
    /// snapshot without mutable UI state between refreshes.
    /// </summary>
    public sealed class WorkshopProgressProjector
    {
        private readonly LevelCatalog _levels;
        private readonly LivingWorkshopCatalog _workshop;
        private readonly string _chapterId;
        private readonly int _stageCount;
        private readonly bool _metadataValid;

        public WorkshopProgressProjector(
            LevelCatalog levels,
            LivingWorkshopCatalog workshop,
            string chapterId)
        {
            _levels = levels;
            _workshop = workshop;
            _chapterId = Normalize(chapterId);

            WorkshopCatalogValidationResult validation =
                WorkshopCatalogValidator.ValidateChapter(
                    _levels,
                    _workshop,
                    _chapterId);
            RestorationChapterInfo chapter = default;
            bool hasChapter = _levels != null &&
                _levels.TryGetRestorationChapter(
                    _chapterId,
                    out chapter);
            _metadataValid = validation.IsValid && hasChapter;
            _stageCount = _metadataValid ? chapter.StageCount : 0;
        }

        public bool TryProject(
            DemoProgressSnapshot snapshot,
            out WorkshopProgressProjection projection)
        {
            projection = default;
            if (!_metadataValid)
            {
                return false;
            }

            var restoredZoneMask = 0;
            var completedBeatCount = 0;
            var dailyCareUnlocked = false;
            var activeHotspotBeatId = string.Empty;

            for (var stageIndex = 0;
                 stageIndex < _stageCount;
                 stageIndex++)
            {
                if (!TryResolveStage(
                        stageIndex,
                        out WorkshopBeatDefinition beat,
                        out int levelIndex,
                        out _))
                {
                    projection = default;
                    return false;
                }

                bool completed = IsCompleted(snapshot, levelIndex);
                if (completed)
                {
                    completedBeatCount++;
                    if (beat.ZoneIndex >= 0 && beat.ZoneIndex < 31)
                    {
                        restoredZoneMask |= 1 << beat.ZoneIndex;
                    }

                    if (beat.UnlocksDailyCare)
                    {
                        dailyCareUnlocked = true;
                    }
                    continue;
                }

                if (string.IsNullOrEmpty(activeHotspotBeatId) &&
                    DemoProgressRules.IsLevelUnlocked(
                        snapshot,
                        levelIndex,
                        _levels.Count))
                {
                    activeHotspotBeatId = beat.BeatId;
                }
            }

            projection = new WorkshopProgressProjection(
                _chapterId,
                restoredZoneMask,
                completedBeatCount,
                _stageCount,
                activeHotspotBeatId,
                FindPendingRevealBeatId(snapshot),
                dailyCareUnlocked,
                completedBeatCount == _stageCount);
            return true;
        }

        public WorkshopRecommendedAction? GetRecommendedAction(
            DemoProgressSnapshot snapshot)
        {
            if (!TryProject(snapshot, out WorkshopProgressProjection projection))
            {
                return null;
            }

            for (var index = 0;
                 index < snapshot.PendingPresentationCount;
                 index++)
            {
                if (!snapshot.TryGetPendingPresentation(
                        index,
                        out PendingPresentationEntry pending) ||
                    pending.RequiresExplicitLaunch)
                {
                    continue;
                }

                switch (pending.Kind)
                {
                    case PendingPresentationKind.RoomReveal:
                        return new WorkshopRecommendedAction(
                            WorkshopRecommendedActionKind.ShowPendingReveal,
                            pending.StableId);
                    case PendingPresentationKind.Memory:
                        return new WorkshopRecommendedAction(
                            WorkshopRecommendedActionKind.ShowPendingMemory,
                            pending.StableId);
                    case PendingPresentationKind.Finale:
                        return new WorkshopRecommendedAction(
                            WorkshopRecommendedActionKind.ShowFinale,
                            pending.StableId);
                }
            }

            if (!string.IsNullOrEmpty(projection.ActiveHotspotBeatId))
            {
                return new WorkshopRecommendedAction(
                    WorkshopRecommendedActionKind.StartLevel,
                    projection.ActiveHotspotBeatId);
            }

            return projection.IsComplete
                ? new WorkshopRecommendedAction(
                    WorkshopRecommendedActionKind.OpenCompletedWorkshop,
                    _chapterId)
                : (WorkshopRecommendedAction?)null;
        }

        public WorkshopRecommendedAction? GetExplicitWorkshopAction(
            DemoProgressSnapshot snapshot)
        {
            WorkshopRecommendedAction? recommended =
                GetRecommendedAction(snapshot);
            if (!recommended.HasValue ||
                recommended.Value.Kind !=
                    WorkshopRecommendedActionKind.OpenCompletedWorkshop)
            {
                return recommended;
            }

            for (var index = 0;
                 index < snapshot.PendingPresentationCount;
                 index++)
            {
                if (!snapshot.TryGetPendingPresentation(
                        index,
                        out PendingPresentationEntry pending) ||
                    pending.Kind != PendingPresentationKind.Finale ||
                    !pending.RequiresExplicitLaunch ||
                    !string.Equals(
                        pending.StableId,
                        _chapterId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                return new WorkshopRecommendedAction(
                    WorkshopRecommendedActionKind.ShowFinale,
                    pending.StableId);
            }

            return recommended;
        }

        internal bool TryResolveStartLevel(
            string beatId,
            out int levelIndex,
            out LevelCatalogEntry entry,
            out WorkshopBeatDefinition beat)
        {
            levelIndex = -1;
            entry = null;
            beat = null;
            if (!_metadataValid ||
                string.IsNullOrWhiteSpace(beatId) ||
                !_workshop.TryFindBeat(beatId, out WorkshopBeatDefinition found) ||
                !string.Equals(found.ChapterId, _chapterId,
                    StringComparison.Ordinal) ||
                !_levels.TryFindRestorationStage(
                    _chapterId,
                    found.StageIndex,
                    out levelIndex,
                    out entry))
            {
                return false;
            }

            beat = found;
            return true;
        }

        internal bool TryResolveCompletion(
            string levelId,
            int levelIndex,
            out WorkshopBeatDefinition beat)
        {
            beat = null;
            if (!_metadataValid ||
                string.IsNullOrWhiteSpace(levelId) ||
                !_levels.TryGetEntry(levelIndex, out LevelCatalogEntry entry) ||
                !string.Equals(entry.Definition.LevelId, levelId,
                    StringComparison.Ordinal) ||
                !_levels.TryGetRestorationStage(
                    levelIndex,
                    out RestorationStageInfo stage) ||
                !string.Equals(stage.ChapterId, _chapterId,
                    StringComparison.Ordinal) ||
                !_workshop.TryFindBeat(
                    _chapterId,
                    stage.StageIndex,
                    out WorkshopBeatDefinition found))
            {
                return false;
            }

            beat = found;
            return true;
        }

        private bool TryResolveStage(
            int stageIndex,
            out WorkshopBeatDefinition beat,
            out int levelIndex,
            out LevelCatalogEntry entry)
        {
            beat = null;
            levelIndex = -1;
            entry = null;
            return _workshop.TryFindBeat(
                       _chapterId,
                       stageIndex,
                       out beat) &&
                _levels.TryFindRestorationStage(
                    _chapterId,
                    stageIndex,
                    out levelIndex,
                    out entry);
        }

        private string FindPendingRevealBeatId(
            DemoProgressSnapshot snapshot)
        {
            for (var index = 0;
                 index < snapshot.PendingPresentationCount;
                 index++)
            {
                if (snapshot.TryGetPendingPresentation(
                        index,
                        out PendingPresentationEntry pending) &&
                    pending.Kind == PendingPresentationKind.RoomReveal &&
                    _workshop.TryFindBeat(
                        pending.StableId,
                        out WorkshopBeatDefinition beat) &&
                    string.Equals(beat.ChapterId, _chapterId,
                        StringComparison.Ordinal))
                {
                    return pending.StableId;
                }
            }

            return string.Empty;
        }

        private static bool IsCompleted(
            DemoProgressSnapshot snapshot,
            int levelIndex)
        {
            return levelIndex >= 0 && levelIndex < 31 &&
                (snapshot.CompletedLevelMask & (1 << levelIndex)) != 0;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
