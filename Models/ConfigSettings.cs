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
    /// config.json の設定を保持するイミュータブルなモデルクラス。
    /// <see cref="ConfigSettingsBuilder.Build"/> を経由してのみ構築されます。
    /// </summary>
    public sealed class ConfigSettings : IReadOnlyConfigSettings
    {
        /// <summary>Default value for <see cref="MaxLogGenerations"/>. / <see cref="MaxLogGenerations"/> の既定値。</summary>
        public const int DefaultMaxLogGenerations = 5;
        /// <summary>Default value for <see cref="TextDiffParallelThresholdKilobytes"/>. / <see cref="TextDiffParallelThresholdKilobytes"/> の既定値。</summary>
        public const int DefaultTextDiffParallelThresholdKilobytes = 512;
        /// <summary>Default value for <see cref="TextDiffChunkSizeKilobytes"/>. / <see cref="TextDiffChunkSizeKilobytes"/> の既定値。</summary>
        public const int DefaultTextDiffChunkSizeKilobytes = 64;
        /// <summary>Default value for <see cref="ILCacheStatsLogIntervalSeconds"/>. / <see cref="ILCacheStatsLogIntervalSeconds"/> の既定値。</summary>
        public const int DefaultILCacheStatsLogIntervalSeconds = 60;
        /// <summary>Default value for <see cref="ILCacheMaxDiskFileCount"/>. / <see cref="ILCacheMaxDiskFileCount"/> の既定値。</summary>
        public const int DefaultILCacheMaxDiskFileCount = 1000;
        /// <summary>Default value for <see cref="ILCacheMaxDiskMegabytes"/>. / <see cref="ILCacheMaxDiskMegabytes"/> の既定値。</summary>
        public const int DefaultILCacheMaxDiskMegabytes = 512;
        /// <summary>Default value for <see cref="ILCacheMaxMemoryMegabytes"/>. / <see cref="ILCacheMaxMemoryMegabytes"/> の既定値。</summary>
        public const int DefaultILCacheMaxMemoryMegabytes = 0;
        /// <summary>Default value for <see cref="ILPrecomputeBatchSize"/>. / <see cref="ILPrecomputeBatchSize"/> の既定値。</summary>
        public const int DefaultILPrecomputeBatchSize = 2048;
        /// <summary>Default value for <see cref="DisassemblerBlacklistTtlMinutes"/>. / <see cref="DisassemblerBlacklistTtlMinutes"/> の既定値。</summary>
        public const int DefaultDisassemblerBlacklistTtlMinutes = 10;
        /// <summary>Default value for <see cref="InlineDiffMaxEditDistance"/>. / <see cref="InlineDiffMaxEditDistance"/> の既定値。</summary>
        public const int DefaultInlineDiffMaxEditDistance = 4000;
        /// <summary>Default value for <see cref="InlineDiffMaxDiffLines"/>. / <see cref="InlineDiffMaxDiffLines"/> の既定値。</summary>
        public const int DefaultInlineDiffMaxDiffLines = 10000;
        /// <summary>Default value for <see cref="InlineDiffMaxOutputLines"/>. / <see cref="InlineDiffMaxOutputLines"/> の既定値。</summary>
        public const int DefaultInlineDiffMaxOutputLines = 10000;

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
            IgnoredExtensions = builder.IgnoredExtensions.AsReadOnly();
            TextFileExtensions = builder.TextFileExtensions.AsReadOnly();
            MaxLogGenerations = builder.MaxLogGenerations;
            ShouldIncludeUnchangedFiles = builder.ShouldIncludeUnchangedFiles;
            ShouldIncludeIgnoredFiles = builder.ShouldIncludeIgnoredFiles;
            ShouldIncludeAssemblySemanticChangesInReport = builder.ShouldIncludeAssemblySemanticChangesInReport;
            ShouldIncludeILCacheStatsInReport = builder.ShouldIncludeILCacheStatsInReport;
            ShouldGenerateHtmlReport = builder.ShouldGenerateHtmlReport;
            ShouldGenerateAuditLog = builder.ShouldGenerateAuditLog;
            ShouldOutputILText = builder.ShouldOutputILText;
            ShouldIgnoreILLinesContainingConfiguredStrings = builder.ShouldIgnoreILLinesContainingConfiguredStrings;
            ILIgnoreLineContainingStrings = builder.ILIgnoreLineContainingStrings.AsReadOnly();
            ShouldOutputFileTimestamps = builder.ShouldOutputFileTimestamps;
            ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp;
            MaxParallelism = builder.MaxParallelism;
            TextDiffParallelThresholdKilobytes = builder.TextDiffParallelThresholdKilobytes;
            TextDiffChunkSizeKilobytes = builder.TextDiffChunkSizeKilobytes;
            TextDiffParallelMemoryLimitMegabytes = builder.TextDiffParallelMemoryLimitMegabytes;
            EnableILCache = builder.EnableILCache;
            ILCacheDirectoryAbsolutePath = builder.ILCacheDirectoryAbsolutePath;
            ILCacheStatsLogIntervalSeconds = builder.ILCacheStatsLogIntervalSeconds;
            ILCacheMaxDiskFileCount = builder.ILCacheMaxDiskFileCount;
            ILCacheMaxDiskMegabytes = builder.ILCacheMaxDiskMegabytes;
            ILCacheMaxMemoryMegabytes = builder.ILCacheMaxMemoryMegabytes;
            ILPrecomputeBatchSize = builder.ILPrecomputeBatchSize;
            OptimizeForNetworkShares = builder.OptimizeForNetworkShares;
            AutoDetectNetworkShares = builder.AutoDetectNetworkShares;
            DisassemblerBlacklistTtlMinutes = builder.DisassemblerBlacklistTtlMinutes;
            SkipIL = builder.SkipIL;
            EnableInlineDiff = builder.EnableInlineDiff;
            InlineDiffContextLines = builder.InlineDiffContextLines;
            InlineDiffMaxEditDistance = builder.InlineDiffMaxEditDistance;
            InlineDiffMaxDiffLines = builder.InlineDiffMaxDiffLines;
            InlineDiffMaxOutputLines = builder.InlineDiffMaxOutputLines;
            InlineDiffLazyRender = builder.InlineDiffLazyRender;
            SpinnerFrames = builder.SpinnerFrames.AsReadOnly();
        }

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
        /// Whether to include unchanged files in the report.
        /// 差異なしのファイルをレポートに出力するか否か。
        /// </summary>
        public bool ShouldIncludeUnchangedFiles { get; }

        /// <summary>
        /// Whether to include files excluded by IgnoredExtensions in the report.
        /// IgnoredExtensions に該当し比較対象から除外されたファイルもレポートへ出力するか否か。
        /// </summary>
        public bool ShouldIncludeIgnoredFiles { get; }

        /// <summary>
        /// Whether to include member-level change details for ILMismatch assemblies in the diff report.
        /// ILMismatch アセンブリのメンバーレベル変更詳細をレポートに出力するかどうか。
        /// </summary>
        public bool ShouldIncludeAssemblySemanticChangesInReport { get; }

        /// <summary>
        /// Whether to include IL cache statistics in the diff report.
        /// IL キャッシュ統計情報を差分レポートに出力するかどうか。
        /// </summary>
        public bool ShouldIncludeILCacheStatsInReport { get; }

        /// <summary>
        /// Whether to generate an interactive HTML report.
        /// インタラクティブ HTML レポートを生成するかどうか。
        /// </summary>
        public bool ShouldGenerateHtmlReport { get; }

        /// <summary>
        /// Whether to generate a structured JSON audit log.
        /// 構造化 JSON 監査ログを生成するかどうか。
        /// </summary>
        public bool ShouldGenerateAuditLog { get; }

        /// <summary>
        /// Whether to output full IL text.
        /// IL全文を出力するか否か。
        /// </summary>
        public bool ShouldOutputILText { get; }

        /// <summary>
        /// Whether to ignore IL lines that contain any of the configured strings during comparison.
        /// IL 比較時に、指定文字列を「含む」行を無視するかどうか。
        /// </summary>
        public bool ShouldIgnoreILLinesContainingConfiguredStrings { get; }

        /// <summary>
        /// List of strings to ignore in IL lines during comparison.
        /// IL 比較時に無視対象とする文字列リスト。
        /// </summary>
        public IReadOnlyList<string> ILIgnoreLineContainingStrings { get; }

        /// <summary>
        /// Whether to include per-file timestamps in the report.
        /// ファイルごとの更新日時をレポートに出力するか否か。
        /// </summary>
        public bool ShouldOutputFileTimestamps { get; }

        /// <summary>
        /// Whether to warn when the new file's timestamp is older than the old file's.
        /// new 側の更新日時が old 側より古い場合に警告を出すかどうか。
        /// </summary>
        public bool ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp { get; }

        /// <summary>
        /// Maximum degree of parallelism for file comparison.
        /// ファイル比較処理の最大並列度。
        /// </summary>
        public int MaxParallelism { get; }

        /// <summary>
        /// Size threshold (KiB) for parallel text diff.
        /// テキスト差分の並列切替閾値（KiB）。
        /// </summary>
        public int TextDiffParallelThresholdKilobytes { get; }

        /// <summary>
        /// Chunk size (KiB) for parallel text diff.
        /// テキスト差分の並列チャンクサイズ（KiB）。
        /// </summary>
        public int TextDiffChunkSizeKilobytes { get; }

        /// <summary>
        /// Additional buffer budget (MB) for parallel text diff.
        /// テキスト差分の並列バッファ予算（MB）。
        /// </summary>
        public int TextDiffParallelMemoryLimitMegabytes { get; }

        /// <summary>
        /// Whether to cache IL disassembly results.
        /// IL 逆アセンブル結果をキャッシュするか。
        /// </summary>
        public bool EnableILCache { get; }

        /// <summary>
        /// Absolute path to the IL cache directory.
        /// IL キャッシュ格納ディレクトリ。
        /// </summary>
        public string ILCacheDirectoryAbsolutePath { get; }

        /// <summary>
        /// Interval (seconds) for IL cache statistics log output.
        /// IL キャッシュ統計ログの出力間隔（秒）。
        /// </summary>
        public int ILCacheStatsLogIntervalSeconds { get; }

        /// <summary>
        /// Maximum number of files in the on-disk IL cache.
        /// ディスク IL キャッシュの最大ファイル数。
        /// </summary>
        public int ILCacheMaxDiskFileCount { get; }

        /// <summary>
        /// Size limit (MB) for the on-disk IL cache.
        /// ディスク IL キャッシュのサイズ上限（MB）。
        /// </summary>
        public int ILCacheMaxDiskMegabytes { get; }

        /// <summary>
        /// Memory budget (MB) for the in-memory IL cache. 0 means unlimited (entry-count limit only).
        /// メモリ内 IL キャッシュのメモリ予算（MB）。0 はエントリ数上限のみで無制限。
        /// </summary>
        public int ILCacheMaxMemoryMegabytes { get; }

        /// <summary>
        /// Batch size for IL precomputation.
        /// IL 事前計算のバッチサイズ。
        /// </summary>
        public int ILPrecomputeBatchSize { get; }

        /// <summary>
        /// Whether to optimize for network shares.
        /// ネットワーク共有に最適化するかどうか。
        /// </summary>
        public bool OptimizeForNetworkShares { get; }

        /// <summary>
        /// Whether to auto-detect network shares.
        /// ネットワーク共有を自動検出するかどうか。
        /// </summary>
        public bool AutoDetectNetworkShares { get; }

        /// <summary>
        /// Blacklist TTL (minutes) for disassembler tools.
        /// 逆アセンブラツールのブラックリスト有効期間（分）。
        /// </summary>
        public int DisassemblerBlacklistTtlMinutes { get; }

        /// <summary>
        /// Whether to skip IL comparison for .NET assemblies.
        /// .NET アセンブリの IL 比較をスキップするかどうか。
        /// </summary>
        public bool SkipIL { get; }

        /// <summary>
        /// Whether to display inline diffs in the HTML report.
        /// HTML レポートにインライン差分を表示するかどうか。
        /// </summary>
        public bool EnableInlineDiff { get; }

        /// <summary>
        /// Number of context lines for inline diff hunks.
        /// インライン差分のコンテキスト行数。
        /// </summary>
        public int InlineDiffContextLines { get; }

        /// <summary>
        /// Maximum edit distance for inline diff computation.
        /// インライン差分の最大編集距離。
        /// </summary>
        public int InlineDiffMaxEditDistance { get; }

        /// <summary>
        /// Maximum diff output lines for inline diff.
        /// インライン差分の最大差分行数。
        /// </summary>
        public int InlineDiffMaxDiffLines { get; }

        /// <summary>
        /// Maximum inline diff lines to render.
        /// レンダリングするインライン差分の最大行数。
        /// </summary>
        public int InlineDiffMaxOutputLines { get; }

        /// <summary>
        /// Whether to lazy-render inline diffs.
        /// インライン差分を遅延レンダリングするかどうか。
        /// </summary>
        public bool InlineDiffLazyRender { get; }

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
