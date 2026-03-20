using System.Collections.Generic;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Abstracts IL diff output operations for .NET assemblies.
    /// .NET アセンブリの IL 差分出力処理を抽象化します。
    /// </summary>
    public interface IILOutputService
    {
        /// <summary>
        /// Pre-computes (pre-caches) IL disassembly results for the specified files.
        /// 指定ファイル群の IL 逆アセンブル結果を事前計算（プリキャッシュ）します。
        /// </summary>
        Task PrecomputeAsync(IEnumerable<string> filesAbsolutePaths, int maxParallel);

        /// <summary>
        /// Compares .NET assembly IL diffs for the specified file between old/new folders.
        /// old/new フォルダ間で指定ファイルの .NET アセンブリ IL 差分を比較します。
        /// </summary>
        /// <param name="shouldOutputIlText">Whether to write IL text to files. / IL テキストをファイルに出力するかどうか。</param>
        /// <returns>A tuple containing an equality flag and the disassembler label used. / アセンブリが等価かどうかを示すフラグと、使用した逆アセンブラのラベルを含むタプル。</returns>
        Task<(bool AreEqual, string DisassemblerLabel)> DiffDotNetAssembliesAsync(string fileRelativePath, string oldFolderAbsolutePath, string newFolderAbsolutePath, bool shouldOutputIlText);
    }
}
