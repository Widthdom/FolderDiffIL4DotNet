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
        #region constants
        /// <summary>
        /// null/空白の場合のメッセージ
        /// </summary>
        private const string ERROR_FILE_RELATIVE_PATH_EMPTY = "{0} cannot be null or whitespace.";
        #endregion

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
        public static ConcurrentQueue<string> OldFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// 新バージョン側（比較先）ファイルの絶対パスのリスト
        /// </summary>
        public static ConcurrentQueue<string> NewFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// 差異のないファイルの相対パスのリスト
        /// </summary>
        public static ConcurrentQueue<string> UnchangedFilesRelativePath { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// 追加されたファイルの絶対パスのリスト
        /// </summary>
        public static ConcurrentQueue<string> AddedFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// 削除されたファイルの絶対パスのリスト
        /// </summary>
        public static ConcurrentQueue<string> RemovedFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// 変更されたファイルの相対パスのリスト
        /// </summary>
        public static ConcurrentQueue<string> ModifiedFilesRelativePath { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// ファイル間の比較結果を保持する辞書 (並列比較で安全に書き込みできるよう ConcurrentDictionary)。
        /// </summary>
        public static ConcurrentDictionary<string, DiffDetailResult> FileRelativePathToDiffDetailDictionary { get; } = new ConcurrentDictionary<string, DiffDetailResult>(StringComparer.Ordinal);

        /// <summary>
        /// IL 比較を実施したファイルごとの逆アセンブラ表示ラベル（例: "dotnet-ildasm (version: 0.12.0)"）。
        /// </summary>
        public static ConcurrentDictionary<string, string> FileRelativePathToIlDisassemblerLabelDictionary { get; } = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// 1 件以上のファイルが <see cref="DiffDetailResult.MD5Mismatch"/> と判定されているかどうか。
        /// </summary>
        public static bool HasAnyMd5Mismatch => FileRelativePathToDiffDetailDictionary.Values.Any(result => result == DiffDetailResult.MD5Mismatch);

        /// <summary>
        /// IgnoredExtensions 対象ファイルの相対パスと所在（旧/新フォルダ）情報。
        /// </summary>
        public static ConcurrentDictionary<string, IgnoredFileLocation> IgnoredFilesRelativePathToLocation { get; } = new ConcurrentDictionary<string, IgnoredFileLocation>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 実行中に使用された逆アセンブラの名称とバージョン（実ツール実行）。
        /// </summary>
        public static ConcurrentDictionary<string, byte> DisassemblerToolVersions { get; } = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// キャッシュ経由で利用された逆アセンブラの名称とバージョン。
        /// </summary>
        public static ConcurrentDictionary<string, byte> DisassemblerToolVersionsFromCache { get; } = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        #endregion

        /// <summary>
        /// 旧バージョン側（比較元）ファイルの絶対パス一覧を置き換えます。
        /// </summary>
        /// <param name="oldFilesAbsolutePath">旧バージョン側（比較元）ファイルの絶対パス一覧。</param>
        public static void SetOldFilesAbsolutePath(IEnumerable<string> oldFilesAbsolutePath)
        {
            ReplaceQueueItems(OldFilesAbsolutePath, oldFilesAbsolutePath, nameof(oldFilesAbsolutePath));
        }

        /// <summary>
        /// 新バージョン側（比較先）ファイルの絶対パス一覧を置き換えます。
        /// </summary>
        /// <param name="newFilesAbsolutePath">新バージョン側（比較先）ファイルの絶対パス一覧。</param>
        public static void SetNewFilesAbsolutePath(IEnumerable<string> newFilesAbsolutePath)
        {
            ReplaceQueueItems(NewFilesAbsolutePath, newFilesAbsolutePath, nameof(newFilesAbsolutePath));
        }

        /// <summary>
        /// 差異のないファイルの相対パスを記録します。
        /// </summary>
        /// <param name="fileRelativePath">ファイルの相対パス。</param>
        public static void AddUnchangedFileRelativePath(string fileRelativePath)
        {
            EnqueuePath(UnchangedFilesRelativePath, fileRelativePath, nameof(fileRelativePath));
        }

        /// <summary>
        /// 追加されたファイルの絶対パスを記録します。
        /// </summary>
        /// <param name="newFileAbsolutePath">追加されたファイルの絶対パス。</param>
        public static void AddAddedFileAbsolutePath(string newFileAbsolutePath)
        {
            EnqueuePath(AddedFilesAbsolutePath, newFileAbsolutePath, nameof(newFileAbsolutePath));
        }

        /// <summary>
        /// 削除されたファイルの絶対パスを記録します。
        /// </summary>
        /// <param name="oldFileAbsolutePath">削除されたファイルの絶対パス。</param>
        public static void AddRemovedFileAbsolutePath(string oldFileAbsolutePath)
        {
            EnqueuePath(RemovedFilesAbsolutePath, oldFileAbsolutePath, nameof(oldFileAbsolutePath));
        }

        /// <summary>
        /// 変更されたファイルの相対パスを記録します。
        /// </summary>
        /// <param name="fileRelativePath">変更されたファイルの相対パス。</param>
        public static void AddModifiedFileRelativePath(string fileRelativePath)
        {
            EnqueuePath(ModifiedFilesRelativePath, fileRelativePath, nameof(fileRelativePath));
        }

        /// <summary>
        /// 比較結果の静的状態をすべて初期化します。
        /// </summary>
        public static void ResetAll()
        {
            OldFilesAbsolutePath.Clear();
            NewFilesAbsolutePath.Clear();
            UnchangedFilesRelativePath.Clear();
            AddedFilesAbsolutePath.Clear();
            RemovedFilesAbsolutePath.Clear();
            ModifiedFilesRelativePath.Clear();
            FileRelativePathToDiffDetailDictionary.Clear();
            FileRelativePathToIlDisassemblerLabelDictionary.Clear();
            IgnoredFilesRelativePathToLocation.Clear();
            DisassemblerToolVersions.Clear();
            DisassemblerToolVersionsFromCache.Clear();
        }

        /// <summary>
        /// ファイルの比較結果を記録します。
        /// </summary>
        /// <param name="fileRelativePath">ファイルの相対パス</param>
        /// <param name="diffDetailResult">ファイルごとの一致/不一致の判定根拠</param>
        /// <param name="ilDisassemblerLabel">IL 比較時に使用した逆アセンブラ表示ラベル（省略可）。</param>
        /// <exception cref="ArgumentNullException">fileRelativePath または diffDetailResult が null の場合にスローされます。</exception>
        /// <exception cref="ArgumentException">fileRelativePath が既に辞書に存在する場合にスローされます。</exception>
        public static void RecordDiffDetail(string fileRelativePath, DiffDetailResult diffDetailResult, string ilDisassemblerLabel = null)
        {
            // 既に存在する場合は上書き、存在しなければ追加 (スレッドセーフ)
            FileRelativePathToDiffDetailDictionary[fileRelativePath] = diffDetailResult;
            if (diffDetailResult == DiffDetailResult.ILMatch || diffDetailResult == DiffDetailResult.ILMismatch)
            {
                if (!string.IsNullOrWhiteSpace(ilDisassemblerLabel))
                {
                    FileRelativePathToIlDisassemblerLabelDictionary[fileRelativePath] = ilDisassemblerLabel;
                }
                else
                {
                    FileRelativePathToIlDisassemblerLabelDictionary.TryRemove(fileRelativePath, out _);
                }
            }
            else
            {
                FileRelativePathToIlDisassemblerLabelDictionary.TryRemove(fileRelativePath, out _);
            }
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
                throw new ArgumentException(string.Format(ERROR_FILE_RELATIVE_PATH_EMPTY, nameof(fileRelativePath)));
            }
            IgnoredFilesRelativePathToLocation.AddOrUpdate(fileRelativePath, location, (_, existing) => existing | location);
        }

        /// <summary>
        /// 使用した逆アセンブラ名とバージョンを記録します。
        /// </summary>
        /// <param name="toolName">ツール名。</param>
        /// <param name="version">バージョン文字列（省略可）。</param>
        public static void RecordDisassemblerToolVersion(string toolName, string version, bool fromCache = false)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return;
            }
            var label = string.IsNullOrWhiteSpace(version) ? toolName : $"{toolName} (version: {version})";
            var target = fromCache ? DisassemblerToolVersionsFromCache : DisassemblerToolVersions;
            target[label] = 0;
        }

        /// <summary>
        /// スレッドセーフキュー内の要素を指定した内容で置き換えます。
        /// </summary>
        /// <param name="targetQueue">置き換え先キュー。</param>
        /// <param name="items">置き換える要素。</param>
        /// <param name="paramName">null チェック用の引数名。</param>
        /// <exception cref="ArgumentNullException">items が null の場合。</exception>
        private static void ReplaceQueueItems(ConcurrentQueue<string> targetQueue, IEnumerable<string> items, string paramName)
        {
            if (items is null)
            {
                throw new ArgumentNullException(paramName);
            }

            targetQueue.Clear();
            foreach (var item in items)
            {
                EnqueuePath(targetQueue, item, paramName);
            }
        }

        /// <summary>
        /// null でないことを確認してキューに追加します。
        /// </summary>
        /// <param name="targetQueue">追加先キュー。</param>
        /// <param name="path">追加するパス。</param>
        /// <param name="paramName">null チェック用の引数名。</param>
        /// <exception cref="ArgumentNullException">path が null の場合。</exception>
        private static void EnqueuePath(ConcurrentQueue<string> targetQueue, string path, string paramName)
        {
            if (path is null)
            {
                throw new ArgumentNullException(paramName);
            }
            targetQueue.Enqueue(path);
        }
    }
}
