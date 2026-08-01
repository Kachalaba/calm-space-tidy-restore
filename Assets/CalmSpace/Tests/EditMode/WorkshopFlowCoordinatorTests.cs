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
        private const string WorkshopTextCatalogPath =
            "Assets/CalmSpace/Config/WorkshopTextCatalog.asset";

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
        public void OwnershipGraphInjectsTheWorkshopProjectorInterface()
        {
            LevelCatalog levels = AssetDatabase.LoadAssetAtPath<
                LevelCatalog>(LevelCatalogPath);
            LivingWorkshopCatalog workshop =
                AssetDatabase.LoadAssetAtPath<LivingWorkshopCatalog>(
                    WorkshopCatalogPath);
            WorkshopTextCatalog text =
                AssetDatabase.LoadAssetAtPath<WorkshopTextCatalog>(
                    WorkshopTextCatalogPath);
            var store = new InMemoryProgressStore(
                DemoProgressRules.CreateDefault(8, "sage"), 8);

            IWorkshopProgressProjector projector =
                new WorkshopProgressProjector(levels, workshop);
            IWorkshopFlowCoordinator coordinator =
                new WorkshopFlowCoordinator(
                    levels,
                    workshop,
                    store,
                    projector,
                    new WorkshopRuntimeAvailability(
                        levels,
                        workshop,
                        text));

            Assert.That(coordinator.GetRecommendedAction()?.Kind,
                Is.EqualTo(WorkshopRecommendedActionKind.StartLevel));
        }

        [Test]
        public void UninitializedStoreFailsClosedWithoutReadingOrMutatingIt()
        {
            var store = new InMemoryProgressStore(
                DemoProgressRules.CreateDefault(8, "sage"),
                8,
                isInitialized: false);
            var coordinator = CreateCoordinator(store);
            var start = new WorkshopRecommendedAction(
                WorkshopRecommendedActionKind.StartLevel,
                "01-soft-blocks");

            Assert.DoesNotThrow(() =>
            {
                Assert.That(coordinator.GetRecommendedAction(), Is.Null);
                Assert.That(
                    coordinator.GetExplicitWorkshopAction(),
                    Is.Null);
                Assert.That(
                    coordinator.TryCreateLaunchRequest(start, out _),
                    Is.False);
                Assert.That(
                    coordinator.CompleteLevel("01-soft-blocks", 0, 5)
                        .Status,
                    Is.EqualTo(ProfileMutationStatus.Invalid));
            });
            Assert.That(store.CompleteCallCount, Is.Zero);
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

        [Test]
        public void InvalidWorkshopMetadataKeepsCoreCatalogCompletionUsable()
        {
            LevelCatalog levels = AssetDatabase.LoadAssetAtPath<
                LevelCatalog>(LevelCatalogPath);
            LivingWorkshopCatalog invalid =
                UnityEngine.ScriptableObject.CreateInstance<
                    LivingWorkshopCatalog>();
            var store = new InMemoryProgressStore(
                DemoProgressRules.CreateDefault(8, "sage"), 8);
            WorkshopTextCatalog text =
                AssetDatabase.LoadAssetAtPath<WorkshopTextCatalog>(
                    WorkshopTextCatalogPath);
            try
            {
                var coordinator = new WorkshopFlowCoordinator(
                    levels,
                    invalid,
                    store,
                    new WorkshopProgressProjector(levels, invalid),
                    new WorkshopRuntimeAvailability(
                        levels,
                        invalid,
                        text));

                ProfileMutationResult<LevelCompletionMutation> valid =
                    coordinator.CompleteLevel("01-soft-blocks", 0, 5);
                ProfileMutationResult<LevelCompletionMutation> wrongId =
                    coordinator.CompleteLevel("not-the-level", 0, 5);
                ProfileMutationResult<LevelCompletionMutation> wrongIndex =
                    coordinator.CompleteLevel("01-soft-blocks", 7, 5);

                Assert.That(valid.Status,
                    Is.EqualTo(ProfileMutationStatus.Applied));
                Assert.That(store.CompleteCallCount, Is.EqualTo(1));
                Assert.That(store.LastCommand.PresentationCount, Is.Zero,
                    "Unavailable workshop metadata must not queue fake meta presentation.");
                Assert.That(wrongId.Status,
                    Is.EqualTo(ProfileMutationStatus.Invalid));
                Assert.That(wrongIndex.Status,
                    Is.EqualTo(ProfileMutationStatus.Invalid));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalid);
            }
        }

        [Test]
        public void InvalidWorkshopTextSuppressesAllMetaPresentations()
        {
            LevelCatalog levels = AssetDatabase.LoadAssetAtPath<
                LevelCatalog>(LevelCatalogPath);
            LivingWorkshopCatalog workshop =
                AssetDatabase.LoadAssetAtPath<LivingWorkshopCatalog>(
                    WorkshopCatalogPath);
            WorkshopTextCatalog emptyText =
                UnityEngine.ScriptableObject.CreateInstance<
                    WorkshopTextCatalog>();
            var store = new InMemoryProgressStore(
                DemoProgressRules.CreateDefault(8, "sage"), 8);
            try
            {
                var availability = new WorkshopRuntimeAvailability(
                    levels,
                    workshop,
                    emptyText);
                Assert.That(availability.MetaAvailable, Is.True);
                Assert.That(availability.TextAvailable, Is.False);
                Assert.That(availability.HomeMetaAvailable, Is.False);
                var coordinator = new WorkshopFlowCoordinator(
                    levels,
                    workshop,
                    store,
                    new WorkshopProgressProjector(levels, workshop),
                    availability);

                ProfileMutationResult<LevelCompletionMutation> valid =
                    coordinator.CompleteLevel("08-dusty-window", 7, 5);
                ProfileMutationResult<LevelCompletionMutation> wrongId =
                    coordinator.CompleteLevel("not-the-level", 0, 5);
                ProfileMutationResult<LevelCompletionMutation> wrongIndex =
                    coordinator.CompleteLevel("01-soft-blocks", 7, 5);

                Assert.That(valid.Status,
                    Is.EqualTo(ProfileMutationStatus.Applied));
                Assert.That(store.CompleteCallCount, Is.EqualTo(1));
                Assert.That(store.LastCommand.PresentationCount, Is.Zero,
                    "Invalid workshop text must suppress every optional meta presentation.");
                Assert.That(wrongId.Status,
                    Is.EqualTo(ProfileMutationStatus.Invalid));
                Assert.That(wrongIndex.Status,
                    Is.EqualTo(ProfileMutationStatus.Invalid));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(emptyText);
            }
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
            LevelCatalog levels = AssetDatabase.LoadAssetAtPath<
                LevelCatalog>(LevelCatalogPath);
            LivingWorkshopCatalog workshop =
                AssetDatabase.LoadAssetAtPath<LivingWorkshopCatalog>(
                    WorkshopCatalogPath);
            WorkshopTextCatalog text =
                AssetDatabase.LoadAssetAtPath<WorkshopTextCatalog>(
                    WorkshopTextCatalogPath);
            return new WorkshopFlowCoordinator(
                levels,
                workshop,
                store,
                new WorkshopProgressProjector(levels, workshop),
                new WorkshopRuntimeAvailability(
                    levels,
                    workshop,
                    text));
        }

        private sealed class InMemoryProgressStore : IDemoProgressStore
        {
            public InMemoryProgressStore(
                DemoProgressSnapshot snapshot,
                int levelCount,
                bool isInitialized = true)
            {
                _current = snapshot;
                LevelCount = levelCount;
                IsInitialized = isInitialized;
            }

            private DemoProgressSnapshot _current;

            public event Action<DemoProgressSnapshot> ProgressChanged;
            public bool IsInitialized { get; }
            public int LevelCount { get; }
            public DemoProgressSnapshot Current
            {
                get
                {
                    if (!IsInitialized)
                    {
                        throw new InvalidOperationException(
                            "Current must not be read before initialization.");
                    }

                    return _current;
                }
                private set => _current = value;
            }
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
