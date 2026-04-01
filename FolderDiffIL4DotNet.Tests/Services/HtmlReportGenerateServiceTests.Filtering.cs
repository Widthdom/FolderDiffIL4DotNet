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
    /// Filtering feature tests.
    /// フィルタリング機能テスト。
    /// </summary>
    public sealed partial class HtmlReportGenerateServiceTests
    {
        // ── Filtering feature tests ─────────────────────────────────────────

        [Fact]
        public void GenerateDiffReportHtml_ContainsFilterBar()
        {
            // Arrange / テスト準備
            var (oldDir, newDir, reportDir) = MakeDirs("filter-bar");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: filter controls are present inside controls bar
            // フィルターコントロールがコントロールバー内に含まれていることを検証
            Assert.Contains("class=\"controls\"", html);
            Assert.Contains("id=\"filter-imp-high\"", html);
            Assert.Contains("id=\"filter-imp-medium\"", html);
            Assert.Contains("id=\"filter-imp-low\"", html);
            Assert.Contains("id=\"filter-unchecked\"", html);
            Assert.Contains("id=\"filter-search\"", html);
            Assert.Contains("applyFilters()", html);
            Assert.Contains("resetFilters()", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_FilterBarIsInsideCtrlMarkers()
        {
            // Arrange / テスト準備
            var (oldDir, newDir, reportDir) = MakeDirs("filter-ctrl");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: filter zone is OUTSIDE <!--CTRL-->...<!--/CTRL--> markers (only button row is inside)
            // フィルターゾーンは <!--CTRL-->...<!--/CTRL--> マーカーの外にある（ボタン行のみ内部）ことを検証
            int ctrlStart = html.IndexOf("<!--CTRL-->", StringComparison.Ordinal);
            int ctrlEnd = html.IndexOf("<!--/CTRL-->", StringComparison.Ordinal);
            int filterSearch = html.IndexOf("id=\"filter-search\"", StringComparison.Ordinal);
            Assert.True(ctrlStart >= 0, "<!--CTRL--> marker not found");
            Assert.True(ctrlEnd > ctrlStart, "<!--/CTRL--> marker not found or before <!--CTRL-->");
            Assert.True(filterSearch > ctrlEnd,
                "filter zone should be OUTSIDE <!--CTRL-->...<!--/CTRL--> markers so it is kept in reviewed mode");
        }

        [Fact]
        public void GenerateDiffReportHtml_ThemeToggleInsideCtrlAndReviewedBanner()
        {
            // Arrange / テスト準備
            var (oldDir, newDir, reportDir) = MakeDirs("theme-toggle");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: theme toggle button exists inside CTRL markers (replaced by reviewed banner which includes it)
            // テーマ切替ボタンが CTRL マーカー内に存在し、reviewed バナー置換時にも含まれることを検証
            Assert.Contains("id=\"theme-toggle\"", html);
            Assert.Contains("cycleTheme()", html);
            Assert.Contains("initTheme()", html);
            int themeBtn = html.IndexOf("id=\"theme-toggle\"", StringComparison.Ordinal);
            int ctrlStart = html.IndexOf("<!--CTRL-->", StringComparison.Ordinal);
            int ctrlEnd = html.IndexOf("<!--/CTRL-->", StringComparison.Ordinal);
            Assert.True(themeBtn > ctrlStart && themeBtn < ctrlEnd,
                "theme toggle should be inside CTRL markers (reviewed banner replacement includes it)");

            // Assert: CTRL markers wrap entire ctrl-buttons div cleanly (no orphaned closing tags)
            // CTRL マーカーが ctrl-buttons div 全体を正しく囲んでいることを検証（孤立した閉じタグなし）
            string ctrlContent = html.Substring(ctrlStart, ctrlEnd - ctrlStart + "<!--/CTRL-->".Length);
            Assert.Contains("class=\"ctrl-buttons\"", ctrlContent);
            Assert.Contains("class=\"ctrl-actions\"", ctrlContent);
        }

        [Fact]
        public void GenerateDiffReportHtml_CelebrationAnimationIncludedInOutput()
        {
            // Arrange / テスト準備
            var (oldDir, newDir, reportDir) = MakeDirs("celebrate");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: celebration CSS and JS are present in generated HTML
            // セレブレーション CSS と JS が生成 HTML に含まれることを検証
            Assert.Contains("celebrate-container", html);
            Assert.Contains("celebrate-rise", html);
            Assert.Contains("celebrateCompletion", html);
            Assert.Contains("__celebrationFired__", html);
            Assert.Contains("prefers-reduced-motion", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_FileRowsHaveDataSectionAttribute()
        {
            // Arrange: add some files to generate rows
            // いくつかのファイルを追加して行を生成
            var (oldDir, newDir, reportDir) = MakeDirs("data-section");
            File.WriteAllText(Path.Combine(newDir, "new.dll"), "new-content");
            var config = CreateConfig();
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "new.dll"));

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: data-section attribute is present on rows
            // data-section 属性が行に存在することを検証
            Assert.Contains("data-section=\"add\"", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_ModifiedRowsWithImportance_HaveDataImportanceAttribute()
        {
            // Arrange: add a modified file with importance
            // 重要度付きの変更ファイルを追加
            var (oldDir, newDir, reportDir) = MakeDirs("data-imp");
            string fileName = "lib.dll";
            File.WriteAllBytes(Path.Combine(oldDir, fileName), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(newDir, fileName), new byte[] { 4, 5, 6 });
            var config = CreateConfig();
            _resultLists.AddModifiedFileRelativePath(fileName);
            _resultLists.RecordDiffDetail(fileName, FileDiffResultLists.DiffDetailResult.SHA256Mismatch);

            // Set up importance via semantic changes
            var summary = new AssemblySemanticChangesSummary
            {
                Entries = new[]
                {
                    new MemberChangeEntry("[ + ]", "TestClass", "System.Object", "public", "", "Method", "GetValue", "", "System.String", "", "", ChangeImportance.High)
                }
            };
            _resultLists.FileRelativePathToAssemblySemanticChanges[fileName] = summary;

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: data-importance attribute present on the modified row
            // 変更行に data-importance 属性が存在することを検証
            Assert.Contains("data-importance=\"High\"", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_FilterBarCss_ContainsFilterHiddenRule()
        {
            // Arrange / テスト準備
            var (oldDir, newDir, reportDir) = MakeDirs("filter-css");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: CSS rules for filter-hidden are present
            // filter-hidden の CSS ルールが含まれていることを検証
            Assert.Contains("tr.filter-hidden", html);
            Assert.Contains("tr.diff-row.filter-hidden-parent", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_JsContainsApplyFiltersFunction()
        {
            // Arrange / テスト準備
            var (oldDir, newDir, reportDir) = MakeDirs("filter-js");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: JS filtering functions are included
            // JS フィルタリング関数が含まれていることを検証
            Assert.Contains("function applyFilters()", html);
            Assert.Contains("function resetFilters()", html);
            Assert.Contains("function copyPath(", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_JsCollectState_ExcludesFilterIds()
        {
            // Arrange / テスト準備
            var (oldDir, newDir, reportDir) = MakeDirs("filter-exclude");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: __filterIds__ array exists in the JS to exclude filter inputs from collectState
            // collectState からフィルタ入力を除外するための __filterIds__ 配列の存在を検証
            Assert.Contains("__filterIds__", html);
            Assert.Contains("filter-imp-high", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_DownloadReviewed_ClearsFilterHiddenClasses()
        {
            // Arrange / テスト準備
            var (oldDir, newDir, reportDir) = MakeDirs("filter-download");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: downloadReviewed() contains filter-hidden clearing logic
            // downloadReviewed() にフィルタ非表示クリアロジックが含まれていることを検証
            Assert.Contains("tr.filter-hidden", html);
            Assert.Contains("tr.filter-hidden-parent", html);
            // The clearing should happen before outerHTML capture
            // outerHTML キャプチャ前にクリアが行われるべき
            int clearIdx = html.IndexOf("classList.remove('filter-hidden')", StringComparison.Ordinal);
            int outerHtmlIdx = html.IndexOf("document.documentElement.outerHTML", StringComparison.Ordinal);
            Assert.True(clearIdx >= 0, "Filter-hidden clearing code not found in JS");
            Assert.True(outerHtmlIdx > clearIdx,
                "Filter-hidden clearing must occur before outerHTML capture");
        }

        [Fact]
        public void GenerateDiffReportHtml_DownloadReviewed_RestoresFiltersAfterCapture()
        {
            // Arrange / テスト準備
            var (oldDir, newDir, reportDir) = MakeDirs("filter-restore");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: applyFilters() is called after outerHTML capture to restore live page filter state
            // outerHTML キャプチャ後に applyFilters() が呼ばれ、ライブページのフィルタ状態が復元されることを検証
            int outerHtmlIdx = html.IndexOf("document.documentElement.outerHTML", StringComparison.Ordinal);
            int restoreIdx = html.IndexOf("document.body.style.color = savedBodyColor", StringComparison.Ordinal);
            int applyFiltersIdx = html.IndexOf("applyFilters();", restoreIdx, StringComparison.Ordinal);
            Assert.True(restoreIdx > outerHtmlIdx,
                "State restoration must appear after outerHTML capture");
            Assert.True(applyFiltersIdx > outerHtmlIdx,
                "applyFilters() must be called after outerHTML capture to restore live filter state");
        }

        [Fact]
        public void GenerateDiffReportHtml_HeaderShowsDisassemblerAvailabilityTable()
        {
            // Arrange: populate availability probe results
            // 利用可否プローブ結果を設定
            _resultLists.DisassemblerAvailability = new List<DisassemblerProbeResult>
            {
                new("dotnet-ildasm", true, "0.12.2", "/usr/local/bin/dotnet-ildasm"),
                new("ilspycmd", false, null, null),
            };

            var (oldDir, newDir, reportDir) = MakeDirs("disasm-avail");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: table structure and content present in HTML
            // テーブルの構造と内容が HTML に含まれていることを検証
            Assert.Contains("dotnet-ildasm", html);
            Assert.Contains("ilspycmd", html);
            Assert.Contains("class=\"status-available\"", html); // green for Available / Available 用の緑
            Assert.Contains("class=\"status-unavailable\"", html); // red for Not Available / Not Available 用の赤
            Assert.Contains("0.12.2", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_HeaderOmitsAvailabilityTable_WhenProbeResultsAreNull()
        {
            // Arrange: no probe results (default: null)
            // プローブ結果なし（既定値: null）
            var (oldDir, newDir, reportDir) = MakeDirs("no-probe-html");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: no availability table when probe results are null
            // プローブ結果が null の場合ツール名は出力されない
            Assert.DoesNotContain("dotnet-ildasm", html);
            Assert.DoesNotContain("ilspycmd", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_ContainsProgressBar_WithCorrectTotalFiles()
        {
            // Arrange: create files across multiple sections / 複数セクションにファイルを作成
            var (oldDir, newDir, reportDir) = MakeDirs("progress-bar");

            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "new.dll"));
            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "old.dll"));
            _resultLists.AddModifiedFileRelativePath("changed.dll");
            _resultLists.RecordDiffDetail("changed.dll", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _resultLists.AddUnchangedFileRelativePath("same.dll");

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: progress bar HTML elements present / プログレスバーHTML要素が存在すること
            Assert.Contains("progress-bar-fill", html);
            Assert.Contains("progress-text", html);
            Assert.Contains("progress-wrap", html);
            // Assert: total = 1 added + 1 removed + 1 modified + 1 SHA256Mismatch warning = 4
            // (Unchanged is excluded from progress count)
            // 合計 = 追加1 + 削除1 + 変更1 + SHA256Mismatch警告1 = 4（Unchangedは進捗カウントから除外）
            Assert.Contains("const __totalFiles__      = 4;", html);
            // Assert: updateProgress function present / updateProgress関数が存在すること
            Assert.Contains("updateProgress()", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_ControlsUseResponsiveProgressAndActionGroups()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("responsive-controls");

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            Assert.Contains("class=\"ctrl-progress\"", html);
            Assert.Contains("class=\"ctrl-actions\"", html);
            Assert.Contains("@media (max-width: 1100px)", html);
            Assert.Contains("@media (max-width: 720px)", html);

            int progressIdx = html.IndexOf("class=\"ctrl-progress\"", StringComparison.Ordinal);
            int actionsIdx = html.IndexOf("class=\"ctrl-actions\"", StringComparison.Ordinal);
            Assert.True(progressIdx >= 0 && actionsIdx > progressIdx,
                "progress group should appear before action buttons so buttons can wrap beneath it on narrow windows");
        }

        [Fact]
        public void GenerateDiffReportHtml_ProgressBar_ExcludesUnchangedAndIgnored()
        {
            // Arrange: add ignored/unchanged files — they should not affect progress total
            // Ignored/Unchangedファイルを追加 — 進捗合計に影響しないこと
            var (oldDir, newDir, reportDir) = MakeDirs("progress-no-ign");

            _resultLists.AddModifiedFileRelativePath("changed.dll");
            _resultLists.RecordDiffDetail("changed.dll", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _resultLists.AddUnchangedFileRelativePath("same.dll");
            _resultLists.RecordIgnoredFile("ignored.log", FileDiffResultLists.IgnoredFileLocation.Old);

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // totalFiles = 1 modified + 1 SHA256Mismatch warning = 2 (Unchanged/Ignored excluded)
            // totalFiles = 変更1 + SHA256Mismatch警告1 = 2（Unchanged/Ignoredは除外）
            Assert.Contains("const __totalFiles__      = 2;", html);
        }

        // -----------------------------------------------------------------------
        // Disassembler warning banners (HTML)
        // 逆アセンブラ警告バナー（HTML）
        // -----------------------------------------------------------------------

        [Fact]
        public void GenerateDiffReportHtml_WarnsWhenNoDisassemblerAvailable()
        {
            _resultLists.DisassemblerAvailability = new List<DisassemblerProbeResult>
            {
                new("dotnet-ildasm", false, null, null),
                new("ilspycmd", false, null, null),
            };

            var (oldDir, newDir, reportDir) = MakeDirs("no-disasm-html");
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, CreateConfig()));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("No disassembler tool is available", html);
            Assert.Contains("warn-danger", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_NoDisassemblerWarning_WhenOneAvailable()
        {
            _resultLists.DisassemblerAvailability = new List<DisassemblerProbeResult>
            {
                new("dotnet-ildasm", true, "0.12.0", "/usr/bin/dotnet-ildasm"),
                new("ilspycmd", false, null, null),
            };

            var (oldDir, newDir, reportDir) = MakeDirs("one-disasm-html");
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, CreateConfig()));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.DoesNotContain("No disassembler tool is available", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_WarnsWhenMultipleDisassemblersUsed()
        {
            _resultLists.DisassemblerToolVersions["dotnet-ildasm (version: 0.12.0)"] = 0;
            _resultLists.DisassemblerToolVersions["ilspycmd (version: 8.2.0)"] = 0;

            var (oldDir, newDir, reportDir) = MakeDirs("mixed-disasm-html");
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, CreateConfig()));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("Multiple disassembler tools were used", html);
            Assert.Contains("warn-caution", html);
            // Version info must be included in the warning / 警告にバージョン情報が含まれること
            Assert.Contains("dotnet-ildasm (version: 0.12.0)", html);
            Assert.Contains("ilspycmd (version: 8.2.0)", html);
            Assert.Contains("clearing the IL cache", html);
        }

    }
}
