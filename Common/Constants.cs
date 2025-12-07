namespace FolderDiffIL4DotNet.Common
{
    /// <summary>
    /// アプリ固有の定数を集約するクラス。
    /// （他プロジェクトへ汎用移植しない前提のリテラルはここへ）
    /// </summary>
    public static class Constants
    {

        /// <summary>
        /// アプリケーション名
        /// </summary>
        public const string APP_NAME = "FolderDiffIL4DotNet";

        /// <summary>
        /// アプリケーションのログを出力するディレクトリ名
        /// </summary>
        public const string LOGS_DIRECTORY_NAME = "Logs";

        /// <summary>
        /// ログファイル名に付与する接頭辞（例: log_yyyyMMdd.log）。
        /// </summary>
        public const string LOG_FILE_PREFIX = "log_";

        /// <summary>
        /// レポート（差分/IL など）を出力するルートディレクトリ名
        /// </summary>
        public const string REPORTS_ROOT_DIR_NAME = "Reports";

        /// <summary>
        /// アプリケーション設定の既定構成ファイル名
        /// </summary>
        public const string CONFIG_FILE_NAME = "config.json";

        /// <summary>
        /// フォルダ差分の概要を出力する Markdown レポートのファイル名
        /// </summary>
        public const string DIFF_REPORT_FILE_NAME = "diff_report.md";

        /// <summary>
        /// IL の共通ラベル
        /// </summary>
        public const string LABEL_IL = "IL";

        /// <summary>
        /// MD5 の共通ラベル
        /// </summary>
        public const string LABEL_MD5 = "MD5";

        /// <summary>
        /// IL テキスト出力用のサブフォルダ名
        /// </summary>
        public const string IL_FOLDER_NAME = LABEL_IL;

        /// <summary>
        /// 旧バージョン側（比較元）の IL 出力サブディレクトリ名
        /// </summary>
        public const string IL_OLD_SUB_DIR = "old";

        /// <summary>
        /// 新バージョン側（比較先）の IL 出力サブディレクトリ名
        /// </summary>
        public const string IL_NEW_SUB_DIR = "new";

        /// <summary>
        /// IL 比較の HTML ログファイル名（サイドバイサイド表示）。
        /// </summary>
        public const string ILTEXT_SUFFIX = "_" + LABEL_IL + ".txt";

        /// <summary>
        /// IL キャッシュ用のデフォルトサブフォルダ名
        /// </summary>
        public const string DEFAULT_IL_CACHE_DIR_NAME = LABEL_IL + "Cache";

        /// <summary>
        /// IL キャッシュ表記
        /// </summary>
        public const string LABEL_IL_CACHE = LABEL_IL + " cache";

        /// <summary>
        /// IL キャッシュ拡張子
        /// </summary>
        public const string IL_CACHE_EXTENSION = ".ilcache";

        /// <summary>
        /// ユーザープロファイル直下の .NET ホームディレクトリ名。
        /// </summary>
        public const string DOTNET_HOME_DIRNAME = ".dotnet";

        /// <summary>
        /// .NET グローバルツールのサブディレクトリ名。
        /// </summary>
        public const string DOTNET_TOOLS_DIRNAME = "tools";

        /// <summary>
        /// dotnet 実行ファイル名。
        /// </summary>
        public const string DOTNET_MUXER = "dotnet";

        /// <summary>
        /// ildasmラベル
        /// </summary>
        public const string ILDASM_LABEL = "ildasm";

        /// <summary>
        /// ildasmコマンド
        /// </summary>
        public const string DOTNET_ILDASM = "dotnet-ildasm";

        /// <summary>
        /// ilspyコマンド
        /// </summary>
        public const string ILSPY = "ilspycmd";

        /// <summary>
        /// ilspycmd の IL 出力を有効にするスイッチ（例: -il）
        /// </summary>
        public const string ILSPY_FLAG_IL = "-il";

        /// <summary>
        /// ilspycmd の出力ファイル指定スイッチ（例: -o <path>）
        /// </summary>
        public const string ILSPY_FLAG_OUTPUT = "-o";

        /// <summary>
        /// 共通フラグ: バージョン表示（ロング）
        /// </summary>
        public const string FLAG_VERSION_LONG = "--version";

        /// <summary>
        /// 共通フラグ: バージョン表示（ショート）
        /// </summary>
        public const string FLAG_VERSION_SHORT = "-v";

        /// <summary>
        /// 共通フラグ: ヘルプ（ショート）
        /// </summary>
        public const string FLAG_HELP_SHORT = "-h";

        /// <summary>
        /// CI 等でキー入力待ちを無効化するためのコマンドラインスイッチ。
        /// </summary>
        public const string NO_PAUSE = "--no-pause";

        /// <summary>
        /// ブラックリスト化判定に用いる連続失敗閾値。
        /// </summary>
        public const int DISASSEMBLE_FAIL_THRESHOLD = 3;

        /// <summary>
        /// テキスト差分の高速化を検討するサイズ閾値（バイト）
        /// </summary>
        public const int TEXT_DIFF_PARALLEL_THRESHOLD_BYTES = 512 * 1024;

        /// <summary>
        /// IL 出力から比較時に除外する MVID 行の接頭辞（ビルドごとに変化するため差分の対象外）。
        /// </summary>
        public const string MVID_PREFIX = "// MVID:";

        /// <summary>
        /// MVID行スキップの但し書き（存在する場合のみ対象）。
        /// </summary>
        public const string NOTE_MVID_SKIP = $"Note: When diffing {LABEL_IL}, lines starting with \"{MVID_PREFIX}\" (if present) are ignored.";

        /// <summary>
        /// MVID行込みの但し書き（存在する場合も除外せず含めて表示する）。
        /// </summary>
        public const string NOTE_MVID_INCLUDE = $"Note: The following shows the complete {LABEL_IL} (no filters applied). Lines starting with \"{MVID_PREFIX}\" are included (not excluded) if present.";

        /// <summary>
        /// 経過時間表示
        /// </summary>
        public const string LOG_ELAPSED_TIME = "Elapsed Time: {0}";

        /// <summary>
        /// ログプレフィックス: INFO
        /// </summary>
        public const string LOG_PREFIX_INFO = "[INFO]";

        /// <summary>
        /// ログプレフィックス: WARNING
        /// </summary>
        public const string LOG_PREFIX_WARNING = "[WARNING]";

        /// <summary>
        /// ログプレフィックス: ERROR
        /// </summary>
        public const string LOG_PREFIX_ERROR = "[ERROR]";

        /// <summary>
        /// レポートタイトル
        /// </summary>
        public const string REPORT_TITLE = "# Folder Diff Report";

        /// <summary>
        /// レポートヘッダ: アプリバージョン
        /// </summary>
        public const string REPORT_HEADER_APP_VERSION = "- App Version: " + APP_NAME + " {0}";

        /// <summary>
        /// レポートヘッダ: 旧フォルダパス
        /// </summary>
        public const string REPORT_HEADER_OLD = "- Old: {0}";

        /// <summary>
        /// レポートヘッダ: 新フォルダパス
        /// </summary>
        public const string REPORT_HEADER_NEW = "- New: {0}";

        /// <summary>
        /// レポートヘッダ: 無視拡張子一覧
        /// </summary>
        public const string REPORT_HEADER_IGNORED_EXTENSIONS = "- Ignored Extensions: {0}";

        /// <summary>
        /// レポートヘッダ: テキスト拡張子一覧
        /// </summary>
        public const string REPORT_HEADER_TEXT_EXTENSIONS = "- Text File Extensions: {0}";

        /// <summary>
        /// レポートヘッダ: 経過時間
        /// </summary>
        public const string REPORT_HEADER_ELAPSED_TIME = "- " + LOG_ELAPSED_TIME;

        /// <summary>
        /// レポート内でのリスト結合区切り
        /// </summary>
        public const string REPORT_LIST_SEPARATOR = ", ";

        /// <summary>
        /// レジェンドのヘッダ
        /// </summary>
        public const string REPORT_LEGEND_HEADER = "- Legend:";

        /// <summary>
        /// レジェンド: 共通サフィックス
        /// </summary>
        public const string REPORT_LEGEND_SUFFIX_MATCH_MISMATCH = "match / mismatch";

        /// <summary>
        /// レジェンド: MD5
        /// </summary>
        public const string REPORT_LEGEND_MD5 = "  - `{0}` / `{1}`: " + LABEL_MD5 + " hash " + REPORT_LEGEND_SUFFIX_MATCH_MISMATCH;

        /// <summary>
        /// レジェンド: IL
        /// </summary>
        public const string REPORT_LEGEND_IL = "  - `{0}` / `{1}`: " + LABEL_IL + "(Intermediate Language) " + REPORT_LEGEND_SUFFIX_MATCH_MISMATCH;

        /// <summary>
        /// レジェンド: テキスト
        /// </summary>
        public const string REPORT_LEGEND_TEXT = "  - `{0}` / `{1}`: Text " + REPORT_LEGEND_SUFFIX_MATCH_MISMATCH;

        /// <summary>
        /// レポートマーカー: Ignored
        /// </summary>
        public const string REPORT_MARKER_IGNORED = "[ x ]";

        /// <summary>
        /// レポートラベル: Ignored
        /// </summary>
        public const string REPORT_LABEL_IGNORED = "Ignored";

        /// <summary>
        /// レポートマーカー: Unchanged
        /// </summary>
        public const string REPORT_MARKER_UNCHANGED = "[ = ]";

        /// <summary>
        /// レポートラベル: Unchanged
        /// </summary>
        public const string REPORT_LABEL_UNCHANGED = "Unchanged";

        /// <summary>
        /// レポートマーカー: Added
        /// </summary>
        public const string REPORT_MARKER_ADDED = "[ + ]";

        /// <summary>
        /// レポートラベル: Added
        /// </summary>
        public const string REPORT_LABEL_ADDED = "Added";

        /// <summary>
        /// レポートマーカー: Removed
        /// </summary>
        public const string REPORT_MARKER_REMOVED = "[ - ]";

        /// <summary>
        /// レポートラベル: Removed
        /// </summary>
        public const string REPORT_LABEL_REMOVED = "Removed";

        /// <summary>
        /// レポートマーカー: Modified
        /// </summary>
        public const string REPORT_MARKER_MODIFIED = "[ * ]";

        /// <summary>
        /// レポートラベル: Modified
        /// </summary>
        public const string REPORT_LABEL_MODIFIED = "Modified";

        /// <summary>
        /// レポートラベル: Compared
        /// </summary>
        public const string REPORT_LABEL_COMPARED = "Compared";

        /// <summary>
        /// レポートセクションの共通プレフィックス
        /// </summary>
        public const string REPORT_SECTION_PREFIX = "\n## ";

        /// <summary>
        /// レポートセクション: Files 接尾辞
        /// </summary>
        public const string REPORT_SECTION_FILES_SUFFIX = " Files";

        /// <summary>
        /// レポートセクション: Ignored Files
        /// </summary>
        public const string REPORT_SECTION_IGNORED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_IGNORED + " " + REPORT_LABEL_IGNORED + REPORT_SECTION_FILES_SUFFIX;

        /// <summary>
        /// レポートセクション: Unchanged Files
        /// </summary>
        public const string REPORT_SECTION_UNCHANGED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_UNCHANGED + " " + REPORT_LABEL_UNCHANGED + REPORT_SECTION_FILES_SUFFIX;

        /// <summary>
        /// レポートセクション: Added Files
        /// </summary>
        public const string REPORT_SECTION_ADDED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_ADDED + " " + REPORT_LABEL_ADDED + REPORT_SECTION_FILES_SUFFIX;

        /// <summary>
        /// レポートセクション: Removed Files
        /// </summary>
        public const string REPORT_SECTION_REMOVED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_REMOVED + " " + REPORT_LABEL_REMOVED + REPORT_SECTION_FILES_SUFFIX;

        /// <summary>
        /// レポートセクション: Modified Files
        /// </summary>
        public const string REPORT_SECTION_MODIFIED_FILES = REPORT_SECTION_PREFIX + REPORT_MARKER_MODIFIED + " " + REPORT_LABEL_MODIFIED + REPORT_SECTION_FILES_SUFFIX;

        /// <summary>
        /// ファイルの位置ラベル（旧）
        /// </summary>
        public const string REPORT_LOCATION_OLD = "(old)";

        /// <summary>
        /// ファイルの位置ラベル（新）
        /// </summary>
        public const string REPORT_LOCATION_NEW = "(new)";

        /// <summary>
        /// ファイルの位置ラベル（旧/新）
        /// </summary>
        public const string REPORT_LOCATION_BOTH = "(old/new)";

        /// <summary>
        /// ファイルの位置ラベル（旧・タイトルケース）
        /// </summary>
        public const string REPORT_LOCATION_OLD_TITLE = "(Old)";

        /// <summary>
        /// ファイルの位置ラベル（新・タイトルケース）
        /// </summary>
        public const string REPORT_LOCATION_NEW_TITLE = "(New)";

        /// <summary>
        /// Ignored ファイル行のフォーマット
        /// </summary>
        public const string REPORT_IGNORED_FILE_ITEM = "- " + REPORT_MARKER_IGNORED + " {0}";

        /// <summary>
        /// Ignored/タイムスタンプの HTML ラッパー
        /// </summary>
        public const string REPORT_TIMESTAMP_HTML_WRAPPER = " <u>({0})</u>";

        /// <summary>
        /// タイムスタンプ結合時の区切り
        /// </summary>
        public const string REPORT_TIMESTAMP_SEPARATOR = ", ";

        /// <summary>
        /// タイムスタンプ: 旧ファイル
        /// </summary>
        public const string REPORT_TIMESTAMP_UPDATED_OLD = "updated_old: {0}";

        /// <summary>
        /// タイムスタンプ: 新ファイル
        /// </summary>
        public const string REPORT_TIMESTAMP_UPDATED_NEW = "updated_new: {0}";

        /// <summary>
        /// タイムスタンプ: 新ファイルのみ
        /// </summary>
        public const string REPORT_TIMESTAMP_UPDATED = "updated: {0}";

        /// <summary>
        /// Unchanged ファイル行（タイムスタンプあり）
        /// </summary>
        public const string REPORT_UNCHANGED_ITEM_WITH_TIMESTAMP = "- " + REPORT_MARKER_UNCHANGED + " {0} <u>{1}</u> `{2}`";

        /// <summary>
        /// Unchanged ファイル行（タイムスタンプなし）
        /// </summary>
        public const string REPORT_UNCHANGED_ITEM = "- " + REPORT_MARKER_UNCHANGED + " {0} `{1}`";

        /// <summary>
        /// Unchanged/タイムスタンプ（旧+新）
        /// </summary>
        public const string REPORT_UNCHANGED_TIMESTAMP_BOTH = "(updated_old: {0}, updated_new: {1})";

        /// <summary>
        /// Unchanged/タイムスタンプ（新のみ）
        /// </summary>
        public const string REPORT_UNCHANGED_TIMESTAMP_NEW = "(updated: {0})";

        /// <summary>
        /// Added ファイル行（タイムスタンプあり）
        /// </summary>
        public const string REPORT_ADDED_ITEM_WITH_TIMESTAMP = "- " + REPORT_MARKER_ADDED + " {0} <u>(updated: {1})</u>";

        /// <summary>
        /// Added ファイル行（タイムスタンプなし）
        /// </summary>
        public const string REPORT_ADDED_ITEM = "- " + REPORT_MARKER_ADDED + " {0}";

        /// <summary>
        /// Removed ファイル行（タイムスタンプあり）
        /// </summary>
        public const string REPORT_REMOVED_ITEM_WITH_TIMESTAMP = "- " + REPORT_MARKER_REMOVED + " {0} <u>(updated: {1})</u>";

        /// <summary>
        /// Removed ファイル行（タイムスタンプなし）
        /// </summary>
        public const string REPORT_REMOVED_ITEM = "- " + REPORT_MARKER_REMOVED + " {0}";

        /// <summary>
        /// Modified ファイル行（タイムスタンプあり）
        /// </summary>
        public const string REPORT_MODIFIED_ITEM_WITH_TIMESTAMP = "- " + REPORT_MARKER_MODIFIED + " {0} <u>(updated_old: {1}, updated_new: {2})</u> `{3}`";

        /// <summary>
        /// Modified ファイル行（タイムスタンプなし）
        /// </summary>
        public const string REPORT_MODIFIED_ITEM = "- " + REPORT_MARKER_MODIFIED + " {0} `{1}`";

        /// <summary>
        /// レポートフッタ: Summary セクション
        /// </summary>
        public const string REPORT_SECTION_SUMMARY = REPORT_SECTION_PREFIX + "Summary";

        /// <summary>
        /// Summary: ラベル幅
        /// </summary>
        public const int REPORT_SUMMARY_LABEL_WIDTH = 10;

        /// <summary>
        /// Summary: ラベル付き共通フォーマット
        /// </summary>
        public const string REPORT_SUMMARY_ITEM_FORMAT = "- {0,-10}: {1}";

        /// <summary>
        /// Summary: Compared
        /// </summary>
        public const string REPORT_SUMMARY_COMPARED = "- {0,-10}: {1} " + REPORT_LOCATION_OLD_TITLE + " vs {2} " + REPORT_LOCATION_NEW_TITLE;

        /// <summary>
        /// Summary: WARNING 行
        /// </summary>
        public const string REPORT_WARNING_LINE = "**WARNING:** {0}";

        /// <summary>
        /// MD5Mismatch警告文言
        /// </summary>
        public const string WARNING_MD5_MISMATCH = $"One or more files were classified as `{LABEL_MD5}Mismatch`. Manual review is recommended because only an {LABEL_MD5} hash comparison was possible.";

        /// <summary>
        /// バージョン文字列取得失敗時の共通メッセージ
        /// </summary>
        public const string ERROR_FAILED_TO_GET_VERSION = "Failed to obtain version string for";

        /// <summary>
        /// バージョン取得失敗時のログメッセージの前半
        /// </summary>
        public const string LOG_FAILED_TO_GET_VERSION = "Failed to get version";

        /// <summary>
        /// バージョン取得失敗ログ詳細
        /// </summary>
        public const string LOG_FAILED_TO_GET_VERSION_DETAIL = "Failed to get version ({0}='{1}', {2}='{3}'): {4}";

        /// <summary>
        /// 逆アセンブラバージョン取得失敗ログ (prefetch)
        /// </summary>
        public const string LOG_FAILED_TO_GET_VERSION_FOR_COMMAND = "Failed to get version for disassemble command '{0}' (candidate: '{1}'). Skipping.";

        /// <summary>
        /// 逆アセンブラのバージョン決定失敗メッセージ共通部分
        /// </summary>
        public const string ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION = "Failed to determine disassembler version";

        /// <summary>
        /// 逆アセンブラのバージョン決定に失敗した際のメッセージ（ラベル未指定）
        /// </summary>
        public const string ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION_EMPTY = ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION + ": empty command label.";

        /// <summary>
        /// 逆アセンブラのバージョン決定に失敗した際のメッセージ（ラベル付き）
        /// </summary>
        public const string ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION_FOR_LABEL = ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION + " for label:";

        /// <summary>
        /// 逆アセンブラのバージョン決定に失敗した際のメッセージ（無効なラベル）
        /// </summary>
        public const string ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION_INVALID = ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION + ": invalid command label '{0}'.";

        /// <summary>
        /// ildasm/ilspyインストールガイダンス
        /// </summary>
        public const string GUIDANCE_INSTALL_DISASSEMBLER =
            DOTNET_ILDASM + " was not found or failed to run.\n" +
            "If it's not installed, install it with:\n" +
            "  " + DOTNET_MUXER + " tool install -g " + DOTNET_ILDASM + "\n" +
            "Also ensure that ~/" + DOTNET_HOME_DIRNAME + "/" + DOTNET_TOOLS_DIRNAME + " is included in your PATH.\n" +
            "Alternatively, you can install " + ILSPY + " and we will use it automatically:\n" +
            "  " + DOTNET_MUXER + " tool install -g " + ILSPY;

        /// <summary>
        /// ildasm実行失敗時の例外フォーマット
        /// </summary>
        public const string ERROR_EXECUTE_ILDASM = "Failed to execute " + ILDASM_LABEL + " for file: {0}. {1}{2}";

        /// <summary>
        /// 例外のルート原因フォーマット
        /// </summary>
        public const string INFO_ROOT_CAUSE_FORMAT = " RootCause: {0}";

        /// <summary>
        /// ildasm失敗エラー
        /// </summary>
        public const string ERROR_ILDASM_FAILED = ILDASM_LABEL + " failed (exit {0}) with command: {1} {2} in {3}\nFile: {4}\nStderr: {5}";

        /// <summary>
        /// 逆アセンブラ起動失敗ログ
        /// </summary>
        public const string LOG_FAILED_TO_START_DISASSEMBLER = "Failed to start disassembler tool '{0}': {1}";

        /// <summary>
        /// 逆アセンブラ準備時の予期せぬエラー
        /// </summary>
        public const string LOG_UNEXPECTED_ERROR_PREPARING_DISASSEMBLER = "Unexpected error while preparing to run '{0}': {1}";

        /// <summary>
        /// タイムスタンプ取得時のパスエラー
        /// </summary>
        public const string ERROR_TIMESTAMP_INVALID_PATH = "Invalid value provided for timestamp retrieval: {0}";

        /// <summary>
        /// null/空白の場合のメッセージ
        /// </summary>
        public const string ERROR_FILE_RELATIVE_PATH_EMPTY = "{0} cannot be null or whitespace.";

        /// <summary>
        /// Configファイル未発見
        /// </summary>
        public const string ERROR_CONFIG_NOT_FOUND = "Config file not found: {0}";

        /// <summary>
        /// Config解析失敗
        /// </summary>
        public const string ERROR_CONFIG_PARSE_FAILED = "Failed to parse the config file.";

        /// <summary>
        /// IL出力失敗ログ
        /// </summary>
        public const string ERROR_FAILED_TO_OUTPUT_IL = $"Failed to output {LABEL_IL}.";

        /// <summary>
        /// IL テキスト出力失敗時のメッセージ
        /// </summary>
        public const string ERROR_FAILED_TO_OUTPUT_IL_TEXT = $"Failed to output {LABEL_IL} Text.";

        /// <summary>
        /// 差分エラーメッセージ
        /// </summary>
        public const string ERROR_DIFFING = "An error occurred while diffing '{0}' and '{1}'.";

        /// <summary>
        /// レポート出力失敗
        /// </summary>
        public const string ERROR_FAILED_TO_OUTPUT_REPORT = "Failed to output report to '{0}'";

        /// <summary>
        /// 最大並列度エラー
        /// </summary>
        public const string ERROR_MAX_PARALLEL = "The maximum degree of parallelism must be 1 or greater.";

        /// <summary>
        /// 進捗範囲エラー
        /// </summary>
        public const string ERROR_PROGRESS_OUT_OF_RANGE = "Progress must be between 0.00 and 100.00. Actual: {0:F2}";

        /// <summary>
        /// 進捗表示
        /// </summary>
        public const string LOG_PROGRESS = "Progress: {0}%";

        /// <summary>
        /// 進捗処理中表示
        /// </summary>
        public const string LOG_PROGRESS_KEEPALIVE = LOG_PROGRESS + " (processing...)";

        /// <summary>
        /// MD5 プリコンピュート共通プレフィックス
        /// </summary>
        public const string LOG_PRECOMPUTE_MD5_PREFIX = $"Precompute {LABEL_MD5}";

        /// <summary>
        /// MD5 プリコンピュート開始ログ
        /// </summary>
        public const string LOG_PRECOMPUTE_MD5_START = LOG_PRECOMPUTE_MD5_PREFIX + ": starting for {0} files ({1}={2})";

        /// <summary>
        /// MD5 プリコンピュート進捗ログ
        /// </summary>
        public const string LOG_PRECOMPUTE_MD5_PROGRESS = LOG_PRECOMPUTE_MD5_PREFIX + ": {0}/{1} ({2}%)";

        /// <summary>
        /// MD5 プリコンピュート完了ログ
        /// </summary>
        public const string LOG_PRECOMPUTE_MD5_COMPLETE = LOG_PRECOMPUTE_MD5_PREFIX + ": completed for {0} files";

        /// <summary>
        /// MD5 プリコンピュート失敗ログ
        /// </summary>
        public const string LOG_FAILED_PRECOMPUTE_MD5_FILE = "Failed to " + LOG_PRECOMPUTE_MD5_PREFIX + " for file '{0}'. This file will be skipped in the cache.";

        /// <summary>
        /// MD5ハッシュ計算失敗ログ
        /// </summary>
        public const string LOG_FAILED_PRECOMPUTE_MD5_HASHES = "Failed to precompute " + LABEL_MD5 + " hashes: {0}";

        /// <summary>
        /// 事前計算対象ログ
        /// </summary>
        public const string LOG_PRECOMPUTE_TARGETS = "Precompute targets: totalFiles={0}, {1}={2}";

        /// <summary>
        /// IL関連ハッシュ失敗ログ
        /// </summary>
        public const string LOG_FAILED_PRECOMPUTE_IL_HASHES = "Failed to precompute " + LABEL_IL + " related hashes: {0}";

        /// <summary>
        /// IL キャッシュディレクトリ作成失敗
        /// </summary>
        public const string LOG_FAILED_CREATE_IL_CACHE_DIR = "Failed to create " + LABEL_IL_CACHE + " directory '{0}': {1}";

        /// <summary>
        /// IL キャッシュファイル操作失敗フォーマット
        /// </summary>
        public const string LOG_FAILED_IL_CACHE_FILE_FORMAT = "Failed to {0} " + LABEL_IL_CACHE + " file '{1}': {2}";

        /// <summary>
        /// キャッシュファイル削除失敗ログ
        /// </summary>
        public const string LOG_FAILED_DELETE_CACHE_FILE = "Failed to delete cache file: {0}";

        /// <summary>
        /// LRU 除外時のディスクキャッシュ削除失敗ログ
        /// </summary>
        public const string LOG_FAILED_REMOVE_DISK_CACHE_FILE = "Failed to remove disk cache file '{0}' during LRU eviction.";

        /// <summary>
        /// ディスククォータ調整ログ
        /// </summary>
        public const string LOG_DISK_QUOTA_TRIM = "Disk quota trim: removed={0}, remain={1}, bytes={2}";

        /// <summary>
        /// ILキャッシュプリフェッチ共通プレフィックス
        /// </summary>
        public const string LOG_PREFETCH_IL_CACHE_PREFIX = "Prefetch " + LABEL_IL_CACHE;

        /// <summary>
        /// ILキャッシュプリフェッチ開始
        /// </summary>
        public const string LOG_PREFETCH_IL_CACHE_START = LOG_PREFETCH_IL_CACHE_PREFIX + ": starting for {0} .NET assemblies ({1}={2})";

        /// <summary>
        /// ILキャッシュプリフェッチ失敗
        /// </summary>
        public const string LOG_FAILED_PREFETCH_IL_CACHE = "Failed to prefetch " + LABEL_IL_CACHE + " for assembly '{0}': {1}";

        /// <summary>
        /// ILキャッシュプリフェッチ進捗
        /// </summary>
        public const string LOG_PREFETCH_IL_CACHE_PROGRESS = LOG_PREFETCH_IL_CACHE_PREFIX + ": {0}/{1} ({2}%), hits={3}";

        /// <summary>
        /// ILキャッシュプリフェッチ完了
        /// </summary>
        public const string LOG_PREFETCH_IL_CACHE_COMPLETE = LOG_PREFETCH_IL_CACHE_PREFIX + ": completed. hits={0}, stores={1}";

        /// <summary>
        /// ILキャッシュ取得失敗
        /// </summary>
        public const string LOG_FAILED_GET_IL_FROM_CACHE = "Failed to get " + LABEL_IL + " from cache for {0} with command {1}: {2}";

        /// <summary>
        /// ILキャッシュ設定失敗
        /// </summary>
        public const string LOG_FAILED_SET_IL_CACHE = "Failed to set " + LABEL_IL_CACHE + " for {0} with command {1}: {2}";

        /// <summary>
        /// ASCII一時コピー作成失敗
        /// </summary>
        public const string LOG_FAILED_CREATE_ASCII_TEMP_COPY = "Failed to create ASCII temp copy for '{0}': {1}";

        /// <summary>
        /// ネットワーク最適化ログ (<see cref="FolderDiffService"/>)
        /// </summary>
        public const string LOG_NETWORK_OPTIMIZED_SKIP_IL = $"Network-optimized mode: skip {LABEL_IL} precompute to reduce network I/O.";

        /// <summary>
        /// ネットワーク共有最適化ログ (<see cref="ILOutputService"/>)
        /// </summary>
        public const string LOG_OPTIMIZE_FOR_NETWORK_SHARES_SKIP = $"OptimizeForNetworkShares=true: Skip {LABEL_IL} precompute/prefetch to reduce network I/O.";

        /// <summary>
        /// 実行モードログ
        /// </summary>
        public const string LOG_EXECUTION_MODE = "Execution mode: {0} ({1})";

        /// <summary>
        /// 並列度ログ
        /// </summary>
        public const string LOG_PARALLEL_DIFF_PROCESSING = "Parallel diff processing: maxParallel={0} (configured={1}, OptimizeForNetworkShares={2}, logical processors={3})";

        /// <summary>
        /// ファイル発見ログ
        /// </summary>
        public const string LOG_DISCOVERY_COMPLETE = "Discovery complete: old={0}, new={1}, union(relative)={2}";

        /// <summary>
        /// IL出力フォルダ準備ログ
        /// </summary>
        public const string LOG_PREPARED_IL_OUTPUT_DIRS = "Prepared " + LABEL_IL + " output directories: old='{0}', new='{1}'";

        /// <summary>
        /// 古いログファイル削除ログ
        /// </summary>
        public const string LOG_DELETED_OLD_LOG_FILE = "Deleted old log file: {0}.";

        /// <summary>
        /// 古いログファイルクリーンアップ失敗
        /// </summary>
        public const string LOG_FAILED_CLEANUP_OLD_LOGS = "Failed to clean up old log files in '{0}'.";

        /// <summary>
        /// レポート生成スピナーのラベル。
        /// </summary>
        public const string SPINNER_LABEL_GENERATING_REPORT = "Generating report";

        /// <summary>
        /// レポート生成完了ログ。
        /// </summary>
        public const string LOG_REPORT_GENERATION_COMPLETED = "Report generation completed.";

        /// <summary>
        /// フォルダ比較スピナーのラベル。
        /// </summary>
        public const string SPINNER_LABEL_FOLDER_DIFF = "Diffing folders";

        /// <summary>
        /// フォルダ比較完了ログ。
        /// </summary>
        public const string LOG_FOLDER_DIFF_COMPLETED = "Folder diff completed.";

        /// <summary>
        /// IL diff 失敗ログ
        /// </summary>
        public const string LOG_IL_DIFF_FAILED = LABEL_IL + " diff failed for '{0}'.";

        /// <summary>
        /// ロガー初期化メッセージ
        /// </summary>
        public const string INFO_INITIALIZING_LOGGER = "[INFO] Initializing logger...";

        /// <summary>
        /// Logger initialized
        /// </summary>
        public const string LOG_LOGGER_INITIALIZED = "Logger initialized.";

        /// <summary>
        /// アプリバージョン
        /// </summary>
        public const string LOG_APPLICATION_VERSION = "Application version: {0}";

        /// <summary>
        /// 引数検証開始
        /// </summary>
        public const string LOG_VALIDATING_ARGS = "Validating command line arguments...";

        /// <summary>
        /// 引数不足
        /// </summary>
        public const string ERROR_INSUFFICIENT_ARGUMENTS = "Insufficient arguments.";

        /// <summary>
        /// 引数null/空
        /// </summary>
        public const string ERROR_ARGUMENTS_NULL_OR_EMPTY = "One or more required arguments are null or empty.";

        /// <summary>
        /// 引数エラーの使用例
        /// </summary>
        public const string ERROR_INVALID_ARGUMENTS_USAGE = "Invalid arguments. Usage: " + APP_NAME + $" <oldFolderAbsolutePath> <newFolderAbsolutePath> <reportLabel> [{NO_PAUSE}]";

        /// <summary>
        /// reportLabelエラー
        /// </summary>
        public const string ERROR_INVALID_REPORT_LABEL = "The value '{0}', provided as the third argument (reportLabel), is invalid as a folder name.";

        /// <summary>
        /// 旧フォルダ存在せず
        /// </summary>
        public const string ERROR_OLD_FOLDER_NOT_FOUND = "The old folder path does not exist: {0}";

        /// <summary>
        /// 新フォルダ存在せず
        /// </summary>
        public const string ERROR_NEW_FOLDER_NOT_FOUND = "The new folder path does not exist: {0}";

        /// <summary>
        /// レポートフォルダ既存
        /// </summary>
        public const string ERROR_REPORT_FOLDER_EXISTS = "The report folder already exists: {0}. Provide a different report label.";

        /// <summary>
        /// 引数検証完了
        /// </summary>
        public const string LOG_ARGS_VALIDATION_COMPLETED = "Command line arguments validation completed.";

        /// <summary>
        /// 設定読み込み開始
        /// </summary>
        public const string LOG_LOADING_CONFIGURATION = "Loading configuration...";

        /// <summary>
        /// 設定読み込み完了
        /// </summary>
        public const string LOG_CONFIGURATION_LOADED = "Configuration loaded successfully.";

        /// <summary>
        /// アプリ開始ログ
        /// </summary>
        public const string LOG_APP_STARTING = "Starting " + APP_NAME + "...";

        /// <summary>
        /// アプリ正常終了
        /// </summary>
        public const string LOG_APP_FINISHED = APP_NAME + " finished without errors. See Reports folder for details.";

        /// <summary>
        /// エラーログパス
        /// </summary>
        public const string LOG_ERROR_DETAILS_PATH = "Error details logged to: {0}";

        /// <summary>
        /// 終了キープロンプト
        /// </summary>
        public const string INFO_PRESS_ANY_KEY = "[INFO] Press any key to exit...";

        /// <summary>
        /// キープロンプトエラー
        /// </summary>
        public const string ERROR_KEY_PROMPT = "An error occurred during key prompt.";

        /// <summary>
        /// テキスト差分比較時のチャンクサイズ（バイト）
        /// </summary>
        public const int TEXT_DIFF_CHUNK_SIZE_BYTES = 64 * 1024;

        /// <summary>
        /// IL キャッシュのメモリ最大エントリ数（既定値）
        /// </summary>
        public const int IL_CACHE_MAX_MEMORY_ENTRIES_DEFAULT = 2000;
    }
}
