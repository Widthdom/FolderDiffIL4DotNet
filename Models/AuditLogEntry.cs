using System;
using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Represents a single entry in the structured audit log,
    /// recording the comparison result and metadata for one file.
    /// 構造化監査ログの 1 エントリ。ファイルごとの比較結果とメタデータを記録します。
    /// </summary>
    public sealed class AuditLogFileEntry
    {
        /// <summary>Relative path of the compared file. / 比較対象ファイルの相対パス。</summary>
        public string RelativePath { get; init; } = string.Empty;

        /// <summary>
        /// Classification of the file: Added, Removed, Modified, Unchanged, or Ignored.
        /// ファイルの分類: Added, Removed, Modified, Unchanged, Ignored。
        /// </summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>
        /// Comparison detail result (e.g. SHA256Match, ILMismatch). Empty for Added/Removed/Ignored.
        /// 比較結果の詳細（SHA256Match, ILMismatch 等）。Added/Removed/Ignored では空。
        /// </summary>
        public string DiffDetail { get; init; } = string.Empty;

        /// <summary>
        /// Disassembler label used for IL comparison, if applicable.
        /// IL 比較に使用した逆アセンブラのラベル（該当しない場合は空）。
        /// </summary>
        public string Disassembler { get; init; } = string.Empty;
    }

    /// <summary>
    /// Top-level audit log record containing run metadata, file entries,
    /// summary statistics, and an integrity hash for tamper detection.
    /// 監査ログのトップレベルレコード。実行メタデータ、ファイルエントリ、
    /// サマリー統計、および改竄検知用のインテグリティハッシュを含みます。
    /// </summary>
    public sealed class AuditLogRecord
    {
        /// <summary>Application version string. / アプリケーションバージョン文字列。</summary>
        public string AppVersion { get; init; } = string.Empty;

        /// <summary>Machine name where the diff was executed. / 差分を実行したマシン名。</summary>
        public string ComputerName { get; init; } = string.Empty;

        /// <summary>Absolute path to the old (baseline) folder. / 旧（ベースライン）フォルダの絶対パス。</summary>
        public string OldFolderPath { get; init; } = string.Empty;

        /// <summary>Absolute path to the new (comparison) folder. / 新（比較先）フォルダの絶対パス。</summary>
        public string NewFolderPath { get; init; } = string.Empty;

        /// <summary>
        /// ISO 8601 timestamp when the audit log was generated.
        /// 監査ログが生成された ISO 8601 タイムスタンプ。
        /// </summary>
        public string Timestamp { get; init; } = string.Empty;

        /// <summary>Elapsed time for the diff execution. / 差分実行の経過時間。</summary>
        public string ElapsedTime { get; init; } = string.Empty;

        /// <summary>Summary statistics (Added, Removed, Modified, Unchanged, Ignored counts). / サマリー統計。</summary>
        public AuditLogSummary Summary { get; init; } = new();

        /// <summary>
        /// SHA256 hash of the generated diff_report.md for integrity verification.
        /// 改竄検知用の diff_report.md の SHA256 ハッシュ。
        /// </summary>
        public string ReportSha256 { get; init; } = string.Empty;

        /// <summary>
        /// SHA256 hash of the generated diff_report.html for integrity verification (empty if HTML report was not generated).
        /// 改竄検知用の diff_report.html の SHA256 ハッシュ（HTML レポート未生成時は空）。
        /// </summary>
        public string HtmlReportSha256 { get; init; } = string.Empty;

        /// <summary>
        /// Disassembler availability probed at startup. Null when not probed.
        /// 起動時にプローブされた逆アセンブラの利用可否。プローブされていない場合は null。
        /// </summary>
        public List<AuditLogDisassemblerAvailability>? DisassemblerAvailability { get; init; }

        /// <summary>Per-file comparison results. / ファイルごとの比較結果一覧。</summary>
        public List<AuditLogFileEntry> Files { get; init; } = new();
    }

    /// <summary>
    /// Disassembler availability entry for the audit log.
    /// 監査ログ用の逆アセンブラ利用可否エントリ。
    /// </summary>
    public sealed class AuditLogDisassemblerAvailability
    {
        /// <summary>Tool name (e.g. <c>dotnet-ildasm</c>). / ツール名。</summary>
        public string ToolName { get; init; } = string.Empty;

        /// <summary>Whether the tool is available. / ツールが利用可能か。</summary>
        public bool Available { get; init; }

        /// <summary>Version string, or empty if unavailable. / バージョン文字列。利用不可時は空。</summary>
        public string Version { get; init; } = string.Empty;
    }

    /// <summary>
    /// Summary statistics for the audit log.
    /// 監査ログ用のサマリー統計。
    /// </summary>
    public sealed class AuditLogSummary
    {
        /// <summary>Number of files present only in the new folder. / 新フォルダにのみ存在するファイル数。</summary>
        public int Added { get; init; }
        /// <summary>Number of files present only in the old folder. / 旧フォルダにのみ存在するファイル数。</summary>
        public int Removed { get; init; }
        /// <summary>Number of files that differ between old and new. / 旧新間で差異のあるファイル数。</summary>
        public int Modified { get; init; }
        /// <summary>Number of files identical between old and new. / 旧新間で同一のファイル数。</summary>
        public int Unchanged { get; init; }
        /// <summary>Number of files excluded by IgnoredExtensions. / IgnoredExtensions により除外されたファイル数。</summary>
        public int Ignored { get; init; }
    }
}
