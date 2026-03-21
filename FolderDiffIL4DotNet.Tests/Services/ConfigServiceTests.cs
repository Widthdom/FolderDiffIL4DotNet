using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public class ConfigServiceTests
    {
        private static readonly string ConfigFilePath = Path.Combine(AppContext.BaseDirectory, "config.json");

        [Fact]
        public async Task LoadConfigAsync_ConfigFileMissing_ThrowsFileNotFoundException()
        {
            await WithConfigFileAsync(content: string.Empty, async () =>
            {
                var service = new ConfigService();
                await Assert.ThrowsAsync<FileNotFoundException>(() => service.LoadConfigAsync());
            }, deleteConfig: true);
        }

        [Fact]
        public async Task LoadConfigAsync_InvalidJson_ThrowsInvalidDataException()
        {
            await WithConfigFileAsync("{ invalid-json", async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigAsync());
                Assert.IsType<JsonException>(ex.InnerException);
                Assert.Contains("JSON syntax error", ex.Message, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public async Task LoadConfigAsync_TrailingCommaInObject_ThrowsInvalidDataExceptionWithHint()
        {
            // Common mistake: trailing comma after the last property in a JSON object
            // よくあるミス: オブジェクトの最後のプロパティ後にカンマを入れてしまう
            await WithConfigFileAsync("{ \"MaxLogGenerations\": 5, }", async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigAsync());
                Assert.IsType<JsonException>(ex.InnerException);
                Assert.Contains("JSON syntax error", ex.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("trailing", ex.Message, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public async Task LoadConfigAsync_TrailingCommaInArray_ThrowsInvalidDataExceptionWithHint()
        {
            // Common mistake: trailing comma after the last element in a JSON array
            // よくあるミス: 配列の最後の要素後にカンマを入れてしまう
            await WithConfigFileAsync("{ \"IgnoredExtensions\": [\".pdb\", \".log\",] }", async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigAsync());
                Assert.IsType<JsonException>(ex.InnerException);
                Assert.Contains("JSON syntax error", ex.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("trailing", ex.Message, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public async Task LoadConfigAsync_TrailingCommaError_MessageIncludesLineNumber()
        {
            // Verify that line number information is included in the error message
            // 行番号情報がメッセージに含まれることを確認
            const string json = """
                {
                  "MaxLogGenerations": 5,
                }
                """;
            await WithConfigFileAsync(json, async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigAsync());
                Assert.IsType<JsonException>(ex.InnerException);
                // Message should contain a line number (integer >= 1)
                // メッセージに行番号（1 以上の整数）が含まれている
                Assert.Matches(@"line \d+", ex.Message);
            });
        }

        [Fact]
        public async Task LoadConfigAsync_ValidJson_ReturnsDeserializedSettings()
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
                var config = await service.LoadConfigAsync();

                Assert.NotNull(config);
                Assert.Equal(42, config.MaxLogGenerations);
                Assert.False(config.ShouldIncludeUnchangedFiles);
                Assert.False(config.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
                Assert.Equal(new[] { ".tmp" }, config.IgnoredExtensions);
                Assert.Equal(new[] { ".cs", ".json" }, config.TextFileExtensions);
            });
        }

        [Fact]
        public async Task LoadConfigAsync_EmptyObject_UsesCodeDefinedDefaults()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                var service = new ConfigService();
                var config = await service.LoadConfigAsync();

                Assert.NotNull(config);
                Assert.Equal(5, config.MaxLogGenerations);
                Assert.True(config.ShouldIncludeUnchangedFiles);
                Assert.True(config.ShouldIncludeIgnoredFiles);
                Assert.True(config.ShouldOutputILText);
                Assert.True(config.ShouldOutputFileTimestamps);
                Assert.True(config.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
                Assert.True(config.EnableILCache);
                Assert.Equal(ConfigSettings.DefaultILCacheStatsLogIntervalSeconds, config.ILCacheStatsLogIntervalSeconds);
                Assert.True(config.AutoDetectNetworkShares);
                Assert.Contains(".cs", config.TextFileExtensions);
                Assert.Contains(".pdb", config.IgnoredExtensions);
            });
        }

        [Fact]
        public async Task LoadConfigAsync_NullJson_ThrowsInvalidDataException()
        {
            await WithConfigFileAsync("null", async () =>
            {
                var service = new ConfigService();
                await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigAsync());
            });
        }

        [Fact]
        public async Task LoadConfigAsync_InvalidMaxLogGenerations_ThrowsInvalidDataExceptionWithDetails()
        {
            const string json = """{ "MaxLogGenerations": 0 }""";

            await WithConfigFileAsync(json, async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigAsync());
                Assert.Contains(ConfigService.ERROR_CONFIG_VALIDATION_PREFIX, ex.Message, StringComparison.Ordinal);
                Assert.Contains("MaxLogGenerations", ex.Message, StringComparison.Ordinal);
            });
        }

        [Fact]
        public async Task LoadConfigAsync_InvalidTextDiffThreshold_ThrowsInvalidDataExceptionWithDetails()
        {
            const string json = """{ "TextDiffParallelThresholdKilobytes": -1 }""";

            await WithConfigFileAsync(json, async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigAsync());
                Assert.Contains(ConfigService.ERROR_CONFIG_VALIDATION_PREFIX, ex.Message, StringComparison.Ordinal);
                Assert.Contains("TextDiffParallelThresholdKilobytes", ex.Message, StringComparison.Ordinal);
            });
        }

        [Fact]
        public async Task LoadConfigAsync_ChunkSizeEqualToThreshold_ThrowsInvalidDataExceptionWithDetails()
        {
            const string json = """{ "TextDiffChunkSizeKilobytes": 64, "TextDiffParallelThresholdKilobytes": 64 }""";

            await WithConfigFileAsync(json, async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigAsync());
                Assert.Contains(ConfigService.ERROR_CONFIG_VALIDATION_PREFIX, ex.Message, StringComparison.Ordinal);
                Assert.Contains("TextDiffChunkSizeKilobytes", ex.Message, StringComparison.Ordinal);
                Assert.Contains("TextDiffParallelThresholdKilobytes", ex.Message, StringComparison.Ordinal);
            });
        }

        [Fact]
        public async Task LoadConfigAsync_MultipleInvalidSettings_ThrowsWithAllErrorDetails()
        {
            const string json = """{ "MaxLogGenerations": 0, "TextDiffParallelThresholdKilobytes": 0, "TextDiffChunkSizeKilobytes": 0 }""";

            await WithConfigFileAsync(json, async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigAsync());
                Assert.Contains(ConfigService.ERROR_CONFIG_VALIDATION_PREFIX, ex.Message, StringComparison.Ordinal);
                Assert.Contains("MaxLogGenerations", ex.Message, StringComparison.Ordinal);
                Assert.Contains("TextDiffParallelThresholdKilobytes", ex.Message, StringComparison.Ordinal);
                Assert.Contains("TextDiffChunkSizeKilobytes", ex.Message, StringComparison.Ordinal);
            });
        }

        [Fact]
        public async Task LoadConfigAsync_ValidCustomSettings_ReturnsSettings()
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
                var config = await service.LoadConfigAsync();

                Assert.Equal(3, config.MaxLogGenerations);
                Assert.Equal(256, config.TextDiffParallelThresholdKilobytes);
                Assert.Equal(32, config.TextDiffChunkSizeKilobytes);
            });
        }

        // ── configFilePath parameter tests / configFilePath パラメータテスト ──

        [Fact]
        public async Task LoadConfigAsync_CustomConfigFilePath_LoadsFromSpecifiedPath()
        {
            var customConfigPath = Path.Combine(Path.GetTempPath(), $"test-custom-{Guid.NewGuid():N}.json");
            const string json = """{ "MaxLogGenerations": 99 }""";
            await File.WriteAllTextAsync(customConfigPath, json);

            try
            {
                var service = new ConfigService();
                var config = await service.LoadConfigAsync(customConfigPath);

                Assert.NotNull(config);
                Assert.Equal(99, config.MaxLogGenerations);
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
        public async Task LoadConfigAsync_CustomConfigFilePathMissing_ThrowsFileNotFoundException()
        {
            var service = new ConfigService();

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => service.LoadConfigAsync("/nonexistent/path/to/config.json"));
        }

        [Fact]
        public async Task LoadConfigAsync_NullConfigFilePath_FallsBackToDefaultPath()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                var service = new ConfigService();
                var config = await service.LoadConfigAsync(null);

                Assert.NotNull(config);
            });
        }

        [Fact]
        public async Task LoadConfigAsync_EmptyConfigFilePath_FallsBackToDefaultPath()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                var service = new ConfigService();
                var config = await service.LoadConfigAsync(string.Empty);

                Assert.NotNull(config);
            });
        }

        // ── Environment variable override tests / 環境変数オーバーライドテスト ──

        [Fact]
        public async Task LoadConfigAsync_EnvVarOverridesIntProperty_AppliesOverride()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_MAXPARALLELISM", "8") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var config = await service.LoadConfigAsync();

                        Assert.Equal(8, config.MaxParallelism);
                    });
            });
        }

        [Fact]
        public async Task LoadConfigAsync_EnvVarOverridesBoolProperty_TrueValue_AppliesOverride()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_ENABLEILCACHE", "false") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var config = await service.LoadConfigAsync();

                        Assert.False(config.EnableILCache);
                    });
            });
        }

        [Fact]
        public async Task LoadConfigAsync_EnvVarOverridesBoolProperty_OneZero_AppliesOverride()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_SHOULDGENERATEHTMLREPORT", "0") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var config = await service.LoadConfigAsync();

                        Assert.False(config.ShouldGenerateHtmlReport);
                    });
            });
        }

        [Fact]
        public async Task LoadConfigAsync_EnvVarOverridesStringProperty_AppliesOverride()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_ILCACHEDIRECTORYABSOLUTEPATH", "/tmp/custom-il-cache") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var config = await service.LoadConfigAsync();

                        Assert.Equal("/tmp/custom-il-cache", config.ILCacheDirectoryAbsolutePath);
                    });
            });
        }

        [Fact]
        public async Task LoadConfigAsync_EnvVarOverridesJsonValue_EnvVarWins()
        {
            const string json = """{ "MaxParallelism": 4 }""";

            await WithConfigFileAsync(json, async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_MAXPARALLELISM", "16") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var config = await service.LoadConfigAsync();

                        Assert.Equal(16, config.MaxParallelism);
                    });
            });
        }

        [Fact]
        public async Task LoadConfigAsync_EnvVarWithInvalidIntValue_IsIgnored()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_MAXPARALLELISM", "not-a-number") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var config = await service.LoadConfigAsync();

                        Assert.Equal(0, config.MaxParallelism);  // default
                    });
            });
        }

        [Fact]
        public async Task LoadConfigAsync_EnvVarWithInvalidBoolValue_IsIgnored()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_ENABLEILCACHE", "yes") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var config = await service.LoadConfigAsync();

                        Assert.True(config.EnableILCache);  // default unchanged
                    });
            });
        }

        [Fact]
        public async Task LoadConfigAsync_EnvVarOverridesInvalidValue_ValidationStillRuns()
        {
            // Env var sets an invalid value (MaxLogGenerations=0), triggering validation failure
            // 環境変数が不正値（MaxLogGenerations=0）を設定し、バリデーション失敗を引き起こす
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_MAXLOGGENERATIONS", "0") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigAsync());
                        Assert.Contains("MaxLogGenerations", ex.Message, StringComparison.Ordinal);
                    });
            });
        }

        [Fact]
        public void ApplyEnvironmentVariableOverrides_CaseInsensitiveBool_TrueVariants()
        {
            foreach (var trueVal in new[] { "true", "TRUE", "True", "1" })
            {
                var config = new ConfigSettings { ShouldGenerateHtmlReport = false };
                WithEnvVar("FOLDERDIFF_SHOULDGENERATEHTMLREPORT", trueVal,
                    () => ConfigService.ApplyEnvironmentVariableOverrides(config));
                Assert.True(config.ShouldGenerateHtmlReport, $"Expected true for value '{trueVal}'");
            }
        }

        [Fact]
        public void ApplyEnvironmentVariableOverrides_CaseInsensitiveBool_FalseVariants()
        {
            foreach (var falseVal in new[] { "false", "FALSE", "False", "0" })
            {
                var config = new ConfigSettings { ShouldGenerateHtmlReport = true };
                WithEnvVar("FOLDERDIFF_SHOULDGENERATEHTMLREPORT", falseVal,
                    () => ConfigService.ApplyEnvironmentVariableOverrides(config));
                Assert.False(config.ShouldGenerateHtmlReport, $"Expected false for value '{falseVal}'");
            }
        }

        [Fact]
        public void ApplyEnvironmentVariableOverrides_MultipleVars_AllApplied()
        {
            var config = new ConfigSettings();
            WithEnvVars(
                new[] {
                    ("FOLDERDIFF_MAXPARALLELISM", "12"),
                    ("FOLDERDIFF_SKIPIL", "true"),
                    ("FOLDERDIFF_SHOULDGENERATEHTMLREPORT", "false"),
                    ("FOLDERDIFF_ILCACHEDIRECTORYABSOLUTEPATH", "/ci/cache"),
                },
                () => ConfigService.ApplyEnvironmentVariableOverrides(config));

            Assert.Equal(12, config.MaxParallelism);
            Assert.True(config.SkipIL);
            Assert.False(config.ShouldGenerateHtmlReport);
            Assert.Equal("/ci/cache", config.ILCacheDirectoryAbsolutePath);
        }

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
