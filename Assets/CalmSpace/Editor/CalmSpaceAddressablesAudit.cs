using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace CalmSpace.Editor
{
    /// <summary>
    /// Builds Addressables content and emits a stable, machine-readable audit
    /// of bundle ownership. The output intentionally excludes transient build
    /// metadata and never attempts to mutate Analyze findings.
    /// </summary>
    public static class CalmSpaceAddressablesAudit
    {
        private const int SummarySchemaVersion = 1;
        private const string SummaryPath =
            "Logs/addressables-audit.json";
        private const string BuildLayoutPath =
            "Library/com.unity.addressables/buildlayout.json";
        private const string WorkshopGroupName = "Workshop Local";
        private const string CategorizedAudioRoot =
            "Assets/CalmSpace/Audio/Tactile";

        [Serializable]
        internal sealed class DuplicateDependencyFinding
        {
            public string Group;
            public string Bundle;
            public string AssetPath;
        }

        internal sealed class AnalyzeClassification
        {
            public readonly List<DuplicateDependencyFinding> Duplicates =
                new List<DuplicateDependencyFinding>();
            public readonly List<string> Errors = new List<string>();
        }

        [Serializable]
        private sealed class AssetBundleOwnership
        {
            public string AssetPath;
            public string Bundle;
        }

        [Serializable]
        private sealed class WorkshopRoomEvidence
        {
            public string Address =
                CalmSpaceWorkshopAssetBuilder.RoomAddress;
            public string Guid;
            public string Group;
            public List<string> Bundles = new List<string>();
            public List<string> SpriteSourceAssets = new List<string>();
            public List<AssetBundleOwnership> SpriteOwners =
                new List<AssetBundleOwnership>();
        }

        [Serializable]
        private sealed class AuditSummary
        {
            public int SchemaVersion = SummarySchemaVersion;
            public string Status = "failed";
            public string BuildTarget = "notEvaluated";
            public string AddressablesToAddressablesDuplication =
                "notEvaluated";
            public string PlayerDataDuplication = "notEvaluated";
            public string CategorizedAudioAddressablesPresence =
                "notEvaluated";
            public List<DuplicateDependencyFinding> DuplicateDependencies =
                new List<DuplicateDependencyFinding>();
            public WorkshopRoomEvidence WorkshopRoom =
                new WorkshopRoomEvidence();
            public List<string> CategorizedAudioSourceAssets =
                new List<string>();
            public List<string> CategorizedAudioAssetsInAddressables =
                new List<string>();
            public List<string> Errors = new List<string>();
        }

        private sealed class AuditFailureException : Exception
        {
            public AuditFailureException(string code)
                : base("Addressables audit failed: " + code)
            {
                Code = code;
            }

            public string Code { get; }
        }

        /// <summary>
        /// Batchmode entrypoint. Invoke with
        /// -executeMethod CalmSpace.Editor.CalmSpaceAddressablesAudit.Run.
        /// Any Analyze, build, layout, or duplicate-dependency failure is
        /// rethrown after the deterministic summary has been written.
        /// </summary>
        public static void Run()
        {
            var summary = new AuditSummary();
            bool previousGenerateBuildLayout =
                ProjectConfigData.GenerateBuildLayout;
            ProjectConfigData.ReportFileFormat previousReportFormat =
                ProjectConfigData.BuildLayoutReportFileFormat;

            try
            {
                ExecuteAudit(summary);
                summary.Status = "passed";
            }
            catch (AuditFailureException exception)
            {
                summary.Errors.Add(exception.Code);
                Debug.LogError(exception.Message);
                throw;
            }
            catch (Exception exception)
            {
                summary.Errors.Add("unexpectedFailure");
                Debug.LogException(exception);
                throw;
            }
            finally
            {
                ProjectConfigData.BuildLayoutReportFileFormat =
                    previousReportFormat;
                ProjectConfigData.GenerateBuildLayout =
                    previousGenerateBuildLayout;
                SortSummary(summary);
                WriteSummary(summary);
            }
        }

        internal static AnalyzeClassification ClassifyAnalyzeResults(
            IReadOnlyList<AnalyzeRule.AnalyzeResult> results)
        {
            var classification = new AnalyzeClassification();
            if (results == null || results.Count == 0)
            {
                classification.Errors.Add("analyzeResultsMissing");
                return classification;
            }

            var uniqueFindings = new Dictionary<
                string,
                DuplicateDependencyFinding>(StringComparer.Ordinal);
            for (var index = 0; index < results.Count; index++)
            {
                AnalyzeRule.AnalyzeResult result = results[index];
                string name = result?.resultName;
                if (result == null || result.severity == MessageType.Error)
                {
                    classification.Errors.Add("analyzeExecutionFailed");
                    continue;
                }

                if (string.Equals(
                        name,
                        "No issues found",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(name) ||
                    name.IndexOf(
                        "Analyze build failed",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf(
                        "Cannot run Analyze",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    classification.Errors.Add("analyzeExecutionFailed");
                    continue;
                }

                string[] parts = name.Split(
                    new[] { AnalyzeRule.kDelimiter },
                    StringSplitOptions.None);
                if (parts.Length != 3 ||
                    string.IsNullOrEmpty(parts[0]) ||
                    string.IsNullOrEmpty(parts[1]) ||
                    string.IsNullOrEmpty(parts[2]))
                {
                    classification.Errors.Add("analyzeResultMalformed");
                    continue;
                }

                bool firstIsAsset = TryNormalizeAssetPath(
                    parts[0], out string firstAssetPath);
                bool thirdIsAsset = TryNormalizeAssetPath(
                    parts[2], out string thirdAssetPath);
                if (firstIsAsset == thirdIsAsset)
                {
                    bool hasUnsafePath =
                        LooksLikeUnsafeAssetPath(parts[0]) ||
                        LooksLikeUnsafeAssetPath(parts[2]);
                    string error = firstIsAsset
                        ? "analyzeResultAmbiguous"
                        : hasUnsafePath
                            ? "analyzeResultUnsafeAssetPath"
                            : "analyzeResultMalformed";
                    classification.Errors.Add(error);
                    continue;
                }

                string group = firstIsAsset ? parts[1] : parts[0];
                string bundle = firstIsAsset ? parts[2] : parts[1];
                string assetPath = firstIsAsset
                    ? firstAssetPath
                    : thirdAssetPath;
                if (LooksLikeUnsafeAssetPath(group) ||
                    LooksLikeUnsafeAssetPath(bundle))
                {
                    classification.Errors.Add(
                        "analyzeResultUnsafeAssetPath");
                    continue;
                }

                string key = group + "\n" + bundle + "\n" + assetPath;
                if (!uniqueFindings.ContainsKey(key))
                {
                    uniqueFindings.Add(
                        key,
                        new DuplicateDependencyFinding
                        {
                            Group = group,
                            Bundle = bundle,
                            AssetPath = assetPath
                        });
                }
            }

            classification.Duplicates.AddRange(uniqueFindings.Values);
            classification.Duplicates.Sort(CompareDuplicateFindings);
            SortAndDeduplicate(classification.Errors);
            return classification;
        }

        private static bool TryNormalizeAssetPath(
            string value,
            out string normalized)
        {
            normalized = value?.Replace('\\', '/');
            if (string.IsNullOrEmpty(normalized) ||
                Path.IsPathRooted(normalized) ||
                !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return false;
            }

            string[] segments = normalized.Split('/');
            for (var index = 0; index < segments.Length; index++)
            {
                if (string.IsNullOrEmpty(segments[index]) ||
                    string.Equals(
                        segments[index], ".", StringComparison.Ordinal) ||
                    string.Equals(
                        segments[index], "..", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool LooksLikeUnsafeAssetPath(string value)
        {
            string normalized = value?.Replace('\\', '/');
            return !string.IsNullOrEmpty(normalized) &&
                (Path.IsPathRooted(normalized) ||
                 normalized.StartsWith("Assets/", StringComparison.Ordinal));
        }

        private static void ExecuteAudit(AuditSummary summary)
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
            {
                throw new AuditFailureException("settingsMissing");
            }

            // Analyze is deliberately first and read-only. Never invoke Fix.
            var duplicateRule = new CheckBundleDupeDependencies();
            List<AnalyzeRule.AnalyzeResult> analyzeResults =
                duplicateRule.RefreshAnalysis(settings);
            AnalyzeClassification classification =
                ClassifyAnalyzeResults(analyzeResults);
            summary.DuplicateDependencies.AddRange(
                classification.Duplicates);
            summary.AddressablesToAddressablesDuplication =
                GetAddressablesDuplicationStatus(classification);
            if (classification.Errors.Count > 0)
            {
                throw new AuditFailureException(classification.Errors[0]);
            }

            ProjectConfigData.GenerateBuildLayout = true;
            ProjectConfigData.BuildLayoutReportFileFormat =
                ProjectConfigData.ReportFileFormat.JSON;

            if (settings.ActivePlayerDataBuilder == null)
            {
                throw new AuditFailureException("activeDataBuilderMissing");
            }

            string fullBuildLayoutPath =
                ResolveProjectPath(BuildLayoutPath);
            RemovePriorBuildLayout(fullBuildLayoutPath);
            AddressableAssetSettings.CleanPlayerContent(
                settings.ActivePlayerDataBuilder);
            AddressableAssetSettings.BuildPlayerContent(
                out AddressablesPlayerBuildResult buildResult);
            if (buildResult == null ||
                !string.IsNullOrEmpty(buildResult.Error))
            {
                throw new AuditFailureException("addressablesBuildFailed");
            }

            if (!File.Exists(fullBuildLayoutPath))
            {
                throw new AuditFailureException("buildLayoutMissing");
            }

            BuildLayout layout = BuildLayout.Open(
                fullBuildLayoutPath,
                readHeader: true,
                readFullFile: true);
            if (layout == null)
            {
                throw new AuditFailureException("buildLayoutMissing");
            }

            try
            {
                if (!string.IsNullOrEmpty(layout.BuildError))
                {
                    throw new AuditFailureException("buildLayoutFailed");
                }

                summary.BuildTarget = layout.BuildTarget.ToString();
                PopulateLayoutEvidence(layout, summary);
            }
            finally
            {
                layout.Close();
            }

            if (summary.DuplicateDependencies.Count > 0)
            {
                throw new AuditFailureException(
                    "unintendedAddressablesDuplicates");
            }
        }

        private static void PopulateLayoutEvidence(
            BuildLayout layout,
            AuditSummary summary)
        {
            summary.WorkshopRoom.Guid = AssetDatabase.AssetPathToGUID(
                CalmSpaceWorkshopAssetBuilder.RoomPrefabPath);
            summary.WorkshopRoom.SpriteSourceAssets.AddRange(
                FindAssetPaths(
                    "t:Texture2D",
                    CalmSpaceWorkshopAssetBuilder.RoomArtRoot));
            summary.CategorizedAudioSourceAssets.AddRange(
                FindAssetPaths("t:AudioClip", CategorizedAudioRoot));

            var roomBundles = new HashSet<string>(StringComparer.Ordinal);
            var roomOwners = new Dictionary<
                string,
                AssetBundleOwnership>(StringComparer.Ordinal);
            var addressableAudio =
                new HashSet<string>(StringComparer.Ordinal);
            int explicitRoomCount = 0;
            string explicitRoomGroup = null;

            foreach (BuildLayout.Bundle bundle in
                     BuildLayoutHelpers.EnumerateBundles(layout))
            {
                if (bundle == null)
                {
                    continue;
                }

                string bundleName = StableBundleName(bundle);
                foreach (BuildLayout.File file in bundle.Files)
                {
                    if (file == null)
                    {
                        continue;
                    }

                    foreach (BuildLayout.ExplicitAsset asset in file.Assets)
                    {
                        if (asset == null)
                        {
                            continue;
                        }

                        RecordAssetPresence(
                            asset.AssetPath,
                            bundleName,
                            roomOwners,
                            addressableAudio);
                        if (string.Equals(
                                asset.Guid,
                                summary.WorkshopRoom.Guid,
                                StringComparison.Ordinal) &&
                            string.Equals(
                                asset.AddressableName,
                                CalmSpaceWorkshopAssetBuilder.RoomAddress,
                                StringComparison.Ordinal))
                        {
                            explicitRoomCount++;
                            explicitRoomGroup = bundle.Group?.Name;
                            roomBundles.Add(bundleName);
                        }
                    }

                    foreach (BuildLayout.DataFromOtherAsset asset in
                             file.OtherAssets)
                    {
                        if (asset != null)
                        {
                            RecordAssetPresence(
                                asset.AssetPath,
                                bundleName,
                                roomOwners,
                                addressableAudio);
                        }
                    }
                }
            }

            if (explicitRoomCount != 1)
            {
                throw new AuditFailureException(
                    "roomExplicitAssetOwnershipInvalid");
            }

            if (!string.Equals(
                    explicitRoomGroup,
                    WorkshopGroupName,
                    StringComparison.Ordinal))
            {
                throw new AuditFailureException("roomGroupOwnershipInvalid");
            }

            summary.WorkshopRoom.Group = explicitRoomGroup;
            summary.WorkshopRoom.Bundles.AddRange(roomBundles);
            summary.WorkshopRoom.SpriteOwners.AddRange(roomOwners.Values);
            summary.CategorizedAudioAssetsInAddressables.AddRange(
                addressableAudio);
            summary.CategorizedAudioAddressablesPresence =
                addressableAudio.Count == 0
                    ? "absentFromAddressables"
                    : "presentInAddressables";

            if (summary.WorkshopRoom.SpriteSourceAssets.Count == 0)
            {
                throw new AuditFailureException("roomSpriteSourcesMissing");
            }

            var ownedSpritePaths =
                new HashSet<string>(StringComparer.Ordinal);
            foreach (AssetBundleOwnership owner in roomOwners.Values)
            {
                ownedSpritePaths.Add(owner.AssetPath);
            }

            foreach (string source in
                     summary.WorkshopRoom.SpriteSourceAssets)
            {
                if (!ownedSpritePaths.Contains(source))
                {
                    throw new AuditFailureException(
                        "roomSpriteOwnershipIncomplete");
                }
            }
        }

        internal static string GetAddressablesDuplicationStatus(
            AnalyzeClassification classification)
        {
            if (classification == null || classification.Errors.Count > 0)
            {
                return "notEvaluated";
            }

            return classification.Duplicates.Count == 0
                ? "clear"
                : "unintendedDuplicatesFound";
        }

        internal static void RemovePriorBuildLayout(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) ||
                !Path.IsPathRooted(fullPath))
            {
                throw new ArgumentException(
                    "Build Layout cleanup requires an absolute file path.",
                    nameof(fullPath));
            }

            var layout = new FileInfo(Path.GetFullPath(fullPath));
            DirectoryInfo addressablesDirectory = layout.Directory;
            DirectoryInfo libraryDirectory = addressablesDirectory?.Parent;
            bool isExactLegacyLayout =
                string.Equals(
                    layout.Name,
                    "buildlayout.json",
                    StringComparison.Ordinal) &&
                string.Equals(
                    addressablesDirectory?.Name,
                    "com.unity.addressables",
                    StringComparison.Ordinal) &&
                string.Equals(
                    libraryDirectory?.Name,
                    "Library",
                    StringComparison.Ordinal);
            if (!isExactLegacyLayout)
            {
                throw new InvalidOperationException(
                    "Refusing to delete an unexpected Build Layout path.");
            }

            if (layout.Exists)
            {
                layout.Delete();
            }
        }

        private static void RecordAssetPresence(
            string assetPath,
            string bundleName,
            IDictionary<string, AssetBundleOwnership> roomOwners,
            ISet<string> addressableAudio)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            string normalized = assetPath.Replace('\\', '/');
            if (IsUnder(
                    normalized,
                    CalmSpaceWorkshopAssetBuilder.RoomArtRoot))
            {
                string key = normalized + "\n" + bundleName;
                if (!roomOwners.ContainsKey(key))
                {
                    roomOwners.Add(
                        key,
                        new AssetBundleOwnership
                        {
                            AssetPath = normalized,
                            Bundle = bundleName
                        });
                }
            }

            if (IsUnder(normalized, CategorizedAudioRoot))
            {
                addressableAudio.Add(normalized);
            }
        }

        private static List<string> FindAssetPaths(
            string filter,
            string root)
        {
            string[] guids = AssetDatabase.FindAssets(
                filter,
                new[] { root });
            var paths = new List<string>(guids.Length);
            for (var index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (IsUnder(path, root))
                {
                    paths.Add(path);
                }
            }

            SortAndDeduplicate(paths);
            return paths;
        }

        private static bool IsUnder(string path, string root)
        {
            return !string.IsNullOrEmpty(path) &&
                path.StartsWith(root + "/", StringComparison.Ordinal);
        }

        private static string StableBundleName(BuildLayout.Bundle bundle)
        {
            string name = string.IsNullOrEmpty(bundle.Name)
                ? bundle.InternalName
                : bundle.Name;
            if (string.IsNullOrEmpty(name))
            {
                throw new AuditFailureException("bundleNameMissing");
            }

            return name.Replace('\\', '/');
        }

        private static void SortSummary(AuditSummary summary)
        {
            summary.DuplicateDependencies.Sort(CompareDuplicateFindings);
            SortAndDeduplicate(summary.Errors);
            SortAndDeduplicate(summary.WorkshopRoom.Bundles);
            SortAndDeduplicate(summary.WorkshopRoom.SpriteSourceAssets);
            summary.WorkshopRoom.SpriteOwners.Sort(
                (left, right) =>
                {
                    int path = string.CompareOrdinal(
                        left.AssetPath,
                        right.AssetPath);
                    return path != 0
                        ? path
                        : string.CompareOrdinal(left.Bundle, right.Bundle);
                });
            SortAndDeduplicate(summary.CategorizedAudioSourceAssets);
            SortAndDeduplicate(
                summary.CategorizedAudioAssetsInAddressables);
        }

        private static int CompareDuplicateFindings(
            DuplicateDependencyFinding left,
            DuplicateDependencyFinding right)
        {
            int group = string.CompareOrdinal(left.Group, right.Group);
            if (group != 0)
            {
                return group;
            }

            int bundle = string.CompareOrdinal(left.Bundle, right.Bundle);
            return bundle != 0
                ? bundle
                : string.CompareOrdinal(left.AssetPath, right.AssetPath);
        }

        private static void SortAndDeduplicate(List<string> values)
        {
            values.Sort(StringComparer.Ordinal);
            for (int index = values.Count - 1; index > 0; index--)
            {
                if (string.Equals(
                        values[index],
                        values[index - 1],
                        StringComparison.Ordinal))
                {
                    values.RemoveAt(index);
                }
            }
        }

        private static void WriteSummary(AuditSummary summary)
        {
            string outputPath = ResolveProjectPath(SummaryPath);
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                outputPath,
                JsonUtility.ToJson(summary, true) + Environment.NewLine);
            Debug.Log("CALMSPACE_ADDRESSABLES_AUDIT " + SummaryPath);
        }

        private static string ResolveProjectPath(string relativePath)
        {
            DirectoryInfo projectRoot =
                Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
            {
                throw new InvalidOperationException(
                    "Could not resolve the Unity project root.");
            }

            return Path.Combine(
                projectRoot.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
