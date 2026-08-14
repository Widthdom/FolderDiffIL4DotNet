using System;
using System.Globalization;
using System.IO;
using System.Text;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    // Header section writer extracted from ReportGenerateService.SectionWriters.cs.
    // ReportGenerateService.SectionWriters.cs から抽出したヘッダセクションライタ。
    public sealed partial class ReportGenerateService
    {
        /// <summary>Writes the header section (title, run info, IL comparison notes). / レポートのヘッダ部を書き込みます。</summary>
        private sealed class HeaderSectionWriter : IReportSectionWriter
        {
            public int Order => 100;

            public bool IsEnabled(ReportWriteContext context) => true;

            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                writer.WriteLine(REPORT_TITLE);
                writer.WriteLine();

                // Key metadata table / キーメタデータテーブル
                writer.WriteLine("| Property | Value |");
                writer.WriteLine("|----------|-------|");
                writer.WriteLine($"| App Version | {Constants.APP_NAME} {ctx.AppVersion} |");
                writer.WriteLine($"| Computer | {ctx.ComputerName} |");
                if (ctx.Config.ShouldOutputFileTimestamps)
                {
                    writer.WriteLine($"| Timezone | {DateTimeOffset.Now:zzz} |");
                }
                if (!string.IsNullOrWhiteSpace(ctx.ElapsedTimeString))
                {
                    writer.WriteLine($"| Elapsed Time | {ctx.ElapsedTimeString} |");
                }
                writer.WriteLine($"| Old Folder | {ctx.OldFolderAbsolutePath} |");
                writer.WriteLine($"| New Folder | {ctx.NewFolderAbsolutePath} |");
                writer.WriteLine();
                var inUseText = BuildDisassemblerHeaderText(ctx.FileDiffResultLists);
                WriteDisassemblerAvailabilityTable(writer, ctx.FileDiffResultLists.DisassemblerAvailability, inUseText);
                WriteDisassemblerWarnings(writer, ctx.FileDiffResultLists);

                // Configuration details / 設定詳細
                writer.WriteLine("### Configuration Details");
                writer.WriteLine();
                writer.WriteLine("| Setting | Value |");
                writer.WriteLine("|---------|-------|");
                writer.WriteLine($"| Ignored Extensions | {string.Join(REPORT_LIST_SEPARATOR, ctx.Config.IgnoredExtensions)} |");
                writer.WriteLine($"| Text File Extensions | {string.Join(REPORT_LIST_SEPARATOR, ctx.Config.TextFileExtensions)} |");
                writer.WriteLine();
                writer.WriteLine("**Built-in IL Normalization** — Rules apply in the listed order to all IL text, preserving each matching prefix and replacing only its build-variant value.");
                writer.WriteLine();
                writer.WriteLine("| Line Prefix Pattern | Replacement | Observed Output From |");
                writer.WriteLine("|----------------------|-------------|----------------------|");
                foreach (var rule in ILOutputService.BuiltInNormalizationRules)
                {
                    writer.WriteLine($"| `{rule.Prefix}` | `{rule.Replacement.TrimStart()}` | `{rule.Disassembler}` |");
                }
                writer.WriteLine();

                var ilNormalizeStrings = ILConfiguredSubstringHelper.GetEffectiveNormalizationSubstrings(ctx.Config.ILNormalizeContainingStrings);
                if (ctx.Config.ShouldILNormalizeContainingConfiguredStrings)
                {
                    writer.WriteLine("**ILNormalizeContainingStrings** — Matches are replaced in the listed order with a comparison-local marker absent from both inputs; all other text remains comparable.");
                    writer.WriteLine();
                    if (ilNormalizeStrings.Count == 0)
                    {
                        writer.WriteLine("Enabled, but no non-empty strings are configured.");
                    }
                    else
                    {
                        writer.WriteLine("| Substring to Normalize (Escaped) |");
                        writer.WriteLine("|----------------------------------|");
                        foreach (var v in ilNormalizeStrings)
                        {
                            writer.WriteLine($"| {FormatConfiguredSubstringTableCell(v)} |");
                        }
                    }
                    writer.WriteLine();
                }

                if (ctx.Config.ShouldIgnoreILLinesContainingConfiguredStrings)
                {
                    var ilIgnoreStrings = ILConfiguredSubstringHelper.GetEffectiveIgnoreLineSubstrings(ctx.Config.ILIgnoreLineContainingStrings);
                    writer.WriteLine($"**ILIgnoreLineContainingStrings** — When diffing {Constants.LABEL_IL}, lines containing any of the configured strings are ignored:");
                    writer.WriteLine();
                    if (ilIgnoreStrings.Count == 0)
                    {
                        writer.WriteLine("Enabled, but no non-empty strings are configured.");
                    }
                    else
                    {
                        writer.WriteLine("| Substring to Ignore (Escaped) |");
                        writer.WriteLine("|-------------------------------|");
                        foreach (var v in ilIgnoreStrings)
                        {
                            writer.WriteLine($"| {FormatConfiguredSubstringTableCell(v)} |");
                        }
                    }
                    writer.WriteLine();
                }
                // (end of Configuration Details section)
                writer.WriteLine();

            }

            private static string FormatConfiguredSubstringTableCell(string value)
            {
                // Keep CommonMark punctuation inert, make invisible characters explicit, and double literal backslashes.
                // CommonMark 記号を無効化し、不可視文字を明示して、実際の backslash を二重化します。
                var encoded = new StringBuilder(value.Length);
                foreach (Rune rune in value.EnumerateRunes())
                {
                    switch (rune.Value)
                    {
                        // Numeric references survive CommonMark parsing without becoming Markdown escapes.
                        // 数値文字参照は CommonMark 解析後に復号されるため、Markdown のエスケープとして消費されません。
                        case '\\':
                            encoded.Append("&#92;&#92;");
                            break;
                        case '\t':
                            encoded.Append("&#92;t");
                            break;
                        case '\r':
                            encoded.Append("&#92;r");
                            break;
                        case '\n':
                            encoded.Append("&#92;n");
                            break;
                        default:
                            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                            if (Rune.IsWhiteSpace(rune)
                                || category == UnicodeCategory.Control
                                || category == UnicodeCategory.Format)
                            {
                                AppendVisibleUnicodeEscape(encoded, rune.Value);
                            }
                            else if (IsAsciiLetterOrDigit(rune.Value))
                            {
                                encoded.Append((char)rune.Value);
                            }
                            else
                            {
                                encoded.Append("&#")
                                    .Append(rune.Value.ToString(CultureInfo.InvariantCulture))
                                    .Append(';');
                            }
                            break;
                    }
                }

                return encoded.ToString();
            }

            private static bool IsAsciiLetterOrDigit(int value) =>
                (value >= 'A' && value <= 'Z')
                || (value >= 'a' && value <= 'z')
                || (value >= '0' && value <= '9');

            private static void AppendVisibleUnicodeEscape(StringBuilder destination, int value)
            {
                destination.Append(value <= 0xFFFF ? "&#92;u" : "&#92;U")
                    .Append(value.ToString(value <= 0xFFFF ? "X4" : "X8", CultureInfo.InvariantCulture));
            }
        }
    }
}
