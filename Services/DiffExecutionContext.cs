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

        public string OldFolderAbsolutePath { get; }
        public string NewFolderAbsolutePath { get; }
        public string ReportsFolderAbsolutePath { get; }
        public string IlOutputFolderAbsolutePath { get; }
        public string IlOldFolderAbsolutePath { get; }
        public string IlNewFolderAbsolutePath { get; }
        public bool OptimizeForNetworkShares { get; }
        public bool DetectedNetworkOld { get; }
        public bool DetectedNetworkNew { get; }
    }
}
