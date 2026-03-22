using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Common;

namespace FolderDiffIL4DotNet.Core.IO
{
    /// <summary>
    /// Provides hash-based and line-by-line text comparison of files.
    /// ファイルのハッシュ比較およびテキスト比較を提供するクラス。
    /// </summary>
    public static class FileComparer
    {
        private const int FILE_STREAM_SEQUENTIAL_BUFFER_SIZE = 64 * CoreConstants.BYTES_PER_KILOBYTE;

        /// <summary>
        /// Compares two files by SHA256 hash, short-circuiting on size mismatch to minimize I/O.
        /// 2 つのファイルの SHA256 ハッシュ値を比較します。サイズが異なれば即座に不一致を返し I/O を最小化します。
        /// </summary>
        public static async Task<bool> DiffFilesByHashAsync(string file1AbsolutePath, string file2AbsolutePath)
        {
            var result = await DiffFilesByHashWithHexAsync(file1AbsolutePath, file2AbsolutePath);
            return result.AreEqual;
        }

        /// <summary>
        /// Compares two files by SHA256 hash and also returns the computed hex strings.
        /// When files differ by size, hashes are null (no I/O performed).
        /// 2 つのファイルの SHA256 ハッシュ値を比較し、計算した 16 進文字列も返します。
        /// サイズが異なる場合、ハッシュは null です（I/O を行わない）。
        /// </summary>
        public static async Task<(bool AreEqual, string? Hash1Hex, string? Hash2Hex)> DiffFilesByHashWithHexAsync(
            string file1AbsolutePath, string file2AbsolutePath)
        {
            try
            {
                // Short-circuit on size mismatch to minimize I/O
                // サイズが異なれば即座に不一致を返し I/O を最小化
                var file1Info = new FileInfo(file1AbsolutePath);
                var file2Info = new FileInfo(file2AbsolutePath);
                if (file1Info.Length != file2Info.Length)
                {
                    return (false, null, null);
                }

                using var sha256 = SHA256.Create();
                // Use a larger sequential buffer for network I/O optimization
                // ネットワーク I/O 最適化: 共通の逐次読み用バッファサイズを指定
                using var file1stream = new FileStream(file1AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: FILE_STREAM_SEQUENTIAL_BUFFER_SIZE, options: FileOptions.SequentialScan);
                using var file2stream = new FileStream(file2AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: FILE_STREAM_SEQUENTIAL_BUFFER_SIZE, options: FileOptions.SequentialScan);
                var hash1 = await sha256.ComputeHashAsync(file1stream);
                var hash2 = await sha256.ComputeHashAsync(file2stream);
                string hex1 = BitConverter.ToString(hash1).Replace("-", string.Empty).ToLowerInvariant();
                string hex2 = BitConverter.ToString(hash2).Replace("-", string.Empty).ToLowerInvariant();
                return (hash1.SequenceEqual(hash2), hex1, hex2);
            }
            catch (FileNotFoundException ex)
            {
                throw new FileNotFoundException($"File not found during hash diff: {ex.FileName}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException($"Access denied during hash diff for file: {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new IOException($"I/O error during hash diff: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Computes the SHA256 hash of a file and returns it as a 64-character lowercase hex string.
        /// 指定ファイルの SHA256 を計算し、64 桁の 16 進小文字文字列として返します。
        /// </summary>
        public static string ComputeFileSha256Hex(string fileAbsolutePath)
        {
            using (var sha256 = SHA256.Create())
            using (var fileStream = new FileStream(fileAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hash = sha256.ComputeHash(fileStream);
                // Convert "AA-BB-..." to "aabb..."
                // BitConverter.ToString の結果 "AA-BB-.." を "aabb.." へ変換
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Compares two text files line-by-line, returning false at the first difference.
        /// テキストファイルを行単位で逐次比較し、最初に差異が見つかった時点で false を返します。
        /// </summary>
        public static async Task<bool> DiffTextFilesAsync(string file1AbsolutePath, string file2AbsolutePath)
        {
            // Use a larger sequential buffer for efficient comparison over network shares
            // 共通の逐次読み用バッファサイズを付与し、ネットワーク共有でも効率的に比較
            using var fs1 = new FileStream(file1AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: FILE_STREAM_SEQUENTIAL_BUFFER_SIZE, options: FileOptions.SequentialScan);
            using var fs2 = new FileStream(file2AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: FILE_STREAM_SEQUENTIAL_BUFFER_SIZE, options: FileOptions.SequentialScan);
            using var file1StreamReader = new StreamReader(fs1);
            using var file2StreamReader = new StreamReader(fs2);

            string? file1Line;
            string? file2Line;

            do
            {
                file1Line = await file1StreamReader.ReadLineAsync();
                file2Line = await file2StreamReader.ReadLineAsync();

                if (file1Line != file2Line)
                {
                    return false;
                }
            } while (file1Line != null && file2Line != null);

            return true;
        }
    }
}
