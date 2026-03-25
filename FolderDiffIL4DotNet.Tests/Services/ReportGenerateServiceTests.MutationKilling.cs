using System;
using System.Collections.Generic;
using System.IO;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="ReportGenerateService"/> — mutation-killing tests (dependency changes, importance, summary rows).
    /// <see cref="ReportGenerateService"/> のテスト — ミューテーションキリングテスト（依存関係変更、重要度、サマリー行）。
    /// </summary>
    public sealed partial class ReportGenerateServiceTests
    {
        // ── Mutation-testing-focused tests / ミューテーションテスト向けテスト ─────────

        /// <summary>
        /// Dependency changes sub-table appears with correct markers.
        /// 依存関係変更サブテーブルが正しいマーカーで表示されることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_ModifiedFiles_DependencyChanges_AppearsWithMarkers()
        {
            var oldDir = Path.Combine(_rootDir, "old-dep-markers");
            var newDir = Path.Combine(_rootDir, "new-dep-markers");
            var reportDir = Path.Combine(_rootDir, "report-dep-markers");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("app.deps.json");
            _resultLists.RecordDiffDetail("app.deps.json", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.FileRelativePathToDependencyChanges["app.deps.json"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Added", "Newtonsoft.Json", "", "13.0.3", ChangeImportance.Medium),
                    new("Removed", "System.Text.Json", "6.0.0", "", ChangeImportance.High),
                    new("Updated", "Serilog", "2.0.0", "3.0.0", ChangeImportance.Low),
                }
            };

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeDependencyChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Assert markers / マーカーを検証
            Assert.Contains("#### Dependency Changes: app.deps.json", reportText);
            Assert.Contains("| Newtonsoft.Json | `[ + ]` | `Medium` |", reportText);
            Assert.Contains("| System.Text.Json | `[ - ]` | `High` |", reportText);
            Assert.Contains("| Serilog | `[ * ]` | `Low` |", reportText);
        }

        /// <summary>
        /// Empty dependency versions render as em-dash.
        /// 空の依存関係バージョンがエムダッシュとして描画されることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_DependencyChanges_EmptyVersionShowsEmDash()
        {
            var oldDir = Path.Combine(_rootDir, "old-dep-emdash");
            var newDir = Path.Combine(_rootDir, "new-dep-emdash");
            var reportDir = Path.Combine(_rootDir, "report-dep-emdash");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("app.deps.json");
            _resultLists.RecordDiffDetail("app.deps.json", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.FileRelativePathToDependencyChanges["app.deps.json"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Added", "NewPkg", "", "1.0.0", ChangeImportance.Low),
                    new("Removed", "OldPkg", "2.0.0", "", ChangeImportance.High),
                }
            };

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeDependencyChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Added: OldVersion should be em-dash / 追加: OldVersion がエムダッシュ
            Assert.Contains("| NewPkg | `[ + ]` | `Low` | \u2014 | 1.0.0 |", reportText);
            // Removed: NewVersion should be em-dash / 削除: NewVersion がエムダッシュ
            Assert.Contains("| OldPkg | `[ - ]` | `High` | 2.0.0 | \u2014 |", reportText);
        }

        /// <summary>
        /// Dependency sub-table not shown when disabled.
        /// 無効時に依存関係サブテーブルが非表示であることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_DependencyChanges_NotShownWhenDisabled()
        {
            var oldDir = Path.Combine(_rootDir, "old-dep-off");
            var newDir = Path.Combine(_rootDir, "new-dep-off");
            var reportDir = Path.Combine(_rootDir, "report-dep-off");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("app.deps.json");
            _resultLists.RecordDiffDetail("app.deps.json", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.FileRelativePathToDependencyChanges["app.deps.json"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Added", "SomePkg", "", "1.0.0", ChangeImportance.Low),
                }
            };

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeDependencyChangesInReport = false;
            var config = builder.Build();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.DoesNotContain("#### Dependency Changes", reportText);
        }

        /// <summary>
        /// Disassembler display empty for non-IL diff detail.
        /// 非 IL 判定時に逆アセンブラ表示が空であることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_DisassemblerDisplay_EmptyForNonILDiffDetail()
        {
            var oldDir = Path.Combine(_rootDir, "old-disasm-sha");
            var newDir = Path.Combine(_rootDir, "new-disasm-sha");
            var reportDir = Path.Combine(_rootDir, "report-disasm-sha");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddUnchangedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.SHA256Match);
            _resultLists.FileRelativePathToIlDisassemblerLabelDictionary["lib.dll"] = "dotnet-ildasm (version: 0.12.0)";

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Unchanged section should not show the disassembler label for SHA256Match / SHA256Match では逆アセンブララベル非表示
            int unchangedIdx = reportText.IndexOf("## [ = ] Unchanged Files", StringComparison.Ordinal);
            Assert.True(unchangedIdx >= 0);
            int summaryIdx = reportText.IndexOf("## Summary", StringComparison.Ordinal);
            string unchangedSection = reportText.Substring(unchangedIdx, summaryIdx - unchangedIdx);
            Assert.Contains("lib.dll", unchangedSection);
            Assert.DoesNotContain("`dotnet-ildasm (version: 0.12.0)`", unchangedSection);
        }

        /// <summary>
        /// Modified file with null importance shows diff detail without importance label.
        /// importance が null の変更ファイルが重要度ラベルなしで判定根拠を表示する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_ModifiedFile_NullImportance_ShowsDiffDetailOnly()
        {
            var oldDir = Path.Combine(_rootDir, "old-null-imp");
            var newDir = Path.Combine(_rootDir, "new-null-imp");
            var reportDir = Path.Combine(_rootDir, "report-null-imp");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("plain.config");
            _resultLists.RecordDiffDetail("plain.config", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            int modifiedIdx = reportText.IndexOf("## [ * ] Modified Files", StringComparison.Ordinal);
            Assert.True(modifiedIdx >= 0);
            int summaryIdx = reportText.IndexOf("## Summary", StringComparison.Ordinal);
            string modifiedSection = reportText.Substring(modifiedIdx, summaryIdx - modifiedIdx);
            Assert.Contains("`TextMismatch`", modifiedSection);
            Assert.DoesNotContain("`High`", modifiedSection);
            Assert.DoesNotContain("`Medium`", modifiedSection);
            Assert.DoesNotContain("`Low`", modifiedSection);
        }

        /// <summary>
        /// Modified file with semantic changes shows both diff detail and importance.
        /// セマンティック変更ありの変更ファイルが判定根拠と重要度の両方を表示する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_ModifiedFile_WithImportance_ShowsBoth()
        {
            var oldDir = Path.Combine(_rootDir, "old-with-imp");
            var newDir = Path.Combine(_rootDir, "new-with-imp");
            var reportDir = Path.Combine(_rootDir, "report-with-imp");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    // Importance must be set explicitly; Classify is not called by the report service
                    // 重要度は明示的に設定する必要がある（レポートサービスは Classify を呼ばない）
                    new("Added", "MyApp.NewService", "", "public", "", "Class", "", "", "", "", "", ChangeImportance.Medium),
                },
            };

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            int modifiedIdx = reportText.IndexOf("## [ * ] Modified Files", StringComparison.Ordinal);
            Assert.True(modifiedIdx >= 0);
            int summaryIdx = reportText.IndexOf("## Summary", StringComparison.Ordinal);
            string modifiedSection = reportText.Substring(modifiedIdx, summaryIdx - modifiedIdx);
            Assert.Contains("`ILMismatch` `Medium`", modifiedSection);
        }

        /// <summary>
        /// Unchanged files with identical timestamps show single timestamp (no arrow).
        /// 同一タイムスタンプの unchanged ファイルが単一タイムスタンプを表示する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_UnchangedFiles_SameTimestamp_NoArrow()
        {
            var oldDir = Path.Combine(_rootDir, "old-same-ts");
            var newDir = Path.Combine(_rootDir, "new-same-ts");
            var reportDir = Path.Combine(_rootDir, "report-same-ts");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var file = "same-ts.txt";
            File.WriteAllText(Path.Combine(oldDir, file), "content");
            File.WriteAllText(Path.Combine(newDir, file), "content");

            var fixedTime = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(Path.Combine(oldDir, file), fixedTime);
            File.SetLastWriteTimeUtc(Path.Combine(newDir, file), fixedTime);

            _resultLists.AddUnchangedFileRelativePath(file);
            _resultLists.RecordDiffDetail(file, FileDiffResultLists.DiffDetailResult.SHA256Match);

            var builder = CreateConfigBuilder();
            builder.ShouldOutputFileTimestamps = true;
            var config = builder.Build();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Same timestamps should not have arrow / 同一タイムスタンプに矢印なし
            int unchangedIdx = reportText.IndexOf("## [ = ] Unchanged Files", StringComparison.Ordinal);
            Assert.True(unchangedIdx >= 0);
            int summaryIdx = reportText.IndexOf("## Summary", StringComparison.Ordinal);
            string unchangedSection = reportText.Substring(unchangedIdx, summaryIdx - unchangedIdx);
            Assert.Contains("same-ts.txt", unchangedSection);
            Assert.DoesNotContain("\u2192", unchangedSection);
        }

        /// <summary>
        /// Summary section omits Ignored row when ShouldIncludeIgnoredFiles=false.
        /// ShouldIncludeIgnoredFiles=false の場合 Summary に Ignored 行がないことを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_Summary_OmitsIgnoredRow_WhenDisabled()
        {
            var oldDir = Path.Combine(_rootDir, "old-sum-no-ign");
            var newDir = Path.Combine(_rootDir, "new-sum-no-ign");
            var reportDir = Path.Combine(_rootDir, "report-sum-no-ign");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.RecordIgnoredFile("ignored.pdb", FileDiffResultLists.IgnoredFileLocation.Old);

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = false;
            var config = builder.Build();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            int summaryIdx = reportText.IndexOf("## Summary", StringComparison.Ordinal);
            Assert.True(summaryIdx >= 0);
            string summarySection = reportText.Substring(summaryIdx);
            Assert.DoesNotContain("| Ignored |", summarySection);
            Assert.Contains("| Unchanged |", summarySection);
        }

        /// <summary>
        /// Summary section includes Ignored row when ShouldIncludeIgnoredFiles=true.
        /// ShouldIncludeIgnoredFiles=true の場合 Summary に Ignored 行があることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_Summary_IncludesIgnoredRow_WhenEnabled()
        {
            var oldDir = Path.Combine(_rootDir, "old-sum-ign");
            var newDir = Path.Combine(_rootDir, "new-sum-ign");
            var reportDir = Path.Combine(_rootDir, "report-sum-ign");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.RecordIgnoredFile("ignored.pdb", FileDiffResultLists.IgnoredFileLocation.Old);

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = true;
            var config = builder.Build();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            int summaryIdx = reportText.IndexOf("## Summary", StringComparison.Ordinal);
            Assert.True(summaryIdx >= 0);
            string summarySection = reportText.Substring(summaryIdx);
            Assert.Contains("| Ignored | 1 |", summarySection);
        }

        /// <summary>
        /// DisassemblerAvailabilityTable not emitted for empty probe list (not null).
        /// 空のプローブリスト（null ではない）で逆アセンブラ利用可否テーブルが非出力。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_DisassemblerAvailability_EmptyList_NotEmitted()
        {
            var oldDir = Path.Combine(_rootDir, "old-disasm-empty");
            var newDir = Path.Combine(_rootDir, "new-disasm-empty");
            var reportDir = Path.Combine(_rootDir, "report-disasm-empty");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.DisassemblerAvailability = new List<DisassemblerProbeResult>();

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.DoesNotContain("### Disassembler Availability", reportText);
        }

        /// <summary>
        /// Modified files with same diff detail sub-sorted by importance (High first).
        /// 同一判定根拠の変更ファイルが重要度でサブソートされる（High が先頭）。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_ModifiedFiles_SortedByImportance()
        {
            var oldDir = Path.Combine(_rootDir, "old-imp-sort");
            var newDir = Path.Combine(_rootDir, "new-imp-sort");
            var reportDir = Path.Combine(_rootDir, "report-imp-sort");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("low-imp.dll");
            _resultLists.RecordDiffDetail("low-imp.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.FileRelativePathToAssemblySemanticChanges["low-imp.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Modified", "Ns.Cls", "", "internal", "", "Method", "M", "", "void", "", "Changed"),
                },
            };

            _resultLists.AddModifiedFileRelativePath("high-imp.dll");
            _resultLists.RecordDiffDetail("high-imp.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.FileRelativePathToAssemblySemanticChanges["high-imp.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Removed", "Ns.PublicApi", "", "public", "", "Method", "DoStuff", "", "void", "", ""),
                },
            };

            _resultLists.AddModifiedFileRelativePath("no-imp.dll");
            _resultLists.RecordDiffDetail("no-imp.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            int modifiedIdx = reportText.IndexOf("## [ * ] Modified Files", StringComparison.Ordinal);
            Assert.True(modifiedIdx >= 0);
            int summaryIdx = reportText.IndexOf("## Summary", StringComparison.Ordinal);
            string modifiedSection = reportText.Substring(modifiedIdx, summaryIdx - modifiedIdx);

            int highIdx = modifiedSection.IndexOf("high-imp.dll", StringComparison.Ordinal);
            int lowIdx = modifiedSection.IndexOf("low-imp.dll", StringComparison.Ordinal);
            int noIdx = modifiedSection.IndexOf("no-imp.dll", StringComparison.Ordinal);

            Assert.True(highIdx < lowIdx, "High importance should appear before Low importance");
            Assert.True(lowIdx < noIdx, "Low importance should appear before null importance");
        }

    }
}
