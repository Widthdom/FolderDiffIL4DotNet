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
    /// Tests for <see cref="ReportGenerateService"/> — header output (disassembler display, availability, MVID note, IL-contains-ignore).
    /// <see cref="ReportGenerateService"/> のテスト — ヘッダー出力（逆アセンブラ表示、利用可否、MVID 注記、IL含有無視）。
    /// </summary>
    public sealed partial class ReportGenerateServiceTests
    {
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
            Assert.Contains("| ilspycmd | No | N/A | No |", reportText);
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
            Assert.Contains("Enabled, but no non-empty strings are configured.", reportText);
        }

        // -----------------------------------------------------------------------
        // Disassembler warning banners
        // 逆アセンブラ警告バナー
        // -----------------------------------------------------------------------

        [Fact]
        public void GenerateDiffReport_WarnsWhenNoDisassemblerAvailable()
        {
            _resultLists.DisassemblerAvailability = new List<DisassemblerProbeResult>
            {
                new("dotnet-ildasm", false, null, null),
                new("ilspycmd", false, null, null),
            };

            var oldDir = Path.Combine(_rootDir, "old-no-disasm");
            var newDir = Path.Combine(_rootDir, "new-no-disasm");
            var reportDir = Path.Combine(_rootDir, "report-no-disasm");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, CreateConfig()));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("No disassembler tool is available", reportText);
            Assert.Contains("Install", reportText);
        }

        [Fact]
        public void GenerateDiffReport_NoDisassemblerWarning_WhenOneIsAvailable()
        {
            _resultLists.DisassemblerAvailability = new List<DisassemblerProbeResult>
            {
                new("dotnet-ildasm", true, "0.12.0", "/usr/bin/dotnet-ildasm"),
                new("ilspycmd", false, null, null),
            };

            var oldDir = Path.Combine(_rootDir, "old-one-avail");
            var newDir = Path.Combine(_rootDir, "new-one-avail");
            var reportDir = Path.Combine(_rootDir, "report-one-avail");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, CreateConfig()));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.DoesNotContain("No disassembler tool is available", reportText);
        }

        [Fact]
        public void GenerateDiffReport_WarnsWhenMultipleDisassemblersUsed()
        {
            _resultLists.DisassemblerToolVersions["dotnet-ildasm (version: 0.12.0)"] = 0;
            _resultLists.DisassemblerToolVersions["ilspycmd (version: 8.2.0)"] = 0;

            var oldDir = Path.Combine(_rootDir, "old-mixed");
            var newDir = Path.Combine(_rootDir, "new-mixed");
            var reportDir = Path.Combine(_rootDir, "report-mixed");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, CreateConfig()));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("Multiple disassembler tools were used", reportText);
            // Version info must be included in the warning / 警告にバージョン情報が含まれること
            Assert.Contains("dotnet-ildasm (version: 0.12.0)", reportText);
            Assert.Contains("ilspycmd (version: 8.2.0)", reportText);
            Assert.Contains("--clear-cache", reportText);
        }

        [Fact]
        public void GenerateDiffReport_NoMixedWarning_WhenSingleDisassemblerUsed()
        {
            _resultLists.DisassemblerToolVersions["dotnet-ildasm (version: 0.12.0)"] = 0;

            var oldDir = Path.Combine(_rootDir, "old-single");
            var newDir = Path.Combine(_rootDir, "new-single");
            var reportDir = Path.Combine(_rootDir, "report-single");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, CreateConfig()));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.DoesNotContain("Multiple disassembler tools were used", reportText);
        }
    }
}

