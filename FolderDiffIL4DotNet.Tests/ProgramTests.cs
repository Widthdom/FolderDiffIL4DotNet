using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests
{
    public sealed class ProgramTests
    {
        private static readonly string ConfigFilePath = Path.Combine(AppContext.BaseDirectory, "config.json");

        [Fact]
        public async Task Main_WithInsufficientArguments_ReturnsErrorCode()
        {
            var exitCode = await InvokeProgramMainAsync(new[] { "--no-pause" });
            Assert.Equal(2, exitCode);
        }

        [Fact]
        public async Task Main_WithMissingConfigFile_ReturnsConfigurationErrorCode()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old-config-missing");
            var newDir = Path.Combine(tempRoot, "new-config-missing");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);

            try
            {
                await WithMissingConfigFileAsync(async () =>
                {
                    var exitCode = await InvokeProgramMainAsync(new[] { oldDir, newDir, "report_" + Guid.NewGuid().ToString("N"), "--no-pause" });
                    Assert.Equal(3, exitCode);
                });
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, recursive: true);
                    }
                }
                catch
                {
                    // ignore cleanup errors in tests / テストのクリーンアップエラーを無視
                }
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

            var reportLabel = "report_" + Guid.NewGuid().ToString("N");
            var reportDir = Path.Combine(AppContext.BaseDirectory, "Reports", reportLabel);
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
                await WithConfigFileAsync(configJson, async () =>
                {
                    var exitCode = await InvokeProgramMainAsync(new[] { oldDir, newDir, reportLabel, "--no-pause" });
                    Assert.Equal(0, exitCode);
                    Assert.True(File.Exists(Path.Combine(reportDir, "diff_report.md")));
                });
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, recursive: true);
                    }
                }
                catch
                {
                    // ignore cleanup errors in tests / テストのクリーンアップエラーを無視
                }
                try
                {
                    if (Directory.Exists(reportDir))
                    {
                        Directory.Delete(reportDir, recursive: true);
                    }
                }
                catch
                {
                    // ignore cleanup errors in tests / テストのクリーンアップエラーを無視
                }
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

            var reportLabel = "report_" + Guid.NewGuid().ToString("N");
            var reportDir = Path.Combine(AppContext.BaseDirectory, "Reports", reportLabel);
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
                await WithConfigFileAsync(configJson, async () =>
                {
                    var exitCode = await InvokeProgramMainAsync(new[] { oldDir, newDir, reportLabel, "--no-pause" });
                    Assert.Equal(0, exitCode);
                });

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
                try
                {
                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, recursive: true);
                    }
                }
                catch
                {
                    // ignore cleanup errors in tests / テストのクリーンアップエラーを無視
                }
                try
                {
                    if (Directory.Exists(reportDir))
                    {
                        Directory.Delete(reportDir, recursive: true);
                    }
                }
                catch
                {
                    // ignore cleanup errors in tests / テストのクリーンアップエラーを無視
                }
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

            var reportLabel = "report_" + Guid.NewGuid().ToString("N");
            var reportDir = Path.Combine(AppContext.BaseDirectory, "Reports", reportLabel);
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
                await WithConfigFileAsync(configJson, async () =>
                {
                    var exitCode = await InvokeProgramMainAsync(new[] { oldDir, newDir, reportLabel, "--no-pause" });
                    Assert.Equal(0, exitCode);
                });

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
                try
                {
                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, recursive: true);
                    }
                }
                catch
                {
                    // ignore cleanup errors in tests / テストのクリーンアップエラーを無視
                }
                try
                {
                    if (Directory.Exists(reportDir))
                    {
                        Directory.Delete(reportDir, recursive: true);
                    }
                }
                catch
                {
                    // ignore cleanup errors in tests / テストのクリーンアップエラーを無視
                }
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
                    await File.WriteAllTextAsync(ConfigFilePath, backupContent ?? string.Empty);
                }
            }
        }
    }
}
