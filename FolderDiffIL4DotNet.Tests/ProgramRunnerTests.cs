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
