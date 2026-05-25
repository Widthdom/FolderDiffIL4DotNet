using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Common;

namespace FolderDiffIL4DotNet.Services.Caching
{
    /// <summary>
    /// Caches IL disassembly results in memory and optionally on disk. Key = file-content SHA256 + tool label.
    /// IL 逆アセンブル結果をメモリ + 任意のディスクにキャッシュするクラス。キー: ファイル内容の SHA256 + 使用ツールラベル。
    /// </summary>
    /// <remarks>
    /// This class owns only the public API and coordination; actual memory storage is delegated to
    /// <see cref="ILMemoryCache"/> and persistence / disk-quota control to <see cref="ILDiskCache"/>.
    /// このクラスはキャッシュ全体の公開 API と調停のみを担います。
    /// 実際のメモリ保持は <see cref="ILMemoryCache"/>、永続化とディスククォータ制御は <see cref="ILDiskCache"/> に委譲します。
    /// </remarks>
    public sealed class ILCache
    {
        private const string KEY_SEPARATOR = "_";
        private const int DEFAULT_STATS_LOG_INTERVAL_SECONDS = 60;
        private const int PREFETCH_PROGRESS_LOG_INTERVAL_SECONDS = 2;
        private const int PREFETCH_PROGRESS_LOG_STEP_COUNT = 500;
        private readonly ILoggerService _logger;
        private readonly ILMemoryCache _memoryCache;
        private readonly ILDiskCache _diskCache;

        private readonly TimeSpan _statsLogInterval;

        // Internal counters for periodic stats logging (FolderDiffService keeps its own aggregates).
        // 内部周期ログ用の統計カウンタ（FolderDiffService 側でも別途集計）。
        private long _internalHits = 0;
        private long _internalStores = 0;
        private long _internalMisses = 0;
        private long _lastStatsLogTicks = 0;

        /// <summary>
        /// Eviction statistics so far (Evicted: LRU, Expired: TTL).
        /// 現在までの削除統計 (Evicted: LRU, Expired: TTL)。
        /// </summary>
        public (long Evicted, long Expired) Stats => _memoryCache.Stats;

        /// <summary>
        /// Returns IL cache statistics for report output.
        /// レポート出力用の IL キャッシュ統計情報を返します。
        /// </summary>
        public ILCacheReportStats GetReportStats()
        {
            var (evicted, expired) = _memoryCache.Stats;
            long hits = Interlocked.Read(ref _internalHits);
            long misses = Interlocked.Read(ref _internalMisses);
            long stores = Interlocked.Read(ref _internalStores);
            return new ILCacheReportStats(hits, misses, stores, evicted, expired);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ILCache"/> with the specified storage and quota settings.
        /// 指定のストレージおよびクォータ設定で <see cref="ILCache"/> の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="ilCacheDirectoryAbsolutePath">Absolute path to the disk cache directory. / ディスクキャッシュディレクトリの絶対パス。</param>
        /// <param name="logger">Logger for diagnostic output (defaults to a new <see cref="LoggerService"/>). / 診断出力用ロガー。</param>
        /// <param name="ilCacheMaxMemoryEntries">Maximum number of entries held in memory. / メモリ内最大エントリ数。</param>
        /// <param name="timeToLive">Optional TTL for memory entries. / メモリエントリの有効期間（省略可）。</param>
        /// <param name="statsLogIntervalSeconds">Interval in seconds between periodic cache statistics log output. / キャッシュ統計ログ出力の間隔（秒）。</param>
        /// <param name="ilCacheMaxDiskFileCount">Maximum number of files on disk (0 = unlimited). / ディスク上の最大ファイル数（0 = 無制限）。</param>
        /// <param name="ilCacheMaxDiskMegabytes">Maximum disk usage in MB (0 = unlimited). / ディスク使用量上限（MB、0 = 無制限）。</param>
        /// <param name="ilCacheMaxMemoryMegabytes">Memory budget in MB for in-memory IL cache (0 = unlimited). / メモリ内 IL キャッシュのメモリ予算（MB、0 = 無制限）。</param>
        public ILCache(string ilCacheDirectoryAbsolutePath, ILoggerService? logger = null, int ilCacheMaxMemoryEntries = ILMemoryCache.DefaultMaxEntries, TimeSpan? timeToLive = null, int statsLogIntervalSeconds = DEFAULT_STATS_LOG_INTERVAL_SECONDS, int ilCacheMaxDiskFileCount = 0, long ilCacheMaxDiskMegabytes = 0, long ilCacheMaxMemoryMegabytes = 0)
        {
            _logger = logger ?? new LoggerService();
            _memoryCache = new ILMemoryCache(ilCacheMaxMemoryEntries, timeToLive, ilCacheMaxMemoryMegabytes);
            _diskCache = new ILDiskCache(ilCacheDirectoryAbsolutePath, _logger, ilCacheMaxDiskFileCount, ilCacheMaxDiskMegabytes);

            if (statsLogIntervalSeconds <= 0)
            {
                statsLogIntervalSeconds = DEFAULT_STATS_LOG_INTERVAL_SECONDS;
            }

            _statsLogInterval = TimeSpan.FromSeconds(statsLogIntervalSeconds);
        }

        /// <summary>
        /// Pre-seeds the SHA256 hash for a file so that <see cref="BuildILCacheKey"/> does not recompute it.
        /// ファイルの SHA256 ハッシュを事前登録し、<see cref="BuildILCacheKey"/> での再計算を回避します。
        /// </summary>
        /// <param name="fileAbsolutePath">Absolute path to the file. / ファイルの絶対パス。</param>
        /// <param name="sha256Hex">64-character lowercase hex SHA256 hash. / 64 桁小文字 16 進 SHA256 ハッシュ。</param>
        public void PreSeedFileHash(string fileAbsolutePath, string sha256Hex)
            => _memoryCache.PreSeedFileHash(fileAbsolutePath, sha256Hex);

        /// <summary>
        /// Pre-warms SHA256 hashes of the target files in parallel to amortize I/O latency for subsequent cache-key generation.
        /// 対象ファイル群の SHA256 を並列プリウォームし、後続キャッシュキー生成の I/O レイテンシを平準化します。
        /// </summary>
        public Task PrecomputeAsync(IEnumerable<string> fileAbsolutePaths, int maxParallel)
        {
            if (fileAbsolutePaths == null)
            {
                return Task.CompletedTask;
            }

            if (maxParallel <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxParallel), maxParallel, Constants.ERROR_MAX_PARALLEL);
            }

            var files = fileAbsolutePaths as ICollection<string> ?? [.. fileAbsolutePaths];
            if (files.Count == 0)
            {
                return Task.CompletedTask;
            }

            _logger.LogMessage(
                AppLogLevel.Info,
                $"Precompute SHA256: starting for {files.Count} files ({nameof(maxParallel)}={maxParallel})",
                shouldOutputMessageToConsole: true);

            RunSha256Precompute(files, maxParallel);

            _logger.LogMessage(
                AppLogLevel.Info,
                $"Precompute SHA256: completed for {files.Count} files",
                shouldOutputMessageToConsole: true);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Looks up IL from the cache: checks memory first (including TTL), then falls back to disk.
        /// キャッシュから IL を取得します。まずメモリキャッシュを確認（期限も確認）し、次にディスクキャッシュを確認します。
        /// </summary>
        public async Task<string?> TryGetILAsync(string fileAbsolutePath, string toolLabel)
        {
            var ilCacheKey = BuildILCacheKey(fileAbsolutePath, toolLabel);
            if (_memoryCache.TryGet(ilCacheKey, out var memoryHit))
            {
                Interlocked.Increment(ref _internalHits);
                LogStatsIfIntervalElapsed();
                return memoryHit;
            }

            var diskHit = await _diskCache.TryReadAsync(ilCacheKey);
            if (diskHit == null)
            {
                Interlocked.Increment(ref _internalMisses);
                return null;
            }

            RemoveDiskEntryIfEvicted(_memoryCache.Store(ilCacheKey, diskHit));
            Interlocked.Increment(ref _internalHits);
            LogStatsIfIntervalElapsed();
            return diskHit;
        }

        /// <summary>
        /// Stores IL text into both memory and disk caches.
        /// キャッシュへ IL を保存します。
        /// </summary>
        public async Task SetILAsync(string fileAbsolutePath, string toolLabel, string ilText)
        {
            if (string.IsNullOrEmpty(ilText))
            {
                return;
            }

            var ilCacheKey = BuildILCacheKey(fileAbsolutePath, toolLabel);
            RemoveDiskEntryIfEvicted(_memoryCache.Store(ilCacheKey, ilText));
            await _diskCache.WriteAsync(ilCacheKey, ilText);

            Interlocked.Increment(ref _internalStores);
            LogStatsIfIntervalElapsed();
        }

        /// <summary>
        /// Builds a cache key: SHA256 + <see cref="KEY_SEPARATOR"/> + toolLabel.
        /// キャッシュキーを生成します: SHA256 + <see cref="KEY_SEPARATOR"/> + toolLabel。
        /// </summary>
        private string BuildILCacheKey(string fileAbsolutePath, string toolLabel) => _memoryCache.GetFileHash(fileAbsolutePath) + KEY_SEPARATOR + toolLabel;

        /// <summary>
        /// Runs SHA256 pre-computation in parallel for the given files.
        /// 指定されたファイル群に対して SHA256 プリコンピュート処理を並列実行します。
        /// </summary>
        private void RunSha256Precompute(ICollection<string> files, int maxParallel)
        {
            int processed = 0;
            long lastLogTicks = DateTime.UtcNow.Ticks;

            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, fileAbsolutePath =>
            {
                try
                {
                    _memoryCache.GetFileHash(fileAbsolutePath);
                }
                catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
                {
                    _logger.LogMessage(
                        AppLogLevel.Warning,
                        $"Failed to precompute SHA256 for file '{fileAbsolutePath}' (TotalFiles={files.Count}, MaxParallel={maxParallel}, {ex.GetType().Name}): {ex.Message}. This file will be skipped in the cache.",
                        shouldOutputMessageToConsole: true,
                        ex);
                }
                finally
                {
                    var done = Interlocked.Increment(ref processed);
                    LogPrecomputeProgress(files.Count, done, ref lastLogTicks);
                }
            });
        }

        /// <summary>
        /// Logs pre-computation progress at a throttled interval.
        /// プリコンピュート処理の進捗を一定間隔でログ出力します。
        /// </summary>
        private void LogPrecomputeProgress(int totalFiles, int processed, ref long lastLogTicks)
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            var prev = Interlocked.Read(ref lastLogTicks);
            bool timeElapsed = new TimeSpan(nowTicks - prev).TotalSeconds >= PREFETCH_PROGRESS_LOG_INTERVAL_SECONDS;
            bool countStep = processed % PREFETCH_PROGRESS_LOG_STEP_COUNT == 0 || processed == totalFiles;
            if (!timeElapsed && !countStep)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref lastLogTicks, nowTicks, prev) != prev)
            {
                return;
            }

            int percent = (int)(processed * 100.0 / totalFiles);

            _logger.LogMessage(
                AppLogLevel.Info,
                $"Precompute SHA256: {processed}/{totalFiles} ({percent}%)",
                shouldOutputMessageToConsole: true);
        }

        /// <summary>
        /// Removes the disk entry corresponding to a key evicted from memory.
        /// メモリ退避で追い出されたキーに対応するディスクエントリを削除します。
        /// </summary>
        private void RemoveDiskEntryIfEvicted(string? evictedCacheKey)
        {
            if (evictedCacheKey == null)
            {
                return;
            }

            _diskCache.Remove(evictedCacheKey);
        }

        /// <summary>
        /// Emits IL cache statistics at the configured interval.
        /// IL キャッシュの統計情報を設定された間隔でログします。
        /// </summary>
        private void LogStatsIfIntervalElapsed()
        {
            if (_statsLogInterval <= TimeSpan.Zero)
            {
                return;
            }

            long nowTicks = DateTime.UtcNow.Ticks;
            long lastTicks = Interlocked.Read(ref _lastStatsLogTicks);
            if (lastTicks != 0 && (nowTicks - lastTicks) < _statsLogInterval.Ticks)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _lastStatsLogTicks, nowTicks, lastTicks) != lastTicks)
            {
                return;
            }

            var (evicted, expired) = _memoryCache.Stats;
            _logger.LogMessage(
                AppLogLevel.Info,
                $"IL cache stats: hits={_internalHits}, misses={_internalMisses}, stores={_internalStores}, evicted={evicted}, expired={expired}",
                shouldOutputMessageToConsole: true);
        }
    }

    /// <summary>
    /// Immutable record holding IL cache statistics for report output.
    /// レポートに出力する IL キャッシュ統計情報を保持するレコード。
    /// </summary>
    public sealed record ILCacheReportStats(long Hits, long Misses, long Stores, long Evicted, long Expired)
    {
        /// <summary>
        /// Cache hit rate (%). Returns 0.0 when there are no accesses.
        /// キャッシュヒット率（%）。アクセスが 0 件の場合は 0.0。
        /// </summary>
        public double HitRatePct => (Hits + Misses) == 0 ? 0.0 : Hits * 100.0 / (Hits + Misses);
    }
}
