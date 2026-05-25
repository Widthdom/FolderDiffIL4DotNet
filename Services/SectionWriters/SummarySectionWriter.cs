using System.IO;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    // Summary section writer extracted from ReportGenerateService.SectionWriters.cs.
    // ReportGenerateService.SectionWriters.cs から抽出した Summary セクションライタ。
    public sealed partial class ReportGenerateService
    {
        /// <summary>Writes the Summary section. / Summary セクションを書き込みます。</summary>
        private sealed class SummarySectionWriter : IReportSectionWriter
        {
            public int Order => 800;

            public bool IsEnabled(ReportWriteContext context) => true;

            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                writer.WriteLine(REPORT_SECTION_SUMMARY);
                writer.WriteLine();
                writer.WriteLine("| Category | Count |");
                writer.WriteLine("|----------|------:|");
                var stats = ctx.FileDiffResultLists.SummaryStatistics;
                if (ctx.Config.ShouldIncludeIgnoredFiles)
                {
                    writer.WriteLine($"| {REPORT_LABEL_IGNORED} | {stats.IgnoredCount} |");
                }
                writer.WriteLine($"| {REPORT_LABEL_UNCHANGED} | {stats.UnchangedCount} |");
                writer.WriteLine($"| {REPORT_LABEL_ADDED} | {stats.AddedCount} |");
                writer.WriteLine($"| {REPORT_LABEL_REMOVED} | {stats.RemovedCount} |");
                writer.WriteLine($"| {REPORT_LABEL_MODIFIED} | {stats.ModifiedCount} |");
                writer.WriteLine($"| {REPORT_LABEL_COMPARED} | {ctx.FileDiffResultLists.OldFilesAbsolutePath.Count} (Old) vs {ctx.FileDiffResultLists.NewFilesAbsolutePath.Count} (New) |");
                writer.WriteLine();
            }
        }
    }
}
