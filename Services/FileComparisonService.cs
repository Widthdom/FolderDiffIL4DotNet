using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Core.IO;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// 実ファイルシステムに対して比較・判定処理を実行する <see cref="IFileComparisonService"/> 実装です。
    /// </summary>
    public sealed class FileComparisonService : IFileComparisonService
    {
        /// <summary>
        /// 2 ファイルの MD5 ハッシュを比較し、一致するか非同期で判定します。
        /// </summary>
        public Task<bool> DiffFilesByHashAsync(string file1AbsolutePath, string file2AbsolutePath)
            => FileComparer.DiffFilesByHashAsync(file1AbsolutePath, file2AbsolutePath);

        /// <summary>
        /// 2 ファイルをテキストとして行単位で比較し、一致するか非同期で判定します。
        /// </summary>
        public Task<bool> DiffTextFilesAsync(string file1AbsolutePath, string file2AbsolutePath)
            => FileComparer.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);

        /// <summary>
        /// 指定ファイルが .NET 実行可能ファイルかどうかを判定します。
        /// </summary>
        public DotNetExecutableDetectionResult DetectDotNetExecutable(string fileAbsolutePath)
            => DotNetDetector.DetectDotNetExecutable(fileAbsolutePath);

        /// <summary>
        /// 指定ファイルが存在するかを返します。
        /// </summary>
        public bool FileExists(string fileAbsolutePath)
            => File.Exists(fileAbsolutePath);

        /// <summary>
        /// 指定ファイルのバイト長を返します。
        /// </summary>
        public long GetFileLength(string fileAbsolutePath)
            => new FileInfo(fileAbsolutePath).Length;

        /// <summary>
        /// 指定ファイルの指定オフセットからチャンクを非同期で読み込みます。
        /// </summary>
        public async Task<int> ReadChunkAsync(string fileAbsolutePath, long offset, Memory<byte> buffer, CancellationToken cancellationToken)
        {
            using var fileStream = new FileStream(fileAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            fileStream.Seek(offset, SeekOrigin.Begin);
            return await fileStream.ReadAsync(buffer, cancellationToken);
        }
    }
}
