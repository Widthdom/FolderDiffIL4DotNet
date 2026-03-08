using System;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// ファイルおよびコンソールへログを出力するロガー抽象。
    /// </summary>
    public interface ILoggerService
    {
        /// <summary>
        /// 現在のログファイル絶対パス（未初期化時は null）。
        /// </summary>
        string LogFileAbsolutePath { get; }

        /// <summary>
        /// ログ基盤（出力先ディレクトリ/ファイルパス）を初期化します。
        /// </summary>
        void Initialize();

        /// <summary>
        /// ログを出力します。
        /// </summary>
        void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, Exception exception = null);

        /// <summary>
        /// ログを出力します（コンソール色指定）。
        /// </summary>
        void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, ConsoleColor? consoleForegroundColor, Exception exception = null);

        /// <summary>
        /// 古いログを世代数に応じて削除します。
        /// </summary>
        void CleanupOldLogFiles(int maxLogGenerations);
    }
}
