using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Console;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Generates a Markdown diff report (<see cref="DIFF_REPORT_FILE_NAME"/>) summarising folder comparison results.
    /// 差分結果の Markdown レポート (<see cref="DIFF_REPORT_FILE_NAME"/>) を生成するサービス。
    /// </summary>
    public sealed class ReportGenerateService
    {
        private readonly FileDiffResultLists _fileDiffResultLists;
        private readonly ILoggerService _logger;
        private readonly string[] _spinnerFrames;

        public ReportGenerateService(FileDiffResultLists fileDiffResultLists, ILoggerService logger, ConfigSettings config)
        {
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            _fileDiffResultLists = fileDiffResultLists;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            ArgumentNullException.ThrowIfNull(config);
            _spinnerFrames = config.SpinnerFrames.ToArray();
        }
        private const string DIFF_REPORT_FILE_NAME = "diff_report.md";
        private const string SPINNER_LABEL_GENERATING_REPORT = "Generating report";
        private const string REPORT_TITLE = "# Folder Diff Report";
        private const string REPORT_DISASSEMBLER_NOT_USED = "N/A";
        private const string REPORT_LIST_SEPARATOR = ", ";
        private const string NOTE_MVID_SKIP = $"Note: When diffing {Constants.LABEL_IL}, lines starting with \"{Constants.IL_MVID_LINE_PREFIX}\" (if present) are ignored because they contain disassembler-emitted Module Version ID metadata that can change on rebuild without meaning the executable IL changed.";

        private const string NOTE_IL_CONTAINS_SKIP_ENABLED_BUT_EMPTY = "Note: IL line-ignore-by-contains is enabled, but no non-empty strings are configured.";
        private const string REPORT_LEGEND_HEADER = "- Legend:";
        private const string REPORT_MARKER_IGNORED = "[ x ]";
        private const string REPORT_LABEL_IGNORED = "Ignored";
        private const string REPORT_MARKER_UNCHANGED = "[ = ]";
        private const string REPORT_LABEL_UNCHANGED = "Unchanged";
        private const string REPORT_MARKER_ADDED = "[ + ]";
        private const string REPORT_LABEL_ADDED = "Added";
        private const string REPORT_MARKER_REMOVED = "[ - ]";
        private const string REPORT_LABEL_REMOVED = "Removed";
        private const string REPORT_MARKER_MODIFIED = "[ * ]";
        private const string REPORT_LABEL_MODIFIED = "Modified";
        private const string REPORT_LABEL_COMPARED = "Compared";
        private const string REPORT_SECTION_PREFIX = "\n## ";
        private const string REPORT_SECTION_FILES_SUFFIX = " Files";
        private const string REPORT_SECTION_IGNORED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_IGNORED + " " + REPORT_LABEL_IGNORED + REPORT_SECTION_FILES_SUFFIX;
        private const string REPORT_SECTION_UNCHANGED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_UNCHANGED + " " + REPORT_LABEL_UNCHANGED + REPORT_SECTION_FILES_SUFFIX;
        private const string REPORT_SECTION_ADDED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_ADDED + " " + REPORT_LABEL_ADDED + REPORT_SECTION_FILES_SUFFIX;
        private const string REPORT_SECTION_REMOVED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_REMOVED + " " + REPORT_LABEL_REMOVED + REPORT_SECTION_FILES_SUFFIX;
        private const string REPORT_SECTION_MODIFIED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_MODIFIED + " " + REPORT_LABEL_MODIFIED + REPORT_SECTION_FILES_SUFFIX;
        private const string REPORT_LOCATION_OLD = "(old)";
        private const string REPORT_LOCATION_NEW = "(new)";
        private const string REPORT_LOCATION_BOTH = "(old/new)";
        private const string REPORT_TIMESTAMP_ARROW = " → ";
        private const string REPORT_SECTION_SUMMARY = REPORT_SECTION_PREFIX + "Summary";
        private const string REPORT_SECTION_IL_CACHE_STATS = REPORT_SECTION_PREFIX + "IL Cache Stats";
        private const string WARNING_NEW_FILE_TIMESTAMP_OLDER_THAN_OLD = "One or more **modified** files in `new` have older last-modified timestamps than the corresponding files in `old`.";
        private const string REPORT_SECTION_WARNINGS = REPORT_SECTION_PREFIX + "Warnings";
        private const string LOG_REPORT_GENERATION_COMPLETED = "Report generation completed.";
        /// <summary>
        /// Generates the <see cref="DIFF_REPORT_FILE_NAME"/> Markdown report.
        /// <see cref="DIFF_REPORT_FILE_NAME"/> Markdown レポートを生成します。
        /// </summary>
        public void GenerateDiffReport(
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            ConfigSettings config,
            ILCache ilCache = null)
        {
            string diffReportAbsolutePath = GetDiffReportAbsolutePath(reportsFolderAbsolutePath);
            bool hasMd5Mismatch = _fileDiffResultLists.HasAnyMd5Mismatch;
            bool hasTimestampRegressionWarning = _fileDiffResultLists.HasAnyNewFileTimestampOlderThanOldWarning;
            using var spinner = new ConsoleSpinner(SPINNER_LABEL_GENERATING_REPORT, frames: _spinnerFrames);
            var reportGenerated = false;
            try
            {
                // diff_report.md is the final artifact — rethrow on any write/path failure.
                // diff_report.md は最終成果物なので、失敗したら継続せず再スローする。
                WriteDiffReport(
                    diffReportAbsolutePath,
                    oldFolderAbsolutePath,
                    newFolderAbsolutePath,
                    appVersion,
                    elapsedTimeString,
                    computerName,
                    config,
                    hasMd5Mismatch,
                    hasTimestampRegressionWarning,
                    ilCache);
                reportGenerated = true;
            }
            catch (ArgumentException)
            {
                LogReportOutputFailure(diffReportAbsolutePath);
                throw;
            }
            catch (IOException)
            {
                LogReportOutputFailure(diffReportAbsolutePath);
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                LogReportOutputFailure(diffReportAbsolutePath);
                throw;
            }
            catch (NotSupportedException)
            {
                LogReportOutputFailure(diffReportAbsolutePath);
                throw;
            }
            finally
            {
                // Best-effort read-only flag — warn only, the report is already usable.
                // 読み取り専用化は best-effort。失敗してもレポート自体は利用可能なので warning のみ。
                TrySetReportReadOnly(diffReportAbsolutePath);
                spinner.Complete(reportGenerated ? LOG_REPORT_GENERATION_COMPLETED : null);
            }
        }

        private static string GetDiffReportAbsolutePath(string reportsFolderAbsolutePath)
            => Path.Combine(reportsFolderAbsolutePath, DIFF_REPORT_FILE_NAME);

        private void WriteDiffReport(
            string diffReportAbsolutePath,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            ConfigSettings config,
            bool hasMd5Mismatch,
            bool hasTimestampRegressionWarning,
            ILCache ilCache)
        {
            PathValidator.ValidateAbsolutePathLengthOrThrow(diffReportAbsolutePath);
            File.Delete(diffReportAbsolutePath);

            using var streamWriter = new StreamWriter(diffReportAbsolutePath);
            WriteReportSections(
                streamWriter,
                oldFolderAbsolutePath,
                newFolderAbsolutePath,
                appVersion,
                elapsedTimeString,
                computerName,
                config,
                hasMd5Mismatch,
                hasTimestampRegressionWarning,
                ilCache);
        }

        /// <summary>
        /// Ordered list of all report section writers; sections are emitted in this order.
        /// レポートに書き込む全セクションのリスト（順序通りに出力されます）。
        /// </summary>
        private static readonly IReadOnlyList<IReportSectionWriter> _sectionWriters = new IReportSectionWriter[]
        {
            new HeaderSectionWriter(),
            new LegendSectionWriter(),
            new IgnoredFilesSectionWriter(),
            new UnchangedFilesSectionWriter(),
            new AddedFilesSectionWriter(),
            new RemovedFilesSectionWriter(),
            new ModifiedFilesSectionWriter(),
            new SummarySectionWriter(),
            new ILCacheStatsSectionWriter(),
            new WarningsSectionWriter(),
        };

        private void WriteReportSections(
            StreamWriter streamWriter,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            ConfigSettings config,
            bool hasMd5Mismatch,
            bool hasTimestampRegressionWarning,
            ILCache ilCache)
        {
            var context = new ReportWriteContext
            {
                OldFolderAbsolutePath = oldFolderAbsolutePath,
                NewFolderAbsolutePath = newFolderAbsolutePath,
                AppVersion = appVersion,
                ElapsedTimeString = elapsedTimeString,
                ComputerName = computerName,
                Config = config,
                HasMd5Mismatch = hasMd5Mismatch,
                HasTimestampRegressionWarning = hasTimestampRegressionWarning,
                IlCache = ilCache,
                FileDiffResultLists = _fileDiffResultLists,
            };
            foreach (var sectionWriter in _sectionWriters)
            {
                sectionWriter.Write(streamWriter, context);
            }
        }

        private void LogReportOutputFailure(string diffReportAbsolutePath)
            => _logger.LogMessage(AppLogLevel.Error, $"Failed to output report to '{diffReportAbsolutePath}'", shouldOutputMessageToConsole: true);

        private void TrySetReportReadOnly(string diffReportAbsolutePath)
        {
            try
            {
                FileSystemUtility.TrySetReadOnly(diffReportAbsolutePath);
            }
            catch (ArgumentException ex)
            {
                LogReportProtectionWarning(ex);
            }
            catch (IOException ex)
            {
                LogReportProtectionWarning(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogReportProtectionWarning(ex);
            }
            catch (NotSupportedException ex)
            {
                LogReportProtectionWarning(ex);
            }
        }

        private void LogReportProtectionWarning(Exception ex)
        {
            _logger.LogMessage(AppLogLevel.Warning, ex.Message, shouldOutputMessageToConsole: true, ex);
        }

        // ── Private static helpers used by section writers ──────────────────────

        private static string GetIgnoredFileLocationLabel(FileDiffResultLists.IgnoredFileLocation location)
            => location switch
            {
                FileDiffResultLists.IgnoredFileLocation.Old => REPORT_LOCATION_OLD,
                FileDiffResultLists.IgnoredFileLocation.New => REPORT_LOCATION_NEW,
                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New => REPORT_LOCATION_BOTH,
                _ => string.Empty
            };

        private static string BuildIgnoredFileTimestampInfo(
            KeyValuePair<string, FileDiffResultLists.IgnoredFileLocation> entry,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath)
        {
            bool hasOld = (entry.Value & FileDiffResultLists.IgnoredFileLocation.Old) != 0;
            bool hasNew = (entry.Value & FileDiffResultLists.IgnoredFileLocation.New) != 0;
            if (!hasOld && !hasNew)
            {
                return null;
            }
            if (hasOld && hasNew)
            {
                var oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, entry.Key));
                var newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, entry.Key));
                return $"[{oldTs}{REPORT_TIMESTAMP_ARROW}{newTs}]";
            }
            var ts = hasOld
                ? Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, entry.Key))
                : Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, entry.Key));
            return $"[{ts}]";
        }

        private static string BuildDisassemblerHeaderText(FileDiffResultLists fileDiffResultLists)
        {
            var observedLabels = fileDiffResultLists.DisassemblerToolVersions.Keys
                .Concat(fileDiffResultLists.DisassemblerToolVersionsFromCache.Keys)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(GetDisassemblerDisplayOrder)
                .ThenByDescending(label => label.IndexOf("(version:", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenBy(label => label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return observedLabels.Count == 0
                ? REPORT_DISASSEMBLER_NOT_USED
                : string.Join(REPORT_LIST_SEPARATOR, observedLabels);
        }

        private static int GetDisassemblerDisplayOrder(string label)
        {
            var toolName = ExtractToolName(label);
            if (string.Equals(toolName, Constants.DOTNET_ILDASM, StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(toolName, Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(toolName, Constants.ILSPY_CMD, StringComparison.OrdinalIgnoreCase)) return 2;
            return 3;
        }

        private static string ExtractToolName(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return string.Empty;
            var versionIndex = label.IndexOf(" (version:", StringComparison.OrdinalIgnoreCase);
            return versionIndex >= 0 ? label.Substring(0, versionIndex).Trim() : label.Trim();
        }

        private static string BuildDiffDetailDisplay(string fileRelativePath, FileDiffResultLists.DiffDetailResult diffDetail, FileDiffResultLists fileDiffResultLists)
        {
            if ((diffDetail == FileDiffResultLists.DiffDetailResult.ILMatch || diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch) &&
                fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(fileRelativePath, out var label) &&
                !string.IsNullOrWhiteSpace(label))
            {
                return $"`{diffDetail}` `{label}`";
            }
            return $"`{diffDetail}`";
        }

        private static List<string> GetNormalizedIlIgnoreContainingStrings(ConfigSettings config)
        {
            if (config?.ILIgnoreLineContainingStrings == null) return new List<string>();
            return config.ILIgnoreLineContainingStrings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        // ── Nested section writer implementations ────────────────────────────

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
