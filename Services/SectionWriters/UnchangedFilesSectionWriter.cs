using System;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    // Unchanged-files section writer extracted from ReportGenerateService.SectionWriters.cs.
    // ReportGenerateService.SectionWriters.cs から抽出した Unchanged Files セクションライタ。
    public sealed partial class ReportGenerateService
    {
        /// <summary>Writes the Unchanged Files section. / Unchanged Files セクションを書き込みます。</summary>
        private sealed class UnchangedFilesSectionWriter : IReportSectionWriter
        {
            public int Order => 400;

            public bool IsEnabled(ReportWriteContext context) => context.Config.ShouldIncludeUnchangedFiles;

            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                int count = ctx.FileDiffResultLists.UnchangedFilesRelativePath.Count;
                writer.WriteLine($"{REPORT_SECTION_PREFIX}{REPORT_MARKER_UNCHANGED} {REPORT_LABEL_UNCHANGED}{REPORT_SECTION_FILES_SUFFIX} ({count})");
                writer.WriteLine();
                writer.WriteLine("| Status | File Path | Timestamp | Diff Reason | Disassembler | .NET SDK |");
                writer.WriteLine("|:------:|-----------|:---------:|:-----------:|--------------|:--------:|");
                var sortedUnchanged = ctx.FileDiffResultLists.UnchangedFilesRelativePath
                    .OrderBy(p => ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(p, out var d) ? GetUnchangedSortOrder(d) : 3)
                    .ThenBy(p => p, StringComparer.OrdinalIgnoreCase);
                foreach (var fileRelativePath in sortedUnchanged)
                {
                    var diffDetail = ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                    var diffDetailDisplay = BuildDiffDetailDisplay(fileRelativePath, diffDetail, ctx.FileDiffResultLists);
                    var disasmDisplay = BuildDisassemblerDisplay(fileRelativePath, diffDetail, ctx.FileDiffResultLists);
                    var sdkDisplay = BuildSdkVersionDisplay(fileRelativePath, ctx.FileDiffResultLists);
                    string tsCol = "";
                    if (ctx.Config.ShouldOutputFileTimestamps)
                    {
                        string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.OldFolderAbsolutePath, fileRelativePath));
                        string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.NewFolderAbsolutePath, fileRelativePath));
                        tsCol = oldTs != newTs ? $"{oldTs}{REPORT_TIMESTAMP_ARROW}{newTs}" : newTs;
                    }
                    writer.WriteLine($"| `{REPORT_MARKER_UNCHANGED}` | {fileRelativePath} | {tsCol} | {diffDetailDisplay} | {disasmDisplay} | {sdkDisplay} |");
                }
            }
        }
    }
}
