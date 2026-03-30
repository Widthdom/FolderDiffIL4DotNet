using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Models
{
    // IL comparison, cache, and disassembler settings partial for the builder.
    // ビルダーの IL 比較・キャッシュ・逆アセンブラ設定の部分クラス。
    public sealed partial class ConfigSettingsBuilder
    {
        private List<string> _ilIgnoreLineContainingStrings = new();
        private string _ilCacheDirectoryAbsolutePath = string.Empty;

        // ── IL comparison / IL 比較 ─────────────────────────────────────────

        /// <inheritdoc cref="ConfigSettings.ShouldOutputILText"/>
        public bool ShouldOutputILText { get; set; } = ConfigSettings.DefaultShouldOutputILText;

        /// <inheritdoc cref="ConfigSettings.ShouldIgnoreILLinesContainingConfiguredStrings"/>
        public bool ShouldIgnoreILLinesContainingConfiguredStrings { get; set; } = ConfigSettings.DefaultShouldIgnoreILLinesContainingConfiguredStrings;

        /// <inheritdoc cref="ConfigSettings.ILIgnoreLineContainingStrings"/>
        public List<string> ILIgnoreLineContainingStrings
        {
            get => _ilIgnoreLineContainingStrings;
            set => _ilIgnoreLineContainingStrings = value ?? new List<string>();
        }

        /// <inheritdoc cref="ConfigSettings.SkipIL"/>
        public bool SkipIL { get; set; } = ConfigSettings.DefaultSkipIL;

        // ── IL cache / IL キャッシュ ────────────────────────────────────────

        /// <inheritdoc cref="ConfigSettings.EnableILCache"/>
        public bool EnableILCache { get; set; } = ConfigSettings.DefaultEnableILCache;

        /// <inheritdoc cref="ConfigSettings.ILCacheDirectoryAbsolutePath"/>
        public string ILCacheDirectoryAbsolutePath
        {
            get => _ilCacheDirectoryAbsolutePath;
            set => _ilCacheDirectoryAbsolutePath = value ?? string.Empty;
        }

        /// <inheritdoc cref="ConfigSettings.ILCacheStatsLogIntervalSeconds"/>
        public int ILCacheStatsLogIntervalSeconds { get; set; } = ConfigSettings.DefaultILCacheStatsLogIntervalSeconds;

        /// <inheritdoc cref="ConfigSettings.ILCacheMaxDiskFileCount"/>
        public int ILCacheMaxDiskFileCount { get; set; } = ConfigSettings.DefaultILCacheMaxDiskFileCount;

        /// <inheritdoc cref="ConfigSettings.ILCacheMaxDiskMegabytes"/>
        public int ILCacheMaxDiskMegabytes { get; set; } = ConfigSettings.DefaultILCacheMaxDiskMegabytes;

        /// <inheritdoc cref="ConfigSettings.ILCacheMaxMemoryMegabytes"/>
        public int ILCacheMaxMemoryMegabytes { get; set; } = ConfigSettings.DefaultILCacheMaxMemoryMegabytes;

        /// <inheritdoc cref="ConfigSettings.ILPrecomputeBatchSize"/>
        public int ILPrecomputeBatchSize { get; set; } = ConfigSettings.DefaultILPrecomputeBatchSize;

        // ── Disassembler / 逆アセンブラ ─────────────────────────────────────

        /// <inheritdoc cref="ConfigSettings.DisassemblerBlacklistTtlMinutes"/>
        public int DisassemblerBlacklistTtlMinutes { get; set; } = ConfigSettings.DefaultDisassemblerBlacklistTtlMinutes;

        /// <inheritdoc cref="ConfigSettings.DisassemblerTimeoutSeconds"/>
        public int DisassemblerTimeoutSeconds { get; set; } = ConfigSettings.DefaultDisassemblerTimeoutSeconds;
    }
}
