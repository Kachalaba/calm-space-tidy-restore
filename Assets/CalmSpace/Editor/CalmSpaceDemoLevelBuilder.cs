using System;
using System.Reflection;
using CalmSpace.Cleaning;
using CalmSpace.Demo;
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
                0.78f),
            new LevelSpec(
                "02-pebble-pairs",
                "Pebble Pairs",
                LevelType.Sorting,
                "Assets/CalmSpace/Config/02PebblePairs.asset",
                "Assets/CalmSpace/Content/Levels/02PebblePairs.prefab",
                "levels/02-pebble-pairs",
                4,
                0.72f),
            new LevelSpec(
                "03-tea-drawer",
                "Tea Drawer",
                LevelType.Fitting,
                "Assets/CalmSpace/Config/03TeaDrawer.asset",
                "Assets/CalmSpace/Content/Levels/03TeaDrawer.prefab",
                "levels/03-tea-drawer",
                4,
                0.68f),
            new LevelSpec(
                "04-color-shelf",
                "Color Shelf",
                LevelType.Sorting,
                "Assets/CalmSpace/Config/04ColorShelf.asset",
                "Assets/CalmSpace/Content/Levels/04ColorShelf.prefab",
                "levels/04-color-shelf",
                5,
                0.64f),
            new LevelSpec(
                "05-fastener-tray",
                "Fastener Tray",
                LevelType.ScrewPuzzle,
                "Assets/CalmSpace/Config/05FastenerTray.asset",
                "Assets/CalmSpace/Content/Levels/05FastenerTray.prefab",
                "levels/05-fastener-tray",
                4,
                0.60f),
            new LevelSpec(
                "06-fresh-surface",
                "Fresh Surface",
                LevelType.Cleaning,
                "Assets/CalmSpace/Config/06FreshSurface.asset",
                "Assets/CalmSpace/Content/Levels/06FreshSurface.prefab",
                "levels/06-fresh-surface",
                0,
                0f)
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

                GameObject prefab = spec.Type == LevelType.Cleaning
                    ? CreateOrUpdateCleaningPrefab(
                        spec,
                        definitions[index],
                        cleanMaterial,
                        targetMaterials[0])
                    : CreateOrUpdateSnapPrefab(
                        spec,
                        index,
                        definitions[index],
                        itemMaterials,
                        targetMaterials);

                prefabGuids[index] = RegisterAddressable(
                    prefab,
                    spec.PrefabPath,
                    spec.Address);
            }

            return CreateOrUpdateCatalog(definitions, prefabGuids);
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
                cleanerSerialized.FindProperty("maskResolution")
                    .vector2IntValue = new Vector2Int(256, 256);
                cleanerSerialized.FindProperty("brushRadiusUv")
                    .floatValue = 0.095f;
                cleanerSerialized.FindProperty("brushHardness")
                    .floatValue = 0.80f;
                cleanerSerialized
                    .FindProperty("progressSampleIntervalSeconds")
                    .floatValue = 0.28f;
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

        private static LevelBase AddLevelComponent(
            GameObject root,
            LevelType type)
        {
            switch (type)
            {
                case LevelType.Sorting:
                    return root.AddComponent<SortingLevel>();
                case LevelType.ScrewPuzzle:
                    return root.AddComponent<ScrewPuzzleLevel>();
                case LevelType.Fitting:
                    return root.AddComponent<FittingLevel>();
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(type),
                        type,
                        "This builder only creates snap-driven levels here.");
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

            if (levelIndex == 4)
            {
                AddPrimitive(
                    targetRoot.transform,
                    PrimitiveType.Cube,
                    "Fastener Socket",
                    new Vector3(0f, -0.18f, 0f),
                    new Vector3(0.92f, 0.10f, 0.58f),
                    Quaternion.identity,
                    material,
                    false);
            }
            else
            {
                Vector3 scale = GetShapeScale(shape, target: true);
                AddPrimitive(
                    targetRoot.transform,
                    ToPrimitiveType(shape),
                    "Socket",
                    new Vector3(0f, -0.22f, 0f),
                    scale,
                    GetShapeRotation(shape),
                    material,
                    false);
            }

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
            else if (levelIndex == 4)
            {
                CreateFastener(item.transform, index, material);
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

        private static void CreateFastener(
            Transform parent,
            int index,
            Material material)
        {
            float length = 0.72f + index * 0.08f;
            AddPrimitive(
                parent,
                PrimitiveType.Cylinder,
                "Fastener Shaft",
                new Vector3(0f, 0f, 0.12f),
                new Vector3(0.18f, length * 0.5f, 0.18f),
                Quaternion.Euler(90f, 0f, 0f),
                material,
                true);
            AddPrimitive(
                parent,
                PrimitiveType.Cylinder,
                "Fastener Head",
                new Vector3(0f, 0f, -length * 0.48f),
                new Vector3(0.40f, 0.12f, 0.40f),
                Quaternion.Euler(90f, 0f, 0f),
                material,
                true);
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
            Shader shader = Shader.Find("CalmSpace/CleanableSurface");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "The CalmSpace cleanable surface shader is unavailable.");
            }

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    CleanMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, CleanMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.name = "Clean Tray";
            material.enableInstancing = true;
            material.SetColor(
                "_BaseColor",
                new Color(0.94f, 0.88f, 0.74f, 1f));
            material.SetColor(
                "_DirtColor",
                new Color(0.28f, 0.20f, 0.14f, 1f));
            material.SetFloat("_DirtStrength", 0.82f);
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

        private readonly struct LevelSpec
        {
            public LevelSpec(
                string id,
                string displayName,
                LevelType type,
                string definitionPath,
                string prefabPath,
                string address,
                int itemCount,
                float snapThreshold)
            {
                Id = id;
                DisplayName = displayName;
                Type = type;
                DefinitionPath = definitionPath;
                PrefabPath = prefabPath;
                Address = address;
                ItemCount = itemCount;
                SnapThreshold = snapThreshold;
            }

            public string Id { get; }

            public string DisplayName { get; }

            public LevelType Type { get; }

            public string DefinitionPath { get; }

            public string PrefabPath { get; }

            public string Address { get; }

            public int ItemCount { get; }

            public float SnapThreshold { get; }
        }
    }
}
