using System;
using System.Globalization;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Common;
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
        private const string LOG_PREFIX_INFO = "[INFO]";
        private const string LOG_PREFIX_WARNING = "[WARNING]";
        private const string LOG_PREFIX_ERROR = "[ERROR]";
        private string? _logDirectoryAbsolutePath;
        private string? _logFileAbsolutePath;

        /// <inheritdoc />
        public string? LogFileAbsolutePath => _logFileAbsolutePath;

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

            using (var streamWriter = new StreamWriter(_logFileAbsolutePath, append: true))
            {
                streamWriter.WriteLine(
                    $"[{DateTime.Now.ToString(Constants.LOG_ENTRY_TIMESTAMP_FORMAT, CultureInfo.InvariantCulture)}] {formattedMessage}");
                if (exception != null)
                {
                    streamWriter.WriteLine(exception.StackTrace);
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
                    throw new ArgumentOutOfRangeException($"MaxLogGenerations must be a non-negative integer, but was {maxLogGenerations}.");
                }
                var logFilesAbsolutePaths = Directory.GetFiles(_logDirectoryAbsolutePath, $"{LOG_FILE_PREFIX}*.log");
                if (logFilesAbsolutePaths.Length > maxLogGenerations)
                {
                    var oldLogFilesToDeleteAbsolutePaths = logFilesAbsolutePaths.OrderBy(f => f).Take(logFilesAbsolutePaths.Length - maxLogGenerations);
                    foreach (var oldLogfileAbsolutePath in oldLogFilesToDeleteAbsolutePaths)
                    {
                        File.Delete(oldLogfileAbsolutePath);
                        LogMessage(AppLogLevel.Info, $"Deleted old log file: {oldLogfileAbsolutePath}.", shouldOutputMessageToConsole: true);
                    }
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                LogMessage(AppLogLevel.Warning, ex.Message + ".", shouldOutputMessageToConsole: true, ex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                LogMessage(AppLogLevel.Warning, $"Failed to clean up old log files in '{_logDirectoryAbsolutePath}'.", shouldOutputMessageToConsole: true, ex);
            }
        }

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
