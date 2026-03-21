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
    public sealed partial class FolderDiffService : IFolderDiffService
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
            IFolderDiffExecutionStrategy? executionStrategy)
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
            catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException
                or IOException or UnauthorizedAccessException
                or InvalidOperationException or NotSupportedException)
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
    }
}
