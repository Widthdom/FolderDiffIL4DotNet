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
    /// 差分結果の Markdown レポート (<see cref="Constants.DIFF_REPORT_FILE_NAME"/>) を生成するサービス。
    /// </summary>
    public sealed class ReportGenerateService
    {
        /// <summary>
        /// <see cref="Constants.DIFF_REPORT_FILE_NAME"/> を生成します。
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
            string diffReportAbsolutePath = Path.Combine(reportsFolderAbsolutePath, Constants.DIFF_REPORT_FILE_NAME);
            bool hasMd5Mismatch = FileDiffResultLists.HasAnyMd5Mismatch;
            if (hasMd5Mismatch)
            {
                LoggerService.LogMessage(LoggerService.LogLevel.Warning, Constants.WARNING_MD5_MISMATCH, shouldOutputMessageToConsole: true);
            }
            using var spinner = new ConsoleSpinner(Constants.SPINNER_LABEL_GENERATING_REPORT);
            var reportGenerated = false;
            try
            {
                Utility.ValidateAbsolutePathLengthOrThrow(diffReportAbsolutePath);
                File.Delete(diffReportAbsolutePath);

                using (var streamWriter = new StreamWriter(diffReportAbsolutePath))
                {
                    // ヘッダ
                    streamWriter.WriteLine(Constants.REPORT_TITLE);
                    streamWriter.WriteLine(string.Format(Constants.REPORT_HEADER_APP_VERSION, appVersion));
                    streamWriter.WriteLine(string.Format(Constants.REPORT_HEADER_OLD, oldFolderAbsolutePath));
                    streamWriter.WriteLine(string.Format(Constants.REPORT_HEADER_NEW, newFolderAbsolutePath));
                    streamWriter.WriteLine(string.Format(Constants.REPORT_HEADER_IGNORED_EXTENSIONS, string.Join(Constants.REPORT_LIST_SEPARATOR, config.IgnoredExtensions)));
                    streamWriter.WriteLine(string.Format(Constants.REPORT_HEADER_TEXT_EXTENSIONS, string.Join(Constants.REPORT_LIST_SEPARATOR, config.TextFileExtensions)));
                    if (!string.IsNullOrWhiteSpace(elapsedTimeString))
                    {
                        streamWriter.WriteLine(string.Format(Constants.REPORT_HEADER_ELAPSED_TIME, elapsedTimeString));
                    }
                    streamWriter.WriteLine("- " + Constants.NOTE_MVID_SKIP);

                    // Legend
                    streamWriter.WriteLine(Constants.REPORT_LEGEND_HEADER);
                    streamWriter.WriteLine(string.Format(Constants.REPORT_LEGEND_MD5, FileDiffResultLists.DiffDetailResult.MD5Match, FileDiffResultLists.DiffDetailResult.MD5Mismatch));
                    streamWriter.WriteLine(string.Format(Constants.REPORT_LEGEND_IL, FileDiffResultLists.DiffDetailResult.ILMatch, FileDiffResultLists.DiffDetailResult.ILMismatch));
                    streamWriter.WriteLine(string.Format(Constants.REPORT_LEGEND_TEXT, FileDiffResultLists.DiffDetailResult.TextMatch, FileDiffResultLists.DiffDetailResult.TextMismatch));

                    // ボディ - Ignored Files -
                    // Ignored Files はアプリ設定（ShouldIncludeIgnoredFiles）が true の場合のみ出力。
                    // ShouldOutputFileTimestamps が true なら旧/新それぞれの存在箇所に応じた最終更新日時を併記し、
                    // 旧/新のどちらに存在したかもラベル（(old)/(new)/(old/new)）で明示します。
                    if (config.ShouldIncludeIgnoredFiles && FileDiffResultLists.IgnoredFilesRelativePathToLocation.Count > 0)
                    {
                        streamWriter.WriteLine(Constants.REPORT_SECTION_IGNORED_FILES);
                        foreach (var entry in FileDiffResultLists.IgnoredFilesRelativePathToLocation.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
                        {
                            var locationLabel = entry.Value switch
                            {
                                FileDiffResultLists.IgnoredFileLocation.Old => Constants.REPORT_LOCATION_OLD,
                                FileDiffResultLists.IgnoredFileLocation.New => Constants.REPORT_LOCATION_NEW,
                                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New => Constants.REPORT_LOCATION_BOTH,
                                _ => string.Empty
                            };

                            string timestampInfo = null;
                            if (config.ShouldOutputFileTimestamps)
                            {
                                var timestampParts = new List<string>();
                                if ((entry.Value & FileDiffResultLists.IgnoredFileLocation.Old) != 0)
                                {
                                    timestampParts.Add(string.Format(Constants.REPORT_TIMESTAMP_UPDATED_OLD, Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, entry.Key))));
                                }
                                if ((entry.Value & FileDiffResultLists.IgnoredFileLocation.New) != 0)
                                {
                                    timestampParts.Add(string.Format(Constants.REPORT_TIMESTAMP_UPDATED_NEW, Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, entry.Key))));
                                }
                                timestampInfo = timestampParts.Count > 0 ? string.Join(Constants.REPORT_TIMESTAMP_SEPARATOR, timestampParts) : null;
                            }

                            var line = string.Format(Constants.REPORT_IGNORED_FILE_ITEM, entry.Key);
                            if (!string.IsNullOrEmpty(locationLabel))
                            {
                                line += " " + locationLabel;
                            }
                            if (config.ShouldOutputFileTimestamps && !string.IsNullOrEmpty(timestampInfo))
                            {
                                line += string.Format(Constants.REPORT_TIMESTAMP_HTML_WRAPPER, timestampInfo);
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
                        streamWriter.WriteLine(Constants.REPORT_SECTION_UNCHANGED_FILES);
                        foreach (var fileRelativePath in FileDiffResultLists.UnchangedFilesRelativePath)
                        {
                            var diffDetail = FileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                            if (config.ShouldOutputFileTimestamps)
                            {
                                string oldFileTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, fileRelativePath));
                                string newFileTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, fileRelativePath));
                                string updateInfo = diffDetail == FileDiffResultLists.DiffDetailResult.ILMatch
                                    ? string.Format(Constants.REPORT_UNCHANGED_TIMESTAMP_BOTH, oldFileTimestamp, newFileTimestamp)
                                    : string.Format(Constants.REPORT_UNCHANGED_TIMESTAMP_NEW, newFileTimestamp);
                                streamWriter.WriteLine(string.Format(Constants.REPORT_UNCHANGED_ITEM_WITH_TIMESTAMP, fileRelativePath, updateInfo, diffDetail));
                            }
                            else
                            {
                                streamWriter.WriteLine(string.Format(Constants.REPORT_UNCHANGED_ITEM, fileRelativePath, diffDetail));
                            }
                        }
                    }

                    // ボディ - Added Files -
                    streamWriter.WriteLine(Constants.REPORT_SECTION_ADDED_FILES);
                    foreach (var newFileAbsolutePath in FileDiffResultLists.AddedFilesAbsolutePath)
                    {
                        if (config.ShouldOutputFileTimestamps)
                        {
                            streamWriter.WriteLine(string.Format(Constants.REPORT_ADDED_ITEM_WITH_TIMESTAMP, newFileAbsolutePath, Caching.TimestampCache.GetOrAdd(newFileAbsolutePath)));
                        }
                        else
                        {
                            streamWriter.WriteLine(string.Format(Constants.REPORT_ADDED_ITEM, newFileAbsolutePath));
                        }
                    }

                    // ボディ - Removed Files -
                    streamWriter.WriteLine(Constants.REPORT_SECTION_REMOVED_FILES);
                    foreach (var oldFileAbsolutePath in FileDiffResultLists.RemovedFilesAbsolutePath)
                    {
                        if (config.ShouldOutputFileTimestamps)
                        {
                            streamWriter.WriteLine(string.Format(Constants.REPORT_REMOVED_ITEM_WITH_TIMESTAMP, oldFileAbsolutePath, Caching.TimestampCache.GetOrAdd(oldFileAbsolutePath)));
                        }
                        else
                        {
                            streamWriter.WriteLine(string.Format(Constants.REPORT_REMOVED_ITEM, oldFileAbsolutePath));
                        }
                    }

                    // ボディ - Modified Files -
                    streamWriter.WriteLine(Constants.REPORT_SECTION_MODIFIED_FILES);
                    foreach (var fileRelativePath in FileDiffResultLists.ModifiedFilesRelativePath)
                    {
                        var diffDetail = FileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                        if (config.ShouldOutputFileTimestamps)
                        {
                            string oldTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, fileRelativePath));
                            string newTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, fileRelativePath));
                            streamWriter.WriteLine(string.Format(Constants.REPORT_MODIFIED_ITEM_WITH_TIMESTAMP, fileRelativePath, oldTimestamp, newTimestamp, diffDetail));
                        }
                        else
                        {
                            streamWriter.WriteLine(string.Format(Constants.REPORT_MODIFIED_ITEM, fileRelativePath, diffDetail));
                        }
                    }

                    // フッタ
                    streamWriter.WriteLine(Constants.REPORT_SECTION_SUMMARY);
                    if (config.ShouldIncludeIgnoredFiles)
                    {
                        streamWriter.WriteLine(string.Format(Constants.REPORT_SUMMARY_ITEM_FORMAT, Constants.REPORT_LABEL_IGNORED, FileDiffResultLists.IgnoredFilesRelativePathToLocation.Count));
                    }
                    streamWriter.WriteLine(string.Format(Constants.REPORT_SUMMARY_ITEM_FORMAT, Constants.REPORT_LABEL_UNCHANGED, FileDiffResultLists.UnchangedFilesRelativePath.Count));
                    streamWriter.WriteLine(string.Format(Constants.REPORT_SUMMARY_ITEM_FORMAT, Constants.REPORT_LABEL_ADDED, FileDiffResultLists.AddedFilesAbsolutePath.Count));
                    streamWriter.WriteLine(string.Format(Constants.REPORT_SUMMARY_ITEM_FORMAT, Constants.REPORT_LABEL_REMOVED, FileDiffResultLists.RemovedFilesAbsolutePath.Count));
                    streamWriter.WriteLine(string.Format(Constants.REPORT_SUMMARY_ITEM_FORMAT, Constants.REPORT_LABEL_MODIFIED, FileDiffResultLists.ModifiedFilesRelativePath.Count));
                    streamWriter.WriteLine(string.Format(Constants.REPORT_SUMMARY_COMPARED, Constants.REPORT_LABEL_COMPARED, FileDiffResultLists.OldFilesAbsolutePath.Count, FileDiffResultLists.NewFilesAbsolutePath.Count));
                    streamWriter.WriteLine();
                    if (hasMd5Mismatch)
                    {
                        streamWriter.WriteLine(string.Format(Constants.REPORT_WARNING_LINE, Constants.WARNING_MD5_MISMATCH));
                    }
                }
                reportGenerated = true;
            }
            catch (Exception)
            {
                LoggerService.LogMessage(LoggerService.LogLevel.Error, string.Format(Constants.ERROR_FAILED_TO_OUTPUT_REPORT, diffReportAbsolutePath), shouldOutputMessageToConsole: true);
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
                spinner.Complete(reportGenerated ? Constants.LOG_REPORT_GENERATION_COMPLETED : null);
            }
        }
    }
}
