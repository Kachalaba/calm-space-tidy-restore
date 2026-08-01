using System;
using System.IO;
using System.Text;
using CalmSpace.Demo;
using CalmSpace.Monetization;
using NUnit.Framework;
using UnityEngine;

namespace CalmSpace.Tests.EditMode
{
    public sealed class EncryptedFileDemoProgressStoreTests
    {
        private static readonly byte[] Secret =
            Encoding.UTF8.GetBytes(
                "calm-space-secure-profile-test-secret-material-v1");

        private static readonly byte[] AssociatedData =
            Encoding.UTF8.GetBytes(
                "com.calmspace.tests|secure-profile|v1");

        [Test]
        public void VersionTwoCodecRoundTripsEveryFieldAndUnknownIds()
        {
            var expected = new DemoProgressSnapshot(
                7,
                0b10110101,
                "ocean",
                false,
                123,
                0b00110101,
                new[]
                {
                    "soft-fern",
                    "future.unknown-decoration"
                },
                new[]
                {
                    new DecorationSelection(
                        "workbench-accent",
                        "future.unknown-decoration"),
                    new DecorationSelection(
                        "future.unknown-slot",
                        "soft-fern")
                },
                new[]
                {
                    "cozy-workshop.clear-passage"
                },
                new[]
                {
                    "family.future-memory"
                },
                new[]
                {
                    "future.chapter"
                },
                new[]
                {
                    PendingPresentationEntry.RoomReveal(
                        "cozy-workshop.pebble-shelf"),
                    PendingPresentationEntry.Memory(
                        "family.future-memory-queued"),
                    PendingPresentationEntry.Finale(
                        "future.finale",
                        requiresExplicitLaunch: true)
                },
                20668,
                4);
            var codec = new SecureProfileCodec();

            byte[] bytes = codec.EncodeV2(expected);
            ProfileLoadStatus status = codec.TryDecode(
                bytes,
                out SecureProfileV1Dto versionOne,
                out DemoProgressSnapshot actual);

            Assert.That(status, Is.EqualTo(ProfileLoadStatus.LoadedV2));
            Assert.That(versionOne, Is.Null);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(
                actual.OwnsDecoration("future.unknown-decoration"),
                Is.True);

            using (var fixture = new SecureStoreFixture())
            {
                fixture.WriteVersionTwo(
                    fixture.FilePath,
                    expected);

                ProfileInitializationResult encrypted =
                    fixture.CreateStore().Initialize(8, "sage", 15);

                Assert.That(encrypted.IsReady, Is.True);
                Assert.That(
                    encrypted.LoadStatus,
                    Is.EqualTo(ProfileLoadStatus.LoadedV2));
                Assert.That(
                    encrypted.Source,
                    Is.EqualTo(ProfileLoadSource.Primary));
                Assert.That(encrypted.Snapshot, Is.EqualTo(expected));
            }
        }

        [Test]
        public void SecureProfileRoundTripsAllProgressAndInventory()
        {
            using (var fixture = new SecureStoreFixture())
            {
                EncryptedFileDemoProgressStore store =
                    fixture.CreateStore();
                store.Initialize(8, "sage", 15);

                Assert.That(
                    store.CompleteLevelAndReward(0, 15),
                    Is.EqualTo(15));
                Assert.That(
                    store.CompleteLevelAndReward(1, 15),
                    Is.EqualTo(15));
                Assert.That(
                    store.TryPurchaseAndSelectDecoration(1, 20),
                    Is.True);
                store.SetSelectedTheme("ocean");
                store.SetMusicEnabled(false);

                DemoProgressSnapshot expected = store.Current;
                Assert.That(expected.CozyTokens, Is.EqualTo(10));
                Assert.That(
                    expected.OwnedDecorationMask,
                    Is.EqualTo(0b0011));
                Assert.That(
                    expected.SelectedDecorationIndex,
                    Is.EqualTo(1));

                EncryptedFileDemoProgressStore reloaded =
                    fixture.CreateStore();
                reloaded.Initialize(8, "sage", 15);

                Assert.That(reloaded.Current, Is.EqualTo(expected));
                Assert.That(reloaded.IsLevelCompleted(0), Is.True);
                Assert.That(reloaded.IsLevelCompleted(1), Is.True);
                Assert.That(reloaded.IsLevelUnlocked(2), Is.True);
                Assert.That(
                    reloaded.Current.SelectedThemeId,
                    Is.EqualTo("ocean"));
                Assert.That(reloaded.Current.MusicEnabled, Is.False);
            }
        }

        [Test]
        public void VersionOneLegacyProfileMigratesWithRewardsOnce()
        {
            using (var fixture = new SecureStoreFixture())
            {
                PlayerPrefs.SetString(
                    fixture.LegacyKey,
                    "{" +
                    "\"version\":1," +
                    "\"highestUnlockedLevelIndex\":2," +
                    "\"completedLevelMask\":3," +
                    "\"selectedThemeId\":\"ocean\"," +
                    "\"musicEnabled\":true" +
                    "}");
                PlayerPrefs.Save();

                EncryptedFileDemoProgressStore imported =
                    fixture.CreateStore();
                imported.Initialize(8, "sage", 15);

                Assert.That(
                    imported.Current.CozyTokens,
                    Is.EqualTo(30));
                Assert.That(
                    imported.Current.RewardedLevelMask,
                    Is.EqualTo(0b0011));
                Assert.That(
                    imported.Current.OwnedDecorationMask,
                    Is.EqualTo(1));

                DemoProgressSnapshot expected = imported.Current;
                EncryptedFileDemoProgressStore reloaded =
                    fixture.CreateStore();
                reloaded.Initialize(8, "sage", 15);

                Assert.That(reloaded.Current, Is.EqualTo(expected));
                Assert.That(
                    reloaded.Current.CozyTokens,
                    Is.EqualTo(30),
                    "Reloading the secure profile must not grant legacy " +
                    "completion rewards twice.");
            }
        }

        [Test]
        public void LegacyPlayerPrefsProfileImportsExactlyOnce()
        {
            using (var fixture = new SecureStoreFixture())
            {
                PlayerPrefs.SetString(
                    fixture.LegacyKey,
                    "{" +
                    "\"version\":2," +
                    "\"highestUnlockedLevelIndex\":3," +
                    "\"completedLevelMask\":3," +
                    "\"selectedThemeId\":\"ocean\"," +
                    "\"musicEnabled\":false," +
                    "\"calmPoints\":47," +
                    "\"rewardedLevelMask\":3," +
                    "\"ownedDecorationMask\":7," +
                    "\"selectedDecorationIndex\":2" +
                    "}");
                PlayerPrefs.Save();

                EncryptedFileDemoProgressStore imported =
                    fixture.CreateStore();
                imported.Initialize(8, "sage", 15);
                DemoProgressSnapshot expected = imported.Current;

                Assert.That(
                    PlayerPrefs.GetInt(fixture.MigrationMarker, 0),
                    Is.EqualTo(1));
                Assert.That(expected.CozyTokens, Is.EqualTo(47));
                Assert.That(
                    expected.CompletedLevelMask,
                    Is.EqualTo(0b0011));
                Assert.That(
                    expected.RewardedLevelMask,
                    Is.EqualTo(0b0011));
                Assert.That(
                    expected.OwnedDecorationMask,
                    Is.EqualTo(0b0111));
                Assert.That(
                    expected.SelectedDecorationIndex,
                    Is.EqualTo(2));
                Assert.That(
                    expected.SelectedThemeId,
                    Is.EqualTo("ocean"));
                Assert.That(expected.MusicEnabled, Is.False);

                PlayerPrefs.SetString(
                    fixture.LegacyKey,
                    "{" +
                    "\"version\":2," +
                    "\"highestUnlockedLevelIndex\":7," +
                    "\"completedLevelMask\":255," +
                    "\"selectedThemeId\":\"sunset\"," +
                    "\"musicEnabled\":true," +
                    "\"calmPoints\":9999," +
                    "\"rewardedLevelMask\":255," +
                    "\"ownedDecorationMask\":15," +
                    "\"selectedDecorationIndex\":3" +
                    "}");
                PlayerPrefs.Save();

                EncryptedFileDemoProgressStore reloaded =
                    fixture.CreateStore();
                reloaded.Initialize(8, "sage", 15);

                Assert.That(
                    reloaded.Current,
                    Is.EqualTo(expected),
                    "The authenticated profile must remain authoritative " +
                    "after the one-time PlayerPrefs import.");
            }
        }

        [Test]
        public void TamperedPrimaryRecoversLastAuthenticatedBackup()
        {
            using (var fixture = new SecureStoreFixture())
            {
                EncryptedFileDemoProgressStore store =
                    fixture.CreateStore();
                store.Initialize(8, "sage", 15);
                store.CompleteLevelAndReward(0, 15);
                DemoProgressSnapshot expectedBackup = store.Current;
                store.CompleteLevelAndReward(1, 15);

                Assert.That(
                    store.Current,
                    Is.Not.EqualTo(expectedBackup));
                Assert.That(File.Exists(fixture.BackupPath), Is.True);

                CorruptOneByte(fixture.FilePath);

                EncryptedFileDemoProgressStore recovered =
                    fixture.CreateStore();
                recovered.Initialize(8, "sage", 15);

                Assert.That(
                    recovered.Current,
                    Is.EqualTo(expectedBackup));
                Assert.That(
                    recovered.IsLevelCompleted(0),
                    Is.True);
                Assert.That(
                    recovered.IsLevelCompleted(1),
                    Is.False);

                EncryptedFileDemoProgressStore secondReload =
                    fixture.CreateStore();
                secondReload.Initialize(8, "sage", 15);
                Assert.That(
                    secondReload.Current,
                    Is.EqualTo(expectedBackup),
                    "Backup recovery must repair the primary profile.");
            }
        }

        [Test]
        public void TamperedProfileWithoutBackupFailsClosed()
        {
            using (var fixture = new SecureStoreFixture())
            {
                EncryptedFileDemoProgressStore store =
                    fixture.CreateStore();
                store.Initialize(8, "sage", 15);
                store.CompleteLevelAndReward(0, 15);

                if (File.Exists(fixture.BackupPath))
                {
                    File.Delete(fixture.BackupPath);
                }

                CorruptOneByte(fixture.FilePath);

                EncryptedFileDemoProgressStore recovered =
                    fixture.CreateStore();
                recovered.Initialize(8, "sage", 15);

                Assert.That(recovered.Current.CozyTokens, Is.Zero);
                Assert.That(
                    recovered.Current.CompletedLevelMask,
                    Is.Zero);
                Assert.That(
                    recovered.Current.OwnedDecorationMask,
                    Is.EqualTo(1));
                Assert.That(
                    recovered.IsLevelUnlocked(0),
                    Is.True);
                Assert.That(
                    recovered.IsLevelUnlocked(1),
                    Is.False);
            }
        }

        [Test]
        public void FailedEncryptedWriteDoesNotMutateVisibleProfile()
        {
            using (var fixture = new SecureStoreFixture())
            using (var protector =
                   new SwitchableAuthenticatedDataProtector(Secret))
            {
                EncryptedFileDemoProgressStore store =
                    fixture.CreateStore(protector);
                store.Initialize(8, "sage", 15);
                DemoProgressSnapshot before = store.Current;
                var changes = 0;
                store.ProgressChanged += _ => changes++;

                protector.ThrowOnProtect = true;
                int granted =
                    store.CompleteLevelAndReward(0, 15);

                Assert.That(granted, Is.Zero);
                Assert.That(store.Current, Is.EqualTo(before));
                Assert.That(changes, Is.Zero);
            }
        }

        [Test]
        public void TypedWriteFailureReturnsPersistFailedWithoutStateOrEvent()
        {
            using (var fixture = new SecureStoreFixture())
            using (var protector =
                   new SwitchableAuthenticatedDataProtector(Secret))
            {
                EncryptedFileDemoProgressStore store =
                    fixture.CreateStore(protector);
                ProfileInitializationResult initialized =
                    store.Initialize(8, "sage", 15);
                Assert.That(initialized.IsReady, Is.True);
                DemoProgressSnapshot before = store.Current;
                var changes = 0;
                store.ProgressChanged += _ => changes++;

                protector.ThrowOnProtect = true;
                ProfileMutationResult<LevelCompletionMutation> result =
                    store.CompleteLevel(
                        new CompleteLevelCommand(
                            "cozy-workshop.clear-passage",
                            0,
                            15,
                            new PendingPresentationEntry[0]));

                Assert.That(
                    result.Status,
                    Is.EqualTo(ProfileMutationStatus.PersistFailed));
                Assert.That(result.Snapshot, Is.EqualTo(before));
                Assert.That(store.Current, Is.EqualTo(before));
                Assert.That(changes, Is.Zero);
            }
        }

        [Test]
        public void SuccessfulCommandReplacesFileBeforeOneEventAndReplayWritesNothing()
        {
            using (var fixture = new SecureStoreFixture())
            {
                EncryptedFileDemoProgressStore store =
                    fixture.CreateStore();
                store.Initialize(8, "sage", 15);
                var changes = 0;
                DemoProgressSnapshot observedOnDisk = default;
                store.ProgressChanged += snapshot =>
                {
                    changes++;
                    EncryptedFileDemoProgressStore reader =
                        fixture.CreateStore();
                    ProfileInitializationResult loaded =
                        reader.Initialize(8, "sage", 15);
                    Assert.That(loaded.IsReady, Is.True);
                    observedOnDisk = reader.Current;
                    Assert.That(observedOnDisk, Is.EqualTo(snapshot));
                };
                var command = new CompleteLevelCommand(
                    "cozy-workshop.clear-passage",
                    0,
                    15,
                    new PendingPresentationEntry[0]);

                ProfileMutationResult<LevelCompletionMutation> applied =
                    store.CompleteLevel(command);
                byte[] afterApplied = File.ReadAllBytes(fixture.FilePath);
                ProfileMutationResult<LevelCompletionMutation> replay =
                    store.CompleteLevel(command);
                byte[] afterReplay = File.ReadAllBytes(fixture.FilePath);

                Assert.That(
                    applied.Status,
                    Is.EqualTo(ProfileMutationStatus.Applied));
                Assert.That(observedOnDisk, Is.EqualTo(applied.Snapshot));
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(
                    replay.Status,
                    Is.EqualTo(ProfileMutationStatus.AlreadyApplied));
                Assert.That(replay.Snapshot, Is.EqualTo(applied.Snapshot));
                Assert.That(afterReplay, Is.EqualTo(afterApplied));
            }
        }

        [Test]
        public void AuthenticatedFuturePrimaryStopsBeforeValidBackupAndPreservesBytes()
        {
            using (var fixture = new SecureStoreFixture())
            {
                fixture.WriteAuthenticatedJson(
                    fixture.FilePath,
                    "{\"version\":99,\"future\":\"primary\"}");
                fixture.WriteVersionOne(
                    fixture.BackupPath,
                    completedMask: 1,
                    tokens: 15);
                byte[] primaryBefore = File.ReadAllBytes(fixture.FilePath);
                byte[] backupBefore = File.ReadAllBytes(fixture.BackupPath);

                ProfileInitializationResult first =
                    fixture.CreateStore().Initialize(8, "sage", 15);
                ProfileInitializationResult second =
                    fixture.CreateStore().Initialize(8, "sage", 15);

                Assert.That(
                    first.LoadStatus,
                    Is.EqualTo(ProfileLoadStatus.UnsupportedVersion));
                Assert.That(first.IsReady, Is.False);
                Assert.That(
                    second.LoadStatus,
                    Is.EqualTo(ProfileLoadStatus.UnsupportedVersion));
                Assert.That(second.IsReady, Is.False);
                Assert.That(
                    File.ReadAllBytes(fixture.FilePath),
                    Is.EqualTo(primaryBefore));
                Assert.That(
                    File.ReadAllBytes(fixture.BackupPath),
                    Is.EqualTo(backupBefore));
            }
        }

        [Test]
        public void FutureTemporaryStopsAfterCorruptPrimaryBeforeValidBackup()
        {
            using (var fixture = new SecureStoreFixture())
            {
                File.WriteAllBytes(fixture.FilePath, new byte[] { 1, 2, 3 });
                fixture.WriteAuthenticatedJson(
                    fixture.TemporaryPath,
                    "{\"version\":99}");
                fixture.WriteVersionOne(
                    fixture.BackupPath,
                    completedMask: 1,
                    tokens: 15);

                ProfileInitializationResult result =
                    fixture.CreateStore().Initialize(8, "sage", 15);

                Assert.That(
                    result.LoadStatus,
                    Is.EqualTo(ProfileLoadStatus.UnsupportedVersion));
                Assert.That(result.Source, Is.EqualTo(
                    ProfileLoadSource.Temporary));
                Assert.That(result.IsReady, Is.False);
            }
        }

        [Test]
        public void FutureBackupStopsAfterUnusableEarlierCandidates()
        {
            using (var fixture = new SecureStoreFixture())
            {
                File.WriteAllBytes(fixture.FilePath, new byte[] { 1, 2, 3 });
                fixture.WriteAuthenticatedJson(
                    fixture.TemporaryPath,
                    "{\"notVersioned\":true}");
                fixture.WriteAuthenticatedJson(
                    fixture.BackupPath,
                    "{\"version\":99}");

                ProfileInitializationResult result =
                    fixture.CreateStore().Initialize(8, "sage", 15);

                Assert.That(
                    result.LoadStatus,
                    Is.EqualTo(ProfileLoadStatus.UnsupportedVersion));
                Assert.That(
                    result.Source,
                    Is.EqualTo(ProfileLoadSource.Backup));
                Assert.That(result.IsReady, Is.False);
            }
        }

        [Test]
        public void CorruptedPrimaryEnvelopeVersionFallsThroughToTemporary()
        {
            using (var fixture = new SecureStoreFixture())
            {
                fixture.WriteVersionTwo(
                    fixture.FilePath,
                    CreateSnapshot(tokens: 11));
                CorruptEnvelopeVersionByte(fixture.FilePath);
                fixture.WriteVersionTwo(
                    fixture.TemporaryPath,
                    CreateSnapshot(tokens: 43));
                fixture.WriteVersionTwo(
                    fixture.BackupPath,
                    CreateSnapshot(tokens: 79));

                ProfileInitializationResult result =
                    fixture.CreateStore().Initialize(8, "sage", 15);

                Assert.That(result.IsReady, Is.True);
                Assert.That(
                    result.Source,
                    Is.EqualTo(ProfileLoadSource.Temporary));
                Assert.That(result.WasPersisted, Is.True);
                Assert.That(result.Snapshot.CozyTokens, Is.EqualTo(43));
            }
        }

        [Test]
        public void CorruptedTemporaryEnvelopeVersionFallsThroughToBackup()
        {
            using (var fixture = new SecureStoreFixture())
            {
                File.WriteAllBytes(
                    fixture.FilePath,
                    new byte[] { 1, 2, 3 });
                fixture.WriteVersionTwo(
                    fixture.TemporaryPath,
                    CreateSnapshot(tokens: 43));
                CorruptEnvelopeVersionByte(fixture.TemporaryPath);
                fixture.WriteVersionTwo(
                    fixture.BackupPath,
                    CreateSnapshot(tokens: 79));

                ProfileInitializationResult result =
                    fixture.CreateStore().Initialize(8, "sage", 15);

                Assert.That(result.IsReady, Is.True);
                Assert.That(
                    result.Source,
                    Is.EqualTo(ProfileLoadSource.Backup));
                Assert.That(result.WasPersisted, Is.True);
                Assert.That(result.Snapshot.CozyTokens, Is.EqualTo(79));
            }
        }

        [Test]
        public void SupportedPrimaryWinsStaleTemporaryAndBackup()
        {
            using (var fixture = new SecureStoreFixture())
            {
                fixture.WriteVersionTwo(
                    fixture.FilePath,
                    CreateSnapshot(tokens: 41));
                fixture.WriteVersionTwo(
                    fixture.TemporaryPath,
                    CreateSnapshot(tokens: 82));
                fixture.WriteVersionOne(
                    fixture.BackupPath,
                    completedMask: 1,
                    tokens: 99);
                byte[] primaryBefore = File.ReadAllBytes(fixture.FilePath);

                ProfileInitializationResult result =
                    fixture.CreateStore().Initialize(8, "sage", 15);

                Assert.That(result.IsReady, Is.True);
                Assert.That(
                    result.LoadStatus,
                    Is.EqualTo(ProfileLoadStatus.LoadedV2));
                Assert.That(
                    result.Source,
                    Is.EqualTo(ProfileLoadSource.Primary));
                Assert.That(result.WasPersisted, Is.False);
                Assert.That(result.Snapshot.CozyTokens, Is.EqualTo(41));
                Assert.That(
                    File.ReadAllBytes(fixture.FilePath),
                    Is.EqualTo(primaryBefore));
            }
        }

        [Test]
        public void AuthenticationAndInvalidCandidatesFallThroughInOrderToBackup()
        {
            using (var fixture = new SecureStoreFixture())
            {
                fixture.WriteVersionTwo(
                    fixture.FilePath,
                    CreateSnapshot(tokens: 10));
                CorruptOneByte(fixture.FilePath);
                fixture.WriteAuthenticatedJson(
                    fixture.TemporaryPath,
                    "{\"version\":2,\"pendingPresentations\":[null]}");
                fixture.WriteVersionOne(
                    fixture.BackupPath,
                    completedMask: 3,
                    tokens: 37);

                ProfileInitializationResult result =
                    fixture.CreateStore().Initialize(8, "sage", 15);

                Assert.That(result.IsReady, Is.True);
                Assert.That(
                    result.LoadStatus,
                    Is.EqualTo(ProfileLoadStatus.LoadedV1));
                Assert.That(
                    result.Source,
                    Is.EqualTo(ProfileLoadSource.Backup));
                Assert.That(result.WasMigrated, Is.True);
                Assert.That(result.WasPersisted, Is.True);
                Assert.That(result.Snapshot.CozyTokens, Is.EqualTo(37));
            }
        }

        [Test]
        public void ValidTemporaryRecoveryRepublishesPrimaryBeforeBecomingReady()
        {
            using (var fixture = new SecureStoreFixture())
            {
                File.WriteAllBytes(
                    fixture.FilePath,
                    new byte[] { 1, 2, 3 });
                DemoProgressSnapshot expected =
                    CreateSnapshot(tokens: 61);
                fixture.WriteVersionTwo(
                    fixture.TemporaryPath,
                    expected);
                fixture.WriteVersionTwo(
                    fixture.BackupPath,
                    CreateSnapshot(tokens: 17));

                ProfileInitializationResult recovered =
                    fixture.CreateStore().Initialize(8, "sage", 15);

                Assert.That(recovered.IsReady, Is.True);
                Assert.That(
                    recovered.LoadStatus,
                    Is.EqualTo(ProfileLoadStatus.LoadedV2));
                Assert.That(
                    recovered.Source,
                    Is.EqualTo(ProfileLoadSource.Temporary));
                Assert.That(recovered.WasPersisted, Is.True);
                Assert.That(recovered.Snapshot, Is.EqualTo(expected));

                ProfileInitializationResult primary =
                    fixture.CreateStore().Initialize(8, "sage", 15);
                Assert.That(primary.IsReady, Is.True);
                Assert.That(
                    primary.Source,
                    Is.EqualTo(ProfileLoadSource.Primary));
                Assert.That(primary.WasPersisted, Is.False);
                Assert.That(primary.Snapshot, Is.EqualTo(expected));
            }
        }

        [Test]
        public void FailedTemporaryRecoveryPreservesCandidateBytesForRetry()
        {
            using (var fixture = new SecureStoreFixture())
            {
                File.WriteAllBytes(
                    fixture.FilePath,
                    new byte[] { 4, 5, 6 });
                DemoProgressSnapshot expected =
                    CreateSnapshot(tokens: 63);
                fixture.WriteVersionTwo(
                    fixture.TemporaryPath,
                    expected);
                byte[] primaryBefore =
                    File.ReadAllBytes(fixture.FilePath);
                byte[] temporaryBefore =
                    File.ReadAllBytes(fixture.TemporaryPath);
                var fileSystem = new FaultInjectingProfileFileSystem
                {
                    FailReplace = true
                };

                ProfileInitializationResult failed =
                    fixture.CreateStore(fileSystem: fileSystem)
                        .Initialize(8, "sage", 15);

                Assert.That(
                    failed.LoadStatus,
                    Is.EqualTo(ProfileLoadStatus.IoError));
                Assert.That(
                    failed.Source,
                    Is.EqualTo(ProfileLoadSource.Temporary));
                Assert.That(failed.IsReady, Is.False);
                Assert.That(
                    File.ReadAllBytes(fixture.FilePath),
                    Is.EqualTo(primaryBefore));
                Assert.That(
                    File.Exists(fixture.TemporaryPath),
                    Is.True,
                    "A failed republish must keep the authenticated " +
                    "temporary recovery candidate.");
                Assert.That(
                    File.ReadAllBytes(fixture.TemporaryPath),
                    Is.EqualTo(temporaryBefore));

                fileSystem.FailReplace = false;
                ProfileInitializationResult retried =
                    fixture.CreateStore(fileSystem: fileSystem)
                        .Initialize(8, "sage", 15);

                Assert.That(retried.IsReady, Is.True);
                Assert.That(
                    retried.Source,
                    Is.EqualTo(ProfileLoadSource.Temporary));
                Assert.That(retried.WasPersisted, Is.True);
                Assert.That(retried.Snapshot, Is.EqualTo(expected));
            }
        }

        [Test]
        public void UnreadableCandidateReturnsIoErrorWithoutWritingDefault()
        {
            using (var fixture = new SecureStoreFixture())
            {
                fixture.WriteVersionTwo(
                    fixture.FilePath,
                    CreateSnapshot(tokens: 22));
                byte[] before = File.ReadAllBytes(fixture.FilePath);
                var fileSystem = new FaultInjectingProfileFileSystem
                {
                    UnreadablePath = fixture.FilePath
                };

                ProfileInitializationResult result =
                    fixture.CreateStore(fileSystem: fileSystem)
                        .Initialize(8, "sage", 15);

                Assert.That(
                    result.LoadStatus,
                    Is.EqualTo(ProfileLoadStatus.IoError));
                Assert.That(result.IsReady, Is.False);
                Assert.That(fileSystem.WriteCount, Is.Zero);
                Assert.That(
                    File.ReadAllBytes(fixture.FilePath),
                    Is.EqualTo(before));
            }
        }

        [Test]
        public void DirectoryAtProfilePathReturnsIoErrorWithoutFallbackWrite()
        {
            using (var fixture = new SecureStoreFixture())
            {
                Directory.CreateDirectory(fixture.FilePath);
                var fileSystem = new TrackingSystemProfileFileSystem();

                ProfileInitializationResult result =
                    fixture.CreateStore(fileSystem: fileSystem)
                        .Initialize(8, "sage", 15);

                Assert.That(result.IsReady, Is.False);
                Assert.That(
                    result.LoadStatus,
                    Is.EqualTo(ProfileLoadStatus.IoError));
                Assert.That(
                    result.Source,
                    Is.EqualTo(ProfileLoadSource.Primary));
                Assert.That(fileSystem.WriteCount, Is.Zero);
            }
        }

        [Test]
        public void MissingProfilePersistsDefaultThroughSystemFileSystem()
        {
            using (var fixture = new SecureStoreFixture())
            {
                var fileSystem = new TrackingSystemProfileFileSystem();

                ProfileInitializationResult result =
                    fixture.CreateStore(fileSystem: fileSystem)
                        .Initialize(8, "sage", 15);

                Assert.That(result.IsReady, Is.True);
                Assert.That(
                    result.LoadStatus,
                    Is.EqualTo(ProfileLoadStatus.Missing));
                Assert.That(
                    result.Source,
                    Is.EqualTo(ProfileLoadSource.Default));
                Assert.That(result.WasPersisted, Is.True);
                Assert.That(fileSystem.WriteCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void ExhaustedAuthenticatedInvalidCandidatesPersistDefault()
        {
            using (var fixture = new SecureStoreFixture())
            {
                fixture.WriteAuthenticatedJson(
                    fixture.FilePath,
                    "{\"version\":2,\"pendingPresentations\":[null]}");
                fixture.WriteAuthenticatedJson(
                    fixture.TemporaryPath,
                    "{\"version\":2,\"pendingPresentations\":[{\"kind\":999}]}");
                byte[] wrongAssociatedData =
                    Encoding.UTF8.GetBytes("wrong-associated-data");
                fixture.WriteAuthenticatedJson(
                    fixture.BackupPath,
                    "{\"version\":2}",
                    wrongAssociatedData);

                ProfileInitializationResult result =
                    fixture.CreateStore().Initialize(8, "sage", 15);

                Assert.That(result.IsReady, Is.True);
                Assert.That(
                    result.Source,
                    Is.EqualTo(ProfileLoadSource.Default));
                Assert.That(result.WasPersisted, Is.True);
                Assert.That(result.Snapshot.CozyTokens, Is.Zero);
                Assert.That(
                    result.Snapshot.CompletedLevelMask,
                    Is.Zero);
            }
        }

        [Test]
        public void FailedRecoveryRepublishLeavesPrimaryUnpublished()
        {
            using (var fixture = new SecureStoreFixture())
            {
                File.WriteAllBytes(
                    fixture.FilePath,
                    new byte[] { 7, 8, 9 });
                fixture.WriteVersionTwo(
                    fixture.BackupPath,
                    CreateSnapshot(tokens: 55));
                byte[] primaryBefore = File.ReadAllBytes(fixture.FilePath);
                var fileSystem = new FaultInjectingProfileFileSystem
                {
                    FailReplace = true
                };
                EncryptedFileDemoProgressStore store =
                    fixture.CreateStore(fileSystem: fileSystem);

                ProfileInitializationResult result =
                    store.Initialize(8, "sage", 15);

                Assert.That(
                    result.LoadStatus,
                    Is.EqualTo(ProfileLoadStatus.IoError));
                Assert.That(result.IsReady, Is.False);
                Assert.That(store.IsInitialized, Is.False);
                Assert.That(
                    File.ReadAllBytes(fixture.FilePath),
                    Is.EqualTo(primaryBefore));
            }
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        [TestCase("v1", ProfileLoadStatus.LoadedV1)]
        [TestCase("v99", ProfileLoadStatus.UnsupportedVersion)]
        public void DevelopmentFixtureUsesConfiguredProtectionAndAssociatedData(
            string fixtureName,
            ProfileLoadStatus expectedStatus)
        {
            using (var fixture = new SecureStoreFixture())
            {
                bool wrote =
                    DevelopmentProfileFixtureInjector.TryWriteFixture(
                        fixtureName,
                        fixture.FilePath,
                        fixture.Protector,
                        AssociatedData,
                        new SystemProfileFileSystem(),
                        out string checksum);

                Assert.That(wrote, Is.True);
                Assert.That(checksum, Is.Not.Empty);
                byte[] protectedData =
                    File.ReadAllBytes(fixture.FilePath);
                DataUnprotectStatus protectionStatus =
                    fixture.Protector.TryUnprotect(
                        protectedData,
                        AssociatedData,
                        out byte[] plaintext);
                try
                {
                    Assert.That(
                        protectionStatus,
                        Is.EqualTo(DataUnprotectStatus.Success));
                    ProfileLoadStatus decodeStatus =
                        new SecureProfileCodec().TryDecode(
                            plaintext,
                            out SecureProfileV1Dto _,
                            out DemoProgressSnapshot _);
                    Assert.That(
                        decodeStatus,
                        Is.EqualTo(expectedStatus));
                }
                finally
                {
                    Array.Clear(
                        protectedData,
                        0,
                        protectedData.Length);
                    if (plaintext != null)
                    {
                        Array.Clear(
                            plaintext,
                            0,
                            plaintext.Length);
                    }
                }
            }
        }
#endif

        private static DemoProgressSnapshot CreateSnapshot(int tokens)
        {
            return new DemoProgressSnapshot(
                0,
                0,
                "sage",
                true,
                tokens,
                0,
                new[] { "soft-fern", "future.unknown" },
                new[]
                {
                    new DecorationSelection(
                        "workbench-accent",
                        "future.unknown")
                },
                new string[0],
                new string[0],
                new string[0],
                new PendingPresentationEntry[0],
                0,
                0);
        }

        private static void CorruptOneByte(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Length, Is.GreaterThan(8));
            bytes[bytes.Length / 2] ^= 0x5A;
            File.WriteAllBytes(path, bytes);
            Array.Clear(bytes, 0, bytes.Length);
        }

        private static void CorruptEnvelopeVersionByte(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Length, Is.GreaterThan(0));
            bytes[0] = unchecked((byte)(bytes[0] + 1));
            File.WriteAllBytes(path, bytes);
            Array.Clear(bytes, 0, bytes.Length);
        }

        private sealed class SecureStoreFixture : IDisposable
        {
            private readonly string _directory;
            private readonly ManagedAuthenticatedDataProtector _protector;

            public SecureStoreFixture()
            {
                _directory = Path.Combine(
                    Path.GetTempPath(),
                    "CalmSpaceSecureProfileTests",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_directory);
                _protector =
                    new ManagedAuthenticatedDataProtector(Secret);
                FilePath = Path.Combine(
                    _directory,
                    "profile.bin");
                LegacyKey =
                    "calmspace.tests.legacy." +
                    Guid.NewGuid().ToString("N");
                MigrationMarker =
                    "calmspace.tests.migration." +
                    Guid.NewGuid().ToString("N");
            }

            public string FilePath { get; }

            public IAuthenticatedDataProtector Protector => _protector;

            public string TemporaryPath => FilePath + ".tmp";

            public string BackupPath => FilePath + ".bak";

            public string LegacyKey { get; }

            public string MigrationMarker { get; }

            public EncryptedFileDemoProgressStore CreateStore()
            {
                return CreateStore(_protector);
            }

            public EncryptedFileDemoProgressStore CreateStore(
                IAuthenticatedDataProtector protector)
            {
                return CreateStore(protector, null);
            }

            public EncryptedFileDemoProgressStore CreateStore(
                IProfileFileSystem fileSystem)
            {
                return CreateStore(_protector, fileSystem);
            }

            public EncryptedFileDemoProgressStore CreateStore(
                IAuthenticatedDataProtector protector,
                IProfileFileSystem fileSystem)
            {
                return new EncryptedFileDemoProgressStore(
                    FilePath,
                    protector,
                    AssociatedData,
                    LegacyKey,
                    MigrationMarker,
                    fileSystem);
            }

            public void WriteVersionOne(
                string path,
                int completedMask,
                int tokens)
            {
                var state = new SecureProfileV1Dto
                {
                    version = 1,
                    highestUnlockedLevelIndex =
                        completedMask == 0 ? 0 : 2,
                    completedLevelMask = completedMask,
                    selectedThemeId = "sage",
                    musicEnabled = true,
                    cozyTokens = tokens,
                    rewardedLevelMask = completedMask,
                    ownedDecorationMask = 1,
                    selectedDecorationIndex = 0
                };
                WriteAuthenticatedJson(
                    path,
                    JsonUtility.ToJson(state));
            }

            public void WriteVersionTwo(
                string path,
                DemoProgressSnapshot snapshot)
            {
                byte[] plaintext =
                    new SecureProfileCodec().EncodeV2(snapshot);
                WriteAuthenticatedBytes(
                    path,
                    plaintext,
                    AssociatedData);
            }

            public void WriteAuthenticatedJson(
                string path,
                string json,
                byte[] associatedData = null)
            {
                byte[] plaintext = Encoding.UTF8.GetBytes(json);
                WriteAuthenticatedBytes(
                    path,
                    plaintext,
                    associatedData ?? AssociatedData);
            }

            private void WriteAuthenticatedBytes(
                string path,
                byte[] plaintext,
                byte[] associatedData)
            {
                byte[] protectedData = null;
                try
                {
                    protectedData =
                        _protector.Protect(
                            plaintext,
                            associatedData);
                    File.WriteAllBytes(path, protectedData);
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

            public void Dispose()
            {
                _protector.Dispose();
                PlayerPrefs.DeleteKey(LegacyKey);
                PlayerPrefs.DeleteKey(MigrationMarker);
                PlayerPrefs.Save();

                try
                {
                    if (Directory.Exists(_directory))
                    {
                        Directory.Delete(_directory, true);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private sealed class TrackingSystemProfileFileSystem :
            IProfileFileSystem
        {
            private readonly SystemProfileFileSystem _inner =
                new SystemProfileFileSystem();

            public int WriteCount { get; private set; }

            public ProfileFilePresence GetFilePresence(string path)
            {
                return _inner.GetFilePresence(path);
            }

            public long GetFileLength(string path)
            {
                return _inner.GetFileLength(path);
            }

            public byte[] ReadAllBytes(string path)
            {
                return _inner.ReadAllBytes(path);
            }

            public void CreateDirectory(string path)
            {
                _inner.CreateDirectory(path);
            }

            public void WriteAllBytesAndFlush(
                string path,
                byte[] bytes)
            {
                WriteCount++;
                _inner.WriteAllBytesAndFlush(path, bytes);
            }

            public void Replace(
                string sourcePath,
                string destinationPath,
                string backupPath)
            {
                _inner.Replace(sourcePath, destinationPath, backupPath);
            }

            public void Move(
                string sourcePath,
                string destinationPath)
            {
                _inner.Move(sourcePath, destinationPath);
            }

            public void Delete(string path)
            {
                _inner.Delete(path);
            }
        }

        private sealed class FaultInjectingProfileFileSystem :
            IProfileFileSystem
        {
            public string UnreadablePath { get; set; }

            public bool FailWrites { get; set; }

            public bool FailReplace { get; set; }

            public int WriteCount { get; private set; }

            public ProfileFilePresence GetFilePresence(string path)
            {
                return File.Exists(path)
                    ? ProfileFilePresence.Exists
                    : ProfileFilePresence.Missing;
            }

            public long GetFileLength(string path)
            {
                return new FileInfo(path).Length;
            }

            public byte[] ReadAllBytes(string path)
            {
                if (string.Equals(
                        path,
                        UnreadablePath,
                        StringComparison.Ordinal))
                {
                    throw new IOException("Synthetic unreadable profile.");
                }

                return File.ReadAllBytes(path);
            }

            public void CreateDirectory(string path)
            {
                Directory.CreateDirectory(path);
            }

            public void WriteAllBytesAndFlush(
                string path,
                byte[] bytes)
            {
                WriteCount++;
                if (FailWrites)
                {
                    throw new IOException("Synthetic profile write failure.");
                }

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

            public void Replace(
                string sourcePath,
                string destinationPath,
                string backupPath)
            {
                if (FailReplace)
                {
                    throw new IOException(
                        "Synthetic profile replacement failure.");
                }

                File.Replace(
                    sourcePath,
                    destinationPath,
                    backupPath);
            }

            public void Move(
                string sourcePath,
                string destinationPath)
            {
                File.Move(sourcePath, destinationPath);
            }

            public void Delete(string path)
            {
                File.Delete(path);
            }
        }

        private sealed class SwitchableAuthenticatedDataProtector :
            IAuthenticatedDataProtector,
            IDisposable
        {
            private readonly ManagedAuthenticatedDataProtector _inner;

            public SwitchableAuthenticatedDataProtector(byte[] secret)
            {
                _inner =
                    new ManagedAuthenticatedDataProtector(secret);
            }

            public bool ThrowOnProtect { get; set; }

            public byte[] Protect(
                byte[] plaintext,
                byte[] associatedData)
            {
                if (ThrowOnProtect)
                {
                    throw new InvalidOperationException(
                        "Synthetic protected-write failure.");
                }

                return _inner.Protect(
                    plaintext,
                    associatedData);
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
        }
    }
}
