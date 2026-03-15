using System;
using System.Text.Json;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Models
{
    public sealed class ConfigSettingsTests
    {
        private static readonly string[] ExpectedDefaultIgnoredExtensions =
        {
            ".cache", ".DS_Store", ".db", ".ilcache", ".log", ".pdb"
        };

        private static readonly string[] ExpectedDefaultTextFileExtensions =
        {
            ".asax", ".ascx", ".asmx", ".aspx", ".bat", ".c", ".cmd", ".config", ".cpp", ".cs",
            ".cshtml", ".csproj", ".csx", ".css", ".csv", ".editorconfig", ".env", ".fs", ".fsi",
            ".fsproj", ".fsx", ".gitattributes", ".gitignore", ".gitmodules", ".go", ".gql",
            ".graphql", ".h", ".hpp", ".htm", ".html", ".http", ".ini", ".js", ".json", ".jsx",
            ".less", ".manifest", ".md", ".mod", ".nlog", ".nuspec", ".plist", ".props", ".ps1",
            ".psd1", ".psm1", ".py", ".razor", ".resx", ".rst", ".sass", ".scss", ".sh", ".sln",
            ".sql", ".sqlproj", ".sum", ".svg", ".targets", ".toml", ".ts", ".tsv", ".tsx",
            ".txt", ".vb", ".vbproj", ".vue", ".xaml", ".xml", ".yaml", ".yml"
        };

        [Fact]
        public void Constructor_UsesCodeDefinedDefaults()
        {
            var config = new ConfigSettings();

            AssertMatchesDefaults(config);
        }

        [Fact]
        public void JsonDeserialize_EmptyObject_UsesCodeDefinedDefaults()
        {
            var config = JsonSerializer.Deserialize<ConfigSettings>("{}");

            Assert.NotNull(config);
            AssertMatchesDefaults(config);
        }

        [Fact]
        public void JsonDeserialize_ExplicitOverrides_AreApplied()
        {
            const string json = """
                {
                  "IgnoredExtensions": [".tmp"],
                  "TextFileExtensions": [".txt"],
                  "MaxLogGenerations": 42,
                  "ShouldIncludeUnchangedFiles": false,
                  "ShouldIncludeIgnoredFiles": false,
                  "ShouldOutputILText": false,
                  "ShouldIgnoreILLinesContainingConfiguredStrings": true,
                  "ILIgnoreLineContainingStrings": ["buildserver", "path"],
                  "ShouldOutputFileTimestamps": false,
                  "ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp": false,
                  "MaxParallelism": 8,
                  "TextDiffParallelThresholdKilobytes": 128,
                  "TextDiffChunkSizeKilobytes": 8,
                  "TextDiffParallelMemoryLimitMegabytes": 32,
                  "EnableILCache": false,
                  "ILCacheDirectoryAbsolutePath": "/tmp/il-cache",
                  "ILCacheStatsLogIntervalSeconds": 30,
                  "ILCacheMaxDiskFileCount": 10,
                  "ILCacheMaxDiskMegabytes": 20,
                  "ILPrecomputeBatchSize": 512,
                  "OptimizeForNetworkShares": true,
                  "AutoDetectNetworkShares": false,
                  "SkipIL": true
                }
                """;

            var config = JsonSerializer.Deserialize<ConfigSettings>(json);

            Assert.NotNull(config);
            Assert.Equal(new[] { ".tmp" }, config.IgnoredExtensions);
            Assert.Equal(new[] { ".txt" }, config.TextFileExtensions);
            Assert.Equal(42, config.MaxLogGenerations);
            Assert.False(config.ShouldIncludeUnchangedFiles);
            Assert.False(config.ShouldIncludeIgnoredFiles);
            Assert.False(config.ShouldOutputILText);
            Assert.True(config.ShouldIgnoreILLinesContainingConfiguredStrings);
            Assert.Equal(new[] { "buildserver", "path" }, config.ILIgnoreLineContainingStrings);
            Assert.False(config.ShouldOutputFileTimestamps);
            Assert.False(config.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
            Assert.Equal(8, config.MaxParallelism);
            Assert.Equal(128, config.TextDiffParallelThresholdKilobytes);
            Assert.Equal(8, config.TextDiffChunkSizeKilobytes);
            Assert.Equal(32, config.TextDiffParallelMemoryLimitMegabytes);
            Assert.False(config.EnableILCache);
            Assert.Equal("/tmp/il-cache", config.ILCacheDirectoryAbsolutePath);
            Assert.Equal(30, config.ILCacheStatsLogIntervalSeconds);
            Assert.Equal(10, config.ILCacheMaxDiskFileCount);
            Assert.Equal(20, config.ILCacheMaxDiskMegabytes);
            Assert.Equal(512, config.ILPrecomputeBatchSize);
            Assert.True(config.OptimizeForNetworkShares);
            Assert.False(config.AutoDetectNetworkShares);
            Assert.True(config.SkipIL);
        }

        [Fact]
        public void JsonDeserialize_NullCollectionsAndCachePath_FallBackToDefaults()
        {
            const string json = """
                {
                  "IgnoredExtensions": null,
                  "TextFileExtensions": null,
                  "ILIgnoreLineContainingStrings": null,
                  "ILCacheDirectoryAbsolutePath": null
                }
                """;

            var config = JsonSerializer.Deserialize<ConfigSettings>(json);

            Assert.NotNull(config);
            Assert.Equal(ExpectedDefaultIgnoredExtensions, config.IgnoredExtensions);
            Assert.Equal(ExpectedDefaultTextFileExtensions, config.TextFileExtensions);
            Assert.NotNull(config.ILIgnoreLineContainingStrings);
            Assert.Empty(config.ILIgnoreLineContainingStrings);
            Assert.Equal(string.Empty, config.ILCacheDirectoryAbsolutePath);
        }

        [Fact]
        public void Validate_DefaultSettings_IsValid()
        {
            var config = new ConfigSettings();

            var result = config.Validate();

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Validate_MaxLogGenerationsLessThanOne_ReturnsError(int value)
        {
            var config = new ConfigSettings { MaxLogGenerations = value };

            var result = config.Validate();

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Contains("MaxLogGenerations", result.Errors[0], StringComparison.Ordinal);
            Assert.Contains(value.ToString(), result.Errors[0], StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Validate_TextDiffParallelThresholdKilobytesLessThanOne_ReturnsError(int value)
        {
            var config = new ConfigSettings { TextDiffParallelThresholdKilobytes = value };

            var result = config.Validate();

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("TextDiffParallelThresholdKilobytes", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Validate_TextDiffChunkSizeKilobytesLessThanOne_ReturnsError(int value)
        {
            var config = new ConfigSettings { TextDiffChunkSizeKilobytes = value };

            var result = config.Validate();

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("TextDiffChunkSizeKilobytes", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(64, 64)]   // equal
        [InlineData(128, 64)]  // chunk > threshold
        public void Validate_ChunkSizeGreaterThanOrEqualToThreshold_ReturnsError(int chunkKb, int thresholdKb)
        {
            var config = new ConfigSettings
            {
                TextDiffChunkSizeKilobytes = chunkKb,
                TextDiffParallelThresholdKilobytes = thresholdKb,
            };

            var result = config.Validate();

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e =>
                e.Contains("TextDiffChunkSizeKilobytes", StringComparison.Ordinal) &&
                e.Contains("TextDiffParallelThresholdKilobytes", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ChunkSizeSmallerThanThreshold_IsValid()
        {
            var config = new ConfigSettings
            {
                TextDiffChunkSizeKilobytes = 63,
                TextDiffParallelThresholdKilobytes = 64,
            };

            var result = config.Validate();

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_MultipleErrors_ReturnsAllErrors()
        {
            var config = new ConfigSettings
            {
                MaxLogGenerations = 0,
                TextDiffParallelThresholdKilobytes = 0,
                TextDiffChunkSizeKilobytes = 0,
            };

            var result = config.Validate();

            Assert.False(result.IsValid);
            Assert.Equal(3, result.Errors.Count);
        }

        private static void AssertMatchesDefaults(ConfigSettings config)
        {
            Assert.Equal(ExpectedDefaultIgnoredExtensions, config.IgnoredExtensions);
            Assert.Equal(ExpectedDefaultTextFileExtensions, config.TextFileExtensions);
            Assert.Equal(5, config.MaxLogGenerations);
            Assert.True(config.ShouldIncludeUnchangedFiles);
            Assert.True(config.ShouldIncludeIgnoredFiles);
            Assert.True(config.ShouldOutputILText);
            Assert.False(config.ShouldIgnoreILLinesContainingConfiguredStrings);
            Assert.NotNull(config.ILIgnoreLineContainingStrings);
            Assert.Empty(config.ILIgnoreLineContainingStrings);
            Assert.True(config.ShouldOutputFileTimestamps);
            Assert.True(config.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
            Assert.Equal(0, config.MaxParallelism);
            Assert.Equal(512, config.TextDiffParallelThresholdKilobytes);
            Assert.Equal(64, config.TextDiffChunkSizeKilobytes);
            Assert.Equal(0, config.TextDiffParallelMemoryLimitMegabytes);
            Assert.True(config.EnableILCache);
            Assert.Equal(string.Empty, config.ILCacheDirectoryAbsolutePath);
            Assert.Equal(60, config.ILCacheStatsLogIntervalSeconds);
            Assert.Equal(1000, config.ILCacheMaxDiskFileCount);
            Assert.Equal(512, config.ILCacheMaxDiskMegabytes);
            Assert.Equal(2048, config.ILPrecomputeBatchSize);
            Assert.False(config.OptimizeForNetworkShares);
            Assert.True(config.AutoDetectNetworkShares);
            Assert.False(config.SkipIL);
        }
    }
}
