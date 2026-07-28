using System;
using System.Security.Cryptography;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
#endif

namespace CalmSpace.Monetization
{
    /// <summary>
    /// Android API 24+ adapter for the non-exportable Android Keystore key held
    /// by CalmSpaceNative.androidlib.
    /// </summary>
    public sealed class AndroidKeystoreAuthenticatedDataProtector :
        IAuthenticatedDataProtector
    {
        private const byte EnvelopeVersion = 1;
        private const int NonceLength = 12;
        private const int TagLength = 16;
        private const int MinimumEnvelopeLength =
            1 + NonceLength + TagLength;
        private const string JavaClassName =
            "com.calmspace.nativebridge.KeystoreProtector";
        private static readonly byte[] EmptyBytes = new byte[0];

        private readonly string _keyAlias;

        public AndroidKeystoreAuthenticatedDataProtector(
            string keyAlias)
        {
            if (string.IsNullOrWhiteSpace(keyAlias))
            {
                throw new ArgumentException(
                    "An Android Keystore alias is required.",
                    nameof(keyAlias));
            }

            if (keyAlias.Length > 200)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(keyAlias),
                    "The Android Keystore alias is too long.");
            }

            _keyAlias = keyAlias;
        }

        public bool IsSupported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using (var bridge =
                           new AndroidJavaClass(JavaClassName))
                    {
                        return bridge.CallStatic<bool>("isSupported");
                    }
                }
                catch (AndroidJavaException)
                {
                    return false;
                }
#else
                return false;
#endif
            }
        }

        public byte[] Protect(byte[] plaintext, byte[] associatedData)
        {
            if (plaintext == null)
            {
                throw new ArgumentNullException(nameof(plaintext));
            }

            associatedData ??= EmptyBytes;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var bridge =
                       new AndroidJavaClass(JavaClassName))
                {
                    byte[] protectedData = bridge.CallStatic<byte[]>(
                        "protect",
                        _keyAlias,
                        plaintext,
                        associatedData);
                    if (protectedData == null ||
                        protectedData.Length <
                        MinimumEnvelopeLength ||
                        protectedData[0] != EnvelopeVersion)
                    {
                        throw new CryptographicException(
                            "Android Keystore protection failed closed.");
                    }

                    return protectedData;
                }
            }
            catch (AndroidJavaException exception)
            {
                throw new CryptographicException(
                    "Android Keystore protection failed.",
                    exception);
            }
#else
            throw new PlatformNotSupportedException(
                "Android Keystore protection is available only in an " +
                "Android player on API level 24 or newer.");
#endif
        }

        public DataUnprotectStatus TryUnprotect(
            byte[] protectedData,
            byte[] associatedData,
            out byte[] plaintext)
        {
            plaintext = null;
            associatedData ??= EmptyBytes;

            if (protectedData == null ||
                protectedData.Length < MinimumEnvelopeLength)
            {
                return DataUnprotectStatus.InvalidPayload;
            }

            if (protectedData[0] != EnvelopeVersion)
            {
                return DataUnprotectStatus.UnsupportedVersion;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var bridge =
                       new AndroidJavaClass(JavaClassName))
                {
                    plaintext = bridge.CallStatic<byte[]>(
                        "unprotect",
                        _keyAlias,
                        protectedData,
                        associatedData);
                    return plaintext == null
                        ? DataUnprotectStatus.AuthenticationFailed
                        : DataUnprotectStatus.Success;
                }
            }
            catch (AndroidJavaException)
            {
                plaintext = null;
                return DataUnprotectStatus.AuthenticationFailed;
            }
#else
            return DataUnprotectStatus.AuthenticationFailed;
#endif
        }
    }
}
