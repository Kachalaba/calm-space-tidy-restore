using System;
using System.Collections.Generic;
using System.Reflection;
using CalmSpace.Cleaning;
using CalmSpace.Core;
using CalmSpace.Input;
using CalmSpace.Levels;
using CalmSpace.Monetization;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CalmSpace.Editor
{
    /// <summary>
    /// One actionable breach of Calm Space's anti-anxiety product contract.
    /// Codes are stable so EditMode tests and CI can assert policy without
    /// depending on player-facing wording.
    /// </summary>
    public readonly struct AntiAnxietyValidationIssue
    {
        public AntiAnxietyValidationIssue(
            string code,
            string context,
            string message)
        {
            Code = code ?? string.Empty;
            Context = context ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }

        public string Context { get; }

        public string Message { get; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Context)
                ? $"[{Code}] {Message}"
                : $"[{Code}] {Context}: {Message}";
        }
    }

    /// <summary>
    /// Enforces structural guarantees that can be proven without interpreting
    /// comments or method bodies: rewarded-only ad presentation, no authored
    /// fail/countdown contracts, undo integration, and low-cost cleaner
    /// coverage settings.
    /// </summary>
    public static class AntiAnxietyProjectValidator
    {
        public const string ForcedAdApiCode = "CALM001";
        public const string NonRewardedShowApiCode = "CALM002";
        public const string NegativeLevelContractCode = "CALM003";
        public const string MissingUndoContractCode = "CALM004";
        public const string CleanerCoverageCode = "CALM005";

        private const string LevelPrefabRoot =
            "Assets/CalmSpace/Content/Levels";
        private const int RequiredCoverageWidth = 64;
        private const int RequiredCoverageHeight = 64;
        private const int MinimumCoverageFrameInterval = 10;

        private static readonly string[] ForbiddenAdContractFragments =
        {
            "interstitial",
            "forcedad",
            "mandatoryad",
            "autoplayad",
            "automaticad",
            "appopenad",
            "bannerad",
            "nativead",
            "offerwall"
        };

        private static readonly string[] ForbiddenLevelContractFragments =
        {
            "countdown",
            "timelimit",
            "remainingtime",
            "remainingseconds",
            "deadline",
            "timeout",
            "failstate",
            "failurestate",
            "failcondition",
            "faillevel",
            "levelfail",
            "canfail",
            "losecondition",
            "losscondition",
            "loselevel",
            "levellose",
            "canlose",
            "gameover",
            "livesremaining",
            "playerlives"
        };

        [MenuItem("Calm Space/Validate Anti-Anxiety Contracts")]
        public static void ValidateFromMenu()
        {
            IReadOnlyList<AntiAnxietyValidationIssue> issues =
                ValidateProject();
            if (issues.Count == 0)
            {
                Debug.Log(
                    "Calm Space anti-anxiety contracts are valid.");
                return;
            }

            throw new InvalidOperationException(
                FormatIssues(issues));
        }

        public static IReadOnlyList<AntiAnxietyValidationIssue>
            ValidateProject()
        {
            var issues = new List<AntiAnxietyValidationIssue>();

            AddRange(
                issues,
                ValidateMonetizationApi(
                    GetMonetizationApiTypes()));
            AddRange(
                issues,
                ValidateLevelContracts(
                    GetRuntimeLevelContractTypes()));
            AddRange(
                issues,
                ValidateUndoContracts(
                    typeof(ItemSnapController),
                    typeof(ScrewInputController)));
            ValidateCleanerCoverageDefaults(issues);

            return issues;
        }

        /// <summary>
        /// Validates supplied API types. Keeping this overload independent of
        /// AssetDatabase makes policy behavior deterministic in EditMode tests.
        /// </summary>
        public static IReadOnlyList<AntiAnxietyValidationIssue>
            ValidateMonetizationApi(IEnumerable<Type> apiTypes)
        {
            var issues = new List<AntiAnxietyValidationIssue>();
            if (apiTypes == null)
            {
                return issues;
            }

            foreach (Type type in apiTypes)
            {
                if (type == null)
                {
                    continue;
                }

                ValidateForbiddenAdName(
                    type.Name,
                    type.FullName,
                    issues);

                MemberInfo[] members = type.GetMembers(
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly);
                for (var index = 0; index < members.Length; index++)
                {
                    MemberInfo member = members[index];
                    ValidateForbiddenAdName(
                        member.Name,
                        $"{type.FullName}.{member.Name}",
                        issues);

                    if (!(member is MethodInfo method) ||
                        method.IsSpecialName ||
                        !method.Name.StartsWith(
                            "Show",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!HasRewardedRequest(method))
                    {
                        issues.Add(
                            new AntiAnxietyValidationIssue(
                                NonRewardedShowApiCode,
                                $"{type.FullName}.{method.Name}",
                                "Every public ad presentation entry point " +
                                "must require an explicit rewarded request."));
                    }
                }

                if (!type.IsEnum)
                {
                    continue;
                }

                string[] values = Enum.GetNames(type);
                for (var index = 0; index < values.Length; index++)
                {
                    ValidateForbiddenAdName(
                        values[index],
                        $"{type.FullName}.{values[index]}",
                        issues);
                }
            }

            return issues;
        }

        /// <summary>
        /// Checks public/protected members and serialized authoring fields.
        /// Private implementation details and comments are intentionally out of
        /// scope: the validator protects contracts designers can configure or
        /// another system can call.
        /// </summary>
        public static IReadOnlyList<AntiAnxietyValidationIssue>
            ValidateLevelContracts(IEnumerable<Type> contractTypes)
        {
            var issues = new List<AntiAnxietyValidationIssue>();
            if (contractTypes == null)
            {
                return issues;
            }

            foreach (Type type in contractTypes)
            {
                if (type == null)
                {
                    continue;
                }

                ValidateForbiddenLevelName(
                    type.Name,
                    type.FullName,
                    issues);

                if (type.IsEnum)
                {
                    string[] values = Enum.GetNames(type);
                    for (var index = 0; index < values.Length; index++)
                    {
                        ValidateForbiddenLevelName(
                            values[index],
                            $"{type.FullName}.{values[index]}",
                            issues);
                    }

                    continue;
                }

                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly);
                for (var index = 0; index < fields.Length; index++)
                {
                    FieldInfo field = fields[index];
                    if (!IsLevelContractField(field))
                    {
                        continue;
                    }

                    ValidateForbiddenLevelName(
                        field.Name,
                        $"{type.FullName}.{field.Name}",
                        issues);
                }

                MethodInfo[] methods = type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly);
                for (var index = 0; index < methods.Length; index++)
                {
                    MethodInfo method = methods[index];
                    if (method.IsSpecialName ||
                        !IsPublicOrProtected(method))
                    {
                        continue;
                    }

                    ValidateForbiddenLevelName(
                        method.Name,
                        $"{type.FullName}.{method.Name}",
                        issues);
                }

                PropertyInfo[] properties = type.GetProperties(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly);
                for (var index = 0; index < properties.Length; index++)
                {
                    PropertyInfo property = properties[index];
                    MethodInfo accessor =
                        property.GetMethod ?? property.SetMethod;
                    if (!IsPublicOrProtected(accessor))
                    {
                        continue;
                    }

                    ValidateForbiddenLevelName(
                        property.Name,
                        $"{type.FullName}.{property.Name}",
                        issues);
                }

                EventInfo[] events = type.GetEvents(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly);
                for (var index = 0; index < events.Length; index++)
                {
                    EventInfo eventInfo = events[index];
                    MethodInfo accessor =
                        eventInfo.AddMethod ?? eventInfo.RemoveMethod;
                    if (!IsPublicOrProtected(accessor))
                    {
                        continue;
                    }

                    ValidateForbiddenLevelName(
                        eventInfo.Name,
                        $"{type.FullName}.{eventInfo.Name}",
                        issues);
                }
            }

            return issues;
        }

        /// <summary>
        /// A controller satisfies the contract through either a direct public
        /// undo operation or explicit injection of the shared unbounded level
        /// history. Central history injection is preferred for input routers.
        /// </summary>
        public static IReadOnlyList<AntiAnxietyValidationIssue>
            ValidateUndoContracts(params Type[] controllerTypes)
        {
            var issues = new List<AntiAnxietyValidationIssue>();

            PropertyInfo canUndo =
                typeof(IUndoHistory).GetProperty(nameof(IUndoHistory.CanUndo));
            MethodInfo undo = typeof(IUndoHistory).GetMethod(
                nameof(IUndoHistory.Undo),
                Type.EmptyTypes);
            if (canUndo == null ||
                canUndo.PropertyType != typeof(bool) ||
                undo == null ||
                undo.ReturnType != typeof(bool))
            {
                issues.Add(
                    new AntiAnxietyValidationIssue(
                        MissingUndoContractCode,
                        typeof(IUndoHistory).FullName,
                        "The shared history must expose CanUndo and Undo()."));
                return issues;
            }

            if (controllerTypes == null)
            {
                return issues;
            }

            for (var index = 0; index < controllerTypes.Length; index++)
            {
                Type controllerType = controllerTypes[index];
                if (controllerType == null ||
                    HasDirectUndoMethod(controllerType) ||
                    HasUndoHistoryInjection(controllerType))
                {
                    continue;
                }

                issues.Add(
                    new AntiAnxietyValidationIssue(
                        MissingUndoContractCode,
                        controllerType?.FullName ?? "<null>",
                        "The interaction controller must expose a direct " +
                        "undo operation or accept IUndoHistory explicitly."));
            }

            return issues;
        }

        public static string FormatIssues(
            IReadOnlyList<AntiAnxietyValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return string.Empty;
            }

            var lines = new string[issues.Count + 1];
            lines[0] = "Calm Space anti-anxiety validation failed:";
            for (var index = 0; index < issues.Count; index++)
            {
                lines[index + 1] = issues[index].ToString();
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static void ValidateCleanerCoverageDefaults(
            ICollection<AntiAnxietyValidationIssue> issues)
        {
            ValidateComponentDefaults(issues);

            string[] prefabGuids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { LevelPrefabRoot });
            var cleanerCount = 0;
            for (var prefabIndex = 0;
                 prefabIndex < prefabGuids.Length;
                 prefabIndex++)
            {
                string path = AssetDatabase.GUIDToAssetPath(
                    prefabGuids[prefabIndex]);
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                RenderTextureCleaner[] cleaners =
                    prefab.GetComponentsInChildren<
                        RenderTextureCleaner>(true);
                for (var cleanerIndex = 0;
                     cleanerIndex < cleaners.Length;
                     cleanerIndex++)
                {
                    cleanerCount++;
                    ValidateCleanerCoverage(
                        cleaners[cleanerIndex],
                        $"{path}/{cleaners[cleanerIndex].name}",
                        issues);
                }
            }

            if (cleanerCount == 0)
            {
                issues.Add(
                    new AntiAnxietyValidationIssue(
                        CleanerCoverageCode,
                        LevelPrefabRoot,
                        "No authored cleaner was found to validate."));
            }
        }

        private static void ValidateComponentDefaults(
            ICollection<AntiAnxietyValidationIssue> issues)
        {
            var root = new GameObject(
                "Anti-Anxiety Cleaner Defaults")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            root.SetActive(false);

            try
            {
                RenderTextureCleaner cleaner =
                    root.AddComponent<RenderTextureCleaner>();
                ValidateCleanerCoverage(
                    cleaner,
                    "RenderTextureCleaner defaults",
                    issues);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateCleanerCoverage(
            RenderTextureCleaner cleaner,
            string context,
            ICollection<AntiAnxietyValidationIssue> issues)
        {
            if (cleaner == null)
            {
                issues.Add(
                    new AntiAnxietyValidationIssue(
                        CleanerCoverageCode,
                        context,
                        "Cleaner reference is missing."));
                return;
            }

            Vector2Int resolution = cleaner.CoverageResolution;
            if (resolution.x != RequiredCoverageWidth ||
                resolution.y != RequiredCoverageHeight)
            {
                issues.Add(
                    new AntiAnxietyValidationIssue(
                        CleanerCoverageCode,
                        context,
                        $"Coverage must be {RequiredCoverageWidth}x" +
                        $"{RequiredCoverageHeight}; found " +
                        $"{resolution.x}x{resolution.y}."));
            }

            if (cleaner.ProgressSampleFrameInterval <
                MinimumCoverageFrameInterval)
            {
                issues.Add(
                    new AntiAnxietyValidationIssue(
                        CleanerCoverageCode,
                        context,
                        "Coverage evaluation must skip at least " +
                        $"{MinimumCoverageFrameInterval - 1} frames."));
            }
        }

        private static IEnumerable<Type> GetRuntimeLevelContractTypes()
        {
            var types = new List<Type>
            {
                typeof(LevelType),
                typeof(LevelState),
                typeof(LevelDefinition),
                typeof(LevelBase)
            };

            Type[] runtimeTypes =
                GetExportedTypes(typeof(LevelBase).Assembly);
            for (var index = 0; index < runtimeTypes.Length; index++)
            {
                Type type = runtimeTypes[index];
                if (type != typeof(LevelBase) &&
                    typeof(LevelBase).IsAssignableFrom(type))
                {
                    types.Add(type);
                }
            }

            return types;
        }

        private static IEnumerable<Type> GetMonetizationApiTypes()
        {
            Type[] runtimeTypes = GetExportedTypes(
                typeof(IMonetizationManager).Assembly);
            var types = new List<Type>();
            for (var index = 0; index < runtimeTypes.Length; index++)
            {
                Type type = runtimeTypes[index];
                if (string.Equals(
                        type.Namespace,
                        typeof(IMonetizationManager).Namespace,
                        StringComparison.Ordinal))
                {
                    types.Add(type);
                }
            }

            return types;
        }

        private static Type[] GetExportedTypes(Assembly assembly)
        {
            if (assembly == null)
            {
                return Array.Empty<Type>();
            }

            try
            {
                return assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                var types = new List<Type>();
                Type[] loaded = exception.Types;
                for (var index = 0; index < loaded.Length; index++)
                {
                    if (loaded[index] != null)
                    {
                        types.Add(loaded[index]);
                    }
                }

                return types.ToArray();
            }
        }

        private static bool HasRewardedRequest(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            for (var index = 0; index < parameters.Length; index++)
            {
                Type parameterType = parameters[index].ParameterType;
                if (parameterType == typeof(RewardedAdRequest) ||
                    parameterType == typeof(ProviderRewardedAdRequest))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLevelContractField(FieldInfo field)
        {
            return
                field.IsPublic ||
                field.IsFamily ||
                field.IsFamilyOrAssembly ||
                field.GetCustomAttribute<SerializeField>() != null;
        }

        private static bool IsPublicOrProtected(MethodBase method)
        {
            return method != null &&
                   (method.IsPublic ||
                    method.IsFamily ||
                    method.IsFamilyOrAssembly);
        }

        private static bool HasDirectUndoMethod(Type controllerType)
        {
            MethodInfo[] methods = controllerType.GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly);
            for (var index = 0; index < methods.Length; index++)
            {
                if (methods[index].Name.StartsWith(
                        "Undo",
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasUndoHistoryInjection(Type controllerType)
        {
            MethodInfo[] methods = controllerType.GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly);
            for (var methodIndex = 0;
                 methodIndex < methods.Length;
                 methodIndex++)
            {
                ParameterInfo[] parameters =
                    methods[methodIndex].GetParameters();
                for (var parameterIndex = 0;
                     parameterIndex < parameters.Length;
                     parameterIndex++)
                {
                    if (parameters[parameterIndex].ParameterType ==
                        typeof(IUndoHistory))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ValidateForbiddenAdName(
            string name,
            string context,
            ICollection<AntiAnxietyValidationIssue> issues)
        {
            string normalized = NormalizeIdentifier(name);
            for (var index = 0;
                 index < ForbiddenAdContractFragments.Length;
                 index++)
            {
                if (!normalized.Contains(
                        ForbiddenAdContractFragments[index]))
                {
                    continue;
                }

                issues.Add(
                    new AntiAnxietyValidationIssue(
                        ForcedAdApiCode,
                        context,
                        "Interstitial and automatic ad formats are " +
                        "not permitted."));
                return;
            }
        }

        private static void ValidateForbiddenLevelName(
            string name,
            string context,
            ICollection<AntiAnxietyValidationIssue> issues)
        {
            string normalized = NormalizeIdentifier(name);
            for (var index = 0;
                 index < ForbiddenLevelContractFragments.Length;
                 index++)
            {
                if (!normalized.Contains(
                        ForbiddenLevelContractFragments[index]))
                {
                    continue;
                }

                issues.Add(
                    new AntiAnxietyValidationIssue(
                        NegativeLevelContractCode,
                        context,
                        "Countdown, lose, and fail contracts are not " +
                        "permitted in level authoring."));
                return;
            }
        }

        private static string NormalizeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var characters = new char[value.Length];
            var count = 0;
            for (var index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!char.IsLetterOrDigit(character))
                {
                    continue;
                }

                characters[count++] =
                    char.ToLowerInvariant(character);
            }

            return new string(characters, 0, count);
        }

        private static void AddRange(
            ICollection<AntiAnxietyValidationIssue> destination,
            IReadOnlyList<AntiAnxietyValidationIssue> source)
        {
            for (var index = 0; index < source.Count; index++)
            {
                destination.Add(source[index]);
            }
        }
    }

    /// <summary>
    /// Prevents a calm-design contract regression from reaching a player
    /// build even when the menu validator was not run manually.
    /// </summary>
    public sealed class AntiAnxietyBuildValidator :
        IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            IReadOnlyList<AntiAnxietyValidationIssue> issues =
                AntiAnxietyProjectValidator.ValidateProject();
            if (issues.Count == 0)
            {
                return;
            }

            throw new BuildFailedException(
                AntiAnxietyProjectValidator.FormatIssues(issues));
        }
    }
}
