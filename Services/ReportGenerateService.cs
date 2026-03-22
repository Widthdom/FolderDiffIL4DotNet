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
    public sealed partial class ReportGenerateService
    {
        private readonly FileDiffResultLists _fileDiffResultLists;
        private readonly ILoggerService _logger;
        private readonly string[] _spinnerFrames;

        public ReportGenerateService(FileDiffResultLists fileDiffResultLists, ILoggerService logger, IReadOnlyConfigSettings config)
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
        private const string REPORT_LEGEND_HEADER = "- Legend (Diff Detail):";
        private const string REPORT_IMPORTANCE_LEGEND_HEADER = "- Legend (Change Importance):";
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
            IReadOnlyConfigSettings config,
            ILCache? ilCache = null)
        {
            string diffReportAbsolutePath = GetDiffReportAbsolutePath(reportsFolderAbsolutePath);
            bool hasSha256Mismatch = _fileDiffResultLists.HasAnySha256Mismatch;
            bool hasTimestampRegressionWarning = _fileDiffResultLists.HasAnyNewFileTimestampOlderThanOldWarning;
            using var spinner = new ConsoleSpinner(SPINNER_LABEL_GENERATING_REPORT, frames: _spinnerFrames);
            var reportGenerated = false;
            try
            {
                WriteDiffReport(
                    diffReportAbsolutePath,
                    oldFolderAbsolutePath,
                    newFolderAbsolutePath,
                    appVersion,
                    elapsedTimeString,
                    computerName,
                    config,
                    hasSha256Mismatch,
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
            IReadOnlyConfigSettings config,
            bool hasSha256Mismatch,
            bool hasTimestampRegressionWarning,
            ILCache? ilCache)
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
                hasSha256Mismatch,
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
            IReadOnlyConfigSettings config,
            bool hasSha256Mismatch,
            bool hasTimestampRegressionWarning,
            ILCache? ilCache)
        {
            var context = new ReportWriteContext
            {
                OldFolderAbsolutePath = oldFolderAbsolutePath,
                NewFolderAbsolutePath = newFolderAbsolutePath,
                AppVersion = appVersion,
                ElapsedTimeString = elapsedTimeString,
                ComputerName = computerName,
                Config = config,
                HasSha256Mismatch = hasSha256Mismatch,
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
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
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

        private static string? BuildIgnoredFileTimestampInfo(
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
                return $"{oldTs}{REPORT_TIMESTAMP_ARROW}{newTs}";
            }
            var ts = hasOld
                ? Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, entry.Key))
                : Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, entry.Key));
            return ts;
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
            var importance = fileDiffResultLists.GetMaxImportance(fileRelativePath);
            if (importance == null)
                return $"`{diffDetail}`";
            return $"`{diffDetail}` `{importance.Value}`";
        }

        private static string BuildDisassemblerDisplay(string fileRelativePath, FileDiffResultLists.DiffDetailResult diffDetail, FileDiffResultLists fileDiffResultLists)
        {
            if ((diffDetail == FileDiffResultLists.DiffDetailResult.ILMatch || diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch) &&
                fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(fileRelativePath, out var label) &&
                !string.IsNullOrWhiteSpace(label))
            {
                return $"`{label}`";
            }
            return "";
        }

        /// <summary>
        /// Returns the display order for Unchanged files: SHA256Match → ILMatch → TextMatch.
        /// Unchanged ファイルの表示順序を返します: SHA256Match → ILMatch → TextMatch。
        /// </summary>
        private static int GetUnchangedSortOrder(FileDiffResultLists.DiffDetailResult detail)
            => detail switch
            {
                FileDiffResultLists.DiffDetailResult.SHA256Match => 0,
                FileDiffResultLists.DiffDetailResult.ILMatch => 1,
                FileDiffResultLists.DiffDetailResult.TextMatch => 2,
                _ => 3
            };

        /// <summary>
        /// Returns the display order for Modified files: TextMismatch → ILMismatch → SHA256Mismatch.
        /// Modified ファイルの表示順序を返します: TextMismatch → ILMismatch → SHA256Mismatch。
        /// </summary>
        private static int GetModifiedSortOrder(FileDiffResultLists.DiffDetailResult detail)
            => detail switch
            {
                FileDiffResultLists.DiffDetailResult.TextMismatch => 0,
                FileDiffResultLists.DiffDetailResult.ILMismatch => 1,
                FileDiffResultLists.DiffDetailResult.SHA256Mismatch => 2,
                _ => 3
            };

        /// <summary>
        /// Returns a sort ordinal for <see cref="ChangeImportance"/> (High=0 first).
        /// <see cref="ChangeImportance"/> のソート序数を返します（High=0 が先頭）。
        /// </summary>
        private static int GetImportanceSortOrder(ChangeImportance? importance)
            => importance switch
            {
                ChangeImportance.High => 0,
                ChangeImportance.Medium => 1,
                ChangeImportance.Low => 2,
                _ => 3 // null / no semantic changes
            };

        private static List<string> GetNormalizedIlIgnoreContainingStrings(IReadOnlyConfigSettings config)
        {
            if (config?.ILIgnoreLineContainingStrings == null) return new List<string>();
            return config.ILIgnoreLineContainingStrings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Writes the Disassembler Availability table to the Markdown report header.
        /// Markdown レポートヘッダに逆アセンブラ利用可否テーブルを書き込みます。
        /// </summary>
        private static void WriteDisassemblerAvailabilityTable(StreamWriter writer, IReadOnlyList<DisassemblerProbeResult>? probeResults)
        {
            if (probeResults == null || probeResults.Count == 0)
            {
                return;
            }
            writer.WriteLine("- Disassembler Availability:");
            writer.WriteLine();
            writer.WriteLine("| Tool | Available | Version |");
            writer.WriteLine("|------|:---------:|---------|");
            foreach (var probe in probeResults)
            {
                var available = probe.Available ? "Yes" : "No";
                var version = probe.Available && !string.IsNullOrWhiteSpace(probe.Version)
                    ? probe.Version
                    : REPORT_DISASSEMBLER_NOT_USED;
                writer.WriteLine($"| {probe.ToolName} | {available} | {version} |");
            }
        }
    }
}
