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
            bool hasILFilterWarnings = _fileDiffResultLists.HasAnyILFilterWarning;
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
                hasILFilterWarnings,
                context.IlCache,
                context.ReviewChecklistItems);
                reportGenerated = true;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                LogReportOutputFailure(context.ReportsFolderAbsolutePath, diffReportAbsolutePath, ex);
                throw;
            }
            finally
            {
                if (reportGenerated)
                {
                    TrySetReportReadOnly(context.ReportsFolderAbsolutePath, diffReportAbsolutePath);
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
            bool hasILFilterWarnings,
            ILCache? ilCache,
            IReadOnlyList<string> reviewChecklistItems)
        {
            PathValidator.ValidateAbsolutePathLengthOrThrow(diffReportAbsolutePath);
            PrepareOutputPathForOverwrite(diffReportAbsolutePath);

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
                hasILFilterWarnings,
                ilCache,
                reviewChecklistItems);
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
            bool hasILFilterWarnings,
            ILCache? ilCache,
            IReadOnlyList<string> reviewChecklistItems)
        {
            var context = new ReportWriteContext
            {
                OldFolderAbsolutePath = oldFolderAbsolutePath,
                NewFolderAbsolutePath = newFolderAbsolutePath,
                AppVersion = appVersion,
                ElapsedTimeString = elapsedTimeString,
                ComputerName = computerName,
                Config = config,
                Logger = _logger,
                HasSha256Mismatch = hasSha256Mismatch,
                HasTimestampRegressionWarning = hasTimestampRegressionWarning,
                HasILFilterWarnings = hasILFilterWarnings,
                IlCache = ilCache,
                FileDiffResultLists = _fileDiffResultLists,
                ReviewChecklistItems = reviewChecklistItems,
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
    }
}
