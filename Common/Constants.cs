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

    }
}
