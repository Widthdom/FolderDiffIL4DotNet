using System.IO;
using System.Reflection;

namespace FolderDiffIL4DotNet.Services
{
    // CSS stylesheet for the HTML diff report, loaded from an embedded resource.
    // HTML 差分レポート用 CSS スタイルシート。埋め込みリソースから読み込みます。
    public sealed partial class HtmlReportGenerateService
    {
        private const string CSS_RESOURCE_NAME = "FolderDiffIL4DotNet.Services.HtmlReport.diff_report.css";

        private static string GetCss()
        {
            return LoadEmbeddedResource(CSS_RESOURCE_NAME);
        }

        /// <summary>
        /// Loads a UTF-8 text resource embedded in the executing assembly.
        /// 実行アセンブリに埋め込まれた UTF-8 テキストリソースを読み込みます。
        /// </summary>
        internal static string LoadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
