namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// コマンドライン引数を解析して <see cref="CliOptions"/> を生成する静的クラスです。
    /// </summary>
    internal static class CliParser
    {
        private const string NO_PAUSE = "--no-pause";
        private const string OPT_HELP_LONG = "--help";
        private const string OPT_HELP_SHORT = "-h";
        private const string OPT_VERSION = "--version";
        private const string OPT_CONFIG = "--config";
        private const string OPT_THREADS = "--threads";
        private const string OPT_NO_IL_CACHE = "--no-il-cache";
        private const string OPT_SKIP_IL = "--skip-il";
        private const string OPT_NO_TIMESTAMP_WARNINGS = "--no-timestamp-warnings";
        private const string OPT_PRINT_CONFIG = "--print-config";

        /// <summary>
        /// コマンドライン引数を走査して CLI オプションを解析します。
        /// 未知のフラグが見つかった場合は <see cref="CliOptions.ParseError"/> に詳細を格納します。
        /// </summary>
        /// <param name="args">コマンドライン引数配列。</param>
        /// <returns>解析済み CLI オプション。</returns>
        internal static CliOptions Parse(string[] args)
        {
            bool showHelp = false, showVersion = false, noPause = false;
            bool noIlCache = false, skipIl = false, noTimestampWarnings = false, printConfig = false;
            string configPath = null;
            int? threadsOverride = null;
            string parseError = null;

            if (args == null)
            {
                return new CliOptions(false, false, false, null, null, false, false, false, false, null);
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
                    case OPT_SKIP_IL:
                        skipIl = true;
                        break;
                    case OPT_NO_TIMESTAMP_WARNINGS:
                        noTimestampWarnings = true;
                        break;
                    case OPT_PRINT_CONFIG:
                        printConfig = true;
                        break;
                    default:
                        // Flags (starting with --) that are not positional arguments and not recognised.
                        if (arg.StartsWith("--", System.StringComparison.Ordinal)
                            || (arg.StartsWith("-", System.StringComparison.Ordinal) && arg.Length == 2))
                        {
                            parseError ??= $"Unknown option: '{arg}'.";
                        }
                        break;
                }
            }

            return new CliOptions(showHelp, showVersion, noPause, configPath, threadsOverride, noIlCache, skipIl, noTimestampWarnings, printConfig, parseError);
        }
    }
}
