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
            _service = new ReportGenerateService(_resultLists, _logger, new ConfigSettingsBuilder().Build());
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
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.DoesNotContain("| IL Disassembler |", reportText);
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
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportPath = Path.Combine(reportDir, "diff_report.md");
            var reportText = File.ReadAllText(reportPath);
            Assert.DoesNotContain("| IL Disassembler |", reportText);
        }

        [Fact]
        public void GenerateDiffReport_HeaderShowsDisassemblerAvailabilityTable()
        {
            // Arrange: populate availability with one available and one unavailable tool
            // 1 つ利用可能、1 つ利用不可のツールで可用性を設定
            _resultLists.DisassemblerAvailability = new List<DisassemblerProbeResult>
            {
                new("dotnet-ildasm", true, "0.12.2", "/usr/local/bin/dotnet-ildasm"),
                new("ilspycmd", false, null, null),
            };

            var oldDir = Path.Combine(_rootDir, "old-avail");
            var newDir = Path.Combine(_rootDir, "new-avail");
            var reportDir = Path.Combine(_rootDir, "report-avail");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Assert: availability table structure and content
            // テーブルの構造と内容を検証
            Assert.Contains("| Tool | Available | Version | In Use |", reportText);
            Assert.Contains("| dotnet-ildasm | Yes | 0.12.2 |", reportText);
            Assert.Contains("| ilspycmd | No | N/A |  |", reportText);
        }

        [Fact]
        public void GenerateDiffReport_HeaderOmitsAvailabilityTable_WhenProbeResultsAreNull()
        {
            // Arrange: no probe results (default: null)
            // プローブ結果なし（既定値: null）
            var oldDir = Path.Combine(_rootDir, "old-no-probe");
            var newDir = Path.Combine(_rootDir, "new-no-probe");
            var reportDir = Path.Combine(_rootDir, "report-no-probe");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Assert: no availability table when probe results are null
            // プローブ結果が null の場合、テーブルは出力されない
            Assert.DoesNotContain("Disassembler Availability", reportText);
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
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

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
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

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

            var builder = CreateConfigBuilder();
            builder.ShouldIgnoreILLinesContainingConfiguredStrings = true;
            builder.ILIgnoreLineContainingStrings = new List<string> { "buildserver", " buildPath ", "", "buildserver" };
            var config = builder.Build();

            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

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

            var builder = CreateConfigBuilder();
            builder.ShouldIgnoreILLinesContainingConfiguredStrings = false;
            builder.ILIgnoreLineContainingStrings = new List<string> { "buildserver" };
            var config = builder.Build();

            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

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

            var builder = CreateConfigBuilder();
            builder.ShouldIgnoreILLinesContainingConfiguredStrings = true;
            builder.ILIgnoreLineContainingStrings = new List<string> { "", "   ", "\t" };
            var config = builder.Build();

            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

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
            Assert.Contains("SHA256Mismatch: binary diff only (2)", reportText);

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
            Assert.Contains("| Status | File Path | Timestamp | Legend |", reportText);
            Assert.Contains("|:------:|-----------|:---------:|:------:|", reportText);
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

            var config = CreateConfig();
            _service.GenerateDiffReport(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: null, computerName: "test-host",
                config, ilCache: null));

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
        /// Verifies that Ignored Files table has 4 columns (no Disassembler) and Added/Removed have 3 columns (no Legend, no Disassembler).
        /// Ignored Files テーブルが 4 列（Disassembler なし）、Added/Removed が 3 列（Legend・Disassembler なし）であることを確認する。
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

            // Ignored Files: 4-column header (Status, File Path, Timestamp, Legend — no Disassembler)
            // Ignored Files: 4 列ヘッダ（Status, File Path, Timestamp, Legend — Disassembler なし）
            int ignoredIdx = reportText.IndexOf("## [ x ] Ignored Files", StringComparison.Ordinal);
            Assert.True(ignoredIdx >= 0);
            string ignoredSection = reportText.Substring(ignoredIdx, reportText.IndexOf("## [", ignoredIdx + 1, StringComparison.Ordinal) - ignoredIdx);
            Assert.Contains("| Status | File Path | Timestamp | Legend |", ignoredSection);
            Assert.DoesNotContain("Disassembler", ignoredSection);

            // Unchanged Files: 5-column header (keeps Disassembler)
            // Unchanged Files: 5 列ヘッダ（Disassembler あり）
            int unchangedIdx = reportText.IndexOf("## [ = ] Unchanged Files", StringComparison.Ordinal);
            Assert.True(unchangedIdx >= 0);
            string unchangedSection = reportText.Substring(unchangedIdx, reportText.IndexOf("## [", unchangedIdx + 1, StringComparison.Ordinal) - unchangedIdx);
            Assert.Contains("| Status | File Path | Timestamp | Legend | Disassembler |", unchangedSection);
            Assert.Contains("dotnet-ildasm (version: 0.12.0)", unchangedSection);

            // Added Files: 3-column header (no Legend, no Disassembler)
            // Added Files: 3 列ヘッダ（Legend・Disassembler なし）
            int addedIdx = reportText.IndexOf("## [ + ] Added Files", StringComparison.Ordinal);
            Assert.True(addedIdx >= 0);
            string addedSection = reportText.Substring(addedIdx, reportText.IndexOf("## [", addedIdx + 1, StringComparison.Ordinal) - addedIdx);
            Assert.Contains("| Status | File Path | Timestamp |", addedSection);
            Assert.DoesNotContain("Legend", addedSection);
            Assert.DoesNotContain("Disassembler", addedSection);

            // Removed Files: 3-column header (no Legend, no Disassembler)
            // Removed Files: 3 列ヘッダ（Legend・Disassembler なし）
            int removedIdx = reportText.IndexOf("## [ - ] Removed Files", StringComparison.Ordinal);
            Assert.True(removedIdx >= 0);
            int removedEnd = reportText.IndexOf("## [", removedIdx + 1, StringComparison.Ordinal);
            if (removedEnd < 0) removedEnd = reportText.IndexOf("## Summary", StringComparison.Ordinal);
            string removedSection = reportText.Substring(removedIdx, removedEnd - removedIdx);
            Assert.Contains("| Status | File Path | Timestamp |", removedSection);
            Assert.DoesNotContain("Legend", removedSection);
            Assert.DoesNotContain("Disassembler", removedSection);
        }

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

        private static ReportGenerationContext CreateReportContext(
            string oldDir, string newDir, string reportDir,
            ConfigSettings config, ILCache? ilCache = null)
            => new(oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: "00:00:01.000",
                computerName: "test-host", config, ilCache);

        private static ConfigSettingsBuilder CreateConfigBuilder() => new()
        {
            IgnoredExtensions = new List<string>(),
            TextFileExtensions = new List<string>(),
            ShouldIncludeUnchangedFiles = true,
            ShouldIncludeIgnoredFiles = false,
            ShouldOutputFileTimestamps = false,
            ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = true
        };

        private static ConfigSettings CreateConfig() => CreateConfigBuilder().Build();

        private void ClearResultLists()
        {
            _resultLists.ResetAll();
        }

        private sealed class TestLogger : ILoggerService
        {
            public string? LogFileAbsolutePath => null;

            public List<LogEntry> Entries { get; } = new();

            public void Initialize() { }

            public void CleanupOldLogFiles(int maxLogGenerations) { }

            public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, Exception? exception = null)
                => LogMessage(logLevel, message, shouldOutputMessageToConsole, consoleForegroundColor: null, exception);

            public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, ConsoleColor? consoleForegroundColor, Exception? exception = null)
                => Entries.Add(new LogEntry(logLevel, message));
        }

        private sealed record LogEntry(AppLogLevel LogLevel, string Message);
    }
}
