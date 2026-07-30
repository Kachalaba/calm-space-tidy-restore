using CalmSpace.Demo;
using NUnit.Framework;

namespace CalmSpace.Tests.EditMode
{
    public sealed class WorkshopProfileRulesTests
    {
        private const string RoomBeatId =
            "cozy-workshop.pebble-shelf";
        private const string MemoryId =
            "family.summer-trail-stones";
        private const string ChapterId =
            "cozy-workshop";

        [Test]
        public void FirstCompletionRewardsOnceAndQueuesOrderedPresentations()
        {
            DemoProgressSnapshot initial =
                DemoProgressRules.CreateDefault(8, "sage");
            var command = new CompleteLevelCommand(
                RoomBeatId,
                1,
                15,
                new[]
                {
                    PendingPresentationEntry.RoomReveal(RoomBeatId),
                    PendingPresentationEntry.Memory(MemoryId),
                    PendingPresentationEntry.Finale(ChapterId)
                });

            ProfileMutationResult<LevelCompletionMutation> first =
                DemoProgressRules.CompleteLevel(initial, command, 8);
            ProfileMutationResult<LevelCompletionMutation> replay =
                DemoProgressRules.CompleteLevel(
                    first.Snapshot,
                    command,
                    8);

            Assert.That(first.Status, Is.EqualTo(
                ProfileMutationStatus.Applied));
            Assert.That(first.IsSuccess, Is.True);
            Assert.That(first.Payload.FirstCompletion, Is.True);
            Assert.That(first.Payload.RewardAmount, Is.EqualTo(15));
            Assert.That(first.Payload.TokenBalance, Is.EqualTo(15));
            Assert.That(
                first.Snapshot.PendingPresentationCount,
                Is.EqualTo(3));
            AssertPending(
                first.Snapshot,
                0,
                PendingPresentationKind.RoomReveal,
                RoomBeatId);
            AssertPending(
                first.Snapshot,
                1,
                PendingPresentationKind.Memory,
                MemoryId);
            AssertPending(
                first.Snapshot,
                2,
                PendingPresentationKind.Finale,
                ChapterId);

            Assert.That(replay.Status, Is.EqualTo(
                ProfileMutationStatus.AlreadyApplied));
            Assert.That(replay.IsSuccess, Is.True);
            Assert.That(replay.Payload.FirstCompletion, Is.False);
            Assert.That(replay.Payload.RewardAmount, Is.Zero);
            Assert.That(replay.Snapshot, Is.EqualTo(first.Snapshot));
        }

        [Test]
        public void CompletionRejectsDuplicateLogicalPresentations()
        {
            DemoProgressSnapshot initial =
                DemoProgressRules.CreateDefault(8, "sage");
            var command = new CompleteLevelCommand(
                RoomBeatId,
                1,
                15,
                new[]
                {
                    PendingPresentationEntry.Finale(
                        ChapterId,
                        requiresExplicitLaunch: false),
                    PendingPresentationEntry.Finale(
                        ChapterId,
                        requiresExplicitLaunch: true)
                });

            ProfileMutationResult<LevelCompletionMutation> result =
                DemoProgressRules.CompleteLevel(initial, command, 8);

            AssertInvalidAndUnchanged(
                result.Status,
                initial,
                result.Snapshot);
        }

        [Test]
        public void PresentationSeenRemovesOnlyMatchingQueueHead()
        {
            DemoProgressSnapshot queued = CompleteWithPresentations();
            PendingPresentationEntry finale =
                PendingPresentationEntry.Finale(ChapterId);

            ProfileMutationResult<PresentationMutation> outOfOrder =
                DemoProgressRules.MarkPresentationSeen(
                    queued,
                    finale);
            ProfileMutationResult<PresentationMutation> room =
                DemoProgressRules.MarkPresentationSeen(
                    queued,
                    PendingPresentationEntry.RoomReveal(RoomBeatId));

            Assert.That(outOfOrder.Status, Is.EqualTo(
                ProfileMutationStatus.Invalid));
            Assert.That(outOfOrder.Snapshot, Is.EqualTo(queued));
            Assert.That(room.Status, Is.EqualTo(
                ProfileMutationStatus.Applied));
            Assert.That(room.Snapshot.HasSeenRoomReveal(RoomBeatId), Is.True);
            Assert.That(room.Snapshot.PendingPresentationCount, Is.EqualTo(2));
            AssertPending(
                room.Snapshot,
                0,
                PendingPresentationKind.Memory,
                MemoryId);

            DemoProgressSnapshot afterMemory =
                DemoProgressRules.MarkMemoryViewed(
                    room.Snapshot,
                    MemoryId).Snapshot;
            ProfileMutationResult<PresentationMutation> seenFinale =
                DemoProgressRules.MarkPresentationSeen(
                    afterMemory,
                    finale);
            Assert.That(seenFinale.Status, Is.EqualTo(
                ProfileMutationStatus.Applied));
            Assert.That(
                seenFinale.Snapshot.HasSeenFinale(ChapterId),
                Is.True);
            Assert.That(
                seenFinale.Snapshot.PendingPresentationCount,
                Is.Zero);
        }

        [Test]
        public void AlreadySeenLogicalQueueHeadIsRemovedDespiteLaunchFlag()
        {
            DemoProgressSnapshot initial = CreateSnapshot(
                0,
                new[] { "soft-fern" },
                new DecorationSelection[0],
                new[]
                {
                    PendingPresentationEntry.Finale(
                        ChapterId,
                        requiresExplicitLaunch: true),
                    PendingPresentationEntry.RoomReveal(RoomBeatId)
                },
                new[] { ChapterId });

            ProfileMutationResult<PresentationMutation> result =
                DemoProgressRules.MarkPresentationSeen(
                    initial,
                    PendingPresentationEntry.Finale(
                        ChapterId,
                        requiresExplicitLaunch: false));

            Assert.That(result.Status, Is.EqualTo(
                ProfileMutationStatus.Applied));
            Assert.That(result.Snapshot.PendingPresentationCount, Is.EqualTo(1));
            AssertPending(
                result.Snapshot,
                0,
                PendingPresentationKind.RoomReveal,
                RoomBeatId);
        }

        [Test]
        public void MemoryViewRemovesQueuedMemoryAndSupportsAlbumFirstView()
        {
            DemoProgressSnapshot queued = CompleteWithPresentations();
            ProfileMutationResult<MemoryMutation> queuedView =
                DemoProgressRules.MarkMemoryViewed(queued, MemoryId);
            ProfileMutationResult<MemoryMutation> albumView =
                DemoProgressRules.MarkMemoryViewed(
                    queuedView.Snapshot,
                    "family.album-only");
            ProfileMutationResult<MemoryMutation> replay =
                DemoProgressRules.MarkMemoryViewed(
                    albumView.Snapshot,
                    "family.album-only");

            Assert.That(queuedView.Status, Is.EqualTo(
                ProfileMutationStatus.Applied));
            Assert.That(queuedView.Payload.FirstView, Is.True);
            Assert.That(
                queuedView.Snapshot.HasViewedMemory(MemoryId),
                Is.True);
            Assert.That(
                queuedView.Snapshot.PendingPresentationCount,
                Is.EqualTo(2));
            AssertPending(
                queuedView.Snapshot,
                1,
                PendingPresentationKind.Finale,
                ChapterId);
            Assert.That(albumView.Payload.FirstView, Is.True);
            Assert.That(
                albumView.Snapshot.HasViewedMemory("family.album-only"),
                Is.True);
            Assert.That(replay.Status, Is.EqualTo(
                ProfileMutationStatus.AlreadyApplied));
            Assert.That(replay.Payload.FirstView, Is.False);
            Assert.That(replay.Snapshot, Is.EqualTo(albumView.Snapshot));
        }

        [Test]
        public void InvalidIdsAndLevelIndicesLeaveSnapshotEqual()
        {
            DemoProgressSnapshot initial =
                DemoProgressRules.CreateDefault(8, "sage");
            var emptyId = new CompleteLevelCommand(
                string.Empty,
                0,
                15,
                new PendingPresentationEntry[0]);
            var outOfRange = new CompleteLevelCommand(
                RoomBeatId,
                8,
                15,
                new PendingPresentationEntry[0]);

            ProfileMutationResult<LevelCompletionMutation> invalidId =
                DemoProgressRules.CompleteLevel(initial, emptyId, 8);
            ProfileMutationResult<LevelCompletionMutation> invalidIndex =
                DemoProgressRules.CompleteLevel(initial, outOfRange, 8);
            ProfileMutationResult<MemoryMutation> invalidMemory =
                DemoProgressRules.MarkMemoryViewed(initial, " ");

            AssertInvalidAndUnchanged(invalidId.Status, initial, invalidId.Snapshot);
            AssertInvalidAndUnchanged(
                invalidIndex.Status,
                initial,
                invalidIndex.Snapshot);
            AssertInvalidAndUnchanged(
                invalidMemory.Status,
                initial,
                invalidMemory.Snapshot);
        }

        [Test]
        public void PurchaseAtomicallySpendsOwnsAndChangesOnlyRequestedSlot()
        {
            DemoProgressSnapshot initial = CreateSnapshot(
                50,
                new[] { "soft-fern" },
                new[]
                {
                    new DecorationSelection("warm-light", "soft-fern"),
                    new DecorationSelection("workbench-accent", "soft-fern")
                });

            ProfileMutationResult<DecorationMutation> purchased =
                DemoProgressRules.PurchaseDecoration(
                    initial,
                    "workbench-accent",
                    "unknown.future-lamp",
                    30);

            Assert.That(purchased.Status, Is.EqualTo(
                ProfileMutationStatus.Applied));
            Assert.That(purchased.Payload.TokenDelta, Is.EqualTo(-30));
            Assert.That(purchased.Payload.TokenBalance, Is.EqualTo(20));
            Assert.That(
                purchased.Snapshot.OwnsDecoration("unknown.future-lamp"),
                Is.True);
            Assert.That(
                purchased.Snapshot.TryGetSelectedDecoration(
                    "workbench-accent",
                    out string selected),
                Is.True);
            Assert.That(selected, Is.EqualTo("unknown.future-lamp"));
            Assert.That(
                purchased.Snapshot.TryGetSelectedDecoration(
                    "warm-light",
                    out string untouched),
                Is.True);
            Assert.That(untouched, Is.EqualTo("soft-fern"));
        }

        [Test]
        public void PurchaseRejectsPaddedAliasWithoutSecondCharge()
        {
            DemoProgressSnapshot initial = CreateSnapshot(
                50,
                new[] { "soft-fern" },
                new DecorationSelection[0]);
            ProfileMutationResult<DecorationMutation> purchased =
                DemoProgressRules.PurchaseDecoration(
                    initial,
                    "workbench-accent",
                    "future-lamp",
                    10);

            ProfileMutationResult<DecorationMutation> paddedReplay =
                DemoProgressRules.PurchaseDecoration(
                    purchased.Snapshot,
                    "workbench-accent",
                    " future-lamp ",
                    10);

            Assert.That(purchased.Status, Is.EqualTo(
                ProfileMutationStatus.Applied));
            AssertInvalidAndUnchanged(
                paddedReplay.Status,
                purchased.Snapshot,
                paddedReplay.Snapshot);
            Assert.That(paddedReplay.Snapshot.CozyTokens, Is.EqualTo(40));
        }

        [Test]
        public void PaddedStableIdsAreInvalidAcrossMutationRules()
        {
            DemoProgressSnapshot initial =
                DemoProgressRules.CreateDefault(8, "sage");
            DemoProgressSnapshot paddedPresentation = CreateSnapshot(
                0,
                new[] { "soft-fern" },
                new DecorationSelection[0],
                new[]
                {
                    PendingPresentationEntry.RoomReveal(
                        " padded-room ")
                });

            ProfileMutationResult<LevelCompletionMutation> level =
                DemoProgressRules.CompleteLevel(
                    initial,
                    new CompleteLevelCommand(
                        " padded-level ",
                        0,
                        1,
                        new PendingPresentationEntry[0]),
                    8);
            ProfileMutationResult<PresentationMutation> presentation =
                DemoProgressRules.MarkPresentationSeen(
                    paddedPresentation,
                    PendingPresentationEntry.RoomReveal(
                        " padded-room "));
            ProfileMutationResult<MemoryMutation> memory =
                DemoProgressRules.MarkMemoryViewed(
                    initial,
                    " padded-memory ");
            ProfileMutationResult<DecorationMutation> grant =
                DemoProgressRules.GrantDecoration(
                    initial,
                    " padded-slot ",
                    "padded-decoration",
                    DecorationGrantSource.Rewarded);
            ProfileMutationResult<DailyCareMutation> care =
                DemoProgressRules.CompleteDailyCare(
                    initial,
                    " padded-care ",
                    20664,
                    5);
            ProfileMutationResult<PreferenceMutation> preference =
                DemoProgressRules.SetPreferences(
                    initial,
                    " sage ",
                    true);

            AssertInvalidAndUnchanged(
                level.Status,
                initial,
                level.Snapshot);
            AssertInvalidAndUnchanged(
                presentation.Status,
                paddedPresentation,
                presentation.Snapshot);
            AssertInvalidAndUnchanged(
                memory.Status,
                initial,
                memory.Snapshot);
            AssertInvalidAndUnchanged(
                grant.Status,
                initial,
                grant.Snapshot);
            AssertInvalidAndUnchanged(
                care.Status,
                initial,
                care.Snapshot);
            AssertInvalidAndUnchanged(
                preference.Status,
                initial,
                preference.Snapshot);
        }

        [Test]
        public void GrantOwnsAndSelectsWithoutSpendingTokens()
        {
            DemoProgressSnapshot initial = CreateSnapshot(
                50,
                new[] { "soft-fern" },
                new[]
                {
                    new DecorationSelection("warm-light", "soft-fern")
                });

            ProfileMutationResult<DecorationMutation> granted =
                DemoProgressRules.GrantDecoration(
                    initial,
                    "warm-light",
                    "rewarded-lantern",
                    DecorationGrantSource.Rewarded);

            Assert.That(granted.Status, Is.EqualTo(
                ProfileMutationStatus.Applied));
            Assert.That(granted.Payload.TokenDelta, Is.Zero);
            Assert.That(granted.Payload.TokenBalance, Is.EqualTo(50));
            Assert.That(
                granted.Snapshot.OwnsDecoration("rewarded-lantern"),
                Is.True);
            Assert.That(
                granted.Snapshot.TryGetSelectedDecoration(
                    "warm-light",
                    out string selected),
                Is.True);
            Assert.That(selected, Is.EqualTo("rewarded-lantern"));
        }

        [Test]
        public void DailyCareCannotGrantTwiceOnSameUtcDay()
        {
            DemoProgressSnapshot initial =
                DemoProgressRules.CreateDefault(8, "sage");

            ProfileMutationResult<DailyCareMutation> first =
                DemoProgressRules.CompleteDailyCare(
                    initial,
                    "family-tea",
                    20664,
                    5);
            ProfileMutationResult<DailyCareMutation> duplicate =
                DemoProgressRules.CompleteDailyCare(
                    first.Snapshot,
                    "family-tea",
                    20664,
                    5);

            Assert.That(first.Status, Is.EqualTo(
                ProfileMutationStatus.Applied));
            Assert.That(first.Payload.RewardAmount, Is.EqualTo(5));
            Assert.That(first.Payload.CompletionCount, Is.EqualTo(1));
            Assert.That(duplicate.Status, Is.EqualTo(
                ProfileMutationStatus.AlreadyApplied));
            Assert.That(duplicate.Payload.RewardAmount, Is.Zero);
            Assert.That(duplicate.Snapshot, Is.EqualTo(first.Snapshot));
        }

        [Test]
        public void ThirdDailyCareQueuesTeaPostcardOnce()
        {
            DemoProgressSnapshot state =
                DemoProgressRules.CreateDefault(8, "sage");

            for (var day = 20664; day < 20667; day++)
            {
                state = DemoProgressRules.CompleteDailyCare(
                    state,
                    "family-tea",
                    day,
                    5).Snapshot;
            }

            Assert.That(state.CompletedDailyCareCount, Is.EqualTo(3));
            Assert.That(state.PendingPresentationCount, Is.EqualTo(1));
            AssertPending(
                state,
                0,
                PendingPresentationKind.Memory,
                "family.tea-postcard-03");

            DemoProgressSnapshot fourth =
                DemoProgressRules.CompleteDailyCare(
                    state,
                    "family-tea",
                    20667,
                    5).Snapshot;
            Assert.That(fourth.PendingPresentationCount, Is.EqualTo(1));
        }

        [Test]
        public void ConstructorAndCommandDefensivelyCopyArrays()
        {
            var owned = new[] { "soft-fern" };
            var selections =
                new[]
                {
                    new DecorationSelection("warm-light", "soft-fern")
                };
            var pending =
                new[]
                {
                    PendingPresentationEntry.RoomReveal(RoomBeatId)
                };
            DemoProgressSnapshot snapshot = CreateSnapshot(
                0,
                owned,
                selections,
                pending);
            var command = new CompleteLevelCommand(
                "cozy-workshop.clear-passage",
                0,
                1,
                pending);

            owned[0] = "mutated";
            selections[0] =
                new DecorationSelection("mutated", "mutated");
            pending[0] = PendingPresentationEntry.Finale("mutated");

            Assert.That(snapshot.OwnsDecoration("soft-fern"), Is.True);
            Assert.That(snapshot.OwnsDecoration("mutated"), Is.False);
            AssertPending(
                snapshot,
                0,
                PendingPresentationKind.RoomReveal,
                RoomBeatId);
            Assert.That(
                command.TryGetPresentation(
                    0,
                    out PendingPresentationEntry entry),
                Is.True);
            Assert.That(
                entry.Kind,
                Is.EqualTo(PendingPresentationKind.RoomReveal));
            Assert.That(entry.StableId, Is.EqualTo(RoomBeatId));
        }

        [Test]
        public void UnknownOwnedDecorationSurvivesPreferenceMutation()
        {
            DemoProgressSnapshot initial = CreateSnapshot(
                10,
                new[] { "soft-fern", "future.unknown-decoration" },
                new DecorationSelection[0]);

            ProfileMutationResult<PreferenceMutation> changed =
                DemoProgressRules.SetPreferences(
                    initial,
                    "ocean",
                    false);

            Assert.That(changed.Status, Is.EqualTo(
                ProfileMutationStatus.Applied));
            Assert.That(
                changed.Snapshot.OwnsDecoration(
                    "future.unknown-decoration"),
                Is.True);
            Assert.That(changed.Snapshot.SelectedThemeId, Is.EqualTo("ocean"));
            Assert.That(changed.Snapshot.MusicEnabled, Is.False);
        }

        [Test]
        public void NormalizePreservesUnknownOwnedDecorationIds()
        {
            DemoProgressSnapshot initial = CreateSnapshot(
                10,
                new[] { "future.unknown-decoration" },
                new DecorationSelection[0]);

            DemoProgressSnapshot normalized =
                DemoProgressRules.Normalize(initial, 8, "sage");

            Assert.That(
                normalized.OwnsDecoration(
                    "future.unknown-decoration"),
                Is.True);
            Assert.That(
                normalized.OwnsDecoration("soft-fern"),
                Is.True);
        }

        [Test]
        public void NormalizeCanonicalizesPaddedDecorationIdsAndSlots()
        {
            DemoProgressSnapshot initial = CreateSnapshot(
                10,
                new[]
                {
                    " future.unknown-decoration ",
                    "future.unknown-decoration"
                },
                new[]
                {
                    new DecorationSelection(
                        " future-slot ",
                        " future.unknown-decoration ")
                });

            DemoProgressSnapshot normalized =
                DemoProgressRules.Normalize(initial, 8, "sage");

            Assert.That(normalized.OwnedDecorationCount, Is.EqualTo(2));
            Assert.That(
                normalized.OwnsDecoration(
                    "future.unknown-decoration"),
                Is.True);
            Assert.That(
                normalized.TryGetSelectedDecoration(
                    "future-slot",
                    out string selected),
                Is.True);
            Assert.That(
                selected,
                Is.EqualTo("future.unknown-decoration"));
        }

        [Test]
        public void DefaultMutationResultIsNotSuccessful()
        {
            ProfileMutationResult<PreferenceMutation> result = default;

            Assert.That(
                result.Status,
                Is.EqualTo(ProfileMutationStatus.Applied));
            Assert.That(result.IsSuccess, Is.False);
        }

        private static DemoProgressSnapshot CompleteWithPresentations()
        {
            DemoProgressSnapshot initial =
                DemoProgressRules.CreateDefault(8, "sage");
            return DemoProgressRules.CompleteLevel(
                initial,
                new CompleteLevelCommand(
                    RoomBeatId,
                    1,
                    15,
                    new[]
                    {
                        PendingPresentationEntry.RoomReveal(RoomBeatId),
                        PendingPresentationEntry.Memory(MemoryId),
                        PendingPresentationEntry.Finale(ChapterId)
                    }),
                8).Snapshot;
        }

        private static DemoProgressSnapshot CreateSnapshot(
            int tokens,
            string[] owned,
            DecorationSelection[] selections,
            PendingPresentationEntry[] pending = null,
            string[] seenFinaleIds = null)
        {
            return new DemoProgressSnapshot(
                0,
                0,
                "sage",
                true,
                tokens,
                0,
                owned,
                selections,
                new string[0],
                new string[0],
                seenFinaleIds ?? new string[0],
                pending ?? new PendingPresentationEntry[0],
                0,
                0);
        }

        private static void AssertPending(
            DemoProgressSnapshot snapshot,
            int index,
            PendingPresentationKind kind,
            string stableId)
        {
            Assert.That(
                snapshot.TryGetPendingPresentation(
                    index,
                    out PendingPresentationEntry entry),
                Is.True);
            Assert.That(entry.Kind, Is.EqualTo(kind));
            Assert.That(entry.StableId, Is.EqualTo(stableId));
        }

        private static void AssertInvalidAndUnchanged(
            ProfileMutationStatus status,
            DemoProgressSnapshot expected,
            DemoProgressSnapshot actual)
        {
            Assert.That(status, Is.EqualTo(ProfileMutationStatus.Invalid));
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
