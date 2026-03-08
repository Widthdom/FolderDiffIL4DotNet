using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
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
            Assert.Equal(1, exitCode);
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
                    // ignore cleanup errors in tests
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
                    // ignore cleanup errors in tests
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
    }
}
