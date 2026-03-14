using System;
using System.Collections.Generic;
using System.IO;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// FolderDiffService が利用する最小限のファイルシステム操作を抽象化します。
    /// </summary>
    public interface IFileSystemService
    {
        IEnumerable<string> GetFiles(string rootFolderAbsolutePath, string searchPattern, SearchOption searchOption);

        void CreateDirectory(string path);

        DateTime GetLastWriteTimeUtc(string path);
    }
}
