using System;
using System.IO;
using System.Reflection;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="ReportGenerateService"/> output overwrite safety and failure logging.
    /// <see cref="ReportGenerateService"/> の出力上書き安全性と失敗ログのテスト。
    /// </summary>
    public sealed partial class ReportGenerateServiceTests
    {
        [Fact]
        public void GenerateDiffReport_WhenExistingReportIsReadOnly_OverwritesIt()
        {
            var logger = new TestLogger();
            var service = new ReportGenerateService(_resultLists, logger, ReportGenerateService.CreateBuiltInSectionWriters());
            var oldDir = Path.Combine(_rootDir, "old-readonly-report");
            var newDir = Path.Combine(_rootDir, "new-readonly-report");
            var reportDir = Path.Combine(_rootDir, "report-readonly-report");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("app.dll");
            _resultLists.RecordDiffDetail("app.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm");

            service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s", computerName: "test-host",
                CreateConfig(), ilCache: null));

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var firstContent = File.ReadAllText(reportPath);

            service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "2.0.0", elapsedTimeString: "0h 0m 2.0s", computerName: "test-host",
                CreateConfig(), ilCache: null));

            var secondContent = File.ReadAllText(reportPath);
            Assert.Contains("2.0.0", secondContent, StringComparison.Ordinal);
            Assert.DoesNotContain("1.0.0", secondContent, StringComparison.Ordinal);
            Assert.NotEqual(firstContent, secondContent);
            Assert.DoesNotContain(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
        }

        [Fact]
        public void GenerateDiffReport_WhenWriteFails_DoesNotEmitReadOnlyWarningForMissingOutput()
        {
            var logger = new TestLogger();
            var service = new ReportGenerateService(_resultLists, logger, ReportGenerateService.CreateBuiltInSectionWriters());
            var oldDir = Path.Combine(_rootDir, "old-report-failure");
            var newDir = Path.Combine(_rootDir, "new-report-failure");
            var reportDir = Path.Combine(_rootDir, "missing", "report-failure");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);

            var exception = Assert.Throws<DirectoryNotFoundException>(() =>
                service.GenerateDiffReport(new ReportGenerationContext(
                    oldDir, newDir, reportDir,
                    appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s", computerName: "test-host",
                    CreateConfig(), ilCache: null)));

            var error = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Error);
            Assert.Contains("reports folder", error.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(DirectoryNotFoundException), error.Message, StringComparison.Ordinal);
            Assert.Contains(reportDir, error.Message, StringComparison.Ordinal);
            Assert.Same(exception, error.Exception);
            Assert.DoesNotContain(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
        }

        [Fact]
        public void LogReportProtectionWarning_IncludesReportsFolderContext()
        {
            var logger = new TestLogger();
            var service = new ReportGenerateService(_resultLists, logger, ReportGenerateService.CreateBuiltInSectionWriters());
            var method = typeof(ReportGenerateService).GetMethod("LogReportProtectionWarning", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            method.Invoke(service, ["/tmp/reports", "/tmp/reports/diff_report.md", new ArgumentException("bad path")]);

            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("reports folder '/tmp/reports'", warning.Message, StringComparison.Ordinal);
            Assert.Contains("diff_report.md", warning.Message, StringComparison.Ordinal);
            Assert.Contains("IsPathRooted=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(ArgumentException), warning.Message, StringComparison.Ordinal);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }
    }
}
