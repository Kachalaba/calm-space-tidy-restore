using System;
using System.Reflection;
using CalmSpace.Cleaning;
using CalmSpace.Demo;
using CalmSpace.Fasteners;
using CalmSpace.Input;
using CalmSpace.Levels;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace CalmSpace.Editor
{
    /// <summary>
    /// Authors the complete vertical-slice level set as Addressable prefabs.
    /// The builder is deterministic so every local and CI build receives the
    /// same catalog without relying on hand-edited scene objects.
    /// </summary>
    internal static class CalmSpaceDemoLevelBuilder
    {
        private const string CatalogPath =
            "Assets/CalmSpace/Config/LevelCatalog.asset";
        private const string CleanMaterialPath =
            "Assets/CalmSpace/Materials/CleanTray.mat";
        private const string AddressableLabel = "calm-space-level";

        /// <summary>
        /// Scattered debris seats, in level XZ. Hand-placed rather than
        /// generated so the sill never looks like a grid.
        /// </summary>
        private static readonly Vector2[] DebrisOffsets =
        {
            new Vector2(-0.64f, 0.46f),
            new Vector2(0.22f, -0.24f),
            new Vector2(0.68f, 0.62f),
            new Vector2(-0.26f, 0.86f)
        };

        // Three beats, two stages. A debris-only stage would have to hide the
        // grimy pane behind it, so the window would read as already clean
        // while the player cleared the sill and would then dirty itself. The
        // sill is cleared inside the sponge stage instead; CleaningStage
        // reports the hands beat on its own while debris remains.
        private static readonly CleaningStageSpec[] DustyWindowStages =
        {
            new CleaningStageSpec(
                CleaningToolKind.Sponge,
                debrisCount: 3,
                brushRadiusUv: 0.072f,
                brushHardness: 0.72f,
                surfaceHeight: 0.43f,
                materialPath:
                    "Assets/CalmSpace/Materials/WindowGrime.mat",
                dirtColor: new Color(0.26f, 0.27f, 0.21f, 1f),
                dirtStrength: 0.86f),
            new CleaningStageSpec(
                CleaningToolKind.Squeegee,
                debrisCount: 0,
                brushRadiusUv: 0.125f,
                brushHardness: 0.95f,
                surfaceHeight: 0.455f,
                materialPath:
                    "Assets/CalmSpace/Materials/WindowStreak.mat",
                dirtColor: new Color(0.70f, 0.77f, 0.82f, 1f),
                dirtStrength: 0.52f)
        };

        private static readonly LevelSpec[] LevelSpecs =
        {
            new LevelSpec(
                "01-soft-blocks",
                "Soft Blocks",
                LevelType.Fitting,
                "Assets/CalmSpace/Config/DemoFitting.asset",
                "Assets/CalmSpace/Content/Levels/DemoFitting.prefab",
                "levels/01-soft-blocks",
                3,
                0.78f,
                "cozy-workshop",
                0,
                8),
            new LevelSpec(
                "02-pebble-pairs",
                "Pebble Pairs",
                LevelType.Sorting,
                "Assets/CalmSpace/Config/02PebblePairs.asset",
                "Assets/CalmSpace/Content/Levels/02PebblePairs.prefab",
                "levels/02-pebble-pairs",
                4,
                0.72f,
                "cozy-workshop",
                1,
                8),
            new LevelSpec(
                "03-tea-drawer",
                "Tea Drawer",
                LevelType.Fitting,
                "Assets/CalmSpace/Config/03TeaDrawer.asset",
                "Assets/CalmSpace/Content/Levels/03TeaDrawer.prefab",
                "levels/03-tea-drawer",
                4,
                0.68f,
                "cozy-workshop",
                2,
                8),
            new LevelSpec(
                "04-color-shelf",
                "Color Shelf",
                LevelType.Sorting,
                "Assets/CalmSpace/Config/04ColorShelf.asset",
                "Assets/CalmSpace/Content/Levels/04ColorShelf.prefab",
                "levels/04-color-shelf",
                5,
                0.64f,
                "cozy-workshop",
                3,
                8),
            new LevelSpec(
                "05-fastener-tray",
                "Fastener Tray",
                LevelType.ScrewPuzzle,
                "Assets/CalmSpace/Config/05FastenerTray.asset",
                "Assets/CalmSpace/Content/Levels/05FastenerTray.prefab",
                "levels/05-fastener-tray",
                0,
                0.62f,
                "cozy-workshop",
                4,
                8,
                screwLayers: new[] { 4 }),
            new LevelSpec(
                "06-fresh-surface",
                "Fresh Surface",
                LevelType.Cleaning,
                "Assets/CalmSpace/Config/06FreshSurface.asset",
                "Assets/CalmSpace/Content/Levels/06FreshSurface.prefab",
                "levels/06-fresh-surface",
                0,
                0f,
                "cozy-workshop",
                5,
                8),
            new LevelSpec(
                "07-cabinet-hinge",
                "Cabinet Hinge",
                LevelType.ScrewPuzzle,
                "Assets/CalmSpace/Config/07CabinetHinge.asset",
                "Assets/CalmSpace/Content/Levels/07CabinetHinge.prefab",
                "levels/07-cabinet-hinge",
                0,
                0.62f,
                "cozy-workshop",
                6,
                8,
                screwLayers: new[] { 3, 2 }),
            new LevelSpec(
                "08-dusty-window",
                "Dusty Window",
                LevelType.Cleaning,
                "Assets/CalmSpace/Config/08DustyWindow.asset",
                "Assets/CalmSpace/Content/Levels/08DustyWindow.prefab",
                "levels/08-dusty-window",
                0,
                0.66f,
                "cozy-workshop",
                7,
                8,
                stages: DustyWindowStages)
        };

        public static LevelCatalog CreateOrUpdate(
            Material[] itemMaterials,
            Material[] targetMaterials)
        {
            if (itemMaterials == null || itemMaterials.Length == 0)
            {
                throw new ArgumentException(
                    "At least one item material is required.",
                    nameof(itemMaterials));
            }

            if (targetMaterials == null || targetMaterials.Length == 0)
            {
                throw new ArgumentException(
                    "At least one target material is required.",
                    nameof(targetMaterials));
            }

            Material cleanMaterial = CreateOrUpdateCleanMaterial();
            var definitions =
                new LevelDefinition[LevelSpecs.Length];
            var prefabGuids = new string[LevelSpecs.Length];

            for (var index = 0; index < LevelSpecs.Length; index++)
            {
                LevelSpec spec = LevelSpecs[index];
                definitions[index] = CreateOrUpdateDefinition(spec);

                GameObject prefab = CreateOrUpdateLevelPrefab(
                    spec,
                    index,
                    definitions[index],
                    cleanMaterial,
                    itemMaterials,
                    targetMaterials);

                prefabGuids[index] = RegisterAddressable(
                    prefab,
                    spec.PrefabPath,
                    spec.Address);
            }

            return CreateOrUpdateCatalog(definitions, prefabGuids);
        }

        private static GameObject CreateOrUpdateLevelPrefab(
            LevelSpec spec,
            int levelIndex,
            LevelDefinition definition,
            Material cleanMaterial,
            Material[] itemMaterials,
            Material[] targetMaterials)
        {
            switch (spec.Type)
            {
                case LevelType.Cleaning:
                    return spec.Stages.Length > 0
                        ? CreateOrUpdateStagedCleaningPrefab(
                            spec,
                            definition,
                            itemMaterials,
                            targetMaterials[0])
                        : CreateOrUpdateCleaningPrefab(
                            spec,
                            definition,
                            cleanMaterial,
                            targetMaterials[0]);
                case LevelType.ScrewPuzzle:
                    return CreateOrUpdateScrewPrefab(
                        spec,
                        definition,
                        itemMaterials,
                        targetMaterials);
                default:
                    return CreateOrUpdateSnapPrefab(
                        spec,
                        levelIndex,
                        definition,
                        itemMaterials,
                        targetMaterials);
            }
        }

        private static LevelDefinition CreateOrUpdateDefinition(
            LevelSpec spec)
        {
            LevelDefinition definition =
                AssetDatabase.LoadAssetAtPath<LevelDefinition>(
                    spec.DefinitionPath);
            if (definition == null)
            {
                definition =
                    ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(
                    definition,
                    spec.DefinitionPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_levelId").stringValue = spec.Id;
            serialized.FindProperty("_displayName").stringValue =
                spec.DisplayName;
            serialized.FindProperty("_levelType").enumValueIndex =
                (int)spec.Type;
            serialized.FindProperty(
                "_restorationChapterId").stringValue =
                spec.RestorationChapterId;
            serialized.FindProperty(
                "_restorationStageIndex").intValue =
                spec.RestorationStageIndex;
            serialized.FindProperty(
                "_restorationStageCount").intValue =
                spec.RestorationStageCount;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static GameObject CreateOrUpdateSnapPrefab(
            LevelSpec spec,
            int levelIndex,
            LevelDefinition definition,
            Material[] itemMaterials,
            Material[] targetMaterials)
        {
            var root = new GameObject(spec.DisplayName + " Level");
            try
            {
                LevelBase level = AddLevelComponent(root, spec.Type);
                SetObjectReference(level, "_definition", definition);

                var targetsRoot = new GameObject("Targets");
                targetsRoot.transform.SetParent(root.transform, false);
                var itemsRoot = new GameObject("Items");
                itemsRoot.transform.SetParent(root.transform, false);
                bool usesSoftSorting =
                    spec.Type == LevelType.Sorting;
                SnapTargetGroup targetGroup = usesSoftSorting
                    ? targetsRoot.AddComponent<SnapTargetGroup>()
                    : null;
                SnapSlot[] snapSlots = usesSoftSorting
                    ? new SnapSlot[spec.ItemCount]
                    : null;

                for (var index = 0; index < spec.ItemCount; index++)
                {
                    Vector3 targetPosition = CalculateSlotPosition(
                        index,
                        spec.ItemCount,
                        target: true);
                    Vector3 itemPosition = CalculateSlotPosition(
                        index,
                        spec.ItemCount,
                        target: false);
                    ShapeKind shape = GetShape(levelIndex, index);
                    int paletteIndex = usesSoftSorting
                        ? (int)shape
                        : index;
                    Material itemMaterial =
                        itemMaterials[
                            paletteIndex % itemMaterials.Length];
                    Material targetMaterial =
                        targetMaterials[
                            paletteIndex % targetMaterials.Length];

                    Transform target = CreateTarget(
                        targetsRoot.transform,
                        index,
                        shape,
                        targetPosition,
                        targetMaterial,
                        levelIndex,
                        paletteIndex);
                    if (usesSoftSorting)
                    {
                        snapSlots[index] = CreateSnapSlot(
                            target,
                            shape);
                    }

                    CreateDraggableItem(
                        itemsRoot.transform,
                        target,
                        targetGroup,
                        index,
                        shape,
                        itemPosition,
                        itemMaterial,
                        spec.SnapThreshold,
                        levelIndex,
                        paletteIndex);
                }

                if (usesSoftSorting)
                {
                    ConfigureSnapTargetGroup(
                        targetGroup,
                        snapSlots);
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    spec.PrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Unity did not save '{spec.PrefabPath}'.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateOrUpdateCleaningPrefab(
            LevelSpec spec,
            LevelDefinition definition,
            Material cleanMaterial,
            Material frameMaterial)
        {
            var root = new GameObject(spec.DisplayName + " Level");
            try
            {
                CleaningLevel level =
                    root.AddComponent<CleaningLevel>();
                SetObjectReference(level, "_definition", definition);

                GameObject surface = GameObject.CreatePrimitive(
                    PrimitiveType.Plane);
                surface.name = "Clean Surface";
                surface.transform.SetParent(root.transform, false);
                surface.transform.localPosition =
                    new Vector3(0f, 0.43f, 0f);
                surface.transform.localScale =
                    new Vector3(0.27f, 1f, 0.27f);
                MeshRenderer renderer =
                    surface.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = cleanMaterial;
                MeshCollider collider =
                    surface.GetComponent<MeshCollider>();

                CreateTrayFrame(
                    root.transform,
                    new Vector3(-1.43f, 0.41f, 0f),
                    new Vector3(0.16f, 0.20f, 3.02f),
                    frameMaterial);
                CreateTrayFrame(
                    root.transform,
                    new Vector3(1.43f, 0.41f, 0f),
                    new Vector3(0.16f, 0.20f, 3.02f),
                    frameMaterial);
                CreateTrayFrame(
                    root.transform,
                    new Vector3(0f, 0.41f, 1.43f),
                    new Vector3(3.02f, 0.20f, 0.16f),
                    frameMaterial);
                CreateTrayFrame(
                    root.transform,
                    new Vector3(0f, 0.41f, -1.43f),
                    new Vector3(3.02f, 0.20f, 0.16f),
                    frameMaterial);

                RenderTextureCleaner cleaner =
                    root.AddComponent<RenderTextureCleaner>();
                var cleanerSerialized = new SerializedObject(cleaner);
                cleanerSerialized.FindProperty("cleanSurfaceCollider")
                    .objectReferenceValue = collider;
                cleanerSerialized.FindProperty("visibleRenderer")
                    .objectReferenceValue = renderer;
                cleanerSerialized.FindProperty("maskBrushShader")
                    .objectReferenceValue =
                        Shader.Find("Hidden/CalmSpace/MaskBrush");
                cleanerSerialized
                    .FindProperty("coverageDownsampleShader")
                    .objectReferenceValue =
                        Shader.Find(
                            "Hidden/CalmSpace/CoverageDownsample");
                cleanerSerialized.FindProperty("maskResolution")
                    .vector2IntValue = new Vector2Int(256, 256);
                cleanerSerialized.FindProperty("coverageResolution")
                    .vector2IntValue = new Vector2Int(64, 64);
                cleanerSerialized.FindProperty("brushRadiusUv")
                    .floatValue = 0.095f;
                cleanerSerialized.FindProperty("brushHardness")
                    .floatValue = 0.80f;
                cleanerSerialized
                    .FindProperty("progressSampleIntervalSeconds")
                    .floatValue = 0.28f;
                cleanerSerialized
                    .FindProperty("progressSampleFrameInterval")
                    .intValue = 10;
                cleanerSerialized.ApplyModifiedPropertiesWithoutUndo();

                CleaningInputController input =
                    root.AddComponent<CleaningInputController>();
                SetObjectReference(input, "_cleaner", cleaner);
                SetObjectReference(level, "_cleaner", cleaner);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    spec.PrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Unity did not save '{spec.PrefabPath}'.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateOrUpdateStagedCleaningPrefab(
            LevelSpec spec,
            LevelDefinition definition,
            Material[] itemMaterials,
            Material frameMaterial)
        {
            var root = new GameObject(spec.DisplayName + " Level");
            try
            {
                CleaningLevel level =
                    root.AddComponent<CleaningLevel>();
                SetObjectReference(level, "_definition", definition);

                CleaningInputController input =
                    root.AddComponent<CleaningInputController>();
                SetObjectReference(level, "_cleaningInput", input);

                CreateTrayFrame(
                    root.transform,
                    new Vector3(-1.28f, 0.41f, 0f),
                    new Vector3(0.16f, 0.20f, 2.72f),
                    frameMaterial);
                CreateTrayFrame(
                    root.transform,
                    new Vector3(1.28f, 0.41f, 0f),
                    new Vector3(0.16f, 0.20f, 2.72f),
                    frameMaterial);
                CreateTrayFrame(
                    root.transform,
                    new Vector3(0f, 0.41f, 1.28f),
                    new Vector3(2.72f, 0.20f, 0.16f),
                    frameMaterial);
                CreateTrayFrame(
                    root.transform,
                    new Vector3(0f, 0.41f, -1.28f),
                    new Vector3(2.72f, 0.20f, 0.16f),
                    frameMaterial);

                var stagesRoot = new GameObject("Stages");
                stagesRoot.transform.SetParent(root.transform, false);

                CleaningStageSpec[] stageSpecs = spec.Stages;
                var stages = new CleaningStage[stageSpecs.Length];
                for (var index = 0;
                     index < stageSpecs.Length;
                     index++)
                {
                    stages[index] = CreateCleaningStage(
                        spec,
                        stageSpecs[index],
                        index,
                        stagesRoot.transform,
                        itemMaterials,
                        frameMaterial);
                }

                var levelSerialized = new SerializedObject(level);
                SetArrayProperty(
                    levelSerialized.FindProperty("_stages"),
                    stages);
                levelSerialized
                    .FindProperty("_stageSettleSeconds")
                    .floatValue = 0.5f;
                levelSerialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    spec.PrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Unity did not save '{spec.PrefabPath}'.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static CleaningStage CreateCleaningStage(
            LevelSpec spec,
            CleaningStageSpec stageSpec,
            int stageIndex,
            Transform parent,
            Material[] itemMaterials,
            Material frameMaterial)
        {
            var stageObject = new GameObject(
                $"Stage {stageIndex + 1:00}");
            stageObject.transform.SetParent(parent, false);
            CleaningStage stage =
                stageObject.AddComponent<CleaningStage>();

            var content = new GameObject("Content");
            content.transform.SetParent(
                stageObject.transform,
                false);

            RenderTextureCleaner cleaner = null;
            if (stageSpec.HasSurface)
            {
                cleaner = CreateStageSurface(
                    content.transform,
                    stageSpec,
                    stageIndex);
            }

            var debris = new ItemSnapController[
                stageSpec.DebrisCount];
            for (var index = 0;
                 index < stageSpec.DebrisCount;
                 index++)
            {
                debris[index] = CreateDebris(
                    content.transform,
                    index,
                    stageSpec.DebrisCount,
                    spec.SnapThreshold,
                    itemMaterials[index % itemMaterials.Length],
                    frameMaterial);
            }

            var serialized = new SerializedObject(stage);
            serialized.FindProperty("_stageRoot")
                .objectReferenceValue = content;
            serialized.FindProperty("_cleaner")
                .objectReferenceValue = cleaner;
            SetArrayProperty(
                serialized.FindProperty("_debris"),
                debris);
            serialized.FindProperty("_tool").enumValueIndex =
                (int)stageSpec.Tool;
            serialized.FindProperty("_brushRadiusUv").floatValue =
                stageSpec.BrushRadiusUv;
            serialized.FindProperty("_brushHardness").floatValue =
                stageSpec.BrushHardness;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Only the first pass is authored visible; the rest are revealed
            // in order. CleaningLevel re-asserts this at runtime.
            if (stageIndex > 0)
            {
                content.SetActive(false);
            }

            return stage;
        }

        private static RenderTextureCleaner CreateStageSurface(
            Transform parent,
            CleaningStageSpec stageSpec,
            int stageIndex)
        {
            Material surfaceMaterial = CreateOrUpdateCleanMaterial(
                stageSpec.MaterialPath,
                $"Cleanable Layer {stageIndex + 1:00}",
                new Color(0.94f, 0.93f, 0.88f, 1f),
                stageSpec.DirtColor,
                stageSpec.DirtStrength);

            GameObject surface = GameObject.CreatePrimitive(
                PrimitiveType.Plane);
            surface.name = "Clean Surface";
            surface.transform.SetParent(parent, false);
            surface.transform.localPosition = new Vector3(
                0f,
                stageSpec.SurfaceHeight,
                0f);
            surface.transform.localScale =
                new Vector3(0.24f, 1f, 0.24f);

            MeshRenderer renderer =
                surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = surfaceMaterial;
            MeshCollider collider =
                surface.GetComponent<MeshCollider>();

            RenderTextureCleaner cleaner =
                parent.gameObject.AddComponent<RenderTextureCleaner>();
            var serialized = new SerializedObject(cleaner);
            serialized.FindProperty("cleanSurfaceCollider")
                .objectReferenceValue = collider;
            serialized.FindProperty("visibleRenderer")
                .objectReferenceValue = renderer;
            serialized.FindProperty("maskBrushShader")
                .objectReferenceValue =
                    Shader.Find("Hidden/CalmSpace/MaskBrush");
            serialized.FindProperty("coverageDownsampleShader")
                .objectReferenceValue =
                    Shader.Find(
                        "Hidden/CalmSpace/CoverageDownsample");
            serialized.FindProperty("maskResolution")
                .vector2IntValue = new Vector2Int(256, 256);
            serialized.FindProperty("coverageResolution")
                .vector2IntValue = new Vector2Int(64, 64);
            serialized.FindProperty("brushRadiusUv").floatValue =
                stageSpec.BrushRadiusUv;
            serialized.FindProperty("brushHardness").floatValue =
                stageSpec.BrushHardness;
            serialized
                .FindProperty("progressSampleIntervalSeconds")
                .floatValue = 0.28f;
            serialized
                .FindProperty("progressSampleFrameInterval")
                .intValue = 10;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return cleaner;
        }

        private static ItemSnapController CreateDebris(
            Transform parent,
            int index,
            int count,
            float snapThreshold,
            Material material,
            Material binMaterial)
        {
            var target = new GameObject(
                $"Sill Bin Slot {index + 1:00}");
            target.transform.SetParent(parent, false);
            target.transform.localPosition = new Vector3(
                (index - (count - 1) * 0.5f) * 0.68f,
                0.50f,
                -1.74f);
            AddThemeColorTag(
                target,
                DemoThemeColorRole.Socket,
                index);
            AddPrimitive(
                target.transform,
                PrimitiveType.Cube,
                "Bin Socket",
                new Vector3(0f, -0.14f, 0f),
                new Vector3(0.60f, 0.10f, 0.60f),
                Quaternion.identity,
                binMaterial,
                false);

            var debris = new GameObject($"Debris {index + 1:00}");
            debris.transform.SetParent(parent, false);
            debris.transform.localPosition = new Vector3(
                DebrisOffsets[index % DebrisOffsets.Length].x,
                0.52f,
                DebrisOffsets[index % DebrisOffsets.Length].y);
            debris.transform.localRotation = Quaternion.Euler(
                0f,
                index * 34f,
                0f);
            AddThemeColorTag(
                debris,
                DemoThemeColorRole.Piece,
                index);
            AddPrimitive(
                debris.transform,
                index % 2 == 0
                    ? PrimitiveType.Cube
                    : PrimitiveType.Sphere,
                "Tactile Piece",
                Vector3.zero,
                new Vector3(0.44f, 0.24f, 0.52f),
                Quaternion.identity,
                material,
                true);

            ItemSnapController snap =
                debris.AddComponent<ItemSnapController>();
            var serialized = new SerializedObject(snap);
            serialized.FindProperty("_snapTarget")
                .objectReferenceValue = target.transform;
            serialized.FindProperty("_positionSnapThreshold")
                .floatValue = snapThreshold;
            serialized
                .FindProperty("_rotationSnapThresholdDegrees")
                .floatValue = 180f;
            serialized.FindProperty("_snapDurationSeconds")
                .floatValue = 0.16f;
            serialized.FindProperty("_returnDurationSeconds")
                .floatValue = 0.15f;
            serialized.FindProperty("_dragHapticIntensity")
                .floatValue = 0.46f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return snap;
        }

        private static GameObject CreateOrUpdateScrewPrefab(
            LevelSpec spec,
            LevelDefinition definition,
            Material[] itemMaterials,
            Material[] targetMaterials)
        {
            int[] layers = spec.ScrewLayers;
            if (layers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Screw level '{spec.Id}' declares no layers.");
            }

            var root = new GameObject(spec.DisplayName + " Level");
            try
            {
                ScrewPuzzleLevel level =
                    root.AddComponent<ScrewPuzzleLevel>();
                SetObjectReference(level, "_definition", definition);
                root.AddComponent<ScrewInputController>();

                var targetsRoot = new GameObject("Targets");
                targetsRoot.transform.SetParent(root.transform, false);
                var layersRoot = new GameObject("Layers");
                layersRoot.transform.SetParent(root.transform, false);

                // Build inward. Every panel has to point at the layer it
                // uncovers, so the deeper one must exist first.
                GameObject deeperLayer = null;
                for (int layerIndex = layers.Length - 1;
                     layerIndex >= 0;
                     layerIndex--)
                {
                    deeperLayer = CreateFastenedLayer(
                        spec,
                        layers.Length,
                        layerIndex,
                        layers[layerIndex],
                        layersRoot.transform,
                        targetsRoot.transform,
                        itemMaterials,
                        targetMaterials,
                        deeperLayer);
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    spec.PrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Unity did not save '{spec.PrefabPath}'.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateFastenedLayer(
            LevelSpec spec,
            int layerCount,
            int layerIndex,
            int screwCount,
            Transform layersRoot,
            Transform targetsRoot,
            Material[] itemMaterials,
            Material[] targetMaterials,
            GameObject deeperLayer)
        {
            var layer = new GameObject(
                $"Layer {layerIndex + 1:00}");
            layer.transform.SetParent(layersRoot, false);

            Material panelMaterial =
                itemMaterials[layerIndex % itemMaterials.Length];
            Material socketMaterial =
                targetMaterials[layerIndex % targetMaterials.Length];

            var panelScale = new Vector3(
                1.86f - layerIndex * 0.30f,
                0.15f,
                1.10f - layerIndex * 0.18f);
            var panelPosition = new Vector3(
                0f,
                0.58f - layerIndex * 0.10f,
                0.86f);

            Transform target = CreatePanelTarget(
                targetsRoot,
                layerIndex,
                new Vector3(
                    (layerIndex - (layerCount - 1) * 0.5f) * 2.0f,
                    0.52f,
                    -1.28f),
                panelScale,
                socketMaterial);

            var panel = new GameObject(
                $"Panel {layerIndex + 1:00}");
            panel.transform.SetParent(layer.transform, false);
            panel.transform.localPosition = panelPosition;
            AddThemeColorTag(
                panel,
                DemoThemeColorRole.Piece,
                layerIndex);

            GameObject plate = AddPrimitive(
                panel.transform,
                PrimitiveType.Cube,
                "Panel Plate",
                Vector3.zero,
                panelScale,
                Quaternion.identity,
                panelMaterial,
                true);

            ItemSnapController item =
                panel.AddComponent<ItemSnapController>();
            var itemSerialized = new SerializedObject(item);
            itemSerialized.FindProperty("_snapTarget")
                .objectReferenceValue = target;
            itemSerialized.FindProperty("_positionSnapThreshold")
                .floatValue = spec.SnapThreshold;
            itemSerialized
                .FindProperty("_rotationSnapThresholdDegrees")
                .floatValue = 180f;
            itemSerialized.FindProperty("_snapDurationSeconds")
                .floatValue = 0.18f;
            itemSerialized.FindProperty("_returnDurationSeconds")
                .floatValue = 0.15f;
            itemSerialized.FindProperty("_dragHapticIntensity")
                .floatValue = 0.52f;
            itemSerialized.ApplyModifiedPropertiesWithoutUndo();

            FastenerPanel fastener =
                panel.AddComponent<FastenerPanel>();

            var screwsRoot = new GameObject("Screws");
            screwsRoot.transform.SetParent(layer.transform, false);
            var screws = new ScrewController[screwCount];
            for (var index = 0; index < screwCount; index++)
            {
                screws[index] = CreateScrew(
                    screwsRoot.transform,
                    index,
                    screwCount,
                    panelPosition,
                    panelScale,
                    panelMaterial,
                    socketMaterial,
                    fastener);
            }

            var panelSerialized = new SerializedObject(fastener);
            SetArrayProperty(
                panelSerialized.FindProperty("_screws"),
                screws);
            SetArrayProperty(
                panelSerialized.FindProperty("_lockedColliders"),
                new Object[] { plate.GetComponent<Collider>() });
            panelSerialized.FindProperty("_item")
                .objectReferenceValue = item;
            panelSerialized.FindProperty("_revealsOnRelease")
                .objectReferenceValue = deeperLayer;
            panelSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (layerIndex > 0)
            {
                layer.SetActive(false);
            }

            return layer;
        }

        private static Transform CreatePanelTarget(
            Transform parent,
            int index,
            Vector3 position,
            Vector3 panelScale,
            Material material)
        {
            var targetRoot = new GameObject(
                $"Panel Target {index + 1:00}");
            targetRoot.transform.SetParent(parent, false);
            targetRoot.transform.localPosition = position;
            AddThemeColorTag(
                targetRoot,
                DemoThemeColorRole.Socket,
                index);

            AddPrimitive(
                targetRoot.transform,
                PrimitiveType.Cube,
                "Panel Socket",
                new Vector3(0f, -0.11f, 0f),
                new Vector3(
                    panelScale.x * 1.08f,
                    0.08f,
                    panelScale.z * 1.08f),
                Quaternion.identity,
                material,
                false);

            return targetRoot.transform;
        }

        private static ScrewController CreateScrew(
            Transform parent,
            int index,
            int count,
            Vector3 panelPosition,
            Vector3 panelScale,
            Material bodyMaterial,
            Material slotMaterial,
            FastenerPanel panel)
        {
            // An eighth-turn offset puts four screws exactly on the corners
            // and spreads any other count evenly around the plate.
            float angle = Mathf.PI * 2f * index / count +
                Mathf.PI * 0.25f;
            var screw = new GameObject($"Screw {index + 1:00}");
            screw.transform.SetParent(parent, false);
            screw.transform.localPosition = new Vector3(
                panelPosition.x +
                    Mathf.Cos(angle) * panelScale.x * 0.33f,
                panelPosition.y + panelScale.y * 0.5f + 0.03f,
                panelPosition.z +
                    Mathf.Sin(angle) * panelScale.z * 0.33f);

            var body = new GameObject("Body");
            body.transform.SetParent(screw.transform, false);
            AddThemeColorTag(
                body,
                DemoThemeColorRole.Board,
                0);
            AddPrimitive(
                body.transform,
                PrimitiveType.Cylinder,
                "Screw Shaft",
                Vector3.zero,
                new Vector3(0.11f, 0.10f, 0.11f),
                Quaternion.identity,
                bodyMaterial,
                false);
            AddPrimitive(
                body.transform,
                PrimitiveType.Cylinder,
                "Screw Head",
                new Vector3(0f, 0.10f, 0f),
                new Vector3(0.34f, 0.035f, 0.34f),
                Quaternion.identity,
                bodyMaterial,
                true);

            // The drive slot is the only asymmetric feature, so it is what
            // makes the turning readable. It stays untagged to keep its
            // contrast against the themed body.
            AddPrimitive(
                screw.transform,
                PrimitiveType.Cube,
                "Screw Slot",
                new Vector3(0f, 0.15f, 0f),
                new Vector3(0.30f, 0.02f, 0.07f),
                Quaternion.identity,
                slotMaterial,
                false);

            ScrewController controller =
                screw.AddComponent<ScrewController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_panel").objectReferenceValue =
                panel;
            serialized.FindProperty("_turnsRequired").intValue =
                3 + index % 2;
            serialized.FindProperty("_secondsPerTurn").floatValue =
                0.40f;
            serialized.FindProperty("_riseHeight").floatValue = 0.34f;
            serialized.FindProperty("_tickIntensity").floatValue =
                0.55f;
            serialized.FindProperty("_settleDurationSeconds")
                .floatValue = 0.22f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }

        private static LevelBase AddLevelComponent(
            GameObject root,
            LevelType type)
        {
            switch (type)
            {
                case LevelType.Sorting:
                    return root.AddComponent<SortingLevel>();
                case LevelType.Fitting:
                    return root.AddComponent<FittingLevel>();
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(type),
                        type,
                        "This builder only creates snap-driven levels here.");
            }
        }

        private static void SetArrayProperty(
            SerializedProperty property,
            Object[] values)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index)
                    .objectReferenceValue = values[index];
            }
        }

        private static Transform CreateTarget(
            Transform parent,
            int index,
            ShapeKind shape,
            Vector3 position,
            Material material,
            int levelIndex,
            int paletteIndex)
        {
            var targetRoot = new GameObject(
                $"Target {index + 1:00}");
            targetRoot.transform.SetParent(parent, false);
            targetRoot.transform.localPosition = position;
            AddThemeColorTag(
                targetRoot,
                DemoThemeColorRole.Socket,
                paletteIndex);

            AddPrimitive(
                targetRoot.transform,
                ToPrimitiveType(shape),
                "Socket",
                new Vector3(0f, -0.22f, 0f),
                GetShapeScale(shape, target: true),
                GetShapeRotation(shape),
                material,
                false);

            return targetRoot.transform;
        }

        private static SnapSlot CreateSnapSlot(
            Transform target,
            ShapeKind shape)
        {
            SnapSlot slot =
                target.gameObject.AddComponent<SnapSlot>();
            var serialized = new SerializedObject(slot);
            serialized.FindProperty("_category").enumValueIndex =
                (int)ToSnapCategory(shape);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return slot;
        }

        private static void ConfigureSnapTargetGroup(
            SnapTargetGroup targetGroup,
            SnapSlot[] slots)
        {
            var serialized = new SerializedObject(targetGroup);
            SerializedProperty slotsProperty =
                serialized.FindProperty("_slots");
            slotsProperty.arraySize = slots.Length;
            for (var index = 0; index < slots.Length; index++)
            {
                slotsProperty.GetArrayElementAtIndex(index)
                    .objectReferenceValue = slots[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateDraggableItem(
            Transform parent,
            Transform target,
            SnapTargetGroup targetGroup,
            int index,
            ShapeKind shape,
            Vector3 position,
            Material material,
            float threshold,
            int levelIndex,
            int paletteIndex)
        {
            var item = new GameObject($"Item {index + 1:00}");
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localRotation = Quaternion.Euler(
                0f,
                index % 2 == 0 ? -9f : 9f,
                0f);
            AddThemeColorTag(
                item,
                DemoThemeColorRole.Piece,
                paletteIndex);

            if (levelIndex == 2)
            {
                CreateTeaObject(item.transform, index, material);
            }
            else
            {
                AddPrimitive(
                    item.transform,
                    ToPrimitiveType(shape),
                    "Tactile Piece",
                    Vector3.zero,
                    GetShapeScale(shape, target: false),
                    GetShapeRotation(shape),
                    material,
                    true);
            }

            ItemSnapController snap =
                item.AddComponent<ItemSnapController>();
            var serialized = new SerializedObject(snap);
            if (targetGroup == null)
            {
                serialized.FindProperty("_snapTarget")
                    .objectReferenceValue = target;
            }
            else
            {
                serialized.FindProperty("_snapTargetGroup")
                    .objectReferenceValue = targetGroup;
                serialized.FindProperty("_snapCategory")
                    .enumValueIndex = (int)ToSnapCategory(shape);
            }

            serialized.FindProperty("_positionSnapThreshold").floatValue =
                threshold;
            serialized.FindProperty("_rotationSnapThresholdDegrees")
                .floatValue = 180f;
            serialized.FindProperty("_snapDurationSeconds").floatValue =
                0.16f;
            serialized.FindProperty("_returnDurationSeconds").floatValue =
                0.15f;
            serialized.FindProperty("_dragHapticIntensity").floatValue =
                0.48f + Mathf.Min(index, 3) * 0.05f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateTeaObject(
            Transform parent,
            int index,
            Material material)
        {
            switch (index)
            {
                case 0:
                    AddPrimitive(
                        parent,
                        PrimitiveType.Cylinder,
                        "Tea Tin",
                        Vector3.zero,
                        new Vector3(0.62f, 0.34f, 0.62f),
                        Quaternion.identity,
                        material,
                        true);
                    break;
                case 1:
                    AddPrimitive(
                        parent,
                        PrimitiveType.Cylinder,
                        "Cup",
                        Vector3.zero,
                        new Vector3(0.58f, 0.30f, 0.58f),
                        Quaternion.identity,
                        material,
                        true);
                    AddPrimitive(
                        parent,
                        PrimitiveType.Cube,
                        "Cup Handle",
                        new Vector3(0.55f, 0f, 0f),
                        new Vector3(0.18f, 0.20f, 0.34f),
                        Quaternion.identity,
                        material,
                        true);
                    break;
                case 2:
                    AddPrimitive(
                        parent,
                        PrimitiveType.Cube,
                        "Spoon Handle",
                        new Vector3(0f, 0f, -0.18f),
                        new Vector3(0.16f, 0.18f, 0.92f),
                        Quaternion.identity,
                        material,
                        true);
                    AddPrimitive(
                        parent,
                        PrimitiveType.Sphere,
                        "Spoon Bowl",
                        new Vector3(0f, 0f, 0.38f),
                        new Vector3(0.42f, 0.18f, 0.52f),
                        Quaternion.identity,
                        material,
                        true);
                    break;
                default:
                    AddPrimitive(
                        parent,
                        PrimitiveType.Cube,
                        "Tea Packet",
                        Vector3.zero,
                        new Vector3(0.70f, 0.22f, 0.88f),
                        Quaternion.identity,
                        material,
                        true);
                    break;
            }
        }

        private static GameObject AddPrimitive(
            Transform parent,
            PrimitiveType primitiveType,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material,
            bool keepCollider)
        {
            GameObject primitive =
                GameObject.CreatePrimitive(primitiveType);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localRotation = localRotation;
            primitive.transform.localScale = localScale;

            MeshRenderer renderer =
                primitive.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            if (!keepCollider)
            {
                Collider[] colliders =
                    primitive.GetComponents<Collider>();
                for (var index = 0; index < colliders.Length; index++)
                {
                    Object.DestroyImmediate(colliders[index]);
                }
            }

            return primitive;
        }

        private static void CreateTrayFrame(
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            AddPrimitive(
                parent,
                PrimitiveType.Cube,
                "Target Tray Frame",
                position,
                scale,
                Quaternion.identity,
                material,
                false);
        }

        private static Vector3 CalculateSlotPosition(
            int index,
            int count,
            bool target)
        {
            float x;
            float z;
            if (count <= 3)
            {
                x = (index - (count - 1) * 0.5f) * 1.05f;
                z = 1.30f;
            }
            else if (count == 4)
            {
                int row = index / 2;
                int column = index % 2;
                x = (column - 0.5f) * 1.45f;
                z = 0.82f + row * 1.02f;
            }
            else
            {
                int row = index < 3 ? 0 : 1;
                int column = row == 0 ? index : index - 3;
                int columnCount = row == 0 ? 3 : 2;
                float spacing = row == 0 ? 1.04f : 1.38f;
                x = (column - (columnCount - 1) * 0.5f) * spacing;
                z = 0.82f + row * 1.02f;
            }

            if (!target)
            {
                z = -z;
            }

            return new Vector3(
                x,
                target ? 0.54f : 0.56f,
                z);
        }

        private static void AddThemeColorTag(
            GameObject gameObject,
            DemoThemeColorRole role,
            int paletteIndex)
        {
            DemoThemeColorTag tag =
                gameObject.AddComponent<DemoThemeColorTag>();
            var serialized = new SerializedObject(tag);
            serialized.FindProperty("_role").enumValueIndex = (int)role;
            serialized.FindProperty("_paletteIndex").intValue =
                paletteIndex;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ShapeKind GetShape(
            int levelIndex,
            int itemIndex)
        {
            switch (levelIndex)
            {
                case 0:
                    return ShapeKind.Block;
                case 1:
                    return itemIndex % 2 == 0
                        ? ShapeKind.Pebble
                        : ShapeKind.Capsule;
                case 3:
                    return (ShapeKind)(itemIndex % 4);
                default:
                    return ShapeKind.Block;
            }
        }

        private static PrimitiveType ToPrimitiveType(ShapeKind shape)
        {
            switch (shape)
            {
                case ShapeKind.Pebble:
                    return PrimitiveType.Sphere;
                case ShapeKind.Capsule:
                    return PrimitiveType.Capsule;
                case ShapeKind.Cylinder:
                    return PrimitiveType.Cylinder;
                default:
                    return PrimitiveType.Cube;
            }
        }

        private static SnapCategory ToSnapCategory(ShapeKind shape)
        {
            return (SnapCategory)(int)shape;
        }

        private static Vector3 GetShapeScale(
            ShapeKind shape,
            bool target)
        {
            float multiplier = target ? 1.10f : 1f;
            switch (shape)
            {
                case ShapeKind.Pebble:
                    return new Vector3(0.82f, 0.34f, 0.68f) *
                        multiplier;
                case ShapeKind.Capsule:
                    return new Vector3(0.46f, 0.44f, 0.46f) *
                        multiplier;
                case ShapeKind.Cylinder:
                    return new Vector3(0.64f, 0.28f, 0.64f) *
                        multiplier;
                default:
                    return new Vector3(0.82f, 0.38f, 0.82f) *
                        multiplier;
            }
        }

        private static Quaternion GetShapeRotation(ShapeKind shape)
        {
            return shape == ShapeKind.Capsule
                ? Quaternion.Euler(0f, 0f, 90f)
                : Quaternion.identity;
        }

        private static Material CreateOrUpdateCleanMaterial()
        {
            return CreateOrUpdateCleanMaterial(
                CleanMaterialPath,
                "Clean Tray",
                new Color(0.94f, 0.88f, 0.74f, 1f),
                new Color(0.28f, 0.20f, 0.14f, 1f),
                0.82f);
        }

        private static Material CreateOrUpdateCleanMaterial(
            string materialPath,
            string materialName,
            Color baseColor,
            Color dirtColor,
            float dirtStrength)
        {
            Shader shader = Shader.Find("CalmSpace/CleanableSurface");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "The CalmSpace cleanable surface shader is unavailable.");
            }

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.name = materialName;
            material.enableInstancing = true;
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_DirtColor", dirtColor);
            material.SetFloat("_DirtStrength", dirtStrength);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static string RegisterAddressable(
            GameObject prefab,
            string prefabPath,
            string address)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException(
                    $"Prefab '{prefabPath}' has no asset GUID.");
            }

            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null || settings.DefaultGroup == null)
            {
                throw new InvalidOperationException(
                    "Addressables settings could not be created.");
            }

            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption
                    .DoNotBuildWithPlayer;
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(
                guid,
                settings.DefaultGroup,
                false,
                false);
            entry.address = address;
            entry.SetLabel(AddressableLabel, true, true, false);
            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.EntryModified,
                entry,
                true);
            EditorUtility.SetDirty(settings);
            return guid;
        }

        private static LevelCatalog CreateOrUpdateCatalog(
            LevelDefinition[] definitions,
            string[] prefabGuids)
        {
            LevelCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var entries =
                new LevelCatalogEntry[definitions.Length];
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = new LevelCatalogEntry();
                SetPrivateField(
                    entry,
                    "_definition",
                    definitions[index]);
                SetPrivateField(
                    entry,
                    "_prefab",
                    new AssetReferenceGameObject(prefabGuids[index]));
                entries[index] = entry;
            }

            SetPrivateField(catalog, "_levels", entries);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void SetObjectReference(
            Object target,
            string propertyName,
            Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property =
                serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new MissingFieldException(
                    target.GetType().FullName,
                    propertyName);
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(
                    target.GetType().FullName,
                    fieldName);
            }

            field.SetValue(target, value);
        }

        private enum ShapeKind
        {
            Block = 0,
            Pebble = 1,
            Capsule = 2,
            Cylinder = 3
        }

        /// <summary>
        /// One pass of a staged cleaning level. A stage with no material path
        /// has no surface to wipe — it is a debris pass, cleared by dragging.
        /// </summary>
        private readonly struct CleaningStageSpec
        {
            public CleaningStageSpec(
                CleaningToolKind tool,
                int debrisCount,
                float brushRadiusUv,
                float brushHardness,
                float surfaceHeight,
                string materialPath,
                Color dirtColor,
                float dirtStrength)
            {
                Tool = tool;
                DebrisCount = debrisCount;
                BrushRadiusUv = brushRadiusUv;
                BrushHardness = brushHardness;
                SurfaceHeight = surfaceHeight;
                MaterialPath = materialPath;
                DirtColor = dirtColor;
                DirtStrength = dirtStrength;
            }

            public CleaningToolKind Tool { get; }

            public int DebrisCount { get; }

            public float BrushRadiusUv { get; }

            public float BrushHardness { get; }

            public float SurfaceHeight { get; }

            public string MaterialPath { get; }

            public Color DirtColor { get; }

            public float DirtStrength { get; }

            public bool HasSurface =>
                !string.IsNullOrEmpty(MaterialPath);
        }

        private readonly struct LevelSpec
        {
            private readonly int[] _screwLayers;
            private readonly CleaningStageSpec[] _stages;

            public LevelSpec(
                string id,
                string displayName,
                LevelType type,
                string definitionPath,
                string prefabPath,
                string address,
                int itemCount,
                float snapThreshold,
                string restorationChapterId,
                int restorationStageIndex,
                int restorationStageCount,
                int[] screwLayers = null,
                CleaningStageSpec[] stages = null)
            {
                Id = id;
                DisplayName = displayName;
                Type = type;
                DefinitionPath = definitionPath;
                PrefabPath = prefabPath;
                Address = address;
                ItemCount = itemCount;
                SnapThreshold = snapThreshold;
                RestorationChapterId =
                    restorationChapterId ?? string.Empty;
                RestorationStageIndex = restorationStageIndex;
                RestorationStageCount = restorationStageCount;
                _screwLayers = screwLayers ?? Array.Empty<int>();
                _stages = stages ??
                    Array.Empty<CleaningStageSpec>();
            }

            /// <summary>
            /// Screws per fastened layer, outermost first.
            /// </summary>
            public int[] ScrewLayers => _screwLayers;

            public CleaningStageSpec[] Stages => _stages;

            public string Id { get; }

            public string DisplayName { get; }

            public LevelType Type { get; }

            public string DefinitionPath { get; }

            public string PrefabPath { get; }

            public string Address { get; }

            public int ItemCount { get; }

            public float SnapThreshold { get; }

            public string RestorationChapterId { get; }

            public int RestorationStageIndex { get; }

            public int RestorationStageCount { get; }
        }
    }
}
