using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Holds lists of file comparison results.
    /// This partial file contains nested types, file classification queues, and classification methods.
    /// ファイル比較結果のリストを保持するクラス。
    /// この partial ファイルにはネスト型、ファイル分類キュー、分類メソッドを含みます。
    /// </summary>
    public sealed partial class FileDiffResultLists
    {
        /// <summary>
        /// Enum representing the basis for match/mismatch determination per file.
        /// ファイルごとの一致/不一致の判定根拠を表す列挙型。
        /// </summary>
        public enum DiffDetailResult
        {
            /// <summary>Files match by SHA256 hash. / SHA256 ハッシュが一致。</summary>
            SHA256Match,
            /// <summary>Files differ by SHA256 hash. / SHA256 ハッシュが不一致。</summary>
            SHA256Mismatch,
            /// <summary>Files match at the IL level (build-specific differences ignored). / IL（中間言語）ベースで一致（ビルド固有情報の差異は無視）。</summary>
            ILMatch,
            /// <summary>Files differ at the IL level (build-specific differences ignored). / IL（中間言語）ベースで不一致（ビルド固有情報の差異は無視）。</summary>
            ILMismatch,
            /// <summary>Files match as text. / テキストベースで一致。</summary>
            TextMatch,
            /// <summary>Files differ as text. / テキストベースで不一致。</summary>
            TextMismatch
        }

        /// <summary>
        /// Flags indicating which folder(s) contained a file excluded by IgnoredExtensions.
        /// IgnoredExtensions により比較対象から除外されたファイルがどのフォルダに存在したかを示すフラグ。
        /// </summary>
        [Flags]
        public enum IgnoredFileLocation
        {
            /// <summary>Default value indicating no folder (0). / フォルダを特定しない初期値（0）。</summary>
            None = 0,
            /// <summary>File exists in the old (source) folder. / 旧バージョン側（比較元）フォルダに存在。</summary>
            Old = 1,
            /// <summary>File exists in the new (target) folder. / 新バージョン側（比較先）フォルダに存在。</summary>
            New = 2
        }

        /// <summary>
        /// Record holding aggregated file counts for the summary section.
        /// サマリーセクション向けのファイル件数の集計値を保持するレコード。
        /// </summary>
        public sealed record DiffSummaryStatistics(
            int AddedCount,
            int RemovedCount,
            int ModifiedCount,
            int UnchangedCount,
            int IgnoredCount);

        // ──────────────────────────────────────────────
        // File classification queues
        // ファイル分類キュー
        // ──────────────────────────────────────────────

        /// <summary>Absolute paths of all files found in the old (baseline) folder. / 旧（ベースライン）フォルダ内の全ファイル絶対パス。</summary>
        public ConcurrentQueue<string> OldFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        /// <summary>Absolute paths of all files found in the new (comparison) folder. / 新（比較対象）フォルダ内の全ファイル絶対パス。</summary>
        public ConcurrentQueue<string> NewFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        /// <summary>Relative paths of files that are identical between old and new. / 旧新間で同一のファイルの相対パス。</summary>
        public ConcurrentQueue<string> UnchangedFilesRelativePath { get; } = new ConcurrentQueue<string>();

        /// <summary>Absolute paths of files present only in the new folder. / 新フォルダにのみ存在するファイルの絶対パス。</summary>
        public ConcurrentQueue<string> AddedFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        /// <summary>Absolute paths of files present only in the old folder. / 旧フォルダにのみ存在するファイルの絶対パス。</summary>
        public ConcurrentQueue<string> RemovedFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        /// <summary>Relative paths of files that differ between old and new. / 旧新間で差異のあるファイルの相対パス。</summary>
        public ConcurrentQueue<string> ModifiedFilesRelativePath { get; } = new ConcurrentQueue<string>();

        // ──────────────────────────────────────────────
        // Classification methods
        // 分類メソッド
        // ──────────────────────────────────────────────

        /// <summary>Replaces the old-file list with the specified paths. / 旧ファイルリストを指定パスで置換します。</summary>
        /// <param name="oldFilesAbsolutePath">Absolute paths of old-side files. / 旧側ファイルの絶対パス群。</param>
        public void SetOldFilesAbsolutePath(IEnumerable<string> oldFilesAbsolutePath)
        {
            ReplaceQueueItems(OldFilesAbsolutePath, oldFilesAbsolutePath, nameof(oldFilesAbsolutePath));
        }

        /// <summary>Replaces the new-file list with the specified paths. / 新ファイルリストを指定パスで置換します。</summary>
        /// <param name="newFilesAbsolutePath">Absolute paths of new-side files. / 新側ファイルの絶対パス群。</param>
        public void SetNewFilesAbsolutePath(IEnumerable<string> newFilesAbsolutePath)
        {
            ReplaceQueueItems(NewFilesAbsolutePath, newFilesAbsolutePath, nameof(newFilesAbsolutePath));
        }

        /// <summary>Records a file as unchanged. / ファイルを「変更なし」として記録します。</summary>
        /// <param name="fileRelativePath">Relative path of the unchanged file. / 変更なしファイルの相対パス。</param>
        public void AddUnchangedFileRelativePath(string fileRelativePath)
        {
            EnqueuePath(UnchangedFilesRelativePath, fileRelativePath, nameof(fileRelativePath));
        }

        /// <summary>Records a file as added (present only in the new folder). / ファイルを「追加」として記録します。</summary>
        /// <param name="newFileAbsolutePath">Absolute path of the added file. / 追加ファイルの絶対パス。</param>
        public void AddAddedFileAbsolutePath(string newFileAbsolutePath)
        {
            EnqueuePath(AddedFilesAbsolutePath, newFileAbsolutePath, nameof(newFileAbsolutePath));
        }

        /// <summary>Records a file as removed (present only in the old folder). / ファイルを「削除」として記録します。</summary>
        /// <param name="oldFileAbsolutePath">Absolute path of the removed file. / 削除ファイルの絶対パス。</param>
        public void AddRemovedFileAbsolutePath(string oldFileAbsolutePath)
        {
            EnqueuePath(RemovedFilesAbsolutePath, oldFileAbsolutePath, nameof(oldFileAbsolutePath));
        }

        /// <summary>Records a file as modified (differs between old and new). / ファイルを「変更あり」として記録します。</summary>
        /// <param name="fileRelativePath">Relative path of the modified file. / 変更ありファイルの相対パス。</param>
        public void AddModifiedFileRelativePath(string fileRelativePath)
        {
            EnqueuePath(ModifiedFilesRelativePath, fileRelativePath, nameof(fileRelativePath));
        }

        // ──────────────────────────────────────────────
        // Aggregation
        // 集計
        // ──────────────────────────────────────────────

        /// <summary>
        /// Computed property returning aggregated file counts (Added/Removed/Modified/Unchanged/Ignored) for the summary section.
        /// サマリーセクション向けのファイル件数を一括で返す計算プロパティ。
        /// </summary>
        public DiffSummaryStatistics SummaryStatistics => new(
            AddedCount: AddedFilesAbsolutePath.Count,
            RemovedCount: RemovedFilesAbsolutePath.Count,
            ModifiedCount: ModifiedFilesRelativePath.Count,
            UnchangedCount: UnchangedFilesRelativePath.Count,
            IgnoredCount: IgnoredFilesRelativePathToLocation.Count);

        /// <summary>
        /// Resets all comparison result state.
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
            FileRelativePathToAssemblySemanticChanges.Clear();
            FileRelativePathToDependencyChanges.Clear();
            DisassemblerAvailability = null;
        }

        // ──────────────────────────────────────────────
        // Private helpers
        // プライベートヘルパー
        // ──────────────────────────────────────────────

        private void ReplaceQueueItems(ConcurrentQueue<string> targetQueue, IEnumerable<string> items, string paramName)
        {
            ArgumentNullException.ThrowIfNull(items, paramName);

            targetQueue.Clear();
            foreach (var item in items)
            {
                EnqueuePath(targetQueue, item, paramName);
            }
        }

        private void EnqueuePath(ConcurrentQueue<string> targetQueue, string path, string paramName)
        {
            ArgumentNullException.ThrowIfNull(path, paramName);
            targetQueue.Enqueue(path);
        }
    }
}
