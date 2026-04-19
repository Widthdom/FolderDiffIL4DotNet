using System;
using System.IO;
using System.Text.Json;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Path-validation and recoverable-failure tests for <see cref="AuditLogGenerateService"/>.
    /// <see cref="AuditLogGenerateService"/> のパス検証と回復可能失敗のテスト。
    /// </summary>
    public sealed partial class AuditLogGenerateServiceTests
    {
        [Fact]
        public void GenerateAuditLog_WhenReportPathIsInvalid_LogsWarningAndDoesNotThrow()
        {
            var logger = new TestLogger();
            var service = new AuditLogGenerateService(_resultLists, logger);
            var oldDir = Path.Combine(_rootDir, "old-invalid-path");
            var newDir = Path.Combine(_rootDir, "new-invalid-path");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);

            var exception = Record.Exception(() =>
                service.GenerateAuditLog(new ReportGenerationContext(
                    oldDir, newDir, "\0invalid-report-dir",
                    appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                    computerName: "test-host",
                    new ConfigSettingsBuilder { ShouldGenerateAuditLog = true }.Build(),
                    ilCache: null)));

            Assert.Null(exception);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("reports folder", warning.Message, StringComparison.Ordinal);
            Assert.Contains(AuditLogGenerateService.AUDIT_LOG_FILE_NAME, warning.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(ArgumentException), warning.Message, StringComparison.Ordinal);
            Assert.NotNull(warning.Exception);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }

        [Fact]
        public void GenerateAuditLog_WhenReportHashReadFails_LogsWarningAndStillWritesAuditLog()
        {
            var logger = new TestLogger();
            var service = new AuditLogGenerateService(_resultLists, logger);
            var (oldDir, newDir, reportDir) = MakeDirs("locked-report-hash");
            var markdownReportPath = Path.Combine(reportDir, "diff_report.md");
            File.WriteAllText(markdownReportPath, "locked-report");

            using var lockStream = new FileStream(markdownReportPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var exception = Record.Exception(() => service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir)));

            Assert.Null(exception);
            var auditLogPath = Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME);
            Assert.True(File.Exists(auditLogPath));
            var doc = JsonDocument.Parse(File.ReadAllText(auditLogPath));
            Assert.Equal(string.Empty, doc.RootElement.GetProperty("reportSha256").GetString());
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Failed to compute Markdown report SHA256", warning.Message, StringComparison.Ordinal);
            Assert.Contains(markdownReportPath, warning.Message, StringComparison.Ordinal);
            Assert.Contains($"ReportsFolder='{reportDir}'", warning.Message, StringComparison.Ordinal);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }

        [Fact]
        public void GenerateAuditLog_WhenHtmlReportHashReadFails_LogsWarningAndStillWritesAuditLog()
        {
            var logger = new TestLogger();
            var service = new AuditLogGenerateService(_resultLists, logger);
            var (oldDir, newDir, reportDir) = MakeDirs("locked-html-report-hash");
            var htmlReportPath = Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME);
            File.WriteAllText(htmlReportPath, "<html>locked-report</html>");

            using var lockStream = new FileStream(htmlReportPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var exception = Record.Exception(() => service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir)));

            Assert.Null(exception);
            var auditLogPath = Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME);
            Assert.True(File.Exists(auditLogPath));
            var doc = JsonDocument.Parse(File.ReadAllText(auditLogPath));
            Assert.Equal(string.Empty, doc.RootElement.GetProperty("htmlReportSha256").GetString());
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Failed to compute HTML report SHA256", warning.Message, StringComparison.Ordinal);
            Assert.Contains(htmlReportPath, warning.Message, StringComparison.Ordinal);
            Assert.Contains($"ReportsFolder='{reportDir}'", warning.Message, StringComparison.Ordinal);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }

        [Fact]
        public void GenerateAuditLog_WhenAddedPathIsInvalid_SkipsBadEntryAndStillWritesAuditLog()
        {
            var logger = new TestLogger();
            var service = new AuditLogGenerateService(_resultLists, logger);
            var (oldDir, newDir, reportDir) = MakeDirs("invalid-added-entry");
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "good.txt"));
            _resultLists.AddAddedFileAbsolutePath("\0bad-added-path");

            var exception = Record.Exception(() => service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir)));

            Assert.Null(exception);
            var auditLogPath = Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME);
            Assert.True(File.Exists(auditLogPath));
            var doc = JsonDocument.Parse(File.ReadAllText(auditLogPath));
            var files = doc.RootElement.GetProperty("files");
            Assert.Contains(files.EnumerateArray(), file => file.GetProperty("relativePath").GetString() == "good.txt");
            Assert.DoesNotContain(files.EnumerateArray(), file => string.Equals(file.GetProperty("relativePath").GetString(), "\0bad-added-path", StringComparison.Ordinal));
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Skipped Added audit log entry", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"Root='{newDir}'", warning.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(ArgumentException), warning.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void GenerateAuditLog_WhenRemovedPathIsInvalid_SkipsBadEntryAndStillWritesAuditLog()
        {
            var logger = new TestLogger();
            var service = new AuditLogGenerateService(_resultLists, logger);
            var (oldDir, newDir, reportDir) = MakeDirs("invalid-removed-entry");
            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "gone.txt"));
            _resultLists.AddRemovedFileAbsolutePath("\0bad-removed-path");

            var exception = Record.Exception(() => service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir)));

            Assert.Null(exception);
            var auditLogPath = Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME);
            Assert.True(File.Exists(auditLogPath));
            var doc = JsonDocument.Parse(File.ReadAllText(auditLogPath));
            var files = doc.RootElement.GetProperty("files");
            Assert.Contains(files.EnumerateArray(), file => file.GetProperty("relativePath").GetString() == "gone.txt");
            Assert.DoesNotContain(files.EnumerateArray(), file => string.Equals(file.GetProperty("relativePath").GetString(), "\0bad-removed-path", StringComparison.Ordinal));
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Skipped Removed audit log entry", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"Root='{oldDir}'", warning.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(ArgumentException), warning.Message, StringComparison.Ordinal);
        }
    }
}
