using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    [Collection("LoggerServiceTests NonParallel")]
    public sealed class LoggerServiceTests
    {
        [Fact]
        public void Initialize_SetsLogFilePath_AndCreatesLogDirectory()
        {
            var logger = new LoggerService();

            logger.Initialize();

            Assert.False(string.IsNullOrWhiteSpace(logger.LogFileAbsolutePath));
            var directory = Path.GetDirectoryName(logger.LogFileAbsolutePath);
            Assert.False(string.IsNullOrWhiteSpace(directory));
            Assert.True(Directory.Exists(directory));
            var logFileName = Path.GetFileName(logger.LogFileAbsolutePath);
            Assert.Matches(new Regex(@"^log_\d{8}\.log$", RegexOptions.CultureInvariant), logFileName);
            var datePart = Assert.IsType<string>(logFileName)[4..^4];
            Assert.True(
                DateTime.TryParseExact(datePart, Constants.LOG_FILE_DATE_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                $"Unexpected log file date format: {logFileName}");
        }

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
                var firstLine = Assert.IsType<string>(logText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[0]);
                Assert.Matches(new Regex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\] \[ERROR\] failure$", RegexOptions.CultureInvariant), firstLine);
                var timestampText = firstLine[1..firstLine.IndexOf(']')];
                Assert.True(
                    DateTime.TryParseExact(timestampText, Constants.LOG_ENTRY_TIMESTAMP_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                    $"Unexpected log entry timestamp format: {firstLine}");
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

        [Fact]
        public void LogMessage_WhenNotInitialized_DoesNotWriteFile()
        {
            var logger = new LoggerService();
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", null);

            try
            {
                logger.LogMessage(AppLogLevel.Info, "message", shouldOutputMessageToConsole: false);
                Assert.Empty(Directory.GetFiles(tempDir));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void CleanupOldLogFiles_DeletesOldFilesBeyondGenerationLimit()
        {
            var logger = new LoggerService();
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var old1 = Path.Combine(tempDir, "log_20240101.log");
            var old2 = Path.Combine(tempDir, "log_20240102.log");
            var keep = Path.Combine(tempDir, "log_20240103.log");
            var active = Path.Combine(tempDir, "log_20991231.log");
            File.WriteAllText(old1, "a");
            File.WriteAllText(old2, "b");
            File.WriteAllText(keep, "c");
            File.WriteAllText(active, "d");
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", active);

            try
            {
                logger.CleanupOldLogFiles(maxLogGenerations: 2);

                Assert.False(File.Exists(old1));
                Assert.False(File.Exists(old2));
                Assert.True(File.Exists(keep));
                Assert.True(File.Exists(active));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void CleanupOldLogFiles_WhenUninitialized_ReturnsWithoutThrowing()
        {
            var logger = new LoggerService();
            SetPrivateField(logger, "_logDirectoryAbsolutePath", null);

            logger.CleanupOldLogFiles(1);
        }

        [Fact]
        public void CleanupOldLogFiles_WithNegativeGeneration_DoesNotThrow()
        {
            var logger = new LoggerService();
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var active = Path.Combine(tempDir, "log_20991231.log");
            File.WriteAllText(active, "active");
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", active);

            try
            {
                logger.CleanupOldLogFiles(-1);
                Assert.True(File.Exists(active));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        private static void SetPrivateField(object target, string fieldName, string value)
        {
            var fieldInfo = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo);
            fieldInfo.SetValue(target, value);
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
    }

    [CollectionDefinition("LoggerServiceTests NonParallel", DisableParallelization = true)]
    public sealed class LoggerServiceTestCollectionDefinition
    {
    }
}
