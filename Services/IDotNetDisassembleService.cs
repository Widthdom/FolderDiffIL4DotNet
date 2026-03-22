using System.Collections.Generic;
using System.Threading;
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
        /// <param name="oldDotNetAssemblyFileAbsolutePath">Absolute path to the old assembly. / 旧アセンブリの絶対パス。</param>
        /// <param name="newDotNetAssemblyFileAbsolutePath">Absolute path to the new assembly. / 新アセンブリの絶対パス。</param>
        /// <param name="cancellationToken">Token to observe for cancellation. / キャンセルを監視するトークン。</param>
        /// <returns>A tuple containing IL text and disassembly command strings for both old and new. / old/new それぞれの IL テキストおよび逆アセンブルコマンド文字列を含むタプル。</returns>
        Task<(string oldIlText, string oldCommandString, string newIlText, string newCommandString)> DisassemblePairWithSameDisassemblerAsync(
            string oldDotNetAssemblyFileAbsolutePath,
            string newDotNetAssemblyFileAbsolutePath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Pre-fetches the IL cache for the specified assemblies asynchronously.
        /// 指定アセンブリ群の IL キャッシュを非同期で事前取得します。
        /// </summary>
        /// <param name="dotNetAssemblyFilesAbsolutePaths">Absolute paths to the .NET assembly files. / .NET アセンブリファイルの絶対パスのコレクション。</param>
        /// <param name="maxParallel">Maximum degree of parallelism. / 最大並列度。</param>
        /// <param name="cancellationToken">Token to observe for cancellation. / キャンセルを監視するトークン。</param>
        Task PrefetchIlCacheAsync(IEnumerable<string> dotNetAssemblyFilesAbsolutePaths, int maxParallel, CancellationToken cancellationToken = default);
    }
}
