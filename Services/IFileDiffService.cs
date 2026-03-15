using System.Collections.Generic;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// 個別ファイルの差分比較処理を抽象化します。
    /// </summary>
    public interface IFileDiffService
    {
        /// <summary>
        /// 指定ファイル群の IL 逆アセンブル結果を事前計算（プリキャッシュ）します。
        /// </summary>
        /// <param name="filesAbsolutePath">事前計算対象ファイルの絶対パス一覧。</param>
        /// <param name="maxParallel">最大並列度。</param>
        /// <returns>事前計算が完了したことを示す <see cref="Task"/>。</returns>
        Task PrecomputeAsync(IEnumerable<string> filesAbsolutePath, int maxParallel);

        /// <summary>
        /// 指定した相対パスのファイルが old/new フォルダ間で等価かどうかを判定します。
        /// </summary>
        /// <param name="fileRelativePath">比較対象ファイルの相対パス。</param>
        /// <param name="maxParallel">最大並列度。</param>
        /// <returns>ファイルが等価な場合は <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
        Task<bool> FilesAreEqualAsync(string fileRelativePath, int maxParallel = 1);
    }
}
