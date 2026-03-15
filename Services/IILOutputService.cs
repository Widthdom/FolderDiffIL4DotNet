using System.Collections.Generic;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// .NET アセンブリの IL 差分出力処理を抽象化します。
    /// </summary>
    public interface IILOutputService
    {
        /// <summary>
        /// 指定ファイル群の IL 逆アセンブル結果を事前計算（プリキャッシュ）します。
        /// </summary>
        /// <param name="filesAbsolutePaths">事前計算対象ファイルの絶対パス一覧。</param>
        /// <param name="maxParallel">最大並列度。</param>
        /// <returns>事前計算が完了したことを示す <see cref="Task"/>。</returns>
        Task PrecomputeAsync(IEnumerable<string> filesAbsolutePaths, int maxParallel);

        /// <summary>
        /// old/new フォルダ間で指定ファイルの .NET アセンブリ IL 差分を比較します。
        /// </summary>
        /// <param name="fileRelativePath">比較対象ファイルの相対パス。</param>
        /// <param name="oldFolderAbsolutePath">old フォルダの絶対パス。</param>
        /// <param name="newFolderAbsolutePath">new フォルダの絶対パス。</param>
        /// <param name="shouldOutputIlText">IL テキストをファイルに出力するかどうか。</param>
        /// <returns>
        /// アセンブリが等価かどうかを示すフラグと、使用した逆アセンブラのラベルを含むタプル。
        /// </returns>
        Task<(bool AreEqual, string DisassemblerLabel)> DiffDotNetAssembliesAsync(string fileRelativePath, string oldFolderAbsolutePath, string newFolderAbsolutePath, bool shouldOutputIlText);
    }
}
