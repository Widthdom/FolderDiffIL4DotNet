using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Common;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Core.Text;

namespace FolderDiffIL4DotNet.Services.Caching
{
    /// <summary>
    /// Disk layer of the IL cache. Handles file I/O and disk-quota enforcement.
    /// IL キャッシュのディスク層。ファイル I/O とディスククォータ制御を担当します。
    /// </summary>
    internal sealed class ILDiskCache
    {
        private const string IL_CACHE_EXTENSION = ".ilcache";
        private readonly ILoggerService _logger;
        private readonly string _cacheDirectoryAbsolutePath;
        private readonly bool _isEnabled;
        private readonly int _maxDiskFileCount;
        private readonly long _maxDiskBytes;
        private readonly object _diskQuotaLock = new();

        internal ILDiskCache(string cacheDirectoryAbsolutePath, ILoggerService logger, int maxDiskFileCount, long maxDiskMegabytes)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheDirectoryAbsolutePath = cacheDirectoryAbsolutePath;
            _maxDiskFileCount = maxDiskFileCount;
            _maxDiskBytes = maxDiskMegabytes > 0
                ? maxDiskMegabytes * Constants.BYTES_PER_KILOBYTE * Constants.BYTES_PER_KILOBYTE
                : 0;

            if (string.IsNullOrWhiteSpace(cacheDirectoryAbsolutePath))
            {
                _isEnabled = false;
                return;
            }

            _isEnabled = TryInitializeCacheDirectory(cacheDirectoryAbsolutePath);
        }

        /// <summary>
        /// Reads the cache file for the given key. Returns null on miss or read failure.
        /// 指定キーのキャッシュファイルを読み込みます。未ヒットまたは読み込み失敗時は null。
        /// </summary>
        internal async Task<string?> TryReadAsync(string? cacheKey)
        {
            if (!_isEnabled)
            {
                return null;
            }

            if (!TryValidateCacheKey(cacheKey, "read"))
            {
                return null;
            }

            var cacheFileAbsolutePath = BuildCacheFileAbsolutePath(cacheKey);
            try
            {
                PathValidator.ValidateAbsolutePathLengthOrThrow(cacheFileAbsolutePath);
                if (!File.Exists(cacheFileAbsolutePath))
                {
                    return null;
                }

                return await File.ReadAllTextAsync(cacheFileAbsolutePath);
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                LogFileOperationFailure("read", cacheKey, cacheFileAbsolutePath, ex);
            }

            return null;
        }

        /// <summary>
        /// Writes IL text to a cache file and enforces the disk quota afterward.
        /// 指定キーのキャッシュファイルを書き込み、ディスククォータを適用します。
        /// </summary>
        internal async Task WriteAsync(string? cacheKey, string ilText)
        {
            if (!_isEnabled)
            {
                return;
            }

            if (!TryValidateCacheKey(cacheKey, "write"))
            {
                return;
            }

            var cacheFileAbsolutePath = BuildCacheFileAbsolutePath(cacheKey);
            try
            {
                PathValidator.ValidateAbsolutePathLengthOrThrow(cacheFileAbsolutePath);
                PrepareCacheFileForOverwrite(cacheFileAbsolutePath);
                await File.WriteAllTextAsync(cacheFileAbsolutePath, ilText);
                EnforceQuota();
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                LogFileOperationFailure("write", cacheKey, cacheFileAbsolutePath, ex);
            }
        }

        /// <summary>
        /// Attempts to delete the cache file for the given key (best-effort).
        /// 指定キーのキャッシュファイル削除を試みます（ベストエフォート）。
        /// </summary>
        internal void Remove(string? cacheKey)
        {
            if (!_isEnabled)
            {
                return;
            }

            if (!TryValidateCacheKey(cacheKey, "remove"))
            {
                return;
            }

            var cacheFileAbsolutePath = BuildCacheFileAbsolutePath(cacheKey);
            try
            {
                PathValidator.ValidateAbsolutePathLengthOrThrow(cacheFileAbsolutePath);
                if (File.Exists(cacheFileAbsolutePath))
                {
                    // Disk deletion on LRU eviction is best-effort; failure does not corrupt diff results, so we log a warning and continue.
                    // LRU 退避に伴うディスク削除は容量維持の best-effort 処理。失敗しても比較結果自体は壊れないため、warning のみで継続する。
                    DeleteCacheFile(cacheFileAbsolutePath);
                }
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                LogRemoveFailure(cacheKey, cacheFileAbsolutePath, ex);
            }
        }

        /// <summary>
        /// Initializes the disk cache directory; returns true on success.
        /// ディスクキャッシュ用ディレクトリを初期化します。成功時 true。
        /// </summary>
        private bool TryInitializeCacheDirectory(string cacheDirectoryAbsolutePath)
        {
            try
            {
                PathValidator.ValidateAbsolutePathLengthOrThrow(cacheDirectoryAbsolutePath);
                Directory.CreateDirectory(cacheDirectoryAbsolutePath);
                return true;
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                LogDirectoryInitializationFailure(cacheDirectoryAbsolutePath, ex);
            }

            return false;
        }

        /// <summary>
        /// Enforces disk quota by file count and total size.
        /// ディスクキャッシュ容量制御（サイズと件数）。
        /// </summary>
        private void EnforceQuota()
        {
            if (!ShouldEnforceQuota())
            {
                return;
            }

            lock (_diskQuotaLock)
            {
                var directoryInfo = new DirectoryInfo(_cacheDirectoryAbsolutePath);
                if (!directoryInfo.Exists)
                {
                    return;
                }

                var (files, totalBytes) = GetCacheFilesSnapshot(directoryInfo);
                TrimCacheFiles(files, totalBytes);
            }
        }

        /// <summary>
        /// Returns true when disk-quota enforcement is needed.
        /// ディスククォータ監視が必要かどうかを判定します。
        /// </summary>
        private bool ShouldEnforceQuota() =>
            _isEnabled && (_maxDiskFileCount > 0 || _maxDiskBytes > 0);

        /// <summary>
        /// Validates the cache key before building a disk path and logs a warning when it is blank.
        /// ディスクパス生成前にキャッシュキーを検証し、空キーの場合は警告を記録します。
        /// </summary>
        private bool TryValidateCacheKey([NotNullWhen(true)] string? cacheKey, string operation)
        {
            if (!string.IsNullOrWhiteSpace(cacheKey))
            {
                return true;
            }

            _logger.LogMessage(
                AppLogLevel.Warning,
                $"Skipped IL disk cache {operation} because the cache key was null, empty, or whitespace. Directory='{_cacheDirectoryAbsolutePath}'.",
                shouldOutputMessageToConsole: true);
            return false;
        }

        /// <summary>
        /// Builds the absolute path of a cache file from a cache key.
        /// キャッシュキーからキャッシュファイルの絶対パスを生成します。
        /// </summary>
        private string BuildCacheFileAbsolutePath(string cacheKey) =>
            Path.Combine(_cacheDirectoryAbsolutePath, TextSanitizer.ToSafeFileName(cacheKey) + IL_CACHE_EXTENSION);

        /// <summary>
        /// Returns a snapshot of cache files sorted by last-write-time (oldest first) and the total byte count.
        /// キャッシュディレクトリ内のファイル一覧を最終更新日時の古い順に整列したスナップショットで返します。
        /// </summary>
        private static (List<FileInfo> Files, long TotalBytes) GetCacheFilesSnapshot(DirectoryInfo directoryInfo)
        {
            var files = directoryInfo
                .GetFiles($"*{IL_CACHE_EXTENSION}", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f.LastWriteTimeUtc)
                .ToList();
            long totalBytes = files.Sum(f => f.Length);
            return (files, totalBytes);
        }

        /// <summary>
        /// Deletes oldest files until the quota is satisfied and logs the result.
        /// 上限を超えている場合に古いファイルから削除し、削除結果をログ出力します。
        /// </summary>
        private void TrimCacheFiles(List<FileInfo> orderedFiles, long initialTotalBytes)
        {
            long totalBytes = initialTotalBytes;
            int fileCount = orderedFiles.Count;
            int removed = 0;
            foreach (var file in orderedFiles)
            {
                if (!IsQuotaExceeded(fileCount, totalBytes))
                {
                    break;
                }

                if (TryDeleteCacheFile(file))
                {
                    totalBytes -= file.Length;
                    fileCount--;
                    removed++;
                }
            }

            if (removed > 0)
            {
                _logger.LogMessage(
                    AppLogLevel.Info,
                    $"Disk quota trim: directory='{_cacheDirectoryAbsolutePath}', removed={removed}, remain={fileCount}, bytes={totalBytes}, maxFiles={_maxDiskFileCount}, maxBytes={_maxDiskBytes}",
                    shouldOutputMessageToConsole: true);
            }
        }

        /// <summary>
        /// Returns true when file count or total size exceeds the disk quota.
        /// ファイル数または総サイズがディスククォータを超過しているかを判定します。
        /// </summary>
        private bool IsQuotaExceeded(int fileCount, long totalBytes) =>
            (_maxDiskFileCount > 0 && fileCount > _maxDiskFileCount) ||
            (_maxDiskBytes > 0 && totalBytes > _maxDiskBytes);

        /// <summary>
        /// Tries to delete the file; logs a warning on failure and returns false.
        /// ファイルの削除を試み、失敗した場合は警告ログを出します。
        /// </summary>
        private bool TryDeleteCacheFile(FileInfo file)
        {
            try
            {
                DeleteCacheFile(file.FullName);
                return true;
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                LogDeleteFailure(file.FullName, ex);
            }

            return false;
        }

        /// <summary>
        /// Logs a directory-initialization failure.
        /// ディレクトリ初期化失敗をログします。
        /// </summary>
        private void LogDirectoryInitializationFailure(string cacheDirectoryAbsolutePath, Exception exception)
        {
            _logger.LogMessage(
                AppLogLevel.Warning,
                $"Failed to create IL cache directory '{cacheDirectoryAbsolutePath}' ({exception.GetType().Name}): {exception.Message}",
                shouldOutputMessageToConsole: true,
                exception);
        }

        /// <summary>
        /// Logs a cache-file read/write failure.
        /// キャッシュファイル読み書き失敗をログします。
        /// </summary>
        private void LogFileOperationFailure(string operation, string cacheKey, string cacheFileAbsolutePath, Exception exception)
        {
            _logger.LogMessage(
                AppLogLevel.Warning,
                $"Failed to {operation} IL cache file '{cacheFileAbsolutePath}' in directory '{_cacheDirectoryAbsolutePath}' ({DescribeCacheKey(cacheKey)}, {exception.GetType().Name}): {exception.Message}",
                shouldOutputMessageToConsole: true,
                exception);
        }

        /// <summary>
        /// Logs a cache-file deletion failure.
        /// キャッシュファイル削除失敗をログします。
        /// </summary>
        private void LogDeleteFailure(string cacheFileAbsolutePath, Exception exception)
        {
            _logger.LogMessage(
                AppLogLevel.Warning,
                $"Failed to delete cache file '{cacheFileAbsolutePath}' in directory '{_cacheDirectoryAbsolutePath}' ({exception.GetType().Name}): {exception.Message}",
                shouldOutputMessageToConsole: true,
                exception);
        }

        private void LogRemoveFailure(string cacheKey, string cacheFileAbsolutePath, Exception exception)
        {
            _logger.LogMessage(
                AppLogLevel.Warning,
                $"Failed to remove disk cache file '{cacheFileAbsolutePath}' during LRU eviction in directory '{_cacheDirectoryAbsolutePath}' ({DescribeCacheKey(cacheKey)}, {exception.GetType().Name}): {exception.Message}",
                shouldOutputMessageToConsole: true,
                exception);
        }

        private static string DescribeCacheKey(string cacheKey) => $"cacheKeyLength={cacheKey.Length}";

        private static void PrepareCacheFileForOverwrite(string cacheFileAbsolutePath)
        {
            if (!File.Exists(cacheFileAbsolutePath))
            {
                return;
            }

            DeleteCacheFile(cacheFileAbsolutePath);
        }

        private static void DeleteCacheFile(string cacheFileAbsolutePath)
        {
            var attributes = File.GetAttributes(cacheFileAbsolutePath);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(cacheFileAbsolutePath, attributes & ~FileAttributes.ReadOnly);
            }

            File.Delete(cacheFileAbsolutePath);
        }
    }
}
