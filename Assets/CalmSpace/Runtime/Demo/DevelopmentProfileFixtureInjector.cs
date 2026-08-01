#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CalmSpace.Monetization;
using UnityEngine;

namespace CalmSpace.Demo
{
    public static class DevelopmentProfileFixtureInjector
    {
        private const string LaunchExtraName =
            "calmspace.profileFixture";

        private static bool _requestConsumed;

        public static bool TryInjectFromAndroidLaunchExtra(
            string filePath,
            IAuthenticatedDataProtector protector,
            byte[] associatedData,
            IProfileFileSystem fileSystem)
        {
            if (_requestConsumed)
            {
                return false;
            }

            string request = ReadAndConsumeLaunchExtra();
            if (string.IsNullOrWhiteSpace(request))
            {
                return false;
            }

            _requestConsumed = true;
            bool wrote = TryWriteFixture(
                request,
                filePath,
                protector,
                associatedData,
                fileSystem,
                out string checksum);
            Debug.Log(
                "Calm Space profile fixture status=" +
                (wrote ? "written" : "rejected") +
                " checksum=" +
                checksum);
            return wrote;
        }

        public static bool TryWriteFixture(
            string fixtureName,
            string filePath,
            IAuthenticatedDataProtector protector,
            byte[] associatedData,
            IProfileFileSystem fileSystem,
            out string checksum)
        {
            checksum = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath) ||
                protector == null ||
                associatedData == null ||
                associatedData.Length == 0 ||
                fileSystem == null)
            {
                return false;
            }

            byte[] plaintext = CreateFixturePlaintext(fixtureName);
            if (plaintext == null)
            {
                return false;
            }

            byte[] protectedData = null;
            string temporaryPath = filePath + ".tmp";
            string backupPath = filePath + ".bak";
            try
            {
                protectedData = protector.Protect(
                    plaintext,
                    associatedData);
                if (protectedData == null ||
                    protectedData.Length == 0)
                {
                    return false;
                }

                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    fileSystem.CreateDirectory(directory);
                }

                fileSystem.WriteAllBytesAndFlush(
                    temporaryPath,
                    protectedData);
                if (fileSystem.GetFilePresence(filePath) ==
                    ProfileFilePresence.Exists)
                {
                    fileSystem.Replace(
                        temporaryPath,
                        filePath,
                        backupPath);
                }
                else
                {
                    fileSystem.Move(temporaryPath, filePath);
                }

                checksum = ComputeChecksum(protectedData);
                return true;
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException ||
                      exception is InvalidOperationException ||
                      exception is NotSupportedException ||
                      exception is CryptographicException)
            {
                TryDelete(fileSystem, temporaryPath);
                Debug.LogWarning(
                    "Calm Space profile fixture failed: " +
                    exception.GetType().Name);
                return false;
            }
            finally
            {
                Array.Clear(plaintext, 0, plaintext.Length);
                if (protectedData != null)
                {
                    Array.Clear(
                        protectedData,
                        0,
                        protectedData.Length);
                }
            }
        }

        private static byte[] CreateFixturePlaintext(
            string fixtureName)
        {
            if (string.Equals(
                    fixtureName,
                    "v1",
                    StringComparison.OrdinalIgnoreCase))
            {
                var dto = new SecureProfileV1Dto
                {
                    version = SecureProfileCodec.VersionOne,
                    highestUnlockedLevelIndex = 2,
                    completedLevelMask = 3,
                    selectedThemeId = "sage",
                    musicEnabled = true,
                    cozyTokens = 30,
                    rewardedLevelMask = 3,
                    ownedDecorationMask = 3,
                    selectedDecorationIndex = 1
                };
                return Encoding.UTF8.GetBytes(
                    JsonUtility.ToJson(dto));
            }

            return string.Equals(
                    fixtureName,
                    "v99",
                    StringComparison.OrdinalIgnoreCase)
                ? Encoding.UTF8.GetBytes("{\"version\":99}")
                : null;
        }

        private static string ComputeChecksum(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                try
                {
                    var builder =
                        new StringBuilder(hash.Length * 2);
                    for (var index = 0;
                         index < hash.Length;
                         index++)
                    {
                        builder.Append(
                            hash[index].ToString("x2"));
                    }

                    return builder.ToString();
                }
                finally
                {
                    Array.Clear(hash, 0, hash.Length);
                }
            }
        }

        private static void TryDelete(
            IProfileFileSystem fileSystem,
            string path)
        {
            try
            {
                if (fileSystem.GetFilePresence(path) ==
                    ProfileFilePresence.Exists)
                {
                    fileSystem.Delete(path);
                }
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException)
            {
            }
        }

        private static string ReadAndConsumeLaunchExtra()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer =
                       new AndroidJavaClass(
                           "com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       unityPlayer.GetStatic<AndroidJavaObject>(
                           "currentActivity"))
                using (AndroidJavaObject intent =
                       activity.Call<AndroidJavaObject>("getIntent"))
                {
                    string value =
                        intent.Call<string>(
                            "getStringExtra",
                            LaunchExtraName);
                    intent.Call("removeExtra", LaunchExtraName);
                    return value;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Calm Space could not read the profile fixture request: " +
                    exception.GetType().Name);
                return string.Empty;
            }
#else
            return string.Empty;
#endif
        }
    }
}
#endif
