using System.Collections.Generic;
using CalmSpace.Cleaning;
using CalmSpace.Input;
using CalmSpace.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
            "Assets/CalmSpace/Content/Levels/06FreshSurface.prefab"
        };

        private static readonly LevelType[] ExpectedTypes =
        {
            LevelType.Fitting,
            LevelType.Sorting,
            LevelType.Fitting,
            LevelType.Sorting,
            LevelType.ScrewPuzzle,
            LevelType.Cleaning
        };

        [Test]
        public void DemoCatalogContainsSixUniqueValidEntries()
        {
            LevelCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                    CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Count, Is.EqualTo(6));

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
    }
}
