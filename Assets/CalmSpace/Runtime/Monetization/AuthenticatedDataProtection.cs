using System;
using System.Security.Cryptography;
using System.Text;

namespace CalmSpace.Monetization
{
    /// <summary>
    /// Authenticated Editor/test and non-Android fallback protection using
    /// independent AES-256-CBC and HMAC-SHA256 subkeys.
    /// </summary>
    public sealed class ManagedAuthenticatedDataProtector :
        IAuthenticatedDataProtector,
        IDisposable
    {
        private const byte EnvelopeVersion = 1;
        private const int IvLength = 16;
        private const int LengthFieldSize = 4;
        private const int MacLength = 32;
        private const int HeaderLength =
            1 + IvLength + LengthFieldSize;
        private const int MinimumCiphertextLength = 16;
        private static readonly byte[] EmptyBytes = new byte[0];
        private static readonly byte[] MacDomain =
            Encoding.UTF8.GetBytes(
                "CalmSpace.ManagedAuthenticatedDataProtector.v1");
        private static readonly byte[] EncryptionKeyLabel =
            Encoding.UTF8.GetBytes("calm-space/aes-256-cbc/v1");
        private static readonly byte[] AuthenticationKeyLabel =
            Encoding.UTF8.GetBytes("calm-space/hmac-sha256/v1");

        private readonly byte[] _encryptionKey;
        private readonly byte[] _authenticationKey;
        private readonly object _sync = new object();
        private bool _isDisposed;

        public ManagedAuthenticatedDataProtector(byte[] secret)
        {
            if (secret == null)
            {
                throw new ArgumentNullException(nameof(secret));
            }

            if (secret.Length < 32)
            {
                throw new ArgumentException(
                    "At least 32 bytes of secret key material are required.",
                    nameof(secret));
            }

            _encryptionKey = DeriveSubkey(
                secret,
                EncryptionKeyLabel);
            _authenticationKey = DeriveSubkey(
                secret,
                AuthenticationKeyLabel);
        }

        public byte[] Protect(byte[] plaintext, byte[] associatedData)
        {
            lock (_sync)
            {
                return ProtectLocked(plaintext, associatedData);
            }
        }

        private byte[] ProtectLocked(
            byte[] plaintext,
            byte[] associatedData)
        {
            ThrowIfDisposed();

            if (plaintext == null)
            {
                throw new ArgumentNullException(nameof(plaintext));
            }

            associatedData ??= EmptyBytes;

            byte[] iv = new byte[IvLength];
            using (RandomNumberGenerator random =
                   RandomNumberGenerator.Create())
            {
                random.GetBytes(iv);
            }

            byte[] ciphertext;
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = _encryptionKey;
                aes.IV = iv;

                using (ICryptoTransform encryptor =
                       aes.CreateEncryptor())
                {
                    ciphertext = encryptor.TransformFinalBlock(
                        plaintext,
                        0,
                        plaintext.Length);
                }
            }

            byte[] envelope = new byte[
                HeaderLength + ciphertext.Length + MacLength];
            envelope[0] = EnvelopeVersion;
            Buffer.BlockCopy(iv, 0, envelope, 1, IvLength);
            WriteInt32BigEndian(
                envelope,
                1 + IvLength,
                ciphertext.Length);
            Buffer.BlockCopy(
                ciphertext,
                0,
                envelope,
                HeaderLength,
                ciphertext.Length);

            byte[] mac = ComputeMac(
                envelope,
                0,
                HeaderLength + ciphertext.Length,
                associatedData);
            Buffer.BlockCopy(
                mac,
                0,
                envelope,
                HeaderLength + ciphertext.Length,
                MacLength);

            Array.Clear(iv, 0, iv.Length);
            Array.Clear(ciphertext, 0, ciphertext.Length);
            Array.Clear(mac, 0, mac.Length);
            return envelope;
        }

        public DataUnprotectStatus TryUnprotect(
            byte[] protectedData,
            byte[] associatedData,
            out byte[] plaintext)
        {
            lock (_sync)
            {
                return TryUnprotectLocked(
                    protectedData,
                    associatedData,
                    out plaintext);
            }
        }

        private DataUnprotectStatus TryUnprotectLocked(
            byte[] protectedData,
            byte[] associatedData,
            out byte[] plaintext)
        {
            ThrowIfDisposed();
            plaintext = null;
            associatedData ??= EmptyBytes;

            if (protectedData == null ||
                protectedData.Length <
                HeaderLength + MinimumCiphertextLength + MacLength)
            {
                return DataUnprotectStatus.InvalidPayload;
            }

            if (protectedData[0] != EnvelopeVersion)
            {
                return DataUnprotectStatus.UnsupportedVersion;
            }

            int ciphertextLength = ReadInt32BigEndian(
                protectedData,
                1 + IvLength);
            if (ciphertextLength < MinimumCiphertextLength ||
                ciphertextLength % IvLength != 0 ||
                protectedData.Length !=
                HeaderLength + ciphertextLength + MacLength)
            {
                return DataUnprotectStatus.InvalidPayload;
            }

            byte[] expectedMac = ComputeMac(
                protectedData,
                0,
                HeaderLength + ciphertextLength,
                associatedData);
            bool macMatches = FixedTimeEquals(
                expectedMac,
                0,
                protectedData,
                HeaderLength + ciphertextLength,
                MacLength);
            Array.Clear(expectedMac, 0, expectedMac.Length);

            if (!macMatches)
            {
                return DataUnprotectStatus.AuthenticationFailed;
            }

            byte[] iv = new byte[IvLength];
            Buffer.BlockCopy(
                protectedData,
                1,
                iv,
                0,
                IvLength);

            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.BlockSize = 128;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = _encryptionKey;
                    aes.IV = iv;

                    using (ICryptoTransform decryptor =
                           aes.CreateDecryptor())
                    {
                        plaintext = decryptor.TransformFinalBlock(
                            protectedData,
                            HeaderLength,
                            ciphertextLength);
                    }
                }

                return DataUnprotectStatus.Success;
            }
            catch (CryptographicException)
            {
                plaintext = null;
                return DataUnprotectStatus.AuthenticationFailed;
            }
            finally
            {
                Array.Clear(iv, 0, iv.Length);
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                Array.Clear(
                    _encryptionKey,
                    0,
                    _encryptionKey.Length);
                Array.Clear(
                    _authenticationKey,
                    0,
                    _authenticationKey.Length);
            }
        }

        private static byte[] DeriveSubkey(
            byte[] secret,
            byte[] label)
        {
            using (var hmac = new HMACSHA256(secret))
            {
                return hmac.ComputeHash(label);
            }
        }

        private byte[] ComputeMac(
            byte[] envelope,
            int envelopeOffset,
            int envelopeCount,
            byte[] associatedData)
        {
            byte[] macInput = new byte[
                MacDomain.Length +
                LengthFieldSize +
                associatedData.Length +
                envelopeCount];
            Buffer.BlockCopy(
                MacDomain,
                0,
                macInput,
                0,
                MacDomain.Length);
            WriteInt32BigEndian(
                macInput,
                MacDomain.Length,
                associatedData.Length);
            Buffer.BlockCopy(
                associatedData,
                0,
                macInput,
                MacDomain.Length + LengthFieldSize,
                associatedData.Length);
            Buffer.BlockCopy(
                envelope,
                envelopeOffset,
                macInput,
                MacDomain.Length +
                LengthFieldSize +
                associatedData.Length,
                envelopeCount);

            using (var hmac =
                   new HMACSHA256(_authenticationKey))
            {
                byte[] mac = hmac.ComputeHash(macInput);
                Array.Clear(macInput, 0, macInput.Length);
                return mac;
            }
        }

        private static bool FixedTimeEquals(
            byte[] left,
            int leftOffset,
            byte[] right,
            int rightOffset,
            int count)
        {
            if (left == null ||
                right == null ||
                leftOffset < 0 ||
                rightOffset < 0 ||
                count < 0 ||
                left.Length - leftOffset < count ||
                right.Length - rightOffset < count)
            {
                return false;
            }

            int difference = 0;
            for (int index = 0; index < count; index++)
            {
                difference |=
                    left[leftOffset + index] ^
                    right[rightOffset + index];
            }

            return difference == 0;
        }

        private static void WriteInt32BigEndian(
            byte[] buffer,
            int offset,
            int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static int ReadInt32BigEndian(
            byte[] buffer,
            int offset)
        {
            return
                (buffer[offset] << 24) |
                (buffer[offset + 1] << 16) |
                (buffer[offset + 2] << 8) |
                buffer[offset + 3];
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(
                    nameof(ManagedAuthenticatedDataProtector));
            }
        }
    }
}
