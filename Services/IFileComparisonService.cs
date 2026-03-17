using System;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// FileDiffService が利用するファイル比較・判定 I/O を抽象化します。
    /// </summary>
    public interface IFileComparisonService
    {
        /// <summary>
        /// 2 つのファイルのハッシュ値を比較し、等価かどうかを返します。
        /// </summary>
        /// <param name="file1AbsolutePath">1 つ目のファイルの絶対パス。</param>
        /// <param name="file2AbsolutePath">2 つ目のファイルの絶対パス。</param>
        /// <returns>ファイルが等価な場合は <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
        Task<bool> DiffFilesByHashAsync(string file1AbsolutePath, string file2AbsolutePath);

        /// <summary>
        /// 2 つのテキストファイルを行単位で比較し、等価かどうかを返します。
        /// </summary>
        /// <param name="file1AbsolutePath">1 つ目のテキストファイルの絶対パス。</param>
        /// <param name="file2AbsolutePath">2 つ目のテキストファイルの絶対パス。</param>
        /// <returns>ファイルが等価な場合は <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
        Task<bool> DiffTextFilesAsync(string file1AbsolutePath, string file2AbsolutePath);

        /// <summary>
        /// 指定ファイルが .NET 実行可能ファイルかどうかを検出します。
        /// </summary>
        /// <param name="fileAbsolutePath">検出対象ファイルの絶対パス。</param>
        /// <returns>.NET 実行可能ファイル判定結果。</returns>
        DotNetExecutableDetectionResult DetectDotNetExecutable(string fileAbsolutePath);

        /// <summary>
        /// 指定パスにファイルが存在するかどうかを返します。
        /// </summary>
        /// <param name="fileAbsolutePath">確認対象ファイルの絶対パス。</param>
        /// <returns>ファイルが存在する場合は <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
        bool FileExists(string fileAbsolutePath);

        /// <summary>
        /// 指定ファイルのサイズ（バイト数）を返します。
        /// </summary>
        /// <param name="fileAbsolutePath">対象ファイルの絶対パス。</param>
        /// <returns>ファイルサイズ（バイト）。</returns>
        long GetFileLength(string fileAbsolutePath);

        /// <summary>
        /// 指定ファイルの指定オフセットからバイト列を非同期で読み込みます。
        /// </summary>
        /// <param name="fileAbsolutePath">読み込み対象ファイルの絶対パス。</param>
        /// <param name="offset">読み込み開始位置（バイトオフセット）。</param>
        /// <param name="buffer">読み込んだデータを格納するバッファ。</param>
        /// <param name="cancellationToken">キャンセルトークン。</param>
        /// <returns>実際に読み込んだバイト数。</returns>
        Task<int> ReadChunkAsync(string fileAbsolutePath, long offset, Memory<byte> buffer, CancellationToken cancellationToken);
    }
}
