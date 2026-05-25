namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Specifies the output format for log entries.
    /// ログエントリの出力形式を指定します。
    /// </summary>
    public enum LogFormat
    {
        /// <summary>
        /// Plain-text log format (default). Each line contains a bracketed log level and message.
        /// プレーンテキスト形式（既定）。各行は角括弧付きログレベルとメッセージを含みます。
        /// </summary>
        Text,

        /// <summary>
        /// Structured JSON format. Each line is a self-contained JSON object with timestamp, level,
        /// message, and optional exception fields. Suitable for SIEM and log aggregation tools.
        /// 構造化 JSON 形式。各行はタイムスタンプ、レベル、メッセージ、オプションの例外フィールドを含む
        /// 自己完結 JSON オブジェクト。SIEM やログ集約ツールとの連携に適しています。
        /// </summary>
        Json
    }
}
