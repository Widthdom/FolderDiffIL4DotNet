using System.Collections.Generic;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Represents the validation result for <see cref="ConfigSettings"/>.
    /// <see cref="ConfigSettings"/> のバリデーション結果を表します。
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

        public ConfigValidationResult(IReadOnlyList<string> errors)
        {
            Errors = errors;
        }
    }


    /// <summary>
    /// Model class that holds settings from config.json.
    /// config.jsonの設定を保持するモデルクラス。
    /// </summary>
    public sealed class ConfigSettings
    {
        private static readonly string[] DefaultIgnoredExtensionsValues =
        {
            ".cache", ".DS_Store", ".db", ".ilcache", ".log", ".pdb"
        };

        private static readonly string[] DefaultTextFileExtensionsValues =
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

        private static readonly string[] DefaultSpinnerFramesValues = ["|", "/", "-", "\\"];

        private List<string> _ignoredExtensions = CreateDefaultIgnoredExtensions();
        private List<string> _textFileExtensions = CreateDefaultTextFileExtensions();
        private List<string> _ilIgnoreLineContainingStrings = new();
        private string _ilCacheDirectoryAbsolutePath = string.Empty;
        private List<string> _spinnerFrames = CreateDefaultSpinnerFrames();

        /// <summary>
        /// List of file extensions to ignore during comparison.
        /// 無視する拡張子のリスト。
        /// </summary>
        public List<string> IgnoredExtensions
        {
            get => _ignoredExtensions;
            set => _ignoredExtensions = value ?? CreateDefaultIgnoredExtensions();
        }

        /// <summary>
        /// List of file extensions to compare line-by-line as text.
        /// 行単位で比較する拡張子のリスト。
        /// </summary>
        public List<string> TextFileExtensions
        {
            get => _textFileExtensions;
            set => _textFileExtensions = value ?? CreateDefaultTextFileExtensions();
        }

        /// <summary>
        /// Maximum number of log generations to retain.
        /// ログの最大世代数。
        /// </summary>
        public int MaxLogGenerations { get; set; } = 5;

        /// <summary>
        /// Whether to include unchanged files in the report.
        /// 差異なしのファイルをレポートに出力するか否か。
        /// </summary>
        public bool ShouldIncludeUnchangedFiles { get; set; } = true;

        /// <summary>
        /// Whether to include files excluded by IgnoredExtensions in the report.
        /// IgnoredExtensions に該当し比較対象から除外されたファイルもレポートへ出力するか否か。
        /// </summary>
        public bool ShouldIncludeIgnoredFiles { get; set; } = true;

        /// <summary>
        /// Whether to include method-level change details (type/method/property/field additions, removals,
        /// and method body changes) for ILMismatch assemblies in the diff report.
        /// When true, a Method-Level Changes section is inserted between Summary and IL Cache Stats.
        /// ILMismatch と判定された .NET アセンブリについて、メンバーレベルの変更詳細
        /// （型・メソッド・プロパティ・フィールドの増減およびメソッドボディの変更）をレポートに出力するかどうか。
        /// true の場合、Summary セクションと IL Cache Stats セクションの間に Method-Level Changes セクションを追加します。
        /// </summary>
        public bool ShouldIncludeMethodLevelChangesInReport { get; set; } = true;

        /// <summary>
        /// Whether to include IL cache statistics (hits, misses, hit rate, etc.) in the diff report.
        /// When true, an IL Cache Stats section is inserted between the Summary and Warnings sections.
        /// If IL caching is disabled (EnableILCache = false), this section is omitted regardless of this setting.
        /// IL キャッシュの統計情報（ヒット数・ミス数・ヒット率など）を差分レポートに出力するかどうか。
        /// true の場合、Summary セクションと Warnings セクションの間に IL Cache Stats セクションを追加します。
        /// なお、IL キャッシュが無効（EnableILCache = false）の場合は本設定が true でもセクションは出力されません。
        /// </summary>
        public bool ShouldIncludeILCacheStatsInReport { get; set; } = false;

        /// <summary>
        /// Whether to generate an interactive HTML report (diff_report.html) alongside diff_report.md.
        /// When true, Removed / Added / Modified file rows include checkboxes, Justification, and Notes columns,
        /// with review state auto-saved to the browser's localStorage.
        /// A "Download as reviewed" button exports the HTML with review state included.
        /// diff_report.md と同内容のインタラクティブ HTML レポート (diff_report.html) を生成するかどうか。
        /// true の場合、Removed / Added / Modified の各ファイル行にチェックボックス・Justification（根拠）・Notes 列が付き、
        /// レビュー状態をブラウザの localStorage に自動保存します。
        /// レビュー完了後に「Download as reviewed」ボタンで状態込みの HTML をダウンロードできます。
        /// </summary>
        public bool ShouldGenerateHtmlReport { get; set; } = true;

        /// <summary>
        /// Whether to output full IL text.
        /// IL全文を出力するか否か。
        /// </summary>
        public bool ShouldOutputILText { get; set; } = true;

        /// <summary>
        /// Whether to ignore IL lines that contain any of the configured strings during comparison.
        /// IL 比較時に、指定文字列を「含む」行を無視するかどうか。
        /// </summary>
        public bool ShouldIgnoreILLinesContainingConfiguredStrings { get; set; } = false;

        /// <summary>
        /// List of strings to ignore in IL lines during comparison (substring match, multiple entries allowed).
        /// IL 比較時に無視対象とする文字列リスト（部分一致、複数指定可）。
        /// </summary>
        public List<string> ILIgnoreLineContainingStrings
        {
            get => _ilIgnoreLineContainingStrings;
            set => _ilIgnoreLineContainingStrings = value ?? new List<string>();
        }

        /// <summary>
        /// Whether to include per-file timestamps in the report.
        /// ファイルごとの更新日時をレポートに出力するか否か。
        /// </summary>
        public bool ShouldOutputFileTimestamps { get; set; } = true;

        /// <summary>
        /// Whether to warn when the new file's timestamp is older than the old file's timestamp for files present in both folders.
        /// old/new の両方に存在するファイルについて、new 側の更新日時が old 側より古い場合に警告を出すかどうか。
        /// </summary>
        public bool ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp { get; set; } = true;

        /// <summary>
        /// Maximum degree of parallelism for file comparison (0 or less = auto-detect based on CPU logical core count). Set to 1 for sequential execution.
        /// ファイル比較処理の最大並列度（0 以下または未指定で CPU 論理コア数、自動判定）。1 の場合は従来通り逐次実行。
        /// </summary>
        public int MaxParallelism { get; set; }

        /// <summary>
        /// Size threshold (KiB) above which text diff switches to parallel chunk comparison. Default: 512.
        /// テキスト差分で並列チャンク比較へ切り替えるサイズ閾値（KiB）。既定値は 512。
        /// </summary>
        public int TextDiffParallelThresholdKilobytes { get; set; } = 512;

        /// <summary>
        /// Chunk size (KiB) used for parallel text diff comparison. Default: 64.
        /// テキスト差分の並列チャンク比較で使用するチャンクサイズ（KiB）。既定値は 64。
        /// </summary>
        public int TextDiffChunkSizeKilobytes { get; set; } = 64;

        /// <summary>
        /// Additional buffer budget (MB) allowed for parallel text diff chunk comparison.
        /// 0 or less means unlimited. Each worker allocates two chunk buffers (old/new);
        /// if this budget is exceeded, the effective parallelism is reduced or falls back to sequential comparison.
        /// テキスト差分の並列チャンク比較で追加確保してよいバッファ予算（MB 単位）。
        /// 0 以下は制限なしです。1 ワーカーあたり old/new 2 本のチャンクバッファを確保する想定で、
        /// この予算を超える場合は実効並列度を下げるか逐次比較へフォールバックします。
        /// </summary>
        public int TextDiffParallelMemoryLimitMegabytes { get; set; }

        /// <summary>
        /// Whether to cache IL disassembly results to avoid redundant disassembly on subsequent runs.
        /// IL 逆アセンブル結果をキャッシュして再実行時の再逆アセンブルを回避するか。
        /// </summary>
        public bool EnableILCache { get; set; } = true;

        /// <summary>
        /// Absolute path to the IL cache directory. When null or empty, defaults to the OS-standard user-local data directory
        /// under <c>FolderDiffIL4DotNet/<see cref="Constants.DEFAULT_IL_CACHE_DIR_NAME"/></c>.
        /// Windows: <c>%LOCALAPPDATA%\FolderDiffIL4DotNet\ILCache</c>,
        /// macOS/Linux: <c>~/.local/share/FolderDiffIL4DotNet/ILCache</c>.
        /// IL キャッシュ格納ディレクトリ（null/空の場合は OS 標準のユーザーローカルデータディレクトリ配下
        /// <c>FolderDiffIL4DotNet/<see cref="Constants.DEFAULT_IL_CACHE_DIR_NAME"/></c> を既定使用。
        /// Windows: <c>%LOCALAPPDATA%\FolderDiffIL4DotNet\ILCache</c>、
        /// macOS/Linux: <c>~/.local/share/FolderDiffIL4DotNet/ILCache</c>）
        /// </summary>
        public string ILCacheDirectoryAbsolutePath
        {
            get => _ilCacheDirectoryAbsolutePath;
            set => _ilCacheDirectoryAbsolutePath = value ?? string.Empty;
        }

        /// <summary>
        /// Interval (seconds) for IL cache statistics log output. 0 or less defaults to 60 seconds.
        /// IL キャッシュ統計ログの出力間隔（秒）。0 以下または未指定で 60 秒。
        /// </summary>
        public int ILCacheStatsLogIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Maximum number of files in the on-disk IL cache (default: 1000, 0 or less = unlimited). Oldest-accessed files are evicted first when exceeded.
        /// ディスク IL キャッシュの最大ファイル数（既定: 1000、0 以下で無制限）。超過時は最終アクセスが最も古いものから削除。
        /// </summary>
        public int ILCacheMaxDiskFileCount { get; set; } = 1000;

        /// <summary>
        /// Size limit (MB) for the on-disk IL cache (default: 512, 0 or less = unlimited). Oldest files are evicted until usage drops below the limit.
        /// ディスク IL キャッシュのサイズ上限（MB 単位、既定: 512、0 以下で無制限）。超過時はサイズが下回るまで古いものを削除。
        /// </summary>
        public int ILCacheMaxDiskMegabytes { get; set; } = 512;

        /// <summary>
        /// Batch size for splitting IL-related precomputation. Default: 2048 (0 or less = use default).
        /// Prevents full aggregation of all old/new files at once, reducing peak memory usage for large file sets.
        /// IL 関連の事前計算を分割実行するバッチサイズ。既定値は 2048、0 以下または未指定で既定値を使います。
        /// 大量ファイル時に old/new 全件の一時集約を避け、追加メモリ使用量を抑えます。
        /// </summary>
        public int ILPrecomputeBatchSize { get; set; } = 2048;

        /// <summary>
        /// Whether to optimize for folder comparison on network shares (NAS/SMB, etc.).
        /// When true, skips MD5 pre-warming / IL cache pre-read and throttles default parallelism
        /// to avoid excessive network I/O.
        /// ネットワーク共有（NAS/SMB など）上のフォルダ比較に最適化するかどうか。
        /// true の場合、事前MD5プリウォーム/ILキャッシュ先読みをスキップし、
        /// 既定の並列度を抑制するなど、ネットワークI/O過多を避ける挙動になります。
        /// </summary>
        public bool OptimizeForNetworkShares { get; set; }

        /// <summary>
        /// Whether to auto-detect network shares (UNC/network drives) from old/new folder paths
        /// and enable network optimization automatically. Effective on Windows (UNC/NetworkDrive detection).
        /// On non-Windows platforms, detection accuracy is limited; manual flag usage is recommended.
        /// 旧/新フォルダの場所から自動でネットワーク共有（UNC/ネットワークドライブなど）を検出し、
        /// ネットワーク最適化を有効化します。Windows で有効（UNC/NetworkDrive 判定）。
        /// 非Windowsでは判定精度の制約があるため既定は手動フラグと併用を推奨します。
        /// </summary>
        public bool AutoDetectNetworkShares { get; set; } = true;

        /// <summary>
        /// Blacklist TTL (minutes) for disassembler tools. Default: 10 minutes.
        /// Tools that exceed the consecutive failure threshold are skipped for this period, then automatically restored.
        /// 0 or less uses the default (10 minutes).
        /// 逆アセンブラツールのブラックリスト有効期間（分）。既定値は 10 分。
        /// 連続失敗が閾値を超えたツールをこの期間スキップし、期間経過後に自動復旧します。
        /// 0 以下または未指定で既定値（10 分）を使用します。
        /// </summary>
        public int DisassemblerBlacklistTtlMinutes { get; set; } = 10;

        /// <summary>
        /// Whether to skip IL comparison for .NET assemblies.
        /// When true, IL disassembly and IL diff comparison are omitted;
        /// assemblies with MD5 mismatches are treated as binary differences.
        /// Can also be set via the CLI option --skip-il.
        /// .NET アセンブリの IL 比較をスキップするかどうか。
        /// true の場合、.NET アセンブリの IL 逆アセンブルおよび IL 差分比較を省略し、
        /// MD5 不一致のアセンブリはそのままバイナリ差分として扱います。
        /// CLI オプション --skip-il でも設定できます。
        /// </summary>
        public bool SkipIL { get; set; }

        /// <summary>
        /// Whether to display inline diffs (GitHub-style unified diff) in the Modified section of the HTML report.
        /// Only applies to text-diff (TextMismatch) files. Shown collapsed by default.
        /// Set to false to disable inline diff generation entirely.
        /// HTML レポートの Modified セクションにインライン差分（GitHub スタイルの unified diff）を表示するかどうか。
        /// テキスト差分 (TextMismatch) のファイルのみ対象です。デフォルトは折りたたみ表示。
        /// false にするとインライン差分は生成されません。
        /// </summary>
        public bool EnableInlineDiff { get; set; } = true;

        /// <summary>
        /// Number of context lines to display before and after inline diff hunks. Default: 0.
        /// インライン差分の前後に表示するコンテキスト行数。既定値は 0。
        /// </summary>
        public int InlineDiffContextLines { get; set; } = 0;

        /// <summary>
        /// Maximum edit distance (total inserted + deleted lines) allowed for inline diff computation.
        /// Inline diff is skipped when the actual diff exceeds this value. Default: 4000 (0 or less = use default).
        /// Small diffs are shown inline regardless of file size (uses Myers diff algorithm).
        /// インライン差分の計算に許容する最大編集距離（挿入行数 + 削除行数の合計）。
        /// 実際の差分がこの値を超える場合はインライン差分の表示をスキップします。既定値は 4000。
        /// 0 以下にすると既定値（4000）を使用します。
        /// ファイルサイズに依らず差分が小さければインライン表示されます（Myers diff アルゴリズム使用）。
        /// </summary>
        public int InlineDiffMaxEditDistance { get; set; } = 4000;

        /// <summary>
        /// Maximum number of diff output lines (including hunk headers) after computation.
        /// Inline diff is skipped when the output exceeds this value. Default: 10000 (0 or less = use default).
        /// インライン差分の計算結果の行数上限。差分計算後、差分出力行数（ハンクヘッダを含む）がこの値を超える場合は
        /// インライン差分の表示をスキップします。既定値は 10000。
        /// 0 以下にすると既定値（10000）を使用します。
        /// </summary>
        public int InlineDiffMaxDiffLines { get; set; } = 10000;

        /// <summary>
        /// Maximum number of inline diff lines (including hunk headers) to render in the HTML report.
        /// Excess lines are truncated. Default: 10000 (0 or less = use default).
        /// HTML レポートに出力するインライン差分の最大行数（ハンクヘッダを含む）。
        /// 超過分は打ち切り表示になります。既定値は 10000。
        /// 0 以下にすると既定値（10000）を使用します。
        /// </summary>
        public int InlineDiffMaxOutputLines { get; set; } = 10000;

        /// <summary>
        /// Whether to lazy-render inline diffs in the HTML report.
        /// When true (default), diff table HTML is Base64-encoded in a <c>data-diff-html</c> attribute
        /// and decoded/inserted into the DOM via JavaScript when the <c>&lt;details&gt;</c> element is opened.
        /// This significantly reduces initial DOM node count when many Modified files exist, improving page load speed.
        /// Set to false to embed all diff tables directly in the DOM (useful for in-browser text search of diff content).
        /// HTML レポートのインライン差分を遅延レンダリング（Lazy Render）するかどうか。
        /// true（既定）の場合、差分テーブルの HTML を Base64 エンコードして <c>data-diff-html</c> 属性に格納し、
        /// <c>&lt;details&gt;</c> を開いたときに JavaScript でデコード・DOM に挿入します。
        /// Modified ファイルが大量にある場合に初期 DOM ノード数を大幅に削減でき、ページの初期表示が高速になります。
        /// false にすると全差分テーブルを DOM に直接埋め込みます（ブラウザの「ページ内検索」で差分内容を検索したい場合に有用）。
        /// </summary>
        public bool InlineDiffLazyRender { get; set; } = true;

        /// <summary>
        /// List of frame strings for the console spinner. Each element represents one animation frame.
        /// Default: <c>["|", "/", "-", "\\"]</c> (4-frame rotation: pipe, slash, dash, backslash).
        /// Multi-character frames (e.g., block characters, emoji) are supported.
        /// null is normalized to the default value. An empty list is invalid.
        /// コンソールスピナーのフレーム文字列リスト。各要素が 1 フレームになります。
        /// 既定値は <c>["|", "/", "-", "\\"]</c>（縦棒・スラッシュ・横棒・バックスラッシュの 4 フレーム回転）。
        /// 複数文字のフレーム（例: ブロック文字、絵文字）も指定できます。
        /// null を指定した場合は既定値に正規化されます。空リストは無効です。
        /// </summary>
        public List<string> SpinnerFrames
        {
            get => _spinnerFrames;
            set => _spinnerFrames = value ?? CreateDefaultSpinnerFrames();
        }

        /// <summary>
        /// Validates the consistency of settings and returns the result.
        /// 設定値の整合性を検証し、結果を返します。
        /// </summary>
        /// <returns>
        /// Validation result. <see cref="ConfigValidationResult.IsValid"/> is false when errors exist.
        /// バリデーション結果。エラーがある場合は <see cref="ConfigValidationResult.IsValid"/> が false になります。
        /// </returns>
        public ConfigValidationResult Validate()
        {
            var errors = new List<string>();

            if (MaxLogGenerations < 1)
            {
                errors.Add($"MaxLogGenerations must be 1 or greater (current value: {MaxLogGenerations}).");
            }

            if (TextDiffParallelThresholdKilobytes < 1)
            {
                errors.Add($"TextDiffParallelThresholdKilobytes must be 1 or greater (current value: {TextDiffParallelThresholdKilobytes}).");
            }

            if (TextDiffChunkSizeKilobytes < 1)
            {
                errors.Add($"TextDiffChunkSizeKilobytes must be 1 or greater (current value: {TextDiffChunkSizeKilobytes}).");
            }
            else if (TextDiffParallelThresholdKilobytes >= 1 && TextDiffChunkSizeKilobytes >= TextDiffParallelThresholdKilobytes)
            {
                errors.Add($"TextDiffChunkSizeKilobytes ({TextDiffChunkSizeKilobytes}) must be less than TextDiffParallelThresholdKilobytes ({TextDiffParallelThresholdKilobytes}).");
            }

            if (SpinnerFrames == null || SpinnerFrames.Count == 0)
            {
                errors.Add("SpinnerFrames must contain at least one frame.");
            }

            if (InlineDiffContextLines < 0)
            {
                errors.Add($"InlineDiffContextLines must be 0 or greater (current value: {InlineDiffContextLines}).");
            }

            return new ConfigValidationResult(errors);
        }

        private static List<string> CreateDefaultIgnoredExtensions() => new(DefaultIgnoredExtensionsValues);

        private static List<string> CreateDefaultTextFileExtensions() => new(DefaultTextFileExtensionsValues);

        private static List<string> CreateDefaultSpinnerFrames() => new(DefaultSpinnerFramesValues);
    }
}
