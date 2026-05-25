using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
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
        private const string SPINNER_LABEL_DISCOVERING_FILES = "Discovering files";
        private const string SPINNER_LABEL_SCANNING_ASSEMBLIES = "Scanning assemblies";
        private const string LOG_NETWORK_OPTIMIZED_SKIP_IL = $"Network-optimized mode: skip {Constants.LABEL_IL} precompute to reduce network I/O.";
        private const string LOG_FOLDER_DIFF_COMPLETED = "Folder diff completed.";
        private const string LOG_FILE_DELETED_DURING_COMPARISON = "File '{0}' was deleted from the new folder after enumeration; classifying as Removed.";
        private const string MODE_LOCAL_OPTIMIZED = "Local-optimized";
        private const string MODE_SERVER_NAS_OPTIMIZED = "Server/NAS-optimized";

        private const int DEFAULT_IL_PRECOMPUTE_BATCH_SIZE = 2048;

        /// <summary>
        /// Threshold at which discovery-phase file count is considered "large" and warrants an extra log line.
        /// Above 10,000 entries the enumeration itself can take several seconds, so we notify the user proactively.
        /// 大規模フォルダとして追加ログを出す和集合件数の閾値。1 万件を超えると発見フェーズ自体が数秒を要し始めるため、
        /// この閾値を超えた場合に追加の進捗ログを出力してユーザーへ処理中であることを知らせます。
        /// </summary>
        private const int LARGE_DISCOVERY_FILE_COUNT_LOG_THRESHOLD = 10000;

        private readonly IReadOnlyConfigSettings _config;
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
        private readonly StringComparer _pathComparer;

        /// <summary>
        /// Initializes a new instance of <see cref="FolderDiffService"/> with default file-system and execution-strategy implementations.
        /// 既定のファイルシステム実装と実行戦略で <see cref="FolderDiffService"/> を初期化します。
        /// </summary>
        public FolderDiffService(
            IReadOnlyConfigSettings config,
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
            IReadOnlyConfigSettings config,
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
            IReadOnlyConfigSettings config,
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
            _pathComparer = DetermineRelativePathComparer(_oldFolderAbsolutePath, _newFolderAbsolutePath);
        }

        /// <summary>
        /// Recursively scans old/new folders, excludes ignored extensions, classifies files as
        /// Unchanged / Added / Removed / Modified, and aggregates results in <see cref="FileDiffResultLists"/>.
        /// Progress is reported per file, using the union count of old and new relative paths as 100%.
        /// 2つのフォルダ（old/new）を再帰的に走査し、無視拡張子を除外した上で
        /// Unchanged / Added / Removed / Modified を分類し、<see cref="FileDiffResultLists"/> に集計します。
        /// 進捗は old と new の相対パスの和集合件数を母数として 1 件ごとに報告します。
        /// </summary>
        public async Task ExecuteFolderDiffAsync(CancellationToken cancellationToken = default)
        {
            LogExecutionMode();
            ClearResultCollections();

            var folderDiffCompleted = false;
            var currentPhase = "initializing folder diff";
            var hasEnumeratedOldFiles = false;
            var hasEnumeratedNewFiles = false;
            int? totalFilesRelativePathCount = null;
            int? maxParallel = null;
            try
            {
                currentPhase = "enumerating files";
                EnumerateAllFiles(ref hasEnumeratedOldFiles, ref hasEnumeratedNewFiles);

                totalFilesRelativePathCount = _executionStrategy.ComputeUnionFileCount(
                    _fileDiffResultLists.OldFilesAbsolutePath,
                    _fileDiffResultLists.NewFilesAbsolutePath);
                if (totalFilesRelativePathCount.Value == 0)
                {
                    _progressReporter.ReportProgress(100);
                    folderDiffCompleted = true;
                    return;
                }

                currentPhase = "determining parallelism";
                maxParallel = _executionStrategy.DetermineMaxParallel();
                currentPhase = "logging discovery statistics";
                LogDiscoveryStats(totalFilesRelativePathCount.Value, maxParallel.Value);
                currentPhase = "scanning assembly candidates";
                ScanAssemblyCandidatesAndLog();
                currentPhase = "validating IL filter strings";
                ValidateILFilterStrings();

                currentPhase = "precomputing IL cache";
                await PrecomputeIlCachesAsync(maxParallel.Value, cancellationToken);

                // Begin the diff classification phase so the bar restarts at 0%.
                // 差分分類フェーズを開始し、バーが 0% から再スタートする。
                _progressReporter.BeginPhase(SPINNER_LABEL_FOLDER_DIFF);

                currentPhase = "creating IL output directories";
                CreateIlOutputDirectoriesIfNeeded();

                var remainingNewFilesAbsolutePathHashSet = new HashSet<string>(_fileDiffResultLists.NewFilesAbsolutePath, _pathComparer);
                int processedFileCount = 0;
                if (maxParallel.Value <= 1)
                {
                    currentPhase = "classifying files sequentially";
                    processedFileCount = await DetermineDiffsSequentiallyAsync(remainingNewFilesAbsolutePathHashSet, totalFilesRelativePathCount.Value, processedFileCount, cancellationToken);
                }
                else
                {
                    currentPhase = "classifying files in parallel";
                    processedFileCount = await DetermineDiffsInParallelAsync(remainingNewFilesAbsolutePathHashSet, totalFilesRelativePathCount.Value, processedFileCount, maxParallel.Value, cancellationToken);
                }

                currentPhase = "processing added files";
                ProcessAddedFiles(remainingNewFilesAbsolutePathHashSet, processedFileCount, totalFilesRelativePathCount.Value);
                folderDiffCompleted = true;
            }
            catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException
                or IOException or UnauthorizedAccessException
                or InvalidOperationException or NotSupportedException)
            {
                LogExpectedFolderDiffFailure(ex, currentPhase, hasEnumeratedOldFiles, hasEnumeratedNewFiles, totalFilesRelativePathCount, maxParallel);
                throw;
            }
            catch (Exception ex)
            {
                LogUnexpectedFolderDiffFailure(ex, currentPhase, hasEnumeratedOldFiles, hasEnumeratedNewFiles, totalFilesRelativePathCount, maxParallel);
                throw;
            }
            finally
            {
                if (folderDiffCompleted)
                {
                    _logger.LogMessage(AppLogLevel.Info, LOG_FOLDER_DIFF_COMPLETED, shouldOutputMessageToConsole: true);
                }
            }
        }

        private void LogExpectedFolderDiffFailure(Exception exception, string currentPhase, bool hasEnumeratedOldFiles, bool hasEnumeratedNewFiles, int? totalFilesRelativePathCount, int? maxParallel)
        {
            _logger.LogMessage(
                AppLogLevel.Error,
                BuildFailureMessage(
                    prefix: "An error occurred while diffing",
                    exception,
                    currentPhase,
                    hasEnumeratedOldFiles,
                    hasEnumeratedNewFiles,
                    totalFilesRelativePathCount,
                    maxParallel),
                shouldOutputMessageToConsole: true,
                exception);
        }

        private void LogUnexpectedFolderDiffFailure(Exception exception, string currentPhase, bool hasEnumeratedOldFiles, bool hasEnumeratedNewFiles, int? totalFilesRelativePathCount, int? maxParallel)
        {
            _logger.LogMessage(
                AppLogLevel.Error,
                BuildFailureMessage(
                    prefix: "An unexpected error occurred while diffing",
                    exception,
                    currentPhase,
                    hasEnumeratedOldFiles,
                    hasEnumeratedNewFiles,
                    totalFilesRelativePathCount,
                    maxParallel),
                shouldOutputMessageToConsole: true,
                exception);
        }

        private string BuildFailureMessage(string prefix, Exception exception, string currentPhase, bool hasEnumeratedOldFiles, bool hasEnumeratedNewFiles, int? totalFilesRelativePathCount, int? maxParallel)
        {
            var message = $"{prefix} '{_oldFolderAbsolutePath}' and '{_newFolderAbsolutePath}' during phase '{currentPhase}'. "
                + $"Mode={GetExecutionModeLabel()}, "
                + $"{PathShapeDiagnostics.DescribeState("OldFolder", _oldFolderAbsolutePath)}, "
                + $"{PathShapeDiagnostics.DescribeState("NewFolder", _newFolderAbsolutePath)}";

            if (maxParallel.HasValue)
            {
                message += $", MaxParallel={maxParallel.Value}";
            }

            if (hasEnumeratedOldFiles)
            {
                message += $", OldFiles={_fileDiffResultLists.OldFilesAbsolutePath.Count}";
            }

            if (hasEnumeratedNewFiles)
            {
                message += $", NewFiles={_fileDiffResultLists.NewFilesAbsolutePath.Count}";
            }

            if (totalFilesRelativePathCount.HasValue)
            {
                message += $", UnionFiles={totalFilesRelativePathCount.Value}";
            }

            return message + $". Failure={exception.GetType().Name}: {exception.Message}";
        }

        private static StringComparer DetermineRelativePathComparer(string oldFolderAbsolutePath, string newFolderAbsolutePath)
        {
            return IsCaseInsensitiveDirectory(oldFolderAbsolutePath) && IsCaseInsensitiveDirectory(newFolderAbsolutePath)
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }

        private static bool IsCaseInsensitiveDirectory(string directoryPath)
        {
            if (OperatingSystem.IsWindows())
            {
                return true;
            }

            try
            {
                string fullPath = Path.GetFullPath(directoryPath);
                if (!Directory.Exists(fullPath))
                {
                    return OperatingSystem.IsMacOS();
                }

                string trimmedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string? parentPath = Path.GetDirectoryName(trimmedPath);
                string directoryName = Path.GetFileName(trimmedPath);
                string alternateName = ToggleAsciiCase(directoryName);
                if (string.IsNullOrEmpty(parentPath) || alternateName == directoryName)
                {
                    return OperatingSystem.IsMacOS();
                }

                return Directory.Exists(Path.Combine(parentPath, alternateName));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return OperatingSystem.IsMacOS();
            }
        }

        private static string ToggleAsciiCase(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (ch is >= 'a' and <= 'z')
                {
                    return value[..i] + char.ToUpperInvariant(ch) + value[(i + 1)..];
                }

                if (ch is >= 'A' and <= 'Z')
                {
                    return value[..i] + char.ToLowerInvariant(ch) + value[(i + 1)..];
                }
            }

            return value;
        }

        /// <summary>
        /// Logs the execution mode (local-optimised / server-NAS-optimised) and the reasoning behind it.
        /// 実行モード（ローカル最適化 / サーバー・NAS 最適化）とその判定理由をログに出力します。
        /// </summary>
        private void LogExecutionMode()
        {
            var reason = $"manual={_config.OptimizeForNetworkShares}, auto={_config.AutoDetectNetworkShares}, oldIsNetwork={_detectedNetworkOld}, newIsNetwork={_detectedNetworkNew}";
            _logger.LogMessage(AppLogLevel.Info, $"Execution mode: {GetExecutionModeLabel()} ({reason})", shouldOutputMessageToConsole: true);
        }

        private string GetExecutionModeLabel()
            => _optimizeForNetworkShares ? MODE_SERVER_NAS_OPTIMIZED : MODE_LOCAL_OPTIMIZED;

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
        private void EnumerateAllFiles(ref bool hasEnumeratedOldFiles, ref bool hasEnumeratedNewFiles)
        {
            // Show a dedicated "Discovering files" phase so the user sees progress during file enumeration.
            // ファイル列挙中にユーザーへ進捗を示すため、専用の「Discovering files」フェーズを表示する。
            _progressReporter.BeginPhase(SPINNER_LABEL_DISCOVERING_FILES);

            _fileDiffResultLists.SetOldFilesAbsolutePath(_executionStrategy.EnumerateIncludedFiles(_oldFolderAbsolutePath, FileDiffResultLists.IgnoredFileLocation.Old));
            hasEnumeratedOldFiles = true;
            _progressReporter.ReportProgress(50.0);

            _fileDiffResultLists.SetNewFilesAbsolutePath(_executionStrategy.EnumerateIncludedFiles(_newFolderAbsolutePath, FileDiffResultLists.IgnoredFileLocation.New));
            hasEnumeratedNewFiles = true;
            _progressReporter.ReportProgress(100.0);
        }

        /// <summary>
        /// Logs parallelism level and file counts after discovery. Does not perform I/O-heavy work.
        /// ディスカバリ後の並列度・ファイル件数をログ出力します。I/O の重い処理は行いません。
        /// </summary>
        private void LogDiscoveryStats(int totalFilesRelativePathCount, int maxParallel)
        {
            _logger.LogMessage(
                AppLogLevel.Info,
                $"Parallel diff processing: maxParallel={maxParallel} (configured={_config.MaxParallelism}, OptimizeForNetworkShares={_optimizeForNetworkShares}, logical processors={Environment.ProcessorCount})",
                shouldOutputMessageToConsole: true);

            int oldCount = _fileDiffResultLists.OldFilesAbsolutePath.Count;
            int newCount = _fileDiffResultLists.NewFilesAbsolutePath.Count;
            _logger.LogMessage(AppLogLevel.Info, $"Discovery complete: old={oldCount}, new={newCount}, union(relative)={totalFilesRelativePathCount}", shouldOutputMessageToConsole: true);

            if (totalFilesRelativePathCount >= LARGE_DISCOVERY_FILE_COUNT_LOG_THRESHOLD)
            {
                _logger.LogMessage(
                    AppLogLevel.Info,
                    $"Large file set detected (union(relative)={totalFilesRelativePathCount}). IL precompute will run in batches to limit peak memory usage.",
                    shouldOutputMessageToConsole: true);
            }
        }

        /// <summary>
        /// Scans files for .NET assembly candidates with per-file progress reporting under a dedicated label,
        /// then logs the precompute-target summary. This eliminates the "dark period" between discovery and IL precompute.
        /// ファイルを .NET アセンブリ候補としてスキャンし、専用ラベル下でファイル単位の進捗を報告した後、
        /// プリコンピュート対象のサマリをログ出力します。ディスカバリと IL プリコンピュートの間の「暗黒期間」を解消します。
        /// </summary>
        private void ScanAssemblyCandidatesAndLog()
        {
            _progressReporter.BeginPhase(SPINNER_LABEL_SCANNING_ASSEMBLIES);

            int dotNetAssemblyCandidates = _executionStrategy.CountDotNetAssemblyCandidates(
                _fileDiffResultLists.OldFilesAbsolutePath,
                _fileDiffResultLists.NewFilesAbsolutePath,
                percentage => _progressReporter.ReportProgress(percentage));

            _progressReporter.ReportProgress(100.0);

            int totalFilesForLog = _fileDiffResultLists.OldFilesAbsolutePath
                .Concat(_fileDiffResultLists.NewFilesAbsolutePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            _logger.LogMessage(
                AppLogLevel.Info,
                $"Precompute targets: totalFiles={totalFilesForLog}, {nameof(dotNetAssemblyCandidates)}={dotNetAssemblyCandidates}, batchSize={GetEffectiveIlPrecomputeBatchSize()}",
                shouldOutputMessageToConsole: true);
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
                _progressReporter.ReportProgress(Math.Min((double)processedFileCount * 100.0 / totalFilesRelativePathCount, 100.0));
            }
        }

        /// <summary>
        /// Validates configured IL filter strings and records warnings for any that appear too short or overly broad.
        /// Also logs each warning to the console.
        /// 設定された IL フィルタ文字列を検証し、短すぎるまたは広範すぎるパターンに対する警告を記録します。
        /// 各警告はコンソールにも出力されます。
        /// </summary>
        private void ValidateILFilterStrings()
        {
            if (_config.ILIgnoreLineContainingStrings == null || _config.ILIgnoreLineContainingStrings.Count == 0)
                return;

            var normalized = _config.ILIgnoreLineContainingStrings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var warnings = ILOutputService.ValidateILFilterStrings(normalized);
            foreach (var warning in warnings)
            {
                _fileDiffResultLists.ILFilterWarnings.Add(warning);
                _logger.LogMessage(AppLogLevel.Warning, warning, shouldOutputMessageToConsole: true);
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
