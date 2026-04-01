namespace FolderDiffIL4DotNet.Services.ReportFormatters
{
    /// <summary>
    /// <see cref="IReportFormatter"/> adapter for interactive HTML report generation.
    /// Delegates to <see cref="HtmlReportGenerateService.GenerateDiffReportHtml"/>.
    /// インタラクティブ HTML レポート生成用の <see cref="IReportFormatter"/> アダプター。
    /// <see cref="HtmlReportGenerateService.GenerateDiffReportHtml"/> に委譲します。
    /// </summary>
    internal sealed class HtmlReportFormatter : IReportFormatter
    {
        private readonly HtmlReportGenerateService _inner;

        public HtmlReportFormatter(HtmlReportGenerateService inner)
        {
            _inner = inner;
        }

        /// <inheritdoc />
        public string FormatId => "html";

        /// <inheritdoc />
        /// <remarks>
        /// Order 200: runs after Markdown so audit log can hash both.
        /// 順序 200: Markdown の後に実行し、監査ログが両方のハッシュを計算可能にする。
        /// </remarks>
        public int Order => 200;

        /// <inheritdoc />
        public bool IsEnabled(ReportGenerationContext context) => context.Config.ShouldGenerateHtmlReport;

        /// <inheritdoc />
        public void Generate(ReportGenerationContext context) => _inner.GenerateDiffReportHtml(context);
    }
}
