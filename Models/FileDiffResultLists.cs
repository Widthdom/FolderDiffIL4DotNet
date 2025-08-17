using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

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
    }
}
