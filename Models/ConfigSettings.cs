using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// config.jsonの設定を保持するモデルクラス。
    /// </summary>
    public sealed class ConfigSettings
    {
        #region public properties
        /// <summary>
        /// 無視する拡張子のリスト
        /// </summary>
        public List<string> IgnoredExtensions { get; set; }

        /// <summary>
        /// 行単位で比較する拡張子のリスト
        /// </summary>
        public List<string> TextFileExtensions { get; set; }

        /// <summary>
        /// ログの最大世代数
        /// </summary>
        public int MaxLogGenerations { get; set; }

        /// <summary>
        /// 差異なしのファイルをレポートに出力するか否か
        /// </summary>
        public bool ShouldIncludeUnchangedFiles { get; set; }

        /// <summary>
        /// IL全文を出力するか否か
        /// </summary>
        public bool ShouldOutputILText { get; set; }

        /// <summary>
        /// ファイルごとの更新日時をレポートに出力するか否か
        /// </summary>
        public bool ShouldOutputFileTimestamps { get; set; }

        /// <summary>
        /// ファイル比較処理の最大並列度（0 以下または未指定で CPU 論理コア数、自動判定）。1 の場合は従来通り逐次実行。
        /// </summary>
        public int MaxParallelism { get; set; }

        /// <summary>
        /// IL 逆アセンブル結果をキャッシュして再実行時の再逆アセンブルを回避するか
        /// </summary>
        public bool EnableILCache { get; set; }

        /// <summary>
        /// IL キャッシュ格納ディレクトリ（null/空の場合は実行ディレクトリ配下 "ILCache" を既定使用）
        /// </summary>
        public string ILCacheDirectoryAbsolutePath { get; set; }

        /// <summary>
        /// IL キャッシュ統計ログの出力間隔（秒）。0 以下または未指定で 60 秒。
        /// </summary>
        public int ILCacheStatsLogIntervalSeconds { get; set; }

        /// <summary>
        /// ディスク IL キャッシュの最大ファイル数（0 以下で無制限）。超過時は最終アクセスが最も古いものから削除。
        /// </summary>
        public int ILCacheMaxDiskFileCount { get; set; }

        /// <summary>
        /// ディスク IL キャッシュのサイズ上限（MB 単位, 0 以下で無制限）。超過時はサイズが下回るまで古いものを削除。
        /// </summary>
        public int ILCacheMaxDiskMegabytes { get; set; }

        /// <summary>
        /// ネットワーク共有（NAS/SMB など）上のフォルダ比較に最適化するかどうか。
        /// true の場合、事前MD5プリウォーム/ILキャッシュ先読みをスキップし、
        /// 既定の並列度を抑制するなど、ネットワークI/O過多を避ける挙動になります。
        /// </summary>
        public bool OptimizeForNetworkShares { get; set; }

        /// <summary>
        /// 旧/新フォルダの場所から自動でネットワーク共有（UNC/ネットワークドライブなど）を検出し、
        /// ネットワーク最適化を有効化します。Windows で有効（UNC/NetworkDrive 判定）。
        /// 非Windowsでは判定精度の制約があるため既定は手動フラグと併用を推奨します。
        /// </summary>
        public bool AutoDetectNetworkShares { get; set; }
        #endregion
    }
}
