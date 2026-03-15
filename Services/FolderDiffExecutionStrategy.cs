using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// フォルダ差分の探索条件と並列実行ポリシーを決定します。
    /// </summary>
    public sealed class FolderDiffExecutionStrategy : IFolderDiffExecutionStrategy
    {
        /// <summary>
        /// ネットワーク最適化時の自動並列度上限。
        /// </summary>
        private const int MAX_PARALLEL_NETWORK_LIMIT = 8;

        private readonly ConfigSettings _config;
        private readonly FileDiffResultLists _fileDiffResultLists;
        private readonly IFileSystemService _fileSystem;
        private readonly string _oldFolderAbsolutePath;
        private readonly string _newFolderAbsolutePath;
        private readonly bool _optimizeForNetworkShares;
        private readonly HashSet<string> _ignoredExtensions;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public FolderDiffExecutionStrategy(
            ConfigSettings config,
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
                return _optimizeForNetworkShares
                    ? Math.Min(Environment.ProcessorCount, MAX_PARALLEL_NETWORK_LIMIT)
                    : Environment.ProcessorCount;
            }

            return _config.MaxParallelism;
        }

        /// <inheritdoc />
        public int CountDotNetAssemblyCandidates(IEnumerable<string> oldFilesAbsolutePath, IEnumerable<string> newFilesAbsolutePath)
            => oldFilesAbsolutePath
                .Concat(newFilesAbsolutePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(DotNetDetector.IsDotNetExecutable);
    }
}
