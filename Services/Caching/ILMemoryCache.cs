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
        private readonly long _maxMemoryBytes;
        private readonly TimeSpan? _timeToLive;
        private readonly object _lruLock = new();
        private long _evictedCount = 0;
        private long _expiredCount = 0;
        private long _currentMemoryBytes = 0;

        /// <summary>
        /// Eviction statistics so far (Evicted: LRU, Expired: TTL).
        /// 現在までの削除統計 (Evicted: LRU, Expired: TTL)。
        /// </summary>
        internal (long Evicted, long Expired) Stats => (_evictedCount, _expiredCount);

        /// <summary>
        /// Approximate memory used by cached IL text in bytes.
        /// キャッシュされた IL テキストが使用しているおおよそのメモリ量（バイト）。
        /// </summary>
        internal long CurrentMemoryBytes => Interlocked.Read(ref _currentMemoryBytes);

        internal ILMemoryCache(int maxEntries, TimeSpan? timeToLive, long maxMemoryMegabytes = 0)
        {
            _maxEntries = maxEntries <= 0 ? DefaultMaxEntries : maxEntries;
            _timeToLive = timeToLive;
            // 0 or negative = unlimited (entry-count limit only)
            // 0 以下 = 無制限（エントリ数上限のみ）
            _maxMemoryBytes = maxMemoryMegabytes > 0 ? maxMemoryMegabytes * 1024L * 1024L : 0;
        }

        /// <summary>
        /// Returns the cached SHA256 hex string for the file, computing it on first access.
        /// 指定ファイルの SHA256 を取得します（初回アクセス時に計算しキャッシュ）。
        /// </summary>
        internal string GetFileHash(string fileAbsolutePath) =>
            _sha256HashCache.GetOrAdd(fileAbsolutePath, static path => FileComparer.ComputeFileSha256Hex(path));

        /// <summary>
        /// Pre-seeds the SHA256 hash for a file path, avoiding redundant recomputation.
        /// ファイルパスの SHA256 ハッシュを事前登録し、冗長な再計算を回避します。
        /// </summary>
        internal void PreSeedFileHash(string fileAbsolutePath, string sha256Hex)
        {
            if (!string.IsNullOrEmpty(fileAbsolutePath) && !string.IsNullOrEmpty(sha256Hex))
                _sha256HashCache.TryAdd(fileAbsolutePath, sha256Hex);
        }

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
                if (_ilEntries.TryRemove(cacheKey, out var removed))
                {
                    Interlocked.Add(ref _currentMemoryBytes, -EstimateStringBytes(removed.ILText));
                }
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

            long newEntryBytes = EstimateStringBytes(ilText);
            var now = DateTime.UtcNow;
            if (_ilEntries.TryGetValue(cacheKey, out var existing))
            {
                long oldBytes = EstimateStringBytes(existing.ILText);
                _ilEntries[cacheKey] = new CacheEntry(ilText, now, now);
                Interlocked.Add(ref _currentMemoryBytes, newEntryBytes - oldBytes);
                return null;
            }

            var evictedKey = EnsureCapacityForInsert(newEntryBytes);
            _ilEntries[cacheKey] = new CacheEntry(ilText, now, now);
            Interlocked.Add(ref _currentMemoryBytes, newEntryBytes);
            return evictedKey;
        }

        /// <summary>
        /// Ensures capacity for a new entry by evicting the least-recently-used entry if at entry-count or memory capacity.
        /// 新規挿入前にエントリ数またはメモリ上限を超えないようにし、必要であれば LRU エントリを削除します。
        /// </summary>
        private string? EnsureCapacityForInsert(long newEntryBytes)
        {
            bool needsEntryEviction = _ilEntries.Count >= _maxEntries;
            bool needsMemoryEviction = _maxMemoryBytes > 0 &&
                (Interlocked.Read(ref _currentMemoryBytes) + newEntryBytes) > _maxMemoryBytes;

            if (!needsEntryEviction && !needsMemoryEviction)
            {
                return null;
            }

            lock (_lruLock)
            {
                // Re-check under lock / ロック下で再チェック
                string? firstEvictedKey = null;

                // Evict until both entry count and memory budget are satisfied
                // エントリ数とメモリ予算の両方が満たされるまで削除
                while (_ilEntries.Count > 0)
                {
                    bool overCount = _ilEntries.Count >= _maxEntries;
                    bool overMemory = _maxMemoryBytes > 0 &&
                        (Interlocked.Read(ref _currentMemoryBytes) + newEntryBytes) > _maxMemoryBytes;

                    if (!overCount && !overMemory)
                    {
                        break;
                    }

                    var oldestEntryKey = FindOldestEntryKey();
                    if (oldestEntryKey == null || !_ilEntries.TryRemove(oldestEntryKey, out var removed))
                    {
                        break;
                    }

                    Interlocked.Add(ref _currentMemoryBytes, -EstimateStringBytes(removed.ILText));
                    Interlocked.Increment(ref _evictedCount);
                    firstEvictedKey ??= oldestEntryKey;
                }

                return firstEvictedKey;
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
        /// Estimates the in-memory byte size of a .NET string (2 bytes per char + object overhead).
        /// .NET 文字列のメモリ上のバイトサイズを推定します（1文字2バイト + オブジェクトオーバーヘッド）。
        /// </summary>
        private static long EstimateStringBytes(string? text)
            => text == null ? 0 : (long)text.Length * 2 + 56;

        /// <summary>
        /// A single memory-cache entry holding IL text and access timestamps.
        /// IL テキストとアクセスタイムスタンプを保持するメモリキャッシュエントリ。
        /// </summary>
        private sealed record CacheEntry(string ILText, DateTime LastAccessUtc, DateTime CreatedUtc);
    }
}
