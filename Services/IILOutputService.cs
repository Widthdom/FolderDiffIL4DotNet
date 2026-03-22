using System.Collections.Generic;
using System.Threading;
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
        /// <param name="cancellationToken">Token to observe for cancellation. / キャンセルを監視するトークン。</param>
        Task PrecomputeAsync(IEnumerable<string> filesAbsolutePaths, int maxParallel, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pre-seeds the SHA256 hash for a file so that IL cache key construction does not recompute it.
        /// No-op when the IL cache is disabled or unavailable.
        /// ファイルの SHA256 ハッシュを事前登録し、IL キャッシュキー生成での再計算を回避します。
        /// IL キャッシュが無効または利用不可の場合は何もしません。
        /// </summary>
        /// <param name="fileAbsolutePath">Absolute path to the file. / ファイルの絶対パス。</param>
        /// <param name="sha256Hex">64-character lowercase hex SHA256 hash. / 64 桁小文字 16 進 SHA256 ハッシュ。</param>
        void PreSeedFileHash(string fileAbsolutePath, string sha256Hex);

        /// <summary>
        /// Compares .NET assembly IL diffs for the specified file between old/new folders.
        /// old/new フォルダ間で指定ファイルの .NET アセンブリ IL 差分を比較します。
        /// </summary>
        /// <param name="shouldOutputIlText">Whether to write IL text to files. / IL テキストをファイルに出力するかどうか。</param>
        /// <param name="cancellationToken">Token to observe for cancellation. / キャンセルを監視するトークン。</param>
        /// <returns>A tuple containing an equality flag and the disassembler label used. / アセンブリが等価かどうかを示すフラグと、使用した逆アセンブラのラベルを含むタプル。</returns>
        Task<(bool AreEqual, string? DisassemblerLabel)> DiffDotNetAssembliesAsync(string fileRelativePath, string oldFolderAbsolutePath, string newFolderAbsolutePath, bool shouldOutputIlText, CancellationToken cancellationToken = default);
    }
}
