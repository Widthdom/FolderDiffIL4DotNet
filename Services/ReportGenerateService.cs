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
        /// レポート生成スピナーのフレーム文字列配列。
        /// </summary>
        private readonly string[] _spinnerFrames;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="fileDiffResultLists">差分結果保持オブジェクト。</param>
        /// <param name="logger">ログ出力サービス。</param>
        /// <param name="config">設定。スピナーフレームの取得に使用します。</param>
        public ReportGenerateService(FileDiffResultLists fileDiffResultLists, ILoggerService logger, ConfigSettings config)
        {
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            _fileDiffResultLists = fileDiffResultLists;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            ArgumentNullException.ThrowIfNull(config);
            _spinnerFrames = config.SpinnerFrames.ToArray();
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
        /// タイムスタンプ新旧区切り（矢印）
        /// </summary>
        private const string REPORT_TIMESTAMP_ARROW = " → ";

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
            using var spinner = new ConsoleSpinner(SPINNER_LABEL_GENERATING_REPORT, frames: _spinnerFrames);
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

        /// <summary>
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

        /// <summary>レポートのヘッダ部（タイトル・実行情報・IL比較の注記）を書き込みます。</summary>
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

        /// <summary>判定根拠ラベルの凡例（Legend）を書き込みます。</summary>
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

        /// <summary>Ignored Files セクションを書き込みます。</summary>
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

        /// <summary>Unchanged Files セクションを書き込みます。</summary>
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

        /// <summary>Added Files セクションを書き込みます。</summary>
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

        /// <summary>Removed Files セクションを書き込みます。</summary>
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

        /// <summary>Modified Files セクションを書き込みます。</summary>
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

        /// <summary>Summary セクションを書き込みます。</summary>
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

        /// <summary>IL Cache Stats セクションを書き込みます（設定有効かつ ilCache 非 null の場合のみ）。</summary>
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

        /// <summary>警告セクションを書き込みます。</summary>
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
