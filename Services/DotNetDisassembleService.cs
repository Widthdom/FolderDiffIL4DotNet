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
    /// Responsible for executing .NET disassembly and managing the <see cref="ILCache"/>.
    /// Handles tool version retrieval (via <see cref="DotNetDisassemblerCache"/>) and blacklist control.
    /// IL-cache prefetch is delegated to <see cref="ILCachePrefetcher"/>.
    /// Creates a temporary ASCII copy of the target file when the path contains non-ASCII characters.
    /// .NET 逆アセンブルの実行および <see cref="ILCache"/> を用いたキャッシュ取得/保存を担当するサービス。
    /// ツールのバージョン取得（<see cref="DotNetDisassemblerCache"/> 経由）・ブラックリスト制御を担当。
    /// IL キャッシュのプリフェッチは <see cref="ILCachePrefetcher"/> へ委譲します。
    /// 日本語/非ASCIIパス対策として必要に応じて ASCII 一時コピーを作成して実行します。
    /// </summary>
    public sealed class DotNetDisassembleService : IDotNetDisassembleService
    {
        /// <summary>
        /// Threshold of consecutive failures before a disassembler tool is blacklisted.
        /// Set to 3 to tolerate transient errors (file locks, brief resource contention)
        /// while still reacting promptly to a broken tool.
        /// ブラックリスト化判定に用いる連続失敗閾値。
        /// 1〜2 回の失敗は一時的な競合やファイルロックによるものが多いため、
        /// 誤ブラックリスト化を避けるため 3 回連続失敗を閾値としています。
        /// </summary>
        private const int DISASSEMBLE_FAIL_THRESHOLD = 3;

        private const string ILSPY_FLAG_IL = "-il";
        private const string ILSPY_FLAG_OUTPUT = "-o";
        private const string GUIDANCE_INSTALL_DISASSEMBLER =
            Constants.DOTNET_ILDASM + " was not found or failed to run.\n" +
            "If it's not installed, install it with:\n" +
            "  " + Constants.DOTNET_MUXER + " tool install -g " + Constants.DOTNET_ILDASM + "\n" +
            "Also ensure that ~/.dotnet/tools is included in your PATH.\n" +
            "Alternatively, you can install " + Constants.ILSPY_CMD + " and we will use it automatically:\n" +
            "  " + Constants.DOTNET_MUXER + " tool install -g " + Constants.ILSPY_CMD;

        private const string VERSION_LABEL_PREFIX = " (version: ";
        private const string UNKNOWN_VERSION_FINGERPRINT_PREFIX = "unavailable; fingerprint: ";
        private const string RUN_FINGERPRINT_PREFIX = "run:";
        private const int DEFAULT_BLACKLIST_TTL_MINUTES = 10;
        private readonly ConfigSettings _config;
        private readonly ILCache? _ilCache;
        private readonly DisassemblerBlacklist _blacklist;

        // Per-run fallback identifier to isolate disk cache when the tool binary cannot be resolved.
        // ツール実体を解決できない場合でも前回実行のキャッシュと混ざらないようにする実行単位識別子。
        private static readonly string _runFingerprint = Guid.NewGuid().ToString("N");
        private int _ilCacheHits;
        private int _ilCacheStores;
        private readonly FileDiffResultLists _fileDiffResultLists;
        private readonly ILoggerService _logger;
        private readonly DotNetDisassemblerCache _dotNetDisassemblerCache;
        private readonly ILCachePrefetcher _prefetcher;

        /// <summary>
        /// Read-only snapshot of total cache hits: disassembly hits plus prefetch hits
        /// from <see cref="ILCachePrefetcher"/>. Uses <see cref="Volatile"/> for thread safety.
        /// IL キャッシュのヒット件数（読み取り専用スナップショット）。
        /// 逆アセンブル実行時のヒット数と <see cref="ILCachePrefetcher"/> のプリフェッチヒット数の合計を返します。
        /// </summary>
        public int IlCacheHits => Volatile.Read(ref _ilCacheHits) + _prefetcher.IlCacheHits;

        /// <summary>
        /// Read-only snapshot of total cache stores. Uses <see cref="Volatile"/> for thread safety.
        /// IL キャッシュへの格納（書き込み）件数（読み取り専用スナップショット）。
        /// </summary>
        public int IlCacheStores => Volatile.Read(ref _ilCacheStores);

        public DotNetDisassembleService(ConfigSettings config, ILCache? ilCache, FileDiffResultLists fileDiffResultLists, ILoggerService logger, DotNetDisassemblerCache dotNetDisassemblerCache)
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
            _prefetcher = new ILCachePrefetcher(config, ilCache, logger, dotNetDisassemblerCache);
        }

        /// <summary>
        /// Tries each candidate disassemble command in order and returns the first successful IL result.
        /// Skips process launch on cache hit and returns a versioned command label.
        /// 設定された候補コマンドを順次試し、成功した最初の逆アセンブル結果を返します。
        /// キャッシュヒット時はプロセス起動を省略し、バージョン情報付きのラベルを返します。
        /// </summary>
        public async Task<(string ilText, string commandString)> DisassembleAsync(string dotNetAssemblyfileAbsolutePath)
        {
            Exception? lastError = null;
            foreach (var candidateDisassembleCommand in CandidateDisassembleCommands())
            {
                // Skip commands temporarily blacklisted due to consecutive failures.
                // 直近で連続失敗したコマンドは一時的にブラックリスト化しているためスキップ。
                if (IsDisassemblerBlacklisted(candidateDisassembleCommand))
                {
                    continue;
                }

                try
                {
                    var (success, ilText, disassembleCommandAndItsVersionWithArguments, error) = await TryDisassembleAsync(candidateDisassembleCommand, dotNetAssemblyfileAbsolutePath, allowCache: true, recordUsage: true);
                    if (success)
                    {
                        return (ilText!, disassembleCommandAndItsVersionWithArguments!);
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
            Exception? lastError = null;
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

                    if (!AreSameDisassemblerVersion(oldResult.DisassembleCommandAndItsVersionWithArguments!, newResult.DisassembleCommandAndItsVersionWithArguments!))
                    {
                        lastError = new InvalidOperationException($"Disassembler version mismatch for command '{candidateDisassembleCommand}'. old='{oldResult.DisassembleCommandAndItsVersionWithArguments}', new='{newResult.DisassembleCommandAndItsVersionWithArguments}'.");
                        continue;
                    }

                    RecordDisassemblerUsage(candidateDisassembleCommand, oldResult.DisassembleCommandAndItsVersionWithArguments);
                    RecordDisassemblerUsage(candidateDisassembleCommand, newResult.DisassembleCommandAndItsVersionWithArguments);
                    return (
                        oldResult.IlText!,
                        oldResult.DisassembleCommandAndItsVersionWithArguments!,
                        newResult.IlText!,
                        newResult.DisassembleCommandAndItsVersionWithArguments!);
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
        public Task PrefetchIlCacheAsync(IEnumerable<string> dotNetAssemblyFilesAbsolutePaths, int maxParallel)
            => _prefetcher.PrefetchIlCacheAsync(dotNetAssemblyFilesAbsolutePaths, maxParallel);
        /// <summary>
        /// Attempts disassembly with the given command, creating a temp ASCII path if needed
        /// and trying each argument set in turn.
        /// 指定コマンドでアセンブリの逆アセンブルを試行します。必要に応じて一時ASCIIパスを生成し、
        /// 複数の引数セットを順に試します。
        /// </summary>
        private async Task<(bool Success, string? IlText, string? DisassembleCommandAndItsVersionWithArguments, Exception? Error)> TryDisassembleAsync(
            string disassembleCommand,
            string dotNetAssemblyFileAbsolutePath,
            bool allowCache,
            bool recordUsage)
        {
            Exception? lastError = null;
            string? tempAsciiPath = CreateAsciiTempCopyIfNeeded(dotNetAssemblyFileAbsolutePath);

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
                if (tempAsciiPath != null) FileSystemUtility.DeleteFileSilent(tempAsciiPath);
            }

            return (Success: false, IlText: null, DisassembleCommandAndItsVersionWithArguments: null, Error: lastError);
        }

        /// <summary>
        /// Tries disassembly with a single argument set: cache check, process execution, cache store.
        /// 1つの引数セットで逆アセンブルを試行します。キャッシュ事前チェック→プロセス実行→キャッシュ格納までを担当。
        /// </summary>
        private async Task<(bool Success, string? IlText, string? DisassembleCommandAndItsVersionWithArguments, Exception? Error)> TryDisassembleWithArguments(
            string disassembleCommand,
            string dotNetAssemblyFileAbsolutePath,
            (string workingDirectory, string[] args, string? tempOut) argset,
            bool allowCache,
            bool recordUsage)
        {
            string? label = null;

            if (allowCache)
            {
                // Skip process launch on cache hit; label is reused on miss for later store.
                // キャッシュヒット時はプロセス起動を省略。ラベルはミス時も後続で再利用する。
                var (hit, cachedIl, computedLabel) = await TryCacheHitAsync(disassembleCommand, dotNetAssemblyFileAbsolutePath, argset.args, recordUsage);
                label = computedLabel;
                if (hit)
                {
                    return (Success: true, IlText: cachedIl, DisassembleCommandAndItsVersionWithArguments: label, Error: null);
                }
            }

            // Cache miss — launch the process to obtain the IL text.
            // キャッシュミス — プロセスを起動して IL テキストを取得する。
            var (exitCode, stdout, stderr, error) = await RunProcessAsync(disassembleCommand, argset.workingDirectory, argset.args);
            if (error != null)
            {
                // Process failed to start — blacklist and return failure.
                // プロセス起動失敗 — ブラックリストへ登録し失敗として返す。
                RegisterDisassembleFailure(disassembleCommand);
                return (Success: false, IlText: null, DisassembleCommandAndItsVersionWithArguments: null, Error: error);
            }

            if (exitCode == 0)
            {
                // Success — clear the blacklist state.
                // 正常終了したらブラックリスト状態を解除。
                ResetDisassembleFailure(disassembleCommand);
                var ilText = await ReadIlTextAfterSuccessAsync(IsIlspyCommand(disassembleCommand), argset, stdout!);
                if (string.IsNullOrEmpty(label))
                {
                    // On cache-miss path the label may not have been computed yet.
                    // キャッシュミス経路ではラベルが未取得の場合があるためここで確保。
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
                // Non-zero exit code — wrap into an exception and blacklist the tool.
                // 終了コードが非 0 の場合は例外に包んでブラックリストに登録。
                var lastError = new InvalidOperationException($"ildasm failed (exit {exitCode}) with command: {disassembleCommand} {ProcessHelper.GetUsedArgs(argset.args)} in {argset.workingDirectory}\nFile: {dotNetAssemblyFileAbsolutePath}\nStderr: {stderr}");
                RegisterDisassembleFailure(disassembleCommand);
                return (Success: false, IlText: null, DisassembleCommandAndItsVersionWithArguments: null, Error: lastError);
            }
        }

        /// <summary>
        /// Attempts an IL cache hit. Returns (true, IlText, Label) on hit, (false, null, Label) on miss,
        /// or (false, null, null) when the cache is disabled or an exception occurs.
        /// The label is returned on miss so the caller can reuse it when storing to the cache.
        /// IL キャッシュへのヒットを試みます。ミス時も Label を返すことで、
        /// 後続のキャッシュ格納でバージョン取得の再実行を省略できます。
        /// </summary>
        private async Task<(bool Hit, string? IlText, string? Label)> TryCacheHitAsync(
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
        /// Reads IL text after a successful process exit.
        /// ilspycmd reads from a temp file (deleted after read); other tools use stdout.
        /// プロセス正常終了後の IL テキストを取得します。
        /// ilspycmd は一時ファイルから読み取り、その他は stdout を使用します。
        /// </summary>
        private static async Task<string> ReadIlTextAfterSuccessAsync(
            bool isIlspy,
            (string workingDirectory, string[] args, string? tempOut) argset,
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
        /// Stores IL text in the cache and increments the store counter.
        /// Logs a warning and continues on error or when the cache is disabled.
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

        private static bool IsDotnetMuxer(string command) => DisassemblerHelper.IsDotnetMuxer(command);
        private static bool IsIlspyCommand(string command) => DisassemblerHelper.IsIlspyCommand(command);

        /// <summary>
        /// Returns a temp ASCII-path copy when the path contains non-ASCII characters; null otherwise.
        /// パスに非ASCII文字がある場合に ASCII 一時パスへコピーしたファイルのパスを返します。該当しなければ null。
        /// </summary>
        private string? CreateAsciiTempCopyIfNeeded(string dotNetAssemblyFileAbsolutePath)
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
        /// Enumerates argument sets to try, based on the command type.
        /// コマンド種別に応じた試行用の引数セットを列挙します。
        /// </summary>
        private static IEnumerable<(string workingDirectory, string[] args, string? tempOut)> BuildArgSets(string disassembleCommand, string disassemblerFileAbsolutePath, string? tempAsciiPath)
        {
            var disassemblerFileDirectoryAbsolutePath = Path.GetDirectoryName(disassemblerFileAbsolutePath) ?? Environment.CurrentDirectory;
            var disassemblerFileNameOnly = Path.GetFileName(disassemblerFileAbsolutePath);
            var isDotnetMuxer = IsDotnetMuxer(disassembleCommand);
            var isIlspy = IsIlspyCommand(disassembleCommand);

            var argSets = new List<(string workingDirectory, string[] args, string? tempOut)>();
            if (!isIlspy)
            {
                argSets.Add((disassemblerFileDirectoryAbsolutePath, isDotnetMuxer ? [Constants.ILDASM_LABEL, disassemblerFileNameOnly] : [disassemblerFileNameOnly], null));
                argSets.Add((Environment.CurrentDirectory, isDotnetMuxer ? [Constants.ILDASM_LABEL, disassemblerFileAbsolutePath] : [disassemblerFileAbsolutePath], null));
                if (!string.IsNullOrEmpty(tempAsciiPath))
                {
                    argSets.Add((Environment.CurrentDirectory, isDotnetMuxer ? [Constants.ILDASM_LABEL, tempAsciiPath] : [tempAsciiPath], null));
                }
            }
            else
            {
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
        /// Appends the tool version to the base label. Returns the base label as-is on failure.
        /// ベースラベルにツールバージョンを付加して返します。取得失敗時はそのまま返します。
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
        /// Builds a fingerprint for the disassembler binary from the command string.
        /// Falls back to the per-run identifier when no binary can be resolved.
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

        private static string? ResolveExecutablePath(string command) => DisassemblerHelper.ResolveExecutablePath(command);

        /// <summary>
        /// Launches the command, waits for exit, and returns exit code / stdout / stderr.
        /// Returns the exception instead of throwing when the process cannot start.
        /// 指定コマンドを起動して終了を待ち、終了コードと標準出力/標準エラーを返します。
        /// 起動失敗時は例外をタプルに含めて返します。
        /// </summary>
        private static async Task<(int ExitCode, string? Stdout, string? Stderr, Exception? Error)> RunProcessAsync(string disassembleCommand, string workingDirectoryAbsolutePath, string[] args)
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
            catch (System.ComponentModel.Win32Exception ex)
            {
                return (ExitCode: int.MinValue, Stdout: null, Stderr: null, Error: ex);
            }
            catch (InvalidOperationException ex)
            {
                return (ExitCode: int.MinValue, Stdout: null, Stderr: null, Error: ex);
            }
            catch (IOException ex)
            {
                return (ExitCode: int.MinValue, Stdout: null, Stderr: null, Error: ex);
            }
            catch (NotSupportedException ex)
            {
                return (ExitCode: int.MinValue, Stdout: null, Stderr: null, Error: ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                return (ExitCode: int.MinValue, Stdout: null, Stderr: null, Error: ex);
            }
        }

        private bool IsDisassemblerBlacklisted(string disassembleCommand)
            => _blacklist.IsBlacklisted(disassembleCommand);

        private void RegisterDisassembleFailure(string disassembleCommand)
            => _blacklist.RegisterFailure(disassembleCommand);

        private void ResetDisassembleFailure(string disassembleCommand)
            => _blacklist.ResetFailure(disassembleCommand);

        private static string UserDotnetToolsDirectory => DisassemblerHelper.UserDotnetToolsDirectory;

        private static IEnumerable<string> CandidateDisassembleCommands() => DisassemblerHelper.CandidateDisassembleCommands();

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

        private static string? ExtractVersionFromLabel(string disassembleCommandAndItsVersionWithArguments)
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

        private static bool AreSameDisassemblerVersion(string oldLabel, string newLabel)
        {
            var oldVersion = ExtractVersionFromLabel(oldLabel) ?? string.Empty;
            var newVersion = ExtractVersionFromLabel(newLabel) ?? string.Empty;
            return string.Equals(oldVersion, newVersion, StringComparison.OrdinalIgnoreCase);
        }
    }
}
