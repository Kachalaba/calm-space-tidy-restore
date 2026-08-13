using System;

namespace CalmSpace.Demo
{
    public interface IDemoProgressStore
    {
        event Action<DemoProgressSnapshot> ProgressChanged;

        bool IsInitialized { get; }

        int LevelCount { get; }

        DemoProgressSnapshot Current { get; }

        ProfileInitializationResult Initialize(
            int levelCount,
            string defaultThemeId,
            int completionReward);

        ProfileMutationResult<LevelCompletionMutation> CompleteLevel(
            CompleteLevelCommand command);

        ProfileMutationResult<PresentationMutation> MarkPresentationSeen(
            PendingPresentationEntry presentation);

        ProfileMutationResult<MemoryMutation> MarkMemoryViewed(
            string memoryId);

        ProfileMutationResult<DecorationMutation>
            PurchaseAndSelectDecoration(
                string slotId,
                string decorationId,
                int cost);

        ProfileMutationResult<DecorationMutation>
            GrantAndSelectDecoration(
                string slotId,
                string decorationId,
                DecorationGrantSource source);

        ProfileMutationResult<DecorationMutation> SelectDecoration(
            string slotId,
            string decorationId);

        ProfileMutationResult<DailyCareMutation> CompleteDailyCare(
            string careId,
            int utcDayKey,
            int rewardAmount,
            string unlockedMemoryId);

        ProfileMutationResult<PreferenceMutation> SetSelectedTheme(
            string themeId);

        ProfileMutationResult<PreferenceMutation> SetMusicEnabled(
            bool enabled);

        bool IsLevelUnlocked(int levelIndex);

        bool IsLevelCompleted(int levelIndex);

        void MarkLevelCompleted(int levelIndex);

        int CompleteLevelAndReward(
            int levelIndex,
            int rewardAmount);

        bool TryPurchaseAndSelectDecoration(
            int decorationIndex,
            int cost);

        bool TrySelectDecoration(int decorationIndex);
    }
}
