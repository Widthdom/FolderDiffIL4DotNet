namespace FolderDiffIL4DotNet.Plugin.Abstractions
{
    /// <summary>
    /// Plugin-visible context for report generation.
    /// Exposes paths and metadata needed by plugin report formatters.
    /// <para>
    /// プラグインから参照可能なレポート生成コンテキスト。
    /// プラグインレポートフォーマッターが必要とするパスやメタデータを公開します。
    /// </para>
    /// </summary>
    public interface IPluginReportGenerationContext
    {
        /// <summary>Absolute path to the baseline (old) folder. / 旧フォルダの絶対パス。</summary>
        string OldFolderAbsolutePath { get; }

        /// <summary>Absolute path to the comparison (new) folder. / 新フォルダの絶対パス。</summary>
        string NewFolderAbsolutePath { get; }

        /// <summary>Absolute path to the report output folder. / レポート出力先フォルダの絶対パス。</summary>
        string ReportsFolderAbsolutePath { get; }

        /// <summary>Application version string. / アプリケーションバージョン文字列。</summary>
        string AppVersion { get; }

        /// <summary>Formatted elapsed time string. / 書式化された経過時間文字列。</summary>
        string ElapsedTimeString { get; }

        /// <summary>Name of the executing machine. / 実行マシン名。</summary>
        string ComputerName { get; }
    }
}
