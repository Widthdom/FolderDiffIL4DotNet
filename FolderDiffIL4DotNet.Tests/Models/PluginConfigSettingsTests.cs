using System.Collections.Generic;
using System.Text.Json;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Models
{
    /// <summary>
    /// Tests for plugin-related settings in <see cref="ConfigSettings"/> and <see cref="ConfigSettingsBuilder"/>.
    /// <see cref="ConfigSettings"/> と <see cref="ConfigSettingsBuilder"/> のプラグイン関連設定のテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class PluginConfigSettingsTests
    {
        [Fact]
        public void DefaultPluginSearchPaths_IsEmpty()
        {
            var builder = new ConfigSettingsBuilder();
            Assert.NotNull(builder.PluginSearchPaths);
            Assert.Empty(builder.PluginSearchPaths);
        }

        [Fact]
        public void DefaultPluginEnabledIds_IsEmpty()
        {
            var builder = new ConfigSettingsBuilder();
            Assert.Empty(builder.PluginEnabledIds);
        }

        [Fact]
        public void DefaultPluginConfig_IsEmpty()
        {
            var builder = new ConfigSettingsBuilder();
            Assert.Empty(builder.PluginConfig);
        }

        [Fact]
        public void Build_PluginSearchPaths_ArePreserved()
        {
            // Arrange / 準備
            var builder = new ConfigSettingsBuilder
            {
                PluginSearchPaths = new List<string> { "/path/to/plugins", "/other/path" }
            };

            // Act / 実行
            var config = builder.Build();

            // Assert / 検証
            Assert.Equal(2, config.PluginSearchPaths.Count);
            Assert.Equal("/path/to/plugins", config.PluginSearchPaths[0]);
            Assert.Equal("/other/path", config.PluginSearchPaths[1]);
        }

        [Fact]
        public void Build_PluginEnabledIds_ArePreserved()
        {
            var builder = new ConfigSettingsBuilder
            {
                PluginEnabledIds = new List<string> { "plugin-a", "plugin-b" }
            };

            var config = builder.Build();

            Assert.Equal(2, config.PluginEnabledIds.Count);
            Assert.Contains("plugin-a", config.PluginEnabledIds);
            Assert.Contains("plugin-b", config.PluginEnabledIds);
        }

        [Fact]
        public void Build_PluginConfig_IsPreserved()
        {
            // Arrange / 準備
            var json = JsonSerializer.Deserialize<JsonElement>("42");
            var builder = new ConfigSettingsBuilder
            {
                PluginConfig = new Dictionary<string, JsonElement>
                {
                    ["my-plugin"] = json
                }
            };

            // Act / 実行
            var config = builder.Build();

            // Assert / 検証
            Assert.Single(config.PluginConfig);
            Assert.True(config.PluginConfig.ContainsKey("my-plugin"));
            Assert.Equal(42, config.PluginConfig["my-plugin"].GetInt32());
        }

        [Fact]
        public void Build_PluginSearchPaths_AreReadOnly()
        {
            var builder = new ConfigSettingsBuilder
            {
                PluginSearchPaths = new List<string> { "/path" }
            };
            var config = builder.Build();

            // Modifying the builder after Build should not affect the config
            // Build 後にビルダーを変更しても config には影響しない
            builder.PluginSearchPaths.Add("/new/path");
            Assert.Single(config.PluginSearchPaths);
        }

        [Fact]
        public void DefaultPluginStrictMode_IsFalse()
        {
            var builder = new ConfigSettingsBuilder();
            Assert.False(builder.PluginStrictMode);
        }

        [Fact]
        public void DefaultPluginTrustedHashes_IsEmpty()
        {
            var builder = new ConfigSettingsBuilder();
            Assert.Empty(builder.PluginTrustedHashes);
        }

        [Fact]
        public void Build_PluginStrictMode_IsPreserved()
        {
            var builder = new ConfigSettingsBuilder { PluginStrictMode = true };
            var config = builder.Build();
            Assert.True(config.PluginStrictMode);
        }

        [Fact]
        public void Build_PluginTrustedHashes_ArePreserved()
        {
            // Arrange / 準備
            var builder = new ConfigSettingsBuilder
            {
                PluginTrustedHashes = new Dictionary<string, string>
                {
                    ["my-plugin"] = "ABC123DEF456"
                }
            };

            // Act / 実行
            var config = builder.Build();

            // Assert / 検証
            Assert.Single(config.PluginTrustedHashes);
            Assert.Equal("ABC123DEF456", config.PluginTrustedHashes["my-plugin"]);
        }

        [Fact]
        public void PluginSettings_RoundTripThroughJson()
        {
            // Arrange / 準備
            var original = new ConfigSettingsBuilder
            {
                PluginSearchPaths = new List<string> { "/plugins" },
                PluginEnabledIds = new List<string> { "id1" },
                PluginConfig = new Dictionary<string, JsonElement>
                {
                    ["id1"] = JsonSerializer.Deserialize<JsonElement>("{\"key\":\"value\"}")
                }
            };

            // Act: serialize and deserialize / シリアライズ/デシリアライズ
            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<ConfigSettingsBuilder>(json)!;

            // Assert / 検証
            Assert.Equal(original.PluginSearchPaths, deserialized.PluginSearchPaths);
            Assert.Equal(original.PluginEnabledIds, deserialized.PluginEnabledIds);
            Assert.Equal(
                original.PluginConfig["id1"].GetProperty("key").GetString(),
                deserialized.PluginConfig["id1"].GetProperty("key").GetString());
        }
    }
}
