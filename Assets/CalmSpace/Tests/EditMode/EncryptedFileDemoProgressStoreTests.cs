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

        private static void CorruptOneByte(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Length, Is.GreaterThan(8));
            bytes[bytes.Length / 2] ^= 0x5A;
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
                return new EncryptedFileDemoProgressStore(
                    FilePath,
                    protector,
                    AssociatedData,
                    LegacyKey,
                    MigrationMarker);
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
