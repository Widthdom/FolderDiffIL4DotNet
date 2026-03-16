using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.IO;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.IO
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

        [Fact]
        public void GetTimestamp_ExistingFile_ReturnsTimestampString()
        {
            var file = CreateTempFile("timestamp.txt", "content");

            var timestamp = FileSystemUtility.GetTimestamp(file);

            Assert.False(string.IsNullOrWhiteSpace(timestamp));
            Assert.True(
                DateTime.TryParseExact(timestamp, Constants.TIMESTAMP_WITH_TIME_ZONE_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                $"Unexpected timestamp format: {timestamp}");
        }

        [Fact]
        public void GetTimestamp_NonexistentFile_ReturnsFormattedString()
        {
            var missing = Path.Combine(_tempDir, "missing.txt");
            var timestamp = FileSystemUtility.GetTimestamp(missing);
            Assert.False(string.IsNullOrWhiteSpace(timestamp));
        }

        [Fact]
        public void GetTimestamp_Null_ThrowsOriginalArgumentException()
        {
            Assert.ThrowsAny<ArgumentException>(() => FileSystemUtility.GetTimestamp(null));
        }

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

        /// <summary>
        /// スラッシュ形式の IP ベース UNC パス（例: //192.168.1.1/share）が Windows ネットワークパスとして検出されることを確認します。
        /// IsLikelyWindowsNetworkPath は <c>\\</c> プレフィックスのみ対応していましたが、
        /// <c>//</c> 形式の UNC パスも同様にネットワークパスと判定できるよう修正された回帰テストです。
        /// </summary>
        [Fact]
        public void IsLikelyWindowsNetworkPath_ForwardSlashIpUncPath_ReturnsTrue()
        {
            var method = typeof(FileSystemUtility).GetMethod(
                "IsLikelyWindowsNetworkPath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            // //192.168.1.1/share は Windows で有効な UNC パス（スラッシュ形式）
            var result = method.Invoke(null, ["//192.168.1.1/share/folder"]);
            Assert.True(Assert.IsType<bool>(result));
        }

        /// <summary>
        /// バックスラッシュ形式の UNC パス（\\server\share）が引き続きネットワークパスとして検出されることを確認します。
        /// </summary>
        [Fact]
        public void IsLikelyWindowsNetworkPath_BackslashUncPath_ReturnsTrue()
        {
            var method = typeof(FileSystemUtility).GetMethod(
                "IsLikelyWindowsNetworkPath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var result = method.Invoke(null, [@"\\server\share\folder"]);
            Assert.True(Assert.IsType<bool>(result));
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

        [Fact]
        public void GetBestMatchingMountFileSystemType_NullInputs_ReturnsNull()
        {
            var method = typeof(FileSystemUtility).GetMethod(
                "GetBestMatchingMountFileSystemType",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            Assert.Null(method.Invoke(null, new object[] { null, new[] { "tmpfs /tmp tmpfs rw 0 0" } }));
            Assert.Null(method.Invoke(null, new object[] { "/tmp/file.txt", null }));
        }
    }
}
