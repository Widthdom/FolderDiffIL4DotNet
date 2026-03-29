using System;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Logger abstraction for outputting logs to file and console.
    /// ファイルおよびコンソールへログを出力するロガー抽象。
    /// </summary>
    public interface ILoggerService
    {
        /// <summary>
        /// Current log file absolute path (null if not yet initialized).
        /// 現在のログファイル絶対パス（未初期化時は null）。
        /// </summary>
        string? LogFileAbsolutePath { get; }

        /// <summary>
        /// Gets or sets the log output format.
        /// When set to <see cref="LogFormat.Json"/>, each log file line is a self-contained JSON object.
        /// Console output remains plain text regardless of this setting.
        /// ログ出力形式を取得・設定します。
        /// <see cref="LogFormat.Json"/> に設定すると、ログファイルの各行は自己完結 JSON オブジェクトになります。
        /// コンソール出力はこの設定に関係なくプレーンテキストのままです。
        /// </summary>
        LogFormat Format { get; set; }

        /// <summary>
        /// Initializes the logging infrastructure (output directory and file path).
        /// ログ基盤（出力先ディレクトリ/ファイルパス）を初期化します。
        /// </summary>
        void Initialize();

        /// <summary>
        /// Writes a log message.
        /// ログを出力します。
        /// </summary>
        void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, Exception? exception = null);

        /// <summary>
        /// Writes a log message with console color specification.
        /// ログを出力します（コンソール色指定）。
        /// </summary>
        void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, ConsoleColor? consoleForegroundColor, Exception? exception = null);

        /// <summary>
        /// Deletes old log files according to the generation count.
        /// 古いログを世代数に応じて削除します。
        /// </summary>
        void CleanupOldLogFiles(int maxLogGenerations);
    }
}
