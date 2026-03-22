using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Services
{
    // Sequential and parallel diff classification logic.
    // 逐次および並列の差分分類ロジック。
    public sealed partial class FolderDiffService
    {
        /// <summary>
        /// Performs sequential (single-threaded) diff classification, processing old-side files one by one
        /// into Unchanged / Modified / Removed and updating progress.
        /// 逐次（単一スレッド）で差分判定を行い、old 側を 1 件ずつ Unchanged / Modified / Removed に分類して進捗を更新します。
        /// </summary>
        private async Task<int> DetermineDiffsSequentiallyAsync(HashSet<string> remainingNewFilesAbsolutePathHashSet, int totalFilesRelativePathCount, int processedFileCountSoFar, CancellationToken cancellationToken = default)
        {
            foreach (var oldFileAbsolutePath in _fileDiffResultLists.OldFilesAbsolutePath)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileRelativePath = Path.GetRelativePath(_oldFolderAbsolutePath, oldFileAbsolutePath);
                var newFileAbsolutePath = Path.Combine(_newFolderAbsolutePath, fileRelativePath);

                if (remainingNewFilesAbsolutePathHashSet.Contains(newFileAbsolutePath))
                {
                    remainingNewFilesAbsolutePathHashSet.Remove(newFileAbsolutePath);
                    bool areEqual;
                    try
                    {
                        areEqual = await _fileDiffService.FilesAreEqualAsync(fileRelativePath, cancellationToken: cancellationToken);
                    }
                    catch (FileNotFoundException)
                    {
                        // If the new-side file was deleted after enumeration, treat as Removed and log a warning.
                        // 列挙後に new 側ファイルが削除された場合は Removed として扱い、警告を記録して継続する。
                        _logger.LogMessage(AppLogLevel.Warning, string.Format(System.Globalization.CultureInfo.InvariantCulture, LOG_FILE_DELETED_DURING_COMPARISON, fileRelativePath), shouldOutputMessageToConsole: true);
                        _fileDiffResultLists.AddRemovedFileAbsolutePath(oldFileAbsolutePath);
                        processedFileCountSoFar++;
                        _progressReporter.ReportProgress(Math.Min((double)processedFileCountSoFar * 100.0 / totalFilesRelativePathCount, 100.0));
                        continue;
                    }
                    if (areEqual)
                    {
                        _fileDiffResultLists.AddUnchangedFileRelativePath(fileRelativePath);
                    }
                    else
                    {
                        _fileDiffResultLists.AddModifiedFileRelativePath(fileRelativePath);
                        RecordNewFileTimestampOlderThanOldWarningIfNeeded(fileRelativePath, oldFileAbsolutePath, newFileAbsolutePath);
                    }
                }
                else
                {
                    _fileDiffResultLists.AddRemovedFileAbsolutePath(oldFileAbsolutePath);
                }
                processedFileCountSoFar++;
                _progressReporter.ReportProgress(Math.Min((double)processedFileCountSoFar * 100.0 / totalFilesRelativePathCount, 100.0));
            }
            return processedFileCountSoFar;
        }

        /// <summary>
        /// Performs parallel diff classification. Only access to the remaining-new-files set is guarded by a
        /// fine-grained lock; classification results are recorded via thread-safe collection APIs.
        /// 並列に差分判定を行います。new 側の未処理集合へのアクセスのみ低粒度ロックで保護し、
        /// 分類結果の追加はスレッドセーフなコレクション API で記録します。
        /// </summary>
        private async Task<int> DetermineDiffsInParallelAsync(HashSet<string> remainingNewFilesAbsolutePathHashSet, int totalFilesRelativePathCount, int processedFileCountSoFar, int maxParallel, CancellationToken cancellationToken = default)
        {
            // Lock that serialises access to remainingNewFilesAbsolutePathHashSet so that
            // Contains-then-Remove is atomic, preventing duplicate comparisons and race conditions.
            // Only the membership-check-and-remove section is locked; expensive work runs outside the lock.
            // new 側の未処理集合へのアクセスを直列化するロック。Contains→Remove をアトミックに行い、
            // 二重比較とレースコンディションを防ぐ。ロック範囲は最小限にし、重い処理はロック外で実行。
            var lockRemaining = new object();
            int processedFileCount = processedFileCountSoFar;

            await Parallel.ForEachAsync(_fileDiffResultLists.OldFilesAbsolutePath, new ParallelOptions { MaxDegreeOfParallelism = maxParallel, CancellationToken = cancellationToken }, async (oldFileAbsolutePath, ct) =>
            {
                var fileRelativePath = Path.GetRelativePath(_oldFolderAbsolutePath, oldFileAbsolutePath);
                var newFileAbsolutePath = Path.Combine(_newFolderAbsolutePath, fileRelativePath);
                bool hasMatchingFileInNewFilesAbsolutePathHashSet;
                lock (lockRemaining)
                {
                    hasMatchingFileInNewFilesAbsolutePathHashSet = remainingNewFilesAbsolutePathHashSet.Contains(newFileAbsolutePath);
                    if (hasMatchingFileInNewFilesAbsolutePathHashSet)
                    {
                        remainingNewFilesAbsolutePathHashSet.Remove(newFileAbsolutePath);
                    }
                }
                if (hasMatchingFileInNewFilesAbsolutePathHashSet)
                {
                    bool areFilesEqual;
                    try
                    {
                        areFilesEqual = await _fileDiffService.FilesAreEqualAsync(fileRelativePath, maxParallel, ct);
                    }
                    catch (FileNotFoundException)
                    {
                        // If the new-side file was deleted after enumeration, treat as Removed and log a warning.
                        // 列挙後に new 側ファイルが削除された場合は Removed として扱い、警告を記録して継続する。
                        _logger.LogMessage(AppLogLevel.Warning, string.Format(System.Globalization.CultureInfo.InvariantCulture, LOG_FILE_DELETED_DURING_COMPARISON, fileRelativePath), shouldOutputMessageToConsole: true);
                        _fileDiffResultLists.AddRemovedFileAbsolutePath(oldFileAbsolutePath);
                        var doneOnDelete = Interlocked.Increment(ref processedFileCount);
                        _progressReporter.ReportProgress(Math.Min((double)doneOnDelete * 100.0 / totalFilesRelativePathCount, 100.0));
                        return;
                    }
                    if (areFilesEqual)
                    {
                        _fileDiffResultLists.AddUnchangedFileRelativePath(fileRelativePath);
                    }
                    else
                    {
                        _fileDiffResultLists.AddModifiedFileRelativePath(fileRelativePath);
                        RecordNewFileTimestampOlderThanOldWarningIfNeeded(fileRelativePath, oldFileAbsolutePath, newFileAbsolutePath);
                    }
                }
                else
                {
                    _fileDiffResultLists.AddRemovedFileAbsolutePath(oldFileAbsolutePath);
                }
                var done = Interlocked.Increment(ref processedFileCount);
                _progressReporter.ReportProgress(Math.Min((double)done * 100.0 / totalFilesRelativePathCount, 100.0));
            });

            return processedFileCount;
        }
    }
}
