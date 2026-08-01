using System;
using System.Collections.Generic;
using CalmSpace.Demo;

namespace CalmSpace.Analytics
{
    /// <summary>
    /// Runtime-only admission gates for workshop analytics transitions.
    /// Producers retain ownership of tracking and call these gates after their
    /// durable action succeeds; this class has no Unity lifecycle dependency.
    /// </summary>
    public sealed class WorkshopAnalyticsSessionState
    {
        private readonly HashSet<int> _availableDailyCareDayKeys =
            new HashSet<int>();
        private readonly HashSet<string> _unlockedMemoryIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _completedRevealBeatIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _completedChapterIds =
            new HashSet<string>(StringComparer.Ordinal);
        private int _activeRewardedOfferSessionId;
        private int _nextRewardedOfferSessionId;

        public bool TryAdmitDailyCareAvailable(int utcDayKey)
        {
            return _availableDailyCareDayKeys.Add(utcDayKey);
        }

        public bool TryBeginRewardedOffer(out int offerSessionId)
        {
            offerSessionId = 0;
            if (_activeRewardedOfferSessionId != 0)
            {
                return false;
            }

            _nextRewardedOfferSessionId++;
            if (_nextRewardedOfferSessionId <= 0)
            {
                _nextRewardedOfferSessionId = 1;
            }

            _activeRewardedOfferSessionId = _nextRewardedOfferSessionId;
            offerSessionId = _activeRewardedOfferSessionId;
            return true;
        }

        public bool TryAdmitRewardedOfferOutcome(int offerSessionId)
        {
            if (offerSessionId <= 0 ||
                offerSessionId != _activeRewardedOfferSessionId)
            {
                return false;
            }

            _activeRewardedOfferSessionId = 0;
            return true;
        }

        public bool TryAdmitMemoryUnlocked(
            string memoryId,
            ProfileMutationStatus status)
        {
            return IsApplied(status) &&
                TryAddStableId(_unlockedMemoryIds, memoryId);
        }

        public bool TryAdmitRestorationRevealCompleted(
            string beatId,
            ProfileMutationStatus status)
        {
            return IsApplied(status) &&
                TryAddStableId(_completedRevealBeatIds, beatId);
        }

        public bool TryAdmitChapterCompleted(
            string chapterId,
            ProfileMutationStatus status)
        {
            return IsApplied(status) &&
                TryAddStableId(_completedChapterIds, chapterId);
        }

        public bool TryAdmitRestorationRevealStarted()
        {
            return true;
        }

        public bool TryAdmitMemoryViewed()
        {
            return true;
        }

        private static bool IsApplied(ProfileMutationStatus status)
        {
            return status == ProfileMutationStatus.Applied;
        }

        private static bool TryAddStableId(
            HashSet<string> admittedIds,
            string stableId)
        {
            return !string.IsNullOrWhiteSpace(stableId) &&
                admittedIds.Add(stableId);
        }
    }
}
