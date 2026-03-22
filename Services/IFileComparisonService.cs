using System;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Abstracts file comparison and detection I/O used by FileDiffService.
    /// FileDiffService が利用するファイル比較・判定 I/O を抽象化します。
    /// </summary>
    public interface IFileComparisonService
    {
        /// <summary>
        /// Compares two files by hash and returns whether they are equal.
        /// 2 つのファイルのハッシュ値を比較し、等価かどうかを返します。
        /// </summary>
        Task<bool> DiffFilesByHashAsync(string file1AbsolutePath, string file2AbsolutePath);

        /// <summary>
        /// Compares two files by SHA256 hash and also returns the computed hex strings.
        /// When files differ by size, hashes are null (no I/O performed).
        /// 2 つのファイルの SHA256 ハッシュ値を比較し、計算した 16 進文字列も返します。
        /// サイズが異なる場合、ハッシュは null です。
        /// </summary>
        Task<(bool AreEqual, string? Hash1Hex, string? Hash2Hex)> DiffFilesByHashWithHexAsync(
            string file1AbsolutePath, string file2AbsolutePath);

        /// <summary>
        /// Compares two text files line-by-line and returns whether they are equal.
        /// 2 つのテキストファイルを行単位で比較し、等価かどうかを返します。
        /// </summary>
        Task<bool> DiffTextFilesAsync(string file1AbsolutePath, string file2AbsolutePath);

        /// <summary>
        /// Detects whether the specified file is a .NET executable.
        /// 指定ファイルが .NET 実行可能ファイルかどうかを検出します。
        /// </summary>
        DotNetExecutableDetectionResult DetectDotNetExecutable(string fileAbsolutePath);

        /// <summary>
        /// Returns whether a file exists at the specified path.
        /// 指定パスにファイルが存在するかどうかを返します。
        /// </summary>
        bool FileExists(string fileAbsolutePath);

        /// <summary>
        /// Returns the size in bytes of the specified file.
        /// 指定ファイルのサイズ（バイト数）を返します。
        /// </summary>
        long GetFileLength(string fileAbsolutePath);

        /// <summary>
        /// Reads bytes asynchronously from the specified file starting at the given offset.
        /// 指定ファイルの指定オフセットからバイト列を非同期で読み込みます。
        /// </summary>
        /// <param name="offset">Byte offset to start reading from. / 読み込み開始位置（バイトオフセット）。</param>
        /// <param name="buffer">Buffer to store the read data. / 読み込んだデータを格納するバッファ。</param>
        /// <returns>The number of bytes actually read. / 実際に読み込んだバイト数。</returns>
        Task<int> ReadChunkAsync(string fileAbsolutePath, long offset, Memory<byte> buffer, CancellationToken cancellationToken);
    }
}
