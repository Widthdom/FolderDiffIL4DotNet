using System.IO;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    // Added-files section writer extracted from ReportGenerateService.SectionWriters.cs.
    // ReportGenerateService.SectionWriters.cs から抽出した Added Files セクションライタ。
    public sealed partial class ReportGenerateService
    {
        /// <summary>Writes the Added Files section. / Added Files セクションを書き込みます。</summary>
        private sealed class AddedFilesSectionWriter : IReportSectionWriter
        {
            public int Order => 500;

            public bool IsEnabled(ReportWriteContext context) => true;

            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                int count = ctx.FileDiffResultLists.AddedFilesAbsolutePath.Count;
                writer.WriteLine($"{REPORT_SECTION_PREFIX}{REPORT_MARKER_ADDED} {REPORT_LABEL_ADDED}{REPORT_SECTION_FILES_SUFFIX} ({count})");
                writer.WriteLine();
                writer.WriteLine("| Status | File Path | Timestamp |");
                writer.WriteLine("|:------:|-----------|:---------:|");
                foreach (var newFileAbsolutePath in ctx.FileDiffResultLists.AddedFilesAbsolutePath)
                {
                    string tsCol = ctx.Config.ShouldOutputFileTimestamps
                        ? Caching.TimestampCache.GetOrAdd(newFileAbsolutePath) : "";
                    writer.WriteLine($"| `{REPORT_MARKER_ADDED}` | {newFileAbsolutePath} | {tsCol} |");
                }
            }
        }
    }
}
