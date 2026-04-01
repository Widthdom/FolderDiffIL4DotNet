namespace FolderDiffIL4DotNet.Plugin.Abstractions
{
    /// <summary>
    /// Abstraction for custom report output formats provided by plugins.
    /// Each implementation generates a single output file from the diff results.
    /// <para>
    /// プラグインが提供するカスタムレポート出力形式の抽象化。
    /// 各実装は差分結果から単一の出力ファイルを生成します。
    /// </para>
    /// </summary>
    public interface IPluginReportFormatter
    {
        /// <summary>
        /// Unique format identifier (e.g. "pdf", "junit-xml").
        /// 一意のフォーマット識別子（例: "pdf", "junit-xml"）。
        /// </summary>
        string FormatId { get; }

        /// <summary>
        /// Execution order. Lower values run first. Built-in formatters use 100-400.
        /// 実行順序。値が小さいほど先に実行。組み込みフォーマッターは 100-400 を使用。
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Returns whether this formatter should run for the current configuration.
        /// この設定でフォーマッターを実行すべきかどうかを返します。
        /// </summary>
        /// <param name="context">Plugin-visible report generation context. / プラグインから参照可能なレポート生成コンテキスト。</param>
        bool IsEnabled(IPluginReportGenerationContext context);

        /// <summary>
        /// Generates the report output file.
        /// レポート出力ファイルを生成します。
        /// </summary>
        /// <param name="context">Plugin-visible report generation context. / プラグインから参照可能なレポート生成コンテキスト。</param>
        void Generate(IPluginReportGenerationContext context);
    }
}
