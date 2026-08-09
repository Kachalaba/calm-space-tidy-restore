using System;
using System.IO;
using CalmSpace.Editor;
using NUnit.Framework;
using UnityEditor;

namespace CalmSpace.Tests.EditMode
{
    public sealed class AndroidSigningCleanupTests
    {
        private struct SigningSnapshot
        {
            public bool UseCustomKeystore;
            public string KeystoreName;
            public string KeystorePass;
            public string KeyAliasName;
            public string KeyAliasPass;
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
            }
            finally
            {
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
                KeyAliasPass = PlayerSettings.Android.keyaliasPass
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
        }
    }
}
