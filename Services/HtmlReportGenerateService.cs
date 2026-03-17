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
    /// Removed / Added / Modified の各ファイル行にチェックボックス・Justification（根拠）・Notes 列を持ち、
    /// localStorage による自動保存と「レビュー済みとして保存」ダウンロード機能を提供します。
    /// </summary>
    public sealed class HtmlReportGenerateService
    {
        private readonly FileDiffResultLists _fileDiffResultLists;
        private readonly ILoggerService _logger;

        /// <summary>出力ファイル名。</summary>
        internal const string DIFF_REPORT_HTML_FILE_NAME = "diff_report.html";

        private const string TIMESTAMP_ARROW = " → ";
        private const string COLOR_ADDED    = "#22863a";
        private const string COLOR_REMOVED  = "#b31d28";
        private const string COLOR_MODIFIED = "#0051c3";
        private const string TH_BG_ADDED    = "#e6ffed";
        private const string TH_BG_REMOVED  = "#ffeef0";
        private const string TH_BG_MODIFIED = "#e3f2fd";
        private const string TH_BG_DEFAULT  = "#fafafa";

        /// <summary>コンストラクタ。</summary>
        public HtmlReportGenerateService(FileDiffResultLists fileDiffResultLists, ILoggerService logger, ConfigSettings config)
        {
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            _fileDiffResultLists = fileDiffResultLists;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            ArgumentNullException.ThrowIfNull(config);
        }

        // ── Public entry point ───────────────────────────────────────────────

        /// <summary>
        /// diff_report.html を生成して <paramref name="reportsFolderAbsolutePath"/> へ書き込みます。
        /// <see cref="ConfigSettings.ShouldGenerateHtmlReport"/> が <see langword="false"/> の場合は何もしません。
        /// </summary>
        /// <param name="oldFolderAbsolutePath">比較元フォルダの絶対パス。レポートヘッダに表示されます。</param>
        /// <param name="newFolderAbsolutePath">比較先フォルダの絶対パス。レポートヘッダに表示されます。</param>
        /// <param name="reportsFolderAbsolutePath">レポート出力先フォルダの絶対パス。diff_report.html はここに書き込まれます。</param>
        /// <param name="appVersion">アプリケーションバージョン文字列。レポートフッタに表示されます。</param>
        /// <param name="elapsedTimeString">実行時間の整形済み文字列。レポートフッタに表示されます。</param>
        /// <param name="computerName">実行マシン名。レポートヘッダに表示されます。</param>
        /// <param name="config">実行時設定。<see cref="ConfigSettings.ShouldGenerateHtmlReport"/> などを参照します。</param>
        /// <param name="ilCache">インライン差分生成に使用する IL キャッシュ。<see langword="null"/> の場合は IL インライン差分を省略します。</param>
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
            string storageKey = "folderdiff-" + label
                .Replace("'", "-").Replace("\"", "-").Replace("\\", "-").Replace("`", "-");
            string reportDate = DateTime.Now.ToString("yyyyMMdd");

            var sb = new StringBuilder(capacity: 131072);
            AppendHtmlHead(sb);
            sb.AppendLine("<body>");

            // Controls bar (markers allow stripping in downloadReviewed)
            sb.AppendLine("<!--CTRL-->");
            sb.AppendLine("<div class=\"controls\">");
            sb.AppendLine("  <button class=\"btn\" onclick=\"downloadReviewed()\">&#x2913; Download as reviewed</button>");
            sb.AppendLine("  <button class=\"btn btn-clear\" onclick=\"clearAll()\">&#x2715; Clear all</button>");
            sb.AppendLine("  <span id=\"save-status\" class=\"save-status\"></span>");
            sb.AppendLine("</div>");
            sb.AppendLine("<!--/CTRL-->");

            sb.AppendLine("<main>");
            AppendHeaderSection(sb, oldFolderAbsolutePath, newFolderAbsolutePath,
                appVersion, elapsedTimeString, computerName, config);

            if (config.ShouldIncludeIgnoredFiles)
                AppendIgnoredSection(sb, oldFolderAbsolutePath, newFolderAbsolutePath, config);

            if (config.ShouldIncludeUnchangedFiles)
                AppendUnchangedSection(sb, oldFolderAbsolutePath, newFolderAbsolutePath, config);

            AppendAddedSection(sb, config);
            AppendRemovedSection(sb, config);
            AppendModifiedSection(sb, oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath, config, ilCache);
            AppendSummarySection(sb, config);

            if (config.ShouldIncludeILCacheStatsInReport && ilCache != null)
                AppendILCacheStatsSection(sb, ilCache);

            AppendWarningsSection(sb, oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath, config, ilCache);

            sb.AppendLine("</main>");
            AppendJs(sb, storageKey, reportDate);
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            return sb.ToString();
        }

        // ── Head ─────────────────────────────────────────────────────────────

        private static void AppendHtmlHead(StringBuilder sb)
        {
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("  <title>diff_report</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine(GetCss());
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
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
            sb.AppendLine($"  <li>Old: {HtmlEncode(oldFolderAbsolutePath)}</li>");
            sb.AppendLine($"  <li>New: {HtmlEncode(newFolderAbsolutePath)}</li>");
            sb.AppendLine($"  <li>Ignored Extensions: {HtmlEncode(string.Join(", ", config.IgnoredExtensions))}</li>");
            sb.AppendLine($"  <li>Text File Extensions: {HtmlEncode(string.Join(", ", config.TextFileExtensions))}</li>");
            sb.AppendLine($"  <li>IL Disassembler: {HtmlEncode(BuildDisassemblerHeaderText())}</li>");
            if (!string.IsNullOrWhiteSpace(elapsedTimeString))
                sb.AppendLine($"  <li>Elapsed Time: {HtmlEncode(elapsedTimeString)}</li>");
            if (config.ShouldOutputFileTimestamps)
                sb.AppendLine($"  <li>Timestamps (timezone): {HtmlEncode(DateTimeOffset.Now.ToString("zzz"))}</li>");

            // MVID note (same style as other meta items)
            sb.AppendLine($"  <li>Note: When diffing IL, lines starting with <code>{HtmlEncode(Constants.IL_MVID_LINE_PREFIX)}</code> (if present) are ignored because they contain disassembler-emitted Module Version ID metadata that can change on rebuild without meaning the executable IL changed.</li>");

            // IL contains-ignore note
            if (config.ShouldIgnoreILLinesContainingConfiguredStrings)
            {
                var ilIgnoreStrings = GetNormalizedIlIgnoreStrings(config);
                if (ilIgnoreStrings.Count == 0)
                {
                    sb.AppendLine("  <li>Note: IL line-ignore-by-contains is enabled, but no non-empty strings are configured.</li>");
                }
                else
                {
                    var plainItems = string.Join(", ", ilIgnoreStrings.Select(s => HtmlEncode($"\"{s}\"")));
                    sb.AppendLine($"  <li>Note: When diffing IL, lines containing any of the configured strings are ignored: {plainItems}.</li>");
                }
            }

            // Legend (as meta bullet items)
            sb.AppendLine("  <li>Legend:");
            sb.AppendLine("    <ul>");
            sb.AppendLine($"      <li><code>MD5Match</code> / <code>MD5Mismatch</code>: MD5 hash match / mismatch</li>");
            sb.AppendLine($"      <li><code>ILMatch</code> / <code>ILMismatch</code>: IL(Intermediate Language) match / mismatch</li>");
            sb.AppendLine($"      <li><code>TextMatch</code> / <code>TextMismatch</code>: Text match / mismatch</li>");
            sb.AppendLine("    </ul>");
            sb.AppendLine("  </li>");
            sb.AppendLine("</ul>");
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

            AppendTableStart(sb, TH_BG_DEFAULT, "Location");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var entry in items)
            {
                bool hasOld = (entry.Value & FileDiffResultLists.IgnoredFileLocation.Old) != 0;
                bool hasNew = (entry.Value & FileDiffResultLists.IgnoredFileLocation.New) != 0;

                // 3-2: absolute path for single-side, relative for both-sides
                string displayPath = (hasOld && hasNew)
                    ? entry.Key
                    : hasOld ? Path.Combine(oldFolderAbsolutePath, entry.Key)
                             : Path.Combine(newFolderAbsolutePath, entry.Key);

                string ts = BuildIgnoredTimestamp(entry.Key, hasOld, hasNew,
                    oldFolderAbsolutePath, newFolderAbsolutePath, config.ShouldOutputFileTimestamps);
                string location = (hasOld && hasNew) ? "old/new" : hasOld ? "old" : "new";
                AppendFileRow(sb, "ign", idx, displayPath, ts, location);
                idx++;
            }
            sb.AppendLine("</tbody></table></div>");
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

            AppendTableStart(sb, TH_BG_DEFAULT, "Diff Reason");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var path in items)
            {
                string ts = "";
                if (config.ShouldOutputFileTimestamps)
                {
                    string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, path));
                    string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, path));
                    ts = oldTs != newTs ? $"[{oldTs}{TIMESTAMP_ARROW}{newTs}]" : $"[{newTs}]";
                }
                _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(path, out var diffDetail);
                string col6 = BuildDiffDetailDisplay(diffDetail);
                AppendFileRow(sb, "unch", idx, path, ts, col6);
                idx++;
            }
            sb.AppendLine("</tbody></table></div>");
        }

        private void AppendAddedSection(StringBuilder sb, ConfigSettings config)
        {
            var items = _fileDiffResultLists.AddedFilesAbsolutePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_ADDED}\">[ + ] Added Files ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine("<p class=\"empty\">(none)</p>"); return; }

            AppendTableStart(sb, TH_BG_ADDED, "Diff Reason");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var absPath in items)
            {
                string ts = config.ShouldOutputFileTimestamps
                    ? $"[{Caching.TimestampCache.GetOrAdd(absPath)}]" : "";
                AppendFileRow(sb, "add", idx, absPath, ts, "");
                idx++;
            }
            sb.AppendLine("</tbody></table></div>");
        }

        private void AppendRemovedSection(StringBuilder sb, ConfigSettings config)
        {
            var items = _fileDiffResultLists.RemovedFilesAbsolutePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_REMOVED}\">[ - ] Removed Files ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine("<p class=\"empty\">(none)</p>"); return; }

            AppendTableStart(sb, TH_BG_REMOVED, "Diff Reason");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var absPath in items)
            {
                string ts = config.ShouldOutputFileTimestamps
                    ? $"[{Caching.TimestampCache.GetOrAdd(absPath)}]" : "";
                AppendFileRow(sb, "rem", idx, absPath, ts, "");
                idx++;
            }
            sb.AppendLine("</tbody></table></div>");
        }

        private void AppendModifiedSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            ConfigSettings config,
            ILCache ilCache)
        {
            var items = _fileDiffResultLists.ModifiedFilesRelativePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ * ] Modified Files ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine("<p class=\"empty\">(none)</p>"); return; }

            AppendTableStart(sb, TH_BG_MODIFIED, "Diff Reason");
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
                string col6 = BuildDiffDetailDisplay(diffDetail);
                AppendFileRow(sb, "mod", idx, path, ts, col6, asm ?? "");

                if (config.EnableInlineDiff &&
                    (diffDetail == FileDiffResultLists.DiffDetailResult.TextMismatch ||
                     diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch))
                {
                    AppendInlineDiffRow(sb, idx, path, oldFolderAbsolutePath, newFolderAbsolutePath,
                        reportsFolderAbsolutePath, config, diffDetail, asm ?? "", ilCache);
                }

                idx++;
            }
            sb.AppendLine("</tbody></table></div>");
        }

        private void AppendInlineDiffRow(
            StringBuilder sb,
            int idx,
            string relPath,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            ConfigSettings config,
            FileDiffResultLists.DiffDetailResult diffDetail,
            string disassemblerLabel,
            ILCache ilCache,
            string sectionPrefix = "mod")
        {
            int maxDiffLines = config.InlineDiffMaxDiffLines  > 0 ? config.InlineDiffMaxDiffLines  : 1000;
            int maxOutput    = config.InlineDiffMaxOutputLines > 0 ? config.InlineDiffMaxOutputLines : 500;
            int contextLines = config.InlineDiffContextLines >= 0 ? config.InlineDiffContextLines : 0;

            string[] oldLines, newLines;

            if (diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch)
            {
                // ILMismatch: read IL text from the *_IL.txt files written during comparison
                string ilFileName = TextSanitizer.Sanitize(relPath) + "_" + Constants.LABEL_IL + ".txt";
                string oldILPath = Path.Combine(reportsFolderAbsolutePath, Constants.LABEL_IL, "old", ilFileName);
                string newILPath = Path.Combine(reportsFolderAbsolutePath, Constants.LABEL_IL, "new", ilFileName);
                if (!File.Exists(oldILPath) || !File.Exists(newILPath)) return; // IL text not written; skip
                oldLines = File.ReadAllLines(oldILPath);
                newLines = File.ReadAllLines(newILPath);
            }
            else
            {
                // TextMismatch: read from disk
                try
                {
                    oldLines = File.ReadAllLines(Path.Combine(oldFolderAbsolutePath, relPath));
                    newLines = File.ReadAllLines(Path.Combine(newFolderAbsolutePath, relPath));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    _logger.LogMessage(AppLogLevel.Warning,
                        $"Inline diff skipped for '{relPath}': {ex.Message}",
                        shouldOutputMessageToConsole: false, ex);
                    return;
                }
            }

            IReadOnlyList<TextDiffer.DiffLine> diffLines;
#pragma warning disable CA1031 // ベストエフォートなインライン差分レンダリングのため全例外を握りつぶす意図的なキャッチ / Intentional catch-all for best-effort inline-diff rendering
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
#pragma warning restore CA1031

            if (diffLines.Count == 0) return;

            // Single Truncated line: file too large for LCS — show message directly without expand arrow
            if (diffLines.Count == 1 && diffLines[0].Kind == TextDiffer.Truncated)
            {
                sb.AppendLine("<tr class=\"diff-row\">");
                sb.AppendLine($"  <td colspan=\"8\"><p class=\"diff-skipped\">#{idx} {HtmlEncode(diffLines[0].Text)}</p></td>");
                sb.AppendLine("</tr>");
                return;
            }

            if (diffLines.Count > maxDiffLines)
            {
                sb.AppendLine("<tr class=\"diff-row\">");
                sb.AppendLine($"  <td colspan=\"8\"><p class=\"diff-skipped\">#{idx} Inline diff skipped: diff too large " +
                    $"({diffLines.Count} diff lines; limit is {maxDiffLines}). " +
                    "Increase <code>InlineDiffMaxDiffLines</code> in config to enable.</p></td>");
                sb.AppendLine("</tr>");
                return;
            }

            int addedCount = 0, removedCount = 0;
            foreach (var line in diffLines)
            {
                if (line.Kind == TextDiffer.Added) addedCount++;
                else if (line.Kind == TextDiffer.Removed) removedCount++;
            }

            string detailsId = $"diff_{sectionPrefix}_{idx}";

            sb.AppendLine("<tr class=\"diff-row\">");
            sb.AppendLine("  <td colspan=\"8\">");
            sb.AppendLine($"    <details id=\"{HtmlEncode(detailsId)}\">");
            string diffLabel = diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch ? "Show IL diff" : "Show diff";
            sb.AppendLine($"      <summary class=\"diff-summary\">#{idx} {HtmlEncode(diffLabel)} (<span class=\"diff-added-cnt\">+{addedCount}</span> / <span class=\"diff-removed-cnt\">-{removedCount}</span>)</summary>");
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
            sb.AppendLine("<h2 class=\"section-heading\">Summary</h2>");
            sb.AppendLine("<table class=\"stat-table\">");
            sb.AppendLine("  <tbody>");
            var stats = _fileDiffResultLists.SummaryStatistics;
            if (config.ShouldIncludeIgnoredFiles)
                sb.AppendLine($"    <tr><td class=\"stat-label\">Ignored</td><td class=\"stat-value\">{stats.IgnoredCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Unchanged</td><td class=\"stat-value\">{stats.UnchangedCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Added</td><td class=\"stat-value\">{stats.AddedCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Removed</td><td class=\"stat-value\">{stats.RemovedCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Modified</td><td class=\"stat-value\">{stats.ModifiedCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Compared</td><td class=\"stat-value\">{_fileDiffResultLists.OldFilesAbsolutePath.Count} (Old) vs {_fileDiffResultLists.NewFilesAbsolutePath.Count} (New)</td></tr>");
            sb.AppendLine("  </tbody>");
            sb.AppendLine("</table>");
        }

        private static void AppendILCacheStatsSection(StringBuilder sb, ILCache ilCache)
        {
            var stats = ilCache.GetReportStats();
            sb.AppendLine("<h2 class=\"section-heading\">IL Cache Stats</h2>");
            sb.AppendLine("<table class=\"stat-table\">");
            sb.AppendLine("  <tbody>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Hits</td><td class=\"stat-value\">{stats.Hits}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Misses</td><td class=\"stat-value\">{stats.Misses}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Hit Rate</td><td class=\"stat-value\">{stats.HitRatePct:F1}%</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Stores</td><td class=\"stat-value\">{stats.Stores}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Evicted</td><td class=\"stat-value\">{stats.Evicted}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Expired</td><td class=\"stat-value\">{stats.Expired}</td></tr>");
            sb.AppendLine("  </tbody>");
            sb.AppendLine("</table>");
        }

        private void AppendWarningsSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            ConfigSettings config,
            ILCache ilCache)
        {
            bool hasMd5 = _fileDiffResultLists.HasAnyMd5Mismatch;
            bool hasTs  = _fileDiffResultLists.HasAnyNewFileTimestampOlderThanOldWarning;
            if (!hasMd5 && !hasTs) return;

            sb.AppendLine("<h2 class=\"section-heading\"><span class=\"warn-icon\">&#x26A0;</span> Warnings</h2>");
            sb.AppendLine("<ul class=\"warnings\">");
            if (hasMd5)
                sb.AppendLine($"  <li>{HtmlEncode(Constants.WARNING_MD5_MISMATCH)}</li>");
            if (hasTs)
            {
                var warnings = _fileDiffResultLists.NewFileTimestampOlderThanOldWarnings.Values
                    .OrderBy(w => w.FileRelativePath, StringComparer.OrdinalIgnoreCase).ToList();
                sb.AppendLine($"  <li>One or more <strong>modified</strong> files in <code>new</code> have older last-modified timestamps than the corresponding files in <code>old</code>.</li>");
                sb.AppendLine("</ul>");

                // Timestamp-regressed files table (same style as Modified Files)
                sb.AppendLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ ! ] Modified Files — Timestamps Regressed ({warnings.Count})</h2>");
                AppendTableStart(sb, TH_BG_MODIFIED, "Diff Reason");
                sb.AppendLine("<tbody>");
                int idx = 0;
                foreach (var w in warnings)
                {
                    string ts = $"[{HtmlEncode(w.OldTimestamp)}{TIMESTAMP_ARROW}{HtmlEncode(w.NewTimestamp)}]";
                    _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(w.FileRelativePath, out var diffDetail);
                    _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(w.FileRelativePath, out var asm);
                    string col6 = BuildDiffDetailDisplay(diffDetail);
                    AppendFileRow(sb, "tsw", idx, w.FileRelativePath, ts, col6, asm ?? "");

                    if (config.EnableInlineDiff &&
                        (diffDetail == FileDiffResultLists.DiffDetailResult.TextMismatch ||
                         diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch))
                    {
                        AppendInlineDiffRow(sb, idx, w.FileRelativePath, oldFolderAbsolutePath, newFolderAbsolutePath,
                            reportsFolderAbsolutePath, config, diffDetail, asm ?? "", ilCache, sectionPrefix: "tsw");
                    }

                    idx++;
                }
                sb.AppendLine("</tbody></table></div>");
                return;
            }
            sb.AppendLine("</ul>");
        }

        // ── Table helpers ────────────────────────────────────────────────────

        /// <param name="sb">追記先の <see cref="StringBuilder"/>。</param>
        /// <param name="headerBgColor">ヘッダ行の背景色。null の場合はデフォルト色。</param>
        /// <param name="col6Header">6 列目（差異理由 / 所在）のヘッダラベル。</param>
        private static void AppendTableStart(StringBuilder sb, string headerBgColor, string col6Header)
        {
            string bg = headerBgColor ?? TH_BG_DEFAULT;
            sb.AppendLine("<div class=\"table-scroll\">");
            sb.AppendLine("<table>");
            sb.AppendLine("<colgroup>");
            sb.AppendLine("  <col class=\"col-no-g\">");
            sb.AppendLine("  <col class=\"col-cb-g\">");
            sb.AppendLine("  <col class=\"col-reason-g\">");
            sb.AppendLine("  <col class=\"col-notes-g\">");
            sb.AppendLine("  <col class=\"col-path-g\">");
            sb.AppendLine("  <col class=\"col-ts-g\">");
            sb.AppendLine("  <col class=\"col-diff-g\">");
            sb.AppendLine("  <col class=\"col-disasm-g\">");
            sb.AppendLine("</colgroup>");
            sb.AppendLine($"<thead><tr style=\"background:{bg}\">");
            sb.AppendLine($"  <th class=\"col-no\">#</th>");
            sb.AppendLine($"  <th class=\"col-cb\">&#x2713;</th>");
            sb.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--col-reason-w\">Justification</th>");
            sb.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--col-notes-w\">Notes</th>");
            sb.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--col-path-w\">File Path</th>");
            sb.AppendLine($"  <th>Timestamp</th>");
            sb.AppendLine($"  <th>{HtmlEncode(col6Header)}</th>");
            sb.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--col-disasm-w\">Disassembler</th>");
            sb.AppendLine("</tr></thead>");
        }

        private static void AppendFileRow(
            StringBuilder sb,
            string sectionPrefix,
            int idx,
            string path,
            string timestamp,
            string col6,
            string disasm = "")
        {
            string cbId     = $"cb_{sectionPrefix}_{idx}";
            string reasonId = $"reason_{sectionPrefix}_{idx}";
            string notesId  = $"notes_{sectionPrefix}_{idx}";
            int recordNo    = idx + 1;
            sb.AppendLine("<tr>");
            sb.AppendLine($"  <td class=\"col-no\">{recordNo}</td>");
            sb.AppendLine($"  <td class=\"col-cb\"><input type=\"checkbox\" id=\"{cbId}\"></td>");
            sb.AppendLine($"  <td class=\"col-reason\"><input type=\"text\" id=\"{reasonId}\"></td>");
            sb.AppendLine($"  <td class=\"col-notes\"><input type=\"text\" id=\"{notesId}\"></td>");
            sb.AppendLine($"  <td class=\"col-path\">{HtmlEncode(path)}</td>");
            sb.AppendLine($"  <td class=\"col-ts\">{HtmlEncode(timestamp)}</td>");
            string col6Cell = string.IsNullOrEmpty(col6) ? "" : $"<code>{HtmlEncode(col6)}</code>";
            sb.AppendLine($"  <td class=\"col-diff\">{col6Cell}</td>");
            string disasmCell = string.IsNullOrEmpty(disasm) ? "" : $"<code>{HtmlEncode(disasm)}</code>";
            sb.AppendLine($"  <td class=\"col-disasm\">{disasmCell}</td>");
            sb.AppendLine("</tr>");
        }

        private static string BuildIgnoredTimestamp(
            string relPath,
            bool hasOld, bool hasNew,
            string oldFolder, string newFolder,
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

        private static string BuildDiffDetailDisplay(
            FileDiffResultLists.DiffDetailResult diffDetail)
        {
            return diffDetail.ToString();
        }

        private static List<string> GetNormalizedIlIgnoreStrings(ConfigSettings config)
        {
            if (config?.ILIgnoreLineContainingStrings == null) return new List<string>();
            return config.ILIgnoreLineContainingStrings
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        // ── JavaScript ───────────────────────────────────────────────────────

        private static void AppendJs(StringBuilder sb, string storageKey, string reportDate)
        {
            sb.AppendLine("<script>");
            sb.AppendLine($"  const __storageKey__  = '{storageKey}';");
            sb.AppendLine($"  const __reportDate__  = '{reportDate}';");
            // NOTE: 'const __savedState__ = null;' is replaced by downloadReviewed(). Do not change whitespace.
            sb.AppendLine("  const __savedState__  = null;");
            sb.AppendLine();
            sb.AppendLine("  function formatTs(d) {");
            sb.AppendLine("    return d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0')");
            sb.AppendLine("      +' '+String(d.getHours()).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0')+':'+String(d.getSeconds()).padStart(2,'0');");
            sb.AppendLine("  }");
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
            // If this is a reviewed/downloaded copy, lock all inputs
            sb.AppendLine("    if (__savedState__ !== null) {");
            sb.AppendLine("      document.querySelectorAll('input[type=\"checkbox\"]').forEach(function(cb){ cb.style.pointerEvents='none'; cb.style.cursor='default'; });");
            sb.AppendLine("      document.querySelectorAll('input[type=\"text\"]').forEach(function(inp){");
            sb.AppendLine("        inp.readOnly=true; inp.style.cursor='default'; inp.style.userSelect='text';");
            sb.AppendLine("      });");
            sb.AppendLine("    } else {");
            sb.AppendLine("      document.querySelectorAll('input, textarea').forEach(function(el) {");
            sb.AppendLine("        el.addEventListener('change', autoSave);");
            sb.AppendLine("        el.addEventListener('input',  autoSave);");
            sb.AppendLine("      });");
            sb.AppendLine("    }");
            sb.AppendLine("    initColResize();");
            sb.AppendLine("    syncTableWidths();");
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
            sb.AppendLine("    if (status) status.textContent = 'Auto-saved at ' + formatTs(new Date());");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  function downloadReviewed() {");
            sb.AppendLine("    var state   = collectState();");
            sb.AppendLine("    var slug    = 'diff_report_' + __reportDate__;");
            sb.AppendLine("    var root    = document.documentElement;");
            sb.AppendLine("    // 1. Collapse all diff-detail elements so exported file starts with diffs closed");
            sb.AppendLine("    var openDetails = Array.from(document.querySelectorAll('details[open]'));");
            sb.AppendLine("    openDetails.forEach(function(d){ d.removeAttribute('open'); });");
            sb.AppendLine("    // 2. Capture current effective column widths to bake into reviewed HTML as defaults");
            sb.AppendLine("    var colVarNames = ['--col-reason-w','--col-notes-w','--col-path-w','--col-diff-w','--col-disasm-w'];");
            sb.AppendLine("    var cs = getComputedStyle(root);");
            sb.AppendLine("    var curWidths = {};");
            sb.AppendLine("    colVarNames.forEach(function(v){ curWidths[v] = (root.style.getPropertyValue(v) || cs.getPropertyValue(v)).trim(); });");
            sb.AppendLine("    var html    = document.documentElement.outerHTML;");
            sb.AppendLine("    // Restore open details in the live page");
            sb.AppendLine("    openDetails.forEach(function(d){ d.setAttribute('open', ''); });");
            sb.AppendLine("    // Embed state");
            sb.AppendLine("    html = html.replace('const __savedState__  = null;',");
            sb.AppendLine("      'const __savedState__  = ' + JSON.stringify(state) + ';');");
            sb.AppendLine("    // Update title");
            sb.AppendLine("    html = html.replace('<title>diff_report</title>',");
            sb.AppendLine("      '<title>' + slug + '_reviewed</title>');");
            sb.AppendLine("    // Bake current column widths as CSS defaults in the reviewed HTML");
            sb.AppendLine("    html = html.replace(/:root \\{ --col-reason-w:[^}]+\\}/,");
            sb.AppendLine("      ':root { --col-reason-w: '  + curWidths['--col-reason-w']");
            sb.AppendLine("      + '; --col-notes-w: '   + curWidths['--col-notes-w']");
            sb.AppendLine("      + '; --col-path-w: '    + curWidths['--col-path-w']");
            sb.AppendLine("      + '; --col-diff-w: '    + curWidths['--col-diff-w']");
            sb.AppendLine("      + '; --col-disasm-w: '  + curWidths['--col-disasm-w'] + '; }');");
            sb.AppendLine("    // Remove inline col-var overrides from <html> element (now baked into :root)");
            sb.AppendLine("    html = html.replace(/(<html\\b[^>]*?) style=\"[^\"]*\"/, '$1');");
            sb.AppendLine("    // Replace controls bar with reviewed banner");
            sb.AppendLine("    html = html.replace(/<!--CTRL-->[\\s\\S]*?<!--\\/CTRL-->/g,");
            sb.AppendLine("      '<div class=\"reviewed-banner\">&#x1F512; Reviewed: ' + formatTs(new Date()) + ' &#x2014; read-only</div>');");
            sb.AppendLine("    var blob = new Blob([html], { type: 'text/html;charset=utf-8' });");
            sb.AppendLine("    var a    = document.createElement('a');");
            sb.AppendLine("    a.href   = URL.createObjectURL(blob);");
            sb.AppendLine("    a.download = slug + '_reviewed.html';");
            sb.AppendLine("    document.body.appendChild(a);");
            sb.AppendLine("    a.click();");
            sb.AppendLine("    document.body.removeChild(a);");
            sb.AppendLine("    setTimeout(function(){ URL.revokeObjectURL(a.href); }, 1000);");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  function clearAll() {");
            sb.AppendLine("    if (!confirm('Clear all checkboxes and text inputs?')) return;");
            sb.AppendLine("    document.querySelectorAll('input[type=\"checkbox\"]').forEach(function(cb){ cb.checked=false; });");
            sb.AppendLine("    document.querySelectorAll('input[type=\"text\"], textarea').forEach(function(inp){ inp.value=''; });");
            sb.AppendLine("    // Reset column widths to defaults");
            sb.AppendLine("    var root = document.documentElement;");
            sb.AppendLine("    ['--col-reason-w','--col-notes-w','--col-path-w','--col-diff-w','--col-disasm-w'].forEach(function(v){ root.style.removeProperty(v); });");
            sb.AppendLine("    syncTableWidths();");
            sb.AppendLine("    // Close all open diff/IL-diff details");
            sb.AppendLine("    document.querySelectorAll('details[open]').forEach(function(d){ d.removeAttribute('open'); });");
            sb.AppendLine("    localStorage.removeItem(__storageKey__);");
            sb.AppendLine("    var status = document.getElementById('save-status');");
            sb.AppendLine("    if (status) status.textContent = 'Cleared.';");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  function syncTableWidths() {");
            sb.AppendLine("    var root = document.documentElement;");
            sb.AppendLine("    var emPx = parseFloat(getComputedStyle(root).fontSize) || 16;");
            sb.AppendLine("    var px = function(v, fb) {");
            sb.AppendLine("      var s = root.style.getPropertyValue(v) || getComputedStyle(root).getPropertyValue(v);");
            sb.AppendLine("      return (parseFloat(s) || fb) * emPx;");
            sb.AppendLine("    };");
            sb.AppendLine("    var w = (3.2 + 2.2 + 16) * emPx");
            sb.AppendLine("          + px('--col-reason-w', 10) + px('--col-notes-w', 10)");
            sb.AppendLine("          + px('--col-path-w', 22)   + px('--col-diff-w', 9)");
            sb.AppendLine("          + px('--col-disasm-w', 28);");
            sb.AppendLine("    document.querySelectorAll('table:not(.stat-table):not(.diff-table)').forEach(function(t) {");
            sb.AppendLine("      t.style.width = w + 'px';");
            sb.AppendLine("    });");
            sb.AppendLine("  }");
            sb.AppendLine();
            sb.AppendLine("  function initColResize() {");
            sb.AppendLine("    document.querySelectorAll('th.th-resizable').forEach(function(th) {");
            sb.AppendLine("      // Wrap text in a block span so overflow:hidden clips reliably at column boundary");
            sb.AppendLine("      var label = document.createElement('span');");
            sb.AppendLine("      label.className = 'th-label';");
            sb.AppendLine("      while (th.childNodes.length) label.appendChild(th.childNodes[0]);");
            sb.AppendLine("      th.appendChild(label);");
            sb.AppendLine("      var handle = document.createElement('div');");
            sb.AppendLine("      handle.className = 'col-resize-handle';");
            sb.AppendLine("      th.appendChild(handle);");
            sb.AppendLine("      var varName = th.dataset.colVar;");
            sb.AppendLine("      handle.addEventListener('mousedown', function(e) {");
            sb.AppendLine("        e.preventDefault();");
            sb.AppendLine("        var startX = e.clientX;");
            sb.AppendLine("        var root   = document.documentElement;");
            sb.AppendLine("        var emPx   = parseFloat(getComputedStyle(root).fontSize) || 16;");
            sb.AppendLine("        var cur    = root.style.getPropertyValue(varName) || getComputedStyle(root).getPropertyValue(varName);");
            sb.AppendLine("        var startPx = (parseFloat(cur) || 10) * emPx;");
            sb.AppendLine("        function onMove(ev) {");
            sb.AppendLine("          var newPx = Math.max(48, startPx + (ev.clientX - startX));");
            sb.AppendLine("          root.style.setProperty(varName, (newPx / emPx).toFixed(2) + 'em');");
            sb.AppendLine("          syncTableWidths();");
            sb.AppendLine("        }");
            sb.AppendLine("        function onUp() {");
            sb.AppendLine("          document.removeEventListener('mousemove', onMove);");
            sb.AppendLine("          document.removeEventListener('mouseup', onUp);");
            sb.AppendLine("        }");
            sb.AppendLine("        document.addEventListener('mousemove', onMove);");
            sb.AppendLine("        document.addEventListener('mouseup', onUp);");
            sb.AppendLine("      });");
            sb.AppendLine("    });");
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
    h1 { font-size: 2.0rem; padding: 1rem 0 0.4rem; }
    h2 { font-size: 1rem; margin: 1.4rem 0 0.35rem; }
    h2.section-heading { font-size: 1.55rem; margin: 1.6rem 0 0.4rem; }
    code { font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', monospace;
           font-size: 12px; background: #f0f0f0; padding: 0 3px; border-radius: 2px; }
    ul.meta { margin: 0.4rem 0 0.8rem 1.4rem; }
    ul.meta li { margin-bottom: 3px; line-height: 1.65; }
    ul.meta ul { margin: 3px 0 3px 1.4rem; list-style: disc; }
    ul.meta ul li { margin-bottom: 1px; }
    /* ── Controls bar (frosted glass, fills full width) ─────────────────── */
    .controls {
      position: sticky; top: 0;
      backdrop-filter: blur(20px) saturate(180%);
      -webkit-backdrop-filter: blur(20px) saturate(180%);
      background: rgba(255,255,255,0.1);
      padding: 0.65rem 2rem; margin: 0 -2rem;
      display: flex; gap: 0.8rem; align-items: center; z-index: 100;
    }
    .reviewed-banner {
      position: sticky; top: 0;
      backdrop-filter: blur(20px) saturate(180%);
      -webkit-backdrop-filter: blur(20px) saturate(180%);
      background: rgba(255,255,255,0.1);
      padding: 0.5rem 2rem; margin: 0 -2rem;
      font-size: 13px; color: #1f2328; font-weight: 500; z-index: 100;
    }
    /* ── Apple-style buttons ─────────────────────────────────────────────── */
    .btn {
      display: inline-flex; align-items: center; gap: 0.35em;
      padding: 0.45rem 1.1rem; cursor: pointer;
      background: #1d1d1f; color: #fff;
      border: 1.5px solid #1d1d1f; font-size: 13px; border-radius: 980px;
      font-family: inherit; letter-spacing: -0.01em;
      transition: background 0.12s, color 0.12s; white-space: nowrap; line-height: 1;
    }
    .btn:hover { background: #424245; border-color: #424245; }
    .btn-clear {
      background: transparent; color: #1d1d1f;
    }
    .btn-clear:hover { background: #f5f5f7; }
    .save-status { font-size: 12px; color: #86868b; }
    .empty { color: #999; font-size: 12px; margin-bottom: 0.8rem; }
    /* ── Column width CSS variables ──────────────────────────────────────── */
    :root { --col-reason-w: 10em; --col-notes-w: 10em; --col-path-w: 22em; --col-diff-w: 9em; --col-disasm-w: 28em; }
    col.col-no-g     { width: 3.2em; }
    col.col-cb-g     { width: 2.2em; }
    col.col-reason-g { width: var(--col-reason-w); }
    col.col-notes-g  { width: var(--col-notes-w); }
    col.col-path-g   { width: var(--col-path-w); }
    col.col-ts-g     { width: 16em; }
    col.col-diff-g   { width: var(--col-diff-w); }
    col.col-disasm-g { width: var(--col-disasm-w); }
    /* ── Data tables ─────────────────────────────────────────────────────── */
    .table-scroll { overflow-x: auto; margin-bottom: 1.2rem; }
    table { border-collapse: collapse; width: 100%; margin-bottom: 1.2rem; }
    table:not(.stat-table):not(.diff-table) { table-layout: fixed; width: 1px; margin-bottom: 0; }
    th { padding: 4px 6px; font-size: 12px; white-space: nowrap; overflow: hidden; text-align: left;
         border: 1px solid #bbb; color: #000; }
    th.th-resizable { position: relative; }
    .th-label { display: block; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
    .col-resize-handle {
      position: absolute; right: 0; top: 0; bottom: 0; width: 5px;
      cursor: col-resize; background: transparent;
    }
    .col-resize-handle:hover, .col-resize-handle:active { background: rgba(0,0,0,0.18); }
    td { padding: 2px 4px; border: 1px solid #e0e0e0; vertical-align: middle; font-size: 12px; }
    td.col-no   { width: 3.2em; text-align: right; color: #aaa;
                  font-family: 'SFMono-Regular', Consolas, monospace; font-size: 11px; }
    td.col-cb   { width: 2.2em; text-align: center; }
    td.col-reason { overflow: hidden; text-align: center; }
    td.col-notes  { overflow: hidden; }
    td.col-path { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    td.col-ts    { white-space: nowrap; width: 16em; overflow: hidden; text-align: center; }
    td.col-diff  { font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', monospace;
                   font-size: 12px; white-space: nowrap; min-width: 9em; text-align: center; }
    td.col-disasm { white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
                    font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', monospace; font-size: 12px; }
    td.col-reason input[type=""text""], td.col-notes input[type=""text""] {
      width: 100%; border: none; padding: 2px 4px; font-size: 12px;
      background: transparent; outline: none; font-family: inherit; }
    td.col-reason input[type=""text""]:focus, td.col-notes input[type=""text""]:focus {
      background: #fffff8; outline: 1px solid #aaa; }
    input[type=""checkbox""] { width: 1.1em; height: 1.1em; cursor: pointer; }
    /* ── Summary / IL Cache Stats (stat table) ───────────────────────────── */
    table.stat-table { width: auto; margin-bottom: 1rem; margin-left: 1.2em; border-collapse: collapse; }
    table.stat-table td { border: none; padding: 2px 20px 2px 0; font-size: 13px; }
    table.stat-table td.stat-label { color: #444; white-space: nowrap; }
    table.stat-table td.stat-value { text-align: right; }
    ul.warnings { margin: 0.3rem 0 0 1.4rem; }
    ul.warnings li { margin-bottom: 0.4rem; line-height: 1.6; }
    .warn-icon { color: #f5a623; font-size: 1.1em; }
    /* ── Inline diff ─────────────────────────────────────────────────────── */
    tr.diff-row { background: #f6f8fa; }
    tr.diff-row > td { padding: 0; border-top: none; }
    .diff-added-cnt { color: #22863a; font-weight: 600; }
    .diff-removed-cnt { color: #b31d28; font-weight: 600; }
    summary.diff-summary {
      display: inline-flex; align-items: center; gap: 0.4em;
      cursor: pointer; font-size: 12px; color: #0051c3;
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
    tr.diff-hunk-tr { background: #f6f8fa; }
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
