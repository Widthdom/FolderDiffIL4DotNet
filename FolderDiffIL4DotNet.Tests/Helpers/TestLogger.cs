using System;
using System.Collections.Generic;
using System.Linq;
using FolderDiffIL4DotNet.Services;

namespace FolderDiffIL4DotNet.Tests.Helpers
{
    /// <summary>
    /// Shared test logger that captures log entries for assertion in tests.
    /// テストでログエントリをキャプチャし、アサーションに使用する共有テストロガー。
    /// </summary>
    internal sealed class TestLogger : ILoggerService
    {
        private readonly Action<TestLogEntry>? _onEntry;

        /// <summary>
        /// Creates a TestLogger with default settings (no callback, null log file path).
        /// デフォルト設定（コールバックなし、ログファイルパス null）で TestLogger を作成します。
        /// </summary>
        public TestLogger()
            : this(onEntry: null, logFileAbsolutePath: null)
        {
        }

        /// <summary>
        /// Creates a TestLogger with an optional callback and log file path.
        /// オプションのコールバックとログファイルパスを指定して TestLogger を作成します。
        /// </summary>
        /// <param name="onEntry">Optional callback invoked for each log entry. / 各ログエントリで呼び出されるオプションのコールバック。</param>
        /// <param name="logFileAbsolutePath">Simulated log file path (null by default). / シミュレートするログファイルパス（デフォルトは null）。</param>
        public TestLogger(Action<TestLogEntry>? onEntry = null, string? logFileAbsolutePath = null)
        {
            _onEntry = onEntry;
            LogFileAbsolutePath = logFileAbsolutePath;
        }

        /// <inheritdoc />
        public string? LogFileAbsolutePath { get; }

        /// <inheritdoc />
        public LogFormat Format { get; set; } = LogFormat.Text;

        /// <summary>
        /// All captured log entries.
        /// キャプチャされた全ログエントリ。
        /// </summary>
        public List<TestLogEntry> Entries { get; } = new();

        /// <summary>
        /// Convenience projection of captured messages (for simple string-based assertions).
        /// キャプチャされたメッセージの簡易プロジェクション（文字列ベースのアサーション用）。
        /// </summary>
        public IEnumerable<string> Messages => Entries.Select(e => e.Message);

        /// <inheritdoc />
        public void Initialize() { }

        /// <inheritdoc />
        public void CleanupOldLogFiles(int maxLogGenerations) { }

        /// <inheritdoc />
        public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, Exception? exception = null)
            => LogMessage(logLevel, message, shouldOutputMessageToConsole, consoleForegroundColor: null, exception);

        /// <inheritdoc />
        public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, ConsoleColor? consoleForegroundColor, Exception? exception = null)
        {
            var entry = new TestLogEntry(logLevel, message, exception);
            Entries.Add(entry);
            _onEntry?.Invoke(entry);
        }
    }

    /// <summary>
    /// Represents a single log entry captured during testing.
    /// テスト中にキャプチャされた単一のログエントリを表す。
    /// </summary>
    internal sealed record TestLogEntry(AppLogLevel LogLevel, string Message, Exception? Exception);
}
