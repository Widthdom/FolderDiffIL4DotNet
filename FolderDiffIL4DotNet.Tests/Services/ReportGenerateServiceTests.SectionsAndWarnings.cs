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
    /// Tests for <see cref="ReportGenerateService"/> — main sections, SHA256 warnings, IL cache stats.
    /// <see cref="ReportGenerateService"/> のテスト — メインセクション、SHA256 警告、IL キャッシュ統計。
    /// </summary>
    public sealed partial class ReportGenerateServiceTests
    {
        [Fact]
        public void GenerateDiffReport_WritesAllMainSections()
        {
            var oldDir = Path.Combine(_rootDir, "old-sections");
            var newDir = Path.Combine(_rootDir, "new-sections");
            var reportDir = Path.Combine(_rootDir, "report-sections");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var oldSame = Path.Combine(oldDir, "same.txt");
            var newSame = Path.Combine(newDir, "same.txt");
            var oldModified = Path.Combine(oldDir, "modified.txt");
            var newModified = Path.Combine(newDir, "modified.txt");
            var oldRemoved = Path.Combine(oldDir, "removed.txt");
            var newAdded = Path.Combine(newDir, "added.txt");
            File.WriteAllText(oldSame, "same");
            File.WriteAllText(newSame, "same");
            File.WriteAllText(oldModified, "before");
            File.WriteAllText(newModified, "after");
            File.WriteAllText(oldRemoved, "removed");
            File.WriteAllText(newAdded, "added");

            _resultLists.SetOldFilesAbsolutePath(new List<string> { oldSame, oldModified, oldRemoved });
            _resultLists.SetNewFilesAbsolutePath(new List<string> { newSame, newModified, newAdded });
            _resultLists.AddUnchangedFileRelativePath("same.txt");
            _resultLists.RecordDiffDetail("same.txt", FileDiffResultLists.DiffDetailResult.SHA256Match);
            _resultLists.AddModifiedFileRelativePath("modified.txt");
            _resultLists.RecordDiffDetail("modified.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.AddRemovedFileAbsolutePath(oldRemoved);
            _resultLists.AddAddedFileAbsolutePath(newAdded);
            _resultLists.RecordIgnoredFile("ignored.pdb", FileDiffResultLists.IgnoredFileLocation.Old);

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = true;
            var config = builder.Build();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("## [ x ] Ignored Files", reportText);
            Assert.Contains("## [ = ] Unchanged Files", reportText);
            Assert.Contains("## [ + ] Added Files", reportText);
            Assert.Contains("## [ - ] Removed Files", reportText);
            Assert.Contains("## [ * ] Modified Files", reportText);
            Assert.Contains("## Summary", reportText);
        }

        [Fact]
        public void GenerateDiffReport_WritesSha256MismatchWarningInWarningsSection_WhenSha256MismatchExists()
        {
            var oldDir = Path.Combine(_rootDir, "old-sha256-warning");
            var newDir = Path.Combine(_rootDir, "new-sha256-warning");
            var reportDir = Path.Combine(_rootDir, "report-sha256-warning");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var oldFile = Path.Combine(oldDir, "payload.bin");
            var newFile = Path.Combine(newDir, "payload.bin");
            File.WriteAllText(oldFile, "old");
            File.WriteAllText(newFile, "new");

            _resultLists.SetOldFilesAbsolutePath(new List<string> { oldFile });
            _resultLists.SetNewFilesAbsolutePath(new List<string> { newFile });
            _resultLists.AddModifiedFileRelativePath("payload.bin");
            _resultLists.RecordDiffDetail("payload.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("## Warnings", reportText);
            Assert.Contains("SHA256Mismatch: binary diff only", reportText);
            Assert.True(
                reportText.IndexOf("## Summary", StringComparison.Ordinal) <
                reportText.IndexOf("## Warnings", StringComparison.Ordinal));
        }

        /// <summary>
        /// Verifies that Markdown Warnings section includes the SHA256Mismatch detail table with file listing.
        /// Markdown の警告セクションに SHA256Mismatch 詳細テーブル（ファイル一覧）が含まれることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReport_WritesSha256MismatchDetailTable_WhenSha256MismatchExists()
        {
            var oldDir = Path.Combine(_rootDir, "old-sha256-table");
            var newDir = Path.Combine(_rootDir, "new-sha256-table");
            var reportDir = Path.Combine(_rootDir, "report-sha256-table");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var oldFile1 = Path.Combine(oldDir, "alpha.bin");
            var newFile1 = Path.Combine(newDir, "alpha.bin");
            File.WriteAllText(oldFile1, "old");
            File.WriteAllText(newFile1, "new");

            var oldFile2 = Path.Combine(oldDir, "beta.bin");
            var newFile2 = Path.Combine(newDir, "beta.bin");
            File.WriteAllText(oldFile2, "old");
            File.WriteAllText(newFile2, "new");

            _resultLists.SetOldFilesAbsolutePath(new List<string> { oldFile1, oldFile2 });
            _resultLists.SetNewFilesAbsolutePath(new List<string> { newFile1, newFile2 });
            _resultLists.AddModifiedFileRelativePath("alpha.bin");
            _resultLists.RecordDiffDetail("alpha.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _resultLists.AddModifiedFileRelativePath("beta.bin");
            _resultLists.RecordDiffDetail("beta.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            // TextMismatch file should NOT appear in the SHA256Mismatch table
            _resultLists.AddModifiedFileRelativePath("gamma.txt");
            _resultLists.RecordDiffDetail("gamma.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);

            // Table heading should exist with count
            Assert.Contains("SHA256Mismatch: binary diff only — not a .NET assembly and not a recognized text file (2)", reportText);

            // Extract the SHA256Mismatch table section
            int sha256TableStart = reportText.IndexOf("SHA256Mismatch: binary diff only", StringComparison.Ordinal);
            Assert.True(sha256TableStart >= 0, "SHA256Mismatch detail table heading should exist");
            string sha256Section = reportText.Substring(sha256TableStart);

            // Both SHA256Mismatch files should appear
            Assert.Contains("alpha.bin", sha256Section);
            Assert.Contains("beta.bin", sha256Section);

            // Files should be sorted alphabetically (alpha before beta)
            int alphaIdx = sha256Section.IndexOf("alpha.bin", StringComparison.Ordinal);
            int betaIdx = sha256Section.IndexOf("beta.bin", StringComparison.Ordinal);
            Assert.True(alphaIdx < betaIdx, "SHA256Mismatch files should be sorted alphabetically");

            // TextMismatch file should NOT appear in SHA256Mismatch table
            string sha256TableEnd = sha256Section;
            int tsTableIdx = sha256TableEnd.IndexOf("new file timestamps older than old", StringComparison.Ordinal);
            if (tsTableIdx > 0) sha256TableEnd = sha256TableEnd.Substring(0, tsTableIdx);
            Assert.DoesNotContain("gamma.txt", sha256TableEnd);
        }

        /// <summary>
        /// Verifies that SHA256Mismatch detail table appears before new file timestamps older than old table in Markdown when both warnings exist.
        /// 両方の警告が存在する場合、Markdown で SHA256Mismatch 詳細テーブルがタイムスタンプ逆行テーブルの前に表示されることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReport_Sha256MismatchTable_AppearsBeforeTimestampRegressedTable()
        {
            var oldDir = Path.Combine(_rootDir, "old-sha256-before-ts");
            var newDir = Path.Combine(_rootDir, "new-sha256-before-ts");
            var reportDir = Path.Combine(_rootDir, "report-sha256-before-ts");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("payload.bin");
            _resultLists.RecordDiffDetail("payload.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("payload.bin", "2026-03-14 10:00:00", "2026-03-14 09:00:00");

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);

            int sha256TableIdx = reportText.IndexOf("SHA256Mismatch: binary diff only", StringComparison.Ordinal);
            int tsTableIdx = reportText.IndexOf("new file timestamps older than old", StringComparison.Ordinal);
            Assert.True(sha256TableIdx >= 0, "SHA256Mismatch detail table should exist");
            Assert.True(tsTableIdx >= 0, "new file timestamps older than old table should exist");
            Assert.True(sha256TableIdx < tsTableIdx, "SHA256Mismatch table should appear before new file timestamps older than old table");
        }

        /// <summary>
        /// Verifies that each warning message is immediately followed by its detail table (interleaved layout).
        /// When both warnings exist, the SHA256Mismatch detail table appears between the SHA256Mismatch warning
        /// and the timestamp regression warning, rather than all warnings being listed first.
        /// 各警告メッセージの直下に対応する詳細テーブルが配置されること（インターリーブレイアウト）を確認する。
        /// 両方の警告がある場合、SHA256Mismatch 詳細テーブルは SHA256Mismatch 警告とタイムスタンプ逆行警告の間に配置される。
        /// </summary>
        [Fact]
        public void GenerateDiffReport_Sha256MismatchDetailTable_AppearsImmediatelyAfterSha256Warning()
        {
            var oldDir = Path.Combine(_rootDir, "old-sha256-interleave");
            var newDir = Path.Combine(_rootDir, "new-sha256-interleave");
            var reportDir = Path.Combine(_rootDir, "report-sha256-interleave");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("payload.bin");
            _resultLists.RecordDiffDetail("payload.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("payload.bin", "2026-03-14 10:00:00", "2026-03-14 09:00:00");

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);

            int sha256TableIdx = reportText.IndexOf("SHA256Mismatch: binary diff only", StringComparison.Ordinal);
            int tsTableIdx = reportText.IndexOf("new file timestamps older than old", StringComparison.Ordinal);

            Assert.True(sha256TableIdx >= 0, "SHA256Mismatch detail table should exist");
            Assert.True(tsTableIdx >= 0, "new file timestamps older than old detail table should exist");

            // SHA256 table should appear before Timestamp table / SHA256テーブルはタイムスタンプテーブルの前に表示
            Assert.True(sha256TableIdx < tsTableIdx,
                "SHA256Mismatch table should appear before timestamp regression table");
        }

        [Fact]
        public void GenerateDiffReport_DoesNotEmitConsoleWarningLog_WhenSha256MismatchExists()
        {
            var oldDir = Path.Combine(_rootDir, "old-sha256-warning-log");
            var newDir = Path.Combine(_rootDir, "new-sha256-warning-log");
            var reportDir = Path.Combine(_rootDir, "report-sha256-warning-log");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var oldFile = Path.Combine(oldDir, "payload.bin");
            var newFile = Path.Combine(newDir, "payload.bin");
            File.WriteAllText(oldFile, "old");
            File.WriteAllText(newFile, "new");

            _resultLists.SetOldFilesAbsolutePath(new List<string> { oldFile });
            _resultLists.SetNewFilesAbsolutePath(new List<string> { newFile });
            _resultLists.AddModifiedFileRelativePath("payload.bin");
            _resultLists.RecordDiffDetail("payload.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);

            var logger = new TestLogger();
            var config = CreateConfig();
            var service = new ReportGenerateService(_resultLists, logger, config);
            service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            Assert.DoesNotContain(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning && entry.Message == Constants.WARNING_SHA256_MISMATCH);
        }

        [Fact]
        public void GenerateDiffReport_WritesWarningsInSeverityOrder_WhenSha256MismatchAndTimestampRegressionExist()
        {
            var oldDir = Path.Combine(_rootDir, "old-timestamp-warning");
            var newDir = Path.Combine(_rootDir, "new-timestamp-warning");
            var reportDir = Path.Combine(_rootDir, "report-timestamp-warning");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.RecordNewFileTimestampOlderThanOldWarning(
                Path.Combine("nested", "payload.bin"),
                "2026-03-14 10:00:00",
                "2026-03-14 09:00:00");
            _resultLists.RecordDiffDetail(Path.Combine("nested", "payload.bin"), FileDiffResultLists.DiffDetailResult.SHA256Mismatch);

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("## Warnings", reportText);
            Assert.Contains("SHA256Mismatch: binary diff only", reportText);
            Assert.Contains("new file timestamps older than old", reportText);
            Assert.Contains("| Status | File Path | Timestamp | Diff Reason | Estimated Change | Disassembler |", reportText);
            Assert.Contains("|:------:|-----------|:---------:|:-----------:|:----------------:|--------------|", reportText);
            Assert.Contains("| nested", reportText);
            Assert.Contains("2026-03-14 10:00:00 → 2026-03-14 09:00:00", reportText);
        }

        [Fact]
        public void GenerateDiffReport_ILCacheStats_NotIncludedByDefault()
        {
            var oldDir = Path.Combine(_rootDir, "old-ilcs-default");
            var newDir = Path.Combine(_rootDir, "new-ilcs-default");
            var reportDir = Path.Combine(_rootDir, "report-ilcs-default");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var config = CreateConfig();
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.DoesNotContain("## IL Cache Stats", reportText);
        }

        [Fact]
        public void GenerateDiffReport_ILCacheStats_NotOutputWhenEnabled_ButCacheIsNull()
        {
            var oldDir = Path.Combine(_rootDir, "old-ilcs-null");
            var newDir = Path.Combine(_rootDir, "new-ilcs-null");
            var reportDir = Path.Combine(_rootDir, "report-ilcs-null");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeILCacheStatsInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config, ilCache: null));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.DoesNotContain("## IL Cache Stats", reportText);
        }

        [Fact]
        public void GenerateDiffReport_ILCacheStats_OutputBetweenSummaryAndWarnings_WhenEnabledWithCache()
        {
            var oldDir = Path.Combine(_rootDir, "old-ilcs-full");
            var newDir = Path.Combine(_rootDir, "new-ilcs-full");
            var reportDir = Path.Combine(_rootDir, "report-ilcs-full");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeILCacheStatsInReport = true;
            var config = builder.Build();
            var ilCache = new ILCache(ilCacheDirectoryAbsolutePath: string.Empty);

            _resultLists.RecordDiffDetail("some.dll", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config, ilCache));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("## IL Cache Stats", reportText);
            Assert.Contains("| Hits |", reportText);
            Assert.Contains("| Misses |", reportText);
            Assert.Contains("| Hit Rate |", reportText);
            Assert.Contains("| Stores |", reportText);
            Assert.Contains("| Evicted |", reportText);
            Assert.Contains("| Expired |", reportText);
            // IL Cache Stats section must appear between Summary and Warnings
            // IL Cache Stats セクションは Summary と Warnings の間に出力されること
            int summaryIdx = reportText.IndexOf("## Summary", StringComparison.Ordinal);
            int ilCacheIdx = reportText.IndexOf("## IL Cache Stats", StringComparison.Ordinal);
            int warningsIdx = reportText.IndexOf("## Warnings", StringComparison.Ordinal);
            Assert.True(summaryIdx < ilCacheIdx);
            Assert.True(ilCacheIdx < warningsIdx);
        }

    }
}
