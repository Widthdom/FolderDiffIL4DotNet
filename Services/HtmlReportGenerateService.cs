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
        private const string COLOR_ADDED    = "#22863a";
        private const string COLOR_REMOVED  = "#b31d28";
        private const string COLOR_MODIFIED = "#0051c3";
        private const string TH_BG_ADDED    = "#e6ffed";
        private const string TH_BG_REMOVED  = "#ffeef0";
        private const string TH_BG_MODIFIED = "#e3f2fd";
        private const string TH_BG_DEFAULT  = "#f0f0f2";

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
            string html = BuildHtml(
                context.OldFolderAbsolutePath, context.NewFolderAbsolutePath, context.ReportsFolderAbsolutePath,
                context.AppVersion, context.ElapsedTimeString, context.ComputerName, context.Config, context.IlCache);
            try
            {
                File.WriteAllText(htmlPath, html, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
            IReadOnlyConfigSettings config,
            ILCache? ilCache)
        {
            string label = Path.GetFileName(
                reportsFolderAbsolutePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "diff";
            string storageKey = "folderdiff-" + label
                .Replace("'", "-").Replace("\"", "-").Replace("\\", "-").Replace("`", "-");
            string reportDate = DateTime.Now.ToString("yyyyMMdd");

            var sb = new StringBuilder(capacity: 131072);
            AppendHtmlHead(sb);
            sb.AppendLine("<body>");
            // Skip link for keyboard navigation / キーボードナビゲーション用スキップリンク
            sb.AppendLine("<a href=\"#main-content\" class=\"skip-link\">Skip to main content</a>");

            // Controls bar — button row is stripped in downloadReviewed, filter zone is kept
            // コントロールバー — ボタン行は downloadReviewed で除去、フィルターゾーンは維持
            sb.AppendLine("<div class=\"controls\" role=\"toolbar\" aria-label=\"Report controls\">");
            sb.AppendLine("<!--CTRL-->");
            sb.AppendLine("<div class=\"ctrl-buttons\">");
            sb.AppendLine("  <div class=\"ctrl-progress\">");
            sb.AppendLine("    <div class=\"progress-wrap\">");
            sb.AppendLine("      <div class=\"progress-bar\"><div id=\"progress-bar-fill\" class=\"progress-bar-fill\"></div></div>");
            sb.AppendLine("      <span id=\"progress-text\" class=\"progress-text\"></span>");
            sb.AppendLine("      <span id=\"progress-detail\" class=\"progress-detail\"></span>");
            sb.AppendLine("    </div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"ctrl-actions\">");
            sb.AppendLine("    <button class=\"btn\" onclick=\"downloadReviewed()\">&#x2913; " + HtmlEncode("Download as reviewed") + "</button>");
            sb.AppendLine("    <button class=\"btn\" onclick=\"downloadExcel()\"><svg aria-hidden=\"true\" width=\"12\" height=\"12\" viewBox=\"0 0 16 16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" style=\"vertical-align:-1px\"><rect x=\"1\" y=\"1\" width=\"14\" height=\"14\" rx=\"1.5\"/><line x1=\"1\" y1=\"5\" x2=\"15\" y2=\"5\"/><line x1=\"1\" y1=\"9\" x2=\"15\" y2=\"9\"/><line x1=\"6\" y1=\"1\" x2=\"6\" y2=\"15\"/><line x1=\"11\" y1=\"1\" x2=\"11\" y2=\"15\"/></svg> " + HtmlEncode("Export as HTML table") + "</button>");
            sb.AppendLine("    <button class=\"btn btn-clear\" onclick=\"collapseAll()\">" + HtmlEncode("Fold all details") + "</button>");
            sb.AppendLine("    <button class=\"btn btn-clear\" onclick=\"resetFilters()\"><svg aria-hidden=\"true\" width=\"12\" height=\"12\" viewBox=\"0 0 16 16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" style=\"vertical-align:-1px\"><path d=\"M2 3h12l-4 5v3l-4 2V8z\"/><line x1=\"10\" y1=\"10\" x2=\"15\" y2=\"15\"/><line x1=\"15\" y1=\"10\" x2=\"10\" y2=\"15\"/></svg> " + HtmlEncode("Reset filters") + "</button>");
            sb.AppendLine("    <button class=\"btn btn-clear\" onclick=\"clearAll()\">&#x2715; " + HtmlEncode("Clear all") + "</button>");
            sb.AppendLine("    <span id=\"save-status\" class=\"save-status\" role=\"status\" aria-live=\"polite\"></span>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");
            sb.AppendLine("<!--/CTRL-->");

            // Filter zone (kept in reviewed HTML for read-only filtering)
            // フィルターゾーン（reviewed HTML にも残し読み取り専用フィルタリングに使用）
            sb.AppendLine("<div class=\"filter-zone\" role=\"search\" aria-label=\"Report filters\">");

            // Search + Unchecked only row / 検索 + 未チェックのみ行
            sb.AppendLine("<div class=\"ctrl-filter-row\">");
            sb.AppendLine("  <label class=\"filter-chip\"><input type=\"checkbox\" id=\"filter-unchecked\" onchange=\"applyFilters()\"> " + HtmlEncode("Unchecked only") + "</label>");
            sb.AppendLine("  <span class=\"filter-sep\"></span>");
            sb.AppendLine("  <input type=\"text\" id=\"filter-search\" placeholder=\"" + HtmlEncode("Search file path...") + "\" class=\"filter-search\" oninput=\"applyFilters()\" aria-label=\"" + HtmlEncode("Search file path") + "\">");
            sb.AppendLine("</div>");

            // Filter tables row / フィルターテーブル行
            sb.AppendLine("<div class=\"filter-tables\">");

            // Diff Detail filter table / Diff Detail フィルターテーブル
            sb.AppendLine("<div class=\"filter-table-wrap\">");
            sb.AppendLine("<table class=\"filter-table\" aria-label=\"Diff Detail filters\">");
            sb.AppendLine("<thead><tr><th scope=\"col\" colspan=\"3\">Diff Detail</th></tr></thead>");
            sb.AppendLine("<tbody>");
            AppendFilterTableRow(sb, "filter-diff-sha256match", "<code>SHA256Match</code>", HtmlEncode("Byte-for-byte match (SHA256)"));
            AppendFilterTableRow(sb, "filter-diff-sha256mismatch", "<code>SHA256Mismatch</code>", HtmlEncode("Byte-for-byte mismatch (SHA256)"));
            AppendFilterTableRow(sb, "filter-diff-ilmatch", "<code>ILMatch</code>", HtmlEncode("IL (Intermediate Language) match"));
            AppendFilterTableRow(sb, "filter-diff-ilmismatch", "<code>ILMismatch</code>", HtmlEncode("IL (Intermediate Language) mismatch"));
            AppendFilterTableRow(sb, "filter-diff-textmatch", "<code>TextMatch</code>", HtmlEncode("Text-based match"));
            AppendFilterTableRow(sb, "filter-diff-textmismatch", "<code>TextMismatch</code>", HtmlEncode("Text-based mismatch"));
            sb.AppendLine("</tbody></table>");
            sb.AppendLine("</div>");

            // Change Importance filter table / Change Importance フィルターテーブル
            sb.AppendLine("<div class=\"filter-table-wrap\">");
            sb.AppendLine("<table class=\"filter-table filter-table-dbl\" aria-label=\"Change Importance filters\">");
            sb.AppendLine("<thead><tr><th scope=\"col\" colspan=\"3\">Change Importance</th></tr></thead>");
            sb.AppendLine("<tbody>");
            AppendFilterTableRow(sb, "filter-imp-high", "<span style=\"color:#d1242f;font-weight:bold\">High</span>", HtmlEncode("Breaking change candidate: public/protected API removal, access narrowing, return-type / parameter / member-type change"));
            AppendFilterTableRow(sb, "filter-imp-medium", "<span style=\"color:#d97706;font-weight:bold\">Medium</span>", HtmlEncode("Notable change: public/protected member addition, modifier change, access widening, internal removal"));
            AppendFilterTableRow(sb, "filter-imp-low", "Low", HtmlEncode("Low-impact change: body-only modification, internal/private member addition"));
            sb.AppendLine("</tbody></table>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>"); // end .filter-tables

            sb.AppendLine("</div>");  // end .filter-zone
            sb.AppendLine("</div>");  // end .controls

            sb.AppendLine("<main id=\"main-content\">");
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

            // Calculate total reviewable files for progress bar (Added + Removed + Modified + Warning sections)
            // Unchanged/Ignored are excluded — they don't require active review.
            // プログレスバー用のレビュー対象ファイル総数を算出（Added + Removed + Modified + Warningsセクション）
            // Unchanged/Ignoredはアクティブなレビュー不要のため除外。
            int addedCount = _fileDiffResultLists.AddedFilesAbsolutePath.Count;
            int removedCount = _fileDiffResultLists.RemovedFilesAbsolutePath.Count;
            int modifiedCount = _fileDiffResultLists.ModifiedFilesRelativePath.Count;
            int sha256WarnCount = _fileDiffResultLists.FileRelativePathToDiffDetailDictionary
                .Values.Count(r => r == FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            int tsWarnCount = _fileDiffResultLists.NewFileTimestampOlderThanOldWarnings.Count;
            int totalFiles = addedCount + removedCount + modifiedCount + sha256WarnCount + tsWarnCount;
            string totalFilesDetail = BuildTotalFilesDetail(addedCount, removedCount, modifiedCount, sha256WarnCount, tsWarnCount);
            AppendJs(sb, storageKey, reportDate, totalFiles, totalFilesDetail);
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            return sb.ToString();
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

        // ── Head ─────────────────────────────────────────────────────────────

        private static void AppendHtmlHead(StringBuilder sb)
        {
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src 'self'\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("  <title>diff_report</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine(GetCss());
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
        }
    }
}
