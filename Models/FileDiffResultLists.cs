using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// ファイル比較結果のリストを保持するインスタンスクラス。
    /// </summary>
    public sealed class FileDiffResultLists
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
        /// <summary>
        /// 旧バージョン側（比較元）ファイルの絶対パスのリスト
        /// </summary>
        public ConcurrentQueue<string> OldFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// 新バージョン側（比較先）ファイルの絶対パスのリスト
        /// </summary>
        public ConcurrentQueue<string> NewFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// 差異のないファイルの相対パスのリスト
        /// </summary>
        public ConcurrentQueue<string> UnchangedFilesRelativePath { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// 追加されたファイルの絶対パスのリスト
        /// </summary>
        public ConcurrentQueue<string> AddedFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// 削除されたファイルの絶対パスのリスト
        /// </summary>
        public ConcurrentQueue<string> RemovedFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// 変更されたファイルの相対パスのリスト
        /// </summary>
        public ConcurrentQueue<string> ModifiedFilesRelativePath { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// ファイル間の比較結果を保持する辞書 (並列比較で安全に書き込みできるよう ConcurrentDictionary)。
        /// </summary>
        public ConcurrentDictionary<string, DiffDetailResult> FileRelativePathToDiffDetailDictionary { get; } = new ConcurrentDictionary<string, DiffDetailResult>(StringComparer.Ordinal);

        /// <summary>
        /// IL 比較を実施したファイルごとの逆アセンブラ表示ラベル（例: "dotnet-ildasm (version: 0.12.0)"）。
        /// </summary>
        public ConcurrentDictionary<string, string> FileRelativePathToIlDisassemblerLabelDictionary { get; } = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// 1 件以上のファイルが <see cref="DiffDetailResult.MD5Mismatch"/> と判定されているかどうか。
        /// </summary>
        public bool HasAnyMd5Mismatch => FileRelativePathToDiffDetailDictionary.Values.Any(result => result == DiffDetailResult.MD5Mismatch);

        /// <summary>
        /// IgnoredExtensions 対象ファイルの相対パスと所在（旧/新フォルダ）情報。
        /// </summary>
        public ConcurrentDictionary<string, IgnoredFileLocation> IgnoredFilesRelativePathToLocation { get; } = new ConcurrentDictionary<string, IgnoredFileLocation>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 実行中に使用された逆アセンブラの名称とバージョン（実ツール実行）。
        /// </summary>
        public ConcurrentDictionary<string, byte> DisassemblerToolVersions { get; } = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// キャッシュ経由で利用された逆アセンブラの名称とバージョン。
        /// </summary>
        public ConcurrentDictionary<string, byte> DisassemblerToolVersionsFromCache { get; } = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// new 側の更新日時が old 側より古いファイルの警告一覧。
        /// </summary>
        public ConcurrentDictionary<string, FileTimestampRegressionWarning> NewFileTimestampOlderThanOldWarnings { get; } = new ConcurrentDictionary<string, FileTimestampRegressionWarning>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 1 件以上のファイルで new 側の更新日時が old 側より古いかどうか。
        /// </summary>
        public bool HasAnyNewFileTimestampOlderThanOldWarning => !NewFileTimestampOlderThanOldWarnings.IsEmpty;

        /// <summary>
        /// 旧バージョン側（比較元）ファイルの絶対パス一覧を置き換えます。
        /// </summary>
        /// <param name="oldFilesAbsolutePath">旧バージョン側（比較元）ファイルの絶対パス一覧。</param>
        public void SetOldFilesAbsolutePath(IEnumerable<string> oldFilesAbsolutePath)
        {
            ReplaceQueueItems(OldFilesAbsolutePath, oldFilesAbsolutePath, nameof(oldFilesAbsolutePath));
        }

        /// <summary>
        /// 新バージョン側（比較先）ファイルの絶対パス一覧を置き換えます。
        /// </summary>
        /// <param name="newFilesAbsolutePath">新バージョン側（比較先）ファイルの絶対パス一覧。</param>
        public void SetNewFilesAbsolutePath(IEnumerable<string> newFilesAbsolutePath)
        {
            ReplaceQueueItems(NewFilesAbsolutePath, newFilesAbsolutePath, nameof(newFilesAbsolutePath));
        }

        /// <summary>
        /// 差異のないファイルの相対パスを記録します。
        /// </summary>
        /// <param name="fileRelativePath">ファイルの相対パス。</param>
        public void AddUnchangedFileRelativePath(string fileRelativePath)
        {
            EnqueuePath(UnchangedFilesRelativePath, fileRelativePath, nameof(fileRelativePath));
        }

        /// <summary>
        /// 追加されたファイルの絶対パスを記録します。
        /// </summary>
        /// <param name="newFileAbsolutePath">追加されたファイルの絶対パス。</param>
        public void AddAddedFileAbsolutePath(string newFileAbsolutePath)
        {
            EnqueuePath(AddedFilesAbsolutePath, newFileAbsolutePath, nameof(newFileAbsolutePath));
        }

        /// <summary>
        /// 削除されたファイルの絶対パスを記録します。
        /// </summary>
        /// <param name="oldFileAbsolutePath">削除されたファイルの絶対パス。</param>
        public void AddRemovedFileAbsolutePath(string oldFileAbsolutePath)
        {
            EnqueuePath(RemovedFilesAbsolutePath, oldFileAbsolutePath, nameof(oldFileAbsolutePath));
        }

        /// <summary>
        /// 変更されたファイルの相対パスを記録します。
        /// </summary>
        /// <param name="fileRelativePath">変更されたファイルの相対パス。</param>
        public void AddModifiedFileRelativePath(string fileRelativePath)
        {
            EnqueuePath(ModifiedFilesRelativePath, fileRelativePath, nameof(fileRelativePath));
        }

        /// <summary>
        /// 比較結果の状態をすべて初期化します。
        /// </summary>
        public void ResetAll()
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
            NewFileTimestampOlderThanOldWarnings.Clear();
        }

        /// <summary>
        /// ファイルの比較結果を記録します。
        /// </summary>
        /// <param name="fileRelativePath">ファイルの相対パス</param>
        /// <param name="diffDetailResult">ファイルごとの一致/不一致の判定根拠</param>
        /// <param name="ilDisassemblerLabel">IL 比較時に使用した逆アセンブラ表示ラベル（省略可）。</param>
        /// <exception cref="ArgumentNullException">fileRelativePath または diffDetailResult が null の場合にスローされます。</exception>
        /// <exception cref="ArgumentException">fileRelativePath が既に辞書に存在する場合にスローされます。</exception>
        public void RecordDiffDetail(string fileRelativePath, DiffDetailResult diffDetailResult, string ilDisassemblerLabel = null)
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
        public void RecordIgnoredFile(string fileRelativePath, IgnoredFileLocation location)
        {
            if (string.IsNullOrWhiteSpace(fileRelativePath))
            {
                throw new ArgumentException($"{nameof(fileRelativePath)} cannot be null or whitespace.");
            }
            IgnoredFilesRelativePathToLocation.AddOrUpdate(fileRelativePath, location, (_, existing) => existing | location);
        }

        /// <summary>
        /// 使用した逆アセンブラ名とバージョンを記録します。
        /// </summary>
        /// <param name="toolName">ツール名。</param>
        /// <param name="version">バージョン文字列（省略可）。</param>
        public void RecordDisassemblerToolVersion(string toolName, string version, bool fromCache = false)
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
        /// new 側の更新日時が old 側より古いファイルの警告を記録します。
        /// </summary>
        /// <param name="fileRelativePath">ファイルの相対パス。</param>
        /// <param name="oldTimestamp">old 側の更新日時。</param>
        /// <param name="newTimestamp">new 側の更新日時。</param>
        public void RecordNewFileTimestampOlderThanOldWarning(string fileRelativePath, string oldTimestamp, string newTimestamp)
        {
            if (string.IsNullOrWhiteSpace(fileRelativePath))
            {
                throw new ArgumentException($"{nameof(fileRelativePath)} cannot be null or whitespace.");
            }

            NewFileTimestampOlderThanOldWarnings[fileRelativePath] = new FileTimestampRegressionWarning(fileRelativePath, oldTimestamp, newTimestamp);
        }

        /// <summary>
        /// スレッドセーフキュー内の要素を指定した内容で置き換えます。
        /// </summary>
        /// <param name="targetQueue">置き換え先キュー。</param>
        /// <param name="items">置き換える要素。</param>
        /// <param name="paramName">null チェック用の引数名。</param>
        /// <exception cref="ArgumentNullException">items が null の場合。</exception>
        private void ReplaceQueueItems(ConcurrentQueue<string> targetQueue, IEnumerable<string> items, string paramName)
        {
            ArgumentNullException.ThrowIfNull(items, paramName);

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
        private void EnqueuePath(ConcurrentQueue<string> targetQueue, string path, string paramName)
        {
            ArgumentNullException.ThrowIfNull(path, paramName);
            targetQueue.Enqueue(path);
        }
    }
}
