namespace FolderDiffIL4DotNet.Core.Common
{
    /// <summary>
    /// 複数プロジェクトで共有する汎用定数を集約するクラス。
    /// </summary>
    public static class CoreConstants
    {
        /// <summary>
        /// 1 KiB (2^10) を表すバイト数。
        /// </summary>
        public const int BYTES_PER_KILOBYTE = 1024;

        /// <summary>
        /// レポートやファイル一覧で使うタイムスタンプ形式（ローカル時刻、秒精度）。
        /// タイムゾーンはレポートヘッダに一括表示するため、個別エントリには含めません。
        /// </summary>
        public const string TIMESTAMP_WITH_TIME_ZONE_FORMAT = "yyyy-MM-dd HH:mm:ss";
    }
}
