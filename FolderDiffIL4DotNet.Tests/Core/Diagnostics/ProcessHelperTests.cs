using System;
using FolderDiffIL4DotNet.Core.Diagnostics;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.Diagnostics
{
    /// <summary>
    /// Tests for <see cref="ProcessHelper"/> command tokenization and label-building utilities.
    /// <see cref="ProcessHelper"/> のコマンドトークン化およびラベル生成ユーティリティのテスト。
    /// </summary>
    public class ProcessHelperTests
    {

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
