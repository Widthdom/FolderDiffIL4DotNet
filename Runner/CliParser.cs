namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// Parses command-line arguments into a <see cref="CliOptions"/> instance.
    /// コマンドライン引数を解析して <see cref="CliOptions"/> を生成する静的クラス。
    /// </summary>
    internal static class CliParser
    {
        private const string NO_PAUSE = "--no-pause";
        private const string OPT_HELP_LONG = "--help";
        private const string OPT_HELP_SHORT = "-h";
        private const string OPT_VERSION = "--version";
        private const string OPT_BANNER = "--banner";
        private const string OPT_CONFIG = "--config";
        private const string OPT_THREADS = "--threads";
        private const string OPT_NO_IL_CACHE = "--no-il-cache";
        private const string OPT_CLEAR_CACHE = "--clear-cache";
        private const string OPT_SKIP_IL = "--skip-il";
        private const string OPT_NO_TIMESTAMP_WARNINGS = "--no-timestamp-warnings";
        private const string OPT_PRINT_CONFIG = "--print-config";
        private const string OPT_VALIDATE_CONFIG = "--validate-config";
        private const string OPT_DRY_RUN = "--dry-run";
        private const string OPT_COFFEE = "--coffee";
        private const string OPT_BEER = "--beer";
        private const string OPT_MATCHA = "--matcha";
        private const string OPT_WHISKY = "--whisky";
        private const string OPT_WINE = "--wine";
        private const string OPT_RAMEN = "--ramen";
        private const string OPT_SUSHI = "--sushi";
        private const string OPT_BELL = "--bell";
        private const string OPT_WIZARD = "--wizard";
        private const string OPT_RANDOM_SPINNER = "--random-spinner";
        private const string OPT_CREDITS = "--credits";
        private const string OPT_LOG_FORMAT = "--log-format";
        private const string OPT_OUTPUT = "--output";

        /// <summary>
        /// Scans command-line arguments and returns parsed CLI options.
        /// Unknown flags are reported via <see cref="CliOptions.ParseError"/>.
        /// コマンドライン引数を走査して CLI オプションを解析する。
        /// 未知のフラグが見つかった場合は <see cref="CliOptions.ParseError"/> に詳細を格納する。
        /// </summary>
        internal static CliOptions Parse(string[] args)
        {
            bool showHelp = false, showVersion = false, showBanner = false, noPause = false;
            bool noIlCache = false, clearCache = false, skipIl = false, noTimestampWarnings = false, printConfig = false, validateConfig = false, dryRun = false;
            bool coffee = false, beer = false, matcha = false, whisky = false, wine = false, ramen = false, sushi = false, bell = false, wizard = false, showCredits = false;
            bool randomSpinner = false;
            // Track how many distinct spinner theme flags have been seen / 何種類のスピナーテーマフラグが指定されたかを追跡
            int spinnerFlagCount = 0;
            string? configPath = null;
            int? threadsOverride = null;
            string? logFormatOverride = null;
            string? outputDirectory = null;
            string? parseError = null;

            if (args == null)
            {
                return new CliOptions(false, false, false, false, null, null, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, null, null, null);
            }

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == null)
                {
                    continue;
                }

                switch (arg.ToLowerInvariant())
                {
                    case OPT_HELP_LONG:
                    case OPT_HELP_SHORT:
                        showHelp = true;
                        break;
                    case OPT_VERSION:
                        showVersion = true;
                        break;
                    case OPT_BANNER:
                        showBanner = true;
                        break;
                    case NO_PAUSE:
                        noPause = true;
                        break;
                    case OPT_CONFIG:
                        if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        {
                            configPath = args[++i];
                        }
                        else
                        {
                            parseError ??= $"'{OPT_CONFIG}' requires a file path argument.";
                        }
                        break;
                    case OPT_THREADS:
                        if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        {
                            string threadArg = args[++i];
                            if (int.TryParse(threadArg, out int n) && n >= 0)
                            {
                                threadsOverride = n;
                            }
                            else
                            {
                                parseError ??= $"'{OPT_THREADS}' requires a non-negative integer. Got: '{threadArg}'.";
                            }
                        }
                        else
                        {
                            parseError ??= $"'{OPT_THREADS}' requires an integer argument.";
                        }
                        break;
                    case OPT_NO_IL_CACHE:
                        noIlCache = true;
                        break;
                    case OPT_CLEAR_CACHE:
                        clearCache = true;
                        break;
                    case OPT_SKIP_IL:
                        skipIl = true;
                        break;
                    case OPT_NO_TIMESTAMP_WARNINGS:
                        noTimestampWarnings = true;
                        break;
                    case OPT_PRINT_CONFIG:
                        printConfig = true;
                        break;
                    case OPT_VALIDATE_CONFIG:
                        validateConfig = true;
                        break;
                    case OPT_DRY_RUN:
                        dryRun = true;
                        break;
                    case OPT_COFFEE:
                        // Last-wins: clear other spinner flags so CLI order determines winner
                        // 最後勝ち: 他のスピナーフラグをクリアしてCLI引数順で決定
                        coffee = true; beer = false; matcha = false; whisky = false; wine = false; ramen = false; sushi = false;
                        spinnerFlagCount++;
                        break;
                    case OPT_BEER:
                        coffee = false; beer = true; matcha = false; whisky = false; wine = false; ramen = false; sushi = false;
                        spinnerFlagCount++;
                        break;
                    case OPT_MATCHA:
                        coffee = false; beer = false; matcha = true; whisky = false; wine = false; ramen = false; sushi = false;
                        spinnerFlagCount++;
                        break;
                    case OPT_WHISKY:
                        coffee = false; beer = false; matcha = false; whisky = true; wine = false; ramen = false; sushi = false;
                        spinnerFlagCount++;
                        break;
                    case OPT_WINE:
                        coffee = false; beer = false; matcha = false; whisky = false; wine = true; ramen = false; sushi = false;
                        spinnerFlagCount++;
                        break;
                    case OPT_RAMEN:
                        coffee = false; beer = false; matcha = false; whisky = false; wine = false; ramen = true; sushi = false;
                        spinnerFlagCount++;
                        break;
                    case OPT_SUSHI:
                        coffee = false; beer = false; matcha = false; whisky = false; wine = false; ramen = false; sushi = true;
                        spinnerFlagCount++;
                        break;
                    case OPT_BELL:
                        bell = true;
                        break;
                    case OPT_WIZARD:
                        wizard = true;
                        break;
                    case OPT_RANDOM_SPINNER:
                        randomSpinner = true;
                        break;
                    case OPT_CREDITS:
                        showCredits = true;
                        break;
                    case OPT_LOG_FORMAT:
                        if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        {
                            string fmt = args[++i].ToLowerInvariant();
                            if (fmt == "text" || fmt == "json")
                            {
                                logFormatOverride = fmt;
                            }
                            else
                            {
                                parseError ??= $"'{OPT_LOG_FORMAT}' must be 'text' or 'json'. Got: '{args[i]}'.";
                            }
                        }
                        else
                        {
                            parseError ??= $"'{OPT_LOG_FORMAT}' requires a format argument (text or json).";
                        }
                        break;
                    case OPT_OUTPUT:
                        if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        {
                            outputDirectory = args[++i];
                        }
                        else
                        {
                            parseError ??= $"'{OPT_OUTPUT}' requires a directory path argument.";
                        }
                        break;
                    default:
                        // Flags (starting with --) that are not positional arguments and not recognised.
                        // 位置引数ではなく認識されないフラグ（-- で始まるもの）を検出する。
                        if (arg.StartsWith("--", System.StringComparison.Ordinal)
                            || (arg.StartsWith("-", System.StringComparison.Ordinal) && arg.Length == 2))
                        {
                            parseError ??= $"Unknown option: '{arg}'.";
                        }
                        break;
                }
            }

            bool multipleSpinnersDetected = spinnerFlagCount > 1;

            return new CliOptions(showHelp, showVersion, showBanner, noPause, configPath, threadsOverride, noIlCache, clearCache, skipIl, noTimestampWarnings, printConfig, validateConfig, dryRun, coffee, beer, matcha, whisky, wine, ramen, sushi, bell, wizard, showCredits, randomSpinner, multipleSpinnersDetected, logFormatOverride, outputDirectory, parseError);
        }
    }
}
