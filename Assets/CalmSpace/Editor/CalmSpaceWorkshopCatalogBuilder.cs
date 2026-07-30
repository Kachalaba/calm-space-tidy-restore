using System;
using System.Reflection;
using CalmSpace.Workshop;
using UnityEditor;
using UnityEngine;

namespace CalmSpace.Editor
{
    /// <summary>
    /// Owns the deterministic living-workshop authoring asset. Level order
    /// remains owned exclusively by LevelCatalog.
    /// </summary>
    internal static class CalmSpaceWorkshopCatalogBuilder
    {
        public const string CatalogPath =
            "Assets/CalmSpace/Config/LivingWorkshopCatalog.asset";

        private static readonly WorkshopBeatDefinition[] Beats =
        {
            CreateBeat(
                WorkshopContentIds.ClearPassageBeatId,
                0,
                "clear-passage"),
            CreateBeat(
                WorkshopContentIds.PebbleShelfBeatId,
                1,
                "pebble-shelf",
                memoryId:
                    WorkshopContentIds
                        .SummerTrailStonesMemoryId),
            CreateBeat(
                WorkshopContentIds.TeaDrawerBeatId,
                2,
                "tea-drawer",
                unlocksDailyCare: true),
            CreateBeat(
                WorkshopContentIds.PaintShelfBeatId,
                3,
                "paint-shelf",
                decorSlotId:
                    WorkshopContentIds
                        .WorkbenchAccentDecorSlotId),
            CreateBeat(
                WorkshopContentIds.FastenerTrayBeatId,
                4,
                "fastener-tray",
                memoryId:
                    WorkshopContentIds.FixEverythingMemoryId),
            CreateBeat(
                WorkshopContentIds.WarmWorkbenchBeatId,
                5,
                "warm-workbench",
                decorSlotId:
                    WorkshopContentIds.WarmLightDecorSlotId),
            CreateBeat(
                WorkshopContentIds.CabinetHingeBeatId,
                6,
                "cabinet-hinge"),
            CreateBeat(
                WorkshopContentIds.OpenWindowBeatId,
                7,
                "open-window",
                memoryId:
                    WorkshopContentIds.OpenWindowsMemoryId,
                isFinale: true)
        };

        public static LivingWorkshopCatalog CreateOrUpdate()
        {
            LivingWorkshopCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    LivingWorkshopCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog =
                    ScriptableObject.CreateInstance<
                        LivingWorkshopCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            SetPrivateField(
                catalog,
                "_beats",
                CloneBeats());
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static WorkshopBeatDefinition CreateBeat(
            string beatId,
            int stageIndex,
            string textKeyStem,
            string memoryId = "",
            string decorSlotId = "",
            bool unlocksDailyCare = false,
            bool isFinale = false)
        {
            return new WorkshopBeatDefinition(
                WorkshopContentIds.CozyWorkshopChapterId,
                beatId,
                stageIndex,
                $"beat.{textKeyStem}.title",
                $"beat.{textKeyStem}.result",
                stageIndex,
                memoryId,
                decorSlotId,
                unlocksDailyCare,
                isFinale);
        }

        private static WorkshopBeatDefinition[] CloneBeats()
        {
            var copy =
                new WorkshopBeatDefinition[Beats.Length];
            for (var index = 0; index < Beats.Length; index++)
            {
                WorkshopBeatDefinition beat = Beats[index];
                copy[index] = new WorkshopBeatDefinition(
                    beat.ChapterId,
                    beat.BeatId,
                    beat.StageIndex,
                    beat.TitleTextKey,
                    beat.ResultTextKey,
                    beat.ZoneIndex,
                    beat.MemoryId,
                    beat.UnlockedDecorSlotId,
                    beat.UnlocksDailyCare,
                    beat.IsFinale);
            }

            return copy;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(
                    target.GetType().FullName,
                    fieldName);
            }

            field.SetValue(target, value);
        }
    }
}
