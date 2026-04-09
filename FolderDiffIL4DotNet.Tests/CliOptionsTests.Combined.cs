// CliOptionsTests.Combined.cs — Combined flags, spinner last-wins, and log-format tests (partial 2/2)
// CliOptionsTests.Combined.cs — 複合フラグ、スピナー最後勝ち、ログ形式テスト（パーシャル 2/2）

using FolderDiffIL4DotNet.Runner;
using Xunit;

namespace FolderDiffIL4DotNet.Tests
{
    public sealed partial class CliOptionsTests
    {
        // -----------------------------------------------------------------------
        // Combined flags
        // 複合フラグ
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_AllFlagsCombined_ParsedCorrectly()
        {
            var opts = CliParser.Parse(new[]
            {
                "/old", "/new", "lbl",
                "--no-pause", "--no-il-cache", "--skip-il",
                "--no-timestamp-warnings", "--creator-il-ignore-profile", "buildserver-winforms", "--print-config", "--dry-run",
                "--coffee", "--bell", "--wizard",
                "--log-format", "json",
            });

            Assert.True(opts.NoPause);
            Assert.True(opts.NoIlCache);
            Assert.True(opts.SkipIL);
            Assert.True(opts.NoTimestampWarnings);
            Assert.Equal("buildserver-winforms", opts.CreatorIlIgnoreProfile);
            Assert.True(opts.PrintConfig);
            Assert.True(opts.DryRun);
            Assert.True(opts.Coffee);
            Assert.True(opts.Bell);
            Assert.True(opts.Wizard);
            Assert.Equal("json", opts.LogFormatOverride);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // Spinner last-wins behavior
        // スピナー最後勝ち動作
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_MultipleSpinnerFlags_LastOneWins()
        {
            // Order: --coffee --beer --matcha --whisky --wine --ramen --sushi
            // Last flag (--sushi) should win; all others should be false.
            // 最後のフラグ (--sushi) が勝ち、他はすべて false。
            var opts = CliParser.Parse(new[]
            {
                "--coffee", "--beer", "--matcha", "--whisky", "--wine", "--ramen", "--sushi",
            });

            Assert.False(opts.Coffee);
            Assert.False(opts.Beer);
            Assert.False(opts.Matcha);
            Assert.False(opts.Whisky);
            Assert.False(opts.Wine);
            Assert.False(opts.Ramen);
            Assert.True(opts.Sushi);
            Assert.Null(opts.ParseError);
        }

        [Fact]
        public void ParseCliOptions_CoffeeAfterWine_CoffeeWins()
        {
            var opts = CliParser.Parse(new[] { "--wine", "--coffee" });

            Assert.True(opts.Coffee);
            Assert.False(opts.Wine);
            Assert.False(opts.Beer);
            Assert.False(opts.Matcha);
            Assert.False(opts.Whisky);
            Assert.False(opts.Ramen);
            Assert.False(opts.Sushi);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --log-format
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_LogFormatJson_SetsLogFormatOverride()
        {
            var opts = CliParser.Parse(new[] { "--log-format", "json" });

            Assert.Equal("json", opts.LogFormatOverride);
            Assert.Null(opts.ParseError);
        }

        [Fact]
        public void ParseCliOptions_LogFormatText_SetsLogFormatOverride()
        {
            var opts = CliParser.Parse(new[] { "--log-format", "text" });

            Assert.Equal("text", opts.LogFormatOverride);
            Assert.Null(opts.ParseError);
        }

        [Fact]
        public void ParseCliOptions_LogFormatJson_CaseInsensitive()
        {
            var opts = CliParser.Parse(new[] { "--log-format", "JSON" });

            Assert.Equal("json", opts.LogFormatOverride);
            Assert.Null(opts.ParseError);
        }

        [Fact]
        public void ParseCliOptions_LogFormatInvalid_SetsParseError()
        {
            var opts = CliParser.Parse(new[] { "--log-format", "xml" });

            Assert.NotNull(opts.ParseError);
            Assert.Contains("--log-format", opts.ParseError, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ParseCliOptions_LogFormatMissingValue_SetsParseError()
        {
            var opts = CliParser.Parse(new[] { "--log-format" });

            Assert.NotNull(opts.ParseError);
            Assert.Contains("--log-format", opts.ParseError, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ParseCliOptions_NoLogFormat_DefaultsToNull()
        {
            var opts = CliParser.Parse(new[] { "--no-pause" });

            Assert.Null(opts.LogFormatOverride);
        }

        // -----------------------------------------------------------------------
        // --output
        // -----------------------------------------------------------------------

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseCliOptions_OutputWithPath_SetsOutputDirectory()
        {
            var opts = CliParser.Parse(new[] { "--output", "/custom/reports" });

            Assert.Equal("/custom/reports", opts.OutputDirectory);
            Assert.Null(opts.ParseError);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseCliOptions_OutputMissingValue_SetsParseError()
        {
            var opts = CliParser.Parse(new[] { "--output" });

            Assert.NotNull(opts.ParseError);
            Assert.Contains("--output", opts.ParseError, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseCliOptions_NoOutput_DefaultsToNull()
        {
            var opts = CliParser.Parse(new[] { "--no-pause" });

            Assert.Null(opts.OutputDirectory);
        }

        // -----------------------------------------------------------------------
        // --random-spinner
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_RandomSpinnerFlag_SetsRandomSpinner()
        {
            var opts = CliParser.Parse(new[] { "--random-spinner" });

            Assert.True(opts.RandomSpinner);
            Assert.Null(opts.ParseError);
        }

        [Fact]
        public void ParseCliOptions_NoRandomSpinnerFlag_DefaultsToFalse()
        {
            var opts = CliParser.Parse(new[] { "--no-pause" });

            Assert.False(opts.RandomSpinner);
        }

        // -----------------------------------------------------------------------
        // MultipleSpinnersDetected
        // 複数スピナー検出
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_SingleSpinnerFlag_MultipleSpinnersNotDetected()
        {
            var opts = CliParser.Parse(new[] { "--coffee" });

            Assert.False(opts.MultipleSpinnersDetected);
        }

        [Fact]
        public void ParseCliOptions_TwoSpinnerFlags_MultipleSpinnersDetected()
        {
            var opts = CliParser.Parse(new[] { "--coffee", "--beer" });

            Assert.True(opts.MultipleSpinnersDetected);
            // Last-wins still applies for the actual theme flag / 実際のテーマフラグには最後勝ちが適用される
            Assert.True(opts.Beer);
            Assert.False(opts.Coffee);
        }

        [Fact]
        public void ParseCliOptions_ThreeSpinnerFlags_MultipleSpinnersDetected()
        {
            var opts = CliParser.Parse(new[] { "--coffee", "--beer", "--matcha" });

            Assert.True(opts.MultipleSpinnersDetected);
            Assert.True(opts.Matcha);
        }
    }
}
