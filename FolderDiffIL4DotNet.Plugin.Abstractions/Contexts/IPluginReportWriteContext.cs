namespace FolderDiffIL4DotNet.Plugin.Abstractions
{
    /// <summary>
    /// Plugin-visible context for report section writing.
    /// Exposes a subset of internal report state needed by plugin section writers.
    /// <para>
    /// プラグインから参照可能なレポートセクション書き込みコンテキスト。
    /// プラグインセクションライターが必要とする内部レポート状態のサブセットを公開します。
    /// </para>
    /// </summary>
    public interface IPluginReportWriteContext
    {
        /// <summary>Absolute path to the baseline (old) folder. / 旧フォルダの絶対パス。</summary>
        string OldFolderAbsolutePath { get; }

        /// <summary>Absolute path to the comparison (new) folder. / 新フォルダの絶対パス。</summary>
        string NewFolderAbsolutePath { get; }

        /// <summary>Application version string. / アプリケーションバージョン文字列。</summary>
        string AppVersion { get; }

        /// <summary>Formatted elapsed time string. / 書式化された経過時間文字列。</summary>
        string ElapsedTimeString { get; }

        /// <summary>Name of the executing machine. / 実行マシン名。</summary>
        string ComputerName { get; }

        /// <summary>Whether any SHA256 mismatch was found. / SHA256 不一致が検出されたかどうか。</summary>
        bool HasSha256Mismatch { get; }

        /// <summary>Whether any timestamp regression warning exists. / タイムスタンプ回帰警告が存在するかどうか。</summary>
        bool HasTimestampRegressionWarning { get; }
    }
}
