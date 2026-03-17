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
        /// <summary>
        /// 指定ルート配下のファイルを遅延列挙します。
        /// </summary>
        IEnumerable<string> EnumerateFiles(string rootFolderAbsolutePath, string searchPattern, SearchOption searchOption);

        /// <summary>
        /// 指定パスのディレクトリを作成します（既に存在する場合は何もしません）。
        /// </summary>
        /// <param name="path">作成するディレクトリの絶対パス。</param>
        void CreateDirectory(string path);

        /// <summary>
        /// 指定パスのファイルまたはディレクトリの最終書き込み日時（UTC）を返します。
        /// </summary>
        /// <param name="path">対象ファイルまたはディレクトリの絶対パス。</param>
        /// <returns>最終書き込み日時（UTC）。</returns>
        DateTime GetLastWriteTimeUtc(string path);
    }
}
