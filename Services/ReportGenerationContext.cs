using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Immutable context object aggregating all parameters required by report generation services
    /// (<see cref="ReportGenerateService"/>, <see cref="HtmlReportGenerateService"/>, <see cref="AuditLogGenerateService"/>).
    /// Eliminates parameter duplication at the <see cref="ProgramRunner"/> boundary.
    /// レポート生成サービス (<see cref="ReportGenerateService"/>, <see cref="HtmlReportGenerateService"/>,
    /// <see cref="AuditLogGenerateService"/>) が必要とする全パラメータを集約した不変コンテキスト。
    /// <see cref="ProgramRunner"/> 境界での引数重複を排除します。
    /// </summary>
    public sealed class ReportGenerationContext
    {
        /// <summary>
        /// Absolute path to the baseline (old) folder.
        /// ベースライン（旧）フォルダの絶対パス。
        /// </summary>
        public string OldFolderAbsolutePath { get; }

        /// <summary>
        /// Absolute path to the comparison (new) folder.
        /// 比較対象（新）フォルダの絶対パス。
        /// </summary>
        public string NewFolderAbsolutePath { get; }

        /// <summary>
        /// Absolute path to the report output folder.
        /// レポート出力先フォルダの絶対パス。
        /// </summary>
        public string ReportsFolderAbsolutePath { get; }

        /// <summary>
        /// Application version string.
        /// アプリケーションバージョン文字列。
        /// </summary>
        public string AppVersion { get; }

        /// <summary>
        /// Formatted elapsed time string (e.g. "0h 5m 30.1s").
        /// 書式化された経過時間文字列（例: "0h 5m 30.1s"）。
        /// </summary>
        public string ElapsedTimeString { get; }

        /// <summary>
        /// Name of the computer where the diff was executed.
        /// 差分実行が行われたコンピュータ名。
        /// </summary>
        public string ComputerName { get; }

        /// <summary>
        /// Read-only configuration settings.
        /// 読み取り専用の設定。
        /// </summary>
        public IReadOnlyConfigSettings Config { get; }

        /// <summary>
        /// Optional IL cache instance (null when caching is disabled).
        /// IL キャッシュインスタンス（キャッシュ無効時は null）。
        /// </summary>
        public ILCache? IlCache { get; }

        public ReportGenerationContext(
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            IReadOnlyConfigSettings config,
            ILCache? ilCache)
        {
            OldFolderAbsolutePath = oldFolderAbsolutePath;
            NewFolderAbsolutePath = newFolderAbsolutePath;
            ReportsFolderAbsolutePath = reportsFolderAbsolutePath;
            AppVersion = appVersion;
            ElapsedTimeString = elapsedTimeString;
            ComputerName = computerName;
            Config = config;
            IlCache = ilCache;
        }
    }
}
