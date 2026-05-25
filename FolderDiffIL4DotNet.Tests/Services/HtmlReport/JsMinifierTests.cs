using FolderDiffIL4DotNet.Services.HtmlReport;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.HtmlReport
{
    /// <summary>
    /// Unit tests for <see cref="JsMinifier"/> covering JS comment stripping and CSS minification.
    /// <see cref="JsMinifier"/> の JS コメント除去および CSS ミニファイのユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class JsMinifierTests
    {
        // ── Minify (JavaScript) ──

        [Fact]
        public void Minify_Null_ReturnsNull()
        {
            // null input should be returned as-is / null 入力はそのまま返すべき
            Assert.Null(JsMinifier.Minify(null!));
        }

        [Fact]
        public void Minify_Empty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, JsMinifier.Minify(string.Empty));
        }

        [Fact]
        public void Minify_WhitespaceOnly_ReturnsWhitespace()
        {
            // Whitespace-only input treated as empty by IsNullOrWhiteSpace / 空白のみは IsNullOrWhiteSpace で空扱い
            var result = JsMinifier.Minify("   ");
            Assert.Equal("   ", result);
        }

        [Fact]
        public void Minify_CodeOnly_PreservesCode()
        {
            // Lines with no comments should remain intact / コメントなしの行はそのまま維持
            var source = "const x = 1;\nfunction foo() { return x; }\n";
            var result = JsMinifier.Minify(source);
            Assert.Contains("const x = 1;", result);
            Assert.Contains("function foo() { return x; }", result);
        }

        [Fact]
        public void Minify_TrailingComment_StripsComment()
        {
            // Trailing // comment should be removed / 末尾の // コメントは除去されるべき
            var source = "const x = 1; // initialize x\n";
            var result = JsMinifier.Minify(source);
            Assert.Contains("const x = 1;", result);
            Assert.DoesNotContain("initialize x", result);
        }

        [Fact]
        public void Minify_CommentOnlyLine_RemovesEntireLine()
        {
            // A line that is only a comment becomes blank and is removed / コメントのみの行は空行となり除去
            var source = "const x = 1;\n// this is a comment\nconst y = 2;\n";
            var result = JsMinifier.Minify(source);
            Assert.Contains("const x = 1;", result);
            Assert.Contains("const y = 2;", result);
            Assert.DoesNotContain("this is a comment", result);
        }

        [Fact]
        public void Minify_DoubleSlashInSingleQuotedString_PreservesString()
        {
            // // inside single-quoted string literal must NOT be stripped / シングルクォート内の // は除去しない
            var source = "const url = 'http://example.com';\n";
            var result = JsMinifier.Minify(source);
            Assert.Contains("'http://example.com'", result);
        }

        [Fact]
        public void Minify_DoubleSlashInDoubleQuotedString_PreservesString()
        {
            // // inside double-quoted string literal must NOT be stripped / ダブルクォート内の // は除去しない
            var source = "const url = \"http://example.com\";\n";
            var result = JsMinifier.Minify(source);
            Assert.Contains("\"http://example.com\"", result);
        }

        [Fact]
        public void Minify_EscapedQuoteInString_CorrectlyParsed()
        {
            // Escaped quote inside string must not confuse the parser / 文字列内のエスケープ引用符でパーサーが混乱しない
            var source = "const s = 'it\\'s // not a comment';\n";
            var result = JsMinifier.Minify(source);
            Assert.Contains("it\\'s // not a comment", result);
        }

        [Fact]
        public void Minify_StringFollowedByComment_StripsOnlyComment()
        {
            // String with // inside, followed by actual comment outside string / 文字列内 // の後に実コメント
            var source = "const url = 'http://example.com'; // set url\n";
            var result = JsMinifier.Minify(source);
            Assert.Contains("'http://example.com';", result);
            Assert.DoesNotContain("set url", result);
        }

        [Fact]
        public void Minify_BlankLinesRemoved()
        {
            // Blank lines in source should be removed / ソース内の空行は除去される
            var source = "const a = 1;\n\n\nconst b = 2;\n";
            var result = JsMinifier.Minify(source);
            // Result should not contain consecutive newlines / 連続改行を含まない
            Assert.DoesNotContain("\n\n", result.Replace("\r\n", "\n"));
        }

        [Fact]
        public void Minify_MixedQuotes_HandledCorrectly()
        {
            // Double quote inside single-quoted string / シングルクォート内のダブルクォート
            var source = "const s = '\"http://test\"'; // comment\n";
            var result = JsMinifier.Minify(source);
            Assert.Contains("'\"http://test\"'", result);
            Assert.DoesNotContain("comment", result);
        }

        [Fact]
        public void Minify_PreservesWhitespaceInCode()
        {
            // Whitespace within code lines must be preserved (critical for downloadReviewed)
            // コード行内の空白は維持（downloadReviewed に重要）
            var source = "const __savedState__  = null;\n";
            var result = JsMinifier.Minify(source);
            Assert.Contains("const __savedState__  = null;", result);
        }

        // ── MinifyCss ──

        [Fact]
        public void MinifyCss_Null_ReturnsNull()
        {
            Assert.Null(JsMinifier.MinifyCss(null!));
        }

        [Fact]
        public void MinifyCss_Empty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, JsMinifier.MinifyCss(string.Empty));
        }

        [Fact]
        public void MinifyCss_RemovesBlockComment()
        {
            // Normal block comments should be removed / 通常のブロックコメントは除去
            var source = "body { color: red; }\n/* this is a comment */\np { margin: 0; }\n";
            var result = JsMinifier.MinifyCss(source);
            Assert.Contains("body { color: red; }", result);
            Assert.Contains("p { margin: 0; }", result);
            Assert.DoesNotContain("this is a comment", result);
        }

        [Fact]
        public void MinifyCss_PreservesImportantComment()
        {
            // /*! ... */ comments are important and must be kept / /*! ... */ は重要コメントで保持
            var source = "/*! license info */\nbody { color: red; }\n";
            var result = JsMinifier.MinifyCss(source);
            Assert.Contains("/*! license info */", result);
            Assert.Contains("body { color: red; }", result);
        }

        [Fact]
        public void MinifyCss_RemovesNormalButKeepsImportant()
        {
            // Mix of normal and important comments / 通常と重要コメントの混在
            var source = "/* remove me */\n/*! keep me */\n.cls { width: 100%; }\n";
            var result = JsMinifier.MinifyCss(source);
            Assert.DoesNotContain("remove me", result);
            Assert.Contains("/*! keep me */", result);
            Assert.Contains(".cls { width: 100%; }", result);
        }

        [Fact]
        public void MinifyCss_RemovesBlankLines()
        {
            // Blank lines resulting from comment removal should be cleaned / コメント除去で生じた空行はクリーン
            var source = ".a { color: red; }\n\n\n.b { color: blue; }\n";
            var result = JsMinifier.MinifyCss(source);
            Assert.DoesNotContain("\n\n", result.Replace("\r\n", "\n"));
        }

        [Fact]
        public void MinifyCss_UnterminatedComment_HandlesGracefully()
        {
            // Unterminated comment should not crash / 終端なしコメントでクラッシュしない
            var source = ".a { color: red; }\n/* unterminated comment\n";
            var result = JsMinifier.MinifyCss(source);
            Assert.Contains(".a { color: red; }", result);
        }

        [Fact]
        public void MinifyCss_PreservesWhitespace()
        {
            // Critical whitespace patterns must be preserved for downloadReviewed regex
            // downloadReviewed の正規表現に必要な空白パターンを保持
            var source = ":root { --col-reason-w: 200px; }\n";
            var result = JsMinifier.MinifyCss(source);
            Assert.Contains(":root { --col-reason-w: 200px; }", result);
        }
    }
}
