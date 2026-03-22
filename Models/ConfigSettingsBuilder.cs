using System.Collections.Generic;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Mutable builder for <see cref="ConfigSettings"/>.
    /// Used for JSON deserialization and applying environment variable / CLI overrides.
    /// After all overrides are applied, call <see cref="Build"/> to produce an immutable <see cref="ConfigSettings"/>.
    /// <see cref="ConfigSettings"/> のミュータブルビルダー。
    /// JSON デシリアライズと環境変数 / CLI オーバーライドの適用に使用します。
    /// すべてのオーバーライド適用後に <see cref="Build"/> を呼び出してイミュータブルな <see cref="ConfigSettings"/> を生成します。
    /// </summary>
    public sealed class ConfigSettingsBuilder
    {
        private List<string> _ignoredExtensions = ConfigSettings.CreateDefaultIgnoredExtensions();
        private List<string> _textFileExtensions = ConfigSettings.CreateDefaultTextFileExtensions();
        private List<string> _ilIgnoreLineContainingStrings = new();
        private string _ilCacheDirectoryAbsolutePath = string.Empty;
        private List<string> _spinnerFrames = ConfigSettings.CreateDefaultSpinnerFrames();

        /// <inheritdoc cref="ConfigSettings.IgnoredExtensions"/>
        public List<string> IgnoredExtensions
        {
            get => _ignoredExtensions;
            set => _ignoredExtensions = value ?? ConfigSettings.CreateDefaultIgnoredExtensions();
        }

        /// <inheritdoc cref="ConfigSettings.TextFileExtensions"/>
        public List<string> TextFileExtensions
        {
            get => _textFileExtensions;
            set => _textFileExtensions = value ?? ConfigSettings.CreateDefaultTextFileExtensions();
        }

        /// <inheritdoc cref="ConfigSettings.MaxLogGenerations"/>
        public int MaxLogGenerations { get; set; } = ConfigSettings.DefaultMaxLogGenerations;

        /// <inheritdoc cref="ConfigSettings.ShouldIncludeUnchangedFiles"/>
        public bool ShouldIncludeUnchangedFiles { get; set; } = true;

        /// <inheritdoc cref="ConfigSettings.ShouldIncludeIgnoredFiles"/>
        public bool ShouldIncludeIgnoredFiles { get; set; } = true;

        /// <inheritdoc cref="ConfigSettings.ShouldIncludeAssemblySemanticChangesInReport"/>
        public bool ShouldIncludeAssemblySemanticChangesInReport { get; set; } = true;

        /// <inheritdoc cref="ConfigSettings.ShouldIncludeILCacheStatsInReport"/>
        public bool ShouldIncludeILCacheStatsInReport { get; set; } = false;

        /// <inheritdoc cref="ConfigSettings.ShouldGenerateHtmlReport"/>
        public bool ShouldGenerateHtmlReport { get; set; } = true;

        /// <inheritdoc cref="ConfigSettings.ShouldGenerateAuditLog"/>
        public bool ShouldGenerateAuditLog { get; set; } = true;

        /// <inheritdoc cref="ConfigSettings.ShouldOutputILText"/>
        public bool ShouldOutputILText { get; set; } = true;

        /// <inheritdoc cref="ConfigSettings.ShouldIgnoreILLinesContainingConfiguredStrings"/>
        public bool ShouldIgnoreILLinesContainingConfiguredStrings { get; set; } = false;

        /// <inheritdoc cref="ConfigSettings.ILIgnoreLineContainingStrings"/>
        public List<string> ILIgnoreLineContainingStrings
        {
            get => _ilIgnoreLineContainingStrings;
            set => _ilIgnoreLineContainingStrings = value ?? new List<string>();
        }

        /// <inheritdoc cref="ConfigSettings.ShouldOutputFileTimestamps"/>
        public bool ShouldOutputFileTimestamps { get; set; } = true;

        /// <inheritdoc cref="ConfigSettings.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp"/>
        public bool ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp { get; set; } = true;

        /// <inheritdoc cref="ConfigSettings.MaxParallelism"/>
        public int MaxParallelism { get; set; }

        /// <inheritdoc cref="ConfigSettings.TextDiffParallelThresholdKilobytes"/>
        public int TextDiffParallelThresholdKilobytes { get; set; } = ConfigSettings.DefaultTextDiffParallelThresholdKilobytes;

        /// <inheritdoc cref="ConfigSettings.TextDiffChunkSizeKilobytes"/>
        public int TextDiffChunkSizeKilobytes { get; set; } = ConfigSettings.DefaultTextDiffChunkSizeKilobytes;

        /// <inheritdoc cref="ConfigSettings.TextDiffParallelMemoryLimitMegabytes"/>
        public int TextDiffParallelMemoryLimitMegabytes { get; set; }

        /// <inheritdoc cref="ConfigSettings.EnableILCache"/>
        public bool EnableILCache { get; set; } = true;

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

        /// <inheritdoc cref="ConfigSettings.ILPrecomputeBatchSize"/>
        public int ILPrecomputeBatchSize { get; set; } = ConfigSettings.DefaultILPrecomputeBatchSize;

        /// <inheritdoc cref="ConfigSettings.OptimizeForNetworkShares"/>
        public bool OptimizeForNetworkShares { get; set; }

        /// <inheritdoc cref="ConfigSettings.AutoDetectNetworkShares"/>
        public bool AutoDetectNetworkShares { get; set; } = true;

        /// <inheritdoc cref="ConfigSettings.DisassemblerBlacklistTtlMinutes"/>
        public int DisassemblerBlacklistTtlMinutes { get; set; } = ConfigSettings.DefaultDisassemblerBlacklistTtlMinutes;

        /// <inheritdoc cref="ConfigSettings.SkipIL"/>
        public bool SkipIL { get; set; }

        /// <inheritdoc cref="ConfigSettings.EnableInlineDiff"/>
        public bool EnableInlineDiff { get; set; } = true;

        /// <inheritdoc cref="ConfigSettings.InlineDiffContextLines"/>
        public int InlineDiffContextLines { get; set; } = 0;

        /// <inheritdoc cref="ConfigSettings.InlineDiffMaxEditDistance"/>
        public int InlineDiffMaxEditDistance { get; set; } = ConfigSettings.DefaultInlineDiffMaxEditDistance;

        /// <inheritdoc cref="ConfigSettings.InlineDiffMaxDiffLines"/>
        public int InlineDiffMaxDiffLines { get; set; } = ConfigSettings.DefaultInlineDiffMaxDiffLines;

        /// <inheritdoc cref="ConfigSettings.InlineDiffMaxOutputLines"/>
        public int InlineDiffMaxOutputLines { get; set; } = ConfigSettings.DefaultInlineDiffMaxOutputLines;

        /// <inheritdoc cref="ConfigSettings.InlineDiffLazyRender"/>
        public bool InlineDiffLazyRender { get; set; } = true;

        /// <inheritdoc cref="ConfigSettings.SpinnerFrames"/>
        public List<string> SpinnerFrames
        {
            get => _spinnerFrames;
            set => _spinnerFrames = value ?? ConfigSettings.CreateDefaultSpinnerFrames();
        }

        /// <summary>
        /// Validates the consistency of settings and returns the result.
        /// 設定値の整合性を検証し、結果を返します。
        /// </summary>
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

        /// <summary>
        /// Builds an immutable <see cref="ConfigSettings"/> from the current builder state.
        /// 現在のビルダー状態からイミュータブルな <see cref="ConfigSettings"/> を生成します。
        /// </summary>
        public ConfigSettings Build()
        {
            return new ConfigSettings(this);
        }
    }
}
