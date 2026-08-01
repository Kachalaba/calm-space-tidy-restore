using CalmSpace.Demo;
using CalmSpace.Levels;
using CalmSpace.Workshop;
using NUnit.Framework;
using UnityEditor;

namespace CalmSpace.Tests.EditMode
{
    public sealed class WorkshopProgressProjectorTests
    {
        private const string ChapterId = "cozy-workshop";
        private const string LevelCatalogPath =
            "Assets/CalmSpace/Config/LevelCatalog.asset";
        private const string WorkshopCatalogPath =
            "Assets/CalmSpace/Config/LivingWorkshopCatalog.asset";

        [Test]
        public void FreshProfileProjectsFirstWorkshopStage()
        {
            var projector = CreateProjector();

            Assert.That(
                projector.TryProject(
                    DemoProgressRules.CreateDefault(8, "sage"),
                    out WorkshopProgressProjection projection),
                Is.True);
            Assert.That(projection.BeatCount, Is.EqualTo(8));
            Assert.That(projection.CompletedBeatCount, Is.Zero);
            Assert.That(
                projection.ActiveHotspotBeatId,
                Is.EqualTo("cozy-workshop.clear-passage"));
            Assert.That(projection.RestoredZoneMask, Is.Zero);
            Assert.That(
                projector.GetRecommendedAction(
                    DemoProgressRules.CreateDefault(8, "sage"))
                    ?.Kind,
                Is.EqualTo(WorkshopRecommendedActionKind.StartLevel));
        }

        [Test]
        public void CompletedBeatIsRestoredBeforeItsQueuedReveal()
        {
            var projector = CreateProjector();
            DemoProgressSnapshot snapshot = CreateSnapshot(
                completedMask: 1,
                highestUnlocked: 1,
                PendingPresentationEntry.RoomReveal(
                    "cozy-workshop.clear-passage"));

            Assert.That(
                projector.TryProject(snapshot, out var projection),
                Is.True);
            Assert.That(projection.RestoredZoneMask, Is.EqualTo(1));
            Assert.That(
                projection.PendingRevealBeatId,
                Is.EqualTo("cozy-workshop.clear-passage"));
            Assert.That(
                projector.GetRecommendedAction(snapshot)?.Kind,
                Is.EqualTo(
                    WorkshopRecommendedActionKind.ShowPendingReveal));
        }

        [Test]
        public void RemovedRevealAdvancesToQueuedMemoryThenNextStage()
        {
            var projector = CreateProjector();
            DemoProgressSnapshot memoryQueued = CreateSnapshot(
                completedMask: 3,
                highestUnlocked: 2,
                PendingPresentationEntry.Memory(
                    "family.summer-trail-stones"));
            DemoProgressSnapshot noPresentation = CreateSnapshot(
                completedMask: 3,
                highestUnlocked: 2);

            Assert.That(
                projector.GetRecommendedAction(memoryQueued)?.Kind,
                Is.EqualTo(
                    WorkshopRecommendedActionKind.ShowPendingMemory));
            WorkshopRecommendedAction? next =
                projector.GetRecommendedAction(noPresentation);
            Assert.That(next?.Kind, Is.EqualTo(
                WorkshopRecommendedActionKind.StartLevel));
            Assert.That(next?.StableId, Is.EqualTo(
                "cozy-workshop.tea-drawer"));
        }

        [Test]
        public void LiveFinaleQueueFollowsRoomAndMemory()
        {
            var projector = CreateProjector();
            DemoProgressSnapshot snapshot = CreateSnapshot(
                completedMask: 255,
                highestUnlocked: 7,
                PendingPresentationEntry.RoomReveal(
                    "cozy-workshop.open-window"),
                PendingPresentationEntry.Memory(
                    "family.open-windows"),
                PendingPresentationEntry.Finale(ChapterId));

            Assert.That(
                projector.GetRecommendedAction(snapshot)?.Kind,
                Is.EqualTo(
                    WorkshopRecommendedActionKind.ShowPendingReveal));
            snapshot = CreateSnapshot(
                255,
                7,
                PendingPresentationEntry.Memory("family.open-windows"),
                PendingPresentationEntry.Finale(ChapterId));
            Assert.That(
                projector.GetRecommendedAction(snapshot)?.Kind,
                Is.EqualTo(
                    WorkshopRecommendedActionKind.ShowPendingMemory));
            snapshot = CreateSnapshot(
                255,
                7,
                PendingPresentationEntry.Finale(ChapterId));
            Assert.That(
                projector.GetRecommendedAction(snapshot)?.Kind,
                Is.EqualTo(WorkshopRecommendedActionKind.ShowFinale));
        }

        [Test]
        public void MigratedExplicitFinaleStaysBehindCompletedWorkshopCta()
        {
            var projector = CreateProjector();
            DemoProgressSnapshot snapshot = CreateSnapshot(
                255,
                7,
                PendingPresentationEntry.Finale(
                    ChapterId,
                    requiresExplicitLaunch: true));

            Assert.That(
                projector.GetRecommendedAction(snapshot)?.Kind,
                Is.EqualTo(
                    WorkshopRecommendedActionKind.OpenCompletedWorkshop));
            Assert.That(
                projector.GetExplicitWorkshopAction(snapshot)?.Kind,
                Is.EqualTo(WorkshopRecommendedActionKind.ShowFinale));
        }

        [Test]
        public void SeenFinaleWithEmptyQueueOpensCompletedWorkshopWithoutRepeatingLevelEight()
        {
            var projector = CreateProjector();
            DemoProgressSnapshot snapshot = new DemoProgressSnapshot(
                7,
                255,
                "sage",
                true,
                0,
                0,
                new string[0],
                new DecorationSelection[0],
                new string[0],
                new string[0],
                new[] { ChapterId },
                new PendingPresentationEntry[0],
                0,
                0);

            WorkshopRecommendedAction? action =
                projector.GetRecommendedAction(snapshot);
            Assert.That(action?.Kind, Is.EqualTo(
                WorkshopRecommendedActionKind.OpenCompletedWorkshop));
            Assert.That(action?.Kind, Is.Not.EqualTo(
                WorkshopRecommendedActionKind.StartLevel));
        }

        [Test]
        public void InvalidWorkshopMetadataFailsClosedWithoutBreakingLevelCatalog()
        {
            LevelCatalog levels = LoadLevels();
            LivingWorkshopCatalog invalid =
                UnityEngine.ScriptableObject.CreateInstance<
                    LivingWorkshopCatalog>();
            try
            {
                var projector = new WorkshopProgressProjector(
                    levels,
                    invalid,
                    ChapterId);

                Assert.That(
                    projector.TryProject(
                        DemoProgressRules.CreateDefault(8, "sage"),
                        out _),
                    Is.False);
                Assert.That(
                    projector.GetRecommendedAction(
                        DemoProgressRules.CreateDefault(8, "sage")),
                    Is.Null);
                Assert.That(
                    levels.TryFindEntry("01-soft-blocks", out var index, out _),
                    Is.True);
                Assert.That(index, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalid);
            }
        }

        private static WorkshopProgressProjector CreateProjector()
        {
            return new WorkshopProgressProjector(
                LoadLevels(),
                AssetDatabase.LoadAssetAtPath<LivingWorkshopCatalog>(
                    WorkshopCatalogPath),
                ChapterId);
        }

        private static LevelCatalog LoadLevels()
        {
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                LevelCatalogPath);
        }

        private static DemoProgressSnapshot CreateSnapshot(
            int completedMask,
            int highestUnlocked,
            params PendingPresentationEntry[] pending)
        {
            return new DemoProgressSnapshot(
                highestUnlocked,
                completedMask,
                "sage",
                true,
                0,
                0,
                new string[0],
                new DecorationSelection[0],
                new string[0],
                new string[0],
                new string[0],
                pending,
                0,
                0);
        }
    }
}
