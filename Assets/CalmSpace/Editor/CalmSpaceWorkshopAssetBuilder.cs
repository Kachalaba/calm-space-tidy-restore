using System;
using System.Collections.Generic;
using System.IO;
using CalmSpace.UI;
using CalmSpace.Workshop;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CalmSpace.Editor
{
    /// <summary>
    /// Deterministic source of truth for the illustrated workshop room. It
    /// imports the generated art with mobile-safe settings, packs one atlas,
    /// rebuilds the room prefab from the zone manifest, and publishes exactly
    /// one Addressable entry. Running it twice produces identical assets.
    /// </summary>
    public static class CalmSpaceWorkshopAssetBuilder
    {
        public const string WorkshopArtRoot =
            "Assets/CalmSpace/UI/Workshop";
        public const string RoomArtRoot =
            WorkshopArtRoot + "/Art/Room";
        public const string ManifestPath =
            WorkshopArtRoot + "/workshop-room-manifest.json";
        public const string AtlasPath =
            WorkshopArtRoot + "/WorkshopRoom.spriteatlas";
        public const string RoomPrefabPath =
            "Assets/CalmSpace/Content/Workshop/CozyWorkshopRoom.prefab";
        public const string RoomAddress =
            "workshop/cozy-workshop/room";
        public const string RoomLabel =
            "calm-space-workshop-cozy-workshop";

        private const string WorkshopGroupName = "Workshop Local";

        private const int ReferenceWidth = 1080;
        private const int ReferenceHeight = 2400;
        private const int FullCanvasMaxSize = 4096;
        private const int ZoneMaxSize = 1024;

        // 48dp on a Pixel 8 is 126 physical pixels; the canvas maps one
        // reference unit to one physical pixel at 1080x2400.
        private const float MinimumHitTarget = 128f;

        [Serializable]
        private sealed class ZoneEntry
        {
            public int stageIndex;
            public string beatId;
            public string name;
            public string restored;
            public string before;
            public int x;
            public int y;
            public int width;
            public int height;
        }

        [Serializable]
        private sealed class RoomManifest
        {
            public int referenceWidth;
            public int referenceHeight;
            public string chapterId;
            public string baseImage;
            public string finaleImage;
            public ZoneEntry[] zones;
        }

        public static GameObject CreateOrUpdate()
        {
            RoomManifest manifest = ReadManifest();
            EnsureFolders();
            ImportArt(manifest);
            CreateOrUpdateAtlas();
            GameObject prefab = BuildRoomPrefab(manifest);
            RegisterAddressable(prefab);
            return prefab;
        }

        private static RoomManifest ReadManifest()
        {
            if (!File.Exists(ManifestPath))
            {
                throw new InvalidOperationException(
                    "Missing generated room manifest at " + ManifestPath +
                    ". Run tools/art/generate_workshop_room.py first.");
            }

            var manifest = JsonUtility.FromJson<RoomManifest>(
                File.ReadAllText(ManifestPath));
            if (manifest == null ||
                manifest.zones == null ||
                manifest.zones.Length !=
                    WorkshopContentIds.CozyWorkshopBeatCount)
            {
                throw new InvalidOperationException(
                    "The room manifest must describe exactly " +
                    WorkshopContentIds.CozyWorkshopBeatCount + " zones.");
            }

            if (manifest.referenceWidth != ReferenceWidth ||
                manifest.referenceHeight != ReferenceHeight)
            {
                throw new InvalidOperationException(
                    "The room manifest must use the 1080x2400 portrait " +
                    "reference composition.");
            }

            Array.Sort(
                manifest.zones,
                (left, right) => left.stageIndex.CompareTo(right.stageIndex));
            for (var index = 0; index < manifest.zones.Length; index++)
            {
                ZoneEntry zone = manifest.zones[index];
                if (zone.stageIndex != index ||
                    !WorkshopContentIds.TryGetCozyWorkshopBeat(
                        index, out WorkshopBeatContract beat) ||
                    !string.Equals(zone.beatId, beat.BeatId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Room manifest zone " + index +
                        " does not match the stable catalog beat.");
                }
            }

            return manifest;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/CalmSpace/Content");
            EnsureFolder("Assets/CalmSpace/Content/Workshop");
            EnsureFolder("Assets/CalmSpace/UI");
            EnsureFolder(WorkshopArtRoot);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        private static void ImportArt(RoomManifest manifest)
        {
            ImportTexture(
                RoomArtRoot + "/" + manifest.baseImage,
                FullCanvasMaxSize,
                alpha: false);
            ImportTexture(
                RoomArtRoot + "/" + manifest.finaleImage,
                FullCanvasMaxSize,
                alpha: true);
            foreach (ZoneEntry zone in manifest.zones)
            {
                ImportTexture(
                    RoomArtRoot + "/" + zone.restored, ZoneMaxSize,
                    alpha: false);
                ImportTexture(
                    RoomArtRoot + "/" + zone.before, ZoneMaxSize,
                    alpha: false);
            }
        }

        private static void ImportTexture(
            string path,
            int maxSize,
            bool alpha)
        {
            if (AssetImporter.GetAtPath(path) == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                throw new InvalidOperationException(
                    "Missing generated room art at " + path +
                    ". Run tools/art/generate_workshop_room.py first.");
            }

            TextureImporterPlatformSettings android =
                importer.GetPlatformTextureSettings("Android");
            bool changed =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !importer.sRGBTexture ||
                importer.alphaIsTransparency != alpha ||
                importer.mipmapEnabled ||
                importer.isReadable ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.maxTextureSize != maxSize ||
                importer.textureCompression !=
                    TextureImporterCompression.CompressedHQ ||
                !android.overridden ||
                android.maxTextureSize != maxSize ||
                android.format != TextureImporterFormat.ASTC_6x6;

            if (!changed)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.alphaSource = alpha
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = alpha;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(
                new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    maxTextureSize = maxSize,
                    format = TextureImporterFormat.ASTC_6x6,
                    textureCompression =
                        TextureImporterCompression.CompressedHQ
                });
            importer.SaveAndReimport();
        }

        private static void CreateOrUpdateAtlas()
        {
            // Build-time atlas packing keeps the editor deterministic while
            // still shipping one packed page to the device.
            if (EditorSettings.spritePackerMode !=
                SpritePackerMode.BuildTimeOnlyAtlas)
            {
                EditorSettings.spritePackerMode =
                    SpritePackerMode.BuildTimeOnlyAtlas;
            }

            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, AtlasPath);
            }

            atlas.SetIncludeInBuild(true);
            atlas.SetPackingSettings(new SpriteAtlasPackingSettings
            {
                blockOffset = 1,
                enableRotation = false,
                enableTightPacking = false,
                padding = 4
            });
            atlas.SetTextureSettings(new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear
            });
            atlas.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = FullCanvasMaxSize,
                format = TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.CompressedHQ
            });

            // A single folder packable keeps the entry set unambiguous and
            // makes duplicate source entries structurally impossible.
            var folder = AssetDatabase.LoadAssetAtPath<Object>(RoomArtRoot);
            if (folder == null)
            {
                throw new InvalidOperationException(
                    "Missing room art folder " + RoomArtRoot);
            }

            Object[] existing = atlas.GetPackables();
            bool matches = existing != null &&
                existing.Length == 1 &&
                existing[0] == folder;
            if (!matches)
            {
                if (existing != null && existing.Length > 0)
                {
                    atlas.Remove(existing);
                }

                atlas.Add(new[] { folder });
            }

            EditorUtility.SetDirty(atlas);
        }

        private static GameObject BuildRoomPrefab(RoomManifest manifest)
        {
            var root = new GameObject(
                "CozyWorkshopRoom", typeof(RectTransform));
            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                Stretch(rootRect);

                GameObject content = Child(root.transform, "Room Content");
                Stretch(content.GetComponent<RectTransform>());

                Image baseImage = CreateLayer(
                    content.transform,
                    "Base Dirty Room",
                    LoadSprite(RoomArtRoot + "/" + manifest.baseImage));
                Stretch(baseImage.rectTransform);

                GameObject zones = Child(content.transform, "Restored Zones");
                Stretch(zones.GetComponent<RectTransform>());

                GameObject overlays = Child(
                    content.transform, "Reveal Overlays");
                Stretch(overlays.GetComponent<RectTransform>());

                Image finaleImage = CreateLayer(
                    content.transform,
                    "Final Sunlight",
                    LoadSprite(RoomArtRoot + "/" + manifest.finaleImage));
                Stretch(finaleImage.rectTransform);
                CanvasGroup finaleGroup =
                    finaleImage.gameObject.AddComponent<CanvasGroup>();
                finaleGroup.alpha = 0f;
                finaleGroup.interactable = false;
                finaleGroup.blocksRaycasts = false;

                GameObject ambient = Child(content.transform, "Ambient");
                Stretch(ambient.GetComponent<RectTransform>());

                GameObject hotspots = Child(content.transform, "Hotspots");
                Stretch(hotspots.GetComponent<RectTransform>());

                var bindings =
                    new List<WorkshopRoomBeatBinding>(manifest.zones.Length);
                foreach (ZoneEntry zone in manifest.zones)
                {
                    WorkshopContentIds.TryGetCozyWorkshopBeat(
                        zone.stageIndex, out WorkshopBeatContract beat);

                    CanvasGroup restored = CreateZoneLayer(
                        zones.transform,
                        zone,
                        zone.name + " Restored",
                        RoomArtRoot + "/" + zone.restored,
                        0f);
                    CanvasGroup before = CreateZoneLayer(
                        overlays.transform,
                        zone,
                        zone.name + " Before",
                        RoomArtRoot + "/" + zone.before,
                        0f);
                    Button hotspot = CreateHotspot(
                        hotspots.transform, zone, zone.name + " Hotspot");

                    var binding = new WorkshopRoomBeatBinding();
                    binding.Configure(
                        beat.BeatId, beat.ZoneIndex, restored, before, hotspot);
                    bindings.Add(binding);
                }

                var presenter = root.AddComponent<WorkshopRoomPresenter>();
                presenter.Configure(
                    manifest.chapterId,
                    content,
                    ambient,
                    finaleGroup,
                    bindings.ToArray());

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                    root, RoomPrefabPath, out bool success);
                if (!success || saved == null)
                {
                    throw new InvalidOperationException(
                        "Could not save " + RoomPrefabPath);
                }

                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static CanvasGroup CreateZoneLayer(
            Transform parent,
            ZoneEntry zone,
            string name,
            string spritePath,
            float alpha)
        {
            Image image = CreateLayer(parent, name, LoadSprite(spritePath));
            AnchorToZone(image.rectTransform, zone);
            CanvasGroup group = image.gameObject.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        private static Button CreateHotspot(
            Transform parent,
            ZoneEntry zone,
            string name)
        {
            GameObject root = Child(parent, name);
            var rect = root.GetComponent<RectTransform>();
            AnchorToZone(rect, zone);

            // Grow the touch target if a narrow zone would fall below 48dp.
            float extraWidth = Mathf.Max(0f, MinimumHitTarget - zone.width);
            float extraHeight = Mathf.Max(0f, MinimumHitTarget - zone.height);
            rect.sizeDelta = new Vector2(extraWidth, extraHeight);

            var image = root.AddComponent<Image>();
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;

            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            root.SetActive(false);
            return button;
        }

        private static Image CreateLayer(
            Transform parent,
            string name,
            Sprite sprite)
        {
            GameObject root = Child(parent, name);
            var image = root.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    "Missing imported room sprite " + path);
            }

            return sprite;
        }

        /// <summary>
        /// Maps a manifest rectangle in the top-left origin 1080x2400
        /// composition onto normalized anchors, so every zone stays pinned to
        /// the master painting at any device resolution.
        /// </summary>
        private static void AnchorToZone(RectTransform rect, ZoneEntry zone)
        {
            float minX = zone.x / (float)ReferenceWidth;
            float maxX = (zone.x + zone.width) / (float)ReferenceWidth;
            float minY =
                1f - (zone.y + zone.height) / (float)ReferenceHeight;
            float maxY = 1f - zone.y / (float)ReferenceHeight;
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static GameObject Child(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void RegisterAddressable(GameObject prefab)
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                throw new InvalidOperationException(
                    "Addressables settings could not be created.");
            }

            string guid = AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(prefab));
            AddressableAssetGroup workshopGroup =
                EnsureWorkshopGroup(settings, guid);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(
                guid,
                workshopGroup,
                false,
                false);
            entry.address = RoomAddress;
            entry.SetLabel(RoomLabel, true, true, false);
            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.EntryModified,
                entry,
                true,
                false);
        }

        private static AddressableAssetGroup EnsureWorkshopGroup(
            AddressableAssetSettings settings,
            string roomGuid)
        {
            AddressableAssetGroup defaultGroup = settings.DefaultGroup;
            if (defaultGroup == null)
            {
                throw new InvalidOperationException(
                    "Addressables has no default local group to copy.");
            }

            BundledAssetGroupSchema defaultBundle =
                defaultGroup.GetSchema<BundledAssetGroupSchema>();
            if (defaultBundle == null)
            {
                throw new InvalidOperationException(
                    "The default local group has no bundled schema.");
            }

            AddressableAssetGroup workshopGroup =
                settings.FindGroup(WorkshopGroupName);
            if (workshopGroup == null)
            {
                workshopGroup = settings.CreateGroup(
                    WorkshopGroupName,
                    false,
                    false,
                    false,
                    new List<AddressableAssetGroupSchema>(
                        defaultGroup.Schemas));
            }

            ValidateWorkshopGroup(
                workshopGroup,
                defaultGroup,
                defaultBundle,
                roomGuid);
            return workshopGroup;
        }

        internal static void ValidateWorkshopGroup(
            AddressableAssetGroup workshopGroup,
            AddressableAssetGroup defaultGroup,
            BundledAssetGroupSchema defaultBundle,
            string roomGuid)
        {
            foreach (AddressableAssetEntry entry in workshopGroup.entries)
            {
                if (!string.Equals(
                        entry.guid,
                        roomGuid,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        WorkshopGroupName +
                        " already contains an unrelated Addressable entry: " +
                        entry.address + ".");
                }
            }

            if (workshopGroup.entries.Count > 1)
            {
                throw new InvalidOperationException(
                    WorkshopGroupName +
                    " must contain at most the single workshop room entry.");
            }

            if (workshopGroup.Schemas.Count != defaultGroup.Schemas.Count)
            {
                throw new InvalidOperationException(
                    WorkshopGroupName +
                    " schema set has drifted from the default local group.");
            }

            foreach (AddressableAssetGroupSchema expected in
                     defaultGroup.Schemas)
            {
                int matches = 0;
                foreach (AddressableAssetGroupSchema actual in
                         workshopGroup.Schemas)
                {
                    if (actual != null &&
                        expected != null &&
                        actual.GetType() == expected.GetType())
                    {
                        matches++;
                    }
                }

                if (matches != 1)
                {
                    throw new InvalidOperationException(
                        WorkshopGroupName +
                        " schema set has drifted from the default local group.");
                }
            }

            BundledAssetGroupSchema workshopBundle =
                workshopGroup.GetSchema<BundledAssetGroupSchema>();
            bool bundleContractMatches =
                workshopBundle != null &&
                workshopBundle.IsEnabled &&
                workshopBundle.IncludeInBuild &&
                workshopBundle.BundleMode ==
                    BundledAssetGroupSchema.BundlePackingMode.PackTogether &&
                string.Equals(
                    workshopBundle.BuildPath.Id,
                    defaultBundle.BuildPath.Id,
                    StringComparison.Ordinal) &&
                string.Equals(
                    workshopBundle.LoadPath.Id,
                    defaultBundle.LoadPath.Id,
                    StringComparison.Ordinal);
            if (!bundleContractMatches)
            {
                throw new InvalidOperationException(
                    WorkshopGroupName +
                    " must remain an enabled local PackTogether group with " +
                    "the default local build and load profile variables.");
            }
        }
    }
}
