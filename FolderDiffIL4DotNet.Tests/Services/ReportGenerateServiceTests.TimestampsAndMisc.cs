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
    /// Tests for <see cref="ReportGenerateService"/> — timestamps, Unicode filenames, ignored files, column structure.
    /// <see cref="ReportGenerateService"/> のテスト — タイムスタンプ、Unicode ファイル名、無視ファイル、列構成。
    /// </summary>
    public sealed partial class ReportGenerateServiceTests
    {
        // ── Unicode filename report output / Unicode ファイル名レポート出力 ──

        [Fact]
        public void GenerateDiffReport_UnicodeFileNames_AreIncludedInReport()
        {
            var oldDir = Path.Combine(_rootDir, "old-unicode");
            var newDir = Path.Combine(_rootDir, "new-unicode");
            var reportDir = Path.Combine(_rootDir, "report-unicode");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            // Simulate modified files with Unicode relative paths to test encoding
            // Unicode 相対パスの変更ファイルをシミュレートしてエンコーディングをテストする
            var unicodePaths = new[]
            {
                Path.Combine("サブディレクトリ", "ファイル名.dll"),
                Path.Combine("Ünïcödé", "tëst.txt"),
                Path.Combine("中文目录", "测试文件.config"),
            };

            foreach (var relPath in unicodePaths)
            {
                _resultLists.AddModifiedFileRelativePath(relPath);
                _resultLists.RecordDiffDetail(relPath, FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            }

            var config = CreateConfig();
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host",
                config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            foreach (var relPath in unicodePaths)
            {
                Assert.Contains(relPath, reportText, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void GenerateDiffReport_UnicodeFileNames_InUnchangedSection()
        {
            var oldDir = Path.Combine(_rootDir, "old-unicode-unch");
            var newDir = Path.Combine(_rootDir, "new-unicode-unch");
            var reportDir = Path.Combine(_rootDir, "report-unicode-unch");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var unicodePath = Path.Combine("日本語", "変更なし.dll");
            _resultLists.AddUnchangedFileRelativePath(unicodePath);
            _resultLists.RecordDiffDetail(unicodePath, FileDiffResultLists.DiffDetailResult.SHA256Match);

            var config = CreateConfig();
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host",
                config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains(unicodePath, reportText, StringComparison.Ordinal);
        }

        // ── Large file count summary statistics / 大量ファイルサマリー統計 ───

        [Fact]
        public void GenerateDiffReport_LargeFileCount_SummaryStatisticsAreCorrect()
        {
            const int fileCount = 10500;
            var oldDir = Path.Combine(_rootDir, "old-large");
            var newDir = Path.Combine(_rootDir, "new-large");
            var reportDir = Path.Combine(_rootDir, "report-large");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var oldFiles = new List<string>();
            var newFiles = new List<string>();
            for (int i = 0; i < fileCount; i++)
            {
                var relPath = $"file{i:D5}.bin";
                oldFiles.Add(Path.Combine(oldDir, relPath));
                newFiles.Add(Path.Combine(newDir, relPath));
                _resultLists.AddUnchangedFileRelativePath(relPath);
                _resultLists.RecordDiffDetail(relPath, FileDiffResultLists.DiffDetailResult.SHA256Match);
            }
            _resultLists.SetOldFilesAbsolutePath(oldFiles);
            _resultLists.SetNewFilesAbsolutePath(newFiles);

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeUnchangedFiles = false; // skip writing 10k lines to report
            var config = builder.Build();
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host",
                config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Summary counts must match (table format)
            // サマリーカウントが一致すること（テーブル形式）
            Assert.Contains($"| Unchanged | {fileCount} |", reportText, StringComparison.Ordinal);
            Assert.Contains($"| Compared | {fileCount} (Old) vs {fileCount} (New) |", reportText, StringComparison.Ordinal);
        }

        [Fact]
        public void GenerateDiffReport_WithUnchangedFilesAndTimestamps_IncludesTimestamps()
        {
            var oldDir = Path.Combine(_rootDir, "old-ts-unc");
            var newDir = Path.Combine(_rootDir, "new-ts-unc");
            var reportDir = Path.Combine(_rootDir, "report-ts-unc");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var file = "unchanged.txt";
            File.WriteAllText(Path.Combine(oldDir, file), "content");
            File.WriteAllText(Path.Combine(newDir, file), "content");
            _resultLists.AddUnchangedFileRelativePath(file);
            _resultLists.RecordDiffDetail(file, FileDiffResultLists.DiffDetailResult.SHA256Match);

            var builder = CreateConfigBuilder();
            builder.ShouldOutputFileTimestamps = true;
            var config = builder.Build();

            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host", config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("unchanged.txt", reportText);
            Assert.Contains("[", reportText); // timestamp
        }

        [Fact]
        public void GenerateDiffReport_WithModifiedFilesAndTimestamps_IncludesTimestamps()
        {
            var oldDir = Path.Combine(_rootDir, "old-ts-mod");
            var newDir = Path.Combine(_rootDir, "new-ts-mod");
            var reportDir = Path.Combine(_rootDir, "report-ts-mod");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var file = "modified.txt";
            File.WriteAllText(Path.Combine(oldDir, file), "old-content");
            File.WriteAllText(Path.Combine(newDir, file), "new-content");
            _resultLists.AddModifiedFileRelativePath(file);
            _resultLists.RecordDiffDetail(file, FileDiffResultLists.DiffDetailResult.TextMismatch);

            var builder = CreateConfigBuilder();
            builder.ShouldOutputFileTimestamps = true;
            var config = builder.Build();

            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host", config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("modified.txt", reportText);
        }

        [Fact]
        public void GenerateDiffReport_WithIgnoredFilesAndTimestamps_IncludesTimestamps()
        {
            var oldDir = Path.Combine(_rootDir, "old-ts-ign");
            var newDir = Path.Combine(_rootDir, "new-ts-ign");
            var reportDir = Path.Combine(_rootDir, "report-ts-ign");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var ignoredFile = "ignored.dll";
            File.WriteAllText(Path.Combine(oldDir, ignoredFile), "old");
            File.WriteAllText(Path.Combine(newDir, ignoredFile), "new");
            _resultLists.RecordIgnoredFile(ignoredFile,
                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New);

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = true;
            builder.ShouldOutputFileTimestamps = true;
            var config = builder.Build();

            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host", config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("ignored.dll", reportText);
        }

        [Fact]
        public void GenerateDiffReport_WithIgnoredFilesNewOnly_AndTimestamps()
        {
            var oldDir = Path.Combine(_rootDir, "old-ts-ign-new");
            var newDir = Path.Combine(_rootDir, "new-ts-ign-new");
            var reportDir = Path.Combine(_rootDir, "report-ts-ign-new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var ignoredFile = "new-only-ignored.dll";
            File.WriteAllText(Path.Combine(newDir, ignoredFile), "new");
            _resultLists.RecordIgnoredFile(ignoredFile, FileDiffResultLists.IgnoredFileLocation.New);

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = true;
            builder.ShouldOutputFileTimestamps = true;
            var config = builder.Build();

            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host", config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("new-only-ignored.dll", reportText);
        }

        // Verify ildasm and ilspycmd labels in DisassemblerToolVersions exercise the display-order branches
        // DisassemblerToolVersions 内の ildasm/ilspycmd ラベルが表示順序分岐を実行することを確認する
        [Fact]
        public void GenerateDiffReport_DisassemblerOrder_IldasmAndIlspycmd_AppearInReport()
        {
            var oldDir = Path.Combine(_rootDir, "old-disasm-order");
            var newDir = Path.Combine(_rootDir, "new-disasm-order");
            var reportDir = Path.Combine(_rootDir, "report-disasm-order");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.DisassemblerToolVersions["ildasm (version: 1.0.0)"] = 0;
            _resultLists.DisassemblerToolVersions["ilspycmd (version: 7.0.0)"] = 0;
            _resultLists.DisassemblerAvailability = new List<DisassemblerProbeResult>
            {
                new("ildasm", true, "1.0.0", "/usr/bin/ildasm"),
                new("ilspycmd", true, "7.0.0", "/usr/bin/ilspycmd"),
            };

            var config = CreateConfig();
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("### Disassembler Availability", reportText);
            Assert.Contains("| ildasm | Yes | 1.0.0 |", reportText);
            Assert.Contains("| ilspycmd | Yes | 7.0.0 |", reportText);
        }

        // IgnoredFileLocation.None exercises the !hasOld && !hasNew path in BuildIgnoredFileTimestampInfo
        // IgnoredFileLocation.None で BuildIgnoredFileTimestampInfo の !hasOld && !hasNew パスを通ることを確認する
        [Fact]
        public void GenerateDiffReport_WithIgnoredFilesNoneLocation_DoesNotBreakReport()
        {
            var oldDir = Path.Combine(_rootDir, "old-ign-none");
            var newDir = Path.Combine(_rootDir, "new-ign-none");
            var reportDir = Path.Combine(_rootDir, "report-ign-none");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            // Directly insert a None location entry to trigger the default code path
            // None ロケーションエントリを直接挿入してデフォルトコードパスをトリガーする
            _resultLists.IgnoredFilesRelativePathToLocation["none-location.pdb"] =
                FileDiffResultLists.IgnoredFileLocation.None;

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = true;
            builder.ShouldOutputFileTimestamps = true;
            var config = builder.Build();

            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host",
                config, ilCache: null));

            // Report should generate without throwing / レポートが例外なく生成される
            Assert.True(File.Exists(Path.Combine(reportDir, "diff_report.md")));
        }

        // ── Column structure: per-table columns / テーブルごとの列構成 ─────────

        /// <summary>
        /// Verifies that Ignored/Added/Removed tables have 3 columns and Unchanged has 5 columns (no Estimated Change).
        /// Ignored/Added/Removed テーブルが 3 列、Unchanged が 5 列（Estimated Change なし）であることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReport_ColumnStructure_PerTableColumns()
        {
            var oldDir = Path.Combine(_rootDir, "old-col-struct");
            var newDir = Path.Combine(_rootDir, "new-col-struct");
            var reportDir = Path.Combine(_rootDir, "report-col-struct");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var oldSame = Path.Combine(oldDir, "same.dll");
            var newSame = Path.Combine(newDir, "same.dll");
            File.WriteAllText(oldSame, "same");
            File.WriteAllText(newSame, "same");
            _resultLists.SetOldFilesAbsolutePath(new List<string> { oldSame });
            _resultLists.SetNewFilesAbsolutePath(new List<string> { newSame });
            _resultLists.AddUnchangedFileRelativePath("same.dll");
            _resultLists.RecordDiffDetail("same.dll", FileDiffResultLists.DiffDetailResult.ILMatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.RecordIgnoredFile("ignored.pdb", FileDiffResultLists.IgnoredFileLocation.Old);
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "added.txt"));
            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "removed.txt"));

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = true;
            var config = builder.Build();
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host", config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Ignored Files: 3-column header (Status, File Path, Timestamp — no Diff Reason, no Estimated Change, no Disassembler)
            // Ignored Files: 3 列ヘッダ（Status, File Path, Timestamp — Diff Reason・Estimated Change・Disassembler なし）
            int ignoredIdx = reportText.IndexOf("## [ x ] Ignored Files", StringComparison.Ordinal);
            Assert.True(ignoredIdx >= 0);
            string ignoredSection = reportText.Substring(ignoredIdx, reportText.IndexOf("## [", ignoredIdx + 1, StringComparison.Ordinal) - ignoredIdx);
            Assert.Contains("| Status | File Path | Timestamp |", ignoredSection);
            Assert.DoesNotContain("Diff Reason", ignoredSection);
            Assert.DoesNotContain("Disassembler", ignoredSection);

            // Unchanged Files: 5-column header (Status, File Path, Timestamp, Diff Reason, Disassembler — no Estimated Change)
            // Unchanged Files: 5 列ヘッダ（Status, File Path, Timestamp, Diff Reason, Disassembler — Estimated Change なし）
            int unchangedIdx = reportText.IndexOf("## [ = ] Unchanged Files", StringComparison.Ordinal);
            Assert.True(unchangedIdx >= 0);
            string unchangedSection = reportText.Substring(unchangedIdx, reportText.IndexOf("## [", unchangedIdx + 1, StringComparison.Ordinal) - unchangedIdx);
            Assert.Contains("| Status | File Path | Timestamp | Diff Reason | Disassembler |", unchangedSection);
            Assert.DoesNotContain("Estimated Change", unchangedSection);
            Assert.Contains("dotnet-ildasm (version: 0.12.0)", unchangedSection);

            // Added Files: 3-column header (Status, File Path, Timestamp — no Diff Reason, no Estimated Change, no Disassembler)
            // Added Files: 3 列ヘッダ（Status, File Path, Timestamp — Diff Reason・Estimated Change・Disassembler なし）
            int addedIdx = reportText.IndexOf("## [ + ] Added Files", StringComparison.Ordinal);
            Assert.True(addedIdx >= 0);
            string addedSection = reportText.Substring(addedIdx, reportText.IndexOf("## [", addedIdx + 1, StringComparison.Ordinal) - addedIdx);
            Assert.Contains("| Status | File Path | Timestamp |", addedSection);
            Assert.DoesNotContain("Diff Reason", addedSection);
            Assert.DoesNotContain("Disassembler", addedSection);

            // Removed Files: 3-column header (Status, File Path, Timestamp — no Diff Reason, no Estimated Change, no Disassembler)
            // Removed Files: 3 列ヘッダ（Status, File Path, Timestamp — Diff Reason・Estimated Change・Disassembler なし）
            int removedIdx = reportText.IndexOf("## [ - ] Removed Files", StringComparison.Ordinal);
            Assert.True(removedIdx >= 0);
            int removedEnd = reportText.IndexOf("## [", removedIdx + 1, StringComparison.Ordinal);
            if (removedEnd < 0) removedEnd = reportText.IndexOf("## Summary", StringComparison.Ordinal);
            string removedSection = reportText.Substring(removedIdx, removedEnd - removedIdx);
            Assert.Contains("| Status | File Path | Timestamp |", removedSection);
            Assert.DoesNotContain("Diff Reason", removedSection);
            Assert.DoesNotContain("Disassembler", removedSection);
        }
    }
}
