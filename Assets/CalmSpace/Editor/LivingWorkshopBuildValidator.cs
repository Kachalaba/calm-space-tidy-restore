using CalmSpace.Levels;
using CalmSpace.Workshop;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace CalmSpace.Editor
{
    /// <summary>
    /// Blocks player builds when workshop meta content cannot be resolved
    /// against the independently-authored level chapter.
    /// </summary>
    public sealed class LivingWorkshopBuildValidator :
        IPreprocessBuildWithReport
    {
        public int callbackOrder => -900;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateProject();
        }

        public static void ValidateProject()
        {
            LevelCatalog levels =
                AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                    CalmSpaceProjectSetup.LevelCatalogPath);
            LivingWorkshopCatalog workshop =
                AssetDatabase.LoadAssetAtPath<
                    LivingWorkshopCatalog>(
                    CalmSpaceWorkshopCatalogBuilder.CatalogPath);

            ValidateOrThrow(
                levels,
                workshop,
                WorkshopContentIds.CozyWorkshopChapterId);
        }

        public static void ValidateOrThrow(
            LevelCatalog levels,
            LivingWorkshopCatalog workshop,
            string chapterId)
        {
            WorkshopCatalogValidationResult result =
                WorkshopCatalogValidator.ValidateChapter(
                    levels,
                    workshop,
                    chapterId);
            if (result.IsValid)
            {
                return;
            }

            throw new BuildFailedException(
                "Living workshop catalog validation failed: " +
                result.Code +
                ", stage " +
                result.InvalidStageIndex +
                ", stable id '" +
                result.StableId +
                "'.");
        }
    }
}
