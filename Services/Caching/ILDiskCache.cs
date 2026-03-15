using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services.Caching
{
    /// <summary>
    /// IL キャッシュのディスク層。ファイル I/O とディスククォータ制御を担当します。
    /// </summary>
    internal sealed class ILDiskCache
    {
        /// <summary>
        /// IL キャッシュ拡張子
        /// </summary>
        private const string IL_CACHE_EXTENSION = ".ilcache";

        /// <summary>
        /// 1 KiB (2^10) を long 型で扱う値。
        /// </summary>
        private const long BYTES_PER_KILOBYTE_LONG = 1024L;

        /// <summary>
        /// ログ出力サービス。
        /// </summary>
        private readonly ILoggerService _logger;

        /// <summary>
        /// ディスクキャッシュ用ルートディレクトリ（未指定 or 作成失敗時 null 同等）。
        /// </summary>
        private readonly string _cacheDirectoryAbsolutePath;

        /// <summary>
        /// ディスクキャッシュが有効かどうか。
        /// </summary>
        private readonly bool _isEnabled;

        /// <summary>
        /// ディスクキャッシュに保持できる最大ファイル数。
        /// </summary>
        private readonly int _maxDiskFileCount;

        /// <summary>
        /// ディスクキャッシュの総サイズ上限（バイト単位）。
        /// </summary>
        private readonly long _maxDiskBytes;

        /// <summary>
        /// ディスククォータ適用処理の同期用ロック。
        /// </summary>
        private readonly object _diskQuotaLock = new();

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="cacheDirectoryAbsolutePath">ディスクキャッシュ格納ディレクトリ。</param>
        /// <param name="logger">ログ出力サービス。</param>
        /// <param name="maxDiskFileCount">ディスクキャッシュの最大ファイル数。</param>
        /// <param name="maxDiskMegabytes">ディスクキャッシュのサイズ上限（MB）。</param>
        internal ILDiskCache(string cacheDirectoryAbsolutePath, ILoggerService logger, int maxDiskFileCount, long maxDiskMegabytes)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheDirectoryAbsolutePath = cacheDirectoryAbsolutePath;
            _maxDiskFileCount = maxDiskFileCount;
            _maxDiskBytes = maxDiskMegabytes > 0
                ? maxDiskMegabytes * BYTES_PER_KILOBYTE_LONG * BYTES_PER_KILOBYTE_LONG
                : 0;

            if (string.IsNullOrWhiteSpace(cacheDirectoryAbsolutePath))
            {
                _isEnabled = false;
                return;
            }

            _isEnabled = TryInitializeCacheDirectory(cacheDirectoryAbsolutePath);
        }

        /// <summary>
        /// 指定キーのキャッシュファイルを読み込みます。
        /// </summary>
        /// <param name="cacheKey">読み込み対象キー。</param>
        /// <returns>ヒット時の IL テキスト。未ヒットまたは読み込み失敗時は null。</returns>
        internal async Task<string> TryReadAsync(string cacheKey)
        {
            if (!_isEnabled)
            {
                return null;
            }

            var cacheFileAbsolutePath = BuildCacheFileAbsolutePath(cacheKey);
            if (!File.Exists(cacheFileAbsolutePath))
            {
                return null;
            }

            try
            {
                return await File.ReadAllTextAsync(cacheFileAbsolutePath);
            }
            catch (IOException ex)
            {
                LogFileOperationFailure("read", cacheFileAbsolutePath, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogFileOperationFailure("read", cacheFileAbsolutePath, ex);
            }
            catch (NotSupportedException ex)
            {
                LogFileOperationFailure("read", cacheFileAbsolutePath, ex);
            }

            return null;
        }

        /// <summary>
        /// 指定キーのキャッシュファイルを書き込みます。
        /// </summary>
        /// <param name="cacheKey">書き込み対象キー。</param>
        /// <param name="ilText">保存する IL テキスト。</param>
        /// <returns>書き込み完了タスク。</returns>
        internal async Task WriteAsync(string cacheKey, string ilText)
        {
            if (!_isEnabled)
            {
                return;
            }

            var cacheFileAbsolutePath = BuildCacheFileAbsolutePath(cacheKey);
            try
            {
                await File.WriteAllTextAsync(cacheFileAbsolutePath, ilText);
                EnforceQuota();
            }
            catch (IOException ex)
            {
                LogFileOperationFailure("write", cacheFileAbsolutePath, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogFileOperationFailure("write", cacheFileAbsolutePath, ex);
            }
            catch (NotSupportedException ex)
            {
                LogFileOperationFailure("write", cacheFileAbsolutePath, ex);
            }
        }

        /// <summary>
        /// 指定キーのキャッシュファイル削除を試みます。
        /// </summary>
        /// <param name="cacheKey">削除対象キー。</param>
        internal void Remove(string cacheKey)
        {
            if (!_isEnabled || cacheKey == null)
            {
                return;
            }

            var cacheFileAbsolutePath = BuildCacheFileAbsolutePath(cacheKey);
            try
            {
                if (File.Exists(cacheFileAbsolutePath))
                {
                    File.Delete(cacheFileAbsolutePath);
                }
            }
            catch (IOException ex)
            {
                _logger.LogMessage(
                    AppLogLevel.Warning,
                    $"Failed to remove disk cache file '{cacheFileAbsolutePath}' during LRU eviction.",
                    shouldOutputMessageToConsole: true,
                    ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogMessage(
                    AppLogLevel.Warning,
                    $"Failed to remove disk cache file '{cacheFileAbsolutePath}' during LRU eviction.",
                    shouldOutputMessageToConsole: true,
                    ex);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogMessage(
                    AppLogLevel.Warning,
                    $"Failed to remove disk cache file '{cacheFileAbsolutePath}' during LRU eviction.",
                    shouldOutputMessageToConsole: true,
                    ex);
            }
        }

        /// <summary>
        /// ディスクキャッシュ用ディレクトリを初期化します。
        /// </summary>
        /// <param name="cacheDirectoryAbsolutePath">初期化対象ディレクトリ。</param>
        /// <returns>初期化成功時 true。</returns>
        private bool TryInitializeCacheDirectory(string cacheDirectoryAbsolutePath)
        {
            try
            {
                PathValidator.ValidateAbsolutePathLengthOrThrow(cacheDirectoryAbsolutePath);
                Directory.CreateDirectory(cacheDirectoryAbsolutePath);
                return true;
            }
            catch (ArgumentException ex)
            {
                LogDirectoryInitializationFailure(cacheDirectoryAbsolutePath, ex);
            }
            catch (IOException ex)
            {
                LogDirectoryInitializationFailure(cacheDirectoryAbsolutePath, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogDirectoryInitializationFailure(cacheDirectoryAbsolutePath, ex);
            }
            catch (NotSupportedException ex)
            {
                LogDirectoryInitializationFailure(cacheDirectoryAbsolutePath, ex);
            }

            return false;
        }

        /// <summary>
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
        /// ディスククォータ監視が必要かどうかを判定します。
        /// </summary>
        /// <returns>監視が必要であれば true。</returns>
        private bool ShouldEnforceQuota() =>
            _isEnabled && (_maxDiskFileCount > 0 || _maxDiskBytes > 0);

        /// <summary>
        /// ディスクキャッシュファイルのパスを生成します。
        /// </summary>
        /// <param name="cacheKey">対象キャッシュキー。</param>
        /// <returns>キャッシュファイル絶対パス。</returns>
        private string BuildCacheFileAbsolutePath(string cacheKey) =>
            Path.Combine(_cacheDirectoryAbsolutePath, TextSanitizer.ToSafeFileName(cacheKey) + IL_CACHE_EXTENSION);

        /// <summary>
        /// キャッシュディレクトリ内のファイル一覧を取得し、最終更新日時の古い順に整列したスナップショットを返します。
        /// </summary>
        /// <param name="directoryInfo">キャッシュディレクトリの情報。</param>
        /// <returns>ファイル一覧と総バイト数のタプル。</returns>
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
        /// 上限を超えている場合に古いファイルから削除し、削除結果をログ出力します。
        /// </summary>
        /// <param name="orderedFiles">最終更新日時の古い順に並んだキャッシュファイル群。</param>
        /// <param name="initialTotalBytes">削除前の総バイト数。</param>
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
                    $"Disk quota trim: removed={removed}, remain={fileCount}, bytes={totalBytes}",
                    shouldOutputMessageToConsole: true);
            }
        }

        /// <summary>
        /// ファイル数または総サイズがディスククォータを超過しているかを判定します。
        /// </summary>
        /// <param name="fileCount">現在のファイル件数。</param>
        /// <param name="totalBytes">現在の総バイト数。</param>
        /// <returns>超過していれば true。</returns>
        private bool IsQuotaExceeded(int fileCount, long totalBytes) =>
            (_maxDiskFileCount > 0 && fileCount > _maxDiskFileCount) ||
            (_maxDiskBytes > 0 && totalBytes > _maxDiskBytes);

        /// <summary>
        /// 指定ファイルの削除を試み、失敗した場合は警告ログを出します。
        /// </summary>
        /// <param name="file">削除対象のファイル。</param>
        /// <returns>削除に成功した場合 true。</returns>
        private bool TryDeleteCacheFile(FileInfo file)
        {
            try
            {
                file.Delete();
                return true;
            }
            catch (IOException ex)
            {
                LogDeleteFailure(file.FullName, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogDeleteFailure(file.FullName, ex);
            }
            catch (NotSupportedException ex)
            {
                LogDeleteFailure(file.FullName, ex);
            }

            return false;
        }

        /// <summary>
        /// ディレクトリ初期化失敗をログします。
        /// </summary>
        /// <param name="cacheDirectoryAbsolutePath">対象ディレクトリ。</param>
        /// <param name="exception">発生した例外。</param>
        private void LogDirectoryInitializationFailure(string cacheDirectoryAbsolutePath, Exception exception)
        {
            _logger.LogMessage(
                AppLogLevel.Warning,
                $"Failed to create IL cache directory '{cacheDirectoryAbsolutePath}': {exception.Message}",
                shouldOutputMessageToConsole: true,
                exception);
        }

        /// <summary>
        /// キャッシュファイル読み書き失敗をログします。
        /// </summary>
        /// <param name="operation">操作名。</param>
        /// <param name="cacheFileAbsolutePath">対象キャッシュファイル。</param>
        /// <param name="exception">発生した例外。</param>
        private void LogFileOperationFailure(string operation, string cacheFileAbsolutePath, Exception exception)
        {
            _logger.LogMessage(
                AppLogLevel.Warning,
                $"Failed to {operation} IL cache file '{cacheFileAbsolutePath}': {exception.Message}",
                shouldOutputMessageToConsole: true,
                exception);
        }

        /// <summary>
        /// キャッシュファイル削除失敗をログします。
        /// </summary>
        /// <param name="cacheFileAbsolutePath">対象キャッシュファイル。</param>
        /// <param name="exception">発生した例外。</param>
        private void LogDeleteFailure(string cacheFileAbsolutePath, Exception exception)
        {
            _logger.LogMessage(
                AppLogLevel.Warning,
                $"Failed to delete cache file: {cacheFileAbsolutePath}",
                shouldOutputMessageToConsole: true,
                exception);
        }
    }
}
