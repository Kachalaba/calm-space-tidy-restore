using CalmSpace.Demo;
using CalmSpace.Workshop;
using NUnit.Framework;

namespace CalmSpace.Tests.EditMode
{
    public sealed class DemoProgressStoreMigrationTests
    {
        [Test]
        public void SecureVersionOnePreservesProgressPreferencesAndRewards()
        {
            var source = new SecureProfileV1Dto
            {
                version = 1,
                highestUnlockedLevelIndex = 6,
                completedLevelMask = 0b00110101,
                selectedThemeId = "ocean",
                musicEnabled = false,
                cozyTokens = 73,
                rewardedLevelMask = 0b00100101,
                ownedDecorationMask = 0b1011,
                selectedDecorationIndex = 3
            };

            DemoProgressSnapshot migrated =
                LegacyProfileV1Migration.Migrate(
                    source,
                    8,
                    "sage");

            Assert.That(migrated.HighestUnlockedLevelIndex, Is.EqualTo(6));
            Assert.That(
                migrated.CompletedLevelMask,
                Is.EqualTo(0b00110101));
            Assert.That(
                migrated.RewardedLevelMask,
                Is.EqualTo(0b00100101));
            Assert.That(migrated.CozyTokens, Is.EqualTo(73));
            Assert.That(migrated.SelectedThemeId, Is.EqualTo("ocean"));
            Assert.That(migrated.MusicEnabled, Is.False);
            Assert.That(migrated.OwnsDecoration("soft-fern"), Is.True);
            Assert.That(migrated.OwnsDecoration("river-stones"), Is.True);
            Assert.That(migrated.OwnsDecoration("clay-vase"), Is.True);
            Assert.That(
                migrated.TryGetSelectedDecoration(
                    WorkshopContentIds.WorkbenchAccentDecorSlotId,
                    out string selected),
                Is.True);
            Assert.That(selected, Is.EqualTo("clay-vase"));
            Assert.That(
                migrated.TryGetSelectedDecoration(
                    WorkshopContentIds.WarmLightDecorSlotId,
                    out string warmLight),
                Is.True);
            Assert.That(warmLight, Is.EqualTo("linen-shade"));
        }

        [TestCase(0, "soft-fern", "workbench-accent")]
        [TestCase(1, "river-stones", "workbench-accent")]
        [TestCase(2, "warm-lantern", "warm-light")]
        [TestCase(3, "clay-vase", "workbench-accent")]
        public void EveryVersionOneDecorationBitMigratesIndependently(
            int legacyIndex,
            string decorationId,
            string mappedSlotId)
        {
            var source = new SecureProfileV1Dto
            {
                version = 1,
                selectedThemeId = "sage",
                musicEnabled = true,
                ownedDecorationMask = 1 << legacyIndex,
                selectedDecorationIndex = legacyIndex
            };

            DemoProgressSnapshot migrated =
                LegacyProfileV1Migration.Migrate(source, 8, "sage");

            Assert.That(
                migrated.OwnsDecoration(decorationId),
                Is.True);
            Assert.That(
                migrated.OwnsDecoration("soft-fern"),
                Is.True);
            Assert.That(
                migrated.OwnsDecoration("linen-shade"),
                Is.True);
            Assert.That(
                migrated.TryGetSelectedDecoration(
                    mappedSlotId,
                    out string selected),
                Is.True);
            Assert.That(selected, Is.EqualTo(decorationId));
        }

        [Test]
        public void CompletedVersionOneBeatsBecomeSeenWithoutViewingOrQueueingMemories()
        {
            var source = new SecureProfileV1Dto
            {
                version = 1,
                highestUnlockedLevelIndex = 7,
                completedLevelMask = 0b10010011,
                selectedThemeId = "sage",
                musicEnabled = true,
                ownedDecorationMask = 1,
                selectedDecorationIndex = 0
            };

            DemoProgressSnapshot migrated =
                LegacyProfileV1Migration.Migrate(source, 8, "sage");

            Assert.That(
                migrated.HasSeenRoomReveal(
                    WorkshopContentIds.ClearPassageBeatId),
                Is.True);
            Assert.That(
                migrated.HasSeenRoomReveal(
                    WorkshopContentIds.PebbleShelfBeatId),
                Is.True);
            Assert.That(
                migrated.HasSeenRoomReveal(
                    WorkshopContentIds.FastenerTrayBeatId),
                Is.True);
            Assert.That(
                migrated.HasSeenRoomReveal(
                    WorkshopContentIds.OpenWindowBeatId),
                Is.True);
            Assert.That(
                migrated.HasViewedMemory(
                    WorkshopContentIds.SummerTrailStonesMemoryId),
                Is.False);
            Assert.That(
                migrated.HasViewedMemory(
                    WorkshopContentIds.FixEverythingMemoryId),
                Is.False);
            Assert.That(
                migrated.HasViewedMemory(
                    WorkshopContentIds.OpenWindowsMemoryId),
                Is.False);
            Assert.That(migrated.PendingPresentationCount, Is.Zero);
        }

        [Test]
        public void FullyCompletedVersionOneQueuesOnlyExplicitFinale()
        {
            var source = new SecureProfileV1Dto
            {
                version = 1,
                highestUnlockedLevelIndex = 7,
                completedLevelMask = 0b11111111,
                selectedThemeId = "sage",
                musicEnabled = true,
                ownedDecorationMask = 1,
                selectedDecorationIndex = 0
            };

            DemoProgressSnapshot migrated =
                LegacyProfileV1Migration.Migrate(source, 8, "sage");

            Assert.That(migrated.SeenRoomRevealCount, Is.EqualTo(8));
            Assert.That(migrated.ViewedMemoryCount, Is.Zero);
            Assert.That(migrated.PendingPresentationCount, Is.EqualTo(1));
            Assert.That(
                migrated.TryGetPendingPresentation(
                    0,
                    out PendingPresentationEntry finale),
                Is.True);
            Assert.That(
                finale.Kind,
                Is.EqualTo(PendingPresentationKind.Finale));
            Assert.That(
                finale.StableId,
                Is.EqualTo(WorkshopContentIds.CozyWorkshopChapterId));
            Assert.That(finale.RequiresExplicitLaunch, Is.True);
        }
    }
}
