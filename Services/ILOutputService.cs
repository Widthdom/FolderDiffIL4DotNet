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
    public sealed class ILOutputService
    {
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
        /// old 側 *_IL.txt を出力するフォルダ（絶対パス）。
        /// </summary>
        private readonly string _ilOldFolderAbsolutePath;

        /// <summary>
        /// new 側 *_IL.txt を出力するフォルダ（絶対パス）。
        /// </summary>
        private readonly string _ilNewFolderAbsolutePath;

        /// <summary>
        /// *_IL.txt の生成を担当するサービス。
        /// </summary>
        private readonly ILTextOutputService _ilTextOutputService;

        /// <summary>
        /// .NET 逆アセンブル担当サービス。
        /// </summary>
        private readonly DotNetDisassembleService _dotNetDisassembleService;
        #endregion

        /// <summary>
        /// コンストラクタ。パスおよび設定値を受け取り必要に応じて IL キャッシュを構築する。
        /// </summary>
        /// <param name="config">設定。</param>
        /// <param name="ilOldFolderAbsolutePath">old 側 IL テキスト出力フォルダ。</param>
        /// <param name="ilNewFolderAbsolutePath">new 側 IL テキスト出力フォルダ。</param>
        public ILOutputService(ConfigSettings config, string ilOldFolderAbsolutePath, string ilNewFolderAbsolutePath)
        {
            _config = config;
            _ilOldFolderAbsolutePath = ilOldFolderAbsolutePath;
            _ilNewFolderAbsolutePath = ilNewFolderAbsolutePath;
            _ilTextOutputService = new ILTextOutputService(_ilOldFolderAbsolutePath, _ilNewFolderAbsolutePath);
            if (_config.EnableILCache)
            {
                _ilCache = new ILCache(
                    string.IsNullOrWhiteSpace(_config.ILCacheDirectoryAbsolutePath) ? Path.Combine(AppContext.BaseDirectory, Constants.DEFAULT_IL_CACHE_DIR_NAME) : _config.ILCacheDirectoryAbsolutePath,
                    ilCacheMaxMemoryEntries: Constants.IL_CACHE_MAX_MEMORY_ENTRIES_DEFAULT,
                    timeToLive: TimeSpan.FromHours(12),
                    statsLogIntervalSeconds: _config.ILCacheStatsLogIntervalSeconds <= 0 ? 60 : _config.ILCacheStatsLogIntervalSeconds,
                    ilCacheMaxDiskFileCount: _config.ILCacheMaxDiskFileCount,
                    ilCacheMaxDiskMegabytes: _config.ILCacheMaxDiskMegabytes
                );
            }
            _dotNetDisassembleService = new DotNetDisassembleService(_config, _ilCache);
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
        /// <item><description><see cref="Utility.IsDotNetExecutable(string)"/> で .NET 実行可能と判定されたファイル群のみを対象に <see cref="PrefetchIlCacheAsync(IEnumerable{string}, int)"/> を呼び出し、使用候補の逆アセンブラー（ildasm / dotnet ildasm / ilspycmd）× 代表的な引数パターンのキャッシュヒットを事前確認（既存エントリがあればヒット数を加算）。</description></item>
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
                LoggerService.LogMessage(LoggerService.LogLevel.Info, Constants.LOG_OPTIMIZE_FOR_NETWORK_SHARES_SKIP, shouldOutputMessageToConsole: true);
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
                await _dotNetDisassembleService.PrefetchIlCacheAsync(filesAbsolutePaths.Where(Utility.IsDotNetExecutable), maxParallel);
            }
            catch (Exception ex)
            {
                LoggerService.LogMessage(LoggerService.LogLevel.Warning, string.Format(Constants.LOG_FAILED_PRECOMPUTE_MD5_HASHES, ex.Message), shouldOutputMessageToConsole: true, ex);
            }
        }

        public async Task<bool> DiffDotNetAssembliesAsync(string fileRelativePath, string oldFolderAbsolutePath, string newFolderAbsolutePath, bool shouldOutputIlText)
        {
            // IL 比較時はビルド毎に異なる MVID 行を除外するためのフィルタをローカルに定義。
            static bool IsNotMvidLine(string line) => line is null || !line.StartsWith(Constants.MVID_PREFIX, StringComparison.Ordinal);

            string file1AbsolutePath = Path.Combine(oldFolderAbsolutePath, fileRelativePath);
            string file2AbsolutePath = Path.Combine(newFolderAbsolutePath, fileRelativePath);

            // それぞれのアセンブリを逆アセンブルし、IL テキストと実行コマンドを受け取る。
            var (ilText1, commandString1) = await _dotNetDisassembleService.DisassembleAsync(file1AbsolutePath);
            var (ilText2, commandString2) = await _dotNetDisassembleService.DisassembleAsync(file2AbsolutePath);

            // 行単位に分割して MVID 行を除外し、純粋な IL のみを比較する。
            var il1Lines = ilText1.Split('\n').ToList();
            var il2Lines = ilText2.Split('\n').ToList();
            var il1LinesMvidExcluded = il1Lines.Where(IsNotMvidLine).ToList();
            var il2LinesMvidExcluded = il2Lines.Where(IsNotMvidLine).ToList();
            bool areILsEqual = il1LinesMvidExcluded.SequenceEqual(il2LinesMvidExcluded);
            try
            {
                if (shouldOutputIlText)
                {
                    // 要求されている場合は、比較用に除外した IL テキストを *_IL.txt として保存する。
                    await _ilTextOutputService.WriteFullIlTextsAsync(fileRelativePath, il1LinesMvidExcluded, il2LinesMvidExcluded);
                }
            }
            catch (Exception)
            {
                // IL テキスト出力に失敗した場合はエラーログを出しつつ再スロー。
                LoggerService.LogMessage(LoggerService.LogLevel.Error, Constants.ERROR_FAILED_TO_OUTPUT_IL, shouldOutputMessageToConsole: true);
                throw;
            }
            return areILsEqual;
        }
    }
}
