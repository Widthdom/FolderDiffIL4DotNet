using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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
                Assert.Equal(60, config.ILCacheStatsLogIntervalSeconds);
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
