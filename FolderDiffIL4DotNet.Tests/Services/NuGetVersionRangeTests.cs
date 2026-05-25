using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="NuGetVersionRange"/>.
    /// <see cref="NuGetVersionRange"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class NuGetVersionRangeTests
    {
        // ── Exact version: [x.y.z] / 完全一致 ─────────────────────────

        [Fact]
        public void Contains_ExactVersion_MatchesExactly()
        {
            Assert.True(NuGetVersionRange.Contains("[1.2.3]", "1.2.3"));
            Assert.False(NuGetVersionRange.Contains("[1.2.3]", "1.2.4"));
            Assert.False(NuGetVersionRange.Contains("[1.2.3]", "1.2.2"));
        }

        // ── Open lower bound: (, max) / 下限なし ──────────────────────

        [Fact]
        public void Contains_OpenLowerExclusiveUpper_VersionBelow()
        {
            // (, 4.3.1) means "less than 4.3.1"
            Assert.True(NuGetVersionRange.Contains("(, 4.3.1)", "4.3.0"));
            Assert.True(NuGetVersionRange.Contains("(, 4.3.1)", "1.0.0"));
            Assert.False(NuGetVersionRange.Contains("(, 4.3.1)", "4.3.1"));
            Assert.False(NuGetVersionRange.Contains("(, 4.3.1)", "5.0.0"));
        }

        [Fact]
        public void Contains_OpenLowerInclusiveUpper_VersionAtOrBelow()
        {
            // (, 4.3.1] means "less than or equal to 4.3.1"
            Assert.True(NuGetVersionRange.Contains("(, 4.3.1]", "4.3.1"));
            Assert.True(NuGetVersionRange.Contains("(, 4.3.1]", "4.3.0"));
            Assert.False(NuGetVersionRange.Contains("(, 4.3.1]", "4.3.2"));
        }

        // ── Closed interval: [min, max) / 閉区間 ──────────────────────

        [Fact]
        public void Contains_InclusiveMinExclusiveMax()
        {
            // [1.0.0, 2.0.0) means ">= 1.0.0 and < 2.0.0"
            Assert.True(NuGetVersionRange.Contains("[1.0.0, 2.0.0)", "1.0.0"));
            Assert.True(NuGetVersionRange.Contains("[1.0.0, 2.0.0)", "1.5.0"));
            Assert.True(NuGetVersionRange.Contains("[1.0.0, 2.0.0)", "1.99.99"));
            Assert.False(NuGetVersionRange.Contains("[1.0.0, 2.0.0)", "0.9.9"));
            Assert.False(NuGetVersionRange.Contains("[1.0.0, 2.0.0)", "2.0.0"));
        }

        [Fact]
        public void Contains_ExclusiveMinInclusiveMax()
        {
            // (1.0.0, 2.0.0] means "> 1.0.0 and <= 2.0.0"
            Assert.True(NuGetVersionRange.Contains("(1.0.0, 2.0.0]", "1.0.1"));
            Assert.True(NuGetVersionRange.Contains("(1.0.0, 2.0.0]", "2.0.0"));
            Assert.False(NuGetVersionRange.Contains("(1.0.0, 2.0.0]", "1.0.0"));
            Assert.False(NuGetVersionRange.Contains("(1.0.0, 2.0.0]", "2.0.1"));
        }

        // ── Open upper bound: [min, ) / 上限なし ──────────────────────

        [Fact]
        public void Contains_OpenUpperBound_InclusiveMin()
        {
            // [1.0.0, ) means ">= 1.0.0"
            Assert.True(NuGetVersionRange.Contains("[1.0.0, )", "1.0.0"));
            Assert.True(NuGetVersionRange.Contains("[1.0.0, )", "99.0.0"));
            Assert.False(NuGetVersionRange.Contains("[1.0.0, )", "0.9.9"));
        }

        // ── Pre-release versions / プレリリースバージョン ──────────────

        [Fact]
        public void Contains_PreReleaseVersion_StripsPreReleaseSuffix()
        {
            Assert.True(NuGetVersionRange.Contains("(, 2.0.0)", "1.0.0-preview.1"));
            Assert.False(NuGetVersionRange.Contains("(, 1.0.0)", "1.0.0-preview.1"));
        }

        // ── Four-part versions / 4パートバージョン ──────────────────────

        [Fact]
        public void Contains_FourPartVersion()
        {
            Assert.True(NuGetVersionRange.Contains("[1.0.0.0, 2.0.0.0)", "1.0.0.0"));
            Assert.True(NuGetVersionRange.Contains("[1.0.0.0, 2.0.0.0)", "1.5.0.0"));
            Assert.False(NuGetVersionRange.Contains("[1.0.0.0, 2.0.0.0)", "2.0.0.0"));
        }

        // ── Edge cases / エッジケース ──────────────────────────────────

        [Theory]
        [InlineData(null, "1.0.0")]
        [InlineData("", "1.0.0")]
        [InlineData("[1.0.0]", null)]
        [InlineData("[1.0.0]", "")]
        [InlineData("invalid", "1.0.0")]
        [InlineData("x", "1.0.0")]
        public void Contains_InvalidInputs_ReturnsFalse(string? range, string? version)
        {
            Assert.False(NuGetVersionRange.Contains(range!, version!));
        }

        // ── ParseVersion / バージョン解析 ──────────────────────────────

        [Fact]
        public void ParseVersion_StandardVersion()
        {
            var result = NuGetVersionRange.ParseVersion("1.2.3");
            Assert.NotNull(result);
            Assert.Equal(new[] { 1, 2, 3, 0 }, result);
        }

        [Fact]
        public void ParseVersion_WithMetadata_Strips()
        {
            var result = NuGetVersionRange.ParseVersion("1.0.0+build.123");
            Assert.NotNull(result);
            Assert.Equal(new[] { 1, 0, 0, 0 }, result);
        }

        [Fact]
        public void ParseVersion_Null_ReturnsNull()
        {
            Assert.Null(NuGetVersionRange.ParseVersion(null!));
            Assert.Null(NuGetVersionRange.ParseVersion(""));
            Assert.Null(NuGetVersionRange.ParseVersion("   "));
        }

        [Fact]
        public void ParseVersion_InvalidFormat_ReturnsNull()
        {
            Assert.Null(NuGetVersionRange.ParseVersion("abc"));
            Assert.Null(NuGetVersionRange.ParseVersion("1.2.3.4.5"));
        }

        // ── Whitespace tolerance / 空白許容 ────────────────────────────

        [Fact]
        public void Contains_WhitespaceInRange_Tolerant()
        {
            Assert.True(NuGetVersionRange.Contains(" [ 1.0.0 , 2.0.0 ) ", "1.5.0"));
        }
    }
}
