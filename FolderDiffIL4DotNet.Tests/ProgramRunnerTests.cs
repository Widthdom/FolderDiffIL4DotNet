// ProgramRunnerTests.cs — Argument validation and IL cache tests (partial 1/4)
// ProgramRunnerTests.cs — 引数検証と IL キャッシュテスト（パーシャル 1/4）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    [Collection("ConsoleOutput NonParallel")]
    public sealed partial class ProgramRunnerTests
    {
        [Fact]
        public async Task RunAsync_WithInvalidReportLabel_ReturnsErrorBeforeTryingToLoadConfig()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-runner-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            var missingConfigPath = Path.Combine(tempRoot, "missing-config.json");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            using var appDataScope = CreateAppDataOverrideScope();

            try
            {
                var exitCode = await runner.RunAsync(new[] { oldDir, newDir, "invalid/name", "--config", missingConfigPath, "--no-pause" });

                Assert.Equal(2, exitCode);
                Assert.Equal(
                    1,
                    logger.Entries.Count(entry =>
                        entry.LogLevel == AppLogLevel.Error &&
                        entry.Message.Contains("provided as the third argument", StringComparison.Ordinal)));
                var errorEntry = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Error);
                Assert.Contains("provided as the third argument", errorEntry.Message, StringComparison.Ordinal);
                Assert.Contains("invalid character", errorEntry.Message, StringComparison.Ordinal);
                Assert.Equal(1, errorEntry.Message.Split("(Parameter 'reportLabel')", StringSplitOptions.None).Length - 1);
                var exception = Assert.IsType<ArgumentException>(errorEntry.Exception);
                Assert.Null(exception.ParamName);
                Assert.Equal("reportLabel", Assert.IsType<ArgumentException>(exception.InnerException).ParamName);
                Assert.DoesNotContain(logger.Messages, message => message.Contains("Config file not found", StringComparison.Ordinal));
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task RunAsync_WithExistingReportDirectory_ReturnsErrorBeforeTryingToLoadConfig()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-runner-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            var reportLabel = "existing_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            using var appDataScope = CreateAppDataOverrideScope();
            var reportDir = Path.Combine(appDataScope.ReportsRootAbsolutePath, reportLabel);
            Directory.CreateDirectory(reportDir);
            var missingConfigPath = Path.Combine(tempRoot, "missing-config.json");
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                var exitCode = await runner.RunAsync(new[] { oldDir, newDir, reportLabel, "--config", missingConfigPath, "--no-pause" });

                Assert.Equal(2, exitCode);
                Assert.Contains(logger.Messages, message => message.Contains("The report folder already exists:", StringComparison.Ordinal));
                Assert.DoesNotContain(logger.Messages, message => message.Contains("Config file not found", StringComparison.Ordinal));
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(reportDir);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task RunAsync_WithMissingExplicitConfigFile_ReturnsConfigurationErrorExitCode()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-runner-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old-config-missing");
            var newDir = Path.Combine(tempRoot, "new-config-missing");
            var missingConfigPath = Path.Combine(tempRoot, "missing-config.json");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            using var appDataScope = CreateAppDataOverrideScope();

            try
            {
                var exitCode = await runner.RunAsync(new[] { oldDir, newDir, "report_" + Guid.NewGuid().ToString("N"), "--config", missingConfigPath, "--no-pause" });

                Assert.Equal(3, exitCode);
                Assert.Contains(logger.Messages, message => message.Contains("Config file not found", StringComparison.Ordinal));
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task RunAsync_WithOmittedReportLabel_UsesAutoGeneratedLabelBeforeLoadingConfig()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-runner-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old-auto-label");
            var newDir = Path.Combine(tempRoot, "new-auto-label");
            var missingConfigPath = Path.Combine(tempRoot, "missing-config.json");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            using var appDataScope = CreateAppDataOverrideScope();

            try
            {
                var exitCode = await runner.RunAsync(new[] { oldDir, newDir, "--config", missingConfigPath, "--no-pause" });

                Assert.Equal(3, exitCode);
                Assert.Contains(logger.Messages, message => message.Contains("Using auto-generated label:", StringComparison.Ordinal));
                Assert.DoesNotContain(logger.Messages, message => message.Contains("Insufficient arguments", StringComparison.Ordinal));
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task RunAsync_WithOptionAsThirdToken_UsesAutoGeneratedLabelInsteadOfOptionName()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-runner-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old-option-label");
            var newDir = Path.Combine(tempRoot, "new-option-label");
            var missingConfigPath = Path.Combine(tempRoot, "missing-config.json");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            using var appDataScope = CreateAppDataOverrideScope();

            try
            {
                var exitCode = await runner.RunAsync(new[] { oldDir, newDir, "--beer", "--config", missingConfigPath, "--no-pause" });

                Assert.Equal(3, exitCode);
                Assert.Contains(logger.Messages, message => message.Contains("Using auto-generated label:", StringComparison.Ordinal));
                Assert.DoesNotContain(logger.Messages, message => message.Contains("The value '--beer'", StringComparison.Ordinal));
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task RunAsync_WithInvalidConfigJson_ReturnsConfigurationErrorExitCode()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-runner-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old-config-invalid");
            var newDir = Path.Combine(tempRoot, "new-config-invalid");
            var configPath = Path.Combine(tempRoot, "invalid-config.json");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            using var appDataScope = CreateAppDataOverrideScope();

            try
            {
                await File.WriteAllTextAsync(configPath, "{ invalid-json");
                var exitCode = await runner.RunAsync(new[] { oldDir, newDir, "report_" + Guid.NewGuid().ToString("N"), "--config", configPath, "--no-pause" });

                Assert.Equal(3, exitCode);
                Assert.Contains(logger.Messages, message => message.Contains("JSON syntax error", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public void CreateIlCache_WhenStatsIntervalIsNonPositive_UsesDocumentedDefaults()
        {
            var config = new ConfigSettingsBuilder
            {
                EnableILCache = true,
                ILCacheStatsLogIntervalSeconds = 0
            }.Build();

            var cache = InvokeCreateIlCache(config, new TestLogger(logFileAbsolutePath: "test.log"));

            Assert.NotNull(cache);
            var memoryCache = GetPrivateFieldValue(cache, "_memoryCache");
            var maxEntries = Assert.IsType<int>(GetPrivateFieldValue(memoryCache, "_maxEntries"));
            var timeToLive = Assert.IsType<TimeSpan>(GetPrivateFieldValue(memoryCache, "_timeToLive"));
            var statsLogInterval = Assert.IsType<TimeSpan>(GetPrivateFieldValue(cache, "_statsLogInterval"));

            Assert.Equal(Constants.IL_CACHE_MAX_MEMORY_ENTRIES_DEFAULT, maxEntries);
            Assert.Equal(TimeSpan.FromHours(Constants.IL_CACHE_TIME_TO_LIVE_DEFAULT_HOURS), timeToLive);
            Assert.Equal(TimeSpan.FromSeconds(Constants.IL_CACHE_STATS_LOG_INTERVAL_DEFAULT_SECONDS), statsLogInterval);
        }

        [Fact]
        public void CreateIlCache_WhenPathIsEmpty_DefaultsToLocalApplicationDataSubfolder()
        {
            var config = new ConfigSettingsBuilder
            {
                EnableILCache = true,
                ILCacheDirectoryAbsolutePath = ""
            }.Build();

            var cache = InvokeCreateIlCache(config, new TestLogger(logFileAbsolutePath: "test.log"));

            Assert.NotNull(cache);
            var diskCache = GetPrivateFieldValue(cache, "_diskCache");
            var cacheDir = Assert.IsType<string>(GetPrivateFieldValue(diskCache, "_cacheDirectoryAbsolutePath"));

            var expectedDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Constants.APP_DATA_DIR_NAME,
                Constants.DEFAULT_IL_CACHE_DIR_NAME);
            Assert.Equal(expectedDir, cacheDir);
            Assert.DoesNotContain(AppContext.BaseDirectory, cacheDir);
        }

        private static AppDataOverrideScope CreateAppDataOverrideScope()
            => new(Path.Combine(Path.GetTempPath(), "fd-runner-appdata-" + Guid.NewGuid().ToString("N")));
    }
}
