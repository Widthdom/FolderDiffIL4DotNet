using System;
using System.Collections.Generic;
using System.IO;
using FolderDiffIL4DotNet.Core.IO;

namespace FolderDiffIL4DotNet.Services.Caching
{
    /// <summary>
    /// In-memory cache for file last-write timestamps (string form). Reuse via <see cref="GetOrAdd"/> and discard via <see cref="Clear"/> to reduce I/O.
    /// Values are held only during the process lifetime.
    /// ファイルの最終更新日時（文字列表現）をメモリ内にキャッシュするヘルパー。
    /// <see cref="GetOrAdd"/> で再利用し、<see cref="Clear"/> で破棄して I/O を削減します。値はプロセス実行中のみ保持されます。
    /// </summary>
    public static class TimestampCache
    {
        private static readonly Dictionary<string, string> cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Clears the internal cache. Recommended to call at the start of each execution / report cycle.
        /// 内部キャッシュをクリアします。1 実行（1 レポート）単位の開始時に呼び出すことを推奨します。
        /// </summary>
        public static void Clear() => cache.Clear();

        /// <summary>
        /// Returns the cached last-write timestamp for the file, computing it via <see cref="FileSystemUtility.GetTimestamp"/> on first access.
        /// 指定ファイルの最終更新日時（文字列表現）を取得します。未取得の場合は計算してキャッシュに保存します。
        /// </summary>
        public static string GetOrAdd(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                throw new ArgumentException($"Invalid value provided for timestamp retrieval: {absolutePath}", nameof(absolutePath));
            }
            if (cache.TryGetValue(absolutePath, out var timestamp))
            {
                return timestamp;
            }
            timestamp = FileSystemUtility.GetTimestamp(absolutePath);
            cache[absolutePath] = timestamp;
            return timestamp;
        }
    }
}
