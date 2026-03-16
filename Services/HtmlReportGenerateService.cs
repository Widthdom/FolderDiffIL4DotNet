using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Text;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// 差分結果のインタラクティブ HTML レポート (<see cref="DIFF_REPORT_HTML_FILE_NAME"/>) を生成するサービス。
    /// Removed / Added / Modified の各ファイル行にチェックボックス・OK 理由・備考列を持ち、
    /// localStorage による自動保存と「レビュー済みとして保存」ダウンロード機能を提供します。
    /// </summary>
    public sealed class HtmlReportGenerateService
    {
        private readonly FileDiffResultLists _fileDiffResultLists;
        private readonly ILoggerService _logger;

        /// <summary>出力ファイル名。</summary>
        internal const string DIFF_REPORT_HTML_FILE_NAME = "diff_report.html";

        private const string TIMESTAMP_ARROW = " → ";
        private const string COLOR_REMOVED = "#cc0000";
        private const string COLOR_ADDED = "#006600";
        private const string COLOR_MODIFIED = "#0000cc";

        /// <summary>コンストラクタ。</summary>
        /// <param name="fileDiffResultLists">差分結果保持オブジェクト。</param>
        /// <param name="logger">ログ出力サービス。</param>
        /// <param name="config">設定（将来の拡張のために受け取りますが、現バージョンでは直接使用しません）。</param>
        public HtmlReportGenerateService(FileDiffResultLists fileDiffResultLists, ILoggerService logger, ConfigSettings config)
        {
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            _fileDiffResultLists = fileDiffResultLists;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            ArgumentNullException.ThrowIfNull(config);
        }

        /// <summary>
        /// diff_report.html を生成して <paramref name="reportsFolderAbsolutePath"/> へ書き込みます。
        /// <see cref="ConfigSettings.ShouldGenerateHtmlReport"/> が <see langword="false"/> の場合は何もしません。
        /// </summary>
        public void GenerateDiffReportHtml(
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            ConfigSettings config,
            ILCache ilCache = null)
        {
            if (!config.ShouldGenerateHtmlReport) return;

            string htmlPath = Path.Combine(reportsFolderAbsolutePath, DIFF_REPORT_HTML_FILE_NAME);
            string html = BuildHtml(
                oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath,
                appVersion, elapsedTimeString, computerName, config, ilCache);
            try
            {
                File.WriteAllText(htmlPath, html, Encoding.UTF8);
            }
            catch (IOException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Failed to write HTML report to '{htmlPath}': {ex.Message}",
                    shouldOutputMessageToConsole: true, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Failed to write HTML report to '{htmlPath}': {ex.Message}",
                    shouldOutputMessageToConsole: true, ex);
            }
        }

        // ── Build ────────────────────────────────────────────────────────────

        private string BuildHtml(
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            ConfigSettings config,
            ILCache ilCache)
        {
            string label = Path.GetFileName(
                reportsFolderAbsolutePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "diff";
            // Storage key: sanitise to a safe JS string literal value
            string storageKey = "folderdiff-" + label
                .Replace("'", "-").Replace("\"", "-").Replace("\\", "-").Replace("`", "-");

            var sb = new StringBuilder(capacity: 65536);
            AppendHtmlHead(sb, label, storageKey);
            sb.AppendLine("<body>");
            AppendControlBar(sb);
            sb.AppendLine("<main>");

            AppendHeaderSection(sb, oldFolderAbsolutePath, newFolderAbsolutePath,
                appVersion, elapsedTimeString, computerName, config);
            AppendLegendSection(sb);

            if (config.ShouldIncludeIgnoredFiles)
                AppendIgnoredSection(sb, oldFolderAbsolutePath, newFolderAbsolutePath, config);

            if (config.ShouldIncludeUnchangedFiles)
                AppendUnchangedSection(sb, oldFolderAbsolutePath, newFolderAbsolutePath, config);

            AppendAddedSection(sb, config);
            AppendRemovedSection(sb, config);
            AppendModifiedSection(sb, oldFolderAbsolutePath, newFolderAbsolutePath, config);
            AppendSummarySection(sb, config);

            if (config.ShouldIncludeILCacheStatsInReport && ilCache != null)
                AppendILCacheStatsSection(sb, ilCache);

            AppendWarningsSection(sb);

            sb.AppendLine("</main>");
            AppendJs(sb, storageKey);
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            return sb.ToString();
        }

        // ── Head / Controls ──────────────────────────────────────────────────

        private static void AppendHtmlHead(StringBuilder sb, string label, string storageKey)
        {
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"  <title>Diff Report - {HtmlEncode(label)}</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine(GetCss());
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
        }

        private static void AppendControlBar(StringBuilder sb)
        {
            sb.AppendLine("<div class=\"controls\">");
            sb.AppendLine("  <button class=\"btn\" onclick=\"downloadReviewed()\">&#x1F4BE; Download reviewed version</button>");
            sb.AppendLine("  <button class=\"btn btn-clear\" onclick=\"clearAll()\">&#x2715; Clear all</button>");
            sb.AppendLine("  <span id=\"save-status\" class=\"save-status\"></span>");
            sb.AppendLine("</div>");
        }

        // ── Report sections ──────────────────────────────────────────────────

        private void AppendHeaderSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            ConfigSettings config)
        {
            sb.AppendLine("<h1>Folder Diff Report</h1>");
            sb.AppendLine("<ul class=\"meta\">");
            sb.AppendLine($"  <li>App Version: FolderDiffIL4DotNet {HtmlEncode(appVersion)}</li>");
            sb.AppendLine($"  <li>Computer: {HtmlEncode(computerName)}</li>");
            sb.AppendLine($"  <li>Old: <code>{HtmlEncode(oldFolderAbsolutePath)}</code></li>");
            sb.AppendLine($"  <li>New: <code>{HtmlEncode(newFolderAbsolutePath)}</code></li>");
            sb.AppendLine($"  <li>Ignored Extensions: {HtmlEncode(string.Join(", ", config.IgnoredExtensions))}</li>");
            sb.AppendLine($"  <li>Text File Extensions: {HtmlEncode(string.Join(", ", config.TextFileExtensions))}</li>");
            sb.AppendLine($"  <li>IL Disassembler: {HtmlEncode(BuildDisassemblerHeaderText())}</li>");
            if (!string.IsNullOrWhiteSpace(elapsedTimeString))
                sb.AppendLine($"  <li>Elapsed Time: {HtmlEncode(elapsedTimeString)}</li>");
            if (config.ShouldOutputFileTimestamps)
                sb.AppendLine($"  <li>Timestamps (timezone): {HtmlEncode(DateTimeOffset.Now.ToString("zzz"))}</li>");
            sb.AppendLine($"  <li class=\"note\">Note: When diffing IL, lines starting with <code>{HtmlEncode(Constants.IL_MVID_LINE_PREFIX)}</code> (if present) are ignored because they contain disassembler-emitted Module Version ID metadata that can change on rebuild without meaning the executable IL changed.</li>");
            sb.AppendLine("</ul>");
        }

        private static void AppendLegendSection(StringBuilder sb)
        {
            sb.AppendLine("<div class=\"legend\">");
            sb.AppendLine("  <strong>Legend:</strong>&nbsp;");
            sb.AppendLine("  <code>MD5Match</code>&nbsp;/&nbsp;<code>MD5Mismatch</code>: MD5 hash match / mismatch");
            sb.AppendLine("  &nbsp;|&nbsp;");
            sb.AppendLine("  <code>ILMatch</code>&nbsp;/&nbsp;<code>ILMismatch</code>: IL(Intermediate Language) match / mismatch");
            sb.AppendLine("  &nbsp;|&nbsp;");
            sb.AppendLine("  <code>TextMatch</code>&nbsp;/&nbsp;<code>TextMismatch</code>: Text match / mismatch");
            sb.AppendLine("</div>");
        }

        private void AppendIgnoredSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            ConfigSettings config)
        {
            var items = _fileDiffResultLists.IgnoredFilesRelativePathToLocation
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2>[ x ] Ignored Files ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine("<p class=\"empty\">(none)</p>"); return; }

            AppendTableStart(sb, null, "Location");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var entry in items)
            {
                bool hasOld = (entry.Value & FileDiffResultLists.IgnoredFileLocation.Old) != 0;
                bool hasNew = (entry.Value & FileDiffResultLists.IgnoredFileLocation.New) != 0;
                string ts = BuildIgnoredTimestamp(entry.Key, hasOld, hasNew,
                    oldFolderAbsolutePath, newFolderAbsolutePath, config.ShouldOutputFileTimestamps);
                string location = (hasOld && hasNew) ? "old/new" : hasOld ? "old" : "new";
                AppendFileRow(sb, "ign", idx, entry.Key, ts, location, "");
                idx++;
            }
            sb.AppendLine("</tbody></table>");
        }

        private void AppendUnchangedSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            ConfigSettings config)
        {
            var items = _fileDiffResultLists.UnchangedFilesRelativePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2>[ = ] Unchanged Files ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine("<p class=\"empty\">(none)</p>"); return; }

            AppendTableStart(sb, null, "Diff Reason");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var path in items)
            {
                string ts = "";
                if (config.ShouldOutputFileTimestamps)
                {
                    string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, path));
                    string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, path));
                    ts = oldTs != newTs
                        ? $"[{oldTs}{TIMESTAMP_ARROW}{newTs}]"
                        : $"[{newTs}]";
                }
                _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(path, out var diffDetail);
                _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(path, out var asm);
                AppendFileRow(sb, "unch", idx, path, ts, diffDetail.ToString(), asm ?? "");
                idx++;
            }
            sb.AppendLine("</tbody></table>");
        }

        private void AppendAddedSection(StringBuilder sb, ConfigSettings config)
        {
            var items = _fileDiffResultLists.AddedFilesAbsolutePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_ADDED}\">[ + ] Added Files ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine("<p class=\"empty\">(none)</p>"); return; }

            AppendTableStart(sb, COLOR_ADDED, "Diff Reason");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var absPath in items)
            {
                string ts = config.ShouldOutputFileTimestamps
                    ? $"[{Caching.TimestampCache.GetOrAdd(absPath)}]" : "";
                AppendFileRow(sb, "add", idx, absPath, ts, "", "");
                idx++;
            }
            sb.AppendLine("</tbody></table>");
        }

        private void AppendRemovedSection(StringBuilder sb, ConfigSettings config)
        {
            var items = _fileDiffResultLists.RemovedFilesAbsolutePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_REMOVED}\">[ - ] Removed Files ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine("<p class=\"empty\">(none)</p>"); return; }

            AppendTableStart(sb, COLOR_REMOVED, "Diff Reason");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var absPath in items)
            {
                string ts = config.ShouldOutputFileTimestamps
                    ? $"[{Caching.TimestampCache.GetOrAdd(absPath)}]" : "";
                AppendFileRow(sb, "rem", idx, absPath, ts, "", "");
                idx++;
            }
            sb.AppendLine("</tbody></table>");
        }

        private void AppendModifiedSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            ConfigSettings config)
        {
            var items = _fileDiffResultLists.ModifiedFilesRelativePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ * ] Modified Files ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine("<p class=\"empty\">(none)</p>"); return; }

            AppendTableStart(sb, COLOR_MODIFIED, "Diff Reason");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var path in items)
            {
                string ts = "";
                if (config.ShouldOutputFileTimestamps)
                {
                    string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, path));
                    string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, path));
                    ts = $"[{oldTs}{TIMESTAMP_ARROW}{newTs}]";
                }
                _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(path, out var diffDetail);
                _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(path, out var asm);
                AppendFileRow(sb, "mod", idx, path, ts, diffDetail.ToString(), asm ?? "");

                if (config.EnableInlineDiff &&
                    diffDetail == FileDiffResultLists.DiffDetailResult.TextMismatch)
                {
                    AppendInlineDiffRow(sb, idx, path, oldFolderAbsolutePath, newFolderAbsolutePath, config);
                }

                idx++;
            }
            sb.AppendLine("</tbody></table>");
        }

        private void AppendInlineDiffRow(
            StringBuilder sb,
            int idx,
            string relPath,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            ConfigSettings config)
        {
            int maxInput = config.InlineDiffMaxInputLines > 0 ? config.InlineDiffMaxInputLines : 1000;
            int maxOutput = config.InlineDiffMaxOutputLines > 0 ? config.InlineDiffMaxOutputLines : 500;
            int contextLines = config.InlineDiffContextLines >= 0 ? config.InlineDiffContextLines : 3;

            string oldAbsPath = Path.Combine(oldFolderAbsolutePath, relPath);
            string newAbsPath = Path.Combine(newFolderAbsolutePath, relPath);

            string[] oldLines, newLines;
            try
            {
                oldLines = File.ReadAllLines(oldAbsPath);
                newLines = File.ReadAllLines(newAbsPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // ファイル読み込み失敗時は差分行を表示しない
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Inline diff skipped for '{relPath}': {ex.Message}",
                    shouldOutputMessageToConsole: false, ex);
                return;
            }

            if (oldLines.Length > maxInput || newLines.Length > maxInput)
            {
                sb.AppendLine("<tr class=\"diff-row\">");
                sb.AppendLine($"  <td colspan=\"7\"><p class=\"diff-skipped\">Inline diff skipped: file too large " +
                    $"({oldLines.Length} vs {newLines.Length} lines; limit is {maxInput}). " +
                    "Increase <code>InlineDiffMaxInputLines</code> in config to enable.</p></td>");
                sb.AppendLine("</tr>");
                return;
            }

            IReadOnlyList<TextDiffer.DiffLine> diffLines;
            try
            {
                diffLines = TextDiffer.Compute(oldLines, newLines, contextLines, maxOutput);
            }
            catch (Exception ex)
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Inline diff computation failed for '{relPath}': {ex.Message}",
                    shouldOutputMessageToConsole: false, ex);
                return;
            }

            if (diffLines.Count == 0) return;

            int addedCount = 0, removedCount = 0;
            foreach (var line in diffLines)
            {
                if (line.Kind == TextDiffer.Added) addedCount++;
                else if (line.Kind == TextDiffer.Removed) removedCount++;
            }

            string summaryText = $"Show diff (+{addedCount} / -{removedCount})";
            string detailsId = $"diff_mod_{idx}";

            sb.AppendLine("<tr class=\"diff-row\">");
            sb.AppendLine("  <td colspan=\"7\">");
            sb.AppendLine($"    <details id=\"{HtmlEncode(detailsId)}\">");
            sb.AppendLine($"      <summary class=\"diff-summary\">{HtmlEncode(summaryText)}</summary>");
            sb.AppendLine("      <div class=\"diff-view\">");
            sb.AppendLine("        <table class=\"diff-table\">");
            sb.AppendLine("          <tbody>");

            foreach (var line in diffLines)
            {
                switch (line.Kind)
                {
                    case TextDiffer.HunkHeader:
                        sb.AppendLine("            <tr class=\"diff-hunk-tr\">");
                        sb.AppendLine("              <td class=\"diff-ln\"></td>");
                        sb.AppendLine("              <td class=\"diff-ln\"></td>");
                        sb.AppendLine($"              <td class=\"diff-hunk-td\">{HtmlEncode(line.Text)}</td>");
                        sb.AppendLine("            </tr>");
                        break;
                    case TextDiffer.Removed:
                        sb.AppendLine("            <tr class=\"diff-del-tr\">");
                        sb.AppendLine($"              <td class=\"diff-ln diff-old-ln\">{line.OldLineNo}</td>");
                        sb.AppendLine("              <td class=\"diff-ln diff-new-ln\"></td>");
                        sb.AppendLine($"              <td class=\"diff-del-td\">-{HtmlEncode(line.Text)}</td>");
                        sb.AppendLine("            </tr>");
                        break;
                    case TextDiffer.Added:
                        sb.AppendLine("            <tr class=\"diff-add-tr\">");
                        sb.AppendLine("              <td class=\"diff-ln diff-old-ln\"></td>");
                        sb.AppendLine($"              <td class=\"diff-ln diff-new-ln\">{line.NewLineNo}</td>");
                        sb.AppendLine($"              <td class=\"diff-add-td\">+{HtmlEncode(line.Text)}</td>");
                        sb.AppendLine("            </tr>");
                        break;
                    case TextDiffer.Context:
                        sb.AppendLine("            <tr class=\"diff-ctx-tr\">");
                        sb.AppendLine($"              <td class=\"diff-ln diff-old-ln\">{line.OldLineNo}</td>");
                        sb.AppendLine($"              <td class=\"diff-ln diff-new-ln\">{line.NewLineNo}</td>");
                        sb.AppendLine($"              <td class=\"diff-ctx-td\"> {HtmlEncode(line.Text)}</td>");
                        sb.AppendLine("            </tr>");
                        break;
                    case TextDiffer.Truncated:
                        sb.AppendLine("            <tr class=\"diff-trunc-tr\">");
                        sb.AppendLine("              <td class=\"diff-ln\"></td>");
                        sb.AppendLine("              <td class=\"diff-ln\"></td>");
                        sb.AppendLine($"              <td class=\"diff-trunc-td\">{HtmlEncode(line.Text)}</td>");
                        sb.AppendLine("            </tr>");
                        break;
                }
            }

            sb.AppendLine("          </tbody>");
            sb.AppendLine("        </table>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </details>");
            sb.AppendLine("  </td>");
            sb.AppendLine("</tr>");
        }

        private void AppendSummarySection(StringBuilder sb, ConfigSettings config)
        {
            sb.AppendLine("<h2>Summary</h2>");
            sb.AppendLine("<ul class=\"summary\">");
            var stats = _fileDiffResultLists.SummaryStatistics;
            if (config.ShouldIncludeIgnoredFiles)
                sb.AppendLine($"  <li>Ignored   : {stats.IgnoredCount}</li>");
            sb.AppendLine($"  <li>Unchanged : {stats.UnchangedCount}</li>");
            sb.AppendLine($"  <li>Added     : {stats.AddedCount}</li>");
            sb.AppendLine($"  <li>Removed   : {stats.RemovedCount}</li>");
            sb.AppendLine($"  <li>Modified  : {stats.ModifiedCount}</li>");
            sb.AppendLine($"  <li>Compared  : {_fileDiffResultLists.OldFilesAbsolutePath.Count} (Old) vs {_fileDiffResultLists.NewFilesAbsolutePath.Count} (New)</li>");
            sb.AppendLine("</ul>");
        }

        private static void AppendILCacheStatsSection(StringBuilder sb, ILCache ilCache)
        {
            var stats = ilCache.GetReportStats();
            sb.AppendLine("<h2>IL Cache Stats</h2>");
            sb.AppendLine("<ul class=\"summary\">");
            sb.AppendLine($"  <li>Hits    : {stats.Hits}</li>");
            sb.AppendLine($"  <li>Misses  : {stats.Misses}</li>");
            sb.AppendLine($"  <li>Hit Rate: {stats.HitRatePct:F1}%</li>");
            sb.AppendLine($"  <li>Stores  : {stats.Stores}</li>");
            sb.AppendLine($"  <li>Evicted : {stats.Evicted}</li>");
            sb.AppendLine($"  <li>Expired : {stats.Expired}</li>");
            sb.AppendLine("</ul>");
        }

        private void AppendWarningsSection(StringBuilder sb)
        {
            bool hasMd5 = _fileDiffResultLists.HasAnyMd5Mismatch;
            bool hasTs = _fileDiffResultLists.HasAnyNewFileTimestampOlderThanOldWarning;
            if (!hasMd5 && !hasTs) return;

            sb.AppendLine("<h2>Warnings</h2>");
            sb.AppendLine("<ul class=\"warnings\">");
            if (hasMd5)
                sb.AppendLine($"  <li><strong>WARNING:</strong> {HtmlEncode(Constants.WARNING_MD5_MISMATCH)}</li>");
            if (hasTs)
            {
                sb.AppendLine("  <li><strong>WARNING:</strong> One or more files in <code>new</code> have older last-modified timestamps than the corresponding files in <code>old</code>.");
                sb.AppendLine("    <ul>");
                foreach (var w in _fileDiffResultLists.NewFileTimestampOlderThanOldWarnings.Values
                    .OrderBy(w => w.FileRelativePath, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"      <li><code>{HtmlEncode(w.FileRelativePath)}</code> [{HtmlEncode(w.OldTimestamp)}{TIMESTAMP_ARROW}{HtmlEncode(w.NewTimestamp)}]</li>");
                }
                sb.AppendLine("    </ul></li>");
            }
            sb.AppendLine("</ul>");
        }

        // ── Table helpers ────────────────────────────────────────────────────

        /// <summary>
        /// セクション用テーブルの開始タグと見出し行を追加します。
        /// </summary>
        /// <param name="sb">書き込み先 <see cref="StringBuilder"/>。</param>
        /// <param name="thColor">見出し文字色。null の場合はデフォルト色。</param>
        /// <param name="col6Header">6 列目（差異理由 / 所在）のヘッダラベル。</param>
        private static void AppendTableStart(StringBuilder sb, string thColor, string col6Header)
        {
            string colorStyle = thColor != null ? $" style=\"color:{thColor}\"" : "";
            sb.AppendLine("<table>");
            sb.AppendLine($"<thead><tr>");
            sb.AppendLine($"  <th class=\"col-cb\"{colorStyle}>&#x2713;</th>");
            sb.AppendLine($"  <th{colorStyle}>OK Reason</th>");
            sb.AppendLine($"  <th{colorStyle}>Notes</th>");
            sb.AppendLine($"  <th{colorStyle}>File Path</th>");
            sb.AppendLine($"  <th{colorStyle}>Timestamp</th>");
            sb.AppendLine($"  <th{colorStyle}>{HtmlEncode(col6Header)}</th>");
            sb.AppendLine($"  <th{colorStyle}>Disassembler</th>");
            sb.AppendLine("</tr></thead>");
        }

        private static void AppendFileRow(
            StringBuilder sb,
            string sectionPrefix,
            int idx,
            string path,
            string timestamp,
            string col6,
            string disassembler)
        {
            string cbId = $"cb_{sectionPrefix}_{idx}";
            string reasonId = $"reason_{sectionPrefix}_{idx}";
            string notesId = $"notes_{sectionPrefix}_{idx}";
            sb.AppendLine("<tr>");
            sb.AppendLine($"  <td class=\"col-cb\"><input type=\"checkbox\" id=\"{cbId}\"></td>");
            sb.AppendLine($"  <td class=\"col-reason\"><input type=\"text\" id=\"{reasonId}\" placeholder=\"OK reason\"></td>");
            sb.AppendLine($"  <td class=\"col-notes\"><input type=\"text\" id=\"{notesId}\" placeholder=\"Notes\"></td>");
            sb.AppendLine($"  <td class=\"col-path\"><code>{HtmlEncode(path)}</code></td>");
            sb.AppendLine($"  <td class=\"col-ts\"><code>{HtmlEncode(timestamp)}</code></td>");
            sb.AppendLine($"  <td class=\"col-diff\"><code>{HtmlEncode(col6)}</code></td>");
            sb.AppendLine($"  <td class=\"col-asm\"><code>{HtmlEncode(disassembler)}</code></td>");
            sb.AppendLine("</tr>");
        }

        private static string BuildIgnoredTimestamp(
            string relPath,
            bool hasOld,
            bool hasNew,
            string oldFolder,
            string newFolder,
            bool shouldOutput)
        {
            if (!shouldOutput) return "";
            if (hasOld && hasNew)
            {
                string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolder, relPath));
                string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolder, relPath));
                return $"[{oldTs}{TIMESTAMP_ARROW}{newTs}]";
            }
            if (hasOld) return $"[{Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolder, relPath))}]";
            if (hasNew) return $"[{Caching.TimestampCache.GetOrAdd(Path.Combine(newFolder, relPath))}]";
            return "";
        }

        private string BuildDisassemblerHeaderText()
        {
            var labels = _fileDiffResultLists.DisassemblerToolVersions.Keys
                .Concat(_fileDiffResultLists.DisassemblerToolVersionsFromCache.Keys)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return labels.Count == 0 ? "N/A" : string.Join(", ", labels);
        }

        // ── JavaScript ───────────────────────────────────────────────────────

        private static void AppendJs(StringBuilder sb, string storageKey)
        {
            sb.AppendLine("<script>");
            // NOTE: 'const __savedState__ = null;' is a sentinel that downloadReviewed() replaces
            // with the serialised review state. Do not change the exact whitespace.
            sb.AppendLine($"  const __storageKey__ = '{storageKey}';");
            sb.AppendLine("  const __savedState__ = null;");
            sb.AppendLine();
            sb.AppendLine("  document.addEventListener('DOMContentLoaded', function() {");
            sb.AppendLine("    var toRestore = __savedState__ || JSON.parse(localStorage.getItem(__storageKey__) || 'null');");
            sb.AppendLine("    if (toRestore) {");
            sb.AppendLine("      Object.entries(toRestore).forEach(function(entry) {");
            sb.AppendLine("        var el = document.getElementById(entry[0]);");
            sb.AppendLine("        if (!el) return;");
            sb.AppendLine("        if (el.type === 'checkbox') el.checked = Boolean(entry[1]);");
            sb.AppendLine("        else el.value = String(entry[1] || '');");
            sb.AppendLine("      });");
            sb.AppendLine("    }");
            sb.AppendLine("    document.querySelectorAll('input, textarea').forEach(function(el) {");
            sb.AppendLine("      el.addEventListener('change', autoSave);");
            sb.AppendLine("      el.addEventListener('input', autoSave);");
            sb.AppendLine("    });");
            sb.AppendLine("  });");
            sb.AppendLine();
            sb.AppendLine("  function collectState() {");
            sb.AppendLine("    var s = {};");
            sb.AppendLine("    document.querySelectorAll('input[id], textarea[id]').forEach(function(el) {");
            sb.AppendLine("      s[el.id] = (el.type === 'checkbox') ? el.checked : el.value;");
            sb.AppendLine("    });");
            sb.AppendLine("    return s;");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  function autoSave() {");
            sb.AppendLine("    localStorage.setItem(__storageKey__, JSON.stringify(collectState()));");
            sb.AppendLine("    var status = document.getElementById('save-status');");
            sb.AppendLine("    if (status) status.textContent = 'Auto-saved at ' + new Date().toLocaleTimeString();");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  function downloadReviewed() {");
            sb.AppendLine("    var state = collectState();");
            sb.AppendLine("    var html = document.documentElement.outerHTML;");
            sb.AppendLine("    html = html.replace('const __savedState__ = null;',");
            sb.AppendLine("      'const __savedState__ = ' + JSON.stringify(state) + ';');");
            sb.AppendLine("    var blob = new Blob([html], { type: 'text/html;charset=utf-8' });");
            sb.AppendLine("    var a = document.createElement('a');");
            sb.AppendLine("    a.href = URL.createObjectURL(blob);");
            sb.AppendLine("    a.download = (document.title || 'diff_report')");
            sb.AppendLine("      .replace(/[^a-zA-Z0-9._\\-]/g, '_') + '_reviewed.html';");
            sb.AppendLine("    document.body.appendChild(a);");
            sb.AppendLine("    a.click();");
            sb.AppendLine("    document.body.removeChild(a);");
            sb.AppendLine("    setTimeout(function() { URL.revokeObjectURL(a.href); }, 1000);");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  function clearAll() {");
            sb.AppendLine("    if (!confirm('Clear all checkboxes and text inputs?')) return;");
            sb.AppendLine("    document.querySelectorAll('input[type=\"checkbox\"]').forEach(function(cb) { cb.checked = false; });");
            sb.AppendLine("    document.querySelectorAll('input[type=\"text\"], textarea').forEach(function(inp) { inp.value = ''; });");
            sb.AppendLine("    localStorage.removeItem(__storageKey__);");
            sb.AppendLine("    var status = document.getElementById('save-status');");
            sb.AppendLine("    if (status) status.textContent = 'Cleared.';");
            sb.AppendLine("  }");
            sb.AppendLine("</script>");
        }

        // ── CSS ──────────────────────────────────────────────────────────────

        private static string GetCss()
        {
            return
@"    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
           font-size: 14px; padding: 0 2rem 3rem; max-width: 2200px; margin: 0 auto; }
    h1 { font-size: 1.4rem; padding: 1rem 0 0.5rem; }
    h2 { font-size: 1.05rem; margin: 1.4rem 0 0.4rem; }
    code { font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', monospace;
           font-size: 12px; background: #f0f0f0; padding: 0 3px; border-radius: 2px; }
    ul.meta { margin: 0.4rem 0 0.8rem 1.2rem; }
    ul.meta li { margin-bottom: 2px; line-height: 1.6; }
    ul.meta li.note { color: #555; font-size: 12px; }
    .legend { margin: 0.4rem 0 0.8rem; font-size: 13px; }
    .controls { position: sticky; top: 0; background: #fff; border-bottom: 1px solid #ddd;
                padding: 0.5rem 0; display: flex; gap: 0.8rem; align-items: center; z-index: 100; }
    .btn { padding: 0.3rem 0.8rem; cursor: pointer; border: 1px solid #aaa;
           background: #f5f5f5; font-size: 13px; border-radius: 3px; }
    .btn:hover { background: #e8e8e8; }
    .save-status { font-size: 12px; color: #777; }
    .empty { color: #999; font-size: 12px; margin-bottom: 0.8rem; }
    table { border-collapse: collapse; width: 100%; margin-bottom: 1.2rem; }
    th { padding: 4px 6px; font-size: 12px; white-space: nowrap; text-align: left;
         border: 1px solid #bbb; background: #fafafa; }
    td { padding: 2px 4px; border: 1px solid #e0e0e0; vertical-align: middle; }
    td.col-cb { width: 2.2em; text-align: center; }
    td.col-reason { min-width: 8em; }
    td.col-notes  { min-width: 8em; }
    td.col-path { font-family: monospace; font-size: 12px; word-break: break-all; min-width: 15em; }
    td.col-ts   { font-family: monospace; font-size: 12px; white-space: nowrap; width: 22em; }
    td.col-diff { font-family: monospace; font-size: 12px; white-space: nowrap; width: 11em; }
    td.col-asm  { font-family: monospace; font-size: 12px; white-space: nowrap; width: 20em; }
    td.col-reason input[type=""text""], td.col-notes input[type=""text""] {
        width: 100%; border: 1px solid #ccc; padding: 2px 5px; font-size: 13px; outline: none; }
    td.col-reason input[type=""text""]:focus, td.col-notes input[type=""text""]:focus {
        border-color: #666; background: #fffff8; }
    input[type=""checkbox""] { width: 1.1em; height: 1.1em; cursor: pointer; }
    ul.summary { list-style: none; margin: 0.3rem 0 1rem 1rem;
                 font-family: monospace; font-size: 13px; }
    ul.summary li { line-height: 1.8; }
    ul.warnings { margin: 0.3rem 0 0 1rem; }
    ul.warnings li { margin-bottom: 0.4rem; line-height: 1.6; }
    /* ── Inline diff ───────────────────────────────────────────────────── */
    tr.diff-row > td { padding: 0; border-top: none; }
    details.diff-details { }
    summary.diff-summary {
      display: inline-flex; align-items: center; gap: 0.4em;
      cursor: pointer; font-size: 12px; color: #0000cc;
      padding: 3px 6px; user-select: none; list-style: none; }
    summary.diff-summary::-webkit-details-marker { display: none; }
    summary.diff-summary::before { content: '▶'; font-size: 10px; transition: transform 0.15s; }
    details[open] > summary.diff-summary::before { transform: rotate(90deg); }
    .diff-view { overflow-x: auto; margin: 0 0 4px 0; }
    table.diff-table { border-collapse: collapse; width: 100%; margin: 0;
                       font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', monospace;
                       font-size: 12px; }
    table.diff-table td { padding: 1px 6px; border: none; white-space: pre; }
    td.diff-ln { width: 3.5em; min-width: 2.5em; text-align: right;
                 color: #999; background: #f6f8fa; border-right: 1px solid #e0e0e0;
                 user-select: none; font-size: 11px; padding: 1px 4px; }
    tr.diff-hunk-tr { background: #f1f8ff; }
    td.diff-hunk-td { color: #0057ae; padding: 1px 8px; }
    tr.diff-del-tr { background: #ffeef0; }
    td.diff-del-td { color: #b31d28; background: #ffeef0; }
    tr.diff-add-tr { background: #e6ffed; }
    td.diff-add-td { color: #22863a; background: #e6ffed; }
    tr.diff-ctx-tr { background: #fff; }
    td.diff-ctx-td { color: #24292e; background: #fff; }
    tr.diff-trunc-tr { background: #fffbdd; }
    td.diff-trunc-td { color: #735c0f; padding: 2px 8px; font-style: italic; }
    p.diff-skipped { color: #735c0f; font-size: 12px; padding: 4px 8px;
                     background: #fffbdd; margin: 0; }";
        }

        // ── Utilities ────────────────────────────────────────────────────────

        internal static string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }
    }
}
