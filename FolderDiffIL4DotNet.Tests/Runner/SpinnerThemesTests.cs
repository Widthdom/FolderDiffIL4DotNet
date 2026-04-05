using System.Linq;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Runner;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Runner
{
    /// <summary>
    /// Unit tests for <see cref="SpinnerThemes"/>.
    /// <see cref="SpinnerThemes"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class SpinnerThemesTests
    {
        private static CliOptions DefaultOpts() =>
            new(ShowHelp: false, ShowVersion: false, ShowBanner: false, NoPause: false,
                ConfigPath: null, ThreadsOverride: null, NoIlCache: false, ClearCache: false,
                SkipIL: false, NoTimestampWarnings: false, PrintConfig: false, ValidateConfig: false,
                DryRun: false, Coffee: false, Beer: false, Matcha: false, Whisky: false,
                Wine: false, Ramen: false, Sushi: false, Bell: false, Wizard: false,
                RandomSpinner: false, MultipleSpinnersDetected: false,
                LogFormatOverride: null, OutputDirectory: null, ParseError: null);

        [Fact]
        public void MultipleSpinnersMessage_HasExpectedContent()
        {
            Assert.Contains("matcha", SpinnerThemes.MULTIPLE_SPINNERS_MESSAGE);
        }

        [Fact]
        public void Apply_CoffeeFlag_SetsFramesWithCoffeeEmoji()
        {
            var builder = new ConfigSettingsBuilder();
            var opts = DefaultOpts() with { Coffee = true };

            SpinnerThemes.Apply(builder, opts);

            Assert.True(builder.SpinnerFrames.All(f => f.Contains("☕")));
        }

        [Fact]
        public void Apply_BeerFlag_SetsFramesWithBeerEmoji()
        {
            var builder = new ConfigSettingsBuilder();
            var opts = DefaultOpts() with { Beer = true };

            SpinnerThemes.Apply(builder, opts);

            Assert.True(builder.SpinnerFrames.All(f => f.Contains("🍺")));
        }

        [Fact]
        public void Apply_MatchaFlag_SetsFramesWithMatchaEmoji()
        {
            var builder = new ConfigSettingsBuilder();
            var opts = DefaultOpts() with { Matcha = true };

            SpinnerThemes.Apply(builder, opts);

            Assert.True(builder.SpinnerFrames.All(f => f.Contains("🍵")));
        }

        [Fact]
        public void Apply_WhiskyFlag_SetsFramesWithWhiskyEmoji()
        {
            var builder = new ConfigSettingsBuilder();
            var opts = DefaultOpts() with { Whisky = true };

            SpinnerThemes.Apply(builder, opts);

            Assert.True(builder.SpinnerFrames.All(f => f.Contains("🥃")));
        }

        [Fact]
        public void Apply_WineFlag_SetsFramesWithWineEmoji()
        {
            var builder = new ConfigSettingsBuilder();
            var opts = DefaultOpts() with { Wine = true };

            SpinnerThemes.Apply(builder, opts);

            Assert.True(builder.SpinnerFrames.All(f => f.Contains("🍷")));
        }

        [Fact]
        public void Apply_RamenFlag_SetsFramesWithRamenEmoji()
        {
            var builder = new ConfigSettingsBuilder();
            var opts = DefaultOpts() with { Ramen = true };

            SpinnerThemes.Apply(builder, opts);

            Assert.True(builder.SpinnerFrames.All(f => f.Contains("🍜")));
        }

        [Fact]
        public void Apply_SushiFlag_SetsFramesWithSushiEmoji()
        {
            var builder = new ConfigSettingsBuilder();
            var opts = DefaultOpts() with { Sushi = true };

            SpinnerThemes.Apply(builder, opts);

            Assert.True(builder.SpinnerFrames.All(f => f.Contains("🍣")));
        }

        [Fact]
        public void Apply_MultipleSpinnersDetected_AppliesMatcha()
        {
            var builder = new ConfigSettingsBuilder();
            var opts = DefaultOpts() with { MultipleSpinnersDetected = true };

            SpinnerThemes.Apply(builder, opts);

            Assert.True(builder.SpinnerFrames.All(f => f.Contains("🍵")));
        }

        [Fact]
        public void Apply_RandomSpinner_SetsNonDefaultFrames()
        {
            var builder = new ConfigSettingsBuilder();
            var defaultFrames = builder.SpinnerFrames.ToList();
            var opts = DefaultOpts() with { RandomSpinner = true };

            SpinnerThemes.Apply(builder, opts);

            // Random should have picked one of the 7 themes, all of which differ from defaults
            // ランダムは7テーマのいずれかを選択し、すべてデフォルトと異なる
            Assert.NotEqual(defaultFrames, builder.SpinnerFrames);
        }

        [Fact]
        public void Apply_NoFlags_DoesNotChangeFrames()
        {
            var builder = new ConfigSettingsBuilder();
            var originalFrames = builder.SpinnerFrames.ToList();

            SpinnerThemes.Apply(builder, DefaultOpts());

            Assert.Equal(originalFrames, builder.SpinnerFrames);
        }

        [Fact]
        public void ApplyMatcha_SetsExpectedFrameCount()
        {
            var builder = new ConfigSettingsBuilder();

            SpinnerThemes.ApplyMatcha(builder);

            Assert.Equal(13, builder.SpinnerFrames.Count);
            Assert.Contains("Douzo", builder.SpinnerFrames[^1]);
        }
    }
}
