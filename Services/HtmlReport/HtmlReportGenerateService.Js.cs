using System.IO;

namespace FolderDiffIL4DotNet.Services
{
    // JavaScript for localStorage auto-save, download-as-reviewed, lazy diff rendering, and column resizing.
    // Loaded from an embedded resource template with placeholder substitution.
    // localStorage 自動保存・レビュー済みダウンロード・遅延差分描画・カラムリサイズ用 JavaScript。
    // 埋め込みリソーステンプレートからプレースホルダーを置換して読み込みます。
    public sealed partial class HtmlReportGenerateService
    {
        private const string JS_RESOURCE_NAME = "FolderDiffIL4DotNet.Services.HtmlReport.diff_report.js";
        private const string JS_PLACEHOLDER_STORAGE_KEY = "{{STORAGE_KEY}}";
        private const string JS_PLACEHOLDER_REPORT_DATE = "{{REPORT_DATE}}";
        private const string JS_PLACEHOLDER_TOTAL_FILES = "{{TOTAL_FILES}}";
        private const string JS_PLACEHOLDER_TOTAL_FILES_DETAIL = "{{TOTAL_FILES_DETAIL}}";

        private static void AppendJs(TextWriter writer, string storageKey, string reportDate, int totalFiles, string totalFilesDetail)
        {
            var jsTemplate = LoadEmbeddedResource(JS_RESOURCE_NAME);
            var js = jsTemplate
                .Replace(JS_PLACEHOLDER_STORAGE_KEY, storageKey)
                .Replace(JS_PLACEHOLDER_REPORT_DATE, reportDate)
                .Replace(JS_PLACEHOLDER_TOTAL_FILES, totalFiles.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Replace(JS_PLACEHOLDER_TOTAL_FILES_DETAIL, totalFilesDetail);
            writer.WriteLine("<script>");
            writer.WriteLine(js);
            writer.WriteLine("</script>");
        }
    }
}
