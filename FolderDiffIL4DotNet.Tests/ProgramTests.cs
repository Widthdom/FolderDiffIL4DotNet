using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests
{
    [Collection("ConsoleOutput NonParallel")]
    [Trait("Category", "Unit")]
    public sealed class ProgramTests
    {
        [Fact]
        public async Task Main_WithInsufficientArguments_ReturnsErrorCode()
        {
            var exitCode = await InvokeProgramMainAsync(new[] { "--no-pause" });
            Assert.Equal(2, exitCode);
        }

        [Fact]
        public async Task Main_WithMissingExplicitConfigFile_ReturnsConfigurationErrorCode()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old-config-missing");
            var newDir = Path.Combine(tempRoot, "new-config-missing");
            var missingConfigPath = Path.Combine(tempRoot, "missing-config.json");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            using var appDataScope = CreateAppDataOverrideScope();

            try
            {
                var exitCode = await InvokeProgramMainAsync(new[] { oldDir, newDir, "report_" + Guid.NewGuid().ToString("N"), "--config", missingConfigPath, "--no-pause" });
                Assert.Equal(3, exitCode);
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task Main_WithTooLongExplicitConfigPath_ReturnsConfigurationErrorCode()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old-config-long");
            var newDir = Path.Combine(tempRoot, "new-config-long");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            string tooLongConfigPath = CreateTooLongConfigPath();
            using var appDataScope = CreateAppDataOverrideScope();

            try
            {
                var exitCode = await InvokeProgramMainAsync(new[] { oldDir, newDir, "report_" + Guid.NewGuid().ToString("N"), "--config", tooLongConfigPath, "--no-pause" });
                Assert.Equal(3, exitCode);
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task Main_WhenLocalApplicationDataIsUnresolved_ReturnsInvalidArgumentsCode()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old-appdata-unresolved");
            var newDir = Path.Combine(tempRoot, "new-appdata-unresolved");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            using var appDataScope = CreateAppDataOverrideScope();
            object? originalOverride = AppContext.GetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY);
            var originalOut = Console.Out;
            using var outputWriter = new StringWriter();

            try
            {
                Console.SetOut(outputWriter);
                AppContext.SetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, string.Empty);

                var exitCode = await InvokeProgramMainAsync(new[] { oldDir, newDir, "report_" + Guid.NewGuid().ToString("N"), "--no-pause" });

                Assert.Equal(2, exitCode);
                string output = outputWriter.ToString();
                Assert.Contains("File logging was unavailable.", output, StringComparison.Ordinal);
                Assert.DoesNotContain("Error details logged to:", output, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetOut(originalOut);
                AppContext.SetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, originalOverride);
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task Main_WithValidArguments_ReturnsSuccessAndGeneratesReport()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            await File.WriteAllTextAsync(Path.Combine(oldDir, "sample.txt"), "same");
            await File.WriteAllTextAsync(Path.Combine(newDir, "sample.txt"), "same");
            using var appDataScope = CreateAppDataOverrideScope();

            var reportLabel = "report_" + Guid.NewGuid().ToString("N");
            var reportRoot = Path.Combine(tempRoot, "reports");
            var reportDir = Path.Combine(reportRoot, reportLabel);
            var configPath = Path.Combine(tempRoot, "config.json");
            var configJson = """
            {
              "IgnoredExtensions": [],
              "TextFileExtensions": [".txt"],
              "MaxLogGenerations": 3,
              "ShouldIncludeUnchangedFiles": true,
              "ShouldIncludeIgnoredFiles": false,
              "ShouldOutputILText": false,
              "ShouldIgnoreILLinesContainingConfiguredStrings": false,
              "ILIgnoreLineContainingStrings": [],
              "ShouldOutputFileTimestamps": false,
              "ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp": true,
              "MaxParallelism": 1,
              "EnableILCache": false,
              "OptimizeForNetworkShares": false,
              "AutoDetectNetworkShares": false
            }
            """;

            try
            {
                await File.WriteAllTextAsync(configPath, configJson);

                var exitCode = await InvokeProgramMainAsync(new[] { oldDir, newDir, reportLabel, "--config", configPath, "--output", reportRoot, "--no-pause" });
                Assert.Equal(0, exitCode);
                Assert.True(File.Exists(Path.Combine(reportDir, "diff_report.md")));
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task Main_WhenNewFileTimestampIsOlder_WritesWarningAndAddsReportSection()
        {
            TimestampCache.Clear();
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old-warning");
            var newDir = Path.Combine(tempRoot, "new-warning");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var fileRelativePath = "sample.txt";
            var oldFile = Path.Combine(oldDir, fileRelativePath);
            var newFile = Path.Combine(newDir, fileRelativePath);
            await File.WriteAllTextAsync(oldFile, "old content");
            await File.WriteAllTextAsync(newFile, "new content");
            File.SetLastWriteTimeUtc(oldFile, new DateTime(2026, 3, 14, 1, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(newFile, new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc));
            using var appDataScope = CreateAppDataOverrideScope();

            var reportLabel = "report_" + Guid.NewGuid().ToString("N");
            var reportRoot = Path.Combine(tempRoot, "reports");
            var reportDir = Path.Combine(reportRoot, reportLabel);
            var configPath = Path.Combine(tempRoot, "config.json");
            var configJson = """
            {
              "IgnoredExtensions": [],
              "TextFileExtensions": [".txt"],
              "MaxLogGenerations": 3,
              "ShouldIncludeUnchangedFiles": true,
              "ShouldIncludeIgnoredFiles": false,
              "ShouldOutputILText": false,
              "ShouldIgnoreILLinesContainingConfiguredStrings": false,
              "ILIgnoreLineContainingStrings": [],
              "ShouldOutputFileTimestamps": false,
              "ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp": true,
              "MaxParallelism": 1,
              "EnableILCache": false,
              "OptimizeForNetworkShares": false,
              "AutoDetectNetworkShares": false
            }
            """;
            var originalOut = Console.Out;
            var writer = new StringWriter();

            try
            {
                Console.SetOut(writer);
                await File.WriteAllTextAsync(configPath, configJson);
                var exitCode = await InvokeProgramMainAsync(new[] { oldDir, newDir, reportLabel, "--config", configPath, "--output", reportRoot, "--no-pause" });
                Assert.Equal(0, exitCode);

                var consoleText = writer.ToString();
                Assert.Contains("older timestamps", consoleText);
                Assert.Contains("See diff_report for details.", consoleText);
                Assert.DoesNotContain("sample.txt", consoleText);

                var reportText = await File.ReadAllTextAsync(Path.Combine(reportDir, "diff_report.md"));
                Assert.Contains("## Warnings", reportText);
                Assert.Contains("sample.txt", reportText);
                Assert.Contains(" → ", reportText);
            }
            finally
            {
                Console.SetOut(originalOut);
                TimestampCache.Clear();
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task Main_WhenSha256MismatchExists_WritesWarningAtEndAndAddsReportWarningsSection()
        {
            TimestampCache.Clear();
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old-sha256-warning");
            var newDir = Path.Combine(tempRoot, "new-sha256-warning");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            await File.WriteAllTextAsync(Path.Combine(oldDir, "payload.bin"), "old");
            await File.WriteAllTextAsync(Path.Combine(newDir, "payload.bin"), "new");
            using var appDataScope = CreateAppDataOverrideScope();

            var reportLabel = "report_" + Guid.NewGuid().ToString("N");
            var reportRoot = Path.Combine(tempRoot, "reports");
            var reportDir = Path.Combine(reportRoot, reportLabel);
            var configPath = Path.Combine(tempRoot, "config.json");
            var configJson = """
            {
              "IgnoredExtensions": [],
              "TextFileExtensions": [],
              "MaxLogGenerations": 3,
              "ShouldIncludeUnchangedFiles": true,
              "ShouldIncludeIgnoredFiles": false,
              "ShouldOutputILText": false,
              "ShouldIgnoreILLinesContainingConfiguredStrings": false,
              "ILIgnoreLineContainingStrings": [],
              "ShouldOutputFileTimestamps": false,
              "ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp": true,
              "MaxParallelism": 1,
              "EnableILCache": false,
              "OptimizeForNetworkShares": false,
              "AutoDetectNetworkShares": false
            }
            """;
            var originalOut = Console.Out;
            var writer = new StringWriter();

            try
            {
                Console.SetOut(writer);
                await File.WriteAllTextAsync(configPath, configJson);
                var exitCode = await InvokeProgramMainAsync(new[] { oldDir, newDir, reportLabel, "--config", configPath, "--output", reportRoot, "--no-pause" });
                Assert.Equal(0, exitCode);

                var consoleText = writer.ToString();
                Assert.Contains(Constants.WARNING_SHA256_MISMATCH, consoleText);

                var reportText = await File.ReadAllTextAsync(Path.Combine(reportDir, "diff_report.md"));
                Assert.Contains("## Warnings", reportText);
                Assert.Contains("SHA256Mismatch: binary diff only", reportText);
            }
            finally
            {
                Console.SetOut(originalOut);
                TimestampCache.Clear();
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        private static async Task<int> InvokeProgramMainAsync(string[] args)
        {
            var programType = typeof(ConfigService).Assembly.GetType("FolderDiffIL4DotNet.Program");
            Assert.NotNull(programType);

            var mainMethod = programType.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(mainMethod);

            var taskObject = mainMethod.Invoke(null, new object[] { args });
            var task = Assert.IsAssignableFrom<Task<int>>(taskObject);
            return await task;
        }

        private static AppDataOverrideScope CreateAppDataOverrideScope()
            => new(Path.Combine(Path.GetTempPath(), "fd-program-appdata-" + Guid.NewGuid().ToString("N")));

        private static string CreateTooLongConfigPath()
            => Path.Combine(Path.GetTempPath(), new string('a', 5000) + ".json");

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
    }
}
