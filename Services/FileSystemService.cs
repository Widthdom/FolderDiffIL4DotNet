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
        /// <summary>
        /// 指定フォルダ配下のファイルを検索パターンと検索オプションに従って列挙します。
        /// </summary>
        public IEnumerable<string> EnumerateFiles(string rootFolderAbsolutePath, string searchPattern, SearchOption searchOption)
            => Directory.EnumerateFiles(rootFolderAbsolutePath, searchPattern, searchOption);

        /// <summary>
        /// 指定パスにディレクトリを作成します（既に存在する場合は何もしません）。
        /// </summary>
        public void CreateDirectory(string path)
            => Directory.CreateDirectory(path);

        /// <summary>
        /// 指定ファイルの最終書き込み日時（UTC）を返します。
        /// </summary>
        public DateTime GetLastWriteTimeUtc(string path)
            => File.GetLastWriteTimeUtc(path);
    }
}
