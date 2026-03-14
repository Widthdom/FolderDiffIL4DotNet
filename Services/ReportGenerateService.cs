using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// 差分結果の Markdown レポート (<see cref="DIFF_REPORT_FILE_NAME"/>) を生成するサービス。
    /// </summary>
    public sealed class ReportGenerateService
    {
        #region private read only member variables
        /// <summary>
        /// 差分結果保持オブジェクト。
        /// </summary>
        private readonly FileDiffResultLists _fileDiffResultLists;

        /// <summary>
        /// ログ出力サービス。
        /// </summary>
        private readonly ILoggerService _logger;
        #endregion

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="fileDiffResultLists">差分結果保持オブジェクト。</param>
        /// <param name="logger">ログ出力サービス。</param>
        public ReportGenerateService(FileDiffResultLists fileDiffResultLists, ILoggerService logger)
        {
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            _fileDiffResultLists = fileDiffResultLists;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }
        #region constants
        /// <summary>
        /// フォルダ差分の概要を出力する Markdown レポートのファイル名
        /// </summary>
        private const string DIFF_REPORT_FILE_NAME = "diff_report.md";

        /// <summary>
        /// IL 出力から比較時に除外する MVID 行の接頭辞（ビルドごとに変化するため差分の対象外）。
        /// </summary>
        private const string MVID_PREFIX = "// MVID:";

        /// <summary>
        /// MD5Mismatch警告文言
        /// </summary>
        private const string WARNING_MD5_MISMATCH = $"One or more files were classified as `{Constants.LABEL_MD5}Mismatch`. Manual review is recommended because only an {Constants.LABEL_MD5} hash comparison was possible.";

        /// <summary>
        /// レポート生成スピナーのラベル。
        /// </summary>
        private const string SPINNER_LABEL_GENERATING_REPORT = "Generating report";

        /// <summary>
        /// レポートタイトル
        /// </summary>
        private const string REPORT_TITLE = "# Folder Diff Report";

        /// <summary>
        /// レポートヘッダ: アプリバージョン
        /// </summary>
        private const string REPORT_HEADER_APP_VERSION = "- App Version: " + Constants.APP_NAME + " {0}";

        /// <summary>
        /// レポートヘッダ: コンピュータ名
        /// </summary>
        private const string REPORT_HEADER_COMPUTER = "- Computer: {0}";

        /// <summary>
        /// レポートヘッダ: 旧フォルダパス
        /// </summary>
        private const string REPORT_HEADER_OLD = "- Old: {0}";

        /// <summary>
        /// レポートヘッダ: 新フォルダパス
        /// </summary>
        private const string REPORT_HEADER_NEW = "- New: {0}";

        /// <summary>
        /// レポートヘッダ: 無視拡張子一覧
        /// </summary>
        private const string REPORT_HEADER_IGNORED_EXTENSIONS = "- Ignored Extensions: {0}";

        /// <summary>
        /// レポートヘッダ: テキスト拡張子一覧
        /// </summary>
        private const string REPORT_HEADER_TEXT_EXTENSIONS = "- Text File Extensions: {0}";

        /// <summary>
        /// レポートヘッダ: 使用した逆アセンブラ
        /// </summary>
        private const string REPORT_HEADER_IL_DISASSEMBLERS = "- IL Disassembler: {0}";

        /// <summary>
        /// 逆アセンブラ未使用時の表示。
        /// </summary>
        private const string REPORT_DISASSEMBLER_NOT_USED = "N/A";

        /// <summary>
        /// レポートヘッダ: 経過時間
        /// </summary>
        private const string REPORT_HEADER_ELAPSED_TIME = "- " + Constants.LOG_ELAPSED_TIME;

        /// <summary>
        /// レポート内でのリスト結合区切り
        /// </summary>
        private const string REPORT_LIST_SEPARATOR = ", ";

        /// <summary>
        /// MVID行スキップの但し書き（存在する場合のみ対象）。
        /// </summary>
        private const string NOTE_MVID_SKIP = $"Note: When diffing {Constants.LABEL_IL}, lines starting with \"{MVID_PREFIX}\" (if present) are ignored.";

        /// <summary>
        /// 設定された部分一致除外文字列の但し書き。
        /// </summary>
        private const string NOTE_IL_CONTAINS_SKIP = $"Note: When diffing {Constants.LABEL_IL}, lines containing any of the configured strings are ignored: {{0}}.";

        /// <summary>
        /// 部分一致除外が有効だが、文字列設定が空の場合の但し書き。
        /// </summary>
        private const string NOTE_IL_CONTAINS_SKIP_ENABLED_BUT_EMPTY = "Note: IL line-ignore-by-contains is enabled, but no non-empty strings are configured.";

        /// <summary>
        /// レジェンドのヘッダ
        /// </summary>
        private const string REPORT_LEGEND_HEADER = "- Legend:";

        /// <summary>
        /// レジェンド: 共通サフィックス
        /// </summary>
        private const string REPORT_LEGEND_SUFFIX_MATCH_MISMATCH = "match / mismatch";

        /// <summary>
        /// レジェンド: MD5
        /// </summary>
        private const string REPORT_LEGEND_MD5 = "  - `{0}` / `{1}`: " + Constants.LABEL_MD5 + " hash " + REPORT_LEGEND_SUFFIX_MATCH_MISMATCH;

        /// <summary>
        /// レジェンド: IL
        /// </summary>
        private const string REPORT_LEGEND_IL = "  - `{0}` / `{1}`: " + Constants.LABEL_IL + "(Intermediate Language) " + REPORT_LEGEND_SUFFIX_MATCH_MISMATCH;

        /// <summary>
        /// レジェンド: テキスト
        /// </summary>
        private const string REPORT_LEGEND_TEXT = "  - `{0}` / `{1}`: Text " + REPORT_LEGEND_SUFFIX_MATCH_MISMATCH;

        /// <summary>
        /// レポートマーカー: Ignored
        /// </summary>
        private const string REPORT_MARKER_IGNORED = "[ x ]";

        /// <summary>
        /// レポートラベル: Ignored
        /// </summary>
        private const string REPORT_LABEL_IGNORED = "Ignored";

        /// <summary>
        /// レポートマーカー: Unchanged
        /// </summary>
        private const string REPORT_MARKER_UNCHANGED = "[ = ]";

        /// <summary>
        /// レポートラベル: Unchanged
        /// </summary>
        private const string REPORT_LABEL_UNCHANGED = "Unchanged";

        /// <summary>
        /// レポートマーカー: Added
        /// </summary>
        private const string REPORT_MARKER_ADDED = "[ + ]";

        /// <summary>
        /// レポートラベル: Added
        /// </summary>
        private const string REPORT_LABEL_ADDED = "Added";

        /// <summary>
        /// レポートマーカー: Removed
        /// </summary>
        private const string REPORT_MARKER_REMOVED = "[ - ]";

        /// <summary>
        /// レポートラベル: Removed
        /// </summary>
        private const string REPORT_LABEL_REMOVED = "Removed";

        /// <summary>
        /// レポートマーカー: Modified
        /// </summary>
        private const string REPORT_MARKER_MODIFIED = "[ * ]";

        /// <summary>
        /// レポートラベル: Modified
        /// </summary>
        private const string REPORT_LABEL_MODIFIED = "Modified";

        /// <summary>
        /// レポートラベル: Compared
        /// </summary>
        private const string REPORT_LABEL_COMPARED = "Compared";

        /// <summary>
        /// レポートセクションの共通プレフィックス
        /// </summary>
        private const string REPORT_SECTION_PREFIX = "\n## ";

        /// <summary>
        /// レポートセクション: Files 接尾辞
        /// </summary>
        private const string REPORT_SECTION_FILES_SUFFIX = " Files";

        /// <summary>
        /// レポートセクション: Ignored Files
        /// </summary>
        private const string REPORT_SECTION_IGNORED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_IGNORED + " " + REPORT_LABEL_IGNORED + REPORT_SECTION_FILES_SUFFIX;

        /// <summary>
        /// レポートセクション: Unchanged Files
        /// </summary>
        private const string REPORT_SECTION_UNCHANGED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_UNCHANGED + " " + REPORT_LABEL_UNCHANGED + REPORT_SECTION_FILES_SUFFIX;

        /// <summary>
        /// レポートセクション: Added Files
        /// </summary>
        private const string REPORT_SECTION_ADDED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_ADDED + " " + REPORT_LABEL_ADDED + REPORT_SECTION_FILES_SUFFIX;

        /// <summary>
        /// レポートセクション: Removed Files
        /// </summary>
        private const string REPORT_SECTION_REMOVED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_REMOVED + " " + REPORT_LABEL_REMOVED + REPORT_SECTION_FILES_SUFFIX;

        /// <summary>
        /// レポートセクション: Modified Files
        /// </summary>
        private const string REPORT_SECTION_MODIFIED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_MODIFIED + " " + REPORT_LABEL_MODIFIED + REPORT_SECTION_FILES_SUFFIX;

        /// <summary>
        /// ファイルの位置ラベル（旧）
        /// </summary>
        private const string REPORT_LOCATION_OLD = "(old)";

        /// <summary>
        /// ファイルの位置ラベル（新）
        /// </summary>
        private const string REPORT_LOCATION_NEW = "(new)";

        /// <summary>
        /// ファイルの位置ラベル（旧/新）
        /// </summary>
        private const string REPORT_LOCATION_BOTH = "(old/new)";

        /// <summary>
        /// ファイルの位置ラベル（旧・タイトルケース）
        /// </summary>
        private const string REPORT_LOCATION_OLD_TITLE = "(Old)";

        /// <summary>
        /// ファイルの位置ラベル（新・タイトルケース）
        /// </summary>
        private const string REPORT_LOCATION_NEW_TITLE = "(New)";

        /// <summary>
        /// Ignored ファイル行のフォーマット
        /// </summary>
        private const string REPORT_IGNORED_FILE_ITEM = "- " + REPORT_MARKER_IGNORED + " {0}";

        /// <summary>
        /// Ignored/タイムスタンプの HTML ラッパー
        /// </summary>
        private const string REPORT_TIMESTAMP_HTML_WRAPPER = " <u>({0})</u>";

        /// <summary>
        /// タイムスタンプ結合時の区切り
        /// </summary>
        private const string REPORT_TIMESTAMP_SEPARATOR = ", ";

        /// <summary>
        /// タイムスタンプ: 旧ファイル
        /// </summary>
        private const string REPORT_TIMESTAMP_UPDATED_OLD = "updated_old: {0}";

        /// <summary>
        /// タイムスタンプ: 新ファイル
        /// </summary>
        private const string REPORT_TIMESTAMP_UPDATED_NEW = "updated_new: {0}";

        /// <summary>
        /// Unchanged ファイル行（タイムスタンプあり）
        /// </summary>
        private const string REPORT_UNCHANGED_ITEM_WITH_TIMESTAMP = "- " + REPORT_MARKER_UNCHANGED + " {0} <u>{1}</u> {2}";

        /// <summary>
        /// Unchanged ファイル行（タイムスタンプなし）
        /// </summary>
        private const string REPORT_UNCHANGED_ITEM = "- " + REPORT_MARKER_UNCHANGED + " {0} {1}";

        /// <summary>
        /// Unchanged/タイムスタンプ（旧+新）
        /// </summary>
        private const string REPORT_UNCHANGED_TIMESTAMP_BOTH = "(updated_old: {0}, updated_new: {1})";

        /// <summary>
        /// Unchanged/タイムスタンプ（新のみ）
        /// </summary>
        private const string REPORT_UNCHANGED_TIMESTAMP_NEW = "(updated: {0})";

        /// <summary>
        /// Added ファイル行（タイムスタンプあり）
        /// </summary>
        private const string REPORT_ADDED_ITEM_WITH_TIMESTAMP = "- " + REPORT_MARKER_ADDED + " {0} <u>(updated: {1})</u>";

        /// <summary>
        /// Added ファイル行（タイムスタンプなし）
        /// </summary>
        private const string REPORT_ADDED_ITEM = "- " + REPORT_MARKER_ADDED + " {0}";

        /// <summary>
        /// Removed ファイル行（タイムスタンプあり）
        /// </summary>
        private const string REPORT_REMOVED_ITEM_WITH_TIMESTAMP = "- " + REPORT_MARKER_REMOVED + " {0} <u>(updated: {1})</u>";

        /// <summary>
        /// Removed ファイル行（タイムスタンプなし）
        /// </summary>
        private const string REPORT_REMOVED_ITEM = "- " + REPORT_MARKER_REMOVED + " {0}";

        /// <summary>
        /// Modified ファイル行（タイムスタンプあり）
        /// </summary>
        private const string REPORT_MODIFIED_ITEM_WITH_TIMESTAMP = "- " + REPORT_MARKER_MODIFIED + " {0} <u>(updated_old: {1}, updated_new: {2})</u> {3}";

        /// <summary>
        /// Modified ファイル行（タイムスタンプなし）
        /// </summary>
        private const string REPORT_MODIFIED_ITEM = "- " + REPORT_MARKER_MODIFIED + " {0} {1}";

        /// <summary>
        /// レポートフッタ: Summary セクション
        /// </summary>
        private const string REPORT_SECTION_SUMMARY = REPORT_SECTION_PREFIX + "Summary";

        /// <summary>
        /// Summary: ラベル幅
        /// </summary>
        private const int REPORT_SUMMARY_LABEL_WIDTH = 10;

        /// <summary>
        /// Summary: ラベル付き共通フォーマット
        /// </summary>
        private const string REPORT_SUMMARY_ITEM_FORMAT = "- {0,-10}: {1}";

        /// <summary>
        /// Summary: Compared
        /// </summary>
        private const string REPORT_SUMMARY_COMPARED = "- {0,-10}: {1} " + REPORT_LOCATION_OLD_TITLE + " vs {2} " + REPORT_LOCATION_NEW_TITLE;

        /// <summary>
        /// Summary: WARNING 行
        /// </summary>
        private const string REPORT_WARNING_LINE = "**WARNING:** {0}";

        /// <summary>
        /// new 側の更新日時逆転警告文言
        /// </summary>
        private const string WARNING_NEW_FILE_TIMESTAMP_OLDER_THAN_OLD = "One or more files in `new` have older last-modified timestamps than the corresponding files in `old`.";

        /// <summary>
        /// 警告セクション
        /// </summary>
        private const string REPORT_SECTION_WARNINGS = REPORT_SECTION_PREFIX + "Warnings";

        /// <summary>
        /// 更新日時逆転警告の一覧行
        /// </summary>
        private const string REPORT_WARNING_TIMESTAMP_REGRESSION_ITEM = "- {0} (updated_old: {1}, updated_new: {2})";

        /// <summary>
        /// レポート出力失敗
        /// </summary>
        private const string ERROR_FAILED_TO_OUTPUT_REPORT = "Failed to output report to '{0}'";

        /// <summary>
        /// レポート生成完了ログ。
        /// </summary>
        private const string LOG_REPORT_GENERATION_COMPLETED = "Report generation completed.";
        #endregion
        /// <summary>
        /// <see cref="DIFF_REPORT_FILE_NAME"/> を生成します。
        /// </summary>
        /// <param name="oldFolderAbsolutePath">旧フォルダの絶対パス</param>
        /// <param name="newFolderAbsolutePath">新フォルダの絶対パス</param>
        /// <param name="reportsFolderAbsolutePath">出力先レポートフォルダの絶対パス</param>
        /// <param name="appVersion">アプリケーションバージョン</param>
        /// <param name="elapsedTimeString">経過時間文字列 (null 可)</param>
        /// <param name="computerName">実行コンピュータ名</param>
        /// <param name="config">設定オブジェクト</param>
        /// <exception cref="Exception">入出力エラーなど予期しない例外</exception>
        public void GenerateDiffReport(
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            ConfigSettings config)
        {
            string diffReportAbsolutePath = Path.Combine(reportsFolderAbsolutePath, DIFF_REPORT_FILE_NAME);
            bool hasMd5Mismatch = _fileDiffResultLists.HasAnyMd5Mismatch;
            if (hasMd5Mismatch)
            {
                _logger.LogMessage(AppLogLevel.Warning, WARNING_MD5_MISMATCH, shouldOutputMessageToConsole: true, ConsoleColor.Yellow);
            }

            bool hasTimestampRegressionWarning = _fileDiffResultLists.HasAnyNewFileTimestampOlderThanOldWarning;
            using var spinner = new ConsoleSpinner(SPINNER_LABEL_GENERATING_REPORT);
            var reportGenerated = false;
            try
            {
                PathValidator.ValidateAbsolutePathLengthOrThrow(diffReportAbsolutePath);
                File.Delete(diffReportAbsolutePath);

                using (var streamWriter = new StreamWriter(diffReportAbsolutePath))
                {
                    WriteReportHeader(streamWriter, oldFolderAbsolutePath, newFolderAbsolutePath, appVersion, elapsedTimeString, computerName, config);
                    WriteLegend(streamWriter);
                    WriteIgnoredFilesSection(streamWriter, config, oldFolderAbsolutePath, newFolderAbsolutePath);
                    WriteUnchangedFilesSection(streamWriter, config, oldFolderAbsolutePath, newFolderAbsolutePath);
                    WriteAddedFilesSection(streamWriter, config);
                    WriteRemovedFilesSection(streamWriter, config);
                    WriteModifiedFilesSection(streamWriter, config, oldFolderAbsolutePath, newFolderAbsolutePath);
                    WriteSummarySection(streamWriter, config, hasMd5Mismatch);
                    WriteWarningsSection(streamWriter, hasTimestampRegressionWarning);
                }
                reportGenerated = true;
            }
            catch (Exception)
            {
                _logger.LogMessage(AppLogLevel.Error, string.Format(ERROR_FAILED_TO_OUTPUT_REPORT, diffReportAbsolutePath), shouldOutputMessageToConsole: true);
                throw;
            }
            finally
            {
                // レポートファイルの読み取り専用属性を設定（失敗した場合は警告を出力し、処理は継続）
                try
                {
                    FileSystemUtility.TrySetReadOnly(diffReportAbsolutePath);
                }
                catch (Exception ex)
                {
                    _logger.LogMessage(AppLogLevel.Warning, ex.Message, shouldOutputMessageToConsole: true, ex);
                }
                spinner.Complete(reportGenerated ? LOG_REPORT_GENERATION_COMPLETED : null);
            }
        }

        /// <summary>
        /// レポートのヘッダ部（タイトル・実行情報・IL比較の注記）を書き込みます。
        /// </summary>
        /// <param name="streamWriter">出力先ライター。</param>
        /// <param name="oldFolderAbsolutePath">旧フォルダの絶対パス。</param>
        /// <param name="newFolderAbsolutePath">新フォルダの絶対パス。</param>
        /// <param name="appVersion">アプリケーションバージョン。</param>
        /// <param name="elapsedTimeString">経過時間文字列。</param>
        /// <param name="computerName">実行コンピュータ名。</param>
        /// <param name="config">設定オブジェクト。</param>
        private void WriteReportHeader(
            StreamWriter streamWriter,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            ConfigSettings config)
        {
            streamWriter.WriteLine(REPORT_TITLE);
            streamWriter.WriteLine(string.Format(REPORT_HEADER_APP_VERSION, appVersion));
            streamWriter.WriteLine(string.Format(REPORT_HEADER_COMPUTER, computerName));
            streamWriter.WriteLine(string.Format(REPORT_HEADER_OLD, oldFolderAbsolutePath));
            streamWriter.WriteLine(string.Format(REPORT_HEADER_NEW, newFolderAbsolutePath));
            streamWriter.WriteLine(string.Format(REPORT_HEADER_IGNORED_EXTENSIONS, string.Join(REPORT_LIST_SEPARATOR, config.IgnoredExtensions)));
            streamWriter.WriteLine(string.Format(REPORT_HEADER_TEXT_EXTENSIONS, string.Join(REPORT_LIST_SEPARATOR, config.TextFileExtensions)));
            var disassemblerText = BuildDisassemblerHeaderText();
            streamWriter.WriteLine(string.Format(REPORT_HEADER_IL_DISASSEMBLERS, disassemblerText));
            if (!string.IsNullOrWhiteSpace(elapsedTimeString))
            {
                streamWriter.WriteLine(string.Format(REPORT_HEADER_ELAPSED_TIME, elapsedTimeString));
            }
            streamWriter.WriteLine("- " + NOTE_MVID_SKIP);
            if (!config.ShouldIgnoreILLinesContainingConfiguredStrings)
            {
                return;
            }

            var ilIgnoreContainingStrings = GetNormalizedIlIgnoreContainingStrings(config);
            streamWriter.WriteLine("- " + (ilIgnoreContainingStrings.Count == 0
                ? NOTE_IL_CONTAINS_SKIP_ENABLED_BUT_EMPTY
                : string.Format(NOTE_IL_CONTAINS_SKIP, string.Join(REPORT_LIST_SEPARATOR, ilIgnoreContainingStrings.Select(value => $"\"{value}\"")))));
        }

        /// <summary>
        /// 判定根拠ラベルの凡例（Legend）を書き込みます。
        /// </summary>
        /// <param name="streamWriter">出力先ライター。</param>
        private static void WriteLegend(StreamWriter streamWriter)
        {
            streamWriter.WriteLine(REPORT_LEGEND_HEADER);
            streamWriter.WriteLine(string.Format(REPORT_LEGEND_MD5, FileDiffResultLists.DiffDetailResult.MD5Match, FileDiffResultLists.DiffDetailResult.MD5Mismatch));
            streamWriter.WriteLine(string.Format(REPORT_LEGEND_IL, FileDiffResultLists.DiffDetailResult.ILMatch, FileDiffResultLists.DiffDetailResult.ILMismatch));
            streamWriter.WriteLine(string.Format(REPORT_LEGEND_TEXT, FileDiffResultLists.DiffDetailResult.TextMatch, FileDiffResultLists.DiffDetailResult.TextMismatch));
        }

        /// <summary>
        /// Ignored Files セクションを書き込みます。
        /// </summary>
        /// <param name="streamWriter">出力先ライター。</param>
        /// <param name="config">設定オブジェクト。</param>
        /// <param name="oldFolderAbsolutePath">旧フォルダの絶対パス。</param>
        /// <param name="newFolderAbsolutePath">新フォルダの絶対パス。</param>
        private void WriteIgnoredFilesSection(StreamWriter streamWriter, ConfigSettings config, string oldFolderAbsolutePath, string newFolderAbsolutePath)
        {
            // Ignored Files はアプリ設定（ShouldIncludeIgnoredFiles）が true の場合のみ出力。
            // ShouldOutputFileTimestamps が true なら旧/新それぞれの存在箇所に応じた最終更新日時を併記し、
            // 旧/新のどちらに存在したかもラベル（(old)/(new)/(old/new)）で明示します。
            if (!config.ShouldIncludeIgnoredFiles || _fileDiffResultLists.IgnoredFilesRelativePathToLocation.Count == 0)
            {
                return;
            }

            streamWriter.WriteLine(REPORT_SECTION_IGNORED_FILES);
            foreach (var entry in _fileDiffResultLists.IgnoredFilesRelativePathToLocation.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                var line = string.Format(REPORT_IGNORED_FILE_ITEM, entry.Key);
                var locationLabel = GetIgnoredFileLocationLabel(entry.Value);
                if (!string.IsNullOrEmpty(locationLabel))
                {
                    line += " " + locationLabel;
                }

                if (config.ShouldOutputFileTimestamps)
                {
                    var timestampInfo = BuildIgnoredFileTimestampInfo(entry, oldFolderAbsolutePath, newFolderAbsolutePath);
                    if (!string.IsNullOrEmpty(timestampInfo))
                    {
                        line += string.Format(REPORT_TIMESTAMP_HTML_WRAPPER, timestampInfo);
                    }
                }

                streamWriter.WriteLine(line);
            }
        }

        /// <summary>
        /// Unchanged Files セクションを書き込みます。
        /// </summary>
        /// <param name="streamWriter">出力先ライター。</param>
        /// <param name="config">設定オブジェクト。</param>
        /// <param name="oldFolderAbsolutePath">旧フォルダの絶対パス。</param>
        /// <param name="newFolderAbsolutePath">新フォルダの絶対パス。</param>
        private void WriteUnchangedFilesSection(StreamWriter streamWriter, ConfigSettings config, string oldFolderAbsolutePath, string newFolderAbsolutePath)
        {
            // Unchanged Files はアプリ設定で出力可否を制御。
            // ShouldOutputFileTimestamps が true の場合のみ、
            // 判定根拠が ILMatch のときは新旧両方の最終更新日時を、
            // それ以外は新の最終更新日時を併記します。
            if (!config.ShouldIncludeUnchangedFiles)
            {
                return;
            }

            streamWriter.WriteLine(REPORT_SECTION_UNCHANGED_FILES);
            foreach (var fileRelativePath in _fileDiffResultLists.UnchangedFilesRelativePath)
            {
                var diffDetail = _fileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                var diffDetailDisplay = BuildDiffDetailDisplay(fileRelativePath, diffDetail);
                if (config.ShouldOutputFileTimestamps)
                {
                    string oldFileTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, fileRelativePath));
                    string newFileTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, fileRelativePath));
                    string updateInfo = diffDetail == FileDiffResultLists.DiffDetailResult.ILMatch
                        ? string.Format(REPORT_UNCHANGED_TIMESTAMP_BOTH, oldFileTimestamp, newFileTimestamp)
                        : string.Format(REPORT_UNCHANGED_TIMESTAMP_NEW, newFileTimestamp);
                    streamWriter.WriteLine(string.Format(REPORT_UNCHANGED_ITEM_WITH_TIMESTAMP, fileRelativePath, updateInfo, diffDetailDisplay));
                }
                else
                {
                    streamWriter.WriteLine(string.Format(REPORT_UNCHANGED_ITEM, fileRelativePath, diffDetailDisplay));
                }
            }
        }

        /// <summary>
        /// Added Files セクションを書き込みます。
        /// </summary>
        /// <param name="streamWriter">出力先ライター。</param>
        /// <param name="config">設定オブジェクト。</param>
        private void WriteAddedFilesSection(StreamWriter streamWriter, ConfigSettings config)
        {
            streamWriter.WriteLine(REPORT_SECTION_ADDED_FILES);
            foreach (var newFileAbsolutePath in _fileDiffResultLists.AddedFilesAbsolutePath)
            {
                if (config.ShouldOutputFileTimestamps)
                {
                    streamWriter.WriteLine(string.Format(REPORT_ADDED_ITEM_WITH_TIMESTAMP, newFileAbsolutePath, Caching.TimestampCache.GetOrAdd(newFileAbsolutePath)));
                }
                else
                {
                    streamWriter.WriteLine(string.Format(REPORT_ADDED_ITEM, newFileAbsolutePath));
                }
            }
        }

        /// <summary>
        /// Removed Files セクションを書き込みます。
        /// </summary>
        /// <param name="streamWriter">出力先ライター。</param>
        /// <param name="config">設定オブジェクト。</param>
        private void WriteRemovedFilesSection(StreamWriter streamWriter, ConfigSettings config)
        {
            streamWriter.WriteLine(REPORT_SECTION_REMOVED_FILES);
            foreach (var oldFileAbsolutePath in _fileDiffResultLists.RemovedFilesAbsolutePath)
            {
                if (config.ShouldOutputFileTimestamps)
                {
                    streamWriter.WriteLine(string.Format(REPORT_REMOVED_ITEM_WITH_TIMESTAMP, oldFileAbsolutePath, Caching.TimestampCache.GetOrAdd(oldFileAbsolutePath)));
                }
                else
                {
                    streamWriter.WriteLine(string.Format(REPORT_REMOVED_ITEM, oldFileAbsolutePath));
                }
            }
        }

        /// <summary>
        /// Modified Files セクションを書き込みます。
        /// </summary>
        /// <param name="streamWriter">出力先ライター。</param>
        /// <param name="config">設定オブジェクト。</param>
        /// <param name="oldFolderAbsolutePath">旧フォルダの絶対パス。</param>
        /// <param name="newFolderAbsolutePath">新フォルダの絶対パス。</param>
        private void WriteModifiedFilesSection(StreamWriter streamWriter, ConfigSettings config, string oldFolderAbsolutePath, string newFolderAbsolutePath)
        {
            streamWriter.WriteLine(REPORT_SECTION_MODIFIED_FILES);
            foreach (var fileRelativePath in _fileDiffResultLists.ModifiedFilesRelativePath)
            {
                var diffDetail = _fileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                var diffDetailDisplay = BuildDiffDetailDisplay(fileRelativePath, diffDetail);
                if (config.ShouldOutputFileTimestamps)
                {
                    string oldTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, fileRelativePath));
                    string newTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, fileRelativePath));
                    streamWriter.WriteLine(string.Format(REPORT_MODIFIED_ITEM_WITH_TIMESTAMP, fileRelativePath, oldTimestamp, newTimestamp, diffDetailDisplay));
                }
                else
                {
                    streamWriter.WriteLine(string.Format(REPORT_MODIFIED_ITEM, fileRelativePath, diffDetailDisplay));
                }
            }
        }

        /// <summary>
        /// Summary セクションを書き込みます。
        /// </summary>
        /// <param name="streamWriter">出力先ライター。</param>
        /// <param name="config">設定オブジェクト。</param>
        /// <param name="hasMd5Mismatch">MD5Mismatch が存在する場合 true。</param>
        private void WriteSummarySection(StreamWriter streamWriter, ConfigSettings config, bool hasMd5Mismatch)
        {
            streamWriter.WriteLine(REPORT_SECTION_SUMMARY);
            if (config.ShouldIncludeIgnoredFiles)
            {
                streamWriter.WriteLine(string.Format(REPORT_SUMMARY_ITEM_FORMAT, REPORT_LABEL_IGNORED, _fileDiffResultLists.IgnoredFilesRelativePathToLocation.Count));
            }
            streamWriter.WriteLine(string.Format(REPORT_SUMMARY_ITEM_FORMAT, REPORT_LABEL_UNCHANGED, _fileDiffResultLists.UnchangedFilesRelativePath.Count));
            streamWriter.WriteLine(string.Format(REPORT_SUMMARY_ITEM_FORMAT, REPORT_LABEL_ADDED, _fileDiffResultLists.AddedFilesAbsolutePath.Count));
            streamWriter.WriteLine(string.Format(REPORT_SUMMARY_ITEM_FORMAT, REPORT_LABEL_REMOVED, _fileDiffResultLists.RemovedFilesAbsolutePath.Count));
            streamWriter.WriteLine(string.Format(REPORT_SUMMARY_ITEM_FORMAT, REPORT_LABEL_MODIFIED, _fileDiffResultLists.ModifiedFilesRelativePath.Count));
            streamWriter.WriteLine(string.Format(REPORT_SUMMARY_COMPARED, REPORT_LABEL_COMPARED, _fileDiffResultLists.OldFilesAbsolutePath.Count, _fileDiffResultLists.NewFilesAbsolutePath.Count));
            streamWriter.WriteLine();
            if (hasMd5Mismatch)
            {
                streamWriter.WriteLine(string.Format(REPORT_WARNING_LINE, WARNING_MD5_MISMATCH));
            }
        }

        /// <summary>
        /// 追加の警告セクションを書き込みます。
        /// </summary>
        private void WriteWarningsSection(StreamWriter streamWriter, bool hasTimestampRegressionWarning)
        {
            if (!hasTimestampRegressionWarning)
            {
                return;
            }

            streamWriter.WriteLine(REPORT_SECTION_WARNINGS);
            streamWriter.WriteLine(string.Format(REPORT_WARNING_LINE, WARNING_NEW_FILE_TIMESTAMP_OLDER_THAN_OLD));
            foreach (var warning in _fileDiffResultLists.NewFileTimestampOlderThanOldWarnings.Values
                .OrderBy(entry => entry.FileRelativePath, StringComparer.OrdinalIgnoreCase))
            {
                streamWriter.WriteLine(string.Format(REPORT_WARNING_TIMESTAMP_REGRESSION_ITEM, warning.FileRelativePath, warning.OldTimestamp, warning.NewTimestamp));
            }
        }

        /// <summary>
        /// Ignored ファイルの所在フラグから表示ラベルを返します。
        /// </summary>
        /// <param name="location">旧/新の所在フラグ。</param>
        /// <returns>表示ラベル文字列。</returns>
        private static string GetIgnoredFileLocationLabel(FileDiffResultLists.IgnoredFileLocation location)
        {
            return location switch
            {
                FileDiffResultLists.IgnoredFileLocation.Old => REPORT_LOCATION_OLD,
                FileDiffResultLists.IgnoredFileLocation.New => REPORT_LOCATION_NEW,
                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New => REPORT_LOCATION_BOTH,
                _ => string.Empty
            };
        }

        /// <summary>
        /// Ignored ファイル行に表示する更新日時情報を組み立てます。
        /// </summary>
        /// <param name="entry">相対パスと所在フラグの組。</param>
        /// <param name="oldFolderAbsolutePath">旧フォルダの絶対パス。</param>
        /// <param name="newFolderAbsolutePath">新フォルダの絶対パス。</param>
        /// <returns>更新日時情報。取得対象がない場合は null。</returns>
        private static string BuildIgnoredFileTimestampInfo(
            KeyValuePair<string, FileDiffResultLists.IgnoredFileLocation> entry,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath)
        {
            var timestampParts = new List<string>();
            if ((entry.Value & FileDiffResultLists.IgnoredFileLocation.Old) != 0)
            {
                timestampParts.Add(string.Format(REPORT_TIMESTAMP_UPDATED_OLD, Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, entry.Key))));
            }
            if ((entry.Value & FileDiffResultLists.IgnoredFileLocation.New) != 0)
            {
                timestampParts.Add(string.Format(REPORT_TIMESTAMP_UPDATED_NEW, Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, entry.Key))));
            }
            return timestampParts.Count > 0 ? string.Join(REPORT_TIMESTAMP_SEPARATOR, timestampParts) : null;
        }

        /// <summary>
        /// レポート冒頭に記載する逆アセンブラ一覧を組み立てます。
        /// 実際に観測されたラベル（実行/キャッシュ経由）だけを表示します。
        /// </summary>
        private string BuildDisassemblerHeaderText()
        {
            var observedLabels = _fileDiffResultLists.DisassemblerToolVersions.Keys
                .Concat(_fileDiffResultLists.DisassemblerToolVersionsFromCache.Keys)
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

        /// <summary>
        /// 表示ラベルを既知ツール順（dotnet-ildasm / ildasm / ilspycmd）で並べるためのソートキーを返します。
        /// </summary>
        private static int GetDisassemblerDisplayOrder(string label)
        {
            var toolName = ExtractToolName(label);
            if (string.Equals(toolName, Constants.DOTNET_ILDASM, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            if (string.Equals(toolName, Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            if (string.Equals(toolName, Constants.ILSPY_CMD, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }
            return 3;
        }

        /// <summary>
        /// 表示ラベルからツール名部分を抽出します。
        /// </summary>
        private static string ExtractToolName(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return string.Empty;
            }

            var versionIndex = label.IndexOf(" (version:", StringComparison.OrdinalIgnoreCase);
            return versionIndex >= 0 ? label.Substring(0, versionIndex).Trim() : label.Trim();
        }

        /// <summary>
        /// 判定根拠表示を組み立てます。IL 判定の場合は逆アセンブラ情報を追記します。
        /// </summary>
        private string BuildDiffDetailDisplay(string fileRelativePath, FileDiffResultLists.DiffDetailResult diffDetail)
        {
            if ((diffDetail == FileDiffResultLists.DiffDetailResult.ILMatch || diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch) &&
                _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(fileRelativePath, out var label) &&
                !string.IsNullOrWhiteSpace(label))
            {
                return $"`{diffDetail}` `{label}`";
            }
            return $"`{diffDetail}`";
        }

        /// <summary>
        /// IL 比較時に「含む」判定で除外対象とする文字列を正規化します（null/空白除外、trim、重複排除）。
        /// </summary>
        private static List<string> GetNormalizedIlIgnoreContainingStrings(ConfigSettings config)
        {
            if (config?.ILIgnoreLineContainingStrings == null)
            {
                return new List<string>();
            }

            return config.ILIgnoreLineContainingStrings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }
}
