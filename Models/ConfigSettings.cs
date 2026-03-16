using System.Collections.Generic;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// <see cref="ConfigSettings"/> のバリデーション結果を表します。
    /// </summary>
    public sealed class ConfigValidationResult
    {
        /// <summary>
        /// バリデーションが成功したかどうか。
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// バリデーションエラーのリスト。IsValid が true の場合は空。
        /// </summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// バリデーション結果を初期化します。
        /// </summary>
        public ConfigValidationResult(IReadOnlyList<string> errors)
        {
            Errors = errors;
        }
    }


    /// <summary>
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

        private List<string> _ignoredExtensions = CreateDefaultIgnoredExtensions();
        private List<string> _textFileExtensions = CreateDefaultTextFileExtensions();
        private List<string> _ilIgnoreLineContainingStrings = new();
        private string _ilCacheDirectoryAbsolutePath = string.Empty;
        private string _spinnerFrames = "|/-\\";

        /// <summary>
        /// 無視する拡張子のリスト
        /// </summary>
        public List<string> IgnoredExtensions
        {
            get => _ignoredExtensions;
            set => _ignoredExtensions = value ?? CreateDefaultIgnoredExtensions();
        }

        /// <summary>
        /// 行単位で比較する拡張子のリスト
        /// </summary>
        public List<string> TextFileExtensions
        {
            get => _textFileExtensions;
            set => _textFileExtensions = value ?? CreateDefaultTextFileExtensions();
        }

        /// <summary>
        /// ログの最大世代数
        /// </summary>
        public int MaxLogGenerations { get; set; } = 5;

        /// <summary>
        /// 差異なしのファイルをレポートに出力するか否か
        /// </summary>
        public bool ShouldIncludeUnchangedFiles { get; set; } = true;

        /// <summary>
        /// IgnoredExtensions に該当し比較対象から除外されたファイルもレポートへ出力するか否か。
        /// </summary>
        public bool ShouldIncludeIgnoredFiles { get; set; } = true;

        /// <summary>
        /// IL キャッシュの統計情報（ヒット数・ミス数・ヒット率など）を差分レポートに出力するかどうか。
        /// true の場合、Summary セクションと Warnings セクションの間に IL Cache Stats セクションを追加します。
        /// なお、IL キャッシュが無効（EnableILCache = false）の場合は本設定が true でもセクションは出力されません。
        /// </summary>
        public bool ShouldIncludeILCacheStatsInReport { get; set; } = false;

        /// <summary>
        /// IL全文を出力するか否か
        /// </summary>
        public bool ShouldOutputILText { get; set; } = true;

        /// <summary>
        /// IL 比較時に、指定文字列を「含む」行を無視するかどうか。
        /// </summary>
        public bool ShouldIgnoreILLinesContainingConfiguredStrings { get; set; } = false;

        /// <summary>
        /// IL 比較時に無視対象とする文字列リスト（部分一致、複数指定可）。
        /// </summary>
        public List<string> ILIgnoreLineContainingStrings
        {
            get => _ilIgnoreLineContainingStrings;
            set => _ilIgnoreLineContainingStrings = value ?? new List<string>();
        }

        /// <summary>
        /// ファイルごとの更新日時をレポートに出力するか否か
        /// </summary>
        public bool ShouldOutputFileTimestamps { get; set; } = true;

        /// <summary>
        /// old/new の両方に存在するファイルについて、new 側の更新日時が old 側より古い場合に警告を出すかどうか。
        /// </summary>
        public bool ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp { get; set; } = true;

        /// <summary>
        /// ファイル比較処理の最大並列度（0 以下または未指定で CPU 論理コア数、自動判定）。1 の場合は従来通り逐次実行。
        /// </summary>
        public int MaxParallelism { get; set; }

        /// <summary>
        /// テキスト差分で並列チャンク比較へ切り替えるサイズ閾値（KiB）。既定値は 512。
        /// </summary>
        public int TextDiffParallelThresholdKilobytes { get; set; } = 512;

        /// <summary>
        /// テキスト差分の並列チャンク比較で使用するチャンクサイズ（KiB）。既定値は 64。
        /// </summary>
        public int TextDiffChunkSizeKilobytes { get; set; } = 64;

        /// <summary>
        /// テキスト差分の並列チャンク比較で追加確保してよいバッファ予算（MB 単位）。
        /// 0 以下は制限なしです。1 ワーカーあたり old/new 2 本のチャンクバッファを確保する想定で、
        /// この予算を超える場合は実効並列度を下げるか逐次比較へフォールバックします。
        /// </summary>
        public int TextDiffParallelMemoryLimitMegabytes { get; set; }

        /// <summary>
        /// IL 逆アセンブル結果をキャッシュして再実行時の再逆アセンブルを回避するか
        /// </summary>
        public bool EnableILCache { get; set; } = true;

        /// <summary>
        /// IL キャッシュ格納ディレクトリ（null/空の場合は実行ディレクトリ配下 <see cref="Constants.DEFAULT_IL_CACHE_DIR_NAME"/> を既定使用）
        /// </summary>
        public string ILCacheDirectoryAbsolutePath
        {
            get => _ilCacheDirectoryAbsolutePath;
            set => _ilCacheDirectoryAbsolutePath = value ?? string.Empty;
        }

        /// <summary>
        /// IL キャッシュ統計ログの出力間隔（秒）。0 以下または未指定で 60 秒。
        /// </summary>
        public int ILCacheStatsLogIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// ディスク IL キャッシュの最大ファイル数（既定: 1000、0 以下で無制限）。超過時は最終アクセスが最も古いものから削除。
        /// </summary>
        public int ILCacheMaxDiskFileCount { get; set; } = 1000;

        /// <summary>
        /// ディスク IL キャッシュのサイズ上限（MB 単位、既定: 512、0 以下で無制限）。超過時はサイズが下回るまで古いものを削除。
        /// </summary>
        public int ILCacheMaxDiskMegabytes { get; set; } = 512;

        /// <summary>
        /// IL 関連の事前計算を分割実行するバッチサイズ。既定値は 2048、0 以下または未指定で既定値を使います。
        /// 大量ファイル時に old/new 全件の一時集約を避け、追加メモリ使用量を抑えます。
        /// </summary>
        public int ILPrecomputeBatchSize { get; set; } = 2048;

        /// <summary>
        /// ネットワーク共有（NAS/SMB など）上のフォルダ比較に最適化するかどうか。
        /// true の場合、事前MD5プリウォーム/ILキャッシュ先読みをスキップし、
        /// 既定の並列度を抑制するなど、ネットワークI/O過多を避ける挙動になります。
        /// </summary>
        public bool OptimizeForNetworkShares { get; set; }

        /// <summary>
        /// 旧/新フォルダの場所から自動でネットワーク共有（UNC/ネットワークドライブなど）を検出し、
        /// ネットワーク最適化を有効化します。Windows で有効（UNC/NetworkDrive 判定）。
        /// 非Windowsでは判定精度の制約があるため既定は手動フラグと併用を推奨します。
        /// </summary>
        public bool AutoDetectNetworkShares { get; set; } = true;

        /// <summary>
        /// .NET アセンブリの IL 比較をスキップするかどうか。
        /// true の場合、.NET アセンブリの IL 逆アセンブルおよび IL 差分比較を省略し、
        /// MD5 不一致のアセンブリはそのままバイナリ差分として扱います。
        /// CLI オプション --skip-il でも設定できます。
        /// </summary>
        public bool SkipIL { get; set; }

        /// <summary>
        /// コンソールスピナーのフレーム文字列。各文字が 1 フレームになります。
        /// 既定値は <c>"|/-\"</c>（縦棒・スラッシュ・横棒・バックスラッシュの 4 フレーム回転）。
        /// null を指定した場合は既定値に正規化されます。空文字列は無効です。
        /// </summary>
        public string SpinnerFrames
        {
            get => _spinnerFrames;
            set => _spinnerFrames = value ?? "|/-\\";
        }

        /// <summary>
        /// 設定値の整合性を検証し、結果を返します。
        /// </summary>
        /// <returns>バリデーション結果。エラーがある場合は <see cref="ConfigValidationResult.IsValid"/> が false になります。</returns>
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

            if (string.IsNullOrEmpty(SpinnerFrames))
            {
                errors.Add("SpinnerFrames must be a non-empty string.");
            }

            return new ConfigValidationResult(errors);
        }

        private static List<string> CreateDefaultIgnoredExtensions() => new(DefaultIgnoredExtensionsValues);

        private static List<string> CreateDefaultTextFileExtensions() => new(DefaultTextFileExtensionsValues);
    }
}
