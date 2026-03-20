using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Core.IO;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// <see cref="IFileComparisonService"/> implementation that runs comparison and detection against the real file system.
    /// 実ファイルシステムに対して比較・判定処理を実行する <see cref="IFileComparisonService"/> 実装です。
    /// </summary>
    public sealed class FileComparisonService : IFileComparisonService
    {
        /// <inheritdoc />
        public Task<bool> DiffFilesByHashAsync(string file1AbsolutePath, string file2AbsolutePath)
            => FileComparer.DiffFilesByHashAsync(file1AbsolutePath, file2AbsolutePath);

        /// <inheritdoc />
        public Task<bool> DiffTextFilesAsync(string file1AbsolutePath, string file2AbsolutePath)
            => FileComparer.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);

        /// <inheritdoc />
        public DotNetExecutableDetectionResult DetectDotNetExecutable(string fileAbsolutePath)
            => DotNetDetector.DetectDotNetExecutable(fileAbsolutePath);

        /// <inheritdoc />
        public bool FileExists(string fileAbsolutePath)
            => File.Exists(fileAbsolutePath);

        /// <inheritdoc />
        public long GetFileLength(string fileAbsolutePath)
            => new FileInfo(fileAbsolutePath).Length;

        /// <inheritdoc />
        public async Task<int> ReadChunkAsync(string fileAbsolutePath, long offset, Memory<byte> buffer, CancellationToken cancellationToken)
        {
            using var fileStream = new FileStream(fileAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            fileStream.Seek(offset, SeekOrigin.Begin);
            return await fileStream.ReadAsync(buffer, cancellationToken);
        }
    }
}
