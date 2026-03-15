using System;
using System.Collections.Generic;
using System.IO;
using FolderDiffIL4DotNet.Core.IO;

namespace FolderDiffIL4DotNet.Services.Caching
{
    /// <summary>
    /// ファイルの最終更新日時（文字列表現）をメモリ内にキャッシュするヘルパー。
    /// 一度取得した値を <see cref="GetOrAdd"/> で再利用し、不要になったら <see cref="Clear"/> で破棄して I/O を削減します。
    /// 値は実行中のみ保持され、プロセス終了時に破棄されます。
    /// </summary>
    public static class TimestampCache
    {
        /// <summary>
        /// キャッシュされたファイルの最終更新日時（文字列表現）を保持する <see cref="Dictionary{TKey,TValue}"/>。
        /// </summary>
        private static readonly Dictionary<string, string> cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 内部 <see cref="cache"/> をクリアします。1 実行（1 レポート）単位の開始時に呼び出すことを推奨します。
        /// </summary>
        public static void Clear() => cache.Clear();

        /// <summary>
        /// 指定ファイルの最終更新日時（文字列表現）を取得します。未取得の場合は <see cref="FileSystemUtility.GetTimestamp"/>
        /// を呼び出して取得し、内部キャッシュに保存した上で返します。
        /// </summary>
        /// <param name="absolutePath">対象ファイルの絶対パス。null/空は不可。</param>
        /// <returns>「yyyy-MM-dd HH:mm:ss.fff zzz」形式の最終更新日時（文字列表現）。</returns>
        /// <exception cref="ArgumentException">absolutePath が null または空文字の場合。</exception>
        /// <exception cref="UnauthorizedAccessException">対象ファイルのメタデータ取得権限が不足している場合。</exception>
        /// <exception cref="NotSupportedException">パスの形式がサポートされない場合。</exception>
        /// <exception cref="IOException">タイムスタンプ取得中に I/O エラーが発生した場合。</exception>
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
