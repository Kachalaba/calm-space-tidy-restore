using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CalmSpace.UI;
using CalmSpace.Workshop;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace CalmSpace.Tests.EditMode
{
    /// <summary>
    /// Authoring guarantees for the illustrated room: aligned original art,
    /// mobile-safe import settings, one atlas, a bounded material budget, and
    /// a prefab whose eight beat bindings match the stable catalog IDs.
    /// </summary>
    public sealed class WorkshopVisualAuthoringTests
    {
        private const string ArtRoot =
            "Assets/CalmSpace/UI/Workshop/Art/Room";
        private const string ManifestPath =
            "Assets/CalmSpace/UI/Workshop/workshop-room-manifest.json";
        private const string AtlasPath =
            "Assets/CalmSpace/UI/Workshop/WorkshopRoom.spriteatlas";
        private const string PrefabPath =
            "Assets/CalmSpace/Content/Workshop/CozyWorkshopRoom.prefab";

        private const int ReferenceWidth = 1080;
        private const int ReferenceHeight = 2400;

        // 48dp on a Pixel 8 (1080x2400, density 2.625) is 126 physical
        // pixels, which on a 1080x2400 reference canvas is 126 reference
        // units. 128 keeps a margin over the requirement.
        private const int MinimumHitTargetPixels = 128;

        private sealed class ZoneRecord
        {
            public int StageIndex;
            public string BeatId;
            public string Restored;
            public string Before;
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }

        private static List<ZoneRecord> ReadZones()
        {
            Assert.That(
                File.Exists(ManifestPath),
                Is.True,
                "The generated room manifest is missing: " + ManifestPath);
            string json = File.ReadAllText(ManifestPath);
            var zones = new List<ZoneRecord>();
            int cursor = 0;
            while (true)
            {
                int start = json.IndexOf("\"beatId\"", cursor,
                    StringComparison.Ordinal);
                if (start < 0)
                {
                    break;
                }

                int objectStart = json.LastIndexOf('{', start);
                int objectEnd = json.IndexOf('}', start);
                Assert.That(objectStart, Is.GreaterThanOrEqualTo(0));
                Assert.That(objectEnd, Is.GreaterThan(objectStart));
                string block = json.Substring(
                    objectStart, objectEnd - objectStart + 1);
                zones.Add(new ZoneRecord
                {
                    StageIndex = ReadInt(block, "stageIndex"),
                    BeatId = ReadString(block, "beatId"),
                    Restored = ReadString(block, "restored"),
                    Before = ReadString(block, "before"),
                    X = ReadInt(block, "x"),
                    Y = ReadInt(block, "y"),
                    Width = ReadInt(block, "width"),
                    Height = ReadInt(block, "height")
                });
                cursor = objectEnd;
            }

            return zones.OrderBy(zone => zone.StageIndex).ToList();
        }

        private static string ReadString(string block, string key)
        {
            int index = block.IndexOf("\"" + key + "\"",
                StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0),
                "Manifest entry is missing key " + key);
            int open = block.IndexOf('"', block.IndexOf(':', index) + 1);
            int close = block.IndexOf('"', open + 1);
            return block.Substring(open + 1, close - open - 1);
        }

        private static int ReadInt(string block, string key)
        {
            int index = block.IndexOf("\"" + key + "\"",
                StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0),
                "Manifest entry is missing key " + key);
            int colon = block.IndexOf(':', index);
            int end = colon + 1;
            while (end < block.Length &&
                   (char.IsDigit(block[end]) ||
                    block[end] == '-' ||
                    block[end] == ' '))
            {
                end++;
            }

            return int.Parse(
                block.Substring(colon + 1, end - colon - 1).Trim(),
                CultureInfo.InvariantCulture);
        }

        [Test]
        public void ManifestDescribesEveryStableCatalogBeatInOrder()
        {
            List<ZoneRecord> zones = ReadZones();
            Assert.That(
                zones.Count,
                Is.EqualTo(WorkshopContentIds.CozyWorkshopBeatCount),
                "The room must author exactly one zone per catalog beat.");

            for (var stageIndex = 0; stageIndex < zones.Count; stageIndex++)
            {
                Assert.That(
                    WorkshopContentIds.TryGetCozyWorkshopBeat(
                        stageIndex, out WorkshopBeatContract beat),
                    Is.True);
                Assert.That(zones[stageIndex].StageIndex, Is.EqualTo(stageIndex));
                Assert.That(
                    zones[stageIndex].BeatId,
                    Is.EqualTo(beat.BeatId),
                    "Zone order must follow the stable catalog stage order.");
            }

            Assert.That(
                zones.Select(zone => zone.BeatId).Distinct().Count(),
                Is.EqualTo(zones.Count),
                "Beat IDs must be unique.");
        }

        [Test]
        public void ZoneRectanglesAreDisjointInsideThePortraitComposition()
        {
            List<ZoneRecord> zones = ReadZones();
            foreach (ZoneRecord zone in zones)
            {
                Assert.That(zone.X, Is.GreaterThanOrEqualTo(0));
                Assert.That(zone.Y, Is.GreaterThanOrEqualTo(0));
                Assert.That(
                    zone.X + zone.Width, Is.LessThanOrEqualTo(ReferenceWidth));
                Assert.That(
                    zone.Y + zone.Height,
                    Is.LessThanOrEqualTo(ReferenceHeight));
                Assert.That(
                    zone.Width,
                    Is.GreaterThanOrEqualTo(MinimumHitTargetPixels),
                    zone.BeatId + " hotspot is narrower than 48dp.");
                Assert.That(
                    zone.Height,
                    Is.GreaterThanOrEqualTo(MinimumHitTargetPixels),
                    zone.BeatId + " hotspot is shorter than 48dp.");
            }

            for (var a = 0; a < zones.Count; a++)
            {
                for (int b = a + 1; b < zones.Count; b++)
                {
                    bool separated =
                        zones[a].X + zones[a].Width <= zones[b].X ||
                        zones[b].X + zones[b].Width <= zones[a].X ||
                        zones[a].Y + zones[a].Height <= zones[b].Y ||
                        zones[b].Y + zones[b].Height <= zones[a].Y;
                    Assert.That(
                        separated,
                        Is.True,
                        $"{zones[a].BeatId} overlaps {zones[b].BeatId}; " +
                        "restored zones must compose independently.");
                }
            }
        }

        [Test]
        public void EveryRoomTextureImportsWithMobileSafeSettings()
        {
            List<ZoneRecord> zones = ReadZones();
            var expected = new List<(string File, int Width, int Height)>
            {
                ("WorkshopBaseDirty.png", ReferenceWidth, ReferenceHeight),
                ("FinalSunlight.png", ReferenceWidth, ReferenceHeight)
            };
            foreach (ZoneRecord zone in zones)
            {
                expected.Add((zone.Restored, zone.Width, zone.Height));
                expected.Add((zone.Before, zone.Width, zone.Height));
            }

            foreach ((string file, int width, int height) in expected)
            {
                string path = ArtRoot + "/" + file;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.That(
                    sprite,
                    Is.Not.Null,
                    path + " is not imported as a Sprite.");

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, path + " has no texture.");
                Assert.That(
                    texture.width,
                    Is.EqualTo(width),
                    path + " width drifted from the aligned master.");
                Assert.That(
                    texture.height,
                    Is.EqualTo(height),
                    path + " height drifted from the aligned master.");

                var importer =
                    AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path + " has no importer.");
                Assert.That(
                    importer.textureType,
                    Is.EqualTo(TextureImporterType.Sprite));
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.isReadable, Is.False, path);
                Assert.That(
                    importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);
                Assert.That(
                    importer.filterMode, Is.EqualTo(FilterMode.Bilinear), path);
                Assert.That(
                    importer.maxTextureSize,
                    Is.GreaterThanOrEqualTo(Mathf.Max(width, height)),
                    path + " would be downscaled and break zone alignment.");

                TextureImporterPlatformSettings android =
                    importer.GetPlatformTextureSettings("Android");
                Assert.That(android.overridden, Is.True, path);
                Assert.That(
                    android.format,
                    Is.EqualTo(TextureImporterFormat.ASTC_6x6),
                    path + " must use Android ASTC 6x6.");
            }
        }

        [Test]
        public void RoomArtIsPackedIntoASingleAtlasWithoutDuplicates()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
            Assert.That(atlas, Is.Not.Null, "Missing " + AtlasPath);

            UnityEngine.Object[] packables =
                SpriteAtlasExtensions.GetPackables(atlas);
            Assert.That(packables, Is.Not.Null);
            Assert.That(
                packables.Length,
                Is.GreaterThan(0),
                "The workshop atlas packs nothing.");
            string[] paths = packables
                .Select(AssetDatabase.GetAssetPath)
                .ToArray();
            Assert.That(
                paths.Distinct().Count(),
                Is.EqualTo(paths.Length),
                "The atlas contains duplicate source entries.");

            List<ZoneRecord> zones = ReadZones();
            var covered = new List<string> { "WorkshopBaseDirty.png" };
            foreach (ZoneRecord zone in zones)
            {
                covered.Add(zone.Restored);
                covered.Add(zone.Before);
            }

            foreach (string file in covered)
            {
                string path = ArtRoot + "/" + file;
                bool packed = paths.Any(
                    candidate =>
                        string.Equals(candidate, path, StringComparison.Ordinal) ||
                        path.StartsWith(candidate + "/", StringComparison.Ordinal));
                Assert.That(packed, Is.True, path + " is not in the atlas.");
            }

            TextureImporterPlatformSettings android =
                atlas.GetPlatformSettings("Android");
            Assert.That(
                android.format,
                Is.EqualTo(TextureImporterFormat.ASTC_6x6),
                "The workshop atlas must use Android ASTC 6x6.");
        }

        [Test]
        public void RoomPrefabBindsEightBeatsWithOneHotspotEach()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "Missing " + PrefabPath);

            var presenter = prefab.GetComponent<WorkshopRoomPresenter>();
            Assert.That(
                presenter,
                Is.Not.Null,
                "The room prefab root must own the presenter.");
            Assert.That(
                presenter.ChapterId,
                Is.EqualTo(WorkshopContentIds.CozyWorkshopChapterId));
            Assert.That(
                presenter.BeatCount,
                Is.EqualTo(WorkshopContentIds.CozyWorkshopBeatCount));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < presenter.BeatCount; index++)
            {
                WorkshopRoomBeatBinding binding = presenter.GetBeat(index);
                Assert.That(binding, Is.Not.Null, "Beat " + index);
                Assert.That(
                    binding.IsValid,
                    Is.True,
                    "Beat " + index + " is missing an authored reference.");
                Assert.That(
                    WorkshopContentIds.TryGetCozyWorkshopBeat(
                        index, out WorkshopBeatContract beat),
                    Is.True);
                Assert.That(binding.BeatId, Is.EqualTo(beat.BeatId));
                Assert.That(binding.ZoneIndex, Is.EqualTo(beat.ZoneIndex));
                Assert.That(
                    seen.Add(binding.BeatId),
                    Is.True,
                    "Duplicate beat binding " + binding.BeatId);

                var hotspotRect =
                    binding.Hotspot.GetComponent<RectTransform>();
                Assert.That(hotspotRect, Is.Not.Null);
                Rect worldRect = ReferenceRect(hotspotRect);
                Assert.That(
                    worldRect.width,
                    Is.GreaterThanOrEqualTo(MinimumHitTargetPixels),
                    binding.BeatId + " hotspot is narrower than 48dp.");
                Assert.That(
                    worldRect.height,
                    Is.GreaterThanOrEqualTo(MinimumHitTargetPixels),
                    binding.BeatId + " hotspot is shorter than 48dp.");
            }
        }

        [Test]
        public void RoomPrefabStaysInsideTheMaterialBudget()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "Missing " + PrefabPath);

            var materials = new HashSet<int>();
            foreach (Graphic graphic in
                     prefab.GetComponentsInChildren<Graphic>(true))
            {
                Material material = graphic.material;
                materials.Add(
                    material == null ? 0 : material.GetInstanceID());
            }

            Assert.That(
                materials.Count,
                Is.LessThanOrEqualTo(4),
                "The room must not exceed four material instances.");
        }

        [Test]
        public void PresenterHasNoPollingUpdateLoop()
        {
            Type type = typeof(WorkshopRoomPresenter);
            foreach (string name in new[]
                     {
                         "Update", "LateUpdate", "FixedUpdate", "OnGUI"
                     })
            {
                Assert.That(
                    type.GetMethod(
                        name,
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic),
                    Is.Null,
                    "The idle room must not poll in " + name + ".");
            }
        }

        private static Rect ReferenceRect(RectTransform rect)
        {
            // The prefab authors zones with stretched anchors against the
            // 1080x2400 reference canvas, so convert the normalized anchors
            // back into reference pixels.
            float width = (rect.anchorMax.x - rect.anchorMin.x) *
                ReferenceWidth + rect.sizeDelta.x;
            float height = (rect.anchorMax.y - rect.anchorMin.y) *
                ReferenceHeight + rect.sizeDelta.y;
            return new Rect(0f, 0f, width, height);
        }
    }
}
