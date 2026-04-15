using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Common;
using FolderDiffIL4DotNet.Core.IO;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Simple logger that writes to both a log file and the console.
    /// Call <see cref="Initialize"/> first to set the log file path.
    /// Before initialization, <see cref="LogMessage(AppLogLevel, string, bool, Exception)"/> only writes to the console.
    /// ファイルおよびコンソールへログを出力する簡易ロガー。
    /// 先に <see cref="Initialize"/> を呼び出してログファイルの出力先を確定してください。
    /// 未初期化の場合はコンソール出力のみを行い、ファイルには書き込みません。
    /// </summary>
    public sealed class LoggerService : ILoggerService
    {
        private const string LOGS_DIRECTORY_NAME = "Logs";
        private const string LOG_FILE_PREFIX = "log_";
        private const string LOG_PREFIX_INFO =    "[INF]";
        private const string LOG_PREFIX_WARNING = "[WRN]";
        private const string LOG_PREFIX_ERROR =   "[ERR]";

        /// <summary>
        /// Lock object for serialising file writes so that concurrent callers do not cause IOException.
        /// 並列呼び出し時の IOException を防ぐためファイル書き込みを直列化するロックオブジェクト。
        /// </summary>
        private readonly object _fileWriteLock = new();

        private string? _logDirectoryAbsolutePath;
        private string? _logFileAbsolutePath;
        private string? _traceId;

        /// <inheritdoc />
        public string? LogFileAbsolutePath => _logFileAbsolutePath;

        /// <inheritdoc />
        public LogFormat Format { get; set; } = LogFormat.Text;

        /// <inheritdoc />
        public string? TraceId => _traceId;

        /// <summary>
        /// Creates the log directory and computes today's log file path (does not create the file itself).
        /// Path lengths are validated via <see cref="PathValidator.ValidateAbsolutePathLengthOrThrow(string, string)"/>.
        /// ログディレクトリを作成し、当日付のログファイルパスを設定します（ファイル自体は作成しません）。
        /// </summary>
        public void Initialize()
        {
            _logDirectoryAbsolutePath = Path.Combine(AppContext.BaseDirectory, LOGS_DIRECTORY_NAME);

            PathValidator.ValidateAbsolutePathLengthOrThrow(_logDirectoryAbsolutePath);

            Directory.CreateDirectory(_logDirectoryAbsolutePath);

            _logFileAbsolutePath = Path.Combine(
                _logDirectoryAbsolutePath,
                $"{LOG_FILE_PREFIX}{DateTime.Now.ToString(Constants.LOG_FILE_DATE_FORMAT, CultureInfo.InvariantCulture)}.log");

            PathValidator.ValidateAbsolutePathLengthOrThrow(_logFileAbsolutePath);

            // Generate a W3C Trace Context compatible trace ID (32 hex chars) for the run.
            // This allows correlating all log entries from a single run in SIEM / OpenTelemetry.
            // 実行単位の W3C Trace Context 互換トレース ID (32桁16進数) を生成。
            // SIEM / OpenTelemetry で同一実行のログを相関付けるために使用。
            _traceId = ActivityTraceId.CreateRandom().ToHexString();
        }

        /// <inheritdoc />
        public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, Exception? exception = null)
            => LogMessage(logLevel, message, shouldOutputMessageToConsole, consoleForegroundColor: null, exception);

        /// <inheritdoc />
        public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, ConsoleColor? consoleForegroundColor, Exception? exception = null)
        {
            string formattedMessage = FormatMessage(message, logLevel);

            if (shouldOutputMessageToConsole)
            {
                if (!Console.IsOutputRedirected)
                {
                    try
                    {
                        if (Console.CursorLeft != 0)
                        {
                            Console.WriteLine();
                        }
                    }
                    catch (IOException)
                    {
                        // ignore console position errors
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // ignore console position errors
                    }
                }

                if (consoleForegroundColor.HasValue && !Console.IsOutputRedirected)
                {
                    var originalColor = Console.ForegroundColor;
                    try
                    {
                        Console.ForegroundColor = consoleForegroundColor.Value;
                        Console.WriteLine(formattedMessage);
                    }
                    finally
                    {
                        Console.ForegroundColor = originalColor;
                    }
                }
                else
                {
                    Console.WriteLine(formattedMessage);
                }
            }

            // Before initialization, only console output is performed.
            // 初期化前の場合はコンソール出力のみで終了。
            if (string.IsNullOrWhiteSpace(_logFileAbsolutePath))
            {
                return;
            }

            // Serialise file writes to prevent IOException under parallel diff processing.
            // 並列差分処理時のファイル書き込み IOException を防止するため直列化。
            lock (_fileWriteLock)
            {
                try
                {
                    PrepareLogFileForAppend(_logFileAbsolutePath);
                    using var streamWriter = new StreamWriter(_logFileAbsolutePath, append: true);
                    if (Format == LogFormat.Json)
                    {
                        WriteJsonLogEntry(streamWriter, logLevel, message, exception);
                    }
                    else
                    {
                        streamWriter.WriteLine(
                            $"[{DateTime.Now.ToString(Constants.LOG_ENTRY_TIMESTAMP_FORMAT, CultureInfo.InvariantCulture)}] {formattedMessage}");
                        WriteTextExceptionDetails(streamWriter, exception);
                    }
                }
                catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
                {
                    TryWriteFileLoggingFailureToConsole(_logFileAbsolutePath, ex);
                }
            }
        }

        /// <inheritdoc />
        public void CleanupOldLogFiles(int maxLogGenerations)
        {
            if (string.IsNullOrWhiteSpace(_logDirectoryAbsolutePath))
            {
                return;
            }

            try
            {
                if (maxLogGenerations < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(maxLogGenerations),
                        maxLogGenerations,
                        "MaxLogGenerations must be a non-negative integer.");
                }
                var logFilesAbsolutePaths = Directory.GetFiles(_logDirectoryAbsolutePath, $"{LOG_FILE_PREFIX}*.log");
                int filesToDelete = logFilesAbsolutePaths.Length - maxLogGenerations;
                if (filesToDelete > 0)
                {
                    foreach (var oldLogfileAbsolutePath in logFilesAbsolutePaths.OrderBy(f => f))
                    {
                        if (filesToDelete <= 0)
                        {
                            break;
                        }

                        // Never delete the active log file; otherwise the cleanup log entry can recreate it.
                        // 現在のログファイルは削除しない。削除ログの出力で再生成されてしまうため。
                        if (string.Equals(oldLogfileAbsolutePath, _logFileAbsolutePath, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (TryDeleteArchivedLogFile(oldLogfileAbsolutePath))
                        {
                            LogMessage(AppLogLevel.Info, $"Deleted old log file: {oldLogfileAbsolutePath}.", shouldOutputMessageToConsole: true);
                            filesToDelete--;
                        }
                    }
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                LogMessage(
                    AppLogLevel.Warning,
                    BuildCleanupFailureMessage("CleanupOldLogFiles failed", maxLogGenerations, ex),
                    shouldOutputMessageToConsole: true,
                    ex);
            }
            catch (Exception ex) when (ExceptionFilters.IsFileIoRecoverable(ex))
            {
                LogMessage(
                    AppLogLevel.Warning,
                    BuildCleanupFailureMessage("Failed to clean up old log files", maxLogGenerations, ex),
                    shouldOutputMessageToConsole: true,
                    ex);
            }
        }

        private bool TryDeleteArchivedLogFile(string oldLogfileAbsolutePath)
        {
            try
            {
                var attributes = File.GetAttributes(oldLogfileAbsolutePath);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(oldLogfileAbsolutePath, attributes & ~FileAttributes.ReadOnly);
                }

                File.Delete(oldLogfileAbsolutePath);
                return true;
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                LogMessage(AppLogLevel.Warning,
                    $"Failed to delete archived log file '{oldLogfileAbsolutePath}' ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true,
                    ex);
                return false;
            }
        }

        private static void PrepareLogFileForAppend(string logFileAbsolutePath)
        {
            if (!File.Exists(logFileAbsolutePath))
            {
                return;
            }

            var attributes = File.GetAttributes(logFileAbsolutePath);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(logFileAbsolutePath, attributes & ~FileAttributes.ReadOnly);
            }
        }

        private string BuildCleanupFailureMessage(string prefix, int maxLogGenerations, Exception exception)
        {
            var activeLog = string.IsNullOrWhiteSpace(_logFileAbsolutePath) ? "(none)" : _logFileAbsolutePath;
            return $"{prefix} in '{_logDirectoryAbsolutePath}' (MaxGenerations={maxLogGenerations}, ActiveLog='{activeLog}', {exception.GetType().Name}): {exception.Message}";
        }

        private static void TryWriteFileLoggingFailureToConsole(string logFileAbsolutePath, Exception ex)
        {
#pragma warning disable CA1031 // Console fallback must never crash logging callers / コンソールへのフォールバックは呼び出し元を絶対に落とさない
            try
            {
                Console.Error.WriteLine(
                    $"[WRN] Failed to write log file '{logFileAbsolutePath}' ({ex.GetType().Name}): {ex.Message}");
            }
            catch
            {
                // Best-effort diagnostic fallback / ベストエフォートの診断フォールバック
            }
#pragma warning restore CA1031
        }

        /// <summary>
        /// Writes a single log entry as a JSON object (one line per entry, NDJSON).
        /// Includes W3C Trace Context fields (<c>traceId</c>, <c>spanId</c>) for SIEM / OpenTelemetry correlation.
        /// 1つのログエントリを JSON オブジェクトとして書き込みます（1行1エントリ、NDJSON 形式）。
        /// SIEM / OpenTelemetry 連携用に W3C Trace Context フィールド（<c>traceId</c>、<c>spanId</c>）を含みます。
        /// </summary>
        private void WriteJsonLogEntry(StreamWriter writer, AppLogLevel logLevel, string message, Exception? exception)
        {
            using var stream = new MemoryStream();
            using (var jsonWriter = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                jsonWriter.WriteStartObject();
                jsonWriter.WriteString("timestamp", DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
                jsonWriter.WriteString("level", GetLogLevelString(logLevel));
                jsonWriter.WriteString("message", message ?? string.Empty);

                // W3C Trace Context fields for SIEM / OpenTelemetry correlation
                // SIEM / OpenTelemetry 相関付け用 W3C Trace Context フィールド
                if (_traceId != null)
                {
                    jsonWriter.WriteString("traceId", _traceId);
                }
                jsonWriter.WriteString("spanId", ActivitySpanId.CreateRandom().ToHexString());

                if (exception != null)
                {
                    jsonWriter.WriteString("exceptionType", exception.GetType().FullName);
                    jsonWriter.WriteString("exceptionMessage", exception.Message);
                    jsonWriter.WriteString("exceptionDetail", exception.ToString());
                    if (exception.StackTrace != null)
                    {
                        jsonWriter.WriteString("stackTrace", exception.StackTrace);
                    }

                    if (exception.InnerException != null)
                    {
                        jsonWriter.WriteString("innerExceptionType", exception.InnerException.GetType().FullName);
                        jsonWriter.WriteString("innerExceptionMessage", exception.InnerException.Message);
                    }
                }
                jsonWriter.WriteEndObject();
            }

            writer.WriteLine(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        }

        private static void WriteTextExceptionDetails(StreamWriter writer, Exception? exception)
        {
            if (exception == null)
            {
                return;
            }

            writer.WriteLine(exception.ToString());
        }

        private static string GetLogLevelString(AppLogLevel logLevel) => logLevel switch
        {
            AppLogLevel.Warning => "WARNING",
            AppLogLevel.Error => "ERROR",
            _ => "INFO"
        };

        private static string FormatMessage(string message, AppLogLevel logLevel)
        {
            var prefix = GetLogLevelPrefix(logLevel);
            return string.IsNullOrWhiteSpace(message)
                ? prefix
                : $"{prefix} {message}";
        }

        private static string GetLogLevelPrefix(AppLogLevel logLevel) => logLevel switch
        {
            AppLogLevel.Warning => LOG_PREFIX_WARNING,
            AppLogLevel.Error => LOG_PREFIX_ERROR,
            _ => LOG_PREFIX_INFO
        };
    }
}
