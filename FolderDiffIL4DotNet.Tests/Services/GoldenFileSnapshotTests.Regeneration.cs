/// <summary>
/// Partial class containing regeneration consistency tests for GoldenFileSnapshotTests.
/// GoldenFileSnapshotTests の再生成一貫性テストを含むパーシャルクラス。
/// </summary>

using System.Collections.Generic;
using System.IO;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed partial class GoldenFileSnapshotTests
    {
        // ── Regeneration consistency tests / 再生成一貫性テスト ──────────────

        [Fact]
        public void GeneratedMarkdownReport_IsDeterministic()
        {
            // Generate the same markdown report twice and verify identical output.
            // Also verify structural correctness of the generated report.
            // 同じ Markdown レポートを2回生成し、同一の出力を検証する。
            // 生成されたレポートの構造的な正しさも検証する。
            var (oldDir, newDir, reportDir1) = MakeDirs("md-det-1");
            var reportDir2 = Path.Combine(_rootDir, "report-md-det-2");
            Directory.CreateDirectory(reportDir2);
            PopulateTestData();

            var config = CreateSnapshotConfig();
            var service = new ReportGenerateService(_resultLists, _logger, ReportGenerateService.CreateBuiltInSectionWriters());

            // Use same old/new dirs for both runs so paths in the report are identical
            // 両方の実行で同じ old/new ディレクトリを使用し、レポート内のパスを同一にする
            service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir1, config));
            var report1 = NormalizeLineEndings(File.ReadAllText(Path.Combine(reportDir1, "diff_report.md")));

            _resultLists.ResetAll();
            PopulateTestData();
            service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir2, config));
            var report2 = NormalizeLineEndings(File.ReadAllText(Path.Combine(reportDir2, "diff_report.md")));

            // Determinism: both runs produce identical output / 決定論: 両方の実行が同一出力
            Assert.Equal(report1, report2);

            // Structural checks on generated report / 生成レポートの構造チェック
            Assert.Contains("# Folder Diff Report", report1);
            Assert.Contains($"| App Version | {Constants.APP_NAME} 1.0.0-snapshot |", report1);
            Assert.Contains("| Computer | snapshot-host |", report1);
            Assert.Contains("## [ = ] Unchanged Files (2)", report1);
            Assert.Contains("## [ + ] Added Files (1)", report1);
            Assert.Contains("## [ - ] Removed Files (1)", report1);
            Assert.Contains("## [ * ] Modified Files (2)", report1);
            Assert.Contains("## [ x ] Ignored Files (1)", report1);
            Assert.Contains("`ILMatch`", report1);
            Assert.Contains("`ILMismatch`", report1);
            Assert.Contains("`TextMatch`", report1);
            Assert.Contains("`TextMismatch`", report1);
            Assert.Contains("| dotnet-ildasm | Yes | 0.12.0 |", report1);
            Assert.Contains("| ilspycmd | No | N/A | No |", report1);
        }

        [Fact]
        public void GeneratedMarkdownReport_WithSemanticChanges_ContainsImportanceLevels()
        {
            // Generate a markdown report with semantic changes and verify importance levels
            // セマンティック変更を含む Markdown レポートを生成し重要度レベルを検証
            var (oldDir, newDir, reportDir) = MakeDirs("md-semantic");
            PopulateTestDataWithSemanticChanges();

            var builder = CreateSnapshotConfigBuilder();
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            var service = new ReportGenerateService(_resultLists, _logger, ReportGenerateService.CreateBuiltInSectionWriters());
            service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var report = NormalizeLineEndings(
                File.ReadAllText(Path.Combine(reportDir, "diff_report.md")));

            // Importance levels should appear in the modified files table
            // 重要度レベルが Modified ファイルテーブルに表示されること
            Assert.Contains("`ILMismatch` `High`", report);

            // Sections should be ordered correctly / セクションの順序が正しいこと
            int unchangedIdx = report.IndexOf("## [ = ] Unchanged Files", System.StringComparison.Ordinal);
            int modifiedIdx = report.IndexOf("## [ * ] Modified Files", System.StringComparison.Ordinal);
            int summaryIdx = report.IndexOf("## Summary", System.StringComparison.Ordinal);
            Assert.True(unchangedIdx < modifiedIdx);
            Assert.True(modifiedIdx < summaryIdx);
        }

        [Fact]
        public void GeneratedHtmlReport_IsDeterministicAndWellFormed()
        {
            // Generate the same HTML report twice and verify identical output + structure.
            // 同じ HTML レポートを2回生成し、同一の出力と構造を検証する。
            var (oldDir, newDir, reportDir) = MakeDirs("html-det");
            PopulateTestData();

            var builder = CreateSnapshotConfigBuilder();
            builder.ShouldGenerateHtmlReport = true;
            builder.EnableInlineDiff = false; // inline diff depends on file content / インライン差分はファイル内容に依存
            var config = builder.Build();
            var service = new HtmlReportGenerateService(_resultLists, _logger, config);

            // Use same dirs for both runs (report dir is used as storage key in HTML)
            // 両方の実行で同じディレクトリを使用（レポートディレクトリは HTML の storage key に使われる）
            service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html1 = NormalizeLineEndings(File.ReadAllText(Path.Combine(reportDir, "diff_report.html")));

            _resultLists.ResetAll();
            PopulateTestData();
            // Overwrite the same file / 同じファイルを上書き
            service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html2 = NormalizeLineEndings(File.ReadAllText(Path.Combine(reportDir, "diff_report.html")));

            // Determinism / 決定論
            Assert.Equal(html1, html2);

            // Structural checks / 構造チェック
            Assert.Contains("<!DOCTYPE html>", html1);
            Assert.Contains("<html", html1);
            Assert.Contains("</html>", html1);
            Assert.Contains("Folder Diff Report", html1);
            Assert.Contains("unchanged.txt", html1);
            Assert.Contains("modified.dll", html1);
            Assert.Contains("added.txt", html1);
            Assert.Contains("removed.txt", html1);
        }
    }
}
