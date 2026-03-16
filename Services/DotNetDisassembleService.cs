using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Core.Text;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// .NET 逆アセンブルの実行および <see cref="ILCache"/> を用いたキャッシュ取得/保存を担当するサービス。
    /// ツールのバージョン取得（<see cref="DotNetDisassemblerCache"/> 経由）・ブラックリスト制御・プリフェッチ（既存キャッシュの事前ヒット確認）も担当。
    /// 日本語/非ASCIIパス対策として必要に応じて ASCII 一時コピーを作成して実行します。
    /// </summary>
    public sealed class DotNetDisassembleService : IDotNetDisassembleService
    {
        /// <summary>
        /// ブラックリスト化判定に用いる連続失敗閾値。
        /// <para>
        /// 1〜2 回の失敗は一時的な競合やファイルロックによるものが多いため、
        /// 誤ブラックリスト化を避けるため 3 回連続失敗を閾値としています。
        /// Threshold of consecutive failures before a disassembler tool is blacklisted.
        /// Set to 3 to tolerate transient errors (file locks, brief resource contention)
        /// while still reacting promptly to a broken tool.
        /// </para>
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
        /// ilspycmd の IL 出力を有効にするスイッチ（例: -il）
        /// </summary>
        private const string ILSPY_FLAG_IL = "-il";

        /// <summary>
        /// ilspycmd の出力ファイル指定スイッチ（例: -o &lt;path&gt;）
        /// </summary>
        private const string ILSPY_FLAG_OUTPUT = "-o";

        /// <summary>
        /// ildasm/ilspyインストールガイダンス
        /// </summary>
        private const string GUIDANCE_INSTALL_DISASSEMBLER =
            Constants.DOTNET_ILDASM + " was not found or failed to run.\n" +
            "If it's not installed, install it with:\n" +
            "  " + Constants.DOTNET_MUXER + " tool install -g " + Constants.DOTNET_ILDASM + "\n" +
            "Also ensure that ~/" + DOTNET_HOME_DIRNAME + "/" + DOTNET_TOOLS_DIRNAME + " is included in your PATH.\n" +
            "Alternatively, you can install " + Constants.ILSPY_CMD + " and we will use it automatically:\n" +
            "  " + Constants.DOTNET_MUXER + " tool install -g " + Constants.ILSPY_CMD;

        /// <summary>
        /// バージョン付与ラベルの接頭辞。
        /// </summary>
        private const string VERSION_LABEL_PREFIX = " (version: ";

        /// <summary>
        /// バージョン取得失敗時の識別子プレフィックス。
        /// </summary>
        private const string UNKNOWN_VERSION_FINGERPRINT_PREFIX = "unavailable; fingerprint: ";

        /// <summary>
        /// ディスクキャッシュ分離のための実行単位フォールバック識別子プレフィックス。
        /// </summary>
        private const string RUN_FINGERPRINT_PREFIX = "run:";
        /// <summary>
        /// ブラックリスト化有効期間の既定値（分）。設定未指定またはゼロ以下の場合に使用。
        /// </summary>
        private const int DEFAULT_BLACKLIST_TTL_MINUTES = 10;
        /// <summary>
        /// 設定値。IL 出力やキャッシュ利用可否、キャッシュパラメータ等を保持する <see cref="ConfigSettings"/>。
        /// </summary>
        private readonly ConfigSettings _config;

        /// <summary>
        /// IL 逆アセンブル結果キャッシュインスタンス。無効化されている場合は null。
        /// </summary>
        private readonly ILCache _ilCache;

        /// <summary>
        /// 逆アセンブラツールのブラックリスト管理。TTL と失敗閾値に基づいてツールを一時スキップします。
        /// </summary>
        private readonly DisassemblerBlacklist _blacklist;

        /// <summary>
        /// 実行単位フォールバック識別子。ツール実体を解決できない場合でも前回実行のキャッシュと混ざらないようにする。
        /// </summary>
        private static readonly string _runFingerprint = Guid.NewGuid().ToString("N");
        /// <summary>
        /// キャッシュヒット回数。
        /// </summary>
        private int _ilCacheHits;

        /// <summary>
        /// キャッシュ書き込み（格納）回数。
        /// </summary>
        private int _ilCacheStores;

        /// <summary>
        /// 比較結果を蓄積する実行単位の状態オブジェクト。
        /// </summary>
        private readonly FileDiffResultLists _fileDiffResultLists;

        /// <summary>
        /// ログ出力サービス。
        /// </summary>
        private readonly ILoggerService _logger;

        /// <summary>
        /// 逆アセンブラバージョン取得のキャッシュサービス。
        /// </summary>
        private readonly DotNetDisassemblerCache _dotNetDisassemblerCache;

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
        /// <param name="config">アプリケーション設定。</param>
        /// <param name="ilCache">IL キャッシュ（無効時は null）。</param>
        /// <param name="fileDiffResultLists">差分結果保持オブジェクト。</param>
        /// <param name="logger">ログ出力サービス。</param>
        /// <param name="dotNetDisassemblerCache">逆アセンブラバージョン取得キャッシュ。</param>
        /// <exception cref="ArgumentNullException"></exception>
        public DotNetDisassembleService(ConfigSettings config, ILCache ilCache, FileDiffResultLists fileDiffResultLists, ILoggerService logger, DotNetDisassemblerCache dotNetDisassemblerCache)
        {
            ArgumentNullException.ThrowIfNull(config);
            _config = config;
            var ttlMinutes = config.DisassemblerBlacklistTtlMinutes > 0
                ? config.DisassemblerBlacklistTtlMinutes
                : DEFAULT_BLACKLIST_TTL_MINUTES;
            _blacklist = new DisassemblerBlacklist(DISASSEMBLE_FAIL_THRESHOLD, TimeSpan.FromMinutes(ttlMinutes));
            _ilCache = ilCache;
            ArgumentNullException.ThrowIfNull(fileDiffResultLists);
            _fileDiffResultLists = fileDiffResultLists;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            ArgumentNullException.ThrowIfNull(dotNetDisassemblerCache);
            _dotNetDisassemblerCache = dotNetDisassemblerCache;
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
                    var (success, ilText, disassembleCommandAndItsVersionWithArguments, error) = await TryDisassembleAsync(candidateDisassembleCommand, dotNetAssemblyfileAbsolutePath, allowCache: true, recordUsage: true);
                    if (success)
                    {
                        return (ilText, disassembleCommandAndItsVersionWithArguments);
                    }
                    if (error != null)
                    {
                        lastError = error;
                    }
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    lastError = ex;
                    _logger.LogMessage(AppLogLevel.Warning, $"Failed to start disassembler tool '{candidateDisassembleCommand}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
                    RegisterDisassembleFailure(candidateDisassembleCommand);
                    continue;
                }
            }

            var innerMsg = lastError != null ? $" RootCause: {lastError.Message}" : string.Empty;
            throw new InvalidOperationException($"Failed to execute ildasm for file: {dotNetAssemblyfileAbsolutePath}. {GUIDANCE_INSTALL_DISASSEMBLER}{innerMsg}", lastError);
        }

        /// <inheritdoc />
        public async Task<(string oldIlText, string oldCommandString, string newIlText, string newCommandString)> DisassemblePairWithSameDisassemblerAsync(
            string oldDotNetAssemblyFileAbsolutePath,
            string newDotNetAssemblyFileAbsolutePath)
        {
            Exception lastError = null;
            foreach (var candidateDisassembleCommand in CandidateDisassembleCommands())
            {
                if (IsDisassemblerBlacklisted(candidateDisassembleCommand))
                {
                    continue;
                }

                try
                {
                    var oldResult = await TryDisassembleAsync(candidateDisassembleCommand, oldDotNetAssemblyFileAbsolutePath, allowCache: true, recordUsage: false);
                    if (!oldResult.Success)
                    {
                        if (oldResult.Error != null)
                        {
                            lastError = oldResult.Error;
                        }
                        continue;
                    }

                    var newResult = await TryDisassembleAsync(candidateDisassembleCommand, newDotNetAssemblyFileAbsolutePath, allowCache: true, recordUsage: false);
                    if (!newResult.Success)
                    {
                        if (newResult.Error != null)
                        {
                            lastError = newResult.Error;
                        }
                        continue;
                    }

                    if (!AreSameDisassemblerVersion(oldResult.DisassembleCommandAndItsVersionWithArguments, newResult.DisassembleCommandAndItsVersionWithArguments))
                    {
                        lastError = new InvalidOperationException($"Disassembler version mismatch for command '{candidateDisassembleCommand}'. old='{oldResult.DisassembleCommandAndItsVersionWithArguments}', new='{newResult.DisassembleCommandAndItsVersionWithArguments}'.");
                        continue;
                    }

                    RecordDisassemblerUsage(candidateDisassembleCommand, oldResult.DisassembleCommandAndItsVersionWithArguments);
                    RecordDisassemblerUsage(candidateDisassembleCommand, newResult.DisassembleCommandAndItsVersionWithArguments);
                    return (
                        oldResult.IlText,
                        oldResult.DisassembleCommandAndItsVersionWithArguments,
                        newResult.IlText,
                        newResult.DisassembleCommandAndItsVersionWithArguments);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    lastError = ex;
                    _logger.LogMessage(AppLogLevel.Warning, $"Failed to start disassembler tool '{candidateDisassembleCommand}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
                    RegisterDisassembleFailure(candidateDisassembleCommand);
                    continue;
                }
            }

            var innerMsg = lastError != null ? $" RootCause: {lastError.Message}" : string.Empty;
            throw new InvalidOperationException(
                $"Failed to execute ildasm with the same disassembler for files: {oldDotNetAssemblyFileAbsolutePath} and {newDotNetAssemblyFileAbsolutePath}. {GUIDANCE_INSTALL_DISASSEMBLER}{innerMsg}",
                lastError);
        }

        /// <inheritdoc />
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

            // 入力列を ICollection に引き上げ（必要なら List を生成）て、件数ゼロなら処理をスキップ。
            var assemblies = dotNetAssemblyFilesAbsolutePaths as ICollection<string> ?? [.. dotNetAssemblyFilesAbsolutePaths];
            if (assemblies.Count == 0)
            {
                return;
            }

            // 対象件数と並列度をログに出し、プリフェッチの開始を明示。
            _logger.LogMessage(
                AppLogLevel.Info,
                $"Prefetch IL cache: starting for {assemblies.Count} .NET assemblies ({nameof(maxParallel)}={maxParallel})",
                shouldOutputMessageToConsole: true);

            // 候補コマンドのバージョンリストを構築。どのコマンドのバージョンも取得できなければプリフェッチ不可。
            var disassembleCommandAndItsVersionList = await BuildDisassemblerVersionListAsync();
            if (disassembleCommandAndItsVersionList.Count == 0)
            {
                return;
            }

            int processed = 0;
            long lastLogTicks = DateTime.UtcNow.Ticks;

            await Parallel.ForEachAsync(assemblies, new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, async (dotNetAssemblyFileAbsolutePath, _) =>
            {
                try
                {
                    await TryHitCacheForAssemblyAsync(dotNetAssemblyFileAbsolutePath, disassembleCommandAndItsVersionList);
                }
                catch (IOException ex)
                {
                    _logger.LogMessage(AppLogLevel.Warning, $"Failed to prefetch IL cache for assembly '{dotNetAssemblyFileAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogMessage(AppLogLevel.Warning, $"Failed to prefetch IL cache for assembly '{dotNetAssemblyFileAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogMessage(AppLogLevel.Warning, $"Failed to prefetch IL cache for assembly '{dotNetAssemblyFileAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
                catch (NotSupportedException ex)
                {
                    _logger.LogMessage(AppLogLevel.Warning, $"Failed to prefetch IL cache for assembly '{dotNetAssemblyFileAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
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
                            _logger.LogMessage(AppLogLevel.Info, $"Prefetch IL cache: {done}/{assemblies.Count} ({percent}%), hits={IlCacheHits}", shouldOutputMessageToConsole: true);
                        }
                    }
                }
            });

            // 最終的なヒット/格納カウントをログに出し、プリフェッチ完了を通知。
            _logger.LogMessage(AppLogLevel.Info, $"Prefetch IL cache: completed. hits={IlCacheHits}, stores={IlCacheStores}", shouldOutputMessageToConsole: true);
        }
        /// <summary>
        /// 指定コマンドでアセンブリの逆アセンブルを試行します。必要に応じて一時ASCIIパスを生成し、
        /// 複数の引数セットを順に試します。
        /// </summary>
        /// <param name="disassembleCommand">使用するコマンド（ildasm / dotnet / ilspycmd など）</param>
        /// <param name="dotNetAssemblyFileAbsolutePath">対象アセンブリの絶対パス</param>
        /// <returns>成功可否、IL テキスト、ツールラベル、発生した例外</returns>
        private async Task<(bool Success, string IlText, string DisassembleCommandAndItsVersionWithArguments, Exception Error)> TryDisassembleAsync(
            string disassembleCommand,
            string dotNetAssemblyFileAbsolutePath,
            bool allowCache,
            bool recordUsage)
        {
            Exception lastError = null;
            string tempAsciiPath = CreateAsciiTempCopyIfNeeded(dotNetAssemblyFileAbsolutePath);

            try
            {
                foreach (var argset in BuildArgSets(disassembleCommand, dotNetAssemblyFileAbsolutePath, tempAsciiPath))
                {
                    var (success, ilText, disassembleCommandAndItsVersionWithArguments, error) = await TryDisassembleWithArguments(disassembleCommand, dotNetAssemblyFileAbsolutePath, argset, allowCache, recordUsage);
                    if (success)
                    {
                        return (success, ilText, disassembleCommandAndItsVersionWithArguments, error);
                    }
                    if (error != null)
                    {
                        lastError = error;
                    }
                }
            }
            finally
            {
                FileSystemUtility.DeleteFileSilent(tempAsciiPath);
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
            (string workingDirectory, string[] args, string tempOut) argset,
            bool allowCache,
            bool recordUsage)
        {
            string label = null;

            if (allowCache)
            {
                // キャッシュヒット時はプロセス起動を省略。ラベルはミス時も後続で再利用する。
                var (hit, cachedIl, computedLabel) = await TryCacheHitAsync(disassembleCommand, dotNetAssemblyFileAbsolutePath, argset.args, recordUsage);
                label = computedLabel;
                if (hit)
                {
                    return (Success: true, IlText: cachedIl, DisassembleCommandAndItsVersionWithArguments: label, Error: null);
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
                var ilText = await ReadIlTextAfterSuccessAsync(IsIlspyCommand(disassembleCommand), argset, stdout);
                if (string.IsNullOrEmpty(label))
                {
                    // キャッシュ miss → プロセス実行の経路ではラベルが未取得の場合があるためここで確保。
                    label = await GetDisassembleCommandAndItsVersionWithArgumentsAsync(ProcessHelper.BuildBaseLabel(disassembleCommand, argset.args));
                }
                await TryStoreToCacheAsync(dotNetAssemblyFileAbsolutePath, label, ilText, disassembleCommand);
                if (recordUsage)
                {
                    RecordDisassemblerUsage(disassembleCommand, label);
                }
                return (Success: true, IlText: ilText, DisassembleCommandAndItsVersionWithArguments: label, Error: null);
            }
            else
            {
                // exit code が非 0 の場合は詳細付き例外に包んで失敗扱いとし、ブラックリストに登録。
                var lastError = new InvalidOperationException($"ildasm failed (exit {exitCode}) with command: {disassembleCommand} {ProcessHelper.GetUsedArgs(argset.args)} in {argset.workingDirectory}\nFile: {dotNetAssemblyFileAbsolutePath}\nStderr: {stderr}");
                RegisterDisassembleFailure(disassembleCommand);
                return (Success: false, IlText: null, DisassembleCommandAndItsVersionWithArguments: null, Error: lastError);
            }
        }

        /// <summary>
        /// IL キャッシュへのヒットを試みます。
        /// ヒット時は (true, IlText, Label)、ミス時は (false, null, Label)、キャッシュ無効や例外時は (false, null, null) を返します。
        /// ミス時も Label を返すことで、後続のキャッシュ格納でバージョン取得の再実行を省略できます。
        /// </summary>
        private async Task<(bool Hit, string IlText, string Label)> TryCacheHitAsync(
            string disassembleCommand,
            string dotNetAssemblyFileAbsolutePath,
            string[] args,
            bool recordUsage)
        {
            if (!_config.EnableILCache || _ilCache == null)
            {
                return (false, null, null);
            }
            try
            {
                var label = await GetDisassembleCommandAndItsVersionWithArgumentsAsync(ProcessHelper.BuildBaseLabel(disassembleCommand, args));
                var cached = await _ilCache.TryGetILAsync(dotNetAssemblyFileAbsolutePath, label);
                if (cached != null)
                {
                    Interlocked.Increment(ref _ilCacheHits);
                    if (recordUsage)
                    {
                        RecordDisassemblerUsage(disassembleCommand, label, fromCache: true);
                    }
                    return (true, cached, label);
                }
                return (false, null, label);
            }
            catch (IOException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to get IL from cache for {dotNetAssemblyFileAbsolutePath} with command {disassembleCommand}: {ex.Message}", shouldOutputMessageToConsole: true, ex);
                return (false, null, null);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to get IL from cache for {dotNetAssemblyFileAbsolutePath} with command {disassembleCommand}: {ex.Message}", shouldOutputMessageToConsole: true, ex);
                return (false, null, null);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to get IL from cache for {dotNetAssemblyFileAbsolutePath} with command {disassembleCommand}: {ex.Message}", shouldOutputMessageToConsole: true, ex);
                return (false, null, null);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to get IL from cache for {dotNetAssemblyFileAbsolutePath} with command {disassembleCommand}: {ex.Message}", shouldOutputMessageToConsole: true, ex);
                return (false, null, null);
            }
        }

        /// <summary>
        /// プロセス正常終了後の IL テキストを取得します。
        /// ilspycmd は一時ファイルから読み取り（読み取り後に削除）、その他は stdout を使用します。
        /// </summary>
        private static async Task<string> ReadIlTextAfterSuccessAsync(
            bool isIlspy,
            (string workingDirectory, string[] args, string tempOut) argset,
            string stdout)
        {
            if (isIlspy && !string.IsNullOrEmpty(argset.tempOut) && File.Exists(argset.tempOut))
            {
                var text = await File.ReadAllTextAsync(argset.tempOut);
                FileSystemUtility.DeleteFileSilent(argset.tempOut);
                return text;
            }
            return stdout ?? string.Empty;
        }

        /// <summary>
        /// IL テキストをキャッシュに格納します。格納成功時はストアカウンタをインクリメントします。
        /// キャッシュ無効時やエラー時は警告ログを出力して処理を継続します。
        /// </summary>
        private async Task TryStoreToCacheAsync(
            string dotNetAssemblyFileAbsolutePath,
            string label,
            string ilText,
            string disassembleCommand)
        {
            if (!_config.EnableILCache || _ilCache == null)
            {
                return;
            }
            try
            {
                await _ilCache.SetILAsync(dotNetAssemblyFileAbsolutePath, label, ilText);
                Interlocked.Increment(ref _ilCacheStores);
            }
            catch (IOException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to set IL cache for {dotNetAssemblyFileAbsolutePath} with command {disassembleCommand}: {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to set IL cache for {dotNetAssemblyFileAbsolutePath} with command {disassembleCommand}: {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to set IL cache for {dotNetAssemblyFileAbsolutePath} with command {disassembleCommand}: {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to set IL cache for {dotNetAssemblyFileAbsolutePath} with command {disassembleCommand}: {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
        }

        /// <summary>
        /// 1 アセンブリについて全コマンド × 引数パターンを総当たりし、IL キャッシュヒットを確認します。
        /// いずれかのパターンでヒットした場合は <see cref="_ilCacheHits"/> をインクリメントします。
        /// </summary>
        private async Task TryHitCacheForAssemblyAsync(
            string dotNetAssemblyFileAbsolutePath,
            IList<(string disassembleCommand, string disassemblerVersion)> disassembleCommandAndItsVersionList)
        {
            var nameOnly = Path.GetFileName(dotNetAssemblyFileAbsolutePath);
            foreach (var (disassembleCommand, disassemblerVersion) in disassembleCommandAndItsVersionList)
            {
                var disassemblerFileName = Path.GetFileName(disassembleCommand);
                var patterns = BuildPrefetchCacheKeyPatterns(disassembleCommand, disassemblerFileName, dotNetAssemblyFileAbsolutePath, nameOnly);
                foreach (var pattern in patterns)
                {
                    var fullLabel = pattern + (string.IsNullOrEmpty(disassemblerVersion) ? string.Empty : $" (version: {disassemblerVersion})");
                    // キャッシュヒット時はヒット数を増やし、残りの引数パターンはスキップして次のコマンドへ。
                    if (await _ilCache.TryGetILAsync(dotNetAssemblyFileAbsolutePath, fullLabel) != null)
                    {
                        Interlocked.Increment(ref _ilCacheHits);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// プリフェッチ用のキャッシュキーパターン（コマンド＋引数の文字列）をコマンド種別に応じて列挙します。
        /// ファイル名のみ版と絶対パス版の 2 パターンを返します。
        /// </summary>
        private static IEnumerable<string> BuildPrefetchCacheKeyPatterns(
            string disassembleCommand,
            string disassemblerFileName,
            string assemblyAbsolutePath,
            string assemblyNameOnly)
        {
            if (string.Equals(disassembleCommand, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase))
            {
                // dotnet muxer は "dotnet ildasm" を正規形とし、旧表記 "dotnet dotnet-ildasm" も互換のため考慮する。
                return
                [
                    $"{Constants.DOTNET_MUXER} {Constants.ILDASM_LABEL} {assemblyNameOnly}",
                    $"{Constants.DOTNET_MUXER} {Constants.ILDASM_LABEL} {assemblyAbsolutePath}",
                    $"{Constants.DOTNET_MUXER} {Constants.DOTNET_ILDASM} {assemblyNameOnly}",
                    $"{Constants.DOTNET_MUXER} {Constants.DOTNET_ILDASM} {assemblyAbsolutePath}"
                ];
            }
            if (string.Equals(disassemblerFileName, Constants.ILSPY_CMD, StringComparison.OrdinalIgnoreCase))
            {
                // ilspycmd は -il スイッチを付与した 2 パターン。
                return [$"{disassemblerFileName} {ILSPY_FLAG_IL} {assemblyNameOnly}", $"{disassemblerFileName} {ILSPY_FLAG_IL} {assemblyAbsolutePath}"];
            }
            // その他（ildasm 等）はコマンド＋ターゲットだけで 2 パターン。
            return [$"{disassemblerFileName} {assemblyNameOnly}", $"{disassemblerFileName} {assemblyAbsolutePath}"];
        }

        /// <summary>
        /// 逆アセンブラ候補コマンドのバージョンリストを構築します。
        /// バージョン取得に失敗したコマンドは警告ログを出力してスキップされます。
        /// </summary>
        private async Task<List<(string Command, string Version)>> BuildDisassemblerVersionListAsync()
        {
            var result = new List<(string Command, string Version)>();
            foreach (var candidateCommand in CandidateDisassembleCommands())
            {
                try
                {
                    // dotnet muxer は "dotnet ildasm" 形式でバージョンを問い合わせる。
                    var versionQueryLabel = IsDotnetMuxer(candidateCommand)
                        ? $"{Constants.DOTNET_MUXER} {Constants.ILDASM_LABEL}"
                        : candidateCommand;
                    var version = await _dotNetDisassemblerCache.GetDisassemblerVersionAsync(versionQueryLabel);
                    result.Add((candidateCommand, version));
                }
                catch (InvalidOperationException)
                {
                    _logger.LogMessage(
                        AppLogLevel.Warning,
                        $"Failed to get version for disassemble command '{candidateCommand}' (candidate: '{candidateCommand}'). Skipping.",
                        shouldOutputMessageToConsole: true);
                }
            }
            return result;
        }

        /// <summary>
        /// 指定コマンドが dotnet 実行ファイルかを判定します。
        /// </summary>
        private static bool IsDotnetMuxer(string command) => string.Equals(command, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 指定コマンドが ilspycmd かを判定します。
        /// </summary>
        private static bool IsIlspyCommand(string command) => string.Equals(Path.GetFileName(command), Constants.ILSPY_CMD, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// パスに非ASCII文字がある場合に、ASCII の一時パスへコピーしたファイルのパスを返します。該当しなければ null。
        /// </summary>
        private string CreateAsciiTempCopyIfNeeded(string dotNetAssemblyFileAbsolutePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(dotNetAssemblyFileAbsolutePath) && TextSanitizer.ContainsNonAscii(dotNetAssemblyFileAbsolutePath))
                {
                    var tempAsciiPath = Path.Combine(Path.GetTempPath(), $"ildasm_input_{Guid.NewGuid():N}.dll");
                    File.Copy(dotNetAssemblyFileAbsolutePath, tempAsciiPath, overwrite: true);
                    return tempAsciiPath;
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to create ASCII temp copy for '{dotNetAssemblyFileAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
            catch (IOException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to create ASCII temp copy for '{dotNetAssemblyFileAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to create ASCII temp copy for '{dotNetAssemblyFileAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogMessage(AppLogLevel.Warning, $"Failed to create ASCII temp copy for '{dotNetAssemblyFileAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
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
                argSets.Add((disassemblerFileDirectoryAbsolutePath, isDotnetMuxer ? [Constants.ILDASM_LABEL, disassemblerFileNameOnly] : [disassemblerFileNameOnly], null));
                argSets.Add((Environment.CurrentDirectory, isDotnetMuxer ? [Constants.ILDASM_LABEL, disassemblerFileAbsolutePath] : [disassemblerFileAbsolutePath], null));
                if (!string.IsNullOrEmpty(tempAsciiPath))
                {
                    argSets.Add((Environment.CurrentDirectory, isDotnetMuxer ? [Constants.ILDASM_LABEL, tempAsciiPath] : [tempAsciiPath], null));
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
                static string MakeTempOut() => Path.Combine(Path.GetTempPath(), $"ilspy_out_{Guid.NewGuid():N}.il");
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
                var disassemblerVersion = await _dotNetDisassemblerCache.GetDisassemblerVersionAsync(disassembleCommandWithArguments);
                var disassemblerVersionOneLine = disassemblerVersion.Replace("\r", " ").Replace("\n", " ").Trim();
                return string.IsNullOrEmpty(disassemblerVersionOneLine) ? disassembleCommandWithArguments : $"{disassembleCommandWithArguments} (version: {disassemblerVersionOneLine})";
            }
            catch (InvalidOperationException)
            {
                var fingerprint = BuildToolFingerprint(disassembleCommandWithArguments);
                return $"{disassembleCommandWithArguments} (version: {UNKNOWN_VERSION_FINGERPRINT_PREFIX}{fingerprint})";
            }
        }

        /// <summary>
        /// コマンド文字列から逆アセンブラ実体のフィンガープリントを構築します。
        /// 取得できる実体がない場合は実行単位識別子を返します。
        /// </summary>
        private static string BuildToolFingerprint(string disassembleCommandWithArguments)
        {
            var tokens = ProcessHelper.TokenizeCommand(disassembleCommandWithArguments);
            if (tokens.Count == 0)
            {
                return RUN_FINGERPRINT_PREFIX + _runFingerprint;
            }

            var executableCandidates = new List<string>();
            var head = Path.GetFileName(tokens[0]) ?? tokens[0];
            if (string.Equals(head, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase) &&
                tokens.Count >= 2 &&
                string.Equals(tokens[1], Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase))
            {
                executableCandidates.Add(tokens[0]);
                executableCandidates.Add(Constants.DOTNET_ILDASM);
                executableCandidates.Add(Path.Combine(UserDotnetToolsDirectory, Constants.DOTNET_ILDASM));
            }
            else
            {
                executableCandidates.Add(tokens[0]);
            }

            var fingerprints = new List<string>();
            foreach (var executableCandidate in executableCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var resolved = ResolveExecutablePath(executableCandidate);
                if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
                {
                    continue;
                }

                var fileInfo = new FileInfo(resolved);
                fingerprints.Add($"{Path.GetFileName(resolved)}@{fileInfo.Length}:{fileInfo.LastWriteTimeUtc.Ticks}");
            }

            if (fingerprints.Count == 0)
            {
                return RUN_FINGERPRINT_PREFIX + _runFingerprint;
            }

            return string.Join("|", fingerprints.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// コマンド名から実行ファイルの絶対パスを解決します。解決できない場合は null。
        /// </summary>
        private static string ResolveExecutablePath(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return null;
            }

            if (Path.IsPathRooted(command))
            {
                return File.Exists(command) ? Path.GetFullPath(command) : null;
            }

            if (command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
            {
                var fullPath = Path.GetFullPath(command);
                return File.Exists(fullPath) ? fullPath : null;
            }

            var pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathVariable))
            {
                return null;
            }

            foreach (var pathEntry in pathVariable.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(pathEntry))
                {
                    continue;
                }
                foreach (var candidateName in EnumerateExecutableNames(command))
                {
                    var candidateAbsolutePath = Path.Combine(pathEntry, candidateName);
                    if (File.Exists(candidateAbsolutePath))
                    {
                        return Path.GetFullPath(candidateAbsolutePath);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// OS に応じて実行可能ファイル名候補を列挙します。
        /// </summary>
        private static IEnumerable<string> EnumerateExecutableNames(string command)
        {
            var hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                command
            };
            if (OperatingSystem.IsWindows())
            {
                if (!command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    hashSet.Add(command + ".exe");
                }
                if (!command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
                {
                    hashSet.Add(command + ".cmd");
                }
                if (!command.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                {
                    hashSet.Add(command + ".bat");
                }
            }
            return hashSet;
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
            catch (System.ComponentModel.Win32Exception ex)
            {
                // プロセス起動自体が失敗した場合はエラーに詰めて呼び出し元に通知。
                return (ExitCode: int.MinValue, Stdout: null, Stderr: null, Error: ex);
            }
            catch (InvalidOperationException ex)
            {
                // プロセス起動自体が失敗した場合はエラーに詰めて呼び出し元に通知。
                return (ExitCode: int.MinValue, Stdout: null, Stderr: null, Error: ex);
            }
            catch (IOException ex)
            {
                // プロセス起動自体が失敗した場合はエラーに詰めて呼び出し元に通知。
                return (ExitCode: int.MinValue, Stdout: null, Stderr: null, Error: ex);
            }
            catch (NotSupportedException ex)
            {
                // プロセス起動自体が失敗した場合はエラーに詰めて呼び出し元に通知。
                return (ExitCode: int.MinValue, Stdout: null, Stderr: null, Error: ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                // プロセス起動自体が失敗した場合はエラーに詰めて呼び出し元に通知。
                return (ExitCode: int.MinValue, Stdout: null, Stderr: null, Error: ex);
            }
        }

        /// <summary>
        /// 指定ツールがブラックリスト化されているかを判定。TTL 満了時は自動解除。
        /// </summary>
        private bool IsDisassemblerBlacklisted(string disassembleCommand)
            => _blacklist.IsBlacklisted(disassembleCommand);

        /// <summary>
        /// 指定ツールの失敗回数をインクリメントします。
        /// </summary>
        private void RegisterDisassembleFailure(string disassembleCommand)
            => _blacklist.RegisterFailure(disassembleCommand);

        /// <summary>
        /// 指定ツールの失敗カウントをリセット（ブラックリスト解除）します。
        /// </summary>
        private void ResetDisassembleFailure(string disassembleCommand)
            => _blacklist.ResetFailure(disassembleCommand);

        /// <summary>
        /// ユーザーの .NET グローバルツールディレクトリ（例: C:\Users\&lt;name&gt;\.dotnet\tools）。
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
            yield return Constants.ILSPY_CMD;
            yield return Path.Combine(UserDotnetToolsDirectory, Constants.ILSPY_CMD);
        }

        /// <summary>
        /// 使用した逆アセンブラ名/バージョンを集計します。
        /// </summary>
        private void RecordDisassemblerUsage(string disassembleCommand, string disassembleCommandAndItsVersionWithArguments, bool fromCache = false)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommandAndItsVersionWithArguments))
            {
                return;
            }

            var toolName = NormalizeDisassemblerName(disassembleCommand);
            var version = ExtractVersionFromLabel(disassembleCommandAndItsVersionWithArguments);
            _fileDiffResultLists.RecordDisassemblerToolVersion(toolName, version, fromCache);
        }

        /// <summary>
        /// コマンドからツール名を正規化します。
        /// </summary>
        private static string NormalizeDisassemblerName(string disassembleCommand)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommand))
            {
                return disassembleCommand;
            }

            var fileName = Path.GetFileName(disassembleCommand);
            if (string.Equals(fileName, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase))
            {
                return Constants.DOTNET_ILDASM;
            }
            if (string.Equals(fileName, Constants.DOTNET_ILDASM, StringComparison.OrdinalIgnoreCase))
            {
                return Constants.DOTNET_ILDASM;
            }
            if (string.Equals(fileName, Constants.ILSPY_CMD, StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ILSPY_CMD;
            }
            if (string.Equals(fileName, Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ILDASM_LABEL;
            }
            return fileName ?? disassembleCommand;
        }

        /// <summary>
        /// " (version: ...)" 形式からバージョン文字列を抽出します。
        /// </summary>
        private static string ExtractVersionFromLabel(string disassembleCommandAndItsVersionWithArguments)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommandAndItsVersionWithArguments))
            {
                return null;
            }

            if (!disassembleCommandAndItsVersionWithArguments.EndsWith(")", StringComparison.Ordinal))
            {
                return null;
            }

            var prefixIndex = disassembleCommandAndItsVersionWithArguments.LastIndexOf(VERSION_LABEL_PREFIX, StringComparison.Ordinal);
            if (prefixIndex < 0)
            {
                return null;
            }

            var start = prefixIndex + VERSION_LABEL_PREFIX.Length;
            var end = disassembleCommandAndItsVersionWithArguments.Length - 1;
            if (start >= end)
            {
                return null;
            }

            return disassembleCommandAndItsVersionWithArguments.Substring(start, end - start).Trim();
        }

        /// <summary>
        /// 2 つの逆アセンブルラベルが同一バージョン識別を持つかを判定します。
        /// </summary>
        private static bool AreSameDisassemblerVersion(string oldLabel, string newLabel)
        {
            var oldVersion = ExtractVersionFromLabel(oldLabel) ?? string.Empty;
            var newVersion = ExtractVersionFromLabel(newLabel) ?? string.Empty;
            return string.Equals(oldVersion, newVersion, StringComparison.OrdinalIgnoreCase);
        }
    }
}
