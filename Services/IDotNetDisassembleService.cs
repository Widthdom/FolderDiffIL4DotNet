using System.Collections.Generic;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// .NET アセンブリの IL 逆アセンブル処理を抽象化します。
    /// </summary>
    public interface IDotNetDisassembleService
    {
        /// <summary>
        /// old/new の .NET アセンブリペアを同一の逆アセンブラで逆アセンブルします。
        /// </summary>
        /// <param name="oldDotNetAssemblyFileAbsolutePath">old アセンブリファイルの絶対パス。</param>
        /// <param name="newDotNetAssemblyFileAbsolutePath">new アセンブリファイルの絶対パス。</param>
        /// <returns>
        /// old/new それぞれの IL テキストおよび逆アセンブルコマンド文字列を含むタプル。
        /// </returns>
        Task<(string oldIlText, string oldCommandString, string newIlText, string newCommandString)> DisassemblePairWithSameDisassemblerAsync(
            string oldDotNetAssemblyFileAbsolutePath,
            string newDotNetAssemblyFileAbsolutePath);

        /// <summary>
        /// 指定アセンブリ群の IL キャッシュを非同期で事前取得します。
        /// </summary>
        /// <param name="dotNetAssemblyFilesAbsolutePaths">対象アセンブリファイルの絶対パス一覧。</param>
        /// <param name="maxParallel">最大並列度。</param>
        /// <returns>事前取得が完了したことを示す <see cref="Task"/>。</returns>
        Task PrefetchIlCacheAsync(IEnumerable<string> dotNetAssemblyFilesAbsolutePaths, int maxParallel);
    }
}
