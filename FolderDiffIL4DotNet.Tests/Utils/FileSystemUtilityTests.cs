using System;
using System.IO;
using System.Reflection;
using FolderDiffIL4DotNet.Utils;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Utils
{
    public class FileSystemUtilityTests : IDisposable
    {
        private readonly string _tempDir;

        public FileSystemUtilityTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"FolderDiffTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        private string CreateTempFile(string name, string content)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content);
            return path;
        }

        #region DeleteFileSilent

        [Fact]
        public void DeleteFileSilent_ExistingFile_Deleted()
        {
            var file = CreateTempFile("delete_me.txt", "content");
            Assert.True(File.Exists(file));
            FileSystemUtility.DeleteFileSilent(file);
            Assert.False(File.Exists(file));
        }

        [Fact]
        public void DeleteFileSilent_NonexistentFile_DoesNotThrow()
        {
            FileSystemUtility.DeleteFileSilent(Path.Combine(_tempDir, "no_such_file.txt"));
        }

        [Fact]
        public void DeleteFileSilent_NullOrEmpty_DoesNotThrow()
        {
            FileSystemUtility.DeleteFileSilent(null);
            FileSystemUtility.DeleteFileSilent(string.Empty);
        }

        #endregion

        #region GetTimestamp

        [Fact]
        public void GetTimestamp_ExistingFile_ReturnsTimestampString()
        {
            var file = CreateTempFile("timestamp.txt", "content");

            var timestamp = FileSystemUtility.GetTimestamp(file);

            Assert.False(string.IsNullOrWhiteSpace(timestamp));
            Assert.Contains("-", timestamp);
            Assert.Contains(":", timestamp);
        }

        [Fact]
        public void GetTimestamp_NonexistentFile_ReturnsFormattedString()
        {
            var missing = Path.Combine(_tempDir, "missing.txt");
            var timestamp = FileSystemUtility.GetTimestamp(missing);
            Assert.False(string.IsNullOrWhiteSpace(timestamp));
        }

        #endregion

        #region TrySetReadOnly

        [Fact]
        public void TrySetReadOnly_ExistingFile_SetsReadOnlyAttribute()
        {
            var file = CreateTempFile("readonly.txt", "content");

            FileSystemUtility.TrySetReadOnly(file);

            var attrs = File.GetAttributes(file);
            Assert.True((attrs & FileAttributes.ReadOnly) != 0);
        }

        [Fact]
        public void TrySetReadOnly_NullOrWhitespace_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => FileSystemUtility.TrySetReadOnly(null));
            Assert.Throws<ArgumentException>(() => FileSystemUtility.TrySetReadOnly("   "));
        }

        [Fact]
        public void TrySetReadOnly_NonexistentFile_ThrowsFileNotFoundException()
        {
            var missing = Path.Combine(_tempDir, "missing-readonly.txt");
            Assert.Throws<FileNotFoundException>(() => FileSystemUtility.TrySetReadOnly(missing));
        }

        #endregion

        #region IsLikelyNetworkPath

        [Fact]
        public void IsLikelyNetworkPath_NullOrWhitespace_ReturnsFalse()
        {
            Assert.False(FileSystemUtility.IsLikelyNetworkPath(null));
            Assert.False(FileSystemUtility.IsLikelyNetworkPath(string.Empty));
            Assert.False(FileSystemUtility.IsLikelyNetworkPath("   "));
        }

        [Fact]
        public void IsLikelyNetworkPath_LocalTempPath_ReturnsFalse()
        {
            Assert.False(FileSystemUtility.IsLikelyNetworkPath(_tempDir));
        }

        [Fact]
        public void IsLikelyNetworkPath_InvalidPathCharacters_ReturnsFalse()
        {
            var invalidPath = $"invalid{'\0'}path";
            Assert.False(FileSystemUtility.IsLikelyNetworkPath(invalidPath));
        }

        [Fact]
        public void GetBestMatchingMountFileSystemType_PicksMostSpecificMountPoint()
        {
            var method = typeof(FileSystemUtility).GetMethod(
                "GetBestMatchingMountFileSystemType",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var fullPath = "/mnt/share/deep/file.txt";
            var lines = new[]
            {
                "server:/export /mnt nfs rw 0 0",
                "server:/share /mnt/share nfs4 rw 0 0",
                "/dev/disk1s1 / apfs rw 0 0",
            };

            var result = method.Invoke(null, new object[] { fullPath, lines });

            Assert.Equal("nfs4", Assert.IsType<string>(result));
        }

        [Fact]
        public void GetBestMatchingMountFileSystemType_IgnoresInvalidLines()
        {
            var method = typeof(FileSystemUtility).GetMethod(
                "GetBestMatchingMountFileSystemType",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var fullPath = "/tmp/file.txt";
            var lines = new[]
            {
                "# comment",
                "invalid",
                "tmpfs /tmp tmpfs rw 0 0"
            };

            var result = method.Invoke(null, new object[] { fullPath, lines });

            Assert.Equal("tmpfs", Assert.IsType<string>(result));
        }

        #endregion
    }
}
