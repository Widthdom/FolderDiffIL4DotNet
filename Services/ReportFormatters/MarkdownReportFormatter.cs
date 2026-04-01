namespace FolderDiffIL4DotNet.Services.ReportFormatters
{
    /// <summary>
    /// <see cref="IReportFormatter"/> adapter for Markdown report generation.
    /// Delegates to <see cref="ReportGenerateService.GenerateDiffReport"/>.
    /// Markdown レポート生成用の <see cref="IReportFormatter"/> アダプター。
    /// <see cref="ReportGenerateService.GenerateDiffReport"/> に委譲します。
    /// </summary>
    internal sealed class MarkdownReportFormatter : IReportFormatter
    {
        private readonly ReportGenerateService _inner;

        public MarkdownReportFormatter(ReportGenerateService inner)
        {
            _inner = inner;
        }

        /// <inheritdoc />
        public string FormatId => "markdown";

        /// <inheritdoc />
        /// <remarks>Markdown report is always generated (order 100). / Markdown レポートは常に生成されます（順序 100）。</remarks>
        public int Order => 100;

        /// <inheritdoc />
        public bool IsEnabled(ReportGenerationContext context) => true;

        /// <inheritdoc />
        public void Generate(ReportGenerationContext context) => _inner.GenerateDiffReport(context);
    }
}
