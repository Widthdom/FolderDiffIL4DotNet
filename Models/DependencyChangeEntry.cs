namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Represents a single dependency change detected between two .deps.json files.
    /// 2 つの .deps.json ファイル間で検出された単一の依存関係変更を表します。
    /// </summary>
    /// <param name="Change">Change kind: "Added", "Removed", "Updated". / 変更種別: "Added", "Removed", "Updated"。</param>
    /// <param name="PackageName">NuGet package name (e.g. "Serilog"). / NuGet パッケージ名（例: "Serilog"）。</param>
    /// <param name="OldVersion">Previous version string, or empty for Added entries. / 旧バージョン文字列。Added エントリの場合は空。</param>
    /// <param name="NewVersion">New version string, or empty for Removed entries. / 新バージョン文字列。Removed エントリの場合は空。</param>
    /// <param name="Importance">Auto-assigned importance level for this change. Defaults to <see cref="ChangeImportance.Low"/>. / この変更に自動付与された重要度レベル。デフォルトは <see cref="ChangeImportance.Low"/>。</param>
    /// <param name="Vulnerabilities">Vulnerability check result for old and new versions (null when not checked). / 旧・新バージョンの脆弱性チェック結果（未チェック時は null）。</param>
    public sealed record DependencyChangeEntry(
        string Change,
        string PackageName,
        string OldVersion,
        string NewVersion,
        ChangeImportance Importance = ChangeImportance.Low,
        VulnerabilityCheckResult? Vulnerabilities = null);
}
