namespace FolderDiffIL4DotNet.Models
{
    // Report output control settings partial for the builder.
    // ビルダーのレポート出力制御設定の部分クラス。
    public sealed partial class ConfigSettingsBuilder
    {
        /// <inheritdoc cref="ConfigSettings.ShouldIncludeUnchangedFiles"/>
        public bool ShouldIncludeUnchangedFiles { get; set; } = ConfigSettings.DefaultShouldIncludeUnchangedFiles;

        /// <inheritdoc cref="ConfigSettings.ShouldIncludeIgnoredFiles"/>
        public bool ShouldIncludeIgnoredFiles { get; set; } = ConfigSettings.DefaultShouldIncludeIgnoredFiles;

        /// <inheritdoc cref="ConfigSettings.ShouldIncludeAssemblySemanticChangesInReport"/>
        public bool ShouldIncludeAssemblySemanticChangesInReport { get; set; } = ConfigSettings.DefaultShouldIncludeAssemblySemanticChangesInReport;

        /// <inheritdoc cref="ConfigSettings.ShouldIncludeDependencyChangesInReport"/>
        public bool ShouldIncludeDependencyChangesInReport { get; set; } = ConfigSettings.DefaultShouldIncludeDependencyChangesInReport;

        /// <inheritdoc cref="ConfigSettings.ShouldIncludeReviewChecklist"/>
        public bool ShouldIncludeReviewChecklist { get; set; } = ConfigSettings.DefaultShouldIncludeReviewChecklist;

        /// <inheritdoc cref="ConfigSettings.EnableNuGetVulnerabilityCheck"/>
        public bool EnableNuGetVulnerabilityCheck { get; set; } = ConfigSettings.DefaultEnableNuGetVulnerabilityCheck;

        /// <inheritdoc cref="ConfigSettings.ShouldIncludeILCacheStatsInReport"/>
        public bool ShouldIncludeILCacheStatsInReport { get; set; } = ConfigSettings.DefaultShouldIncludeILCacheStatsInReport;

        /// <inheritdoc cref="ConfigSettings.ShouldGenerateHtmlReport"/>
        public bool ShouldGenerateHtmlReport { get; set; } = ConfigSettings.DefaultShouldGenerateHtmlReport;

        /// <inheritdoc cref="ConfigSettings.ShouldGenerateAuditLog"/>
        public bool ShouldGenerateAuditLog { get; set; } = ConfigSettings.DefaultShouldGenerateAuditLog;

        /// <inheritdoc cref="ConfigSettings.ShouldGenerateSbom"/>
        public bool ShouldGenerateSbom { get; set; } = ConfigSettings.DefaultShouldGenerateSbom;

        /// <inheritdoc cref="ConfigSettings.SbomFormat"/>
        public string SbomFormat { get; set; } = ConfigSettings.DefaultSbomFormat;

        /// <inheritdoc cref="ConfigSettings.ShouldOutputFileTimestamps"/>
        public bool ShouldOutputFileTimestamps { get; set; } = ConfigSettings.DefaultShouldOutputFileTimestamps;

        /// <inheritdoc cref="ConfigSettings.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp"/>
        public bool ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp { get; set; } = ConfigSettings.DefaultShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp;
    }
}
