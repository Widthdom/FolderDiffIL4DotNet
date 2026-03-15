using System;
using System.Collections.Generic;
using System.IO;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// 実ファイルシステムに委譲する <see cref="IFileSystemService"/> 実装です。
    /// </summary>
    public sealed class FileSystemService : IFileSystemService
    {
        public IEnumerable<string> EnumerateFiles(string rootFolderAbsolutePath, string searchPattern, SearchOption searchOption)
            => Directory.EnumerateFiles(rootFolderAbsolutePath, searchPattern, searchOption);

        public void CreateDirectory(string path)
            => Directory.CreateDirectory(path);

        public DateTime GetLastWriteTimeUtc(string path)
            => File.GetLastWriteTimeUtc(path);
    }
}
