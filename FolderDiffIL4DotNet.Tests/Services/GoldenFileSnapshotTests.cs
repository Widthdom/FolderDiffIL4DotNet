using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Snapshot tests that validate report output structure and detect regressions
    /// by comparing generated reports against stored golden files.
    /// レポート出力の構造を検証し、保存されたゴールデンファイルと比較して
    /// リグレッションを検出するスナップショットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class GoldenFileSnapshotTests : IDisposable
    {
        private readonly string _rootDir;
        private readonly FileDiffResultLists _resultLists = new();
        private readonly ILoggerService _logger = new LoggerService();

        /// <summary>
        /// Path to the golden sample markdown report maintained in doc/samples/.
        /// doc/samples/ で管理されるゴールデンサンプル Markdown レポートのパス。
        /// </summary>
        private static readonly string GoldenSamplePath = Path.Combine(
            FindRepositoryRoot(), "doc", "samples", "diff_report.md");

        public GoldenFileSnapshotTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-snapshot-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
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

        // ── Golden sample structural validation / ゴールデンサンプル構造検証 ─────

        [Fact]
        public void GoldenSample_ContainsAllExpectedSections()
        {
            // Validate doc/samples/diff_report.md has the expected section structure
            // doc/samples/diff_report.md に期待されるセクション構造があることを検証
            var content = File.ReadAllText(GoldenSamplePath);

            var expectedSections = new[]
            {
                "# Folder Diff Report",
                "## [ x ] Ignored Files",
                "## [ = ] Unchanged Files",
                "## [ + ] Added Files",
                "## [ - ] Removed Files",
                "## [ * ] Modified Files",
                "## Summary",
                "## IL Cache Stats",
                "## Warnings",
            };

            foreach (var section in expectedSections)
            {
                Assert.Contains(section, content);
            }
        }

        [Fact]
        public void GoldenSample_SectionCountsMatchSummaryTable()
        {
            // Verify that section heading counts match the Summary table
            // セクション見出しの件数が Summary テーブルと一致することを検証
            var content = File.ReadAllText(GoldenSamplePath);

            // Extract counts from section headings
            // セクション見出しから件数を抽出
            var headingCounts = new Dictionary<string, int>();
            var headingPattern = new Regex(@"## \[ . \] (?:Ignored|Unchanged|Added|Removed|Modified) Files \((\d+)\)");
            foreach (Match m in headingPattern.Matches(content))
            {
                var label = m.Value.Contains("Ignored") ? "Ignored"
                    : m.Value.Contains("Unchanged") ? "Unchanged"
                    : m.Value.Contains("Added") ? "Added"
                    : m.Value.Contains("Removed") ? "Removed"
                    : "Modified";
                headingCounts[label] = int.Parse(m.Groups[1].Value);
            }

            // Extract counts from Summary table
            // Summary テーブルから件数を抽出
            var summaryPattern = new Regex(@"\| (\w+)\s+\|\s+(\d+)\s+\|");
            var summaryCounts = new Dictionary<string, int>();
            foreach (Match m in summaryPattern.Matches(content))
            {
                summaryCounts[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);
            }

            foreach (var kvp in headingCounts)
            {
                Assert.True(summaryCounts.ContainsKey(kvp.Key),
                    $"Summary table should contain '{kvp.Key}' row");
                Assert.Equal(kvp.Value, summaryCounts[kvp.Key]);
            }
        }

        [Fact]
        public void GoldenSample_DisassemblerAvailabilityTableIsWellFormed()
        {
            // Validate the disassembler availability table structure
            // 逆アセンブラ可用性テーブルの構造を検証
            var content = File.ReadAllText(GoldenSamplePath);

            Assert.Contains("| Tool | Available | Version |", content);
            Assert.Contains("|------|:---------:|---------|", content);

            // At least one tool row should exist / 少なくとも1つのツール行があること
            Assert.Matches(@"\| [\w-]+ \| (Yes|No) \|", content);
        }

        [Fact]
        public void GoldenSample_ModifiedFilesTableHasDisassemblerColumn()
        {
            // Modified files table must include the Disassembler column
            // Modified ファイルテーブルに Disassembler 列が含まれること
            var content = File.ReadAllText(GoldenSamplePath);

            // Find Modified Files section header and check the table header
            // Modified Files セクションヘッダーを見つけてテーブルヘッダーを確認
            int modifiedIdx = content.IndexOf("## [ * ] Modified Files", StringComparison.Ordinal);
            Assert.True(modifiedIdx >= 0, "Modified Files section must exist");

            var afterModified = content.Substring(modifiedIdx);
            Assert.Contains("| Status | File Path | Timestamp | Legend | Disassembler |", afterModified);
        }

        [Fact]
        public void GoldenSample_WarningsSectionContainsSHA256MismatchAndTimestampRegression()
        {
            // Warnings section should contain both SHA256Mismatch and timestamp regression warnings
            // Warnings セクションに SHA256Mismatch とタイムスタンプ回帰の両方の警告が含まれること
            var content = File.ReadAllText(GoldenSamplePath);

            int warningsIdx = content.IndexOf("## Warnings", StringComparison.Ordinal);
            Assert.True(warningsIdx >= 0, "Warnings section must exist");
            var warningsContent = content.Substring(warningsIdx);

            Assert.Contains("SHA256Mismatch", warningsContent);
            Assert.Contains("SHA256Mismatch: hash-only comparison, review recommended", warningsContent);
            Assert.Contains("new file timestamps older than old", warningsContent);
        }

        [Fact]
        public void GoldenSample_LegendTablesExist()
        {
            // Legend tables for diff detail and change importance must be present
            // 差分詳細と変更重要度のレジェンドテーブルが存在すること
            var content = File.ReadAllText(GoldenSamplePath);

            Assert.Contains("Legend — Diff Detail", content);
            Assert.Contains("`SHA256Match`", content);
            Assert.Contains("`ILMatch`", content);
            Assert.Contains("`TextMatch`", content);

            Assert.Contains("Legend — Change Importance", content);
            Assert.Contains("`High`", content);
            Assert.Contains("`Medium`", content);
            Assert.Contains("`Low`", content);
        }

        [Fact]
        public void GoldenSample_ILCacheStatsTableIsComplete()
        {
            // IL Cache Stats table must have all expected metrics
            // IL Cache Stats テーブルに期待されるすべてのメトリクスがあること
            var content = File.ReadAllText(GoldenSamplePath);

            int cacheIdx = content.IndexOf("## IL Cache Stats", StringComparison.Ordinal);
            Assert.True(cacheIdx >= 0, "IL Cache Stats section must exist");
            var cacheContent = content.Substring(cacheIdx);

            var expectedMetrics = new[] { "Hits", "Misses", "Hit Rate", "Stores", "Evicted", "Expired" };
            foreach (var metric in expectedMetrics)
            {
                Assert.Contains($"| {metric} |", cacheContent);
            }
        }

        [Fact]
        public void GoldenSample_ImportanceLevelsAppearInModifiedFiles()
        {
            // Modified files with ILMismatch should include importance levels
            // ILMismatch の Modified ファイルに重要度レベルが含まれること
            var content = File.ReadAllText(GoldenSamplePath);

            int modifiedIdx = content.IndexOf("## [ * ] Modified Files", StringComparison.Ordinal);
            var modifiedContent = content.Substring(modifiedIdx);

            Assert.Contains("`ILMismatch` `High`", modifiedContent);
            Assert.Contains("`ILMismatch` `Medium`", modifiedContent);
            Assert.Contains("`ILMismatch` `Low`", modifiedContent);
        }

        [Fact]
        public void GoldenSample_HeaderContainsExpectedMetadata()
        {
            // The report header should include all expected metadata fields
            // レポートヘッダーに期待されるすべてのメタデータフィールドが含まれること
            var content = File.ReadAllText(GoldenSamplePath);

            Assert.Contains("| App Version |", content);
            Assert.Contains("| Computer |", content);
            Assert.Contains("| Old |", content);
            Assert.Contains("| New |", content);
            Assert.Contains("Ignored Extensions", content);
            Assert.Contains("Text File Extensions", content);
            Assert.Contains("| Tool | Available | Version |", content);
            Assert.Contains("| Elapsed Time |", content);
        }

        [Fact]
        public void GoldenSample_SectionOrderIsCorrect()
        {
            // Sections must appear in the correct order
            // セクションが正しい順序で出現すること
            var content = File.ReadAllText(GoldenSamplePath);

            var orderedSections = new[]
            {
                "# Folder Diff Report",
                "## [ x ] Ignored Files",
                "## [ = ] Unchanged Files",
                "## [ + ] Added Files",
                "## [ - ] Removed Files",
                "## [ * ] Modified Files",
                "## Summary",
                "## IL Cache Stats",
                "## Warnings",
            };

            int lastIdx = -1;
            foreach (var section in orderedSections)
            {
                int idx = content.IndexOf(section, StringComparison.Ordinal);
                Assert.True(idx >= 0, $"Section '{section}' must exist");
                Assert.True(idx > lastIdx,
                    $"Section '{section}' must appear after the previous section (at index {idx}, previous was at {lastIdx})");
                lastIdx = idx;
            }
        }

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
            var service = new ReportGenerateService(_resultLists, _logger, config);

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
            Assert.Contains("| App Version | FolderDiffIL4DotNet 1.0.0-snapshot |", report1);
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
            Assert.Contains("| ilspycmd | No | N/A |", report1);
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
            var service = new ReportGenerateService(_resultLists, _logger, config);
            service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var report = NormalizeLineEndings(
                File.ReadAllText(Path.Combine(reportDir, "diff_report.md")));

            // Importance levels should appear in the modified files table
            // 重要度レベルが Modified ファイルテーブルに表示されること
            Assert.Contains("`ILMismatch` `High`", report);

            // Sections should be ordered correctly / セクションの順序が正しいこと
            int unchangedIdx = report.IndexOf("## [ = ] Unchanged Files", StringComparison.Ordinal);
            int modifiedIdx = report.IndexOf("## [ * ] Modified Files", StringComparison.Ordinal);
            int summaryIdx = report.IndexOf("## Summary", StringComparison.Ordinal);
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

        // ── Helpers / ヘルパー ────────────────────────────────────────────────

        private static string NormalizeLineEndings(string text)
            => text.Replace("\r\n", "\n").TrimEnd('\n');

        /// <summary>
        /// Populate test data with a representative set of file statuses (no timestamps).
        /// タイムスタンプなしの代表的なファイルステータスセットでテストデータを設定する。
        /// </summary>
        private void PopulateTestData()
        {
            _resultLists.ResetAll();

            var oldDir = "/test/old";
            var newDir = "/test/new";

            _resultLists.SetOldFilesAbsolutePath(new[]
            {
                $"{oldDir}/unchanged.txt", $"{oldDir}/unchanged.dll",
                $"{oldDir}/modified.config", $"{oldDir}/modified.dll",
                $"{oldDir}/removed.txt",
            });
            _resultLists.SetNewFilesAbsolutePath(new[]
            {
                $"{newDir}/unchanged.txt", $"{newDir}/unchanged.dll",
                $"{newDir}/modified.config", $"{newDir}/modified.dll",
                $"{newDir}/added.txt",
            });

            // Unchanged files / 変更なしファイル
            _resultLists.AddUnchangedFileRelativePath("unchanged.txt");
            _resultLists.RecordDiffDetail("unchanged.txt", FileDiffResultLists.DiffDetailResult.TextMatch);
            _resultLists.AddUnchangedFileRelativePath("unchanged.dll");
            _resultLists.RecordDiffDetail("unchanged.dll", FileDiffResultLists.DiffDetailResult.ILMatch,
                "dotnet-ildasm (version: 0.12.0)");

            // Modified files / 変更ファイル
            _resultLists.AddModifiedFileRelativePath("modified.config");
            _resultLists.RecordDiffDetail("modified.config", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.AddModifiedFileRelativePath("modified.dll");
            _resultLists.RecordDiffDetail("modified.dll", FileDiffResultLists.DiffDetailResult.ILMismatch,
                "dotnet-ildasm (version: 0.12.0)");

            // Added / 追加
            _resultLists.AddAddedFileAbsolutePath($"{newDir}/added.txt");

            // Removed / 削除
            _resultLists.AddRemovedFileAbsolutePath($"{oldDir}/removed.txt");

            // Ignored / 除外
            _resultLists.RecordIgnoredFile("ignored.pdb", FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New);

            // Disassembler availability / 逆アセンブラ可用性
            _resultLists.DisassemblerAvailability = new List<DisassemblerProbeResult>
            {
                new("dotnet-ildasm", true, "0.12.0", "/usr/bin/dotnet-ildasm"),
                new("ilspycmd", false, null, null),
            };
            _resultLists.DisassemblerToolVersions["dotnet-ildasm (version: 0.12.0)"] = 0;
        }

        /// <summary>
        /// Populate test data including assembly semantic changes with importance levels.
        /// 重要度レベルを含むアセンブリセマンティック変更を含むテストデータを設定する。
        /// </summary>
        private void PopulateTestDataWithSemanticChanges()
        {
            PopulateTestData();

            // Add semantic changes for modified.dll / modified.dll にセマンティック変更を追加
            _resultLists.FileRelativePathToAssemblySemanticChanges["modified.dll"] =
                new AssemblySemanticChangesSummary
                {
                    Entries = new List<MemberChangeEntry>
                    {
                        new("Removed", "MyApp.Api.Controller", "object", "public", "",
                            "Method", "GetLegacy", "", "IActionResult", "int id", "",
                            ChangeImportance.High),
                        new("Added", "MyApp.Api.Controller", "object", "public", "",
                            "Method", "GetV2", "", "IActionResult", "int id, string format", "",
                            ChangeImportance.Medium),
                        new("Modified", "MyApp.Api.Controller", "object", "internal", "",
                            "Method", "ProcessInternal", "", "void", "", "Changed",
                            ChangeImportance.Low),
                    },
                };
        }

        private (string oldDir, string newDir, string reportDir) MakeDirs(string label)
        {
            var old = Path.Combine(_rootDir, "old-" + label);
            var @new = Path.Combine(_rootDir, "new-" + label);
            var report = Path.Combine(_rootDir, "report-" + label);
            Directory.CreateDirectory(old);
            Directory.CreateDirectory(@new);
            Directory.CreateDirectory(report);
            return (old, @new, report);
        }

        private static ConfigSettingsBuilder CreateSnapshotConfigBuilder() => new()
        {
            IgnoredExtensions = new List<string> { ".pdb" },
            TextFileExtensions = new List<string> { ".txt", ".config" },
            ShouldIncludeUnchangedFiles = true,
            ShouldIncludeIgnoredFiles = true,
            ShouldOutputFileTimestamps = false,
            ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = false,
            ShouldGenerateHtmlReport = false,
            ShouldIncludeAssemblySemanticChangesInReport = false,
            ShouldIncludeILCacheStatsInReport = false,
        };

        private static ConfigSettings CreateSnapshotConfig() => CreateSnapshotConfigBuilder().Build();

        private static ReportGenerationContext CreateReportContext(
            string oldDir, string newDir, string reportDir,
            ConfigSettings config, ILCache? ilCache = null)
            => new(oldDir, newDir, reportDir,
                appVersion: "1.0.0-snapshot", elapsedTimeString: "0h 0m 0.0s",
                computerName: "snapshot-host", config, ilCache);

        /// <summary>
        /// Find the repository root by searching upward for the .sln file.
        /// .sln ファイルを上方検索してリポジトリルートを見つける。
        /// </summary>
        private static string FindRepositoryRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "FolderDiffIL4DotNet.sln")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            // Fallback: assume standard project layout
            // フォールバック: 標準プロジェクトレイアウトを仮定
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }
    }
}
