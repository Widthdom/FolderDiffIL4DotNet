using System;
using System.IO;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    [Collection("LoggerServiceTests NonParallel")]
    public sealed class LoggerServiceTests
    {
        [Fact]
        public void LogMessage_WithExplicitConsoleColor_WritesFormattedMessageAndStackTraceToLogFile()
        {
            var originalOut = Console.Out;
            var originalLogDir = LoggerService._logDirectoryAbsolutePath;
            var originalLogFile = LoggerService._logFileAbsolutePath;
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var tempLogPath = Path.Combine(tempDir, "log_test.log");
            var writer = new StringWriter();

            try
            {
                Console.SetOut(writer);
                LoggerService._logDirectoryAbsolutePath = tempDir;
                LoggerService._logFileAbsolutePath = tempLogPath;

                Exception captured;
                try
                {
                    throw new InvalidOperationException("boom");
                }
                catch (Exception ex)
                {
                    captured = ex;
                }

                LoggerService.LogMessage(LoggerService.LogLevel.Error, "failure", shouldOutputMessageToConsole: true, ConsoleColor.Red, captured);

                var consoleText = writer.ToString();
                Assert.Contains("[ERROR] failure", consoleText);

                var logText = File.ReadAllText(tempLogPath);
                Assert.Contains("[ERROR] failure", logText);
                Assert.Contains(captured.StackTrace, logText);
            }
            finally
            {
                Console.SetOut(originalOut);
                LoggerService._logDirectoryAbsolutePath = originalLogDir;
                LoggerService._logFileAbsolutePath = originalLogFile;
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch
                {
                    // ignore cleanup errors in tests
                }
            }
        }

        [Fact]
        public void LogMessage_DefaultOverload_KeepsExistingConsoleFormat()
        {
            var originalOut = Console.Out;
            var originalLogFile = LoggerService._logFileAbsolutePath;
            var writer = new StringWriter();

            try
            {
                Console.SetOut(writer);
                LoggerService._logFileAbsolutePath = null;

                LoggerService.LogMessage(LoggerService.LogLevel.Info, "success", shouldOutputMessageToConsole: true);

                var consoleText = writer.ToString();
                Assert.Contains("[INFO] success", consoleText);
            }
            finally
            {
                Console.SetOut(originalOut);
                LoggerService._logFileAbsolutePath = originalLogFile;
            }
        }
    }

    [CollectionDefinition("LoggerServiceTests NonParallel", DisableParallelization = true)]
    public sealed class LoggerServiceTestCollectionDefinition
    {
    }
}
