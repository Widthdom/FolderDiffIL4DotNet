using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Plugin.Abstractions;

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
        private readonly IReadOnlyList<IFileComparisonHook> _comparisonHooks;

        // In-memory cache for assembly semantic analysis results, keyed by (oldSHA256, newSHA256).
        // Avoids redundant analysis when the same assembly content appears at multiple paths.
        // アセンブリセマンティック解析結果のインメモリキャッシュ。(oldSHA256, newSHA256) をキーとする。
        // 同一アセンブリ内容が複数パスに存在する場合の重複解析を回避する。
        private readonly ConcurrentDictionary<(string OldHash, string NewHash), AssemblySemanticChangesSummary?> _semanticAnalysisCache = new();

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
            : this(config, ilOutputService, executionContext, fileDiffResultLists, logger, new FileComparisonService(), Array.Empty<IFileComparisonHook>())
        {
        }

        /// <summary>
        /// Constructor that allows substituting the comparison I/O and hooks for testing.
        /// テスト向けに比較 I/O とフックを差し替え可能なコンストラクタ。
        /// </summary>
        public FileDiffService(
            IReadOnlyConfigSettings config,
            IILOutputService ilOutputService,
            DiffExecutionContext executionContext,
            FileDiffResultLists fileDiffResultLists,
            ILoggerService logger,
            IFileComparisonService fileComparisonService)
            : this(config, ilOutputService, executionContext, fileDiffResultLists, logger, fileComparisonService, Array.Empty<IFileComparisonHook>())
        {
        }

        /// <summary>
        /// Full constructor with all dependencies including plugin comparison hooks.
        /// プラグイン比較フックを含むすべての依存関係を受け取る完全コンストラクタ。
        /// </summary>
        public FileDiffService(
            IReadOnlyConfigSettings config,
            IILOutputService ilOutputService,
            DiffExecutionContext executionContext,
            FileDiffResultLists fileDiffResultLists,
            ILoggerService logger,
            IFileComparisonService fileComparisonService,
            IEnumerable<IFileComparisonHook> comparisonHooks)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(ilOutputService);
            ArgumentNullException.ThrowIfNull(executionContext);
            ArgumentNullException.ThrowIfNull(fileComparisonService);
            ArgumentNullException.ThrowIfNull(comparisonHooks);

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
            _comparisonHooks = comparisonHooks.OrderBy(h => h.Order).ToList();
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
            var comparisonStage = "starting comparison";
            try
            {
                // 0) Plugin hooks: allow plugins to override built-in comparison.
                // 0) プラグインフック: プラグインが組み込み比較をオーバーライドできるようにする。
                comparisonStage = "running BeforeCompare hooks";
                var hookResult = await TryRunBeforeCompareHooksAsync(fileRelativePath, cancellationToken);
                if (hookResult != null)
                {
                    if (hookResult.DiffDetailLabel != null)
                    {
                        _fileDiffResultLists.RecordDiffDetail(fileRelativePath,
                            hookResult.AreEqual ? FileDiffResultLists.DiffDetailResult.SHA256Match : FileDiffResultLists.DiffDetailResult.SHA256Mismatch,
                            hookResult.DiffDetailLabel);
                    }
                    comparisonStage = "running AfterCompare hooks";
                    await RunAfterCompareHooksAsync(fileRelativePath, hookResult.AreEqual, cancellationToken);
                    return hookResult.AreEqual;
                }

                // 1) SHA256: exit early when file size and content are identical.
                //    Also capture computed hex hashes to seed the IL cache, avoiding redundant SHA256 recomputation.
                // 1) SHA256: ファイルサイズや内容が完全一致する場合はここで終了。
                //    計算済みハッシュ値を IL キャッシュに事前登録し、SHA256 の二重計算を回避する。
                comparisonStage = "computing SHA256 hashes";
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
                    comparisonStage = "running AfterCompare hooks";
                    await RunAfterCompareHooksAsync(fileRelativePath, true, cancellationToken);
                    return true;
                }

                // 2) IL for .NET assemblies: delegated to a separate service because it involves assembly-specific processing (MVID / configured-string line exclusion).
                //    When SkipIL is true, skip IL comparison and fall through to text/binary comparison.
                // 2) .NET アセンブリなら IL: IL 比較は行除外（MVID や設定文字列）などアセンブリ固有処理を伴うため別サービスに委譲。
                //    SkipIL が true の場合は IL 比較をスキップしてテキスト/バイナリ比較に進む。
                comparisonStage = "detecting .NET executable";
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
                        comparisonStage = "comparing IL";
                        var (areDotNetAssembliesEqual, disassemblerLabel) = await _ilOutputService.DiffDotNetAssembliesAsync(fileRelativePath, _oldFolderAbsolutePath, _newFolderAbsolutePath, _config.ShouldOutputILText, cancellationToken);
                        _fileDiffResultLists.RecordDiffDetail(
                            fileRelativePath,
                            areDotNetAssembliesEqual ? FileDiffResultLists.DiffDetailResult.ILMatch : FileDiffResultLists.DiffDetailResult.ILMismatch,
                            disassemblerLabel);

                        // Best-effort target framework version extraction for display in reports
                        // ベストエフォートでターゲットフレームワークバージョンを抽出しレポート表示用に記録
                        var sdkDisplay = AssemblySdkVersionReader.ReadPairDisplayString(file1AbsolutePath, file2AbsolutePath);
                        if (sdkDisplay != null)
                        {
                            _fileDiffResultLists.FileRelativePathToSdkVersionDictionary[fileRelativePath] = sdkDisplay;
                        }

                        // Best-effort assembly semantic analysis for ILMismatch assemblies
                        if (!areDotNetAssembliesEqual && _config.ShouldIncludeAssemblySemanticChangesInReport)
                        {
                            TryAnalyzeAssemblySemanticChanges(fileRelativePath, file1AbsolutePath, file2AbsolutePath, hash1Hex, hash2Hex);
                            TryClassifyChangeTags(fileRelativePath);
                        }

                        comparisonStage = "running AfterCompare hooks";
                        await RunAfterCompareHooksAsync(fileRelativePath, areDotNetAssembliesEqual, cancellationToken);
                        return areDotNetAssembliesEqual;
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogMessage(AppLogLevel.Error, $"IL diff failed for '{fileRelativePath}' ({ex.GetType().Name}): {ex.Message}", shouldOutputMessageToConsole: true, ex);
                        throw;
                    }
                }

                // 3) Text comparison for text-extension files: sequential when network-optimised, otherwise parallel above a threshold.
                // 3) テキスト拡張子ならテキスト比較: ネットワーク最適化時は逐次、それ以外は閾値に応じて並列比較を選択。
                string fileExtension = Path.GetExtension(file1AbsolutePath);
                if (_config.TextFileExtensions.Any(configuredExtension => string.Equals(configuredExtension, fileExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    comparisonStage = "comparing text";
                    bool areTextFilesEqual = await CompareAsTextAsync(fileRelativePath, file1AbsolutePath, file2AbsolutePath, maxParallel);
                    _fileDiffResultLists.RecordDiffDetail(fileRelativePath, areTextFilesEqual ? FileDiffResultLists.DiffDetailResult.TextMatch : FileDiffResultLists.DiffDetailResult.TextMismatch);

                    // Best-effort dependency change analysis for .deps.json files
                    // .deps.json ファイルに対するベストエフォートの依存関係変更分析
                    if (!areTextFilesEqual && _config.ShouldIncludeDependencyChangesInReport
                        && fileRelativePath.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase))
                    {
                        TryAnalyzeDependencyChanges(fileRelativePath, file1AbsolutePath, file2AbsolutePath);
                        TryClassifyChangeTags(fileRelativePath);
                    }

                    comparisonStage = "running AfterCompare hooks";
                    await RunAfterCompareHooksAsync(fileRelativePath, areTextFilesEqual, cancellationToken);
                    return areTextFilesEqual;
                }

                _fileDiffResultLists.RecordDiffDetail(fileRelativePath, FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
                comparisonStage = "running AfterCompare hooks";
                await RunAfterCompareHooksAsync(fileRelativePath, false, cancellationToken);
                return false;
            }
            // Failures in the main comparison directly affect file-classification correctness,
            // so even expected runtime exceptions are logged as errors and re-thrown to the caller.
            // このメソッドの本比較で起きた失敗はファイル分類の正しさに直結するため、
            // 想定内の実行時例外も error を残して呼び出し元へ再スローする。
            catch (Exception ex) when (ex is DirectoryNotFoundException or IOException
                or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
            {
                LogExpectedFileDiffFailure(fileRelativePath, file1AbsolutePath, file2AbsolutePath, comparisonStage, maxParallel, ex);
                throw;
            }
            catch (Exception ex)
            {
                LogUnexpectedFileDiffFailure(fileRelativePath, file1AbsolutePath, file2AbsolutePath, comparisonStage, maxParallel, ex);
                throw;
            }
        }

        /// <summary>
        /// Runs all registered <see cref="IFileComparisonHook.BeforeCompareAsync"/> in order.
        /// Returns the first non-null result, or <see langword="null"/> to proceed with built-in comparison.
        /// 登録済みの <see cref="IFileComparisonHook.BeforeCompareAsync"/> を順に実行します。
        /// 最初の非null結果を返すか、組み込み比較に進む場合は <see langword="null"/> を返します。
        /// </summary>
        private async Task<FileComparisonHookResult?> TryRunBeforeCompareHooksAsync(
            string fileRelativePath, CancellationToken cancellationToken)
        {
            if (_comparisonHooks.Count == 0) return null;

            var context = new FileComparisonHookContext
            {
                FileRelativePath = fileRelativePath,
                OldFolderAbsolutePath = _oldFolderAbsolutePath,
                NewFolderAbsolutePath = _newFolderAbsolutePath
            };

            foreach (var hook in _comparisonHooks)
            {
                try
                {
                    var result = await hook.BeforeCompareAsync(context, cancellationToken);
                    if (result != null)
                    {
                        _logger.LogMessage(AppLogLevel.Info,
                            $"Plugin hook overrode comparison for '{fileRelativePath}': AreEqual={result.AreEqual}, Label={result.DiffDetailLabel ?? "(none)"}",
                            shouldOutputMessageToConsole: false);
                        return result;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
#pragma warning disable CA1031 // Plugin hooks are best-effort / プラグインフックはベストエフォート
                catch (Exception ex)
                {
                    _logger.LogMessage(AppLogLevel.Warning,
                        BuildHookFailureMessage("BeforeCompare", hook, fileRelativePath, ex),
                        shouldOutputMessageToConsole: false, ex);
                }
#pragma warning restore CA1031
            }

            return null;
        }

        /// <summary>
        /// Runs all registered <see cref="IFileComparisonHook.AfterCompareAsync"/> in order (best-effort).
        /// 登録済みの <see cref="IFileComparisonHook.AfterCompareAsync"/> を順に実行します（ベストエフォート）。
        /// </summary>
        private async Task RunAfterCompareHooksAsync(
            string fileRelativePath, bool areEqual, CancellationToken cancellationToken)
        {
            if (_comparisonHooks.Count == 0) return;

            var context = new FileComparisonHookContext
            {
                FileRelativePath = fileRelativePath,
                OldFolderAbsolutePath = _oldFolderAbsolutePath,
                NewFolderAbsolutePath = _newFolderAbsolutePath
            };

            foreach (var hook in _comparisonHooks)
            {
                try
                {
                    await hook.AfterCompareAsync(context, areEqual, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
#pragma warning disable CA1031 // Plugin hooks are best-effort / プラグインフックはベストエフォート
                catch (Exception ex)
                {
                    _logger.LogMessage(AppLogLevel.Warning,
                        BuildHookFailureMessage("AfterCompare", hook, fileRelativePath, ex),
                        shouldOutputMessageToConsole: false, ex);
                }
#pragma warning restore CA1031
            }
        }

        /// <summary>
        /// Best-effort dependency change analysis for .deps.json files.
        /// Failures are logged but do not affect the comparison result.
        /// .deps.json ファイルに対するベストエフォートの依存関係変更分析。
        /// 失敗してもファイル比較結果には影響しません。
        /// </summary>
        private void TryAnalyzeDependencyChanges(string fileRelativePath, string oldPath, string newPath)
        {
            try
            {
                var summary = DepsJsonAnalyzer.Analyze(oldPath, newPath,
                    ex => LogDependencyAnalysisFailure(fileRelativePath, ex));
                if (summary?.HasChanges == true)
                {
                    _fileDiffResultLists.FileRelativePathToDependencyChanges[fileRelativePath] = summary;
                }
            }
#pragma warning disable CA1031 // ベストエフォート解析のため全例外をキャッチ / Catch-all for best-effort analysis
            catch (Exception ex)
            {
                LogDependencyAnalysisFailure(fileRelativePath, ex);
            }
#pragma warning restore CA1031
        }

        /// <summary>
        /// Best-effort assembly semantic analysis using System.Reflection.Metadata.
        /// Results are cached by (oldHash, newHash) to avoid redundant analysis when the same
        /// assembly content appears at multiple paths.
        /// Failures are logged but do not affect the comparison result.
        /// System.Reflection.Metadata を使用したベストエフォートのアセンブリセマンティック解析。
        /// 同一アセンブリ内容が複数パスに存在する場合の重複解析を回避するため、
        /// (oldHash, newHash) をキーにして結果をキャッシュする。
        /// 失敗してもファイル比較結果には影響しません。
        /// </summary>
        private void TryAnalyzeAssemblySemanticChanges(string fileRelativePath, string oldPath, string newPath,
            string? oldHash, string? newHash)
        {
            try
            {
                AssemblySemanticChangesSummary? summary;

                // Use cached result if both hashes are available and a prior analysis exists
                // 両方のハッシュが利用可能で事前解析結果が存在する場合はキャッシュを使用
                if (oldHash != null && newHash != null)
                {
                    var cacheKey = (oldHash, newHash);
                    if (_semanticAnalysisCache.TryGetValue(cacheKey, out summary))
                    {
                        _logger.LogMessage(AppLogLevel.Info,
                            $"Semantic analysis cache hit for '{fileRelativePath}'.",
                            shouldOutputMessageToConsole: false);
                    }
                    else
                    {
                        summary = AssemblyMethodAnalyzer.Analyze(oldPath, newPath, onError: ex =>
                            _logger.LogMessage(AppLogLevel.Warning,
                                $"Semantic analysis failed for '{fileRelativePath}' ({ex.GetType().Name}): {ex.Message}",
                                shouldOutputMessageToConsole: false, ex));
                        _semanticAnalysisCache.TryAdd(cacheKey, summary);
                    }
                }
                else
                {
                    summary = AssemblyMethodAnalyzer.Analyze(oldPath, newPath, onError: ex =>
                        _logger.LogMessage(AppLogLevel.Warning,
                            $"Semantic analysis failed for '{fileRelativePath}' ({ex.GetType().Name}): {ex.Message}",
                            shouldOutputMessageToConsole: false, ex));
                }

                if (summary?.HasChanges == true)
                {
                    _fileDiffResultLists.FileRelativePathToAssemblySemanticChanges[fileRelativePath] = summary;
                }
            }
#pragma warning disable CA1031 // ベストエフォート解析のため全例外をキャッチ / Catch-all for best-effort analysis
            catch (Exception ex)
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Method-level analysis failed for '{fileRelativePath}' ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: false, ex);
            }
#pragma warning restore CA1031
        }

        /// <summary>
        /// Best-effort change tag classification based on available semantic and dependency data.
        /// セマンティック・依存関係データに基づくベストエフォートの変更タグ分類。
        /// </summary>
        private void TryClassifyChangeTags(string fileRelativePath)
        {
            _fileDiffResultLists.FileRelativePathToAssemblySemanticChanges.TryGetValue(fileRelativePath, out var semanticChanges);
            _fileDiffResultLists.FileRelativePathToDependencyChanges.TryGetValue(fileRelativePath, out var depChanges);

            var tags = ChangeTagClassifier.Classify(semanticChanges, depChanges);
            if (tags.Count > 0)
            {
                _fileDiffResultLists.FileRelativePathToChangeTags[fileRelativePath] = tags;
            }
        }

        private void LogExpectedFileDiffFailure(string fileRelativePath, string file1AbsolutePath, string file2AbsolutePath, string comparisonStage, int maxParallel, Exception exception)
        {
            _logger.LogMessage(
                AppLogLevel.Error,
                BuildFileDiffFailureMessage("An error occurred while diffing", fileRelativePath, file1AbsolutePath, file2AbsolutePath, comparisonStage, maxParallel),
                shouldOutputMessageToConsole: true,
                exception);
        }

        private void LogDependencyAnalysisFailure(string fileRelativePath, Exception exception)
        {
            _logger.LogMessage(AppLogLevel.Warning,
                $"Dependency change analysis failed for '{fileRelativePath}' ({exception.GetType().Name}): {exception.Message}",
                shouldOutputMessageToConsole: false, exception);
        }

        private void LogUnexpectedFileDiffFailure(string fileRelativePath, string file1AbsolutePath, string file2AbsolutePath, string comparisonStage, int maxParallel, Exception exception)
        {
            _logger.LogMessage(
                AppLogLevel.Error,
                BuildFileDiffFailureMessage("An unexpected error occurred while diffing", fileRelativePath, file1AbsolutePath, file2AbsolutePath, comparisonStage, maxParallel),
                shouldOutputMessageToConsole: true,
                exception);
        }

        private static string BuildHookFailureMessage(string phase, IFileComparisonHook hook, string fileRelativePath, Exception exception) =>
            $"Plugin {phase} hook '{hook.GetType().Name}' failed for '{fileRelativePath}' (Order={hook.Order}, {exception.GetType().Name}): {exception.Message}";

        private string BuildFileDiffFailureMessage(string prefix, string fileRelativePath, string file1AbsolutePath, string file2AbsolutePath, string comparisonStage, int maxParallel) =>
            $"{prefix} '{file1AbsolutePath}' and '{file2AbsolutePath}'. RelativePath='{fileRelativePath}', Stage='{comparisonStage}', SkipIL={_config.SkipIL}, MaxParallel={maxParallel}.";

    }
}
