using System;
using System.Globalization;
using System.IO;
using FolderDiffIL4DotNet.Core.Common;

namespace FolderDiffIL4DotNet.Core.IO
{
    /// <summary>
    /// Provides file-system operations: timestamp retrieval, read-only flag, silent deletion, and network-path detection.
    /// ファイルシステム操作（タイムスタンプ取得、読み取り専用設定、ファイル削除、ネットワークパス検出）を提供するクラス。
    /// </summary>
    public static class FileSystemUtility
    {
        private const string ERROR_FILE_PATH_NULL = "File path cannot be null or whitespace.";
        private const string ERROR_FILE_NOT_FOUND = "File not found.";

        /// <summary>
        /// Returns the file's last-write time as a "yyyy-MM-dd HH:mm:ss" string.
        /// 指定ファイルの最終更新日時を「yyyy-MM-dd HH:mm:ss」形式で返します。
        /// </summary>
        public static string GetTimestamp(string fileAbsolutepath)
        {
            return new DateTimeOffset(File.GetLastWriteTime(fileAbsolutepath))
                .ToString(CoreConstants.TIMESTAMP_WITH_TIME_ZONE_FORMAT, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Sets the read-only attribute on the file; no-op if already read-only.
        /// 指定ファイルに読み取り専用属性を付与します。既に読み取り専用の場合は何もしません。
        /// </summary>
        public static void TrySetReadOnly(string fileAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(fileAbsolutePath))
            {
                throw new ArgumentException(ERROR_FILE_PATH_NULL, nameof(fileAbsolutePath));
            }
            if (!File.Exists(fileAbsolutePath))
            {
                throw new FileNotFoundException(ERROR_FILE_NOT_FOUND, fileAbsolutePath);
            }
            var fileAttributes = File.GetAttributes(fileAbsolutePath);
            if ((fileAttributes & FileAttributes.ReadOnly) == 0)
            {
                File.SetAttributes(fileAbsolutePath, fileAttributes | FileAttributes.ReadOnly);
            }
        }

        /// <summary>
        /// Deletes a file, silently ignoring any exceptions.
        /// ファイルを例外を無視して削除します。
        /// </summary>
        public static void DeleteFileSilent(string fileAbsolutePath)
        {
            if (string.IsNullOrEmpty(fileAbsolutePath))
            {
                return;
            }

            try
            {
                File.Delete(fileAbsolutePath);
            }
            catch (IOException)
            {
                /* ignore */
            }
            catch (UnauthorizedAccessException)
            {
                /* ignore */
            }
            catch (NotSupportedException)
            {
                /* ignore */
            }
        }

        /// <summary>
        /// Returns true if the path is likely a network share (UNC / NFS / CIFS / SSHFS etc.).
        /// Delegates to <see cref="NetworkPathDetector.IsLikelyNetworkPath"/>.
        /// 指定パスがネットワーク共有の可能性が高いかを判定します。
        /// <see cref="NetworkPathDetector.IsLikelyNetworkPath"/> に委譲します。
        /// </summary>
        public static bool IsLikelyNetworkPath(string absolutePath)
            => NetworkPathDetector.IsLikelyNetworkPath(absolutePath);
    }
}
