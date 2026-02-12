using System;
using System.Runtime.InteropServices;
using FolderDiffIL4DotNet.Utils;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Utils
{
    public class UtilityPureFunctionTests
    {
        #region ValidateFolderNameOrThrow

        [Theory]
        [InlineData("MyFolder")]
        [InlineData("release-v1.2.3")]
        [InlineData("2024-01-01_build")]
        [InlineData("a")]
        public void ValidateFolderNameOrThrow_ValidNames_DoesNotThrow(string name)
        {
            Utility.ValidateFolderNameOrThrow(name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateFolderNameOrThrow_NullOrEmpty_ThrowsArgumentException(string name)
        {
            Assert.Throws<ArgumentException>(() => Utility.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        public void ValidateFolderNameOrThrow_DotNames_ThrowsArgumentException(string name)
        {
            Assert.Throws<ArgumentException>(() => Utility.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData("my\\folder")]
        [InlineData("my/folder")]
        [InlineData("my:folder")]
        [InlineData("my*folder")]
        [InlineData("my?folder")]
        [InlineData("my\"folder")]
        [InlineData("my<folder")]
        [InlineData("my>folder")]
        [InlineData("my|folder")]
        public void ValidateFolderNameOrThrow_InvalidChars_ThrowsArgumentException(string name)
        {
            Assert.Throws<ArgumentException>(() => Utility.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData("folder ")]
        [InlineData("folder.")]
        public void ValidateFolderNameOrThrow_TrailingSpaceOrDot_ThrowsArgumentException(string name)
        {
            Assert.Throws<ArgumentException>(() => Utility.ValidateFolderNameOrThrow(name));
        }

        [Theory]
        [InlineData("CON")]
        [InlineData("con")]
        [InlineData("PRN")]
        [InlineData("AUX")]
        [InlineData("NUL")]
        [InlineData("COM1")]
        [InlineData("LPT1")]
        public void ValidateFolderNameOrThrow_WindowsReservedNames_ThrowsArgumentException(string name)
        {
            Assert.Throws<ArgumentException>(() => Utility.ValidateFolderNameOrThrow(name));
        }

        [Fact]
        public void ValidateFolderNameOrThrow_ControlChar_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Utility.ValidateFolderNameOrThrow("my\x01folder"));
        }

        #endregion

        #region ToSafeFileName

        [Fact]
        public void ToSafeFileName_NormalInput_ReturnsUnchanged()
        {
            Assert.Equal("hello_world", Utility.ToSafeFileName("hello_world"));
        }

        [Fact]
        public void ToSafeFileName_Null_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, Utility.ToSafeFileName(null));
        }

        [Fact]
        public void ToSafeFileName_Empty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, Utility.ToSafeFileName(string.Empty));
        }

        [Fact]
        public void ToSafeFileName_ColonReplaced_ReturnsUnderscore()
        {
            var result = Utility.ToSafeFileName("file:name");
            Assert.DoesNotContain(":", result);
            Assert.Contains("_", result);
        }

        [Fact]
        public void ToSafeFileName_LongInput_IsTruncatedWithHash()
        {
            var longName = new string('a', 200);
            var result = Utility.ToSafeFileName(longName);
            Assert.True(result.Length < longName.Length);
            Assert.Contains("_.._", result);
        }

        [Fact]
        public void ToSafeFileName_ShortInput_NotTruncated()
        {
            var shortName = new string('a', 50);
            var result = Utility.ToSafeFileName(shortName);
            Assert.Equal(shortName, result);
        }

        #endregion

        #region Sanitize

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("hello", "hello")]
        public void Sanitize_BasicInputs(string input, string expected)
        {
            Assert.Equal(expected, Utility.Sanitize(input));
        }

        [Fact]
        public void Sanitize_BackslashAndSlashAndColon_ReplacedWithDot()
        {
            var result = Utility.Sanitize("a\\b/c:d");
            Assert.Equal("a.b.c.d", result);
        }

        [Fact]
        public void Sanitize_ConsecutiveDots_Collapsed()
        {
            var result = Utility.Sanitize("a\\\\b");
            Assert.DoesNotContain("..", result);
        }

        [Fact]
        public void Sanitize_LeadingTrailingDots_Trimmed()
        {
            var result = Utility.Sanitize("\\value\\");
            Assert.False(result.StartsWith("."));
            Assert.False(result.EndsWith("."));
        }

        #endregion

        #region TokenizeCommand

        [Fact]
        public void TokenizeCommand_NullOrEmpty_ReturnsEmptyList()
        {
            Assert.Empty(Utility.TokenizeCommand(null));
            Assert.Empty(Utility.TokenizeCommand(string.Empty));
        }

        [Fact]
        public void TokenizeCommand_SimpleTokens_SplitByWhitespace()
        {
            var result = Utility.TokenizeCommand("dotnet build --release");
            Assert.Equal(3, result.Count);
            Assert.Equal("dotnet", result[0]);
            Assert.Equal("build", result[1]);
            Assert.Equal("--release", result[2]);
        }

        [Fact]
        public void TokenizeCommand_DoubleQuotes_PreservesSpaces()
        {
            var result = Utility.TokenizeCommand("cmd \"arg with spaces\"");
            Assert.Equal(2, result.Count);
            Assert.Equal("cmd", result[0]);
            Assert.Equal("arg with spaces", result[1]);
        }

        [Fact]
        public void TokenizeCommand_SingleQuotes_PreservesSpaces()
        {
            var result = Utility.TokenizeCommand("cmd 'arg with spaces'");
            Assert.Equal(2, result.Count);
            Assert.Equal("arg with spaces", result[1]);
        }

        [Fact]
        public void TokenizeCommand_MultipleSpaces_Collapsed()
        {
            var result = Utility.TokenizeCommand("a   b   c");
            Assert.Equal(3, result.Count);
        }

        #endregion

        #region ContainsNonAscii

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("hello world", false)]
        [InlineData("ASCII-only_123!@#", false)]
        public void ContainsNonAscii_AsciiOnly_ReturnsFalse(string input, bool expected)
        {
            Assert.Equal(expected, Utility.ContainsNonAscii(input));
        }

        [Theory]
        [InlineData("日本語", true)]
        [InlineData("hello世界", true)]
        [InlineData("caf\u00e9", true)]
        public void ContainsNonAscii_NonAscii_ReturnsTrue(string input, bool expected)
        {
            Assert.Equal(expected, Utility.ContainsNonAscii(input));
        }

        #endregion

        #region BuildBaseLabel / GetUsedArgs

        [Fact]
        public void BuildBaseLabel_NoArgs_ReturnsCommandOnly()
        {
            var result = Utility.BuildBaseLabel("dotnet", Array.Empty<string>());
            Assert.Equal("dotnet", result);
        }

        [Fact]
        public void BuildBaseLabel_WithArgs_ReturnsCommandAndArgs()
        {
            var result = Utility.BuildBaseLabel("dotnet", new[] { "build", "--release" });
            Assert.Equal("dotnet build --release", result);
        }

        [Fact]
        public void GetUsedArgs_ArgsWithSpaces_Quoted()
        {
            var result = Utility.GetUsedArgs(new[] { "normal", "has space" });
            Assert.Equal("normal \"has space\"", result);
        }

        [Fact]
        public void GetUsedArgs_NoSpaces_NotQuoted()
        {
            var result = Utility.GetUsedArgs(new[] { "a", "b" });
            Assert.Equal("a b", result);
        }

        #endregion

        #region ValidateAbsolutePathLengthOrThrow

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateAbsolutePathLengthOrThrow_NullOrEmpty_ThrowsArgumentException(string path)
        {
            Assert.Throws<ArgumentException>(() => Utility.ValidateAbsolutePathLengthOrThrow(path));
        }

        [Fact]
        public void ValidateAbsolutePathLengthOrThrow_NormalPath_DoesNotThrow()
        {
            Utility.ValidateAbsolutePathLengthOrThrow("/usr/local/bin/test");
        }

        [Fact]
        public void ValidateAbsolutePathLengthOrThrow_ExceedsLimit_ThrowsArgumentException()
        {
            int limit;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                limit = 260;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                limit = 1024;
            else
                limit = 4096;

            var tooLongPath = "/" + new string('a', limit + 1);
            Assert.Throws<ArgumentException>(() => Utility.ValidateAbsolutePathLengthOrThrow(tooLongPath));
        }

        #endregion
    }
}
