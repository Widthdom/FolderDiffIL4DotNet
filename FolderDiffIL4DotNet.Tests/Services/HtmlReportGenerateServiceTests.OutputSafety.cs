using System;
using System.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Output safety and recoverable-failure tests for <see cref="HtmlReportGenerateService"/>.
    /// <see cref="HtmlReportGenerateService"/> の出力安全性と回復可能失敗のテスト。
    /// </summary>
    public sealed partial class HtmlReportGenerateServiceTests
    {
        [Fact]
        public void GenerateDiffReportHtml_WhenExistingHtmlIsReadOnly_OverwritesIt()
        {
            var logger = new TestLogger();
            var service = new HtmlReportGenerateService(_resultLists, logger, CreateConfig());
            var (oldDir, newDir, reportDir) = MakeDirs("readonly-overwrite");

            service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, CreateConfig()));
            var htmlPath = Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME);
            var firstContent = File.ReadAllText(htmlPath);

            service.GenerateDiffReportHtml(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "2.0", elapsedTimeString: "0h 0m 2.0s",
                computerName: "test-host-second", CreateConfig(), ilCache: null));

            var secondContent = File.ReadAllText(htmlPath);
            Assert.Contains("test-host-second", secondContent, StringComparison.Ordinal);
            Assert.DoesNotContain("0h 0m 1.0s", secondContent, StringComparison.Ordinal);
            Assert.NotEqual(firstContent, secondContent);
            Assert.DoesNotContain(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
        }

        [Fact]
        public void GenerateDiffReportHtml_WhenReportPathIsInvalid_LogsWarningAndDoesNotThrow()
        {
            var logger = new TestLogger();
            var service = new HtmlReportGenerateService(_resultLists, logger, CreateConfig());
            var oldDir = Path.Combine(_rootDir, "old-invalid-path");
            var newDir = Path.Combine(_rootDir, "new-invalid-path");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);

            var exception = Record.Exception(() =>
                service.GenerateDiffReportHtml(new ReportGenerationContext(
                    oldDir, newDir, "\0invalid-report-dir",
                    appVersion: "1.0", elapsedTimeString: "0h 0m 1.0s",
                    computerName: "test-host", CreateConfig(), ilCache: null)));

            Assert.Null(exception);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("reports folder", warning.Message, StringComparison.Ordinal);
            Assert.Contains(HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME, warning.Message, StringComparison.Ordinal);
            Assert.Contains("IsPathRooted=", warning.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(ArgumentException), warning.Message, StringComparison.Ordinal);
            Assert.NotNull(warning.Exception);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }
    }
}
