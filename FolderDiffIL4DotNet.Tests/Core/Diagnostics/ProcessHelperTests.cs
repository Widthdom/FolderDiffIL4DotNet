using System;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.Diagnostics
{
    /// <summary>
    /// Tests for <see cref="ProcessHelper"/> command tokenization, label-building, and process execution utilities.
    /// <see cref="ProcessHelper"/> のコマンドトークン化、ラベル生成、プロセス実行ユーティリティのテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public class ProcessHelperTests
    {
        // ── TryGetProcessOutputAsync / プロセス出力取得 ─────────

        [Fact]
        public async Task TryGetProcessOutputAsync_NonZeroExitCode_ReturnsNull()
        {
            // A process that exits with non-zero should return null.
            // 終了コードが 0 以外のプロセスは null を返すこと。
            var result = await ProcessHelper.TryGetProcessOutputAsync("dotnet", new[] { "--nonexistent-flag-xyz" });
            Assert.Null(result);
        }

        [Fact]
        public async Task TryGetProcessOutputAsync_ValidCommand_ReturnsOutput()
        {
            // dotnet --version should succeed and return a version string.
            // dotnet --version は成功してバージョン文字列を返すこと。
            var result = await ProcessHelper.TryGetProcessOutputAsync("dotnet", new[] { "--version" });
            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result));
        }

        [Fact]
        public async Task TryGetProcessOutputAsync_NullArgs_DoesNotThrow()
        {
            // Passing null args should not throw.
            // null 引数を渡しても例外にならないこと。
            // Note: "dotnet" without args exits with 0 and prints usage info.
            var result = await ProcessHelper.TryGetProcessOutputAsync("dotnet", null);
            // May return output or null depending on exit code, but should not throw
            // 終了コードに応じて output か null だが例外にはならない
            _ = result;
        }

        // ── TokenizeCommand / コマンドトークン化 ──────────────

        [Fact]
        public void TokenizeCommand_NullOrEmpty_ReturnsEmptyList()
        {
            Assert.Empty(ProcessHelper.TokenizeCommand(null));
            Assert.Empty(ProcessHelper.TokenizeCommand(string.Empty));
        }

        [Fact]
        public void TokenizeCommand_SimpleTokens_SplitByWhitespace()
        {
            var result = ProcessHelper.TokenizeCommand("dotnet build --release");
            Assert.Equal(3, result.Count);
            Assert.Equal("dotnet", result[0]);
            Assert.Equal("build", result[1]);
            Assert.Equal("--release", result[2]);
        }

        [Fact]
        public void TokenizeCommand_DoubleQuotes_PreservesSpaces()
        {
            var result = ProcessHelper.TokenizeCommand("cmd \"arg with spaces\"");
            Assert.Equal(2, result.Count);
            Assert.Equal("cmd", result[0]);
            Assert.Equal("arg with spaces", result[1]);
        }

        [Fact]
        public void TokenizeCommand_SingleQuotes_PreservesSpaces()
        {
            var result = ProcessHelper.TokenizeCommand("cmd 'arg with spaces'");
            Assert.Equal(2, result.Count);
            Assert.Equal("arg with spaces", result[1]);
        }

        [Fact]
        public void TokenizeCommand_MultipleSpaces_Collapsed()
        {
            var result = ProcessHelper.TokenizeCommand("a   b   c");
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void BuildBaseLabel_NoArgs_ReturnsCommandOnly()
        {
            var result = ProcessHelper.BuildBaseLabel("dotnet", Array.Empty<string>());
            Assert.Equal("dotnet", result);
        }

        [Fact]
        public void BuildBaseLabel_WithArgs_ReturnsCommandAndArgs()
        {
            var result = ProcessHelper.BuildBaseLabel("dotnet", new[] { "build", "--release" });
            Assert.Equal("dotnet build --release", result);
        }

        [Fact]
        public void GetUsedArgs_ArgsWithSpaces_Quoted()
        {
            var result = ProcessHelper.GetUsedArgs(new[] { "normal", "has space" });
            Assert.Equal("normal \"has space\"", result);
        }

        [Fact]
        public void GetUsedArgs_NoSpaces_NotQuoted()
        {
            var result = ProcessHelper.GetUsedArgs(new[] { "a", "b" });
            Assert.Equal("a b", result);
        }
    }
}
