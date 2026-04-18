using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Inline diff tests for HtmlReportGenerateService.
    /// HtmlReportGenerateService のインライン diff テスト。
    /// </summary>
    public sealed partial class HtmlReportGenerateServiceTests
    {
        // ── Inline diff / インラインdiff ──────────────────────────────────────

        [Fact]
        public void GenerateDiffReportHtml_TextMismatch_WithRealFiles_ShowsInlineDiff()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-basic");

            // Create actual text files so the inline diff engine can read and compare them
            // インライン diff エンジンが読み取り比較できるように実ファイルを作成する
            File.WriteAllLines(Path.Combine(oldDir, "app.txt"), new[] { "line1", "old-line2", "line3" });
            File.WriteAllLines(Path.Combine(newDir, "app.txt"), new[] { "line1", "new-line2", "line3" });

            _resultLists.AddModifiedFileRelativePath("app.txt");
            _resultLists.RecordDiffDetail("app.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig(enableInlineDiff: true);
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("<details", html);
            Assert.Contains("diff-summary", html);
            Assert.Contains("diff-del-td", html);
            Assert.Contains("diff-add-td", html);
            Assert.Contains("old-line2", html);
            Assert.Contains("new-line2", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_TextMismatch_EnableInlineDiffFalse_NoDetailsElement()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-disabled");

            File.WriteAllLines(Path.Combine(oldDir, "app.txt"), new[] { "line1", "old-line2" });
            File.WriteAllLines(Path.Combine(newDir, "app.txt"), new[] { "line1", "new-line2" });

            _resultLists.AddModifiedFileRelativePath("app.txt");
            _resultLists.RecordDiffDetail("app.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig(enableInlineDiff: false);
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.DoesNotContain("<details", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_ILMismatch_NoInlineDiff()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-il");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            var config = CreateConfig(enableInlineDiff: true);
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // ILMismatch without IL.txt files: IL text is unavailable, so no inline diff is rendered
            // ILMismatch で IL.txt ファイルがない場合、IL テキストが利用不可のためインライン diff は描画されない
            Assert.DoesNotContain("<details", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_ILMismatch_WithILTextFiles_ShowsInlineDiff()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-il-with-files");

            // Create IL.txt files in the expected report structure (Reports/IL/old and /new)
            // 期待されるレポート構造（Reports/IL/old と /new）に IL.txt ファイルを作成する
            string ilOldDir = Path.Combine(reportDir, "IL", "old");
            string ilNewDir = Path.Combine(reportDir, "IL", "new");
            Directory.CreateDirectory(ilOldDir);
            Directory.CreateDirectory(ilNewDir);
            File.WriteAllLines(Path.Combine(ilOldDir, "lib.dll_IL.txt"), new[] { "old-il-line1", "common-line" });
            File.WriteAllLines(Path.Combine(ilNewDir, "lib.dll_IL.txt"), new[] { "new-il-line1", "common-line" });

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            var config = CreateConfig(enableInlineDiff: true);
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("<details", html);
            Assert.Contains("Show IL diff", html);
            Assert.Contains("old-il-line1", html);
            Assert.Contains("new-il-line1", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_TextMismatch_DiffTooLarge_ShowsSkippedMessage()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-large");

            // Prepare a file where diff output exceeds maxDiffLines (set to 1).
            // 2 changed lines produce 5 diff lines (1 hunk header + 2 del + 2 add) > 1, so the diff is skipped.
            // diff 出力が maxDiffLines を超えるファイルを用意する（maxDiffLines=1 に設定）。
            // 変更 2 行で diff 出力 5 行（ハンクヘッダ 1 + 削除 2 + 追加 2）> 1 となりスキップされる。
            File.WriteAllLines(Path.Combine(oldDir, "big.txt"), new[] { "A", "B" });
            File.WriteAllLines(Path.Combine(newDir, "big.txt"), new[] { "A-changed", "B-changed" });

            _resultLists.AddModifiedFileRelativePath("big.txt");
            _resultLists.RecordDiffDetail("big.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var builder = CreateConfigBuilder(enableInlineDiff: true);
            builder.InlineDiffMaxDiffLines = 1;  // 5 diff lines > 1 limit => skip / diff 出力 5 行 > 制限 1 → スキップ
            var config = builder.Build();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("diff-skipped", html);
            Assert.Contains("InlineDiffMaxDiffLines", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_TextMismatch_EditDistanceTooLarge_ShowsSkippedMessageWithoutExpandArrow()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-edit-distance-large");

            // All 2001 lines differ => edit distance D = 4002 > default InlineDiffMaxEditDistance (4000).
            // Returns a single Truncated line; skip message is shown directly without an expand arrow.
            // 2001 行すべて異なり編集距離 D = 4002 > 既定の InlineDiffMaxEditDistance (4000)。
            // Truncated 1 行のみ返り、矢印なしで skip メッセージを直接表示する。
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
            Assert.Contains("diff-skipped", html);
            Assert.Contains("edit distance too large", html);
            Assert.Contains("InlineDiffMaxEditDistance", html);
            // Shown directly without a <details> expand arrow
            // <details> 展開矢印なしで直接表示される
            Assert.DoesNotContain("<details", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_TextMismatch_MissingFile_SkipsGracefully()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-missing");

            // No files created => IO error => no diff lines (should not crash)
            // ファイル未作成 → IO エラー → 差分行なし（クラッシュしない）
            _resultLists.AddModifiedFileRelativePath("ghost.txt");
            _resultLists.RecordDiffDetail("ghost.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig(enableInlineDiff: true);
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            // Report should be generated without crashing
            // クラッシュせずレポートが生成される
            Assert.True(File.Exists(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME)));
        }

        [Fact]
        public void GenerateDiffReportHtml_TextMismatch_InvalidRelativePath_SkipsAndLogsWarning()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var service = new HtmlReportGenerateService(_resultLists, logger, new ConfigSettingsBuilder().Build());
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-invalid-text-path");
            var relPath = "bad\0name.txt";

            _resultLists.AddModifiedFileRelativePath(relPath);
            _resultLists.RecordDiffDetail(relPath, FileDiffResultLists.DiffDetailResult.TextMismatch);

            var ex = Record.Exception(() => service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, CreateConfig(enableInlineDiff: true))));

            Assert.Null(ex);
            Assert.True(File.Exists(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME)));
            var entry = Assert.Single(logger.Entries, e => e.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Inline diff skipped", entry.Message, StringComparison.Ordinal);
            Assert.Contains("TextMismatch", entry.Message, StringComparison.Ordinal);
            Assert.Contains("ArgumentException", entry.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void GenerateDiffReportHtml_ILMismatch_InvalidRelativePath_SkipsAndLogsWarning()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var service = new HtmlReportGenerateService(_resultLists, logger, new ConfigSettingsBuilder().Build());
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-invalid-il-path");
            var relPath = new string('a', 5000) + ".dll";

            _resultLists.AddModifiedFileRelativePath(relPath);
            _resultLists.RecordDiffDetail(relPath, FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            var ex = Record.Exception(() => service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, CreateConfig(enableInlineDiff: true))));

            Assert.Null(ex);
            Assert.True(File.Exists(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME)));
            var entry = Assert.Single(logger.Entries, e => e.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Inline diff skipped", entry.Message, StringComparison.Ordinal);
            Assert.Contains("ILMismatch", entry.Message, StringComparison.Ordinal);
            Assert.Contains("ArgumentException", entry.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void GenerateDiffReportHtml_TextMismatch_InlineDiff_SummaryContainsAddedRemoved()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-summary");

            File.WriteAllLines(Path.Combine(oldDir, "f.txt"), new[] { "removed-line", "common" });
            File.WriteAllLines(Path.Combine(newDir, "f.txt"), new[] { "added-line", "common" });

            _resultLists.AddModifiedFileRelativePath("f.txt");
            _resultLists.RecordDiffDetail("f.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig(enableInlineDiff: true);
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Summary should contain +N / -M counts
            // summary に +N / -M カウントが含まれる
            Assert.Contains("+1", html);
            Assert.Contains("-1", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_InlineDiffSummary_UsesSameOneBasedNumberAsLeftmostColumn()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-numbering");

            File.WriteAllLines(Path.Combine(oldDir, "a.txt"), new[] { "old-a" });
            File.WriteAllLines(Path.Combine(newDir, "a.txt"), new[] { "new-a" });
            File.WriteAllLines(Path.Combine(oldDir, "b.txt"), new[] { "old-b" });
            File.WriteAllLines(Path.Combine(newDir, "b.txt"), new[] { "new-b" });

            _resultLists.AddModifiedFileRelativePath("a.txt");
            _resultLists.RecordDiffDetail("a.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.AddModifiedFileRelativePath("b.txt");
            _resultLists.RecordDiffDetail("b.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig(enableInlineDiff: true);
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("<td class=\"col-no\">1</td>", html);
            Assert.Contains("<td class=\"col-no\">2</td>", html);
            Assert.Contains(">#1 ", html);
            Assert.Contains(">#2 ", html);
            Assert.DoesNotContain(">#0 ", html);
            Assert.Contains("Show diff", html);
        }

        // Verify DIFF REASON / Location / Timestamp body cells have text-align: center, but Notes column does not
        // DIFF REASON・Location・Timestamp 列ボディセルに text-align: center が設定され、Notes 列には設定されていないことを確認する
        [Fact]
        public void GenerateDiffReportHtml_BodyCells_ColReasonPathTs_HaveCenterAlignment()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("col-align");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Justification / Timestamp / Diff Reason columns are center-aligned
            // Justification / Timestamp / Diff Reason 列は中央揃え
            Assert.Contains("td.col-reason { overflow: hidden; text-align: center; }", html);
            Assert.Contains("td.col-ts    { white-space: nowrap; text-align: center; }", html);
            Assert.Contains("min-width: 10.8em; text-align: center; }", html); // col-diff has text-align: center
            // File Path is NOT center-aligned / File Path 列は中央揃えではない
            Assert.Contains("td.col-path { white-space: nowrap; overflow: hidden; }", html);
            // Notes column is NOT center-aligned / Notes 列は中央揃えではない
            Assert.Contains("td.col-notes  { overflow: hidden; }", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_TimestampColumn_UsesCompactWidthVariable()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("ts-width");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("--col-ts-w: 24em;", html);
            Assert.Contains("col.col-ts-g     { width: var(--col-ts-w); }", html);
            Assert.Contains("'col-ts-g': px('--col-ts-w', 24),", html);
        }
    }
}
