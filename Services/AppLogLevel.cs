namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// アプリケーション内で使用するログレベル。
    /// <para>Log severity levels used throughout the application.</para>
    /// </summary>
    public enum AppLogLevel
    {
        /// <summary>
        /// 情報メッセージ。通常の処理フローを記録します。
        /// <para>Informational message recording normal processing flow.</para>
        /// </summary>
        Info,

        /// <summary>
        /// 警告メッセージ。処理は継続しますが、注意が必要な状態を示します。
        /// <para>Warning message indicating a noteworthy condition that does not halt processing.</para>
        /// </summary>
        Warning,

        /// <summary>
        /// エラーメッセージ。処理の中断や失敗を示します。
        /// <para>Error message indicating a processing failure or abort.</para>
        /// </summary>
        Error
    }
}
