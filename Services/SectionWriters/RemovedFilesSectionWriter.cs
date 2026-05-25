using System.IO;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    // Removed-files section writer extracted from ReportGenerateService.SectionWriters.cs.
    // ReportGenerateService.SectionWriters.cs から抽出した Removed Files セクションライタ。
    public sealed partial class ReportGenerateService
    {
        /// <summary>Writes the Removed Files section. / Removed Files セクションを書き込みます。</summary>
        private sealed class RemovedFilesSectionWriter : IReportSectionWriter
        {
            public int Order => 600;

            public bool IsEnabled(ReportWriteContext context) => true;

            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                int count = ctx.FileDiffResultLists.RemovedFilesAbsolutePath.Count;
                writer.WriteLine($"{REPORT_SECTION_PREFIX}{REPORT_MARKER_REMOVED} {REPORT_LABEL_REMOVED}{REPORT_SECTION_FILES_SUFFIX} ({count})");
                writer.WriteLine();
                writer.WriteLine("| Status | File Path | Timestamp |");
                writer.WriteLine("|:------:|-----------|:---------:|");
                foreach (var oldFileAbsolutePath in ctx.FileDiffResultLists.RemovedFilesAbsolutePath)
                {
                    string tsCol = ctx.Config.ShouldOutputFileTimestamps
                        ? Caching.TimestampCache.GetOrAdd(oldFileAbsolutePath) : "";
                    writer.WriteLine($"| `{REPORT_MARKER_REMOVED}` | {oldFileAbsolutePath} | {tsCol} |");
                }
            }
        }
    }
}
