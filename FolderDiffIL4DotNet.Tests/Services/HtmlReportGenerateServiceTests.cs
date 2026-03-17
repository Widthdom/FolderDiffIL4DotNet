using System;
using System.Collections.Generic;
using System.IO;
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
                // ignore cleanup errors in tests
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
            Assert.Contains("stat-label\">Unchanged</td><td class=\"stat-value\">1</td>", html);
            Assert.Contains("stat-label\">Added</td><td class=\"stat-value\">1</td>", html);
            Assert.Contains("stat-label\">Removed</td><td class=\"stat-value\">1</td>", html);
            Assert.Contains("stat-label\">Modified</td><td class=\"stat-value\">1</td>", html);
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
            // Raw angle brackets should not appear in file path cell
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

        // ── Inline diff ───────────────────────────────────────────────────────

        [Fact]
        public void GenerateDiffReportHtml_TextMismatch_WithRealFiles_ShowsInlineDiff()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-basic");

            // Create actual text files so inline diff can read them
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
            // ILMismatch without ILCache: IL text not available, so no inline diff rendered
            Assert.DoesNotContain("<details", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_TextMismatch_DiffTooLarge_ShowsSkippedMessage()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-large");

            // diff行数が maxDiffLines を超えるファイルを用意 (maxDiffLines=1 に設定、変更行は2行)
            File.WriteAllLines(Path.Combine(oldDir, "big.txt"), new[] { "A", "B" });
            File.WriteAllLines(Path.Combine(newDir, "big.txt"), new[] { "A-changed", "B-changed" });

            _resultLists.AddModifiedFileRelativePath("big.txt");
            _resultLists.RecordDiffDetail("big.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig(enableInlineDiff: true);
            config.InlineDiffMaxDiffLines = 1;  // diff 2行 > 1 → スキップ

            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("diff-skipped", html);
            Assert.Contains("InlineDiffMaxDiffLines", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_TextMismatch_MissingFile_SkipsGracefully()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("inline-diff-missing");

            // ファイルを作成しない → IO エラー → 差分行なし（クラッシュしない）
            _resultLists.AddModifiedFileRelativePath("ghost.txt");
            _resultLists.RecordDiffDetail("ghost.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig(enableInlineDiff: true);
            _service.GenerateDiffReportHtml(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null,
                computerName: "test-host", config);

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
            // summary に +N / -M が含まれる
            Assert.Contains("+1", html);
            Assert.Contains("-1", html);
        }

        // Helpers

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

        /// <summary>
        /// ShouldIncludeILCacheStatsInReport=true かつ ILCache を渡した場合、IL Cache Stats セクションが出力されることを確認します。
        /// </summary>
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

        /// <summary>
        /// ShouldIncludeIgnoredFiles=true で無視ファイルがある場合、無視ファイルセクションにコンテンツが出力されることを確認します。
        /// </summary>
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

        /// <summary>
        /// ShouldIncludeIgnoredFiles=true かつ ShouldOutputFileTimestamps=true で、
        /// old/new 両方に存在する無視ファイルのタイムスタンプが出力されることを確認します。
        /// </summary>
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

        /// <summary>
        /// ShouldOutputFileTimestamps=true で unchanged ファイルのタイムスタンプが出力されることを確認します。
        /// </summary>
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

        /// <summary>
        /// ShouldIncludeIgnoredFiles=true で new 側のみに無視ファイルがある場合のタイムスタンプ出力を確認します。
        /// </summary>
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

        private static ConfigSettings CreateConfig(bool enableInlineDiff = true) => new()
        {
            IgnoredExtensions = new List<string>(),
            TextFileExtensions = new List<string>(),
            ShouldIncludeUnchangedFiles = true,
            ShouldIncludeIgnoredFiles = false,
            ShouldOutputFileTimestamps = false,
            ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = false,
            ShouldGenerateHtmlReport = true,
            EnableInlineDiff = enableInlineDiff
        };
    }
}
