using System.Collections.Generic;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Provides discovery and parallelism policies for folder diff execution.
    /// フォルダ差分実行における探索・並列度決定ポリシーを提供します。
    /// </summary>
    public interface IFolderDiffExecutionStrategy
    {
        /// <summary>
        /// Enumerates files to include in comparison after applying configured ignore-extensions.
        /// 設定済みの無視拡張子を適用し、比較対象へ含めるファイルを列挙します。
        /// </summary>
        List<string> EnumerateIncludedFiles(string rootFolderAbsolutePath, FileDiffResultLists.IgnoredFileLocation locationFlag);

        /// <summary>
        /// Returns the union count of relative paths across old/new folders.
        /// old/new の相対パス和集合件数を返します。
        /// </summary>
        int ComputeUnionFileCount(IReadOnlyCollection<string> oldFilesAbsolutePath, IReadOnlyCollection<string> newFilesAbsolutePath);

        /// <summary>
        /// Returns the maximum degree of parallelism based on the execution mode and settings.
        /// 実行モードと設定に基づいて最大並列度を返します。
        /// </summary>
        int DetermineMaxParallel();

        /// <summary>
        /// Estimates the number of .NET assembly candidates across old/new folders.
        /// old/new 全体から .NET アセンブリ候補数を概算します。
        /// </summary>
        int CountDotNetAssemblyCandidates(IEnumerable<string> oldFilesAbsolutePath, IEnumerable<string> newFilesAbsolutePath);
    }
}
