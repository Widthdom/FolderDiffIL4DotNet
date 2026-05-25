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
        /// Writes all old/new IL text lines (with MVID excluded) to files.
        /// MVID を除外した old/new の IL テキスト全行をファイルに書き込みます。
        /// </summary>
        /// <param name="fileRelativePath">Relative path of the target file (used to generate output file names). / 対象ファイルの相対パス（出力ファイル名の生成に使用します）。</param>
        /// <param name="il1LinesMvidExcluded">MVID-excluded IL text lines for the old side. / old 側の MVID 除外済み IL テキスト行。</param>
        /// <param name="il2LinesMvidExcluded">MVID-excluded IL text lines for the new side. / new 側の MVID 除外済み IL テキスト行。</param>
        Task WriteFullIlTextsAsync(string fileRelativePath, IEnumerable<string> il1LinesMvidExcluded, IEnumerable<string> il2LinesMvidExcluded);
    }
}
