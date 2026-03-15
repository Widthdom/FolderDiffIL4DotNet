using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Services.Caching
{
    /// <summary>
    /// IL 逆アセンブル結果をメモリ + 任意のディスクにキャッシュするクラス。
    /// キー: ファイル内容の MD5 + 使用ツールラベル
    /// </summary>
    /// <remarks>
    /// このクラスはキャッシュ全体の公開 API と調停のみを担います。
    /// 実際のメモリ保持は <see cref="ILMemoryCache"/>、永続化とディスククォータ制御は <see cref="ILDiskCache"/> に委譲します。
    /// </remarks>
    public sealed class ILCache
    {
        /// <summary>
        /// キャッシュキーの区切り
        /// </summary>
        private const string KEY_SEPARATOR = "_";

        /// <summary>
        /// 統計ログの既定出力間隔（秒）。
        /// </summary>
        private const int DEFAULT_STATS_LOG_INTERVAL_SECONDS = 60;

        /// <summary>
        /// プリフェッチ進捗ログを出力する最小間隔（秒）。
        /// </summary>
        private const int PREFETCH_PROGRESS_LOG_INTERVAL_SECONDS = 2;

        /// <summary>
        /// プリフェッチ進捗ログのステップ件数。
        /// </summary>
        private const int PREFETCH_PROGRESS_LOG_STEP_COUNT = 500;

        /// <summary>
        /// ログ出力サービス。
        /// </summary>
        private readonly ILoggerService _logger;

        /// <summary>
        /// メモリ上の IL / MD5 キャッシュ。
        /// </summary>
        private readonly ILMemoryCache _memoryCache;

        /// <summary>
        /// 永続化された IL キャッシュ。
        /// </summary>
        private readonly ILDiskCache _diskCache;

        /// <summary>
        /// 内部キャッシュ統計ログ（ヒット率など）を出力する最小間隔。
        /// </summary>
        private readonly TimeSpan _statsLogInterval;

        /// <summary>
        /// 統計ログのためのアクセスヒットカウンタ（<see cref="FolderDiffService"/> 側でも集計しているが内部周期ログ用）
        /// </summary>
        private long _internalHits = 0;

        /// <summary>
        /// 統計ログのためのアクセス保存カウンタ（<see cref="FolderDiffService"/> 側でも集計しているが内部周期ログ用）
        /// </summary>
        private long _internalStores = 0;

        /// <summary>
        /// 統計ログのためのアクセスミスカウンタ（メモリ・ディスク両方でキャッシュ未ヒットの件数）
        /// </summary>
        private long _internalMisses = 0;

        /// <summary>
        /// 統計ログの最終出力時刻（Ticks）
        /// </summary>
        private long _lastStatsLogTicks = 0;

        /// <summary>
        /// 現在までの削除統計 (Evicted: LRU, Expired: TTL)
        /// </summary>
        public (long Evicted, long Expired) Stats => _memoryCache.Stats;

        /// <summary>
        /// レポート出力用の IL キャッシュ統計情報を返します。
        /// </summary>
        /// <returns>ヒット数・ミス数・保存数・退避数などを含む統計情報。</returns>
        public ILCacheReportStats GetReportStats()
        {
            var (evicted, expired) = _memoryCache.Stats;
            long hits = Interlocked.Read(ref _internalHits);
            long misses = Interlocked.Read(ref _internalMisses);
            long stores = Interlocked.Read(ref _internalStores);
            return new ILCacheReportStats(hits, misses, stores, evicted, expired);
        }

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="ilCacheDirectoryAbsolutePath">ディスクキャッシュ格納ディレクトリ（null/空で無効。作成に失敗した場合も無効）</param>
        /// <param name="logger">ログ出力サービス（未指定時は <see cref="LoggerService"/> を使用）。</param>
        /// <param name="ilCacheMaxMemoryEntries">メモリキャッシュ最大件数（0 以下は既定値。超過時は LRU で削除）</param>
        /// <param name="timeToLive">各エントリの Time-To-Live（有効期間）（null で無期限。期限切れは参照時にパージ）</param>
        /// <param name="statsLogIntervalSeconds">内部統計ログ（ヒット率など）の出力間隔（秒）。0 以下で既定値。</param>
        /// <param name="ilCacheMaxDiskFileCount">ディスクキャッシュの最大ファイル数（0 以下で無制限。超過時は古い順に削除）</param>
        /// <param name="ilCacheMaxDiskMegabytes">ディスクキャッシュのサイズ上限（MB）。0 以下で無制限。超過時は古い順に削除</param>
        public ILCache(string ilCacheDirectoryAbsolutePath, ILoggerService logger = null, int ilCacheMaxMemoryEntries = ILMemoryCache.DefaultMaxEntries, TimeSpan? timeToLive = null, int statsLogIntervalSeconds = DEFAULT_STATS_LOG_INTERVAL_SECONDS, int ilCacheMaxDiskFileCount = 0, long ilCacheMaxDiskMegabytes = 0)
        {
            _logger = logger ?? new LoggerService();
            _memoryCache = new ILMemoryCache(ilCacheMaxMemoryEntries, timeToLive);
            _diskCache = new ILDiskCache(ilCacheDirectoryAbsolutePath, _logger, ilCacheMaxDiskFileCount, ilCacheMaxDiskMegabytes);

            if (statsLogIntervalSeconds <= 0)
            {
                statsLogIntervalSeconds = DEFAULT_STATS_LOG_INTERVAL_SECONDS;
            }

            _statsLogInterval = TimeSpan.FromSeconds(statsLogIntervalSeconds);
        }

        /// <summary>
        /// 対象ファイル群の MD5 を並列プリウォーム（後続キャッシュキー生成の I/O レイテンシを平準化）
        /// </summary>
        /// <param name="fileAbsolutePaths">対象ファイル絶対パス群</param>
        /// <param name="maxParallel">最大並列度（呼び出し側で決定済みの値を想定）</param>
        /// <returns>Task</returns>
        /// <exception cref="ArgumentOutOfRangeException">maxParallel が 0 以下の場合にスローされます。</exception>
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
                $"Precompute MD5: starting for {files.Count} files ({nameof(maxParallel)}={maxParallel})",
                shouldOutputMessageToConsole: true);

            RunMd5Precompute(files, maxParallel);

            _logger.LogMessage(
                AppLogLevel.Info,
                $"Precompute MD5: completed for {files.Count} files",
                shouldOutputMessageToConsole: true);
            return Task.CompletedTask;
        }

        /// <summary>
        /// キャッシュから IL を取得
        /// まずメモリキャッシュを確認（期限も確認）し、次にディスクキャッシュを確認します。
        /// </summary>
        /// <param name="fileAbsolutePath">ファイルパス</param>
        /// <param name="toolLabel">ツールラベル</param>
        /// <returns>IL 文字列（未ヒット時 null）</returns>
        /// <exception cref="System.IO.IOException">キャッシュキー生成時のハッシュ計算で I/O エラーが発生した場合。</exception>
        /// <exception cref="UnauthorizedAccessException">キャッシュ対象ファイルにアクセスできない場合。</exception>
        public async Task<string> TryGetILAsync(string fileAbsolutePath, string toolLabel)
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
        /// キャッシュへ IL を保存
        /// </summary>
        /// <param name="fileAbsolutePath">ファイルパス</param>
        /// <param name="toolLabel">ツールラベル</param>
        /// <param name="ilText">IL 文字列</param>
        /// <returns>Task</returns>
        /// <exception cref="System.IO.IOException">キャッシュキー生成時のハッシュ計算で I/O エラーが発生した場合。</exception>
        /// <exception cref="UnauthorizedAccessException">キャッシュ対象ファイルにアクセスできない場合。</exception>
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
        /// キャッシュのキー文字列を生成。
        /// MD5 + <see cref="KEY_SEPARATOR"/> + toolLabel
        /// </summary>
        /// <param name="fileAbsolutePath">ファイルパス</param>
        /// <param name="toolLabel">ツールラベル（コマンド+バージョン）</param>
        /// <returns>キー</returns>
        private string BuildILCacheKey(string fileAbsolutePath, string toolLabel) => _memoryCache.GetFileHash(fileAbsolutePath) + KEY_SEPARATOR + toolLabel;

        /// <summary>
        /// 指定されたファイル群に対して MD5 プリコンピュート処理を並列実行します。
        /// </summary>
        /// <param name="files">MD5 を計算する対象ファイルの一覧。</param>
        /// <param name="maxParallel">平行実行時の最大並列度。</param>
        private void RunMd5Precompute(ICollection<string> files, int maxParallel)
        {
            int processed = 0;
            long lastLogTicks = DateTime.UtcNow.Ticks;

            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, fileAbsolutePath =>
            {
                try
                {
                    _memoryCache.GetFileHash(fileAbsolutePath);
                }
                catch (System.IO.IOException ex)
                {
                    _logger.LogMessage(
                        AppLogLevel.Warning,
                        $"Failed to Precompute MD5 for file '{fileAbsolutePath}'. This file will be skipped in the cache.",
                        shouldOutputMessageToConsole: true,
                        ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogMessage(
                        AppLogLevel.Warning,
                        $"Failed to Precompute MD5 for file '{fileAbsolutePath}'. This file will be skipped in the cache.",
                        shouldOutputMessageToConsole: true,
                        ex);
                }
                catch (NotSupportedException ex)
                {
                    _logger.LogMessage(
                        AppLogLevel.Warning,
                        $"Failed to Precompute MD5 for file '{fileAbsolutePath}'. This file will be skipped in the cache.",
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
        /// プリコンピュート処理の進捗を一定間隔でログ出力します。
        /// </summary>
        /// <param name="totalFiles">対象ファイルの総数。</param>
        /// <param name="processed">現在までに処理済みの件数。</param>
        /// <param name="lastLogTicks">最後にログした時刻（Tick）。</param>
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
                $"Precompute MD5: {processed}/{totalFiles} ({percent}%)",
                shouldOutputMessageToConsole: true);
        }

        /// <summary>
        /// メモリ退避で追い出されたキーに対応するディスクエントリを削除します。
        /// </summary>
        /// <param name="evictedCacheKey">削除対象キー。null の場合は何もしません。</param>
        private void RemoveDiskEntryIfEvicted(string evictedCacheKey)
        {
            if (evictedCacheKey == null)
            {
                return;
            }

            _diskCache.Remove(evictedCacheKey);
        }

        /// <summary>
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
    /// レポートに出力する IL キャッシュ統計情報を保持するレコード。
    /// </summary>
    /// <param name="Hits">キャッシュヒット数（メモリ + ディスク）。</param>
    /// <param name="Misses">キャッシュミス数（メモリ・ディスク両方で未ヒット）。</param>
    /// <param name="Stores">キャッシュ保存数。</param>
    /// <param name="Evicted">LRU により退避されたエントリ数。</param>
    /// <param name="Expired">TTL 期限切れにより削除されたエントリ数。</param>
    public sealed record ILCacheReportStats(long Hits, long Misses, long Stores, long Evicted, long Expired)
    {
        /// <summary>
        /// キャッシュヒット率（%）。アクセスが 0 件の場合は 0.0。
        /// </summary>
        public double HitRatePct => (Hits + Misses) == 0 ? 0.0 : Hits * 100.0 / (Hits + Misses);
    }
}
