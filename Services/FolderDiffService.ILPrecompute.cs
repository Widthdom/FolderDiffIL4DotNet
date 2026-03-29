using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Common;
using FolderDiffIL4DotNet.Core.IO;

namespace FolderDiffIL4DotNet.Services
{
    // IL-cache pre-computation logic (batched warm-up with progress reporting, directory preparation).
    // IL キャッシュ事前計算ロジック（進捗報告付きバッチウォームアップ、ディレクトリ準備）。
    public sealed partial class FolderDiffService
    {
        private const string SPINNER_LABEL_IL_PRECOMPUTE = "Precomputing IL caches";

        /// <summary>
        /// Runs IL-cache pre-computation for all old/new files: SHA256 hashing, internal key generation, and disassembler-result cache warm-up.
        /// Reports per-batch progress under a dedicated label so that the user sees meaningful progress
        /// instead of a stalled 0% during the precompute phase.
        /// 新旧すべてのファイルを対象に、IL キャッシュ用の事前計算（SHA256 計算、内部キー生成、逆アセンブラ結果キャッシュのウォームアップ）を実行します。
        /// プリコンピュートフェーズ中に 0% のまま停滞して見えないよう、バッチ単位で進捗を専用ラベルで報告します。
        /// </summary>
        private async Task PrecomputeIlCachesAsync(int maxParallel, CancellationToken cancellationToken = default)
        {
            // Begin the IL precompute phase with numbered label.
            // 番号付きラベルで IL プリコンピュートフェーズを開始する。
            _progressReporter.BeginPhase(SPINNER_LABEL_IL_PRECOMPUTE);

            // In network-optimised mode, skip SHA256/IL cache warm-up.
            // ネットワーク最適化モードでは SHA256/IL キャッシュのウォームアップをスキップ。
            if (_optimizeForNetworkShares)
            {
                _logger.LogMessage(AppLogLevel.Info, LOG_NETWORK_OPTIMIZED_SKIP_IL, shouldOutputMessageToConsole: true);
                _progressReporter.ReportProgress(100.0);
                return;
            }
            int precomputeBatchSize = GetEffectiveIlPrecomputeBatchSize();

            try
            {
                try
                {
                    // Pre-compute failure only degrades performance; the main comparison re-executes what is needed.
                    // Therefore this is best-effort: log a warning and continue.
                    // プリコンピュート失敗は性能劣化に留まり、後続の本比較で必要な処理は再実行される。
                    // そのため、ここは best-effort として warning を残しつつ継続する。
                    int totalDistinctFiles = CountDistinctPrecomputeTargets();
                    int processedFiles = 0;
                    foreach (var batch in EnumerateDistinctPrecomputeBatches(precomputeBatchSize))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await _fileDiffService.PrecomputeAsync(batch, maxParallel, cancellationToken);
                        processedFiles += batch.Count;
                        if (totalDistinctFiles > 0)
                        {
                            _progressReporter.ReportProgress(Math.Min((double)processedFiles * 100.0 / totalDistinctFiles, 100.0));
                        }
                    }
                }
                catch (Exception ex) when (ExceptionFilters.IsFileIoOrOperationRecoverable(ex))
                {
                    _logger.LogMessage(AppLogLevel.Warning, $"Failed to precompute IL related hashes: {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
            }
            finally
            {
                // Finalize precompute progress at 100% so the bar completes visually before switching phases.
                // フェーズ切り替え前に 100% を表示してプリコンピュートの進捗バーを視覚的に完了させる。
                _progressReporter.ReportProgress(100.0);
            }
        }

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
        /// Counts the total number of distinct file paths across old and new file lists.
        /// Used as the denominator for precompute progress reporting.
        /// old/new ファイルリスト全体の重複排除後の件数を返します。プリコンピュート進捗報告の母数に使用します。
        /// </summary>
        private int CountDistinctPrecomputeTargets()
            => _fileDiffResultLists.OldFilesAbsolutePath
                .Concat(_fileDiffResultLists.NewFilesAbsolutePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

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
