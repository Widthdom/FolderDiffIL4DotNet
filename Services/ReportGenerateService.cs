using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Common;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Generates a Markdown diff report (<see cref="DIFF_REPORT_FILE_NAME"/>) summarising folder comparison results.
    /// Section writers are injected via DI and ordered by <see cref="IReportSectionWriter.Order"/>.
    /// 差分結果の Markdown レポート (<see cref="DIFF_REPORT_FILE_NAME"/>) を生成するサービス。
    /// セクションライターは DI 経由で注入され、<see cref="IReportSectionWriter.Order"/> でソートされます。
    /// </summary>
    public sealed partial class ReportGenerateService
    {
        private readonly FileDiffResultLists _fileDiffResultLists;
        private readonly ILoggerService _logger;
        private readonly IReadOnlyList<IReportSectionWriter> _sectionWriters;

        /// <summary>
        /// Initializes a new instance of <see cref="ReportGenerateService"/>.
        /// <see cref="ReportGenerateService"/> の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="fileDiffResultLists">Comparison results to include in the report. / レポートに含める比較結果。</param>
        /// <param name="logger">Logger for diagnostic output. / 診断出力用ロガー。</param>
        /// <param name="sectionWriters">Ordered collection of section writers injected via DI. / DI 経由で注入されるセクションライターのコレクション。</param>
        public ReportGenerateService(FileDiffResultLists fileDiffResultLists, ILoggerService logger, IEnumerable<IReportSectionWriter> sectionWriters)
        {
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            _fileDiffResultLists = fileDiffResultLists;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            ArgumentNullException.ThrowIfNull(sectionWriters);
            _sectionWriters = sectionWriters.OrderBy(w => w.Order).ToList();
        }
        private const string DIFF_REPORT_FILE_NAME = "diff_report.md";
        private const string REPORT_TITLE = "# Folder Diff Report";
        private const string REPORT_DISASSEMBLER_NOT_USED = "N/A";
        private const string REPORT_LIST_SEPARATOR = ", ";
        private const string NOTE_MVID_SKIP = $"Note: When diffing {Constants.LABEL_IL}, lines starting with \"{Constants.IL_MVID_LINE_PREFIX}\" (if present) are ignored because they contain disassembler-emitted Module Version ID metadata that can change on rebuild without meaning the executable IL changed.";

        private const string NOTE_IL_CONTAINS_SKIP_ENABLED_BUT_EMPTY = "Note: IL line-ignore-by-contains is enabled, but no non-empty strings are configured.";
        private const string REPORT_LEGEND_HEADER = "### Legend — Diff Detail";
        private const string REPORT_IMPORTANCE_LEGEND_HEADER = "### Legend — Change Importance";
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
        private const string REPORT_SECTION_WARNINGS = REPORT_SECTION_PREFIX + "Warnings";
        private const string LOG_REPORT_GENERATION_COMPLETED = "Report generation completed.";

        /// <summary>
        /// Generates the <see cref="DIFF_REPORT_FILE_NAME"/> Markdown report.
        /// <see cref="DIFF_REPORT_FILE_NAME"/> Markdown レポートを生成します。
        /// </summary>
        public void GenerateDiffReport(ReportGenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            string diffReportAbsolutePath = GetDiffReportAbsolutePath(context.ReportsFolderAbsolutePath);
            bool hasSha256Mismatch = _fileDiffResultLists.HasAnySha256Mismatch;
            bool hasTimestampRegressionWarning = _fileDiffResultLists.HasAnyNewFileTimestampOlderThanOldWarning;
            var reportGenerated = false;
            try
            {
                WriteDiffReport(
                    diffReportAbsolutePath,
                    context.OldFolderAbsolutePath,
                    context.NewFolderAbsolutePath,
                    context.AppVersion,
                    context.ElapsedTimeString,
                    context.ComputerName,
                    context.Config,
                    hasSha256Mismatch,
                    hasTimestampRegressionWarning,
                    context.IlCache);
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
                if (reportGenerated)
                {
                    _logger.LogMessage(AppLogLevel.Info, LOG_REPORT_GENERATION_COMPLETED, shouldOutputMessageToConsole: false);
                }
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
                if (sectionWriter.IsEnabled(context))
                {
                    sectionWriter.Write(streamWriter, context);
                }
            }
        }

        /// <summary>
        /// Returns all built-in section writer instances in default order.
        /// Used by <see cref="Runner.RunScopeBuilder"/> to register them in DI.
        /// 組み込みセクションライターのインスタンスをデフォルト順序で返します。
        /// <see cref="Runner.RunScopeBuilder"/> が DI に登録する際に使用します。
        /// </summary>
        internal static IReadOnlyList<IReportSectionWriter> CreateBuiltInSectionWriters() =>
            new IReportSectionWriter[]
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

        private void LogReportOutputFailure(string diffReportAbsolutePath)
            => _logger.LogMessage(AppLogLevel.Error, $"Failed to output report to '{diffReportAbsolutePath}'", shouldOutputMessageToConsole: true);

        private void TrySetReportReadOnly(string diffReportAbsolutePath)
        {
            try
            {
                FileSystemUtility.TrySetReadOnly(diffReportAbsolutePath);
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
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

        /// <summary>
        /// Builds a Markdown vulnerability column value for a dependency change entry.
        /// 依存関係変更エントリの Markdown 脆弱性カラム値を構築します。
        /// </summary>
        private static string BuildMarkdownVulnColumn(VulnerabilityCheckResult? vuln)
        {
            if (vuln == null || !vuln.HasAnyVulnerabilities)
                return "—";

            var parts = new System.Collections.Generic.List<string>();

            // New version vulnerabilities / 新バージョンの脆弱性
            foreach (var v in vuln.NewVersionVulnerabilities)
            {
                string sev = VulnerabilityCheckResult.SeverityToLabel(v.Severity);
                parts.Add($"⚠ {sev}");
            }

            // Resolved vulnerabilities / 解消済み脆弱性
            if (vuln.HasResolvedVulnerabilities)
            {
                foreach (var v in vuln.OldVersionVulnerabilities)
                {
                    bool alsoNew = false;
                    foreach (var nv in vuln.NewVersionVulnerabilities)
                        if (string.Equals(nv.AdvisoryUrl, v.AdvisoryUrl, System.StringComparison.Ordinal)) { alsoNew = true; break; }
                    if (alsoNew) continue;
                    string sev = VulnerabilityCheckResult.SeverityToLabel(v.Severity);
                    parts.Add($"~~{sev}~~");
                }
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "—";
        }

        /// <summary>
        /// Builds a Markdown referencing assemblies column value for a dependency change entry.
        /// 依存関係変更エントリの Markdown 参照アセンブリカラム値を構築します。
        /// </summary>
        private static string BuildMarkdownRefsColumn(System.Collections.Generic.IReadOnlyList<string>? refs)
        {
            if (refs is not { Count: > 0 })
                return "—";
            return string.Join(", ", refs);
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

        private static string BuildChangeTagDisplay(string fileRelativePath, FileDiffResultLists fileDiffResultLists)
        {
            if (fileDiffResultLists.FileRelativePathToChangeTags.TryGetValue(fileRelativePath, out var tags) && tags.Count > 0)
            {
                // Wrap each tag label individually in backticks / 各タグラベルを個別にバッククォートで囲む
                return string.Join(", ", tags.Select(t => $"`{ChangeTagClassifier.GetLabel(t)}`"));
            }
            return "";
        }

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
        private static void WriteDisassemblerAvailabilityTable(StreamWriter writer, IReadOnlyList<DisassemblerProbeResult>? probeResults, string inUseHeaderText)
        {
            if (probeResults == null || probeResults.Count == 0)
            {
                return;
            }
            writer.WriteLine("### Disassembler Availability");
            writer.WriteLine();
            writer.WriteLine("| Tool | Available | Version | In Use |");
            writer.WriteLine("|------|:---------:|---------|:------:|");
            foreach (var probe in probeResults)
            {
                // Check if this tool is the one actually used / このツールが実際に使用されたかチェック
                bool isInUse = !string.IsNullOrWhiteSpace(inUseHeaderText)
                    && inUseHeaderText.IndexOf(probe.ToolName, StringComparison.OrdinalIgnoreCase) >= 0;
                var available = probe.Available ? "Yes" : "No";
                var version = probe.Available && !string.IsNullOrWhiteSpace(probe.Version)
                    ? probe.Version
                    : REPORT_DISASSEMBLER_NOT_USED;
                var inUseCol = isInUse ? "Yes" : "No";
                writer.WriteLine($"| {probe.ToolName} | {available} | {version} | {inUseCol} |");
            }
        }

        /// <summary>
        /// Writes warning banners for disassembler issues: no disassembler available, or mixed tool usage.
        /// 逆アセンブラの問題に関する警告バナーを出力: 逆アセンブラ未検出、または複数ツール混在使用。
        /// </summary>
        private static void WriteDisassemblerWarnings(StreamWriter writer, FileDiffResultLists fileDiffResultLists)
        {
            // Warning: no disassembler available / 警告: 逆アセンブラが利用不可
            var probeResults = fileDiffResultLists.DisassemblerAvailability;
            if (probeResults != null && probeResults.Count > 0 && !probeResults.Any(p => p.Available))
            {
                writer.WriteLine();
                writer.WriteLine("> **⚠ Warning**: No disassembler tool is available. .NET assembly comparison will fail if any .dll/.exe files with differing SHA256 hashes are detected. Install `dotnet-ildasm` or `ilspycmd` to enable IL-level comparison.");
                writer.WriteLine();
            }

            // Warning: multiple different disassembler tools used / 警告: 異なる逆アセンブラツールが混在
            var allLabels = fileDiffResultLists.DisassemblerToolVersions.Keys
                .Concat(fileDiffResultLists.DisassemblerToolVersionsFromCache.Keys)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var distinctToolNames = allLabels
                .Select(label => ExtractToolName(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctToolNames.Count > 1)
            {
                writer.WriteLine();
                writer.WriteLine($"> **⚠ Warning**: Multiple disassembler tools were used across file comparisons in this run ({string.Join(", ", allLabels)}). Each file pair is compared using the same tool, but IL output format may differ between tools, reducing cross-file consistency. This typically occurs when the preferred tool fails on certain assemblies and the fallback is used. If caused by stale cache entries from a previous tool, use --clear-cache to resolve.");
                writer.WriteLine();
            }
        }
    }
}
