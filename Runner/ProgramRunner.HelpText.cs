using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet
{
    // Help text partial: contains the CLI help message constant.
    // ヘルプテキスト部分: CLI ヘルプメッセージ定数を格納。
    public sealed partial class ProgramRunner
    {
        private const string HELP_TEXT =
            "Usage: " + Constants.APP_NAME + " <oldFolder> <newFolder> [reportLabel] [options]\n\n" +
            "Arguments:\n" +
            "  <oldFolder>    Absolute path to the baseline (old) folder.\n" +
            "  <newFolder>    Absolute path to the comparison (new) folder.\n" +
            "  [reportLabel]  Optional label used as the subfolder name under the active reports root.\n" +
            "                 Default root is the user-local app-data Reports/ folder, or --output if specified.\n" +
            "                 When omitted, a high-resolution timestamp label is auto-generated.\n\n" +
            "Options:\n" +
            "  --help, -h                  Show this help message and exit.\n" +
            "  --version                   Show the public SemVer version and exit.\n" +
            "  --banner                    Show the ASCII-art banner and exit.\n" +
            "  --no-banner                 Suppress the startup banner during normal diff runs.\n" +
            "  --doctor                    Print build/commit diagnostics and probe IL disassembler availability.\n" +
            "  --print-config              Diagnostic: print env+supported CLI overrides as indented JSON without semantic validation.\n" +
            "  --validate-config           Validate config.json + env overrides before runtime CLI overrides (0=valid, 3=invalid).\n" +
            "  --no-pause                  Skip key-wait at process end.\n" +
            "  --config <path>             Path to config.json (default: user-local app-data config.json;\n" +
            "                              falls back to bundled config.json when absent).\n" +
            "  --threads <N>               Override MaxParallelism (0 = auto).\n" +
            "  --no-il-cache               Disable the IL cache for this run.\n" +
            "  --clear-cache               Interactive wizard to selectively delete IL cache files.\n" +
            "  --skip-il                   Skip IL comparison for .NET assemblies.\n" +
            "  --no-timestamp-warnings     Suppress timestamp-regression warnings.\n" +
            "  --creator                   Apply the default maintainer IL normalization profile.\n" +
            "  --creator-il-ignore-profile <name>\n" +
            "                              Apply a maintainer-managed IL normalization profile.\n" +
            "  --wizard                    Interactive mode: prompts for old/new folders and an optional report label.\n" +
            "                              Shows existing report folders before the label prompt; press Enter to auto-generate.\n" +
            "                              Drag-and-drop friendly.\n" +
            "  --dry-run                   Enumerate files and show statistics without running comparison.\n" +
            "  --fail-on-diff              Exit with code 5 when final reportable differences remain.\n" +
            "                              Reports and other artifacts are still generated before exit.\n" +
            "  --credits                   Show credits and acknowledgements.\n" +
            "  --bell                      Ring terminal bell when execution completes.\n" +
            "  --output <path>             Output directory for reports (default: user-local app-data Reports/).\n" +
            "  --log-format <text|json>    Log file output format (default: text).\n" +
            "                              'json' emits NDJSON lines for SIEM/log aggregation.\n" +
            "  --open-reports              Open the default user-local Reports folder in the file manager.\n" +
            "                              Respects --output if specified.\n" +
            "  --open-config               Open the default user-local config folder in the file manager.\n" +
            "                              Respects --config if specified.\n" +
            "  --open-logs                 Open the default user-local Logs folder in the file manager.\n\n" +
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
            "  4  Diff execution/report generation failure, or --doctor with no IL disassembler.\n" +
            "  5  Reportable differences found with --fail-on-diff.\n" +
            "  1  Unexpected internal error.\n\n" +
            "Tip:\n" +
            "  Use --print-config to display the effective configuration\n" +
            "  (config.json + environment variable + supported CLI overrides) as JSON and exit.\n" +
            "  This is useful for verifying which settings are active before a run.";
    }
}
