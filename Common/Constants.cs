using FolderDiffIL4DotNet.Core.Common;

namespace FolderDiffIL4DotNet.Common
{
    /// <summary>
    /// Aggregates application-specific constants (literals not intended for reuse in other projects).
    /// アプリ固有の定数を集約するクラス（他プロジェクトへ汎用移植しない前提のリテラルはここへ）。
    /// </summary>
    public static class Constants
    {
        public const int BYTES_PER_KILOBYTE = CoreConstants.BYTES_PER_KILOBYTE;

        public const string APP_NAME = "FolderDiffIL4DotNet";

        public const string LABEL_IL = "IL";

        public const string DEFAULT_IL_CACHE_DIR_NAME = LABEL_IL + "Cache";

        public const string DOTNET_MUXER = "dotnet";

        public const string DOTNET_ILDASM = "dotnet-ildasm";

        public const string ILDASM_LABEL = "ildasm";

        public const string ILSPY_CMD = "ilspycmd";

        public const string ERROR_MAX_PARALLEL = "The maximum degree of parallelism must be 1 or greater.";

        public const string WARNING_MD5_MISMATCH = "One or more files were classified as `MD5Mismatch`. Manual review is recommended because only an MD5 hash comparison was possible.";

        /// <summary>
        /// Timestamp format for reports and file listings (local time with milliseconds and UTC offset).
        /// レポートやファイル一覧で使うタイムスタンプ形式（ローカル時刻、ミリ秒、UTC オフセット付き）。
        /// </summary>
        public const string TIMESTAMP_WITH_TIME_ZONE_FORMAT = CoreConstants.TIMESTAMP_WITH_TIME_ZONE_FORMAT;

        /// <summary>
        /// Timestamp format for log file entries (local time with milliseconds).
        /// ログファイル本文で使うタイムスタンプ形式（ローカル時刻、ミリ秒）。
        /// </summary>
        public const string LOG_ENTRY_TIMESTAMP_FORMAT = "yyyy-MM-dd HH:mm:ss.fff";

        public const string LOG_FILE_DATE_FORMAT = "yyyyMMdd";

        /// <summary>
        /// Default in-memory entry limit for the IL cache. 2000 balances reuse across old/new sides
        /// and multiple assemblies while keeping resident memory reasonable for a console tool.
        /// IL キャッシュの既定メモリ件数。比較中の old/new 両側と複数アセンブリをまたいだ再利用を確保しつつ、
        /// コンソールツールとしてメモリ常駐量が過大になりにくい中間値として 2000 件を採用。
        /// </summary>
        public const int IL_CACHE_MAX_MEMORY_ENTRIES_DEFAULT = 2000;

        /// <summary>
        /// Default interval (seconds) for IL cache statistics logging. 60 seconds allows short-term
        /// progress tracking without generating excessive log output during normal runs.
        /// IL キャッシュ内部統計ログの既定間隔（秒）。短時間の進捗把握はできる一方、
        /// 通常実行でログが過剰に増えない 60 秒を基準にする。
        /// </summary>
        public const int IL_CACHE_STATS_LOG_INTERVAL_DEFAULT_SECONDS = 60;

        /// <summary>
        /// Default TTL (hours) for IL cache entries. 12 hours enables reuse within the same day
        /// while preventing stale artefacts from lingering into the next day.
        /// IL キャッシュの既定 TTL（時間）。同日中の再実行では再利用しやすく、
        /// かつ古い成果物を翌日まで引きずりにくい 12 時間を基準にする。
        /// </summary>
        public const int IL_CACHE_TIME_TO_LIVE_DEFAULT_HOURS = 12;

        /// <summary>
        /// Line prefix used to identify and exclude MVID lines from IL output during comparison.
        /// MVID (Module Version ID) is metadata that can change on every rebuild and does not
        /// represent a meaningful IL difference.
        /// IL 出力から比較時に除外する MVID 行の接頭辞。
        /// MVID は再ビルドごとに変わり得る Module Version ID メタデータで、実行される IL 差分を直接意味しない。
        /// </summary>
        public const string IL_MVID_LINE_PREFIX = "// MVID:";

    }
}
