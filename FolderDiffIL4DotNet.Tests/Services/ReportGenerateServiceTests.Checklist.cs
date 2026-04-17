using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for user-defined review checklist output in the Markdown report.
    /// Markdown レポートのユーザー定義レビューチェックリスト出力テスト。
    /// </summary>
    public sealed partial class ReportGenerateServiceTests
    {
        [Fact]
        public void GenerateDiffReport_WithChecklistFile_WritesReviewChecklistTable()
        {
            var oldDir = Path.Combine(_rootDir, "old-checklist");
            var newDir = Path.Combine(_rootDir, "new-checklist");
            var reportDir = Path.Combine(_rootDir, "report-checklist");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);
            var checklistItems = new List<string>
            {
                "Confirm version.json and release notes are aligned.",
                "Verify upgrade guide steps.\nInclude rollback notes if applicable."
            };

            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, CreateConfig(), reviewChecklistItems: checklistItems));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("## Review Checklist", reportText);
            Assert.Contains("| ✓ | Checklist Item | Notes |", reportText);
            Assert.Contains("| ☐ | Confirm version.json and release notes are aligned. | |", reportText);
            Assert.Contains("Verify upgrade guide steps.<br>Include rollback notes if applicable.", reportText);
        }

        [Fact]
        public void GenerateDiffReport_WhenChecklistFileMissing_OmitsReviewChecklistTable()
        {
            var oldDir = Path.Combine(_rootDir, "old-checklist-missing");
            var newDir = Path.Combine(_rootDir, "new-checklist-missing");
            var reportDir = Path.Combine(_rootDir, "report-checklist-missing");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, CreateConfig(), reviewChecklistItems: Array.Empty<string>()));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.DoesNotContain("## Review Checklist", reportText);
            Assert.DoesNotContain("| ✓ | Checklist Item | Notes |", reportText);
        }
    }
}
