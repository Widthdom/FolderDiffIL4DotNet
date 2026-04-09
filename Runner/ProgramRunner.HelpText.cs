using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet
{
    // Help text partial: contains the CLI help message constant.
    // ヘルプテキスト部分: CLI ヘルプメッセージ定数を格納。
    public sealed partial class ProgramRunner
    {
        private const string HELP_TEXT =
            "Usage: " + Constants.APP_NAME + " <oldFolder> <newFolder> <reportLabel> [options]\n\n" +
            "Arguments:\n" +
            "  <oldFolder>    Absolute path to the baseline (old) folder.\n" +
            "  <newFolder>    Absolute path to the comparison (new) folder.\n" +
            "  <reportLabel>  Label used as the subfolder name under Reports/.\n\n" +
            "Options:\n" +
            "  --help, -h                  Show this help message and exit.\n" +
            "  --version                   Show the application version and exit.\n" +
            "  --banner                    Show the ASCII-art banner and exit.\n" +
            "  --print-config              Print the effective configuration as indented JSON and exit.\n" +
            "  --validate-config           Validate the configuration file and exit (0=valid, 3=invalid).\n" +
            "  --no-pause                  Skip key-wait at process end.\n" +
            "  --config <path>             Path to config.json (default: <exe>/config.json).\n" +
            "  --threads <N>               Override MaxParallelism (0 = auto).\n" +
            "  --no-il-cache               Disable the IL cache for this run.\n" +
            "  --clear-cache               Interactive wizard to selectively delete IL cache files.\n" +
            "  --skip-il                   Skip IL comparison for .NET assemblies.\n" +
            "  --no-timestamp-warnings     Suppress timestamp-regression warnings.\n" +
            "  --creator-il-ignore-profile <name>\n" +
            "                              Apply a maintainer-managed IL ignore profile and enable IL string filtering.\n" +
            "  --wizard                    Interactive mode: prompts for old/new folders and report label.\n" +
            "                              Drag-and-drop friendly (auto-strips quotes, file:// URIs).\n" +
            "  --dry-run                   Enumerate files and show statistics without running comparison.\n" +
            "  --coffee                    Use coffee-themed spinner animation during execution.\n" +
            "  --beer                      Use beer-themed spinner animation during execution.\n" +
            "  --matcha                    Use matcha tea ceremony spinner animation during execution.\n" +
            "  --whisky                    Use whisky distilling spinner animation during execution.\n" +
            "  --wine                      Use wine making spinner animation during execution.\n" +
            "  --ramen                     Use ramen steaming spinner animation during execution.\n" +
            "  --sushi                     Use conveyor-belt sushi spinner animation during execution.\n" +
            "  --random-spinner            Randomly select a spinner theme for each run.\n" +
            "  --credits                   Show credits and acknowledgements.\n" +
            "  --bell                      Ring terminal bell when execution completes.\n" +
            "  --output <path>             Output directory for reports (default: <exe>/Reports/).\n" +
            "  --log-format <text|json>    Log file output format (default: text).\n" +
            "                              'json' emits NDJSON lines for SIEM/log aggregation.\n" +
            "  --open-reports              Open the Reports folder in the file manager.\n" +
            "                              Respects --output if specified.\n" +
            "  --open-config               Open the config folder in the file manager.\n" +
            "                              Respects --config if specified.\n" +
            "  --open-logs                 Open the Logs folder in the file manager.\n\n" +
            "Environment variables (override config.json values):\n" +
            "  FOLDERDIFF_MAXPARALLELISM=<N>               Override MaxParallelism.\n" +
            "  FOLDERDIFF_ENABLEILCACHE=<true|false>       Enable/disable the IL cache.\n" +
            "  FOLDERDIFF_ILCACHEDIRECTORYABSOLUTEPATH=<p> IL cache directory path.\n" +
            "  FOLDERDIFF_SKIPIL=<true|false>              Skip IL comparison.\n" +
            "  FOLDERDIFF_SHOULDGENERATEHTMLREPORT=<t|f>   Generate HTML report.\n" +
            "  Any FOLDERDIFF_<PROPERTYNAME>=<value> key overrides the matching\n" +
            "  config.json property (bool: true/false/1/0, int: integer).\n\n" +
            "Exit codes:\n" +
            "  0  Success.\n" +
            "  2  Invalid arguments or input paths.\n" +
            "  3  Configuration load or parse error.\n" +
            "  4  Diff execution or report generation failure.\n" +
            "  1  Unexpected internal error.\n\n" +
            "Tip:\n" +
            "  Use --print-config to display the effective configuration\n" +
            "  (config.json + environment variable + supported CLI overrides) as JSON and exit.\n" +
            "  This is useful for verifying which settings are active before a run.";
    }
}
