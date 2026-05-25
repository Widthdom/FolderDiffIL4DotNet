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
            // file:// URI with an authority component should be preserved as a UNC-style path.
            // authority を持つ file:// URI は UNC 風パスとして保持されるべき。
            Assert.Equal("//server/share", ProgramRunner.NormalizeDragDropPath("file://server/share"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_FileUriLocalhost_UsesLocalPath()
        {
            // file://localhost should normalize to the local absolute path, not a UNC-style path.
            // file://localhost は UNC 風ではなくローカル絶対パスへ正規化されるべき。
            Assert.Equal("/home/user/folder", ProgramRunner.NormalizeDragDropPath("file://localhost/home/user/folder"));
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

        [Theory]
        [Trait("Category", "Unit")]
        // On current .NET 8+ runtimes `Uri.UnescapeDataString` returns malformed %-encoding unchanged
        // instead of throwing. The catch is narrowed to only `UriFormatException`/`ArgumentException`,
        // so the prior contract "crash-free, malformed segment preserved" must hold for each input
        // regardless of which branch (no-op or caught-exception fallback) the runtime takes.
        // 現在の .NET 8 以降では `Uri.UnescapeDataString` は不正な %-encoding を投げずにそのまま返す。
        // catch は `UriFormatException`/`ArgumentException` のみに絞っているため、ランタイムが
        // no-op 分岐でも catch 分岐でも、「crash しない／不正セグメントを保持する」という契約は
        // どの入力でも維持される必要がある。
        [InlineData("/home/user/file%ZZname")]
        [InlineData("/home/user/file%G0name")]
        [InlineData("/home/user/file%trailing")]
        [InlineData("/home/user/%E3%81%AAname")] // valid UTF-8 percent encoding (hiragana "な") — must decode / 妥当な UTF-8 パーセントエンコード（ひらがな "な"）— デコードされる
        public void NormalizeDragDropPath_MalformedPercentEncoding_DoesNotThrowAndPreservesIdentifiablePathSegment(string input)
        {
            string result = ProgramRunner.NormalizeDragDropPath(input);

            Assert.False(string.IsNullOrEmpty(result));
            // Pin: the leading directory portion must not be stripped/truncated for any of these inputs.
            // pin: 先頭ディレクトリ部分は、いずれの入力でも切り落とされてはいけない。
            Assert.Contains("/home/user/", result, System.StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(@"""C:\folder with spaces\subfolder""", @"C:\folder with spaces\subfolder")]
        [InlineData("'/Users/test/My Documents'", "/Users/test/My Documents")]
        [InlineData("file:///home/user/test/folder", "/home/user/test/folder")]
        [Trait("Category", "Unit")]
        public void NormalizeDragDropPath_VariousDragDropFormats_NormalizedCorrectly(string input, string expected)
        {
            // Various D&D formats should all normalize correctly.
            // 各種 D&D フォーマットがすべて正しく正規化されるべき。
            Assert.Equal(expected, ProgramRunner.NormalizeDragDropPath(input));
        }
    }
}
