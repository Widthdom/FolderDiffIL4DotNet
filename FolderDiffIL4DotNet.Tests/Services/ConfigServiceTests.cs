using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public partial class ConfigServiceTests
    {
        [Fact]
        public async Task LoadConfigBuilderAsync_ExplicitConfigFileMissing_ThrowsFileNotFoundException()
        {
            var service = new ConfigService();
            string missingConfigPath = Path.Combine(Path.GetTempPath(), "fd-config-missing-" + Guid.NewGuid().ToString("N"), "missing.json");

            var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => service.LoadConfigBuilderAsync(missingConfigPath));
            Assert.Contains(Path.GetFullPath(missingConfigPath), ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_DefaultUserConfigMissing_FallsBackToBundledConfig()
        {
            using var appDataScope = CreateAppDataOverrideScope();
            var service = new ConfigService();

            var builder = await service.LoadConfigBuilderAsync();

            Assert.NotNull(builder);
            Assert.Equal(5, builder.MaxLogGenerations);
            Assert.True(builder.ShouldIncludeUnchangedFiles);
        }

        [Fact]
        public void GetLocalApplicationDataRootAbsolutePath_WhenOverrideIsEmpty_ThrowsInvalidOperationException()
        {
            using var appDataScope = CreateAppDataOverrideScope();
            object? originalOverride = AppContext.GetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY);

            try
            {
                AppContext.SetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, string.Empty);

                var ex = Assert.Throws<InvalidOperationException>(() => AppDataPaths.GetLocalApplicationDataRootAbsolutePath());
                Assert.Contains("LocalApplicationData", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                AppContext.SetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, originalOverride);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public void ResolveConfigFileAbsolutePath_WhenLocalApplicationDataOverrideIsEmpty_ThrowsInvalidOperationException()
        {
            using var appDataScope = CreateAppDataOverrideScope();
            object? originalOverride = AppContext.GetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY);

            try
            {
                AppContext.SetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, string.Empty);

                var ex = Assert.Throws<InvalidOperationException>(() => ConfigService.ResolveConfigFileAbsolutePath());
                Assert.Contains("LocalApplicationData", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                AppContext.SetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, originalOverride);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_WhenUserConfigJsonIsInvalid_AttachesResolvedUserConfigPath()
        {
            using var appDataScope = CreateAppDataOverrideScope();
            Directory.CreateDirectory(Path.GetDirectoryName(appDataScope.UserConfigFileAbsolutePath)!);
            await File.WriteAllTextAsync(appDataScope.UserConfigFileAbsolutePath, "{ invalid-json");
            var service = new ConfigService();

            try
            {
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigBuilderAsync());
                Assert.Equal(
                    Path.GetFullPath(appDataScope.UserConfigFileAbsolutePath),
                    ConfigService.TryGetResolvedConfigFileAbsolutePath(ex));
            }
            finally
            {
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_WhenBundledFallbackJsonIsInvalid_AttachesResolvedBundledConfigPath()
        {
            using var appDataScope = CreateAppDataOverrideScope();
            string bundledRoot = Path.Combine(Path.GetTempPath(), "fd-bundled-config-" + Guid.NewGuid().ToString("N"));
            string bundledConfigPath = Path.Combine(bundledRoot, "config.json");
            Directory.CreateDirectory(bundledRoot);
            await File.WriteAllTextAsync(bundledConfigPath, "{ invalid-json");
            var service = new ConfigService(() => bundledConfigPath);

            try
            {
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigBuilderAsync());
                Assert.Equal(
                    Path.GetFullPath(bundledConfigPath),
                    ConfigService.TryGetResolvedConfigFileAbsolutePath(ex));
            }
            finally
            {
                TryDeleteDirectory(bundledRoot);
                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        [Fact]
        public async Task AppDataOverrideScope_WhenConcurrentScopesOverlap_SerializesAccessAndRestoresOriginalOverride()
        {
            object? originalOverride = AppContext.GetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY);
            string firstRoot = Path.Combine(Path.GetTempPath(), "fd-appdata-scope-first-" + Guid.NewGuid().ToString("N"));
            string secondRoot = Path.Combine(Path.GetTempPath(), "fd-appdata-scope-second-" + Guid.NewGuid().ToString("N"));
            var secondAttemptStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondScopeEntered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseSecondScope = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            AppDataOverrideScope? firstScope = null;
            try
            {
                firstScope = new AppDataOverrideScope(firstRoot);
                Assert.Equal(Path.GetFullPath(firstRoot), AppContext.GetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY));

                Task secondTask = Task.Run(async () =>
                {
                    secondAttemptStarted.SetResult();
                    using var secondScope = new AppDataOverrideScope(secondRoot);
                    secondScopeEntered.SetResult(secondScope.RootAbsolutePath);
                    await releaseSecondScope.Task;
                });

                await secondAttemptStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await Task.Delay(100);
                Assert.False(secondScopeEntered.Task.IsCompleted);

                firstScope.Dispose();
                firstScope = null;

                string enteredRoot = await secondScopeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(Path.GetFullPath(secondRoot), enteredRoot);
                Assert.Equal(Path.GetFullPath(secondRoot), AppContext.GetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY));

                releaseSecondScope.SetResult();
                await secondTask;

                Assert.Equal(originalOverride, AppContext.GetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY));
            }
            finally
            {
                firstScope?.Dispose();
                AppContext.SetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, originalOverride);
                TryDeleteDirectory(firstRoot);
                TryDeleteDirectory(secondRoot);
            }
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
        public async Task LoadConfigBuilderAsync_TrailingCommaInObject_ThrowsInvalidDataExceptionWithHint()
        {
            // Common mistake: trailing comma after the last property in a JSON object
            // よくあるミス: オブジェクトの最後のプロパティ後にカンマを入れてしまう
            await WithConfigFileAsync("{ \"MaxLogGenerations\": 5, }", async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigBuilderAsync());
                Assert.IsType<JsonException>(ex.InnerException);
                Assert.Contains("JSON syntax error", ex.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("trailing", ex.Message, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_TrailingCommaInArray_ThrowsInvalidDataExceptionWithHint()
        {
            // Common mistake: trailing comma after the last element in a JSON array
            // よくあるミス: 配列の最後の要素後にカンマを入れてしまう
            await WithConfigFileAsync("{ \"IgnoredExtensions\": [\".pdb\", \".log\",] }", async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigBuilderAsync());
                Assert.IsType<JsonException>(ex.InnerException);
                Assert.Contains("JSON syntax error", ex.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("trailing", ex.Message, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_TrailingCommaError_MessageIncludesLineNumber()
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
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigBuilderAsync());
                Assert.IsType<JsonException>(ex.InnerException);
                // Message should contain a line number (integer >= 1)
                // メッセージに行番号（1 以上の整数）が含まれている
                Assert.Matches(@"line \d+", ex.Message);
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_TrailingCommaError_MessageIncludesPositionInfo()
        {
            // Verify that position information (column) is included alongside line number.
            // 行番号に加え位置情報（列）もメッセージに含まれることを確認。
            const string json = """
                {
                  "MaxLogGenerations": 5,
                }
                """;
            await WithConfigFileAsync(json, async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigBuilderAsync());
                Assert.IsType<JsonException>(ex.InnerException);
                // Message should contain both line and position numbers
                // メッセージに行番号と位置番号の両方が含まれている
                Assert.Matches(@"line \d+, position \d+", ex.Message);
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_InvalidJson_MessageIncludesTrailingCommaHint()
        {
            // Verify that the trailing-comma hint is appended to every JSON parse error.
            // すべてのJSONパースエラーにトレイリングカンマのヒントが付与されることを確認。
            await WithConfigFileAsync("{ \"Key\": }", async () =>
            {
                var service = new ConfigService();
                var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadConfigBuilderAsync());
                Assert.IsType<JsonException>(ex.InnerException);
                // Hint about trailing commas and comments should be present
                // トレイリングカンマとコメントに関するヒントが存在する
                Assert.Contains("trailing", ex.Message, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_CustomConfigPath_FileNotFound_ThrowsFileNotFoundException()
        {
            // Verify that specifying a non-existent custom config path throws FileNotFoundException.
            // 存在しないカスタム設定パスを指定した場合に FileNotFoundException がスローされることを確認。
            var service = new ConfigService();
            const string customConfigPath = "/non/existent/config.json";
            var ex = await Assert.ThrowsAsync<FileNotFoundException>(
                () => service.LoadConfigBuilderAsync(customConfigPath));
            Assert.Contains(Path.GetFullPath(customConfigPath), ex.Message, StringComparison.Ordinal);
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
            using var appDataScope = CreateAppDataOverrideScope();
            string configFilePath = appDataScope.UserConfigFileAbsolutePath;
            var backupExists = File.Exists(configFilePath);
            var backupContent = backupExists ? await File.ReadAllTextAsync(configFilePath) : null;

            try
            {
                if (deleteConfig)
                {
                    if (File.Exists(configFilePath))
                    {
                        File.Delete(configFilePath);
                    }
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(configFilePath)!);
                    await File.WriteAllTextAsync(configFilePath, content);
                }

                await assertion();
            }
            finally
            {
                if (backupExists)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(configFilePath)!);
                    await File.WriteAllTextAsync(configFilePath, backupContent ?? string.Empty);
                }
                else if (File.Exists(configFilePath))
                {
                    File.Delete(configFilePath);
                }

                TryDeleteDirectory(appDataScope.RootAbsolutePath);
            }
        }

        private static AppDataOverrideScope CreateAppDataOverrideScope()
            => new(Path.Combine(Path.GetTempPath(), "fd-config-appdata-" + Guid.NewGuid().ToString("N")));

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup errors in tests / テストのクリーンアップエラーを無視
            }
        }
    }
}
