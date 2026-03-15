using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// 個々のファイル比較（MD5/IL/テキスト）と、その前段となる事前計算の入口を提供するサービス。
    /// </summary>
    public sealed class FileDiffService : IFileDiffService
    {
        /// <summary>
        /// 1 KiB (2^10) を表すバイト数。
        /// </summary>
        private const int BYTES_PER_KILOBYTE = 1024;

        /// <summary>
        /// テキスト差分の高速化を検討するサイズ閾値（バイト）の既定値。
        /// </summary>
        private const int DEFAULT_TEXT_DIFF_PARALLEL_THRESHOLD_BYTES = 512 * BYTES_PER_KILOBYTE;

        /// <summary>
        /// テキスト差分比較時のチャンクサイズ（バイト）の既定値。
        /// </summary>
        private const int DEFAULT_TEXT_DIFF_CHUNK_SIZE_BYTES = 64 * BYTES_PER_KILOBYTE;
        /// <summary>
        /// アプリケーションの設定情報
        /// </summary>
        private readonly ConfigSettings _config;

        /// <summary>
        /// IL 出力サービス
        /// </summary>
        private readonly IILOutputService _ilOutputService;

        /// <summary>
        /// 旧バージョン側（比較元）のIL全文ファイル出力先の絶対パス
        /// </summary>
        private readonly string _oldFolderAbsolutePath;

        /// <summary>
        /// 新バージョン側（比較先）のIL全文ファイル出力先の絶対パス
        /// </summary>
        private readonly string _newFolderAbsolutePath;

        /// <summary>
        /// ネットワーク共有向け最適化を行うか（実行時決定の統合フラグ）。
        /// </summary>
        private readonly bool _optimizeForNetworkShares;

        /// <summary>
        /// 比較結果を蓄積する実行単位の状態オブジェクト。
        /// </summary>
        private readonly FileDiffResultLists _fileDiffResultLists;

        /// <summary>
        /// ログ出力サービス。
        /// </summary>
        private readonly ILoggerService _logger;

        /// <summary>
        /// ファイル比較・判定 I/O。
        /// </summary>
        private readonly IFileComparisonService _fileComparisonService;

        /// <summary>
        /// 依存関係を受け取り初期化します。
        /// </summary>
        /// <param name="config">アプリケーション設定。</param>
        /// <param name="ilOutputService">IL 比較・出力サービス。</param>
        /// <param name="executionContext">実行コンテキスト。</param>
        /// <param name="fileDiffResultLists">差分結果保持オブジェクト。</param>
        /// <param name="logger">ログ出力サービス。</param>
        public FileDiffService(
            ConfigSettings config,
            IILOutputService ilOutputService,
            DiffExecutionContext executionContext,
            FileDiffResultLists fileDiffResultLists,
            ILoggerService logger)
            : this(config, ilOutputService, executionContext, fileDiffResultLists, logger, new FileComparisonService())
        {
        }

        /// <summary>
        /// テスト向けに比較 I/O を差し替え可能なコンストラクタ。
        /// </summary>
        public FileDiffService(
            ConfigSettings config,
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
        /// IL キャッシュ関連の事前計算を実行します（実体は <see cref="ILOutputService"/> に委譲）。
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="filesAbsolutePath"/> が null の場合。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxParallel"/> が 0 以下の場合。</exception>
        public Task PrecomputeAsync(System.Collections.Generic.IEnumerable<string> filesAbsolutePath, int maxParallel)
        {
            ArgumentNullException.ThrowIfNull(filesAbsolutePath);
            return _ilOutputService.PrecomputeAsync(filesAbsolutePath, maxParallel);
        }

        /// <summary>
        /// 2つのファイルが等しいかを判定し、MD5→IL→テキストの順で比較を試みる統合メソッド。
        /// 判定結果は <see cref="FileDiffResultLists"/> に記録され、ネットワーク最適化や拡張子設定にも追従します。
        /// </summary>
        /// <param name="fileRelativePath">比較対象ファイルのフォルダ基準相対パス。</param>
        /// <param name="maxParallel">テキスト比較を並列実行する際の最大並列数（1 以上）。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxParallel"/> が 0 以下で、並列テキスト比較が選択された場合。</exception>
        /// <exception cref="DirectoryNotFoundException">比較途中で親ディレクトリが見つからなくなった場合。</exception>
        /// <exception cref="IOException">ハッシュ比較、IL 比較、またはテキスト比較中の I/O に失敗した場合。</exception>
        /// <exception cref="UnauthorizedAccessException">比較対象ファイルへのアクセス権が不足している場合。</exception>
        /// <exception cref="NotSupportedException">パス形式または比較対象ファイルの形式がサポートされない場合。</exception>
        /// <exception cref="InvalidOperationException">IL 逆アセンブルツールが見つからない、または実行に失敗した場合。</exception>
        public async Task<bool> FilesAreEqualAsync(string fileRelativePath, int maxParallel = 1)
        {
            string file1AbsolutePath = Path.Combine(_oldFolderAbsolutePath, fileRelativePath);
            string file2AbsolutePath = Path.Combine(_newFolderAbsolutePath, fileRelativePath);
            try
            {
                // 1) MD5: ファイルサイズや内容が完全一致する場合はここで終了。
                if (await _fileComparisonService.DiffFilesByHashAsync(file1AbsolutePath, file2AbsolutePath))
                {
                    _fileDiffResultLists.RecordDiffDetail(fileRelativePath, FileDiffResultLists.DiffDetailResult.MD5Match);
                    return true;
                }

                // 2) .NET アセンブリなら IL: IL 比較は行除外（MVID や設定文字列）などアセンブリ固有処理を伴うため別サービスに委譲。
                var dotNetDetectionResult = _fileComparisonService.DetectDotNetExecutable(file1AbsolutePath);
                if (dotNetDetectionResult.IsFailure)
                {
                    _logger.LogMessage(
                        AppLogLevel.Warning,
                        $"Failed to detect whether '{fileRelativePath}' is a .NET executable. Skipping IL diff.",
                        shouldOutputMessageToConsole: true,
                        dotNetDetectionResult.Exception);
                }

                if (dotNetDetectionResult.IsDotNetExecutable)
                {
                    try
                    {
                        var (areDotNetAssembliesEqual, disassemblerLabel) = await _ilOutputService.DiffDotNetAssembliesAsync(fileRelativePath, _oldFolderAbsolutePath, _newFolderAbsolutePath, _config.ShouldOutputILText);
                        _fileDiffResultLists.RecordDiffDetail(
                            fileRelativePath,
                            areDotNetAssembliesEqual ? FileDiffResultLists.DiffDetailResult.ILMatch : FileDiffResultLists.DiffDetailResult.ILMismatch,
                            disassemblerLabel);
                        return areDotNetAssembliesEqual;
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogMessage(AppLogLevel.Error, $"IL diff failed for '{fileRelativePath}'.", shouldOutputMessageToConsole: true, ex);
                        throw;
                    }
                }

                // 3) テキスト拡張子ならテキスト比較: ネットワーク最適化時は逐次、それ以外は閾値に応じて並列比較を選択。
                string fileExtension = Path.GetExtension(file1AbsolutePath);
                if (_config.TextFileExtensions.Any(configuredExtension => string.Equals(configuredExtension, fileExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    int textDiffParallelThresholdBytes = GetEffectiveBytesFromConfiguredKilobytes(
                        configuredKilobytes: _config.TextDiffParallelThresholdKilobytes,
                        defaultBytes: DEFAULT_TEXT_DIFF_PARALLEL_THRESHOLD_BYTES);
                    int textDiffChunkSizeBytes = GetEffectiveBytesFromConfiguredKilobytes(
                        configuredKilobytes: _config.TextDiffChunkSizeKilobytes,
                        defaultBytes: DEFAULT_TEXT_DIFF_CHUNK_SIZE_BYTES);
                    bool areTextFilesEqual;
                    try
                    {
                        if (_optimizeForNetworkShares)
                        {
                            // ネットワーク共有最適化時は、チャンク毎のOpen/Closeを伴う並列比較は避け、逐次読みで比較
                            areTextFilesEqual = await _fileComparisonService.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                        }
                        else
                        {
                            long file1Length = _fileComparisonService.GetFileLength(file1AbsolutePath);
                            if (file1Length >= textDiffParallelThresholdBytes)
                            {
                                // 大きいファイルは並列チャンク比較で高速化
                                areTextFilesEqual = await DiffTextFilesParallelAsync(
                                    file1AbsolutePath,
                                    file2AbsolutePath,
                                    largeFileSizeThresholdBytes: textDiffParallelThresholdBytes,
                                    chunkSizeBytes: textDiffChunkSizeBytes,
                                    maxParallel: maxParallel);
                            }
                            else
                            {
                                // 小さいファイルは逐次行比較（並列化のオーバーヘッドを避ける）
                                areTextFilesEqual = await _fileComparisonService.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                            }
                        }
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        _logger.LogMessage(AppLogLevel.Warning, $"Parallel text diff failed for '{fileRelativePath}'. Falling back to sequential text diff.", shouldOutputMessageToConsole: true, ex);
                        areTextFilesEqual = await _fileComparisonService.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                    }
                    catch (IOException ex)
                    {
                        _logger.LogMessage(AppLogLevel.Warning, $"Parallel text diff failed for '{fileRelativePath}'. Falling back to sequential text diff.", shouldOutputMessageToConsole: true, ex);
                        areTextFilesEqual = await _fileComparisonService.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogMessage(AppLogLevel.Warning, $"Parallel text diff failed for '{fileRelativePath}'. Falling back to sequential text diff.", shouldOutputMessageToConsole: true, ex);
                        areTextFilesEqual = await _fileComparisonService.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                    }
                    catch (NotSupportedException ex)
                    {
                        _logger.LogMessage(AppLogLevel.Warning, $"Parallel text diff failed for '{fileRelativePath}'. Falling back to sequential text diff.", shouldOutputMessageToConsole: true, ex);
                        areTextFilesEqual = await _fileComparisonService.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                    }
                    _fileDiffResultLists.RecordDiffDetail(fileRelativePath, areTextFilesEqual ? FileDiffResultLists.DiffDetailResult.TextMatch : FileDiffResultLists.DiffDetailResult.TextMismatch);
                    return areTextFilesEqual;
                }

                _fileDiffResultLists.RecordDiffDetail(fileRelativePath, FileDiffResultLists.DiffDetailResult.MD5Mismatch);
                return false;
            }
            // このメソッドの本比較で起きた失敗はファイル分類の正しさに直結するため、
            // 想定内の実行時例外も error を残して呼び出し元へ再スローする。
            catch (DirectoryNotFoundException ex)
            {
                LogExpectedFileDiffFailure(file1AbsolutePath, file2AbsolutePath, ex);
                throw;
            }
            catch (IOException ex)
            {
                LogExpectedFileDiffFailure(file1AbsolutePath, file2AbsolutePath, ex);
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogExpectedFileDiffFailure(file1AbsolutePath, file2AbsolutePath, ex);
                throw;
            }
            catch (InvalidOperationException ex)
            {
                LogExpectedFileDiffFailure(file1AbsolutePath, file2AbsolutePath, ex);
                throw;
            }
            catch (NotSupportedException ex)
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
        /// サイズが閾値を超えるテキストファイルに対して高速化を目的に並列チャンク比較を行う実験的メソッド。
        /// 完全一致判定のみを行い、差分箇所の特定は行いません。
        /// エラーや引数不正は呼び出し側へ送出し、呼び出し側で逐次比較へのフォールバック可否を判断します。
        /// </summary>
        /// <param name="file1AbsolutePath">ファイル1の絶対パス</param>
        /// <param name="file2AbsolutePath">ファイル2の絶対パス</param>
        /// <param name="largeFileSizeThresholdBytes">並列化閾値（バイト）。これ未満は逐次比較。</param>
        /// <param name="chunkSizeBytes">チャンクサイズ（バイト）。</param>
        /// <param name="maxParallel">最大並列度</param>
        /// <returns>一致すれば true。不一致なら false。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxParallel"/> が 0 以下の場合。</exception>
        /// <exception cref="IOException">チャンク読み取りまたはファイル長取得に失敗した場合。</exception>
        /// <exception cref="UnauthorizedAccessException">比較対象ファイルへのアクセス権が不足している場合。</exception>
        /// <exception cref="NotSupportedException">パス形式または比較対象ファイルの形式がサポートされない場合。</exception>
        private async Task<bool> DiffTextFilesParallelAsync(string file1AbsolutePath, string file2AbsolutePath, long largeFileSizeThresholdBytes, int chunkSizeBytes, int maxParallel)
        {
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
            // 小さいファイルは既存の逐次比較に委譲して余計なオーバーヘッドを避ける。
            if (file1Length < largeFileSizeThresholdBytes)
            {
                return await _fileComparisonService.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
            }
            if (maxParallel <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxParallel), maxParallel, Constants.ERROR_MAX_PARALLEL);
            }

            // 大きなファイルは固定サイズのチャンクに分割し、読み取り→比較を並列実行する。
            int chunkCount = (int)((file1Length + chunkSizeBytes - 1) / chunkSizeBytes);
            var differences = 0;
            await Parallel.ForEachAsync(Enumerable.Range(0, chunkCount), new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, async (index, cancellationToken) =>
            {
                // 既に差分が見つかっていれば以降のチャンクは読む必要がない。
                if (Volatile.Read(ref differences) != 0)
                {
                    return;
                }
                var buffer1 = new byte[chunkSizeBytes];
                var buffer2 = new byte[chunkSizeBytes];
                int read1 = await _fileComparisonService.ReadChunkAsync(file1AbsolutePath, (long)index * chunkSizeBytes, buffer1.AsMemory(0, chunkSizeBytes), cancellationToken);
                int read2 = await _fileComparisonService.ReadChunkAsync(file2AbsolutePath, (long)index * chunkSizeBytes, buffer2.AsMemory(0, chunkSizeBytes), cancellationToken);
                // 同じオフセットのチャンクでも読み取りバイト数が異なれば即時不一致。
                if (read1 != read2)
                {
                    Interlocked.Exchange(ref differences, 1);
                    return;
                }
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
            // 差分フラグが立っていなければ完全一致。
            return differences == 0;
        }

        /// <summary>
        /// KiB 指定の設定値をバイトへ変換します。設定値が 0 以下または変換でオーバーフローする場合は既定値を返します。
        /// </summary>
        private static int GetEffectiveBytesFromConfiguredKilobytes(int configuredKilobytes, int defaultBytes)
        {
            if (configuredKilobytes <= 0)
            {
                return defaultBytes;
            }

            long bytes = (long)configuredKilobytes * BYTES_PER_KILOBYTE;
            if (bytes > int.MaxValue)
            {
                return defaultBytes;
            }

            return (int)bytes;
        }
    }
}
