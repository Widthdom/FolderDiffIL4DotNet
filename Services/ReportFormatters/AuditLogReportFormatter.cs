namespace FolderDiffIL4DotNet.Services.ReportFormatters
{
    /// <summary>
    /// <see cref="IReportFormatter"/> adapter for structured JSON audit log generation.
    /// Delegates to <see cref="AuditLogGenerateService.GenerateAuditLog"/>.
    /// JSON 監査ログ生成用の <see cref="IReportFormatter"/> アダプター。
    /// <see cref="AuditLogGenerateService.GenerateAuditLog"/> に委譲します。
    /// </summary>
    internal sealed class AuditLogReportFormatter : IReportFormatter
    {
        private readonly AuditLogGenerateService _inner;

        public AuditLogReportFormatter(AuditLogGenerateService inner)
        {
            _inner = inner;
        }

        /// <inheritdoc />
        public string FormatId => "audit-log";

        /// <inheritdoc />
        /// <remarks>
        /// Order 300: runs after Markdown and HTML so it can read their SHA256 hashes.
        /// 順序 300: Markdown と HTML の後に実行し、SHA256 ハッシュを読み取れるようにする。
        /// </remarks>
        public int Order => 300;

        /// <inheritdoc />
        public bool IsEnabled(ReportGenerationContext context) => context.Config.ShouldGenerateAuditLog;

        /// <inheritdoc />
        public void Generate(ReportGenerationContext context) => _inner.GenerateAuditLog(context);
    }
}
