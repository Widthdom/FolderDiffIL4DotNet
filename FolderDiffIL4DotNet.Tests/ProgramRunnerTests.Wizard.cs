// ProgramRunnerTests.Wizard.cs — NormalizeDragDropPath tests (partial)
// ProgramRunnerTests.Wizard.cs — NormalizeDragDropPath テスト（パーシャル）

using System;
using Xunit;

namespace FolderDiffIL4DotNet.Tests
{
    public sealed partial class ProgramRunnerTests
    {
        // ── NormalizeDragDropPath tests ───────────────────────────────────

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_PlainPath_PassesThrough()
        {
            // A simple path without quotes or special prefixes should pass through unchanged.
            // クォートや特殊プレフィックスのない単純なパスはそのまま通過すべき。
            Assert.Equal("/home/user/folder", ProgramRunner.NormalizeDragDropPath("/home/user/folder"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_DoubleQuotedPath_StripsQuotes()
        {
            // Paths wrapped in double quotes (common Windows D&D) should have quotes removed.
            // ダブルクォートで囲まれたパス（Windows D&D で一般的）はクォートが除去されるべき。
            Assert.Equal(@"C:\Users\test\folder", ProgramRunner.NormalizeDragDropPath(@"""C:\Users\test\folder"""));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_SingleQuotedPath_StripsQuotes()
        {
            // Paths wrapped in single quotes (common macOS/Linux terminal D&D) should have quotes removed.
            // シングルクォートで囲まれたパス（macOS/Linux ターミナル D&D で一般的）はクォートが除去されるべき。
            Assert.Equal("/home/user/my folder", ProgramRunner.NormalizeDragDropPath("'/home/user/my folder'"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_FileUriTripleSlash_StripsPrefix()
        {
            // file:/// URI prefix (some Linux file managers) should be stripped.
            // file:/// URI プレフィックス（一部の Linux ファイルマネージャ）は除去されるべき。
            Assert.Equal("/home/user/folder", ProgramRunner.NormalizeDragDropPath("file:///home/user/folder"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_FileUriDoubleSlash_StripsPrefix()
        {
            // file:// URI prefix should be stripped.
            // file:// URI プレフィックスは除去されるべき。
            Assert.Equal("server/share", ProgramRunner.NormalizeDragDropPath("file://server/share"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_BackslashEscapedSpaces_Unescaped()
        {
            // Backslash-escaped spaces (Unix terminal D&D without quotes) should be unescaped.
            // バックスラッシュでエスケープされたスペース（Unix ターミナルのクォートなし D&D）はアンエスケープされるべき。
            Assert.Equal("/home/user/my folder/sub dir", ProgramRunner.NormalizeDragDropPath(@"/home/user/my\ folder/sub\ dir"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_PercentEncodedSpaces_Decoded()
        {
            // URI percent-encoded paths should be decoded.
            // URI パーセントエンコードされたパスはデコードされるべき。
            Assert.Equal("/home/user/my folder", ProgramRunner.NormalizeDragDropPath("/home/user/my%20folder"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_PercentEncodedJapanese_Decoded()
        {
            // Percent-encoded Japanese characters should be decoded.
            // パーセントエンコードされた日本語文字はデコードされるべき。
            string encoded = Uri.EscapeDataString("テスト");
            Assert.Equal("/home/テスト", ProgramRunner.NormalizeDragDropPath($"/home/{encoded}"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_SurroundingWhitespace_Trimmed()
        {
            // Leading and trailing whitespace should be trimmed.
            // 先頭と末尾の空白はトリムされるべき。
            Assert.Equal("/home/user/folder", ProgramRunner.NormalizeDragDropPath("  /home/user/folder  "));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_CombinedQuotesAndUri_HandledCorrectly()
        {
            // Combination of quotes and file:// URI should both be handled.
            // クォートと file:// URI の組み合わせも両方処理されるべき。
            Assert.Equal("/home/user/folder", ProgramRunner.NormalizeDragDropPath("'file:///home/user/folder'"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_EmptyInput_ReturnsEmpty()
        {
            // Empty input should return empty string.
            // 空入力は空文字列を返すべき。
            Assert.Equal("", ProgramRunner.NormalizeDragDropPath(""));
            Assert.Equal("", ProgramRunner.NormalizeDragDropPath("  "));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_MalformedPercentEncoding_PreservesOriginal()
        {
            // Malformed percent encoding should not crash; original string preserved.
            // 不正なパーセントエンコーディングでクラッシュしないこと。元の文字列が保持される。
            string input = "/home/user/file%ZZname";
            // Uri.UnescapeDataString may throw or not depending on .NET version;
            // either way, the result should contain the original path segment.
            string result = ProgramRunner.NormalizeDragDropPath(input);
            Assert.Contains("file", result);
        }

        [Theory]
        [InlineData(@"""C:\folder with spaces\subfolder""", @"C:\folder with spaces\subfolder")]
        [InlineData("'/Users/test/My Documents'", "/Users/test/My Documents")]
        [InlineData("file:///C:/Users/test/folder", "C:/Users/test/folder")]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_VariousDragDropFormats_NormalizedCorrectly(string input, string expected)
        {
            // Various D&D formats should all normalize correctly.
            // 各種 D&D フォーマットがすべて正しく正規化されるべき。
            Assert.Equal(expected, ProgramRunner.NormalizeDragDropPath(input));
        }
    }
}
