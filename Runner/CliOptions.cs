namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>解析済みの CLI オプションを保持するレコードです。</summary>
    /// <param name="ShowHelp">--help/-h が指定された場合 true。</param>
    /// <param name="ShowVersion">--version が指定された場合 true。</param>
    /// <param name="NoPause">--no-pause が指定された場合 true。</param>
    /// <param name="ConfigPath">--config で指定されたパス。未指定の場合 null。</param>
    /// <param name="ThreadsOverride">--threads で指定されたスレッド数。未指定の場合 null。</param>
    /// <param name="NoIlCache">--no-il-cache が指定された場合 true。</param>
    /// <param name="SkipIL">--skip-il が指定された場合 true。</param>
    /// <param name="NoTimestampWarnings">--no-timestamp-warnings が指定された場合 true。</param>
    /// <param name="PrintConfig">--print-config が指定された場合 true。</param>
    /// <param name="ParseError">解析エラーのメッセージ。エラーがない場合 null。</param>
    internal sealed record CliOptions(
        bool ShowHelp,
        bool ShowVersion,
        bool NoPause,
        string ConfigPath,
        int? ThreadsOverride,
        bool NoIlCache,
        bool SkipIL,
        bool NoTimestampWarnings,
        bool PrintConfig,
        string ParseError);
}
