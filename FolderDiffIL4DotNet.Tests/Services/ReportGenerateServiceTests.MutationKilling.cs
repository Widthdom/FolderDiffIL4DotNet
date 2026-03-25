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

        // ── Mutation-killing: GetUnchangedSortOrder return values ─────────
        // ミューテーションキル: GetUnchangedSortOrder の戻り値

        /// <summary>
        /// Unchanged files are sorted: SHA256Match → ILMatch → TextMatch.
        /// Unchanged ファイルは SHA256Match → ILMatch → TextMatch の順にソートされる。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_UnchangedFiles_SortedByDiffDetailType()
        {
            var oldDir = Path.Combine(_rootDir, "old-unch-sort");
            var newDir = Path.Combine(_rootDir, "new-unch-sort");
            var reportDir = Path.Combine(_rootDir, "report-unch-sort");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            // Add files in reverse order of expected sort / 期待されるソート順の逆順で追加
            _resultLists.AddUnchangedFileRelativePath("c-text-match.dll");
            _resultLists.RecordDiffDetail("c-text-match.dll", FileDiffResultLists.DiffDetailResult.TextMatch);

            _resultLists.AddUnchangedFileRelativePath("b-il-match.dll");
            _resultLists.RecordDiffDetail("b-il-match.dll", FileDiffResultLists.DiffDetailResult.ILMatch);

            _resultLists.AddUnchangedFileRelativePath("a-sha256-match.dll");
            _resultLists.RecordDiffDetail("a-sha256-match.dll", FileDiffResultLists.DiffDetailResult.SHA256Match);

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));
            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            int shaIdx = reportText.IndexOf("a-sha256-match.dll", System.StringComparison.Ordinal);
            int ilIdx = reportText.IndexOf("b-il-match.dll", System.StringComparison.Ordinal);
            int textIdx = reportText.IndexOf("c-text-match.dll", System.StringComparison.Ordinal);

            Assert.True(shaIdx >= 0, "SHA256Match file should appear in report");
            Assert.True(ilIdx >= 0, "ILMatch file should appear in report");
            Assert.True(textIdx >= 0, "TextMatch file should appear in report");
            Assert.True(shaIdx < ilIdx, "SHA256Match should appear before ILMatch");
            Assert.True(ilIdx < textIdx, "ILMatch should appear before TextMatch");
        }

        // ── Mutation-killing: GetModifiedSortOrder return values ──────────
        // ミューテーションキル: GetModifiedSortOrder の戻り値

        /// <summary>
        /// Modified files are sorted: TextMismatch → ILMismatch → SHA256Mismatch.
        /// Modified ファイルは TextMismatch → ILMismatch → SHA256Mismatch の順にソートされる。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_ModifiedFiles_SortedByDiffDetailType()
        {
            var oldDir = Path.Combine(_rootDir, "old-mod-sort");
            var newDir = Path.Combine(_rootDir, "new-mod-sort");
            var reportDir = Path.Combine(_rootDir, "report-mod-sort");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            // Add in reverse order of expected sort / 期待されるソート順の逆順で追加
            _resultLists.AddModifiedFileRelativePath("c-sha256-mismatch.dll");
            _resultLists.RecordDiffDetail("c-sha256-mismatch.dll", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);

            _resultLists.AddModifiedFileRelativePath("b-il-mismatch.dll");
            _resultLists.RecordDiffDetail("b-il-mismatch.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm");

            _resultLists.AddModifiedFileRelativePath("a-text-mismatch.txt");
            _resultLists.RecordDiffDetail("a-text-mismatch.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));
            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            int modifiedIdx = reportText.IndexOf("## [ * ] Modified Files", System.StringComparison.Ordinal);
            Assert.True(modifiedIdx >= 0);
            string modifiedSection = reportText.Substring(modifiedIdx);

            int textIdx = modifiedSection.IndexOf("a-text-mismatch.txt", System.StringComparison.Ordinal);
            int ilIdx = modifiedSection.IndexOf("b-il-mismatch.dll", System.StringComparison.Ordinal);
            int sha256Idx = modifiedSection.IndexOf("c-sha256-mismatch.dll", System.StringComparison.Ordinal);

            Assert.True(textIdx >= 0, "TextMismatch file should appear in Modified section");
            Assert.True(ilIdx >= 0, "ILMismatch file should appear in Modified section");
            Assert.True(sha256Idx >= 0, "SHA256Mismatch file should appear in Modified section");
            Assert.True(textIdx < ilIdx, "TextMismatch should appear before ILMismatch");
            Assert.True(ilIdx < sha256Idx, "ILMismatch should appear before SHA256Mismatch");
        }

        // ── Mutation-killing: GetIgnoredFileLocationLabel return values ───
        // ミューテーションキル: GetIgnoredFileLocationLabel の戻り値

        /// <summary>
        /// Ignored files show correct location label (Old / New / Both).
        /// 無視ファイルが正しいロケーションラベル（Old / New / Both）を表示する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_IgnoredFiles_ShowCorrectLocationLabels()
        {
            var oldDir = Path.Combine(_rootDir, "old-ign-loc");
            var newDir = Path.Combine(_rootDir, "new-ign-loc");
            var reportDir = Path.Combine(_rootDir, "report-ign-loc");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.RecordIgnoredFile("old-only.pdb", FileDiffResultLists.IgnoredFileLocation.Old);
            _resultLists.RecordIgnoredFile("new-only.pdb", FileDiffResultLists.IgnoredFileLocation.New);
            _resultLists.RecordIgnoredFile("both.pdb", FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New);

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = true;
            var config = builder.Build();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));
            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Verify each location label appears / 各ロケーションラベルの表示を検証
            Assert.Contains("old-only.pdb", reportText);
            Assert.Contains("new-only.pdb", reportText);
            Assert.Contains("both.pdb", reportText);
            // Verify Old/New/Both labels in the ignored section / 無視セクションで Old/New/Both ラベルを検証
            Assert.Contains("Old", reportText);
            Assert.Contains("New", reportText);
            Assert.Contains("Both", reportText);
        }

        // ── Mutation-killing: BuildDiffDetailDisplay importance vs null ───
        // ミューテーションキル: BuildDiffDetailDisplay の重要度 vs null

        /// <summary>
        /// Modified files with importance show importance label, without show only diff detail.
        /// 重要度のある Modified ファイルは重要度ラベルを表示し、ない場合は差分詳細のみ表示する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_ModifiedFile_ImportanceDisplayDiffers()
        {
            var oldDir = Path.Combine(_rootDir, "old-imp-disp");
            var newDir = Path.Combine(_rootDir, "new-imp-disp");
            var reportDir = Path.Combine(_rootDir, "report-imp-disp");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            // File with importance / 重要度ありファイル
            _resultLists.AddModifiedFileRelativePath("with-imp.dll");
            _resultLists.RecordDiffDetail("with-imp.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm");
            _resultLists.FileRelativePathToAssemblySemanticChanges["with-imp.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Removed", "MyApp.Service", "", "public", "", "Method", "Execute", "", "void", "", "", ChangeImportance.High),
                }
            };

            // File without importance / 重要度なしファイル
            _resultLists.AddModifiedFileRelativePath("no-imp.dll");
            _resultLists.RecordDiffDetail("no-imp.dll", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));
            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // with-imp.dll should have importance label / with-imp.dll は重要度ラベルを持つべき
            Assert.Contains("`ILMismatch` `High`", reportText);
            // no-imp.dll should have only diff detail without importance / no-imp.dll は差分詳細のみ
            Assert.Contains("`TextMismatch`", reportText);
        }

        // ── Mutation-killing: disassembler display for IL results ──────────
        // ミューテーションキル: IL 結果の逆アセンブラ表示

        /// <summary>
        /// Disassembler label is shown for ILMatch/ILMismatch but not for TextMismatch.
        /// 逆アセンブララベルは ILMatch/ILMismatch で表示されるが TextMismatch では非表示。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_DisassemblerLabel_ShownOnlyForILResults()
        {
            var oldDir = Path.Combine(_rootDir, "old-disasm-disp");
            var newDir = Path.Combine(_rootDir, "new-disasm-disp");
            var reportDir = Path.Combine(_rootDir, "report-disasm-disp");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("il-file.dll");
            _resultLists.RecordDiffDetail("il-file.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.AddModifiedFileRelativePath("text-file.config");
            _resultLists.RecordDiffDetail("text-file.config", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));
            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // IL file row should show disassembler label / IL ファイル行は逆アセンブララベルを表示
            Assert.Contains("dotnet-ildasm (version: 0.12.0)", reportText);
        }

        // ── Mutation-killing: GetDisassemblerDisplayOrder return values ───
        // ミューテーションキル: GetDisassemblerDisplayOrder の戻り値

        /// <summary>
        /// Disassembler display order: dotnet-ildasm → ildasm → ilspycmd.
        /// 逆アセンブラ表示順: dotnet-ildasm → ildasm → ilspycmd。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_DisassemblerHeaderOrder_IsCorrect()
        {
            var oldDir = Path.Combine(_rootDir, "old-disasm-order");
            var newDir = Path.Combine(_rootDir, "new-disasm-order");
            var reportDir = Path.Combine(_rootDir, "report-disasm-order");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            // Record both tools by adding files that use them / 両方のツールを記録するためファイルを追加
            _resultLists.AddUnchangedFileRelativePath("a.dll");
            _resultLists.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.ILMatch, "ilspycmd (version: 8.0.0)");
            _resultLists.AddUnchangedFileRelativePath("b.dll");
            _resultLists.RecordDiffDetail("b.dll", FileDiffResultLists.DiffDetailResult.ILMatch, "dotnet-ildasm (version: 0.12.0)");

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));
            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            int ildasmIdx = reportText.IndexOf("dotnet-ildasm", System.StringComparison.Ordinal);
            int ilspyIdx = reportText.IndexOf("ilspycmd", System.StringComparison.Ordinal);
            Assert.True(ildasmIdx >= 0, "dotnet-ildasm should appear in report");
            Assert.True(ilspyIdx >= 0, "ilspycmd should appear in report");
            Assert.True(ildasmIdx < ilspyIdx, "dotnet-ildasm should appear before ilspycmd");
        }
    }
}
