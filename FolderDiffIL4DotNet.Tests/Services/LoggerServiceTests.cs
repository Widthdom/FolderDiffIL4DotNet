using System;
using System.IO;
using System.Reflection;
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
            var logger = new LoggerService();
            var originalOut = Console.Out;
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var tempLogPath = Path.Combine(tempDir, "log_test.log");
            var writer = new StringWriter();

            try
            {
                Console.SetOut(writer);
                SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
                SetPrivateField(logger, "_logFileAbsolutePath", tempLogPath);

                Exception captured;
                try
                {
                    throw new InvalidOperationException("boom");
                }
                catch (Exception ex)
                {
                    captured = ex;
                }

                logger.LogMessage(AppLogLevel.Error, "failure", shouldOutputMessageToConsole: true, ConsoleColor.Red, captured);

                var consoleText = writer.ToString();
                Assert.Contains("[ERROR] failure", consoleText);

                var logText = File.ReadAllText(tempLogPath);
                Assert.Contains("[ERROR] failure", logText);
                Assert.Contains(captured.StackTrace, logText);
            }
            finally
            {
                Console.SetOut(originalOut);
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
            var logger = new LoggerService();
            var originalOut = Console.Out;
            var writer = new StringWriter();

            try
            {
                Console.SetOut(writer);
                SetPrivateField(logger, "_logFileAbsolutePath", null);

                logger.LogMessage(AppLogLevel.Info, "success", shouldOutputMessageToConsole: true);

                var consoleText = writer.ToString();
                Assert.Contains("[INFO] success", consoleText);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        private static void SetPrivateField(object target, string fieldName, string value)
        {
            var fieldInfo = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo);
            fieldInfo.SetValue(target, value);
        }
    }

    [CollectionDefinition("LoggerServiceTests NonParallel", DisableParallelization = true)]
    public sealed class LoggerServiceTestCollectionDefinition
    {
    }
}
