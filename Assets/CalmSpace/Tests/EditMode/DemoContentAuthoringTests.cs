using System.Collections.Generic;
using CalmSpace.Cleaning;
using CalmSpace.Demo;
using CalmSpace.Fasteners;
using CalmSpace.Input;
using CalmSpace.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CalmSpace.Tests.EditMode
{
    public sealed class DemoContentAuthoringTests
    {
        private const string CatalogPath =
            "Assets/CalmSpace/Config/LevelCatalog.asset";

        private static readonly string[] PrefabPaths =
        {
            "Assets/CalmSpace/Content/Levels/DemoFitting.prefab",
            "Assets/CalmSpace/Content/Levels/02PebblePairs.prefab",
            "Assets/CalmSpace/Content/Levels/03TeaDrawer.prefab",
            "Assets/CalmSpace/Content/Levels/04ColorShelf.prefab",
            "Assets/CalmSpace/Content/Levels/05FastenerTray.prefab",
            "Assets/CalmSpace/Content/Levels/06FreshSurface.prefab",
            "Assets/CalmSpace/Content/Levels/07CabinetHinge.prefab",
            "Assets/CalmSpace/Content/Levels/08DustyWindow.prefab"
        };

        private static readonly LevelType[] ExpectedTypes =
        {
            LevelType.Fitting,
            LevelType.Sorting,
            LevelType.Fitting,
            LevelType.Sorting,
            LevelType.ScrewPuzzle,
            LevelType.Cleaning,
            LevelType.ScrewPuzzle,
            LevelType.Cleaning
        };

        [Test]
        public void DemoCatalogContainsEightUniqueValidEntries()
        {
            LevelCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                    CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Count, Is.EqualTo(8));

            var ids = new HashSet<string>();
            for (var index = 0; index < catalog.Count; index++)
            {
                Assert.That(
                    catalog.TryGetEntry(index, out var entry),
                    Is.True,
                    "Invalid catalog entry at index " + index);
                Assert.That(entry.Definition.Type, Is.EqualTo(
                    ExpectedTypes[index]));
                Assert.That(
                    ids.Add(entry.Definition.LevelId),
                    Is.True,
                    "Duplicate id " + entry.Definition.LevelId);
                Assert.That(
                    entry.Prefab.RuntimeKeyIsValid(),
                    Is.True);
            }
        }

        [Test]
        public void EveryAuthoredPrefabMatchesItsDefinitionType()
        {
            for (var index = 0; index < PrefabPaths.Length; index++)
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        PrefabPaths[index]);
                Assert.That(
                    prefab,
                    Is.Not.Null,
                    PrefabPaths[index]);

                LevelBase level =
                    prefab.GetComponent<LevelBase>();
                Assert.That(level, Is.Not.Null);
                Assert.That(
                    level.SupportedType,
                    Is.EqualTo(ExpectedTypes[index]));
                Assert.That(level.Definition, Is.Not.Null);
                Assert.That(
                    level.Definition.Type,
                    Is.EqualTo(ExpectedTypes[index]));

                if (ExpectedTypes[index] != LevelType.Cleaning)
                {
                    ItemSnapController[] items =
                        prefab.GetComponentsInChildren<
                            ItemSnapController>(true);
                    Assert.That(items.Length, Is.GreaterThan(0));
                }
            }
        }

        [Test]
        public void CleaningPrefabHasCompleteGpuMaskContract()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPaths[5]);
            Assert.That(prefab, Is.Not.Null);

            RenderTextureCleaner cleaner =
                prefab.GetComponent<RenderTextureCleaner>();
            CleaningInputController input =
                prefab.GetComponent<CleaningInputController>();
            MeshCollider surface =
                prefab.GetComponentInChildren<MeshCollider>(true);

            Assert.That(cleaner, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(surface, Is.Not.Null);
            Assert.That(
                surface.GetComponent<Renderer>()?.sharedMaterial?.shader
                    ?.name,
                Is.EqualTo("CalmSpace/CleanableSurface"));

            var serialized = new SerializedObject(cleaner);
            Assert.That(
                serialized.FindProperty("cleanSurfaceCollider")
                    .objectReferenceValue,
                Is.EqualTo(surface));
            Assert.That(
                serialized.FindProperty("visibleRenderer")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("maskBrushShader")
                    .objectReferenceValue,
                Is.Not.Null);
        }

        [Test]
        public void EveryScrewLevelIsFullyFastened()
        {
            for (var index = 0; index < PrefabPaths.Length; index++)
            {
                if (ExpectedTypes[index] != LevelType.ScrewPuzzle)
                {
                    continue;
                }

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        PrefabPaths[index]);
                Assert.That(prefab, Is.Not.Null, PrefabPaths[index]);
                Assert.That(
                    prefab.GetComponent<ScrewInputController>(),
                    Is.Not.Null,
                    PrefabPaths[index] + " has no screw input.");

                ScrewController[] screws =
                    prefab.GetComponentsInChildren<ScrewController>(
                        true);
                FastenerPanel[] panels =
                    prefab.GetComponentsInChildren<FastenerPanel>(
                        true);
                Assert.That(
                    screws.Length,
                    Is.GreaterThan(0),
                    PrefabPaths[index]);
                Assert.That(
                    panels.Length,
                    Is.GreaterThan(0),
                    PrefabPaths[index]);

                var claimedScrews = new HashSet<Object>();
                for (var panelIndex = 0;
                     panelIndex < panels.Length;
                     panelIndex++)
                {
                    AssertPanelIsWired(
                        panels[panelIndex],
                        claimedScrews,
                        PrefabPaths[index]);
                }

                Assert.That(
                    claimedScrews.Count,
                    Is.EqualTo(screws.Length),
                    PrefabPaths[index] +
                    " has screws that hold no panel.");
            }
        }

        [Test]
        public void LayeredScrewLevelHidesEveryLayerBehindItsPanel()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/CalmSpace/Content/Levels/" +
                    "07CabinetHinge.prefab");
            Assert.That(prefab, Is.Not.Null);

            FastenerPanel[] panels =
                prefab.GetComponentsInChildren<FastenerPanel>(true);
            Assert.That(panels.Length, Is.EqualTo(2));

            var revealed = new HashSet<GameObject>();
            for (var index = 0; index < panels.Length; index++)
            {
                var serialized = new SerializedObject(panels[index]);
                var reveal = serialized
                        .FindProperty("_revealsOnRelease")
                        .objectReferenceValue as GameObject;
                if (reveal != null)
                {
                    Assert.That(
                        reveal.activeSelf,
                        Is.False,
                        "A covered layer must start hidden.");
                    Assert.That(revealed.Add(reveal), Is.True);
                }
            }

            Assert.That(
                revealed.Count,
                Is.EqualTo(1),
                "Exactly one layer sits under the outer panel.");
        }

        [Test]
        public void LayeredScrewLevelParksPanelsAtSeparateSocketTargets()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/CalmSpace/Content/Levels/" +
                    "07CabinetHinge.prefab");
            Assert.That(prefab, Is.Not.Null);

            ItemSnapController[] panels =
                prefab.GetComponentsInChildren<ItemSnapController>(
                    true);
            Assert.That(
                panels.Length,
                Is.EqualTo(2),
                "Level 07 should author one parking target per panel.");

            var targetNames = new List<string>();
            var targetBounds = new List<Bounds>();
            for (var index = 0; index < panels.Length; index++)
            {
                var serialized = new SerializedObject(panels[index]);
                var target = serialized.FindProperty("_snapTarget")
                        .objectReferenceValue as Transform;
                Assert.That(
                    target,
                    Is.Not.Null,
                    panels[index].name + " has no parking target.");

                Renderer socket =
                    target.GetComponentInChildren<Renderer>(true);
                Assert.That(
                    socket,
                    Is.Not.Null,
                    target.name + " has no authored socket bounds.");

                Bounds bounds = socket.bounds;
                for (var other = 0;
                     other < targetBounds.Count;
                     other++)
                {
                    Assert.That(
                        bounds.Intersects(targetBounds[other]),
                        Is.False,
                        target.name + " overlaps " +
                        targetNames[other] +
                        " at their authored parking positions.");
                }

                targetNames.Add(target.name);
                targetBounds.Add(bounds);
            }
        }

        [Test]
        public void StagedCleaningLevelGivesEveryPassItsOwnMask()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/CalmSpace/Content/Levels/" +
                    "08DustyWindow.prefab");
            Assert.That(prefab, Is.Not.Null);

            CleaningLevel level = prefab.GetComponent<CleaningLevel>();
            Assert.That(level, Is.Not.Null);

            var levelSerialized = new SerializedObject(level);
            Assert.That(
                levelSerialized.FindProperty("_cleaningInput")
                    .objectReferenceValue,
                Is.EqualTo(
                    prefab.GetComponent<CleaningInputController>()),
                "A staged level must drive its own cleaning input.");

            SerializedProperty stagesProperty =
                levelSerialized.FindProperty("_stages");
            Assert.That(stagesProperty.arraySize, Is.GreaterThan(1));

            var cleaners = new HashSet<Object>();
            for (var index = 0;
                 index < stagesProperty.arraySize;
                 index++)
            {
                var stage = stagesProperty
                        .GetArrayElementAtIndex(index)
                        .objectReferenceValue as CleaningStage;
                Assert.That(stage, Is.Not.Null, "Stage " + index);

                var stageSerialized = new SerializedObject(stage);
                var root = stageSerialized
                        .FindProperty("_stageRoot")
                        .objectReferenceValue as GameObject;
                Assert.That(root, Is.Not.Null, "Stage " + index);
                Assert.That(
                    root.activeSelf,
                    Is.EqualTo(index == 0),
                    "Only the first pass is authored visible.");

                var cleaner = stageSerialized
                        .FindProperty("_cleaner")
                        .objectReferenceValue as RenderTextureCleaner;
                Assert.That(
                    cleaner,
                    Is.Not.Null,
                    "Stage " + index + " has no surface.");
                Assert.That(
                    cleaners.Add(cleaner),
                    Is.True,
                    "Stages must not share one mask.");

                var cleanerSerialized = new SerializedObject(cleaner);
                Assert.That(
                    cleanerSerialized
                        .FindProperty("cleanSurfaceCollider")
                        .objectReferenceValue,
                    Is.Not.Null,
                    "Stage " + index);
                Assert.That(
                    cleanerSerialized.FindProperty("visibleRenderer")
                        .objectReferenceValue,
                    Is.Not.Null,
                    "Stage " + index);
                Assert.That(
                    cleanerSerialized.FindProperty("maskBrushShader")
                        .objectReferenceValue,
                    Is.Not.Null,
                    "Stage " + index);
            }

            ItemSnapController[] debris =
                prefab.GetComponentsInChildren<ItemSnapController>(
                    true);
            Assert.That(
                debris.Length,
                Is.GreaterThan(0),
                "The window keeps its drag-and-clear pass.");
        }

        private static void AssertPanelIsWired(
            FastenerPanel panel,
            HashSet<Object> claimedScrews,
            string prefabPath)
        {
            var serialized = new SerializedObject(panel);

            Assert.That(
                serialized.FindProperty("_item")
                    .objectReferenceValue,
                Is.EqualTo(panel.GetComponent<ItemSnapController>()),
                prefabPath + " panel carries no item.");

            SerializedProperty colliders =
                serialized.FindProperty("_lockedColliders");
            Assert.That(
                colliders.arraySize,
                Is.GreaterThan(0),
                prefabPath + " panel has no collider to lock.");
            for (var index = 0; index < colliders.arraySize; index++)
            {
                Assert.That(
                    colliders.GetArrayElementAtIndex(index)
                        .objectReferenceValue,
                    Is.Not.Null,
                    prefabPath + " panel has an empty collider slot.");
            }

            SerializedProperty screws =
                serialized.FindProperty("_screws");
            Assert.That(
                screws.arraySize,
                Is.GreaterThan(0),
                prefabPath + " panel is held by nothing.");
            for (var index = 0; index < screws.arraySize; index++)
            {
                Object screw = screws.GetArrayElementAtIndex(index)
                    .objectReferenceValue;
                Assert.That(screw, Is.Not.Null, prefabPath);
                Assert.That(
                    claimedScrews.Add(screw),
                    Is.True,
                    prefabPath + " shares a screw between panels.");
                Assert.That(
                    ((ScrewController)screw).Panel,
                    Is.EqualTo(panel),
                    prefabPath + " screw points at another panel.");
            }
        }

        [Test]
        public void DecorationCatalogCanBeCompletedFromDemoRewards()
        {
            DemoDecorationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<DemoDecorationCatalog>(
                    "Assets/CalmSpace/Config/" +
                    "DemoDecorationCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Count, Is.EqualTo(4));

            var totalCost = 0;
            var ids = new HashSet<string>();
            for (var index = 0; index < catalog.Count; index++)
            {
                Assert.That(
                    catalog.TryGetDecoration(
                        index,
                        out DemoDecorationDefinition decoration),
                    Is.True);
                Assert.That(ids.Add(decoration.Id), Is.True);
                totalCost += decoration.Cost;
            }

            Assert.That(
                totalCost,
                Is.LessThanOrEqualTo(
                    catalog.CompletionReward * 8),
                "Completing the demo should make every decoration " +
                "obtainable without replay grinding.");
        }
    }
}
