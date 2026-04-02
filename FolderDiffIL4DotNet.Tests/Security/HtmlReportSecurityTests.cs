using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Security
{
    /// <summary>
    /// Security tests verifying XSS prevention, HTML injection, and encoding safety
    /// in the HTML report generation layer.
    /// HTML レポート生成レイヤーにおける XSS 防止、HTML インジェクション、エンコーディング安全性のセキュリティテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class HtmlReportSecurityTests
    {
        [Theory]
        [InlineData("<script>alert('xss')</script>")]
        [InlineData("<img onerror=alert(1) src=x>")]
        [InlineData("<svg onload=alert(1)>")]
        [InlineData("<body onload=alert(1)>")]
        [InlineData("<iframe src='javascript:alert(1)'>")]
        public void HtmlEncode_ScriptTags_FullyEscaped(string input)
        {
            var result = HtmlReportGenerateService.HtmlEncode(input);
            Assert.DoesNotContain("<script", result);
            Assert.DoesNotContain("<img", result);
            Assert.DoesNotContain("<svg", result);
            Assert.DoesNotContain("<iframe", result);
            Assert.DoesNotContain("onerror", result.ToLowerInvariant().Replace("&", ""));
        }

        [Theory]
        [InlineData("\" onclick=\"alert(1)\"")]
        [InlineData("' onfocus='alert(1)'")]
        [InlineData("\" style=\"background:url(javascript:alert(1))\"")]
        public void HtmlEncode_AttributeInjection_QuotesEscaped(string input)
        {
            var result = HtmlReportGenerateService.HtmlEncode(input);
            // All quotes and angle brackets must be escaped
            // 全ての引用符と山括弧がエスケープされていること
            Assert.DoesNotContain("\"", result.Replace("&quot;", ""));
        }

        [Theory]
        [InlineData("`${document.cookie}`")]
        [InlineData("`alert(1)`")]
        [InlineData("${7*7}")]
        public void HtmlEncode_TemplateLiteral_BackticksEscaped(string input)
        {
            var result = HtmlReportGenerateService.HtmlEncode(input);
            Assert.DoesNotContain("`", result);
            Assert.Contains("&#96;", result);
        }

        [Theory]
        [InlineData("&amp;")]
        [InlineData("&lt;script&gt;")]
        [InlineData("&amp;lt;")]
        public void HtmlEncode_DoubleEncoding_DoesNotProduceRawTags(string input)
        {
            // Double-encoding should not produce exploitable output
            // 二重エンコードが攻撃可能な出力を生成しないこと
            var result = HtmlReportGenerateService.HtmlEncode(input);
            Assert.DoesNotContain("<script", result);
        }

        [Fact]
        public void HtmlEncode_NullAndEmpty_ReturnEmptyString()
        {
            Assert.Equal(string.Empty, HtmlReportGenerateService.HtmlEncode(null!));
            Assert.Equal(string.Empty, HtmlReportGenerateService.HtmlEncode(string.Empty));
        }

        [Fact]
        public void HtmlEncode_JapaneseText_PreservedUnescaped()
        {
            // CJK characters should pass through without modification
            // CJK 文字は変更なしで通過すること
            var input = "テスト用パス/日本語ファイル.dll";
            var result = HtmlReportGenerateService.HtmlEncode(input);
            Assert.Contains("テスト用パス", result);
            Assert.Contains("日本語ファイル", result);
        }

        [Theory]
        [InlineData("C:\\Users\\<script>\\file.dll")]
        [InlineData("/home/user/../../../etc/passwd")]
        [InlineData("\\\\server\\share\\<img src=x onerror=alert(1)>")]
        public void HtmlEncode_MaliciousFilePaths_SafelyEncoded(string path)
        {
            var result = HtmlReportGenerateService.HtmlEncode(path);
            Assert.DoesNotContain("<script>", result);
            Assert.DoesNotContain("<img", result);
            Assert.DoesNotContain("onerror", result.ToLowerInvariant().Replace("&", ""));
        }

        [Fact]
        public void HtmlEncode_AllFiveHtmlSpecialChars_Escaped()
        {
            var result = HtmlReportGenerateService.HtmlEncode("&<>\"'`");
            Assert.Contains("&amp;", result);
            Assert.Contains("&lt;", result);
            Assert.Contains("&gt;", result);
            Assert.Contains("&quot;", result);
            Assert.Contains("&#96;", result);
        }

        [Theory]
        [InlineData("\0")]
        [InlineData("\x01")]
        [InlineData("\x1F")]
        [InlineData("\r\n")]
        public void HtmlEncode_ControlCharacters_DoNotProduceRawHtml(string input)
        {
            // Control characters must not produce exploitable HTML
            // 制御文字が攻撃可能な HTML を生成しないこと
            var result = HtmlReportGenerateService.HtmlEncode(input);
            Assert.DoesNotContain("<", result);
            Assert.DoesNotContain(">", result);
        }
    }
}
