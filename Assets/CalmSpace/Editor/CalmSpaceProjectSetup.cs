using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using CalmSpace.Audio;
using CalmSpace.Core;
using CalmSpace.Demo;
using CalmSpace.Haptics;
using CalmSpace.Input;
using CalmSpace.Levels;
using CalmSpace.UI;
using CalmSpace.Workshop;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CalmSpace.Editor
{
    /// <summary>
    /// Idempotently creates the mobile render setup, the single startup scene,
    /// and a small Addressable fitting level that exercises the real runtime.
    /// </summary>
    public static class CalmSpaceProjectSetup
    {
        public const string MainScenePath =
            "Assets/CalmSpace/Scenes/Main.unity";
        public const string DemoLevelPrefabPath =
            "Assets/CalmSpace/Content/Levels/DemoFitting.prefab";
        public const string LevelCatalogPath =
            "Assets/CalmSpace/Config/LevelCatalog.asset";
        public const string BuildOutputPath =
            "Builds/Android/CalmSpace-Tidy-Restore.aab";
        public const string ReleaseBuildOutputPath =
            "Builds/Android/CalmSpace-Tidy-Restore-release.aab";
        public const string TestApkOutputPath =
            "Builds/Android/CalmSpace-Demo-debug.apk";
        public const string VerificationApkOutputPath =
            "Builds/Android/CalmSpace-Release-verify.apk";

        private const string DemoLevelDefinitionPath =
            "Assets/CalmSpace/Config/DemoFitting.asset";
        private const string RendererDataPath =
            "Assets/CalmSpace/Rendering/CalmSpaceRenderer.asset";
        private const string PipelineAssetPath =
            "Assets/CalmSpace/Rendering/CalmSpaceMobileURP.asset";
        private const string AudioMixerPath =
            "Assets/CalmSpace/Audio/CalmSpaceAudio.mixer";
        private const string SnapClipPath =
            "Assets/CalmSpace/Audio/SnapSoft.wav";
        private const string AddressableAddress =
            "levels/demo-fitting";
        private const string AddressableLabel =
            "calm-space-level";
        /// <summary>
        /// Pinned Google Play target level. Google requires new uploads to
        /// target a recent API, and "Auto" would silently ship whatever SDK
        /// the build machine happens to have installed.
        /// </summary>
        private const AndroidSdkVersions TargetSdkVersion =
            AndroidSdkVersions.AndroidApiLevel36;

        private const string KeystorePathVariable =
            "CALMSPACE_KEYSTORE_PATH";
        private const string KeystorePassVariable =
            "CALMSPACE_KEYSTORE_PASS";
        private const string KeyAliasVariable =
            "CALMSPACE_KEY_ALIAS";
        private const string KeyAliasPassVariable =
            "CALMSPACE_KEY_ALIAS_PASS";
        private const string VersionCodeVariable =
            "CALMSPACE_VERSION_CODE";
        private const string VersionNameVariable =
            "CALMSPACE_VERSION_NAME";

        private const string GeneratedSceneMarkerPrefix =
            "CalmSpace Generated Scene · ";
        private static readonly string[] GeneratedSceneSourcePaths =
        {
            "Assets/CalmSpace/Editor/CalmSpaceWorkshopSceneBuilder.cs",
            "Assets/CalmSpace/Editor/CalmSpaceDemoSceneBuilder.cs",
            "Assets/CalmSpace/Editor/CalmSpaceProjectSetup.cs",
            "Assets/CalmSpace/Runtime/UI/Workshop/WorkshopBottomSheet.cs",
            "Assets/CalmSpace/Runtime/UI/Workshop/WorkshopHomeView.cs",
            "Assets/CalmSpace/Runtime/UI/Workshop/WorkshopHomeController.cs",
            "Assets/CalmSpace/Runtime/UI/Workshop/WorkshopRoomPresenter.cs",
            "Assets/CalmSpace/Runtime/UI/DemoExperienceController.cs",
            "Assets/CalmSpace/Runtime/Audio/IAsmrAudioService.cs",
            "Assets/CalmSpace/Runtime/Audio/AsmrAudioService.cs",
            "Assets/CalmSpace/Runtime/Composition/CalmSpaceLifetimeScope.cs",
            "Assets/CalmSpace/Runtime/Workshop/WorkshopRuntimeAvailability.cs",
            "Assets/CalmSpace/Runtime/Workshop/AddressableWorkshopRoomLoader.cs",
            "Assets/CalmSpace/Editor/CalmSpaceWorkshopAssetBuilder.cs",
            "Assets/CalmSpace/Editor/CalmSpaceTactileAudioBuilder.cs"
        };

        private static readonly Color BackgroundColor =
            new Color(0.075f, 0.09f, 0.12f, 1f);
        private static readonly Color PlatformColor =
            new Color(0.76f, 0.73f, 0.66f, 1f);
        private static readonly Color[] ItemColors =
        {
            new Color(0.92f, 0.42f, 0.34f, 1f),
            new Color(0.30f, 0.72f, 0.61f, 1f),
            new Color(0.95f, 0.69f, 0.28f, 1f)
        };

        [MenuItem("Calm Space/Configure Project")]
        public static void ConfigureProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Exit Play Mode before configuring Calm Space.");
            }

            EnsureFolders();
            Texture2D appIcon =
                CalmSpaceDemoAssetBuilder.ImportAppIcon();
            ConfigurePlayer(appIcon);

            UniversalRenderPipelineAsset pipelineAsset =
                ConfigureRenderPipeline();
            Material platformMaterial = CreateOrUpdateMaterial(
                "Assets/CalmSpace/Materials/Platform.mat",
                PlatformColor,
                0.28f);
            Material[] itemMaterials = CreateItemMaterials();
            Material[] targetMaterials = CreateTargetMaterials();

            AudioClip snapClip = CreateOrLoadSnapClip();
            CalmSpaceDemoAssetBuilder.ConfigureSnapImport(
                SnapClipPath);
            AudioClip ambientLoop =
                CalmSpaceDemoAssetBuilder.CreateOrLoadAmbientLoop();
            Sprite menuBackground =
                CalmSpaceDemoAssetBuilder.ImportMenuBackground();
            Sprite roundedSprite =
                CalmSpaceDemoAssetBuilder.CreateOrLoadRoundedSprite();
            AudioMixerGroup mixerGroup = CreateOrLoadAudioMixerGroup();
            LevelCatalog catalog =
                CalmSpaceDemoLevelBuilder.CreateOrUpdate(
                itemMaterials,
                targetMaterials);
            LivingWorkshopCatalog workshopCatalog =
                CalmSpaceWorkshopCatalogBuilder.CreateOrUpdate();
            WorkshopTextCatalog workshopTextCatalog =
                CalmSpaceWorkshopTextBuilder.CreateOrUpdate();
            CalmSpaceWorkshopAssetBuilder.CreateOrUpdate();

            CreateOrUpdateMainScene(
                catalog,
                platformMaterial,
                snapClip,
                ambientLoop,
                mixerGroup,
                menuBackground,
                roundedSprite,
                workshopCatalog,
                workshopTextCatalog);
            EnsureCleaningShadersAreIncluded();

            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "CALMSPACE_SETUP_COMPLETE " +
                MainScenePath +
                " " +
                AddressableAddress);
        }

        [MenuItem("Calm Space/Development/Build Android Test App Bundle")]
        public static void BuildAndroidAppBundle()
        {
            BuildAndroidPlayer(
                BuildOutputPath,
                requireReleaseSigning: false,
                buildAppBundle: true);
        }

        [MenuItem("Calm Space/Development/Build Android Test APK")]
        public static void BuildAndroidTestApk()
        {
            BuildAndroidPlayer(
                TestApkOutputPath,
                requireReleaseSigning: false,
                buildAppBundle: false);
        }

        [MenuItem("Calm Space/Release/Build Google Play App Bundle")]
        public static void BuildGooglePlayAppBundle()
        {
            BuildAndroidPlayer(
                ReleaseBuildOutputPath,
                requireReleaseSigning: true,
                buildAppBundle: true);
        }

        /// <summary>
        /// A release-signed APK carrying exactly the settings of the uploaded
        /// bundle. An app bundle cannot be installed directly, so this is how
        /// release managed stripping and IL2CPP get verified on a device
        /// before the bundle goes to Google Play.
        /// </summary>
        [MenuItem("Calm Space/Release/Build Signed Verification APK")]
        public static void BuildSignedVerificationApk()
        {
            BuildAndroidPlayer(
                VerificationApkOutputPath,
                requireReleaseSigning: true,
                buildAppBundle: false);
        }

        private static void BuildAndroidPlayer(
            string buildOutputPath,
            bool requireReleaseSigning,
            bool buildAppBundle)
        {
            if (EditorUserBuildSettings.activeBuildTarget !=
                BuildTarget.Android)
            {
                throw new BuildFailedException(
                    "Run this method with Android as the active build target.");
            }

            using (var signingCleanup = new AndroidSigningCleanupScope())
            {
                ConfigureProject();
                ApplyVersionOverrides();
                if (requireReleaseSigning)
                {
                    // Arm before applying anything so even a partial setter
                    // failure cannot leave release state serialized.
                    signingCleanup.Arm();
                    ApplyReleaseSigningFromEnvironment();
                    ValidateGooglePlayReleaseSettings();
                }

                EditorUserBuildSettings.buildAppBundle = buildAppBundle;

                AddressableAssetSettings.BuildPlayerContent(
                    out AddressablesPlayerBuildResult addressablesResult);
                if (!string.IsNullOrEmpty(addressablesResult.Error))
                {
                    throw new BuildFailedException(
                        "Addressables build failed: " +
                        addressablesResult.Error);
                }

                string outputPath = Path.GetFullPath(buildOutputPath);
                string outputDirectory = Path.GetDirectoryName(outputPath);
                if (string.IsNullOrEmpty(outputDirectory))
                {
                    throw new BuildFailedException(
                        "Could not resolve the Android build directory.");
                }

                Directory.CreateDirectory(outputDirectory);

                BuildOptions buildOptions = BuildOptions.CompressWithLz4HC;
                if (!requireReleaseSigning)
                {
                    buildOptions |= BuildOptions.Development;
                }

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { MainScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = buildOptions
                };

                // Debug builds historically clear custom signing only once
                // they reach the player build. Preserve that behavior while
                // release builds arm immediately before signing is applied.
                signingCleanup.Arm();
                BuildReport report = BuildPipeline.BuildPlayer(options);

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        $"Android build ended with {report.summary.result}. " +
                        $"Errors: {report.summary.totalErrors}.");
                }

                Debug.Log(
                    $"CALMSPACE_ANDROID_BUILD_COMPLETE {outputPath} " +
                    $"{report.summary.totalSize} bytes " +
                    $"versionCode={PlayerSettings.Android.bundleVersionCode} " +
                    $"versionName={PlayerSettings.bundleVersion} " +
                    $"targetSdk={PlayerSettings.Android.targetSdkVersion}");
            }
        }

        /// <summary>
        /// Version name and code come from the environment so a release can be
        /// re-cut without editing tracked project settings. Play rejects an
        /// upload whose version code is not higher than the previous one.
        /// </summary>
        private static void ApplyVersionOverrides()
        {
            string versionName = Environment.GetEnvironmentVariable(
                VersionNameVariable);
            string normalizedVersionName =
                string.IsNullOrWhiteSpace(versionName)
                    ? null
                    : versionName.Trim();

            string versionCode = Environment.GetEnvironmentVariable(
                VersionCodeVariable);
            int? parsedVersionCode = null;
            if (!string.IsNullOrWhiteSpace(versionCode))
            {
                if (!int.TryParse(
                        versionCode.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int parsed) ||
                    parsed < 1)
                {
                    throw new BuildFailedException(
                        VersionCodeVariable +
                        " must be a positive integer.");
                }

                parsedVersionCode = parsed;
            }

            if (normalizedVersionName != null)
            {
                PlayerSettings.bundleVersion = normalizedVersionName;
            }

            if (parsedVersionCode.HasValue)
            {
                PlayerSettings.Android.bundleVersionCode =
                    parsedVersionCode.Value;
            }
        }

        /// <summary>
        /// Reads the upload key from the environment. Secrets are never stored
        /// in the repository and are cleared again once the build finishes.
        /// </summary>
        private static void ApplyReleaseSigningFromEnvironment()
        {
            string keystorePath = Environment.GetEnvironmentVariable(
                KeystorePathVariable);
            string keystorePass = Environment.GetEnvironmentVariable(
                KeystorePassVariable);
            string keyAlias = Environment.GetEnvironmentVariable(
                KeyAliasVariable);
            string keyAliasPass = Environment.GetEnvironmentVariable(
                KeyAliasPassVariable);

            if (string.IsNullOrWhiteSpace(keystorePath) ||
                string.IsNullOrWhiteSpace(keystorePass) ||
                string.IsNullOrWhiteSpace(keyAlias) ||
                string.IsNullOrWhiteSpace(keyAliasPass))
            {
                throw new BuildFailedException(
                    "Google Play release blocked: set " +
                    KeystorePathVariable + ", " + KeystorePassVariable + ", " +
                    KeyAliasVariable + " and " + KeyAliasPassVariable +
                    " before building. Create the upload key with keytool; " +
                    "it is never stored in this repository.");
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName =
                Path.GetFullPath(keystorePath.Trim());
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = keyAlias.Trim();
            PlayerSettings.Android.keyaliasPass = keyAliasPass;
        }

        /// <summary>
        /// Signing configuration belongs to the build machine, not the
        /// repository: passwords must never be serialized, and the keystore
        /// path and alias would otherwise be committed and break every other
        /// machine.
        /// </summary>
        private static void ClearSigningSecrets()
        {
            PlayerSettings.Android.keystorePass = string.Empty;
            PlayerSettings.Android.keyaliasPass = string.Empty;
            PlayerSettings.Android.keystoreName = string.Empty;
            PlayerSettings.Android.keyaliasName = string.Empty;
            PlayerSettings.Android.useCustomKeystore = false;
        }

        internal sealed class AndroidSigningCleanupScope : IDisposable
        {
            private readonly string _bundleVersion =
                PlayerSettings.bundleVersion;
            private readonly int _bundleVersionCode =
                PlayerSettings.Android.bundleVersionCode;
            private bool _armed;
            private bool _disposed;

            internal void Arm()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(
                        nameof(AndroidSigningCleanupScope));
                }

                _armed = true;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                try
                {
                    if (_armed)
                    {
                        ClearSigningSecrets();
                    }
                }
                finally
                {
                    try
                    {
                        PlayerSettings.bundleVersion = _bundleVersion;
                    }
                    finally
                    {
                        PlayerSettings.Android.bundleVersionCode =
                            _bundleVersionCode;
                    }
                }
            }
        }

        private static void ValidateGooglePlayReleaseSettings()
        {
            if (!PlayerSettings.Android.useCustomKeystore)
            {
                throw new BuildFailedException(
                    "Google Play release blocked: enable a custom Android " +
                    "keystore. Debug-signed bundles cannot be uploaded.");
            }

            string keystoreName = PlayerSettings.Android.keystoreName;
            if (string.IsNullOrWhiteSpace(keystoreName))
            {
                throw new BuildFailedException(
                    "Google Play release blocked: select an upload keystore.");
            }

            string keystorePath = Path.GetFullPath(keystoreName);
            if (!File.Exists(keystorePath))
            {
                throw new BuildFailedException(
                    "Google Play release blocked: the configured upload " +
                    "keystore file does not exist.");
            }

            if (string.Equals(
                    Path.GetFileName(keystorePath),
                    "debug.keystore",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new BuildFailedException(
                    "Google Play release blocked: the Android debug keystore " +
                    "cannot be used as an upload key.");
            }

            string keyAlias = PlayerSettings.Android.keyaliasName;
            if (string.IsNullOrWhiteSpace(keyAlias) ||
                string.Equals(
                    keyAlias,
                    "androiddebugkey",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new BuildFailedException(
                    "Google Play release blocked: select a non-debug upload " +
                    "key alias.");
            }

            if (string.IsNullOrEmpty(
                    PlayerSettings.Android.keystorePass) ||
                string.IsNullOrEmpty(
                    PlayerSettings.Android.keyaliasPass))
            {
                throw new BuildFailedException(
                    "Google Play release blocked: enter the keystore and key " +
                    "passwords for this Editor session.");
            }

            if ((PlayerSettings.Android.targetArchitectures &
                 AndroidArchitecture.ARM64) == 0)
            {
                throw new BuildFailedException(
                    "Google Play release blocked: ARM64 must be enabled.");
            }

            if (PlayerSettings.GetScriptingBackend(
                    NamedBuildTarget.Android) !=
                ScriptingImplementation.IL2CPP)
            {
                throw new BuildFailedException(
                    "Google Play release blocked: Android must use IL2CPP.");
            }

            if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion) ||
                PlayerSettings.Android.bundleVersionCode < 1)
            {
                throw new BuildFailedException(
                    "Google Play release blocked: configure a valid version " +
                    "name and positive version code.");
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/CalmSpace/Content");
            EnsureFolder("Assets/CalmSpace/Content/Levels");
            EnsureFolder("Assets/CalmSpace/Config");
            EnsureFolder("Assets/CalmSpace/Materials");
            EnsureFolder("Assets/CalmSpace/Audio");
            EnsureFolder("Assets/CalmSpace/Rendering");
            EnsureFolder("Assets/CalmSpace/Scenes");
            EnsureFolder("Assets/CalmSpace/UI");
            EnsureFolder("Assets/CalmSpace/UI/Art");
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            int separator = assetPath.LastIndexOf('/');
            if (separator <= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid asset folder path '{assetPath}'.");
            }

            string parent = assetPath.Substring(0, separator);
            string folder = assetPath.Substring(separator + 1);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folder);
        }

        private static void ConfigurePlayer(Texture2D appIcon)
        {
            PlayerSettings.companyName = "Calm Space Studio";
            PlayerSettings.productName = "Calm Space: Tidy & Restore";
            if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))
            {
                PlayerSettings.bundleVersion = "1.0.0";
            }

            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android,
                "com.calmspace.tidyrestore");
            if (PlayerSettings.Android.bundleVersionCode < 1)
            {
                PlayerSettings.Android.bundleVersionCode = 1;
            }
            PlayerSettings.Android.minSdkVersion =
                AndroidSdkVersions.AndroidApiLevel24;
            // Pinned, not Auto: "highest installed" makes the shipped API
            // level depend on the build machine, and Google Play rejects a
            // bundle that targets an API level below the current floor.
            PlayerSettings.Android.targetSdkVersion =
                TargetSdkVersion;
            PlayerSettings.Android.targetArchitectures =
                AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android,
                ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(
                NamedBuildTarget.Android,
                ManagedStrippingLevel.Medium);
            PlayerSettings.gcIncremental = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation =
                UIOrientation.Portrait;
            PlayerSettings.use32BitDisplayBuffer = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(
                BuildTarget.Android,
                false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                new[] { GraphicsDeviceType.OpenGLES3 });
            ConfigureAndroidIcons(appIcon);

            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.development = false;
        }

        private static void ConfigureAndroidIcons(Texture2D appIcon)
        {
            if (appIcon == null)
            {
                throw new ArgumentNullException(nameof(appIcon));
            }

            PlatformIconKind[] kinds =
                PlayerSettings.GetSupportedIconKinds(
                    NamedBuildTarget.Android);
            for (var kindIndex = 0;
                 kindIndex < kinds.Length;
                 kindIndex++)
            {
                PlatformIconKind kind = kinds[kindIndex];
                PlatformIcon[] icons =
                    PlayerSettings.GetPlatformIcons(
                        NamedBuildTarget.Android,
                        kind);
                for (var iconIndex = 0;
                     iconIndex < icons.Length;
                     iconIndex++)
                {
                    PlatformIcon icon = icons[iconIndex];
                    var layers =
                        new Texture2D[icon.maxLayerCount];
                    for (var layerIndex = 0;
                         layerIndex < layers.Length;
                         layerIndex++)
                    {
                        layers[layerIndex] = appIcon;
                    }

                    icon.SetTextures(layers);
                }

                PlayerSettings.SetPlatformIcons(
                    NamedBuildTarget.Android,
                    kind,
                    icons);
            }
        }

        private static UniversalRenderPipelineAsset
            ConfigureRenderPipeline()
        {
            UniversalRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                    RendererDataPath);
            if (rendererData == null)
            {
                rendererData =
                    ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(
                    rendererData,
                    RendererDataPath);
            }

            rendererData.intermediateTextureMode =
                IntermediateTextureMode.Auto;

            UniversalRenderPipelineAsset pipelineAsset =
                AssetDatabase.LoadAssetAtPath<
                    UniversalRenderPipelineAsset>(
                    PipelineAssetPath);
            if (pipelineAsset == null)
            {
                pipelineAsset =
                    UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(
                    pipelineAsset,
                    PipelineAssetPath);
            }

            pipelineAsset.supportsCameraDepthTexture = false;
            pipelineAsset.supportsCameraOpaqueTexture = false;
            pipelineAsset.supportsHDR = false;
            pipelineAsset.msaaSampleCount = 2;
            pipelineAsset.renderScale = 1f;
            ConfigurePipelineLighting(pipelineAsset);
            pipelineAsset.shadowCascadeCount = 1;
            pipelineAsset.shadowDistance = 20f;

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;
            QualitySettings.antiAliasing = 2;
            QualitySettings.vSyncCount = 0;

            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(pipelineAsset);
            return pipelineAsset;
        }

        private static void ConfigurePipelineLighting(
            UniversalRenderPipelineAsset pipelineAsset)
        {
            var serialized = new SerializedObject(pipelineAsset);
            serialized.FindProperty("m_MainLightRenderingMode")
                .intValue = (int)LightRenderingMode.PerPixel;
            serialized.FindProperty("m_AdditionalLightsRenderingMode")
                .intValue = (int)LightRenderingMode.PerVertex;
            serialized.FindProperty("m_MainLightShadowsSupported")
                .boolValue = true;
            serialized.FindProperty("m_AdditionalLightShadowsSupported")
                .boolValue = false;
            SerializedProperty anyShadows =
                serialized.FindProperty("m_AnyShadowsSupported");
            if (anyShadows != null)
            {
                anyShadows.boolValue = true;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material[] CreateItemMaterials()
        {
            var materials = new Material[ItemColors.Length];
            for (int index = 0; index < ItemColors.Length; index++)
            {
                materials[index] = CreateOrUpdateMaterial(
                    $"Assets/CalmSpace/Materials/Item{index + 1}.mat",
                    ItemColors[index],
                    0.48f);
            }

            return materials;
        }

        private static Material[] CreateTargetMaterials()
        {
            var materials = new Material[ItemColors.Length];
            for (int index = 0; index < ItemColors.Length; index++)
            {
                Color color = Color.Lerp(
                    ItemColors[index],
                    PlatformColor,
                    0.58f);
                materials[index] = CreateOrUpdateMaterial(
                    $"Assets/CalmSpace/Materials/Target{index + 1}.mat",
                    color,
                    0.16f);
            }

            return materials;
        }

        private static Material CreateOrUpdateMaterial(
            string path,
            Color color,
            float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "URP Lit shader is unavailable.");
            }

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.name = Path.GetFileNameWithoutExtension(path);
            material.enableInstancing = true;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static LevelDefinition
            CreateOrUpdateDemoLevelDefinition()
        {
            LevelDefinition definition =
                AssetDatabase.LoadAssetAtPath<LevelDefinition>(
                    DemoLevelDefinitionPath);
            if (definition == null)
            {
                definition =
                    ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(
                    definition,
                    DemoLevelDefinitionPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_levelId").stringValue =
                "demo-fitting";
            serialized.FindProperty("_displayName").stringValue =
                "First Restore";
            serialized.FindProperty("_levelType").enumValueIndex =
                (int)LevelType.Fitting;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static GameObject CreateOrUpdateDemoLevelPrefab(
            LevelDefinition definition,
            Material[] itemMaterials,
            Material[] targetMaterials)
        {
            var root = new GameObject("Demo Fitting Level");
            try
            {
                FittingLevel level = root.AddComponent<FittingLevel>();
                SetObjectReference(level, "_definition", definition);

                for (int index = 0; index < ItemColors.Length; index++)
                {
                    float x = (index - 1) * 1.55f;
                    Transform target = CreateTarget(
                        root.transform,
                        index,
                        new Vector3(x, 0.46f, 1.35f),
                        targetMaterials[index]);
                    CreateDraggableItem(
                        root.transform,
                        target,
                        index,
                        new Vector3(x, 0.46f, -1.45f),
                        itemMaterials[index]);
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    DemoLevelPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        "Unity did not save the demo level prefab.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Transform CreateTarget(
            Transform parent,
            int index,
            Vector3 position,
            Material material)
        {
            var targetRoot = new GameObject(
                $"Target {index + 1}");
            targetRoot.transform.SetParent(parent, false);
            targetRoot.transform.localPosition = position;

            GameObject plate = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            plate.name = "Socket";
            plate.transform.SetParent(targetRoot.transform, false);
            plate.transform.localPosition =
                new Vector3(0f, -0.35f, 0f);
            plate.transform.localScale =
                new Vector3(1.04f, 0.12f, 1.04f);
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.GetComponent<MeshRenderer>().sharedMaterial = material;

            return targetRoot.transform;
        }

        private static void CreateDraggableItem(
            Transform parent,
            Transform target,
            int index,
            Vector3 position,
            Material material)
        {
            GameObject item = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            item.name = $"Restore Piece {index + 1}";
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale =
                new Vector3(0.9f, 0.42f, 0.9f);
            item.GetComponent<MeshRenderer>().sharedMaterial = material;

            ItemSnapController snap =
                item.AddComponent<ItemSnapController>();
            var serialized = new SerializedObject(snap);
            serialized.FindProperty("_snapTarget").objectReferenceValue =
                target;
            serialized.FindProperty(
                    "_positionSnapThreshold")
                .floatValue = 0.78f;
            serialized.FindProperty(
                    "_rotationSnapThresholdDegrees")
                .floatValue = 180f;
            serialized.FindProperty("_snapDurationSeconds").floatValue =
                0.16f;
            serialized.FindProperty("_returnDurationSeconds").floatValue =
                0.14f;
            serialized.FindProperty("_dragHapticIntensity").floatValue =
                0.55f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string RegisterAddressableLevel(
            GameObject levelPrefab)
        {
            if (levelPrefab == null)
            {
                throw new ArgumentNullException(nameof(levelPrefab));
            }

            string guid = AssetDatabase.AssetPathToGUID(
                DemoLevelPrefabPath);
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException(
                    "The demo level prefab has no asset GUID.");
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
            entry.address = AddressableAddress;
            entry.SetLabel(AddressableLabel, true, true, false);
            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.EntryModified,
                entry,
                true);
            EditorUtility.SetDirty(settings);
            return guid;
        }

        private static LevelCatalog CreateOrUpdateCatalog(
            LevelDefinition definition,
            string prefabGuid)
        {
            LevelCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                    LevelCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, LevelCatalogPath);
            }

            var entry = new LevelCatalogEntry();
            SetPrivateField(entry, "_definition", definition);
            SetPrivateField(
                entry,
                "_prefab",
                new AssetReferenceGameObject(prefabGuid));
            SetPrivateField(
                catalog,
                "_levels",
                new[] { entry });
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void CreateOrUpdateMainScene(
            LevelCatalog catalog,
            Material platformMaterial,
            AudioClip snapClip,
            AudioClip ambientLoop,
            AudioMixerGroup mixerGroup,
            Sprite menuBackground,
            Sprite roundedSprite,
            LivingWorkshopCatalog workshopCatalog,
            WorkshopTextCatalog workshopTextCatalog)
        {
            CalmSpaceTactileAudioBuilder.GeneratedAudioPalette audioPalette =
                CalmSpaceTactileAudioBuilder.CreateOrUpdatePalette();
            string generatedSceneMarker = GetGeneratedSceneMarker();
            if (TryReuseCurrentGeneratedScene(generatedSceneMarker))
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            scene.name = "Main";
            new GameObject(generatedSceneMarker);

            Camera camera = CreateCamera();
            CreateLighting();
            Renderer[] boardRenderers =
                CreateEnvironment(platformMaterial);
            DemoRoomPresenter roomPresenter =
                CalmSpaceDemoSceneBuilder.CreateHomeRoom();

            var levelRootObject = new GameObject("Level Root");
            Transform levelRoot = levelRootObject.transform;

            var services = new GameObject("App Services");
            AsmrAudioService audioService =
                services.AddComponent<AsmrAudioService>();
            BackgroundMusicController musicController =
                services.AddComponent<BackgroundMusicController>();
            services.AddComponent<HapticLifecycleRelay>();
            CalmSpaceLifetimeScope lifetimeScope =
                services.AddComponent<CalmSpaceLifetimeScope>();

            var audioSerialized = new SerializedObject(audioService);

            // The pooled service picks a clip at random per placement, so the
            // whole tactile palette goes in: one repeated click is what makes
            // a tidying game feel mechanical.
            var snapClips = new List<AudioClip>();
            if (snapClip != null)
            {
                snapClips.Add(snapClip);
            }

            foreach (AudioClip tactile in audioPalette.Placement)
            {
                if (tactile != null)
                {
                    snapClips.Add(tactile);
                }
            }

            SetAudioClipBank(audioSerialized, "_snapClips", snapClips);
            SetAudioClipBank(
                audioSerialized, "_screwTurnClips", audioPalette.ScrewTurn);
            SetAudioClipBank(
                audioSerialized, "_screwReleaseClips", audioPalette.ScrewRelease);
            SetAudioClipBank(
                audioSerialized, "_cleaningClothClips", audioPalette.CleaningCloth);
            SetAudioClipBank(
                audioSerialized, "_cleaningSpongeClips", audioPalette.CleaningSponge);
            SetAudioClipBank(
                audioSerialized,
                "_cleaningSqueegeeClips",
                audioPalette.CleaningSqueegee);
            SetAudioClipBank(
                audioSerialized, "_levelCompleteClips", audioPalette.LevelComplete);
            SetAudioClipBank(
                audioSerialized, "_roomRevealClips", audioPalette.RoomReveal);
            SetAudioClipBank(
                audioSerialized, "_uiTapClips", audioPalette.UiTap);

            audioSerialized.FindProperty("_outputMixerGroup")
                .objectReferenceValue = mixerGroup;
            audioSerialized.ApplyModifiedPropertiesWithoutUndo();

            var musicSerialized =
                new SerializedObject(musicController);
            musicSerialized.FindProperty("_musicClip")
                .objectReferenceValue = ambientLoop;
            musicSerialized.FindProperty("_outputMixerGroup")
                .objectReferenceValue = mixerGroup;
            musicSerialized.FindProperty("_baseVolume").floatValue =
                0.24f;
            musicSerialized.FindProperty("_fadeDuration").floatValue =
                0.32f;
            musicSerialized.ApplyModifiedPropertiesWithoutUndo();

            DemoThemeCatalog themeCatalog =
                CalmSpaceDemoSceneBuilder
                    .CreateOrUpdateThemeCatalog();
            DemoDecorationCatalog decorationCatalog =
                CalmSpaceDemoSceneBuilder
                    .CreateOrUpdateDecorationCatalog();
            SetObjectReference(
                lifetimeScope,
                "_levelCatalog",
                catalog);
            SetObjectReference(
                lifetimeScope,
                "_levelRoot",
                levelRoot);
            SetObjectReference(
                lifetimeScope,
                "_demoThemeCatalog",
                themeCatalog);
            SetObjectReference(
                lifetimeScope,
                "_demoDecorationCatalog",
                decorationCatalog);
            SetObjectReference(
                lifetimeScope,
                "_livingWorkshopCatalog",
                workshopCatalog);
            SetObjectReference(
                lifetimeScope,
                "_workshopTextCatalog",
                workshopTextCatalog);

            var inputObject = new GameObject("Input");
            DragInputRouter router =
                inputObject.AddComponent<DragInputRouter>();
            SetObjectReference(router, "_camera", camera);

            CalmSpaceDemoSceneBuilder.CreateDemoUi(
                services.transform,
                camera,
                boardRenderers,
                roomPresenter,
                menuBackground,
                roundedSprite,
                catalog.Count);

            if (!EditorSceneManager.SaveScene(scene, MainScenePath))
            {
                throw new InvalidOperationException(
                    "Unity could not save the startup scene.");
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };
        }

        private static bool TryReuseCurrentGeneratedScene(
            string expectedMarker)
        {
            if (!File.Exists(Path.GetFullPath(MainScenePath)))
            {
                return false;
            }

            Scene scene = EditorSceneManager.OpenScene(
                MainScenePath,
                OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            for (var index = 0; index < roots.Length; index++)
            {
                if (!string.Equals(
                        roots[index].name,
                        expectedMarker,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!ValidateGeneratedWorkshopScene(scene))
                {
                    return false;
                }

                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(MainScenePath, true)
                };
                return true;
            }

            return false;
        }

        internal static bool ValidateGeneratedWorkshopScene(Scene scene)
        {
            WorkshopHomeController home =
                Object.FindFirstObjectByType<WorkshopHomeController>(
                    FindObjectsInactive.Include);
            DemoExperienceController experience =
                Object.FindFirstObjectByType<DemoExperienceController>(
                    FindObjectsInactive.Include);
            CalmSpaceLifetimeScope scope =
                Object.FindFirstObjectByType<CalmSpaceLifetimeScope>(
                    FindObjectsInactive.Include);
            AsmrAudioService audio =
                Object.FindFirstObjectByType<AsmrAudioService>(
                    FindObjectsInactive.Include);
            if (home == null || experience == null || scope == null ||
                audio == null ||
                !HasObjectReferences(home, "_view", "_roomParent"))
            {
                return false;
            }

            WorkshopHomeView view = home.View;
            if (!(home is IWorkshopHomeRecovery) ||
                !HasObjectReferences(
                    view,
                    "_primaryButton",
                    "_catalogButton",
                    "_settingsButton",
                    "_hotspotButton",
                    "_albumRoot",
                    "_decorRoot",
                    "_dailyCareRoot",
                    "_relaxPassRoot",
                    "_primaryLabel",
                    "_taskTitle",
                    "_progressLabel",
                    "_catalogLabel",
                    "_bottomSheet",
                    "_settingsMusicButton",
                    "_settingsLocaleButton",
                    "_settingsHapticButton",
                    "_settingsMusicLabel",
                    "_settingsLocaleLabel",
                    "_settingsHapticLabel"))
            {
                return false;
            }

            WorkshopBottomSheet bottomSheet = view.BottomSheet;
            if (!HasObjectReferences(
                    bottomSheet,
                    "_sheet",
                    "_closeButton",
                    "_actionButton",
                    "_settingsContent",
                    "_recoveryContent",
                    "_recoveryTitleLabel",
                    "_actionLabel"))
            {
                return false;
            }

            SerializedObject scopeData = new SerializedObject(scope);
            if (scopeData.FindProperty("_livingWorkshopCatalog")
                    ?.objectReferenceValue == null ||
                scopeData.FindProperty("_workshopTextCatalog")
                    ?.objectReferenceValue == null)
            {
                return false;
            }

            var audioData = new SerializedObject(audio);
            foreach (string field in new[]
                     {
                         "_snapClips",
                         "_screwTurnClips",
                         "_screwReleaseClips",
                         "_cleaningClothClips",
                         "_cleaningSpongeClips",
                         "_cleaningSqueegeeClips",
                         "_levelCompleteClips",
                         "_roomRevealClips",
                         "_uiTapClips"
                     })
            {
                SerializedProperty bank = audioData.FindProperty(field);
                if (bank == null || bank.arraySize == 0)
                {
                    return false;
                }

                for (var index = 0; index < bank.arraySize; index++)
                {
                    if (bank.GetArrayElementAtIndex(index)
                            .objectReferenceValue == null)
                    {
                        return false;
                    }
                }
            }

            DemoRoomPresenter room =
                Object.FindFirstObjectByType<DemoRoomPresenter>(
                    FindObjectsInactive.Include);
            return room != null && room.RoomRoot != null &&
                room.RoomRoot.GetComponentsInChildren<Graphic>(true).Length >= 4;
        }

        private static bool HasObjectReferences(
            Object target,
            params string[] fieldNames)
        {
            if (target == null)
            {
                return false;
            }

            var serialized = new SerializedObject(target);
            for (var index = 0; index < fieldNames.Length; index++)
            {
                SerializedProperty field =
                    serialized.FindProperty(fieldNames[index]);
                if (field == null ||
                    field.propertyType !=
                    SerializedPropertyType.ObjectReference ||
                    field.objectReferenceValue == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void SetAudioClipBank(
            SerializedObject serialized,
            string fieldName,
            IReadOnlyList<AudioClip> clips)
        {
            SerializedProperty bank = serialized.FindProperty(fieldName);
            if (bank == null)
            {
                throw new InvalidOperationException(
                    "Missing ASMR audio bank " + fieldName + ".");
            }

            int count = clips?.Count ?? 0;
            bank.arraySize = count;
            for (var index = 0; index < count; index++)
            {
                bank.GetArrayElementAtIndex(index).objectReferenceValue =
                    clips[index];
            }
        }

        private static string GetGeneratedSceneMarker()
        {
            using (SHA256 sha = SHA256.Create())
            using (var stream = new MemoryStream())
            {
                for (var index = 0;
                     index < GeneratedSceneSourcePaths.Length;
                     index++)
                {
                    string path = Path.GetFullPath(
                        GeneratedSceneSourcePaths[index]);
                    if (!File.Exists(path))
                    {
                        throw new InvalidOperationException(
                            "Generated scene source is missing: " + path);
                    }

                    byte[] bytes = File.ReadAllBytes(path);
                    stream.Write(bytes, 0, bytes.Length);
                }

                stream.Position = 0;
                byte[] hash = sha.ComputeHash(stream);
                string fingerprint = BitConverter.ToString(hash)
                    .Replace("-", string.Empty)
                    .Substring(0, 16);
                return GeneratedSceneMarkerPrefix + fingerprint;
            }
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();

            camera.transform.position =
                new Vector3(0f, 7.4f, -7.2f);
            camera.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 0.4f, 0.1f) -
                camera.transform.position,
                Vector3.up);
            camera.fieldOfView = 44f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            return camera;
        }

        private static void CreateLighting()
        {
            var lightObject = new GameObject("Key Light");
            Light keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.94f, 0.84f, 1f);
            keyLight.intensity = 1.25f;
            keyLight.shadows = LightShadows.Soft;
            lightObject.transform.rotation =
                Quaternion.Euler(48f, -28f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight =
                new Color(0.34f, 0.37f, 0.42f, 1f);
            RenderSettings.fog = false;
        }

        private static Renderer[] CreateEnvironment(
            Material platformMaterial)
        {
            var environment = new GameObject("Environment");
            var renderers = new Renderer[5];

            GameObject platform = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            platform.name = "Tidy Board";
            platform.transform.SetParent(environment.transform, false);
            platform.transform.localPosition =
                new Vector3(0f, -0.1f, 0f);
            platform.transform.localScale =
                new Vector3(5.8f, 0.16f, 5.8f);
            MeshRenderer platformRenderer =
                platform.GetComponent<MeshRenderer>();
            platformRenderer.sharedMaterial = platformMaterial;
            renderers[0] = platformRenderer;

            renderers[1] = CreateRail(
                environment.transform,
                new Vector3(-2.92f, 0.16f, 0f),
                new Vector3(0.12f, 0.38f, 5.8f),
                platformMaterial);
            renderers[2] = CreateRail(
                environment.transform,
                new Vector3(2.92f, 0.16f, 0f),
                new Vector3(0.12f, 0.38f, 5.8f),
                platformMaterial);
            renderers[3] = CreateRail(
                environment.transform,
                new Vector3(0f, 0.16f, 2.92f),
                new Vector3(5.8f, 0.38f, 0.12f),
                platformMaterial);
            renderers[4] = CreateRail(
                environment.transform,
                new Vector3(0f, 0.16f, -2.92f),
                new Vector3(5.8f, 0.38f, 0.12f),
                platformMaterial);
            return renderers;
        }

        private static Renderer CreateRail(
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject rail = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            rail.name = "Board Rail";
            rail.transform.SetParent(parent, false);
            rail.transform.localPosition = position;
            rail.transform.localScale = scale;
            MeshRenderer renderer =
                rail.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static AudioClip CreateOrLoadSnapClip()
        {
            if (!File.Exists(Path.GetFullPath(SnapClipPath)))
            {
                File.WriteAllBytes(
                    Path.GetFullPath(SnapClipPath),
                    BuildSnapWave());
            }

            AssetDatabase.ImportAsset(
                SnapClipPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            AudioClip clip =
                AssetDatabase.LoadAssetAtPath<AudioClip>(SnapClipPath);
            if (clip == null)
            {
                throw new InvalidOperationException(
                    "The generated snap audio clip could not be imported.");
            }

            return clip;
        }

        private static byte[] BuildSnapWave()
        {
            const int sampleRate = 44100;
            const int sampleCount = 3970;
            const short channels = 1;
            const short bitsPerSample = 16;
            const int bytesPerSample = bitsPerSample / 8;
            int dataLength = sampleCount * channels * bytesPerSample;

            using (var stream = new MemoryStream(44 + dataLength))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataLength);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * bytesPerSample);
                writer.Write((short)(channels * bytesPerSample));
                writer.Write(bitsPerSample);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataLength);

                uint noiseState = 0xC0FFEEu;
                for (int sample = 0; sample < sampleCount; sample++)
                {
                    float time = (float)sample / sampleRate;
                    float envelope = Mathf.Exp(-time * 52f);
                    noiseState ^= noiseState << 13;
                    noiseState ^= noiseState >> 17;
                    noiseState ^= noiseState << 5;
                    float noise =
                        (noiseState / (float)uint.MaxValue) * 2f - 1f;
                    float tone =
                        Mathf.Sin(2f * Mathf.PI * 182f * time);
                    float signal = envelope *
                        (tone * 0.42f + noise * 0.22f);
                    writer.Write(
                        (short)Mathf.RoundToInt(
                            Mathf.Clamp(signal, -1f, 1f) *
                            short.MaxValue));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static AudioMixerGroup CreateOrLoadAudioMixerGroup()
        {
            AudioMixer mixer =
                AssetDatabase.LoadAssetAtPath<AudioMixer>(
                    AudioMixerPath);
            if (mixer == null)
            {
                Type controllerType = FindEditorType(
                    "UnityEditor.Audio.AudioMixerController");
                MethodInfo createMethod = controllerType?.GetMethod(
                    "CreateMixerControllerAtPath",
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);
                if (createMethod == null)
                {
                    throw new InvalidOperationException(
                        "Unity AudioMixer creation API was not found.");
                }

                createMethod.Invoke(null, new object[] { AudioMixerPath });
                AssetDatabase.ImportAsset(
                    AudioMixerPath,
                    ImportAssetOptions.ForceSynchronousImport);
                mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(
                    AudioMixerPath);
            }

            if (mixer == null)
            {
                throw new InvalidOperationException(
                    "Unity did not create the Calm Space AudioMixer.");
            }

            AudioMixerGroup[] groups =
                mixer.FindMatchingGroups("Master");
            if (groups == null || groups.Length == 0)
            {
                throw new InvalidOperationException(
                    "The Calm Space AudioMixer has no Master group.");
            }

            return groups[0];
        }

        private static Type FindEditorType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void EnsureCleaningShadersAreIncluded()
        {
            Shader brush = Shader.Find("Hidden/CalmSpace/MaskBrush");
            Shader coverage =
                Shader.Find(
                    "Hidden/CalmSpace/CoverageDownsample");
            Shader surface = Shader.Find("CalmSpace/CleanableSurface");
            if (brush == null ||
                coverage == null ||
                surface == null)
            {
                throw new InvalidOperationException(
                    "Calm Space cleaning shaders did not import.");
            }

            Object[] graphicsSettingsObjects =
                AssetDatabase.LoadAllAssetsAtPath(
                    "ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettingsObjects == null ||
                graphicsSettingsObjects.Length == 0)
            {
                Debug.LogWarning(
                    "GraphicsSettings could not be serialized; assign " +
                    "cleaning shaders explicitly on cleaning prefabs.");
                return;
            }

            var serialized =
                new SerializedObject(graphicsSettingsObjects[0]);
            SerializedProperty shaders =
                serialized.FindProperty("m_AlwaysIncludedShaders");
            if (shaders == null || !shaders.isArray)
            {
                Debug.LogWarning(
                    "Always Included Shaders is unavailable; assign " +
                    "cleaning shaders explicitly on cleaning prefabs.");
                return;
            }

            AddUniqueObjectReference(shaders, brush);
            AddUniqueObjectReference(shaders, coverage);
            AddUniqueObjectReference(shaders, surface);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddUniqueObjectReference(
            SerializedProperty array,
            Object value)
        {
            for (int index = 0; index < array.arraySize; index++)
            {
                if (array.GetArrayElementAtIndex(index)
                        .objectReferenceValue == value)
                {
                    return;
                }
            }

            int newIndex = array.arraySize;
            array.InsertArrayElementAtIndex(newIndex);
            array.GetArrayElementAtIndex(newIndex)
                .objectReferenceValue = value;
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
    }
}
