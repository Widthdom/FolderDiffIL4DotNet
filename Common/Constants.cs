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
        /// IL の共通ラベル
        /// </summary>
        public const string LABEL_IL = "IL";

        /// <summary>
        /// MD5 の共通ラベル
        /// </summary>
        public const string LABEL_MD5 = "MD5";

        /// <summary>
        /// IL キャッシュ用のデフォルトサブフォルダ名
        /// </summary>
        public const string DEFAULT_IL_CACHE_DIR_NAME = LABEL_IL + "Cache";

        /// <summary>
        /// IL キャッシュ表記
        /// </summary>
        public const string LABEL_IL_CACHE = LABEL_IL + " cache";

        /// <summary>
        /// dotnet 実行ファイル名。
        /// </summary>
        public const string DOTNET_MUXER = "dotnet";

        /// <summary>
        /// ildasmコマンド
        /// </summary>
        public const string DOTNET_ILDASM = "dotnet-ildasm";

        /// <summary>
        /// ilspyコマンド
        /// </summary>
        public const string ILSPY_CMD = "ilspycmd";

        /// <summary>
        /// 経過時間表示
        /// </summary>
        public const string LOG_ELAPSED_TIME = "Elapsed Time: {0}";

        /// <summary>
        /// 差分エラーメッセージ
        /// </summary>
        public const string ERROR_DIFFING = "An error occurred while diffing '{0}' and '{1}'.";

        /// <summary>
        /// 最大並列度エラー
        /// </summary>
        public const string ERROR_MAX_PARALLEL = "The maximum degree of parallelism must be 1 or greater.";

    }
}
