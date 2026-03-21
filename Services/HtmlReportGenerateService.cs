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
        private const string TH_BG_DEFAULT  = "#fafafa";

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
        /// Generates diff_report.html and writes it to <paramref name="reportsFolderAbsolutePath"/>.
        /// No-op when <see cref="ConfigSettings.ShouldGenerateHtmlReport"/> is <see langword="false"/>.
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
            ILCache? ilCache = null)
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
            sb.AppendLine("  <button class=\"btn\" onclick=\"downloadReviewed()\">&#x2913; " + I18n("Download as reviewed", "レビュー済みとしてダウンロード") + "</button>");
            sb.AppendLine("  <button class=\"btn btn-clear\" onclick=\"clearAll()\">&#x2715; " + I18n("Clear all", "すべてクリア") + "</button>");
            sb.AppendLine("  <button class=\"btn btn-lang\" onclick=\"toggleLang()\" id=\"btn-lang\">" + I18n("日本語", "English") + "</button>");
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
    }
}
