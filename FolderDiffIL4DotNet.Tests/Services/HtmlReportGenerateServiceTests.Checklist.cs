using System;
using System.Collections.Generic;
using System.IO;
using FolderDiffIL4DotNet.Services;
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
            var (oldDir, newDir, reportDir) = MakeDirs("checklist-present");
            var checklistItems = new List<string>
            {
                "Confirm release metadata.",
                "Verify migration notes\nwhen schema version changes."
            };

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config, reviewChecklistItems: checklistItems));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("Review Checklist", html);
            Assert.Contains("Checklist Item", html);
            Assert.Contains("checklist_cb_0", html);
            Assert.Contains("checklist_notes_0", html);
            Assert.Contains("<textarea id=\"checklist_notes_0\" class=\"checklist-notes-input\"", html);
            Assert.Contains("Toggle all checklist checkboxes", html);
            Assert.Contains("Verify migration notes\nwhen schema version changes.", html);
            Assert.Contains("checklist-item-text-singleline", html);
            Assert.Contains("checklist-item-text-multiline", html);
            Assert.Contains(".checklist-item-text-multiline {\n      white-space: pre; word-break: normal;", html);
            Assert.Contains("--col-checklist-item-w", html);
            Assert.Contains("--col-checklist-notes-w", html);
            Assert.Contains("display: block; width: 100%; min-height: 1.45em; max-height: 10.5em;", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_WhenChecklistFileMissing_OmitsReviewChecklistSection()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("checklist-missing");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config, reviewChecklistItems: Array.Empty<string>()));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.DoesNotContain("<h2 class=\"section-heading\">Review Checklist", html);
            Assert.DoesNotContain("checklist_cb_0", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_WhenChecklistFileContainsOnlyBlankItems_OmitsReviewChecklistSection()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("checklist-empty");
            var config = CreateConfig();
            var checklistItems = new List<string>();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config, reviewChecklistItems: checklistItems));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.DoesNotContain("<h2 class=\"section-heading\">Review Checklist", html);
            Assert.DoesNotContain("checklist_notes_0", html);
        }
    }
}
