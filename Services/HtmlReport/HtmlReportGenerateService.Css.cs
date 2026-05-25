using System.IO;
using System.Reflection;
using FolderDiffIL4DotNet.Services.HtmlReport;

namespace FolderDiffIL4DotNet.Services
{
    // CSS stylesheet for the HTML diff report, loaded from an embedded resource and minified.
    // HTML 差分レポート用 CSS スタイルシート。埋め込みリソースから読み込みミニファイします。
    public sealed partial class HtmlReportGenerateService
    {
        private const string CSS_RESOURCE_NAME = "FolderDiffIL4DotNet.Services.HtmlReport.diff_report.css";

        // Cached minified CSS (computed once per process).
        // キャッシュ済みミニファイ CSS（プロセスあたり1回だけ計算）。
        private static string? _cachedMinifiedCss;
        private static readonly object _cssLock = new();

        private static string GetCss()
        {
            if (_cachedMinifiedCss != null)
            {
                return _cachedMinifiedCss;
            }

            lock (_cssLock)
            {
                if (_cachedMinifiedCss != null)
                {
                    return _cachedMinifiedCss;
                }

                var raw = LoadEmbeddedResource(CSS_RESOURCE_NAME);
                _cachedMinifiedCss = JsMinifier.MinifyCss(raw);
                return _cachedMinifiedCss;
            }
        }

        /// <summary>
        /// Clears the cached minified CSS (for testing purposes only).
        /// キャッシュ済みミニファイ CSS をクリアします（テスト専用）。
        /// </summary>
        internal static void ClearCssCache()
        {
            lock (_cssLock)
            {
                _cachedMinifiedCss = null;
            }
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
