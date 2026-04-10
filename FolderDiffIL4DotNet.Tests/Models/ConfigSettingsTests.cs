using System;
using System.Text.Json;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Models
{
    public sealed partial class ConfigSettingsTests
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
            var config = new ConfigSettingsBuilder().Build();

            AssertMatchesDefaults(config);
        }

        [Fact]
        public void JsonDeserialize_EmptyObject_UsesCodeDefinedDefaults()
        {
            var config = JsonSerializer.Deserialize<ConfigSettingsBuilder>("{}")!.Build();

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
                  "ILCacheMaxMemoryMegabytes": 256,
                  "ILPrecomputeBatchSize": 512,
                  "OptimizeForNetworkShares": true,
                  "AutoDetectNetworkShares": false,
                  "SkipIL": true,
                  "ShouldIncludeILCacheStatsInReport": true,
                  "SpinnerFrames": [">", ">>", ">>>"],
                  "DisassemblerBlacklistTtlMinutes": 25,
                  "DisassemblerTimeoutSeconds": 600
                }
                """;

            var config = JsonSerializer.Deserialize<ConfigSettingsBuilder>(json)!.Build();

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
            Assert.Equal(256, config.ILCacheMaxMemoryMegabytes);
            Assert.Equal(512, config.ILPrecomputeBatchSize);
            Assert.True(config.OptimizeForNetworkShares);
            Assert.False(config.AutoDetectNetworkShares);
            Assert.True(config.SkipIL);
            Assert.True(config.ShouldIncludeILCacheStatsInReport);
            Assert.Equal(new[] { ">", ">>", ">>>" }, config.SpinnerFrames);
            Assert.Equal(25, config.DisassemblerBlacklistTtlMinutes);
            Assert.Equal(600, config.DisassemblerTimeoutSeconds);
        }

        [Fact]
        public void JsonDeserialize_NullSpinnerFrames_FallsBackToDefault()
        {
            const string json = """{ "SpinnerFrames": null }""";

            var config = JsonSerializer.Deserialize<ConfigSettingsBuilder>(json)!.Build();

            Assert.NotNull(config);
            Assert.Equal(new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" }, config.SpinnerFrames);
        }

        [Fact]
        public void Validate_EmptySpinnerFrames_ReturnsError()
        {
            var builder = new ConfigSettingsBuilder { SpinnerFrames = new System.Collections.Generic.List<string>() };

            var result = builder.Validate();

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("SpinnerFrames", StringComparison.Ordinal));
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

            var config = JsonSerializer.Deserialize<ConfigSettingsBuilder>(json)!.Build();

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
            var builder = new ConfigSettingsBuilder();

            var result = builder.Validate();

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Validate_MaxLogGenerationsLessThanOne_ReturnsError(int value)
        {
            var builder = new ConfigSettingsBuilder { MaxLogGenerations = value };

            var result = builder.Validate();

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
            var builder = new ConfigSettingsBuilder { TextDiffParallelThresholdKilobytes = value };

            var result = builder.Validate();

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("TextDiffParallelThresholdKilobytes", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Validate_TextDiffChunkSizeKilobytesLessThanOne_ReturnsError(int value)
        {
            var builder = new ConfigSettingsBuilder { TextDiffChunkSizeKilobytes = value };

            var result = builder.Validate();

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("TextDiffChunkSizeKilobytes", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(64, 64)]   // equal
        [InlineData(128, 64)]  // chunk > threshold
        public void Validate_ChunkSizeGreaterThanOrEqualToThreshold_ReturnsError(int chunkKb, int thresholdKb)
        {
            var builder = new ConfigSettingsBuilder
            {
                TextDiffChunkSizeKilobytes = chunkKb,
                TextDiffParallelThresholdKilobytes = thresholdKb,
            };

            var result = builder.Validate();

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e =>
                e.Contains("TextDiffChunkSizeKilobytes", StringComparison.Ordinal) &&
                e.Contains("TextDiffParallelThresholdKilobytes", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ChunkSizeSmallerThanThreshold_IsValid()
        {
            var builder = new ConfigSettingsBuilder
            {
                TextDiffChunkSizeKilobytes = 63,
                TextDiffParallelThresholdKilobytes = 64,
            };

            var result = builder.Validate();

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_MultipleErrors_ReturnsAllErrors()
        {
            var builder = new ConfigSettingsBuilder
            {
                MaxLogGenerations = 0,
                TextDiffParallelThresholdKilobytes = 0,
                TextDiffChunkSizeKilobytes = 0,
            };

            var result = builder.Validate();

            Assert.False(result.IsValid);
            Assert.Equal(3, result.Errors.Count);
        }

        // B-1: DisassemblerBlacklistTtlMinutes cross-validation

        [Fact]
        public void Constructor_DisassemblerBlacklistTtlMinutes_DefaultIsTen()
        {
            var config = new ConfigSettingsBuilder().Build();
            Assert.Equal(ConfigSettings.DefaultDisassemblerBlacklistTtlMinutes, config.DisassemblerBlacklistTtlMinutes);
        }

        [Fact]
        public void JsonDeserialize_DisassemblerBlacklistTtlMinutes_IsApplied()
        {
            const string json = """{ "DisassemblerBlacklistTtlMinutes": 30 }""";
            var config = JsonSerializer.Deserialize<ConfigSettingsBuilder>(json)!.Build();
            Assert.NotNull(config);
            Assert.Equal(30, config.DisassemblerBlacklistTtlMinutes);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(60)]
        public void Validate_DisassemblerBlacklistTtlMinutes_PositiveValues_IsValid(int minutes)
        {
            var builder = new ConfigSettingsBuilder { DisassemblerBlacklistTtlMinutes = minutes };
            var result = builder.Validate();
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_DisassemblerBlacklistTtlMinutes_NonPositive_UsesDefault_IsValid(int minutes)
        {
            // Values <= 0 are treated as the default (10 min), so Validate passes
            // 0 以下は既定値（10 分）として扱われるため Validate エラーにならない
            var builder = new ConfigSettingsBuilder { DisassemblerBlacklistTtlMinutes = minutes };
            var result = builder.Validate();
            Assert.True(result.IsValid);
        }

        // B-1: TextDiffChunkSizeKilobytes boundary at exactly threshold - 1 (valid edge)
        [Fact]
        public void Validate_ChunkSizeExactlyOneBeforeThreshold_IsValid()
        {
            var builder = new ConfigSettingsBuilder
            {
                TextDiffChunkSizeKilobytes = ConfigSettings.DefaultTextDiffParallelThresholdKilobytes - 1,
                TextDiffParallelThresholdKilobytes = ConfigSettings.DefaultTextDiffParallelThresholdKilobytes,
            };
            var result = builder.Validate();
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        // B-1: TextDiffChunkSizeKilobytes equal to threshold is invalid
        [Fact]
        public void Validate_ChunkSizeEqualToThreshold_ReturnsError()
        {
            var builder = new ConfigSettingsBuilder
            {
                TextDiffChunkSizeKilobytes = ConfigSettings.DefaultTextDiffParallelThresholdKilobytes,
                TextDiffParallelThresholdKilobytes = ConfigSettings.DefaultTextDiffParallelThresholdKilobytes,
            };
            var result = builder.Validate();
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e =>
                e.Contains("TextDiffChunkSizeKilobytes", StringComparison.Ordinal) &&
                e.Contains("TextDiffParallelThresholdKilobytes", StringComparison.Ordinal));
        }

        // ── Helpers / ヘルパー ────────────────────────────────────────────────────

        private static void AssertMatchesDefaults(ConfigSettings config)
        {
            Assert.Equal(ExpectedDefaultIgnoredExtensions, config.IgnoredExtensions);
            Assert.Equal(ExpectedDefaultTextFileExtensions, config.TextFileExtensions);
            Assert.Equal(ConfigSettings.DefaultMaxLogGenerations, config.MaxLogGenerations);
            Assert.Equal(ConfigSettings.DefaultShouldIncludeUnchangedFiles, config.ShouldIncludeUnchangedFiles);
            Assert.Equal(ConfigSettings.DefaultShouldIncludeIgnoredFiles, config.ShouldIncludeIgnoredFiles);
            Assert.Equal(ConfigSettings.DefaultShouldOutputILText, config.ShouldOutputILText);
            Assert.Equal(ConfigSettings.DefaultShouldIgnoreILLinesContainingConfiguredStrings, config.ShouldIgnoreILLinesContainingConfiguredStrings);
            Assert.NotNull(config.ILIgnoreLineContainingStrings);
            Assert.Empty(config.ILIgnoreLineContainingStrings);
            Assert.Equal(ConfigSettings.DefaultShouldOutputFileTimestamps, config.ShouldOutputFileTimestamps);
            Assert.Equal(ConfigSettings.DefaultShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp, config.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
            Assert.Equal(ConfigSettings.DefaultMaxParallelism, config.MaxParallelism);
            Assert.Equal(ConfigSettings.DefaultTextDiffParallelThresholdKilobytes, config.TextDiffParallelThresholdKilobytes);
            Assert.Equal(ConfigSettings.DefaultTextDiffChunkSizeKilobytes, config.TextDiffChunkSizeKilobytes);
            Assert.Equal(ConfigSettings.DefaultTextDiffParallelMemoryLimitMegabytes, config.TextDiffParallelMemoryLimitMegabytes);
            Assert.Equal(ConfigSettings.DefaultEnableILCache, config.EnableILCache);
            Assert.Equal(string.Empty, config.ILCacheDirectoryAbsolutePath);
            Assert.Equal(ConfigSettings.DefaultILCacheStatsLogIntervalSeconds, config.ILCacheStatsLogIntervalSeconds);
            Assert.Equal(ConfigSettings.DefaultILCacheMaxDiskFileCount, config.ILCacheMaxDiskFileCount);
            Assert.Equal(ConfigSettings.DefaultILCacheMaxDiskMegabytes, config.ILCacheMaxDiskMegabytes);
            Assert.Equal(ConfigSettings.DefaultILCacheMaxMemoryMegabytes, config.ILCacheMaxMemoryMegabytes);
            Assert.Equal(ConfigSettings.DefaultILPrecomputeBatchSize, config.ILPrecomputeBatchSize);
            Assert.Equal(ConfigSettings.DefaultOptimizeForNetworkShares, config.OptimizeForNetworkShares);
            Assert.Equal(ConfigSettings.DefaultAutoDetectNetworkShares, config.AutoDetectNetworkShares);
            Assert.Equal(ConfigSettings.DefaultSkipIL, config.SkipIL);
            Assert.Equal(ConfigSettings.DefaultShouldIncludeILCacheStatsInReport, config.ShouldIncludeILCacheStatsInReport);
            Assert.Equal(new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" }, config.SpinnerFrames);
            Assert.Equal(ConfigSettings.DefaultDisassemblerBlacklistTtlMinutes, config.DisassemblerBlacklistTtlMinutes);
            Assert.Equal(ConfigSettings.DefaultDisassemblerTimeoutSeconds, config.DisassemblerTimeoutSeconds);
            Assert.Equal(ConfigSettings.DefaultEnableInlineDiff, config.EnableInlineDiff);
            Assert.Equal(ConfigSettings.DefaultInlineDiffContextLines, config.InlineDiffContextLines);
            Assert.Equal(ConfigSettings.DefaultInlineDiffMaxEditDistance, config.InlineDiffMaxEditDistance);
            Assert.Equal(ConfigSettings.DefaultInlineDiffMaxDiffLines, config.InlineDiffMaxDiffLines);
            Assert.Equal(ConfigSettings.DefaultInlineDiffMaxOutputLines, config.InlineDiffMaxOutputLines);
            Assert.Equal(ConfigSettings.DefaultInlineDiffLazyRender, config.InlineDiffLazyRender);
            Assert.Equal(ConfigSettings.DefaultShouldIncludeAssemblySemanticChangesInReport, config.ShouldIncludeAssemblySemanticChangesInReport);
            Assert.Equal(ConfigSettings.DefaultShouldGenerateHtmlReport, config.ShouldGenerateHtmlReport);
            Assert.Equal(ConfigSettings.DefaultShouldGenerateAuditLog, config.ShouldGenerateAuditLog);
            Assert.Equal(ConfigSettings.DefaultShouldGenerateSbom, config.ShouldGenerateSbom);
            Assert.Equal(ConfigSettings.DefaultSbomFormat, config.SbomFormat);
        }

        private static void AssertJsonBool(JsonElement root, string propertyName, bool expected)
        {
            Assert.True(root.TryGetProperty(propertyName, out var el),
                $"config.sample.jsonc is missing property '{propertyName}'");
            Assert.Equal(expected, el.GetBoolean());
        }

        private static void AssertJsonInt(JsonElement root, string propertyName, int expected)
        {
            Assert.True(root.TryGetProperty(propertyName, out var el),
                $"config.sample.jsonc is missing property '{propertyName}'");
            Assert.Equal(expected, el.GetInt32());
        }

        private static string FindRepoRoot()
        {
            // Walk up from the test assembly output directory to find the repo root (contains .git)
            // テストアセンブリ出力ディレクトリから上へ辿り、リポジトリルート（.git を含む）を探す
            var dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (System.IO.Directory.Exists(System.IO.Path.Combine(dir, ".git")))
                {
                    return dir;
                }
                dir = System.IO.Path.GetDirectoryName(dir);
            }
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }

        private static string StripJsoncComments(string jsonc)
        {
            // Remove single-line comments (// ...) while preserving strings / 文字列を保持しつつ行コメントを除去
            var sb = new System.Text.StringBuilder(jsonc.Length);
            bool inString = false;
            for (int i = 0; i < jsonc.Length; i++)
            {
                var c = jsonc[i];
                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < jsonc.Length) { sb.Append(jsonc[++i]); }
                    else if (c == '"') { inString = false; }
                }
                else if (c == '"') { inString = true; sb.Append(c); }
                else if (c == '/' && i + 1 < jsonc.Length && jsonc[i + 1] == '/')
                {
                    while (i < jsonc.Length && jsonc[i] != '\n') { i++; }
                    if (i < jsonc.Length) { sb.Append('\n'); }
                }
                else { sb.Append(c); }
            }
            return sb.ToString();
        }
    }
}
