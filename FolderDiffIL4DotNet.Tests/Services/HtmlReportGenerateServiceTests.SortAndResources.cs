using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Legend, stat-table, diff-row, copy button, sort order, and embedded resource tests.
    /// 凡例、統計テーブル、差分行、コピーボタン、ソート順、埋め込みリソーステスト。
    /// </summary>
    public sealed partial class HtmlReportGenerateServiceTests
    {
        // ── Req1: Legend table / 凡例テーブル ─────────────────────────────────

        [Fact]
        public void GenerateDiffReportHtml_LegendSection_UsesTableFormat()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("legend-table");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("filter-table", html);
            Assert.Contains("<table class=\"filter-table\" aria-label=\"Diff Detail filters\">", html);
        }

        // ── Req2: stat-table borders / 統計テーブルボーダー ────────────────────

        [Fact]
        public void GenerateDiffReportHtml_StatTable_HasVisibleBorders()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("stat-border");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("border: 1px solid var(--color-border-light)", html);
        }

        // ── Req4: InlineDiffMaxEditDistance code tag / code タグ ──────────────

        [Fact]
        public void GenerateDiffReportHtml_EditDistanceSkipped_InlineDiffMaxEditDistanceHasCodeTag()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("edit-dist-code-tag");

            File.WriteAllLines(Path.Combine(oldDir, "huge.txt"), Enumerable.Range(1, 2001).Select(i => $"old{i}"));
            File.WriteAllLines(Path.Combine(newDir, "huge.txt"), Enumerable.Range(1, 2001).Select(i => $"new{i}"));

            _resultLists.AddModifiedFileRelativePath("huge.txt");
            _resultLists.RecordDiffDetail("huge.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig(enableInlineDiff: true);

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("<code>InlineDiffMaxEditDistance</code>", html);
            Assert.Contains("current value:", html);
        }

        // ── Req5: diff-row background / 差分行背景色 ─────────────────────────

        [Fact]
        public void GenerateDiffReportHtml_DiffRowBackground_UsesDarkerColor()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("diff-row-bg");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // diff-row background should use CSS variable, not hardcoded hex / diff-row の背景は CSS 変数を使用すること
            Assert.Contains("tr.diff-row { background: var(--color-diff-row-bg); }", html);
            Assert.DoesNotContain("tr.diff-row { background: #f6f8fa; }", html);
        }

        // ── Req7: Copy paths button / コピーボタン ──────────────────────────────

        [Fact]
        public void GenerateDiffReportHtml_FilePathRow_HasCopyButton()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("copy-btn");

            _resultLists.AddModifiedFileRelativePath("src/app.dll");
            _resultLists.RecordDiffDetail("src/app.dll", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("btn-copy-path", html);
            Assert.Contains("copyPath", html);
            Assert.Contains("path-text", html);
        }

        // ── Req8: Row hover highlight / 行ホバーハイライト ──────────────────────

        [Fact]
        public void GenerateDiffReportHtml_RowHover_HasLightPurpleHighlight()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("hover-highlight");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains(":not(.stat-table):not(.legend-table):not(.il-ignore-table) > tbody tr:not(.diff-row):not(.diff-hunk-tr):not(.diff-del-tr):not(.diff-add-tr):hover { background: var(--color-surface-hover); }", html);
            Assert.Contains("table.semantic-changes-table tbody tr:hover td { background: var(--color-surface-hover) !important; }", html);
        }

        // ── Sort order: Unchanged files / Unchanged ファイルのソート順 ─────────

        /// <summary>
        /// Verifies that HTML Unchanged files are sorted by SHA256Match → ILMatch → TextMatch, then by File Path ascending.
        /// HTML の Unchanged ファイルが SHA256Match → ILMatch → TextMatch の順でソートされることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_UnchangedFiles_SortedByDiffDetailThenPath()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("unch-sort");
            var config = CreateConfig(enableInlineDiff: false);

            _resultLists.AddUnchangedFileRelativePath("zzz-text.config");
            _resultLists.RecordDiffDetail("zzz-text.config", FileDiffResultLists.DiffDetailResult.TextMatch);
            _resultLists.AddUnchangedFileRelativePath("aaa-sha256.bin");
            _resultLists.RecordDiffDetail("aaa-sha256.bin", FileDiffResultLists.DiffDetailResult.SHA256Match);
            _resultLists.AddUnchangedFileRelativePath("bbb-il.dll");
            _resultLists.RecordDiffDetail("bbb-il.dll", FileDiffResultLists.DiffDetailResult.ILMatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.AddUnchangedFileRelativePath("ccc-sha256.bin");
            _resultLists.RecordDiffDetail("ccc-sha256.bin", FileDiffResultLists.DiffDetailResult.SHA256Match);
            _resultLists.AddUnchangedFileRelativePath("aaa-text.txt");
            _resultLists.RecordDiffDetail("aaa-text.txt", FileDiffResultLists.DiffDetailResult.TextMatch);

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Expected order: SHA256Match (aaa-sha256.bin, ccc-sha256.bin), ILMatch (bbb-il.dll), TextMatch (aaa-text.txt, zzz-text.config)
            int sha256_aaa = html.IndexOf("aaa-sha256.bin", StringComparison.Ordinal);
            int sha256_ccc = html.IndexOf("ccc-sha256.bin", StringComparison.Ordinal);
            int il_bbb = html.IndexOf("bbb-il.dll", StringComparison.Ordinal);
            int text_aaa = html.IndexOf("aaa-text.txt", StringComparison.Ordinal);
            int text_zzz = html.IndexOf("zzz-text.config", StringComparison.Ordinal);

            Assert.True(sha256_aaa < sha256_ccc, "SHA256Match files should be sorted by path (aaa < ccc)");
            Assert.True(sha256_ccc < il_bbb, "SHA256Match should appear before ILMatch");
            Assert.True(il_bbb < text_aaa, "ILMatch should appear before TextMatch");
            Assert.True(text_aaa < text_zzz, "TextMatch files should be sorted by path (aaa < zzz)");
        }

        // ── Sort order: Modified files / Modified ファイルのソート順 ─────────

        /// <summary>
        /// Verifies that HTML Modified files are sorted by TextMismatch → ILMismatch → SHA256Mismatch, then by File Path ascending.
        /// HTML の Modified ファイルが TextMismatch → ILMismatch → SHA256Mismatch の順でソートされることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_ModifiedFiles_SortedByDiffDetailThenPath()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("mod-sort");
            var config = CreateConfig(enableInlineDiff: false);

            _resultLists.AddModifiedFileRelativePath("zzz-sha256.bin");
            _resultLists.RecordDiffDetail("zzz-sha256.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _resultLists.AddModifiedFileRelativePath("aaa-il.dll");
            _resultLists.RecordDiffDetail("aaa-il.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.AddModifiedFileRelativePath("bbb-text.config");
            _resultLists.RecordDiffDetail("bbb-text.config", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.AddModifiedFileRelativePath("ccc-il.dll");
            _resultLists.RecordDiffDetail("ccc-il.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.AddModifiedFileRelativePath("aaa-text.txt");
            _resultLists.RecordDiffDetail("aaa-text.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Expected order: TextMismatch (aaa-text.txt, bbb-text.config), ILMismatch (aaa-il.dll, ccc-il.dll), SHA256Mismatch (zzz-sha256.bin)
            int text_aaa = html.IndexOf("aaa-text.txt", StringComparison.Ordinal);
            int text_bbb = html.IndexOf("bbb-text.config", StringComparison.Ordinal);
            int il_aaa = html.IndexOf("aaa-il.dll", StringComparison.Ordinal);
            int il_ccc = html.IndexOf("ccc-il.dll", StringComparison.Ordinal);
            int sha256_zzz = html.IndexOf("zzz-sha256.bin", StringComparison.Ordinal);

            Assert.True(text_aaa < text_bbb, "TextMismatch files should be sorted by path (aaa < bbb)");
            Assert.True(text_bbb < il_aaa, "TextMismatch should appear before ILMismatch");
            Assert.True(il_aaa < il_ccc, "ILMismatch files should be sorted by path (aaa < ccc)");
            Assert.True(il_ccc < sha256_zzz, "ILMismatch should appear before SHA256Mismatch");
        }

        // ── Sort order: Warnings timestamp-regressed table / 警告タイムスタンプ逆行テーブルのソート順 ─────────

        /// <summary>
        /// Verifies that HTML Warnings timestamp-regressed table is sorted by TextMismatch → ILMismatch → SHA256Mismatch, then by File Path ascending.
        /// HTML の警告タイムスタンプ逆行テーブルが TextMismatch → ILMismatch → SHA256Mismatch の順でソートされることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_WarningsTimestampRegressed_SortedByDiffDetailThenPath()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("warn-sort");
            var builder = CreateConfigBuilder(enableInlineDiff: false);
            builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = true;
            var config = builder.Build();

            _resultLists.AddModifiedFileRelativePath("zzz-sha256.bin");
            _resultLists.RecordDiffDetail("zzz-sha256.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("zzz-sha256.bin", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            _resultLists.AddModifiedFileRelativePath("aaa-il.dll");
            _resultLists.RecordDiffDetail("aaa-il.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("aaa-il.dll", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            _resultLists.AddModifiedFileRelativePath("bbb-text.config");
            _resultLists.RecordDiffDetail("bbb-text.config", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("bbb-text.config", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Only look at the Warnings section (after "new file timestamps older than old")
            int warningsSectionStart = html.IndexOf("new file timestamps older than old", StringComparison.Ordinal);
            Assert.True(warningsSectionStart >= 0, "new file timestamps older than old section should exist");
            string warningsSection = html.Substring(warningsSectionStart);

            int text_bbb = warningsSection.IndexOf("bbb-text.config", StringComparison.Ordinal);
            int il_aaa = warningsSection.IndexOf("aaa-il.dll", StringComparison.Ordinal);
            int sha256_zzz = warningsSection.IndexOf("zzz-sha256.bin", StringComparison.Ordinal);

            Assert.True(text_bbb < il_aaa, "TextMismatch should appear before ILMismatch in Warnings");
            Assert.True(il_aaa < sha256_zzz, "ILMismatch should appear before SHA256Mismatch in Warnings");
        }

        // ── Embedded resource tests ─────────────────────────────────────────

        [Fact]
        public void LoadEmbeddedResource_CssResource_ReturnsNonEmptyString()
        {
            var css = HtmlReportGenerateService.LoadEmbeddedResource("FolderDiffIL4DotNet.Services.HtmlReport.diff_report.css");
            Assert.False(string.IsNullOrWhiteSpace(css));
            Assert.Contains("box-sizing", css);
        }

        [Fact]
        public void LoadEmbeddedResource_JsStateModule_ReturnsNonEmptyString()
        {
            var js = HtmlReportGenerateService.LoadEmbeddedResource("FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_state.js");
            Assert.False(string.IsNullOrWhiteSpace(js));
            Assert.Contains("function", js);
        }

        [Fact]
        public void LoadEmbeddedResource_JsStateModule_ContainsPlaceholders()
        {
            var js = HtmlReportGenerateService.LoadEmbeddedResource("FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_state.js");
            Assert.Contains("{{STORAGE_KEY}}", js);
            Assert.Contains("{{REPORT_DATE}}", js);
        }

        [Fact]
        public void LoadEmbeddedResource_AllJsModules_ReturnNonEmptyString()
        {
            // Verify all JS module embedded resources are loadable
            // 全 JS モジュール埋め込みリソースがロード可能なことを検証
            var moduleNames = new[]
            {
                "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_state.js",
                "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_export.js",
                "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_diffview.js",
                "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_lazy.js",
                "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_layout.js",
                "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_filter.js",
                "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_excel.js",
                "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_theme.js",
                "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_init.js",
            };
            foreach (var name in moduleNames)
            {
                var js = HtmlReportGenerateService.LoadEmbeddedResource(name);
                Assert.False(string.IsNullOrWhiteSpace(js), $"Module {name} should not be empty");
            }
        }

        [Fact]
        public void LoadEmbeddedResource_InvalidResource_ThrowsFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() =>
                HtmlReportGenerateService.LoadEmbeddedResource("NonExistent.Resource.Name"));
        }

    }
}
