namespace FolderDiffIL4DotNet.Models
{
    // Report output control settings partial.
    // レポート出力制御設定の部分クラス。
    public sealed partial class ConfigSettings
    {
        // ── Report defaults / レポートデフォルト ─────────────────────────────
        /// <summary>Default value for <see cref="ShouldIncludeUnchangedFiles"/>. / <see cref="ShouldIncludeUnchangedFiles"/> の既定値。</summary>
        public const bool DefaultShouldIncludeUnchangedFiles = true;
        /// <summary>Default value for <see cref="ShouldIncludeIgnoredFiles"/>. / <see cref="ShouldIncludeIgnoredFiles"/> の既定値。</summary>
        public const bool DefaultShouldIncludeIgnoredFiles = true;
        /// <summary>Default value for <see cref="ShouldIncludeAssemblySemanticChangesInReport"/>. / <see cref="ShouldIncludeAssemblySemanticChangesInReport"/> の既定値。</summary>
        public const bool DefaultShouldIncludeAssemblySemanticChangesInReport = true;
        /// <summary>Default value for <see cref="ShouldIncludeDependencyChangesInReport"/>. / <see cref="ShouldIncludeDependencyChangesInReport"/> の既定値。</summary>
        public const bool DefaultShouldIncludeDependencyChangesInReport = true;
        /// <summary>Default value for <see cref="EnableNuGetVulnerabilityCheck"/>. / <see cref="EnableNuGetVulnerabilityCheck"/> の既定値。</summary>
        public const bool DefaultEnableNuGetVulnerabilityCheck = false;
        /// <summary>Default value for <see cref="ShouldIncludeILCacheStatsInReport"/>. / <see cref="ShouldIncludeILCacheStatsInReport"/> の既定値。</summary>
        public const bool DefaultShouldIncludeILCacheStatsInReport = false;
        /// <summary>Default value for <see cref="ShouldGenerateHtmlReport"/>. / <see cref="ShouldGenerateHtmlReport"/> の既定値。</summary>
        public const bool DefaultShouldGenerateHtmlReport = true;
        /// <summary>Default value for <see cref="ShouldGenerateAuditLog"/>. / <see cref="ShouldGenerateAuditLog"/> の既定値。</summary>
        public const bool DefaultShouldGenerateAuditLog = true;
        /// <summary>Default value for <see cref="ShouldOutputFileTimestamps"/>. / <see cref="ShouldOutputFileTimestamps"/> の既定値。</summary>
        public const bool DefaultShouldOutputFileTimestamps = true;
        /// <summary>Default value for <see cref="ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp"/>. / <see cref="ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp"/> の既定値。</summary>
        public const bool DefaultShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = true;

        // ── Report properties / レポートプロパティ ───────────────────────────

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
        /// Whether to include structured dependency changes for .deps.json files in the diff report.
        /// .deps.json ファイルの構造化された依存関係変更をレポートに出力するかどうか。
        /// </summary>
        public bool ShouldIncludeDependencyChangesInReport { get; }

        /// <summary>
        /// Whether to check NuGet package versions against known vulnerabilities (NVD/GitHub Advisory via NuGet API).
        /// Requires network access. Disabled by default.
        /// NuGet パッケージバージョンを既知の脆弱性（NuGet API 経由の NVD/GitHub Advisory）と突合するかどうか。
        /// ネットワークアクセスが必要。デフォルトは無効。
        /// </summary>
        public bool EnableNuGetVulnerabilityCheck { get; }

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
        /// Whether to include per-file timestamps in the report.
        /// ファイルごとの更新日時をレポートに出力するか否か。
        /// </summary>
        public bool ShouldOutputFileTimestamps { get; }

        /// <summary>
        /// Whether to warn when the new file's timestamp is older than the old file's.
        /// new 側の更新日時が old 側より古い場合に警告を出すかどうか。
        /// </summary>
        public bool ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp { get; }
    }
}
