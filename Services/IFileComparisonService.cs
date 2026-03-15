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
        Task<bool> DiffFilesByHashAsync(string file1AbsolutePath, string file2AbsolutePath);

        Task<bool> DiffTextFilesAsync(string file1AbsolutePath, string file2AbsolutePath);

        DotNetExecutableDetectionResult DetectDotNetExecutable(string fileAbsolutePath);

        bool FileExists(string fileAbsolutePath);

        long GetFileLength(string fileAbsolutePath);

        Task<int> ReadChunkAsync(string fileAbsolutePath, long offset, Memory<byte> buffer, CancellationToken cancellationToken);
    }
}
