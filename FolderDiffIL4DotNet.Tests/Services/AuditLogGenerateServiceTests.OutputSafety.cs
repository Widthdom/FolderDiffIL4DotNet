using System;
using System.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="AuditLogGenerateService"/> overwrite safety.
    /// <see cref="AuditLogGenerateService"/> の上書き安全性テスト。
    /// </summary>
    public sealed partial class AuditLogGenerateServiceTests
    {
        [Fact]
        public void GenerateAuditLog_WhenExistingAuditLogIsReadOnly_OverwritesIt()
        {
            var logger = new TestLogger();
            var service = new AuditLogGenerateService(_resultLists, logger);
            var (oldDir, newDir, reportDir) = MakeDirs("readonly-overwrite");

            service.GenerateAuditLog(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host",
                new ConfigSettingsBuilder { ShouldGenerateAuditLog = true }.Build(),
                ilCache: null));

            var auditLogPath = Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME);
            var firstContent = File.ReadAllText(auditLogPath);

            service.GenerateAuditLog(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "2.0.0", elapsedTimeString: "0h 0m 2.0s",
                computerName: "test-host",
                new ConfigSettingsBuilder { ShouldGenerateAuditLog = true }.Build(),
                ilCache: null));

            var secondContent = File.ReadAllText(auditLogPath);
            Assert.Contains("\"appVersion\": \"2.0.0\"", secondContent, StringComparison.Ordinal);
            Assert.DoesNotContain("\"appVersion\": \"1.0.0\"", secondContent, StringComparison.Ordinal);
            Assert.NotEqual(firstContent, secondContent);
            Assert.DoesNotContain(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
        }
    }
}
