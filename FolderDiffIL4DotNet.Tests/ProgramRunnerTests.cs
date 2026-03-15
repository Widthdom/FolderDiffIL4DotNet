using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services;
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

                    Assert.Equal(1, exitCode);
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

                    Assert.Equal(1, exitCode);
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
