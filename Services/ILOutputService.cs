using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Services.ILOutput;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// 逆アセンブル（<see cref="DotNetDisassembleService"/>）・キャッシュ制御（<see cref="ILCache"/>）と出力サービスへの委譲を担うファサード。
    /// </summary>
    public sealed class ILOutputService : IILOutputService
    {
        #region constants
        /// <summary>
        /// ネットワーク共有最適化ログ (<see cref="ILOutputService"/>)
        /// </summary>
        private const string LOG_OPTIMIZE_FOR_NETWORK_SHARES_SKIP = $"OptimizeForNetworkShares=true: Skip {Constants.LABEL_IL} precompute/prefetch to reduce network I/O.";

        /// <summary>
        /// MD5ハッシュ計算失敗ログ
        /// </summary>
        private const string LOG_FAILED_PRECOMPUTE_MD5_HASHES = "Failed to precompute " + Constants.LABEL_MD5 + " hashes: {0}";

        /// <summary>
        /// IL 出力から比較時に除外する MVID 行の接頭辞
        /// </summary>
        private const string MVID_PREFIX = "// MVID:";

        /// <summary>
        /// バージョンラベル接頭辞。
        /// </summary>
        private const string VERSION_LABEL_PREFIX = " (version: ";

        /// <summary>
        /// IL出力失敗ログ
        /// </summary>
        private const string ERROR_FAILED_TO_OUTPUT_IL = $"Failed to output {Constants.LABEL_IL}.";

        /// <summary>
        /// old/new で同一逆アセンブラに揃えられなかった場合のエラー。
        /// </summary>
        private const string ERROR_MISMATCHED_DISASSEMBLER = "IL comparison requires the same disassembler and version for old/new. old: '{0}', new: '{1}'.";
        #endregion

        #region private read only member variables
        /// <summary>
        /// 設定値。IL 出力やキャッシュ利用可否、キャッシュパラメータ等を保持する <see cref="ConfigSettings"/>。
        /// </summary>
        private readonly ConfigSettings _config;

        /// <summary>
        /// IL 逆アセンブル結果キャッシュインスタンス。無効化されている場合は null。
        /// </summary>
        private readonly ILCache _ilCache;

        /// <summary>
        /// *_IL.txt の生成を担当するサービス。
        /// </summary>
        private readonly IILTextOutputService _ilTextOutputService;

        /// <summary>
        /// .NET 逆アセンブル担当サービス。
        /// </summary>
        private readonly IDotNetDisassembleService _dotNetDisassembleService;

        /// <summary>
        /// ログ出力サービス。
        /// </summary>
        private readonly ILoggerService _logger;

        #endregion

        /// <summary>
        /// コンストラクタ。実行コンテキストと協調サービスを受け取ります。
        /// </summary>
        /// <param name="config">設定。</param>
        /// <param name="executionContext">実行コンテキスト。</param>
        /// <param name="ilTextOutputService">IL テキスト出力サービス。</param>
        /// <param name="dotNetDisassembleService">.NET 逆アセンブルサービス。</param>
        /// <param name="ilCache">IL キャッシュインスタンス。無効時は null。</param>
        /// <param name="logger">ログ出力サービス。</param>
        public ILOutputService(
            ConfigSettings config,
            DiffExecutionContext executionContext,
            IILTextOutputService ilTextOutputService,
            IDotNetDisassembleService dotNetDisassembleService,
            ILCache ilCache,
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
        /// IL キャッシュ関連の事前計算を行います。
        /// </summary>
        /// <param name="filesAbsolutePaths">ファイルの絶対パス群。重複は呼び出し側で Distinct されている想定ですが、されていなくても動作します。</param>
        /// <param name="maxParallel">同時実行する最大並列数。</param>
        /// <remarks>
        /// 主な処理:
        /// <list type="number">
        /// <item><description>IL キャッシュが無効 (<c>EnableILCache == false</c>) またはキャッシュインスタンス未生成の場合は即 return。</description></item>
        /// <item><description><see cref="ILCache.PrecomputeAsync(IEnumerable{string}, int)"/> を呼び出し、対象ファイル（物理ファイル）ごとの MD5 など内部キー計算を先行実行し I/O コストを平準化。</description></item>
        /// <item><description><see cref="DotNetDetector.IsDotNetExecutable(string)"/> で .NET 実行可能と判定されたファイル群のみを対象に <see cref="PrefetchIlCacheAsync(IEnumerable{string}, int)"/> を呼び出し、使用候補の逆アセンブラー（ildasm / dotnet ildasm / ilspycmd）× 代表的な引数パターンのキャッシュヒットを事前確認（既存エントリがあればヒット数を加算）。</description></item>
        /// </list>
        /// 例外は内部で catch され WARNING ログ出力後に握りつぶします（差分処理本体の継続性を優先）。
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">maxParallel が 0 以下の場合にスローされます。</exception>
        /// <exception cref="Exception">下層の I/O / ハッシュ計算 / プロセス起動等で想定外の例外が発生した場合でも、メソッド内で捕捉されログ化されるため、呼び出し側へは再スローされません。</exception>
        /// <seealso cref="PrefetchIlCacheAsync(IEnumerable{string}, int)"/>
        /// <seealso cref="ILCache"/>
        public async Task PrecomputeAsync(IEnumerable<string> filesAbsolutePaths, int maxParallel)
        {
            if (_config.OptimizeForNetworkShares)
            {
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
                // .NET 実行可能のみを対象に、逆アセンブル用キャッシュをプリフェッチ
                await _dotNetDisassembleService.PrefetchIlCacheAsync(filesAbsolutePaths.Where(DotNetDetector.IsDotNetExecutable), maxParallel);
            }
            catch (Exception ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, string.Format(LOG_FAILED_PRECOMPUTE_MD5_HASHES, ex.Message), shouldOutputMessageToConsole: true, ex);
            }
        }

        public async Task<(bool AreEqual, string DisassemblerLabel)> DiffDotNetAssembliesAsync(string fileRelativePath, string oldFolderAbsolutePath, string newFolderAbsolutePath, bool shouldOutputIlText)
        {
            string file1AbsolutePath = Path.Combine(oldFolderAbsolutePath, fileRelativePath);
            string file2AbsolutePath = Path.Combine(newFolderAbsolutePath, fileRelativePath);

            // old/new を同一逆アセンブラ（同一バージョン識別）で逆アセンブルする。
            var (ilText1, commandString1, ilText2, commandString2) =
                await _dotNetDisassembleService.DisassemblePairWithSameDisassemblerAsync(file1AbsolutePath, file2AbsolutePath);
            var disassemblerLabel = BuildComparisonDisassemblerLabel(commandString1, commandString2);

            // 行単位に分割し、MVID 行および設定で指定された文字列を含む行を除外して比較する。
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
                    // 要求されている場合は、比較用に除外した IL テキストを *_IL.txt として保存する。
                    await _ilTextOutputService.WriteFullIlTextsAsync(fileRelativePath, il1LinesExcluded, il2LinesExcluded);
                }
            }
            catch (Exception)
            {
                // IL テキスト出力に失敗した場合はエラーログを出しつつ再スロー。
                _logger.LogMessage(AppLogLevel.Error, ERROR_FAILED_TO_OUTPUT_IL, shouldOutputMessageToConsole: true);
                throw;
            }
            return (areILsEqual, disassemblerLabel);
        }

        /// <summary>
        /// IL 比較時に除外すべき行かを判定します。
        /// </summary>
        private static bool ShouldExcludeIlLine(string line, bool shouldIgnoreContainingStrings, IReadOnlyCollection<string> ilIgnoreContainingStrings)
        {
            if (line is null)
            {
                return false;
            }

            if (line.StartsWith(MVID_PREFIX, StringComparison.Ordinal))
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
        /// old/new で使用された逆アセンブラ表示ラベルを比較用に 1 つへまとめます。
        /// </summary>
        private static string BuildComparisonDisassemblerLabel(string commandStringOld, string commandStringNew)
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
            throw new InvalidOperationException(string.Format(ERROR_MISMATCHED_DISASSEMBLER, oldLabel, newLabel));
        }

        /// <summary>
        /// 実行コマンド文字列から「ツール名 (version: x.y.z)」形式を抽出します。
        /// </summary>
        private static string BuildToolAndVersionLabel(string commandString)
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
