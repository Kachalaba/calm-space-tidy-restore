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
                CreateBeats());
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static WorkshopBeatDefinition[] CreateBeats()
        {
            var beats = new WorkshopBeatDefinition[
                WorkshopContentIds.CozyWorkshopBeatCount];
            for (var index = 0; index < beats.Length; index++)
            {
                if (!WorkshopContentIds.TryGetCozyWorkshopBeat(
                        index,
                        out var contract))
                {
                    throw new InvalidOperationException(
                        "Canonical workshop stage is missing: " +
                        index +
                        ".");
                }

                beats[index] = new WorkshopBeatDefinition(
                    WorkshopContentIds.CozyWorkshopChapterId,
                    contract.BeatId,
                    contract.StageIndex,
                    contract.TitleTextKey,
                    contract.ResultTextKey,
                    contract.ZoneIndex,
                    contract.MemoryId,
                    contract.UnlockedDecorSlotId,
                    contract.UnlocksDailyCare,
                    contract.IsFinale);
            }

            return beats;
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
