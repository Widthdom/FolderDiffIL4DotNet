using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Mutable builder for <see cref="ConfigSettings"/>.
    /// Used for JSON deserialization and applying environment variable / CLI overrides.
    /// After all overrides are applied, call <see cref="Build"/> to produce an immutable <see cref="ConfigSettings"/>.
    /// Category-specific properties are in partial files:
    /// <c>ConfigSettingsBuilder.ReportSettings.cs</c>, <c>ConfigSettingsBuilder.ILSettings.cs</c>,
    /// <c>ConfigSettingsBuilder.DiffSettings.cs</c>.
    /// <see cref="ConfigSettings"/> のミュータブルビルダー。
    /// JSON デシリアライズと環境変数 / CLI オーバーライドの適用に使用します。
    /// すべてのオーバーライド適用後に <see cref="Build"/> を呼び出してイミュータブルな <see cref="ConfigSettings"/> を生成します。
    /// カテゴリ別プロパティは部分ファイルに分割:
    /// <c>ConfigSettingsBuilder.ReportSettings.cs</c>、<c>ConfigSettingsBuilder.ILSettings.cs</c>、
    /// <c>ConfigSettingsBuilder.DiffSettings.cs</c>。
    /// </summary>
    public sealed partial class ConfigSettingsBuilder
    {
        /// <summary>
        /// Captures unmapped JSON properties (e.g. <c>$schema</c>) so that deserialization
        /// does not fail when the config file contains a schema reference.
        /// マップされない JSON プロパティ（例: <c>$schema</c>）を保持し、設定ファイルに
        /// スキーマ参照が含まれていてもデシリアライズが失敗しないようにします。
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }

        private List<string> _ignoredExtensions = ConfigSettings.CreateDefaultIgnoredExtensions();
        private List<string> _textFileExtensions = ConfigSettings.CreateDefaultTextFileExtensions();
        private List<string> _spinnerFrames = ConfigSettings.CreateDefaultSpinnerFrames();

        // ── General / 一般 ──────────────────────────────────────────────────

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

        /// <inheritdoc cref="ConfigSettings.SpinnerFrames"/>
        public List<string> SpinnerFrames
        {
            get => _spinnerFrames;
            set => _spinnerFrames = value ?? ConfigSettings.CreateDefaultSpinnerFrames();
        }

        // ── Validation & Build / バリデーション・ビルド ─────────────────────

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

            if (ILCacheMaxMemoryMegabytes < 0)
            {
                errors.Add($"ILCacheMaxMemoryMegabytes must be 0 or greater (current value: {ILCacheMaxMemoryMegabytes}).");
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
