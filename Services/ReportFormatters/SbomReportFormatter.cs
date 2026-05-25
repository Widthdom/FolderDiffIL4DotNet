namespace FolderDiffIL4DotNet.Services.ReportFormatters
{
    /// <summary>
    /// <see cref="IReportFormatter"/> adapter for SBOM (Software Bill of Materials) generation.
    /// Delegates to <see cref="SbomGenerateService.GenerateSbom"/>.
    /// SBOM（ソフトウェア部品表）生成用の <see cref="IReportFormatter"/> アダプター。
    /// <see cref="SbomGenerateService.GenerateSbom"/> に委譲します。
    /// </summary>
    internal sealed class SbomReportFormatter : IReportFormatter
    {
        private readonly SbomGenerateService _inner;

        public SbomReportFormatter(SbomGenerateService inner)
        {
            _inner = inner;
        }

        /// <inheritdoc />
        public string FormatId => "sbom";

        /// <inheritdoc />
        /// <remarks>Order 400: runs last among built-in formatters. / 順序 400: 組み込みフォーマッターの最後に実行。</remarks>
        public int Order => 400;

        /// <inheritdoc />
        public bool IsEnabled(ReportGenerationContext context) => context.Config.ShouldGenerateSbom;

        /// <inheritdoc />
        public void Generate(ReportGenerationContext context) => _inner.GenerateSbom(context);
    }
}
