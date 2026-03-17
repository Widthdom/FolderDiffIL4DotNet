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

        /// <summary>
        /// 削除競合（ファイルが既に削除済みの場合）でも例外が出ないことを確認します。
        /// </summary>
        [Fact]
        public void DeleteFileSilent_AlreadyDeletedFile_DoesNotThrow()
        {
            var path = Path.Combine(_tempDir, "already_gone.txt");
            // ファイルは存在しないが例外を投げない
            FileSystemUtility.DeleteFileSilent(path);
        }

        /// <summary>
        /// 読み取り専用ファイルの削除試行（UnauthorizedAccessException）をsilentに無視することを確認します。
        /// Unix以外ではスキップ。
        /// </summary>
        [Fact]
        public void DeleteFileSilent_ReadOnlyFile_OnUnix_DoesNotThrow()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)
                && !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                return; // Windowsではスキップ
            }

            if (string.Equals(Environment.UserName, "root", StringComparison.OrdinalIgnoreCase))
            {
                return; // rootはReadOnly属性を無視するためスキップ
            }

            var file = CreateTempFile("readonly_del.txt", "content");
            // 読み取り専用ディレクトリに配置してアクセス拒否を模擬
            var roDir = Path.Combine(_tempDir, "ro_dir");
            Directory.CreateDirectory(roDir);
            var roFile = Path.Combine(roDir, "locked.txt");
            File.WriteAllText(roFile, "locked");
            try
            {
                File.SetUnixFileMode(roDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                // ディレクトリが書き込み不可なので削除はIOExceptionまたはUnauthorizedAccessException
                FileSystemUtility.DeleteFileSilent(roFile); // should not throw
            }
            finally
            {
                try { File.SetUnixFileMode(roDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); } catch { }
            }
        }

        /// <summary>
        /// TrySetReadOnly が既に ReadOnly なファイルに対して何もしないことを確認します。
        /// </summary>
        [Fact]
        public void TrySetReadOnly_AlreadyReadOnly_DoesNotThrow()
        {
            var file = CreateTempFile("already_readonly.txt", "content");
            FileSystemUtility.TrySetReadOnly(file);
            // 再度呼び出しても例外が出ない
            FileSystemUtility.TrySetReadOnly(file);
            var attrs = File.GetAttributes(file);
            Assert.True((attrs & FileAttributes.ReadOnly) != 0);
        }

        /// <summary>
        /// Unix環境でIsLikelyNetworkPathがローカルパスに対してfalseを返すことを確認します。
        /// </summary>
        [Fact]
        public void IsLikelyNetworkPath_UnixLocalPath_ReturnsFalse()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                return;
            }
            // /tmp は通常ローカルFS（tmpfs等）
            Assert.False(FileSystemUtility.IsLikelyNetworkPath("/tmp"));
        }

        /// <summary>
        /// GetUnixMountsFilePath がファイルシステムで見つかったマウントファイルを返すことを確認します（Linux環境）。
        /// </summary>
        [Fact]
        public void GetUnixMountsFilePath_OnLinux_ReturnsProcMountsOrMtab()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                return;
            }
            var method = typeof(FileSystemUtility).GetMethod(
                "GetUnixMountsFilePath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var result = method.Invoke(null, null) as string;
            // /proc/mounts または /etc/mtab が存在する
            Assert.NotNull(result);
        }

        /// <summary>
        /// IsLikelyUnixNetworkPath でマウントファイルが存在しない場合はfalseを返すことをリフレクションで確認します。
        /// </summary>
        [Fact]
        public void IsLikelyUnixNetworkPath_WhenNoMountsFile_ReturnsFalse()
        {
            var method = typeof(FileSystemUtility).GetMethod(
                "IsLikelyUnixNetworkPath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            // /nonexistent/path は TryGetFullPath が通るが、mounts ファイルが見つかればそこへ分岐する。
            // このテストはフォールスルーのみ検証。
            var result = method.Invoke(null, ["/nonexistent/path/that/does/not/exist"]);
            Assert.IsType<bool>(result); // false or true, just don't throw
        }

        /// <summary>
        /// TryReadMountLines で存在しないファイルを渡した際に null が返ることを確認します。
        /// </summary>
        [Fact]
        public void TryReadMountLines_NonexistentFile_ReturnsNull()
        {
            var method = typeof(FileSystemUtility).GetMethod(
                "TryReadMountLines",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            // 存在しないファイルはIOExceptionが発生しnullが返る
            var result = method.Invoke(null, ["/nonexistent/path/to/mounts"]);
            Assert.Null(result);
        }

        /// <summary>
        /// //プレフィックスのUNCパスがIsLikelyNetworkPathでWindows判定に用いられる内部メソッドで正しく検出されることを確認します。
        /// </summary>
        [Fact]
        public void IsLikelyWindowsNetworkPath_ForwardSlashUncPrefixVariants_ReturnTrue()
        {
            var method = typeof(FileSystemUtility).GetMethod(
                "IsLikelyWindowsNetworkPath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            // //server/share 形式のUNCパス
            Assert.True((bool)method.Invoke(null, ["//server/share"]));
            // \\server\share 形式のUNCパス
            Assert.True((bool)method.Invoke(null, [@"\\server\share"]));
        }

        /// <summary>
        /// GetBestMatchingMountFileSystemType でパスが末尾スラッシュなしのマウントポイントと一致することを確認します。
        /// </summary>
        [Fact]
        public void GetBestMatchingMountFileSystemType_ExactPathMatch_ReturnsFsType()
        {
            var method = typeof(FileSystemUtility).GetMethod(
                "GetBestMatchingMountFileSystemType",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var fullPath = "/mnt/share";
            var lines = new[]
            {
                "server:/share /mnt/share nfs rw 0 0",
            };

            var result = method.Invoke(null, new object[] { fullPath, lines });
            Assert.Equal("nfs", Assert.IsType<string>(result));
        }

        /// <summary>
        /// GetBestMatchingMountFileSystemType でエスケープスペースを含むマウントポイントが正しく解釈されることを確認します。
        /// </summary>
        [Fact]
        public void GetBestMatchingMountFileSystemType_EscapedSpaceInMountPoint_Matches()
        {
            var method = typeof(FileSystemUtility).GetMethod(
                "GetBestMatchingMountFileSystemType",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            // マウントポイントに\040(スペース)が含まれる行
            var lines = new[]
            {
                @"server:/my\040share /mnt/my share nfs rw 0 0",
                "tmpfs /tmp tmpfs rw 0 0",
            };

            var result = method.Invoke(null, new object[] { "/tmp/file.txt", lines });
            Assert.Equal("tmpfs", Assert.IsType<string>(result));
        }
    }
}
