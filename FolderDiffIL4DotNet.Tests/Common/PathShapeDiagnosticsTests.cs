using System.IO;
using FolderDiffIL4DotNet.Common;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Common
{
    /// <summary>
    /// Unit tests for <see cref="PathShapeDiagnostics"/>. This helper is reused across many warning/error
    /// messages (audit log, SBOM, report writers, cache layers, disassembler launcher, logger cleanup,
    /// preflight validator, plugin loader, open-folder commands, IL text writer, etc.), so the edges it
    /// exposes are load-bearing: a regression here silently weakens every downstream diagnostic.
    /// <see cref="PathShapeDiagnostics"/> のユニットテスト。この helper は監査ログ、SBOM、レポート出力、
    /// キャッシュ層、逆アセンブラ起動、logger cleanup、プリフライト、プラグインローダ、open-folder、
    /// IL テキスト出力など多数の warning/error メッセージで再利用されるため、ここが退行すると下流の
    /// 診断がまとめて弱くなる。境界条件を明示的に pin する。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class PathShapeDiagnosticsTests
    {
        // ── DescribeRootedState ──

        [Fact]
        public void DescribeRootedState_NullInput_ReturnsUnknown()
        {
            Assert.Equal("Unknown", PathShapeDiagnostics.DescribeRootedState(null));
        }

        [Fact]
        public void DescribeRootedState_EmptyInput_ReturnsUnknown()
        {
            Assert.Equal("Unknown", PathShapeDiagnostics.DescribeRootedState(string.Empty));
        }

        [Fact]
        public void DescribeRootedState_WhitespaceInput_ReturnsUnknown()
        {
            Assert.Equal("Unknown", PathShapeDiagnostics.DescribeRootedState("   "));
        }

        [Fact]
        public void DescribeRootedState_EmbeddedNullByte_ReturnsUnknown()
        {
            // Embedded NUL must never reach Path.IsPathRooted (which would throw on some runtimes),
            // and must not be reported as a legitimate rooted/relative path.
            // 埋め込み NUL は Path.IsPathRooted に渡さない（ランタイムによっては例外）。
            // また正規の rooted/relative パスとして報告してもいけない。
            Assert.Equal("Unknown", PathShapeDiagnostics.DescribeRootedState("bad\0path"));
        }

        [Fact]
        public void DescribeRootedState_RelativePath_ReturnsFalse()
        {
            Assert.Equal("False", PathShapeDiagnostics.DescribeRootedState("relative/path"));
        }

        [Fact]
        public void DescribeRootedState_AbsolutePath_ReturnsTrue()
        {
            string absolute = Path.Combine(Path.GetTempPath(), "some", "path");
            Assert.Equal("True", PathShapeDiagnostics.DescribeRootedState(absolute));
        }

        // ── LooksLikePath ──

        [Fact]
        public void LooksLikePath_NullInput_ReturnsFalse()
        {
            Assert.False(PathShapeDiagnostics.LooksLikePath(null));
        }

        [Fact]
        public void LooksLikePath_EmptyInput_ReturnsFalse()
        {
            Assert.False(PathShapeDiagnostics.LooksLikePath(string.Empty));
        }

        [Fact]
        public void LooksLikePath_EmbeddedNullByte_ReturnsFalse()
        {
            Assert.False(PathShapeDiagnostics.LooksLikePath("bad\0tool"));
        }

        [Fact]
        public void LooksLikePath_PlainCommandName_ReturnsFalse()
        {
            // No directory separator and not rooted — treated as a bare command name, not a path.
            // 区切り文字なし・rooted でもない → 裸のコマンド名扱い、path-like ではない。
            Assert.False(PathShapeDiagnostics.LooksLikePath("dotnet-ildasm"));
        }

        [Fact]
        public void LooksLikePath_ForwardSlashPath_ReturnsTrue()
        {
            Assert.True(PathShapeDiagnostics.LooksLikePath("foo/bar"));
        }

        [Fact]
        public void LooksLikePath_PlatformDirectorySeparator_ReturnsTrue()
        {
            // Use the runtime's primary separator so the expectation holds on Windows and Unix alike.
            // プラットフォームのプライマリ区切り文字を使い、Windows/Unix ともに期待値が成立するようにする。
            string pathWithSeparator = $"foo{Path.DirectorySeparatorChar}bar";
            Assert.True(PathShapeDiagnostics.LooksLikePath(pathWithSeparator));
        }

        [Fact]
        public void LooksLikePath_AbsolutePath_ReturnsTrue()
        {
            string absolute = Path.Combine(Path.GetTempPath(), "foo");
            Assert.True(PathShapeDiagnostics.LooksLikePath(absolute));
        }

        // ── DescribeState ──

        [Fact]
        public void DescribeState_UsesLabelPrefix()
        {
            string state = PathShapeDiagnostics.DescribeState("Foo", "relative/path");

            Assert.Contains("FooIsPathRooted=False", state);
            Assert.Contains("FooLooksPathLike=True", state);
        }

        [Fact]
        public void DescribeState_NullInput_ReportsUnknownAndFalse()
        {
            string state = PathShapeDiagnostics.DescribeState("Target", null);

            Assert.Contains("TargetIsPathRooted=Unknown", state);
            Assert.Contains("TargetLooksPathLike=False", state);
        }

        [Fact]
        public void DescribeState_EmbeddedNullByte_ReportsUnknownAndFalse()
        {
            string state = PathShapeDiagnostics.DescribeState("Cmd", "bad\0tool");

            Assert.Contains("CmdIsPathRooted=Unknown", state);
            Assert.Contains("CmdLooksPathLike=False", state);
        }

        [Fact]
        public void DescribeState_AbsolutePath_ReportsTrueTrue()
        {
            string absolute = Path.Combine(Path.GetTempPath(), "x");

            string state = PathShapeDiagnostics.DescribeState("Out", absolute);

            Assert.Contains("OutIsPathRooted=True", state);
            Assert.Contains("OutLooksPathLike=True", state);
        }
    }
}
