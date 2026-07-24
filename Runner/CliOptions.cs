namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// Holds parsed CLI options and positional run arguments.
    /// 解析済みの CLI オプションと位置指定の実行引数を保持するレコード。
    /// </summary>
    internal sealed record CliOptions
    {
        internal string? OldFolder { get; init; }
        internal string? NewFolder { get; init; }
        internal string? ReportLabel { get; init; }
        internal bool ShowHelp { get; init; }
        internal bool ShowVersion { get; init; }
        internal bool ShowBanner { get; init; }
        internal bool NoBanner { get; init; }
        internal bool Doctor { get; init; }
        internal bool NoPause { get; init; }
        internal string? ConfigPath { get; init; }
        internal int? ThreadsOverride { get; init; }
        internal bool NoIlCache { get; init; }
        internal bool ClearCache { get; init; }
        internal bool SkipIL { get; init; }
        internal bool NoTimestampWarnings { get; init; }
        internal bool Creator { get; init; }
        internal string? CreatorIlIgnoreProfile { get; init; }
        internal bool PrintConfig { get; init; }
        internal bool ValidateConfig { get; init; }
        internal bool DryRun { get; init; }
        internal bool Coffee { get; init; }
        internal bool Beer { get; init; }
        internal bool Matcha { get; init; }
        internal bool Whisky { get; init; }
        internal bool Wine { get; init; }
        internal bool Ramen { get; init; }
        internal bool Sushi { get; init; }
        internal bool Bell { get; init; }
        internal bool Wizard { get; init; }
        internal bool ShowCredits { get; init; }
        internal bool RandomSpinner { get; init; }
        internal bool MultipleSpinnersDetected { get; init; }
        internal string? LogFormatOverride { get; init; }
        internal string? OutputDirectory { get; init; }
        internal bool OpenReports { get; init; }
        internal bool OpenConfig { get; init; }
        internal bool OpenLogs { get; init; }
        internal string? ParseError { get; init; }
    }
}
