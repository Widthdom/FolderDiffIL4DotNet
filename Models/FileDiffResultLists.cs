using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Holds lists of file comparison results.
    /// ファイル比較結果のリストを保持するクラス。
    /// </summary>
    public sealed class FileDiffResultLists
    {
        /// <summary>
        /// Enum representing the basis for match/mismatch determination per file.
        /// ファイルごとの一致/不一致の判定根拠を表す列挙型。
        /// </summary>
        public enum DiffDetailResult
        {
            /// <summary>Files match by MD5 hash. / MD5 ハッシュが一致。</summary>
            MD5Match,
            /// <summary>Files differ by MD5 hash. / MD5 ハッシュが不一致。</summary>
            MD5Mismatch,
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
        public ConcurrentQueue<string> OldFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        public ConcurrentQueue<string> NewFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        public ConcurrentQueue<string> UnchangedFilesRelativePath { get; } = new ConcurrentQueue<string>();

        public ConcurrentQueue<string> AddedFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        public ConcurrentQueue<string> RemovedFilesAbsolutePath { get; } = new ConcurrentQueue<string>();

        public ConcurrentQueue<string> ModifiedFilesRelativePath { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// Thread-safe dictionary mapping file relative paths to their comparison results.
        /// ファイル間の比較結果を保持する辞書（並列比較で安全に書き込みできるよう ConcurrentDictionary）。
        /// </summary>
        public ConcurrentDictionary<string, DiffDetailResult> FileRelativePathToDiffDetailDictionary { get; } = new ConcurrentDictionary<string, DiffDetailResult>(StringComparer.Ordinal);

        /// <summary>
        /// Disassembler display label per IL-compared file (e.g., "dotnet-ildasm (version: 0.12.0)").
        /// IL 比較を実施したファイルごとの逆アセンブラ表示ラベル（例: "dotnet-ildasm (version: 0.12.0)"）。
        /// </summary>
        public ConcurrentDictionary<string, string> FileRelativePathToIlDisassemblerLabelDictionary { get; } = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        public bool HasAnyMd5Mismatch => FileRelativePathToDiffDetailDictionary.Values.Any(result => result == DiffDetailResult.MD5Mismatch);

        /// <summary>
        /// Relative paths and folder locations (old/new) of files excluded by IgnoredExtensions.
        /// IgnoredExtensions 対象ファイルの相対パスと所在（旧/新フォルダ）情報。
        /// </summary>
        public ConcurrentDictionary<string, IgnoredFileLocation> IgnoredFilesRelativePathToLocation { get; } = new ConcurrentDictionary<string, IgnoredFileLocation>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Disassembler names and versions used during actual tool execution.
        /// 実行中に使用された逆アセンブラの名称とバージョン（実ツール実行）。
        /// </summary>
        public ConcurrentDictionary<string, byte> DisassemblerToolVersions { get; } = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Disassembler names and versions retrieved via cache.
        /// キャッシュ経由で利用された逆アセンブラの名称とバージョン。
        /// </summary>
        public ConcurrentDictionary<string, byte> DisassemblerToolVersionsFromCache { get; } = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Warnings for Modified files where the new file's timestamp is older than the old file's.
        /// Modified と判定されたファイルのうち、new 側の更新日時が old 側より古いものの警告一覧。
        /// </summary>
        public ConcurrentDictionary<string, FileTimestampRegressionWarning> NewFileTimestampOlderThanOldWarnings { get; } = new ConcurrentDictionary<string, FileTimestampRegressionWarning>(StringComparer.OrdinalIgnoreCase);

        public bool HasAnyNewFileTimestampOlderThanOldWarning => !NewFileTimestampOlderThanOldWarnings.IsEmpty;

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

        public void SetOldFilesAbsolutePath(IEnumerable<string> oldFilesAbsolutePath)
        {
            ReplaceQueueItems(OldFilesAbsolutePath, oldFilesAbsolutePath, nameof(oldFilesAbsolutePath));
        }

        public void SetNewFilesAbsolutePath(IEnumerable<string> newFilesAbsolutePath)
        {
            ReplaceQueueItems(NewFilesAbsolutePath, newFilesAbsolutePath, nameof(newFilesAbsolutePath));
        }

        public void AddUnchangedFileRelativePath(string fileRelativePath)
        {
            EnqueuePath(UnchangedFilesRelativePath, fileRelativePath, nameof(fileRelativePath));
        }

        public void AddAddedFileAbsolutePath(string newFileAbsolutePath)
        {
            EnqueuePath(AddedFilesAbsolutePath, newFileAbsolutePath, nameof(newFileAbsolutePath));
        }

        public void AddRemovedFileAbsolutePath(string oldFileAbsolutePath)
        {
            EnqueuePath(RemovedFilesAbsolutePath, oldFileAbsolutePath, nameof(oldFileAbsolutePath));
        }

        public void AddModifiedFileRelativePath(string fileRelativePath)
        {
            EnqueuePath(ModifiedFilesRelativePath, fileRelativePath, nameof(fileRelativePath));
        }

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
        }

        /// <summary>
        /// Records the comparison result for a file, optionally associating a disassembler label for IL comparisons.
        /// ファイルの比較結果を記録します。IL 比較時は逆アセンブラ表示ラベルも関連付けます。
        /// </summary>
        public void RecordDiffDetail(string fileRelativePath, DiffDetailResult diffDetailResult, string? ilDisassemblerLabel = null)
        {
            // Upsert: overwrite if exists, add if not (thread-safe)
            // 既に存在する場合は上書き、存在しなければ追加（スレッドセーフ）
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
        /// Records the location info for a file matched by IgnoredExtensions.
        /// IgnoredExtensions に該当したファイルの所在情報を記録します。
        /// </summary>
        public void RecordIgnoredFile(string fileRelativePath, IgnoredFileLocation location)
        {
            if (string.IsNullOrWhiteSpace(fileRelativePath))
            {
                throw new ArgumentException($"{nameof(fileRelativePath)} cannot be null or whitespace.");
            }
            IgnoredFilesRelativePathToLocation.AddOrUpdate(fileRelativePath, location, (_, existing) => existing | location);
        }

        /// <summary>
        /// Records the disassembler tool name and version used.
        /// 使用した逆アセンブラ名とバージョンを記録します。
        /// </summary>
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
        /// Records a warning when a Modified file's new-side timestamp is older than its old-side timestamp.
        /// Modified と判定されたファイルについて、new 側の更新日時が old 側より古い場合の警告を記録します。
        /// </summary>
        public void RecordNewFileTimestampOlderThanOldWarning(string fileRelativePath, string oldTimestamp, string newTimestamp)
        {
            if (string.IsNullOrWhiteSpace(fileRelativePath))
            {
                throw new ArgumentException($"{nameof(fileRelativePath)} cannot be null or whitespace.");
            }

            NewFileTimestampOlderThanOldWarnings[fileRelativePath] = new FileTimestampRegressionWarning(fileRelativePath, oldTimestamp, newTimestamp);
        }

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
