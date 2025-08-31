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
    /// .NET 逆アセンブルの実行および ILCache を用いたキャッシュ取得/保存を担当するサービス。
    /// ツールのバージョン取得（DotNetDisassemblerCache 経由）・ブラックリスト制御・プリフェッチ（既存キャッシュの事前ヒット確認）も担当。
    /// 日本語/非ASCIIパス対策として必要に応じて ASCII 一時コピーを作成して実行します。
    /// </summary>
    public sealed class DotNetDisassembleService
    {
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
        public int IlCacheHits => System.Threading.Volatile.Read(ref _ilCacheHits);

        /// <summary>
        /// IL キャッシュへの格納（書き込み）件数（読み取り専用スナップショット）。
        /// </summary>
        /// <remarks>
        /// 並列に更新されるため、可視性を担保する目的で Volatile.Read を使用しています。
        /// </remarks>
        public int IlCacheStores => System.Threading.Volatile.Read(ref _ilCacheStores);

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
        /// 指定アセンブリを複数のコマンド候補と引数パターンで逆アセンブルし、成功した最初の結果を返す。
        /// キャッシュヒットすればプロセス起動をスキップし、ツールバージョン情報付きラベルを返す。
        /// </summary>
        public async Task<(string ilText, string commandString)> DisassembleAsync(string dotNetAssemblyfileAbsolutePath)
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
                    LoggerService.LogMessage($"[WARNING] Failed to start disassembler tool '{candidateDisassembleCommand}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
                    RegisterDisassembleFailure(candidateDisassembleCommand);
                    continue;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    LoggerService.LogMessage($"[WARNING] Unexpected error while preparing to run '{candidateDisassembleCommand}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
                    RegisterDisassembleFailure(candidateDisassembleCommand);
                    continue;
                }
            }

            var guidance =
                $"{Constants.DOTNET_ILDASM} was not found or failed to run.\n" +
                "If it's not installed, install it with:\n" +
                $"  {Constants.DOTNET_MUXER} tool install -g {Constants.DOTNET_ILDASM}\n" +
                $"Also ensure that ~/" + Constants.DOTNET_HOME_DIRNAME + "/" + Constants.DOTNET_TOOLS_DIRNAME + " is included in your PATH.\n" +
                $"Alternatively, you can install {Constants.ILSPY} and we will use it automatically:\n" +
                $"  {Constants.DOTNET_MUXER} tool install -g {Constants.ILSPY}";
            var innerMsg = lastError != null ? $" RootCause: {lastError.Message}" : string.Empty;
            throw new InvalidOperationException($"Failed to execute ildasm for file: {dotNetAssemblyfileAbsolutePath}. {guidance}{innerMsg}", lastError);
        }

        /// <summary>
        /// 指定された .NET アセンブリ群に対して代表的な逆アセンブラコマンド × 引数パターンのキャッシュ有無を事前確認し、
        /// 既存キャッシュヒット時はヒットカウンタを加算するプリフェッチ的処理。
        /// 最適化: 入力列挙を <see cref="ICollection{T}"/> に引き上げ（必要なら ToList）、件数 0 の場合は早期 return して
        /// 以降の前処理・並列ループをスキップします（無駄な初期化/ログ/ループを避けるための微小最適化）。
        /// </summary>
        public async Task PrefetchIlCacheAsync(IEnumerable<string> dotNetAssemblyFilesAbsolutePaths, int maxParallel)
        {
            if (dotNetAssemblyFilesAbsolutePaths == null || !_config.EnableILCache || _ilCache == null)
            {
                return;
            }
            if (maxParallel <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxParallel), maxParallel, "The maximum degree of parallelism must be 1 or greater.");
            }

            var assemblies = dotNetAssemblyFilesAbsolutePaths as ICollection<string> ?? dotNetAssemblyFilesAbsolutePaths.ToList();
            if (assemblies.Count == 0)
            {
                return;
            }

            LoggerService.LogMessage($"[INFO] Prefetch IL cache: starting for {assemblies.Count} .NET assemblies (maxParallel={maxParallel})", shouldOutputMessageToConsole: true);

            var disassembleCommandAndItsVersionList = new List<(string DisassembleCommand, string DisassemblerVersion)>();
            foreach (var candidateDisassembleCommand in CandidateDisassembleCommands())
            {
                string disassembleCommand = string.Equals(candidateDisassembleCommand, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase)
                    ? $"{Constants.DOTNET_MUXER} {Constants.DOTNET_ILDASM}"
                    : (Path.GetFileName(candidateDisassembleCommand) ?? candidateDisassembleCommand);
                try
                {
                    var disassemblerVersion = await DotNetDisassemblerCache.GetDisassemblerVersionAsync(disassembleCommand);
                    if (!string.IsNullOrWhiteSpace(disassemblerVersion))
                    {
                        disassembleCommandAndItsVersionList.Add((candidateDisassembleCommand, disassemblerVersion.Replace("\r", " ").Replace("\n", " ").Trim()));
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.LogMessage($"[WARNING] Failed to get version for disassemble command '{disassembleCommand}' (candidate: '{candidateDisassembleCommand}'). Skipping.", shouldOutputMessageToConsole: false, ex);
                }
            }
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
                    // 絶対パスに加えてファイル名のみもキーにしてキャッシュを確認
                    var dotNetAssemblyNameOnly = Path.GetFileName(dotNetAssemblyFileAbsolutePath);
                    foreach (var (disassembleCommand, disassemblerVersion) in disassembleCommandAndItsVersionList)
                    {
                        IEnumerable<string> disassembleCommandsWithArguments;
                        var disassemblerFileName = Path.GetFileName(disassembleCommand);
                        if (string.Equals(disassembleCommand, Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase))
                        {
                            disassembleCommandsWithArguments = [$"{Constants.DOTNET_MUXER} {Constants.DOTNET_ILDASM} {dotNetAssemblyNameOnly}", $"{Constants.DOTNET_MUXER} {Constants.DOTNET_ILDASM} {dotNetAssemblyFileAbsolutePath}"];
                        }
                        else if (string.Equals(disassemblerFileName, Constants.ILSPY, StringComparison.OrdinalIgnoreCase))
                        {
                            disassembleCommandsWithArguments = [$"{disassemblerFileName} {Constants.ILSPY_FLAG_IL} {dotNetAssemblyNameOnly}", $"{disassemblerFileName} {Constants.ILSPY_FLAG_IL} {dotNetAssemblyFileAbsolutePath}"];
                        }
                        else
                        {
                            disassembleCommandsWithArguments = [$"{disassemblerFileName} {dotNetAssemblyNameOnly}", $"{disassemblerFileName} {dotNetAssemblyFileAbsolutePath}"];
                        }

                        foreach (var disassembleCommandWithArguments in disassembleCommandsWithArguments)
                        {
                            var disassembleCommandAndItsVersionWithArguments = disassembleCommandWithArguments + (string.IsNullOrEmpty(disassemblerVersion) ? string.Empty : $" (version: {disassemblerVersion})");
                            var cachedIL = await _ilCache.TryGetILAsync(dotNetAssemblyFileAbsolutePath, disassembleCommandAndItsVersionWithArguments);
                            if (cachedIL != null)
                            {
                                System.Threading.Interlocked.Increment(ref _ilCacheHits);
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.LogMessage($"[WARNING] Failed to prefetch IL cache for assembly '{dotNetAssemblyFileAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
                finally
                {
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
                            LoggerService.LogMessage($"[INFO] Prefetch IL cache: {done}/{assemblies.Count} ({percent}%), hits={IlCacheHits}", shouldOutputMessageToConsole: true);
                        }
                    }
                }
            });

            LoggerService.LogMessage($"[INFO] Prefetch IL cache: completed. hits={IlCacheHits}, stores={IlCacheStores}", shouldOutputMessageToConsole: true);
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

            // 事前 TryGet（ヒットなら起動回避）
            if (_config.EnableILCache && _ilCache != null)
            {
                try
                {
                    disassembleCommandAndItsVersionWithArguments = await GetDisassembleCommandAndItsVersionWithArgumentsAsync(Utility.BuildBaseLabel(disassembleCommand, argset.args));
                    var cachedIL = await _ilCache.TryGetILAsync(dotNetAssemblyFileAbsolutePath, disassembleCommandAndItsVersionWithArguments);
                    if (cachedIL != null)
                    {
                        System.Threading.Interlocked.Increment(ref _ilCacheHits);
                        return (Success: true, IlText: cachedIL, DisassembleCommandAndItsVersionWithArguments: disassembleCommandAndItsVersionWithArguments, Error: null);
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.LogMessage($"[WARNING] Failed to get IL from cache for {dotNetAssemblyFileAbsolutePath} with command {disassembleCommand}: {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
            }

            // 実行
            var (exitCode, stdout, stderr, error) = await RunProcessAsync(disassembleCommand, argset.workingDirectory, argset.args);
            if (error != null)
            {
                RegisterDisassembleFailure(disassembleCommand);
                return (Success: false, IlText: null, DisassembleCommandAndItsVersionWithArguments: null, Error: error);
            }

            if (exitCode == 0)
            {
                ResetDisassembleFailure(disassembleCommand);

                var isIlspy = IsIlspyCommand(disassembleCommand);
                if (isIlspy && !string.IsNullOrEmpty(argset.tempOut) && File.Exists(argset.tempOut))
                {
                    ilText = await File.ReadAllTextAsync(argset.tempOut);
                    Utility.DeleteFileSilent(argset.tempOut);
                }
                else
                {
                    ilText = stdout ?? string.Empty;
                }

                if (string.IsNullOrEmpty(disassembleCommandAndItsVersionWithArguments))
                {
                    disassembleCommandAndItsVersionWithArguments = await GetDisassembleCommandAndItsVersionWithArgumentsAsync(Utility.BuildBaseLabel(disassembleCommand, argset.args));
                }

                if (_config.EnableILCache && _ilCache != null)
                {
                    try
                    {
                        await _ilCache.SetILAsync(dotNetAssemblyFileAbsolutePath, disassembleCommandAndItsVersionWithArguments, ilText);
                        System.Threading.Interlocked.Increment(ref _ilCacheStores);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogMessage($"[WARNING] Failed to set IL cache for {dotNetAssemblyFileAbsolutePath} with command {disassembleCommand}: {ex.Message}", shouldOutputMessageToConsole: true, ex);
                    }
                }

                return (Success: true, IlText: ilText, DisassembleCommandAndItsVersionWithArguments: disassembleCommandAndItsVersionWithArguments, Error: null);
            }
            else
            {
                var lastError = new InvalidOperationException($"ildasm failed (exit {exitCode}) with command: {disassembleCommand} {Utility.GetUsedArgs(argset.args)} in {argset.workingDirectory}\nFile: {dotNetAssemblyFileAbsolutePath}\nStderr: {stderr}");
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
                LoggerService.LogMessage($"[WARNING] Failed to create ASCII temp copy for '{dotNetAssemblyFileAbsolutePath}': {ex.Message}", shouldOutputMessageToConsole: true, ex);
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
                argSets.Add((disassemblerFileDirectoryAbsolutePath, [Constants.ILSPY_FLAG_IL, disassemblerFileNameOnly], null));
                argSets.Add((Environment.CurrentDirectory, [Constants.ILSPY_FLAG_IL, disassemblerFileAbsolutePath], null));
                if (!string.IsNullOrEmpty(tempAsciiPath))
                {
                    argSets.Add((Environment.CurrentDirectory, [Constants.ILSPY_FLAG_IL, tempAsciiPath], null));
                }
                string MakeTempOut() => Path.Combine(Path.GetTempPath(), $"ilspy_out_{Guid.NewGuid():N}.il");
                var out1 = MakeTempOut();
                argSets.Add((disassemblerFileDirectoryAbsolutePath, [Constants.ILSPY_FLAG_IL, Constants.ILSPY_FLAG_OUTPUT, out1, disassemblerFileNameOnly], out1));
                var out2 = MakeTempOut();
                argSets.Add((Environment.CurrentDirectory, [Constants.ILSPY_FLAG_IL, Constants.ILSPY_FLAG_OUTPUT, out2, disassemblerFileAbsolutePath], out2));
                if (!string.IsNullOrEmpty(tempAsciiPath))
                {
                    var out3 = MakeTempOut();
                    argSets.Add((Environment.CurrentDirectory, [Constants.ILSPY_FLAG_IL, Constants.ILSPY_FLAG_OUTPUT, out3, tempAsciiPath], out3));
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
        /// 指定コマンドを起動して終了を待ち、終了コードと標準出力/標準エラーを返します。起動失敗時は例外を返します。
        /// </summary>
        private static async Task<(int ExitCode, string Stdout, string Stderr, Exception Error)> RunProcessAsync(string disassembleCommand, string workingDirectoryAbsolutePath, string[] args)
        {
            try
            {
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
                process.Start();
                var outTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outTask, errTask);
                await process.WaitForExitAsync();
                var stdOutput = outTask.Result;
                var errorOutput = errTask.Result;
                return (ExitCode: process.ExitCode, Stdout: stdOutput, Stderr: errorOutput, Error: null);
            }
            catch (Exception ex)
            {
                return (ExitCode: int.MinValue, Stdout: null, Stderr: null, Error: ex);
            }
        }

        /// <summary>
        /// コマンドラベルから逆アセンブラの種類・キャッシュキー・実行ファイル名を解決します。
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
            if (info.FailCount < Constants.DISASSEMBLE_FAIL_THRESHOLD)
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
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Constants.DOTNET_HOME_DIRNAME, Constants.DOTNET_TOOLS_DIRNAME);

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
