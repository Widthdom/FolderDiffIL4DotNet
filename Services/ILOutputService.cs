using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
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
    public sealed class ILOutputService : IILOutputService
    {
        private const string LOG_OPTIMIZE_FOR_NETWORK_SHARES_SKIP = $"OptimizeForNetworkShares=true: Skip {Constants.LABEL_IL} precompute/prefetch to reduce network I/O.";
        private const string VERSION_LABEL_PREFIX = " (version: ";
        private const string ERROR_FAILED_TO_OUTPUT_IL = $"Failed to output {Constants.LABEL_IL}.";
        private readonly ConfigSettings _config;
        private readonly ILCache? _ilCache;
        private readonly IILTextOutputService _ilTextOutputService;
        private readonly IDotNetDisassembleService _dotNetDisassembleService;
        private readonly ILoggerService _logger;

        public ILOutputService(
            ConfigSettings config,
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
        /// <item><description>Calls <see cref="ILCache.PrecomputeAsync(IEnumerable{string}, int)"/> to pre-calculate per-file MD5 keys, smoothing out I/O cost. / <see cref="ILCache.PrecomputeAsync(IEnumerable{string}, int)"/> を呼び出し、対象ファイルごとの MD5 など内部キー計算を先行実行し I/O コストを平準化。</description></item>
        /// <item><description>Calls <see cref="IDotNetDisassembleService.PrefetchIlCacheAsync"/> for files identified as .NET executables by <see cref="DotNetDetector.IsDotNetExecutable(string)"/>, checking cache hits across candidate disassembler x argument patterns. / <see cref="DotNetDetector.IsDotNetExecutable(string)"/> で .NET 実行可能と判定されたファイル群のみを対象に <see cref="IDotNetDisassembleService.PrefetchIlCacheAsync"/> を呼び出し、使用候補の逆アセンブラー × 代表的な引数パターンのキャッシュヒットを事前確認。</description></item>
        /// </list>
        /// Exceptions are caught internally and logged as WARNING to prioritise continuation of the main diff processing.
        /// 例外は内部で catch され WARNING ログ出力後に握りつぶします（差分処理本体の継続性を優先）。
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="filesAbsolutePaths"/> is null. / <paramref name="filesAbsolutePaths"/> が null の場合にスローされます。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxParallel"/> is 0 or negative. / maxParallel が 0 以下の場合にスローされます。</exception>
        /// <seealso cref="IDotNetDisassembleService.PrefetchIlCacheAsync"/>
        /// <seealso cref="ILCache"/>
        public async Task PrecomputeAsync(IEnumerable<string> filesAbsolutePaths, int maxParallel)
        {
            ArgumentNullException.ThrowIfNull(filesAbsolutePaths);

            if (_config.OptimizeForNetworkShares)
            {
                // When network-share optimisation is on, skip MD5 pre-warming and IL cache prefetch
                // ネットワーク共有最適化時は、MD5 プリウォームおよび IL キャッシュ先読みをスキップ
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
                // Prefetch disassembly cache for .NET executables only
                // .NET 実行可能のみを対象に、逆アセンブル用キャッシュをプリフェッチ
                await _dotNetDisassembleService.PrefetchIlCacheAsync(filesAbsolutePaths.Where(DotNetDetector.IsDotNetExecutable), maxParallel);
            }
            catch (IOException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to precompute MD5 hashes: {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to precompute MD5 hashes: {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to precompute MD5 hashes: {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to precompute MD5 hashes: {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
        }

        /// <summary>
        /// Disassembles old/new .NET assemblies with the same disassembler, applies exclusion lines (MVID, configured strings), and compares the IL.
        /// Outputs IL text files when <paramref name="shouldOutputIlText"/> is true.
        /// old/new の .NET アセンブリを同一逆アセンブラで逆アセンブルし、MVID などの除外行を適用したうえで IL を比較します。
        /// <paramref name="shouldOutputIlText"/> が true の場合は IL テキストをファイルに出力します。
        /// </summary>
        public async Task<(bool AreEqual, string? DisassemblerLabel)> DiffDotNetAssembliesAsync(string fileRelativePath, string oldFolderAbsolutePath, string newFolderAbsolutePath, bool shouldOutputIlText)
        {
            string file1AbsolutePath = Path.Combine(oldFolderAbsolutePath, fileRelativePath);
            string file2AbsolutePath = Path.Combine(newFolderAbsolutePath, fileRelativePath);

            // Disassemble old/new with the same disassembler (same version identity).
            // old/new を同一逆アセンブラ（同一バージョン識別）で逆アセンブルする。
            var (ilText1, commandString1, ilText2, commandString2) =
                await _dotNetDisassembleService.DisassemblePairWithSameDisassemblerAsync(file1AbsolutePath, file2AbsolutePath);
            var disassemblerLabel = BuildComparisonDisassemblerLabel(commandString1, commandString2);

            // Split into lines and exclude MVID lines and configured ignore-strings before comparison.
            // 行単位に分割し、再ビルドで変わり得る MVID 行および設定で指定された文字列を含む行を除外して比較する。
            var ilIgnoreContainingStrings = GetNormalizedIlIgnoreContainingStrings(_config);
            var il1Lines = ilText1.Split('\n').ToList();
            var il2Lines = ilText2.Split('\n').ToList();
            var il1LinesExcluded = il1Lines.Where(line => !ShouldExcludeIlLine(line, _config.ShouldIgnoreILLinesContainingConfiguredStrings, ilIgnoreContainingStrings)).ToList();
            var il2LinesExcluded = il2Lines.Where(line => !ShouldExcludeIlLine(line, _config.ShouldIgnoreILLinesContainingConfiguredStrings, ilIgnoreContainingStrings)).ToList();
            bool areILsEqual = il1LinesExcluded.SequenceEqual(il2LinesExcluded);
            try
            {
                if (shouldOutputIlText)
                {
                    // Save the exclusion-filtered IL text as *_IL.txt when requested.
                    // 要求されている場合は、比較用に除外した IL テキストを *_IL.txt として保存する。
                    await _ilTextOutputService.WriteFullIlTextsAsync(fileRelativePath, il1LinesExcluded, il2LinesExcluded);
                }
            }
            catch (Exception)
            {
                // Log error and re-throw on IL text output failure.
                // IL テキスト出力に失敗した場合はエラーログを出しつつ再スロー。
                _logger.LogMessage(AppLogLevel.Error, ERROR_FAILED_TO_OUTPUT_IL, shouldOutputMessageToConsole: true);
                throw;
            }
            return (areILsEqual, disassemblerLabel);
        }

        /// <summary>
        /// Determines whether a line should be excluded from IL comparison.
        /// IL 比較時に除外すべき行かを判定します。
        /// </summary>
        private static bool ShouldExcludeIlLine(string line, bool shouldIgnoreContainingStrings, IReadOnlyCollection<string> ilIgnoreContainingStrings)
        {
            if (line is null)
            {
                return false;
            }

            if (line.StartsWith(Constants.IL_MVID_LINE_PREFIX, StringComparison.Ordinal))
            {
                return true;
            }

            if (!shouldIgnoreContainingStrings || ilIgnoreContainingStrings == null || ilIgnoreContainingStrings.Count == 0)
            {
                return false;
            }

            return ilIgnoreContainingStrings.Any(target => line.Contains(target, StringComparison.Ordinal));
        }

        /// <summary>
        /// Normalises the strings used for contains-based line exclusion during IL comparison (removes null/whitespace, trims, deduplicates).
        /// IL 比較時に「含む」判定で除外対象とする文字列を正規化します（null/空白除外、trim、重複排除）。
        /// </summary>
        private static List<string> GetNormalizedIlIgnoreContainingStrings(ConfigSettings config)
        {
            if (config?.ILIgnoreLineContainingStrings == null)
            {
                return new List<string>();
            }

            return config.ILIgnoreLineContainingStrings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Merges the disassembler labels used for old/new into a single comparison label.
        /// old/new で使用された逆アセンブラ表示ラベルを比較用に 1 つへまとめます。
        /// </summary>
        private static string? BuildComparisonDisassemblerLabel(string commandStringOld, string commandStringNew)
        {
            var oldLabel = BuildToolAndVersionLabel(commandStringOld);
            var newLabel = BuildToolAndVersionLabel(commandStringNew);
            if (string.IsNullOrWhiteSpace(oldLabel))
            {
                return newLabel;
            }
            if (string.IsNullOrWhiteSpace(newLabel))
            {
                return oldLabel;
            }
            if (string.Equals(oldLabel, newLabel, StringComparison.OrdinalIgnoreCase))
            {
                return oldLabel;
            }
            throw new InvalidOperationException($"IL comparison requires the same disassembler and version for old/new. old: '{oldLabel}', new: '{newLabel}'.");
        }

        /// <summary>
        /// Extracts a "toolName (version: x.y.z)" label from a command string.
        /// 実行コマンド文字列から「ツール名 (version: x.y.z)」形式を抽出します。
        /// </summary>
        private static string? BuildToolAndVersionLabel(string commandString)
        {
            if (string.IsNullOrWhiteSpace(commandString))
            {
                return null;
            }

            var tokens = ProcessHelper.TokenizeCommand(commandString);
            if (tokens.Count == 0)
            {
                return null;
            }

            string toolName;
            if (string.Equals(tokens[0], Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase) &&
                tokens.Count >= 2 &&
                (string.Equals(tokens[1], Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(tokens[1], Constants.DOTNET_ILDASM, StringComparison.OrdinalIgnoreCase)))
            {
                toolName = Constants.DOTNET_ILDASM;
            }
            else
            {
                toolName = Path.GetFileName(tokens[0]);
            }

            if (string.IsNullOrWhiteSpace(toolName))
            {
                return null;
            }
            if (string.Equals(toolName, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase))
            {
                toolName = Constants.DOTNET_ILDASM;
            }

            var versionStart = commandString.IndexOf(VERSION_LABEL_PREFIX, StringComparison.Ordinal);
            if (versionStart < 0)
            {
                return toolName;
            }

            var versionEnd = commandString.IndexOf(')', versionStart + VERSION_LABEL_PREFIX.Length);
            if (versionEnd <= versionStart)
            {
                return toolName;
            }

            var version = commandString.Substring(versionStart + VERSION_LABEL_PREFIX.Length, versionEnd - (versionStart + VERSION_LABEL_PREFIX.Length)).Trim();
            if (string.IsNullOrWhiteSpace(version))
            {
                return toolName;
            }

            if (string.Equals(toolName, Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase))
            {
                return $"{Constants.ILDASM_LABEL} (version: {version})";
            }
            return $"{toolName} (version: {version})";
        }
    }
}
