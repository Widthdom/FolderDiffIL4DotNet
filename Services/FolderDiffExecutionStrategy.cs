using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Determines discovery criteria and parallelism policy for folder diffs.
    /// フォルダ差分の探索条件と並列実行ポリシーを決定します。
    /// </summary>
    public sealed class FolderDiffExecutionStrategy : IFolderDiffExecutionStrategy
    {
        /// <summary>
        /// Maximum parallelism when network-share optimisation is active. Limited to 8
        /// because NAS/SMB servers typically throttle or error with too many simultaneous
        /// connections; empirical testing shows 8 concurrent workers as a stable upper bound.
        /// ネットワーク最適化時の自動並列度上限。NAS/SMB 環境では同時接続数が多すぎるとスロットリングや
        /// エラーが発生するため、実測で安定する 8 並列を上限としています。
        /// </summary>
        private const int MAX_PARALLEL_NETWORK_LIMIT = 8;

        /// <summary>
        /// Multiplier applied to <see cref="Environment.ProcessorCount"/> for local I/O-bound
        /// workloads. File hashing and IL comparison are I/O-dominant, so using twice the core
        /// count keeps the CPU busy while other threads wait on disk reads.
        /// ローカル I/O バウンドワークロード用の <see cref="Environment.ProcessorCount"/> 倍率。
        /// ファイルハッシュや IL 比較はディスク I/O が支配的なため、コア数の2倍を使用して
        /// 他スレッドのディスク読み取り待ち中も CPU を稼働させます。
        /// </summary>
        private const int IO_BOUND_MULTIPLIER = 2;

        private readonly IReadOnlyConfigSettings _config;
        private readonly FileDiffResultLists _fileDiffResultLists;
        private readonly IFileSystemService _fileSystem;
        private readonly string _oldFolderAbsolutePath;
        private readonly string _newFolderAbsolutePath;
        private readonly bool _optimizeForNetworkShares;
        private readonly HashSet<string> _ignoredExtensions;
        /// <summary>
        /// Initializes a new instance of <see cref="FolderDiffExecutionStrategy"/>.
        /// <see cref="FolderDiffExecutionStrategy"/> の新しいインスタンスを初期化します。
        /// </summary>
        public FolderDiffExecutionStrategy(
            IReadOnlyConfigSettings config,
            DiffExecutionContext executionContext,
            FileDiffResultLists fileDiffResultLists,
            IFileSystemService fileSystem)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(executionContext);
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            ArgumentNullException.ThrowIfNull(fileSystem);

            _config = config;
            _fileDiffResultLists = fileDiffResultLists;
            _fileSystem = fileSystem;
            _oldFolderAbsolutePath = executionContext.OldFolderAbsolutePath;
            _newFolderAbsolutePath = executionContext.NewFolderAbsolutePath;
            _optimizeForNetworkShares = executionContext.OptimizeForNetworkShares;
            _ignoredExtensions = new HashSet<string>(_config.IgnoredExtensions ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public List<string> EnumerateIncludedFiles(string rootFolderAbsolutePath, FileDiffResultLists.IgnoredFileLocation locationFlag)
        {
            var includedFiles = new List<string>();
            foreach (var fileAbsolutePath in _fileSystem.EnumerateFiles(rootFolderAbsolutePath, "*", SearchOption.AllDirectories))
            {
                if (_ignoredExtensions.Contains(Path.GetExtension(fileAbsolutePath)))
                {
                    if (_config.ShouldIncludeIgnoredFiles)
                    {
                        var relativePath = Path.GetRelativePath(rootFolderAbsolutePath, fileAbsolutePath);
                        _fileDiffResultLists.RecordIgnoredFile(relativePath, locationFlag);
                    }

                    continue;
                }

                includedFiles.Add(fileAbsolutePath);
            }

            return includedFiles;
        }

        /// <inheritdoc />
        public int ComputeUnionFileCount(IReadOnlyCollection<string> oldFilesAbsolutePath, IReadOnlyCollection<string> newFilesAbsolutePath)
        {
            var oldRelativePathSet = new HashSet<string>(
                oldFilesAbsolutePath.Select(path => Path.GetRelativePath(_oldFolderAbsolutePath, path)),
                StringComparer.OrdinalIgnoreCase);
            var newRelativePathSet = new HashSet<string>(
                newFilesAbsolutePath.Select(path => Path.GetRelativePath(_newFolderAbsolutePath, path)),
                StringComparer.OrdinalIgnoreCase);

            oldRelativePathSet.UnionWith(newRelativePathSet);
            return oldRelativePathSet.Count;
        }

        /// <inheritdoc />
        public int DetermineMaxParallel()
        {
            if (_config.MaxParallelism <= 0)
            {
                if (_optimizeForNetworkShares)
                {
                    return Math.Min(Environment.ProcessorCount, MAX_PARALLEL_NETWORK_LIMIT);
                }

                // File comparison is I/O-bound: use ProcessorCount × 2 so threads that
                // block on disk I/O leave room for others to keep the CPU busy.
                // ファイル比較は I/O バウンド: ディスク I/O 待ちスレッドの隙間を
                // 他スレッドで埋めるため、ProcessorCount × 2 を使用。
                return Environment.ProcessorCount * IO_BOUND_MULTIPLIER;
            }

            return _config.MaxParallelism;
        }

        /// <inheritdoc />
        public int CountDotNetAssemblyCandidates(IEnumerable<string> oldFilesAbsolutePath, IEnumerable<string> newFilesAbsolutePath, Action<double>? progressCallback = null)
        {
            var distinctFiles = oldFilesAbsolutePath
                .Concat(newFilesAbsolutePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int total = distinctFiles.Count;
            int dotNetCount = 0;
            for (int i = 0; i < total; i++)
            {
                if (DotNetDetector.IsDotNetExecutable(distinctFiles[i]))
                {
                    dotNetCount++;
                }
                progressCallback?.Invoke(total > 0 ? Math.Min((double)(i + 1) * 100.0 / total, 100.0) : 100.0);
            }
            return dotNetCount;
        }
    }
}
