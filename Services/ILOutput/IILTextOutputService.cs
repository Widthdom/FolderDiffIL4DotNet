using System.Collections.Generic;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services.ILOutput
{
    /// <summary>
    /// IL テキストのファイル出力処理を抽象化します。
    /// </summary>
    public interface IILTextOutputService
    {
        /// <summary>
        /// MVID を除外した old/new の IL テキスト全行をファイルに書き込みます。
        /// </summary>
        /// <param name="fileRelativePath">対象ファイルの相対パス（出力ファイル名の生成に使用します）。</param>
        /// <param name="il1LinesMvidExcluded">old 側の MVID 除外済み IL テキスト行。</param>
        /// <param name="il2LinesMvidExcluded">new 側の MVID 除外済み IL テキスト行。</param>
        /// <returns>書き込みが完了したことを示す <see cref="Task"/>。</returns>
        Task WriteFullIlTextsAsync(string fileRelativePath, IEnumerable<string> il1LinesMvidExcluded, IEnumerable<string> il2LinesMvidExcluded);
    }
}
