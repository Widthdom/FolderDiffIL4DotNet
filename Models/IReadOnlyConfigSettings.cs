using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Read-only view of <see cref="ConfigSettings"/>.
    /// Consumers that do not need to mutate settings should depend on this interface.
    /// <see cref="ConfigSettings"/> の読み取り専用ビュー。
    /// 設定を変更しないコンシューマーはこのインターフェースに依存してください。
    /// </summary>
    public interface IReadOnlyConfigSettings
    {
        // ── General / 一般 ──────────────────────────────────────────────────

        /// <summary>
        /// List of file extensions to ignore during comparison.
        /// 無視する拡張子のリスト。
        /// </summary>
        IReadOnlyList<string> IgnoredExtensions { get; }

        /// <summary>
        /// List of file extensions to compare line-by-line as text.
        /// 行単位で比較する拡張子のリスト。
        /// </summary>
        IReadOnlyList<string> TextFileExtensions { get; }

        /// <summary>Maximum number of log generations to retain. / ログの最大世代数。</summary>
        int MaxLogGenerations { get; }

        /// <summary>Console spinner frame strings. / コンソールスピナーのフレーム文字列リスト。</summary>
        IReadOnlyList<string> SpinnerFrames { get; }

        // ── Report output / レポート出力 ────────────────────────────────────

        /// <summary>Whether to include unchanged files in the report. / 差異なしのファイルをレポートに出力するか否か。</summary>
        bool ShouldIncludeUnchangedFiles { get; }

        /// <summary>Whether to include files excluded by IgnoredExtensions in the report. / IgnoredExtensions に該当し比較対象から除外されたファイルもレポートへ出力するか否か。</summary>
        bool ShouldIncludeIgnoredFiles { get; }

        /// <summary>Whether to include member-level change details for ILMismatch assemblies. / ILMismatch アセンブリのメンバーレベル変更詳細をレポートに出力するかどうか。</summary>
        bool ShouldIncludeAssemblySemanticChangesInReport { get; }

        /// <summary>Whether to include structured dependency changes for .deps.json files. / .deps.json ファイルの構造化された依存関係変更をレポートに出力するかどうか。</summary>
        bool ShouldIncludeDependencyChangesInReport { get; }

        /// <summary>Whether to check NuGet packages against known vulnerabilities. Requires network. / NuGet パッケージの既知脆弱性チェックを行うか。ネットワーク必要。</summary>
        bool EnableNuGetVulnerabilityCheck { get; }

        /// <summary>Whether to include IL cache statistics in the diff report. / IL キャッシュ統計情報を差分レポートに出力するかどうか。</summary>
        bool ShouldIncludeILCacheStatsInReport { get; }

        /// <summary>Whether to generate an interactive HTML report. / インタラクティブ HTML レポートを生成するかどうか。</summary>
        bool ShouldGenerateHtmlReport { get; }

        /// <summary>Whether to generate a structured JSON audit log. / 構造化 JSON 監査ログを生成するかどうか。</summary>
        bool ShouldGenerateAuditLog { get; }

        /// <summary>Whether to generate an SBOM (Software Bill of Materials). / SBOM（ソフトウェア部品表）を生成するかどうか。</summary>
        bool ShouldGenerateSbom { get; }

        /// <summary>SBOM output format: "CycloneDX" or "SPDX". / SBOM 出力形式: "CycloneDX" または "SPDX"。</summary>
        string SbomFormat { get; }

        /// <summary>Whether to include per-file timestamps in the report. / ファイルごとの更新日時をレポートに出力するか否か。</summary>
        bool ShouldOutputFileTimestamps { get; }

        /// <summary>Whether to warn when the new file's timestamp is older than the old file's. / new 側の更新日時が old 側より古い場合に警告を出すかどうか。</summary>
        bool ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp { get; }

        // ── IL comparison / IL 比較 ─────────────────────────────────────────

        /// <summary>Whether to output full IL text. / IL全文を出力するか否か。</summary>
        bool ShouldOutputILText { get; }

        /// <summary>Whether to ignore IL lines that contain any of the configured strings. / IL 比較時に指定文字列を含む行を無視するかどうか。</summary>
        bool ShouldIgnoreILLinesContainingConfiguredStrings { get; }

        /// <summary>List of strings to ignore in IL lines during comparison. / IL 比較時に無視対象とする文字列リスト。</summary>
        IReadOnlyList<string> ILIgnoreLineContainingStrings { get; }

        /// <summary>Whether to skip IL comparison for .NET assemblies. / .NET アセンブリの IL 比較をスキップするかどうか。</summary>
        bool SkipIL { get; }

        /// <summary>Whether to ignore MVID lines during IL comparison (default true). / IL 比較時に MVID 行を無視するかどうか（デフォルト true）。</summary>
        bool ShouldIgnoreMVID { get; }

        // ── IL cache / IL キャッシュ ────────────────────────────────────────

        /// <summary>Whether to cache IL disassembly results. / IL 逆アセンブル結果をキャッシュするか。</summary>
        bool EnableILCache { get; }

        /// <summary>Absolute path to the IL cache directory. / IL キャッシュ格納ディレクトリ。</summary>
        string ILCacheDirectoryAbsolutePath { get; }

        /// <summary>Interval (seconds) for IL cache statistics log output. / IL キャッシュ統計ログの出力間隔（秒）。</summary>
        int ILCacheStatsLogIntervalSeconds { get; }

        /// <summary>Maximum number of files in the on-disk IL cache. / ディスク IL キャッシュの最大ファイル数。</summary>
        int ILCacheMaxDiskFileCount { get; }

        /// <summary>Size limit (MB) for the on-disk IL cache. / ディスク IL キャッシュのサイズ上限（MB）。</summary>
        int ILCacheMaxDiskMegabytes { get; }

        /// <summary>Memory budget (MB) for the in-memory IL cache. 0 = unlimited. / メモリ内 IL キャッシュのメモリ予算（MB）。0 = 無制限。</summary>
        int ILCacheMaxMemoryMegabytes { get; }

        /// <summary>Batch size for IL precomputation. / IL 事前計算のバッチサイズ。</summary>
        int ILPrecomputeBatchSize { get; }

        // ── Disassembler / 逆アセンブラ ─────────────────────────────────────

        /// <summary>Blacklist TTL (minutes) for disassembler tools. / 逆アセンブラツールのブラックリスト有効期間（分）。</summary>
        int DisassemblerBlacklistTtlMinutes { get; }

        /// <summary>Timeout (seconds) for each disassembler process. 0 = no timeout. / 逆アセンブラプロセスのタイムアウト（秒）。0 = 無制限。</summary>
        int DisassemblerTimeoutSeconds { get; }

        // ── Parallelism / 並列処理 ──────────────────────────────────────────

        /// <summary>Maximum degree of parallelism for file comparison. / ファイル比較処理の最大並列度。</summary>
        int MaxParallelism { get; }

        /// <summary>Size threshold (KiB) for parallel text diff. / テキスト差分の並列切替閾値（KiB）。</summary>
        int TextDiffParallelThresholdKilobytes { get; }

        /// <summary>Chunk size (KiB) for parallel text diff. / テキスト差分の並列チャンクサイズ（KiB）。</summary>
        int TextDiffChunkSizeKilobytes { get; }

        /// <summary>Additional buffer budget (MB) for parallel text diff. / テキスト差分の並列バッファ予算（MB）。</summary>
        int TextDiffParallelMemoryLimitMegabytes { get; }

        // ── Network / ネットワーク ──────────────────────────────────────────

        /// <summary>Whether to optimize for network shares. / ネットワーク共有に最適化するかどうか。</summary>
        bool OptimizeForNetworkShares { get; }

        /// <summary>Whether to auto-detect network shares. / ネットワーク共有を自動検出するかどうか。</summary>
        bool AutoDetectNetworkShares { get; }

        // ── Inline diff / インライン差分 ────────────────────────────────────

        /// <summary>Whether to display inline diffs in the HTML report. / HTML レポートにインライン差分を表示するかどうか。</summary>
        bool EnableInlineDiff { get; }

        /// <summary>Number of context lines for inline diff hunks. / インライン差分のコンテキスト行数。</summary>
        int InlineDiffContextLines { get; }

        /// <summary>Maximum edit distance for inline diff computation. / インライン差分の最大編集距離。</summary>
        int InlineDiffMaxEditDistance { get; }

        /// <summary>Maximum diff output lines for inline diff. / インライン差分の最大出力行数。</summary>
        int InlineDiffMaxDiffLines { get; }

        /// <summary>Maximum inline diff lines to render. / レンダリングするインライン差分の最大行数。</summary>
        int InlineDiffMaxOutputLines { get; }

        /// <summary>Whether to lazy-render inline diffs. / インライン差分を遅延レンダリングするかどうか。</summary>
        bool InlineDiffLazyRender { get; }

        // ── Plugin / プラグイン ──────────────────────────────────────────────

        /// <summary>Directories to scan for plugin subdirectories. / プラグインサブディレクトリをスキャンするディレクトリ。</summary>
        IReadOnlyList<string> PluginSearchPaths { get; }

        /// <summary>Plugin IDs to load. Empty = all found. / 読み込むプラグイン ID。空 = 全て。</summary>
        IReadOnlyList<string> PluginEnabledIds { get; }

        /// <summary>Per-plugin configuration as raw JSON. / プラグインごとの設定（生JSON）。</summary>
        System.Collections.Generic.IReadOnlyDictionary<string, System.Text.Json.JsonElement> PluginConfig { get; }
    }
}
