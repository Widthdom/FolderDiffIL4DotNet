using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public partial class ConfigServiceTests
    {
        private static readonly string ConfigFilePath = Path.Combine(AppContext.BaseDirectory, "config.json");

        [Fact]
        public async Task LoadConfigBuilderAsync_ConfigFileMissing_ThrowsFileNotFoundException()
        {
            await WithConfigFileAsync(content: string.Empty, async () =>
            {
                var service = new ConfigService();
                await Assert.ThrowsAsync<FileNotFoundException>(() => service.LoadConfigBuilderAsync());
            }, deleteConfig: true);
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_InvalidJson_ThrowsInvalidDataException()
        {
            await WithConfigFileAsync("{ invalid-json", async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigBuilderAsync());
                Assert.IsType<JsonException>(ex.InnerException);
                Assert.Contains("JSON syntax error", ex.Message, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_TrailingCommaInObject_Accepted()
        {
            // JSONC support: trailing commas are now allowed
            // JSONC サポート: 末尾カンマは許容される
            await WithConfigFileAsync("{ \"MaxLogGenerations\": 5, }", async () =>
            {
                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync();

                Assert.NotNull(builder);
                Assert.Equal(5, builder.MaxLogGenerations);
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_TrailingCommaInArray_Accepted()
        {
            // JSONC support: trailing commas in arrays are now allowed
            // JSONC サポート: 配列の末尾カンマは許容される
            await WithConfigFileAsync("{ \"IgnoredExtensions\": [\".pdb\", \".log\",] }", async () =>
            {
                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync();

                Assert.NotNull(builder);
                Assert.Equal(new[] { ".pdb", ".log" }, builder.IgnoredExtensions);
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_SyntaxError_MessageIncludesLineNumber()
        {
            // Verify that line number information is included in the error message for genuine syntax errors
            // 真の構文エラー時にエラーメッセージに行番号が含まれることを確認
            const string json = """
                {
                  "MaxLogGenerations": 5
                  "Extra": true
                }
                """;
            await WithConfigFileAsync(json, async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigBuilderAsync());
                Assert.IsType<JsonException>(ex.InnerException);
                // Message should contain a line number (integer >= 1)
                // メッセージに行番号（1 以上の整数）が含まれている
                Assert.Matches(@"line \d+", ex.Message);
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_JsoncWithComments_Accepted()
        {
            // JSONC support: single-line comments are now allowed in config.json
            // JSONC サポート: config.json でシングルラインコメントが許容される
            const string jsonc = """
                {
                  // This is a comment
                  "MaxLogGenerations": 7
                }
                """;
            await WithConfigFileAsync(jsonc, async () =>
            {
                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync();

                Assert.NotNull(builder);
                Assert.Equal(7, builder.MaxLogGenerations);
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_ValidJson_ReturnsDeserializedBuilder()
        {
            const string json = """
                {
                  "IgnoredExtensions": [".tmp"],
                  "TextFileExtensions": [".cs", ".json"],
                  "MaxLogGenerations": 42,
                  "ShouldIncludeUnchangedFiles": false,
                  "ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp": false
                }
                """;

            await WithConfigFileAsync(json, async () =>
            {
                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync();

                Assert.NotNull(builder);
                Assert.Equal(42, builder.MaxLogGenerations);
                Assert.False(builder.ShouldIncludeUnchangedFiles);
                Assert.False(builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
                Assert.Equal(new[] { ".tmp" }, builder.IgnoredExtensions);
                Assert.Equal(new[] { ".cs", ".json" }, builder.TextFileExtensions);
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_EmptyObject_UsesCodeDefinedDefaults()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync();

                Assert.NotNull(builder);
                Assert.Equal(5, builder.MaxLogGenerations);
                Assert.True(builder.ShouldIncludeUnchangedFiles);
                Assert.True(builder.ShouldIncludeIgnoredFiles);
                Assert.True(builder.ShouldOutputILText);
                Assert.True(builder.ShouldOutputFileTimestamps);
                Assert.True(builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
                Assert.True(builder.EnableILCache);
                Assert.Equal(ConfigSettings.DefaultILCacheStatsLogIntervalSeconds, builder.ILCacheStatsLogIntervalSeconds);
                Assert.True(builder.AutoDetectNetworkShares);
                Assert.Contains(".cs", builder.TextFileExtensions);
                Assert.Contains(".pdb", builder.IgnoredExtensions);
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_NullJson_ThrowsInvalidDataException()
        {
            await WithConfigFileAsync("null", async () =>
            {
                var service = new ConfigService();
                await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigBuilderAsync());
            });
        }

        [Fact]
        public void Validate_InvalidMaxLogGenerations_ReturnsErrorWithDetails()
        {
            var builder = new ConfigSettingsBuilder { MaxLogGenerations = 0 };
            var result = builder.Validate();

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("MaxLogGenerations", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_InvalidTextDiffThreshold_ReturnsErrorWithDetails()
        {
            var builder = new ConfigSettingsBuilder { TextDiffParallelThresholdKilobytes = -1 };
            var result = builder.Validate();

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("TextDiffParallelThresholdKilobytes", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ChunkSizeEqualToThreshold_ReturnsErrorWithDetails()
        {
            var builder = new ConfigSettingsBuilder
            {
                TextDiffChunkSizeKilobytes = 64,
                TextDiffParallelThresholdKilobytes = 64
            };
            var result = builder.Validate();

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e =>
                e.Contains("TextDiffChunkSizeKilobytes", StringComparison.Ordinal) &&
                e.Contains("TextDiffParallelThresholdKilobytes", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_MultipleInvalidSettings_ReturnsAllErrorDetails()
        {
            var builder = new ConfigSettingsBuilder
            {
                MaxLogGenerations = 0,
                TextDiffParallelThresholdKilobytes = 0,
                TextDiffChunkSizeKilobytes = 0
            };
            var result = builder.Validate();

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("MaxLogGenerations", StringComparison.Ordinal));
            Assert.Contains(result.Errors, e => e.Contains("TextDiffParallelThresholdKilobytes", StringComparison.Ordinal));
            Assert.Contains(result.Errors, e => e.Contains("TextDiffChunkSizeKilobytes", StringComparison.Ordinal));
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_ValidCustomSettings_ReturnsBuilder()
        {
            const string json = """
                {
                  "MaxLogGenerations": 3,
                  "TextDiffParallelThresholdKilobytes": 256,
                  "TextDiffChunkSizeKilobytes": 32
                }
                """;

            await WithConfigFileAsync(json, async () =>
            {
                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync();

                Assert.Equal(3, builder.MaxLogGenerations);
                Assert.Equal(256, builder.TextDiffParallelThresholdKilobytes);
                Assert.Equal(32, builder.TextDiffChunkSizeKilobytes);
            });
        }

        // ── configFilePath parameter tests / configFilePath パラメータテスト ──

        [Fact]
        public async Task LoadConfigBuilderAsync_CustomConfigFilePath_LoadsFromSpecifiedPath()
        {
            var customConfigPath = Path.Combine(Path.GetTempPath(), $"test-custom-{Guid.NewGuid():N}.json");
            const string json = """{ "MaxLogGenerations": 99 }""";
            await File.WriteAllTextAsync(customConfigPath, json);

            try
            {
                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync(customConfigPath);

                Assert.NotNull(builder);
                Assert.Equal(99, builder.MaxLogGenerations);
            }
            finally
            {
                if (File.Exists(customConfigPath))
                {
                    File.Delete(customConfigPath);
                }
            }
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_CustomConfigFilePathMissing_ThrowsFileNotFoundException()
        {
            var service = new ConfigService();

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => service.LoadConfigBuilderAsync("/nonexistent/path/to/config.json"));
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_NullConfigFilePath_FallsBackToDefaultPath()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync(null);

                Assert.NotNull(builder);
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_EmptyConfigFilePath_FallsBackToDefaultPath()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                var service = new ConfigService();
                var builder = await service.LoadConfigBuilderAsync(string.Empty);

                Assert.NotNull(builder);
            });
        }

        // ── Helpers / ヘルパー ──────────────────────────────────────────────────

        private static async Task WithEnvVarsAsync(
            (string key, string value)[] vars,
            Func<Task> action)
        {
            var originals = new (string key, string original)[vars.Length];
            for (int i = 0; i < vars.Length; i++)
            {
                originals[i] = (vars[i].key, Environment.GetEnvironmentVariable(vars[i].key));
                Environment.SetEnvironmentVariable(vars[i].key, vars[i].value);
            }

            try
            {
                await action();
            }
            finally
            {
                foreach (var (key, original) in originals)
                {
                    Environment.SetEnvironmentVariable(key, original);
                }
            }
        }

        private static void WithEnvVars((string key, string value)[] vars, Action action)
        {
            var originals = new (string key, string original)[vars.Length];
            for (int i = 0; i < vars.Length; i++)
            {
                originals[i] = (vars[i].key, Environment.GetEnvironmentVariable(vars[i].key));
                Environment.SetEnvironmentVariable(vars[i].key, vars[i].value);
            }

            try
            {
                action();
            }
            finally
            {
                foreach (var (key, original) in originals)
                {
                    Environment.SetEnvironmentVariable(key, original);
                }
            }
        }

        private static void WithEnvVar(string key, string value, Action action)
            => WithEnvVars(new[] { (key, value) }, action);

        private static async Task WithConfigFileAsync(string content, Func<Task> assertion, bool deleteConfig = false)
        {
            var backupExists = File.Exists(ConfigFilePath);
            var backupContent = backupExists ? await File.ReadAllTextAsync(ConfigFilePath) : null;

            try
            {
                if (deleteConfig)
                {
                    if (File.Exists(ConfigFilePath))
                    {
                        File.Delete(ConfigFilePath);
                    }
                }
                else
                {
                    await File.WriteAllTextAsync(ConfigFilePath, content);
                }

                await assertion();
            }
            finally
            {
                if (backupExists)
                {
                    await File.WriteAllTextAsync(ConfigFilePath, backupContent ?? string.Empty);
                }
                else if (File.Exists(ConfigFilePath))
                {
                    File.Delete(ConfigFilePath);
                }
            }
        }
    }
}
