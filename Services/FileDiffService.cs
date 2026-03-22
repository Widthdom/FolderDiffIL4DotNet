using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Provides the entry point for individual file comparison (SHA256/IL/text) and the preceding pre-computation phase.
    /// 個々のファイル比較（SHA256/IL/テキスト）と、その前段となる事前計算の入口を提供するサービス。
    /// </summary>
    public sealed class FileDiffService : IFileDiffService
    {
        private const int DEFAULT_TEXT_DIFF_PARALLEL_THRESHOLD_BYTES = 512 * Constants.BYTES_PER_KILOBYTE;
        private const int DEFAULT_TEXT_DIFF_CHUNK_SIZE_BYTES = 64 * Constants.BYTES_PER_KILOBYTE;
        private const int BYTES_PER_MEGABYTE = Constants.BYTES_PER_KILOBYTE * Constants.BYTES_PER_KILOBYTE;
        private readonly IReadOnlyConfigSettings _config;
        private readonly IILOutputService _ilOutputService;
        private readonly string _oldFolderAbsolutePath;
        private readonly string _newFolderAbsolutePath;
        private readonly bool _optimizeForNetworkShares;
        private readonly FileDiffResultLists _fileDiffResultLists;
        private readonly ILoggerService _logger;
        private readonly IFileComparisonService _fileComparisonService;

        /// <summary>
        /// Initializes a new instance of <see cref="FileDiffService"/> with the default <see cref="FileComparisonService"/>.
        /// 既定の <see cref="FileComparisonService"/> で <see cref="FileDiffService"/> を初期化します。
        /// </summary>
        public FileDiffService(
            IReadOnlyConfigSettings config,
            IILOutputService ilOutputService,
            DiffExecutionContext executionContext,
            FileDiffResultLists fileDiffResultLists,
            ILoggerService logger)
            : this(config, ilOutputService, executionContext, fileDiffResultLists, logger, new FileComparisonService())
        {
        }

        /// <summary>
        /// Constructor that allows substituting the comparison I/O for testing.
        /// テスト向けに比較 I/O を差し替え可能なコンストラクタ。
        /// </summary>
        public FileDiffService(
            IReadOnlyConfigSettings config,
            IILOutputService ilOutputService,
            DiffExecutionContext executionContext,
            FileDiffResultLists fileDiffResultLists,
            ILoggerService logger,
            IFileComparisonService fileComparisonService)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(ilOutputService);
            ArgumentNullException.ThrowIfNull(executionContext);
            ArgumentNullException.ThrowIfNull(fileComparisonService);

            _config = config;
            _ilOutputService = ilOutputService;
            _oldFolderAbsolutePath = executionContext.OldFolderAbsolutePath;
            _newFolderAbsolutePath = executionContext.NewFolderAbsolutePath;
            _optimizeForNetworkShares = executionContext.OptimizeForNetworkShares;
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            _fileDiffResultLists = fileDiffResultLists;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _fileComparisonService = fileComparisonService;
        }

        /// <summary>
        /// Runs IL-cache pre-computation (delegated to <see cref="ILOutputService"/>).
        /// IL キャッシュ関連の事前計算を実行します（実体は <see cref="ILOutputService"/> に委譲）。
        /// </summary>
        public Task PrecomputeAsync(System.Collections.Generic.IEnumerable<string> filesAbsolutePath, int maxParallel, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filesAbsolutePath);
            if (_config.SkipIL)
            {
                return Task.CompletedTask;
            }

            return _ilOutputService.PrecomputeAsync(filesAbsolutePath, maxParallel, cancellationToken);
        }

        /// <summary>
        /// Determines whether two files are equal by trying SHA256, then IL, then text comparison in order.
        /// Results are recorded in <see cref="FileDiffResultLists"/> and honour network-optimisation and extension settings.
        /// 2つのファイルが等しいかを判定し、SHA256→IL→テキストの順で比較を試みる統合メソッド。
        /// 判定結果は <see cref="FileDiffResultLists"/> に記録され、ネットワーク最適化や拡張子設定にも追従します。
        /// </summary>
        public async Task<bool> FilesAreEqualAsync(string fileRelativePath, int maxParallel = 1, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string file1AbsolutePath = Path.Combine(_oldFolderAbsolutePath, fileRelativePath);
            string file2AbsolutePath = Path.Combine(_newFolderAbsolutePath, fileRelativePath);
            try
            {
                // 1) SHA256: exit early when file size and content are identical.
                //    Also capture computed hex hashes to seed the IL cache, avoiding redundant SHA256 recomputation.
                // 1) SHA256: ファイルサイズや内容が完全一致する場合はここで終了。
                //    計算済みハッシュ値を IL キャッシュに事前登録し、SHA256 の二重計算を回避する。
                var (areHashEqual, hash1Hex, hash2Hex) = await _fileComparisonService.DiffFilesByHashWithHexAsync(file1AbsolutePath, file2AbsolutePath);
                if (hash1Hex != null)
                {
                    _ilOutputService.PreSeedFileHash(file1AbsolutePath, hash1Hex);
                }
                if (hash2Hex != null)
                {
                    _ilOutputService.PreSeedFileHash(file2AbsolutePath, hash2Hex);
                }
                if (areHashEqual)
                {
                    _fileDiffResultLists.RecordDiffDetail(fileRelativePath, FileDiffResultLists.DiffDetailResult.SHA256Match);
                    return true;
                }

                // 2) IL for .NET assemblies: delegated to a separate service because it involves assembly-specific processing (MVID / configured-string line exclusion).
                //    When SkipIL is true, skip IL comparison and fall through to text/binary comparison.
                // 2) .NET アセンブリなら IL: IL 比較は行除外（MVID や設定文字列）などアセンブリ固有処理を伴うため別サービスに委譲。
                //    SkipIL が true の場合は IL 比較をスキップしてテキスト/バイナリ比較に進む。
                var dotNetDetectionResult = _config.SkipIL
                    ? default
                    : _fileComparisonService.DetectDotNetExecutable(file1AbsolutePath);
                if (!_config.SkipIL && dotNetDetectionResult.IsFailure)
                {
                    _logger.LogMessage(
                        AppLogLevel.Warning,
                        $"Failed to detect whether '{fileRelativePath}' is a .NET executable. Skipping IL diff.",
                        shouldOutputMessageToConsole: true,
                        dotNetDetectionResult.Exception);
                }

                if (!_config.SkipIL && dotNetDetectionResult.IsDotNetExecutable)
                {
                    try
                    {
                        var (areDotNetAssembliesEqual, disassemblerLabel) = await _ilOutputService.DiffDotNetAssembliesAsync(fileRelativePath, _oldFolderAbsolutePath, _newFolderAbsolutePath, _config.ShouldOutputILText, cancellationToken);
                        _fileDiffResultLists.RecordDiffDetail(
                            fileRelativePath,
                            areDotNetAssembliesEqual ? FileDiffResultLists.DiffDetailResult.ILMatch : FileDiffResultLists.DiffDetailResult.ILMismatch,
                            disassemblerLabel);

                        // Best-effort assembly semantic analysis for ILMismatch assemblies
                        if (!areDotNetAssembliesEqual && _config.ShouldIncludeAssemblySemanticChangesInReport)
                        {
                            TryAnalyzeAssemblySemanticChanges(fileRelativePath, file1AbsolutePath, file2AbsolutePath);
                        }

                        return areDotNetAssembliesEqual;
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogMessage(AppLogLevel.Error, $"IL diff failed for '{fileRelativePath}'. {ex.Message}", shouldOutputMessageToConsole: true, ex);
                        throw;
                    }
                }

                // 3) Text comparison for text-extension files: sequential when network-optimised, otherwise parallel above a threshold.
                // 3) テキスト拡張子ならテキスト比較: ネットワーク最適化時は逐次、それ以外は閾値に応じて並列比較を選択。
                string fileExtension = Path.GetExtension(file1AbsolutePath);
                if (_config.TextFileExtensions.Any(configuredExtension => string.Equals(configuredExtension, fileExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    bool areTextFilesEqual = await CompareAsTextAsync(fileRelativePath, file1AbsolutePath, file2AbsolutePath, maxParallel);
                    _fileDiffResultLists.RecordDiffDetail(fileRelativePath, areTextFilesEqual ? FileDiffResultLists.DiffDetailResult.TextMatch : FileDiffResultLists.DiffDetailResult.TextMismatch);
                    return areTextFilesEqual;
                }

                _fileDiffResultLists.RecordDiffDetail(fileRelativePath, FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
                return false;
            }
            // Failures in the main comparison directly affect file-classification correctness,
            // so even expected runtime exceptions are logged as errors and re-thrown to the caller.
            // このメソッドの本比較で起きた失敗はファイル分類の正しさに直結するため、
            // 想定内の実行時例外も error を残して呼び出し元へ再スローする。
            catch (Exception ex) when (ex is DirectoryNotFoundException or IOException
                or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
            {
                LogExpectedFileDiffFailure(file1AbsolutePath, file2AbsolutePath, ex);
                throw;
            }
            catch (Exception ex)
            {
                LogUnexpectedFileDiffFailure(file1AbsolutePath, file2AbsolutePath, ex);
                throw;
            }
        }

        /// <summary>
        /// Best-effort assembly semantic analysis using System.Reflection.Metadata.
        /// Failures are logged but do not affect the comparison result.
        /// System.Reflection.Metadata を使用したベストエフォートのアセンブリセマンティック解析。
        /// 失敗してもファイル比較結果には影響しません。
        /// </summary>
        private void TryAnalyzeAssemblySemanticChanges(string fileRelativePath, string oldPath, string newPath)
        {
            try
            {
                var summary = AssemblyMethodAnalyzer.Analyze(oldPath, newPath);
                if (summary?.HasChanges == true)
                {
                    _fileDiffResultLists.FileRelativePathToAssemblySemanticChanges[fileRelativePath] = summary;
                }
            }
#pragma warning disable CA1031 // ベストエフォート解析のため全例外をキャッチ / Catch-all for best-effort analysis
            catch (Exception ex)
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Method-level analysis failed for '{fileRelativePath}': {ex.Message}",
                    shouldOutputMessageToConsole: false, ex);
            }
#pragma warning restore CA1031
        }

        private void LogExpectedFileDiffFailure(string file1AbsolutePath, string file2AbsolutePath, Exception exception)
        {
            _logger.LogMessage(
                AppLogLevel.Error,
                $"An error occurred while diffing '{file1AbsolutePath}' and '{file2AbsolutePath}'.",
                shouldOutputMessageToConsole: true,
                exception);
        }

        private void LogUnexpectedFileDiffFailure(string file1AbsolutePath, string file2AbsolutePath, Exception exception)
        {
            _logger.LogMessage(
                AppLogLevel.Error,
                $"An unexpected error occurred while diffing '{file1AbsolutePath}' and '{file2AbsolutePath}'.",
                shouldOutputMessageToConsole: true,
                exception);
        }

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
                _logger.LogMessage(AppLogLevel.Warning, $"Parallel text diff failed for '{fileRelativePath}'. Falling back to sequential text diff.", shouldOutputMessageToConsole: true, ex);
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
