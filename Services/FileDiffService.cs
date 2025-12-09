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
    public sealed class FileDiffService
    {
        #region constants
        /// <summary>
        /// IL diff 失敗ログ
        /// </summary>
        private const string LOG_IL_DIFF_FAILED = Constants.LABEL_IL + " diff failed for '{0}'.";

        /// <summary>
        /// 1 KiB (2^10) を表すバイト数。
        /// </summary>
        private const int BYTES_PER_KILOBYTE = 1024;

        /// <summary>
        /// テキスト差分の高速化を検討するサイズ閾値（バイト）
        /// </summary>
        private const int TEXT_DIFF_PARALLEL_THRESHOLD_BYTES = 512 * BYTES_PER_KILOBYTE;

        /// <summary>
        /// テキスト差分比較時のチャンクサイズ（バイト）
        /// </summary>
        private const int TEXT_DIFF_CHUNK_SIZE_BYTES = 64 * BYTES_PER_KILOBYTE;
        #endregion

        /// <summary>
        /// アプリケーションの設定情報
        /// </summary>
        private readonly ConfigSettings _config;

        /// <summary>
        /// IL 出力サービス
        /// </summary>
        private readonly ILOutputService _ilOutputService;

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
        /// 依存関係を受け取り初期化します。
        /// </summary>
        public FileDiffService(ConfigSettings config, ILOutputService ilOutputService, string oldFolderAbsolutePath, string newFolderAbsolutePath, bool optimizeForNetworkShares)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _ilOutputService = ilOutputService ?? throw new ArgumentNullException(nameof(ilOutputService));
            _oldFolderAbsolutePath = oldFolderAbsolutePath ?? throw new ArgumentNullException(nameof(oldFolderAbsolutePath));
            _newFolderAbsolutePath = newFolderAbsolutePath ?? throw new ArgumentNullException(nameof(newFolderAbsolutePath));
            _optimizeForNetworkShares = optimizeForNetworkShares;
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
                if (await Utility.DiffFilesByHashAsync(file1AbsolutePath, file2AbsolutePath))
                {
                    FileDiffResultLists.RecordDiffDetail(fileRelativePath, FileDiffResultLists.DiffDetailResult.MD5Match);
                    return true;
                }

                // 2) .NET アセンブリなら IL: IL 比較は MVID 行の除外などアセンブリ固有処理を伴うため別サービスに委譲。
                if (Utility.IsDotNetExecutable(file1AbsolutePath))
                {
                    try
                    {
                        bool areDotNetAssembliesEqual = await _ilOutputService.DiffDotNetAssembliesAsync(fileRelativePath, _oldFolderAbsolutePath, _newFolderAbsolutePath, _config.ShouldOutputILText);
                        FileDiffResultLists.RecordDiffDetail(fileRelativePath, areDotNetAssembliesEqual ? FileDiffResultLists.DiffDetailResult.ILMatch : FileDiffResultLists.DiffDetailResult.ILMismatch);
                        return areDotNetAssembliesEqual;
                    }
                    catch (InvalidOperationException ex)
                    {
                        LoggerService.LogMessage(LoggerService.LogLevel.Error, string.Format(LOG_IL_DIFF_FAILED, fileRelativePath), shouldOutputMessageToConsole: true, ex);
                        throw;
                    }
                }

                // 3) テキスト拡張子ならテキスト比較: ネットワーク最適化時は逐次、それ以外は閾値に応じて並列比較を選択。
                if (_config.TextFileExtensions.Contains(Path.GetExtension(file1AbsolutePath).ToLower()))
                {
                    bool areTextFilesEqual;
                    try
                    {
                        if (_optimizeForNetworkShares)
                        {
                            // ネットワーク共有最適化時は、チャンク毎のOpen/Closeを伴う並列比較は避け、逐次読みで比較
                            areTextFilesEqual = await Utility.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                        }
                        else
                        {
                            var file1Info = new FileInfo(file1AbsolutePath);
                            areTextFilesEqual = await DiffTextFilesParallelAsync(file1AbsolutePath, file2AbsolutePath, largeFileSizeThresholdBytes: TEXT_DIFF_PARALLEL_THRESHOLD_BYTES, maxParallel);
                            if (file1Info.Length < TEXT_DIFF_PARALLEL_THRESHOLD_BYTES)
                            {
                                areTextFilesEqual = await Utility.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                            }
                        }
                    }
                    catch
                    {
                        areTextFilesEqual = await Utility.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                    }
                    FileDiffResultLists.RecordDiffDetail(fileRelativePath, areTextFilesEqual ? FileDiffResultLists.DiffDetailResult.TextMatch : FileDiffResultLists.DiffDetailResult.TextMismatch);
                    return areTextFilesEqual;
                }

                FileDiffResultLists.RecordDiffDetail(fileRelativePath, FileDiffResultLists.DiffDetailResult.MD5Mismatch);
                return false;
            }
            catch (Exception)
            {
                // 各比較手段での失敗は最終的にここでログ化し、呼び出し元へ再スロー。
                LoggerService.LogMessage(LoggerService.LogLevel.Error, string.Format(Constants.ERROR_DIFFING, file1AbsolutePath, file2AbsolutePath), shouldOutputMessageToConsole: true);
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
        /// <param name="maxParallel">最大並列度</param>
        /// <returns>一致すれば true。エラーや引数不正時は false。</returns>
        private static async Task<bool> DiffTextFilesParallelAsync(string file1AbsolutePath, string file2AbsolutePath, long largeFileSizeThresholdBytes, int maxParallel)
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
                    return await Utility.DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                }
                if (maxParallel <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxParallel), maxParallel, Constants.ERROR_MAX_PARALLEL);
                }

                // 大きなファイルは固定サイズのチャンクに分割し、読み取り→比較を並列実行する。
                int chunkCount = (int)((file1Info.Length + TEXT_DIFF_CHUNK_SIZE_BYTES - 1) / TEXT_DIFF_CHUNK_SIZE_BYTES);
                var differences = 0;
                await Parallel.ForEachAsync(Enumerable.Range(0, chunkCount), new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, async (index, cancellationToken) =>
                {
                    // 既に差分が見つかっていれば以降のチャンクは読む必要がない。
                    if (Volatile.Read(ref differences) != 0)
                    {
                        return;
                    }
                    var buffer1 = new byte[TEXT_DIFF_CHUNK_SIZE_BYTES];
                    var buffer2 = new byte[TEXT_DIFF_CHUNK_SIZE_BYTES];
                    int read1, read2;
                    using (var file1Stream = new FileStream(file1AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var file2Stream = new FileStream(file2AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        file1Stream.Seek((long)index * TEXT_DIFF_CHUNK_SIZE_BYTES, SeekOrigin.Begin);
                        file2Stream.Seek((long)index * TEXT_DIFF_CHUNK_SIZE_BYTES, SeekOrigin.Begin);
                        read1 = await file1Stream.ReadAsync(buffer1.AsMemory(0, TEXT_DIFF_CHUNK_SIZE_BYTES), cancellationToken);
                        read2 = await file2Stream.ReadAsync(buffer2.AsMemory(0, TEXT_DIFF_CHUNK_SIZE_BYTES), cancellationToken);
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
    }
}
