using System;
using CalmSpace.Demo;
using CalmSpace.Levels;

namespace CalmSpace.Workshop
{
    public interface IWorkshopFlowCoordinator
    {
        WorkshopRecommendedAction? GetRecommendedAction();

        WorkshopRecommendedAction? GetExplicitWorkshopAction();

        bool TryCreateLaunchRequest(
            WorkshopRecommendedAction action,
            out LevelLaunchRequest request);

        ProfileMutationResult<LevelCompletionMutation> CompleteLevel(
            string levelId,
            int levelIndex,
            int rewardAmount);
    }

    /// <summary>
    /// Composes profile mutations and navigation intent. Presentation and
    /// addressable level loading stay with callers outside this coordinator.
    /// </summary>
    public sealed class WorkshopFlowCoordinator : IWorkshopFlowCoordinator
    {
        private readonly LevelCatalog _levels;
        private readonly IDemoProgressStore _store;
        private readonly IWorkshopProgressProjector _projector;

        public WorkshopFlowCoordinator(
            LevelCatalog levels,
            LivingWorkshopCatalog workshop,
            IDemoProgressStore store,
            IWorkshopProgressProjector projector)
        {
            _levels = levels;
            _store = store;
            _projector = projector;
        }

        public WorkshopRecommendedAction? GetRecommendedAction()
        {
            return !IsStoreReady() || _projector == null
                ? (WorkshopRecommendedAction?)null
                : _projector.GetRecommendedAction(_store.Current);
        }

        public WorkshopRecommendedAction? GetExplicitWorkshopAction()
        {
            return !IsStoreReady() || _projector == null
                ? (WorkshopRecommendedAction?)null
                : _projector.GetExplicitWorkshopAction(_store.Current);
        }

        public bool TryCreateLaunchRequest(
            WorkshopRecommendedAction action,
            out LevelLaunchRequest request)
        {
            request = default;
            if (!IsStoreReady() ||
                action.Kind != WorkshopRecommendedActionKind.StartLevel)
            {
                return false;
            }

            if (_projector != null &&
                _projector.TryResolveStartLevel(
                    action.StableId,
                    out int levelIndex,
                    out LevelCatalogEntry workshopEntry,
                    out WorkshopBeatDefinition beat))
            {
                request = new LevelLaunchRequest(
                    workshopEntry.Definition.LevelId,
                    levelIndex,
                    LevelLaunchSource.Workshop,
                    beat.ChapterId,
                    beat.BeatId);
                return true;
            }

            if (_levels != null &&
                _levels.TryFindEntry(
                    action.StableId,
                    out int catalogIndex,
                    out LevelCatalogEntry catalogEntry))
            {
                request = new LevelLaunchRequest(
                    catalogEntry.Definition.LevelId,
                    catalogIndex,
                    LevelLaunchSource.Catalog,
                    string.Empty,
                    string.Empty);
                return true;
            }

            return false;
        }

        public ProfileMutationResult<LevelCompletionMutation> CompleteLevel(
            string levelId,
            int levelIndex,
            int rewardAmount)
        {
            if (!IsStoreReady())
            {
                return InvalidCompletion();
            }

            PendingPresentationEntry[] presentations;
            if (_projector != null &&
                _projector.TryResolveCompletion(
                    levelId,
                    levelIndex,
                    out WorkshopBeatDefinition beat))
            {
                presentations = CreatePresentations(beat);
            }
            else if (!IsExactCatalogLevel(levelId, levelIndex))
            {
                return InvalidCompletion();
            }
            else
            {
                presentations = Array.Empty<PendingPresentationEntry>();
            }

            return _store.CompleteLevel(
                new CompleteLevelCommand(
                    levelId,
                    levelIndex,
                    rewardAmount,
                    presentations));
        }

        private bool IsExactCatalogLevel(string levelId, int levelIndex)
        {
            return _levels != null &&
                _levels.TryGetEntry(levelIndex, out LevelCatalogEntry entry) &&
                string.Equals(
                    entry.Definition.LevelId,
                    levelId,
                    StringComparison.Ordinal);
        }

        private PendingPresentationEntry[] CreatePresentations(
            WorkshopBeatDefinition beat)
        {
            var count = 1;
            if (!string.IsNullOrEmpty(beat.MemoryId))
            {
                count++;
            }

            if (beat.IsFinale)
            {
                count++;
            }

            var presentations = new PendingPresentationEntry[count];
            var index = 0;
            presentations[index++] = PendingPresentationEntry.RoomReveal(
                beat.BeatId);
            if (!string.IsNullOrEmpty(beat.MemoryId))
            {
                presentations[index++] = PendingPresentationEntry.Memory(
                    beat.MemoryId);
            }

            if (beat.IsFinale)
            {
                presentations[index] = PendingPresentationEntry.Finale(
                    beat.ChapterId);
            }

            return presentations;
        }

        private ProfileMutationResult<LevelCompletionMutation>
            InvalidCompletion()
        {
            DemoProgressSnapshot snapshot = !IsStoreReady()
                ? default
                : _store.Current;
            return new ProfileMutationResult<LevelCompletionMutation>(
                ProfileMutationStatus.Invalid,
                snapshot,
                new LevelCompletionMutation(
                    false,
                    0,
                    snapshot.CozyTokens));
        }

        private bool IsStoreReady()
        {
            return _store != null && _store.IsInitialized;
        }
    }
}
