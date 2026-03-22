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
    public sealed partial class FileDiffService : IFileDiffService
    {
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

    }
}
