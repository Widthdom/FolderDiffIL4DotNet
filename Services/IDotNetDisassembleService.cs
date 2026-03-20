using System.Collections.Generic;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Abstracts IL disassembly operations for .NET assemblies.
    /// .NET アセンブリの IL 逆アセンブル処理を抽象化します。
    /// </summary>
    public interface IDotNetDisassembleService
    {
        /// <summary>
        /// Disassembles an old/new .NET assembly pair using the same disassembler.
        /// old/new の .NET アセンブリペアを同一の逆アセンブラで逆アセンブルします。
        /// </summary>
        /// <returns>A tuple containing IL text and disassembly command strings for both old and new. / old/new それぞれの IL テキストおよび逆アセンブルコマンド文字列を含むタプル。</returns>
        Task<(string oldIlText, string oldCommandString, string newIlText, string newCommandString)> DisassemblePairWithSameDisassemblerAsync(
            string oldDotNetAssemblyFileAbsolutePath,
            string newDotNetAssemblyFileAbsolutePath);

        /// <summary>
        /// Pre-fetches the IL cache for the specified assemblies asynchronously.
        /// 指定アセンブリ群の IL キャッシュを非同期で事前取得します。
        /// </summary>
        Task PrefetchIlCacheAsync(IEnumerable<string> dotNetAssemblyFilesAbsolutePaths, int maxParallel);
    }
}
