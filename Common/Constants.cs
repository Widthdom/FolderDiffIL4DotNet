using FolderDiffIL4DotNet.Core.Common;

namespace FolderDiffIL4DotNet.Common
{
    /// <summary>
    /// アプリ固有の定数を集約するクラス。
    /// （他プロジェクトへ汎用移植しない前提のリテラルはここへ）
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// 1 KiB (2^10) を表すバイト数。
        /// </summary>
        public const int BYTES_PER_KILOBYTE = CoreConstants.BYTES_PER_KILOBYTE;

        /// <summary>
        /// アプリケーション名
        /// </summary>
        public const string APP_NAME = "FolderDiffIL4DotNet";

        /// <summary>
        /// IL の共通ラベル
        /// </summary>
        public const string LABEL_IL = "IL";

        /// <summary>
        /// IL キャッシュ用のデフォルトサブフォルダ名
        /// </summary>
        public const string DEFAULT_IL_CACHE_DIR_NAME = LABEL_IL + "Cache";

        /// <summary>
        /// dotnet 実行ファイル名。
        /// </summary>
        public const string DOTNET_MUXER = "dotnet";

        /// <summary>
        /// dotnet-ildasm グローバルツールの実行ファイル名。
        /// </summary>
        public const string DOTNET_ILDASM = "dotnet-ildasm";

        /// <summary>
        /// dotnet サブコマンドとしての ildasm ラベル（`dotnet ildasm`）。
        /// </summary>
        public const string ILDASM_LABEL = "ildasm";

        /// <summary>
        /// ilspyコマンド
        /// </summary>
        public const string ILSPY_CMD = "ilspycmd";

        /// <summary>
        /// 最大並列度エラー
        /// </summary>
        public const string ERROR_MAX_PARALLEL = "The maximum degree of parallelism must be 1 or greater.";

        /// <summary>
        /// MD5Mismatch の共通警告文言。
        /// </summary>
        public const string WARNING_MD5_MISMATCH = "One or more files were classified as `MD5Mismatch`. Manual review is recommended because only an MD5 hash comparison was possible.";

        /// <summary>
        /// レポートやファイル一覧で使うタイムスタンプ形式（ローカル時刻、ミリ秒、UTC オフセット付き）。
        /// </summary>
        public const string TIMESTAMP_WITH_TIME_ZONE_FORMAT = CoreConstants.TIMESTAMP_WITH_TIME_ZONE_FORMAT;

        /// <summary>
        /// ログファイル本文で使うタイムスタンプ形式（ローカル時刻、ミリ秒）。
        /// </summary>
        public const string LOG_ENTRY_TIMESTAMP_FORMAT = "yyyy-MM-dd HH:mm:ss.fff";

        /// <summary>
        /// ログローテーションのファイル名に使う日付形式。
        /// </summary>
        public const string LOG_FILE_DATE_FORMAT = "yyyyMMdd";

        /// <summary>
        /// IL キャッシュの既定メモリ件数。比較中の old/new 両側と複数アセンブリをまたいだ再利用を確保しつつ、
        /// コンソールツールとしてメモリ常駐量が過大になりにくい中間値として 2000 件を採用します。
        /// </summary>
        public const int IL_CACHE_MAX_MEMORY_ENTRIES_DEFAULT = 2000;

        /// <summary>
        /// IL キャッシュ内部統計ログの既定間隔（秒）。短時間の進捗把握はできる一方、
        /// 通常実行でログが過剰に増えない 60 秒を基準にします。
        /// </summary>
        public const int IL_CACHE_STATS_LOG_INTERVAL_DEFAULT_SECONDS = 60;

        /// <summary>
        /// IL キャッシュの既定 TTL（時間）。同日中の再実行では再利用しやすく、
        /// かつ古い成果物を翌日まで引きずりにくい 12 時間を基準にします。
        /// </summary>
        public const int IL_CACHE_TIME_TO_LIVE_DEFAULT_HOURS = 12;

    }
}
