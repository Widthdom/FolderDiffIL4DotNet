using System;
using System.IO;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Holds path sets and execution modes for a single diff run.
    /// 1 回の差分実行にひもづくパス群と実行モードを保持します。
    /// </summary>
    public sealed class DiffExecutionContext
    {
        private const string IL_OLD_SUB_DIR = "old";
        private const string IL_NEW_SUB_DIR = "new";

        /// <summary>
        /// Initializes a new instance of <see cref="DiffExecutionContext"/> and derives IL output sub-paths.
        /// <see cref="DiffExecutionContext"/> の新しいインスタンスを初期化し、IL 出力サブパスを導出します。
        /// </summary>
        /// <param name="oldFolderAbsolutePath">Absolute path to the baseline (old) folder. / 旧フォルダの絶対パス。</param>
        /// <param name="newFolderAbsolutePath">Absolute path to the comparison (new) folder. / 新フォルダの絶対パス。</param>
        /// <param name="reportsFolderAbsolutePath">Absolute path to the report output folder. / レポート出力先の絶対パス。</param>
        /// <param name="optimizeForNetworkShares">Whether network-share optimizations are enabled. / ネットワーク共有最適化の有効フラグ。</param>
        /// <param name="detectedNetworkOld">Whether the old folder was detected as a network share. / 旧フォルダがネットワーク共有と判定されたか。</param>
        /// <param name="detectedNetworkNew">Whether the new folder was detected as a network share. / 新フォルダがネットワーク共有と判定されたか。</param>
        public DiffExecutionContext(
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            bool optimizeForNetworkShares,
            bool detectedNetworkOld,
            bool detectedNetworkNew)
        {
            ArgumentNullException.ThrowIfNull(oldFolderAbsolutePath);
            ArgumentNullException.ThrowIfNull(newFolderAbsolutePath);
            ArgumentNullException.ThrowIfNull(reportsFolderAbsolutePath);

            OldFolderAbsolutePath = oldFolderAbsolutePath;
            NewFolderAbsolutePath = newFolderAbsolutePath;
            ReportsFolderAbsolutePath = reportsFolderAbsolutePath;
            OptimizeForNetworkShares = optimizeForNetworkShares;
            DetectedNetworkOld = detectedNetworkOld;
            DetectedNetworkNew = detectedNetworkNew;

            IlOutputFolderAbsolutePath = Path.Combine(ReportsFolderAbsolutePath, Constants.LABEL_IL);
            IlOldFolderAbsolutePath = Path.Combine(IlOutputFolderAbsolutePath, IL_OLD_SUB_DIR);
            IlNewFolderAbsolutePath = Path.Combine(IlOutputFolderAbsolutePath, IL_NEW_SUB_DIR);
        }

        /// <summary>Absolute path to the baseline (old) folder. / 旧（ベースライン）フォルダの絶対パス。</summary>
        public string OldFolderAbsolutePath { get; }
        /// <summary>Absolute path to the comparison (new) folder. / 新（比較対象）フォルダの絶対パス。</summary>
        public string NewFolderAbsolutePath { get; }
        /// <summary>Absolute path to the report output folder. / レポート出力先フォルダの絶対パス。</summary>
        public string ReportsFolderAbsolutePath { get; }
        /// <summary>Absolute path to the IL output root folder. / IL 出力ルートフォルダの絶対パス。</summary>
        public string IlOutputFolderAbsolutePath { get; }
        /// <summary>Absolute path to the IL output sub-folder for old-side files. / 旧側 IL 出力サブフォルダの絶対パス。</summary>
        public string IlOldFolderAbsolutePath { get; }
        /// <summary>Absolute path to the IL output sub-folder for new-side files. / 新側 IL 出力サブフォルダの絶対パス。</summary>
        public string IlNewFolderAbsolutePath { get; }
        /// <summary>Whether network-share optimizations are enabled. / ネットワーク共有最適化の有効フラグ。</summary>
        public bool OptimizeForNetworkShares { get; }
        /// <summary>Whether the old folder was detected as a network share. / 旧フォルダがネットワーク共有と検出されたか。</summary>
        public bool DetectedNetworkOld { get; }
        /// <summary>Whether the new folder was detected as a network share. / 新フォルダがネットワーク共有と検出されたか。</summary>
        public bool DetectedNetworkNew { get; }
    }
}
