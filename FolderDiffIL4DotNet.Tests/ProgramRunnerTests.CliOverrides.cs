// ProgramRunnerTests.CliOverrides.cs — CLI override and --print-config tests (partial 3/4)
// ProgramRunnerTests.CliOverrides.cs — CLI オーバーライドと --print-config テスト（パーシャル 3/4）

using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests
{
    public sealed partial class ProgramRunnerTests
    {
        // -----------------------------------------------------------------------
        // --config <path> – points to custom config file
        // --config <path> – カスタム設定ファイルを指定
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
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            using var appDataScope = CreateAppDataOverrideScope();

            try
            {
                var exitCode = await runner.RunAsync(new[]
                {
                    oldDir, newDir, "lbl_cfg_" + Guid.NewGuid().ToString("N"),
                    "--config", customConfigPath,
                    "--no-pause"
                });

                // Config loaded successfully (diff may have no files = exit 0, or execution phase)
                // 設定読み込み成功（ファイルなし = exit 0、または実行フェーズ）
                Assert.NotEqual(3, exitCode);
                Assert.Contains(logger.Messages, m => m.Contains("Configuration loaded", StringComparison.Ordinal));
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
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
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
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

        [Fact]
        public async Task RunAsync_ConfigFlagWithTooLongPath_ReturnsConfigurationError()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-runner-config-long-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            string tooLongConfigPath = CreateTooLongConfigPath();
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                var exitCode = await runner.RunAsync(new[]
                {
                    oldDir, newDir, "lbl_cfg_long_" + Guid.NewGuid().ToString("N"),
                    "--config", tooLongConfigPath,
                    "--no-pause"
                });

                Assert.Equal(3, exitCode);
                Assert.Contains(logger.Messages, m => m.Contains("too long", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        // -----------------------------------------------------------------------
        // --threads override
        // --threads オーバーライド
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_ThreadsFlag_OverridesMaxParallelismInConfig()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-runner-threads-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                await WithConfigFileAsync("{}", async () =>
                {
                    // Just verify it doesn't blow up with config error and reaches execution phase
                    // 設定エラーにならず実行フェーズに到達することを検証
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
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
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
        // ApplyCliOverrides カバレッジ: --no-il-cache, --skip-il, --no-timestamp-warnings
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_NoIlCacheFlag_DisablesILCacheForRun()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-runner-no-ilcache-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
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
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
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
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
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
        // --print-config
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_PrintConfigFlag_ExitsZeroAndOutputsJson()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                await WithConfigFileAsync("{}", async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { "--print-config" });

                    Assert.Equal(0, exitCode);
                    var output = sw.ToString();
                    Assert.Contains("\"IgnoredExtensions\"", output, StringComparison.Ordinal);
                    Assert.Contains("\"TextFileExtensions\"", output, StringComparison.Ordinal);
                    Assert.Contains("\"MaxLogGenerations\"", output, StringComparison.Ordinal);
                    Assert.Contains(".pdb", output, StringComparison.Ordinal);   // default IgnoredExtensions value
                    Assert.Contains(".cs", output, StringComparison.Ordinal);    // default TextFileExtensions value
                    // Logger should NOT have been initialized (no diff run)
                    // ロガーは初期化されていないはず（差分実行なし）
                    Assert.Empty(logger.Messages);
                });
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public async Task RunAsync_PrintConfigFlag_ReflectsEnvVarOverride()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);
            var prevVal = Environment.GetEnvironmentVariable("FOLDERDIFF_MAXLOGGENERATIONS");

            try
            {
                Environment.SetEnvironmentVariable("FOLDERDIFF_MAXLOGGENERATIONS", "99");
                await WithConfigFileAsync("{}", async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { "--print-config" });

                    Assert.Equal(0, exitCode);
                    Assert.Contains("\"MaxLogGenerations\": 99", sw.ToString(), StringComparison.Ordinal);
                });
            }
            finally
            {
                Console.SetOut(origOut);
                Environment.SetEnvironmentVariable("FOLDERDIFF_MAXLOGGENERATIONS", prevVal);
            }
        }

        [Fact]
        public async Task RunAsync_PrintConfigFlag_WithInvalidConfigPath_ReturnsConfigurationError()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var originalError = Console.Error;
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            try
            {
                var exitCode = await runner.RunAsync(["--print-config", "--config", "bad\0config.json"]);

                Assert.Equal(3, exitCode);
                var errorOutput = errorWriter.ToString();
                Assert.Contains("Failed to print effective configuration", errorOutput, StringComparison.Ordinal);
                Assert.Contains("bad", errorOutput, StringComparison.Ordinal);
                Assert.Empty(logger.Messages);
            }
            finally
            {
                Console.SetError(originalError);
            }
        }

        [Fact]
        public async Task RunAsync_ValidateConfigFlag_WithInvalidConfigPath_ReturnsConfigurationError()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var originalError = Console.Error;
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            try
            {
                var exitCode = await runner.RunAsync(["--validate-config", "--config", "bad\0config.json"]);

                Assert.Equal(3, exitCode);
                var errorOutput = errorWriter.ToString();
                Assert.Contains("Configuration validation failed", errorOutput, StringComparison.Ordinal);
                Assert.Contains("bad", errorOutput, StringComparison.Ordinal);
                Assert.Empty(logger.Messages);
            }
            finally
            {
                Console.SetError(originalError);
            }
        }

        [Fact]
        public async Task RunAsync_PrintConfigFlag_WithInvalidUserLocalConfig_ShowsResolvedUserConfigPath()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            using var appDataScope = CreateAppDataOverrideScope();
            Directory.CreateDirectory(Path.GetDirectoryName(appDataScope.UserConfigFileAbsolutePath)!);
            await File.WriteAllTextAsync(appDataScope.UserConfigFileAbsolutePath, "{ invalid-json");
            var originalError = Console.Error;
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            try
            {
                var exitCode = await runner.RunAsync(["--print-config"]);

                Assert.Equal(3, exitCode);
                var errorOutput = errorWriter.ToString();
                Assert.Contains(Path.GetFullPath(appDataScope.UserConfigFileAbsolutePath), errorOutput, StringComparison.Ordinal);
                Assert.Contains("JSON syntax error", errorOutput, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Console.SetError(originalError);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task RunAsync_ValidateConfigFlag_WithInvalidBundledFallback_ShowsResolvedBundledConfigPath()
        {
            var bundledRoot = Path.Combine(Path.GetTempPath(), "fd-validate-config-bundled-" + Guid.NewGuid().ToString("N"));
            var bundledConfigPath = Path.Combine(bundledRoot, "config.json");
            Directory.CreateDirectory(bundledRoot);
            await File.WriteAllTextAsync(bundledConfigPath, "{ invalid-json");
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService(() => bundledConfigPath));
            using var appDataScope = CreateAppDataOverrideScope();
            var originalError = Console.Error;
            using var errorWriter = new StringWriter();
            Console.SetError(errorWriter);

            try
            {
                var exitCode = await runner.RunAsync(["--validate-config"]);

                Assert.Equal(3, exitCode);
                var errorOutput = errorWriter.ToString();
                Assert.Contains(Path.GetFullPath(bundledConfigPath), errorOutput, StringComparison.Ordinal);
                Assert.Contains("JSON syntax error", errorOutput, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Console.SetError(originalError);
                TryDeleteDirectory(bundledRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public void DeleteCacheFileForClearCache_ReadOnlyFile_DeletesFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-clear-cache-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var cacheFile = Path.Combine(tempDir, "sample.ilcache");
            File.WriteAllText(cacheFile, "cached");
            File.SetAttributes(cacheFile, File.GetAttributes(cacheFile) | FileAttributes.ReadOnly);

            try
            {
                ProgramRunner.DeleteCacheFileForClearCache(cacheFile);
                Assert.False(File.Exists(cacheFile));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public async Task RunAsync_PrintConfigFlag_ReflectsCliOverride()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                await WithConfigFileAsync("{}", async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { "--threads", "7", "--print-config" });

                    Assert.Equal(0, exitCode);
                    Assert.Contains("\"MaxParallelism\": 7", sw.ToString(), StringComparison.Ordinal);
                });
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public async Task RunAsync_PrintConfigFlag_WithCreatorIlIgnoreProfile_OutputsMergedIlFilters()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            const string configJson = """
                {
                  "ShouldIgnoreILLinesContainingConfiguredStrings": false,
                  "ILIgnoreLineContainingStrings": ["existing-filter"]
                }
                """;

            try
            {
                await WithConfigFileAsync(configJson, async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { "--creator-il-ignore-profile", "buildserver-winforms", "--print-config" });

                    Assert.Equal(0, exitCode);
                    var output = sw.ToString();
                    Assert.Contains("\"ShouldIgnoreILLinesContainingConfiguredStrings\": true", output, StringComparison.Ordinal);
                    Assert.Contains("existing-filter", output, StringComparison.Ordinal);
                    Assert.Contains("buildserver1_", output, StringComparison.Ordinal);
                    Assert.Contains("// Code size ", output, StringComparison.Ordinal);
                });
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public async Task RunAsync_PrintConfigFlag_WithCreator_OutputsDefaultProfileFilters()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                await WithConfigFileAsync("{}", async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { "--creator", "--print-config" });

                    Assert.Equal(0, exitCode);
                    var output = sw.ToString();
                    Assert.Contains("\"ShouldIgnoreILLinesContainingConfiguredStrings\": true", output, StringComparison.Ordinal);
                    Assert.Contains("buildserver1_", output, StringComparison.Ordinal);
                });
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public async Task RunAsync_PrintConfigFlag_WithCustomConfigPath_ReflectsCustomValues()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-print-config-custom-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            var customConfigPath = Path.Combine(tempRoot, "custom.json");
            await File.WriteAllTextAsync(customConfigPath, """{ "MaxLogGenerations": 12 }""");
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            using var appDataScope = CreateAppDataOverrideScope();
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--config", customConfigPath, "--print-config" });

                Assert.Equal(0, exitCode);
                Assert.Contains("\"MaxLogGenerations\": 12", sw.ToString(), StringComparison.Ordinal);
            }
            finally
            {
                Console.SetOut(origOut);
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task RunAsync_PrintConfigFlag_WithMissingExplicitConfig_ReturnsConfigurationError()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            using var appDataScope = CreateAppDataOverrideScope();
            string missingConfigPath = Path.Combine(Path.GetTempPath(), "fd-print-config-missing-" + Guid.NewGuid().ToString("N"), "missing.json");

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--print-config", "--config", missingConfigPath });
                Assert.Equal(3, exitCode);
            }
            finally
            {
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        private static string CreateTooLongConfigPath()
            => Path.Combine(Path.GetTempPath(), new string('a', 5000) + ".json");
    }
}
