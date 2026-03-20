using System;
using System.Collections.Generic;
using System.IO;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Abstracts minimal file system operations used by FolderDiffService.
    /// FolderDiffService が利用する最小限のファイルシステム操作を抽象化します。
    /// </summary>
    public interface IFileSystemService
    {
        /// <summary>
        /// Lazily enumerates files under the specified root directory.
        /// 指定ルート配下のファイルを遅延列挙します。
        /// </summary>
        IEnumerable<string> EnumerateFiles(string rootFolderAbsolutePath, string searchPattern, SearchOption searchOption);

        /// <summary>
        /// Creates the directory at the specified path (no-op if it already exists).
        /// 指定パスのディレクトリを作成します（既に存在する場合は何もしません）。
        /// </summary>
        void CreateDirectory(string path);

        /// <summary>
        /// Returns the last write time (UTC) of the file or directory at the specified path.
        /// 指定パスのファイルまたはディレクトリの最終書き込み日時（UTC）を返します。
        /// </summary>
        DateTime GetLastWriteTimeUtc(string path);
    }
}
