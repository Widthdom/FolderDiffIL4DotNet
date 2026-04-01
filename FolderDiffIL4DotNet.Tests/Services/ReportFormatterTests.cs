using System.Linq;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.ReportFormatters;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="IReportFormatter"/> implementations.
    /// <see cref="IReportFormatter"/> 実装のテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class ReportFormatterTests
    {
        [Fact]
        public void MarkdownReportFormatter_HasLowestOrder()
        {
            var formatter = new MarkdownReportFormatter(
                new ReportGenerateService(new FileDiffResultLists(), new Helpers.TestLogger(), ReportGenerateService.CreateBuiltInSectionWriters()));
            Assert.Equal(100, formatter.Order);
        }

        [Fact]
        public void MarkdownReportFormatter_IsAlwaysEnabled()
        {
            var formatter = new MarkdownReportFormatter(
                new ReportGenerateService(new FileDiffResultLists(), new Helpers.TestLogger(), ReportGenerateService.CreateBuiltInSectionWriters()));
            var context = CreateMinimalContext();
            Assert.True(formatter.IsEnabled(context));
        }

        [Fact]
        public void HtmlReportFormatter_IsDisabledByDefault()
        {
            var formatter = new HtmlReportFormatter(
                new HtmlReportGenerateService(new FileDiffResultLists(), new Helpers.TestLogger(), new ConfigSettingsBuilder().Build()));
            var context = CreateMinimalContext(shouldGenerateHtml: false);
            Assert.False(formatter.IsEnabled(context));
        }

        [Fact]
        public void HtmlReportFormatter_IsEnabledWhenConfigured()
        {
            var formatter = new HtmlReportFormatter(
                new HtmlReportGenerateService(new FileDiffResultLists(), new Helpers.TestLogger(), new ConfigSettingsBuilder().Build()));
            var context = CreateMinimalContext(shouldGenerateHtml: true);
            Assert.True(formatter.IsEnabled(context));
        }

        [Fact]
        public void AuditLogReportFormatter_IsDisabledByDefault()
        {
            var formatter = new AuditLogReportFormatter(
                new AuditLogGenerateService(new FileDiffResultLists(), new Helpers.TestLogger()));
            var context = CreateMinimalContext(shouldGenerateAuditLog: false);
            Assert.False(formatter.IsEnabled(context));
        }

        [Fact]
        public void SbomReportFormatter_IsDisabledByDefault()
        {
            var formatter = new SbomReportFormatter(
                new SbomGenerateService(new FileDiffResultLists(), new Helpers.TestLogger()));
            var context = CreateMinimalContext(shouldGenerateSbom: false);
            Assert.False(formatter.IsEnabled(context));
        }

        [Fact]
        public void FormatterOrder_MarkdownFirst_HtmlSecond()
        {
            var md = new MarkdownReportFormatter(
                new ReportGenerateService(new FileDiffResultLists(), new Helpers.TestLogger(), ReportGenerateService.CreateBuiltInSectionWriters()));
            var html = new HtmlReportFormatter(
                new HtmlReportGenerateService(new FileDiffResultLists(), new Helpers.TestLogger(), new ConfigSettingsBuilder().Build()));
            var audit = new AuditLogReportFormatter(
                new AuditLogGenerateService(new FileDiffResultLists(), new Helpers.TestLogger()));
            var sbom = new SbomReportFormatter(
                new SbomGenerateService(new FileDiffResultLists(), new Helpers.TestLogger()));

            var ordered = new IReportFormatter[] { sbom, audit, html, md }.OrderBy(f => f.Order).ToList();
            Assert.IsType<MarkdownReportFormatter>(ordered[0]);
            Assert.IsType<HtmlReportFormatter>(ordered[1]);
            Assert.IsType<AuditLogReportFormatter>(ordered[2]);
            Assert.IsType<SbomReportFormatter>(ordered[3]);
        }

        // ── Helper / ヘルパー ──

        private static ReportGenerationContext CreateMinimalContext(
            bool shouldGenerateHtml = false,
            bool shouldGenerateAuditLog = false,
            bool shouldGenerateSbom = false)
        {
            var builder = new ConfigSettingsBuilder
            {
                ShouldGenerateHtmlReport = shouldGenerateHtml,
                ShouldGenerateAuditLog = shouldGenerateAuditLog,
                ShouldGenerateSbom = shouldGenerateSbom
            };
            var config = builder.Build();

            return new ReportGenerationContext(
                oldFolderAbsolutePath: "/virtual/old",
                newFolderAbsolutePath: "/virtual/new",
                reportsFolderAbsolutePath: "/virtual/reports",
                appVersion: "1.0.0",
                elapsedTimeString: "0h 0m 0.0s",
                computerName: "TEST",
                config: config,
                ilCache: null);
        }
    }
}
