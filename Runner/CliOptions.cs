namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// Holds parsed CLI options.
    /// 解析済みの CLI オプションを保持するレコード。
    /// </summary>
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
