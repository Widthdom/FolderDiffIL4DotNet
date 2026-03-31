using System;
using System.IO;
using System.Linq;
using System.Text;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Generates an interactive HTML diff report (<see cref="DIFF_REPORT_HTML_FILE_NAME"/>).
    /// Each file row has a checkbox, Justification, and Notes columns with localStorage auto-save
    /// and a "Download as reviewed" feature.
    /// 差分結果のインタラクティブ HTML レポート (<see cref="DIFF_REPORT_HTML_FILE_NAME"/>) を生成するサービス。
    /// 各ファイル行にチェックボックス・Justification・Notes 列を持ち、
    /// localStorage による自動保存と「レビュー済みとして保存」ダウンロード機能を提供します。
    /// </summary>
    public sealed partial class HtmlReportGenerateService
    {
        private readonly FileDiffResultLists _fileDiffResultLists;
        private readonly ILoggerService _logger;

        internal const string DIFF_REPORT_HTML_FILE_NAME = "diff_report.html";

        private const string TIMESTAMP_ARROW = " → ";
        // CSS variable references for dark mode support / ダークモード対応の CSS 変数参照
        private const string COLOR_ADDED    = "var(--color-added)";
        private const string COLOR_REMOVED  = "var(--color-removed)";
        private const string COLOR_MODIFIED = "var(--color-modified)";
        private const string TH_BG_ADDED    = "var(--color-added-bg)";
        private const string TH_BG_REMOVED  = "var(--color-removed-bg)";
        private const string TH_BG_MODIFIED = "var(--color-modified-bg)";
        private const string TH_BG_DEFAULT  = "var(--color-default-bg)";

        /// <summary>
        /// Initializes a new instance of <see cref="HtmlReportGenerateService"/>.
        /// <see cref="HtmlReportGenerateService"/> の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="fileDiffResultLists">Comparison results to render in the HTML report. / HTML レポートに描画する比較結果。</param>
        /// <param name="logger">Logger for diagnostic output. / 診断出力用ロガー。</param>
        /// <param name="config">Read-only configuration settings. / 読み取り専用の設定。</param>
        public HtmlReportGenerateService(FileDiffResultLists fileDiffResultLists, ILoggerService logger, IReadOnlyConfigSettings config)
        {
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            _fileDiffResultLists = fileDiffResultLists;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            ArgumentNullException.ThrowIfNull(config);
        }

        // ── Public entry point ───────────────────────────────────────────────

        /// <summary>
        /// Generates diff_report.html using the specified <paramref name="context"/>.
        /// No-op when <see cref="IReadOnlyConfigSettings.ShouldGenerateHtmlReport"/> is <see langword="false"/>.
        /// 指定された <paramref name="context"/> を使って diff_report.html を生成します。
        /// <see cref="IReadOnlyConfigSettings.ShouldGenerateHtmlReport"/> が <see langword="false"/> の場合は何もしません。
        /// </summary>
        public void GenerateDiffReportHtml(ReportGenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!context.Config.ShouldGenerateHtmlReport) return;

            string htmlPath = Path.Combine(context.ReportsFolderAbsolutePath, DIFF_REPORT_HTML_FILE_NAME);
            try
            {
                // Stream HTML chunks directly to disk to reduce peak memory usage for large reports.
                // 大規模レポートのピークメモリ使用量を削減するため、HTML チャンクを直接ディスクにストリーム書き出しする。
                using var writer = new StreamWriter(htmlPath, append: false, Encoding.UTF8, bufferSize: 65536);
                WriteHtml(writer,
                    context.OldFolderAbsolutePath, context.NewFolderAbsolutePath, context.ReportsFolderAbsolutePath,
                    context.AppVersion, context.ElapsedTimeString, context.ComputerName, context.Config, context.IlCache);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Failed to write HTML report to '{htmlPath}': {ex.Message}",
                    shouldOutputMessageToConsole: true, ex);
            }
        }

        // ── Build ────────────────────────────────────────────────────────────

        private void WriteHtml(
            TextWriter writer,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            IReadOnlyConfigSettings config,
            ILCache? ilCache)
        {
            string label = Path.GetFileName(
                reportsFolderAbsolutePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "diff";
            string storageKey = "folderdiff-" + label
                .Replace("'", "-").Replace("\"", "-").Replace("\\", "-").Replace("`", "-");
            string reportDate = DateTime.Now.ToString("yyyyMMdd");

            AppendHtmlHead(writer);
            writer.WriteLine("<body>");
            // Skip link for keyboard navigation / キーボードナビゲーション用スキップリンク
            writer.WriteLine("<a href=\"#main-content\" class=\"skip-link\">Skip to main content</a>");

            AppendControlsBar(writer);
            AppendFilterZone(writer);
            writer.WriteLine("</div>");  // end .controls

            AppendMainSections(writer, oldFolderAbsolutePath, newFolderAbsolutePath,
                reportsFolderAbsolutePath, appVersion, elapsedTimeString, computerName, config, ilCache);

            AppendProgressAndJs(writer, storageKey, reportDate);
            AppendKeyboardHelpOverlay(writer);
            writer.WriteLine("</body>");
            writer.WriteLine("</html>");
        }

        // ── Controls bar (button row stripped in downloadReviewed) ─────────────
        // コントロールバー（ボタン行は downloadReviewed で除去）

        private static void AppendControlsBar(TextWriter writer)
        {
            writer.WriteLine("<div class=\"controls\" role=\"toolbar\" aria-label=\"Report controls\">");
            writer.WriteLine("<!--CTRL-->");
            writer.WriteLine("<div class=\"ctrl-buttons\">");
            writer.WriteLine("  <div class=\"ctrl-progress\">");
            writer.WriteLine("    <div class=\"progress-wrap\">");
            writer.WriteLine("      <div class=\"progress-bar\"><div id=\"progress-bar-fill\" class=\"progress-bar-fill\"></div></div>");
            writer.WriteLine("      <span id=\"progress-text\" class=\"progress-text\"></span>");
            writer.WriteLine("      <span id=\"progress-detail\" class=\"progress-detail\"></span>");
            writer.WriteLine("      <span class=\"storage-group\"><span class=\"storage-label\">Storage:</span><span class=\"storage-bar\"><span id=\"storage-bar-fill\" class=\"storage-bar-fill\"></span></span><span id=\"storage-text\" class=\"storage-text\"></span></span>");
            writer.WriteLine("    </div>");
            writer.WriteLine("  </div>");
            writer.WriteLine("  <div class=\"ctrl-actions\">");
            writer.WriteLine("    <span class=\"btn-tooltip-wrap\"><button class=\"btn btn-clear\" onclick=\"clearOldReviewStates()\"><svg aria-hidden=\"true\" width=\"12\" height=\"12\" viewBox=\"0 0 16 16\" fill=\"currentColor\" style=\"vertical-align:-1px\"><path d=\"M8 0l2 6 6 2-6 2-2 6-2-6-6-2 6-2z\"/></svg> " + HtmlEncode("Free up review storage") + "</button><span class=\"btn-tooltip\">" + HtmlEncode("Each report auto-saves review state to browser storage. Old reports accumulate \u2014 free space here.") + "</span></span>");
            writer.WriteLine("    <button class=\"btn\" onclick=\"downloadReviewed()\">&#x2913; " + HtmlEncode("Download as reviewed") + "</button>");
            writer.WriteLine("    <button class=\"btn btn-clear\" onclick=\"collapseAll()\"><svg aria-hidden=\"true\" width=\"12\" height=\"12\" viewBox=\"0 0 16 16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" style=\"vertical-align:-1px\"><polyline points=\"4 7 8 3 12 7\"/><polyline points=\"4 13 8 9 12 13\"/></svg> " + HtmlEncode("Fold all details") + "</button>");
            writer.WriteLine("    <button class=\"btn btn-clear\" onclick=\"resetFilters()\"><svg aria-hidden=\"true\" width=\"12\" height=\"12\" viewBox=\"0 0 16 16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" style=\"vertical-align:-1px\"><path d=\"M2 3h12l-4 5v3l-4 2V8z\"/><line x1=\"10\" y1=\"10\" x2=\"15\" y2=\"15\"/><line x1=\"15\" y1=\"10\" x2=\"10\" y2=\"15\"/></svg> " + HtmlEncode("Reset filters") + "</button>");
            writer.WriteLine("    <button class=\"btn btn-clear\" onclick=\"clearAll()\"><svg aria-hidden=\"true\" width=\"12\" height=\"12\" viewBox=\"0 0 16 16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" style=\"vertical-align:-1px\"><path d=\"M3 4h10l-1 10H4z\"/><line x1=\"1\" y1=\"4\" x2=\"15\" y2=\"4\"/><line x1=\"6\" y1=\"2\" x2=\"10\" y2=\"2\"/></svg> " + HtmlEncode("Clear all") + "</button>");
            writer.WriteLine("    <button id=\"theme-toggle\" class=\"btn btn-clear theme-toggle\" onclick=\"cycleTheme()\" title=\"Toggle theme (Light / Dark / System)\">\u2699 System</button>");
            writer.WriteLine("    <span id=\"save-status\" class=\"save-status\" role=\"status\" aria-live=\"polite\"></span>");
            writer.WriteLine("  </div>");
            writer.WriteLine("</div>");
            writer.WriteLine("<!--/CTRL-->");
        }

        // ── Filter zone (kept in reviewed HTML for read-only filtering) ───────
        // フィルターゾーン（reviewed HTML にも残し読み取り専用フィルタリングに使用）

        /// <summary>
        /// Diff Detail filter definitions used to build filter table rows.
        /// Diff Detail フィルターテーブル行の定義。
        /// </summary>
        private static readonly (string Id, string Display, string Description)[] s_diffDetailFilters =
        {
            ("filter-diff-sha256match",    "<code>SHA256Match</code>",    "Byte-for-byte match (SHA256)"),
            ("filter-diff-sha256mismatch", "<code>SHA256Mismatch</code>", "Byte-for-byte mismatch (SHA256)"),
            ("filter-diff-ilmatch",        "<code>ILMatch</code>",        "IL (Intermediate Language) match"),
            ("filter-diff-ilmismatch",     "<code>ILMismatch</code>",     "IL (Intermediate Language) mismatch"),
            ("filter-diff-textmatch",      "<code>TextMatch</code>",      "Text-based match"),
            ("filter-diff-textmismatch",   "<code>TextMismatch</code>",   "Text-based mismatch"),
        };

        /// <summary>
        /// Change Importance filter definitions used to build filter table rows.
        /// Change Importance フィルターテーブル行の定義。
        /// </summary>
        private static readonly (string Id, string Display, string Description)[] s_importanceFilters =
        {
            ("filter-imp-high",   "<span class=\"imp-high\">High</span>",   "Breaking change candidate: public/protected API removal, access narrowing, return-type / parameter / member-type change"),
            ("filter-imp-medium", "<span class=\"imp-medium\">Medium</span>", "Notable change: public/protected member addition, modifier change, access widening, internal removal"),
            ("filter-imp-low",    "Low",                                                           "Low-impact change: body-only modification, internal/private member addition"),
        };

        private static void AppendFilterZone(TextWriter writer)
        {
            writer.WriteLine("<div class=\"filter-zone\" role=\"search\" aria-label=\"Report filters\">");

            // Search + Unchecked only row / 検索 + 未チェックのみ行
            writer.WriteLine("<div class=\"ctrl-filter-row\">");
            writer.WriteLine("  <label class=\"filter-chip\"><input type=\"checkbox\" id=\"filter-unchecked\" onchange=\"applyFilters()\"> " + HtmlEncode("Unchecked only") + "</label>");
            writer.WriteLine("  <span class=\"filter-sep\"></span>");
            writer.WriteLine("  <input type=\"text\" id=\"filter-search\" placeholder=\"" + HtmlEncode("Search file path...") + "\" class=\"filter-search\" oninput=\"applyFilters()\" aria-label=\"" + HtmlEncode("Search file path") + "\">");
            writer.WriteLine("</div>");

            // Filter tables row / フィルターテーブル行
            writer.WriteLine("<div class=\"filter-tables\">");

            // Diff Detail filter table (2 items per row) / Diff Detail フィルターテーブル（1行2項目）
            writer.WriteLine("<div class=\"filter-table-wrap\">");
            writer.WriteLine("<table class=\"filter-table\" aria-label=\"Diff Detail filters\">");
            writer.WriteLine("<thead><tr><th scope=\"col\" colspan=\"6\">Diff Detail</th></tr></thead>");
            writer.WriteLine("<tbody>");
            for (int i = 0; i < s_diffDetailFilters.Length; i += 2)
            {
                writer.Write("<tr>");
                AppendFilterTableCells(writer, s_diffDetailFilters[i]);
                if (i + 1 < s_diffDetailFilters.Length)
                    AppendFilterTableCells(writer, s_diffDetailFilters[i + 1]);
                else
                    writer.Write("<td></td><td></td><td></td>");
                writer.WriteLine("</tr>");
            }
            writer.WriteLine("</tbody></table>");
            writer.WriteLine("</div>");

            // Change Importance filter table / Change Importance フィルターテーブル
            writer.WriteLine("<div class=\"filter-table-wrap\">");
            writer.WriteLine("<table class=\"filter-table\" aria-label=\"Change Importance filters\">");
            writer.WriteLine("<thead><tr><th scope=\"col\" colspan=\"3\">Change Importance</th></tr></thead>");
            writer.WriteLine("<tbody>");
            foreach (var (id, display, desc) in s_importanceFilters)
            {
                AppendFilterTableRow(writer, id, display, HtmlEncode(desc));
            }
            writer.WriteLine("</tbody></table>");
            writer.WriteLine("</div>");
            // Estimated Change legend table (2 items per row, read-only, no filter checkboxes)
            // 推定変更凡例テーブル（1行2項目、読み取り専用、フィルターチェックボックスなし）
            writer.WriteLine("<div class=\"filter-table-wrap\">");
            writer.WriteLine("<table class=\"filter-table\" aria-label=\"Estimated Change legend\">");
            writer.WriteLine("<thead><tr><th scope=\"col\" colspan=\"4\">Legend — Estimated Change</th></tr></thead>");
            writer.WriteLine("<tbody>");
            var allLabels = ChangeTagClassifier.AllLabels.ToArray();
            for (int i = 0; i < allLabels.Length; i += 2)
            {
                writer.Write("<tr>");
                writer.Write($"<td class=\"ft-label\"><code>{HtmlEncode(allLabels[i].Value)}</code></td><td class=\"ft-desc\">{HtmlEncode(GetChangeTagDescription(allLabels[i].Key))}</td>");
                if (i + 1 < allLabels.Length)
                    writer.Write($"<td class=\"ft-label\"><code>{HtmlEncode(allLabels[i + 1].Value)}</code></td><td class=\"ft-desc\">{HtmlEncode(GetChangeTagDescription(allLabels[i + 1].Key))}</td>");
                else
                    writer.Write("<td></td><td></td>");
                writer.WriteLine("</tr>");
            }
            writer.WriteLine("</tbody></table>");
            writer.WriteLine("</div>");
            writer.WriteLine("</div>"); // end .filter-tables

            writer.WriteLine("</div>");  // end .filter-zone
        }

        // ── Main content sections ─────────────────────────────────────────────
        // メインコンテンツセクション

        private void AppendMainSections(
            TextWriter writer,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            IReadOnlyConfigSettings config,
            ILCache? ilCache)
        {
            writer.WriteLine("<main id=\"main-content\">");
            AppendHeaderSection(writer, oldFolderAbsolutePath, newFolderAbsolutePath,
                appVersion, elapsedTimeString, computerName, config);

            if (config.ShouldIncludeIgnoredFiles)
                AppendIgnoredSection(writer, oldFolderAbsolutePath, newFolderAbsolutePath, config);

            if (config.ShouldIncludeUnchangedFiles)
                AppendUnchangedSection(writer, oldFolderAbsolutePath, newFolderAbsolutePath, config);

            AppendAddedSection(writer, config);
            AppendRemovedSection(writer, config);
            AppendModifiedSection(writer, oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath, config, ilCache);
            AppendSummarySection(writer, config);

            if (config.ShouldIncludeILCacheStatsInReport && ilCache != null)
                AppendILCacheStatsSection(writer, ilCache);

            AppendWarningsSection(writer, oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath, config, ilCache);

            writer.WriteLine("</main>");
        }

        // ── Progress bar calculation and JS injection ─────────────────────────
        // プログレスバー計算と JS 注入

        private void AppendProgressAndJs(TextWriter writer, string storageKey, string reportDate)
        {
            int addedCount = _fileDiffResultLists.AddedFilesAbsolutePath.Count;
            int removedCount = _fileDiffResultLists.RemovedFilesAbsolutePath.Count;
            int modifiedCount = _fileDiffResultLists.ModifiedFilesRelativePath.Count;
            int sha256WarnCount = _fileDiffResultLists.FileRelativePathToDiffDetailDictionary
                .Values.Count(r => r == FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            int tsWarnCount = _fileDiffResultLists.NewFileTimestampOlderThanOldWarnings.Count;
            int totalFiles = addedCount + removedCount + modifiedCount + sha256WarnCount + tsWarnCount;
            string totalFilesDetail = BuildTotalFilesDetail(addedCount, removedCount, modifiedCount, sha256WarnCount, tsWarnCount);
            AppendJs(writer, storageKey, reportDate, totalFiles, totalFilesDetail);
        }

        // ── Progress detail ──────────────────────────────────────────────────

        /// <summary>
        /// Builds a compact breakdown string for the progress bar detail (e.g. "Added: 1 + Removed: 1 + Modified: 14").
        /// Sections with 0 count are omitted.
        /// プログレスバーの明細文字列を構築します（例: "Added: 1 + Removed: 1 + Modified: 14"）。
        /// 件数0のセクションは省略されます。
        /// </summary>
        private static string BuildTotalFilesDetail(int added, int removed, int modified, int sha256Warn, int tsWarn)
        {
            var parts = new System.Collections.Generic.List<string>(5);
            if (added > 0) parts.Add($"Added: {added}");
            if (removed > 0) parts.Add($"Removed: {removed}");
            if (modified > 0) parts.Add($"Modified: {modified}");
            if (sha256Warn > 0) parts.Add($"SHA256Warn: {sha256Warn}");
            if (tsWarn > 0) parts.Add($"TsWarn: {tsWarn}");
            return string.Join(" + ", parts);
        }

        // ── Keyboard shortcut help overlay ────────────────────────────────────
        // キーボードショートカットヘルプオーバーレイ

        private static void AppendKeyboardHelpOverlay(TextWriter writer)
        {
            writer.WriteLine("<div id=\"kb-help\" class=\"kb-help\" aria-label=\"Keyboard shortcuts\">");
            writer.WriteLine("  <div class=\"kb-help-title\">Keyboard Shortcuts</div>");
            writer.WriteLine("  <div class=\"kb-help-row\"><span class=\"kb-help-keys\"><kbd>j</kbd> / <kbd>k</kbd></span> <span class=\"kb-help-desc\">Next / Previous file</span></div>");
            writer.WriteLine("  <div class=\"kb-help-row\"><span class=\"kb-help-keys\"><kbd>x</kbd></span> <span class=\"kb-help-desc\">Toggle review check</span></div>");
            writer.WriteLine("  <div class=\"kb-help-row\"><span class=\"kb-help-keys\"><kbd>Enter</kbd></span> <span class=\"kb-help-desc\">Expand / collapse diff</span></div>");
            writer.WriteLine("  <div class=\"kb-help-row\"><span class=\"kb-help-keys\"><kbd>Esc</kbd></span> <span class=\"kb-help-desc\">Close diff or exit input</span></div>");
            writer.WriteLine("  <div class=\"kb-help-row\"><span class=\"kb-help-keys\"><kbd>?</kbd></span> <span class=\"kb-help-desc\">Toggle this help</span></div>");
            writer.WriteLine("</div>");
        }

        // ── Head ─────────────────────────────────────────────────────────────

        private static void AppendHtmlHead(TextWriter writer)
        {
            writer.WriteLine("<!DOCTYPE html>");
            writer.WriteLine("<html lang=\"en\">");
            writer.WriteLine("<head>");
            writer.WriteLine("  <meta charset=\"UTF-8\">");
            writer.WriteLine("  <meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src 'self'\">");
            writer.WriteLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            writer.WriteLine("  <title>diff_report</title>");
            writer.WriteLine("  <style>");
            writer.WriteLine(GetCss());
            writer.WriteLine("  </style>");
            writer.WriteLine("</head>");
        }
    }
}
