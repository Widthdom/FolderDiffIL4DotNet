using System.Collections.Generic;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services.ILOutput
{
    /// <summary>
    /// Abstracts IL text file output operations.
    /// IL テキストのファイル出力処理を抽象化します。
    /// </summary>
    public interface IILTextOutputService
    {
        /// <summary>
        /// Writes filtered old/new IL text lines to files.
        /// フィルタ済みの old/new IL テキスト行をファイルに書き込みます。
        /// </summary>
        /// <param name="fileRelativePath">Relative path of the target file (used to generate output file names). / 対象ファイルの相対パス（出力ファイル名の生成に使用します）。</param>
        /// <param name="filteredIl1Lines">
        /// Filtered IL text lines for the old side.
        /// old 側のフィルタ済み IL テキスト行。
        /// </param>
        /// <param name="filteredIl2Lines">
        /// Filtered IL text lines for the new side.
        /// new 側のフィルタ済み IL テキスト行。
        /// </param>
        Task WriteFullIlTextsAsync(string fileRelativePath, IEnumerable<string> filteredIl1Lines, IEnumerable<string> filteredIl2Lines);
    }
}
