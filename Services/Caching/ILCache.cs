using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services.Caching
{
    /// <summary>
    /// IL 逆アセンブル結果をメモリ + 任意のディスクにキャッシュするクラス。
    /// キー: ファイル内容の MD5 + 使用ツールラベル
    /// </summary>
    /// <remarks>
    /// このキャッシュは、同じバイナリを何度も逆アセンブルしないための仕組みです。
    /// - キーは「ファイル内容のMD5」+「使用した逆アセンブラとそのバージョン（toolLabel）」の組み合わせです。
    /// - メモリ上のキャッシュ（高速）と、任意でディスク上のキャッシュ（プロセスをまたいで有効）を併用します。
    /// - TTL（有効期限）と LRU（最近使われていないものから削除）でメモリ使用量を制御します。
    /// - ディスク上ではファイル名に使用できない文字をサニタイズし、必要に応じて短縮します。
    ///
    /// 典型的な使い方（擬似コード）:
    /// 1) PrecomputeAsync で対象ファイル群の MD5 を先読み（任意）
    /// 2) TryGetAsync で IL テキストのキャッシュを検索
    ///    - 見つかればそれを利用
    ///    - 見つからなければ逆アセンブルを実行
    /// 3) 逆アセンブル結果を SetAsync でキャッシュへ保存
    ///
    /// スレッド安全性:
    /// - 複数スレッドから同時に呼び出される前提で、ConcurrentDictionary とロックを併用して安全に動作します。
    /// </remarks>
    public sealed class ILCache
    {
        #region constants
        /// <summary>
        /// キャッシュキーの区切り
        /// </summary>
        private const string KEY_SEPARATOR = "_";

        /// <summary>
        /// MD5 プリコンピュート共通プレフィックス
        /// </summary>
        private const string LOG_PRECOMPUTE_MD5_PREFIX = $"Precompute {Constants.LABEL_MD5}";

        /// <summary>
        /// MD5 プリコンピュート開始ログ
        /// </summary>
        private const string LOG_PRECOMPUTE_MD5_START = LOG_PRECOMPUTE_MD5_PREFIX + ": starting for {0} files ({1}={2})";

        /// <summary>
        /// MD5 プリコンピュート進捗ログ
        /// </summary>
        private const string LOG_PRECOMPUTE_MD5_PROGRESS = LOG_PRECOMPUTE_MD5_PREFIX + ": {0}/{1} ({2}%)";

        /// <summary>
        /// MD5 プリコンピュート完了ログ
        /// </summary>
        private const string LOG_PRECOMPUTE_MD5_COMPLETE = LOG_PRECOMPUTE_MD5_PREFIX + ": completed for {0} files";

        /// <summary>
        /// MD5 プリコンピュート失敗ログ
        /// </summary>
        private const string LOG_FAILED_PRECOMPUTE_MD5_FILE = "Failed to " + LOG_PRECOMPUTE_MD5_PREFIX + " for file '{0}'. This file will be skipped in the cache.";

        /// <summary>
        /// IL キャッシュディレクトリ作成失敗
        /// </summary>
        private const string LOG_FAILED_CREATE_IL_CACHE_DIR = "Failed to create " + Constants.LABEL_IL_CACHE + " directory '{0}': {1}";

        /// <summary>
        /// IL キャッシュファイル操作失敗フォーマット
        /// </summary>
        private const string LOG_FAILED_IL_CACHE_FILE_FORMAT = "Failed to {0} " + Constants.LABEL_IL_CACHE + " file '{1}': {2}";

        /// <summary>
        /// IL キャッシュ拡張子
        /// </summary>
        private const string IL_CACHE_EXTENSION = ".ilcache";

        /// <summary>
        /// キャッシュファイル削除失敗ログ
        /// </summary>
        private const string LOG_FAILED_DELETE_CACHE_FILE = "Failed to delete cache file: {0}";

        /// <summary>
        /// LRU 除外時のディスクキャッシュ削除失敗ログ
        /// </summary>
        private const string LOG_FAILED_REMOVE_DISK_CACHE_FILE = "Failed to remove disk cache file '{0}' during LRU eviction.";

        /// <summary>
        /// ディスククォータ調整ログ
        /// </summary>
        private const string LOG_DISK_QUOTA_TRIM = "Disk quota trim: removed={0}, remain={1}, bytes={2}";

        /// <summary>
        /// IL キャッシュ統計ログ
        /// </summary>
        private const string LOG_IL_CACHE_STATS = Constants.LABEL_IL_CACHE + " stats: hits={0}, stores={1}, evicted={2}, expired={3}";

        /// <summary>
        /// 1 KiB (2^10) をlong 型で扱う値。
        /// </summary>
        private const long BYTES_PER_KILOBYTE_LONG = 1024L;

        /// <summary>
        /// メモリキャッシュの既定最大件数。
        /// </summary>
        private const int DEFAULT_MAX_MEMORY_ENTRIES = 1000;

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
        #endregion

        #region private read-only variables
        /// <summary>
        /// メモリ上の IL キャッシュ本体。
        /// Key ：ファイル内容MD5 + ツールラベル)
        /// Value ：IL文字列, 最終アクセス日時（協定世界時）, 生成日時（協定世界時）
        /// </summary>
        private readonly ConcurrentDictionary<string, (string ILText, DateTime LastAccessUtc, DateTime CreatedUtc)> _memoryILCacheDictionary = new(StringComparer.Ordinal);

        /// <summary>
        /// ファイルパスに対するMD5 の計算結果をキャッシュする辞書
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _md5HashCacheDictionary = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// ディスクキャッシュ用ルートディレクトリ（未指定 or 作成失敗時 null 同等）
        /// </summary>
        private readonly string _ilCacheDirectoryAbsolutePath;

        /// <summary>
        /// ディスクキャッシュが有効かどうか
        /// </summary>
        private readonly bool _isDiskCacheEnabled;

        /// <summary>
        /// メモリキャッシュの最大保持エントリ数（超過時に古いものを削除）
        /// </summary>
        private readonly int _ilCacheMaxMemoryEntries;

        /// <summary>
        /// 各エントリの有効期間 (Time To Live)。null の場合は無期限。
        /// </summary>
        private readonly TimeSpan? _timeToLive;

        /// <summary>
        /// LRU（Least Recently Used）方式での削除処理の同期用ロック
        /// </summary>
        private readonly object _lruLock = new();

        /// <summary>
        /// ディスクキャッシュに保持できる最大ファイル数。
        /// 0 以下の場合は無制限。上限超過時は最終アクセスが古い順に削除します。
        /// </summary>
        private readonly int _ilCacheMaxDiskFileCount; // 0 = unlimited

        /// <summary>
        /// ディスクキャッシュの総サイズ上限（バイト単位）。
        /// 0 以下の場合は無制限。上限超過時はサイズが下回るまで古い順に削除します。
        /// </summary>
        private readonly long _ilCacheMaxDiskBytes;   // 0 = unlimited

        /// <summary>
        /// ディスククォータ適用処理（件数・サイズ制御）の同期用ロックオブジェクト。
        /// </summary>
        private readonly object _diskQuotaLock = new();
        #endregion

        #region private member variables
        /// <summary>
        /// LRU（Least Recently Used）方式で削除(退避)された累計件数
        /// </summary>
        private long _evictedCount = 0;

        /// <summary>
        /// TTL 失効により削除された累計件数
        /// </summary>
        private long _expiredCount = 0;

        /// <summary>
        /// 統計ログのためのアクセスヒットカウンタ（<see cref="FolderDiffService"/> 側でも集計しているが内部周期ログ用）
        /// </summary>
        private long _internalHits = 0;

        /// <summary>
        /// 統計ログのためのアクセス保存カウンタ（<see cref="FolderDiffService"/> 側でも集計しているが内部周期ログ用）
        /// </summary>
        private long _internalStores = 0;

        /// <summary>
        /// 統計ログの最終出力時刻（Ticks）
        /// </summary>
        private long _lastStatsLogTicks = 0;

        /// <summary>
        /// 内部キャッシュ統計ログ（ヒット率など）を出力する最小間隔。
        /// コンストラクタの statsLogIntervalSeconds で上書きされ、既定は 60 秒です。
        /// </summary>
        private static TimeSpan StatsLogInterval = TimeSpan.FromSeconds(60);
        #endregion

        #region public properties
        /// <summary>
        /// 現在までの削除統計 (Evicted: LRU, Expired: TTL)
        /// </summary>
        public (long Evicted, long Expired) Stats => (_evictedCount, _expiredCount);
        #endregion

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="ilCacheDirectoryAbsolutePath">ディスクキャッシュ格納ディレクトリ（null/空で無効。作成に失敗した場合も無効）</param>
        /// <param name="ilCacheMaxMemoryEntries">メモリキャッシュ最大件数（0 以下は既定 DEFAULT_MAX_MEMORY_ENTRIES を採用。超過時は LRU で削除）</param>
        /// <param name="timeToLive">各エントリのTime-To-Live（有効期間）（null で無期限。期限切れは参照時にパージ）</param>
        /// <param name="statsLogIntervalSeconds">内部統計ログ（ヒット率など）の出力間隔（秒）。0 以下で既定の DEFAULT_STATS_LOG_INTERVAL_SECONDS 秒。</param>
        /// <param name="ilCacheMaxDiskFileCount">ディスクキャッシュの最大ファイル数（0 以下で無制限。超過時は古い順に削除）</param>
        /// <param name="ilCacheMaxDiskMegabytes">ディスクキャッシュのサイズ上限（MB）。0 以下で無制限。超過時は古い順に削除</param>
        /// <remarks>
        /// - directory が有効かつ作成に成功した場合のみディスクキャッシュ（_diskEnabled）が有効になります。
        /// - 統計ログの出力間隔は静的フィールド（全インスタンス共通）の <see cref="StatsLogInterval"/> に反映されます。
        /// - ディスクキャッシュの件数／サイズ制御は書き込み後に <see cref="EnforceDiskQuota"/> で適用されます。
        /// </remarks>
        public ILCache(string ilCacheDirectoryAbsolutePath, int ilCacheMaxMemoryEntries = DEFAULT_MAX_MEMORY_ENTRIES, TimeSpan? timeToLive = null, int statsLogIntervalSeconds = DEFAULT_STATS_LOG_INTERVAL_SECONDS, int ilCacheMaxDiskFileCount = 0, long ilCacheMaxDiskMegabytes = 0)
        {
            // メモリキャッシュ容量（0 以下なら既定の DEFAULT_MAX_MEMORY_ENTRIES 件）
            _ilCacheMaxMemoryEntries = ilCacheMaxMemoryEntries <= 0 ? DEFAULT_MAX_MEMORY_ENTRIES : ilCacheMaxMemoryEntries;
            // TTL（null で無期限。失効チェックは参照時に実施）
            _timeToLive = timeToLive;
            // 統計ログの出力間隔（0 以下は DEFAULT_STATS_LOG_INTERVAL_SECONDS 秒に補正）
            if (statsLogIntervalSeconds <= 0)
            {
                statsLogIntervalSeconds = DEFAULT_STATS_LOG_INTERVAL_SECONDS;
            }
            StatsLogInterval = TimeSpan.FromSeconds(statsLogIntervalSeconds);

            _ilCacheMaxDiskFileCount = ilCacheMaxDiskFileCount;
            _ilCacheMaxDiskBytes = ilCacheMaxDiskMegabytes > 0
                ? ilCacheMaxDiskMegabytes * BYTES_PER_KILOBYTE_LONG * BYTES_PER_KILOBYTE_LONG
                : 0;
            // ディスクキャッシュ用ディレクトリの初期化（作成成功時のみ有効化）
            if (!string.IsNullOrWhiteSpace(ilCacheDirectoryAbsolutePath))
            {
                _ilCacheDirectoryAbsolutePath = ilCacheDirectoryAbsolutePath;
                try
                {
                    Utility.ValidateAbsolutePathLengthOrThrow(_ilCacheDirectoryAbsolutePath);
                    Directory.CreateDirectory(_ilCacheDirectoryAbsolutePath);
                    _isDiskCacheEnabled = true;
                }
                catch (Exception ex)
                {
                    _isDiskCacheEnabled = false;
                    LoggerService.LogMessage(
                        LoggerService.LogLevel.Warning,
                        string.Format(LOG_FAILED_CREATE_IL_CACHE_DIR, _ilCacheDirectoryAbsolutePath, ex.Message),
                        shouldOutputMessageToConsole: true,
                        ex);
                }
            }
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

            try
            {
                var files = fileAbsolutePaths as ICollection<string> ?? [.. fileAbsolutePaths];
                if (files.Count == 0)
                {
                    return Task.CompletedTask;
                }

                LoggerService.LogMessage(
                    LoggerService.LogLevel.Info,
                    string.Format(LOG_PRECOMPUTE_MD5_START, files.Count, nameof(maxParallel), maxParallel),
                    shouldOutputMessageToConsole: true);

                RunMd5Precompute(files, maxParallel);

                LoggerService.LogMessage(
                    LoggerService.LogLevel.Info,
                    string.Format(LOG_PRECOMPUTE_MD5_COMPLETE, files.Count),
                    shouldOutputMessageToConsole: true);
            }
            catch
            {
                // ignore
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// キャッシュから IL を取得
        /// まずメモリキャッシュを確認（期限も確認）し、次にディスクキャッシュを確認します。
        /// </summary>
        /// <param name="fileAbsolutePath">ファイルパス</param>
        /// <param name="toolLabel">ツールラベル</param>
        /// <returns>IL 文字列（未ヒット時 null）</returns>
        /// <exception cref="IOException">キャッシュキー生成時のハッシュ計算で I/O エラーが発生した場合。</exception>
        /// <exception cref="UnauthorizedAccessException">キャッシュ対象ファイルにアクセスできない場合。</exception>
        public async Task<string> TryGetILAsync(string fileAbsolutePath, string toolLabel)
        {
            // 1) キーを生成（MD5 + toolLabel）
            var ilCacheKey = BuildILCacheKey(fileAbsolutePath, toolLabel);
            if (_memoryILCacheDictionary.TryGetValue(ilCacheKey, out var ilCache))
            {
                // 2) メモリキャッシュを最初に確認。time to live が設定されている場合は期限切れをチェック
                if (_timeToLive.HasValue && (DateTime.UtcNow - ilCache.CreatedUtc) > _timeToLive.Value)
                {
                    // 期限切れならメモリから削除
                    _memoryILCacheDictionary.TryRemove(ilCacheKey, out _);

                    Interlocked.Increment(ref _expiredCount);
                    LogStatsIfIntervalElapsed();
                }
                else
                {
                    // 期限切れでない場合は、最終アクセス日時（協定世界時）をシステム日時で更新
                    _memoryILCacheDictionary[ilCacheKey] = (ilCache.ILText, DateTime.UtcNow, ilCache.CreatedUtc);

                    Interlocked.Increment(ref _internalHits);
                    LogStatsIfIntervalElapsed();
                    return ilCache.ILText;
                }
            }
            if (_isDiskCacheEnabled)
            {
                // 3) ディスクキャッシュが有効ならファイルの存在を確認
                var diskILCacheFileAbsolutePath = BuildILCacheFileAbsolutePath(ilCacheKey);
                if (File.Exists(diskILCacheFileAbsolutePath))
                {
                    try
                    {
                        // ヒットしたらファイルから内容(IL)を読み込み、
                        // メモリキャッシュに登録。最終アクセス日時（協定世界時）と生成日時（協定世界時）をシステム日時とする
                        var ilText = await File.ReadAllTextAsync(diskILCacheFileAbsolutePath);
                        _memoryILCacheDictionary[ilCacheKey] = (ilText, DateTime.UtcNow, DateTime.UtcNow);

                        Interlocked.Increment(ref _internalHits);
                        LogStatsIfIntervalElapsed();
                        return ilText;
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogMessage(
                            LoggerService.LogLevel.Warning,
                            string.Format(LOG_FAILED_IL_CACHE_FILE_FORMAT, "read", diskILCacheFileAbsolutePath, ex.Message),
                            shouldOutputMessageToConsole: true,
                            ex);
                    }
                }
            }
            // 4) どこにも無ければ呼び出し元で逆アセンブル実行し、成功後 SetAsync で登録
            return null;
        }

        /// <summary>
        /// キャッシュへ IL を保存
        /// </summary>
        /// <param name="fileAbsolutePath">ファイルパス</param>
        /// <param name="toolLabel">ツールラベル</param>
        /// <param name="ilText">IL 文字列</param>
        /// <returns>Task</returns>
        /// <exception cref="IOException">キャッシュキー生成時のハッシュ計算で I/O エラーが発生した場合。</exception>
        /// <exception cref="UnauthorizedAccessException">キャッシュ対象ファイルにアクセスできない場合。</exception>
        public async Task SetILAsync(string fileAbsolutePath, string toolLabel, string ilText)
        {
            if (string.IsNullOrEmpty(ilText))
            {
                return;
            }
            var ilCacheKey = BuildILCacheKey(fileAbsolutePath, toolLabel);
            StoreInMemoryCache(ilCacheKey, ilText);
            await PersistCacheIfNeeded(ilCacheKey, ilText);
            Interlocked.Increment(ref _internalStores);
            LogStatsIfIntervalElapsed();
        }

        #region private methods
        /// <summary>
        /// 指定ファイルの MD5 を取得（内部キャッシュ使用）。
        /// </summary>
        /// <param name="fileAbsolutePath">対象ファイル絶対パス</param>
        /// <returns>MD5 (hex lowercase)</returns>
        private string GetFileHash(string fileAbsolutePath)
        {
            // 同じパスに対しては MD5 を再計算しないよう、ConcurrentDictionary でキャッシュします。
            // 注意: ファイル内容が後から変更された場合でも、ここでは自動で無効化されません。
            //       呼び出し側では通常「比較対象のスナップショット」を処理する想定です。
            return _md5HashCacheDictionary.GetOrAdd(fileAbsolutePath, static path => Utility.ComputeFileMd5Hex(path));
        }

        /// <summary>
        /// キャッシュのキー文字列を生成。
        /// MD5 + <see cref="KEY_SEPARATOR"/> + toolLabel
        /// </summary>
        /// <param name="fileAbsolutePath">ファイルパス</param>
        /// <param name="toolLabel">ツールラベル（コマンド+バージョン）</param>
        /// <returns>キー</returns>
        // toolLabel にはコマンド名とバージョンが含まれます。
        // 後段のディスク保存では、このキーをサニタイズして安全なファイル名に変換します。
        private string BuildILCacheKey(string fileAbsolutePath, string toolLabel) => GetFileHash(fileAbsolutePath) + KEY_SEPARATOR + toolLabel;

        /// <summary>
        /// ディスクキャッシュファイルのパスを生成。
        /// </summary>
        /// <param name="ilCacheKey">キャッシュキー</param>
        /// <returns>パス（無効時は null）</returns>
        private string BuildILCacheFileAbsolutePath(string ilCacheKey)
        {
            if (_ilCacheDirectoryAbsolutePath == null)
            {
                return null;
            }
            // キャッシュキーをそのままファイル名に使用できない可能性が高いため、変換（サニタイズあるいは短縮）したうえで、拡張子を付けて返却します。
            return Path.Combine(_ilCacheDirectoryAbsolutePath, Utility.ToSafeFileName(ilCacheKey) + IL_CACHE_EXTENSION);
        }

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
                    // 個々のファイルの MD5 を計算し、内部キャッシュに投入（GetOrAdd）
                    GetFileHash(fileAbsolutePath);
                }
                catch (Exception ex)
                {
                    LoggerService.LogMessage(
                        LoggerService.LogLevel.Warning,
                        string.Format(LOG_FAILED_PRECOMPUTE_MD5_FILE, fileAbsolutePath),
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
        private static void LogPrecomputeProgress(int totalFiles, int processed, ref long lastLogTicks)
        {
            // 一定秒数間隔または一定件数ごとに進捗ログを出す（初回は直ちに出力）
            var nowTicks = DateTime.UtcNow.Ticks;
            var prev = Interlocked.Read(ref lastLogTicks);
            bool timeElapsed = new TimeSpan(nowTicks - prev).TotalSeconds >= PREFETCH_PROGRESS_LOG_INTERVAL_SECONDS;
            bool countStep = processed % PREFETCH_PROGRESS_LOG_STEP_COUNT == 0 || processed == totalFiles;
            if (!timeElapsed && !countStep)
            {
                return;
            }

            // 他スレッドが既にログを出していたらスキップ
            if (Interlocked.CompareExchange(ref lastLogTicks, nowTicks, prev) != prev)
            {
                return;
            }

            int percent = (int)(processed * 100.0 / totalFiles);

            LoggerService.LogMessage(
                LoggerService.LogLevel.Info,
                string.Format(LOG_PRECOMPUTE_MD5_PROGRESS, processed, totalFiles, percent),
                shouldOutputMessageToConsole: true);
        }

        /// <summary>
        /// メモリキャッシュへ指定キーの IL テキストを格納します。
        /// </summary>
        /// <param name="ilCacheKey">格納対象のキャッシュキー。</param>
        /// <param name="ilText">保存する IL テキスト。</param>
        /// <exception cref="ArgumentNullException"><paramref name="ilCacheKey"/> が null の場合。</exception>
        private void StoreInMemoryCache(string ilCacheKey, string ilText)
        {
            if (ilCacheKey == null)
            {
                throw new ArgumentNullException(nameof(ilCacheKey));
            }
            EnsureMemoryCapacity();
            _memoryILCacheDictionary[ilCacheKey] = (ilText, DateTime.UtcNow, DateTime.UtcNow);
        }

        /// <summary>
        /// メモリキャッシュの上限を超えないようにし、必要であれば最終アクセスが最も古いエントリを削除します。
        /// </summary>
        private void EnsureMemoryCapacity()
        {
            // まだ上限に達していなければ何もしない
            if (_memoryILCacheDictionary.Count < _ilCacheMaxMemoryEntries)
            {
                return;
            }

            lock (_lruLock)
            {
                // ロック獲得後も上限に余裕があれば抜ける
                if (_memoryILCacheDictionary.Count < _ilCacheMaxMemoryEntries)
                {
                    return;
                }

                // LRU を探し出し、削除に失敗したらそのまま終了
                var oldestEntryKey = FindOldestEntryKey();
                if (oldestEntryKey == null || !_memoryILCacheDictionary.TryRemove(oldestEntryKey, out _))
                {
                    return;
                }

                Interlocked.Increment(ref _evictedCount);
                RemoveDiskEntryIfNeeded(oldestEntryKey);
                LogStatsIfIntervalElapsed();
            }
        }

        /// <summary>
        /// 最終アクセス時刻が最も古いエントリのキャッシュキーを探します。
        /// </summary>
        /// <returns>削除対象のキー。キャッシュが空なら null。</returns>
        private string FindOldestEntryKey()
        {
            string oldestILCacheKey = null;
            DateTime oldestLastAccessUtc = DateTime.MaxValue;
            // すべてのエントリを走査し、最も古い最終アクセス時刻を持つキーを覚えておく
            foreach (var keyAndValue in _memoryILCacheDictionary)
            {
                if (keyAndValue.Value.LastAccessUtc < oldestLastAccessUtc)
                {
                    oldestLastAccessUtc = keyAndValue.Value.LastAccessUtc;
                    oldestILCacheKey = keyAndValue.Key;
                }
            }
            return oldestILCacheKey;
        }

        /// <summary>
        /// ディスクキャッシュが有効であれば、指定されたキーに対応するファイルを削除します。
        /// </summary>
        /// <param name="oldestILCacheKey">削除対象となるキャッシュキー。</param>
        private void RemoveDiskEntryIfNeeded(string oldestILCacheKey)
        {
            // ディスクキャッシュ無効時は処理不要
            if (!_isDiskCacheEnabled)
            {
                return;
            }
            try
            {
                // ファイルが存在する場合のみ削除
                var diskILCacheFileToRemoveAbsolutePath = BuildILCacheFileAbsolutePath(oldestILCacheKey);
                if (File.Exists(diskILCacheFileToRemoveAbsolutePath))
                {
                    File.Delete(diskILCacheFileToRemoveAbsolutePath);
                }
            }
            catch (Exception ex)
            {
                // 削除に失敗した場合は警告ログを吐く
                LoggerService.LogMessage(
                    LoggerService.LogLevel.Warning,
                    string.Format(LOG_FAILED_REMOVE_DISK_CACHE_FILE, BuildILCacheFileAbsolutePath(oldestILCacheKey)),
                    shouldOutputMessageToConsole: true,
                    ex);
            }
        }

        /// <summary>
        /// ディスクキャッシュ有効時に、指定キーの内容をディスクへ書き込みます。
        /// </summary>
        /// <param name="ilCacheKey">書き込み対象のキャッシュキー。</param>
        /// <param name="ilText">ディスクへ保存する IL テキスト。</param>
        /// <returns>書き込みとディスククォータ確認が完了すると完了状態になるタスク。</returns>
        private async Task PersistCacheIfNeeded(string ilCacheKey, string ilText)
        {
            // ディスクキャッシュが無効なら保存不要
            if (!_isDiskCacheEnabled)
            {
                return;
            }
            var diskILCacheFileToWriteAbsolutePath = BuildILCacheFileAbsolutePath(ilCacheKey);
            try
            {
                // テキストを書き込み、上限超過をチェック
                await File.WriteAllTextAsync(diskILCacheFileToWriteAbsolutePath, ilText);
                EnforceDiskQuota();
            }
            catch (Exception ex)
            {
                // 例外は握りつぶさず警告ログに残す
                LoggerService.LogMessage(
                    LoggerService.LogLevel.Warning,
                    string.Format(LOG_FAILED_IL_CACHE_FILE_FORMAT, "write", diskILCacheFileToWriteAbsolutePath, ex.Message),
                    shouldOutputMessageToConsole: true,
                    ex);
            }
        }

        /// <summary>
        /// ディスクキャッシュ容量制御（サイズと件数）。SetAsync で新規書き込み後に実行。
        /// </summary>
        private void EnforceDiskQuota()
        {
            if (!ShouldEnforceDiskQuota())
            {
                return;
            }

            lock (_diskQuotaLock)
            {
                var directoryInfo = new DirectoryInfo(_ilCacheDirectoryAbsolutePath);
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
        private bool ShouldEnforceDiskQuota() =>
            _isDiskCacheEnabled && (_ilCacheMaxDiskFileCount > 0 || _ilCacheMaxDiskBytes > 0);

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
            LogDiskQuotaTrim(removed, fileCount, totalBytes);
        }

        /// <summary>
        /// ファイル数または総サイズがディスククォータを超過しているかを判定します。
        /// </summary>
        /// <param name="fileCount">現在のファイル件数。</param>
        /// <param name="totalBytes">現在の総バイト数。</param>
        /// <returns>超過していれば true。</returns>
        private bool IsQuotaExceeded(int fileCount, long totalBytes) =>
            (_ilCacheMaxDiskFileCount > 0 && fileCount > _ilCacheMaxDiskFileCount) ||
            (_ilCacheMaxDiskBytes > 0 && totalBytes > _ilCacheMaxDiskBytes);

        /// <summary>
        /// 指定ファイルの削除を試み、失敗した場合は警告ログを出します。
        /// </summary>
        /// <param name="file">削除対象のファイル。</param>
        /// <returns>削除に成功した場合 true。</returns>
        private static bool TryDeleteCacheFile(FileInfo file)
        {
            try
            {
                file.Delete();
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogMessage(
                    LoggerService.LogLevel.Warning,
                    string.Format(LOG_FAILED_DELETE_CACHE_FILE, file.FullName),
                    shouldOutputMessageToConsole: true,
                    ex);
            }
            return false;
        }

        /// <summary>
        /// ディスククォータ削減の結果をログ出力します。
        /// </summary>
        /// <param name="removed">削除したファイル件数。</param>
        /// <param name="remainingCount">削除後のファイル件数。</param>
        /// <param name="remainingBytes">削除後の総バイト数。</param>
        private static void LogDiskQuotaTrim(int removed, int remainingCount, long remainingBytes)
        {
            if (removed <= 0)
            {
                return;
            }
            // どれだけ削除され、残数・残容量がいくつかをコンソール・ログ出力
            LoggerService.LogMessage(
                LoggerService.LogLevel.Info,
                string.Format(LOG_DISK_QUOTA_TRIM, removed, remainingCount, remainingBytes),
                shouldOutputMessageToConsole: true);
        }

        /// <summary>
        /// IL キャッシュの統計情報を設定された間隔でログします。
        /// </summary>
        /// <returns>なし。</returns>
        private void LogStatsIfIntervalElapsed()
        {
            if (StatsLogInterval <= TimeSpan.Zero)
            {
                return;
            }
            long nowTicks = DateTime.UtcNow.Ticks;
            long lastTicks = Interlocked.Read(ref _lastStatsLogTicks);
            if (lastTicks != 0 && (nowTicks - lastTicks) < StatsLogInterval.Ticks)
            {
                return;
            }
            if (Interlocked.CompareExchange(ref _lastStatsLogTicks, nowTicks, lastTicks) != lastTicks)
            {
                return;
            }
            LoggerService.LogMessage(
                LoggerService.LogLevel.Info,
                string.Format(LOG_IL_CACHE_STATS, _internalHits, _internalStores, _evictedCount, _expiredCount),
                shouldOutputMessageToConsole: true);
        }
        #endregion
    }
}
