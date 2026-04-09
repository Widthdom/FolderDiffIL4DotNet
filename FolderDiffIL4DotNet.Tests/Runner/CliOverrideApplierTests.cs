using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Runner;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Runner
{
    /// <summary>
    /// Unit tests for <see cref="CliOverrideApplier"/>.
    /// <see cref="CliOverrideApplier"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class CliOverrideApplierTests
    {
        private static CliOptions DefaultOpts() =>
            new(ShowHelp: false, ShowVersion: false, ShowBanner: false, NoPause: false,
                ConfigPath: null, ThreadsOverride: null, NoIlCache: false, ClearCache: false,
                SkipIL: false, NoTimestampWarnings: false, CreatorIlIgnoreProfile: null, PrintConfig: false, ValidateConfig: false,
                DryRun: false, Coffee: false, Beer: false, Matcha: false, Whisky: false,
                Wine: false, Ramen: false, Sushi: false, Bell: false, Wizard: false,
                ShowCredits: false, RandomSpinner: false, MultipleSpinnersDetected: false,
                LogFormatOverride: null, OutputDirectory: null,
                OpenReports: false, OpenConfig: false, OpenLogs: false,
                ParseError: null);

        [Fact]
        public void Apply_ThreadsOverride_SetsMaxParallelism()
        {
            var builder = new ConfigSettingsBuilder();
            var opts = DefaultOpts() with { ThreadsOverride = 4 };

            CliOverrideApplier.Apply(builder, opts);

            Assert.Equal(4, builder.MaxParallelism);
        }

        [Fact]
        public void Apply_NoThreadsOverride_DoesNotChangeMaxParallelism()
        {
            var builder = new ConfigSettingsBuilder();
            int original = builder.MaxParallelism;

            CliOverrideApplier.Apply(builder, DefaultOpts());

            Assert.Equal(original, builder.MaxParallelism);
        }

        [Fact]
        public void Apply_NoIlCache_DisablesILCache()
        {
            var builder = new ConfigSettingsBuilder { EnableILCache = true };
            var opts = DefaultOpts() with { NoIlCache = true };

            CliOverrideApplier.Apply(builder, opts);

            Assert.False(builder.EnableILCache);
        }

        [Fact]
        public void Apply_NoIlCacheFalse_DoesNotDisableILCache()
        {
            var builder = new ConfigSettingsBuilder { EnableILCache = true };

            CliOverrideApplier.Apply(builder, DefaultOpts());

            Assert.True(builder.EnableILCache);
        }

        [Fact]
        public void Apply_SkipIL_EnablesSkipIL()
        {
            var builder = new ConfigSettingsBuilder { SkipIL = false };
            var opts = DefaultOpts() with { SkipIL = true };

            CliOverrideApplier.Apply(builder, opts);

            Assert.True(builder.SkipIL);
        }

        [Fact]
        public void Apply_NoTimestampWarnings_DisablesTimestampWarnings()
        {
            var builder = new ConfigSettingsBuilder
            {
                ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = true
            };
            var opts = DefaultOpts() with { NoTimestampWarnings = true };

            CliOverrideApplier.Apply(builder, opts);

            Assert.False(builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
        }

        [Fact]
        public void Apply_DefaultOptions_DoesNotModifyBuilder()
        {
            var builder = new ConfigSettingsBuilder();
            int originalParallelism = builder.MaxParallelism;
            bool originalIlCache = builder.EnableILCache;
            bool originalSkipIl = builder.SkipIL;
            bool originalTimestamp = builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp;

            CliOverrideApplier.Apply(builder, DefaultOpts());

            Assert.Equal(originalParallelism, builder.MaxParallelism);
            Assert.Equal(originalIlCache, builder.EnableILCache);
            Assert.Equal(originalSkipIl, builder.SkipIL);
            Assert.Equal(originalTimestamp, builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
        }

        [Fact]
        public void Apply_CreatorIlIgnoreProfile_EnablesFilteringAndMergesStrings()
        {
            var builder = new ConfigSettingsBuilder
            {
                ShouldIgnoreILLinesContainingConfiguredStrings = false,
                ILIgnoreLineContainingStrings = new System.Collections.Generic.List<string> { "existing-filter" }
            };
            var opts = DefaultOpts() with { CreatorIlIgnoreProfile = "buildserver-winforms" };

            CliOverrideApplier.Apply(builder, opts);

            Assert.True(builder.ShouldIgnoreILLinesContainingConfiguredStrings);
            Assert.Contains("existing-filter", builder.ILIgnoreLineContainingStrings);
            Assert.Contains("buildserver1_", builder.ILIgnoreLineContainingStrings);
            Assert.Contains("// Code size ", builder.ILIgnoreLineContainingStrings);
        }

        [Fact]
        public void Apply_CoffeeFlag_SetsSpinnerFrames()
        {
            var builder = new ConfigSettingsBuilder();
            var opts = DefaultOpts() with { Coffee = true };

            CliOverrideApplier.Apply(builder, opts);

            Assert.Contains("☕", builder.SpinnerFrames[0]);
        }

        [Fact]
        public void Apply_MultipleOverrides_AllApplied()
        {
            var builder = new ConfigSettingsBuilder
            {
                EnableILCache = true,
                SkipIL = false,
                ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = true
            };
            var opts = DefaultOpts() with
            {
                ThreadsOverride = 2,
                NoIlCache = true,
                SkipIL = true,
                NoTimestampWarnings = true
            };

            CliOverrideApplier.Apply(builder, opts);

            Assert.Equal(2, builder.MaxParallelism);
            Assert.False(builder.EnableILCache);
            Assert.True(builder.SkipIL);
            Assert.False(builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
        }
    }
}
