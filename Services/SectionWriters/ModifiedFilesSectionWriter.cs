using System;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    // Modified-files section writer extracted from ReportGenerateService.SectionWriters.cs.
    // ReportGenerateService.SectionWriters.cs から抽出した Modified Files セクションライタ。
    public sealed partial class ReportGenerateService
    {
        /// <summary>Writes the Modified Files section. / Modified Files セクションを書き込みます。</summary>
        private sealed class ModifiedFilesSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                int count = ctx.FileDiffResultLists.ModifiedFilesRelativePath.Count;
                writer.WriteLine($"{REPORT_SECTION_PREFIX}{REPORT_MARKER_MODIFIED} {REPORT_LABEL_MODIFIED}{REPORT_SECTION_FILES_SUFFIX} ({count})");
                writer.WriteLine();
                writer.WriteLine("| Status | File Path | Timestamp | Legend | Disassembler |");
                writer.WriteLine("|:------:|-----------|:---------:|:------:|--------------|");
                var sortedModified = ctx.FileDiffResultLists.ModifiedFilesRelativePath
                    .OrderBy(p => ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(p, out var d) ? GetModifiedSortOrder(d) : 3)
                    .ThenBy(p => GetImportanceSortOrder(ctx.FileDiffResultLists.GetMaxImportance(p)))
                    .ThenBy(p => p, StringComparer.OrdinalIgnoreCase);
                foreach (var fileRelativePath in sortedModified)
                {
                    var diffDetail = ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                    var diffDetailDisplay = BuildDiffDetailDisplay(fileRelativePath, diffDetail, ctx.FileDiffResultLists);
                    var disasmDisplay = BuildDisassemblerDisplay(fileRelativePath, diffDetail, ctx.FileDiffResultLists);
                    string tsCol = "";
                    if (ctx.Config.ShouldOutputFileTimestamps)
                    {
                        string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.OldFolderAbsolutePath, fileRelativePath));
                        string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.NewFolderAbsolutePath, fileRelativePath));
                        tsCol = $"{oldTs}{REPORT_TIMESTAMP_ARROW}{newTs}";
                    }
                    writer.WriteLine($"| `{REPORT_MARKER_MODIFIED}` | {fileRelativePath} | {tsCol} | {diffDetailDisplay} | {disasmDisplay} |");
                }

                // Dependency changes sub-sections for .deps.json files
                // .deps.json ファイルの依存関係変更サブセクション
                if (ctx.Config.ShouldIncludeDependencyChangesInReport)
                {
                    foreach (var fileRelativePath in sortedModified)
                    {
                        if (ctx.FileDiffResultLists.FileRelativePathToDependencyChanges.TryGetValue(fileRelativePath, out var depSummary) && depSummary.HasChanges)
                        {
                            writer.WriteLine();
                            writer.WriteLine($"#### Dependency Changes: {fileRelativePath}");
                            writer.WriteLine();
                            writer.WriteLine("| Package | Status | Importance | Old Version | New Version |");
                            writer.WriteLine("|---------|:------:|:----------:|:-----------:|:-----------:|");
                            foreach (var e in depSummary.EntriesByImportance)
                            {
                                string marker = e.Change switch { "Added" => "[ + ]", "Removed" => "[ - ]", "Updated" => "[ * ]", _ => e.Change };
                                string oldVer = e.OldVersion.Length > 0 ? e.OldVersion : "—";
                                string newVer = e.NewVersion.Length > 0 ? e.NewVersion : "—";
                                writer.WriteLine($"| {e.PackageName} | `{marker}` | `{e.Importance}` | {oldVer} | {newVer} |");
                            }
                        }
                    }
                }
            }
        }
    }
}
