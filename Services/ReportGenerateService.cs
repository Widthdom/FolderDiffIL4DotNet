using System;
using System.IO;
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
            try
            {
                Utility.ValidateAbsolutePathLengthOrThrow(diffReportAbsolutePath);
                File.Delete(diffReportAbsolutePath);

                using (var streamWriter = new StreamWriter(diffReportAbsolutePath))
                {
                    // ヘッダ
                    streamWriter.WriteLine("# Folder Diff Report");
                    streamWriter.WriteLine($"- App Version: FolderDiffIL4DotNet {appVersion}");
                    streamWriter.WriteLine($"- Old: {oldFolderAbsolutePath}");
                    streamWriter.WriteLine($"- New: {newFolderAbsolutePath}");
                    streamWriter.WriteLine($"- Ignored Extensions: {string.Join(", ", config.IgnoredExtensions)}");
                    streamWriter.WriteLine($"- Text File Extensions: {string.Join(", ", config.TextFileExtensions)}");
                    if (!string.IsNullOrWhiteSpace(elapsedTimeString))
                    {
                        streamWriter.WriteLine($"- Elapsed Time: {elapsedTimeString}");
                    }
                    streamWriter.WriteLine($"- {Constants.NOTE_MVID_SKIP}");

                    // Legend
                    streamWriter.WriteLine("- Legend:");
                    streamWriter.WriteLine($"  - `{FileDiffResultLists.DiffDetailResult.MD5Match}` / `{FileDiffResultLists.DiffDetailResult.MD5Mismatch}`: MD5 hash match / mismatch");
                    streamWriter.WriteLine($"  - `{FileDiffResultLists.DiffDetailResult.ILMatch}` / `{FileDiffResultLists.DiffDetailResult.ILMismatch}`: IL(Intermediate Language) match / mismatch");
                    streamWriter.WriteLine($"  - `{FileDiffResultLists.DiffDetailResult.TextMatch}` / `{FileDiffResultLists.DiffDetailResult.TextMismatch}`: Text match / mismatch");

                    // ボディ - Unchanged Files -
                    // Unchanged Files はアプリ設定で出力可否を制御。
                    // ShouldOutputFileTimestamps が true の場合のみ、
                    // 判定根拠が ILMatch のときは新旧両方の最終更新日時を、
                    // それ以外は新の最終更新日時を併記します。
                    if (config.ShouldIncludeUnchangedFiles)
                    {
                        streamWriter.WriteLine("\n## [ = ] Unchanged Files");
                        foreach (var fileRelativePath in FileDiffResultLists.UnchangedFilesRelativePath)
                        {
                            var diffDetail = FileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                            if (config.ShouldOutputFileTimestamps)
                            {
                                string oldFileTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, fileRelativePath));
                                string newFileTimestamp = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, fileRelativePath));
                                string updateInfo = diffDetail == FileDiffResultLists.DiffDetailResult.ILMatch
                                    ? $"(updated_old: {oldFileTimestamp}, updated_new: {newFileTimestamp})"
                                    : $"(updated: {newFileTimestamp})";
                                streamWriter.WriteLine($"- [ = ] {fileRelativePath} <u>{updateInfo}</u> `{diffDetail}`");
                            }
                            else
                            {
                                streamWriter.WriteLine($"- [ = ] {fileRelativePath} `{diffDetail}`");
                            }
                        }
                    }

                    // ボディ - Added Files -
                    streamWriter.WriteLine("\n## [ + ] Added Files");
                    foreach (var newFileAbsolutePath in FileDiffResultLists.AddedFilesAbsolutePath)
                    {
                        if (config.ShouldOutputFileTimestamps)
                        {
                            streamWriter.WriteLine($"- [ + ] {newFileAbsolutePath} <u>(updated: {Caching.TimestampCache.GetOrAdd(newFileAbsolutePath)})</u>");
                        }
                        else
                        {
                            streamWriter.WriteLine($"- [ + ] {newFileAbsolutePath}");
                        }
                    }

                    // ボディ - Removed Files -
                    streamWriter.WriteLine("\n## [ - ] Removed Files");
                    foreach (var oldFileAbsolutePath in FileDiffResultLists.RemovedFilesAbsolutePath)
                    {
                        if (config.ShouldOutputFileTimestamps)
                        {
                            streamWriter.WriteLine($"- [ - ] {oldFileAbsolutePath} <u>(updated: {Caching.TimestampCache.GetOrAdd(oldFileAbsolutePath)})</u>");
                        }
                        else
                        {
                            streamWriter.WriteLine($"- [ - ] {oldFileAbsolutePath}");
                        }
                    }

                    // ボディ - Modified Files -
                    streamWriter.WriteLine("\n## [ * ] Modified Files");
                    foreach (var fileRelativePath in FileDiffResultLists.ModifiedFilesRelativePath)
                    {
                        var diffDetail = FileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                        if (config.ShouldOutputFileTimestamps)
                        {
                            streamWriter.WriteLine($"- [ * ] {fileRelativePath} <u>(updated_old: {Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, fileRelativePath))}, updated_new: {Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, fileRelativePath))})</u> `{diffDetail}`");
                        }
                        else
                        {
                            streamWriter.WriteLine($"- [ * ] {fileRelativePath} `{diffDetail}`");
                        }
                    }

                    // フッタ
                    streamWriter.WriteLine("\n## Summary");
                    streamWriter.WriteLine($"- Unchanged : {FileDiffResultLists.UnchangedFilesRelativePath.Count}");
                    streamWriter.WriteLine($"- Added     : {FileDiffResultLists.AddedFilesAbsolutePath.Count}");
                    streamWriter.WriteLine($"- Removed   : {FileDiffResultLists.RemovedFilesAbsolutePath.Count}");
                    streamWriter.WriteLine($"- Modified  : {FileDiffResultLists.ModifiedFilesRelativePath.Count}");
                    streamWriter.WriteLine($"- Compared  : {FileDiffResultLists.OldFilesAbsolutePath.Count} (Old) vs {FileDiffResultLists.NewFilesAbsolutePath.Count} (New)");
                }
            }
            catch (Exception)
            {
                LoggerService.LogMessage($"[ERROR] Failed to output report to '{diffReportAbsolutePath}'", shouldOutputMessageToConsole: true);
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
                    LoggerService.LogMessage($"[WARNING] {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
            }
        }
    }
}
