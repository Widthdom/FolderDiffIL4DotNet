using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using FolderDiffIL4DotNet.Core.IO;

namespace FolderDiffIL4DotNet.Services.Caching
{
    /// <summary>
    /// IL キャッシュのメモリ層。IL テキストとファイル MD5 を保持し、TTL と LRU を管理します。
    /// </summary>
    internal sealed class ILMemoryCache
    {
        /// <summary>
        /// メモリキャッシュの既定最大件数。
        /// </summary>
        internal const int DefaultMaxEntries = 1000;

        /// <summary>
        /// メモリ上の IL キャッシュ本体。
        /// </summary>
        private readonly ConcurrentDictionary<string, CacheEntry> _ilEntries = new(StringComparer.Ordinal);

        /// <summary>
        /// ファイルパスに対する MD5 の計算結果をキャッシュする辞書。
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _md5HashCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// メモリキャッシュの最大保持エントリ数。
        /// </summary>
        private readonly int _maxEntries;

        /// <summary>
        /// 各エントリの有効期間 (Time To Live)。null の場合は無期限。
        /// </summary>
        private readonly TimeSpan? _timeToLive;

        /// <summary>
        /// LRU 方式での削除処理の同期用ロック。
        /// </summary>
        private readonly object _lruLock = new();

        /// <summary>
        /// LRU で削除された累計件数。
        /// </summary>
        private long _evictedCount = 0;

        /// <summary>
        /// TTL 失効により削除された累計件数。
        /// </summary>
        private long _expiredCount = 0;

        /// <summary>
        /// 現在までの削除統計 (Evicted: LRU, Expired: TTL)
        /// </summary>
        internal (long Evicted, long Expired) Stats => (_evictedCount, _expiredCount);

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="maxEntries">メモリキャッシュ最大件数。0 以下は既定値。</param>
        /// <param name="timeToLive">各エントリの有効期間。null で無期限。</param>
        internal ILMemoryCache(int maxEntries, TimeSpan? timeToLive)
        {
            _maxEntries = maxEntries <= 0 ? DefaultMaxEntries : maxEntries;
            _timeToLive = timeToLive;
        }

        /// <summary>
        /// 指定ファイルの MD5 を取得します。
        /// </summary>
        /// <param name="fileAbsolutePath">対象ファイル絶対パス。</param>
        /// <returns>MD5 (hex lowercase)</returns>
        /// <exception cref="IOException">MD5 計算中に I/O 例外が発生した場合。</exception>
        /// <exception cref="UnauthorizedAccessException">MD5 計算対象ファイルにアクセスできない場合。</exception>
        internal string GetFileHash(string fileAbsolutePath) =>
            _md5HashCache.GetOrAdd(fileAbsolutePath, static path => FileComparer.ComputeFileMd5Hex(path));

        /// <summary>
        /// 指定キーの IL テキスト取得を試みます。
        /// </summary>
        /// <param name="cacheKey">取得対象キー。</param>
        /// <param name="ilText">ヒット時の IL テキスト。</param>
        /// <returns>ヒット時 true。</returns>
        internal bool TryGet(string cacheKey, out string ilText)
        {
            if (!_ilEntries.TryGetValue(cacheKey, out var entry))
            {
                ilText = null;
                return false;
            }

            if (_timeToLive.HasValue && (DateTime.UtcNow - entry.CreatedUtc) > _timeToLive.Value)
            {
                _ilEntries.TryRemove(cacheKey, out _);
                Interlocked.Increment(ref _expiredCount);
                ilText = null;
                return false;
            }

            _ilEntries[cacheKey] = entry with { LastAccessUtc = DateTime.UtcNow };
            ilText = entry.ILText;
            return true;
        }

        /// <summary>
        /// 指定キーの IL テキストを保存します。
        /// </summary>
        /// <param name="cacheKey">保存対象キー。</param>
        /// <param name="ilText">保存する IL テキスト。</param>
        /// <returns>LRU により追い出されたキー。追い出しが無ければ null。</returns>
        internal string Store(string cacheKey, string ilText)
        {
            ArgumentNullException.ThrowIfNull(cacheKey);

            var now = DateTime.UtcNow;
            if (_ilEntries.ContainsKey(cacheKey))
            {
                _ilEntries[cacheKey] = new CacheEntry(ilText, now, now);
                return null;
            }

            var evictedKey = EnsureCapacityForInsert();
            _ilEntries[cacheKey] = new CacheEntry(ilText, now, now);
            return evictedKey;
        }

        /// <summary>
        /// 新規挿入前にメモリキャッシュ上限を超えないようにし、必要であれば最終アクセスが最も古いエントリを削除します。
        /// </summary>
        /// <returns>削除したキャッシュキー。削除がなければ null。</returns>
        private string EnsureCapacityForInsert()
        {
            if (_ilEntries.Count < _maxEntries)
            {
                return null;
            }

            lock (_lruLock)
            {
                if (_ilEntries.Count < _maxEntries)
                {
                    return null;
                }

                var oldestEntryKey = FindOldestEntryKey();
                if (oldestEntryKey == null || !_ilEntries.TryRemove(oldestEntryKey, out _))
                {
                    return null;
                }

                Interlocked.Increment(ref _evictedCount);
                return oldestEntryKey;
            }
        }

        /// <summary>
        /// 最終アクセス時刻が最も古いエントリのキャッシュキーを探します。
        /// </summary>
        /// <returns>削除対象のキー。キャッシュが空なら null。</returns>
        private string FindOldestEntryKey()
        {
            string oldestCacheKey = null;
            DateTime oldestLastAccessUtc = DateTime.MaxValue;
            foreach (var keyAndValue in _ilEntries)
            {
                if (keyAndValue.Value.LastAccessUtc < oldestLastAccessUtc)
                {
                    oldestLastAccessUtc = keyAndValue.Value.LastAccessUtc;
                    oldestCacheKey = keyAndValue.Key;
                }
            }

            return oldestCacheKey;
        }

        /// <summary>
        /// メモリキャッシュエントリ。
        /// </summary>
        /// <param name="ILText">IL テキスト。</param>
        /// <param name="LastAccessUtc">最終アクセス日時。</param>
        /// <param name="CreatedUtc">生成日時。</param>
        private sealed record CacheEntry(string ILText, DateTime LastAccessUtc, DateTime CreatedUtc);
    }
}
