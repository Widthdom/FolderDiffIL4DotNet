using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;
using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Context object passed to <see cref="IReportSectionWriter.Write"/>,
    /// aggregating all parameters needed for section-level report writing.
    /// <see cref="IReportSectionWriter"/> の <c>Write</c> メソッドに渡すレポート生成コンテキスト。
    /// セクション単位の書き込みに必要なすべてのパラメータを 1 か所に集約します。
    /// </summary>
    public sealed class ReportWriteContext
    {
        /// <summary>Absolute path to the baseline (old) folder. / 基準（旧）フォルダの絶対パス。</summary>
        public string OldFolderAbsolutePath { get; init; } = null!;

        /// <summary>Absolute path to the comparison (new) folder. / 比較（新）フォルダの絶対パス。</summary>
        public string NewFolderAbsolutePath { get; init; } = null!;

        /// <summary>Application version string. / アプリケーションバージョン文字列。</summary>
        public string AppVersion { get; init; } = null!;

        /// <summary>Formatted elapsed time string for the diff run. / 差分実行の経過時間フォーマット済み文字列。</summary>
        public string ElapsedTimeString { get; init; } = null!;

        /// <summary>Name of the computer that executed the run. / 実行マシン名。</summary>
        public string ComputerName { get; init; } = null!;

        /// <summary>Immutable configuration for this run. / この実行の不変設定。</summary>
        public IReadOnlyConfigSettings Config { get; init; } = null!;

        /// <summary>Logger for recoverable report-generation diagnostics. / 回復可能なレポート生成診断用ロガー。</summary>
        public ILoggerService Logger { get; init; } = null!;

        /// <summary>Whether any SHA256 mismatch was detected. / SHA256 不一致が検出されたかどうか。</summary>
        public bool HasSha256Mismatch { get; init; }

        /// <summary>Whether any timestamp regression warning was raised. / タイムスタンプ後退警告が発生したかどうか。</summary>
        public bool HasTimestampRegressionWarning { get; init; }

        /// <summary>Whether any IL filter string validation warning was raised. / IL フィルタ文字列検証警告が発生したかどうか。</summary>
        public bool HasILFilterWarnings { get; init; }

        /// <summary>Optional IL cache instance for reporting cache statistics. / キャッシュ統計レポート用の IL キャッシュインスタンス（任意）。</summary>
        public ILCache? IlCache { get; init; }

        /// <summary>Aggregated file diff results for report sections. / レポートセクション用の集約済みファイル差分結果。</summary>
        public FileDiffResultLists FileDiffResultLists { get; init; } = null!;

        /// <summary>Resolved review checklist items for this report run. / このレポート実行で解決済みのレビューチェックリスト項目。</summary>
        public IReadOnlyList<string> ReviewChecklistItems { get; init; } = [];
    }
}
