using System;
using System.IO;
using System.Reflection;
using CalmSpace.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace CalmSpace.Tests.EditMode
{
    public sealed class AndroidSigningCleanupTests
    {
        private const string VersionCodeVariable =
            "CALMSPACE_VERSION_CODE";
        private const string VersionNameVariable =
            "CALMSPACE_VERSION_NAME";

        private struct SigningSnapshot
        {
            public bool UseCustomKeystore;
            public string KeystoreName;
            public string KeystorePass;
            public string KeyAliasName;
            public string KeyAliasPass;
            public string BundleVersion;
            public int BundleVersionCode;
        }

        [Test]
        public void ValidOverridesAreAppliedOnlyInsideSuccessfulBuildScope()
        {
            SigningSnapshot original = Capture();
            string originalNameEnvironment =
                Environment.GetEnvironmentVariable(VersionNameVariable);
            string originalCodeEnvironment =
                Environment.GetEnvironmentVariable(VersionCodeVariable);
            string temporaryName = original.BundleVersion + "-scope";
            int temporaryCode = original.BundleVersionCode == int.MaxValue
                ? int.MaxValue - 1
                : original.BundleVersionCode + 1;
            try
            {
                Environment.SetEnvironmentVariable(
                    VersionNameVariable,
                    "  " + temporaryName + "  ");
                Environment.SetEnvironmentVariable(
                    VersionCodeVariable,
                    temporaryCode.ToString());

                using (var cleanup = new CalmSpaceProjectSetup
                           .AndroidSigningCleanupScope())
                {
                    InvokeApplyVersionOverrides();
                    Assert.That(
                        PlayerSettings.bundleVersion,
                        Is.EqualTo(temporaryName));
                    Assert.That(
                        PlayerSettings.Android.bundleVersionCode,
                        Is.EqualTo(temporaryCode));
                }

                Assert.That(
                    PlayerSettings.bundleVersion,
                    Is.EqualTo(original.BundleVersion));
                Assert.That(
                    PlayerSettings.Android.bundleVersionCode,
                    Is.EqualTo(original.BundleVersionCode));
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    VersionNameVariable,
                    originalNameEnvironment);
                Environment.SetEnvironmentVariable(
                    VersionCodeVariable,
                    originalCodeEnvironment);
                Restore(original);
            }
        }

        [Test]
        public void ArmedScopeClearsSigningStateWhenWorkThrows()
        {
            SigningSnapshot original = Capture();
            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                {
                    using (var cleanup =
                           new CalmSpaceProjectSetup
                               .AndroidSigningCleanupScope())
                    {
                        cleanup.Arm();
                        PlayerSettings.Android.useCustomKeystore = true;
                        PlayerSettings.Android.keystoreName = Path.Combine(
                            Path.GetTempPath(),
                            "temporary-upload.keystore");
                        PlayerSettings.Android.keystorePass = "temporary-pass";
                        PlayerSettings.Android.keyaliasName = "temporary-alias";
                        PlayerSettings.Android.keyaliasPass = "temporary-pass";
                        PlayerSettings.bundleVersion = "temporary-version";
                        PlayerSettings.Android.bundleVersionCode =
                            original.BundleVersionCode == int.MaxValue
                                ? int.MaxValue - 1
                                : original.BundleVersionCode + 1;
                        throw new InvalidOperationException(
                            "simulated early build failure");
                    }
                });

                Assert.That(
                    PlayerSettings.Android.useCustomKeystore,
                    Is.False);
                Assert.That(PlayerSettings.Android.keystoreName, Is.Empty);
                Assert.That(PlayerSettings.Android.keystorePass, Is.Empty);
                Assert.That(PlayerSettings.Android.keyaliasName, Is.Empty);
                Assert.That(PlayerSettings.Android.keyaliasPass, Is.Empty);
                Assert.That(
                    PlayerSettings.bundleVersion,
                    Is.EqualTo(original.BundleVersion));
                Assert.That(
                    PlayerSettings.Android.bundleVersionCode,
                    Is.EqualTo(original.BundleVersionCode));
            }
            finally
            {
                Restore(original);
            }
        }

        [Test]
        public void InvalidCodeDoesNotApplyValidVersionName()
        {
            SigningSnapshot original = Capture();
            string originalNameEnvironment =
                Environment.GetEnvironmentVariable(VersionNameVariable);
            string originalCodeEnvironment =
                Environment.GetEnvironmentVariable(VersionCodeVariable);
            try
            {
                Environment.SetEnvironmentVariable(
                    VersionNameVariable,
                    original.BundleVersion + "-must-not-apply");
                Environment.SetEnvironmentVariable(
                    VersionCodeVariable,
                    "not-a-positive-integer");

                TargetInvocationException failure =
                    Assert.Throws<TargetInvocationException>(
                        InvokeApplyVersionOverrides);
                Assert.That(
                    failure?.InnerException,
                    Is.TypeOf<BuildFailedException>());
                Assert.That(
                    PlayerSettings.bundleVersion,
                    Is.EqualTo(original.BundleVersion));
                Assert.That(
                    PlayerSettings.Android.bundleVersionCode,
                    Is.EqualTo(original.BundleVersionCode));
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    VersionNameVariable,
                    originalNameEnvironment);
                Environment.SetEnvironmentVariable(
                    VersionCodeVariable,
                    originalCodeEnvironment);
                Restore(original);
            }
        }

        private static SigningSnapshot Capture()
        {
            return new SigningSnapshot
            {
                UseCustomKeystore =
                    PlayerSettings.Android.useCustomKeystore,
                KeystoreName = PlayerSettings.Android.keystoreName,
                KeystorePass = PlayerSettings.Android.keystorePass,
                KeyAliasName = PlayerSettings.Android.keyaliasName,
                KeyAliasPass = PlayerSettings.Android.keyaliasPass,
                BundleVersion = PlayerSettings.bundleVersion,
                BundleVersionCode =
                    PlayerSettings.Android.bundleVersionCode
            };
        }

        private static void Restore(SigningSnapshot snapshot)
        {
            PlayerSettings.Android.keystoreName = snapshot.KeystoreName;
            PlayerSettings.Android.keystorePass = snapshot.KeystorePass;
            PlayerSettings.Android.keyaliasName = snapshot.KeyAliasName;
            PlayerSettings.Android.keyaliasPass = snapshot.KeyAliasPass;
            PlayerSettings.Android.useCustomKeystore =
                snapshot.UseCustomKeystore;
            PlayerSettings.bundleVersion = snapshot.BundleVersion;
            PlayerSettings.Android.bundleVersionCode =
                snapshot.BundleVersionCode;
        }

        private static void InvokeApplyVersionOverrides()
        {
            MethodInfo method = typeof(CalmSpaceProjectSetup).GetMethod(
                "ApplyVersionOverrides",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, null);
        }
    }
}
