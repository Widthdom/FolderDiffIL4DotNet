using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Text comparison strategies for <see cref="FileDiffService"/>:
    /// sequential, chunk-parallel, and memory-budget-aware parallelism.
    /// <see cref="FileDiffService"/> のテキスト比較戦略:
    /// 逐次比較、チャンク並列比較、メモリ予算考慮の並列度制御。
    /// </summary>
    public sealed partial class FileDiffService
    {
        private const int DEFAULT_TEXT_DIFF_PARALLEL_THRESHOLD_BYTES = 512 * Constants.BYTES_PER_KILOBYTE;
        private const int DEFAULT_TEXT_DIFF_CHUNK_SIZE_BYTES = 64 * Constants.BYTES_PER_KILOBYTE;
        private const int BYTES_PER_MEGABYTE = Constants.BYTES_PER_KILOBYTE * Constants.BYTES_PER_KILOBYTE;

        /// <summary>
        /// Compares two text files using sequential or chunk-parallel strategies depending on file size and network mode.
        /// ファイルサイズやネットワークモードに応じて逐次比較またはチャンク並列比較でテキストファイルを比較します。
        /// </summary>
        private async Task<bool> CompareAsTextAsync(string fileRelativePath, string file1AbsolutePath, string file2AbsolutePath, int maxParallel)
        {
            int textDiffParallelThresholdBytes = GetEffectiveBytesFromConfiguredKilobytes(
                configuredKilobytes: _config.TextDiffParallelThresholdKilobytes,
                defaultBytes: DEFAULT_TEXT_DIFF_PARALLEL_THRESHOLD_BYTES);
            int textDiffChunkSizeBytes = GetEffectiveBytesFromConfiguredKilobytes(
                configuredKilobytes: _config.TextDiffChunkSizeKilobytes,
                defaultBytes: DEFAULT_TEXT_DIFF_CHUNK_SIZE_BYTES);
            try
            {
                if (_optimizeForNetworkShares)
                {
                    // Under network-share optimisation, avoid parallel comparison (which opens/closes per chunk) and compare sequentially.
                    // ネットワーク共有最適化時は、チャンク毎のOpen/Closeを伴う並列比較は避け、逐次読みで比較
                    return await _fileComparisonService.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                }

                long file1Length = _fileComparisonService.GetFileLength(file1AbsolutePath);
                if (file1Length >= textDiffParallelThresholdBytes)
                {
                    int effectiveMaxParallel = DetermineEffectiveTextDiffParallelism(fileRelativePath, maxParallel, textDiffChunkSizeBytes);
                    if (effectiveMaxParallel == 1)
                    {
                        // Fall back to sequential comparison to avoid extra buffer allocation when the memory budget is small.
                        // メモリ予算が小さい場合は追加バッファ確保を抑えるため逐次比較へ切り替える。
                        return await _fileComparisonService.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                    }

                    // Speed up large files with parallel chunk comparison.
                    // 大きいファイルは並列チャンク比較で高速化
                    return await DiffTextFilesParallelAsync(
                        file1AbsolutePath,
                        file2AbsolutePath,
                        largeFileSizeThresholdBytes: textDiffParallelThresholdBytes,
                        chunkSizeBytes: textDiffChunkSizeBytes,
                        maxParallel: effectiveMaxParallel);
                }

                // Sequential line comparison for small files to avoid parallelisation overhead.
                // 小さいファイルは逐次行比較（並列化のオーバーヘッドを避ける）
                return await _fileComparisonService.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Parallel text diff failed for '{fileRelativePath}' ({ex.GetType().Name}). Falling back to sequential text diff.", shouldOutputMessageToConsole: true, ex);
                return await _fileComparisonService.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
            }
        }

        /// <summary>
        /// Experimental parallel chunk comparison for text files exceeding the size threshold, aimed at speed-up.
        /// Only performs exact-match detection; does not identify specific differences.
        /// Errors and invalid arguments are propagated to the caller, which decides whether to fall back to sequential comparison.
        /// サイズが閾値を超えるテキストファイルに対して高速化を目的に並列チャンク比較を行う実験的メソッド。
        /// 完全一致判定のみを行い、差分箇所の特定は行いません。
        /// エラーや引数不正は呼び出し側へ送出し、呼び出し側で逐次比較へのフォールバック可否を判断します。
        /// </summary>
        private async Task<bool> DiffTextFilesParallelAsync(string file1AbsolutePath, string file2AbsolutePath, long largeFileSizeThresholdBytes, int chunkSizeBytes, int maxParallel)
        {
            // If either file is missing or their sizes differ, they are unequal without further comparison.
            // どちらかが存在しない、またはサイズが異なる場合は比較するまでもなく不一致。
            if (!_fileComparisonService.FileExists(file1AbsolutePath) || !_fileComparisonService.FileExists(file2AbsolutePath))
            {
                return false;
            }
            long file1Length = _fileComparisonService.GetFileLength(file1AbsolutePath);
            long file2Length = _fileComparisonService.GetFileLength(file2AbsolutePath);
            if (file1Length != file2Length)
            {
                return false;
            }
            // Delegate small files to existing sequential comparison to avoid unnecessary overhead.
            // 小さいファイルは既存の逐次比較に委譲して余計なオーバーヘッドを避ける。
            if (file1Length < largeFileSizeThresholdBytes)
            {
                return await _fileComparisonService.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
            }
            if (maxParallel <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxParallel), maxParallel, Constants.ERROR_MAX_PARALLEL);
            }

            // Split large files into fixed-size chunks and run read-then-compare in parallel.
            // 大きなファイルは固定サイズのチャンクに分割し、読み取り→比較を並列実行する。
            int chunkCount = (int)((file1Length + chunkSizeBytes - 1) / chunkSizeBytes);
            var differences = 0;
            await Parallel.ForEachAsync(Enumerable.Range(0, chunkCount), new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, async (index, cancellationToken) =>
            {
                // No need to read further chunks once a difference has been found.
                // 既に差分が見つかっていれば以降のチャンクは読む必要がない。
                if (Volatile.Read(ref differences) != 0)
                {
                    return;
                }
                var buffer1 = new byte[chunkSizeBytes];
                var buffer2 = new byte[chunkSizeBytes];
                int read1 = await _fileComparisonService.ReadChunkAsync(file1AbsolutePath, (long)index * chunkSizeBytes, buffer1.AsMemory(0, chunkSizeBytes), cancellationToken);
                int read2 = await _fileComparisonService.ReadChunkAsync(file2AbsolutePath, (long)index * chunkSizeBytes, buffer2.AsMemory(0, chunkSizeBytes), cancellationToken);
                // Chunks at the same offset with different read lengths are immediately unequal.
                // 同じオフセットのチャンクでも読み取りバイト数が異なれば即時不一致。
                if (read1 != read2)
                {
                    Interlocked.Exchange(ref differences, 1);
                    return;
                }
                // Any single-byte difference within a chunk marks inequality and aborts remaining chunks.
                // チャンク内で1バイトでも異なれば不一致とし、他チャンクも打ち切る。
                for (int i = 0; i < read1; i++)
                {
                    if (buffer1[i] != buffer2[i])
                    {
                        Interlocked.Exchange(ref differences, 1);
                        break;
                    }
                }
            });
            // Exact match if the difference flag was never set.
            // 差分フラグが立っていなければ完全一致。
            return differences == 0;
        }

        /// <summary>
        /// Converts a KiB configuration value to bytes; returns the default when the value is non-positive or would overflow.
        /// KiB 指定の設定値をバイトへ変換します。設定値が 0 以下または変換でオーバーフローする場合は既定値を返します。
        /// </summary>
        private static int GetEffectiveBytesFromConfiguredKilobytes(int configuredKilobytes, int defaultBytes)
        {
            if (configuredKilobytes <= 0)
            {
                return defaultBytes;
            }

            long bytes = (long)configuredKilobytes * Constants.BYTES_PER_KILOBYTE;
            if (bytes > int.MaxValue)
            {
                return defaultBytes;
            }

            return (int)bytes;
        }

        /// <summary>
        /// Determines the effective parallelism for chunk-parallel comparison, accounting for the text-diff memory budget.
        /// テキスト差分の追加メモリ予算を考慮して、チャンク並列比較に使う実効並列度を決定します。
        /// </summary>
        private int DetermineEffectiveTextDiffParallelism(string fileRelativePath, int requestedMaxParallel, int chunkSizeBytes)
        {
            if (requestedMaxParallel <= 1)
            {
                return requestedMaxParallel;
            }

            long memoryLimitBytes = GetConfiguredTextDiffParallelMemoryLimitBytes();
            if (memoryLimitBytes <= 0)
            {
                return requestedMaxParallel;
            }

            long bytesPerWorker = (long)chunkSizeBytes * 2;
            if (bytesPerWorker <= 0)
            {
                return requestedMaxParallel;
            }

            long maxWorkersByBudget = memoryLimitBytes / bytesPerWorker;
            if (maxWorkersByBudget <= 1)
            {
                LogTextDiffMemoryLimitApplied(fileRelativePath, requestedMaxParallel, effectiveMaxParallel: 1, chunkSizeBytes, memoryLimitBytes, fallbackToSequential: true);
                return 1;
            }

            int effectiveMaxParallel = (int)Math.Min(requestedMaxParallel, Math.Min(maxWorkersByBudget, int.MaxValue));
            if (effectiveMaxParallel < requestedMaxParallel)
            {
                LogTextDiffMemoryLimitApplied(fileRelativePath, requestedMaxParallel, effectiveMaxParallel, chunkSizeBytes, memoryLimitBytes, fallbackToSequential: false);
            }

            return effectiveMaxParallel;
        }

        /// <summary>
        /// Returns the configured text-diff additional memory budget in bytes (non-positive means unlimited).
        /// 設定されたテキスト差分の追加メモリ予算をバイト単位で返します（0 以下は制限なし）。
        /// </summary>
        private long GetConfiguredTextDiffParallelMemoryLimitBytes()
        {
            if (_config.TextDiffParallelMemoryLimitMegabytes <= 0)
            {
                return 0;
            }

            return (long)_config.TextDiffParallelMemoryLimitMegabytes * BYTES_PER_MEGABYTE;
        }

        /// <summary>
        /// Logs that the effective parallelism was adjusted due to the text-diff memory budget.
        /// テキスト差分のメモリ予算により実効並列度を調整したことをログ出力します。
        /// </summary>
        private void LogTextDiffMemoryLimitApplied(string fileRelativePath, int requestedMaxParallel, int effectiveMaxParallel, int chunkSizeBytes, long memoryLimitBytes, bool fallbackToSequential)
        {
            long managedHeapBytes = GC.GetTotalMemory(forceFullCollection: false);
            string action = fallbackToSequential
                ? "Falling back to sequential text diff."
                : $"Reducing chunk-parallel text diff to maxParallel={effectiveMaxParallel}.";
            _logger.LogMessage(
                AppLogLevel.Info,
                $"Text diff memory budget applied for '{fileRelativePath}'. requestedMaxParallel={requestedMaxParallel}, chunkSizeBytes={chunkSizeBytes}, additionalBufferBudgetBytes={memoryLimitBytes}, managedHeapBytes={managedHeapBytes}. {action}",
                shouldOutputMessageToConsole: true);
        }
    }
}
