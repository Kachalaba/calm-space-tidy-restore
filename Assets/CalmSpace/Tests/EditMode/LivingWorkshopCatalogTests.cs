using System;
using System.Collections.Generic;
using System.Reflection;
using CalmSpace.Editor;
using CalmSpace.Levels;
using CalmSpace.Workshop;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CalmSpace.Tests.EditMode
{
    public sealed class LivingWorkshopCatalogTests
    {
        private const string LevelCatalogPath =
            "Assets/CalmSpace/Config/LevelCatalog.asset";
        private const string WorkshopCatalogPath =
            "Assets/CalmSpace/Config/LivingWorkshopCatalog.asset";

        private static readonly string[] ExpectedBeatIds =
        {
            "cozy-workshop.clear-passage",
            "cozy-workshop.pebble-shelf",
            "cozy-workshop.tea-drawer",
            "cozy-workshop.paint-shelf",
            "cozy-workshop.fastener-tray",
            "cozy-workshop.warm-workbench",
            "cozy-workshop.cabinet-hinge",
            "cozy-workshop.open-window"
        };

        private static readonly string[] ExpectedLevelIds =
        {
            "01-soft-blocks",
            "02-pebble-pairs",
            "03-tea-drawer",
            "04-color-shelf",
            "05-fastener-tray",
            "06-fresh-surface",
            "07-cabinet-hinge",
            "08-dusty-window"
        };

        private readonly List<LivingWorkshopCatalog> _createdWorkshops =
            new List<LivingWorkshopCatalog>();

        private readonly List<LevelCatalog> _createdLevelCatalogs =
            new List<LevelCatalog>();

        [TearDown]
        public void TearDown()
        {
            for (var index = 0;
                 index < _createdWorkshops.Count;
                 index++)
            {
                if (_createdWorkshops[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        _createdWorkshops[index]);
                }
            }

            _createdWorkshops.Clear();

            for (var index = 0;
                 index < _createdLevelCatalogs.Count;
                 index++)
            {
                if (_createdLevelCatalogs[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        _createdLevelCatalogs[index]);
                }
            }

            _createdLevelCatalogs.Clear();
        }

        [Test]
        public void StableContentIdsMatchTheLivingWorkshopContract()
        {
            Assert.That(
                WorkshopContentIds.CozyWorkshopChapterId,
                Is.EqualTo("cozy-workshop"));
            Assert.That(
                WorkshopContentIds.ClearPassageBeatId,
                Is.EqualTo("cozy-workshop.clear-passage"));
            Assert.That(
                WorkshopContentIds.PebbleShelfBeatId,
                Is.EqualTo("cozy-workshop.pebble-shelf"));
            Assert.That(
                WorkshopContentIds.TeaDrawerBeatId,
                Is.EqualTo("cozy-workshop.tea-drawer"));
            Assert.That(
                WorkshopContentIds.PaintShelfBeatId,
                Is.EqualTo("cozy-workshop.paint-shelf"));
            Assert.That(
                WorkshopContentIds.FastenerTrayBeatId,
                Is.EqualTo("cozy-workshop.fastener-tray"));
            Assert.That(
                WorkshopContentIds.WarmWorkbenchBeatId,
                Is.EqualTo("cozy-workshop.warm-workbench"));
            Assert.That(
                WorkshopContentIds.CabinetHingeBeatId,
                Is.EqualTo("cozy-workshop.cabinet-hinge"));
            Assert.That(
                WorkshopContentIds.OpenWindowBeatId,
                Is.EqualTo("cozy-workshop.open-window"));
            Assert.That(
                WorkshopContentIds.SummerTrailStonesMemoryId,
                Is.EqualTo("family.summer-trail-stones"));
            Assert.That(
                WorkshopContentIds.FixEverythingMemoryId,
                Is.EqualTo("family.fix-everything"));
            Assert.That(
                WorkshopContentIds.OpenWindowsMemoryId,
                Is.EqualTo("family.open-windows"));
            Assert.That(
                WorkshopContentIds.TeaPostcardMemoryId,
                Is.EqualTo("family.tea-postcard-03"));
            Assert.That(
                WorkshopContentIds.WorkbenchAccentDecorSlotId,
                Is.EqualTo("workbench-accent"));
            Assert.That(
                WorkshopContentIds.WarmLightDecorSlotId,
                Is.EqualTo("warm-light"));
            Assert.That(
                WorkshopContentIds.FamilyTeaDailyCareId,
                Is.EqualTo("family-tea"));
        }

        [Test]
        public void AuthoredCatalogHasExactStableBeatAndRewardMapping()
        {
            LivingWorkshopCatalog catalog =
                LoadWorkshopCatalog();
            Assert.That(catalog.Count, Is.EqualTo(8));

            var beatIds = new HashSet<string>();
            var zoneIndices = new HashSet<int>();
            for (var stageIndex = 0;
                 stageIndex < catalog.Count;
                 stageIndex++)
            {
                Assert.That(
                    catalog.TryGetBeat(stageIndex, out var beat),
                    Is.True,
                    "Missing beat at stage " + stageIndex);
                Assert.That(beat.IsValid, Is.True);
                Assert.That(
                    beat.ChapterId,
                    Is.EqualTo("cozy-workshop"));
                Assert.That(
                    beat.BeatId,
                    Is.EqualTo(ExpectedBeatIds[stageIndex]));
                Assert.That(
                    beat.StageIndex,
                    Is.EqualTo(stageIndex));
                Assert.That(
                    beat.ZoneIndex,
                    Is.EqualTo(stageIndex));
                Assert.That(beat.TitleTextKey, Is.Not.Empty);
                Assert.That(beat.ResultTextKey, Is.Not.Empty);
                Assert.That(beatIds.Add(beat.BeatId), Is.True);
                Assert.That(
                    zoneIndices.Add(beat.ZoneIndex),
                    Is.True);
            }

            AssertBeatMeta(
                catalog,
                1,
                "family.summer-trail-stones",
                string.Empty,
                unlocksDailyCare: false,
                isFinale: false);
            AssertBeatMeta(
                catalog,
                2,
                string.Empty,
                string.Empty,
                unlocksDailyCare: true,
                isFinale: false);
            AssertBeatMeta(
                catalog,
                3,
                string.Empty,
                "workbench-accent",
                unlocksDailyCare: false,
                isFinale: false);
            AssertBeatMeta(
                catalog,
                4,
                "family.fix-everything",
                string.Empty,
                unlocksDailyCare: false,
                isFinale: false);
            AssertBeatMeta(
                catalog,
                5,
                string.Empty,
                "warm-light",
                unlocksDailyCare: false,
                isFinale: false);
            AssertBeatMeta(
                catalog,
                7,
                "family.open-windows",
                string.Empty,
                unlocksDailyCare: false,
                isFinale: true);

            var finaleCount = 0;
            for (var index = 0; index < catalog.Count; index++)
            {
                catalog.TryGetBeat(index, out var beat);
                if (beat.IsFinale)
                {
                    finaleCount++;
                }
            }

            Assert.That(finaleCount, Is.EqualTo(1));
        }

        [Test]
        public void CatalogLookupsUseStableIdAndChapterStage()
        {
            LivingWorkshopCatalog catalog =
                LoadWorkshopCatalog();

            Assert.That(
                catalog.TryFindBeat(
                    "cozy-workshop",
                    4,
                    out var byStage),
                Is.True);
            Assert.That(
                byStage.BeatId,
                Is.EqualTo("cozy-workshop.fastener-tray"));
            Assert.That(
                catalog.TryFindBeat(
                    "cozy-workshop.fastener-tray",
                    out var byId),
                Is.True);
            Assert.That(byId, Is.SameAs(byStage));
            Assert.That(
                catalog.TryFindBeat(
                    "cozy-workshop",
                    8,
                    out _),
                Is.False);
            Assert.That(
                catalog.TryFindBeat("unknown-beat", out _),
                Is.False);
        }

        [Test]
        public void ValidatorJoinsEveryBeatToTheAuthoredLevelStage()
        {
            LevelCatalog levels = LoadLevelCatalog();
            LivingWorkshopCatalog workshop =
                LoadWorkshopCatalog();

            WorkshopCatalogValidationResult result =
                WorkshopCatalogValidator.ValidateChapter(
                    levels,
                    workshop,
                    "cozy-workshop");

            Assert.That(result.IsValid, Is.True);
            Assert.That(
                result.Code,
                Is.EqualTo(WorkshopCatalogValidationCode.Valid));
            Assert.That(result.InvalidStageIndex, Is.EqualTo(-1));
            Assert.That(
                result.StableId,
                Is.EqualTo("cozy-workshop"));

            for (var stageIndex = 0;
                 stageIndex < ExpectedLevelIds.Length;
                 stageIndex++)
            {
                Assert.That(
                    workshop.TryFindBeat(
                        "cozy-workshop",
                        stageIndex,
                        out var beat),
                    Is.True);
                Assert.That(
                    levels.TryFindRestorationStage(
                        beat.ChapterId,
                        beat.StageIndex,
                        out _,
                        out var level),
                    Is.True);
                Assert.That(
                    level.Definition.LevelId,
                    Is.EqualTo(ExpectedLevelIds[stageIndex]));
            }
        }

        [Test]
        public void ValidatorReturnsTypedCatalogFailures()
        {
            LevelCatalog levels = LoadLevelCatalog();
            AssertFailure(
                levels,
                CreateWorkshop(),
                WorkshopCatalogValidationCode.MissingBeat,
                expectedStageIndex: 0);

            WorkshopBeatDefinition[] duplicateId =
                CreateValidBeats();
            duplicateId[1] = CreateBeat(
                1,
                beatId: ExpectedBeatIds[0]);
            AssertFailure(
                levels,
                CreateWorkshop(duplicateId),
                WorkshopCatalogValidationCode.DuplicateBeatId,
                expectedStageIndex: 1,
                expectedStableId: ExpectedBeatIds[0]);

            WorkshopBeatDefinition[] duplicateZone =
                CreateValidBeats();
            duplicateZone[1] = CreateBeat(
                1,
                zoneIndex: 0);
            AssertFailure(
                levels,
                CreateWorkshop(duplicateZone),
                WorkshopCatalogValidationCode.DuplicateZoneIndex,
                expectedStageIndex: 1,
                expectedStableId: ExpectedBeatIds[1]);

            WorkshopBeatDefinition[] stageGap =
                CreateValidBeats();
            stageGap[1] = CreateBeat(
                8,
                beatId: ExpectedBeatIds[1],
                zoneIndex: 1);
            AssertFailure(
                levels,
                CreateWorkshop(stageGap),
                WorkshopCatalogValidationCode.StageGap,
                expectedStageIndex: 8,
                expectedStableId: ExpectedBeatIds[1]);

            WorkshopBeatDefinition[] invalidMemory =
                CreateValidBeats();
            invalidMemory[1] = CreateBeat(
                1,
                memoryId: "family.unknown");
            AssertFailure(
                levels,
                CreateWorkshop(invalidMemory),
                WorkshopCatalogValidationCode.InvalidMemoryLink,
                expectedStageIndex: 1,
                expectedStableId: "family.unknown");

            WorkshopBeatDefinition[] missingFinale =
                CreateValidBeats();
            missingFinale[7] = CreateBeat(
                7,
                isFinale: false);
            AssertFailure(
                levels,
                CreateWorkshop(missingFinale),
                WorkshopCatalogValidationCode.MissingFinale,
                expectedStageIndex: 7);

            WorkshopBeatDefinition[] multipleFinales =
                CreateValidBeats();
            multipleFinales[6] = CreateBeat(
                6,
                isFinale: true);
            AssertFailure(
                levels,
                CreateWorkshop(multipleFinales),
                WorkshopCatalogValidationCode.MultipleFinales,
                expectedStageIndex: 6,
                expectedStableId: ExpectedBeatIds[6]);
        }

        [Test]
        public void ValidatorRejectsReplacedStableBeatId()
        {
            WorkshopBeatDefinition[] beats = CreateValidBeats();
            beats[1] = CreateBeat(
                1,
                beatId: "cozy-workshop.replaced");

            AssertFailure(
                LoadLevelCatalog(),
                CreateWorkshop(beats),
                WorkshopCatalogValidationCode.MissingBeat,
                expectedStageIndex: 1);
        }

        [Test]
        public void ValidatorRejectsMissingMemoryLink()
        {
            WorkshopBeatDefinition[] beats = CreateValidBeats();
            beats[1] = CreateBeat(
                1,
                memoryId: string.Empty);

            AssertFailure(
                LoadLevelCatalog(),
                CreateWorkshop(beats),
                WorkshopCatalogValidationCode.InvalidMemoryLink,
                expectedStageIndex: 1,
                expectedStableId:
                    "family.summer-trail-stones");
        }

        [Test]
        public void ValidatorRejectsSwappedKnownMemoryLinks()
        {
            WorkshopBeatDefinition[] beats = CreateValidBeats();
            beats[1] = CreateBeat(
                1,
                memoryId: "family.fix-everything");
            beats[4] = CreateBeat(
                4,
                memoryId: "family.summer-trail-stones");

            AssertFailure(
                LoadLevelCatalog(),
                CreateWorkshop(beats),
                WorkshopCatalogValidationCode.InvalidMemoryLink,
                expectedStageIndex: 1,
                expectedStableId: "family.fix-everything");
        }

        [Test]
        public void BuildGateRejectsSwappedKnownMemoryLinks()
        {
            WorkshopBeatDefinition[] beats = CreateValidBeats();
            beats[1] = CreateBeat(
                1,
                memoryId: "family.fix-everything");
            beats[4] = CreateBeat(
                4,
                memoryId: "family.summer-trail-stones");

            BuildFailedException exception = Assert.Throws<
                BuildFailedException>(
                () => LivingWorkshopBuildValidator.ValidateOrThrow(
                    LoadLevelCatalog(),
                    CreateWorkshop(beats),
                    "cozy-workshop"));

            Assert.That(
                exception.Message,
                Does.Contain(
                    WorkshopCatalogValidationCode
                        .InvalidMemoryLink
                        .ToString()));
        }

        [Test]
        public void DefaultValidationResultIsInvalid()
        {
            WorkshopCatalogValidationResult result = default;

            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ValidatorRejectsMovedDecorUnlock()
        {
            WorkshopBeatDefinition[] beats = CreateValidBeats();
            beats[2] = CreateBeat(
                2,
                decorSlotId: "workbench-accent");
            beats[3] = CreateBeat(
                3,
                decorSlotId: string.Empty);

            AssertFailure(
                LoadLevelCatalog(),
                CreateWorkshop(beats),
                WorkshopCatalogValidationCode.InvalidDecorLink,
                expectedStageIndex: 2);
        }

        [Test]
        public void ValidatorRejectsUnknownDecorUnlock()
        {
            WorkshopBeatDefinition[] beats = CreateValidBeats();
            beats[3] = CreateBeat(
                3,
                decorSlotId: "unknown-slot");

            AssertFailure(
                LoadLevelCatalog(),
                CreateWorkshop(beats),
                WorkshopCatalogValidationCode.InvalidDecorLink,
                expectedStageIndex: 3);
        }

        [Test]
        public void ValidatorRejectsMovedDailyCareUnlock()
        {
            WorkshopBeatDefinition[] beats = CreateValidBeats();
            beats[1] = CreateBeat(
                1,
                unlocksDailyCare: true);
            beats[2] = CreateBeat(
                2,
                unlocksDailyCare: false);

            AssertFailure(
                LoadLevelCatalog(),
                CreateWorkshop(beats),
                WorkshopCatalogValidationCode.InvalidDailyCareUnlock,
                expectedStageIndex: 1);
        }

        [Test]
        public void ValidatorReportsOutOfRangeZonePrecisely()
        {
            WorkshopBeatDefinition[] beats = CreateValidBeats();
            beats[1] = CreateBeat(
                1,
                zoneIndex: 8);

            AssertFailure(
                LoadLevelCatalog(),
                CreateWorkshop(beats),
                WorkshopCatalogValidationCode.ZoneIndexOutOfRange,
                expectedStageIndex: 1);
        }

        [Test]
        public void ValidatorReportsFailedLevelJoin()
        {
            LevelCatalog source = LoadLevelCatalog();
            LevelCatalog brokenJoin =
                CreateCatalogWithInvalidEntry(source, 3);
            LivingWorkshopCatalog workshop =
                CreateWorkshop(CreateValidBeats());

            WorkshopCatalogValidationResult result =
                WorkshopCatalogValidator.ValidateChapter(
                    brokenJoin,
                    workshop,
                    "cozy-workshop");

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Code,
                Is.EqualTo(
                    WorkshopCatalogValidationCode.LevelJoinFailed));
            Assert.That(result.InvalidStageIndex, Is.EqualTo(3));
            Assert.That(
                result.StableId,
                Is.EqualTo(ExpectedBeatIds[3]));
        }

        [Test]
        public void RuntimeMetaResolutionReturnsCanonicalBeat()
        {
            Assert.That(
                WorkshopCatalogValidator.TryResolveMetaAction(
                    LoadLevelCatalog(),
                    LoadWorkshopCatalog(),
                    "cozy-workshop",
                    4,
                    out var beat),
                Is.True);
            Assert.That(
                beat.BeatId,
                Is.EqualTo("cozy-workshop.fastener-tray"));
            Assert.That(
                beat.MemoryId,
                Is.EqualTo("family.fix-everything"));
        }

        [Test]
        public void InvalidWorkshopReturnsNoMetaActionAndLevelStaysPlayable()
        {
            LevelCatalog levels = LoadLevelCatalog();
            Assert.That(
                levels.TryGetEntry(1, out var before),
                Is.True);
            Assert.That(
                levels.TryFindRestorationStage(
                    "cozy-workshop",
                    1,
                    out var levelIndex,
                    out var joinedBefore),
                Is.True);
            Assert.That(levelIndex, Is.EqualTo(1));
            Assert.That(joinedBefore, Is.SameAs(before));

            WorkshopBeatDefinition[] beats = CreateValidBeats();
            beats[1] = CreateBeat(
                1,
                memoryId: string.Empty);
            LivingWorkshopCatalog invalidWorkshop =
                CreateWorkshop(beats);

            Assert.That(
                WorkshopCatalogValidator.TryResolveMetaAction(
                    levels,
                    invalidWorkshop,
                    "cozy-workshop",
                    1,
                    out var metaAction),
                Is.False);
            Assert.That(metaAction, Is.Null);

            Assert.That(
                levels.TryGetEntry(1, out var after),
                Is.True,
                "Invalid workshop metadata must not disable the level.");
            Assert.That(after, Is.SameAs(before));
            Assert.That(
                levels.TryFindRestorationStage(
                    "cozy-workshop",
                    1,
                    out levelIndex,
                    out var joinedAfter),
                Is.True);
            Assert.That(levelIndex, Is.EqualTo(1));
            Assert.That(joinedAfter, Is.SameAs(before));
            Assert.That(
                after.Definition.LevelId,
                Is.EqualTo("02-pebble-pairs"));
        }

        [Test]
        public void MissingLevelChapterReturnsTypedFailure()
        {
            WorkshopCatalogValidationResult result =
                WorkshopCatalogValidator.ValidateChapter(
                    LoadLevelCatalog(),
                    CreateWorkshop(CreateValidBeats()),
                    "missing-chapter");

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Code,
                Is.EqualTo(
                    WorkshopCatalogValidationCode.MissingChapter));
            Assert.That(result.InvalidStageIndex, Is.EqualTo(-1));
            Assert.That(
                result.StableId,
                Is.EqualTo("missing-chapter"));
        }

        [Test]
        public void CurrentCatalogPassesThePlayerBuildHook()
        {
            Assert.DoesNotThrow(
                LivingWorkshopBuildValidator.ValidateProject);
            Assert.That(
                new LivingWorkshopBuildValidator().callbackOrder,
                Is.LessThan(0));
        }

        [Test]
        public void PlayerBuildHookThrowsForInvalidCatalogResult()
        {
            LevelCatalog levels = LoadLevelCatalog();
            WorkshopBeatDefinition[] beats = CreateValidBeats();
            beats[4] = CreateBeat(
                4,
                memoryId: "family.invalid");
            LivingWorkshopCatalog workshop =
                CreateWorkshop(beats);

            BuildFailedException exception = Assert.Throws<
                BuildFailedException>(
                () => LivingWorkshopBuildValidator.ValidateOrThrow(
                    levels,
                    workshop,
                    "cozy-workshop"));

            Assert.That(
                exception.Message,
                Does.Contain(
                    WorkshopCatalogValidationCode
                        .InvalidMemoryLink
                        .ToString()));
            Assert.That(
                exception.Message,
                Does.Contain("family.invalid"));
        }

        private static void AssertBeatMeta(
            LivingWorkshopCatalog catalog,
            int stageIndex,
            string expectedMemoryId,
            string expectedDecorSlotId,
            bool unlocksDailyCare,
            bool isFinale)
        {
            Assert.That(
                catalog.TryFindBeat(
                    "cozy-workshop",
                    stageIndex,
                    out var beat),
                Is.True);
            Assert.That(
                beat.MemoryId,
                Is.EqualTo(expectedMemoryId));
            Assert.That(
                beat.UnlockedDecorSlotId,
                Is.EqualTo(expectedDecorSlotId));
            Assert.That(
                beat.UnlocksDailyCare,
                Is.EqualTo(unlocksDailyCare));
            Assert.That(
                beat.IsFinale,
                Is.EqualTo(isFinale));
        }

        private static LivingWorkshopCatalog LoadWorkshopCatalog()
        {
            LivingWorkshopCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LivingWorkshopCatalog>(
                    WorkshopCatalogPath);
            Assert.That(
                catalog,
                Is.Not.Null,
                "Run Calm Space/Configure Project to author the " +
                "living workshop catalog.");
            return catalog;
        }

        private static LevelCatalog LoadLevelCatalog()
        {
            LevelCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                    LevelCatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return catalog;
        }

        private void AssertFailure(
            LevelCatalog levels,
            LivingWorkshopCatalog workshop,
            WorkshopCatalogValidationCode expectedCode,
            int expectedStageIndex,
            string expectedStableId = null)
        {
            WorkshopCatalogValidationResult result =
                WorkshopCatalogValidator.ValidateChapter(
                    levels,
                    workshop,
                    "cozy-workshop");

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Code, Is.EqualTo(expectedCode));
            Assert.That(
                result.InvalidStageIndex,
                Is.EqualTo(expectedStageIndex));
            if (expectedStableId != null)
            {
                Assert.That(
                    result.StableId,
                    Is.EqualTo(expectedStableId));
            }
        }

        private LivingWorkshopCatalog CreateWorkshop(
            params WorkshopBeatDefinition[] beats)
        {
            const BindingFlags Flags =
                BindingFlags.Instance |
                BindingFlags.NonPublic;
            FieldInfo beatsField =
                typeof(LivingWorkshopCatalog).GetField(
                    "_beats",
                    Flags);
            Assert.That(beatsField, Is.Not.Null);

            LivingWorkshopCatalog catalog =
                ScriptableObject.CreateInstance<
                    LivingWorkshopCatalog>();
            beatsField.SetValue(
                catalog,
                beats ?? Array.Empty<WorkshopBeatDefinition>());
            _createdWorkshops.Add(catalog);
            return catalog;
        }

        private LevelCatalog CreateCatalogWithInvalidEntry(
            LevelCatalog source,
            int invalidIndex)
        {
            const BindingFlags Flags =
                BindingFlags.Instance |
                BindingFlags.NonPublic;
            FieldInfo levelsField =
                typeof(LevelCatalog).GetField("_levels", Flags);
            FieldInfo definitionField =
                typeof(LevelCatalogEntry).GetField(
                    "_definition",
                    Flags);
            Assert.That(levelsField, Is.Not.Null);
            Assert.That(definitionField, Is.Not.Null);

            var entries = new LevelCatalogEntry[source.Count];
            for (var index = 0; index < entries.Length; index++)
            {
                Assert.That(
                    source.TryGetEntry(index, out var sourceEntry),
                    Is.True);
                entries[index] = sourceEntry;
            }

            Assert.That(
                source.TryGetEntry(
                    invalidIndex,
                    out var invalidSourceEntry),
                Is.True);
            var invalidEntry = new LevelCatalogEntry();
            definitionField.SetValue(
                invalidEntry,
                invalidSourceEntry.Definition);
            entries[invalidIndex] = invalidEntry;

            LevelCatalog catalog =
                ScriptableObject.CreateInstance<LevelCatalog>();
            levelsField.SetValue(catalog, entries);
            _createdLevelCatalogs.Add(catalog);
            return catalog;
        }

        private static WorkshopBeatDefinition[] CreateValidBeats()
        {
            var beats =
                new WorkshopBeatDefinition[ExpectedBeatIds.Length];
            for (var index = 0; index < beats.Length; index++)
            {
                beats[index] = CreateBeat(index);
            }

            return beats;
        }

        private static WorkshopBeatDefinition CreateBeat(
            int stageIndex,
            string beatId = null,
            int? zoneIndex = null,
            string memoryId = null,
            string decorSlotId = null,
            bool? unlocksDailyCare = null,
            bool? isFinale = null)
        {
            int authoredIndex = Math.Min(
                Math.Max(stageIndex, 0),
                ExpectedBeatIds.Length - 1);
            string authoredMemoryId = string.Empty;
            if (stageIndex == 1)
            {
                authoredMemoryId =
                    "family.summer-trail-stones";
            }
            else if (stageIndex == 4)
            {
                authoredMemoryId =
                    "family.fix-everything";
            }
            else if (stageIndex == 7)
            {
                authoredMemoryId =
                    "family.open-windows";
            }

            string authoredDecorSlotId =
                stageIndex == 3
                    ? "workbench-accent"
                    : stageIndex == 5
                        ? "warm-light"
                        : string.Empty;

            return new WorkshopBeatDefinition(
                "cozy-workshop",
                beatId ?? ExpectedBeatIds[authoredIndex],
                stageIndex,
                "beat." +
                ExpectedBeatIds[authoredIndex]
                    .Substring("cozy-workshop.".Length) +
                ".title",
                "beat." +
                ExpectedBeatIds[authoredIndex]
                    .Substring("cozy-workshop.".Length) +
                ".result",
                zoneIndex ?? stageIndex,
                memoryId ?? authoredMemoryId,
                decorSlotId ?? authoredDecorSlotId,
                unlocksDailyCare ?? stageIndex == 2,
                isFinale ?? stageIndex == 7);
        }
    }
}
