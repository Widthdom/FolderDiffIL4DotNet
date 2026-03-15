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
    /// フォルダ間の差分を比較するサービスクラス
    /// </summary>
    public sealed class FolderDiffService : IFolderDiffService
    {
        /// <summary>
        /// フォルダ比較スピナーのラベル。
        /// </summary>
        private const string SPINNER_LABEL_FOLDER_DIFF = "Diffing folders";

        /// <summary>
        /// ネットワーク最適化ログ (<see cref="FolderDiffService"/>)
        /// </summary>
        private const string LOG_NETWORK_OPTIMIZED_SKIP_IL = $"Network-optimized mode: skip {Constants.LABEL_IL} precompute to reduce network I/O.";

        /// <summary>
        /// フォルダ比較完了ログ。
        /// </summary>
        private const string LOG_FOLDER_DIFF_COMPLETED = "Folder diff completed.";

        /// <summary>
        /// 比較中にファイルが削除された場合の警告ログフォーマット。
        /// </summary>
        private const string LOG_FILE_DELETED_DURING_COMPARISON = "File '{0}' was deleted from the new folder after enumeration; classifying as Removed.";

        /// <summary>
        /// ローカルモードの表示名。
        /// </summary>
        private const string MODE_LOCAL_OPTIMIZED = "Local-optimized";

        /// <summary>
        /// NAS/サーバーモードの表示名。
        /// </summary>
        private const string MODE_SERVER_NAS_OPTIMIZED = "Server/NAS-optimized";

        /// <summary>
        /// キープアライブの出力間隔（秒）。
        /// </summary>
        private const int KEEP_ALIVE_INTERVAL_SECONDS = 5;

        /// <summary>
        /// IL 関連事前計算バッチサイズの既定値。
        /// </summary>
        private const int DEFAULT_IL_PRECOMPUTE_BATCH_SIZE = 2048;

        /// <summary>
        /// 大規模フォルダとして追加ログを出す和集合件数の閾値。
        /// </summary>
        private const int LARGE_DISCOVERY_FILE_COUNT_LOG_THRESHOLD = 10000;
        /// <summary>
        /// アプリケーションの設定情報
        /// </summary>
        private readonly ConfigSettings _config;

        /// <summary>
        /// 進捗状況を報告するためのユーティリティ
        /// </summary>
        private readonly ProgressReportService _progressReporter;

        /// <summary>
        /// 旧バージョン側（比較元）フォルダの絶対パス
        /// </summary>
        private readonly string _oldFolderAbsolutePath;

        /// <summary>
        /// 新バージョン側（比較先）フォルダの絶対パス
        /// </summary>
        private readonly string _newFolderAbsolutePath;

        /// <summary>
        /// IL全文ファイル出力先の絶対パス
        /// </summary>
        private readonly string _ilOutputFolderAbsolutePath;

        /// <summary>
        /// 旧バージョン側（比較元）のIL全文ファイル出力先の絶対パス
        /// </summary>
        private readonly string _ilOldFolderAbsolutePath;

        /// <summary>
        /// 新バージョン側（比較先）のIL全文ファイル出力先の絶対パス
        /// </summary>
        private readonly string _ilNewFolderAbsolutePath;

        /// <summary>
        /// ファイル比較サービス
        /// </summary>
        private readonly IFileDiffService _fileDiffService;

        /// <summary>
        /// 実行時に決定されるネットワーク最適化フラグ（Auto検知 + 手動フラグ を統合）。
        /// </summary>
        private readonly bool _optimizeForNetworkShares;

        /// <summary>
        /// AutoDetectNetworkShares に基づく「旧フォルダ」側の自動検出結果。
        /// true の場合、この実行では旧フォルダパスがネットワーク共有（UNC/NFS/SMB/SSHFS 等）と判定されています。
        /// </summary>
        private readonly bool _detectedNetworkOld;

        /// <summary>
        /// AutoDetectNetworkShares に基づく「新フォルダ」側の自動検出結果。
        /// true の場合、この実行では新フォルダパスがネットワーク共有（UNC/NFS/SMB/SSHFS 等）と判定されています。
        /// </summary>
        private readonly bool _detectedNetworkNew;

        /// <summary>
        /// 比較結果を蓄積する実行単位の状態オブジェクト。
        /// </summary>
        private readonly FileDiffResultLists _fileDiffResultLists;

        /// <summary>
        /// ログ出力サービス。
        /// </summary>
        private readonly ILoggerService _logger;

        /// <summary>
        /// ファイルシステムアクセス。
        /// </summary>
        private readonly IFileSystemService _fileSystem;

        /// <summary>
        /// 探索・並列度決定ポリシー。
        /// </summary>
        private readonly IFolderDiffExecutionStrategy _executionStrategy;

        /// <summary>
        /// コンストラクタ。
        /// 必須パラメータをフィールドに束ね、IL 出力先（old/new サブディレクトリのパス）も初期化します。
        /// </summary>
        /// <param name="config">アプリケーションの設定情報</param>
        /// <param name="progressReporter">進捗状況を報告するためのユーティリティ</param>
        /// <param name="executionContext">実行コンテキスト。</param>
        /// <param name="fileDiffService">ファイル比較サービス。</param>
        /// <param name="fileDiffResultLists">差分結果保持オブジェクト。</param>
        /// <param name="logger">ログ出力サービス。</param>
        /// <exception cref="ArgumentNullException">config または progressReporter または executionContext が null の場合。</exception>
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
        /// 2つのフォルダ（old/new）を再帰的に走査し、無視拡張子を除外した上で
        /// Unchanged / Added / Removed / Modified を分類し、<see cref="FileDiffResultLists"/> に集計します。
        /// 進捗は old と new の相対パスの和集合件数を母数として 1 件ごとに報告します。
        /// </summary>
        /// <remarks>
        /// 主な処理の流れ:
        /// 1) old/new のファイル一覧取得（IgnoredExtensions を除外）
        /// 2) 進捗の母数を old∪new の相対パス件数で算出し、1 件処理ごとに ProgressReporter に通知
        /// 3) old 側を基準に走査し、各相対パスについて以下の順で比較:
        ///    - MD5 ハッシュ一致なら Unchanged
        ///    - .NET 実行可能なら IL を逆アセンブルして比較（再ビルドで変わり得る MVID 行および設定で指定された文字列を含む行を除外）。
        ///    - TextFileExtensions に含まれる拡張子ならテキスト差分で比較
        ///    - いずれも一致しなければ Modified と判定
        ///    new 側に存在しない場合は Removed として記録
        /// 4) old 側に存在せず new 側のみに残ったパスは Added として記録
        ///
        /// 補足:
        /// - ShouldOutputILText が true の場合、各側の IL テキストを
        ///   Reports/&lt;コマンドライン第3引数に指定したレポートのラベル&gt;/IL/old と Reports/&lt;コマンドライン第3引数に指定したレポートのラベル&gt;/IL/new に保存します（比較時に除外した行は出力前に除外）。
        /// - IL 比較では実際に使用したツールとコマンド、バージョン情報をログへ併記します。
        /// - 分類結果と詳細は <see cref="FileDiffResultLists"/> に集計され、後段のレポート生成で利用されます。
        /// - IL プリコンピュートは best-effort で、warning を記録して継続します。一方、列挙・出力先準備・本比較での失敗は error を記録して再スローします。
        /// </remarks>
        /// <returns>非同期操作を表すタスク。</returns>
        /// <exception cref="ArgumentException">出力先パスが無効、または長さ検証に失敗した場合。</exception>
        /// <exception cref="IOException">ファイルの列挙や読み書きに失敗した場合。</exception>
        /// <exception cref="UnauthorizedAccessException">アクセス権限が不足している場合。</exception>
        /// <exception cref="DirectoryNotFoundException">指定フォルダが存在しない場合。</exception>
        /// <exception cref="InvalidOperationException">IL 逆アセンブルツールが見つからない、または実行に失敗した場合。</exception>
        /// <exception cref="NotSupportedException">パス形式または比較対象ファイルの形式がサポートされない場合。</exception>
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
        /// 新旧すべてのファイルを対象に、IL キャッシュ用の事前計算を実行します。
        /// MD5 計算や内部キー生成、.NET アセンブリに対する逆アセンブラ結果キャッシュのウォームアップを行います。
        /// </summary>
        /// <param name="maxParallel">最大並列度</param>
        private async Task PrecomputeIlCachesAsync(int maxParallel)
        {
            // ネットワーク最適化モードでは MD5/IL キャッシュのウォームアップをスキップし、進捗のみゼロリセット。
            if (_optimizeForNetworkShares)
            {
                _logger.LogMessage(AppLogLevel.Info, LOG_NETWORK_OPTIMIZED_SKIP_IL, shouldOutputMessageToConsole: true);
                _progressReporter.ReportProgress(0.0);
                return;
            }
            int precomputeBatchSize = GetEffectiveIlPrecomputeBatchSize();
            // 事前計算が長引いても進捗が止まって見えないよう、定期的に 0% を流すキープアライブを起動。
            using var keepAliveCts = new CancellationTokenSource();
            var keepAliveTask = Task.Run(async () =>
            {
                try
                {
                    while (!keepAliveCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(KEEP_ALIVE_INTERVAL_SECONDS), keepAliveCts.Token);
                        _progressReporter.ReportProgress(0.0);
                    }
                }
                catch (OperationCanceledException)
                {
                    // expected when the keep-alive loop is stopped
                }
            });

            try
            {
                try
                {
                    // プリコンピュート失敗は性能劣化に留まり、後続の本比較で必要な処理は再実行される。
                    // そのため、ここは best-effort として warning を残しつつ継続する。
                    // MD5 ハッシュや内部キー計算（ILCache.PrecomputeAsync）と、逆アセンブラのキャッシュウォームを実行。
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
                // プリフェッチ完了後に進捗を更新しておく。
                _progressReporter.ReportProgress(0.0);
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
        /// 逐次（単一スレッド）で差分判定を行います。
        /// old 側を 1 件ずつ処理し、Unchanged / Modified / Removed を分類して進捗を更新します。
        /// </summary>
        /// <param name="remainingNewFilesAbsolutePathHashSet">未処理の new 側ファイル集合（相対パス一致時に削除されます）</param>
        /// <param name="totalFilesRelativePathCount">進捗計算の母数</param>
        /// <param name="processedFileCountSoFar">これまでの処理件数</param>
        /// <returns>処理済件数</returns>
        private async Task<int> DetermineDiffsSequentiallyAsync(HashSet<string> remainingNewFilesAbsolutePathHashSet, int totalFilesRelativePathCount, int processedFileCountSoFar)
        {
            foreach (var oldFileAbsolutePath in _fileDiffResultLists.OldFilesAbsolutePath)
            {
                var fileRelativePath = Path.GetRelativePath(_oldFolderAbsolutePath, oldFileAbsolutePath);
                var newFileAbsolutePath = Path.Combine(_newFolderAbsolutePath, fileRelativePath);

                if (remainingNewFilesAbsolutePathHashSet.Contains(newFileAbsolutePath))
                {
                    remainingNewFilesAbsolutePathHashSet.Remove(newFileAbsolutePath);
                    RecordNewFileTimestampOlderThanOldWarningIfNeeded(fileRelativePath, oldFileAbsolutePath, newFileAbsolutePath);
                    bool areEqual;
                    try
                    {
                        areEqual = await _fileDiffService.FilesAreEqualAsync(fileRelativePath);
                    }
                    catch (FileNotFoundException)
                    {
                        // 列挙後に new 側ファイルが削除された場合は Removed として扱い、警告を記録して継続する。
                        _logger.LogMessage(AppLogLevel.Warning, string.Format(System.Globalization.CultureInfo.InvariantCulture, LOG_FILE_DELETED_DURING_COMPARISON, fileRelativePath), shouldOutputMessageToConsole: true);
                        _fileDiffResultLists.AddRemovedFileAbsolutePath(oldFileAbsolutePath);
                        processedFileCountSoFar++;
                        _progressReporter.ReportProgress((double)processedFileCountSoFar * 100.0 / totalFilesRelativePathCount);
                        continue;
                    }
                    if (areEqual)
                    {
                        // - Unchanged -
                        _fileDiffResultLists.AddUnchangedFileRelativePath(fileRelativePath);
                    }
                    else
                    {
                        // - Modified -
                        _fileDiffResultLists.AddModifiedFileRelativePath(fileRelativePath);
                    }
                }
                else
                {
                    // - Removed -
                    _fileDiffResultLists.AddRemovedFileAbsolutePath(oldFileAbsolutePath);
                }
                processedFileCountSoFar++;
                _progressReporter.ReportProgress((double)processedFileCountSoFar * 100.0 / totalFilesRelativePathCount);
            }
            return processedFileCountSoFar;
        }

        /// <summary>
        /// 並列に差分判定を行います。new 側の未処理集合へのアクセスのみ低粒度ロックで保護し、
        /// 分類結果の追加はスレッドセーフなコレクション API で記録します。
        /// </summary>
        /// <param name="remainingNewFilesAbsolutePathHashSet">未処理の new 側ファイル集合（相対パス一致時に削除されます）</param>
        /// <param name="totalFilesRelativePathCount">進捗計算の母数</param>
        /// <param name="processedFileCountSoFar">これまでの処理件数</param>
        /// <param name="maxParallel">最大並列度</param>
        /// <returns>処理済件数</returns>
        private async Task<int> DetermineDiffsInParallelAsync(HashSet<string> remainingNewFilesAbsolutePathHashSet, int totalFilesRelativePathCount, int processedFileCountSoFar, int maxParallel)
        {
            // lockRemaining: new 側の未処理集合（remainingNewFilesAbsolutePathHashSet）へのアクセスを直列化するためのロック。
            //   - 役割: 「存在確認（Contains）→ 削除（Remove）」をアトミックに行い、
            //           同じ相対パスの二重比較（重複処理）とレースコンディションによる不整合を防ぐ。
            //   - 粒度: メンバーシップ確認と削除の最小限の区間だけロックし、計算の重い処理はロック外で行う。
            var lockRemaining = new object();
            // 処理済件数をインクリメントするための変数
            int processedFileCount = processedFileCountSoFar;

            await Parallel.ForEachAsync(_fileDiffResultLists.OldFilesAbsolutePath, new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, async (oldFileAbsolutePath, cancellationToken) =>
            {
                var fileRelativePath = Path.GetRelativePath(_oldFolderAbsolutePath, oldFileAbsolutePath);
                var newFileAbsolutePath = Path.Combine(_newFolderAbsolutePath, fileRelativePath);
                bool hasMatchingFileInNewFilesAbsolutePathHashSet;
                // 未処理集合の存在確認と削除は「確認→削除」を不可分にするため同一ロックで保護
                lock (lockRemaining)
                {
                    // new 側に同じ相対パスがあるかチェックし、あれば集合から除去（重複比較を防止）
                    hasMatchingFileInNewFilesAbsolutePathHashSet = remainingNewFilesAbsolutePathHashSet.Contains(newFileAbsolutePath);
                    if (hasMatchingFileInNewFilesAbsolutePathHashSet)
                    {
                        remainingNewFilesAbsolutePathHashSet.Remove(newFileAbsolutePath);
                    }
                }
                if (hasMatchingFileInNewFilesAbsolutePathHashSet)
                {
                    RecordNewFileTimestampOlderThanOldWarningIfNeeded(fileRelativePath, oldFileAbsolutePath, newFileAbsolutePath);
                    // 比較本体はロック外で実行（I/O / 計算を含むため）
                    bool areFilesEqual;
                    try
                    {
                        areFilesEqual = await _fileDiffService.FilesAreEqualAsync(fileRelativePath, maxParallel);
                    }
                    catch (FileNotFoundException)
                    {
                        // 列挙後に new 側ファイルが削除された場合は Removed として扱い、警告を記録して継続する。
                        _logger.LogMessage(AppLogLevel.Warning, string.Format(System.Globalization.CultureInfo.InvariantCulture, LOG_FILE_DELETED_DURING_COMPARISON, fileRelativePath), shouldOutputMessageToConsole: true);
                        _fileDiffResultLists.AddRemovedFileAbsolutePath(oldFileAbsolutePath);
                        var doneOnDelete = Interlocked.Increment(ref processedFileCount);
                        _progressReporter.ReportProgress((double)doneOnDelete * 100.0 / totalFilesRelativePathCount);
                        return;
                    }
                    if (areFilesEqual)
                    {
                        // - Unchanged -
                        _fileDiffResultLists.AddUnchangedFileRelativePath(fileRelativePath);
                    }
                    else
                    {
                        // - Modified -
                        _fileDiffResultLists.AddModifiedFileRelativePath(fileRelativePath);
                    }
                }
                else
                {
                    // new 側に無いので Removed 判定
                    // - Removed -
                    _fileDiffResultLists.AddRemovedFileAbsolutePath(oldFileAbsolutePath);
                }
                // 処理済件数をインクリメントし進捗を報告
                var done = Interlocked.Increment(ref processedFileCount);
                _progressReporter.ReportProgress((double)done * 100.0 / totalFilesRelativePathCount);
            });

            return processedFileCount;
        }

        /// <summary>
        /// 実行モード（ローカル最適化 / サーバー・NAS 最適化）とその判定理由をログに出力します。
        /// </summary>
        private void LogExecutionMode()
        {
            var mode = _optimizeForNetworkShares ? MODE_SERVER_NAS_OPTIMIZED : MODE_LOCAL_OPTIMIZED;
            var reason = $"manual={_config.OptimizeForNetworkShares}, auto={_config.AutoDetectNetworkShares}, oldIsNetwork={_detectedNetworkOld}, newIsNetwork={_detectedNetworkNew}";
            _logger.LogMessage(AppLogLevel.Info, $"Execution mode: {mode} ({reason})", shouldOutputMessageToConsole: true);
        }

        /// <summary>
        /// 前回実行の分類結果をすべてクリアします。
        /// </summary>
        private void ClearResultCollections()
        {
            _fileDiffResultLists.ResetAll();
        }

        /// <summary>
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
        /// IL 出力先ディレクトリを必要に応じて作成します。
        /// <see cref="ConfigSettings.ShouldOutputILText"/> が true の場合は old/new サブディレクトリも作成します。
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
        /// old/new の両方に存在するファイルについて、new 側の更新日時が old 側より古い場合に警告情報を記録します。
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
        /// IL 関連の事前計算に使う実効バッチサイズを返します。
        /// </summary>
        /// <returns>1 以上のバッチサイズ。</returns>
        private int GetEffectiveIlPrecomputeBatchSize()
            => _config.ILPrecomputeBatchSize > 0
                ? _config.ILPrecomputeBatchSize
                : DEFAULT_IL_PRECOMPUTE_BATCH_SIZE;

        /// <summary>
        /// old/new の重複を除いたファイル群を、指定サイズごとのバッチに分けて列挙します。
        /// </summary>
        /// <param name="batchSize">1 バッチあたりの最大件数。</param>
        /// <returns>重複排除済みのファイル絶対パスバッチ列挙。</returns>
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
