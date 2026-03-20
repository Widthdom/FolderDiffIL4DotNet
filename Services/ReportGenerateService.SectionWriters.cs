using System;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    // Nested section writer implementations for the Markdown diff report.
    // Markdown 差分レポート用のネストされたセクションライタ実装群。
    public sealed partial class ReportGenerateService
    {
        /// <summary>Writes the header section (title, run info, IL comparison notes). / レポートのヘッダ部を書き込みます。</summary>
        private sealed class HeaderSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                writer.WriteLine(REPORT_TITLE);
                writer.WriteLine($"- App Version: FolderDiffIL4DotNet {ctx.AppVersion}");
                writer.WriteLine($"- Computer: {ctx.ComputerName}");
                writer.WriteLine($"- Old: {ctx.OldFolderAbsolutePath}");
                writer.WriteLine($"- New: {ctx.NewFolderAbsolutePath}");
                writer.WriteLine($"- Ignored Extensions: {string.Join(REPORT_LIST_SEPARATOR, ctx.Config.IgnoredExtensions)}");
                writer.WriteLine($"- Text File Extensions: {string.Join(REPORT_LIST_SEPARATOR, ctx.Config.TextFileExtensions)}");
                writer.WriteLine($"- IL Disassembler: {BuildDisassemblerHeaderText(ctx.FileDiffResultLists)}");
                if (!string.IsNullOrWhiteSpace(ctx.ElapsedTimeString))
                {
                    writer.WriteLine($"- Elapsed Time: {ctx.ElapsedTimeString}");
                }
                if (ctx.Config.ShouldOutputFileTimestamps)
                {
                    writer.WriteLine($"- Timestamps (timezone): {DateTimeOffset.Now:zzz}");
                }
                writer.WriteLine("- " + NOTE_MVID_SKIP);
                if (!ctx.Config.ShouldIgnoreILLinesContainingConfiguredStrings) return;

                var ilIgnoreStrings = GetNormalizedIlIgnoreContainingStrings(ctx.Config);
                writer.WriteLine("- " + (ilIgnoreStrings.Count == 0
                    ? NOTE_IL_CONTAINS_SKIP_ENABLED_BUT_EMPTY
                    : $"Note: When diffing {Constants.LABEL_IL}, lines containing any of the configured strings are ignored: {string.Join(REPORT_LIST_SEPARATOR, ilIgnoreStrings.Select(v => $"\"{v}\""))}."));
            }
        }

        /// <summary>Writes the Legend section for diff-detail labels. / 判定根拠ラベルの凡例を書き込みます。</summary>
        private sealed class LegendSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                writer.WriteLine(REPORT_LEGEND_HEADER);
                writer.WriteLine($"  - `{FileDiffResultLists.DiffDetailResult.MD5Match}` / `{FileDiffResultLists.DiffDetailResult.MD5Mismatch}`: MD5 hash match / mismatch");
                writer.WriteLine($"  - `{FileDiffResultLists.DiffDetailResult.ILMatch}` / `{FileDiffResultLists.DiffDetailResult.ILMismatch}`: IL(Intermediate Language) match / mismatch");
                writer.WriteLine($"  - `{FileDiffResultLists.DiffDetailResult.TextMatch}` / `{FileDiffResultLists.DiffDetailResult.TextMismatch}`: Text match / mismatch");
            }
        }

        /// <summary>Writes the Ignored Files section. / Ignored Files セクションを書き込みます。</summary>
        private sealed class IgnoredFilesSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                if (!ctx.Config.ShouldIncludeIgnoredFiles || ctx.FileDiffResultLists.IgnoredFilesRelativePathToLocation.Count == 0) return;

                int count = ctx.FileDiffResultLists.IgnoredFilesRelativePathToLocation.Count;
                writer.WriteLine($"{REPORT_SECTION_PREFIX}{REPORT_MARKER_IGNORED} {REPORT_LABEL_IGNORED}{REPORT_SECTION_FILES_SUFFIX} ({count})");
                foreach (var entry in ctx.FileDiffResultLists.IgnoredFilesRelativePathToLocation.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    bool hasOld = (entry.Value & FileDiffResultLists.IgnoredFileLocation.Old) != 0;
                    bool hasNew = (entry.Value & FileDiffResultLists.IgnoredFileLocation.New) != 0;
                    string displayPath = (hasOld && hasNew)
                        ? entry.Key
                        : hasOld
                            ? Path.Combine(ctx.OldFolderAbsolutePath, entry.Key)
                            : Path.Combine(ctx.NewFolderAbsolutePath, entry.Key);

                    var line = $"- [ x ] {displayPath}";
                    var locationLabel = GetIgnoredFileLocationLabel(entry.Value);
                    if (!string.IsNullOrEmpty(locationLabel)) line += " " + locationLabel;

                    if (ctx.Config.ShouldOutputFileTimestamps)
                    {
                        var timestampInfo = BuildIgnoredFileTimestampInfo(entry, ctx.OldFolderAbsolutePath, ctx.NewFolderAbsolutePath);
                        if (!string.IsNullOrEmpty(timestampInfo)) line += $" {timestampInfo}";
                    }
                    writer.WriteLine(line);
                }
            }
        }

        /// <summary>Writes the Unchanged Files section. / Unchanged Files セクションを書き込みます。</summary>
        private sealed class UnchangedFilesSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                if (!ctx.Config.ShouldIncludeUnchangedFiles) return;

                int count = ctx.FileDiffResultLists.UnchangedFilesRelativePath.Count;
                writer.WriteLine($"{REPORT_SECTION_PREFIX}{REPORT_MARKER_UNCHANGED} {REPORT_LABEL_UNCHANGED}{REPORT_SECTION_FILES_SUFFIX} ({count})");
                foreach (var fileRelativePath in ctx.FileDiffResultLists.UnchangedFilesRelativePath)
                {
                    var diffDetail = ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                    var diffDetailDisplay = BuildDiffDetailDisplay(fileRelativePath, diffDetail, ctx.FileDiffResultLists);
                    if (ctx.Config.ShouldOutputFileTimestamps)
                    {
                        string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.OldFolderAbsolutePath, fileRelativePath));
                        string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.NewFolderAbsolutePath, fileRelativePath));
                        string updateInfo = oldTs != newTs ? $"[{oldTs}{REPORT_TIMESTAMP_ARROW}{newTs}]" : $"[{newTs}]";
                        writer.WriteLine($"- [ = ] {fileRelativePath} {updateInfo} {diffDetailDisplay}");
                    }
                    else
                    {
                        writer.WriteLine($"- [ = ] {fileRelativePath} {diffDetailDisplay}");
                    }
                }
            }
        }

        /// <summary>Writes the Added Files section. / Added Files セクションを書き込みます。</summary>
        private sealed class AddedFilesSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                int count = ctx.FileDiffResultLists.AddedFilesAbsolutePath.Count;
                writer.WriteLine($"{REPORT_SECTION_PREFIX}{REPORT_MARKER_ADDED} {REPORT_LABEL_ADDED}{REPORT_SECTION_FILES_SUFFIX} ({count})");
                foreach (var newFileAbsolutePath in ctx.FileDiffResultLists.AddedFilesAbsolutePath)
                {
                    writer.WriteLine(ctx.Config.ShouldOutputFileTimestamps
                        ? $"- [ + ] {newFileAbsolutePath} [{Caching.TimestampCache.GetOrAdd(newFileAbsolutePath)}]"
                        : $"- [ + ] {newFileAbsolutePath}");
                }
            }
        }

        /// <summary>Writes the Removed Files section. / Removed Files セクションを書き込みます。</summary>
        private sealed class RemovedFilesSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                int count = ctx.FileDiffResultLists.RemovedFilesAbsolutePath.Count;
                writer.WriteLine($"{REPORT_SECTION_PREFIX}{REPORT_MARKER_REMOVED} {REPORT_LABEL_REMOVED}{REPORT_SECTION_FILES_SUFFIX} ({count})");
                foreach (var oldFileAbsolutePath in ctx.FileDiffResultLists.RemovedFilesAbsolutePath)
                {
                    writer.WriteLine(ctx.Config.ShouldOutputFileTimestamps
                        ? $"- [ - ] {oldFileAbsolutePath} [{Caching.TimestampCache.GetOrAdd(oldFileAbsolutePath)}]"
                        : $"- [ - ] {oldFileAbsolutePath}");
                }
            }
        }

        /// <summary>Writes the Modified Files section. / Modified Files セクションを書き込みます。</summary>
        private sealed class ModifiedFilesSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                int count = ctx.FileDiffResultLists.ModifiedFilesRelativePath.Count;
                writer.WriteLine($"{REPORT_SECTION_PREFIX}{REPORT_MARKER_MODIFIED} {REPORT_LABEL_MODIFIED}{REPORT_SECTION_FILES_SUFFIX} ({count})");
                foreach (var fileRelativePath in ctx.FileDiffResultLists.ModifiedFilesRelativePath)
                {
                    var diffDetail = ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                    var diffDetailDisplay = BuildDiffDetailDisplay(fileRelativePath, diffDetail, ctx.FileDiffResultLists);
                    if (ctx.Config.ShouldOutputFileTimestamps)
                    {
                        string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.OldFolderAbsolutePath, fileRelativePath));
                        string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.NewFolderAbsolutePath, fileRelativePath));
                        writer.WriteLine($"- [ * ] {fileRelativePath} [{oldTs}{REPORT_TIMESTAMP_ARROW}{newTs}] {diffDetailDisplay}");
                    }
                    else
                    {
                        writer.WriteLine($"- [ * ] {fileRelativePath} {diffDetailDisplay}");
                    }
                }
            }
        }

        /// <summary>Writes the Summary section. / Summary セクションを書き込みます。</summary>
        private sealed class SummarySectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                writer.WriteLine(REPORT_SECTION_SUMMARY);
                var stats = ctx.FileDiffResultLists.SummaryStatistics;
                if (ctx.Config.ShouldIncludeIgnoredFiles)
                {
                    writer.WriteLine($"- {REPORT_LABEL_IGNORED,-10}: {stats.IgnoredCount}");
                }
                writer.WriteLine($"- {REPORT_LABEL_UNCHANGED,-10}: {stats.UnchangedCount}");
                writer.WriteLine($"- {REPORT_LABEL_ADDED,-10}: {stats.AddedCount}");
                writer.WriteLine($"- {REPORT_LABEL_REMOVED,-10}: {stats.RemovedCount}");
                writer.WriteLine($"- {REPORT_LABEL_MODIFIED,-10}: {stats.ModifiedCount}");
                writer.WriteLine($"- {REPORT_LABEL_COMPARED,-10}: {ctx.FileDiffResultLists.OldFilesAbsolutePath.Count} (Old) vs {ctx.FileDiffResultLists.NewFilesAbsolutePath.Count} (New)");
                writer.WriteLine();
            }
        }

        /// <summary>Writes the Method-Level Changes section for ILMismatch assemblies. / ILMismatch アセンブリのメソッドレベル変更セクションを書き込みます。</summary>
        private sealed class MethodLevelChangesSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                if (!ctx.Config.ShouldIncludeMethodLevelChangesInReport) return;
                var changes = ctx.FileDiffResultLists.FileRelativePathToMethodLevelChanges;
                if (changes.IsEmpty) return;

                writer.WriteLine(REPORT_SECTION_METHOD_LEVEL_CHANGES);

                foreach (var (filePath, summary) in changes.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                {
                    writer.WriteLine($"\n### {filePath}");

                    if (summary.Entries.Count > 0)
                    {
                        writer.WriteLine();
                        writer.WriteLine("| Assembly | Change | Class | Access | Kind | Name | Details |");
                        writer.WriteLine("|----------|--------|-------|--------|------|------|---------|");
                        foreach (var e in summary.Entries)
                        {
                            writer.WriteLine($"| {EscapeMdTable(filePath)} | `{e.Change}` | {EscapeMdTable(e.TypeName)} | {EscapeMdTable(e.Access)} | {e.MemberKind} | {EscapeMdTable(e.MemberName)} | {EscapeMdTable(e.Details)} |");
                        }
                    }
                    else
                    {
                        writer.WriteLine("- Other changes only. See IL diff for details.");
                    }

                    writer.WriteLine($"- Method count: {summary.OldMethodCount} (Old) vs {summary.NewMethodCount} (New)");
                }

                writer.WriteLine();
            }

            /// <summary>Escape pipe characters for Markdown table cells. / Markdown テーブルセル用にパイプ文字をエスケープ。</summary>
            private static string EscapeMdTable(string value) => value.Replace("|", "\\|");
        }

        /// <summary>Writes the IL Cache Stats section (only when enabled and ilCache is non-null). / IL Cache Stats セクションを書き込みます。</summary>
        private sealed class ILCacheStatsSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                if (!ctx.Config.ShouldIncludeILCacheStatsInReport || ctx.IlCache == null) return;

                var stats = ctx.IlCache.GetReportStats();
                writer.WriteLine(REPORT_SECTION_IL_CACHE_STATS);
                writer.WriteLine($"- Hits    : {stats.Hits}");
                writer.WriteLine($"- Misses  : {stats.Misses}");
                writer.WriteLine($"- Hit Rate: {stats.HitRatePct:F1}%");
                writer.WriteLine($"- Stores  : {stats.Stores}");
                writer.WriteLine($"- Evicted : {stats.Evicted}");
                writer.WriteLine($"- Expired : {stats.Expired}");
                writer.WriteLine();
            }
        }

        /// <summary>Writes the Warnings section. / 警告セクションを書き込みます。</summary>
        private sealed class WarningsSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                if (!ctx.HasMd5Mismatch && !ctx.HasTimestampRegressionWarning) return;

                writer.WriteLine(REPORT_SECTION_WARNINGS);
                if (ctx.HasMd5Mismatch)
                {
                    writer.WriteLine($"- **WARNING:** {Constants.WARNING_MD5_MISMATCH}");
                }
                if (!ctx.HasTimestampRegressionWarning) return;

                writer.WriteLine($"- **WARNING:** {WARNING_NEW_FILE_TIMESTAMP_OLDER_THAN_OLD}");
                foreach (var warning in ctx.FileDiffResultLists.NewFileTimestampOlderThanOldWarnings.Values
                    .OrderBy(entry => entry.FileRelativePath, StringComparer.OrdinalIgnoreCase))
                {
                    writer.WriteLine($"  - {warning.FileRelativePath} [{warning.OldTimestamp}{REPORT_TIMESTAMP_ARROW}{warning.NewTimestamp}]");
                }
            }
        }
    }
}
