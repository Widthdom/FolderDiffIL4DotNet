namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Log severity levels used throughout the application.
    /// アプリケーション内で使用するログレベル。
    /// </summary>
    public enum AppLogLevel
    {
        /// <summary>Informational message. / 情報メッセージ。</summary>
        Info,

        /// <summary>Warning that does not prevent execution. / 実行を妨げない警告。</summary>
        Warning,

        /// <summary>Error that may cause execution to fail. / 実行失敗を引き起こし得るエラー。</summary>
        Error
    }
}
