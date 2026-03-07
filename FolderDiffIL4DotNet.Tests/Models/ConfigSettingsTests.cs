using System.Text.Json;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Models
{
    public sealed class ConfigSettingsTests
    {
        [Fact]
        public void Constructor_DefaultDiskCacheLimits_Are1000And512()
        {
            var config = new ConfigSettings();

            Assert.Equal(1000, config.ILCacheMaxDiskFileCount);
            Assert.Equal(512, config.ILCacheMaxDiskMegabytes);
        }

        [Fact]
        public void JsonDeserialize_MissingDiskCacheLimits_UsesDefaults()
        {
            var config = JsonSerializer.Deserialize<ConfigSettings>("{}");
            Assert.NotNull(config);
            Assert.Equal(1000, config.ILCacheMaxDiskFileCount);
            Assert.Equal(512, config.ILCacheMaxDiskMegabytes);
        }

        [Fact]
        public void JsonDeserialize_ExplicitZeroDiskCacheLimits_KeepsZero()
        {
            var json = "{\"ILCacheMaxDiskFileCount\":0,\"ILCacheMaxDiskMegabytes\":0}";
            var config = JsonSerializer.Deserialize<ConfigSettings>(json);
            Assert.NotNull(config);
            Assert.Equal(0, config.ILCacheMaxDiskFileCount);
            Assert.Equal(0, config.ILCacheMaxDiskMegabytes);
        }
    }
}
