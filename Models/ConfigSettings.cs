using System.Collections.Generic;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Represents the validation result for <see cref="ConfigSettingsBuilder"/>.
    /// <see cref="ConfigSettingsBuilder"/> のバリデーション結果を表します。
    /// </summary>
    public sealed class ConfigValidationResult
    {
        /// <summary>
        /// Whether validation succeeded. True when there are no errors.
        /// バリデーションが成功したかどうか。エラーがない場合に true。
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// List of validation errors. Empty when <see cref="IsValid"/> is true.
        /// バリデーションエラーのリスト。<see cref="IsValid"/> が true の場合は空。
        /// </summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ConfigValidationResult"/>.
        /// <see cref="ConfigValidationResult"/> の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="errors">Validation error messages (empty when valid). / バリデーションエラーメッセージ（正常時は空）。</param>
        public ConfigValidationResult(IReadOnlyList<string> errors)
        {
            Errors = errors;
        }
    }


    /// <summary>
    /// Immutable model class that holds settings from config.json.
    /// Constructed exclusively via <see cref="ConfigSettingsBuilder.Build"/>.
    /// Category-specific defaults and properties are in partial files:
    /// <c>ConfigSettings.ReportSettings.cs</c>, <c>ConfigSettings.ILSettings.cs</c>,
    /// <c>ConfigSettings.DiffSettings.cs</c>.
    /// config.json の設定を保持するイミュータブルなモデルクラス。
    /// <see cref="ConfigSettingsBuilder.Build"/> を経由してのみ構築されます。
    /// カテゴリ別のデフォルト値・プロパティは部分ファイルに分割:
    /// <c>ConfigSettings.ReportSettings.cs</c>、<c>ConfigSettings.ILSettings.cs</c>、
    /// <c>ConfigSettings.DiffSettings.cs</c>。
    /// </summary>
    public sealed partial class ConfigSettings : IReadOnlyConfigSettings
    {
        // ── General defaults / 一般デフォルト ────────────────────────────────
        /// <summary>Default value for <see cref="MaxLogGenerations"/>. / <see cref="MaxLogGenerations"/> の既定値。</summary>
        public const int DefaultMaxLogGenerations = 5;

        // ── Collection defaults / コレクションデフォルト ─────────────────────
        internal static readonly string[] DefaultIgnoredExtensionsValues =
        {
            ".cache", ".DS_Store", ".db", ".ilcache", ".log", ".pdb"
        };

        internal static readonly string[] DefaultTextFileExtensionsValues =
        {
            ".asax", ".ascx", ".asmx", ".aspx", ".bat", ".c", ".cmd", ".config", ".cpp", ".cs",
            ".cshtml", ".csproj", ".csx", ".css", ".csv", ".editorconfig", ".env", ".fs", ".fsi",
            ".fsproj", ".fsx", ".gitattributes", ".gitignore", ".gitmodules", ".go", ".gql",
            ".graphql", ".h", ".hpp", ".htm", ".html", ".http", ".ini", ".js", ".json", ".jsx",
            ".less", ".manifest", ".md", ".mod", ".nlog", ".nuspec", ".plist", ".props", ".ps1",
            ".psd1", ".psm1", ".py", ".razor", ".resx", ".rst", ".sass", ".scss", ".sh", ".sln",
            ".sql", ".sqlproj", ".sum", ".svg", ".targets", ".toml", ".ts", ".tsv", ".tsx",
            ".txt", ".vb", ".vbproj", ".vue", ".xaml", ".xml", ".yaml", ".yml"
        };

        internal static readonly string[] DefaultSpinnerFramesValues = ["|", "/", "-", "\\"];

        /// <summary>
        /// Constructs an immutable <see cref="ConfigSettings"/> from the specified builder.
        /// ビルダーからイミュータブルな <see cref="ConfigSettings"/> を構築します。
        /// </summary>
        internal ConfigSettings(ConfigSettingsBuilder builder)
        {
            // General / 一般
            IgnoredExtensions = builder.IgnoredExtensions.AsReadOnly();
            TextFileExtensions = builder.TextFileExtensions.AsReadOnly();
            MaxLogGenerations = builder.MaxLogGenerations;
            SpinnerFrames = builder.SpinnerFrames.AsReadOnly();

            // Report / レポート
            ShouldIncludeUnchangedFiles = builder.ShouldIncludeUnchangedFiles;
            ShouldIncludeIgnoredFiles = builder.ShouldIncludeIgnoredFiles;
            ShouldIncludeAssemblySemanticChangesInReport = builder.ShouldIncludeAssemblySemanticChangesInReport;
            ShouldIncludeDependencyChangesInReport = builder.ShouldIncludeDependencyChangesInReport;
            EnableNuGetVulnerabilityCheck = builder.EnableNuGetVulnerabilityCheck;
            ShouldIncludeILCacheStatsInReport = builder.ShouldIncludeILCacheStatsInReport;
            ShouldGenerateHtmlReport = builder.ShouldGenerateHtmlReport;
            ShouldGenerateAuditLog = builder.ShouldGenerateAuditLog;
            ShouldGenerateSbom = builder.ShouldGenerateSbom;
            SbomFormat = builder.SbomFormat;
            ShouldOutputFileTimestamps = builder.ShouldOutputFileTimestamps;
            ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp;

            // IL comparison / IL 比較
            ShouldOutputILText = builder.ShouldOutputILText;
            ShouldIgnoreILLinesContainingConfiguredStrings = builder.ShouldIgnoreILLinesContainingConfiguredStrings;
            ILIgnoreLineContainingStrings = builder.ILIgnoreLineContainingStrings.AsReadOnly();
            SkipIL = builder.SkipIL;
            ShouldIgnoreMVID = builder.ShouldIgnoreMVID;

            // IL cache / IL キャッシュ
            EnableILCache = builder.EnableILCache;
            ILCacheDirectoryAbsolutePath = builder.ILCacheDirectoryAbsolutePath;
            ILCacheStatsLogIntervalSeconds = builder.ILCacheStatsLogIntervalSeconds;
            ILCacheMaxDiskFileCount = builder.ILCacheMaxDiskFileCount;
            ILCacheMaxDiskMegabytes = builder.ILCacheMaxDiskMegabytes;
            ILCacheMaxMemoryMegabytes = builder.ILCacheMaxMemoryMegabytes;
            ILPrecomputeBatchSize = builder.ILPrecomputeBatchSize;

            // Disassembler / 逆アセンブラ
            DisassemblerBlacklistTtlMinutes = builder.DisassemblerBlacklistTtlMinutes;
            DisassemblerTimeoutSeconds = builder.DisassemblerTimeoutSeconds;

            // Parallelism / 並列処理
            MaxParallelism = builder.MaxParallelism;
            TextDiffParallelThresholdKilobytes = builder.TextDiffParallelThresholdKilobytes;
            TextDiffChunkSizeKilobytes = builder.TextDiffChunkSizeKilobytes;
            TextDiffParallelMemoryLimitMegabytes = builder.TextDiffParallelMemoryLimitMegabytes;

            // Network / ネットワーク
            OptimizeForNetworkShares = builder.OptimizeForNetworkShares;
            AutoDetectNetworkShares = builder.AutoDetectNetworkShares;

            // Inline diff / インライン差分
            EnableInlineDiff = builder.EnableInlineDiff;
            InlineDiffContextLines = builder.InlineDiffContextLines;
            InlineDiffMaxEditDistance = builder.InlineDiffMaxEditDistance;
            InlineDiffMaxDiffLines = builder.InlineDiffMaxDiffLines;
            InlineDiffMaxOutputLines = builder.InlineDiffMaxOutputLines;
            InlineDiffLazyRender = builder.InlineDiffLazyRender;

            // Plugin / プラグイン
            PluginSearchPaths = new List<string>(builder.PluginSearchPaths).AsReadOnly();
            PluginEnabledIds = new List<string>(builder.PluginEnabledIds).AsReadOnly();
            PluginConfig = new Dictionary<string, System.Text.Json.JsonElement>(builder.PluginConfig);
        }

        // ── General properties / 一般プロパティ ─────────────────────────────

        /// <summary>
        /// List of file extensions to ignore during comparison.
        /// 無視する拡張子のリスト。
        /// </summary>
        public IReadOnlyList<string> IgnoredExtensions { get; }

        /// <summary>
        /// List of file extensions to compare line-by-line as text.
        /// 行単位で比較する拡張子のリスト。
        /// </summary>
        public IReadOnlyList<string> TextFileExtensions { get; }

        /// <summary>
        /// Maximum number of log generations to retain.
        /// ログの最大世代数。
        /// </summary>
        public int MaxLogGenerations { get; }

        /// <summary>
        /// Console spinner frame strings.
        /// コンソールスピナーのフレーム文字列リスト。
        /// </summary>
        public IReadOnlyList<string> SpinnerFrames { get; }

        // Factory methods for default collections (used by ConfigSettingsBuilder).
        // デフォルトコレクションのファクトリメソッド（ConfigSettingsBuilder で使用）。
        internal static List<string> CreateDefaultIgnoredExtensions() => new(DefaultIgnoredExtensionsValues);
        internal static List<string> CreateDefaultTextFileExtensions() => new(DefaultTextFileExtensionsValues);
        internal static List<string> CreateDefaultSpinnerFrames() => new(DefaultSpinnerFramesValues);
    }
}
