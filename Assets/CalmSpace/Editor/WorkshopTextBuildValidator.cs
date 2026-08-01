using System;
using System.Collections.Generic;
using CalmSpace.Workshop;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace CalmSpace.Editor
{
    /// <summary>Blocks builds with incomplete localized workshop prose.</summary>
    public sealed class WorkshopTextBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -850;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateProject();
        }

        public static void ValidateProject()
        {
            WorkshopTextCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WorkshopTextCatalog>(
                    CalmSpaceWorkshopTextBuilder.CatalogPath);
            ValidateOrThrow(catalog);
        }

        public static void ValidateOrThrow(WorkshopTextCatalog catalog)
        {
            if (catalog == null)
            {
                throw new BuildFailedException(
                    "Workshop text catalog is missing.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorkshopTextEntry entry in catalog.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    throw new BuildFailedException(
                        "Workshop text catalog contains a blank key.");
                }

                if (!seen.Add(entry.Key))
                {
                    throw new BuildFailedException(
                        "Workshop text catalog contains duplicate key '" +
                        entry.Key + "'.");
                }

                if (string.IsNullOrWhiteSpace(entry.English) ||
                    string.IsNullOrWhiteSpace(entry.Ukrainian) ||
                    string.IsNullOrWhiteSpace(entry.Russian))
                {
                    throw new BuildFailedException(
                        "Workshop text catalog has a blank translation for '" +
                        entry.Key + "'.");
                }
            }

            foreach (string key in CalmSpaceWorkshopTextBuilder.RequiredKeys)
            {
                if (!seen.Contains(key))
                {
                    throw new BuildFailedException(
                        "Workshop text catalog is missing key '" + key +
                        "'.");
                }
            }

            if (seen.Count != CalmSpaceWorkshopTextBuilder.RequiredKeys.Count)
            {
                throw new BuildFailedException(
                    "Workshop text catalog contains an unexpected key.");
            }
        }
    }
}
