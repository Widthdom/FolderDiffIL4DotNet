using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Console;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Service that compares two folders and classifies files as Unchanged / Added / Removed / Modified.
    /// フォルダ間の差分を比較し、ファイルを Unchanged / Added / Removed / Modified に分類するサービス。
    /// </summary>
    public sealed class FolderDiffService : IFolderDiffService
    {
        private const string SPINNER_LABEL_FOLDER_DIFF = "Diffing folders";
        private const string LOG_NETWORK_OPTIMIZED_SKIP_IL = $"Network-optimized mode: skip {Constants.LABEL_IL} precompute to reduce network I/O.";
        private const string LOG_FOLDER_DIFF_COMPLETED = "Folder diff completed.";
        private const string LOG_FILE_DELETED_DURING_COMPARISON = "File '{0}' was deleted from the new folder after enumeration; classifying as Removed.";
        private const string MODE_LOCAL_OPTIMIZED = "Local-optimized";
        private const string MODE_SERVER_NAS_OPTIMIZED = "Server/NAS-optimized";

        /// <summary>
        /// Keep-alive output interval in seconds. Set to 5s so that CI environments
        /// and SSH sessions with 10-30s no-output timeouts receive a heartbeat well within the limit.
        /// キープアライブの出力間隔（秒）。CI 環境や SSH セッションは無出力が 10～30 秒続くと
        /// タイムアウトすることが多いため、5 秒間隔とすることで安全マージンを確保しています。
        /// </summary>
        private const int KEEP_ALIVE_INTERVAL_SECONDS = 5;

        private const int DEFAULT_IL_PRECOMPUTE_BATCH_SIZE = 2048;

        /// <summary>
        /// Threshold at which discovery-phase file count is considered "large" and warrants an extra log line.
        /// Above 10,000 entries the enumeration itself can take several seconds, so we notify the user proactively.
        /// 大規模フォルダとして追加ログを出す和集合件数の閾値。1 万件を超えると発見フェーズ自体が数秒を要し始めるため、
        /// この閾値を超えた場合に追加の進捗ログを出力してユーザーへ処理中であることを知らせます。
        /// </summary>
        private const int LARGE_DISCOVERY_FILE_COUNT_LOG_THRESHOLD = 10000;

        private readonly ConfigSettings _config;
        private readonly ProgressReportService _progressReporter;
        private readonly string _oldFolderAbsolutePath;
        private readonly string _newFolderAbsolutePath;
        private readonly string _ilOutputFolderAbsolutePath;
        private readonly string _ilOldFolderAbsolutePath;
        private readonly string _ilNewFolderAbsolutePath;
        private readonly IFileDiffService _fileDiffService;
        private readonly bool _optimizeForNetworkShares;
        private readonly bool _detectedNetworkOld;
        private readonly bool _detectedNetworkNew;
        private readonly FileDiffResultLists _fileDiffResultLists;
        private readonly ILoggerService _logger;
        private readonly IFileSystemService _fileSystem;
        private readonly IFolderDiffExecutionStrategy _executionStrategy;

        public FolderDiffService(
            ConfigSettings config,
            ProgressReportService progressReporter,
            DiffExecutionContext executionContext,
            IFileDiffService fileDiffService,
            FileDiffResultLists fileDiffResultLists,
            ILoggerService logger)
            : this(config, progressReporter, executionContext, fileDiffService, fileDiffResultLists, logger, new FileSystemService(), null)
        {
        }

        /// <summary>
        /// Constructor that allows substituting the file-system implementation for testing.
        /// テスト向けにファイルシステム実装を差し替え可能なコンストラクタ。
        /// </summary>
        public FolderDiffService(
            ConfigSettings config,
            ProgressReportService progressReporter,
            DiffExecutionContext executionContext,
            IFileDiffService fileDiffService,
            FileDiffResultLists fileDiffResultLists,
            ILoggerService logger,
            IFileSystemService fileSystem)
            : this(config, progressReporter, executionContext, fileDiffService, fileDiffResultLists, logger, fileSystem, null)
        {
        }

        /// <summary>
        /// Constructor that also allows substituting the execution strategy for testing or DI.
        /// テストや DI 向けに戦略オブジェクトも差し替え可能なコンストラクタ。
        /// </summary>
        public FolderDiffService(
            ConfigSettings config,
            ProgressReportService progressReporter,
            DiffExecutionContext executionContext,
            IFileDiffService fileDiffService,
            FileDiffResultLists fileDiffResultLists,
            ILoggerService logger,
            IFileSystemService fileSystem,
            IFolderDiffExecutionStrategy executionStrategy)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(progressReporter);
            ArgumentNullException.ThrowIfNull(executionContext);
            ArgumentNullException.ThrowIfNull(fileDiffService);
            ArgumentNullException.ThrowIfNull(fileSystem);

            _config = config;
            _progressReporter = progressReporter;
            _progressReporter.SetLabel(SPINNER_LABEL_FOLDER_DIFF);
            _oldFolderAbsolutePath = executionContext.OldFolderAbsolutePath;
            _newFolderAbsolutePath = executionContext.NewFolderAbsolutePath;
            _ilOutputFolderAbsolutePath = executionContext.IlOutputFolderAbsolutePath;
            _ilOldFolderAbsolutePath = executionContext.IlOldFolderAbsolutePath;
            _ilNewFolderAbsolutePath = executionContext.IlNewFolderAbsolutePath;
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            _fileDiffResultLists = fileDiffResultLists;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _detectedNetworkOld = executionContext.DetectedNetworkOld;
            _detectedNetworkNew = executionContext.DetectedNetworkNew;
            _optimizeForNetworkShares = executionContext.OptimizeForNetworkShares;
            _fileDiffService = fileDiffService;
            _fileSystem = fileSystem;
            _executionStrategy = executionStrategy ?? new FolderDiffExecutionStrategy(config, executionContext, fileDiffResultLists, fileSystem);
        }

        /// <summary>
        /// Recursively scans old/new folders, excludes ignored extensions, classifies files as
        /// Unchanged / Added / Removed / Modified, and aggregates results in <see cref="FileDiffResultLists"/>.
        /// Progress is reported per file, using the union count of old and new relative paths as 100%.
        /// 2つのフォルダ（old/new）を再帰的に走査し、無視拡張子を除外した上で
        /// Unchanged / Added / Removed / Modified を分類し、<see cref="FileDiffResultLists"/> に集計します。
        /// 進捗は old と new の相対パスの和集合件数を母数として 1 件ごとに報告します。
        /// </summary>
        /// <remarks>
        /// Processing flow / 主な処理の流れ:
        /// 1) Enumerate old/new file lists (excluding IgnoredExtensions) / old/new のファイル一覧取得（IgnoredExtensions を除外）
        /// 2) Compute progress denominator from old-union-new relative-path count / 進捗の母数を old∪new の相対パス件数で算出
        /// 3) Iterate old-side files and compare in order: MD5 hash -> IL disassembly -> text diff -> Modified
        ///    old 側を基準に走査し、MD5→IL→テキストの順で比較。一致しなければ Modified、new に無ければ Removed
        /// 4) Remaining new-only paths are classified as Added / old に無く new のみのパスは Added として記録
        ///
        /// Notes / 補足:
        /// - IL precompute is best-effort (warnings logged); enumeration/output-dir/main-comparison failures are errors and re-thrown.
        ///   IL プリコンピュートは best-effort（warning 記録で継続）。列挙・出力先準備・本比較の失敗は error で再スロー。
        /// </remarks>
        public async Task ExecuteFolderDiffAsync()
        {
            LogExecutionMode();
            ClearResultCollections();

            var folderDiffCompleted = false;
            try
            {
                _progressReporter.ReportProgress(0.0);

                EnumerateAllFiles();

                var totalFilesRelativePathCount = _executionStrategy.ComputeUnionFileCount(
                    _fileDiffResultLists.OldFilesAbsolutePath,
                    _fileDiffResultLists.NewFilesAbsolutePath);
                if (totalFilesRelativePathCount == 0)
                {
                    _progressReporter.ReportProgress(100);
                    folderDiffCompleted = true;
                    return;
                }
                _progressReporter.ReportProgress(0.0);

                var maxParallel = _executionStrategy.DetermineMaxParallel();
                LogDiscoveryAndParallelStats(totalFilesRelativePathCount, maxParallel);

                await PrecomputeIlCachesAsync(maxParallel);
                _progressReporter.ReportProgress(0.0);

                CreateIlOutputDirectoriesIfNeeded();

                var remainingNewFilesAbsolutePathHashSet = new HashSet<string>(_fileDiffResultLists.NewFilesAbsolutePath, StringComparer.OrdinalIgnoreCase);
                int processedFileCount = 0;
                if (maxParallel <= 1)
                {
                    processedFileCount = await DetermineDiffsSequentiallyAsync(remainingNewFilesAbsolutePathHashSet, totalFilesRelativePathCount, processedFileCount);
                }
                else
                {
                    processedFileCount = await DetermineDiffsInParallelAsync(remainingNewFilesAbsolutePathHashSet, totalFilesRelativePathCount, processedFileCount, maxParallel);
                }

                ProcessAddedFiles(remainingNewFilesAbsolutePathHashSet, processedFileCount, totalFilesRelativePathCount);
                folderDiffCompleted = true;
            }
            // Enumeration, IL-output-dir preparation, and main-comparison failures affect the overall run correctness,
            // so even expected runtime exceptions are logged as errors and re-thrown here.
            // 列挙、IL 出力先準備、本比較での失敗は run 全体の正しさに影響するため、
            // 想定内の実行時例外もここで error を記録して再スローする。
            catch (ArgumentException ex)
            {
                LogExpectedFolderDiffFailure(ex);
                throw;
            }
            catch (DirectoryNotFoundException ex)
            {
                LogExpectedFolderDiffFailure(ex);
                throw;
            }
            catch (IOException ex)
            {
                LogExpectedFolderDiffFailure(ex);
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogExpectedFolderDiffFailure(ex);
                throw;
            }
            catch (InvalidOperationException ex)
            {
                LogExpectedFolderDiffFailure(ex);
                throw;
            }
            catch (NotSupportedException ex)
            {
                LogExpectedFolderDiffFailure(ex);
                throw;
            }
            catch (Exception ex)
            {
                LogUnexpectedFolderDiffFailure(ex);
                throw;
            }
            finally
            {
                if (folderDiffCompleted)
                {
                    lock (ConsoleRenderCoordinator.RenderSyncRoot)
                    {
                        Console.WriteLine(LOG_FOLDER_DIFF_COMPLETED);
                        Console.Out.Flush();
                    }
                }
            }
        }

        /// <summary>
        /// Runs IL-cache pre-computation for all old/new files: MD5 hashing, internal key generation, and disassembler-result cache warm-up.
        /// 新旧すべてのファイルを対象に、IL キャッシュ用の事前計算（MD5 計算、内部キー生成、逆アセンブラ結果キャッシュのウォームアップ）を実行します。
        /// </summary>
        private async Task PrecomputeIlCachesAsync(int maxParallel)
        {
            // In network-optimised mode, skip MD5/IL cache warm-up and only reset progress to zero.
            // ネットワーク最適化モードでは MD5/IL キャッシュのウォームアップをスキップし、進捗のみゼロリセット。
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
                catch (IOException ex)
                {
                    _logger.LogMessage(AppLogLevel.Warning, $"Failed to precompute IL related hashes: {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogMessage(AppLogLevel.Warning, $"Failed to precompute IL related hashes: {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogMessage(AppLogLevel.Warning, $"Failed to precompute IL related hashes: {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
                catch (NotSupportedException ex)
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

        private void LogExpectedFolderDiffFailure(Exception exception)
        {
            _logger.LogMessage(
                AppLogLevel.Error,
                $"An error occurred while diffing '{_oldFolderAbsolutePath}' and '{_newFolderAbsolutePath}'.",
                shouldOutputMessageToConsole: true,
                exception);
        }

        private void LogUnexpectedFolderDiffFailure(Exception exception)
        {
            _logger.LogMessage(
                AppLogLevel.Error,
                $"An unexpected error occurred while diffing '{_oldFolderAbsolutePath}' and '{_newFolderAbsolutePath}'.",
                shouldOutputMessageToConsole: true,
                exception);
        }

        /// <summary>
        /// Performs sequential (single-threaded) diff classification, processing old-side files one by one
        /// into Unchanged / Modified / Removed and updating progress.
        /// 逐次（単一スレッド）で差分判定を行い、old 側を 1 件ずつ Unchanged / Modified / Removed に分類して進捗を更新します。
        /// </summary>
        private async Task<int> DetermineDiffsSequentiallyAsync(HashSet<string> remainingNewFilesAbsolutePathHashSet, int totalFilesRelativePathCount, int processedFileCountSoFar)
        {
            foreach (var oldFileAbsolutePath in _fileDiffResultLists.OldFilesAbsolutePath)
            {
                var fileRelativePath = Path.GetRelativePath(_oldFolderAbsolutePath, oldFileAbsolutePath);
                var newFileAbsolutePath = Path.Combine(_newFolderAbsolutePath, fileRelativePath);

                if (remainingNewFilesAbsolutePathHashSet.Contains(newFileAbsolutePath))
                {
                    remainingNewFilesAbsolutePathHashSet.Remove(newFileAbsolutePath);
                    bool areEqual;
                    try
                    {
                        areEqual = await _fileDiffService.FilesAreEqualAsync(fileRelativePath);
                    }
                    catch (FileNotFoundException)
                    {
                        // If the new-side file was deleted after enumeration, treat as Removed and log a warning.
                        // 列挙後に new 側ファイルが削除された場合は Removed として扱い、警告を記録して継続する。
                        _logger.LogMessage(AppLogLevel.Warning, string.Format(System.Globalization.CultureInfo.InvariantCulture, LOG_FILE_DELETED_DURING_COMPARISON, fileRelativePath), shouldOutputMessageToConsole: true);
                        _fileDiffResultLists.AddRemovedFileAbsolutePath(oldFileAbsolutePath);
                        processedFileCountSoFar++;
                        _progressReporter.ReportProgress((double)processedFileCountSoFar * 100.0 / totalFilesRelativePathCount);
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
                _progressReporter.ReportProgress((double)processedFileCountSoFar * 100.0 / totalFilesRelativePathCount);
            }
            return processedFileCountSoFar;
        }

        /// <summary>
        /// Performs parallel diff classification. Only access to the remaining-new-files set is guarded by a
        /// fine-grained lock; classification results are recorded via thread-safe collection APIs.
        /// 並列に差分判定を行います。new 側の未処理集合へのアクセスのみ低粒度ロックで保護し、
        /// 分類結果の追加はスレッドセーフなコレクション API で記録します。
        /// </summary>
        private async Task<int> DetermineDiffsInParallelAsync(HashSet<string> remainingNewFilesAbsolutePathHashSet, int totalFilesRelativePathCount, int processedFileCountSoFar, int maxParallel)
        {
            // Lock that serialises access to remainingNewFilesAbsolutePathHashSet so that
            // Contains-then-Remove is atomic, preventing duplicate comparisons and race conditions.
            // Only the membership-check-and-remove section is locked; expensive work runs outside the lock.
            // new 側の未処理集合へのアクセスを直列化するロック。Contains→Remove をアトミックに行い、
            // 二重比較とレースコンディションを防ぐ。ロック範囲は最小限にし、重い処理はロック外で実行。
            var lockRemaining = new object();
            int processedFileCount = processedFileCountSoFar;

            await Parallel.ForEachAsync(_fileDiffResultLists.OldFilesAbsolutePath, new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, async (oldFileAbsolutePath, cancellationToken) =>
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
                        areFilesEqual = await _fileDiffService.FilesAreEqualAsync(fileRelativePath, maxParallel);
                    }
                    catch (FileNotFoundException)
                    {
                        // If the new-side file was deleted after enumeration, treat as Removed and log a warning.
                        // 列挙後に new 側ファイルが削除された場合は Removed として扱い、警告を記録して継続する。
                        _logger.LogMessage(AppLogLevel.Warning, string.Format(System.Globalization.CultureInfo.InvariantCulture, LOG_FILE_DELETED_DURING_COMPARISON, fileRelativePath), shouldOutputMessageToConsole: true);
                        _fileDiffResultLists.AddRemovedFileAbsolutePath(oldFileAbsolutePath);
                        var doneOnDelete = Interlocked.Increment(ref processedFileCount);
                        _progressReporter.ReportProgress((double)doneOnDelete * 100.0 / totalFilesRelativePathCount);
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
                _progressReporter.ReportProgress((double)done * 100.0 / totalFilesRelativePathCount);
            });

            return processedFileCount;
        }

        /// <summary>
        /// Logs the execution mode (local-optimised / server-NAS-optimised) and the reasoning behind it.
        /// 実行モード（ローカル最適化 / サーバー・NAS 最適化）とその判定理由をログに出力します。
        /// </summary>
        private void LogExecutionMode()
        {
            var mode = _optimizeForNetworkShares ? MODE_SERVER_NAS_OPTIMIZED : MODE_LOCAL_OPTIMIZED;
            var reason = $"manual={_config.OptimizeForNetworkShares}, auto={_config.AutoDetectNetworkShares}, oldIsNetwork={_detectedNetworkOld}, newIsNetwork={_detectedNetworkNew}";
            _logger.LogMessage(AppLogLevel.Info, $"Execution mode: {mode} ({reason})", shouldOutputMessageToConsole: true);
        }

        /// <summary>
        /// Clears all classification results from the previous run.
        /// 前回実行の分類結果をすべてクリアします。
        /// </summary>
        private void ClearResultCollections()
        {
            _fileDiffResultLists.ResetAll();
        }

        /// <summary>
        /// Enumerates old/new folder files (excluding ignored extensions) into <see cref="FileDiffResultLists"/>.
        /// 無視拡張子を除いた旧・新フォルダのファイル一覧を <see cref="FileDiffResultLists"/> に格納します。
        /// </summary>
        private void EnumerateAllFiles()
        {
            _fileDiffResultLists.SetOldFilesAbsolutePath(_executionStrategy.EnumerateIncludedFiles(_oldFolderAbsolutePath, FileDiffResultLists.IgnoredFileLocation.Old));
            _progressReporter.ReportProgress(0.0);
            _fileDiffResultLists.SetNewFilesAbsolutePath(_executionStrategy.EnumerateIncludedFiles(_newFolderAbsolutePath, FileDiffResultLists.IgnoredFileLocation.New));
            _progressReporter.ReportProgress(0.0);
        }

        /// <summary>
        /// Logs parallelism level, file counts, and .NET assembly candidate counts.
        /// 並列度・ファイル件数・.NET アセンブリ候補数をログに出力します。
        /// </summary>
        private void LogDiscoveryAndParallelStats(int totalFilesRelativePathCount, int maxParallel)
        {
            _logger.LogMessage(
                AppLogLevel.Info,
                $"Parallel diff processing: maxParallel={maxParallel} (configured={_config.MaxParallelism}, OptimizeForNetworkShares={_optimizeForNetworkShares}, logical processors={Environment.ProcessorCount})",
                shouldOutputMessageToConsole: true);
            _progressReporter.ReportProgress(0.0);

            int oldCount = _fileDiffResultLists.OldFilesAbsolutePath.Count;
            int newCount = _fileDiffResultLists.NewFilesAbsolutePath.Count;
            _logger.LogMessage(AppLogLevel.Info, $"Discovery complete: old={oldCount}, new={newCount}, union(relative)={totalFilesRelativePathCount}", shouldOutputMessageToConsole: true);

            // Also log approximate .NET assembly candidate count.
            // .NET アセンブリ候補数も概算表示
            int dotNetAssemblyCandidates = _executionStrategy.CountDotNetAssemblyCandidates(
                _fileDiffResultLists.OldFilesAbsolutePath,
                _fileDiffResultLists.NewFilesAbsolutePath);
            int totalFilesForLog = _fileDiffResultLists.OldFilesAbsolutePath
                .Concat(_fileDiffResultLists.NewFilesAbsolutePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            _logger.LogMessage(
                AppLogLevel.Info,
                $"Precompute targets: totalFiles={totalFilesForLog}, {nameof(dotNetAssemblyCandidates)}={dotNetAssemblyCandidates}, batchSize={GetEffectiveIlPrecomputeBatchSize()}",
                shouldOutputMessageToConsole: true);
            if (totalFilesRelativePathCount >= LARGE_DISCOVERY_FILE_COUNT_LOG_THRESHOLD)
            {
                _logger.LogMessage(
                    AppLogLevel.Info,
                    $"Large file set detected (union(relative)={totalFilesRelativePathCount}). IL precompute will run in batches to limit peak memory usage.",
                    shouldOutputMessageToConsole: true);
            }
            _progressReporter.ReportProgress(0.0);
        }

        /// <summary>
        /// Creates IL output directories as needed; also creates old/new sub-directories when
        /// <see cref="ConfigSettings.ShouldOutputILText"/> is true.
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
        /// Records remaining new-side files (absent from old side) as Added and updates progress.
        /// new 側に残っているファイル（old 側に存在しないもの）を Added として記録し、進捗を更新します。
        /// </summary>
        private void ProcessAddedFiles(IEnumerable<string> remainingNewFiles, int processedFileCount, int totalFilesRelativePathCount)
        {
            foreach (var newFileAbsolutePath in remainingNewFiles)
            {
                _fileDiffResultLists.AddAddedFileAbsolutePath(newFileAbsolutePath);
                processedFileCount++;
                _progressReporter.ReportProgress((double)processedFileCount * 100.0 / totalFilesRelativePathCount);
            }
        }

        /// <summary>
        /// Records a warning when a Modified file's new-side timestamp is older than the old side.
        /// Modified と判定されたファイルについて、new 側の更新日時が old 側より古い場合に警告情報を記録します。
        /// </summary>
        private void RecordNewFileTimestampOlderThanOldWarningIfNeeded(string fileRelativePath, string oldFileAbsolutePath, string newFileAbsolutePath)
        {
            if (!_config.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp)
            {
                return;
            }

            var oldLastWriteTimeUtc = _fileSystem.GetLastWriteTimeUtc(oldFileAbsolutePath);
            var newLastWriteTimeUtc = _fileSystem.GetLastWriteTimeUtc(newFileAbsolutePath);
            if (newLastWriteTimeUtc >= oldLastWriteTimeUtc)
            {
                return;
            }

            _fileDiffResultLists.RecordNewFileTimestampOlderThanOldWarning(
                fileRelativePath,
                Caching.TimestampCache.GetOrAdd(oldFileAbsolutePath),
                Caching.TimestampCache.GetOrAdd(newFileAbsolutePath));
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
