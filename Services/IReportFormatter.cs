namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Abstraction for report output formats (Markdown, HTML, audit log, SBOM, or custom plugin formats).
    /// Each implementation generates a single output file from the diff results.
    /// Implementations are registered in DI and invoked by <see cref="Runner.DiffPipelineExecutor"/>.
    /// <para>
    /// レポート出力形式の抽象化（Markdown、HTML、監査ログ、SBOM、またはカスタムプラグイン形式）。
    /// 各実装は差分結果から単一の出力ファイルを生成します。
    /// 実装は DI に登録され、<see cref="Runner.DiffPipelineExecutor"/> から呼び出されます。
    /// </para>
    /// </summary>
    public interface IReportFormatter
    {
        /// <summary>
        /// Unique format identifier (e.g. "markdown", "html", "audit-log", "sbom").
        /// Used for logging and diagnostics.
        /// 一意のフォーマット識別子（例: "markdown", "html", "audit-log", "sbom"）。
        /// ログや診断に使用されます。
        /// </summary>
        string FormatId { get; }

        /// <summary>
        /// Execution order. Formatters with lower values run first.
        /// Some formatters depend on outputs from earlier ones (e.g. audit log reads report hashes).
        /// 実行順序。値が小さいフォーマッターが先に実行されます。
        /// 一部のフォーマッターは先行出力に依存します（例: 監査ログがレポートハ���シュを読む）。
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Returns whether this formatter should run for the current configuration.
        /// この設定でフォーマッターを実行すべきかどうかを返します。
        /// </summary>
        /// <param name="context">Report generation context. / レポート生成コンテキスト。</param>
        /// <returns><see langword="true"/> if the formatter should run; otherwise <see langword="false"/>.</returns>
        bool IsEnabled(ReportGenerationContext context);

        /// <summary>
        /// Generates the report output file.
        /// レポート出力ファイルを生成します。
        /// </summary>
        /// <param name="context">Report generation context. / レポート生成コンテキスト。</param>
        void Generate(ReportGenerationContext context);
    }
}
