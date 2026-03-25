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
    public sealed partial class HtmlReportGenerateServiceTests : IDisposable
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

        /// <summary>
        /// Verifies that the generated HTML includes the downloadExcelCompatibleHtml function
        /// for exporting an Excel-compatible HTML table from the reviewed report.
        /// reviewed レポートから Excel 互換 HTML テーブルをエクスポートする
        /// downloadExcelCompatibleHtml 関数が生成 HTML に含まれることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_ContainsExcelCompatibleHtmlExportFunction()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("excel-export");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Assert: JS contains downloadExcelCompatibleHtml function / JS に downloadExcelCompatibleHtml 関数が含まれることを検証
            Assert.Contains("function downloadExcelCompatibleHtml()", html);
            Assert.Contains("function buildExcelRow(tr)", html);
            Assert.Contains("_reviewed_Excel-compatible.html", html);
        }

        /// <summary>
        /// Verifies that the Excel export button is placed inside the reviewed banner (via JS),
        /// not inside the CTRL markers of the original report.
        /// Excel エクスポートボタンが CTRL マーカー内ではなく、JS による reviewed バナー内に
        /// 配置されることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_ExcelExportButton_NotInCtrlMarkers()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("excel-btn-ctrl");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // The CTRL section should NOT contain the Excel export button (it is in the reviewed banner only)
            // CTRL セクションには Excel エクスポートボタンを含まないことを検証（reviewed バナーのみ）
            int ctrlStart = html.IndexOf("<!--CTRL-->", StringComparison.Ordinal);
            int ctrlEnd = html.IndexOf("<!--/CTRL-->", StringComparison.Ordinal);
            Assert.True(ctrlStart >= 0, "<!--CTRL--> marker not found");
            Assert.True(ctrlEnd > ctrlStart, "<!--/CTRL--> marker not found");
            string ctrlSection = html[ctrlStart..ctrlEnd];
            Assert.DoesNotContain("downloadExcelCompatibleHtml", ctrlSection);

            // But the function exists in JS (outside CTRL) / JS 内（CTRL 外）に関数が存在することを検証
            Assert.Contains("downloadExcelCompatibleHtml", html);
        }

        /// <summary>
        /// Verifies that the downloadAsPdf function exists in JS and its button reference is not in CTRL markers.
        /// downloadAsPdf 関数が JS に存在し、ボタン参照が CTRL マーカー内にないことを検証する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_PdfExportButton_NotInCtrlMarkers()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("pdf-btn-ctrl");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // The CTRL section should NOT contain the PDF export button (it is in the reviewed banner only)
            // CTRL セクションには PDF エクスポートボタンを含まないことを検証（reviewed バナーのみ）
            int ctrlStart = html.IndexOf("<!--CTRL-->", StringComparison.Ordinal);
            int ctrlEnd = html.IndexOf("<!--/CTRL-->", StringComparison.Ordinal);
            Assert.True(ctrlStart >= 0, "<!--CTRL--> marker not found");
            Assert.True(ctrlEnd > ctrlStart, "<!--/CTRL--> marker not found");
            string ctrlSection = html[ctrlStart..ctrlEnd];
            Assert.DoesNotContain("downloadAsPdf", ctrlSection);

            // But the function exists in JS (outside CTRL) / JS 内（CTRL 外）に関数が存在することを検証
            Assert.Contains("function downloadAsPdf()", html);
        }

        /// <summary>
        /// Verifies that PDF print header/footer CSS classes exist in the embedded CSS.
        /// PDF 印刷ヘッダー/フッター CSS クラスが埋め込み CSS に存在することを検証する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_ContainsPdfPrintCssClasses()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("pdf-css");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // PDF print header/footer CSS classes should be embedded / PDF 印刷用 CSS クラスが埋め込まれていること
            Assert.Contains(".pdf-print-header", html);
            Assert.Contains(".pdf-print-footer", html);
            Assert.Contains("pdf-print-mode", html);
        }

        private static ReportGenerationContext CreateReportContext(
            string oldDir, string newDir, string reportDir,
            ConfigSettings config, ILCache? ilCache = null)
            => new(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", config, ilCache);
    }
}

