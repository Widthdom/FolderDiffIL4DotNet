using NUglify;

namespace FolderDiffIL4DotNet.Services.HtmlReport
{
    /// <summary>
    /// Minifies JavaScript and CSS source code using NUglify to reduce HTML report size.
    /// NUglify を使用して JavaScript/CSS ソースコードをミニファイし、HTML レポートサイズを削減します。
    /// </summary>
    internal static class JsMinifier
    {
        /// <summary>
        /// Minifies the given JavaScript source code. Returns the original source on failure.
        /// 指定された JavaScript ソースコードをミニファイします。失敗時は元のソースを返します。
        /// </summary>
        internal static string Minify(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return source;
            }

            var result = Uglify.Js(source, new NUglify.JavaScript.CodeSettings
            {
                // Preserve function names for debuggability in reviewed HTML artifacts
                // reviewed HTML 成果物でのデバッグ性を維持するため関数名を保持
                PreserveFunctionNames = true,
                // Keep important comments (/*! ... */) for license attribution
                // ライセンス表記のため重要コメント (/*! ... */) を保持
                PreserveImportantComments = true,
                // Do not rename local variables — reviewed HTML must be human-readable
                // ローカル変数のリネームは行わない — reviewed HTML は人間が読める必要がある
                LocalRenaming = NUglify.JavaScript.LocalRenaming.KeepAll,
                // Remove unreachable code but keep structure intact
                // 到達不能コードは削除するが構造は維持
                RemoveUnneededCode = true,
            });

            // Fall back to original source if minification produced errors
            // ミニファイがエラーを生成した場合は元のソースにフォールバック
            return result.HasErrors ? source : result.Code;
        }

        /// <summary>
        /// Minifies the given CSS source code. Returns the original source on failure.
        /// 指定された CSS ソースコードをミニファイします。失敗時は元のソースを返します。
        /// </summary>
        internal static string MinifyCss(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return source;
            }

            var result = Uglify.Css(source, new NUglify.Css.CssSettings
            {
                // Keep comments that start with /*! for license / attribution
                // ライセンス・帰属表記のため /*! で始まるコメントを保持
                CommentMode = NUglify.Css.CssComment.Important,
            });

            // Fall back to original source if minification produced errors
            // ミニファイがエラーを生成した場合は元のソースにフォールバック
            return result.HasErrors ? source : result.Code;
        }
    }
}
