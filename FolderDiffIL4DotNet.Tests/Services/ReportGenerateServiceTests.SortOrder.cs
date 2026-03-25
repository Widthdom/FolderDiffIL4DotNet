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
    /// Tests for <see cref="ReportGenerateService"/> — sort order of unchanged, modified, and warnings tables.
    /// <see cref="ReportGenerateService"/> のテスト — unchanged・modified・warnings テーブルのソート順。
    /// </summary>
    public sealed partial class ReportGenerateServiceTests
    {

        /// <summary>
        /// Verifies that Warnings section tables have 4 columns (no Disassembler).
        /// Warnings セクションのテーブルが 4 列（Disassembler なし）であることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReport_WarningsColumnStructure_NoDisassemblerColumn()
        {
            var oldDir = Path.Combine(_rootDir, "old-warn-col");
            var newDir = Path.Combine(_rootDir, "new-warn-col");
            var reportDir = Path.Combine(_rootDir, "report-warn-col");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("payload.bin");
            _resultLists.RecordDiffDetail("payload.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("payload.bin", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("lib.dll", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            var config = CreateConfig();
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host", config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // SHA256Mismatch warning table: 4 columns (no Disassembler)
            int sha256Start = reportText.IndexOf("SHA256Mismatch: binary diff only", StringComparison.Ordinal);
            Assert.True(sha256Start >= 0);
            int tsRegressedStart = reportText.IndexOf("new file timestamps older than old", StringComparison.Ordinal);
            string sha256Section = reportText.Substring(sha256Start, tsRegressedStart - sha256Start);
            Assert.Contains("| Status | File Path | Timestamp | Legend |", sha256Section);
            Assert.DoesNotContain("Disassembler", sha256Section);

            // new file timestamps older than old warning table: 4 columns (no Disassembler)
            string tsSection = reportText.Substring(tsRegressedStart);
            Assert.Contains("| Status | File Path | Timestamp | Legend |", tsSection);
            Assert.DoesNotContain("Disassembler", tsSection);
        }

        // ── Assembly Semantic Changes removed from Markdown report ─────────────
        // Assembly Semantic Changes are only shown in the HTML report (as expandable
        // inline rows above IL diffs). The Markdown report no longer outputs this section.
        // アセンブリ意味変更は HTML レポートのみに表示（IL diff 上の展開可能行）。
        // Markdown レポートにはこのセクションを出力しない。

        [Fact]
        public void GenerateDiffReport_AssemblySemanticChanges_NotIncludedInMarkdownReport()
        {
            var oldDir = Path.Combine(_rootDir, "old-asc");
            var newDir = Path.Combine(_rootDir, "new-asc");
            var reportDir = Path.Combine(_rootDir, "report-asc");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("src/App.dll");
            _resultLists.RecordDiffDetail("src/App.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["src/App.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "MyApp.NewService", "", "public", "", "Class", "", "", "", "", ""),
                    new("Modified", "MyApp.UserService", "", "public", "", "Method", "Login", "", "bool", "string user, string pass", "Changed"),
                },
            };

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Markdown report must NOT contain the Assembly Semantic Changes section
            // Markdown レポートに Assembly Semantic Changes セクションが含まれないこと
            Assert.DoesNotContain("## Assembly Semantic Changes", reportText);
            Assert.DoesNotContain("semantic", reportText.ToLowerInvariant());
        }

        // ── Sort order: Unchanged files / Unchanged ファイルのソート順 ─────────

        /// <summary>
        /// Verifies that Unchanged files are sorted by SHA256Match → ILMatch → TextMatch, then by File Path ascending.
        /// Unchanged ファイルが SHA256Match → ILMatch → TextMatch の順でソートされ、その後ファイルパス昇順であることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReport_UnchangedFiles_SortedByDiffDetailThenPath()
        {
            var oldDir = Path.Combine(_rootDir, "old-unch-sort");
            var newDir = Path.Combine(_rootDir, "new-unch-sort");
            var reportDir = Path.Combine(_rootDir, "report-unch-sort");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            // Add files in deliberately wrong order (TextMatch first, then SHA256Match, then ILMatch)
            // 意図的に異なる順序でファイルを追加する（TextMatch → SHA256Match → ILMatch）
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

            var config = CreateConfig();
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Expected order: SHA256Match (aaa-sha256.bin, ccc-sha256.bin), ILMatch (bbb-il.dll), TextMatch (aaa-text.txt, zzz-text.config)
            // 期待される順序: SHA256Match (aaa-sha256.bin, ccc-sha256.bin), ILMatch (bbb-il.dll), TextMatch (aaa-text.txt, zzz-text.config)
            int sha256_aaa = reportText.IndexOf("aaa-sha256.bin", StringComparison.Ordinal);
            int sha256_ccc = reportText.IndexOf("ccc-sha256.bin", StringComparison.Ordinal);
            int il_bbb = reportText.IndexOf("bbb-il.dll", StringComparison.Ordinal);
            int text_aaa = reportText.IndexOf("aaa-text.txt", StringComparison.Ordinal);
            int text_zzz = reportText.IndexOf("zzz-text.config", StringComparison.Ordinal);

            Assert.True(sha256_aaa < sha256_ccc, "SHA256Match files should be sorted by path (aaa < ccc)");
            Assert.True(sha256_ccc < il_bbb, "SHA256Match should appear before ILMatch");
            Assert.True(il_bbb < text_aaa, "ILMatch should appear before TextMatch");
            Assert.True(text_aaa < text_zzz, "TextMatch files should be sorted by path (aaa < zzz)");
        }

        // ── Sort order: Modified files / Modified ファイルのソート順 ─────────

        /// <summary>
        /// Verifies that Modified files are sorted by TextMismatch → ILMismatch → SHA256Mismatch, then by File Path ascending.
        /// Modified ファイルが TextMismatch → ILMismatch → SHA256Mismatch の順でソートされ、その後ファイルパス昇順であることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReport_ModifiedFiles_SortedByDiffDetailThenPath()
        {
            var oldDir = Path.Combine(_rootDir, "old-mod-sort");
            var newDir = Path.Combine(_rootDir, "new-mod-sort");
            var reportDir = Path.Combine(_rootDir, "report-mod-sort");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            // Add files in deliberately wrong order (SHA256Mismatch first, then ILMismatch, then TextMismatch)
            // 意図的に異なる順序でファイルを追加する（SHA256Mismatch → ILMismatch → TextMismatch）
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

            var config = CreateConfig();
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Expected order: TextMismatch (aaa-text.txt, bbb-text.config), ILMismatch (aaa-il.dll, ccc-il.dll), SHA256Mismatch (zzz-sha256.bin)
            // 期待される順序: TextMismatch (aaa-text.txt, bbb-text.config), ILMismatch (aaa-il.dll, ccc-il.dll), SHA256Mismatch (zzz-sha256.bin)
            int text_aaa = reportText.IndexOf("aaa-text.txt", StringComparison.Ordinal);
            int text_bbb = reportText.IndexOf("bbb-text.config", StringComparison.Ordinal);
            int il_aaa = reportText.IndexOf("aaa-il.dll", StringComparison.Ordinal);
            int il_ccc = reportText.IndexOf("ccc-il.dll", StringComparison.Ordinal);
            int sha256_zzz = reportText.IndexOf("zzz-sha256.bin", StringComparison.Ordinal);

            Assert.True(text_aaa < text_bbb, "TextMismatch files should be sorted by path (aaa < bbb)");
            Assert.True(text_bbb < il_aaa, "TextMismatch should appear before ILMismatch");
            Assert.True(il_aaa < il_ccc, "ILMismatch files should be sorted by path (aaa < ccc)");
            Assert.True(il_ccc < sha256_zzz, "ILMismatch should appear before SHA256Mismatch");
        }

        // ── Sort order: Warnings timestamp-regressed table / 警告タイムスタンプ逆行テーブルのソート順 ─────────

        /// <summary>
        /// Verifies that the Warnings timestamp-regressed table is sorted by TextMismatch → ILMismatch → SHA256Mismatch, then by File Path ascending.
        /// 警告セクションのタイムスタンプ逆行テーブルが TextMismatch → ILMismatch → SHA256Mismatch の順でソートされ、その後ファイルパス昇順であることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReport_WarningsTimestampRegressed_SortedByDiffDetailThenPath()
        {
            var oldDir = Path.Combine(_rootDir, "old-warn-sort");
            var newDir = Path.Combine(_rootDir, "new-warn-sort");
            var reportDir = Path.Combine(_rootDir, "report-warn-sort");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            // Register modified files and timestamp regression warnings in deliberately wrong order
            // 意図的に異なる順序で変更ファイルとタイムスタンプ逆行警告を登録する
            _resultLists.AddModifiedFileRelativePath("zzz-sha256.bin");
            _resultLists.RecordDiffDetail("zzz-sha256.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("zzz-sha256.bin", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            _resultLists.AddModifiedFileRelativePath("aaa-il.dll");
            _resultLists.RecordDiffDetail("aaa-il.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("aaa-il.dll", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            _resultLists.AddModifiedFileRelativePath("bbb-text.config");
            _resultLists.RecordDiffDetail("bbb-text.config", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("bbb-text.config", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            var config = CreateConfig();
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // In the new file timestamps older than old table, expected order: TextMismatch (bbb-text.config), ILMismatch (aaa-il.dll), SHA256Mismatch (zzz-sha256.bin)
            // タイムスタンプ逆行テーブルの期待される順序: TextMismatch (bbb-text.config), ILMismatch (aaa-il.dll), SHA256Mismatch (zzz-sha256.bin)
            // Only look at the new file timestamps older than old section (after "new file timestamps older than old")
            int tsRegressedStart = reportText.IndexOf("new file timestamps older than old", StringComparison.Ordinal);
            Assert.True(tsRegressedStart >= 0, "new file timestamps older than old section should exist");
            string tsRegressedSection = reportText.Substring(tsRegressedStart);

            int text_bbb = tsRegressedSection.IndexOf("bbb-text.config", StringComparison.Ordinal);
            int il_aaa = tsRegressedSection.IndexOf("aaa-il.dll", StringComparison.Ordinal);
            int sha256_zzz = tsRegressedSection.IndexOf("zzz-sha256.bin", StringComparison.Ordinal);

            Assert.True(text_bbb < il_aaa, "TextMismatch should appear before ILMismatch in new file timestamps older than old table");
            Assert.True(il_aaa < sha256_zzz, "ILMismatch should appear before SHA256Mismatch in new file timestamps older than old table");
        }

    }
}
