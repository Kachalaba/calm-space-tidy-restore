using System;

namespace CalmSpace.Workshop
{
    /// <summary>
    /// Outcome of one room reveal presentation. The presenter never persists
    /// anything, so the caller decides what a completed playback means.
    /// </summary>
    public enum WorkshopRevealPlaybackResult
    {
        Completed = 0,
        Cancelled = 1,
        MissingVisual = 2
    }

    /// <summary>
    /// The immutable visual snapshot the room renders. It is derived from
    /// completed levels only, so the permanent look of the room is a pure
    /// function of the persisted profile and never of transient UI state.
    /// </summary>
    public readonly struct WorkshopRoomVisualState :
        IEquatable<WorkshopRoomVisualState>
    {
        public const int MaxZoneCount = 31;

        public WorkshopRoomVisualState(
            string chapterId,
            int restoredZoneMask,
            string activeHotspotBeatId,
            bool isComplete)
        {
            ChapterId = chapterId ?? string.Empty;
            RestoredZoneMask = restoredZoneMask;
            ActiveHotspotBeatId = activeHotspotBeatId ?? string.Empty;
            IsComplete = isComplete;
        }

        public string ChapterId { get; }

        public int RestoredZoneMask { get; }

        public string ActiveHotspotBeatId { get; }

        public bool IsComplete { get; }

        public static WorkshopRoomVisualState FromProjection(
            WorkshopProgressProjection projection)
        {
            return new WorkshopRoomVisualState(
                projection.ChapterId,
                projection.RestoredZoneMask,
                projection.ActiveHotspotBeatId,
                projection.IsComplete);
        }

        public bool IsZoneRestored(int zoneIndex)
        {
            return zoneIndex >= 0 &&
                zoneIndex < MaxZoneCount &&
                (RestoredZoneMask & (1 << zoneIndex)) != 0;
        }

        public bool Equals(WorkshopRoomVisualState other)
        {
            return RestoredZoneMask == other.RestoredZoneMask &&
                IsComplete == other.IsComplete &&
                string.Equals(
                    ChapterId, other.ChapterId, StringComparison.Ordinal) &&
                string.Equals(
                    ActiveHotspotBeatId,
                    other.ActiveHotspotBeatId,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is WorkshopRoomVisualState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RestoredZoneMask;
                hash = (hash * 397) ^ (IsComplete ? 1 : 0);
                hash = (hash * 397) ^
                    (ChapterId != null ? ChapterId.GetHashCode() : 0);
                hash = (hash * 397) ^
                    (ActiveHotspotBeatId != null
                        ? ActiveHotspotBeatId.GetHashCode()
                        : 0);
                return hash;
            }
        }
    }
}
