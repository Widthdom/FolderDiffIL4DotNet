using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Abstracts per-file diff comparison operations.
    /// 個別ファイルの差分比較処理を抽象化します。
    /// </summary>
    public interface IFileDiffService
    {
        /// <summary>
        /// Pre-computes (pre-caches) IL disassembly results for the specified files.
        /// 指定ファイル群の IL 逆アセンブル結果を事前計算（プリキャッシュ）します。
        /// </summary>
        /// <param name="filesAbsolutePath">Absolute paths to files for pre-computation. / 事前計算対象ファイルの絶対パスのコレクション。</param>
        /// <param name="maxParallel">Maximum degree of parallelism. / 最大並列度。</param>
        /// <param name="cancellationToken">Token to observe for cancellation. / キャンセルを監視するトークン。</param>
        Task PrecomputeAsync(IEnumerable<string> filesAbsolutePath, int maxParallel, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether the file at the specified relative path is equal between old/new folders.
        /// 指定した相対パスのファイルが old/new フォルダ間で等価かどうかを判定します。
        /// </summary>
        /// <param name="fileRelativePath">Relative path to the file to compare. / 比較するファイルの相対パス。</param>
        /// <param name="maxParallel">Maximum degree of parallelism. / 最大並列度。</param>
        /// <param name="cancellationToken">Token to observe for cancellation. / キャンセルを監視するトークン。</param>
        Task<bool> FilesAreEqualAsync(string fileRelativePath, int maxParallel = 1, CancellationToken cancellationToken = default);
    }
}
