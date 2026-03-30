namespace FolderDiffIL4DotNet.Models
{
    // Parallelism, network, and inline diff settings partial for the builder.
    // ビルダーの並列処理・ネットワーク・インライン差分設定の部分クラス。
    public sealed partial class ConfigSettingsBuilder
    {
        // ── Parallelism / 並列処理 ──────────────────────────────────────────

        /// <inheritdoc cref="ConfigSettings.MaxParallelism"/>
        public int MaxParallelism { get; set; } = ConfigSettings.DefaultMaxParallelism;

        /// <inheritdoc cref="ConfigSettings.TextDiffParallelThresholdKilobytes"/>
        public int TextDiffParallelThresholdKilobytes { get; set; } = ConfigSettings.DefaultTextDiffParallelThresholdKilobytes;

        /// <inheritdoc cref="ConfigSettings.TextDiffChunkSizeKilobytes"/>
        public int TextDiffChunkSizeKilobytes { get; set; } = ConfigSettings.DefaultTextDiffChunkSizeKilobytes;

        /// <inheritdoc cref="ConfigSettings.TextDiffParallelMemoryLimitMegabytes"/>
        public int TextDiffParallelMemoryLimitMegabytes { get; set; } = ConfigSettings.DefaultTextDiffParallelMemoryLimitMegabytes;

        // ── Network / ネットワーク ──────────────────────────────────────────

        /// <inheritdoc cref="ConfigSettings.OptimizeForNetworkShares"/>
        public bool OptimizeForNetworkShares { get; set; } = ConfigSettings.DefaultOptimizeForNetworkShares;

        /// <inheritdoc cref="ConfigSettings.AutoDetectNetworkShares"/>
        public bool AutoDetectNetworkShares { get; set; } = ConfigSettings.DefaultAutoDetectNetworkShares;

        // ── Inline diff / インライン差分 ────────────────────────────────────

        /// <inheritdoc cref="ConfigSettings.EnableInlineDiff"/>
        public bool EnableInlineDiff { get; set; } = ConfigSettings.DefaultEnableInlineDiff;

        /// <inheritdoc cref="ConfigSettings.InlineDiffContextLines"/>
        public int InlineDiffContextLines { get; set; } = ConfigSettings.DefaultInlineDiffContextLines;

        /// <inheritdoc cref="ConfigSettings.InlineDiffMaxEditDistance"/>
        public int InlineDiffMaxEditDistance { get; set; } = ConfigSettings.DefaultInlineDiffMaxEditDistance;

        /// <inheritdoc cref="ConfigSettings.InlineDiffMaxDiffLines"/>
        public int InlineDiffMaxDiffLines { get; set; } = ConfigSettings.DefaultInlineDiffMaxDiffLines;

        /// <inheritdoc cref="ConfigSettings.InlineDiffMaxOutputLines"/>
        public int InlineDiffMaxOutputLines { get; set; } = ConfigSettings.DefaultInlineDiffMaxOutputLines;

        /// <inheritdoc cref="ConfigSettings.InlineDiffLazyRender"/>
        public bool InlineDiffLazyRender { get; set; } = ConfigSettings.DefaultInlineDiffLazyRender;
    }
}
