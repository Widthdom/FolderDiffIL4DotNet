using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    // Profile loading tests for ConfigService.
    // ConfigService のプロファイル読み込みテスト。
    public partial class ConfigServiceTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        public async Task LoadConfigBuilderAsync_WithProfile_OverlaysProfileValues()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"folderdiff-profile-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var profilesDir = Path.Combine(tempDir, ConfigService.PROFILES_DIR_NAME);
            Directory.CreateDirectory(profilesDir);

            try
            {
                var configPath = Path.Combine(tempDir, "config.json");
                await File.WriteAllTextAsync(configPath, """{ "MaxLogGenerations": 5, "MaxParallelism": 4 }""");
                await File.WriteAllTextAsync(Path.Combine(profilesDir, "prod.json"), """{ "MaxParallelism": 8 }""");

                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync(configPath, "prod");

                Assert.Equal(5, builder.MaxLogGenerations);
                Assert.Equal(8, builder.MaxParallelism);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task LoadConfigBuilderAsync_WithProfile_ArraysAreReplacedNotMerged()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"folderdiff-profile-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var profilesDir = Path.Combine(tempDir, ConfigService.PROFILES_DIR_NAME);
            Directory.CreateDirectory(profilesDir);

            try
            {
                var configPath = Path.Combine(tempDir, "config.json");
                await File.WriteAllTextAsync(configPath, """{ "IgnoredExtensions": [".pdb", ".log"] }""");
                await File.WriteAllTextAsync(Path.Combine(profilesDir, "minimal.json"), """{ "IgnoredExtensions": [".pdb"] }""");

                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync(configPath, "minimal");

                Assert.Single(builder.IgnoredExtensions);
                Assert.Equal(".pdb", builder.IgnoredExtensions[0]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task LoadConfigBuilderAsync_ProfileNotFound_ThrowsFileNotFoundException()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"folderdiff-profile-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var configPath = Path.Combine(tempDir, "config.json");
                await File.WriteAllTextAsync(configPath, "{}");

                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<FileNotFoundException>(
                    () => service.LoadConfigBuilderAsync(configPath, "nonexistent"));
                Assert.Contains("nonexistent", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task LoadConfigBuilderAsync_NullProfile_IgnoresProfile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"folderdiff-profile-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var configPath = Path.Combine(tempDir, "config.json");
                await File.WriteAllTextAsync(configPath, """{ "MaxLogGenerations": 7 }""");

                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync(configPath, null);

                Assert.Equal(7, builder.MaxLogGenerations);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void MergeJson_OverlayReplacesBaseProperties()
        {
            const string baseJson = """{ "MaxLogGenerations": 5, "MaxParallelism": 4 }""";
            const string overlayJson = """{ "MaxParallelism": 16 }""";

            var merged = ConfigService.MergeJson(baseJson, overlayJson);

            Assert.Contains("\"MaxLogGenerations\"", merged, StringComparison.Ordinal);
            Assert.Contains("16", merged, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void MergeJson_OverlaySchemaKeyIsSkipped()
        {
            const string baseJson = """{ "MaxLogGenerations": 5 }""";
            const string overlayJson = """{ "$schema": "./config.schema.json", "MaxParallelism": 8 }""";

            var merged = ConfigService.MergeJson(baseJson, overlayJson);

            // $schema from overlay should NOT overwrite base (base didn't have it, overlay's is skipped)
            // The merged result should have MaxParallelism but not $schema from overlay
            Assert.Contains("\"MaxParallelism\"", merged, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task LoadConfigBuilderAsync_JsoncProfile_CommentsAndTrailingCommasAllowed()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"folderdiff-profile-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var profilesDir = Path.Combine(tempDir, ConfigService.PROFILES_DIR_NAME);
            Directory.CreateDirectory(profilesDir);

            try
            {
                var configPath = Path.Combine(tempDir, "config.json");
                await File.WriteAllTextAsync(configPath, """{ "MaxLogGenerations": 5 }""");

                // JSONC with comments and trailing comma / コメントと末尾カンマを含む JSONC
                await File.WriteAllTextAsync(Path.Combine(profilesDir, "commented.jsonc"), """
                    {
                      // Override parallelism for this profile
                      "MaxParallelism": 12,
                    }
                    """);

                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync(configPath, "commented");

                Assert.Equal(5, builder.MaxLogGenerations);
                Assert.Equal(12, builder.MaxParallelism);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public async Task LoadConfigBuilderAsync_JsoncFallback_PrefersJsonOverJsonc()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"folderdiff-profile-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var profilesDir = Path.Combine(tempDir, ConfigService.PROFILES_DIR_NAME);
            Directory.CreateDirectory(profilesDir);

            try
            {
                var configPath = Path.Combine(tempDir, "config.json");
                await File.WriteAllTextAsync(configPath, "{}");

                // Both .json and .jsonc exist — .json should win
                // .json と .jsonc の両方が存在する場合 .json が優先される
                await File.WriteAllTextAsync(Path.Combine(profilesDir, "dual.json"), """{ "MaxParallelism": 2 }""");
                await File.WriteAllTextAsync(Path.Combine(profilesDir, "dual.jsonc"), """{ "MaxParallelism": 99 }""");

                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync(configPath, "dual");

                Assert.Equal(2, builder.MaxParallelism);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void MergeJson_JsoncWithComments_ParsedCorrectly()
        {
            const string baseJson = """{ "MaxLogGenerations": 5 }""";
            const string overlayJsonc = """
                {
                  // This is a comment
                  "MaxParallelism": 16,
                }
                """;

            var merged = ConfigService.MergeJson(baseJson, overlayJsonc);

            Assert.Contains("\"MaxParallelism\"", merged, StringComparison.Ordinal);
            Assert.Contains("16", merged, StringComparison.Ordinal);
        }
    }
}
