using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using FolderDiffIL4DotNet.Core.IO;

namespace FolderDiffIL4DotNet.Services.Caching
{
    /// <summary>
    /// Memory layer of the IL cache. Holds IL text and file SHA256 hashes, managing TTL expiry and LRU eviction.
    /// IL キャッシュのメモリ層。IL テキストとファイル SHA256 を保持し、TTL と LRU を管理します。
    /// </summary>
    internal sealed class ILMemoryCache
    {
        internal const int DefaultMaxEntries = 1000;
        private readonly ConcurrentDictionary<string, CacheEntry> _ilEntries = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, string> _sha256HashCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly int _maxEntries;
        private readonly TimeSpan? _timeToLive;
        private readonly object _lruLock = new();
        private long _evictedCount = 0;
        private long _expiredCount = 0;

        /// <summary>
        /// Eviction statistics so far (Evicted: LRU, Expired: TTL).
        /// 現在までの削除統計 (Evicted: LRU, Expired: TTL)。
        /// </summary>
        internal (long Evicted, long Expired) Stats => (_evictedCount, _expiredCount);

        internal ILMemoryCache(int maxEntries, TimeSpan? timeToLive)
        {
            _maxEntries = maxEntries <= 0 ? DefaultMaxEntries : maxEntries;
            _timeToLive = timeToLive;
        }

        /// <summary>
        /// Returns the cached SHA256 hex string for the file, computing it on first access.
        /// 指定ファイルの SHA256 を取得します（初回アクセス時に計算しキャッシュ）。
        /// </summary>
        internal string GetFileHash(string fileAbsolutePath) =>
            _sha256HashCache.GetOrAdd(fileAbsolutePath, static path => FileComparer.ComputeFileSha256Hex(path));

        /// <summary>
        /// Tries to retrieve IL text for the given key; expired entries are purged on access.
        /// 指定キーの IL テキスト取得を試みます。期限切れエントリは参照時にパージされます。
        /// </summary>
        internal bool TryGet(string cacheKey, out string? ilText)
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
        /// Stores IL text under the given key. Returns the evicted key (LRU) or null if no eviction occurred.
        /// 指定キーの IL テキストを保存します。LRU により追い出されたキーを返します（追い出しが無ければ null）。
        /// </summary>
        internal string? Store(string cacheKey, string ilText)
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
        /// Ensures capacity for a new entry by evicting the least-recently-used entry if at capacity.
        /// 新規挿入前にメモリキャッシュ上限を超えないようにし、必要であれば LRU エントリを削除します。
        /// </summary>
        private string? EnsureCapacityForInsert()
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
        /// Finds the cache key with the oldest last-access time.
        /// 最終アクセス時刻が最も古いエントリのキャッシュキーを探します。
        /// </summary>
        private string? FindOldestEntryKey()
        {
            string? oldestCacheKey = null;
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
        /// A single memory-cache entry holding IL text and access timestamps.
        /// IL テキストとアクセスタイムスタンプを保持するメモリキャッシュエントリ。
        /// </summary>
        private sealed record CacheEntry(string ILText, DateTime LastAccessUtc, DateTime CreatedUtc);
    }
}
