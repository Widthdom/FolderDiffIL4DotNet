using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Utils
{
    /// <summary>
    /// ファイルのハッシュ比較およびテキスト比較を提供するクラス
    /// </summary>
    public static class FileComparer
    {
        /// <summary>
        /// 1 KiB (2^10) を表すバイト数。
        /// </summary>
        private const int BYTES_PER_KILOBYTE = 1024;

        /// <summary>
        /// 逐次読み取りで利用する既定の FileStream バッファサイズ（64KiB）。
        /// </summary>
        private const int FILE_STREAM_SEQUENTIAL_BUFFER_SIZE = 64 * BYTES_PER_KILOBYTE;

        /// <summary>
        /// 指定された2つのファイルのMD5ハッシュ値を比較します。
        /// </summary>
        /// <param name="file1AbsolutePath">ファイル1の絶対パス</param>
        /// <param name="file2AbsolutePath">ファイル2の絶対パス</param>
        /// <returns>ハッシュ値が等しい場合は true、それ以外の場合は false</returns>
        /// <exception cref="FileNotFoundException">指定されたファイルが見つからない場合にスローされます。</exception>
        /// <exception cref="UnauthorizedAccessException">ファイルへのアクセス権限がない場合にスローされます。</exception>
        /// <exception cref="IOException">ファイルの読み取り中にエラーが発生した場合にスローされます。</exception>
        public static async Task<bool> DiffFilesByHashAsync(string file1AbsolutePath, string file2AbsolutePath)
        {
            try
            {
                // まずサイズが異なれば不一致（I/O を最小化）
                var file1Info = new FileInfo(file1AbsolutePath);
                var file2Info = new FileInfo(file2AbsolutePath);
                if (file1Info.Length != file2Info.Length)
                {
                    return false;
                }

                using var md5 = MD5.Create();
                // ネットワークI/O最適化: 共通の逐次読み用バッファサイズを指定
                using var file1stream = new FileStream(file1AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: FILE_STREAM_SEQUENTIAL_BUFFER_SIZE, options: FileOptions.SequentialScan);
                using var file2stream = new FileStream(file2AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: FILE_STREAM_SEQUENTIAL_BUFFER_SIZE, options: FileOptions.SequentialScan);
                var hash1 = await md5.ComputeHashAsync(file1stream);
                var hash2 = await md5.ComputeHashAsync(file2stream);
                return hash1.SequenceEqual(hash2);
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
        /// 指定ファイルの MD5 を計算し、32桁の16進小文字文字列として返します。
        /// </summary>
        /// <param name="fileAbsolutePath">対象ファイルの絶対パス。</param>
        /// <returns>MD5 の16進小文字文字列。</returns>
        /// <exception cref="FileNotFoundException">ファイルが存在しない場合。</exception>
        /// <exception cref="UnauthorizedAccessException">アクセス権が不足している場合。</exception>
        /// <exception cref="IOException">読み取り中に I/O エラーが発生した場合。</exception>
        public static string ComputeFileMd5Hex(string fileAbsolutePath)
        {
            using (var md5 = MD5.Create())
            using (var fileStream = new FileStream(fileAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hash = md5.ComputeHash(fileStream);
                // 例: BitConverter.ToString => "AA-BB-.." を "aabb.." へ
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        /// <summary>
        /// テキストファイルを行単位で逐次比較します。両ファイルの先頭から読み進め、
        /// いずれかの行が異なった時点で false を返します。全行が一致すれば true を返します。
        /// </summary>
        /// <param name="file1AbsolutePath">ファイル1の絶対パス</param>
        /// <param name="file2AbsolutePath">ファイル2の絶対パス</param>
        /// <returns>ファイルが等しい場合は true、それ以外の場合は false</returns>
        /// <exception cref="FileNotFoundException">指定されたファイルが見つからない場合にスローされます。</exception>
        /// <exception cref="UnauthorizedAccessException">ファイルへのアクセス権限がない場合にスローされます。</exception>
        /// <exception cref="IOException">ファイルの読み取り中にエラーが発生した場合にスローされます。</exception>
        public static async Task<bool> DiffTextFilesAsync(string file1AbsolutePath, string file2AbsolutePath)
        {
            // 共通の逐次読み用バッファサイズを付与し、ネットワーク共有でも効率的に比較
            using var fs1 = new FileStream(file1AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: FILE_STREAM_SEQUENTIAL_BUFFER_SIZE, options: FileOptions.SequentialScan);
            using var fs2 = new FileStream(file2AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: FILE_STREAM_SEQUENTIAL_BUFFER_SIZE, options: FileOptions.SequentialScan);
            using var file1StreamReader = new StreamReader(fs1);
            using var file2StreamReader = new StreamReader(fs2);

            string file1Line;
            string file2Line;

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
