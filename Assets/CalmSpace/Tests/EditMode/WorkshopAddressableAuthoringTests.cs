using System;
using System.Collections.Generic;
using System.Linq;
using CalmSpace.UI;
using CalmSpace.Workshop;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace CalmSpace.Tests.EditMode
{
    /// <summary>
    /// The room is reachable through exactly one Addressable entry, and the
    /// loader is the only owner of that address.
    /// </summary>
    public sealed class WorkshopAddressableAuthoringTests
    {
        private const string RoomAddress = "workshop/cozy-workshop/room";
        private const string RoomLabel =
            "calm-space-workshop-cozy-workshop";
        private const string RoomGuid =
            "c4caa204aabee4dfbbd6705bd97b4168";
        private const string PrefabPath =
            "Assets/CalmSpace/Content/Workshop/CozyWorkshopRoom.prefab";
        private const string WorkshopGroupName = "Workshop Local";
        private static readonly string[] StableLevelAddresses =
        {
            "levels/01-soft-blocks",
            "levels/02-pebble-pairs",
            "levels/03-tea-drawer",
            "levels/04-color-shelf",
            "levels/05-fastener-tray",
            "levels/06-fresh-surface",
            "levels/07-cabinet-hinge",
            "levels/08-dusty-window"
        };

        private static List<AddressableAssetEntry> AllEntries()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.That(
                settings,
                Is.Not.Null,
                "Addressables settings have not been created.");
            var entries = new List<AddressableAssetEntry>();
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                entries.AddRange(group.entries.Where(entry => entry != null));
            }

            return entries;
        }

        [Test]
        public void LoaderBuildsTheExactChapterAddress()
        {
            Assert.That(
                AddressableWorkshopRoomLoader.BuildAddress(
                    WorkshopContentIds.CozyWorkshopChapterId),
                Is.EqualTo(RoomAddress));
        }

        [Test]
        public void RoomIsExposedByExactlyOneEntryAtTheExactAddress()
        {
            List<AddressableAssetEntry> entries = AllEntries();
            List<AddressableAssetEntry> matches = entries
                .Where(entry => string.Equals(
                    entry.address, RoomAddress, StringComparison.Ordinal))
                .ToList();

            Assert.That(
                matches.Count,
                Is.EqualTo(1),
                "Exactly one Addressable entry must publish the room.");
            Assert.That(
                matches[0].guid,
                Is.EqualTo(RoomGuid),
                "The stable room prefab GUID must not change.");
            Assert.That(
                AssetDatabase.GUIDToAssetPath(matches[0].guid),
                Is.EqualTo(PrefabPath));
            Assert.That(
                matches[0].labels.Contains(RoomLabel),
                Is.True,
                "The room entry is missing label " + RoomLabel);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                prefab.GetComponent<WorkshopRoomPresenter>(),
                Is.Not.Null);
        }

        [Test]
        public void RoomEntryBelongsToWorkshopLocalGroup()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.That(settings, Is.Not.Null);

            AddressableAssetGroup group = settings.FindGroup(WorkshopGroupName);
            Assert.That(group, Is.Not.Null, WorkshopGroupName + " is missing.");
            Assert.That(
                group.entries.Count,
                Is.EqualTo(1),
                WorkshopGroupName + " must contain only the resident room.");
            AddressableAssetEntry room = group.entries.Single();
            Assert.That(room.guid, Is.EqualTo(RoomGuid));
            Assert.That(room.address, Is.EqualTo(RoomAddress));
        }

        [Test]
        public void WorkshopLocalGroupIsBundledAndLocal()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.That(settings, Is.Not.Null);

            AddressableAssetGroup workshop =
                settings.FindGroup(WorkshopGroupName);
            Assert.That(workshop, Is.Not.Null, WorkshopGroupName + " is missing.");

            BundledAssetGroupSchema workshopSchema =
                workshop.GetSchema<BundledAssetGroupSchema>();
            BundledAssetGroupSchema defaultSchema =
                settings.DefaultGroup.GetSchema<BundledAssetGroupSchema>();
            Assert.That(workshopSchema, Is.Not.Null);
            Assert.That(defaultSchema, Is.Not.Null);
            Assert.That(workshopSchema.IsEnabled, Is.True);
            Assert.That(workshopSchema.IncludeInBuild, Is.True);
            Assert.That(
                workshopSchema.BundleMode,
                Is.EqualTo(BundledAssetGroupSchema.BundlePackingMode.PackTogether));
            Assert.That(
                workshopSchema.BuildPath.Id,
                Is.EqualTo(defaultSchema.BuildPath.Id));
            Assert.That(
                workshopSchema.LoadPath.Id,
                Is.EqualTo(defaultSchema.LoadPath.Id));
        }

        [Test]
        public void DefaultLocalGroupContainsNoWorkshopAddress()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.That(settings, Is.Not.Null);
            Assert.That(
                settings.DefaultGroup.entries.Any(entry =>
                    string.Equals(
                        entry.address, RoomAddress, StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        public void RoomEntryDoesNotDisturbTheEightLevelEntries()
        {
            List<AddressableAssetEntry> entries = AllEntries();
            foreach (string address in StableLevelAddresses)
            {
                Assert.That(
                    entries.Count(entry => string.Equals(
                        entry.address, address, StringComparison.Ordinal)),
                    Is.EqualTo(1),
                    address + " must survive room authoring.");
            }
        }

        [Test]
        public void RoomPrefabIsTheOnlyWorkshopAddressableAsset()
        {
            List<AddressableAssetEntry> entries = AllEntries();
            List<string> workshopAddresses = entries
                .Select(entry => entry.address)
                .Where(address =>
                    address != null &&
                    address.StartsWith("workshop/", StringComparison.Ordinal))
                .ToList();

            Assert.That(
                workshopAddresses,
                Is.EqualTo(new[] { RoomAddress }),
                "Ship-mode scope publishes only the room under workshop/.");
        }
    }
}
