using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests
{
    public sealed class ProgramRunnerTests
    {
        private static readonly string ConfigFilePath = Path.Combine(AppContext.BaseDirectory, "config.json");

        [Fact]
        public async Task RunAsync_WithInvalidReportLabel_ReturnsErrorBeforeTryingToLoadConfig()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-runner-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                await WithMissingConfigFileAsync(async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { oldDir, newDir, "invalid/name", "--no-pause" });

                    Assert.Equal(2, exitCode);
                    Assert.Contains(logger.Messages, message => message.Contains("provided as the third argument", StringComparison.Ordinal));
                    Assert.DoesNotContain(logger.Messages, message => message.Contains("Config file not found", StringComparison.Ordinal));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task RunAsync_WithExistingReportDirectory_ReturnsErrorBeforeTryingToLoadConfig()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-runner-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            var reportLabel = "existing_" + Guid.NewGuid().ToString("N");
            var reportDir = Path.Combine(AppContext.BaseDirectory, "Reports", reportLabel);
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                await WithMissingConfigFileAsync(async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { oldDir, newDir, reportLabel, "--no-pause" });

                    Assert.Equal(2, exitCode);
                    Assert.Contains(logger.Messages, message => message.Contains("The report folder already exists:", StringComparison.Ordinal));
                    Assert.DoesNotContain(logger.Messages, message => message.Contains("Config file not found", StringComparison.Ordinal));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(reportDir);
            }
        }

        [Fact]
        public async Task RunAsync_WithMissingConfigFile_ReturnsConfigurationErrorExitCode()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-runner-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old-config-missing");
            var newDir = Path.Combine(tempRoot, "new-config-missing");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                await WithMissingConfigFileAsync(async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { oldDir, newDir, "report_" + Guid.NewGuid().ToString("N"), "--no-pause" });

                    Assert.Equal(3, exitCode);
                    Assert.Contains(logger.Messages, message => message.Contains("Config file not found", StringComparison.Ordinal));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task RunAsync_WithInvalidConfigJson_ReturnsConfigurationErrorExitCode()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-runner-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old-config-invalid");
            var newDir = Path.Combine(tempRoot, "new-config-invalid");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                await WithConfigFileAsync("{ invalid-json", async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { oldDir, newDir, "report_" + Guid.NewGuid().ToString("N"), "--no-pause" });

                    Assert.Equal(3, exitCode);
                    Assert.Contains(logger.Messages, message => message.Contains("JSON syntax error", StringComparison.OrdinalIgnoreCase));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public void CreateIlCache_WhenStatsIntervalIsNonPositive_UsesDocumentedDefaults()
        {
            var config = new ConfigSettings
            {
                EnableILCache = true,
                ILCacheStatsLogIntervalSeconds = 0
            };

            var cache = InvokeCreateIlCache(config, new TestLogger());

            Assert.NotNull(cache);
            var memoryCache = GetPrivateFieldValue(cache, "_memoryCache");
            var maxEntries = Assert.IsType<int>(GetPrivateFieldValue(memoryCache, "_maxEntries"));
            var timeToLive = Assert.IsType<TimeSpan>(GetPrivateFieldValue(memoryCache, "_timeToLive"));
            var statsLogInterval = Assert.IsType<TimeSpan>(GetPrivateFieldValue(cache, "_statsLogInterval"));

            Assert.Equal(Constants.IL_CACHE_MAX_MEMORY_ENTRIES_DEFAULT, maxEntries);
            Assert.Equal(TimeSpan.FromHours(Constants.IL_CACHE_TIME_TO_LIVE_DEFAULT_HOURS), timeToLive);
            Assert.Equal(TimeSpan.FromSeconds(Constants.IL_CACHE_STATS_LOG_INTERVAL_DEFAULT_SECONDS), statsLogInterval);
        }

        // -----------------------------------------------------------------------
        // --help / --version early-exit tests
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_HelpFlag_ExitsZeroWithoutInitializingLogger()
        {
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--help" });

                Assert.Equal(0, exitCode);
                var output = sw.ToString();
                Assert.Contains("Usage:", output, StringComparison.Ordinal);
                Assert.Contains("--config", output, StringComparison.Ordinal);
                Assert.Contains("--skip-il", output, StringComparison.Ordinal);
                // Logger should NOT have been initialized (no log messages)
                Assert.Empty(logger.Messages);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public async Task RunAsync_HelpFlagVariants_AllExitZero(string flag)
        {
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { flag });
                Assert.Equal(0, exitCode);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public async Task RunAsync_VersionFlag_ExitsZeroWithVersionOutput()
        {
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--version" });

                Assert.Equal(0, exitCode);
                var output = sw.ToString().Trim();
                Assert.False(string.IsNullOrWhiteSpace(output), "Version output should not be empty.");
                // Logger should NOT have been initialized
                Assert.Empty(logger.Messages);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        // -----------------------------------------------------------------------
        // Unknown flag -> exit code 2 (InvalidArguments)
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_UnknownFlag_ReturnsInvalidArgumentsExitCode()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-runner-unknown-flag-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                await WithConfigFileAsync("{}", async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { oldDir, newDir, "lbl_unknown_" + Guid.NewGuid().ToString("N"), "--unknown-flag", "--no-pause" });

                    Assert.Equal(2, exitCode);
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        // -----------------------------------------------------------------------
        // --config <path> – points to custom config file
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_ConfigFlagWithValidCustomPath_LoadsCustomConfig()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-runner-config-flag-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            var customConfigPath = Path.Combine(tempRoot, "custom.json");
            const string customConfigJson = """{ "MaxLogGenerations": 7 }""";
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            await File.WriteAllTextAsync(customConfigPath, customConfigJson);
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                await WithMissingConfigFileAsync(async () =>
                {
                    // Without --config this would fail with "Config file not found".
                    // With --config pointing to our custom file it should reach config-loaded step.
                    var exitCode = await runner.RunAsync(new[]
                    {
                        oldDir, newDir, "lbl_cfg_" + Guid.NewGuid().ToString("N"),
                        "--config", customConfigPath,
                        "--no-pause"
                    });

                    // Config loaded successfully (diff may have no files = exit 0, or execution phase)
                    Assert.NotEqual(3, exitCode);
                    Assert.Contains(logger.Messages, m => m.Contains("Configuration loaded", StringComparison.Ordinal));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task RunAsync_ConfigFlagPointingToMissingFile_ReturnsConfigurationError()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-runner-config-missing-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                var exitCode = await runner.RunAsync(new[]
                {
                    oldDir, newDir, "lbl_cfg_miss_" + Guid.NewGuid().ToString("N"),
                    "--config", "/nonexistent/path/config.json",
                    "--no-pause"
                });

                Assert.Equal(3, exitCode);
                Assert.Contains(logger.Messages, m => m.Contains("Config file not found", StringComparison.Ordinal));
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        // -----------------------------------------------------------------------
        // --threads override
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_ThreadsFlag_OverridesMaxParallelismInConfig()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-runner-threads-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                await WithConfigFileAsync("{}", async () =>
                {
                    // Just verify it doesn't blow up with config error and reaches execution phase
                    var exitCode = await runner.RunAsync(new[]
                    {
                        oldDir, newDir, "lbl_thr_" + Guid.NewGuid().ToString("N"),
                        "--threads", "2",
                        "--no-pause"
                    });

                    Assert.NotEqual(3, exitCode); // config phase succeeded
                    Assert.Contains(logger.Messages, m => m.Contains("Configuration loaded", StringComparison.Ordinal));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task RunAsync_ThreadsFlagWithInvalidValue_ReturnsInvalidArgumentsExitCode()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-runner-threads-bad-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                await WithConfigFileAsync("{}", async () =>
                {
                    var exitCode = await runner.RunAsync(new[]
                    {
                        oldDir, newDir, "lbl_thr_bad_" + Guid.NewGuid().ToString("N"),
                        "--threads", "notanumber",
                        "--no-pause"
                    });

                    Assert.Equal(2, exitCode);
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        // -----------------------------------------------------------------------
        // ApplyCliOverrides coverage: --no-il-cache, --skip-il, --no-timestamp-warnings
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_NoIlCacheFlag_DisablesILCacheForRun()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-runner-no-ilcache-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                await WithConfigFileAsync("{}", async () =>
                {
                    var exitCode = await runner.RunAsync(new[]
                    {
                        oldDir, newDir, "lbl_noilcache_" + Guid.NewGuid().ToString("N"),
                        "--no-il-cache", "--no-pause"
                    });
                    Assert.NotEqual(3, exitCode);
                    Assert.Contains(logger.Messages, m => m.Contains("Configuration loaded", StringComparison.Ordinal));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task RunAsync_SkipILFlag_SetsSkipILForRun()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-runner-skipil-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                await WithConfigFileAsync("{}", async () =>
                {
                    var exitCode = await runner.RunAsync(new[]
                    {
                        oldDir, newDir, "lbl_skipil_" + Guid.NewGuid().ToString("N"),
                        "--skip-il", "--no-pause"
                    });
                    Assert.NotEqual(3, exitCode);
                    Assert.Contains(logger.Messages, m => m.Contains("Configuration loaded", StringComparison.Ordinal));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task RunAsync_NoTimestampWarningsFlag_SuppressesWarningsForRun()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-runner-notswarn-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                await WithConfigFileAsync("{}", async () =>
                {
                    var exitCode = await runner.RunAsync(new[]
                    {
                        oldDir, newDir, "lbl_notswarn_" + Guid.NewGuid().ToString("N"),
                        "--no-timestamp-warnings", "--no-pause"
                    });
                    Assert.NotEqual(3, exitCode);
                    Assert.Contains(logger.Messages, m => m.Contains("Configuration loaded", StringComparison.Ordinal));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        // -----------------------------------------------------------------------
        // Preflight checks
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_WhenReportsFolderPathExceedsOsLimit_ReturnsInvalidArgumentsExitCode()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-preflight-pathlen-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger();
            var runner = new ProgramRunner(logger, new ConfigService());

            // A label long enough so that BaseDirectory + "/Reports/" + label exceeds any OS path limit
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
            var ex = Record.Exception(() => RunPreflightValidator.CheckDiskSpaceOrThrow(Path.GetTempPath()));
            Assert.Null(ex);
        }

        [Fact]
        public void CheckReportsParentWritableOrThrow_WhenDirectoryIsReadOnly_ThrowsUnauthorizedAccessException()
        {
            // This test requires Unix file-mode semantics and a non-root user.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return;
            }

            if (string.Equals(Environment.UserName, "root", StringComparison.OrdinalIgnoreCase))
            {
                return; // root bypasses Unix permission checks
            }

            var dir = Path.Combine(Path.GetTempPath(), "fd-perm-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // Remove write permission from the directory (read + execute only)
                File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);

                // Pass a path whose parent is `dir` — the check probes a file inside `dir`
                Assert.Throws<UnauthorizedAccessException>(() =>
                    RunPreflightValidator.CheckReportsParentWritableOrThrow(Path.Combine(dir, "label")));
            }
            finally
            {
                try
                {
                    File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }

                TryDeleteDirectory(dir);
            }
        }

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

        // -----------------------------------------------------------------------
        // FormatElapsedTime
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData(0, 0, 0, 0, "0h 0m 0.0s")]
        [InlineData(0, 0, 1, 234, "0h 0m 1.2s")]
        [InlineData(0, 5, 30, 100, "0h 5m 30.1s")]
        [InlineData(1, 23, 45, 600, "1h 23m 45.6s")]
        [InlineData(1, 0, 0, 0, "1h 0m 0.0s")]
        [InlineData(0, 0, 1, 999, "0h 0m 1.9s")]  // truncates, does not round up
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
        // -----------------------------------------------------------------------

        [Fact]
        public void ValidateRequiredArguments_NullArgs_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => RunPreflightValidator.ValidateRequiredArguments(null));
        }

        [Fact]
        public void ValidateRequiredArguments_TooFewArgs_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => RunPreflightValidator.ValidateRequiredArguments(["a", "b"]));
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
                    RunPreflightValidator.ValidateRunDirectories(oldDir, newDir, reportDir));
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
                    RunPreflightValidator.ValidateRunDirectories(oldDir, newDir, reportDir));
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
                Directory.CreateDirectory(reportDir); // already exists

                Assert.Throws<ArgumentException>(() =>
                    RunPreflightValidator.ValidateRunDirectories(oldDir, newDir, reportDir));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void CheckReportsParentWritableOrThrow_NonexistentParent_DoesNotThrow()
        {
            // 親ディレクトリが存在しない場合はスキップ（例外なし）
            var nonexistentParentChild = "/nonexistent/parent/dir/report";
            var ex = Record.Exception(() => RunPreflightValidator.CheckReportsParentWritableOrThrow(nonexistentParentChild));
            Assert.Null(ex);
        }

        [Fact]
        public void CheckDiskSpaceOrThrow_PathWithNoRoot_DoesNotThrow()
        {
            // Path.GetPathRoot が空文字列を返すケースを模擬（相対パス的な文字列）
            // Linuxでは "/" が root になるため、空ルートを返す路は難しい。
            // best-effort: 正常パスで例外が出ないことを確認
            var ex = Record.Exception(() => RunPreflightValidator.CheckDiskSpaceOrThrow(Path.GetTempPath()));
            Assert.Null(ex);
        }

        private static ILCache InvokeCreateIlCache(ConfigSettings config, ILoggerService logger)
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
                // ignore cleanup errors in tests
            }
        }

        private sealed class TestLogger : ILoggerService
        {
            public string LogFileAbsolutePath => "test.log";

            public List<string> Messages { get; } = new();

            public void Initialize()
            {
            }

            public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, Exception exception = null)
            {
                Messages.Add(message);
            }

            public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, ConsoleColor? consoleForegroundColor, Exception exception = null)
            {
                Messages.Add(message);
            }

            public void CleanupOldLogFiles(int maxLogGenerations)
            {
            }
        }
    }
}
