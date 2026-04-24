using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Common;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Services.ILOutput;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Facade coordinating disassembly (<see cref="DotNetDisassembleService"/>), cache control (<see cref="ILCache"/>), and output delegation.
    /// 逆アセンブル（<see cref="DotNetDisassembleService"/>）・キャッシュ制御（<see cref="ILCache"/>）と出力サービスへの委譲を担うファサード。
    /// </summary>
    public sealed partial class ILOutputService : IILOutputService
    {
        private const string LOG_OPTIMIZE_FOR_NETWORK_SHARES_SKIP = $"OptimizeForNetworkShares=true: Skip {Constants.LABEL_IL} precompute/prefetch to reduce network I/O.";
        private const string VERSION_LABEL_PREFIX = " (version: ";
        private const string ERROR_FAILED_TO_OUTPUT_IL = $"Failed to output {Constants.LABEL_IL}.";
        private const int IL_FILTER_STRING_MIN_LENGTH = 4;
        private readonly IReadOnlyConfigSettings _config;
        private readonly ILCache? _ilCache;
        private readonly IILTextOutputService _ilTextOutputService;
        private readonly IDotNetDisassembleService _dotNetDisassembleService;
        private readonly ILoggerService _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="ILOutputService"/>.
        /// <see cref="ILOutputService"/> の新しいインスタンスを初期化します。
        /// </summary>
        public ILOutputService(
            IReadOnlyConfigSettings config,
            DiffExecutionContext executionContext,
            IILTextOutputService ilTextOutputService,
            IDotNetDisassembleService dotNetDisassembleService,
            ILCache? ilCache,
            ILoggerService logger)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(executionContext);
            _config = config;
            ArgumentNullException.ThrowIfNull(ilTextOutputService);
            _ilTextOutputService = ilTextOutputService;
            ArgumentNullException.ThrowIfNull(dotNetDisassembleService);
            _dotNetDisassembleService = dotNetDisassembleService;
            _ilCache = ilCache;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <inheritdoc />
        public void PreSeedFileHash(string fileAbsolutePath, string sha256Hex)
        {
            _ilCache?.PreSeedFileHash(fileAbsolutePath, sha256Hex);
        }

        /// <summary>
        /// Performs IL-cache-related precomputation for the given files.
        /// IL キャッシュ関連の事前計算を行います。
        /// </summary>
        /// <param name="filesAbsolutePaths">
        /// Absolute paths of target files. Duplicates are tolerated but callers are expected to Distinct.
        /// ファイルの絶対パス群。重複は呼び出し側で Distinct されている想定ですが、されていなくても動作します。
        /// </param>
        /// <param name="maxParallel">
        /// Maximum degree of parallelism.
        /// 同時実行する最大並列数。
        /// </param>
        /// <remarks>
        /// Main steps / 主な処理:
        /// <list type="number">
        /// <item><description>Returns immediately if IL cache is disabled (<c>EnableILCache == false</c>) or no cache instance exists. / IL キャッシュが無効 (<c>EnableILCache == false</c>) またはキャッシュインスタンス未生成の場合は即 return。</description></item>
        /// <item><description>Calls <see cref="ILCache.PrecomputeAsync(IEnumerable{string}, int)"/> to pre-calculate per-file SHA256 keys, smoothing out I/O cost. / <see cref="ILCache.PrecomputeAsync(IEnumerable{string}, int)"/> を呼び出し、対象ファイルごとの SHA256 など内部キー計算を先行実行し I/O コストを平準化。</description></item>
        /// <item><description>Calls <see cref="IDotNetDisassembleService.PrefetchIlCacheAsync"/> for files identified as .NET executables by <see cref="DotNetDetector.IsDotNetExecutable(string)"/>, checking cache hits across candidate disassembler x argument patterns. / <see cref="DotNetDetector.IsDotNetExecutable(string)"/> で .NET 実行可能と判定されたファイル群のみを対象に <see cref="IDotNetDisassembleService.PrefetchIlCacheAsync"/> を呼び出し、使用候補の逆アセンブラー × 代表的な引数パターンのキャッシュヒットを事前確認。</description></item>
        /// </list>
        /// Exceptions are caught internally and logged as WARNING to prioritise continuation of the main diff processing.
        /// 例外は内部で catch され WARNING ログ出力後に握りつぶします（差分処理本体の継続性を優先）。
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="filesAbsolutePaths"/> is null. / <paramref name="filesAbsolutePaths"/> が null の場合にスローされます。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxParallel"/> is 0 or negative. / maxParallel が 0 以下の場合にスローされます。</exception>
        /// <param name="cancellationToken">Token to observe for cancellation. / キャンセルを監視するトークン。</param>
        /// <seealso cref="IDotNetDisassembleService.PrefetchIlCacheAsync"/>
        /// <seealso cref="ILCache"/>
        public async Task PrecomputeAsync(IEnumerable<string> filesAbsolutePaths, int maxParallel, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filesAbsolutePaths);

            if (_config.OptimizeForNetworkShares)
            {
                // When network-share optimisation is on, skip SHA256 pre-warming and IL cache prefetch
                // ネットワーク共有最適化時は、SHA256 プリウォームおよび IL キャッシュ先読みをスキップ
                _logger.LogMessage(AppLogLevel.Info, LOG_OPTIMIZE_FOR_NETWORK_SHARES_SKIP, shouldOutputMessageToConsole: true);
                return;
            }
            if (maxParallel <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxParallel), maxParallel, Constants.ERROR_MAX_PARALLEL);
            }
            if (!_config.EnableILCache || _ilCache == null)
            {
                return;
            }
            try
            {
                await _ilCache.PrecomputeAsync(filesAbsolutePaths, maxParallel);
                cancellationToken.ThrowIfCancellationRequested();
                // Prefetch disassembly cache for .NET executables only
                // .NET 実行可能のみを対象に、逆アセンブル用キャッシュをプリフェッチ
                await _dotNetDisassembleService.PrefetchIlCacheAsync(filesAbsolutePaths.Where(DotNetDetector.IsDotNetExecutable), maxParallel, cancellationToken);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException
                or InvalidOperationException or NotSupportedException)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to precompute SHA256 hashes ({ex.GetType().Name}): {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
        }

        /// <summary>
        /// Disassembles old/new .NET assemblies with the same disassembler, applies exclusion lines (MVID, configured strings), and compares the IL.
        /// Uses line-based streaming: reads process stdout line-by-line (avoids LOH allocations) and compares
        /// without materializing filtered line lists when IL text output is not required.
        /// Outputs IL text files when <paramref name="shouldOutputIlText"/> is true.
        /// old/new の .NET アセンブリを同一逆アセンブラで逆アセンブルし、MVID などの除外行を適用したうえで IL を比較します。
        /// 行単位のストリーミング処理を使用: プロセスの stdout を行単位で読み取り（LOH 割り当てを回避）、
        /// IL テキスト出力が不要な場合はフィルタ済み行リストを実体化せずに比較します。
        /// <paramref name="shouldOutputIlText"/> が true の場合は IL テキストをファイルに出力します。
        /// </summary>
        public async Task<(bool AreEqual, string? DisassemblerLabel)> DiffDotNetAssembliesAsync(string fileRelativePath, string oldFolderAbsolutePath, string newFolderAbsolutePath, bool shouldOutputIlText, CancellationToken cancellationToken = default)
        {
            string file1AbsolutePath = Path.Combine(oldFolderAbsolutePath, fileRelativePath);
            string file2AbsolutePath = Path.Combine(newFolderAbsolutePath, fileRelativePath);

            var ilIgnoreContainingStrings = GetNormalizedIlIgnoreContainingStrings(_config);
            bool shouldIgnore = _config.ShouldIgnoreILLinesContainingConfiguredStrings;
            bool ignoreMVID = _config.ShouldIgnoreMVID;

            // Disassemble old/new as lines (reads process stdout line-by-line, avoiding LOH-sized string allocations).
            // old/new を行リストとして逆アセンブル（プロセス stdout を行単位で読み取り、LOH サイズの文字列割り当てを回避）。
            var (il1Lines, commandString1, il2Lines, commandString2) =
                await _dotNetDisassembleService.DisassemblePairAsLinesWithSameDisassemblerAsync(file1AbsolutePath, file2AbsolutePath, cancellationToken);
            var disassemblerLabel = BuildComparisonDisassemblerLabel(commandString1, commandString2);

            if (!shouldOutputIlText)
            {
                // Streaming comparison: filter and compare line-by-line without materializing filtered lists.
                // If lines differ, fall back to block-aware comparison to handle method reordering.
                // ストリーミング比較: フィルタ済み行リストを実体化せずに行単位でフィルタ・比較する。
                // 行単位で不一致の場合、メソッド並び順変更を考慮しブロック単位比較にフォールバック。
                bool areILsEqual = StreamingFilteredSequenceEqual(il1Lines, il2Lines, shouldIgnore, ilIgnoreContainingStrings, ignoreMVID);
                if (!areILsEqual)
                {
                    var filtered1 = FilterIlLines(il1Lines, shouldIgnore, ilIgnoreContainingStrings, ignoreMVID);
                    var filtered2 = FilterIlLines(il2Lines, shouldIgnore, ilIgnoreContainingStrings, ignoreMVID);
                    areILsEqual = BlockAwareSequenceEqual(filtered1, filtered2);
                }
                return (areILsEqual, disassemblerLabel);
            }

            // Materialized path: need full filtered lists for IL text file output.
            // 実体化パス: IL テキストファイル出力用にフィルタ済み全行リストが必要。
            var il1LinesExcluded = FilterIlLines(il1Lines, shouldIgnore, ilIgnoreContainingStrings, ignoreMVID);
            var il2LinesExcluded = FilterIlLines(il2Lines, shouldIgnore, ilIgnoreContainingStrings, ignoreMVID);
            bool areEqual = il1LinesExcluded.SequenceEqual(il2LinesExcluded);
            if (!areEqual)
            {
                // Fall back to block-aware comparison to handle method/class reordering by the compiler.
                // コンパイラによるメソッド・クラスの並び替えを考慮し、ブロック単位比較にフォールバック。
                areEqual = BlockAwareSequenceEqual(il1LinesExcluded, il2LinesExcluded);
            }
            try
            {
                // Save the exclusion-filtered IL text as *_IL.txt.
                // 比較用に除外した IL テキストを *_IL.txt として保存する。
                await _ilTextOutputService.WriteFullIlTextsAsync(fileRelativePath, il1LinesExcluded, il2LinesExcluded);
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                // Log error with exception details and re-throw on IL text output failure.
                // IL テキスト出力に失敗した場合は例外詳細付きエラーログを出しつつ再スロー。
                _logger.LogMessage(AppLogLevel.Error,
                    $"{ERROR_FAILED_TO_OUTPUT_IL} for '{fileRelativePath}' (Old='{file1AbsolutePath}', New='{file2AbsolutePath}', {ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true, ex);
                throw;
            }
            return (areEqual, disassemblerLabel);
        }

    }
}
