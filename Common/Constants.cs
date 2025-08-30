namespace FolderDiffIL4DotNet.Common
{
    /// <summary>
    /// アプリ固有の定数を集約するクラス。
    /// （他プロジェクトへ汎用移植しない前提のリテラルはここへ）
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// アプリケーションのログを出力するディレクトリ名
        /// </summary>
        public const string LOGS_DIRECTORY_NAME = "Logs";

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
        /// IL テキスト出力用のサブフォルダ名
        /// </summary>
        public const string IL_FOLDER_NAME = "IL";

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
        public const string ILTEXT_SUFFIX = "_IL.txt";

        /// <summary>
        /// IL キャッシュ用のデフォルトサブフォルダ名
        /// </summary>
        public const string DEFAULT_IL_CACHE_DIR_NAME = "ILCache";

        /// <summary>
        /// IL キャッシュ拡張子
        /// </summary>
        public const string IL_CACHE_EXTENSION = ".ilcache";

        /// <summary>
        /// CI 等でキー入力待ちを無効化するためのコマンドラインスイッチ。
        /// </summary>
        public const string NO_PAUSE = "--no-pause";

        /// <summary>
        /// IL 出力から比較時に除外する MVID 行の接頭辞（ビルドごとに変化するため差分の対象外）。
        /// </summary>
        public const string MVID_PREFIX = "// MVID:";

        /// <summary>
        /// ログファイル名に付与する接頭辞（例: log_yyyyMMdd.log）。
        /// </summary>
        public const string LOG_FILE_PREFIX = "log_";

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
        /// ブラックリスト化判定に用いる連続失敗閾値。
        /// </summary>
        public const int DISASSEMBLE_FAIL_THRESHOLD = 3;

        /// <summary>
        /// MVID行スキップの但し書き（存在する場合のみ対象）。
        /// </summary>
        public const string NOTE_MVID_SKIP = $"Note: When diffing IL, lines starting with \"{Constants.MVID_PREFIX}\" (if present) are ignored.";

        /// <summary>
        /// MVID行込みの但し書き（存在する場合も除外せず含めて表示する）。
        /// </summary>
        public const string NOTE_MVID_INCLUDE = $"Note: The following shows the complete IL (no filters applied). Lines starting with \"{Constants.MVID_PREFIX}\" are included (not excluded) if present.";
    }
}
