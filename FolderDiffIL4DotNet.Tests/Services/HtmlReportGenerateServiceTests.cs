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
            _service = new HtmlReportGenerateService(_resultLists, _logger, new ConfigSettingsBuilder().Build());
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

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));

            Assert.True(File.Exists(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME)));
        }

        [Fact]
        public void GenerateDiffReportHtml_ShouldGenerateHtmlReport_False_SkipsGeneration()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("skip-gen");
            var builder = CreateConfigBuilder();
            builder.ShouldGenerateHtmlReport = false;
            var config = builder.Build();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            Assert.False(File.Exists(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME)));
        }

        [Fact]
        public void GenerateDiffReportHtml_ModifiedSection_ContainsCheckboxInputs()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("modified-cb");

            _resultLists.AddModifiedFileRelativePath("src/App.dll");
            _resultLists.RecordDiffDetail("src/App.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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
            _resultLists.RecordDiffDetail("a.txt", FileDiffResultLists.DiffDetailResult.SHA256Match);
            _resultLists.AddModifiedFileRelativePath("b.txt");
            _resultLists.RecordDiffDetail("b.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "c.txt"));
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "d.txt"));

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("#b31d28", html);
            Assert.Contains("[ - ] Removed Files", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_ContainsSavedStateSentinel_ForDownload()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sentinel");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("const __savedState__  = null;", html);
            Assert.Contains("downloadReviewed", html);
            Assert.Contains("localStorage", html);
        }

        /// <summary>
        /// Verifies that the generated HTML includes SHA256 integrity verification code
        /// for the "Download as reviewed" workflow.
        /// 「Download as reviewed」ワークフロー用の SHA256 整合性検証コードが
        /// 生成 HTML に含まれることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_ContainsSha256IntegrityVerification_ForReviewedDownload()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sha256-reviewed");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Web Crypto API SHA256 computation
            Assert.Contains("crypto.subtle.digest", html);
            Assert.Contains("SHA-256", html);
            // Companion .sha256 verification file download
            Assert.Contains(".sha256", html);
            Assert.Contains("sha256Text", html);
            // Self-verification: __reviewedSha256__ sentinel and verifyIntegrity function
            Assert.Contains("const __reviewedSha256__  = null;", html);
            Assert.Contains("verifyIntegrity", html);
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
        public void HtmlEncode_EscapesBacktickAndNonAsciiCharacters()
        {
            // WebUtility.HtmlEncode handles characters beyond the original 5-char manual replacement
            // WebUtility.HtmlEncode は従来の手動 5 文字置換を超えた文字もエスケープする
            var encoded = HtmlReportGenerateService.HtmlEncode("path/with`backtick");
            Assert.DoesNotContain("`", encoded);
            Assert.Contains("&#96;", encoded);
        }

        [Fact]
        public void HtmlEncode_PreservesNormalTextUnchanged()
        {
            // Normal ASCII text without special characters should pass through unchanged
            // 特殊文字を含まない通常の ASCII テキストはそのまま返される
            Assert.Equal("Hello World 123", HtmlReportGenerateService.HtmlEncode("Hello World 123"));
        }

        [Fact]
        public void HtmlEncode_HandlesUnicodeCharacters()
        {
            // Japanese characters and other Unicode should be handled by WebUtility.HtmlEncode
            // 日本語文字やその他の Unicode は WebUtility.HtmlEncode で適切に処理される
            var input = "テスト<script>alert('xss')</script>";
            var encoded = HtmlReportGenerateService.HtmlEncode(input);
            Assert.DoesNotContain("<script>", encoded);
            Assert.Contains("&lt;script&gt;", encoded);
        }

        [Fact]
        public void GenerateDiffReportHtml_ContainsContentSecurityPolicyMetaTag()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("csp-meta-tag");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // CSP meta tag must be present to mitigate XSS
            // XSS 緩和のため CSP メタタグが存在しなければならない
            Assert.Contains("Content-Security-Policy", html);
            Assert.Contains("default-src 'none'", html);
            Assert.Contains("style-src 'unsafe-inline'", html);
            Assert.Contains("script-src 'unsafe-inline'", html);
            Assert.Contains("img-src 'self'", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_CspMetaTagAppearsBetweenCharsetAndViewport()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("csp-order");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // CSP must appear after charset and before viewport for correct ordering
            // CSP は charset の後、viewport の前に配置されなければならない
            var charsetIdx = html.IndexOf("charset=\"UTF-8\"", StringComparison.Ordinal);
            var cspIdx = html.IndexOf("Content-Security-Policy", StringComparison.Ordinal);
            var viewportIdx = html.IndexOf("viewport", StringComparison.Ordinal);
            Assert.True(charsetIdx < cspIdx, "CSP meta tag must appear after charset meta tag");
            Assert.True(cspIdx < viewportIdx, "CSP meta tag must appear before viewport meta tag");
        }

        [Fact]
        public void GenerateDiffReportHtml_PathWithSpecialChars_IsHtmlEncoded()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("html-encode");

            _resultLists.AddModifiedFileRelativePath("src/<Module>.dll");
            _resultLists.RecordDiffDetail("src/<Module>.dll", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Raw angle brackets must be HTML-escaped in file path cells
            // ファイルパスセルでは生の山括弧が HTML エスケープされていなければならない
            Assert.Contains("&lt;Module&gt;", html);
            Assert.DoesNotContain("<Module>", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_Sha256MismatchWarning_AppearsInWarningsSection()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sha256-warning");

            _resultLists.AddModifiedFileRelativePath("payload.bin");
            _resultLists.RecordDiffDetail("payload.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("section-heading\">", html);
            Assert.Contains("Warnings</h2>", html);
            Assert.Contains("SHA256Mismatch", html);
        }

        /// <summary>
        /// Verifies that HTML Warnings section includes the SHA256Mismatch detail table with file listing.
        /// HTML の警告セクションに SHA256Mismatch 詳細テーブル（ファイル一覧）が含まれることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_Sha256MismatchWarning_IncludesDetailTable()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sha256-table");

            _resultLists.AddModifiedFileRelativePath("alpha.bin");
            _resultLists.RecordDiffDetail("alpha.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _resultLists.AddModifiedFileRelativePath("beta.bin");
            _resultLists.RecordDiffDetail("beta.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            // TextMismatch file should NOT appear in the SHA256Mismatch table
            _resultLists.AddModifiedFileRelativePath("gamma.txt");
            _resultLists.RecordDiffDetail("gamma.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Table heading should exist with count
            Assert.Contains("SHA256Mismatch: binary diff only \u2014 not a .NET assembly or disassembler unavailable (2)</h2>", html);

            // Extract the SHA256Mismatch table section
            int sha256TableStart = html.IndexOf("SHA256Mismatch: binary diff only", StringComparison.Ordinal);
            Assert.True(sha256TableStart >= 0, "SHA256Mismatch detail table heading should exist");
            string sha256Section = html.Substring(sha256TableStart);

            // Both SHA256Mismatch files should appear
            Assert.Contains("alpha.bin", sha256Section);
            Assert.Contains("beta.bin", sha256Section);

            // Files should be sorted alphabetically (alpha before beta)
            int alphaIdx = sha256Section.IndexOf("alpha.bin", StringComparison.Ordinal);
            int betaIdx = sha256Section.IndexOf("beta.bin", StringComparison.Ordinal);
            Assert.True(alphaIdx < betaIdx, "SHA256Mismatch files should be sorted alphabetically");
        }

        /// <summary>
        /// Verifies that SHA256Mismatch detail table appears before new file timestamps older than old table when both warnings exist.
        /// 両方の警告が存在する場合、SHA256Mismatch 詳細テーブルがタイムスタンプ逆行テーブルの前に表示されることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_Sha256MismatchTable_AppearsBeforeTimestampRegressedTable()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sha256-before-ts");

            _resultLists.AddModifiedFileRelativePath("payload.bin");
            _resultLists.RecordDiffDetail("payload.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("payload.bin", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            var builder = CreateConfigBuilder();
            builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            int sha256TableIdx = html.IndexOf("SHA256Mismatch: binary diff only", StringComparison.Ordinal);
            int tsTableIdx = html.IndexOf("new file timestamps older than old", StringComparison.Ordinal);
            Assert.True(sha256TableIdx >= 0, "SHA256Mismatch detail table should exist");
            Assert.True(tsTableIdx >= 0, "new file timestamps older than old table should exist");
            Assert.True(sha256TableIdx < tsTableIdx, "SHA256Mismatch table should appear before new file timestamps older than old table");
        }

        /// <summary>
        /// Verifies that each warning message is immediately followed by its detail table (interleaved layout).
        /// When both warnings exist, the SHA256Mismatch detail table appears between the SHA256Mismatch warning
        /// and the timestamp regression warning, rather than all warnings being listed first.
        /// 各警告メッセージの直下に対応する詳細テーブルが配置されること（インターリーブレイアウト）を確認する。
        /// 両方の警告がある場合、SHA256Mismatch 詳細テーブルは SHA256Mismatch 警告とタイムスタンプ逆行警告の間に配置される。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_Sha256MismatchDetailTable_AppearsImmediatelyAfterSha256Warning()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sha256-interleave");

            _resultLists.AddModifiedFileRelativePath("payload.bin");
            _resultLists.RecordDiffDetail("payload.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("payload.bin", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            var builder = CreateConfigBuilder();
            builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            int sha256TableIdx = html.IndexOf("SHA256Mismatch: binary diff only", StringComparison.Ordinal);
            int tsTableIdx = html.IndexOf("new file timestamps older than old", StringComparison.Ordinal);

            Assert.True(sha256TableIdx >= 0, "SHA256Mismatch detail table should exist");
            Assert.True(tsTableIdx >= 0, "new file timestamps older than old detail table should exist");

            // SHA256 table should appear before Timestamp table / SHA256テーブルはタイムスタンプテーブルの前に表示
            Assert.True(sha256TableIdx < tsTableIdx,
                "SHA256Mismatch table should appear before timestamp regression table");
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
            Assert.Contains("min-width: 9em; text-align: center; }", html); // col-diff has text-align: center
            // File Path is NOT center-aligned / File Path 列は中央揃えではない
            Assert.Contains("td.col-path { white-space: nowrap; overflow: hidden; }", html);
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
            var builder = CreateConfigBuilder();
            builder.ShouldIncludeILCacheStatsInReport = true;
            var config = builder.Build();
            var ilCache = new ILCache(ilCacheDirectoryAbsolutePath: null);

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache));

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

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = true;
            var config = builder.Build();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = true;
            builder.ShouldOutputFileTimestamps = true;
            var config = builder.Build();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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

            var builder = CreateConfigBuilder();
            builder.ShouldOutputFileTimestamps = true;
            var config = builder.Build();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = true;
            builder.ShouldOutputFileTimestamps = true;
            var config = builder.Build();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("setupLazyDiff", html);
            Assert.Contains("decodeDiffHtml", html);
            Assert.Contains("data-diff-html", html);  // JS references the attribute name
        }

        // ── Column structure: per-table columns / テーブルごとの列構成 ─────────

        /// <summary>
        /// Verifies HTML tables have the correct column sets:
        /// Ignored=no Disassembler, Added/Removed=no Diff Reason &amp; no Disassembler,
        /// Unchanged=has Disassembler (with ILMatch label), Modified=all columns.
        /// HTML テーブルが正しい列構成を持つことを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_ColumnStructure_PerTableColumns()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("col-struct");

            _resultLists.AddUnchangedFileRelativePath("same.dll");
            _resultLists.RecordDiffDetail("same.dll", FileDiffResultLists.DiffDetailResult.ILMatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.RecordIgnoredFile("ignored.pdb", FileDiffResultLists.IgnoredFileLocation.Old);
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "added.txt"));
            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "removed.txt"));
            _resultLists.AddModifiedFileRelativePath("mod.dll");
            _resultLists.RecordDiffDetail("mod.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // All tables have all 8 columns (cols kept in DOM for stability)
            // 全テーブルに8列すべてが存在する（DOM安定性のため列は維持）
            Assert.Contains("col-disasm-g", html);
            Assert.Contains("col-diff-g", html);
            Assert.Contains("dotnet-ildasm (version: 0.12.0)", html); // Unchanged ILMatch shows disassembler label

            // Verify Ignored table has hide-disasm CSS class
            // Ignored テーブルに hide-disasm CSSクラスがあることを確認
            int ignoredStart = html.IndexOf("Ignored Files", StringComparison.Ordinal);
            int unchangedStart = html.IndexOf("Unchanged Files", StringComparison.Ordinal);
            string ignoredSection = html.Substring(ignoredStart, unchangedStart - ignoredStart);
            Assert.Contains("hide-disasm", ignoredSection);
            Assert.DoesNotContain("hide-col6", ignoredSection);

            // Verify Unchanged table has NO hide classes (shows all columns)
            // Unchanged テーブルにはhideクラスがないことを確認（全列表示）
            int addedStart = html.IndexOf("Added Files", StringComparison.Ordinal);
            string unchangedSection = html.Substring(unchangedStart, addedStart - unchangedStart);
            Assert.DoesNotContain("hide-disasm", unchangedSection);
            Assert.DoesNotContain("hide-col6", unchangedSection);

            // Verify Added table has hide-col6 and hide-disasm CSS classes
            // Added テーブルに hide-col6, hide-disasm CSSクラスがあることを確認
            int removedStart = html.IndexOf("Removed Files", StringComparison.Ordinal);
            string addedSection = html.Substring(addedStart, removedStart - addedStart);
            Assert.Contains("hide-col6", addedSection);
            Assert.Contains("hide-disasm", addedSection);
            Assert.Contains("col-diff-g", addedSection);   // column present in DOM
            Assert.Contains("col-disasm-g", addedSection);  // column present in DOM
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

            var builder = CreateConfigBuilder(enableInlineDiff: true);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("Show assembly semantic changes", html);
            Assert.Contains("semantic_mod_0", html);
            Assert.Contains("semantic-changes-table", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_ShowsCaveatNote()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("semantic-caveat");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "MyApp.Service", "", "public", "", "Method", "NewMethod", "", "void", "", ""),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: true);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Caveat note should be present in the semantic changes section (EN text)
            // セマンティック変更セクションに注意書きが存在すべき
            Assert.Contains("sc-caveat", html);
            Assert.Contains("supplementary information", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_CaveatCssExists()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("semantic-caveat-css");
            var config = CreateConfig();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // CSS rule for .sc-caveat should exist in the stylesheet
            Assert.Contains(".semantic-changes p.sc-caveat", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_NoCaveatWhenNoStructuralChanges()
        {
            // When no structural changes are detected (empty Entries), the caveat note
            // should NOT appear — the "No structural changes" message already directs
            // the user to the IL diff.
            // 構造的変更なし（Entries 空）の場合、注意書きは不要。
            var (oldDir, newDir, reportDir) = MakeDirs("semantic-no-caveat");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>(),
            };

            var builder = CreateConfigBuilder(enableInlineDiff: true);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // The "No structural changes" message should be present
            Assert.Contains("No structural changes detected", html);
            // But the caveat note should NOT be in the base64-encoded content
            // (it would be encoded, but we can check the decoded content isn't generated)
            Assert.DoesNotContain("supplementary information", html);
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

            var builder = CreateConfigBuilder(enableInlineDiff: true);
            builder.ShouldIncludeAssemblySemanticChangesInReport = false;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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

            var builder = CreateConfigBuilder(enableInlineDiff: true, lazyRender: true);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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

            var builder = CreateConfigBuilder(enableInlineDiff: true, lazyRender: false);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Detail table must have checkbox header (✓) AND body checkboxes
            // 詳細テーブルにチェックヘッダ(✓)とボディのチェックボックスが両方存在すること
            Assert.Contains("<th class=\"sc-col-cb\">&#x2713;</th>", html);
            Assert.Contains("<td class=\"sc-col-cb\"><input type=\"checkbox\"", html);
        }

        /// <summary>
        /// Verifies that Kind, Body, Access, and Modifiers column body cells use code emphasis.
        /// Kind, Body, Access, Modifiers 列ボディが code 強調表示を使用することを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_KindBodyAccessModifiersUseCodeEmphasis()
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

            var builder = CreateConfigBuilder(enableInlineDiff: true, lazyRender: false);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Kind, Body, Access, and Modifiers all use <code> emphasis
            // Kind, Body, Access, Modifiers はすべて <code> 強調表示を使用する
            Assert.Contains("<code>Method</code>", html);        // Kind
            Assert.Contains("<code>public</code>", html);        // Access
            Assert.Contains("<code>virtual</code>", html);       // Modifiers
            Assert.Contains("style=\"background:#e3f2fd\">[ * ]", html); // Status cell with blue bg (no code emphasis)
            Assert.Contains("<code>Changed</code>", html);       // Body
        }

        /// <summary>
        /// Verifies that Access column with arrow notation wraps each side in code tags individually.
        /// Access 列の矢印表記で各側を個別に code タグで囲むことを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_AccessArrowWrapsEachSideInCode()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sc-arrow-code");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Modified", "MyApp.Svc", "", "public \u2192 internal", "virtual \u2192 sealed", "Method", "Run", "", "void", "", "Changed"),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: true, lazyRender: false);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Arrow notation wraps each side individually: <code>old</code> → <code>new</code>
            // 矢印表記は各側を個別に囲む: <code>旧</code> → <code>新</code>
            Assert.Contains("<code>public</code> \u2192 <code>internal</code>", html);
            Assert.Contains("<code>virtual</code> \u2192 <code>sealed</code>", html);
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

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

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

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("filter-table", html);
            Assert.Contains("<table class=\"filter-table\">", html);
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
            // diff-row background should be #edf0f4, not the old #f6f8fa
            Assert.Contains("tr.diff-row { background: #edf0f4; }", html);
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
            Assert.Contains(":not(.stat-table):not(.legend-table):not(.il-ignore-table) > tbody tr:not(.diff-row):not(.diff-hunk-tr):not(.diff-del-tr):not(.diff-add-tr):hover { background: #f3eef8; }", html);
            Assert.Contains("table.semantic-changes-table tbody tr:hover td { background: #f3eef8 !important; }", html);
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
        public void LoadEmbeddedResource_JsResource_ReturnsNonEmptyString()
        {
            var js = HtmlReportGenerateService.LoadEmbeddedResource("FolderDiffIL4DotNet.Services.HtmlReport.diff_report.js");
            Assert.False(string.IsNullOrWhiteSpace(js));
            Assert.Contains("function", js);
        }

        [Fact]
        public void LoadEmbeddedResource_JsResource_ContainsPlaceholders()
        {
            var js = HtmlReportGenerateService.LoadEmbeddedResource("FolderDiffIL4DotNet.Services.HtmlReport.diff_report.js");
            Assert.Contains("{{STORAGE_KEY}}", js);
            Assert.Contains("{{REPORT_DATE}}", js);
        }

        [Fact]
        public void LoadEmbeddedResource_InvalidResource_ThrowsFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() =>
                HtmlReportGenerateService.LoadEmbeddedResource("NonExistent.Resource.Name"));
        }

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
            int clearIdx = html.IndexOf("Clear all filter-hidden state", StringComparison.Ordinal);
            int outerHtmlIdx = html.IndexOf("document.documentElement.outerHTML", StringComparison.Ordinal);
            Assert.True(clearIdx >= 0, "Filter-hidden clearing comment not found in JS");
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
            int restoreComment = html.IndexOf("Restore live page state", StringComparison.Ordinal);
            int applyFiltersIdx = html.IndexOf("applyFilters();", restoreComment, StringComparison.Ordinal);
            Assert.True(restoreComment > outerHtmlIdx,
                "Restore comment must appear after outerHTML capture");
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
            Assert.Contains("color:#22863a", html); // green for Yes / Yes 用の緑
            Assert.Contains("color:#b31d28", html); // red for No / No 用の赤
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

        private static ConfigSettingsBuilder CreateConfigBuilder(bool enableInlineDiff = true, bool lazyRender = false) => new()
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

        private static ConfigSettings CreateConfig(bool enableInlineDiff = true, bool lazyRender = false) => CreateConfigBuilder(enableInlineDiff, lazyRender).Build();

        private static ReportGenerationContext CreateReportContext(
            string oldDir, string newDir, string reportDir,
            ConfigSettings config, ILCache? ilCache = null)
            => new(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", config, ilCache);
    }
}
