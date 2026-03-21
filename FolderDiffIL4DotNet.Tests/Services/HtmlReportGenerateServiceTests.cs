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
    public sealed class HtmlReportGenerateServiceTests : IDisposable
    {
        private readonly string _rootDir;
        private readonly FileDiffResultLists _resultLists = new();
        private readonly ILoggerService _logger = new LoggerService();
        private readonly HtmlReportGenerateService _service;

        public HtmlReportGenerateServiceTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-html-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
            _service = new HtmlReportGenerateService(_resultLists, _logger, new ConfigSettings());
        }

        public void Dispose()
        {
            _resultLists.ResetAll();
            try
            {
                if (Directory.Exists(_rootDir))
                    Directory.Delete(_rootDir, recursive: true);
            }
            catch
            {
                // ignore cleanup errors / クリーンアップエラーを無視
            }
        }

        [Fact]
        public void GenerateDiffReportHtml_CreatesFile()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("creates-file");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", config);

            Assert.True(File.Exists(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME)));
        }

        [Fact]
        public void GenerateDiffReportHtml_ShouldGenerateHtmlReport_False_SkipsGeneration()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("skip-gen");
            var config = CreateConfig();
            config.ShouldGenerateHtmlReport = false;

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            Assert.False(File.Exists(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME)));
        }

        [Fact]
        public void GenerateDiffReportHtml_ModifiedSection_ContainsCheckboxInputs()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("modified-cb");

            _resultLists.AddModifiedFileRelativePath("src/App.dll");
            _resultLists.RecordDiffDetail("src/App.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("type=\"checkbox\"", html);
            Assert.Contains("cb_mod_0", html);
            Assert.Contains("reason_mod_0", html);
            Assert.Contains("notes_mod_0", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_ModifiedSection_ShowsILMismatchAndDisassemblerLabel()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("modified-il");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.2)");

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("ILMismatch", html);
            Assert.Contains("dotnet-ildasm (version: 0.12.2)", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_SummarySection_ContainsCorrectCounts()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("summary-counts");

            var oldFiles = new List<string>
            {
                Path.Combine(oldDir, "a.txt"), Path.Combine(oldDir, "b.txt"), Path.Combine(oldDir, "c.txt")
            };
            var newFiles = new List<string>
            {
                Path.Combine(newDir, "a.txt"), Path.Combine(newDir, "b.txt"), Path.Combine(newDir, "d.txt")
            };
            _resultLists.SetOldFilesAbsolutePath(oldFiles);
            _resultLists.SetNewFilesAbsolutePath(newFiles);

            _resultLists.AddUnchangedFileRelativePath("a.txt");
            _resultLists.RecordDiffDetail("a.txt", FileDiffResultLists.DiffDetailResult.MD5Match);
            _resultLists.AddModifiedFileRelativePath("b.txt");
            _resultLists.RecordDiffDetail("b.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "c.txt"));
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "d.txt"));

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("stat-label\">Unchanged</td>", html);
            Assert.Contains("stat-label\">Added</td>", html);
            Assert.Contains("stat-label\">Removed</td>", html);
            Assert.Contains("stat-label\">Modified</td>", html);
            Assert.Contains("stat-value\">1</td>", html);
            Assert.Contains("3 (Old) vs 3 (New)", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_AddedSection_UsesGreenColor()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("added-color");

            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "new-file.txt"));

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("#22863a", html);
            Assert.Contains("[ + ] Added Files", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_RemovedSection_UsesRedColor()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("removed-color");

            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "old-file.txt"));

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("#b31d28", html);
            Assert.Contains("[ - ] Removed Files", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_ContainsSavedStateSentinel_ForDownload()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sentinel");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("const __savedState__  = null;", html);
            Assert.Contains("downloadReviewed", html);
            Assert.Contains("localStorage", html);
        }

        [Fact]
        public void HtmlEncode_EscapesAllSpecialCharacters()
        {
            Assert.Equal("&amp;&lt;&gt;&quot;&#39;", HtmlReportGenerateService.HtmlEncode("&<>\"'"));
        }

        [Fact]
        public void HtmlEncode_ReturnsEmpty_ForNullOrEmptyInput()
        {
            Assert.Equal(string.Empty, HtmlReportGenerateService.HtmlEncode(null));
            Assert.Equal(string.Empty, HtmlReportGenerateService.HtmlEncode(""));
        }

        [Fact]
        public void GenerateDiffReportHtml_PathWithSpecialChars_IsHtmlEncoded()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("html-encode");

            _resultLists.AddModifiedFileRelativePath("src/<Module>.dll");
            _resultLists.RecordDiffDetail("src/<Module>.dll", FileDiffResultLists.DiffDetailResult.MD5Mismatch);

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Raw angle brackets must be HTML-escaped in file path cells
            // ファイルパスセルでは生の山括弧が HTML エスケープされていなければならない
            Assert.Contains("&lt;Module&gt;", html);
            Assert.DoesNotContain("<Module>", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_Md5MismatchWarning_AppearsInWarningsSection()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("md5-warning");

            _resultLists.AddModifiedFileRelativePath("payload.bin");
            _resultLists.RecordDiffDetail("payload.bin", FileDiffResultLists.DiffDetailResult.MD5Mismatch);

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("section-heading\">", html);
            Assert.Contains("Warnings</h2>", html);
            Assert.Contains("MD5Mismatch", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_Header_ContainsMyersDiffAlgorithmReference()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("myers-ref");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("Myers Diff Algorithm", html);
            Assert.Contains("http://www.xmailserver.org/diff2.pdf", html);
            Assert.Contains("Algorithmica", html);
        }

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
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

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
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

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
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

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
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

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

            var config = CreateConfig(enableInlineDiff: true);
            config.InlineDiffMaxDiffLines = 1;  // 5 diff lines > 1 limit => skip / diff 出力 5 行 > 制限 1 → スキップ

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

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

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

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
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            // Report should be generated without crashing
            // クラッシュせずレポートが生成される
            Assert.True(File.Exists(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME)));
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
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

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
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

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

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Justification / Timestamp / Diff Reason columns are center-aligned
            // Justification / Timestamp / Diff Reason 列は中央揃え
            Assert.Contains("td.col-reason { overflow: hidden; text-align: center; }", html);
            Assert.Contains("td.col-ts    { white-space: nowrap; text-align: center; }", html);
            Assert.Contains("min-width: 9em; text-align: center; }", html); // col-diff has text-align: center
            // File Path is NOT center-aligned / File Path 列は中央揃えではない
            Assert.Contains("td.col-path { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }", html);
            // Notes column is NOT center-aligned / Notes 列は中央揃えではない
            Assert.Contains("td.col-notes  { overflow: hidden; }", html);
        }

        // ── Helpers / ヘルパー ──────────────────────────────────────────────────

        private (string oldDir, string newDir, string reportDir) MakeDirs(string label)
        {
            string old = Path.Combine(_rootDir, "old-" + label);
            string @new = Path.Combine(_rootDir, "new-" + label);
            string report = Path.Combine(_rootDir, "report-" + label);
            Directory.CreateDirectory(old);
            Directory.CreateDirectory(@new);
            Directory.CreateDirectory(report);
            return (old, @new, report);
        }

        [Fact]
        public void GenerateDiffReportHtml_WithILCacheStats_IncludesILCacheStatsSection()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("il-cache-stats");
            var config = CreateConfig();
            config.ShouldIncludeILCacheStatsInReport = true;
            var ilCache = new ILCache(ilCacheDirectoryAbsolutePath: null);

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config, ilCache);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("IL Cache Stats", html);
            Assert.Contains("Hit Rate", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_WithIgnoredFiles_IncludesIgnoredSection()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("ignored-files");
            var ignoredFile = "ignored.txt";
            File.WriteAllText(Path.Combine(oldDir, ignoredFile), "content");
            _resultLists.RecordIgnoredFile(ignoredFile, FileDiffResultLists.IgnoredFileLocation.Old);

            var config = CreateConfig();
            config.ShouldIncludeIgnoredFiles = true;

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("ignored.txt", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_WithIgnoredFilesBothSides_AndTimestamps_IncludesTimestamp()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("ignored-timestamps");
            var ignoredFile = "ts-ignored.txt";
            File.WriteAllText(Path.Combine(oldDir, ignoredFile), "old-content");
            File.WriteAllText(Path.Combine(newDir, ignoredFile), "new-content");
            _resultLists.RecordIgnoredFile(ignoredFile,
                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New);

            var config = CreateConfig();
            config.ShouldIncludeIgnoredFiles = true;
            config.ShouldOutputFileTimestamps = true;

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("ts-ignored.txt", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_WithUnchangedFilesAndTimestamps_IncludesTimestamps()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("unchanged-timestamps");
            var file = "same.txt";
            File.WriteAllText(Path.Combine(oldDir, file), "content");
            File.WriteAllText(Path.Combine(newDir, file), "content");
            _resultLists.AddUnchangedFileRelativePath(file);

            var config = CreateConfig();
            config.ShouldOutputFileTimestamps = true;

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("same.txt", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_WithIgnoredFileNewSideOnly_AndTimestamps()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("ignored-new-only");
            var ignoredFile = "new-only-ignored.txt";
            File.WriteAllText(Path.Combine(newDir, ignoredFile), "new-content");
            _resultLists.RecordIgnoredFile(ignoredFile, FileDiffResultLists.IgnoredFileLocation.New);

            var config = CreateConfig();
            config.ShouldIncludeIgnoredFiles = true;
            config.ShouldOutputFileTimestamps = true;

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("new-only-ignored.txt", html);
        }

        // ── Lazy Render / 遅延レンダリング ────────────────────────────────────

        [Fact]
        public void GenerateDiffReportHtml_LazyRender_DiffContentStoredInDataAttribute()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("lazy-render-attr");

            File.WriteAllLines(Path.Combine(oldDir, "app.txt"), new[] { "line1", "old-line2", "line3" });
            File.WriteAllLines(Path.Combine(newDir, "app.txt"), new[] { "line1", "new-line2", "line3" });

            _resultLists.AddModifiedFileRelativePath("app.txt");
            _resultLists.RecordDiffDetail("app.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig(enableInlineDiff: true, lazyRender: true);
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // The <details> element should have a data-diff-html attribute (base64-encoded diff content)
            // <details> 要素に data-diff-html 属性（base64 エンコード済み diff コンテンツ）があるはず
            Assert.Contains("data-diff-html=\"", html);
            // The summary (with "+N / -N") should still be in the raw HTML / summary は生 HTML に残る
            Assert.Contains("diff-summary", html);
            Assert.Contains("Show diff", html);
            // Raw diff content should NOT be inline in HTML (stored in base64 attribute instead)
            // 生 diff コンテンツは HTML にインライン展開されず、base64 属性に格納される
            Assert.DoesNotContain("old-line2", html);
            Assert.DoesNotContain("new-line2", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_LazyRender_DataAttributeDecodesCorrectly()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("lazy-render-decode");

            File.WriteAllLines(Path.Combine(oldDir, "f.txt"), new[] { "removed-line", "common" });
            File.WriteAllLines(Path.Combine(newDir, "f.txt"), new[] { "added-line", "common" });

            _resultLists.AddModifiedFileRelativePath("f.txt");
            _resultLists.RecordDiffDetail("f.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig(enableInlineDiff: true, lazyRender: true);
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Extract and decode the base64 value from data-diff-html
            // data-diff-html から base64 値を抽出してデコードする
            var match = System.Text.RegularExpressions.Regex.Match(html, @"data-diff-html=""([^""]+)""");
            Assert.True(match.Success, "Expected data-diff-html attribute in HTML");

            var decodedHtml = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups[1].Value));
            Assert.Contains("diff-del-td", decodedHtml);
            Assert.Contains("diff-add-td", decodedHtml);
            Assert.Contains("removed-line", decodedHtml);
            Assert.Contains("added-line", decodedHtml);
        }

        [Fact]
        public void GenerateDiffReportHtml_LazyRender_False_DiffContentIsInline()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("lazy-render-false");

            File.WriteAllLines(Path.Combine(oldDir, "app.txt"), new[] { "line1", "old-line2" });
            File.WriteAllLines(Path.Combine(newDir, "app.txt"), new[] { "line1", "new-line2" });

            _resultLists.AddModifiedFileRelativePath("app.txt");
            _resultLists.RecordDiffDetail("app.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig(enableInlineDiff: true, lazyRender: false);
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.DoesNotContain("data-diff-html=", html);
            Assert.Contains("diff-del-td", html);
            Assert.Contains("old-line2", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_LazyRender_JsSetupFunctionPresent()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("lazy-render-js");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("setupLazyDiff", html);
            Assert.Contains("decodeDiffHtml", html);
            Assert.Contains("data-diff-html", html);  // JS references the attribute name
        }

        // ── Assembly Semantic Changes / アセンブリ意味変更 ─────────────────────

        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_ShowsInlineAboveILDiff()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("semantic-changes");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "MyApp.Service", "", "public", "", "Method", "NewMethod", "", "void", "string name", ""),
                    new("Modified", "MyApp.Service", "", "public", "virtual", "Method", "ExistingMethod", "", "bool", "int id", "Changed"),
                    new("Added", "MyApp.Service", "", "public", "", "Property", "NewProp", "string", "", "", ""),
                    new("Removed", "MyApp.Service", "", "private", "readonly", "Field", "_oldField", "int", "", "", ""),
                },
            };

            var config = CreateConfig(enableInlineDiff: true);
            config.ShouldIncludeAssemblySemanticChangesInReport = true;
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("Show assembly semantic changes", html);
            Assert.Contains("semantic_mod_0", html);
            Assert.Contains("semantic-changes-table", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_NotShownWhenDisabled()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("semantic-changes-off");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "MyApp.Service", "", "public", "", "Method", "NewMethod", "", "void", "string name", ""),
                },
            };

            var config = CreateConfig(enableInlineDiff: true);
            config.ShouldIncludeAssemblySemanticChangesInReport = false;
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.DoesNotContain("Show assembly semantic changes", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_LazyRender_EncodesAsBase64()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("semantic-changes-lazy");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "Foo", "", "public", "", "Method", "Bar", "", "void", "", ""),
                },
            };

            var config = CreateConfig(enableInlineDiff: true, lazyRender: true);
            config.ShouldIncludeAssemblySemanticChangesInReport = true;
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Should contain a data-diff-html attribute for the semantic changes row
            Assert.Contains("semantic_mod_0", html);
            Assert.Contains("Show assembly semantic changes", html);
            // Content should NOT be inline (lazy rendered) — table markup is base64-encoded
            Assert.DoesNotContain("<table class=\"semantic-changes-table", html);
            Assert.Contains("data-diff-html", html);
        }

        // ── Assembly Semantic Changes table styling / テーブルスタイル ────────

        /// <summary>
        /// Verifies the semantic-changes table header background uses the lighter gray (#98989d).
        /// semantic-changes テーブルヘッダ背景に薄い灰色 (#98989d) が使われることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_TableHeaderUsesLighterGray()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sc-header-color");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Semantic changes table header must use lighter gray (#98989d), not the old dark gray (#6b6b6e)
            // semantic-changes テーブルヘッダは旧暗灰色(#6b6b6e)ではなく薄灰色(#98989d)であること
            Assert.Contains("background: #98989d", html);
            Assert.DoesNotContain("background: #6b6b6e", html);
        }

        /// <summary>
        /// Verifies that the checkbox column header (&#x2713;) is present in the semantic-changes detail table.
        /// semantic-changes 詳細テーブルにチェック列ヘッダ (&#x2713;) が存在することを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_CheckboxHeaderPresent()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sc-cb-header");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "Foo", "", "public", "", "Method", "Bar", "", "void", "", ""),
                },
            };

            var config = CreateConfig(enableInlineDiff: true, lazyRender: false);
            config.ShouldIncludeAssemblySemanticChangesInReport = true;
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Detail table must have checkbox header (✓) AND body checkboxes
            // 詳細テーブルにチェックヘッダ(✓)とボディのチェックボックスが両方存在すること
            Assert.Contains("<th class=\"sc-col-cb\">&#x2713;</th>", html);
            Assert.Contains("<td class=\"sc-col-cb\"><input type=\"checkbox\"", html);
        }

        /// <summary>
        /// Verifies that Kind, Access, and Modifiers column body cells use code emphasis (like TextMatch).
        /// Kind, Access, Modifiers 列ボディが code 強調表示を使用すること（TextMatch と同等）を確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_KindAccessModifiersUseCodeEmphasis()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sc-emphasis");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Modified", "MyApp.Svc", "", "public", "virtual", "Method", "Run", "", "void", "", "Changed"),
                },
            };

            var config = CreateConfig(enableInlineDiff: true, lazyRender: false);
            config.ShouldIncludeAssemblySemanticChangesInReport = true;
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Kind, Access, and Modifiers must use <code> emphasis (matching TextMatch in other tables)
            // Kind、Access、Modifiers は <code> 強調表示を使うこと（他テーブルの TextMatch と同様）
            Assert.Contains("<code>Method</code>", html);        // Kind
            Assert.Contains("<code>public</code>", html);        // Access
            Assert.Contains("<code>virtual</code>", html);       // Modifiers
            Assert.Contains("<code>Modified</code>", html);      // Change
            Assert.Contains("<code>Changed</code>", html);       // Body
        }

        /// <summary>
        /// Verifies the th.sc-col-cb CSS rule exists for proper checkbox column header styling.
        /// th.sc-col-cb CSS ルールが存在しチェック列ヘッダが正しくスタイリングされることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_ThScColCbCssRuleExists()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sc-th-css");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Both th.sc-col-cb and td.sc-col-cb CSS rules must exist
            // th.sc-col-cb と td.sc-col-cb 両方の CSS ルールが存在すること
            Assert.Contains("table.semantic-changes-table th.sc-col-cb", html);
            Assert.Contains("table.semantic-changes-table td.sc-col-cb", html);
        }

        /// <summary>
        /// Verifies the semantic changes table td cells have white background styling.
        /// セマンティック変更テーブルの td セルに白背景スタイルが適用されていることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_TdHasWhiteBackground()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sc-td-bg");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // td cells in the semantic-changes-table must have white background
            // semantic-changes-table の td セルは白背景であること
            Assert.Contains("background: #fff", html);
        }

        // ── Req1: Legend table / 凡例テーブル ─────────────────────────────────

        [Fact]
        public void GenerateDiffReportHtml_LegendSection_UsesTableFormat()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("legend-table");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("legend-table", html);
            Assert.Contains("<table class=\"legend-table\">", html);
        }

        // ── Req2: stat-table borders / 統計テーブルボーダー ────────────────────

        [Fact]
        public void GenerateDiffReportHtml_StatTable_HasVisibleBorders()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("stat-border");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("border: 1px solid #ddd", html);
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

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

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

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // diff-row background should be #edf0f4, not the old #f6f8fa
            Assert.Contains("tr.diff-row { background: #edf0f4; }", html);
            Assert.DoesNotContain("#f6f8fa", html);
        }

        // ── Req7: Copy paths button / コピーボタン ──────────────────────────────

        [Fact]
        public void GenerateDiffReportHtml_FilePathRow_HasCopyButton()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("copy-btn");

            _resultLists.AddModifiedFileRelativePath("src/app.dll");
            _resultLists.RecordDiffDetail("src/app.dll", FileDiffResultLists.DiffDetailResult.MD5Mismatch);

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

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

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("tbody tr:not(.diff-row):hover { background: #f3eef8; }", html);
            Assert.Contains("table.semantic-changes-table tbody tr:hover td { background: #f3eef8; }", html);
        }

        // ── Req9: Language toggle / 言語切り替え ────────────────────────────────

        [Fact]
        public void I18n_ReturnsHtmlEncodedEnglishText()
        {
            string result = HtmlReportGenerateService.I18n("Hello", "こんにちは");
            Assert.Equal("Hello", result);
        }

        private static ConfigSettings CreateConfig(bool enableInlineDiff = true, bool lazyRender = false) => new()
        {
            IgnoredExtensions = new List<string>(),
            TextFileExtensions = new List<string>(),
            ShouldIncludeUnchangedFiles = true,
            ShouldIncludeIgnoredFiles = false,
            ShouldOutputFileTimestamps = false,
            ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = false,
            ShouldGenerateHtmlReport = true,
            EnableInlineDiff = enableInlineDiff,
            InlineDiffLazyRender = lazyRender
        };
    }
}
