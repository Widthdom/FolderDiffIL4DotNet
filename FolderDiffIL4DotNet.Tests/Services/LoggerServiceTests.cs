using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
                Assert.Contains("[ERR] failure", consoleText);

                var logText = File.ReadAllText(tempLogPath);
                Assert.Contains("[ERR] failure", logText);
                Assert.Contains(captured.StackTrace, logText);
                var firstLine = Assert.IsType<string>(logText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[0]);
                Assert.Matches(new Regex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\] \[ERR\] failure$", RegexOptions.CultureInvariant), firstLine);
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
                    // ignore cleanup errors / クリーンアップエラーを無視
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
                Assert.Contains("[INF] success", consoleText);
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

        // CleanupOldLogFiles catches UnauthorizedAccessException and continues when log directory is read-only (Linux/macOS non-root only)
        // ログディレクトリが読み取り専用の場合、CleanupOldLogFiles は UnauthorizedAccessException をキャッチして継続する（Linux/macOS 非 root のみ）
        [Fact]
        public void CleanupOldLogFiles_ReadOnlyDirectory_LogsWarningAndDoesNotThrow()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)
                && !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                return;
            }
            if (string.Equals(Environment.UserName, "root", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var logger = new LoggerService();
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            // Create multiple log files to exceed the generation limit
            // ジェネレーション超過状態にするため複数のログファイルを作成する
            var log1 = Path.Combine(tempDir, "log_20240101.log");
            var log2 = Path.Combine(tempDir, "log_20240102.log");
            var active = Path.Combine(tempDir, "log_20991231.log");
            File.WriteAllText(log1, "a");
            File.WriteAllText(log2, "b");
            File.WriteAllText(active, "c");
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", active);

            try
            {
                // Make directory read-only to prevent file deletion
                // ディレクトリを読み取り専用にしてファイル削除を阻止
#pragma warning disable CA1416 // Unix-only API; test is skipped on Windows / Unix 専用 API; Windows ではテストがスキップされます
                File.SetUnixFileMode(tempDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
#pragma warning restore CA1416

                // Should catch IOException or UnauthorizedAccessException and continue
                // IOException または UnauthorizedAccessException をキャッチして継続することを確認
                var ex = Record.Exception(() => logger.CleanupOldLogFiles(maxLogGenerations: 1));
                Assert.Null(ex);
            }
            finally
            {
#pragma warning disable CA1416 // Unix-only API; test is skipped on Windows / Unix 専用 API; Windows ではテストがスキップされます
                try { File.SetUnixFileMode(tempDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); } catch { }
#pragma warning restore CA1416
                TryDeleteDirectory(tempDir);
            }
        }

        /// <summary>
        /// Verifies that concurrent LogMessage calls do not throw IOException.
        /// 並列 LogMessage 呼び出しで IOException が発生しないことを検証する。
        /// </summary>
        [Fact]
        public void LogMessage_ConcurrentCalls_DoNotThrow()
        {
            var logger = new LoggerService();
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var tempLogPath = Path.Combine(tempDir, "log_concurrent.log");
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", tempLogPath);

            try
            {
                var ex = Record.Exception(() =>
                {
                    Parallel.For(0, 100, i =>
                    {
                        logger.LogMessage(AppLogLevel.Info, $"Concurrent message {i}", shouldOutputMessageToConsole: false);
                    });
                });

                Assert.Null(ex);

                var logText = File.ReadAllText(tempLogPath);
                for (int i = 0; i < 100; i++)
                {
                    Assert.Contains($"Concurrent message {i}", logText);
                }
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        // ── Mutation-testing additions / ミューテーションテスト追加 ──────────────

        [Fact]
        public void CleanupOldLogFiles_ExactlyAtMaxGenerations_NothingDeleted()
        {
            // When file count == maxLogGenerations, no files should be deleted
            // ファイル数 == maxLogGenerations のとき、ファイルは削除されないこと
            var logger = new LoggerService();
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var log1 = Path.Combine(tempDir, "log_20240101.log");
            var log2 = Path.Combine(tempDir, "log_20240102.log");
            var active = Path.Combine(tempDir, "log_20991231.log");
            File.WriteAllText(log1, "a");
            File.WriteAllText(log2, "b");
            File.WriteAllText(active, "c");
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", active);

            try
            {
                // 3 files, maxLogGenerations=3 → no deletion
                // 3 ファイル、maxLogGenerations=3 → 削除なし
                logger.CleanupOldLogFiles(maxLogGenerations: 3);

                Assert.True(File.Exists(log1));
                Assert.True(File.Exists(log2));
                Assert.True(File.Exists(active));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void CleanupOldLogFiles_FewerThanMaxGenerations_NothingDeleted()
        {
            // When file count < maxLogGenerations, no files should be deleted
            // ファイル数 < maxLogGenerations のとき、ファイルは削除されないこと
            var logger = new LoggerService();
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var active = Path.Combine(tempDir, "log_20991231.log");
            File.WriteAllText(active, "a");
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", active);

            try
            {
                // 1 file, maxLogGenerations=5 → no deletion
                // 1 ファイル、maxLogGenerations=5 → 削除なし
                logger.CleanupOldLogFiles(maxLogGenerations: 5);

                Assert.True(File.Exists(active));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void CleanupOldLogFiles_MaxGenerationsZero_PreservesActiveLogAndDeletesArchivedLogs()
        {
            var logger = new LoggerService();
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var archived1 = Path.Combine(tempDir, "log_20240101.log");
            var archived2 = Path.Combine(tempDir, "log_20240102.log");
            var active = Path.Combine(tempDir, "log_20991231.log");
            File.WriteAllText(archived1, "a");
            File.WriteAllText(archived2, "b");
            File.WriteAllText(active, "active");
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", active);

            try
            {
                logger.CleanupOldLogFiles(maxLogGenerations: 0);

                Assert.False(File.Exists(archived1));
                Assert.False(File.Exists(archived2));
                Assert.True(File.Exists(active));
                var logText = File.ReadAllText(active);
                Assert.Contains("Deleted old log file", logText, StringComparison.Ordinal);
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void CleanupOldLogFiles_WithNegativeGeneration_LogsSingleReadableWarningMessage()
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

                var logText = File.ReadAllText(active);
                Assert.Contains("MaxLogGenerations must be a non-negative integer.", logText, StringComparison.Ordinal);
                Assert.DoesNotContain("integer..", logText, StringComparison.Ordinal);
                Assert.Contains("maxLogGenerations", logText, StringComparison.Ordinal);
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void CleanupOldLogFiles_ReadOnlyArchivedFile_DeletesItOnWindows()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var logger = new LoggerService();
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var archived = Path.Combine(tempDir, "log_20240101.log");
            var active = Path.Combine(tempDir, "log_20991231.log");
            File.WriteAllText(archived, "archived");
            File.WriteAllText(active, "active");
            File.SetAttributes(archived, File.GetAttributes(archived) | FileAttributes.ReadOnly);
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", active);

            try
            {
                logger.CleanupOldLogFiles(maxLogGenerations: 1);

                Assert.False(File.Exists(archived));
                Assert.True(File.Exists(active));
            }
            finally
            {
                if (File.Exists(archived))
                {
                    File.SetAttributes(archived, FileAttributes.Normal);
                }

                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void CleanupOldLogFiles_WhenOneDeletionFails_ContinuesDeletingOtherArchivedLogsOnWindows()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var logger = new LoggerService();
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var lockedArchived = Path.Combine(tempDir, "log_20240101.log");
            var deletableArchived = Path.Combine(tempDir, "log_20240102.log");
            var active = Path.Combine(tempDir, "log_20991231.log");
            File.WriteAllText(lockedArchived, "locked");
            File.WriteAllText(deletableArchived, "delete-me");
            File.WriteAllText(active, "active");
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", active);

            using var lockStream = new FileStream(lockedArchived, FileMode.Open, FileAccess.Read, FileShare.None);

            try
            {
                logger.CleanupOldLogFiles(maxLogGenerations: 1);

                Assert.True(File.Exists(lockedArchived));
                Assert.False(File.Exists(deletableArchived));
                Assert.True(File.Exists(active));
                var logText = File.ReadAllText(active);
                Assert.Contains("Failed to delete archived log file", logText, StringComparison.Ordinal);
                Assert.Contains("Deleted old log file", logText, StringComparison.Ordinal);
            }
            finally
            {
                lockStream.Dispose();
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void FormatMessage_WhitespaceOnlyMessage_ReturnsPrefixOnly()
        {
            // When message is whitespace-only, FormatMessage should return just the prefix
            // メッセージがホワイトスペースのみの場合、FormatMessage はプレフィックスのみを返す
            var logger = new LoggerService();
            var originalOut = Console.Out;
            var writer = new StringWriter();

            try
            {
                Console.SetOut(writer);
                SetPrivateField(logger, "_logFileAbsolutePath", null);

                logger.LogMessage(AppLogLevel.Info, "   ", shouldOutputMessageToConsole: true);

                var consoleText = writer.ToString();
                // Should contain just the prefix without trailing whitespace message
                // プレフィックスのみが含まれ、後続のホワイトスペースメッセージがないこと
                Assert.Contains("[INF]", consoleText);
                // The trimmed line should be exactly the prefix
                // トリムされた行はプレフィックスのみであること
                var trimmed = consoleText.Trim();
                Assert.Equal("[INF]", trimmed);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        // -----------------------------------------------------------------------
        // JSON log format / JSON ログ形式
        // -----------------------------------------------------------------------

        [Fact]
        public void LogMessage_JsonFormat_WritesNdjsonToFile()
        {
            var logger = new LoggerService { Format = LogFormat.Json };
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var tempLogPath = Path.Combine(tempDir, "log_json_test.log");
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", tempLogPath);

            try
            {
                logger.LogMessage(AppLogLevel.Info, "hello json", shouldOutputMessageToConsole: false);

                Assert.True(File.Exists(tempLogPath));
                var content = File.ReadAllText(tempLogPath).Trim();
                var doc = System.Text.Json.JsonDocument.Parse(content);
                var root = doc.RootElement;

                Assert.True(root.TryGetProperty("timestamp", out _));
                Assert.Equal("INFO", root.GetProperty("level").GetString());
                Assert.Equal("hello json", root.GetProperty("message").GetString());
                Assert.False(root.TryGetProperty("exceptionType", out _));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void LogMessage_JsonFormat_IncludesExceptionFields()
        {
            var logger = new LoggerService { Format = LogFormat.Json };
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var tempLogPath = Path.Combine(tempDir, "log_json_exc.log");
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", tempLogPath);

            try
            {
                Exception captured;
                try { throw new InvalidOperationException("test-error"); }
                catch (Exception ex) { captured = ex; }

                logger.LogMessage(AppLogLevel.Error, "failure", shouldOutputMessageToConsole: false, captured);

                var content = File.ReadAllText(tempLogPath).Trim();
                var doc = System.Text.Json.JsonDocument.Parse(content);
                var root = doc.RootElement;

                Assert.Equal("ERROR", root.GetProperty("level").GetString());
                Assert.Equal("failure", root.GetProperty("message").GetString());
                Assert.Equal("System.InvalidOperationException", root.GetProperty("exceptionType").GetString());
                Assert.Equal("test-error", root.GetProperty("exceptionMessage").GetString());
                Assert.True(root.TryGetProperty("stackTrace", out _));
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void LogMessage_JsonFormat_TimestampIsIso8601()
        {
            var logger = new LoggerService { Format = LogFormat.Json };
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var tempLogPath = Path.Combine(tempDir, "log_json_ts.log");
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", tempLogPath);

            try
            {
                logger.LogMessage(AppLogLevel.Warning, "ts-check", shouldOutputMessageToConsole: false);

                var content = File.ReadAllText(tempLogPath).Trim();
                var doc = System.Text.Json.JsonDocument.Parse(content);
                var tsStr = doc.RootElement.GetProperty("timestamp").GetString();

                // ISO 8601 round-trip format / ISO 8601 ラウンドトリップ形式
                Assert.True(
                    DateTimeOffset.TryParse(tsStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
                    $"Timestamp '{tsStr}' is not a valid ISO 8601 format");
                Assert.Equal("WARNING", doc.RootElement.GetProperty("level").GetString());
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        [Fact]
        public void Format_DefaultIsText()
        {
            var logger = new LoggerService();
            Assert.Equal(LogFormat.Text, logger.Format);
        }

        // -----------------------------------------------------------------------
        // TraceId / JSON traceId and spanId fields
        // トレース ID / JSON traceId・spanId フィールド
        // -----------------------------------------------------------------------

        [Fact]
        public void TraceId_BeforeInitialization_IsNull()
        {
            var logger = new LoggerService();
            Assert.Null(logger.TraceId);
        }

        [Fact]
        public void TraceId_AfterInitialization_Is32HexChars()
        {
            var logger = new LoggerService();
            logger.Initialize();

            Assert.NotNull(logger.TraceId);
            Assert.Matches(new Regex(@"^[0-9a-f]{32}$", RegexOptions.CultureInvariant), logger.TraceId);
        }

        [Fact]
        public void LogMessage_JsonFormat_IncludesTraceIdAndSpanId()
        {
            var logger = new LoggerService { Format = LogFormat.Json };
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-logger-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var tempLogPath = Path.Combine(tempDir, "log_json_trace.log");

            logger.Initialize();
            SetPrivateField(logger, "_logDirectoryAbsolutePath", tempDir);
            SetPrivateField(logger, "_logFileAbsolutePath", tempLogPath);

            try
            {
                logger.LogMessage(AppLogLevel.Info, "trace-test", shouldOutputMessageToConsole: false);

                var content = File.ReadAllText(tempLogPath).Trim();
                var doc = System.Text.Json.JsonDocument.Parse(content);
                var root = doc.RootElement;

                // traceId should be a 32-char hex string matching the logger's TraceId
                // traceId はロガーの TraceId と一致する 32 桁の 16 進文字列であること
                var traceId = root.GetProperty("traceId").GetString();
                Assert.Equal(logger.TraceId, traceId);

                // spanId should be a 16-char hex string (W3C Trace Context format)
                // spanId は 16 桁の 16 進文字列（W3C Trace Context 形式）であること
                var spanId = root.GetProperty("spanId").GetString();
                Assert.NotNull(spanId);
                Assert.Matches(new Regex(@"^[0-9a-f]{16}$", RegexOptions.CultureInvariant), spanId);
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
                // ignore cleanup errors / クリーンアップエラーを無視
            }
        }
    }

    [CollectionDefinition("LoggerServiceTests NonParallel", DisableParallelization = true)]
    public sealed class LoggerServiceTestCollectionDefinition
    {
    }
}
