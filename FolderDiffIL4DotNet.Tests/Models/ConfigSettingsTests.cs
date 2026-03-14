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
            Assert.Equal(512, config.TextDiffParallelThresholdKilobytes);
            Assert.Equal(64, config.TextDiffChunkSizeKilobytes);
            Assert.True(config.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
            Assert.False(config.ShouldIgnoreILLinesContainingConfiguredStrings);
            Assert.NotNull(config.ILIgnoreLineContainingStrings);
            Assert.Empty(config.ILIgnoreLineContainingStrings);
        }

        [Fact]
        public void JsonDeserialize_MissingDiskCacheLimits_UsesDefaults()
        {
            var config = JsonSerializer.Deserialize<ConfigSettings>("{}");
            Assert.NotNull(config);
            Assert.Equal(1000, config.ILCacheMaxDiskFileCount);
            Assert.Equal(512, config.ILCacheMaxDiskMegabytes);
            Assert.Equal(512, config.TextDiffParallelThresholdKilobytes);
            Assert.Equal(64, config.TextDiffChunkSizeKilobytes);
            Assert.True(config.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
            Assert.False(config.ShouldIgnoreILLinesContainingConfiguredStrings);
            Assert.NotNull(config.ILIgnoreLineContainingStrings);
            Assert.Empty(config.ILIgnoreLineContainingStrings);
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

        [Fact]
        public void JsonDeserialize_TextDiffParallelSettings_AreApplied()
        {
            var json = "{\"TextDiffParallelThresholdKilobytes\":128,\"TextDiffChunkSizeKilobytes\":8}";
            var config = JsonSerializer.Deserialize<ConfigSettings>(json);
            Assert.NotNull(config);
            Assert.Equal(128, config.TextDiffParallelThresholdKilobytes);
            Assert.Equal(8, config.TextDiffChunkSizeKilobytes);
        }

        [Fact]
        public void JsonDeserialize_IlIgnoreContainsSettings_AreApplied()
        {
            var json = "{\"ShouldIgnoreILLinesContainingConfiguredStrings\":true,\"ILIgnoreLineContainingStrings\":[\"buildserver\",\"path\"]}";
            var config = JsonSerializer.Deserialize<ConfigSettings>(json);
            Assert.NotNull(config);
            Assert.True(config.ShouldIgnoreILLinesContainingConfiguredStrings);
            Assert.Equal(new[] { "buildserver", "path" }, config.ILIgnoreLineContainingStrings);
        }

        [Fact]
        public void JsonDeserialize_TimestampWarningSetting_CanBeDisabled()
        {
            var json = "{\"ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp\":false}";
            var config = JsonSerializer.Deserialize<ConfigSettings>(json);
            Assert.NotNull(config);
            Assert.False(config.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
        }
    }
}
