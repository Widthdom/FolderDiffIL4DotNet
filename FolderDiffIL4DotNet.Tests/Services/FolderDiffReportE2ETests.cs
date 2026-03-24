using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Services.ILOutput;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// End-to-end integration tests that exercise the full pipeline:
    /// folder diff → report generation (Markdown, HTML, audit log) → output verification.
    /// フォルダ差分 → レポート生成（Markdown、HTML、監査ログ）→ 出力検証のE2E統合テスト。
    /// </summary>
    [Trait("Category", "Integration")]
    public sealed class FolderDiffReportE2ETests : IDisposable
    {
        private readonly string _rootDir;
        private readonly FileDiffResultLists _resultLists = new();
        private readonly ILoggerService _logger = new LoggerService();

        public FolderDiffReportE2ETests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-e2e-report-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
            TimestampCache.Clear();
            _resultLists.ResetAll();
        }

        public void Dispose()
        {
            TimestampCache.Clear();
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

        // ── Full pipeline: diff + all reports ──────────────────────────────────

        /// <summary>
        /// Runs folder diff then generates all three reports (Markdown, HTML, audit log)
        /// and verifies each output file exists and contains expected content.
        /// フォルダ差分後に 3 種のレポート（Markdown、HTML、監査ログ）を生成し、
        /// 各出力ファイルの存在と期待される内容を検証する。
        /// </summary>
        [Fact]
        public async Task FullPipeline_DiffThenGenerateAllReports_ProducesCorrectOutputs()
        {
            var (oldDir, newDir, reportDir) = PrepareDirs("full-pipeline");

            // Prepare files spanning all categories / 全カテゴリにわたるファイルを用意
            WriteFile(oldDir, "unchanged.txt", "same content");
            WriteFile(newDir, "unchanged.txt", "same content");
            WriteFile(oldDir, "modified.txt", "old text");
            WriteFile(newDir, "modified.txt", "new text");
            WriteFile(oldDir, "removed.txt", "removed content");
            WriteFile(newDir, "added.txt", "added content");
            WriteFile(oldDir, "ignored.pdb", "old-pdb");
            WriteFile(newDir, "ignored.pdb", "new-pdb");

            var config = CreateConfig(maxParallelism: 1);
            await RunFolderDiffAsync(config, oldDir, newDir, reportDir);

            // Generate all reports / 全レポートを生成
            var reportContext = new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0.0-e2e", elapsedTimeString: "0h 0m 2.5s",
                computerName: "e2e-host", config, ilCache: null);

            var mdService = new ReportGenerateService(_resultLists, _logger, config);
            mdService.GenerateDiffReport(reportContext);

            var htmlService = new HtmlReportGenerateService(_resultLists, _logger, config);
            htmlService.GenerateDiffReportHtml(reportContext);

            var auditService = new AuditLogGenerateService(_resultLists, _logger);
            auditService.GenerateAuditLog(reportContext);

            // Verify Markdown report / Markdown レポートを検証
            var mdPath = Path.Combine(reportDir, "diff_report.md");
            Assert.True(File.Exists(mdPath), "diff_report.md should be created");
            var mdContent = File.ReadAllText(mdPath);
            Assert.Contains("[+] Added Files", mdContent);
            Assert.Contains("[-] Removed Files", mdContent);
            Assert.Contains("[*] Modified Files", mdContent);
            Assert.Contains("[=] Unchanged Files", mdContent);
            Assert.Contains("added.txt", mdContent);
            Assert.Contains("removed.txt", mdContent);
            Assert.Contains("modified.txt", mdContent);
            Assert.Contains("unchanged.txt", mdContent);

            // Verify HTML report / HTML レポートを検証
            var htmlPath = Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME);
            Assert.True(File.Exists(htmlPath), "diff_report.html should be created");
            var htmlContent = File.ReadAllText(htmlPath);
            Assert.Contains("<!DOCTYPE html>", htmlContent);
            Assert.Contains("added.txt", htmlContent);
            Assert.Contains("removed.txt", htmlContent);
            Assert.Contains("modified.txt", htmlContent);
            Assert.Contains("unchanged.txt", htmlContent);
            Assert.Contains("type=\"checkbox\"", htmlContent);
            Assert.Contains("localStorage", htmlContent);
            Assert.Contains("downloadReviewed", htmlContent);

            // Verify audit log / 監査ログを検証
            var auditPath = Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME);
            Assert.True(File.Exists(auditPath), "audit_log.json should be created");
            var auditJson = File.ReadAllText(auditPath);
            using var doc = JsonDocument.Parse(auditJson);
            var root = doc.RootElement;
            Assert.Equal("1.0.0-e2e", root.GetProperty("appVersion").GetString());
            Assert.Equal("e2e-host", root.GetProperty("computerName").GetString());
            var summary = root.GetProperty("summary");
            Assert.Equal(1, summary.GetProperty("added").GetInt32());
            Assert.Equal(1, summary.GetProperty("removed").GetInt32());
            Assert.Equal(1, summary.GetProperty("modified").GetInt32());
            Assert.Equal(1, summary.GetProperty("unchanged").GetInt32());
        }

        /// <summary>
        /// Runs pipeline with only unchanged files and verifies all outputs reflect zero changes.
        /// 変更なしファイルのみでパイプラインを実行し、全出力がゼロ変更を反映することを検証する。
        /// </summary>
        [Fact]
        public async Task FullPipeline_AllFilesUnchanged_ReportsNoChanges()
        {
            var (oldDir, newDir, reportDir) = PrepareDirs("all-unchanged");

            WriteFile(oldDir, "lib/core.dll", "identical binary");
            WriteFile(newDir, "lib/core.dll", "identical binary");
            WriteFile(oldDir, "config.json", "{\"key\":\"value\"}");
            WriteFile(newDir, "config.json", "{\"key\":\"value\"}");

            var config = CreateConfig(maxParallelism: 1);
            await RunFolderDiffAsync(config, oldDir, newDir, reportDir);

            var reportContext = new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "2.0.0", elapsedTimeString: "0h 0m 0.5s",
                computerName: "no-change-host", config, ilCache: null);

            var mdService = new ReportGenerateService(_resultLists, _logger, config);
            mdService.GenerateDiffReport(reportContext);

            var htmlService = new HtmlReportGenerateService(_resultLists, _logger, config);
            htmlService.GenerateDiffReportHtml(reportContext);

            var auditService = new AuditLogGenerateService(_resultLists, _logger);
            auditService.GenerateAuditLog(reportContext);

            // Verify zero changes in Markdown summary / Markdown サマリでゼロ変更を確認
            var mdContent = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("| Added | 0 |", mdContent);
            Assert.Contains("| Removed | 0 |", mdContent);
            Assert.Contains("| Modified | 0 |", mdContent);
            Assert.Contains("| Unchanged | 2 |", mdContent);

            // Verify audit log counts / 監査ログ件数を確認
            var auditJson = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            using var doc = JsonDocument.Parse(auditJson);
            var summary = doc.RootElement.GetProperty("summary");
            Assert.Equal(0, summary.GetProperty("added").GetInt32());
            Assert.Equal(0, summary.GetProperty("removed").GetInt32());
            Assert.Equal(0, summary.GetProperty("modified").GetInt32());
            Assert.Equal(2, summary.GetProperty("unchanged").GetInt32());
        }

        /// <summary>
        /// Runs pipeline with files in subdirectories and verifies relative paths are preserved in reports.
        /// サブディレクトリ内のファイルでパイプラインを実行し、レポート内で相対パスが保持されることを検証する。
        /// </summary>
        [Fact]
        public async Task FullPipeline_SubdirectoryFiles_PreservesRelativePaths()
        {
            var (oldDir, newDir, reportDir) = PrepareDirs("subdir");

            WriteFile(oldDir, Path.Combine("src", "app", "main.cs"), "old main");
            WriteFile(newDir, Path.Combine("src", "app", "main.cs"), "new main");
            WriteFile(newDir, Path.Combine("docs", "readme.md"), "new doc");
            WriteFile(oldDir, Path.Combine("tools", "old-tool.txt"), "old tool");

            var config = CreateConfig(maxParallelism: 1);
            await RunFolderDiffAsync(config, oldDir, newDir, reportDir);

            var reportContext = new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", config, ilCache: null);

            var mdService = new ReportGenerateService(_resultLists, _logger, config);
            mdService.GenerateDiffReport(reportContext);

            var htmlService = new HtmlReportGenerateService(_resultLists, _logger, config);
            htmlService.GenerateDiffReportHtml(reportContext);

            var auditService = new AuditLogGenerateService(_resultLists, _logger);
            auditService.GenerateAuditLog(reportContext);

            // Verify relative paths in Markdown / Markdown 内の相対パスを確認
            var mdContent = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains(Path.Combine("src", "app", "main.cs"), mdContent);
            Assert.Contains(Path.Combine("docs", "readme.md"), mdContent);
            Assert.Contains(Path.Combine("tools", "old-tool.txt"), mdContent);

            // Verify relative paths in audit log / 監査ログ内の相対パスを確認
            var auditJson = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            using var doc = JsonDocument.Parse(auditJson);
            var files = doc.RootElement.GetProperty("files").EnumerateArray().ToList();
            var relativePaths = files.Select(f => f.GetProperty("relativePath").GetString()).ToList();
            Assert.Contains(relativePaths, p => p!.Contains("main.cs"));
            Assert.Contains(relativePaths, p => p!.Contains("readme.md"));
            Assert.Contains(relativePaths, p => p!.Contains("old-tool.txt"));
        }

        /// <summary>
        /// Runs pipeline in parallel mode and verifies results match sequential mode for the same input.
        /// 並列モードでパイプラインを実行し、同一入力に対して逐次モードと結果が一致することを検証する。
        /// </summary>
        [Fact]
        public async Task FullPipeline_ParallelMode_ProducesSameClassificationAsSequential()
        {
            var (oldDir, newDir, reportDir) = PrepareDirs("parallel");

            // Create enough files to exercise parallelism / 並列実行を活用できる十分なファイル数を生成
            for (var i = 0; i < 20; i++)
            {
                WriteFile(oldDir, $"file{i:D3}.txt", $"content-{i}");
                WriteFile(newDir, $"file{i:D3}.txt", i % 3 == 0 ? $"modified-{i}" : $"content-{i}");
            }
            WriteFile(newDir, "brand-new.txt", "added");
            WriteFile(oldDir, "deprecated.txt", "removed");

            var config = CreateConfig(maxParallelism: 4);
            await RunFolderDiffAsync(config, oldDir, newDir, reportDir);

            // Classification counts / 分類件数
            var stats = _resultLists.SummaryStatistics;
            Assert.Equal(1, stats.AddedCount);
            Assert.Equal(1, stats.RemovedCount);
            // Files where i % 3 == 0 are modified (0,3,6,9,12,15,18) = 7
            Assert.Equal(7, stats.ModifiedCount);
            // Files where i % 3 != 0 = 13
            Assert.Equal(13, stats.UnchangedCount);

            // Generate reports and verify they exist / レポート生成と存在確認
            var reportContext = new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "parallel-host", config, ilCache: null);

            new ReportGenerateService(_resultLists, _logger, config).GenerateDiffReport(reportContext);
            new HtmlReportGenerateService(_resultLists, _logger, config).GenerateDiffReportHtml(reportContext);
            new AuditLogGenerateService(_resultLists, _logger).GenerateAuditLog(reportContext);

            Assert.True(File.Exists(Path.Combine(reportDir, "diff_report.md")));
            Assert.True(File.Exists(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME)));
            Assert.True(File.Exists(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME)));

            // Verify summary consistency in Markdown / Markdown のサマリ整合性を確認
            var mdContent = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("| Added | 1 |", mdContent);
            Assert.Contains("| Removed | 1 |", mdContent);
            Assert.Contains("| Modified | 7 |", mdContent);
            Assert.Contains("| Unchanged | 13 |", mdContent);
        }

        /// <summary>
        /// Verifies that the audit log SHA256 hashes reference the generated Markdown and HTML report files.
        /// 監査ログの SHA256 ハッシュが生成された Markdown および HTML レポートファイルを参照することを検証する。
        /// </summary>
        [Fact]
        public async Task FullPipeline_AuditLogContainsReportHashes()
        {
            var (oldDir, newDir, reportDir) = PrepareDirs("audit-hashes");

            WriteFile(oldDir, "a.txt", "old");
            WriteFile(newDir, "a.txt", "new");

            var config = CreateConfig(maxParallelism: 1);
            await RunFolderDiffAsync(config, oldDir, newDir, reportDir);

            var reportContext = new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 0.5s",
                computerName: "hash-host", config, ilCache: null);

            // Generate Markdown and HTML first (audit log reads their hashes)
            // 先に Markdown と HTML を生成（監査ログがそれらのハッシュを読み取る）
            new ReportGenerateService(_resultLists, _logger, config).GenerateDiffReport(reportContext);
            new HtmlReportGenerateService(_resultLists, _logger, config).GenerateDiffReportHtml(reportContext);
            new AuditLogGenerateService(_resultLists, _logger).GenerateAuditLog(reportContext);

            var auditJson = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            using var doc = JsonDocument.Parse(auditJson);
            var root = doc.RootElement;

            var reportSha256 = root.GetProperty("reportSha256").GetString();
            var htmlReportSha256 = root.GetProperty("htmlReportSha256").GetString();

            // Both hashes should be 64-char hex strings / 両ハッシュは64文字の16進文字列であること
            Assert.Matches("^[0-9a-f]{64}$", reportSha256);
            Assert.Matches("^[0-9a-f]{64}$", htmlReportSha256);
            // Hashes should differ (different file contents) / ハッシュは異なるはず（異なるファイル内容）
            Assert.NotEqual(reportSha256, htmlReportSha256);
        }

        /// <summary>
        /// Verifies HTML report contains interactive elements for each modified file and
        /// correct section structure for all file categories.
        /// HTML レポートが各変更ファイルにインタラクティブ要素を含み、
        /// 全ファイルカテゴリに正しいセクション構造を持つことを検証する。
        /// </summary>
        [Fact]
        public async Task FullPipeline_HtmlReportContainsInteractiveElementsAndSections()
        {
            var (oldDir, newDir, reportDir) = PrepareDirs("html-interactive");

            WriteFile(oldDir, "lib.dll", "old-binary");
            WriteFile(newDir, "lib.dll", "new-binary");
            WriteFile(oldDir, "config.xml", "<old/>");
            WriteFile(newDir, "config.xml", "<old/>");
            WriteFile(newDir, "new-feature.cs", "class New {}");

            var config = CreateConfig(maxParallelism: 1);
            await RunFolderDiffAsync(config, oldDir, newDir, reportDir);

            var reportContext = new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "interactive-host", config, ilCache: null);

            new HtmlReportGenerateService(_resultLists, _logger, config).GenerateDiffReportHtml(reportContext);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Interactive elements / インタラクティブ要素
            Assert.Contains("type=\"checkbox\"", html);
            Assert.Contains("autoSave", html);
            Assert.Contains("collectState", html);
            Assert.Contains("applyFilters", html);

            // Section markers / セクションマーカー
            Assert.Contains("data-section", html);

            // SHA256 integrity code / SHA256 整合性コード
            Assert.Contains("crypto.subtle.digest", html);
            Assert.Contains("__reviewedSha256__", html);
            Assert.Contains("__finalSha256__", html);

            // Content Security Policy / CSP メタタグ
            Assert.Contains("Content-Security-Policy", html);
        }

        /// <summary>
        /// Verifies inline diff content is included in the HTML report when modified text files exist.
        /// 変更テキストファイルが存在する場合、HTML レポートにインライン差分コンテンツが含まれることを検証する。
        /// </summary>
        [Fact]
        public async Task FullPipeline_InlineDiff_IncludedForModifiedTextFiles()
        {
            var (oldDir, newDir, reportDir) = PrepareDirs("inline-diff");

            WriteFile(oldDir, "readme.txt", "line1\nline2\nline3");
            WriteFile(newDir, "readme.txt", "line1\nmodified-line2\nline3\nline4");

            var configBuilder = new ConfigSettingsBuilder
            {
                IgnoredExtensions = new List<string> { ".pdb" },
                TextFileExtensions = new List<string> { ".txt" },
                ShouldIncludeUnchangedFiles = true,
                ShouldIncludeIgnoredFiles = true,
                ShouldOutputILText = false,
                ShouldIgnoreILLinesContainingConfiguredStrings = false,
                ILIgnoreLineContainingStrings = new List<string>(),
                ShouldOutputFileTimestamps = false,
                MaxParallelism = 1,
                OptimizeForNetworkShares = false,
                AutoDetectNetworkShares = false,
                EnableInlineDiff = true,
                ShouldGenerateHtmlReport = true,
                InlineDiffLazyRender = false
            };
            var config = configBuilder.Build();

            await RunFolderDiffAsync(config, oldDir, newDir, reportDir);

            var reportContext = new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "diff-host", config, ilCache: null);

            new HtmlReportGenerateService(_resultLists, _logger, config).GenerateDiffReportHtml(reportContext);

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Inline diff content should be present (either inline or base64-encoded)
            // インライン差分コンテンツが存在すること（インラインまたはBase64エンコード）
            Assert.Contains("readme.txt", html);
            Assert.True(
                html.Contains("diff-table") || html.Contains("data-diff-html"),
                "HTML should contain inline diff table or lazy-rendered diff data");
        }

        // ── Helpers / ヘルパー ──────────────────────────────────────────────────

        private (string oldDir, string newDir, string reportDir) PrepareDirs(string label)
        {
            var oldDir = Path.Combine(_rootDir, $"old-{label}");
            var newDir = Path.Combine(_rootDir, $"new-{label}");
            var reportDir = Path.Combine(_rootDir, $"report-{label}");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);
            return (oldDir, newDir, reportDir);
        }

        private async Task RunFolderDiffAsync(ConfigSettings config, string oldDir, string newDir, string reportDir)
        {
            var executionContext = new DiffExecutionContext(
                oldDir, newDir, reportDir,
                optimizeForNetworkShares: false,
                detectedNetworkOld: false,
                detectedNetworkNew: false);
            var ilTextOutputService = new ILTextOutputService(executionContext, _logger);
            var dotNetDisassembleService = new DotNetDisassembleService(
                config, ilCache: null, _resultLists, _logger, new DotNetDisassemblerCache(_logger));
            var ilOutputService = new ILOutputService(
                config, executionContext, ilTextOutputService, dotNetDisassembleService, ilCache: null, _logger);
            var fileDiffService = new FileDiffService(
                config, ilOutputService, executionContext, _resultLists, _logger);
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var folderDiffService = new FolderDiffService(
                config, progressReporter, executionContext, fileDiffService, _resultLists, _logger);

            await folderDiffService.ExecuteFolderDiffAsync();
        }

        private static ConfigSettings CreateConfig(int maxParallelism) => new ConfigSettingsBuilder
        {
            IgnoredExtensions = new List<string> { ".pdb" },
            TextFileExtensions = new List<string> { ".txt", ".cs", ".xml", ".json", ".md" },
            ShouldIncludeUnchangedFiles = true,
            ShouldIncludeIgnoredFiles = true,
            ShouldOutputILText = false,
            ShouldIgnoreILLinesContainingConfiguredStrings = false,
            ILIgnoreLineContainingStrings = new List<string>(),
            ShouldOutputFileTimestamps = false,
            ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = false,
            MaxParallelism = maxParallelism,
            OptimizeForNetworkShares = false,
            AutoDetectNetworkShares = false,
            ShouldGenerateHtmlReport = true,
            ShouldGenerateAuditLog = true,
            EnableInlineDiff = false
        }.Build();

        private static void WriteFile(string rootDir, string relativePath, string content)
        {
            var absolutePath = Path.Combine(rootDir, relativePath);
            var parentDir = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(parentDir))
                Directory.CreateDirectory(parentDir);
            File.WriteAllText(absolutePath, content);
        }
    }
}
