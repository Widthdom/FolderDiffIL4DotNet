using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// .NET 逆アセンブルの実行および <see cref="ILCache"/> を用いたキャッシュ取得/保存を担当するサービス。
    /// ツールのバージョン取得（<see cref="DotNetDisassemblerCache"/> 経由）・ブラックリスト制御・プリフェッチ（既存キャッシュの事前ヒット確認）も担当。
    /// 日本語/非ASCIIパス対策として必要に応じて ASCII 一時コピーを作成して実行します。
    /// </summary>
    public sealed class DotNetDisassembleService
    {
        #region constants
        /// <summary>
        /// ブラックリスト化判定に用いる連続失敗閾値。
        /// </summary>
        private const int DISASSEMBLE_FAIL_THRESHOLD = 3;

        /// <summary>
        /// ユーザープロファイル直下の .NET ホームディレクトリ名。
        /// </summary>
        private const string DOTNET_HOME_DIRNAME = ".dotnet";

        /// <summary>
        /// .NET グローバルツールのサブディレクトリ名。
        /// </summary>
        private const string DOTNET_TOOLS_DIRNAME = "tools";

        /// <summary>
        /// ildasmラベル
        /// </summary>
        private const string ILDASM_LABEL = "ildasm";

        /// <summary>
        /// ilspycmd の IL 出力を有効にするスイッチ（例: -il）
        /// </summary>
        private const string ILSPY_FLAG_IL = "-il";

        /// <summary>
        /// ilspycmd の出力ファイル指定スイッチ（例: -o &lt;path&gt;）
        /// </summary>
        private const string ILSPY_FLAG_OUTPUT = "-o";

        /// <summary>
        /// 例外のルート原因フォーマット
        /// </summary>
        private const string INFO_ROOT_CAUSE_FORMAT = " RootCause: {0}";

        /// <summary>
        /// 逆アセンブラ起動失敗ログ
        /// </summary>
        private const string LOG_FAILED_TO_START_DISASSEMBLER = "Failed to start disassembler tool '{0}': {1}";

        /// <summary>
        /// 逆アセンブラ準備時の予期せぬエラー
        /// </summary>
        private const string LOG_UNEXPECTED_ERROR_PREPARING_DISASSEMBLER = "Unexpected error while preparing to run '{0}': {1}";

        /// <summary>
        /// ildasm実行失敗時の例外フォーマット
        /// </summary>
        private const string ERROR_EXECUTE_ILDASM = "Failed to execute " + ILDASM_LABEL + " for file: {0}. {1}{2}";

        /// <summary>
        /// ASCII一時コピー作成失敗
        /// </summary>
        private const string LOG_FAILED_CREATE_ASCII_TEMP_COPY = "Failed to create ASCII temp copy for '{0}': {1}";

        /// <summary>
        /// 逆アセンブラバージョン取得失敗ログ (prefetch)
        /// </summary>
        private const string LOG_FAILED_TO_GET_VERSION_FOR_COMMAND = "Failed to get version for disassemble command '{0}' (candidate: '{1}'). Skipping.";

        /// <summary>
        /// ILキャッシュプリフェッチ共通プレフィックス
        /// </summary>
        private const string LOG_PREFETCH_IL_CACHE_PREFIX = "Prefetch " + Constants.LABEL_IL_CACHE;

        /// <summary>
        /// ILキャッシュプリフェッチ開始
        /// </summary>
        private const string LOG_PREFETCH_IL_CACHE_START = LOG_PREFETCH_IL_CACHE_PREFIX + ": starting for {0} .NET assemblies ({1}={2})";

        /// <summary>
        /// ILキャッシュプリフェッチ進捗
        /// </summary>
        private const string LOG_PREFETCH_IL_CACHE_PROGRESS = LOG_PREFETCH_IL_CACHE_PREFIX + ": {0}/{1} ({2}%), hits={3}";

        /// <summary>
        /// ILキャッシュプリフェッチ完了
        /// </summary>
        private const string LOG_PREFETCH_IL_CACHE_COMPLETE = LOG_PREFETCH_IL_CACHE_PREFIX + ": completed. hits={0}, stores={1}";

        /// <summary>
        /// ILキャッシュプリフェッチ失敗
        /// </summary>
        private const string LOG_FAILED_PREFETCH_IL_CACHE = "Failed to prefetch " + Constants.LABEL_IL_CACHE + " for assembly '{0}': {1}";

        /// <summary>
        /// ILキャッシュ取得失敗
        /// </summary>
        private const string LOG_FAILED_GET_IL_FROM_CACHE = "Failed to get " + Constants.LABEL_IL + " from cache for {0} with command {1}: {2}";

        /// <summary>
        /// ILキャッシュ設定失敗
        /// </summary>
        private const string LOG_FAILED_SET_IL_CACHE = "Failed to set " + Constants.LABEL_IL_CACHE + " for {0} with command {1}: {2}";

        /// <summary>
        /// ildasm失敗エラー
        /// </summary>
        private const string ERROR_ILDASM_FAILED = ILDASM_LABEL + " failed (exit {0}) with command: {1} {2} in {3}\nFile: {4}\nStderr: {5}";

        /// <summary>
        /// ildasm/ilspyインストールガイダンス
        /// </summary>
        private const string GUIDANCE_INSTALL_DISASSEMBLER =
            Constants.DOTNET_ILDASM + " was not found or failed to run.\n" +
            "If it's not installed, install it with:\n" +
            "  " + Constants.DOTNET_MUXER + " tool install -g " + Constants.DOTNET_ILDASM + "\n" +
            "Also ensure that ~/" + DOTNET_HOME_DIRNAME + "/" + DOTNET_TOOLS_DIRNAME + " is included in your PATH.\n" +
            "Alternatively, you can install " + Constants.ILSPY + " and we will use it automatically:\n" +
            "  " + Constants.DOTNET_MUXER + " tool install -g " + Constants.ILSPY;
        #endregion
        private enum DisassemblerKind
        {
            Unknown = 0,
            DotnetIldasm,
            Ildasm,
            Ilspy
        }

        #region private read only member variables
        /// <summary>
        /// ブラックリスト化有効期間。
        /// </summary>
        private static readonly TimeSpan _toolBlackListDuration = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 設定値。IL 出力やキャッシュ利用可否、キャッシュパラメータ等を保持する <see cref="ConfigSettings"/>。
        /// </summary>
        private readonly ConfigSettings _config;

        /// <summary>
        /// IL 逆アセンブル結果キャッシュインスタンス。無効化されている場合は null。
        /// </summary>
        private readonly ILCache _ilCache;

        /// <summary>
        /// ツール毎の連続失敗回数と最終失敗時刻(協定世界時刻)。閾値超過で一定時間ブラックリスト化するために利用。
        /// </summary>
        private static readonly ConcurrentDictionary<string, (int FailCount, DateTime LastFailUtc)> _disassembleFailCountAndTime = new();
        #endregion

        #region private writable member variables
        /// <summary>
        /// キャッシュヒット回数。
        /// </summary>
        private int _ilCacheHits;

        /// <summary>
        /// キャッシュ書き込み（格納）回数。
        /// </summary>
        private int _ilCacheStores;
        #endregion

        /// <summary>
        /// IL キャッシュのヒット件数（読み取り専用スナップショット）。
        /// </summary>
        /// <remarks>
        /// 並列に更新されるため、可視性を担保する目的で Volatile.Read を使用しています。
        /// </remarks>
        public int IlCacheHits => Volatile.Read(ref _ilCacheHits);

        /// <summary>
        /// IL キャッシュへの格納（書き込み）件数（読み取り専用スナップショット）。
        /// </summary>
        /// <remarks>
        /// 並列に更新されるため、可視性を担保する目的で Volatile.Read を使用しています。
        /// </remarks>
        public int IlCacheStores => Volatile.Read(ref _ilCacheStores);

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="config"></param>
        /// <param name="ilCache"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public DotNetDisassembleService(ConfigSettings config, ILCache ilCache)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _ilCache = ilCache;
        }

        /// <summary>
        /// 設定された候補コマンド（dotnet-ildasm / ildasm / ilspy など）を順次試し、成功した最初の逆アセンブル結果を返します。
        /// キャッシュヒット時はプロセス起動を省略し、使用したコマンド＋バージョン情報付きのラベルを返します。
        /// </summary>
        /// <param name="dotNetAssemblyfileAbsolutePath">逆アセンブル対象となる .NET アセンブリの絶対パス。</param>
        /// <returns>逆アセンブル済み IL テキストと、人間が読めるコマンド表示（バージョン付き）をタプルで返します。</returns>
        public async Task<(string ilText, string commandString)> DisassembleAsync(string dotNetAssemblyfileAbsolutePath)
        {
            Exception lastError = null;

            foreach (var candidateDisassembleCommand in CandidateDisassembleCommands())
            {
                // 直近で連続失敗したコマンドは一時的にブラックリスト化しているためスキップ。
                if (IsDisassemblerBlacklisted(candidateDisassembleCommand))
                {
                    continue;
                }

                try
                {
                    // キャッシュ確認とプロセス起動を内包した TryDisassembleAsync を実行。
                    var attempt = await TryDisassembleAsync(candidateDisassembleCommand, dotNetAssemblyfileAbsolutePath);
                    if (attempt.Success)
                    {
                        return (attempt.IlText, attempt.DisassembleCommandAndItsVersionWithArguments);
                    }
                    if (attempt.Error != null)
                    {
                        lastError = attempt.Error;
                    }
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    lastError = ex;
                    LoggerService.LogMessage(LoggerService.LogLevel.Warning, string.Format(LOG_FAILED_TO_START_DISASSEMBLER, candidateDisassembleCommand, ex.Message), shouldOutputMessageToConsole: true, ex);
                    RegisterDisassembleFailure(candidateDisassembleCommand);
                    continue;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    LoggerService.LogMessage(LoggerService.LogLevel.Warning, string.Format(LOG_UNEXPECTED_ERROR_PREPARING_DISASSEMBLER, candidateDisassembleCommand, ex.Message), shouldOutputMessageToConsole: true, ex);
                    RegisterDisassembleFailure(candidateDisassembleCommand);
                    continue;
                }
            }

            var innerMsg = lastError != null ? string.Format(INFO_ROOT_CAUSE_FORMAT, lastError.Message) : string.Empty;
            throw new InvalidOperationException(string.Format(ERROR_EXECUTE_ILDASM, dotNetAssemblyfileAbsolutePath, GUIDANCE_INSTALL_DISASSEMBLER, innerMsg), lastError);
        }

        /// <summary>
        /// 指定された .NET アセンブリ群に対して代表的な逆アセンブラコマンド × 引数パターンを総当たりし、
        /// 既存 IL キャッシュにヒットするかを事前確認するプリフェッチ的処理。
        /// ハッシュ連続アクセス負荷を避けるため、入力列挙を <see cref="ICollection{T}"/> に引き上げて件数 0 なら即帰還し、
        /// その後のログ出力や Parallel.ForEach 初期化を省略します。
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxParallel"/> が 1 未満の場合。</exception>
        /// </summary>
        public async Task PrefetchIlCacheAsync(IEnumerable<string> dotNetAssemblyFilesAbsolutePaths, int maxParallel)
        {
            // IL キャッシュ無効 or null、または入力の null といった前提が揃わない場合は早期終了。
            if (dotNetAssemblyFilesAbsolutePaths == null || !_config.EnableILCache || _ilCache == null)
            {
                return;
            }
            if (maxParallel <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxParallel), maxParallel, Constants.ERROR_MAX_PARALLEL);
            }

            // 入力列を ICollection に引き上げ（必要なら ToList）て、件数ゼロなら処理をスキップ。
            var assemblies = dotNetAssemblyFilesAbsolutePaths as ICollection<string> ?? dotNetAssemblyFilesAbsolutePaths.ToList();
            if (assemblies.Count == 0)
            {
                return;
            }

            // 対象件数と並列度をログに出し、プリフェッチの開始を明示。
            LoggerService.LogMessage(
                LoggerService.LogLevel.Info,
                string.Format(LOG_PREFETCH_IL_CACHE_START, assemblies.Count, nameof(maxParallel), maxParallel),
                shouldOutputMessageToConsole: true);

            var disassembleCommandAndItsVersionList = new List<(string DisassembleCommand, string DisassemblerVersion)>();
            foreach (var candidateDisassembleCommand in CandidateDisassembleCommands())
            {
                // dotnet-ildasm の場合は `dotnet dotnet-ildasm` に正規化、それ以外はファイル名を抽出して扱いやすくする。
                string disassembleCommand = string.Equals(candidateDisassembleCommand, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase)
                    ? $"{Constants.DOTNET_MUXER} {Constants.DOTNET_ILDASM}"
                    : (Path.GetFileName(candidateDisassembleCommand) ?? candidateDisassembleCommand);
                try
                {
                    // コマンドごとのバージョンを取得して正規化した文字列を保持。
                    var disassemblerVersion = await DotNetDisassemblerCache.GetDisassemblerVersionAsync(disassembleCommand);
                    if (!string.IsNullOrWhiteSpace(disassemblerVersion))
                    {
                        // バージョン表記に改行等が含まれるケースがあるためホワイトスペースを潰して 1 行にそろえる。
                        disassembleCommandAndItsVersionList.Add((candidateDisassembleCommand, disassemblerVersion.Replace("\r", " ").Replace("\n", " ").Trim()));
                    }
                }
                catch (Exception ex)
                {
                    // バージョン取得に失敗した場合は警告のみ出し、そのコマンドはスキップ。
                    LoggerService.LogMessage(LoggerService.LogLevel.Warning, string.Format(LOG_FAILED_TO_GET_VERSION_FOR_COMMAND, disassembleCommand, candidateDisassembleCommand), shouldOutputMessageToConsole: false, ex);
                }
            }
            if (disassembleCommandAndItsVersionList.Count == 0)
            {
                // どのコマンドのバージョンも取得できなかった場合はプリフェッチ継続不可なので終了。
                return;
            }

            int processed = 0;
            long lastLogTicks = DateTime.UtcNow.Ticks;

            await Parallel.ForEachAsync(assemblies, new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, async (dotNetAssemblyFileAbsolutePath, _) =>
            {
                try
                {
                    // 絶対パス版とファイル名版の両キャッシュキーで代表的な引数パターンを総当たりし、
                    // 「バージョン付きラベル × 引数」の形でキャッシュにヒットするかを検証する。
                    var dotNetAssemblyNameOnly = Path.GetFileName(dotNetAssemblyFileAbsolutePath);
                    foreach (var (disassembleCommand, disassemblerVersion) in disassembleCommandAndItsVersionList)
                    {
                        // コマンド種別 dotnet-ildasm / ilspy / ildasm でそれぞれ異なるデフォルト引数を組む。
                        IEnumerable<string> disassembleCommandsWithArguments;
                        var disassemblerFileName = Path.GetFileName(disassembleCommand);
                        if (string.Equals(disassembleCommand, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase))
                        {
                            // dotnet muxer は "dotnet dotnet-ildasm" の形で実行されるため、相対パス・絶対パスの双方を試す。
                            disassembleCommandsWithArguments = [$"{Constants.DOTNET_MUXER} {Constants.DOTNET_ILDASM} {dotNetAssemblyNameOnly}", $"{Constants.DOTNET_MUXER} {Constants.DOTNET_ILDASM} {dotNetAssemblyFileAbsolutePath}"];
                        }
                        else if (string.Equals(disassemblerFileName, Constants.ILSPY, StringComparison.OrdinalIgnoreCase))
                        {
                            // ilspycmd は /il スイッチを付与する必要があるため、これも付けた状態で 2 パターン生成。
                            disassembleCommandsWithArguments = [$"{disassemblerFileName} {ILSPY_FLAG_IL} {dotNetAssemblyNameOnly}", $"{disassemblerFileName} {ILSPY_FLAG_IL} {dotNetAssemblyFileAbsolutePath}"];
                        }
                        else
                        {
                            // それ以外（ildasm 等）はコマンド + ターゲットだけで十分なので同様に 2 パターン。
                            disassembleCommandsWithArguments = [$"{disassemblerFileName} {dotNetAssemblyNameOnly}", $"{disassemblerFileName} {dotNetAssemblyFileAbsolutePath}"];
                        }

                        foreach (var disassembleCommandWithArguments in disassembleCommandsWithArguments)
                        {
                            var disassembleCommandAndItsVersionWithArguments = disassembleCommandWithArguments + (string.IsNullOrEmpty(disassemblerVersion) ? string.Empty : $" (version: {disassemblerVersion})");
                            // キャッシュヒット時はヒット数を増やし、残りのコマンドはスキップして次のアセンブリへ。
                            var cachedIL = await _ilCache.TryGetILAsync(dotNetAssemblyFileAbsolutePath, disassembleCommandAndItsVersionWithArguments);
                            if (cachedIL != null)
                            {
                                Interlocked.Increment(ref _ilCacheHits);
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.LogMessage(LoggerService.LogLevel.Warning, string.Format(LOG_FAILED_PREFETCH_IL_CACHE, dotNetAssemblyFileAbsolutePath, ex.Message), shouldOutputMessageToConsole: true, ex);
                }
                finally
                {
                    // 進捗ログを適度な頻度で出すため、件数ステップと経過時間の両方をトリガーにしてログ出力を制御。
                    var done = Interlocked.Increment(ref processed);
                    var nowTicks = DateTime.UtcNow.Ticks;
                    var prev = Interlocked.Read(ref lastLogTicks);
                    bool timeElapsed = new TimeSpan(nowTicks - prev).TotalSeconds >= 2;
                    bool countStep = done % 100 == 0 || done == assemblies.Count;
                    if (timeElapsed || countStep)
                    {
                        if (Interlocked.CompareExchange(ref lastLogTicks, nowTicks, prev) == prev)
                        {
                            int percent = (int)(done * 100.0 / assemblies.Count);
                            LoggerService.LogMessage(LoggerService.LogLevel.Info, string.Format(LOG_PREFETCH_IL_CACHE_PROGRESS, done, assemblies.Count, percent, IlCacheHits), shouldOutputMessageToConsole: true);
                        }
                    }
                }
            });

            // 最終的なヒット/格納カウントをログに出し、プリフェッチ完了を通知。
            LoggerService.LogMessage(LoggerService.LogLevel.Info, string.Format(LOG_PREFETCH_IL_CACHE_COMPLETE, IlCacheHits, IlCacheStores), shouldOutputMessageToConsole: true);
        }

        #region private methods
        /// <summary>
        /// 指定コマンドでアセンブリの逆アセンブルを試行します。必要に応じて一時ASCIIパスを生成し、
        /// 複数の引数セットを順に試します。
        /// </summary>
        /// <param name="disassembleCommand">使用するコマンド（ildasm / dotnet / ilspycmd など）</param>
        /// <param name="dotNetAssemblyFileAbsolutePath">対象アセンブリの絶対パス</param>
        /// <returns>成功可否、IL テキスト、ツールラベル、発生した例外</returns>
        private async Task<(bool Success, string IlText, string DisassembleCommandAndItsVersionWithArguments, Exception Error)> TryDisassembleAsync(string disassembleCommand, string dotNetAssemblyFileAbsolutePath)
        {
            Exception lastError = null;
            string tempAsciiPath = CreateAsciiTempCopyIfNeeded(dotNetAssemblyFileAbsolutePath);

            try
            {
                foreach (var argset in BuildArgSets(disassembleCommand, dotNetAssemblyFileAbsolutePath, tempAsciiPath))
                {
                    var result = await TryDisassembleWithArguments(disassembleCommand, dotNetAssemblyFileAbsolutePath, argset);
                    if (result.Success)
                    {
                        return result;
                    }
                    if (result.Error != null)
                    {
                        lastError = result.Error;
                    }
                }
            }
            finally
            {
                Utility.DeleteFileSilent(tempAsciiPath);
            }

            return (Success: false, IlText: null, DisassembleCommandAndItsVersionWithArguments: null, Error: lastError);
        }

        /// <summary>
        /// 1つの引数セットで逆アセンブルを試行します。キャッシュ事前チェック→プロセス実行→キャッシュ格納までを担当。
        /// </summary>
        /// <param name="disassembleCommand">実行コマンド</param>
        /// <param name="dotNetAssemblyFileAbsolutePath">対象アセンブリの絶対パス</param>
        /// <param name="argset">作業ディレクトリ/引数/一時出力のタプル</param>
        private async Task<(bool Success, string IlText, string DisassembleCommandAndItsVersionWithArguments, Exception Error)> TryDisassembleWithArguments(
            string disassembleCommand,
            string dotNetAssemblyFileAbsolutePath,
            (string workingDirectory, string[] args, string tempOut) argset)
        {
            string disassembleCommandAndItsVersionWithArguments = null;
            string ilText = null;

            // 逆アセンブル結果の取得前に IL キャッシュを確認してヒットすればプロセス起動を省略する。
            if (_config.EnableILCache && _ilCache != null)
            {
                try
                {
                    // コマンド＋引数を正規化（バージョン取得のキーにも使う）し、キャッシュのキーにする。
                    disassembleCommandAndItsVersionWithArguments = await GetDisassembleCommandAndItsVersionWithArgumentsAsync(Utility.BuildBaseLabel(disassembleCommand, argset.args));
                    var cachedIL = await _ilCache.TryGetILAsync(dotNetAssemblyFileAbsolutePath, disassembleCommandAndItsVersionWithArguments);
                    if (cachedIL != null)
                    {
                        Interlocked.Increment(ref _ilCacheHits);
                        return (Success: true, IlText: cachedIL, DisassembleCommandAndItsVersionWithArguments: disassembleCommandAndItsVersionWithArguments, Error: null);
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.LogMessage(LoggerService.LogLevel.Warning, string.Format(LOG_FAILED_GET_IL_FROM_CACHE, dotNetAssemblyFileAbsolutePath, disassembleCommand, ex.Message), shouldOutputMessageToConsole: true, ex);
                }
            }

            // キャッシュヒットしなかった場合は実際にプロセスを起動して IL 取得を試みる。
            var (exitCode, stdout, stderr, error) = await RunProcessAsync(disassembleCommand, argset.workingDirectory, argset.args);
            if (error != null)
            {
                // プロセス自体が起動できない場合はブラックリストへ登録し、当該試行を失敗として返す。
                RegisterDisassembleFailure(disassembleCommand);
                return (Success: false, IlText: null, DisassembleCommandAndItsVersionWithArguments: null, Error: error);
            }

            if (exitCode == 0)
            {
                // 正常終了したらブラックリスト状態を解除。
                ResetDisassembleFailure(disassembleCommand);

                var isIlspy = IsIlspyCommand(disassembleCommand);
                if (isIlspy && !string.IsNullOrEmpty(argset.tempOut) && File.Exists(argset.tempOut))
                {
                    // ilspycmd は一時ファイルに IL を出力するため、ファイルから読み込む。
                    ilText = await File.ReadAllTextAsync(argset.tempOut);
                    Utility.DeleteFileSilent(argset.tempOut);
                }
                else
                {
                    // ildasm / dotnet-ildasm は標準出力に吐くため stdout を採用。
                    ilText = stdout ?? string.Empty;
                }

                if (string.IsNullOrEmpty(disassembleCommandAndItsVersionWithArguments))
                {
                    // キャッシュ miss → プロセス実行の経路ではラベルが未取得の場合があるためここで確保。
                    disassembleCommandAndItsVersionWithArguments = await GetDisassembleCommandAndItsVersionWithArgumentsAsync(Utility.BuildBaseLabel(disassembleCommand, argset.args));
                }

                if (_config.EnableILCache && _ilCache != null)
                {
                    try
                    {
                        // 得られた IL をキャッシュに格納し、書き込みカウンタを増やす。
                        await _ilCache.SetILAsync(dotNetAssemblyFileAbsolutePath, disassembleCommandAndItsVersionWithArguments, ilText);
                        Interlocked.Increment(ref _ilCacheStores);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogMessage(LoggerService.LogLevel.Warning, string.Format(LOG_FAILED_SET_IL_CACHE, dotNetAssemblyFileAbsolutePath, disassembleCommand, ex.Message), shouldOutputMessageToConsole: true, ex);
                    }
                }

                return (Success: true, IlText: ilText, DisassembleCommandAndItsVersionWithArguments: disassembleCommandAndItsVersionWithArguments, Error: null);
            }
            else
            {
                // exit code が非 0 の場合は詳細付き例外に包んで失敗扱いとし、ブラックリストに登録。
                var lastError = new InvalidOperationException(string.Format(ERROR_ILDASM_FAILED, exitCode, disassembleCommand, Utility.GetUsedArgs(argset.args), argset.workingDirectory, dotNetAssemblyFileAbsolutePath, stderr));
                RegisterDisassembleFailure(disassembleCommand);
                return (Success: false, IlText: null, DisassembleCommandAndItsVersionWithArguments: null, Error: lastError);
            }
        }

        /// <summary>
        /// 指定コマンドが dotnet 実行ファイルかを判定します。
        /// </summary>
        private static bool IsDotnetMuxer(string command) => string.Equals(command, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 指定コマンドが ilspycmd かを判定します。
        /// </summary>
        private static bool IsIlspyCommand(string command) => string.Equals(Path.GetFileName(command), Constants.ILSPY, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// パスに非ASCII文字がある場合に、ASCII の一時パスへコピーしたファイルのパスを返します。該当しなければ null。
        /// </summary>
        private static string CreateAsciiTempCopyIfNeeded(string dotNetAssemblyFileAbsolutePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(dotNetAssemblyFileAbsolutePath) && Utility.ContainsNonAscii(dotNetAssemblyFileAbsolutePath))
                {
                    var tempAsciiPath = Path.Combine(Path.GetTempPath(), $"ildasm_input_{Guid.NewGuid():N}.dll");
                    File.Copy(dotNetAssemblyFileAbsolutePath, tempAsciiPath, overwrite: true);
                    return tempAsciiPath;
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogMessage(LoggerService.LogLevel.Warning, string.Format(LOG_FAILED_CREATE_ASCII_TEMP_COPY, dotNetAssemblyFileAbsolutePath, ex.Message), shouldOutputMessageToConsole: true, ex);
            }
            return null;
        }

        /// <summary>
        /// コマンド種別に応じた試行用の引数セットを列挙します。
        /// </summary>
        private static IEnumerable<(string workingDirectory, string[] args, string tempOut)> BuildArgSets(string disassembleCommand, string disassemblerFileAbsolutePath, string tempAsciiPath)
        {
            var disassemblerFileDirectoryAbsolutePath = Path.GetDirectoryName(disassemblerFileAbsolutePath) ?? Environment.CurrentDirectory;
            var disassemblerFileNameOnly = Path.GetFileName(disassemblerFileAbsolutePath);
            var isDotnetMuxer = IsDotnetMuxer(disassembleCommand);
            var isIlspy = IsIlspyCommand(disassembleCommand);

            var argSets = new List<(string workingDirectory, string[] args, string tempOut)>();
            if (!isIlspy)
            {
                // ildasm 系
                argSets.Add((disassemblerFileDirectoryAbsolutePath, isDotnetMuxer ? [ILDASM_LABEL, disassemblerFileNameOnly] : [disassemblerFileNameOnly], null));
                argSets.Add((Environment.CurrentDirectory, isDotnetMuxer ? [ILDASM_LABEL, disassemblerFileAbsolutePath] : [disassemblerFileAbsolutePath], null));
                if (!string.IsNullOrEmpty(tempAsciiPath))
                {
                    argSets.Add((Environment.CurrentDirectory, isDotnetMuxer ? [ILDASM_LABEL, tempAsciiPath] : [tempAsciiPath], null));
                }
            }
            else
            {
                // ilspycmd
                argSets.Add((disassemblerFileDirectoryAbsolutePath, [ILSPY_FLAG_IL, disassemblerFileNameOnly], null));
                argSets.Add((Environment.CurrentDirectory, [ILSPY_FLAG_IL, disassemblerFileAbsolutePath], null));
                if (!string.IsNullOrEmpty(tempAsciiPath))
                {
                    argSets.Add((Environment.CurrentDirectory, [ILSPY_FLAG_IL, tempAsciiPath], null));
                }
                string MakeTempOut() => Path.Combine(Path.GetTempPath(), $"ilspy_out_{Guid.NewGuid():N}.il");
                var out1 = MakeTempOut();
                argSets.Add((disassemblerFileDirectoryAbsolutePath, [ILSPY_FLAG_IL, ILSPY_FLAG_OUTPUT, out1, disassemblerFileNameOnly], out1));
                var out2 = MakeTempOut();
                argSets.Add((Environment.CurrentDirectory, [ILSPY_FLAG_IL, ILSPY_FLAG_OUTPUT, out2, disassemblerFileAbsolutePath], out2));
                if (!string.IsNullOrEmpty(tempAsciiPath))
                {
                    var out3 = MakeTempOut();
                    argSets.Add((Environment.CurrentDirectory, [ILSPY_FLAG_IL, ILSPY_FLAG_OUTPUT, out3, tempAsciiPath], out3));
                }
            }
            return argSets;
        }

        /// <summary>
        /// ベースラベルにツールバージョンを付加して返します。取得失敗時はベースラベルをそのまま返します。
        /// </summary>
        private async Task<string> GetDisassembleCommandAndItsVersionWithArgumentsAsync(string disassembleCommandWithArguments)
        {
            try
            {
                var disassemblerVersion = await DotNetDisassemblerCache.GetDisassemblerVersionAsync(disassembleCommandWithArguments);
                var disassemblerVersionOneLine = disassemblerVersion.Replace("\r", " ").Replace("\n", " ").Trim();
                return string.IsNullOrEmpty(disassemblerVersionOneLine) ? disassembleCommandWithArguments : $"{disassembleCommandWithArguments} (version: {disassemblerVersionOneLine})";
            }
            catch
            {
                return disassembleCommandWithArguments;
            }
        }

        /// <summary>
        /// 指定コマンドを起動して終了を待ち、終了コードと標準出力/標準エラーを返します。起動失敗やアクセス拒否などがあれば例外を返します。
        /// </summary>
        private static async Task<(int ExitCode, string Stdout, string Stderr, Exception Error)> RunProcessAsync(string disassembleCommand, string workingDirectoryAbsolutePath, string[] args)
        {
            try
            {
                // プロセス起動設定を構築（標準出力/標準エラーをリダイレクトし、GUIは生成しない）。
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = disassembleCommand,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectoryAbsolutePath
                };
                foreach (var arg in args)
                {
                    processStartInfo.ArgumentList.Add(arg);
                }

                using var process = new Process { StartInfo = processStartInfo };
                // 実行開始→標準出力/標準エラー読み取り→終了待ちの順で非同期に処理。
                process.Start();
                var outTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outTask, errTask);
                await process.WaitForExitAsync();
                var stdOutput = outTask.Result;
                var errorOutput = errTask.Result;
                // 成功時は終了コードと標準出力/標準エラーを詰めて返す。
                return (ExitCode: process.ExitCode, Stdout: stdOutput, Stderr: errorOutput, Error: null);
            }
            catch (Exception ex)
            {
                // プロセス起動自体が失敗した場合はエラーに詰めて呼び出し元に通知。
                return (ExitCode: int.MinValue, Stdout: null, Stderr: null, Error: ex);
            }
        }

        /// <summary>
        /// コマンドラベルから逆アセンブラの種類・キャッシュキー・実行ファイル名を解決します。
        /// Dotnet muxer 経由のケースや ilspycmd 単体起動など、既知のパターンを判定して適切なキーへ正規化します。
        /// </summary>
        private static (DisassemblerKind kind, string toolKey, string disassemblerExe) ResolveDisassembler(string commandLabel)
        {
            var tokens = Utility.TokenizeCommand(commandLabel);
            if (tokens.Count == 0)
            {
                throw new InvalidOperationException($"Failed to determine disassembler version: invalid command label '{commandLabel}'.");
            }

            if (string.Equals(tokens[0], Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Count >= 2 && string.Equals(tokens[1], Constants.DOTNET_ILDASM, StringComparison.OrdinalIgnoreCase))
                {
                    return (kind: DisassemblerKind.DotnetIldasm, toolKey: $"{Constants.DOTNET_MUXER} {Constants.DOTNET_ILDASM}", disassemblerExe: Constants.DOTNET_MUXER);
                }
            }

            var exe = tokens[0];
            return Path.GetFileName(exe)?.ToLowerInvariant() switch
            {
                Constants.DOTNET_ILDASM => (kind: DisassemblerKind.Ildasm, toolKey: Constants.DOTNET_ILDASM, disassemblerExe: exe),
                Constants.ILSPY => (kind: DisassemblerKind.Ilspy, toolKey: Constants.ILSPY, disassemblerExe: exe),
                _ => (kind: DisassemblerKind.Unknown, toolKey: null, disassemblerExe: null)
            };
        }

        /// <summary>
        /// 指定ツールがブラックリスト化されているかを判定。一定期間内に失敗が閾値を超えた場合は解除
        /// </summary>
        /// <param name="disassembleCommand"></param>
        /// <returns></returns>
        private static bool IsDisassemblerBlacklisted(string disassembleCommand)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommand))
            {
                return false;
            }
            if (!_disassembleFailCountAndTime.TryGetValue(disassembleCommand, out var info))
            {
                return false;
            }
            if (info.FailCount < DISASSEMBLE_FAIL_THRESHOLD)
            {
                return false;
            }
            if ((DateTime.UtcNow - info.LastFailUtc) > _toolBlackListDuration)
            {
                _disassembleFailCountAndTime.TryRemove(disassembleCommand, out _);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 指定ツールの失敗回数をインクリメントし、ブラックリスト判定に利用するデータを更新。
        /// </summary>
        /// <param name="disassembleCommand">コマンド名</param>
        private static void RegisterDisassembleFailure(string disassembleCommand)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommand))
            {
                return;
            }
            _disassembleFailCountAndTime.AddOrUpdate(
                disassembleCommand,
                _ => (1, DateTime.UtcNow),
                (_, old) => (old.FailCount + 1, DateTime.UtcNow));
        }

        /// <summary>
        /// 指定ツールの失敗カウントをリセット（ブラックリスト解除）。
        /// </summary>
        /// <param name="disassembleCommand">コマンド名</param>
        private static void ResetDisassembleFailure(string disassembleCommand)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommand))
            {
                return;
            }
            _disassembleFailCountAndTime.TryRemove(disassembleCommand, out _);
        }

        /// <summary>
        /// ユーザーの .NET グローバルツールディレクトリ（例: C:\Users\<name>\.dotnet\tools）。
        /// </summary>
        private static string UserDotnetToolsDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DOTNET_HOME_DIRNAME, DOTNET_TOOLS_DIRNAME);

        /// <summary>
        /// 逆アセンブラ候補コマンドを優先順で列挙します。
        /// </summary>
        private static IEnumerable<string> CandidateDisassembleCommands()
        {
            yield return Constants.DOTNET_ILDASM;
            yield return Path.Combine(UserDotnetToolsDirectory, Constants.DOTNET_ILDASM);
            yield return Constants.DOTNET_MUXER;
            yield return Constants.ILSPY;
            yield return Path.Combine(UserDotnetToolsDirectory, Constants.ILSPY);
        }
        #endregion
    }
}
