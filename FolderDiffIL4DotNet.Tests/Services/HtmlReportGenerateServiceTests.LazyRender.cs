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
    /// Lazy render, IL cache stats, ignored files, timestamps, and column structure tests.
    /// 遅延レンダリング、IL キャッシュ統計、無視ファイル、タイムスタンプ、列構成テスト。
    /// </summary>
    public sealed partial class HtmlReportGenerateServiceTests
    {
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
    }
}
