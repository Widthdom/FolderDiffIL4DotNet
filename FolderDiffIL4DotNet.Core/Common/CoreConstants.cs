namespace FolderDiffIL4DotNet.Core.Common
{
    /// <summary>
    /// Shared general-purpose constants used across multiple projects.
    /// 複数プロジェクトで共有する汎用定数を集約するクラス。
    /// </summary>
    public static class CoreConstants
    {
        /// <summary>1 KiB (2^10) in bytes. / 1 KiB のバイト数。</summary>
        public const int BYTES_PER_KILOBYTE = 1024;

        /// <summary>
        /// Timestamp format for reports and file listings (local time, second precision).
        /// Time zone is shown once in the report header, so individual entries omit it.
        /// レポートやファイル一覧で使うタイムスタンプ形式（ローカル時刻、秒精度）。
        /// タイムゾーンはレポートヘッダに一括表示するため、個別エントリには含めません。
        /// </summary>
        public const string TIMESTAMP_WITH_TIME_ZONE_FORMAT = "yyyy-MM-dd HH:mm:ss";
    }
}
