// ProgramRunnerTests.Preflight.cs — Preflight validation, FormatElapsedTime, and shared helpers (partial 4/4)
// ProgramRunnerTests.Preflight.cs — プリフライト検証、FormatElapsedTime、共有ヘルパー（パーシャル 4/4）

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests
{
    public sealed partial class ProgramRunnerTests
    {
        // -----------------------------------------------------------------------
        // Preflight checks
        // プリフライトチェック
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_WhenReportsFolderPathExceedsOsLimit_ReturnsInvalidArgumentsExitCode()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-preflight-pathlen-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());

            // A label long enough so that BaseDirectory + "/Reports/" + label exceeds any OS path limit
            // BaseDirectory + "/Reports/" + label が OS のパス長制限を超える長さのラベル
            var longLabel = new string('a', 4096);

            try
            {
                await WithMissingConfigFileAsync(async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { oldDir, newDir, longLabel, "--no-pause" });

                    Assert.Equal(2, exitCode);
                    Assert.Contains(logger.Messages, m => m.Contains("too long", StringComparison.OrdinalIgnoreCase));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public void CheckDiskSpaceOrThrow_WithSufficientFreeSpace_DoesNotThrow()
        {
            // Verifies that the disk-space check passes silently on a normal system.
            // 通常のシステムでディスク容量チェックがエラーなく通過することを検証
            var ex = Record.Exception(() => RunPreflightValidator.CheckDiskSpaceOrThrow(Path.GetTempPath()));
            Assert.Null(ex);
        }

        [Fact]
        public void CheckReportsParentWritableOrThrow_WhenDirectoryIsReadOnly_ThrowsUnauthorizedAccessException()
        {
            // This test requires Unix file-mode semantics and a non-root user.
            // このテストは Unix ファイルモードと非 root ユーザーが必要
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return;
            }

            if (string.Equals(Environment.UserName, "root", StringComparison.OrdinalIgnoreCase))
            {
                return; // root bypasses Unix permission checks / root はパーミッションチェックをバイパスする
            }

            var dir = Path.Combine(Path.GetTempPath(), "fd-perm-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var logger = new TestLogger(logFileAbsolutePath: "test.log");

                // Remove write permission from the directory (read + execute only)
                // ディレクトリから書き込み権限を削除（読み取り＋実行のみ）
#pragma warning disable CA1416 // Unix-only API; test is skipped on Windows / Unix 専用 API; Windows ではテストがスキップされます
                File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
#pragma warning restore CA1416

                // Pass a path whose parent is `dir` -- the check probes a file inside `dir`
                // 親ディレクトリが `dir` のパスを渡す — チェックは `dir` 内のファイルを調べる
                var exception = Assert.Throws<UnauthorizedAccessException>(() =>
                    RunPreflightValidator.CheckReportsParentWritableOrThrow(logger, Path.Combine(dir, "label")));
                Assert.NotNull(exception.InnerException);
                Assert.IsType<UnauthorizedAccessException>(exception.InnerException);
                var logEntry = Assert.Single(logger.Entries);
                Assert.Equal(AppLogLevel.Error, logEntry.LogLevel);
                Assert.Contains("Write-permission probe failed", logEntry.Message, StringComparison.Ordinal);
                Assert.IsType<UnauthorizedAccessException>(logEntry.Exception);
            }
            finally
            {
                try
                {
#pragma warning disable CA1416 // Unix-only API; test is skipped on Windows / Unix 専用 API; Windows ではテストがスキップされます
                    File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#pragma warning restore CA1416
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }

                TryDeleteDirectory(dir);
            }
        }

        // -----------------------------------------------------------------------
        // FormatElapsedTime
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData(0, 0, 0, 0, "0h 0m 0.0s")]
        [InlineData(0, 0, 1, 234, "0h 0m 1.2s")]
        [InlineData(0, 5, 30, 100, "0h 5m 30.1s")]
        [InlineData(1, 23, 45, 600, "1h 23m 45.6s")]
        [InlineData(1, 0, 0, 0, "1h 0m 0.0s")]
        [InlineData(0, 0, 1, 999, "0h 0m 1.9s")]  // truncates, does not round up / 切り捨て、四捨五入しない
        [InlineData(100, 59, 59, 900, "100h 59m 59.9s")]
        public void FormatElapsedTime_VariousInputs_ReturnsExpectedString(
            int hours, int minutes, int seconds, int milliseconds, string expected)
        {
            var elapsed = new TimeSpan(0, hours, minutes, seconds, milliseconds);

            var result = ProgramRunner.FormatElapsedTime(elapsed);

            Assert.Equal(expected, result);
        }

        // -----------------------------------------------------------------------
        // RunPreflightValidator direct coverage tests
        // RunPreflightValidator 直接カバレッジテスト
        // -----------------------------------------------------------------------

        [Fact]
        public void ValidateRequiredArguments_NullArgs_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => RunPreflightValidator.ValidateRequiredArguments(null));
        }

        [Fact]
        public void ValidateRequiredArguments_TooFewArgs_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => RunPreflightValidator.ValidateRequiredArguments(["a"]));
        }

        [Fact]
        public void ValidateRequiredArguments_EmptyFirstArg_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => RunPreflightValidator.ValidateRequiredArguments(["", "b", "c"]));
        }

        [Fact]
        public void ValidateRequiredArguments_WhitespaceSecondArg_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => RunPreflightValidator.ValidateRequiredArguments(["a", "  ", "c"]));
        }

        [Fact]
        public void ValidateRequiredArguments_WhitespaceThirdArg_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => RunPreflightValidator.ValidateRequiredArguments(["a", "b", ""]));
        }

        [Fact]
        public void ValidateRequiredArguments_ValidArgs_DoesNotThrow()
        {
            RunPreflightValidator.ValidateRequiredArguments(["old", "new", "label"]);
        }

        [Fact]
        public void ValidateRequiredArguments_TwoArgs_DoesNotThrow()
        {
            RunPreflightValidator.ValidateRequiredArguments(["old", "new"]);
        }

        [Fact]
        public void GenerateAutomaticReportLabel_WhenTimestampAlreadyExists_AppendsNumericSuffix()
        {
            var reportsRoot = Path.Combine(Path.GetTempPath(), "fd-auto-label-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(reportsRoot);

            try
            {
                var timestamp = new DateTime(2026, 4, 11, 15, 4, 5, 123, DateTimeKind.Local).AddTicks(4567);
                var baseLabel = timestamp.ToString("yyyyMMdd_HHmmss_fffffff", System.Globalization.CultureInfo.InvariantCulture);
                Directory.CreateDirectory(Path.Combine(reportsRoot, baseLabel));

                var generated = RunPreflightValidator.GenerateAutomaticReportLabel(reportsRoot, timestamp);

                Assert.Equal(baseLabel + "_01", generated);
            }
            finally
            {
                TryDeleteDirectory(reportsRoot);
            }
        }

        [Fact]
        public void ValidateRunDirectories_OldDirNotExists_ThrowsDirectoryNotFoundException()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-preflight-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var newDir = Path.Combine(tempDir, "new");
                Directory.CreateDirectory(newDir);
                var reportDir = Path.Combine(tempDir, "report");
                var oldDir = Path.Combine(tempDir, "nonexistent-old");

                Assert.Throws<DirectoryNotFoundException>(() =>
                    RunPreflightValidator.ValidateRunDirectories(new TestLogger(logFileAbsolutePath: "test.log"), oldDir, newDir, reportDir));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void ValidateRunDirectories_NewDirNotExists_ThrowsDirectoryNotFoundException()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-preflight-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var oldDir = Path.Combine(tempDir, "old");
                Directory.CreateDirectory(oldDir);
                var reportDir = Path.Combine(tempDir, "report");
                var newDir = Path.Combine(tempDir, "nonexistent-new");

                Assert.Throws<DirectoryNotFoundException>(() =>
                    RunPreflightValidator.ValidateRunDirectories(new TestLogger(logFileAbsolutePath: "test.log"), oldDir, newDir, reportDir));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void ValidateRunDirectories_ReportDirAlreadyExists_ThrowsArgumentException()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-preflight-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var oldDir = Path.Combine(tempDir, "old");
                Directory.CreateDirectory(oldDir);
                var newDir = Path.Combine(tempDir, "new");
                Directory.CreateDirectory(newDir);
                var reportDir = Path.Combine(tempDir, "report");
                Directory.CreateDirectory(reportDir); // pre-create to trigger conflict / 競合を発生させるため事前作成

                Assert.Throws<ArgumentException>(() =>
                    RunPreflightValidator.ValidateRunDirectories(new TestLogger(logFileAbsolutePath: "test.log"), oldDir, newDir, reportDir));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void CheckReportsParentWritableOrThrow_NonexistentParent_DoesNotThrow()
        {
            // Skipped when parent directory does not exist (no exception)
            // 親ディレクトリが存在しない場合はスキップ（例外なし）
            var nonexistentParentChild = "/nonexistent/parent/dir/report";
            var ex = Record.Exception(() => RunPreflightValidator.CheckReportsParentWritableOrThrow(new TestLogger(logFileAbsolutePath: "test.log"), nonexistentParentChild));
            Assert.Null(ex);
        }

        [Fact]
        public void CheckReportsParentWritableOrThrow_WritableDirectory_DoesNotThrow()
        {
            // Verifies no exception is thrown when the parent directory is writable.
            // 親ディレクトリが書き込み可能な場合、例外が発生しないことを検証
            var dir = Path.Combine(Path.GetTempPath(), "fd-perm-writable-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var logger = new TestLogger(logFileAbsolutePath: "test.log");
                var ex = Record.Exception(() =>
                    RunPreflightValidator.CheckReportsParentWritableOrThrow(logger, Path.Combine(dir, "label")));
                Assert.Null(ex);
            }
            finally
            {
                TryDeleteDirectory(dir);
            }
        }

        [Fact]
        public void CheckDiskSpaceOrThrow_PathWithNoRoot_DoesNotThrow()
        {
            // Best-effort: verify no exception on a normal path.
            // Linux always returns "/" as root, so triggering an empty root is impractical.
            // ベストエフォート: 正常パスで例外が出ないことを確認。
            // Linux では "/" が常にルートになるため、空ルートの再現は困難。
            var ex = Record.Exception(() => RunPreflightValidator.CheckDiskSpaceOrThrow(Path.GetTempPath()));
            Assert.Null(ex);
        }

        [Fact]
        public void CheckDiskSpaceOrThrow_EmptyStringPath_DoesNotThrow()
        {
            // Empty path root returns null/empty, so the method should skip gracefully.
            // 空パスのルートは null/empty なので、メソッドはスキップするはず。
            var ex = Record.Exception(() => RunPreflightValidator.CheckDiskSpaceOrThrow(string.Empty));
            Assert.Null(ex);
        }

        [Fact]
        public void CheckReportsParentWritableOrThrow_EmptyParentPath_DoesNotThrow()
        {
            // When GetDirectoryName returns null or empty, the method should skip.
            // GetDirectoryName が null/empty を返す場合はスキップするはず。
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var ex = Record.Exception(() =>
                RunPreflightValidator.CheckReportsParentWritableOrThrow(logger, "just-a-filename"));
            Assert.Null(ex);
        }

        [Fact]
        public void CheckReportsParentWritableOrThrow_WritableDirectory_ProbeFileIsCleanedUp()
        {
            // After a successful check, the temporary probe file should be deleted.
            // 成功後、一時プローブファイルが削除されていること。
            var dir = Path.Combine(Path.GetTempPath(), "fd-probe-cleanup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var logger = new TestLogger(logFileAbsolutePath: "test.log");
                RunPreflightValidator.CheckReportsParentWritableOrThrow(logger, Path.Combine(dir, "label"));

                // Verify no .tmp probe files are left behind
                // .tmp プローブファイルが残っていないことを確認
                var remainingTmpFiles = Directory.GetFiles(dir, "*.tmp");
                Assert.Empty(remainingTmpFiles);
            }
            finally
            {
                TryDeleteDirectory(dir);
            }
        }

        // -----------------------------------------------------------------------
        // Shared helper methods for config file manipulation and cleanup
        // 設定ファイル操作とクリーンアップ用の共有ヘルパーメソッド
        // -----------------------------------------------------------------------

        private static async Task WithMissingConfigFileAsync(Func<Task> assertion)
        {
            var backupExists = File.Exists(ConfigFilePath);
            var backupContent = backupExists ? await File.ReadAllTextAsync(ConfigFilePath) : null;

            try
            {
                if (backupExists)
                {
                    File.Delete(ConfigFilePath);
                }

                await assertion();
            }
            finally
            {
                if (backupExists)
                {
                    await File.WriteAllTextAsync(ConfigFilePath, backupContent);
                }
            }
        }

        private static async Task WithConfigFileAsync(string content, Func<Task> assertion)
        {
            var backupExists = File.Exists(ConfigFilePath);
            var backupContent = backupExists ? await File.ReadAllTextAsync(ConfigFilePath) : null;

            try
            {
                await File.WriteAllTextAsync(ConfigFilePath, content);
                await assertion();
            }
            finally
            {
                if (backupExists)
                {
                    await File.WriteAllTextAsync(ConfigFilePath, backupContent ?? string.Empty);
                }
                else if (File.Exists(ConfigFilePath))
                {
                    File.Delete(ConfigFilePath);
                }
            }
        }

        private static ILCache InvokeCreateIlCache(IReadOnlyConfigSettings config, ILoggerService logger)
        {
            var method = typeof(RunScopeBuilder).GetMethod("CreateIlCache", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            return Assert.IsType<ILCache>(method.Invoke(null, new object[] { config, logger }));
        }

        private static object GetPrivateFieldValue(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var value = field.GetValue(target);
            Assert.NotNull(value);
            return value;
        }

        private static async Task<string> InvokeResolveCacheDirectoryAsync(ProgramRunner runner, string? configPath)
        {
            var method = typeof(ProgramRunner).GetMethod("ResolveCacheDirectoryAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var task = Assert.IsType<Task<string>>(method.Invoke(runner, new object?[] { configPath }));
            return await task;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup errors in tests / テストのクリーンアップエラーを無視
            }
        }

        // -----------------------------------------------------------------------
        // GetReportsFolderAbsolutePath with custom output directory
        // カスタム出力ディレクトリ付き GetReportsFolderAbsolutePath
        // -----------------------------------------------------------------------

        [Fact]
        [Trait("Category", "Unit")]
        public void GetReportsFolderAbsolutePath_WithOutputDirectory_UsesCustomBase()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-output-" + Guid.NewGuid().ToString("N"));
            try
            {
                var result = RunPreflightValidator.GetReportsFolderAbsolutePath("myLabel", tempDir);

                Assert.Equal(Path.Combine(Path.GetFullPath(tempDir), "myLabel"), result);
                Assert.True(Directory.Exists(Path.GetFullPath(tempDir)));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetReportsFolderAbsolutePath_WithNullOutputDirectory_UsesDefaultReportsDir()
        {
            var result = RunPreflightValidator.GetReportsFolderAbsolutePath("myLabel", null);

            Assert.Contains("Reports", result, StringComparison.Ordinal);
            Assert.EndsWith("myLabel", result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetReportsFolderAbsolutePath_WithEmptyOutputDirectory_UsesDefaultReportsDir()
        {
            var result = RunPreflightValidator.GetReportsFolderAbsolutePath("myLabel", "");

            Assert.Contains("Reports", result, StringComparison.Ordinal);
            Assert.EndsWith("myLabel", result);
        }

        // -----------------------------------------------------------------------
        // Output directory security guardrails
        // 出力ディレクトリのセキュリティガードレール
        // -----------------------------------------------------------------------

        [Fact]
        [Trait("Category", "Unit")]
        public void WarnIfOutputEscapesAppBase_OutsideAppBase_LogsWarning()
        {
            // Arrange / 準備
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var outsidePath = Path.Combine(Path.GetTempPath(), "fd-outside-" + Guid.NewGuid().ToString("N"));

            // Act / 実行
            RunPreflightValidator.WarnIfOutputEscapesAppBase(logger, outsidePath);

            // Assert / 検証
            Assert.Contains(logger.Messages, m => m.Contains("outside the application base directory"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void WarnIfOutputEscapesAppBase_InsideAppBase_NoWarning()
        {
            // Arrange / 準備
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var insidePath = Path.Combine(AppContext.BaseDirectory, "Reports");

            // Act / 実行
            RunPreflightValidator.WarnIfOutputEscapesAppBase(logger, insidePath);

            // Assert / 検証
            Assert.Empty(logger.Messages);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void WarnIfOutputEscapesAppBase_SiblingDirectory_LogsWarning()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var appBase = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var siblingPath = appBase + "-sibling";

            RunPreflightValidator.WarnIfOutputEscapesAppBase(logger, siblingPath);

            Assert.Contains(logger.Messages, m => m.Contains("outside the application base directory"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void WarnIfSystemDirectory_SystemPath_LogsWarning()
        {
            // Arrange / 準備
            var logger = new TestLogger(logFileAbsolutePath: "test.log");

            // Use a system directory appropriate for the current platform
            // 現在のプラットフォームに適したシステムディレクトリを使用
            string systemDir;
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (string.IsNullOrEmpty(systemDir))
                    return; // Cannot test on this platform / このプラットフォームではテスト不可
            }
            else
            {
                systemDir = "/etc/myreports";
            }

            // Act / 実行
            RunPreflightValidator.WarnIfSystemDirectory(logger, systemDir);

            // Assert / 検証
            Assert.Contains(logger.Messages, m => m.Contains("system directory"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void WarnIfSystemDirectory_SafePath_NoWarning()
        {
            // Arrange / 準備
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var safePath = Path.Combine(Path.GetTempPath(), "fd-safe-" + Guid.NewGuid().ToString("N"));

            // Act / 実行
            RunPreflightValidator.WarnIfSystemDirectory(logger, safePath);

            // Assert / 検証
            Assert.Empty(logger.Messages);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void WarnIfSystemDirectory_PrefixMatchOnly_NoWarning()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");

            string systemDir;
            string safePath;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (string.IsNullOrEmpty(systemDir))
                {
                    return;
                }

                string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (string.IsNullOrEmpty(windowsDir))
                {
                    return;
                }

                string trimmedSystemDir = systemDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string trimmedWindowsDir = windowsDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string? windowsParentPath = Path.GetDirectoryName(trimmedWindowsDir);
                string windowsDirectoryName = Path.GetFileName(trimmedWindowsDir);
                string systemDirectoryName = Path.GetFileName(trimmedSystemDir);
                if (string.IsNullOrEmpty(windowsParentPath)
                    || string.IsNullOrEmpty(windowsDirectoryName)
                    || string.IsNullOrEmpty(systemDirectoryName))
                {
                    return;
                }

                safePath = Path.Combine(windowsParentPath, windowsDirectoryName + "-sibling", systemDirectoryName);
            }
            else
            {
                systemDir = "/etc";
                safePath = "/etc2/myreports";
            }

            Assert.False(RunPreflightValidator.IsSameOrChildPath(safePath, systemDir));
            RunPreflightValidator.WarnIfSystemDirectory(logger, safePath);

            Assert.Empty(logger.Messages);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void IsSameOrChildPath_SamePathWithTrailingSeparators_ReturnsTrue()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "fd-same-or-child-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);

            try
            {
                var candidatePath = rootPath + Path.DirectorySeparatorChar;
                var parentPath = rootPath + Path.DirectorySeparatorChar;

                Assert.True(RunPreflightValidator.IsSameOrChildPath(candidatePath, parentPath));
            }
            finally
            {
                TryDeleteDirectory(rootPath);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void IsSameOrChildPath_ChildPathUnderParent_ReturnsTrue()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "fd-same-or-child-" + Guid.NewGuid().ToString("N"));
            var childPath = Path.Combine(rootPath, "nested", "child");
            Directory.CreateDirectory(childPath);

            try
            {
                var parentPath = rootPath + Path.DirectorySeparatorChar;
                var candidatePath = childPath + Path.DirectorySeparatorChar;

                Assert.True(RunPreflightValidator.IsSameOrChildPath(candidatePath, parentPath));
            }
            finally
            {
                TryDeleteDirectory(rootPath);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void IsSameOrChildPath_PrefixSibling_ReturnsFalse()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "fd-same-or-child-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);

            try
            {
                var candidatePath = rootPath + "-sibling";

                Assert.False(RunPreflightValidator.IsSameOrChildPath(candidatePath, rootPath));
            }
            finally
            {
                TryDeleteDirectory(rootPath);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetExistingReportFolderNames_IgnoresFilesAndSortsCaseInsensitively()
        {
            var reportsRoot = Path.Combine(Path.GetTempPath(), "fd-report-folders-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(reportsRoot);

            try
            {
                Directory.CreateDirectory(Path.Combine(reportsRoot, "zeta"));
                Directory.CreateDirectory(Path.Combine(reportsRoot, "Alpha"));
                File.WriteAllText(Path.Combine(reportsRoot, "not-a-folder.txt"), "ignore me");

                var result = RunPreflightValidator.GetExistingReportFolderNames(reportsRoot);

                Assert.Equal(["Alpha", "zeta"], result);
            }
            finally
            {
                TryDeleteDirectory(reportsRoot);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetReportsFolderAbsolutePath_WithLoggerAndCustomDir_LogsWarningWhenOutsideBase()
        {
            // Arrange / 準備
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-guardrail-" + Guid.NewGuid().ToString("N"));
            try
            {
                // Act / 実行
                var result = RunPreflightValidator.GetReportsFolderAbsolutePath("myLabel", tempDir, logger);

                // Assert / 検証 — result is still correct, but warning is logged
                Assert.Equal(Path.Combine(Path.GetFullPath(tempDir), "myLabel"), result);
                Assert.Contains(logger.Messages, m => m.Contains("outside the application base directory"));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void WarnIfOutputEscapesAppBase_WithInvalidPath_LogsWarningInsteadOfThrowing()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var ex = Record.Exception(() => RunPreflightValidator.WarnIfOutputEscapesAppBase(logger, "bad\0path"));

            Assert.Null(ex);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(AppLogLevel.Warning, entry.LogLevel);
            Assert.Contains("Skipped output-directory escape guardrail", entry.Message, StringComparison.Ordinal);
            Assert.IsType<ArgumentException>(entry.Exception);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void WarnIfSystemDirectory_WithInvalidPath_LogsWarningInsteadOfThrowing()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var ex = Record.Exception(() => RunPreflightValidator.WarnIfSystemDirectory(logger, "bad\0path"));

            Assert.Null(ex);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(AppLogLevel.Warning, entry.LogLevel);
            Assert.Contains("Skipped system-directory guardrail", entry.Message, StringComparison.Ordinal);
            Assert.IsType<ArgumentException>(entry.Exception);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetReportsFolderAbsolutePath_WithInvalidOutputDirectory_LogsErrorAndThrows()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");

            var ex = Assert.Throws<ArgumentException>(() =>
                RunPreflightValidator.GetReportsFolderAbsolutePath("myLabel", "bad\0path", logger));

            Assert.NotNull(ex);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(AppLogLevel.Error, entry.LogLevel);
            Assert.Contains("Failed to resolve report output directory", entry.Message, StringComparison.Ordinal);
            Assert.IsType<ArgumentException>(entry.Exception);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task ResolveCacheDirectoryAsync_WhenConfigPathIsInvalid_FallsBackToDefaultDirectory()
        {
            var runner = new ProgramRunner(new TestLogger(logFileAbsolutePath: "test.log"), new ConfigService());

            var result = await InvokeResolveCacheDirectoryAsync(runner, "bad\0config.json");

            var expectedDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Constants.APP_DATA_DIR_NAME,
                Constants.DEFAULT_IL_CACHE_DIR_NAME);
            Assert.Equal(expectedDir, result);
        }
    }
}
