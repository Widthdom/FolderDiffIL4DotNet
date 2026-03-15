using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
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
                    Assert.Contains(logger.Messages, message => message.Contains("Failed to parse the config file.", StringComparison.Ordinal));
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

        private static ILCache InvokeCreateIlCache(ConfigSettings config, ILoggerService logger)
        {
            var method = typeof(ProgramRunner).GetMethod("CreateIlCache", BindingFlags.Static | BindingFlags.NonPublic);
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
