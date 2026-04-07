using System;
using System.IO;
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
                if (ctx.Config.ShouldIgnoreILLinesContainingConfiguredStrings)
                {
                    var ilIgnoreStrings = GetNormalizedIlIgnoreContainingStrings(ctx.Config);
                    if (ilIgnoreStrings.Count == 0)
                    {
                        writer.WriteLine("| IL Line Ignore | Enabled, but no non-empty strings are configured. |");
                    }
                }
                writer.WriteLine();
                if (ctx.Config.ShouldIgnoreILLinesContainingConfiguredStrings)
                {
                    var ilIgnoreStrings = GetNormalizedIlIgnoreContainingStrings(ctx.Config);
                    if (ilIgnoreStrings.Count > 0)
                    {
                        writer.WriteLine($"**IL Ignored Strings** — When diffing {Constants.LABEL_IL}, lines containing any of the configured strings are ignored:");
                        writer.WriteLine();
                        writer.WriteLine("| Ignored String |");
                        writer.WriteLine("|----------------|");
                        foreach (var v in ilIgnoreStrings)
                        {
                            writer.WriteLine($"| \"{v}\" |");
                        }
                        writer.WriteLine();
                    }
                }
                // (end of Configuration Details section)
                writer.WriteLine();

                // Notes — only show MVID note when MVID lines are actually ignored
                // ノート — MVID 行が実際に除外される場合のみ MVID ノートを表示
                if (ctx.Config.ShouldIgnoreMVID)
                {
                    writer.WriteLine($"> {NOTE_MVID_SKIP}");
                    writer.WriteLine();
                }
            }
        }
    }
}
