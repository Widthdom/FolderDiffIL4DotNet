using FolderDiffIL4DotNet.Utils;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Utils
{
    public class TextSanitizerTests
    {

        [Fact]
        public void ToSafeFileName_NormalInput_ReturnsUnchanged()
        {
            Assert.Equal("hello_world", TextSanitizer.ToSafeFileName("hello_world"));
        }

        [Fact]
        public void ToSafeFileName_Null_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, TextSanitizer.ToSafeFileName(null));
        }

        [Fact]
        public void ToSafeFileName_Empty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, TextSanitizer.ToSafeFileName(string.Empty));
        }

        [Fact]
        public void ToSafeFileName_ColonReplaced_ReturnsUnderscore()
        {
            var result = TextSanitizer.ToSafeFileName("file:name");
            Assert.DoesNotContain(":", result);
            Assert.Contains("_", result);
        }

        [Fact]
        public void ToSafeFileName_LongInput_IsTruncatedWithHash()
        {
            var longName = new string('a', 200);
            var result = TextSanitizer.ToSafeFileName(longName);
            Assert.True(result.Length < longName.Length);
            Assert.Contains("_.._", result);
        }

        [Fact]
        public void ToSafeFileName_ShortInput_NotTruncated()
        {
            var shortName = new string('a', 50);
            var result = TextSanitizer.ToSafeFileName(shortName);
            Assert.Equal(shortName, result);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("hello", "hello")]
        public void Sanitize_BasicInputs(string input, string expected)
        {
            Assert.Equal(expected, TextSanitizer.Sanitize(input));
        }

        [Fact]
        public void Sanitize_BackslashAndSlashAndColon_ReplacedWithDot()
        {
            var result = TextSanitizer.Sanitize("a\\b/c:d");
            Assert.Equal("a.b.c.d", result);
        }

        [Fact]
        public void Sanitize_ConsecutiveDots_Collapsed()
        {
            var result = TextSanitizer.Sanitize("a\\\\b");
            Assert.DoesNotContain("..", result);
        }

        [Fact]
        public void Sanitize_LeadingTrailingDots_Trimmed()
        {
            var result = TextSanitizer.Sanitize("\\value\\");
            Assert.False(result.StartsWith("."));
            Assert.False(result.EndsWith("."));
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("hello world", false)]
        [InlineData("ASCII-only_123!@#", false)]
        public void ContainsNonAscii_AsciiOnly_ReturnsFalse(string input, bool expected)
        {
            Assert.Equal(expected, TextSanitizer.ContainsNonAscii(input));
        }

        [Theory]
        [InlineData("日本語", true)]
        [InlineData("hello世界", true)]
        [InlineData("caf\u00e9", true)]
        public void ContainsNonAscii_NonAscii_ReturnsTrue(string input, bool expected)
        {
            Assert.Equal(expected, TextSanitizer.ContainsNonAscii(input));
        }
    }
}
