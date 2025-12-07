using System;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// ファイルおよびコンソールへログを出力する簡易ロガー。
    /// <para>
    /// 先に <see cref="Initialize"/> を呼び出してログファイルの出力先を確定してください。
    /// 未初期化の場合、<see cref="LogMessage(LogLevel, string, bool, Exception)"/> はコンソール出力のみを行い、ファイルには書き込みません。
    /// </para>
    /// </summary>
    public static class LoggerService
    {
        /// <summary>
        /// ログレベル
        /// </summary>
        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        #region public member variables
        /// <summary>
        /// ログディレクトリの絶対パス
        /// </summary>
        public static string _logDirectoryAbsolutePath;

        /// <summary>
        /// ログファイルの絶対パス
        /// </summary>
        public static string _logFileAbsolutePath;
        #endregion

        /// <summary>
        /// ログディレクトリと当日付のログファイル（パスのみ）を設定します。
        /// <para>
        /// ログディレクトリは <c>AppContext.BaseDirectory/<see cref="Constants.LOGS_DIRECTORY_NAME"/></c> に作成され、
        /// ログファイルパスは <c>log_yyyyMMdd.log</c> 形式で構成されます（本メソッドではファイル自体は作成しません）。
        /// パス長は <see cref="Utility.ValidateAbsolutePathLengthOrThrow(string, string)"/> により検証されます。
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentException">ログディレクトリまたはログファイルのパス長がOSの上限を超えるなどで不正な場合。</exception>
        /// <exception cref="UnauthorizedAccessException">ログディレクトリの作成権限がない場合。</exception>
        /// <exception cref="IOException">ディレクトリの作成に失敗した場合や、I/O エラーが発生した場合。</exception>
        /// <exception cref="PathTooLongException">パスが長すぎる場合（環境による）。</exception>
        public static void Initialize()
        {
            _logDirectoryAbsolutePath = Path.Combine(AppContext.BaseDirectory, Constants.LOGS_DIRECTORY_NAME);

            Utility.ValidateAbsolutePathLengthOrThrow(_logDirectoryAbsolutePath);

            Directory.CreateDirectory(_logDirectoryAbsolutePath);

            _logFileAbsolutePath = Path.Combine(_logDirectoryAbsolutePath, $"{Constants.LOG_FILE_PREFIX}{DateTime.Now:yyyyMMdd}.log");

            Utility.ValidateAbsolutePathLengthOrThrow(_logFileAbsolutePath);
        }

        /// <summary>
        /// メッセージをログファイルに追記し、必要に応じてコンソールにも出力します。
        /// <para>
        /// まだ <see cref="Initialize"/> が呼ばれていない場合は、コンソール出力（指定時）のみ行い、ファイル出力はスキップします。
        /// </para>
        /// </summary>
        /// <param name="logLevel">ログレベル。</param>
        /// <param name="message">出力するメッセージ（null 可）。</param>
        /// <param name="shouldOutputMessageToConsole">true の場合、メッセージをコンソールにも出力します。</param>
        /// <param name="exception">例外情報（省略可）。指定した場合、スタックトレースをログファイルに追記します。</param>
        /// <exception cref="UnauthorizedAccessException">ログファイルへの書き込み権限がない場合。</exception>
        /// <exception cref="DirectoryNotFoundException">ログディレクトリが存在しない、またはパスが無効な場合。</exception>
        /// <exception cref="IOException">ファイル書き込み時に I/O エラーが発生した場合。</exception>
        public static void LogMessage(LogLevel logLevel, string message, bool shouldOutputMessageToConsole, Exception exception = null)
        {
            string formattedMessage = FormatMessage(message, logLevel);

            if (shouldOutputMessageToConsole)
            {
                Console.WriteLine(formattedMessage);
            }

            // 初期化前の場合はコンソール出力のみで終了。
            if (string.IsNullOrWhiteSpace(_logFileAbsolutePath))
            {
                return;
            }

            using (var streamWriter = new StreamWriter(_logFileAbsolutePath, append: true))
            {
                streamWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {formattedMessage}");
                if (exception != null)
                {
                    streamWriter.WriteLine(exception.StackTrace);
                }
            }
        }

        /// <summary>
        /// ログディレクトリ内のローテーション済みログ (<c>log_*.log</c>) を世代保持数に従い整理します。
        /// <para>
        /// 振る舞い: 
        /// <list type="bullet">
        /// <item><description><paramref name="maxLogGenerations"/> &lt; 0 : 例外を捕捉して警告ログを出力 (削除処理は行わない)</description></item>
        /// <item><description><paramref name="maxLogGenerations"/> == 0 : すべて削除</description></item>
        /// <item><description><paramref name="maxLogGenerations"/> &gt; 0 : ファイル数が上限を超えている場合、古い順に (ファイル名昇順) 不足分を削除し、最新 <paramref name="maxLogGenerations"/> 件のみ残す</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// 削除成功時は各ファイルを <c>[INFO]</c> で記録。削除処理中の想定外例外は捕捉し <c>[WARNING]</c> で記録しつつ継続します (呼び出し元へは再スローしません)。
        /// </para>
        /// </summary>
        /// <param name="maxLogGenerations">保持したいログ世代数。0 で全削除。負値は無効 (警告ログ出力のみ)。</param>
        /// <exception cref="Exception">本メソッド内で発生した例外は捕捉され、呼び出し元には送出されません。</exception>
        public static void CleanupOldLogFiles(int maxLogGenerations)
        {
            try
            {
                if (maxLogGenerations < 0)
                {
                    throw new ArgumentOutOfRangeException($"MaxLogGenerations must be a non-negative integer, but was {maxLogGenerations}.");
                }
                var logFilesAbsolutePaths = Directory.GetFiles(_logDirectoryAbsolutePath, $"{Constants.LOG_FILE_PREFIX}*.log");
                if (logFilesAbsolutePaths.Length > maxLogGenerations)
                {
                    var oldLogFilesToDeleteAbsolutePaths = logFilesAbsolutePaths.OrderBy(f => f).Take(logFilesAbsolutePaths.Length - maxLogGenerations);
                    foreach (var oldLogfileAbsolutePath in oldLogFilesToDeleteAbsolutePaths)
                    {
                        File.Delete(oldLogfileAbsolutePath);
                        LogMessage(LogLevel.Info, string.Format(Constants.LOG_DELETED_OLD_LOG_FILE, oldLogfileAbsolutePath), shouldOutputMessageToConsole: true);
                    }
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                LogMessage(LogLevel.Warning, ex.Message + ".", shouldOutputMessageToConsole: true, ex);
            }
            catch (Exception ex)
            {
                LogMessage(LogLevel.Warning, string.Format(Constants.LOG_FAILED_CLEANUP_OLD_LOGS, _logDirectoryAbsolutePath), shouldOutputMessageToConsole: true, ex);
            }
        }

        /// <summary>
        /// メッセージにログレベルのプレフィックスを付与し、空文字の場合はプレフィックスのみを返します。
        /// </summary>
        private static string FormatMessage(string message, LogLevel logLevel)
        {
            string prefix = GetLogLevelPrefix(logLevel);

            if (string.IsNullOrWhiteSpace(message))
            {
                return prefix;
            }

            return $"{prefix} {message}";
        }

        /// <summary>
        /// ログレベルに応じたプレフィックス（[INFO] など）を取得します。
        /// </summary>
        private static string GetLogLevelPrefix(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Warning => Constants.LOG_PREFIX_WARNING,
            LogLevel.Error => Constants.LOG_PREFIX_ERROR,
            _ => Constants.LOG_PREFIX_INFO
        };
    }
}
