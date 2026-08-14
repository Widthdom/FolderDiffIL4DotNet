using System;
using System.Collections.Generic;
using System.Linq;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Services;
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
        private static CliOptions DefaultOpts() => new();

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
        public void Apply_CreatorIlIgnoreProfile_PrependsProfileBeforeConfiguredNormalizationStrings()
        {
            var builder = new ConfigSettingsBuilder
            {
                ShouldIgnoreILLinesContainingConfiguredStrings = false,
                ILIgnoreLineContainingStrings = new List<string> { "existing-filter" },
                ILNormalizeContainingStrings = new List<string> { "existing-normalization" }
            };
            var opts = DefaultOpts() with { CreatorIlIgnoreProfile = "creator-default" };

            CliOverrideApplier.Apply(builder, opts);

            Assert.False(builder.ShouldIgnoreILLinesContainingConfiguredStrings);
            Assert.True(builder.ShouldILNormalizeContainingConfiguredStrings);
            Assert.Contains("existing-filter", builder.ILIgnoreLineContainingStrings);
            Assert.Equal(
                new[]
                {
                    "buildserver1_",
                    "buildserver2_",
                    @"A:\temp\develop\",
                    @"B:\temp\develop\",
                    @"C:\temp\develop\",
                    @"D:\temp\develop\",
                    @"E:\temp\develop\",
                    @"F:\temp\develop\",
                    @"G:\temp\develop\",
                    @"H:\temp\develop\",
                    @"I:\temp\develop\",
                    @"J:\temp\develop\",
                    @"K:\temp\develop\",
                    @"L:\temp\develop\",
                    @"M:\temp\develop\",
                    @"N:\temp\develop\",
                    @"O:\temp\develop\",
                    @"P:\temp\develop\",
                    @"Q:\temp\develop\",
                    @"R:\temp\develop\",
                    @"S:\temp\develop\",
                    @"T:\temp\develop\",
                    @"U:\temp\develop\",
                    @"V:\temp\develop\",
                    @"W:\temp\develop\",
                    @"X:\temp\develop\",
                    @"Y:\temp\develop\",
                    @"Z:\temp\develop\",
                    "existing-normalization"
                },
                builder.ILNormalizeContainingStrings);
            Assert.DoesNotContain("// Method begins at Relative Virtual Address (RVA) 0x", builder.ILNormalizeContainingStrings);
            Assert.DoesNotContain("// Code size ", builder.ILNormalizeContainingStrings);
            Assert.DoesNotContain("TypeLibraryTimeStampAttribute", builder.ILNormalizeContainingStrings);
            Assert.DoesNotContain(".publickeytoken = ( ", builder.ILIgnoreLineContainingStrings);
        }

        [Fact]
        public void Apply_CreatorFlag_DoesNotSuppressPublicKeyTokenDifferences()
        {
            var builder = new ConfigSettingsBuilder();
            var opts = DefaultOpts() with { Creator = true };

            CliOverrideApplier.Apply(builder, opts);

            var oldLines = new[]
            {
                ".assembly extern Vendor.Library",
                "{",
                "  .publickeytoken = ( 12 34 56 78 90 AB CD EF )",
                "}"
            };
            var newLines = new[]
            {
                ".assembly extern Vendor.Library",
                "{",
                "  .publickeytoken = ( FE DC BA 09 87 65 43 21 )",
                "}"
            };

            var areEqual = ILOutputService.StreamingFilteredSequenceEqual(
                oldLines,
                newLines,
                builder.ShouldIgnoreILLinesContainingConfiguredStrings,
                builder.ILIgnoreLineContainingStrings,
                builder.ILNormalizeContainingStrings);

            Assert.False(areEqual);
        }

        [Fact]
        public void Apply_CreatorFlag_UsesDefaultProfile()
        {
            var builder = new ConfigSettingsBuilder();
            var opts = DefaultOpts() with { Creator = true };

            CliOverrideApplier.Apply(builder, opts);

            Assert.True(builder.ShouldILNormalizeContainingConfiguredStrings);
            Assert.Contains("buildserver1_", builder.ILNormalizeContainingStrings);
            Assert.Contains(@"A:\temp\develop\", builder.ILNormalizeContainingStrings);
            Assert.Contains(@"Z:\temp\develop\", builder.ILNormalizeContainingStrings);
            Assert.Equal(28, builder.ILNormalizeContainingStrings.Count);
        }

        [Fact]
        public void Apply_CreatorFlag_PreservesDuplicateForValidationBeforeEffectiveDeduplication()
        {
            var builder = new ConfigSettingsBuilder
            {
                ILNormalizeContainingStrings = new List<string> { "buildserver1_" }
            };

            CliOverrideApplier.Apply(builder, DefaultOpts() with { Creator = true });

            Assert.Equal(2, builder.ILNormalizeContainingStrings.Count(value => value == "buildserver1_"));
            Assert.Contains(
                ILOutputService.ValidateILNormalizeContainingStrings(builder.ILNormalizeContainingStrings),
                warning => warning.Contains("configured more than once", StringComparison.Ordinal));
        }

        [Fact]
        public void Apply_CreatorFlag_ProfileValuesCountTowardRuntimeNormalizationLimit()
        {
            var builder = new ConfigSettingsBuilder
            {
                ILNormalizeContainingStrings = Enumerable.Range(
                        0,
                        ConfigSettings.MaxILNormalizeContainingStringsCount)
                    .Select(index => $"normalization-{index}")
                    .ToList()
            };

            CliOverrideApplier.Apply(builder, DefaultOpts() with { Creator = true });

            ConfigValidationResult validation = builder.Validate();
            Assert.False(validation.IsValid);
            Assert.Contains(
                validation.Errors,
                error => error.Contains("Creator-profile values", StringComparison.Ordinal)
                    && error.Contains(
                        $"at most {ConfigSettings.MaxILNormalizeContainingStringsCount} values",
                        StringComparison.Ordinal));
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
