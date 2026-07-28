using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace CalmSpace.Monetization
{
    public static class MonetizationAssociatedData
    {
        public static byte[] Create(
            string applicationIdentifier,
            int schemaVersion)
        {
            if (string.IsNullOrWhiteSpace(applicationIdentifier))
            {
                throw new ArgumentException(
                    "An application identifier is required.",
                    nameof(applicationIdentifier));
            }

            if (schemaVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion));
            }

            return Encoding.UTF8.GetBytes(
                applicationIdentifier +
                "|monetization|v" +
                schemaVersion);
        }
    }

    /// <summary>
    /// Fixed-width, versioned binary representation of the entitlement cache.
    /// </summary>
    public sealed class PersistentMonetizationStateCodec
    {
        private const int EncodedLength = 18;
        private const byte FileFormatVersion = 1;
        private static readonly byte[] Magic =
        {
            (byte)'C',
            (byte)'S',
            (byte)'M',
            (byte)'S'
        };

        private readonly int _supportedSchemaVersion;

        public PersistentMonetizationStateCodec(
            int supportedSchemaVersion = 1)
        {
            if (supportedSchemaVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(supportedSchemaVersion));
            }

            _supportedSchemaVersion = supportedSchemaVersion;
        }

        public byte[] Encode(PersistentMonetizationState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.SchemaVersion != _supportedSchemaVersion)
            {
                throw new InvalidOperationException(
                    "The state schema is not supported by this codec.");
            }

            byte[] bytes = new byte[EncodedLength];
            Buffer.BlockCopy(Magic, 0, bytes, 0, Magic.Length);
            bytes[4] = FileFormatVersion;
            WriteInt32BigEndian(bytes, 5, state.SchemaVersion);
            bytes[9] = state.LifetimeNoAds ? (byte)1 : (byte)0;
            WriteInt64BigEndian(
                bytes,
                10,
                state.VerifiedAtUnixSeconds);
            return bytes;
        }

        public PersistentStateLoadStatus TryDecode(
            byte[] bytes,
            out PersistentMonetizationState state)
        {
            state = null;

            if (bytes == null || bytes.Length != EncodedLength)
            {
                return PersistentStateLoadStatus.InvalidData;
            }

            for (int index = 0; index < Magic.Length; index++)
            {
                if (bytes[index] != Magic[index])
                {
                    return PersistentStateLoadStatus.InvalidData;
                }
            }

            if (bytes[4] != FileFormatVersion)
            {
                return PersistentStateLoadStatus.UnsupportedVersion;
            }

            int schemaVersion = ReadInt32BigEndian(bytes, 5);
            if (schemaVersion != _supportedSchemaVersion)
            {
                return PersistentStateLoadStatus.UnsupportedVersion;
            }

            if (bytes[9] != 0 && bytes[9] != 1)
            {
                return PersistentStateLoadStatus.InvalidData;
            }

            state = new PersistentMonetizationState(
                schemaVersion,
                bytes[9] == 1,
                ReadInt64BigEndian(bytes, 10));
            return PersistentStateLoadStatus.Found;
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

        private static void WriteInt64BigEndian(
            byte[] buffer,
            int offset,
            long value)
        {
            ulong unsigned = unchecked((ulong)value);
            for (int index = 7; index >= 0; index--)
            {
                buffer[offset + index] = (byte)unsigned;
                unsigned >>= 8;
            }
        }

        private static long ReadInt64BigEndian(
            byte[] buffer,
            int offset)
        {
            ulong value = 0;
            for (int index = 0; index < 8; index++)
            {
                value = (value << 8) | buffer[offset + index];
            }

            return unchecked((long)value);
        }
    }

    /// <summary>
    /// Stores only authenticated ciphertext. The valid primary is authoritative;
    /// temporary and backup files are consulted only when the primary is missing
    /// or cannot be authenticated/decoded.
    /// </summary>
    public sealed class EncryptedFileMonetizationStateStore :
        IMonetizationStateStore
    {
        private const long MaximumEnvelopeBytes = 64 * 1024;

        private readonly object _sync = new object();
        private readonly string _filePath;
        private readonly string _temporaryPath;
        private readonly string _backupPath;
        private readonly IAuthenticatedDataProtector _protector;
        private readonly byte[] _associatedData;
        private readonly PersistentMonetizationStateCodec _codec;

        public EncryptedFileMonetizationStateStore(
            string filePath,
            IAuthenticatedDataProtector protector,
            byte[] associatedData)
            : this(
                filePath,
                protector,
                associatedData,
                new PersistentMonetizationStateCodec())
        {
        }

        public EncryptedFileMonetizationStateStore(
            IAuthenticatedDataProtector protector,
            string filePath,
            byte[] associatedData)
            : this(filePath, protector, associatedData)
        {
        }

        public EncryptedFileMonetizationStateStore(
            string filePath,
            IAuthenticatedDataProtector protector,
            byte[] associatedData,
            PersistentMonetizationStateCodec codec)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "A state file path is required.",
                    nameof(filePath));
            }

            _protector = protector ??
                throw new ArgumentNullException(nameof(protector));
            _associatedData = associatedData == null
                ? new byte[0]
                : (byte[])associatedData.Clone();
            _codec = codec ??
                throw new ArgumentNullException(nameof(codec));

            _filePath = Path.GetFullPath(filePath);
            _temporaryPath = _filePath + ".tmp";
            _backupPath = _filePath + ".bak";
        }

        public string FilePath => _filePath;

        public string TemporaryPath => _temporaryPath;

        public string BackupPath => _backupPath;

        public UniTask<PersistentStateLoadResult> LoadAsync(
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return UniTask.FromResult(
                    PersistentStateLoadResult.Failed(
                        PersistentStateLoadStatus.Cancelled,
                        null));
            }

            lock (_sync)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return UniTask.FromResult(
                        PersistentStateLoadResult.Failed(
                            PersistentStateLoadStatus.Cancelled,
                            null));
                }

                return UniTask.FromResult(LoadLocked());
            }
        }

        public UniTask<PersistentStateSaveResult> SaveAsync(
            PersistentMonetizationState state,
            CancellationToken cancellationToken)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return UniTask.FromResult(
                    PersistentStateSaveResult.Cancelled());
            }

            lock (_sync)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return UniTask.FromResult(
                        PersistentStateSaveResult.Cancelled());
                }

                return UniTask.FromResult(
                    SaveLocked(state, cancellationToken));
            }
        }

        private PersistentStateLoadResult LoadLocked()
        {
            CandidateReadResult primary = ReadCandidate(_filePath);
            if (primary.IsValid)
            {
                Array.Clear(
                    primary.ProtectedEnvelope,
                    0,
                    primary.ProtectedEnvelope.Length);
                return PersistentStateLoadResult.Found(primary.State);
            }

            if (primary.Status ==
                PersistentStateLoadStatus.UnsupportedVersion ||
                primary.Status == PersistentStateLoadStatus.IoError)
            {
                return PersistentStateLoadResult.Failed(
                    primary.Status,
                    primary.ErrorMessage);
            }

            CandidateReadResult temporary =
                ReadCandidate(_temporaryPath);
            if (temporary.IsValid)
            {
                TryRestorePrimary(temporary.ProtectedEnvelope);
                Array.Clear(
                    temporary.ProtectedEnvelope,
                    0,
                    temporary.ProtectedEnvelope.Length);
                return
                    PersistentStateLoadResult.RecoveredFromTemporary(
                        temporary.State);
            }

            CandidateReadResult backup = ReadCandidate(_backupPath);
            if (backup.IsValid)
            {
                TryRestorePrimary(backup.ProtectedEnvelope);
                Array.Clear(
                    backup.ProtectedEnvelope,
                    0,
                    backup.ProtectedEnvelope.Length);
                return PersistentStateLoadResult.RecoveredFromBackup(
                    backup.State);
            }

            if (primary.Status == PersistentStateLoadStatus.Missing &&
                temporary.Status ==
                PersistentStateLoadStatus.Missing &&
                backup.Status == PersistentStateLoadStatus.Missing)
            {
                return PersistentStateLoadResult.Missing();
            }

            PersistentStateLoadStatus failureStatus =
                SelectFailureStatus(primary, temporary, backup);
            return PersistentStateLoadResult.Failed(
                failureStatus,
                "No authoritative authenticated state file could be recovered.");
        }

        private PersistentStateSaveResult SaveLocked(
            PersistentMonetizationState state,
            CancellationToken cancellationToken)
        {
            try
            {
                byte[] plaintext = _codec.Encode(state);
                byte[] protectedEnvelope;
                try
                {
                    protectedEnvelope = _protector.Protect(
                        plaintext,
                        _associatedData);
                }
                finally
                {
                    Array.Clear(plaintext, 0, plaintext.Length);
                }

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WriteAtomically(protectedEnvelope);
                    return PersistentStateSaveResult.Succeeded();
                }
                finally
                {
                    Array.Clear(
                        protectedEnvelope,
                        0,
                        protectedEnvelope.Length);
                }
            }
            catch (OperationCanceledException)
            {
                TryDeleteTemporaryFile();
                return PersistentStateSaveResult.Cancelled();
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException ||
                      exception is InvalidOperationException ||
                      exception is NotSupportedException ||
                      exception is System.Security.Cryptography
                          .CryptographicException)
            {
                TryDeleteTemporaryFile();
                return PersistentStateSaveResult.Failed(
                    exception.GetType().Name);
            }
        }

        private CandidateReadResult ReadCandidate(string path)
        {
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists)
                {
                    return CandidateReadResult.Missing();
                }

                if (file.Length <= 0 ||
                    file.Length > MaximumEnvelopeBytes)
                {
                    return CandidateReadResult.Failed(
                        PersistentStateLoadStatus.InvalidData,
                        "Envelope length is invalid.");
                }

                byte[] protectedEnvelope = File.ReadAllBytes(path);
                DataUnprotectStatus protectionStatus =
                    _protector.TryUnprotect(
                        protectedEnvelope,
                        _associatedData,
                        out byte[] plaintext);

                if (protectionStatus != DataUnprotectStatus.Success)
                {
                    Array.Clear(
                        protectedEnvelope,
                        0,
                        protectedEnvelope.Length);
                    return CandidateReadResult.Failed(
                        MapProtectionFailure(protectionStatus),
                        protectionStatus.ToString());
                }

                try
                {
                    PersistentStateLoadStatus decodeStatus =
                        _codec.TryDecode(
                            plaintext,
                            out PersistentMonetizationState state);
                    if (decodeStatus !=
                        PersistentStateLoadStatus.Found)
                    {
                        Array.Clear(
                            protectedEnvelope,
                            0,
                            protectedEnvelope.Length);
                        return CandidateReadResult.Failed(
                            decodeStatus,
                            decodeStatus.ToString());
                    }

                    return CandidateReadResult.Valid(
                        state,
                        protectedEnvelope);
                }
                finally
                {
                    if (plaintext != null)
                    {
                        Array.Clear(plaintext, 0, plaintext.Length);
                    }
                }
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException ||
                      exception is InvalidOperationException ||
                      exception is NotSupportedException ||
                      exception is System.Security.Cryptography
                          .CryptographicException)
            {
                return CandidateReadResult.Failed(
                    exception is System.Security.Cryptography
                        .CryptographicException
                        ? PersistentStateLoadStatus
                            .AuthenticationFailed
                        : PersistentStateLoadStatus.IoError,
                    exception.GetType().Name);
            }
        }

        private void WriteAtomically(byte[] protectedEnvelope)
        {
            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            WriteAndFlush(_temporaryPath, protectedEnvelope);

            if (File.Exists(_filePath))
            {
                File.Replace(
                    _temporaryPath,
                    _filePath,
                    _backupPath);
            }
            else
            {
                File.Move(_temporaryPath, _filePath);
            }
        }

        private void TryRestorePrimary(byte[] protectedEnvelope)
        {
            string recoveryPath = _filePath + ".recovery.tmp";
            try
            {
                string directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                WriteAndFlush(recoveryPath, protectedEnvelope);
                if (File.Exists(_filePath))
                {
                    File.Replace(recoveryPath, _filePath, null);
                }
                else
                {
                    File.Move(recoveryPath, _filePath);
                }
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException)
            {
                TryDelete(recoveryPath);
            }
        }

        private static void WriteAndFlush(
            string path,
            byte[] bytes)
        {
            using (var stream = new FileStream(
                   path,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None,
                   4096,
                   FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private void TryDeleteTemporaryFile()
        {
            TryDelete(_temporaryPath);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException)
            {
                // A stale temporary file is safe: authenticated recovery
                // validates it before considering it on the next load.
            }
        }

        private static PersistentStateLoadStatus
            MapProtectionFailure(DataUnprotectStatus status)
        {
            switch (status)
            {
                case DataUnprotectStatus.AuthenticationFailed:
                    return
                        PersistentStateLoadStatus.AuthenticationFailed;
                case DataUnprotectStatus.UnsupportedVersion:
                    return
                        PersistentStateLoadStatus.UnsupportedVersion;
                default:
                    return PersistentStateLoadStatus.InvalidData;
            }
        }

        private static PersistentStateLoadStatus SelectFailureStatus(
            CandidateReadResult primary,
            CandidateReadResult temporary,
            CandidateReadResult backup)
        {
            if (primary.Status != PersistentStateLoadStatus.Missing)
            {
                return primary.Status;
            }

            if (temporary.Status != PersistentStateLoadStatus.Missing)
            {
                return temporary.Status;
            }

            return backup.Status;
        }

        private readonly struct CandidateReadResult
        {
            private CandidateReadResult(
                PersistentStateLoadStatus status,
                PersistentMonetizationState state,
                byte[] protectedEnvelope,
                string errorMessage)
            {
                Status = status;
                State = state;
                ProtectedEnvelope = protectedEnvelope;
                ErrorMessage = errorMessage;
            }

            public PersistentStateLoadStatus Status { get; }

            public PersistentMonetizationState State { get; }

            public byte[] ProtectedEnvelope { get; }

            public string ErrorMessage { get; }

            public bool IsValid =>
                Status == PersistentStateLoadStatus.Found &&
                State != null &&
                ProtectedEnvelope != null;

            public static CandidateReadResult Missing()
            {
                return new CandidateReadResult(
                    PersistentStateLoadStatus.Missing,
                    null,
                    null,
                    null);
            }

            public static CandidateReadResult Valid(
                PersistentMonetizationState state,
                byte[] protectedEnvelope)
            {
                return new CandidateReadResult(
                    PersistentStateLoadStatus.Found,
                    state,
                    protectedEnvelope,
                    null);
            }

            public static CandidateReadResult Failed(
                PersistentStateLoadStatus status,
                string errorMessage)
            {
                return new CandidateReadResult(
                    status,
                    null,
                    null,
                    errorMessage);
            }
        }
    }
}
