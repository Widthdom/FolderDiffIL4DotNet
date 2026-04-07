// HtmlReportGenerateServiceTests.Security.cs — URI scheme allowlist tests (partial)
// HtmlReportGenerateServiceTests.Security.cs — URI スキーム許可リストテスト（パーシャル）

using Xunit;
using FolderDiffIL4DotNet.Services;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed partial class HtmlReportGenerateServiceTests
    {
        // -----------------------------------------------------------------------
        // IsAllowedUriScheme — advisory URL scheme validation
        // IsAllowedUriScheme — アドバイザリ URL スキームバリデーション
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData("https://github.com/advisories/GHSA-1234-5678-9abc", true)]
        [InlineData("http://example.com/advisory", true)]
        [InlineData("HTTPS://GITHUB.COM/ADVISORIES/GHSA-1234", true)]
        [InlineData("HTTP://EXAMPLE.COM", true)]
        [Trait("Category", "Unit")]
        public void IsAllowedUriScheme_HttpAndHttps_ReturnsTrue(string url, bool expected)
        {
            Assert.Equal(expected, HtmlReportGenerateService.IsAllowedUriScheme(url));
        }

        [Theory]
        [InlineData("javascript:alert(1)")]
        [InlineData("data:text/html,<script>alert(1)</script>")]
        [InlineData("vbscript:MsgBox(1)")]
        [InlineData("file:///etc/passwd")]
        [InlineData("ftp://example.com/file")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-url")]
        [Trait("Category", "Unit")]
        public void IsAllowedUriScheme_DangerousOrInvalid_ReturnsFalse(string url)
        {
            Assert.False(HtmlReportGenerateService.IsAllowedUriScheme(url));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void IsAllowedUriScheme_Null_ReturnsFalse()
        {
            Assert.False(HtmlReportGenerateService.IsAllowedUriScheme(null!));
        }
    }
}
