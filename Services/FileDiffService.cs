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
        #region constants
        /// <summary>
        /// IL diff 失敗ログ
        /// </summary>
        private const string LOG_IL_DIFF_FAILED = Constants.LABEL_IL + " diff failed for '{0}'.";

        /// <summary>
        /// テキスト並列比較失敗時のフォールバックログ
        /// </summary>
        private const string LOG_TEXT_DIFF_PARALLEL_FALLBACK = "Parallel text diff failed for '{0}'. Falling back to sequential text diff.";

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
        #endregion

        #region private read only member variables
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
        #endregion

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
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(ilOutputService);
            ArgumentNullException.ThrowIfNull(executionContext);

            _config = config;
            _ilOutputService = ilOutputService;
            _oldFolderAbsolutePath = executionContext.OldFolderAbsolutePath;
            _newFolderAbsolutePath = executionContext.NewFolderAbsolutePath;
            _optimizeForNetworkShares = executionContext.OptimizeForNetworkShares;
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            _fileDiffResultLists = fileDiffResultLists;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <summary>
        /// IL キャッシュ関連の事前計算を実行します（実体は <see cref="ILOutputService"/> に委譲）。
        /// </summary>
        /// <exception cref="Exception">IL キャッシュの事前計算中に発生した例外。</exception>
        public Task PrecomputeAsync(System.Collections.Generic.IEnumerable<string> filesAbsolutePath, int maxParallel)
            => _ilOutputService.PrecomputeAsync(filesAbsolutePath, maxParallel);

        /// <summary>
        /// 2つのファイルが等しいかを判定し、MD5→IL→テキストの順で比較を試みる統合メソッド。
        /// 判定結果は <see cref="FileDiffResultLists"/> に記録され、ネットワーク最適化や拡張子設定にも追従します。
        /// </summary>
        /// <param name="fileRelativePath">比較対象ファイルのフォルダ基準相対パス。</param>
        /// <param name="maxParallel">テキスト比較を並列実行する際の最大並列数（1 以上）。</param>
        /// <exception cref="Exception">比較中に予期しないエラーが発生した場合。</exception>
        public async Task<bool> FilesAreEqualAsync(string fileRelativePath, int maxParallel = 1)
        {
            string file1AbsolutePath = Path.Combine(_oldFolderAbsolutePath, fileRelativePath);
            string file2AbsolutePath = Path.Combine(_newFolderAbsolutePath, fileRelativePath);
            try
            {
                // 1) MD5: ファイルサイズや内容が完全一致する場合はここで終了。
                if (await FileComparer.DiffFilesByHashAsync(file1AbsolutePath, file2AbsolutePath))
                {
                    _fileDiffResultLists.RecordDiffDetail(fileRelativePath, FileDiffResultLists.DiffDetailResult.MD5Match);
                    return true;
                }

                // 2) .NET アセンブリなら IL: IL 比較は行除外（MVID や設定文字列）などアセンブリ固有処理を伴うため別サービスに委譲。
                if (DotNetDetector.IsDotNetExecutable(file1AbsolutePath))
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
                        _logger.LogMessage(AppLogLevel.Error, string.Format(LOG_IL_DIFF_FAILED, fileRelativePath), shouldOutputMessageToConsole: true, ex);
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
                            areTextFilesEqual = await FileComparer.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                        }
                        else
                        {
                            var file1Info = new FileInfo(file1AbsolutePath);
                            if (file1Info.Length >= textDiffParallelThresholdBytes)
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
                                areTextFilesEqual = await FileComparer.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogMessage(AppLogLevel.Warning, string.Format(LOG_TEXT_DIFF_PARALLEL_FALLBACK, fileRelativePath), shouldOutputMessageToConsole: true, ex);
                        areTextFilesEqual = await FileComparer.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                    }
                    _fileDiffResultLists.RecordDiffDetail(fileRelativePath, areTextFilesEqual ? FileDiffResultLists.DiffDetailResult.TextMatch : FileDiffResultLists.DiffDetailResult.TextMismatch);
                    return areTextFilesEqual;
                }

                _fileDiffResultLists.RecordDiffDetail(fileRelativePath, FileDiffResultLists.DiffDetailResult.MD5Mismatch);
                return false;
            }
            catch (Exception)
            {
                // 各比較手段での失敗は最終的にここでログ化し、呼び出し元へ再スロー。
                _logger.LogMessage(AppLogLevel.Error, string.Format(Constants.ERROR_DIFFING, file1AbsolutePath, file2AbsolutePath), shouldOutputMessageToConsole: true);
                throw;
            }
        }

        /// <summary>
        /// サイズが閾値を超えるテキストファイルに対して高速化を目的に並列チャンク比較を行う実験的メソッド。
        /// 完全一致判定のみを行い、差分箇所の特定は行いません。
        /// なお、本メソッドはエラーや引数不正が発生した場合でも例外を呼出し側へ送出せず、false を返します。
        /// </summary>
        /// <param name="file1AbsolutePath">ファイル1の絶対パス</param>
        /// <param name="file2AbsolutePath">ファイル2の絶対パス</param>
        /// <param name="largeFileSizeThresholdBytes">並列化閾値（バイト）。これ未満は逐次比較。</param>
        /// <param name="chunkSizeBytes">チャンクサイズ（バイト）。</param>
        /// <param name="maxParallel">最大並列度</param>
        /// <returns>一致すれば true。エラーや引数不正時は false。</returns>
        private static async Task<bool> DiffTextFilesParallelAsync(string file1AbsolutePath, string file2AbsolutePath, long largeFileSizeThresholdBytes, int chunkSizeBytes, int maxParallel)
        {
            try
            {
                var file1Info = new FileInfo(file1AbsolutePath);
                var file2Info = new FileInfo(file2AbsolutePath);
                // どちらかが存在しない、またはサイズが異なる場合は比較するまでもなく不一致。
                if (!file1Info.Exists || !file2Info.Exists)
                {
                    return false;
                }
                if (file1Info.Length != file2Info.Length)
                {
                    return false;
                }
                // 小さいファイルは既存の逐次比較に委譲して余計なオーバーヘッドを避ける。
                if (file1Info.Length < largeFileSizeThresholdBytes)
                {
                    return await FileComparer.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                }
                if (maxParallel <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxParallel), maxParallel, Constants.ERROR_MAX_PARALLEL);
                }

                // 大きなファイルは固定サイズのチャンクに分割し、読み取り→比較を並列実行する。
                int chunkCount = (int)((file1Info.Length + chunkSizeBytes - 1) / chunkSizeBytes);
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
                    int read1, read2;
                    using (var file1Stream = new FileStream(file1AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var file2Stream = new FileStream(file2AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        file1Stream.Seek((long)index * chunkSizeBytes, SeekOrigin.Begin);
                        file2Stream.Seek((long)index * chunkSizeBytes, SeekOrigin.Begin);
                        read1 = await file1Stream.ReadAsync(buffer1.AsMemory(0, chunkSizeBytes), cancellationToken);
                        read2 = await file2Stream.ReadAsync(buffer2.AsMemory(0, chunkSizeBytes), cancellationToken);
                    }
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
            catch
            {
                return false;
            }
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
