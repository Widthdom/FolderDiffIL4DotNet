using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    // Ignored-files section writer extracted from ReportGenerateService.SectionWriters.cs.
    // ReportGenerateService.SectionWriters.cs から抽出した Ignored Files セクションライタ。
    public sealed partial class ReportGenerateService
    {
        /// <summary>Writes the Ignored Files section. / Ignored Files セクションを書き込みます。</summary>
        private sealed class IgnoredFilesSectionWriter : IReportSectionWriter
        {
            public int Order => 300;

            public bool IsEnabled(ReportWriteContext context) => context.Config.ShouldIncludeIgnoredFiles;

            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                if (ctx.FileDiffResultLists.IgnoredFilesRelativePathToLocation.Count == 0) return;

                int count = ctx.FileDiffResultLists.IgnoredFilesRelativePathToLocation.Count;
                writer.WriteLine($"{REPORT_SECTION_PREFIX}{REPORT_MARKER_IGNORED} {REPORT_LABEL_IGNORED}{REPORT_SECTION_FILES_SUFFIX} ({count})");
                writer.WriteLine();
                writer.WriteLine("| Status | File Path | Timestamp |");
                writer.WriteLine("|:------:|-----------|:---------:|");
                foreach (var entry in ctx.FileDiffResultLists.IgnoredFilesRelativePathToLocation.OrderBy(kvp => kvp.Key, System.StringComparer.OrdinalIgnoreCase))
                {
                    bool hasOld = (entry.Value & FileDiffResultLists.IgnoredFileLocation.Old) != 0;
                    bool hasNew = (entry.Value & FileDiffResultLists.IgnoredFileLocation.New) != 0;
                    string displayPath = (hasOld && hasNew)
                        ? entry.Key
                        : hasOld
                            ? Path.Combine(ctx.OldFolderAbsolutePath, entry.Key)
                            : Path.Combine(ctx.NewFolderAbsolutePath, entry.Key);

                    var locationLabel = GetIgnoredFileLocationLabel(entry.Value);
                    string tsCol = "";
                    if (ctx.Config.ShouldOutputFileTimestamps)
                    {
                        var timestampInfo = BuildIgnoredFileTimestampInfo(entry, ctx.OldFolderAbsolutePath, ctx.NewFolderAbsolutePath);
                        if (!string.IsNullOrEmpty(timestampInfo)) tsCol = timestampInfo;
                    }
                    writer.WriteLine($"| `{REPORT_MARKER_IGNORED}` | {displayPath} {locationLabel} | {tsCol} |");
                }
            }
        }
    }
}
