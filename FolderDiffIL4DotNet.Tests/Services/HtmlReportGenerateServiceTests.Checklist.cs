using System;
using System.IO;
using System.Text.Json;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// User-defined checklist rendering tests for <see cref="HtmlReportGenerateService"/>.
    /// <see cref="HtmlReportGenerateService"/> のユーザー定義チェックリスト描画テスト。
    /// </summary>
    public sealed partial class HtmlReportGenerateServiceTests
    {
        [Fact]
        public void GenerateDiffReportHtml_WithChecklistFile_RendersReviewChecklistSection()
        {
            using var appDataScope = CreateAppDataOverrideScope();
            var (oldDir, newDir, reportDir) = MakeDirs("checklist-present");
            WriteChecklistFile(
                appDataScope,
                "Confirm release metadata.",
                "Verify migration notes\nwhen schema version changes.");

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("Review Checklist", html);
            Assert.Contains("Checklist Item", html);
            Assert.Contains("checklist_cb_0", html);
            Assert.Contains("checklist_notes_0", html);
            Assert.Contains("Toggle all checklist checkboxes", html);
            Assert.Contains("Verify migration notes\nwhen schema version changes.", html);
            Assert.Contains("--col-checklist-item-w", html);
            Assert.Contains("--col-checklist-notes-w", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_WhenChecklistFileMissing_OmitsReviewChecklistSection()
        {
            using var appDataScope = CreateAppDataOverrideScope();
            var (oldDir, newDir, reportDir) = MakeDirs("checklist-missing");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.DoesNotContain("<h2 class=\"section-heading\">Review Checklist", html);
            Assert.DoesNotContain("checklist_cb_0", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_WhenChecklistFileContainsOnlyBlankItems_OmitsReviewChecklistSection()
        {
            using var appDataScope = CreateAppDataOverrideScope();
            var (oldDir, newDir, reportDir) = MakeDirs("checklist-empty");
            WriteChecklistFile(appDataScope, " ", "\r\n\t");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.DoesNotContain("<h2 class=\"section-heading\">Review Checklist", html);
            Assert.DoesNotContain("checklist_notes_0", html);
        }

        private static AppDataOverrideScope CreateAppDataOverrideScope()
            => new(Path.Combine(Path.GetTempPath(), "fd-html-appdata-" + Guid.NewGuid().ToString("N")));

        private static void WriteChecklistFile(AppDataOverrideScope appDataScope, params string[] items)
        {
            string checklistPath = appDataScope.HtmlReportChecklistFileAbsolutePath;
            Directory.CreateDirectory(Path.GetDirectoryName(checklistPath)!);
            File.WriteAllText(checklistPath, JsonSerializer.Serialize(items));
        }
    }
}
