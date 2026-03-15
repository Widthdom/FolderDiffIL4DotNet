using System;
using System.IO;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// 1 回の差分実行にひもづくパス群と実行モードを保持します。
    /// </summary>
    public sealed class DiffExecutionContext
    {
        private const string IL_OLD_SUB_DIR = "old";
        private const string IL_NEW_SUB_DIR = "new";

        /// <summary>
        /// 実行コンテキストを初期化し、IL 出力先サブフォルダのパスを構築します。
        /// </summary>
        /// <param name="oldFolderAbsolutePath">比較元フォルダの絶対パス。</param>
        /// <param name="newFolderAbsolutePath">比較先フォルダの絶対パス。</param>
        /// <param name="reportsFolderAbsolutePath">レポート出力先フォルダの絶対パス。</param>
        /// <param name="optimizeForNetworkShares">ネットワーク共有向け最適化を行うか。</param>
        /// <param name="detectedNetworkOld">比較元フォルダがネットワークパスと判定されたか。</param>
        /// <param name="detectedNetworkNew">比較先フォルダがネットワークパスと判定されたか。</param>
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

        /// <summary>
        /// 比較元フォルダの絶対パス。
        /// </summary>
        public string OldFolderAbsolutePath { get; }

        /// <summary>
        /// 比較先フォルダの絶対パス。
        /// </summary>
        public string NewFolderAbsolutePath { get; }

        /// <summary>
        /// レポート出力先フォルダの絶対パス。
        /// </summary>
        public string ReportsFolderAbsolutePath { get; }

        /// <summary>
        /// IL 出力先フォルダ（old/new 共通親）の絶対パス。
        /// </summary>
        public string IlOutputFolderAbsolutePath { get; }

        /// <summary>
        /// 比較元 IL 出力先サブフォルダの絶対パス。
        /// </summary>
        public string IlOldFolderAbsolutePath { get; }

        /// <summary>
        /// 比較先 IL 出力先サブフォルダの絶対パス。
        /// </summary>
        public string IlNewFolderAbsolutePath { get; }

        /// <summary>
        /// ネットワーク共有向け最適化を行うか（自動検出または設定による統合フラグ）。
        /// </summary>
        public bool OptimizeForNetworkShares { get; }

        /// <summary>
        /// 比較元フォルダがネットワークパスと自動検出されたか。
        /// </summary>
        public bool DetectedNetworkOld { get; }

        /// <summary>
        /// 比較先フォルダがネットワークパスと自動検出されたか。
        /// </summary>
        public bool DetectedNetworkNew { get; }
    }
}
