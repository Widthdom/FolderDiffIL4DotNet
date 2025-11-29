using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// ファイル比較結果のリストを保持する静的クラス。
    /// </summary>
    public static class FileDiffResultLists
    {
        /// <summary>
        /// ファイルごとの一致/不一致の判定根拠を表す列挙型。
        /// </summary>
        public enum DiffDetailResult
        {
            MD5Match, // MD5ハッシュが一致
            MD5Mismatch, // MD5ハッシュが不一致
            ILMatch, // IL（中間言語）ベースで一致（ビルド固有情報の差異は無視）
            ILMismatch, // IL（中間言語）ベースで不一致（ビルド固有情報の差異は無視）
            TextMatch, // テキストベースで一致
            TextMismatch // テキストベースで不一致
        }

        /// <summary>
        /// IgnoredExtensions により比較対象から除外されたファイルがどのフォルダに存在したかを示すフラグ。
        /// </summary>
        [Flags]
        public enum IgnoredFileLocation
        {
            None = 0,
            Old = 1,
            New = 2
        }
        #region public properties
        /// <summary>
        /// 旧バージョン側（比較元）ファイルの絶対パスのリスト
        /// </summary>
        public static List<string> OldFilesAbsolutePath { get; set; } = [];

        /// <summary>
        /// 新バージョン側（比較先）ファイルの絶対パスのリスト
        /// </summary>
        public static List<string> NewFilesAbsolutePath { get; set; } = [];

        /// <summary>
        /// 差異のないファイルの相対パスのリスト
        /// </summary>
        public static List<string> UnchangedFilesRelativePath { get; set; } = [];

        /// <summary>
        /// 追加されたファイルの絶対パスのリスト
        /// </summary>
        public static List<string> AddedFilesAbsolutePath { get; set; } = [];

        /// <summary>
        /// 削除されたファイルの絶対パスのリスト
        /// </summary>
        public static List<string> RemovedFilesAbsolutePath { get; set; } = [];

        /// <summary>
        /// 変更されたファイルの相対パスのリスト
        /// </summary>
        public static List<string> ModifiedFilesRelativePath { get; set; } = [];

        /// <summary>
        /// ファイル間の比較結果を保持する辞書 (並列比較で安全に書き込みできるよう ConcurrentDictionary)。
        /// </summary>
        public static ConcurrentDictionary<string, DiffDetailResult> FileRelativePathToDiffDetailDictionary { get; } = new ConcurrentDictionary<string, DiffDetailResult>(StringComparer.Ordinal);

        /// <summary>
        /// 1 件以上のファイルが <see cref="DiffDetailResult.MD5Mismatch"/> と判定されているかどうか。
        /// </summary>
        public static bool HasAnyMd5Mismatch => FileRelativePathToDiffDetailDictionary.Values.Any(result => result == DiffDetailResult.MD5Mismatch);

        /// <summary>
        /// IgnoredExtensions 対象ファイルの相対パスと所在（旧/新フォルダ）情報。
        /// </summary>
        public static ConcurrentDictionary<string, IgnoredFileLocation> IgnoredFilesRelativePathToLocation { get; } = new ConcurrentDictionary<string, IgnoredFileLocation>(StringComparer.OrdinalIgnoreCase);
        #endregion

        /// <summary>
        /// ファイルの比較結果を記録します。
        /// </summary>
        /// <param name="fileRelativePath">ファイルの相対パス</param>
        /// <param name="diffDetailResult">ファイルごとの一致/不一致の判定根拠</param>
        /// <exception cref="ArgumentNullException">fileRelativePath または diffDetailResult が null の場合にスローされます。</exception>
        /// <exception cref="ArgumentException">fileRelativePath が既に辞書に存在する場合にスローされます。</exception>
        public static void RecordDiffDetail(string fileRelativePath, DiffDetailResult diffDetailResult)
        {
            // 既に存在する場合は上書き、存在しなければ追加 (スレッドセーフ)
            FileRelativePathToDiffDetailDictionary[fileRelativePath] = diffDetailResult;
        }

        /// <summary>
        /// IgnoredExtensions に該当したファイルの所在情報を記録します。
        /// </summary>
        /// <param name="fileRelativePath">フォルダ基準の相対パス。</param>
        /// <param name="location">旧/新のどちらに存在したかを示すフラグ。</param>
        /// <exception cref="ArgumentException">fileRelativePath が null または空白の場合。</exception>
        public static void RecordIgnoredFile(string fileRelativePath, IgnoredFileLocation location)
        {
            if (string.IsNullOrWhiteSpace(fileRelativePath))
            {
                throw new ArgumentException("fileRelativePath cannot be null or whitespace.", nameof(fileRelativePath));
            }
            IgnoredFilesRelativePathToLocation.AddOrUpdate(fileRelativePath, location, (_, existing) => existing | location);
        }
    }
}
