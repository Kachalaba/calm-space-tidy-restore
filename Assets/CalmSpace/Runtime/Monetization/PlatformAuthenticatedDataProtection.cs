using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;

namespace CalmSpace.Monetization
{
    public enum AuthenticatedProtectionPlatform
    {
        DevelopmentFallback = 0,
        AndroidKeystore = 1,
        IosKeychain = 2
    }

    public static class PlatformAuthenticatedDataProtectorFactory
    {
        public static AuthenticatedProtectionPlatform ResolvePlatform(
            RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.Android:
                    return AuthenticatedProtectionPlatform.AndroidKeystore;
                case RuntimePlatform.IPhonePlayer:
                    return AuthenticatedProtectionPlatform.IosKeychain;
                default:
                    return AuthenticatedProtectionPlatform
                        .DevelopmentFallback;
            }
        }

        public static IAuthenticatedDataProtector Create(
            RuntimePlatform platform,
            string keyAlias,
            string developmentKeyPath)
        {
            switch (ResolvePlatform(platform))
            {
                case AuthenticatedProtectionPlatform.AndroidKeystore:
                    return new AndroidKeystoreAuthenticatedDataProtector(
                        keyAlias);
                case AuthenticatedProtectionPlatform.IosKeychain:
                    return new IosKeychainAuthenticatedDataProtector(
                        keyAlias);
                default:
                    return new DevelopmentFileAuthenticatedDataProtector(
                        developmentKeyPath);
            }
        }
    }

    /// <summary>
    /// Uses a random AES/HMAC master key stored in the iOS Keychain. The key is
    /// copied into managed memory only while constructing the authenticated
    /// protector and is cleared immediately afterwards.
    /// </summary>
    public sealed class IosKeychainAuthenticatedDataProtector :
        IAuthenticatedDataProtector,
        IDisposable
    {
        private const int MasterKeyLength = 32;
        private readonly ManagedAuthenticatedDataProtector _inner;

        public IosKeychainAuthenticatedDataProtector(string keyAlias)
        {
            if (string.IsNullOrWhiteSpace(keyAlias))
            {
                throw new ArgumentException(
                    "A Keychain alias is required.",
                    nameof(keyAlias));
            }

#if UNITY_IOS && !UNITY_EDITOR
            var key = new byte[MasterKeyLength];
            try
            {
                int copied = CalmSpaceKeychainCopyOrCreateKey(
                    keyAlias,
                    "authenticated-data-master-key",
                    key,
                    key.Length);
                if (copied != MasterKeyLength)
                {
                    throw new CryptographicException(
                        "The iOS Keychain master key was unavailable.");
                }

                _inner =
                    new ManagedAuthenticatedDataProtector(key);
            }
            finally
            {
                Array.Clear(key, 0, key.Length);
            }
#else
            throw new PlatformNotSupportedException(
                "iOS Keychain protection is available only in an iOS player.");
#endif
        }

        public byte[] Protect(
            byte[] plaintext,
            byte[] associatedData)
        {
            return _inner.Protect(plaintext, associatedData);
        }

        public DataUnprotectStatus TryUnprotect(
            byte[] protectedData,
            byte[] associatedData,
            out byte[] plaintext)
        {
            return _inner.TryUnprotect(
                protectedData,
                associatedData,
                out plaintext);
        }

        public void Dispose()
        {
            _inner.Dispose();
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal", CharSet = CharSet.Ansi)]
        private static extern int CalmSpaceKeychainCopyOrCreateKey(
            string service,
            string account,
            [Out] byte[] output,
            int outputCapacity);
#endif
    }

    /// <summary>
    /// Editor/desktop-only development fallback. Mobile players always use a
    /// platform key store; this file-backed key merely keeps local iteration
    /// and tests persistent without embedding a secret in source code.
    /// </summary>
    public sealed class DevelopmentFileAuthenticatedDataProtector :
        IAuthenticatedDataProtector,
        IDisposable
    {
        private const int MasterKeyLength = 32;
        private readonly ManagedAuthenticatedDataProtector _inner;

        public DevelopmentFileAuthenticatedDataProtector(string keyPath)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
            {
                throw new ArgumentException(
                    "A development key path is required.",
                    nameof(keyPath));
            }

            byte[] key = LoadOrCreateKey(keyPath);
            try
            {
                _inner =
                    new ManagedAuthenticatedDataProtector(key);
            }
            finally
            {
                Array.Clear(key, 0, key.Length);
            }
        }

        public byte[] Protect(
            byte[] plaintext,
            byte[] associatedData)
        {
            return _inner.Protect(plaintext, associatedData);
        }

        public DataUnprotectStatus TryUnprotect(
            byte[] protectedData,
            byte[] associatedData,
            out byte[] plaintext)
        {
            return _inner.TryUnprotect(
                protectedData,
                associatedData,
                out plaintext);
        }

        public void Dispose()
        {
            _inner.Dispose();
        }

        private static byte[] LoadOrCreateKey(string keyPath)
        {
            if (File.Exists(keyPath))
            {
                byte[] existing = File.ReadAllBytes(keyPath);
                if (existing.Length == MasterKeyLength)
                {
                    return existing;
                }

                Array.Clear(existing, 0, existing.Length);
                throw new CryptographicException(
                    "The development key file has an invalid length.");
            }

            string directory = Path.GetDirectoryName(keyPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var key = new byte[MasterKeyLength];
            using (RandomNumberGenerator random =
                   RandomNumberGenerator.Create())
            {
                random.GetBytes(key);
            }

            string temporaryPath = keyPath + ".tmp";
            File.WriteAllBytes(temporaryPath, key);
            if (File.Exists(keyPath))
            {
                File.Delete(temporaryPath);
                byte[] existing = File.ReadAllBytes(keyPath);
                Array.Clear(key, 0, key.Length);
                return existing;
            }

            File.Move(temporaryPath, keyPath);
            return key;
        }
    }
}
