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
        private const string REPORT_UNCHANGED_ITEM_WITH_TIMESTAMP = "- " + REPORT_MARKER_UNCHANGED + " {0} <u>{1}</u> `{2}`";

        /// <summary>
        /// Unchanged ファイル行（タイムスタンプなし）
        /// </summary>
        private const string REPORT_UNCHANGED_ITEM = "- " + REPORT_MARKER_UNCHANGED + " {0} `{1}`";

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
        private const string REPORT_MODIFIED_ITEM_WITH_TIMESTAMP = "- " + REPORT_MARKER_MODIFIED + " {0} <u>(updated_old: {1}, updated_new: {2})</u> `{3}`";

        /// <summary>
        /// Modified ファイル行（タイムスタンプなし）
        /// </summary>
        private const string REPORT_MODIFIED_ITEM = "- " + REPORT_MARKER_MODIFIED + " {0} `{1}`";

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
        /// <param name="config">設定オブジェクト</param>
        /// <exception cref="Exception">入出力エラーなど予期しない例外</exception>
        public void GenerateDiffReport(
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            ConfigSettings config)
        {
            string diffReportAbsolutePath = Path.Combine(reportsFolderAbsolutePath, DIFF_REPORT_FILE_NAME);
            bool hasMd5Mismatch = FileDiffResultLists.HasAnyMd5Mismatch;
            if (hasMd5Mismatch)
            {
                LoggerService.LogMessage(LoggerService.LogLevel.Warning, WARNING_MD5_MISMATCH, shouldOutputMessageToConsole: true);
            }
            using var spinner = new ConsoleSpinner(SPINNER_LABEL_GENERATING_REPORT);
            var reportGenerated = false;
            try
            {
                Utility.ValidateAbsolutePathLengthOrThrow(diffReportAbsolutePath);
                File.Delete(diffReportAbsolutePath);

                using (var streamWriter = new StreamWriter(diffReportAbsolutePath))
                {
                    // ヘッダ
                    streamWriter.WriteLine(REPORT_TITLE);
                    streamWriter.WriteLine(string.Format(REPORT_HEADER_APP_VERSION, appVersion));
                    streamWriter.WriteLine(string.Format(REPORT_HEADER_OLD, oldFolderAbsolutePath));
                    streamWriter.WriteLine(string.Format(REPORT_HEADER_NEW, newFolderAbsolutePath));
                    streamWriter.WriteLine(string.Format(REPORT_HEADER_IGNORED_EXTENSIONS, string.Join(REPORT_LIST_SEPARATOR, config.IgnoredExtensions)));
                    streamWriter.WriteLine(string.Format(REPORT_HEADER_TEXT_EXTENSIONS, string.Join(REPORT_LIST_SEPARATOR, config.TextFileExtensions)));
                    if (!string.IsNullOrWhiteSpace(elapsedTimeString))
                    {
                        streamWriter.WriteLine(string.Format(REPORT_HEADER_ELAPSED_TIME, elapsedTimeString));
                    }
                    streamWriter.WriteLine("- " + NOTE_MVID_SKIP);

                    // Legend
                    streamWriter.WriteLine(REPORT_LEGEND_HEADER);
                    streamWriter.WriteLine(string.Format(REPORT_LEGEND_MD5, FileDiffResultLists.DiffDetailResult.MD5Match, FileDiffResultLists.DiffDetailResult.MD5Mismatch));
                    streamWriter.WriteLine(string.Format(REPORT_LEGEND_IL, FileDiffResultLists.DiffDetailResult.ILMatch, FileDiffResultLists.DiffDetailResult.ILMismatch));
                    streamWriter.WriteLine(string.Format(REPORT_LEGEND_TEXT, FileDiffResultLists.DiffDetailResult.TextMatch, FileDiffResultLists.DiffDetailResult.TextMismatch));

                    // ボディ - Ignored Files -
                    // Ignored Files はアプリ設定（ShouldIncludeIgnoredFiles）が true の場合のみ出力。
                    // ShouldOutputFileTimestamps が true なら旧/新それぞれの存在箇所に応じた最終更新日時を併記し、
                    // 旧/新のどちらに存在したかもラベル（(old)/(new)/(old/new)）で明示します。
                    if (config.ShouldIncludeIgnoredFiles && FileDiffResultLists.IgnoredFilesRelativePathToLocation.Count > 0)
                    {
                        streamWriter.WriteLine(REPORT_SECTION_IGNORED_FILES);
                        foreach (var entry in FileDiffResultLists.IgnoredFilesRelativePathToLocation.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
                        {
                            var locationLabel = entry.Value switch
                            {
                                FileDiffResultLists.IgnoredFileLocation.Old => REPORT_LOCATION_OLD,
                                FileDiffResultLists.IgnoredFileLocation.New => REPORT_LOCATION_NEW,
                                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New => REPORT_LOCATION_BOTH,
                                _ => string.Empty
                            };

                            string timestampInfo = null;
                            if (config.ShouldOutputFileTimestamps)
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
                                timestampInfo = timestampParts.Count > 0 ? string.Join(REPORT_TIMESTAMP_SEPARATOR, timestampParts) : null;
                            }

                            var line = string.Format(REPORT_IGNORED_FILE_ITEM, entry.Key);
                            if (!string.IsNullOrEmpty(locationLabel))
                            {
                                line += " " + locationLabel;
                            }
                            if (config.ShouldOutputFileTimestamps && !string.IsNullOrEmpty(timestampInfo))
                            {
                                line += string.Format(REPORT_TIMESTAMP_HTML_WRAPPER, timestampInfo);
                            }
                            streamWriter.WriteLine(line);
                        }
                    }

                    // ボディ - Unchanged Files -
                    // Unchanged Files はアプリ設定で出力可否を制御。
                    // ShouldOutputFileTimestamps が true の場合のみ、
                    // 判定根拠が ILMatch のときは新旧両方の最終更新日時を、
                    // それ以外は新の最終更新日時を併記します。
                    if (config.ShouldIncludeUnchangedFiles)
                    {
                        streamWriter.WriteLine(REPORT_SECTION_UNCHANGED_FILES);
                        foreach (var fileRelativePath in FileDiffResultLists.UnchangedFilesRelativePath)
                        {
                            var diffDetail = FileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                            if (config.ShouldOutputFileTimestamps)
                            {
                                string oldFileTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, fileRelativePath));
                                string newFileTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, fileRelativePath));
                                string updateInfo = diffDetail == FileDiffResultLists.DiffDetailResult.ILMatch
                                    ? string.Format(REPORT_UNCHANGED_TIMESTAMP_BOTH, oldFileTimestamp, newFileTimestamp)
                                    : string.Format(REPORT_UNCHANGED_TIMESTAMP_NEW, newFileTimestamp);
                                streamWriter.WriteLine(string.Format(REPORT_UNCHANGED_ITEM_WITH_TIMESTAMP, fileRelativePath, updateInfo, diffDetail));
                            }
                            else
                            {
                                streamWriter.WriteLine(string.Format(REPORT_UNCHANGED_ITEM, fileRelativePath, diffDetail));
                            }
                        }
                    }

                    // ボディ - Added Files -
                    streamWriter.WriteLine(REPORT_SECTION_ADDED_FILES);
                    foreach (var newFileAbsolutePath in FileDiffResultLists.AddedFilesAbsolutePath)
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

                    // ボディ - Removed Files -
                    streamWriter.WriteLine(REPORT_SECTION_REMOVED_FILES);
                    foreach (var oldFileAbsolutePath in FileDiffResultLists.RemovedFilesAbsolutePath)
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

                    // ボディ - Modified Files -
                    streamWriter.WriteLine(REPORT_SECTION_MODIFIED_FILES);
                    foreach (var fileRelativePath in FileDiffResultLists.ModifiedFilesRelativePath)
                    {
                        var diffDetail = FileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                        if (config.ShouldOutputFileTimestamps)
                        {
                            string oldTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, fileRelativePath));
                            string newTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, fileRelativePath));
                            streamWriter.WriteLine(string.Format(REPORT_MODIFIED_ITEM_WITH_TIMESTAMP, fileRelativePath, oldTimestamp, newTimestamp, diffDetail));
                        }
                        else
                        {
                            streamWriter.WriteLine(string.Format(REPORT_MODIFIED_ITEM, fileRelativePath, diffDetail));
                        }
                    }

                    // フッタ
                    streamWriter.WriteLine(REPORT_SECTION_SUMMARY);
                    if (config.ShouldIncludeIgnoredFiles)
                    {
                        streamWriter.WriteLine(string.Format(REPORT_SUMMARY_ITEM_FORMAT, REPORT_LABEL_IGNORED, FileDiffResultLists.IgnoredFilesRelativePathToLocation.Count));
                    }
                    streamWriter.WriteLine(string.Format(REPORT_SUMMARY_ITEM_FORMAT, REPORT_LABEL_UNCHANGED, FileDiffResultLists.UnchangedFilesRelativePath.Count));
                    streamWriter.WriteLine(string.Format(REPORT_SUMMARY_ITEM_FORMAT, REPORT_LABEL_ADDED, FileDiffResultLists.AddedFilesAbsolutePath.Count));
                    streamWriter.WriteLine(string.Format(REPORT_SUMMARY_ITEM_FORMAT, REPORT_LABEL_REMOVED, FileDiffResultLists.RemovedFilesAbsolutePath.Count));
                    streamWriter.WriteLine(string.Format(REPORT_SUMMARY_ITEM_FORMAT, REPORT_LABEL_MODIFIED, FileDiffResultLists.ModifiedFilesRelativePath.Count));
                    streamWriter.WriteLine(string.Format(REPORT_SUMMARY_COMPARED, REPORT_LABEL_COMPARED, FileDiffResultLists.OldFilesAbsolutePath.Count, FileDiffResultLists.NewFilesAbsolutePath.Count));
                    streamWriter.WriteLine();
                    if (hasMd5Mismatch)
                    {
                        streamWriter.WriteLine(string.Format(REPORT_WARNING_LINE, WARNING_MD5_MISMATCH));
                    }
                }
                reportGenerated = true;
            }
            catch (Exception)
            {
                LoggerService.LogMessage(LoggerService.LogLevel.Error, string.Format(ERROR_FAILED_TO_OUTPUT_REPORT, diffReportAbsolutePath), shouldOutputMessageToConsole: true);
                throw;
            }
            finally
            {
                // レポートファイルの読み取り専用属性を設定（失敗した場合は警告を出力し、処理は継続）
                try
                {
                    Utility.TrySetReadOnly(diffReportAbsolutePath);
                }
                catch (Exception ex)
                {
                    LoggerService.LogMessage(LoggerService.LogLevel.Warning, ex.Message, shouldOutputMessageToConsole: true, ex);
                }
                spinner.Complete(reportGenerated ? LOG_REPORT_GENERATION_COMPLETED : null);
            }
        }
    }
}
