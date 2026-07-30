using System;
using System.IO;

namespace CalmSpace.Demo
{
    [Serializable]
    public sealed class SecureProfileV1Dto
    {
        public int version;
        public int highestUnlockedLevelIndex;
        public int completedLevelMask;
        public string selectedThemeId;
        public bool musicEnabled;
        public int cozyTokens;
        public int rewardedLevelMask;
        public int ownedDecorationMask;
        public int selectedDecorationIndex;
    }

    [Serializable]
    public sealed class SecureDecorationSelectionDto
    {
        public string slotId;
        public string decorationId;
    }

    [Serializable]
    public sealed class SecurePendingPresentationDto
    {
        public int kind;
        public string stableId;
        public bool requiresExplicitLaunch;
    }

    [Serializable]
    public sealed class SecureProfileV2Dto
    {
        public int version;
        public int highestUnlockedLevelIndex;
        public int completedLevelMask;
        public string selectedThemeId;
        public bool musicEnabled;
        public int cozyTokens;
        public int rewardedLevelMask;
        public int ownedDecorationMask;
        public int selectedDecorationIndex;
        public string[] ownedDecorationIds;
        public SecureDecorationSelectionDto[] decorationSelections;
        public string[] seenRoomRevealIds;
        public string[] viewedMemoryIds;
        public string[] seenFinaleIds;
        public SecurePendingPresentationDto[] pendingPresentations;
        public int lastDailyCareUtcDayKey;
        public int completedDailyCareCount;
    }

    public interface IProfileFileSystem
    {
        bool FileExists(string path);

        long GetFileLength(string path);

        byte[] ReadAllBytes(string path);

        void CreateDirectory(string path);

        void WriteAllBytesAndFlush(string path, byte[] bytes);

        void Replace(
            string sourcePath,
            string destinationPath,
            string backupPath);

        void Move(string sourcePath, string destinationPath);

        void Delete(string path);
    }

    public sealed class SystemProfileFileSystem : IProfileFileSystem
    {
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public long GetFileLength(string path)
        {
            return new FileInfo(path).Length;
        }

        public byte[] ReadAllBytes(string path)
        {
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
}
