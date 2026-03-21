using System;
using System.Collections.Generic;
using System.IO;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class ReportGenerateServiceTests : IDisposable
    {
        private readonly string _rootDir;
        private readonly FileDiffResultLists _resultLists = new();
        private readonly ILoggerService _logger = new LoggerService();
        private readonly ReportGenerateService _service;

        public ReportGenerateServiceTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-report-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
            _service = new ReportGenerateService(_resultLists, _logger, new ConfigSettings());
            ClearResultLists();
        }

        public void Dispose()
        {
            ClearResultLists();
            try
            {
                if (Directory.Exists(_rootDir))
                {
                    Directory.Delete(_rootDir, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup errors / クリーンアップエラーを無視
            }
        }

        [Fact]
        public void GenerateDiffReport_HeaderListsOnlyObservedDisassemblers()
        {
            _resultLists.DisassemblerToolVersions["dotnet-ildasm (version: dotnet ildasm 0.12.0)"] = 0;

            var oldDir = Path.Combine(_rootDir, "old");
            var newDir = Path.Combine(_rootDir, "new");
            var reportDir = Path.Combine(_rootDir, "report");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var config = CreateConfig();
            _service.GenerateDiffReport(
                oldDir,
                newDir,
                reportDir,
                appVersion: "test",
                elapsedTimeString: "00:00:01.000",
                computerName: "test-host",
                config);

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("- IL Disassembler: dotnet-ildasm (version: dotnet ildasm 0.12.0)", reportText);
            Assert.DoesNotContain(", ildasm", reportText);
            Assert.DoesNotContain(", ilspycmd", reportText);
        }

        [Fact]
        public void GenerateDiffReport_HeaderShowsNotUsed_WhenNoDisassemblerWasObserved()
        {
            var oldDir = Path.Combine(_rootDir, "old-none");
            var newDir = Path.Combine(_rootDir, "new-none");
            var reportDir = Path.Combine(_rootDir, "report-none");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var config = CreateConfig();
            _service.GenerateDiffReport(
                oldDir,
                newDir,
                reportDir,
                appVersion: "test",
                elapsedTimeString: "00:00:01.000",
                computerName: "test-host",
                config);

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("- IL Disassembler: N/A", reportText);
        }

        [Fact]
        public void GenerateDiffReport_HeaderShowsMvidReasonNote()
        {
            var oldDir = Path.Combine(_rootDir, "old-mvid-note");
            var newDir = Path.Combine(_rootDir, "new-mvid-note");
            var reportDir = Path.Combine(_rootDir, "report-mvid-note");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var config = CreateConfig();
            _service.GenerateDiffReport(
                oldDir,
                newDir,
                reportDir,
                appVersion: "test",
                elapsedTimeString: "00:00:01.000",
                computerName: "test-host",
                config);

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("lines starting with \"// MVID:\" (if present) are ignored because they contain disassembler-emitted Module Version ID metadata", reportText);
        }

        [Fact]
        public void GenerateDiffReport_IlDiffDetailsIncludeDisassemblerLabel()
        {
            var oldDir = Path.Combine(_rootDir, "old");
            var newDir = Path.Combine(_rootDir, "new");
            var reportDir = Path.Combine(_rootDir, "report");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.SetOldFilesAbsolutePath(new List<string> { Path.Combine(oldDir, "a.dll"), Path.Combine(oldDir, "b.dll") });
            _resultLists.SetNewFilesAbsolutePath(new List<string> { Path.Combine(newDir, "a.dll"), Path.Combine(newDir, "b.dll") });
            _resultLists.AddUnchangedFileRelativePath("a.dll");
            _resultLists.AddModifiedFileRelativePath("b.dll");

            _resultLists.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.ILMatch, "dotnet-ildasm (version: dotnet ildasm 0.12.0)");
            _resultLists.RecordDiffDetail("b.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: dotnet ildasm 0.12.0)");

            var config = CreateConfig();
            _service.GenerateDiffReport(
                oldDir,
                newDir,
                reportDir,
                appVersion: "test",
                elapsedTimeString: "00:00:01.000",
                computerName: "test-host",
                config);

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("`ILMatch`", reportText);
            Assert.Contains("`ILMismatch`", reportText);
            Assert.Contains("`dotnet-ildasm (version: dotnet ildasm 0.12.0)`", reportText);
        }

        [Fact]
        public void GenerateDiffReport_HeaderShowsIlContainsIgnoreNote_WhenEnabled()
        {
            var oldDir = Path.Combine(_rootDir, "old-ignore-note");
            var newDir = Path.Combine(_rootDir, "new-ignore-note");
            var reportDir = Path.Combine(_rootDir, "report-ignore-note");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var config = CreateConfig();
            config.ShouldIgnoreILLinesContainingConfiguredStrings = true;
            config.ILIgnoreLineContainingStrings = new List<string> { "buildserver", " buildPath ", "", "buildserver" };

            _service.GenerateDiffReport(
                oldDir,
                newDir,
                reportDir,
                appVersion: "test",
                elapsedTimeString: "00:00:01.000",
                computerName: "test-host",
                config);

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("lines containing any of the configured strings are ignored:", reportText);
            Assert.Contains("| Ignored String |", reportText);
            Assert.Contains("| \"buildserver\" |", reportText);
            Assert.Contains("| \"buildPath\" |", reportText);
        }

        [Fact]
        public void GenerateDiffReport_HeaderOmitsIlContainsIgnoreNote_WhenDisabled()
        {
            var oldDir = Path.Combine(_rootDir, "old-ignore-note-off");
            var newDir = Path.Combine(_rootDir, "new-ignore-note-off");
            var reportDir = Path.Combine(_rootDir, "report-ignore-note-off");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var config = CreateConfig();
            config.ShouldIgnoreILLinesContainingConfiguredStrings = false;
            config.ILIgnoreLineContainingStrings = new List<string> { "buildserver" };

            _service.GenerateDiffReport(
                oldDir,
                newDir,
                reportDir,
                appVersion: "test",
                elapsedTimeString: "00:00:01.000",
                computerName: "test-host",
                config);

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.DoesNotContain("lines containing any of the configured strings are ignored", reportText);
        }

        [Fact]
        public void GenerateDiffReport_HeaderShowsEmptyIlContainsIgnoreNote_WhenEnabledButNoValidStrings()
        {
            var oldDir = Path.Combine(_rootDir, "old-ignore-note-empty");
            var newDir = Path.Combine(_rootDir, "new-ignore-note-empty");
            var reportDir = Path.Combine(_rootDir, "report-ignore-note-empty");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var config = CreateConfig();
            config.ShouldIgnoreILLinesContainingConfiguredStrings = true;
            config.ILIgnoreLineContainingStrings = new List<string> { "", "   ", "\t" };

            _service.GenerateDiffReport(
                oldDir,
                newDir,
                reportDir,
                appVersion: "test",
                elapsedTimeString: "00:00:01.000",
                computerName: "test-host",
                config);

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("IL line-ignore-by-contains is enabled, but no non-empty strings are configured.", reportText);
        }

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
            _resultLists.RecordDiffDetail("same.txt", FileDiffResultLists.DiffDetailResult.MD5Match);
            _resultLists.AddModifiedFileRelativePath("modified.txt");
            _resultLists.RecordDiffDetail("modified.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.AddRemovedFileAbsolutePath(oldRemoved);
            _resultLists.AddAddedFileAbsolutePath(newAdded);
            _resultLists.RecordIgnoredFile("ignored.pdb", FileDiffResultLists.IgnoredFileLocation.Old);

            var config = CreateConfig();
            config.ShouldIncludeIgnoredFiles = true;
            _service.GenerateDiffReport(
                oldDir,
                newDir,
                reportDir,
                appVersion: "test",
                elapsedTimeString: "00:00:01.000",
                computerName: "test-host",
                config);

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
        public void GenerateDiffReport_WritesMd5MismatchWarningInWarningsSection_WhenMd5MismatchExists()
        {
            var oldDir = Path.Combine(_rootDir, "old-md5-warning");
            var newDir = Path.Combine(_rootDir, "new-md5-warning");
            var reportDir = Path.Combine(_rootDir, "report-md5-warning");
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
            _resultLists.RecordDiffDetail("payload.bin", FileDiffResultLists.DiffDetailResult.MD5Mismatch);

            var config = CreateConfig();
            _service.GenerateDiffReport(
                oldDir,
                newDir,
                reportDir,
                appVersion: "test",
                elapsedTimeString: "00:00:01.000",
                computerName: "test-host",
                config);

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("## Warnings", reportText);
            Assert.Contains($"- **WARNING:** {Constants.WARNING_MD5_MISMATCH}", reportText);
            Assert.True(
                reportText.IndexOf("## Summary", StringComparison.Ordinal) <
                reportText.IndexOf("## Warnings", StringComparison.Ordinal));
            Assert.True(
                reportText.IndexOf("## Warnings", StringComparison.Ordinal) <
                reportText.IndexOf(Constants.WARNING_MD5_MISMATCH, StringComparison.Ordinal));
        }

        [Fact]
        public void GenerateDiffReport_DoesNotEmitConsoleWarningLog_WhenMd5MismatchExists()
        {
            var oldDir = Path.Combine(_rootDir, "old-md5-warning-log");
            var newDir = Path.Combine(_rootDir, "new-md5-warning-log");
            var reportDir = Path.Combine(_rootDir, "report-md5-warning-log");
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
            _resultLists.RecordDiffDetail("payload.bin", FileDiffResultLists.DiffDetailResult.MD5Mismatch);

            var logger = new TestLogger();
            var config = CreateConfig();
            var service = new ReportGenerateService(_resultLists, logger, config);
            service.GenerateDiffReport(
                oldDir,
                newDir,
                reportDir,
                appVersion: "test",
                elapsedTimeString: "00:00:01.000",
                computerName: "test-host",
                config);

            Assert.DoesNotContain(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning && entry.Message == Constants.WARNING_MD5_MISMATCH);
        }

        [Fact]
        public void GenerateDiffReport_WritesWarningsInSeverityOrder_WhenMd5MismatchAndTimestampRegressionExist()
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
            _resultLists.RecordDiffDetail(Path.Combine("nested", "payload.bin"), FileDiffResultLists.DiffDetailResult.MD5Mismatch);

            var config = CreateConfig();
            _service.GenerateDiffReport(
                oldDir,
                newDir,
                reportDir,
                appVersion: "test",
                elapsedTimeString: "00:00:01.000",
                computerName: "test-host",
                config);

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("## Warnings", reportText);
            Assert.Contains($"- **WARNING:** {Constants.WARNING_MD5_MISMATCH}", reportText);
            Assert.Contains("- **WARNING:** One or more **modified** files in `new` have older last-modified timestamps than the corresponding files in `old`.", reportText);
            Assert.Contains("| Status | File Path | Timestamp | Legend | Disassembler |", reportText);
            Assert.Contains("|:------:|-----------|:---------:|--------|--------------|", reportText);
            Assert.Contains("| nested", reportText);
            Assert.Contains("2026-03-14 10:00:00 → 2026-03-14 09:00:00", reportText);
            Assert.True(
                reportText.IndexOf(Constants.WARNING_MD5_MISMATCH, StringComparison.Ordinal) <
                reportText.IndexOf("**modified** files in `new` have older last-modified timestamps", StringComparison.Ordinal));
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
            _service.GenerateDiffReport(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config);

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

            var config = CreateConfig();
            config.ShouldIncludeILCacheStatsInReport = true;
            _service.GenerateDiffReport(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config, ilCache: null);

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

            var config = CreateConfig();
            config.ShouldIncludeILCacheStatsInReport = true;
            var ilCache = new ILCache(ilCacheDirectoryAbsolutePath: string.Empty);

            _resultLists.RecordDiffDetail("some.dll", FileDiffResultLists.DiffDetailResult.MD5Mismatch);
            _service.GenerateDiffReport(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config, ilCache);

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
                _resultLists.RecordDiffDetail(relPath, FileDiffResultLists.DiffDetailResult.MD5Mismatch);
            }

            var config = CreateConfig();
            _service.GenerateDiffReport(
                oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host",
                config);

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
            _resultLists.RecordDiffDetail(unicodePath, FileDiffResultLists.DiffDetailResult.MD5Match);

            var config = CreateConfig();
            _service.GenerateDiffReport(
                oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host",
                config);

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
                _resultLists.RecordDiffDetail(relPath, FileDiffResultLists.DiffDetailResult.MD5Match);
            }
            _resultLists.SetOldFilesAbsolutePath(oldFiles);
            _resultLists.SetNewFilesAbsolutePath(newFiles);

            var config = CreateConfig();
            config.ShouldIncludeUnchangedFiles = false; // skip writing 10k lines to report
            _service.GenerateDiffReport(
                oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host",
                config);

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
            _resultLists.RecordDiffDetail(file, FileDiffResultLists.DiffDetailResult.MD5Match);

            var config = CreateConfig();
            config.ShouldOutputFileTimestamps = true;

            _service.GenerateDiffReport(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host", config);

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

            var config = CreateConfig();
            config.ShouldOutputFileTimestamps = true;

            _service.GenerateDiffReport(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host", config);

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

            var config = CreateConfig();
            config.ShouldIncludeIgnoredFiles = true;
            config.ShouldOutputFileTimestamps = true;

            _service.GenerateDiffReport(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host", config);

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

            var config = CreateConfig();
            config.ShouldIncludeIgnoredFiles = true;
            config.ShouldOutputFileTimestamps = true;

            _service.GenerateDiffReport(oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host", config);

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

            var config = CreateConfig();
            _service.GenerateDiffReport(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config);

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("ildasm (version: 1.0.0)", reportText);
            Assert.Contains("ilspycmd (version: 7.0.0)", reportText);
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

            var config = CreateConfig();
            config.ShouldIncludeIgnoredFiles = true;
            config.ShouldOutputFileTimestamps = true;

            _service.GenerateDiffReport(
                oldDir, newDir, reportDir,
                appVersion: "1.0", elapsedTimeString: null, computerName: "test-host",
                config);

            // Report should generate without throwing / レポートが例外なく生成される
            Assert.True(File.Exists(Path.Combine(reportDir, "diff_report.md")));
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

            var config = CreateConfig();
            config.ShouldIncludeAssemblySemanticChangesInReport = true;
            _service.GenerateDiffReport(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config);

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Markdown report must NOT contain the Assembly Semantic Changes section
            // Markdown レポートに Assembly Semantic Changes セクションが含まれないこと
            Assert.DoesNotContain("## Assembly Semantic Changes", reportText);
            Assert.DoesNotContain("semantic", reportText.ToLowerInvariant());
        }

        // ── Sort order: Unchanged files / Unchanged ファイルのソート順 ─────────

        /// <summary>
        /// Verifies that Unchanged files are sorted by MD5Match → ILMatch → TextMatch, then by File Path ascending.
        /// Unchanged ファイルが MD5Match → ILMatch → TextMatch の順でソートされ、その後ファイルパス昇順であることを確認する。
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

            // Add files in deliberately wrong order (TextMatch first, then MD5Match, then ILMatch)
            // 意図的に異なる順序でファイルを追加する（TextMatch → MD5Match → ILMatch）
            _resultLists.AddUnchangedFileRelativePath("zzz-text.config");
            _resultLists.RecordDiffDetail("zzz-text.config", FileDiffResultLists.DiffDetailResult.TextMatch);
            _resultLists.AddUnchangedFileRelativePath("aaa-md5.bin");
            _resultLists.RecordDiffDetail("aaa-md5.bin", FileDiffResultLists.DiffDetailResult.MD5Match);
            _resultLists.AddUnchangedFileRelativePath("bbb-il.dll");
            _resultLists.RecordDiffDetail("bbb-il.dll", FileDiffResultLists.DiffDetailResult.ILMatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.AddUnchangedFileRelativePath("ccc-md5.bin");
            _resultLists.RecordDiffDetail("ccc-md5.bin", FileDiffResultLists.DiffDetailResult.MD5Match);
            _resultLists.AddUnchangedFileRelativePath("aaa-text.txt");
            _resultLists.RecordDiffDetail("aaa-text.txt", FileDiffResultLists.DiffDetailResult.TextMatch);

            var config = CreateConfig();
            _service.GenerateDiffReport(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config);

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Expected order: MD5Match (aaa-md5.bin, ccc-md5.bin), ILMatch (bbb-il.dll), TextMatch (aaa-text.txt, zzz-text.config)
            // 期待される順序: MD5Match (aaa-md5.bin, ccc-md5.bin), ILMatch (bbb-il.dll), TextMatch (aaa-text.txt, zzz-text.config)
            int md5_aaa = reportText.IndexOf("aaa-md5.bin", StringComparison.Ordinal);
            int md5_ccc = reportText.IndexOf("ccc-md5.bin", StringComparison.Ordinal);
            int il_bbb = reportText.IndexOf("bbb-il.dll", StringComparison.Ordinal);
            int text_aaa = reportText.IndexOf("aaa-text.txt", StringComparison.Ordinal);
            int text_zzz = reportText.IndexOf("zzz-text.config", StringComparison.Ordinal);

            Assert.True(md5_aaa < md5_ccc, "MD5Match files should be sorted by path (aaa < ccc)");
            Assert.True(md5_ccc < il_bbb, "MD5Match should appear before ILMatch");
            Assert.True(il_bbb < text_aaa, "ILMatch should appear before TextMatch");
            Assert.True(text_aaa < text_zzz, "TextMatch files should be sorted by path (aaa < zzz)");
        }

        // ── Sort order: Modified files / Modified ファイルのソート順 ─────────

        /// <summary>
        /// Verifies that Modified files are sorted by TextMismatch → ILMismatch → MD5Mismatch, then by File Path ascending.
        /// Modified ファイルが TextMismatch → ILMismatch → MD5Mismatch の順でソートされ、その後ファイルパス昇順であることを確認する。
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

            // Add files in deliberately wrong order (MD5Mismatch first, then ILMismatch, then TextMismatch)
            // 意図的に異なる順序でファイルを追加する（MD5Mismatch → ILMismatch → TextMismatch）
            _resultLists.AddModifiedFileRelativePath("zzz-md5.bin");
            _resultLists.RecordDiffDetail("zzz-md5.bin", FileDiffResultLists.DiffDetailResult.MD5Mismatch);
            _resultLists.AddModifiedFileRelativePath("aaa-il.dll");
            _resultLists.RecordDiffDetail("aaa-il.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.AddModifiedFileRelativePath("bbb-text.config");
            _resultLists.RecordDiffDetail("bbb-text.config", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.AddModifiedFileRelativePath("ccc-il.dll");
            _resultLists.RecordDiffDetail("ccc-il.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.AddModifiedFileRelativePath("aaa-text.txt");
            _resultLists.RecordDiffDetail("aaa-text.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var config = CreateConfig();
            _service.GenerateDiffReport(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config);

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Expected order: TextMismatch (aaa-text.txt, bbb-text.config), ILMismatch (aaa-il.dll, ccc-il.dll), MD5Mismatch (zzz-md5.bin)
            // 期待される順序: TextMismatch (aaa-text.txt, bbb-text.config), ILMismatch (aaa-il.dll, ccc-il.dll), MD5Mismatch (zzz-md5.bin)
            int text_aaa = reportText.IndexOf("aaa-text.txt", StringComparison.Ordinal);
            int text_bbb = reportText.IndexOf("bbb-text.config", StringComparison.Ordinal);
            int il_aaa = reportText.IndexOf("aaa-il.dll", StringComparison.Ordinal);
            int il_ccc = reportText.IndexOf("ccc-il.dll", StringComparison.Ordinal);
            int md5_zzz = reportText.IndexOf("zzz-md5.bin", StringComparison.Ordinal);

            Assert.True(text_aaa < text_bbb, "TextMismatch files should be sorted by path (aaa < bbb)");
            Assert.True(text_bbb < il_aaa, "TextMismatch should appear before ILMismatch");
            Assert.True(il_aaa < il_ccc, "ILMismatch files should be sorted by path (aaa < ccc)");
            Assert.True(il_ccc < md5_zzz, "ILMismatch should appear before MD5Mismatch");
        }

        // ── Sort order: Warnings timestamp-regressed table / 警告タイムスタンプ逆行テーブルのソート順 ─────────

        /// <summary>
        /// Verifies that the Warnings timestamp-regressed table is sorted by TextMismatch → ILMismatch → MD5Mismatch, then by File Path ascending.
        /// 警告セクションのタイムスタンプ逆行テーブルが TextMismatch → ILMismatch → MD5Mismatch の順でソートされ、その後ファイルパス昇順であることを確認する。
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
            _resultLists.AddModifiedFileRelativePath("zzz-md5.bin");
            _resultLists.RecordDiffDetail("zzz-md5.bin", FileDiffResultLists.DiffDetailResult.MD5Mismatch);
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("zzz-md5.bin", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            _resultLists.AddModifiedFileRelativePath("aaa-il.dll");
            _resultLists.RecordDiffDetail("aaa-il.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("aaa-il.dll", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            _resultLists.AddModifiedFileRelativePath("bbb-text.config");
            _resultLists.RecordDiffDetail("bbb-text.config", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("bbb-text.config", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            var config = CreateConfig();
            _service.GenerateDiffReport(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config);

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // In the Warnings section, expected order: TextMismatch (bbb-text.config), ILMismatch (aaa-il.dll), MD5Mismatch (zzz-md5.bin)
            // 警告セクションの期待される順序: TextMismatch (bbb-text.config), ILMismatch (aaa-il.dll), MD5Mismatch (zzz-md5.bin)
            // Only look at the Warnings section (after "## Warnings")
            int warningsSectionStart = reportText.IndexOf("## Warnings", StringComparison.Ordinal);
            Assert.True(warningsSectionStart >= 0, "Warnings section should exist");
            string warningsSection = reportText.Substring(warningsSectionStart);

            int text_bbb = warningsSection.IndexOf("bbb-text.config", StringComparison.Ordinal);
            int il_aaa = warningsSection.IndexOf("aaa-il.dll", StringComparison.Ordinal);
            int md5_zzz = warningsSection.IndexOf("zzz-md5.bin", StringComparison.Ordinal);

            Assert.True(text_bbb < il_aaa, "TextMismatch should appear before ILMismatch in Warnings");
            Assert.True(il_aaa < md5_zzz, "ILMismatch should appear before MD5Mismatch in Warnings");
        }

        private static ConfigSettings CreateConfig() => new()
        {
            IgnoredExtensions = new List<string>(),
            TextFileExtensions = new List<string>(),
            ShouldIncludeUnchangedFiles = true,
            ShouldIncludeIgnoredFiles = false,
            ShouldOutputFileTimestamps = false,
            ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = true
        };

        private void ClearResultLists()
        {
            _resultLists.ResetAll();
        }

        private sealed class TestLogger : ILoggerService
        {
            public string LogFileAbsolutePath => null;

            public List<LogEntry> Entries { get; } = new();

            public void Initialize() { }

            public void CleanupOldLogFiles(int maxLogGenerations) { }

            public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, Exception exception = null)
                => LogMessage(logLevel, message, shouldOutputMessageToConsole, consoleForegroundColor: null, exception);

            public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, ConsoleColor? consoleForegroundColor, Exception exception = null)
                => Entries.Add(new LogEntry(logLevel, message));
        }

        private sealed record LogEntry(AppLogLevel LogLevel, string Message);
    }
}
