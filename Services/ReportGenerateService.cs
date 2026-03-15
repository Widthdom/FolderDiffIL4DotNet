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
    /// 差分結果の Markdown レポート (<see cref="DIFF_REPORT_FILE_NAME"/>) を生成するサービス。
    /// </summary>
    public sealed class ReportGenerateService
    {
        /// <summary>
        /// 差分結果保持オブジェクト。
        /// </summary>
        private readonly FileDiffResultLists _fileDiffResultLists;

        /// <summary>
        /// ログ出力サービス。
        /// </summary>
        private readonly ILoggerService _logger;

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
        /// <summary>
        /// フォルダ差分の概要を出力する Markdown レポートのファイル名
        /// </summary>
        private const string DIFF_REPORT_FILE_NAME = "diff_report.md";

        /// <summary>
        /// レポート生成スピナーのラベル。
        /// </summary>
        private const string SPINNER_LABEL_GENERATING_REPORT = "Generating report";

        /// <summary>
        /// レポートタイトル
        /// </summary>
        private const string REPORT_TITLE = "# Folder Diff Report";

        /// <summary>
        /// 逆アセンブラ未使用時の表示。
        /// </summary>
        private const string REPORT_DISASSEMBLER_NOT_USED = "N/A";

        /// <summary>
        /// レポート内でのリスト結合区切り
        /// </summary>
        private const string REPORT_LIST_SEPARATOR = ", ";

        /// <summary>
        /// MVID行スキップの但し書き（存在する場合のみ対象）。
        /// </summary>
        private const string NOTE_MVID_SKIP = $"Note: When diffing {Constants.LABEL_IL}, lines starting with \"{Constants.IL_MVID_LINE_PREFIX}\" (if present) are ignored because they contain disassembler-emitted Module Version ID metadata that can change on rebuild without meaning the executable IL changed.";

        /// <summary>
        /// 部分一致除外が有効だが、文字列設定が空の場合の但し書き。
        /// </summary>
        private const string NOTE_IL_CONTAINS_SKIP_ENABLED_BUT_EMPTY = "Note: IL line-ignore-by-contains is enabled, but no non-empty strings are configured.";

        /// <summary>
        /// レジェンドのヘッダ
        /// </summary>
        private const string REPORT_LEGEND_HEADER = "- Legend:";

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
        /// タイムスタンプ結合時の区切り
        /// </summary>
        private const string REPORT_TIMESTAMP_SEPARATOR = ", ";

        /// <summary>
        /// レポートフッタ: Summary セクション
        /// </summary>
        private const string REPORT_SECTION_SUMMARY = REPORT_SECTION_PREFIX + "Summary";

        /// <summary>
        /// レポートセクション: IL Cache Stats
        /// </summary>
        private const string REPORT_SECTION_IL_CACHE_STATS = REPORT_SECTION_PREFIX + "IL Cache Stats";

        /// <summary>
        /// new 側の更新日時逆転警告文言
        /// </summary>
        private const string WARNING_NEW_FILE_TIMESTAMP_OLDER_THAN_OLD = "One or more files in `new` have older last-modified timestamps than the corresponding files in `old`.";

        /// <summary>
        /// 警告セクション
        /// </summary>
        private const string REPORT_SECTION_WARNINGS = REPORT_SECTION_PREFIX + "Warnings";

        /// <summary>
        /// レポート生成完了ログ。
        /// </summary>
        private const string LOG_REPORT_GENERATION_COMPLETED = "Report generation completed.";
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
        /// <param name="ilCache">IL キャッシュインスタンス（null の場合は IL Cache Stats セクションを出力しない）</param>
        /// <exception cref="ArgumentException">出力先パスが無効、または長さ検証に失敗した場合。</exception>
        /// <exception cref="IOException">レポートファイルの削除または書き込みに失敗した場合。</exception>
        /// <exception cref="UnauthorizedAccessException">レポート出力先へのアクセス権が不足している場合。</exception>
        /// <exception cref="NotSupportedException">出力先パスの形式がサポートされない場合。</exception>
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
            using var spinner = new ConsoleSpinner(SPINNER_LABEL_GENERATING_REPORT);
            var reportGenerated = false;
            try
            {
                // diff_report.md は最終成果物なので、削除/書き込み/パス検証に失敗したら継続せず再スローする。
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
                // 書き込み後の読み取り専用化は best-effort。失敗してもレポート自体は既に利用可能なので warning のみ。
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
            WriteReportHeader(streamWriter, oldFolderAbsolutePath, newFolderAbsolutePath, appVersion, elapsedTimeString, computerName, config);
            WriteLegend(streamWriter);
            WriteIgnoredFilesSection(streamWriter, config, oldFolderAbsolutePath, newFolderAbsolutePath);
            WriteUnchangedFilesSection(streamWriter, config, oldFolderAbsolutePath, newFolderAbsolutePath);
            WriteAddedFilesSection(streamWriter, config);
            WriteRemovedFilesSection(streamWriter, config);
            WriteModifiedFilesSection(streamWriter, config, oldFolderAbsolutePath, newFolderAbsolutePath);
            WriteSummarySection(streamWriter, config);
            WriteILCacheStatsSection(streamWriter, config, ilCache);
            WriteWarningsSection(streamWriter, hasMd5Mismatch, hasTimestampRegressionWarning);
        }

        private void LogReportOutputFailure(string diffReportAbsolutePath)
        {
            _logger.LogMessage(AppLogLevel.Error, $"Failed to output report to '{diffReportAbsolutePath}'", shouldOutputMessageToConsole: true);
        }

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
            streamWriter.WriteLine($"- App Version: FolderDiffIL4DotNet {appVersion}");
            streamWriter.WriteLine($"- Computer: {computerName}");
            streamWriter.WriteLine($"- Old: {oldFolderAbsolutePath}");
            streamWriter.WriteLine($"- New: {newFolderAbsolutePath}");
            streamWriter.WriteLine($"- Ignored Extensions: {string.Join(REPORT_LIST_SEPARATOR, config.IgnoredExtensions)}");
            streamWriter.WriteLine($"- Text File Extensions: {string.Join(REPORT_LIST_SEPARATOR, config.TextFileExtensions)}");
            var disassemblerText = BuildDisassemblerHeaderText();
            streamWriter.WriteLine($"- IL Disassembler: {disassemblerText}");
            if (!string.IsNullOrWhiteSpace(elapsedTimeString))
            {
                streamWriter.WriteLine($"- Elapsed Time: {elapsedTimeString}");
            }
            streamWriter.WriteLine("- " + NOTE_MVID_SKIP);
            if (!config.ShouldIgnoreILLinesContainingConfiguredStrings)
            {
                return;
            }

            var ilIgnoreContainingStrings = GetNormalizedIlIgnoreContainingStrings(config);
            streamWriter.WriteLine("- " + (ilIgnoreContainingStrings.Count == 0
                ? NOTE_IL_CONTAINS_SKIP_ENABLED_BUT_EMPTY
                : $"Note: When diffing {Constants.LABEL_IL}, lines containing any of the configured strings are ignored: {string.Join(REPORT_LIST_SEPARATOR, ilIgnoreContainingStrings.Select(value => $"\"{value}\""))}."));
        }

        /// <summary>
        /// 判定根拠ラベルの凡例（Legend）を書き込みます。
        /// </summary>
        /// <param name="streamWriter">出力先ライター。</param>
        private static void WriteLegend(StreamWriter streamWriter)
        {
            streamWriter.WriteLine(REPORT_LEGEND_HEADER);
            streamWriter.WriteLine($"  - `{FileDiffResultLists.DiffDetailResult.MD5Match}` / `{FileDiffResultLists.DiffDetailResult.MD5Mismatch}`: MD5 hash match / mismatch");
            streamWriter.WriteLine($"  - `{FileDiffResultLists.DiffDetailResult.ILMatch}` / `{FileDiffResultLists.DiffDetailResult.ILMismatch}`: IL(Intermediate Language) match / mismatch");
            streamWriter.WriteLine($"  - `{FileDiffResultLists.DiffDetailResult.TextMatch}` / `{FileDiffResultLists.DiffDetailResult.TextMismatch}`: Text match / mismatch");
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
                var line = $"- [ x ] {entry.Key}";
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
                        line += $" <u>({timestampInfo})</u>";
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
                        ? $"(updated_old: {oldFileTimestamp}, updated_new: {newFileTimestamp})"
                        : $"(updated: {newFileTimestamp})";
                    streamWriter.WriteLine($"- [ = ] {fileRelativePath} <u>{updateInfo}</u> {diffDetailDisplay}");
                }
                else
                {
                    streamWriter.WriteLine($"- [ = ] {fileRelativePath} {diffDetailDisplay}");
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
                    streamWriter.WriteLine($"- [ + ] {newFileAbsolutePath} <u>(updated: {Caching.TimestampCache.GetOrAdd(newFileAbsolutePath)})</u>");
                }
                else
                {
                    streamWriter.WriteLine($"- [ + ] {newFileAbsolutePath}");
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
                    streamWriter.WriteLine($"- [ - ] {oldFileAbsolutePath} <u>(updated: {Caching.TimestampCache.GetOrAdd(oldFileAbsolutePath)})</u>");
                }
                else
                {
                    streamWriter.WriteLine($"- [ - ] {oldFileAbsolutePath}");
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
                    streamWriter.WriteLine($"- [ * ] {fileRelativePath} <u>(updated_old: {oldTimestamp}, updated_new: {newTimestamp})</u> {diffDetailDisplay}");
                }
                else
                {
                    streamWriter.WriteLine($"- [ * ] {fileRelativePath} {diffDetailDisplay}");
                }
            }
        }

        /// <summary>
        /// Summary セクションを書き込みます。
        /// </summary>
        /// <param name="streamWriter">出力先ライター。</param>
        /// <param name="config">設定オブジェクト。</param>
        private void WriteSummarySection(StreamWriter streamWriter, ConfigSettings config)
        {
            streamWriter.WriteLine(REPORT_SECTION_SUMMARY);
            if (config.ShouldIncludeIgnoredFiles)
            {
                streamWriter.WriteLine($"- {REPORT_LABEL_IGNORED,-10}: {_fileDiffResultLists.IgnoredFilesRelativePathToLocation.Count}");
            }
            streamWriter.WriteLine($"- {REPORT_LABEL_UNCHANGED,-10}: {_fileDiffResultLists.UnchangedFilesRelativePath.Count}");
            streamWriter.WriteLine($"- {REPORT_LABEL_ADDED,-10}: {_fileDiffResultLists.AddedFilesAbsolutePath.Count}");
            streamWriter.WriteLine($"- {REPORT_LABEL_REMOVED,-10}: {_fileDiffResultLists.RemovedFilesAbsolutePath.Count}");
            streamWriter.WriteLine($"- {REPORT_LABEL_MODIFIED,-10}: {_fileDiffResultLists.ModifiedFilesRelativePath.Count}");
            streamWriter.WriteLine($"- {REPORT_LABEL_COMPARED,-10}: {_fileDiffResultLists.OldFilesAbsolutePath.Count} (Old) vs {_fileDiffResultLists.NewFilesAbsolutePath.Count} (New)");
            streamWriter.WriteLine();
        }

        /// <summary>
        /// IL Cache Stats セクションを書き込みます。
        /// <see cref="ConfigSettings.ShouldIncludeILCacheStatsInReport"/> が true かつ <paramref name="ilCache"/> が非 null の場合のみ出力されます。
        /// </summary>
        /// <param name="streamWriter">出力先ライター。</param>
        /// <param name="config">設定オブジェクト。</param>
        /// <param name="ilCache">IL キャッシュインスタンス（null の場合はスキップ）。</param>
        private static void WriteILCacheStatsSection(StreamWriter streamWriter, ConfigSettings config, ILCache ilCache)
        {
            if (!config.ShouldIncludeILCacheStatsInReport || ilCache == null)
            {
                return;
            }

            var stats = ilCache.GetReportStats();
            streamWriter.WriteLine(REPORT_SECTION_IL_CACHE_STATS);
            streamWriter.WriteLine($"- Hits    : {stats.Hits}");
            streamWriter.WriteLine($"- Misses  : {stats.Misses}");
            streamWriter.WriteLine($"- Hit Rate: {stats.HitRatePct:F1}%");
            streamWriter.WriteLine($"- Stores  : {stats.Stores}");
            streamWriter.WriteLine($"- Evicted : {stats.Evicted}");
            streamWriter.WriteLine($"- Expired : {stats.Expired}");
            streamWriter.WriteLine();
        }

        /// <summary>
        /// 追加の警告セクションを書き込みます。
        /// </summary>
        private void WriteWarningsSection(StreamWriter streamWriter, bool hasMd5Mismatch, bool hasTimestampRegressionWarning)
        {
            if (!hasMd5Mismatch && !hasTimestampRegressionWarning)
            {
                return;
            }

            streamWriter.WriteLine(REPORT_SECTION_WARNINGS);
            if (hasMd5Mismatch)
            {
                streamWriter.WriteLine($"- **WARNING:** {Constants.WARNING_MD5_MISMATCH}");
            }

            if (!hasTimestampRegressionWarning)
            {
                return;
            }

            streamWriter.WriteLine($"- **WARNING:** {WARNING_NEW_FILE_TIMESTAMP_OLDER_THAN_OLD}");
            foreach (var warning in _fileDiffResultLists.NewFileTimestampOlderThanOldWarnings.Values
                .OrderBy(entry => entry.FileRelativePath, StringComparer.OrdinalIgnoreCase))
            {
                streamWriter.WriteLine($"  - {warning.FileRelativePath} (updated_old: {warning.OldTimestamp}, updated_new: {warning.NewTimestamp})");
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
                timestampParts.Add($"updated_old: {Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, entry.Key))}");
            }
            if ((entry.Value & FileDiffResultLists.IgnoredFileLocation.New) != 0)
            {
                timestampParts.Add($"updated_new: {Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, entry.Key))}");
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
