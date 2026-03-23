using System;
using System.Collections.Generic;
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
                writer.WriteLine();

                // Key metadata table / キーメタデータテーブル
                writer.WriteLine("| Property | Value |");
                writer.WriteLine("|----------|-------|");
                writer.WriteLine($"| App Version | FolderDiffIL4DotNet {ctx.AppVersion} |");
                writer.WriteLine($"| Computer | {ctx.ComputerName} |");
                writer.WriteLine($"| Old | {ctx.OldFolderAbsolutePath} |");
                writer.WriteLine($"| New | {ctx.NewFolderAbsolutePath} |");
                if (ctx.Config.ShouldOutputFileTimestamps)
                {
                    writer.WriteLine($"| Timezone | {DateTimeOffset.Now:zzz} |");
                }
                if (!string.IsNullOrWhiteSpace(ctx.ElapsedTimeString))
                {
                    writer.WriteLine($"| Elapsed Time | {ctx.ElapsedTimeString} |");
                }
                writer.WriteLine();
                var inUseText = BuildDisassemblerHeaderText(ctx.FileDiffResultLists);
                WriteDisassemblerAvailabilityTable(writer, ctx.FileDiffResultLists.DisassemblerAvailability, inUseText);

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

                // Notes / ノート
                writer.WriteLine($"> {NOTE_MVID_SKIP}");
                writer.WriteLine();
            }
        }

        /// <summary>Writes the Legend section for diff-detail labels. / 判定根拠ラベルの凡例を書き込みます。</summary>
        private sealed class LegendSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                writer.WriteLine(REPORT_LEGEND_HEADER);
                writer.WriteLine();
                writer.WriteLine("| Label | Description |");
                writer.WriteLine("|-------|-------------|");
                writer.WriteLine($"| `{FileDiffResultLists.DiffDetailResult.SHA256Match}` / `{FileDiffResultLists.DiffDetailResult.SHA256Mismatch}` | SHA256 hash match / mismatch |");
                writer.WriteLine($"| `{FileDiffResultLists.DiffDetailResult.ILMatch}` / `{FileDiffResultLists.DiffDetailResult.ILMismatch}` | IL(Intermediate Language) match / mismatch |");
                writer.WriteLine($"| `{FileDiffResultLists.DiffDetailResult.TextMatch}` / `{FileDiffResultLists.DiffDetailResult.TextMismatch}` | Text match / mismatch |");
                writer.WriteLine();
                writer.WriteLine(REPORT_IMPORTANCE_LEGEND_HEADER);
                writer.WriteLine();
                writer.WriteLine("| Label | Description |");
                writer.WriteLine("|-------|-------------|");
                writer.WriteLine($"| `High` | Breaking change candidate: public/protected API removal, access narrowing, return-type / parameter / member-type change |");
                writer.WriteLine($"| `Medium` | Notable change: public/protected member addition, modifier change, access widening, internal removal |");
                writer.WriteLine($"| `Low` | Low-impact change: body-only modification, internal/private member addition |");
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
                writer.WriteLine();
                writer.WriteLine("| Status | File Path | Timestamp | Legend |");
                writer.WriteLine("|:------:|-----------|:---------:|:------:|");
                foreach (var entry in ctx.FileDiffResultLists.IgnoredFilesRelativePathToLocation.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
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
                    writer.WriteLine($"| `{REPORT_MARKER_IGNORED}` | {displayPath} {locationLabel} | {tsCol} | |");
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
                writer.WriteLine();
                writer.WriteLine("| Status | File Path | Timestamp | Legend | Disassembler |");
                writer.WriteLine("|:------:|-----------|:---------:|:------:|--------------|");
                var sortedUnchanged = ctx.FileDiffResultLists.UnchangedFilesRelativePath
                    .OrderBy(p => ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(p, out var d) ? GetUnchangedSortOrder(d) : 3)
                    .ThenBy(p => p, StringComparer.OrdinalIgnoreCase);
                foreach (var fileRelativePath in sortedUnchanged)
                {
                    var diffDetail = ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                    var diffDetailDisplay = BuildDiffDetailDisplay(fileRelativePath, diffDetail, ctx.FileDiffResultLists);
                    var disasmDisplay = BuildDisassemblerDisplay(fileRelativePath, diffDetail, ctx.FileDiffResultLists);
                    string tsCol = "";
                    if (ctx.Config.ShouldOutputFileTimestamps)
                    {
                        string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.OldFolderAbsolutePath, fileRelativePath));
                        string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.NewFolderAbsolutePath, fileRelativePath));
                        tsCol = oldTs != newTs ? $"{oldTs}{REPORT_TIMESTAMP_ARROW}{newTs}" : newTs;
                    }
                    writer.WriteLine($"| `{REPORT_MARKER_UNCHANGED}` | {fileRelativePath} | {tsCol} | {diffDetailDisplay} | {disasmDisplay} |");
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

        /// <summary>Writes the Removed Files section. / Removed Files セクションを書き込みます。</summary>
        private sealed class RemovedFilesSectionWriter : IReportSectionWriter
        {
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
            }
        }

        /// <summary>Writes the Summary section. / Summary セクションを書き込みます。</summary>
        private sealed class SummarySectionWriter : IReportSectionWriter
        {
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

        /// <summary>Writes the IL Cache Stats section (only when enabled and ilCache is non-null). / IL Cache Stats セクションを書き込みます。</summary>
        private sealed class ILCacheStatsSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                if (!ctx.Config.ShouldIncludeILCacheStatsInReport || ctx.IlCache == null) return;

                var stats = ctx.IlCache.GetReportStats();
                writer.WriteLine(REPORT_SECTION_IL_CACHE_STATS);
                writer.WriteLine();
                writer.WriteLine("| Metric | Value |");
                writer.WriteLine("|--------|------:|");
                writer.WriteLine($"| Hits | {stats.Hits} |");
                writer.WriteLine($"| Misses | {stats.Misses} |");
                writer.WriteLine($"| Hit Rate | {stats.HitRatePct:F1}% |");
                writer.WriteLine($"| Stores | {stats.Stores} |");
                writer.WriteLine($"| Evicted | {stats.Evicted} |");
                writer.WriteLine($"| Expired | {stats.Expired} |");
                writer.WriteLine();
            }
        }

        /// <summary>
        /// Writes the Warnings section. Each warning bullet is immediately followed by its detail table.
        /// 警告セクションを書き込みます。各警告メッセージの直下に対応する詳細テーブルを配置します。
        /// </summary>
        private sealed class WarningsSectionWriter : IReportSectionWriter
        {
            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                if (!ctx.HasSha256Mismatch && !ctx.HasTimestampRegressionWarning) return;

                writer.WriteLine(REPORT_SECTION_WARNINGS);

                // SHA256Mismatch warning + detail table (grouped together)
                // SHA256Mismatch 警告 + 詳細テーブル（まとめて配置）
                if (ctx.HasSha256Mismatch)
                {
                    var sha256Files = ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary
                        .Where(kv => kv.Value == FileDiffResultLists.DiffDetailResult.SHA256Mismatch)
                        .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (sha256Files.Count > 0)
                    {
                        writer.WriteLine($"### [ ! ] {REPORT_LABEL_MODIFIED}{REPORT_SECTION_FILES_SUFFIX} — SHA256Mismatch: hash-only comparison, review recommended ({sha256Files.Count})");
                        writer.WriteLine();
                        writer.WriteLine("| Status | File Path | Timestamp | Legend |");
                        writer.WriteLine("|:------:|-----------|:---------:|:------:|");
                        foreach (var kv in sha256Files)
                        {
                            string tsCol = "";
                            if (ctx.Config.ShouldOutputFileTimestamps)
                            {
                                string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.OldFolderAbsolutePath, kv.Key));
                                string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.NewFolderAbsolutePath, kv.Key));
                                tsCol = $"{oldTs}{REPORT_TIMESTAMP_ARROW}{newTs}";
                            }
                            writer.WriteLine($"| `{REPORT_MARKER_MODIFIED}` | {kv.Key} | {tsCol} | `{kv.Value}` |");
                        }
                    }
                }

                // Timestamp-regressed warning + detail table (grouped together)
                // タイムスタンプ逆行 警告 + 詳細テーブル（まとめて配置）
                if (ctx.HasTimestampRegressionWarning)
                {
                    writer.WriteLine();
                    var tsWarnings = ctx.FileDiffResultLists.NewFileTimestampOlderThanOldWarnings.Values
                        .OrderBy(entry => ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(entry.FileRelativePath, out var d) ? GetModifiedSortOrder(d) : 3)
                        .ThenBy(entry => GetImportanceSortOrder(ctx.FileDiffResultLists.GetMaxImportance(entry.FileRelativePath)))
                        .ThenBy(entry => entry.FileRelativePath, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    writer.WriteLine($"### [ ! ] {REPORT_LABEL_MODIFIED}{REPORT_SECTION_FILES_SUFFIX} — new file timestamps older than old ({tsWarnings.Count})");
                    writer.WriteLine();
                    writer.WriteLine("| Status | File Path | Timestamp | Legend |");
                    writer.WriteLine("|:------:|-----------|:---------:|:------:|");
                    foreach (var warning in tsWarnings)
                    {
                        var fileRelativePath = warning.FileRelativePath;
                        var diffDetail = ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                        var diffDetailDisplay = BuildDiffDetailDisplay(fileRelativePath, diffDetail, ctx.FileDiffResultLists);
                        string tsCol = $"{warning.OldTimestamp}{REPORT_TIMESTAMP_ARROW}{warning.NewTimestamp}";
                        writer.WriteLine($"| `{REPORT_MARKER_MODIFIED}` | {fileRelativePath} | {tsCol} | {diffDetailDisplay} |");
                    }
                }
            }
        }
    }
}
