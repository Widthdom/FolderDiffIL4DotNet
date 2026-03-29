using FolderDiffIL4DotNet.Runner;
using Xunit;

namespace FolderDiffIL4DotNet.Tests
{
    public sealed class CliOptionsTests
    {
        // -----------------------------------------------------------------------
        // ParseCliOptions – positional-only (no flags)
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_NullArgs_ReturnsAllDefaults()
        {
            var opts = CliParser.Parse(null);

            Assert.False(opts.ShowHelp);
            Assert.False(opts.ShowVersion);
            Assert.False(opts.ShowBanner);
            Assert.False(opts.NoPause);
            Assert.Null(opts.ConfigPath);
            Assert.Null(opts.ThreadsOverride);
            Assert.False(opts.NoIlCache);
            Assert.False(opts.SkipIL);
            Assert.False(opts.NoTimestampWarnings);
            Assert.False(opts.PrintConfig);
            Assert.False(opts.DryRun);
            Assert.False(opts.Coffee);
            Assert.False(opts.Beer);
            Assert.False(opts.Matcha);
            Assert.False(opts.Whisky);
            Assert.False(opts.Wine);
            Assert.False(opts.Bell);
            Assert.Null(opts.LogFormatOverride);
            Assert.Null(opts.ParseError);
        }

        [Fact]
        public void ParseCliOptions_EmptyArgs_ReturnsAllDefaults()
        {
            var opts = CliParser.Parse(System.Array.Empty<string>());

            Assert.False(opts.ShowHelp);
            Assert.Null(opts.ParseError);
        }

        [Fact]
        public void ParseCliOptions_PositionalArgsOnly_ReturnsAllDefaultFlags()
        {
            var opts = CliParser.Parse(new[] { "/old", "/new", "label" });

            Assert.False(opts.ShowHelp);
            Assert.False(opts.ShowVersion);
            Assert.False(opts.ShowBanner);
            Assert.False(opts.NoPause);
            Assert.Null(opts.ConfigPath);
            Assert.Null(opts.ThreadsOverride);
            Assert.False(opts.NoIlCache);
            Assert.False(opts.SkipIL);
            Assert.False(opts.NoTimestampWarnings);
            Assert.False(opts.PrintConfig);
            Assert.False(opts.DryRun);
            Assert.False(opts.Coffee);
            Assert.False(opts.Beer);
            Assert.False(opts.Matcha);
            Assert.False(opts.Whisky);
            Assert.False(opts.Wine);
            Assert.False(opts.Bell);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --help / -h
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("--HELP")]
        [InlineData("-H")]
        public void ParseCliOptions_HelpFlag_SetsShowHelp(string helpArg)
        {
            var opts = CliParser.Parse(new[] { helpArg });

            Assert.True(opts.ShowHelp);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --version
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData("--version")]
        [InlineData("--VERSION")]
        public void ParseCliOptions_VersionFlag_SetsShowVersion(string arg)
        {
            var opts = CliParser.Parse(new[] { arg });

            Assert.True(opts.ShowVersion);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --banner
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData("--banner")]
        [InlineData("--BANNER")]
        public void ParseCliOptions_BannerFlag_SetsShowBanner(string arg)
        {
            var opts = CliParser.Parse(new[] { arg });

            Assert.True(opts.ShowBanner);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --no-pause
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_NoPauseFlag_SetsNoPause()
        {
            var opts = CliParser.Parse(new[] { "--no-pause" });

            Assert.True(opts.NoPause);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --config <path>
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_ConfigWithPath_SetsConfigPath()
        {
            var opts = CliParser.Parse(new[] { "/old", "/new", "lbl", "--config", "/tmp/my-config.json" });

            Assert.Equal("/tmp/my-config.json", opts.ConfigPath);
            Assert.Null(opts.ParseError);
        }

        [Fact]
        public void ParseCliOptions_ConfigWithoutPath_SetsParseError()
        {
            var opts = CliParser.Parse(new[] { "--config" });

            Assert.NotNull(opts.ParseError);
            Assert.Contains("--config", opts.ParseError, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ParseCliOptions_ConfigFollowedByAnotherFlag_SetsParseError()
        {
            var opts = CliParser.Parse(new[] { "--config", "--no-pause" });

            Assert.NotNull(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --threads <N>
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData("0", 0)]
        [InlineData("1", 1)]
        [InlineData("8", 8)]
        [InlineData("64", 64)]
        public void ParseCliOptions_ThreadsWithValidValue_SetsThreadsOverride(string value, int expected)
        {
            var opts = CliParser.Parse(new[] { "--threads", value });

            Assert.Equal(expected, opts.ThreadsOverride);
            Assert.Null(opts.ParseError);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("abc")]
        [InlineData("3.5")]
        public void ParseCliOptions_ThreadsWithInvalidValue_SetsParseError(string value)
        {
            var opts = CliParser.Parse(new[] { "--threads", value });

            Assert.NotNull(opts.ParseError);
            Assert.Contains("--threads", opts.ParseError, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ParseCliOptions_ThreadsWithoutValue_SetsParseError()
        {
            var opts = CliParser.Parse(new[] { "--threads" });

            Assert.NotNull(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --no-il-cache
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_NoIlCacheFlag_SetsNoIlCache()
        {
            var opts = CliParser.Parse(new[] { "--no-il-cache" });

            Assert.True(opts.NoIlCache);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --skip-il
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_SkipILFlag_SetsSkipIL()
        {
            var opts = CliParser.Parse(new[] { "--skip-il" });

            Assert.True(opts.SkipIL);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --no-timestamp-warnings
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_NoTimestampWarningsFlag_SetsNoTimestampWarnings()
        {
            var opts = CliParser.Parse(new[] { "--no-timestamp-warnings" });

            Assert.True(opts.NoTimestampWarnings);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --print-config
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_PrintConfigFlag_SetsPrintConfig()
        {
            var opts = CliParser.Parse(new[] { "--print-config" });

            Assert.True(opts.PrintConfig);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --dry-run
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_DryRunFlag_SetsDryRun()
        {
            var opts = CliParser.Parse(new[] { "--dry-run" });

            Assert.True(opts.DryRun);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --coffee
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_CoffeeFlag_SetsCoffee()
        {
            var opts = CliParser.Parse(new[] { "--coffee" });

            Assert.True(opts.Coffee);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --beer
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_BeerFlag_SetsBeer()
        {
            var opts = CliParser.Parse(new[] { "--beer" });

            Assert.True(opts.Beer);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --matcha
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_MatchaFlag_SetsMatcha()
        {
            var opts = CliParser.Parse(new[] { "--matcha" });

            Assert.True(opts.Matcha);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --whisky
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_WhiskyFlag_SetsWhisky()
        {
            var opts = CliParser.Parse(new[] { "--whisky" });

            Assert.True(opts.Whisky);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --wine
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_WineFlag_SetsWine()
        {
            var opts = CliParser.Parse(new[] { "--wine" });

            Assert.True(opts.Wine);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // --bell
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_BellFlag_SetsBell()
        {
            var opts = CliParser.Parse(new[] { "--bell" });

            Assert.True(opts.Bell);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // Unknown / invalid flags
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData("--unknown-flag")]
        [InlineData("--xyz")]
        public void ParseCliOptions_UnknownFlag_SetsParseError(string flag)
        {
            var opts = CliParser.Parse(new[] { flag });

            Assert.NotNull(opts.ParseError);
            Assert.Contains(flag, opts.ParseError, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ParseCliOptions_UnknownFlagAfterKnownFlag_ParseErrorCapturesFirst()
        {
            var opts = CliParser.Parse(new[] { "--no-pause", "--bogus", "--also-bogus" });

            Assert.True(opts.NoPause);
            Assert.NotNull(opts.ParseError);
            Assert.Contains("--bogus", opts.ParseError, System.StringComparison.OrdinalIgnoreCase);
            // Second error is NOT overwritten (parseError uses ??=)
            // 2 番目のエラーは上書きされない（parseError は ??= を使用）
            Assert.DoesNotContain("--also-bogus", opts.ParseError, System.StringComparison.OrdinalIgnoreCase);
        }

        // -----------------------------------------------------------------------
        // Null element in args array
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_NullElementInArgs_IsSkipped()
        {
            var opts = CliParser.Parse(new[] { "--no-pause", null, "--skip-il" });

            Assert.True(opts.NoPause);
            Assert.True(opts.SkipIL);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // Combined flags
        // -----------------------------------------------------------------------

        [Fact]
        public void ParseCliOptions_AllFlagsCombined_ParsedCorrectly()
        {
            var opts = CliParser.Parse(new[]
            {
                "/old", "/new", "label",
                "--no-pause",
                "--config", "/etc/my.json",
                "--threads", "4",
                "--no-il-cache",
                "--skip-il",
                "--no-timestamp-warnings",
                "--print-config",
                "--dry-run",
                "--coffee",
                "--beer",
                "--matcha",
                "--whisky",
                "--wine",
                "--bell"
            });

            Assert.False(opts.ShowHelp);
            Assert.False(opts.ShowVersion);
            Assert.False(opts.ShowBanner);
            Assert.True(opts.NoPause);
            Assert.Equal("/etc/my.json", opts.ConfigPath);
            Assert.Equal(4, opts.ThreadsOverride);
            Assert.True(opts.NoIlCache);
            Assert.True(opts.SkipIL);
            Assert.True(opts.NoTimestampWarnings);
            Assert.True(opts.PrintConfig);
            Assert.True(opts.DryRun);
            // Last-wins: --wine is last spinner flag, so only Wine is true
            // 最後勝ち: --wine が最後のスピナーフラグなので Wine のみ true
            Assert.False(opts.Coffee);
            Assert.False(opts.Beer);
            Assert.False(opts.Matcha);
            Assert.False(opts.Whisky);
            Assert.True(opts.Wine);
            Assert.True(opts.Bell);
            Assert.Null(opts.ParseError);
        }

        // -----------------------------------------------------------------------
        // Spinner last-wins behavior / スピナー最後勝ち動作
        // -----------------------------------------------------------------------

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseCliOptions_MultipleSpinnerFlags_LastOneWins()
        {
            var opts = CliParser.Parse(new[] { "--matcha", "--beer" });

            Assert.False(opts.Coffee);
            Assert.True(opts.Beer);
            Assert.False(opts.Matcha);
            Assert.False(opts.Whisky);
            Assert.False(opts.Wine);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseCliOptions_CoffeeAfterWine_CoffeeWins()
        {
            var opts = CliParser.Parse(new[] { "--wine", "--coffee" });

            Assert.True(opts.Coffee);
            Assert.False(opts.Wine);
        }

        // -----------------------------------------------------------------------
        // --log-format / ログフォーマット
        // -----------------------------------------------------------------------

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseCliOptions_LogFormatJson_SetsLogFormatOverride()
        {
            var opts = CliParser.Parse(new[] { "--log-format", "json" });

            Assert.Equal("json", opts.LogFormatOverride);
            Assert.Null(opts.ParseError);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseCliOptions_LogFormatText_SetsLogFormatOverride()
        {
            var opts = CliParser.Parse(new[] { "--log-format", "text" });

            Assert.Equal("text", opts.LogFormatOverride);
            Assert.Null(opts.ParseError);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseCliOptions_LogFormatJson_CaseInsensitive()
        {
            var opts = CliParser.Parse(new[] { "--log-format", "JSON" });

            Assert.Equal("json", opts.LogFormatOverride);
            Assert.Null(opts.ParseError);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseCliOptions_LogFormatInvalid_SetsParseError()
        {
            var opts = CliParser.Parse(new[] { "--log-format", "xml" });

            Assert.NotNull(opts.ParseError);
            Assert.Contains("text", opts.ParseError, System.StringComparison.Ordinal);
            Assert.Contains("json", opts.ParseError, System.StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseCliOptions_LogFormatMissingValue_SetsParseError()
        {
            var opts = CliParser.Parse(new[] { "--log-format" });

            Assert.NotNull(opts.ParseError);
            Assert.Contains("--log-format", opts.ParseError, System.StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseCliOptions_NoLogFormat_DefaultsToNull()
        {
            var opts = CliParser.Parse(new[] { "/old", "/new", "label" });

            Assert.Null(opts.LogFormatOverride);
        }
    }
}
