using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.IO;

namespace FolderDiffIL4DotNet.Services
{
    // IL-cache pre-computation logic (batched warm-up, keep-alive, directory preparation).
    // IL キャッシュ事前計算ロジック（バッチウォームアップ、キープアライブ、ディレクトリ準備）。
    public sealed partial class FolderDiffService
    {
        /// <summary>
        /// Runs IL-cache pre-computation for all old/new files: SHA256 hashing, internal key generation, and disassembler-result cache warm-up.
        /// 新旧すべてのファイルを対象に、IL キャッシュ用の事前計算（SHA256 計算、内部キー生成、逆アセンブラ結果キャッシュのウォームアップ）を実行します。
        /// </summary>
        private async Task PrecomputeIlCachesAsync(int maxParallel)
        {
            // In network-optimised mode, skip SHA256/IL cache warm-up and only reset progress to zero.
            // ネットワーク最適化モードでは SHA256/IL キャッシュのウォームアップをスキップし、進捗のみゼロリセット。
            if (_optimizeForNetworkShares)
            {
                _logger.LogMessage(AppLogLevel.Info, LOG_NETWORK_OPTIMIZED_SKIP_IL, shouldOutputMessageToConsole: true);
                _progressReporter.ReportProgress(0.0);
                return;
            }
            int precomputeBatchSize = GetEffectiveIlPrecomputeBatchSize();
            // Start a keep-alive that periodically reports 0% so progress does not appear stalled during pre-computation.
            // 事前計算が長引いても進捗が止まって見えないよう、定期的に 0% を流すキープアライブを起動。
            using var keepAliveCts = new CancellationTokenSource();
            var keepAliveTask = CreateKeepAliveTask(keepAliveCts);

            try
            {
                try
                {
                    // Pre-compute failure only degrades performance; the main comparison re-executes what is needed.
                    // Therefore this is best-effort: log a warning and continue.
                    // プリコンピュート失敗は性能劣化に留まり、後続の本比較で必要な処理は再実行される。
                    // そのため、ここは best-effort として warning を残しつつ継続する。
                    foreach (var batch in EnumerateDistinctPrecomputeBatches(precomputeBatchSize))
                    {
                        await _fileDiffService.PrecomputeAsync(batch, maxParallel);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
                {
                    _logger.LogMessage(AppLogLevel.Warning, $"Failed to precompute IL related hashes: {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
            }
            finally
            {
                // Stop the keep-alive and swallow OperationCanceledException while awaiting task completion.
                // キープアライブを停止し、タスク終了待ちで OperationCanceledException を無視。
                keepAliveCts.Cancel();
                try
                {
                    await keepAliveTask;
                }
                catch (OperationCanceledException)
                {
                    // ignore cancellation
                }
                // Update progress after prefetch completes.
                // プリフェッチ完了後に進捗を更新しておく。
                _progressReporter.ReportProgress(0.0);
            }
        }

        /// <summary>
        /// Starts a background task that periodically reports 0% progress to keep the spinner alive
        /// during the IL pre-compute phase. The loop exits cleanly when <paramref name="cts"/> is cancelled.
        /// 事前計算フェーズ中に進捗表示が止まって見えないよう、定期的に 0% を送り続けるバックグラウンドタスクを起動します。
        /// <paramref name="cts"/> をキャンセルするとループは正常終了します。
        /// </summary>
        private Task CreateKeepAliveTask(CancellationTokenSource cts)
            => Task.Run(async () =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(KEEP_ALIVE_INTERVAL_SECONDS), cts.Token);
                        _progressReporter.ReportProgress(0.0);
                    }
                }
                catch (OperationCanceledException)
                {
                    // expected when the keep-alive loop is stopped
                }
            });

        /// <summary>
        /// Creates IL output directories as needed; also creates old/new sub-directories when
        /// <see cref="Models.ConfigSettings.ShouldOutputILText"/> is true.
        /// IL 出力先ディレクトリを必要に応じて作成します。ShouldOutputILText が true の場合は old/new サブディレクトリも作成します。
        /// </summary>
        private void CreateIlOutputDirectoriesIfNeeded()
        {
            PathValidator.ValidateAbsolutePathLengthOrThrow(_ilOutputFolderAbsolutePath);
            _fileSystem.CreateDirectory(_ilOutputFolderAbsolutePath);
            if (_config.ShouldOutputILText)
            {
                PathValidator.ValidateAbsolutePathLengthOrThrow(_ilOldFolderAbsolutePath);
                PathValidator.ValidateAbsolutePathLengthOrThrow(_ilNewFolderAbsolutePath);
                _fileSystem.CreateDirectory(_ilOldFolderAbsolutePath);
                _fileSystem.CreateDirectory(_ilNewFolderAbsolutePath);
                _logger.LogMessage(AppLogLevel.Info, $"Prepared IL output directories: old='{_ilOldFolderAbsolutePath}', new='{_ilNewFolderAbsolutePath}'", shouldOutputMessageToConsole: true);
            }
        }

        /// <summary>
        /// Returns the effective batch size for IL-related pre-computation (at least 1).
        /// IL 関連の事前計算に使う実効バッチサイズを返します（1 以上）。
        /// </summary>
        private int GetEffectiveIlPrecomputeBatchSize()
            => _config.ILPrecomputeBatchSize > 0
                ? _config.ILPrecomputeBatchSize
                : DEFAULT_IL_PRECOMPUTE_BATCH_SIZE;

        /// <summary>
        /// Yields deduplicated old/new file paths in batches of the specified size.
        /// old/new の重複を除いたファイル群を、指定サイズごとのバッチに分けて列挙します。
        /// </summary>
        private IEnumerable<IReadOnlyList<string>> EnumerateDistinctPrecomputeBatches(int batchSize)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var batch = new List<string>(batchSize);

            foreach (var fileAbsolutePath in _fileDiffResultLists.OldFilesAbsolutePath.Concat(_fileDiffResultLists.NewFilesAbsolutePath))
            {
                if (!seen.Add(fileAbsolutePath))
                {
                    continue;
                }

                batch.Add(fileAbsolutePath);
                if (batch.Count >= batchSize)
                {
                    yield return batch;
                    batch = new List<string>(batchSize);
                }
            }

            if (batch.Count > 0)
            {
                yield return batch;
            }
        }
    }
}
