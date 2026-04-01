using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Visual regression tests for the HTML report.
    /// Verifies structural invariants of generated HTML output to detect unintended visual regressions.
    /// HTML レポートのビジュアルリグレッションテスト。
    /// 生成された HTML 出力の構造的不変条件を検証し、意図しないビジュアル回帰を検出します。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class HtmlReportVisualRegressionTests : IDisposable
    {
        private readonly string _rootDir;
        private readonly FileDiffResultLists _resultLists = new();
        private readonly TestLogger _logger = new();
        private readonly string _generatedHtml;

        public HtmlReportVisualRegressionTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), $"fd-visual-regress-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_rootDir);

            // Seed comprehensive test data / 包括的なテストデータを投入
            SeedTestData();

            // Generate the HTML report once for all assertions / 全アサーション用に HTML レポートを一度生成
            var config = new ConfigSettingsBuilder
            {
                ShouldGenerateHtmlReport = true,
                ShouldIncludeUnchangedFiles = true,
                ShouldIncludeIgnoredFiles = true,
                ShouldIncludeAssemblySemanticChangesInReport = true
            }.Build();

            var service = new HtmlReportGenerateService(_resultLists, _logger, config);
            var oldDir = Path.Combine(_rootDir, "old");
            var newDir = Path.Combine(_rootDir, "new");
            var reportDir = Path.Combine(_rootDir, "reports");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0.0-test", elapsedTimeString: "0h 0m 1.5s",
                    computerName: "VISUAL-TEST", config, ilCache: null));

            _generatedHtml = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
        }

        public void Dispose()
        {
            try { Directory.Delete(_rootDir, recursive: true); }
            catch { /* best-effort / ベストエフォート */ }
        }

        private void SeedTestData()
        {
            // Added files / 追加ファイル
            _resultLists.AddAddedFileAbsolutePath("new-module.dll");
            _resultLists.AddAddedFileAbsolutePath("config/appsettings.json");

            // Removed files / 削除ファイル
            _resultLists.AddRemovedFileAbsolutePath("old-legacy.dll");

            // Modified files / 変更ファイル
            _resultLists.AddModifiedFileRelativePath("core/Engine.dll");
            _resultLists.RecordDiffDetail("core/Engine.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm");

            _resultLists.AddModifiedFileRelativePath("data/schema.json");
            _resultLists.RecordDiffDetail("data/schema.json", FileDiffResultLists.DiffDetailResult.TextMismatch);

            // Unchanged files / 未変更ファイル
            _resultLists.AddUnchangedFileRelativePath("lib/Stable.dll");
            _resultLists.RecordDiffDetail("lib/Stable.dll", FileDiffResultLists.DiffDetailResult.SHA256Match);
        }

        // ── Document structure / ドキュメント構造 ──

        [Fact]
        public void HtmlReport_HasDoctype()
        {
            Assert.StartsWith("<!DOCTYPE html>", _generatedHtml.TrimStart(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void HtmlReport_HasCharsetMeta()
        {
            Assert.Contains("<meta charset=\"utf-8\"", _generatedHtml, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void HtmlReport_HasContentSecurityPolicy()
        {
            Assert.Contains("Content-Security-Policy", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ContainsStyleBlock()
        {
            Assert.Contains("<style>", _generatedHtml);
            Assert.Contains("</style>", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ContainsScriptBlock()
        {
            Assert.Contains("<script>", _generatedHtml);
            Assert.Contains("</script>", _generatedHtml);
        }

        // ── Section structure / セクション構造 ──

        [Fact]
        public void HtmlReport_ContainsAllFileSections()
        {
            Assert.Contains("Added", _generatedHtml);
            Assert.Contains("Removed", _generatedHtml);
            Assert.Contains("Modified", _generatedHtml);
            Assert.Contains("Unchanged", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ContainsReviewedMarkers()
        {
            // The <!--CTRL--> markers must be present for download/reviewed workflow
            // <!--CTRL--> マーカーはダウンロード/レビュー済みワークフローに必須
            Assert.Contains("<!--CTRL-->", _generatedHtml);
            Assert.Contains("<!--/CTRL-->", _generatedHtml);
        }

        // ── Interactive elements / インタラクティブ要素 ──

        [Fact]
        public void HtmlReport_ContainsCheckboxes()
        {
            Assert.Contains("type=\"checkbox\"", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ContainsTextInputsForJustification()
        {
            // Modified files should have text inputs for reviewer justification
            // 変更ファイルにはレビュアーの理由入力用テキスト入力が含まれる
            Assert.Contains("type=\"text\"", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ContainsFilterControls()
        {
            // Filter zone for diff-detail/importance/search filtering
            // 差分詳細/重要度/検索フィルタリング用フィルタゾーン
            Assert.Contains("applyFilters", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ContainsDownloadReviewedButton()
        {
            Assert.Contains("downloadReviewed", _generatedHtml);
        }

        // ── CSS / JS integrity / CSS/JS 完全性 ──

        [Fact]
        public void HtmlReport_ContainsDarkModeSupport()
        {
            Assert.Contains("prefers-color-scheme", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ContainsKeyboardShortcutSupport()
        {
            Assert.Contains("kb-help", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ContainsLocalStorageAutoSave()
        {
            Assert.Contains("autoSave", _generatedHtml);
            Assert.Contains("localStorage", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ContainsSha256IntegrityVerification()
        {
            Assert.Contains("verifyIntegrity", _generatedHtml);
            Assert.Contains("crypto.subtle.digest", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ContainsCelebrationAnimation()
        {
            Assert.Contains("celebrateCompletion", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ContainsExcelExportFunction()
        {
            Assert.Contains("downloadExcelChunked", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ContainsThemeToggle()
        {
            Assert.Contains("cycleTheme", _generatedHtml);
        }

        // ── Data attributes / データ属性 ──

        [Fact]
        public void HtmlReport_FileRows_HaveDataSectionAttributes()
        {
            Assert.Contains("data-section=", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ModifiedRows_HaveDiffDetailAttribute()
        {
            // ILMismatch rows should have data-diff attribute
            // ILMismatch 行には data-diff 属性が付く
            Assert.Contains("ILMismatch", _generatedHtml);
        }

        // ── Seeded data rendered / 投入データの描画確認 ──

        [Fact]
        public void HtmlReport_ContainsSeededFilePaths()
        {
            Assert.Contains("new-module.dll", _generatedHtml);
            Assert.Contains("old-legacy.dll", _generatedHtml);
            Assert.Contains("core/Engine.dll", _generatedHtml);
            Assert.Contains("data/schema.json", _generatedHtml);
            Assert.Contains("lib/Stable.dll", _generatedHtml);
        }

        [Fact]
        public void HtmlReport_ContainsVersionAndComputerName()
        {
            Assert.Contains("1.0.0-test", _generatedHtml);
            Assert.Contains("VISUAL-TEST", _generatedHtml);
        }

        // ── Self-contained / 自己完結性 ──

        [Fact]
        public void HtmlReport_HasNoExternalDependencies()
        {
            // No <link rel="stylesheet" href=...> or <script src=...> tags pointing externally
            // 外部を参照する <link> や <script src> タグがないこと
            Assert.DoesNotContain("<link rel=\"stylesheet\" href=", _generatedHtml);
            Assert.DoesNotContain("<script src=", _generatedHtml);
        }

        // ── Report consistency / レポート整合性 ──

        [Fact]
        public void HtmlReport_DeterministicOutput_SameDataProducesSameHtml()
        {
            // Generate a second report with a directory that has the SAME leaf name to avoid
            // localStorage key differences (the key is derived from the report folder name).
            // 同一データ・同一フォルダ名で2回生成し、同一の HTML が出力されることを検証
            // （localStorage キーはレポートフォルダ名から導出されるため同名にする）
            var reportDir2 = Path.Combine(_rootDir, "alt", "reports");
            Directory.CreateDirectory(reportDir2);

            var config = new ConfigSettingsBuilder
            {
                ShouldGenerateHtmlReport = true,
                ShouldIncludeUnchangedFiles = true,
                ShouldIncludeIgnoredFiles = true,
                ShouldIncludeAssemblySemanticChangesInReport = true
            }.Build();

            var service2 = new HtmlReportGenerateService(_resultLists, _logger, config);
            service2.GenerateDiffReportHtml(
                new ReportGenerationContext(
                    Path.Combine(_rootDir, "old"),
                    Path.Combine(_rootDir, "new"),
                    reportDir2,
                    appVersion: "1.0.0-test",
                    elapsedTimeString: "0h 0m 1.5s",
                    computerName: "VISUAL-TEST",
                    config, ilCache: null));

            var secondHtml = File.ReadAllText(Path.Combine(reportDir2, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Equal(_generatedHtml, secondHtml);
        }
    }
}
