namespace CalmSpace.Workshop
{
    public enum LevelLaunchSource
    {
        Workshop = 0,
        Catalog = 1,
        DailyCare = 2
    }

    public readonly struct LevelLaunchRequest
    {
        public LevelLaunchRequest(
            string levelId,
            int levelIndex,
            LevelLaunchSource source,
            string chapterId,
            string beatId)
        {
            LevelId = levelId ?? string.Empty;
            LevelIndex = levelIndex;
            Source = source;
            ChapterId = chapterId ?? string.Empty;
            BeatId = beatId ?? string.Empty;
        }

        public string LevelId { get; }

        public int LevelIndex { get; }

        public LevelLaunchSource Source { get; }

        public string ChapterId { get; }

        public string BeatId { get; }
    }

    public enum WorkshopRecommendedActionKind
    {
        StartLevel = 0,
        ShowPendingReveal = 1,
        ShowPendingMemory = 2,
        ShowFinale = 3,
        OpenCompletedWorkshop = 4
    }

    public readonly struct WorkshopRecommendedAction
    {
        public WorkshopRecommendedAction(
            WorkshopRecommendedActionKind kind,
            string stableId)
        {
            Kind = kind;
            StableId = stableId ?? string.Empty;
        }

        public WorkshopRecommendedActionKind Kind { get; }

        public string StableId { get; }
    }

    public readonly struct WorkshopProgressProjection
    {
        public WorkshopProgressProjection(
            string chapterId,
            int restoredZoneMask,
            int completedBeatCount,
            int beatCount,
            string activeHotspotBeatId,
            string pendingRevealBeatId,
            bool dailyCareUnlocked,
            bool isComplete)
        {
            ChapterId = chapterId ?? string.Empty;
            RestoredZoneMask = restoredZoneMask;
            CompletedBeatCount = completedBeatCount;
            BeatCount = beatCount;
            ActiveHotspotBeatId = activeHotspotBeatId ?? string.Empty;
            PendingRevealBeatId = pendingRevealBeatId ?? string.Empty;
            DailyCareUnlocked = dailyCareUnlocked;
            IsComplete = isComplete;
        }

        public string ChapterId { get; }

        public int RestoredZoneMask { get; }

        public int CompletedBeatCount { get; }

        public int BeatCount { get; }

        public string ActiveHotspotBeatId { get; }

        public string PendingRevealBeatId { get; }

        public bool DailyCareUnlocked { get; }

        public bool IsComplete { get; }
    }
}
