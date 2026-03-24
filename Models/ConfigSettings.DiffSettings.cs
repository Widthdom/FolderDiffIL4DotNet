namespace FolderDiffIL4DotNet.Models
{
    // Parallelism, network, and inline diff settings partial.
    // 並列処理・ネットワーク・インライン差分設定の部分クラス。
    public sealed partial class ConfigSettings
    {
        // ── Parallelism defaults / 並列処理デフォルト ─────────────────────────
        /// <summary>Default value for <see cref="MaxParallelism"/>. / <see cref="MaxParallelism"/> の既定値。</summary>
        public const int DefaultMaxParallelism = 0;
        /// <summary>Default value for <see cref="TextDiffParallelThresholdKilobytes"/>. / <see cref="TextDiffParallelThresholdKilobytes"/> の既定値。</summary>
        public const int DefaultTextDiffParallelThresholdKilobytes = 512;
        /// <summary>Default value for <see cref="TextDiffChunkSizeKilobytes"/>. / <see cref="TextDiffChunkSizeKilobytes"/> の既定値。</summary>
        public const int DefaultTextDiffChunkSizeKilobytes = 64;
        /// <summary>Default value for <see cref="TextDiffParallelMemoryLimitMegabytes"/>. / <see cref="TextDiffParallelMemoryLimitMegabytes"/> の既定値。</summary>
        public const int DefaultTextDiffParallelMemoryLimitMegabytes = 0;

        // ── Network defaults / ネットワークデフォルト ─────────────────────────
        /// <summary>Default value for <see cref="OptimizeForNetworkShares"/>. / <see cref="OptimizeForNetworkShares"/> の既定値。</summary>
        public const bool DefaultOptimizeForNetworkShares = false;
        /// <summary>Default value for <see cref="AutoDetectNetworkShares"/>. / <see cref="AutoDetectNetworkShares"/> の既定値。</summary>
        public const bool DefaultAutoDetectNetworkShares = true;

        // ── Inline diff defaults / インライン差分デフォルト ───────────────────
        /// <summary>Default value for <see cref="EnableInlineDiff"/>. / <see cref="EnableInlineDiff"/> の既定値。</summary>
        public const bool DefaultEnableInlineDiff = true;
        /// <summary>Default value for <see cref="InlineDiffContextLines"/>. / <see cref="InlineDiffContextLines"/> の既定値。</summary>
        public const int DefaultInlineDiffContextLines = 0;
        /// <summary>Default value for <see cref="InlineDiffMaxEditDistance"/>. / <see cref="InlineDiffMaxEditDistance"/> の既定値。</summary>
        public const int DefaultInlineDiffMaxEditDistance = 4000;
        /// <summary>Default value for <see cref="InlineDiffMaxDiffLines"/>. / <see cref="InlineDiffMaxDiffLines"/> の既定値。</summary>
        public const int DefaultInlineDiffMaxDiffLines = 10000;
        /// <summary>Default value for <see cref="InlineDiffMaxOutputLines"/>. / <see cref="InlineDiffMaxOutputLines"/> の既定値。</summary>
        public const int DefaultInlineDiffMaxOutputLines = 10000;
        /// <summary>Default value for <see cref="InlineDiffLazyRender"/>. / <see cref="InlineDiffLazyRender"/> の既定値。</summary>
        public const bool DefaultInlineDiffLazyRender = true;

        // ── Parallelism properties / 並列処理プロパティ ──────────────────────

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

        // ── Network properties / ネットワークプロパティ ──────────────────────

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

        // ── Inline diff properties / インライン差分プロパティ ────────────────

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
    }
}
