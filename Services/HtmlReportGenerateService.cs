using System;
using System.IO;
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

            // Controls bar (markers allow stripping in downloadReviewed)
            sb.AppendLine("<!--CTRL-->");
            sb.AppendLine("<div class=\"controls\">");
            sb.AppendLine("<div class=\"ctrl-buttons\">");
            sb.AppendLine("  <button class=\"btn\" onclick=\"downloadReviewed()\">&#x2913; " + HtmlEncode("Download as reviewed") + "</button>");
            sb.AppendLine("  <button class=\"btn btn-clear\" onclick=\"collapseAll()\">" + HtmlEncode("Fold all details") + "</button>");
            sb.AppendLine("  <button class=\"btn btn-clear\" onclick=\"resetFilters()\"><svg width=\"12\" height=\"12\" viewBox=\"0 0 16 16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" style=\"vertical-align:-1px\"><path d=\"M2 3h12l-4 5v3l-4 2V8z\"/><line x1=\"10\" y1=\"10\" x2=\"15\" y2=\"15\"/><line x1=\"15\" y1=\"10\" x2=\"10\" y2=\"15\"/></svg> " + HtmlEncode("Reset filters") + "</button>");
            sb.AppendLine("  <button class=\"btn btn-clear\" onclick=\"clearAll()\">&#x2715; " + HtmlEncode("Clear all") + "</button>");
            sb.AppendLine("  <span id=\"save-status\" class=\"save-status\"></span>");
            sb.AppendLine("</div>");

            // Filter zone / フィルターゾーン
            sb.AppendLine("<div class=\"filter-zone\">");

            // Search + Unchecked only row / 検索 + 未チェックのみ行
            sb.AppendLine("<div class=\"ctrl-filter-row\">");
            sb.AppendLine("  <label class=\"filter-chip\"><input type=\"checkbox\" id=\"filter-unchecked\" onchange=\"applyFilters()\"> " + HtmlEncode("Unchecked only") + "</label>");
            sb.AppendLine("  <span class=\"filter-sep\"></span>");
            sb.AppendLine("  <input type=\"text\" id=\"filter-search\" placeholder=\"" + HtmlEncode("Search file path...") + "\" class=\"filter-search\" oninput=\"applyFilters()\">");
            sb.AppendLine("</div>");

            // Filter tables row / フィルターテーブル行
            sb.AppendLine("<div class=\"filter-tables\">");

            // Diff Detail filter table / Diff Detail フィルターテーブル
            sb.AppendLine("<div class=\"filter-table-wrap\">");
            sb.AppendLine("<table class=\"filter-table\">");
            sb.AppendLine("<thead><tr><th colspan=\"3\">Diff Detail</th></tr></thead>");
            sb.AppendLine("<tbody>");
            AppendFilterTableRow(sb, "filter-diff-sha256match", "<code>SHA256Match</code>", HtmlEncode("SHA256 hash match"));
            AppendFilterTableRow(sb, "filter-diff-sha256mismatch", "<code>SHA256Mismatch</code>", HtmlEncode("SHA256 hash mismatch"));
            AppendFilterTableRow(sb, "filter-diff-ilmatch", "<code>ILMatch</code>", HtmlEncode("IL (Intermediate Language) match"));
            AppendFilterTableRow(sb, "filter-diff-ilmismatch", "<code>ILMismatch</code>", HtmlEncode("IL (Intermediate Language) mismatch"));
            AppendFilterTableRow(sb, "filter-diff-textmatch", "<code>TextMatch</code>", HtmlEncode("Text-based match"));
            AppendFilterTableRow(sb, "filter-diff-textmismatch", "<code>TextMismatch</code>", HtmlEncode("Text-based mismatch"));
            sb.AppendLine("</tbody></table>");
            sb.AppendLine("</div>");

            // Change Importance filter table / Change Importance フィルターテーブル
            sb.AppendLine("<div class=\"filter-table-wrap\">");
            sb.AppendLine("<table class=\"filter-table filter-table-wide\">");
            sb.AppendLine("<thead><tr><th colspan=\"3\">Change Importance</th></tr></thead>");
            sb.AppendLine("<tbody>");
            AppendFilterTableRow(sb, "filter-imp-high", "<span style=\"color:#d1242f;font-weight:bold\">High</span>", HtmlEncode("Breaking change candidate: public/protected API removal, access narrowing, return-type / parameter / member-type change"));
            AppendFilterTableRow(sb, "filter-imp-medium", "<span style=\"color:#d97706;font-weight:bold\">Medium</span>", HtmlEncode("Notable change: public/protected member addition, modifier change, access widening, internal removal"));
            AppendFilterTableRow(sb, "filter-imp-low", "Low", HtmlEncode("Low-impact change: body-only modification, internal/private member addition"));
            sb.AppendLine("</tbody></table>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>"); // end .filter-tables

            sb.AppendLine("</div>");  // end .filter-zone
            sb.AppendLine("</div>");  // end .controls
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
