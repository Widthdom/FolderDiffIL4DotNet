using System.IO;

namespace FolderDiffIL4DotNet.Services
{
    // JavaScript for localStorage auto-save, download-as-reviewed, lazy diff rendering, and column resizing.
    // Loaded from embedded resource modules with placeholder substitution.
    // localStorage 自動保存・レビュー済みダウンロード・遅延差分描画・カラムリサイズ用 JavaScript。
    // 埋め込みリソースモジュールからプレースホルダーを置換して読み込みます。
    public sealed partial class HtmlReportGenerateService
    {
        // Module load order matters: state (constants) must come first, init must come last.
        // モジュール読み込み順序: state（定数）が最初、init が最後。
        private static readonly string[] JS_MODULE_RESOURCE_NAMES =
        [
            "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_state.js",
            "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_export.js",
            "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_diffview.js",
            "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_lazy.js",
            "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_layout.js",
            "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_filter.js",
            "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_excel.js",
            "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_theme.js",
            "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_celebrate.js",
            "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_highlight.js",
            "FolderDiffIL4DotNet.Services.HtmlReport.js.diff_report_init.js",
        ];

        private const string JS_PLACEHOLDER_STORAGE_KEY = "{{STORAGE_KEY}}";
        private const string JS_PLACEHOLDER_REPORT_DATE = "{{REPORT_DATE}}";
        private const string JS_PLACEHOLDER_TOTAL_FILES = "{{TOTAL_FILES}}";
        private const string JS_PLACEHOLDER_TOTAL_FILES_DETAIL = "{{TOTAL_FILES_DETAIL}}";

        private static void AppendJs(TextWriter writer, string storageKey, string reportDate, int totalFiles, string totalFilesDetail)
        {
            var jsTemplate = LoadJsModules();
            var js = jsTemplate
                .Replace(JS_PLACEHOLDER_STORAGE_KEY, storageKey)
                .Replace(JS_PLACEHOLDER_REPORT_DATE, reportDate)
                .Replace(JS_PLACEHOLDER_TOTAL_FILES, totalFiles.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Replace(JS_PLACEHOLDER_TOTAL_FILES_DETAIL, totalFilesDetail);
            writer.WriteLine("<script>");
            writer.WriteLine(js);
            writer.WriteLine("</script>");
        }

        // Concatenate all JS module embedded resources in order.
        // 全 JS モジュール埋め込みリソースを順序どおりに結合します。
        private static string LoadJsModules()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var resourceName in JS_MODULE_RESOURCE_NAMES)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.Append(LoadEmbeddedResource(resourceName));
            }
            return sb.ToString();
        }
    }
}
