using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public void GenerateDiffReport_HeaderShowsBuiltInNormalizationRules()
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
            Assert.Contains("**Built-in IL Normalization**", reportText);
            Assert.Contains(
                "Rules apply in the listed order to all IL text, preserving each matching prefix and replacing only its build-variant value.",
                reportText);
            Assert.Contains("| Line Prefix Pattern | Replacement | Observed Output From |", reportText);
            Assert.DoesNotContain("All listed rules are applied to every IL text", reportText);
            Assert.DoesNotContain("For ilspycmd's multiline TypeLibraryTimeStampAttribute", reportText);
            Assert.DoesNotContain("Disassembler (Observed In)", reportText);
            Assert.DoesNotContain("it does not limit where the rule is applied", reportText);
            Assert.Contains("`// MVID:` | `<nildiff:normalized:mvid>` | `dotnet-ildasm`", reportText);
            Assert.Contains("`// Method begins at RVA 0x` | `<nildiff:normalized:rva>` | `ilspycmd`", reportText);
            Assert.Contains("`// Code size: ` | `<nildiff:normalized:code-size>` | `ilspycmd`", reportText);
            Assert.Contains($"`{Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX}` | `<nildiff:normalized:type-library-timestamp>` | `ilspycmd`", reportText);
            var expectedRuleOrder = ILOutputService.BuiltInNormalizationRules
                .OrderBy(rule => rule.Prefix, StringComparer.Ordinal)
                .ThenBy(rule => rule.Disassembler, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(expectedRuleOrder, ILOutputService.BuiltInNormalizationRules);
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
            Assert.Contains("| Substring to Ignore (Escaped) |", reportText);
            Assert.Contains("| buildserver |", reportText);
            Assert.Contains("| buildPath |", reportText);
            Assert.DoesNotContain("| \"buildserver\" |", reportText);
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
        public void GenerateDiffReport_HeaderShowsIlContainsIgnoreNote_WhenEnabledButNoValidStrings()
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
            Assert.Contains("ILIgnoreLineContainingStrings", reportText);
            Assert.Contains("Enabled, but no non-empty strings are configured.", reportText);
            Assert.DoesNotContain("Substring to Ignore", reportText);
        }

        [Fact]
        public void GenerateDiffReport_HeaderShowsNormalizationThenConfiguredNormalizationThenIgnoreStrings()
        {
            var oldDir = Path.Combine(_rootDir, "old-normalize-note");
            var newDir = Path.Combine(_rootDir, "new-normalize-note");
            var reportDir = Path.Combine(_rootDir, "report-normalize-note");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var builder = CreateConfigBuilder();
            builder.ShouldIgnoreILLinesContainingConfiguredStrings = true;
            builder.ILIgnoreLineContainingStrings = new List<string> { "ignored-value" };
            builder.ShouldILNormalizeContainingConfiguredStrings = true;
            builder.ILNormalizeContainingStrings = new List<string> { "buildserver2_", "", "buildserver1_", "buildserver2_" };

            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, builder.Build()));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            int builtInIndex = reportText.IndexOf("**Built-in IL Normalization**", StringComparison.Ordinal);
            int ignoreIndex = reportText.IndexOf("**ILIgnoreLineContainingStrings**", StringComparison.Ordinal);
            int normalizeIndex = reportText.IndexOf("**ILNormalizeContainingStrings**", StringComparison.Ordinal);
            Assert.True(builtInIndex >= 0);
            Assert.True(normalizeIndex > builtInIndex);
            Assert.True(ignoreIndex > normalizeIndex);
            Assert.Contains(
                "Matches are replaced in the listed order with a comparison-local marker absent from both inputs; all other text remains comparable.",
                reportText);
            Assert.DoesNotContain("Every occurrence of each configured substring", reportText);
            Assert.DoesNotContain("comparison-local collision-free marker", reportText);
            Assert.DoesNotContain("<nildiff:normalized:configured-value>", reportText);
            Assert.DoesNotContain("all remaining text on the line stays unchanged and comparable", reportText);
            Assert.DoesNotContain("The substrings are listed in application order.", reportText);
            Assert.Equal(1, reportText.Split("| buildserver1&#95; |", StringSplitOptions.None).Length - 1);
            Assert.Equal(1, reportText.Split("| buildserver2&#95; |", StringSplitOptions.None).Length - 1);
            Assert.True(
                reportText.IndexOf("| buildserver2&#95; |", StringComparison.Ordinal)
                < reportText.IndexOf("| buildserver1&#95; |", StringComparison.Ordinal));
            Assert.DoesNotContain("| \"buildserver1_\" |", reportText);
        }

        [Fact]
        public void GenerateDiffReport_HeaderShowsNormalizeStrings_WhenEnabledButNoValidValues()
        {
            var oldDir = Path.Combine(_rootDir, "old-normalize-empty");
            var newDir = Path.Combine(_rootDir, "new-normalize-empty");
            var reportDir = Path.Combine(_rootDir, "report-normalize-empty");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var builder = CreateConfigBuilder();
            builder.ShouldILNormalizeContainingConfiguredStrings = true;
            builder.ILNormalizeContainingStrings = new List<string> { "", "   " };

            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, builder.Build()));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("ILNormalizeContainingStrings", reportText);
            Assert.Contains("Enabled, but no non-empty strings are configured.", reportText);
            Assert.DoesNotContain("Substring to Normalize", reportText);
        }

        [Fact]
        public void GenerateDiffReport_HeaderUsesReversibleEscapesForConfiguredStringsInMarkdownTables()
        {
            var oldDir = Path.Combine(_rootDir, "old-configured-markdown");
            var newDir = Path.Combine(_rootDir, "new-configured-markdown");
            var reportDir = Path.Combine(_rootDir, "report-configured-markdown");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var builder = CreateConfigBuilder();
            builder.ShouldILNormalizeContainingConfiguredStrings = true;
            builder.ILNormalizeContainingStrings = new List<string>
            {
                "normalize|value",
                "line1\r\nline2",
                "  padded  ",
                "\ttabbed\t",
                "inner\tspace value\nnext\u00A0",
                @"\temp\develop\",
                @"\\temp\\develop\\",
                "`code` *emphasis* _underscore_ ~~strike~~",
                "[link](https://example.com)",
                "![image](https://example.com/image.png)",
                "format\u200Dcontrol\0",
                "emoji\U0001F600value"
            };
            builder.ShouldIgnoreILLinesContainingConfiguredStrings = true;
            builder.ILIgnoreLineContainingStrings = new List<string> { "ignore|value" };

            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, builder.Build()));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.Contains("| Substring to Normalize (Escaped) |", reportText);
            Assert.Contains("| normalize&#124;value |", reportText);
            Assert.Contains("| line1&#92;r&#92;nline2 |", reportText);
            Assert.Contains("| &#92;u0020&#92;u0020padded&#92;u0020&#92;u0020 |", reportText);
            Assert.Contains("| &#92;ttabbed&#92;t |", reportText);
            Assert.Contains("| inner&#92;tspace&#92;u0020value&#92;nnext&#92;u00A0 |", reportText);
            Assert.Contains("| &#92;&#92;temp&#92;&#92;develop&#92;&#92; |", reportText);
            Assert.Contains("| &#92;&#92;&#92;&#92;temp&#92;&#92;&#92;&#92;develop&#92;&#92;&#92;&#92; |", reportText);
            Assert.Contains("| &#96;code&#96;&#92;u0020&#42;emphasis&#42;&#92;u0020&#95;underscore&#95;&#92;u0020&#126;&#126;strike&#126;&#126; |", reportText);
            Assert.Contains("| &#91;link&#93;&#40;https&#58;&#47;&#47;example&#46;com&#41; |", reportText);
            Assert.Contains("| &#33;&#91;image&#93;&#40;https&#58;&#47;&#47;example&#46;com&#47;image&#46;png&#41; |", reportText);
            Assert.Contains("| format&#92;u200Dcontrol&#92;u0000 |", reportText);
            Assert.Contains("| emoji&#128512;value |", reportText);
            Assert.Contains("| ignore&#124;value |", reportText);
            Assert.DoesNotContain("<br>", reportText);
            Assert.DoesNotContain("&#32;", reportText);
            Assert.DoesNotContain("*emphasis*", reportText);
            Assert.DoesNotContain("[link](https://example.com)", reportText);
            Assert.DoesNotContain("![image](https://example.com/image.png)", reportText);
        }

        [Fact]
        public void GenerateDiffReport_HeaderOmitsNormalizeStrings_WhenDisabledWithValues()
        {
            var oldDir = Path.Combine(_rootDir, "old-normalize-disabled");
            var newDir = Path.Combine(_rootDir, "new-normalize-disabled");
            var reportDir = Path.Combine(_rootDir, "report-normalize-disabled");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var builder = CreateConfigBuilder();
            builder.ShouldILNormalizeContainingConfiguredStrings = false;
            builder.ILNormalizeContainingStrings = new List<string> { "buildserver1_" };

            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, builder.Build()));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.DoesNotContain("ILNormalizeContainingStrings", reportText);
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
