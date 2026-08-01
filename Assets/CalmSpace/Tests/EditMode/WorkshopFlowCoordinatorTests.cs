using System;
using CalmSpace.Demo;
using CalmSpace.Levels;
using CalmSpace.Workshop;
using NUnit.Framework;
using UnityEditor;

namespace CalmSpace.Tests.EditMode
{
    public sealed class WorkshopFlowCoordinatorTests
    {
        private const string ChapterId = "cozy-workshop";
        private const string LevelCatalogPath =
            "Assets/CalmSpace/Config/LevelCatalog.asset";
        private const string WorkshopCatalogPath =
            "Assets/CalmSpace/Config/LivingWorkshopCatalog.asset";

        [Test]
        public void StartActionCreatesTypedWorkshopLaunchRequest()
        {
            var store = new InMemoryProgressStore(
                DemoProgressRules.CreateDefault(8, "sage"), 8);
            var coordinator = CreateCoordinator(store);
            WorkshopRecommendedAction action =
                coordinator.GetRecommendedAction().Value;

            Assert.That(
                coordinator.TryCreateLaunchRequest(action, out var request),
                Is.True);
            Assert.That(request.LevelId, Is.EqualTo("01-soft-blocks"));
            Assert.That(request.LevelIndex, Is.Zero);
            Assert.That(request.Source, Is.EqualTo(
                LevelLaunchSource.Workshop));
            Assert.That(request.ChapterId, Is.EqualTo(ChapterId));
            Assert.That(request.BeatId, Is.EqualTo(action.StableId));
        }

        [Test]
        public void CompleteLevelQueuesRoomMemoryAndFinaleInOneStoreCall()
        {
            var store = new InMemoryProgressStore(
                DemoProgressRules.CreateDefault(8, "sage"), 8);
            var coordinator = CreateCoordinator(store);

            ProfileMutationResult<LevelCompletionMutation> result =
                coordinator.CompleteLevel("08-dusty-window", 7, 5);

            Assert.That(result.Status, Is.EqualTo(
                ProfileMutationStatus.Applied));
            Assert.That(store.CompleteCallCount, Is.EqualTo(1));
            Assert.That(store.LastCommand.PresentationCount, Is.EqualTo(3));
            AssertPresentation(
                store.LastCommand,
                0,
                PendingPresentationKind.RoomReveal,
                "cozy-workshop.open-window");
            AssertPresentation(
                store.LastCommand,
                1,
                PendingPresentationKind.Memory,
                "family.open-windows");
            AssertPresentation(
                store.LastCommand,
                2,
                PendingPresentationKind.Finale,
                ChapterId);
        }

        [Test]
        public void NonLaunchActionsNeverCreateLevelRequests()
        {
            var store = new InMemoryProgressStore(
                DemoProgressRules.CreateDefault(8, "sage"), 8);
            var coordinator = CreateCoordinator(store);
            var action = new WorkshopRecommendedAction(
                WorkshopRecommendedActionKind.ShowFinale,
                ChapterId);

            Assert.That(
                coordinator.TryCreateLaunchRequest(action, out _),
                Is.False);
        }

        private static void AssertPresentation(
            CompleteLevelCommand command,
            int index,
            PendingPresentationKind expectedKind,
            string expectedId)
        {
            Assert.That(command.TryGetPresentation(index, out var actual),
                Is.True);
            Assert.That(actual.Kind, Is.EqualTo(expectedKind));
            Assert.That(actual.StableId, Is.EqualTo(expectedId));
            Assert.That(actual.RequiresExplicitLaunch, Is.False);
        }

        private static WorkshopFlowCoordinator CreateCoordinator(
            IDemoProgressStore store)
        {
            return new WorkshopFlowCoordinator(
                AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogPath),
                AssetDatabase.LoadAssetAtPath<LivingWorkshopCatalog>(
                    WorkshopCatalogPath),
                store,
                ChapterId);
        }

        private sealed class InMemoryProgressStore : IDemoProgressStore
        {
            public InMemoryProgressStore(
                DemoProgressSnapshot snapshot,
                int levelCount)
            {
                Current = snapshot;
                LevelCount = levelCount;
                IsInitialized = true;
            }

            public event Action<DemoProgressSnapshot> ProgressChanged;
            public bool IsInitialized { get; }
            public int LevelCount { get; }
            public DemoProgressSnapshot Current { get; private set; }
            public int CompleteCallCount { get; private set; }
            public CompleteLevelCommand LastCommand { get; private set; }

            public ProfileInitializationResult Initialize(int levelCount, string defaultThemeId, int completionReward) { throw new NotSupportedException(); }
            public ProfileMutationResult<LevelCompletionMutation> CompleteLevel(CompleteLevelCommand command)
            {
                CompleteCallCount++;
                LastCommand = command;
                ProfileMutationResult<LevelCompletionMutation> result =
                    DemoProgressRules.CompleteLevel(Current, command, LevelCount);
                if (result.IsSuccess)
                {
                    Current = result.Snapshot;
                    ProgressChanged?.Invoke(Current);
                }
                return result;
            }
            public ProfileMutationResult<PresentationMutation> MarkPresentationSeen(PendingPresentationEntry presentation) { throw new NotSupportedException(); }
            public ProfileMutationResult<MemoryMutation> MarkMemoryViewed(string memoryId) { throw new NotSupportedException(); }
            public ProfileMutationResult<DecorationMutation> PurchaseAndSelectDecoration(string slotId, string decorationId, int cost) { throw new NotSupportedException(); }
            public ProfileMutationResult<DecorationMutation> GrantAndSelectDecoration(string slotId, string decorationId, DecorationGrantSource source) { throw new NotSupportedException(); }
            public ProfileMutationResult<DecorationMutation> SelectDecoration(string slotId, string decorationId) { throw new NotSupportedException(); }
            public ProfileMutationResult<DailyCareMutation> CompleteDailyCare(string careId, int utcDayKey, int rewardAmount, string unlockedMemoryId) { throw new NotSupportedException(); }
            public ProfileMutationResult<PreferenceMutation> SetSelectedTheme(string themeId) { throw new NotSupportedException(); }
            public ProfileMutationResult<PreferenceMutation> SetMusicEnabled(bool enabled) { throw new NotSupportedException(); }
            public bool IsLevelUnlocked(int levelIndex) { return DemoProgressRules.IsLevelUnlocked(Current, levelIndex, LevelCount); }
            public bool IsLevelCompleted(int levelIndex) { return DemoProgressRules.IsLevelCompleted(Current, levelIndex, LevelCount); }
            public void MarkLevelCompleted(int levelIndex) { throw new NotSupportedException(); }
            public int CompleteLevelAndReward(int levelIndex, int rewardAmount) { throw new NotSupportedException(); }
            public bool TryPurchaseAndSelectDecoration(int decorationIndex, int cost) { throw new NotSupportedException(); }
            public bool TrySelectDecoration(int decorationIndex) { throw new NotSupportedException(); }
        }
    }
}
