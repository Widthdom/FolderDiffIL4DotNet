using System;
using System.IO;
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
            Assert.Contains(nameof(ArgumentException), warning.Message, StringComparison.Ordinal);
            Assert.NotNull(warning.Exception);
        }
    }
}
