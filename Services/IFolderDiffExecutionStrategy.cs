using System.Collections.Generic;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// フォルダ差分実行における探索・並列度決定ポリシーを提供します。
    /// </summary>
    public interface IFolderDiffExecutionStrategy
    {
        /// <summary>
        /// 設定済みの無視拡張子を適用し、比較対象へ含めるファイルを列挙します。
        /// </summary>
        List<string> EnumerateIncludedFiles(string rootFolderAbsolutePath, FileDiffResultLists.IgnoredFileLocation locationFlag);

        /// <summary>
        /// old/new の相対パス和集合件数を返します。
        /// </summary>
        int ComputeUnionFileCount(IReadOnlyCollection<string> oldFilesAbsolutePath, IReadOnlyCollection<string> newFilesAbsolutePath);

        /// <summary>
        /// 実行モードと設定に基づいて最大並列度を返します。
        /// </summary>
        int DetermineMaxParallel();

        /// <summary>
        /// old/new 全体から .NET アセンブリ候補数を概算します。
        /// </summary>
        int CountDotNetAssemblyCandidates(IEnumerable<string> oldFilesAbsolutePath, IEnumerable<string> newFilesAbsolutePath);
    }
}
