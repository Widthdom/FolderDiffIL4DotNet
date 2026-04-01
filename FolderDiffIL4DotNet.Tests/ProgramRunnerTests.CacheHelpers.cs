// ProgramRunnerTests.CacheHelpers.cs — IL cache helper method tests (partial 5/5)
// ProgramRunnerTests.CacheHelpers.cs — IL キャッシュヘルパーメソッドのテスト（パーシャル 5/5）

using System;
using Xunit;

namespace FolderDiffIL4DotNet.Tests
{
    public sealed partial class ProgramRunnerTests
    {
        // -----------------------------------------------------------------------
        // Cache helper method tests (FilterCacheFilesByTool, ExtractDistinctToolLabels, etc.)
        // キャッシュヘルパーメソッドテスト
        // -----------------------------------------------------------------------

        [Fact]
        [Trait("Category", "Unit")]
        public void FilterCacheFilesByTool_MatchesIldasmFiles()
        {
            var files = new[]
            {
                "/cache/abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789_dotnet-ildasm _version_ 0.12.0_.ilcache",
                "/cache/1111110123456789abcdef0123456789abcdef0123456789abcdef0123456789_ilspycmd _version_ 8.2.0_.ilcache",
                "/cache/2222220123456789abcdef0123456789abcdef0123456789abcdef0123456789_dotnet-ildasm _version_ 0.13.0_.ilcache",
            };

            var result = ProgramRunner.FilterCacheFilesByTool(files, "dotnet-ildasm");

            Assert.Equal(2, result.Length);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void FilterCacheFilesByTool_MatchesIlspyFiles()
        {
            var files = new[]
            {
                "/cache/abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789_dotnet-ildasm _version_ 0.12.0_.ilcache",
                "/cache/1111110123456789abcdef0123456789abcdef0123456789abcdef0123456789_ilspycmd _version_ 8.2.0_.ilcache",
            };

            var result = ProgramRunner.FilterCacheFilesByTool(files, "ilspycmd");

            Assert.Single(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void FilterCacheFilesByTool_SkipsShortFilenames()
        {
            var files = new[] { "/cache/short.ilcache" };

            var result = ProgramRunner.FilterCacheFilesByTool(files, "dotnet-ildasm");

            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void FilterCacheFilesByToolLabel_MatchesExactVersion()
        {
            var files = new[]
            {
                "/cache/abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789_dotnet-ildasm _version_ 0.12.0_.ilcache",
                "/cache/1111110123456789abcdef0123456789abcdef0123456789abcdef0123456789_dotnet-ildasm _version_ 0.13.0_.ilcache",
                "/cache/2222220123456789abcdef0123456789abcdef0123456789abcdef0123456789_ilspycmd _version_ 8.2.0_.ilcache",
            };

            var result = ProgramRunner.FilterCacheFilesByToolLabel(files, "dotnet-ildasm (version: 0.12.0)");

            Assert.Single(result);
            Assert.Contains("0.12.0", result[0]);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ExtractDistinctToolLabels_ReturnsUniqueLabels()
        {
            var files = new[]
            {
                "/cache/abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789_dotnet-ildasm _version_ 0.12.0_.ilcache",
                "/cache/1111110123456789abcdef0123456789abcdef0123456789abcdef0123456789_dotnet-ildasm _version_ 0.12.0_.ilcache",
                "/cache/2222220123456789abcdef0123456789abcdef0123456789abcdef0123456789_ilspycmd _version_ 8.2.0_.ilcache",
                "/cache/3333330123456789abcdef0123456789abcdef0123456789abcdef0123456789_dotnet-ildasm _version_ 0.13.0_.ilcache",
            };

            var result = ProgramRunner.ExtractDistinctToolLabels(files);

            Assert.Equal(3, result.Length);
            Assert.Contains("dotnet-ildasm (version: 0.12.0)", result);
            Assert.Contains("dotnet-ildasm (version: 0.13.0)", result);
            Assert.Contains("ilspycmd (version: 8.2.0)", result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ExtractDistinctToolLabels_ReturnsSortedAlphabetically()
        {
            var files = new[]
            {
                "/cache/abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789_ilspycmd _version_ 8.2.0_.ilcache",
                "/cache/1111110123456789abcdef0123456789abcdef0123456789abcdef0123456789_dotnet-ildasm _version_ 0.12.0_.ilcache",
            };

            var result = ProgramRunner.ExtractDistinctToolLabels(files);

            Assert.Equal("dotnet-ildasm (version: 0.12.0)", result[0]);
            Assert.Equal("ilspycmd (version: 8.2.0)", result[1]);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ExtractDistinctToolLabels_EmptyArray_ReturnsEmpty()
        {
            var result = ProgramRunner.ExtractDistinctToolLabels(Array.Empty<string>());

            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ExtractDistinctToolLabels_SkipsShortFilenames()
        {
            var files = new[] { "/cache/tooshort.ilcache" };

            var result = ProgramRunner.ExtractDistinctToolLabels(files);

            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void UnsanitizeToolLabel_ReversesVersionPattern()
        {
            var result = ProgramRunner.UnsanitizeToolLabel("dotnet-ildasm _version_ 0.12.0_");

            Assert.Equal("dotnet-ildasm (version: 0.12.0)", result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void UnsanitizeToolLabel_NoVersionPattern_ReturnsAsIs()
        {
            var result = ProgramRunner.UnsanitizeToolLabel("unknown-tool");

            Assert.Equal("unknown-tool", result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SanitizeForCacheMatch_ReplacesColonsAndParentheses()
        {
            var result = ProgramRunner.SanitizeForCacheMatch("dotnet-ildasm (version: 0.12.0)");

            Assert.Equal("dotnet-ildasm _version_ 0.12.0_", result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SanitizeAndUnsanitize_Roundtrip()
        {
            // Verify that sanitize → unsanitize produces the original label
            // サニタイズ → 逆サニタイズで元のラベルに戻ることを検証
            var original = "ilspycmd (version: 8.2.0)";
            var sanitized = ProgramRunner.SanitizeForCacheMatch(original);
            var restored = ProgramRunner.UnsanitizeToolLabel(sanitized);

            Assert.Equal(original, restored);
        }
    }
}
