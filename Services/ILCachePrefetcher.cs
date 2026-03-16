using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// IL キャッシュのプリフェッチ（事前ヒット確認）処理を担当するクラス。
    /// <see cref="DotNetDisassembleService"/> から分離し、キャッシュウォームアップ専用の責務を持ちます。
    /// <para>
    /// Responsible for IL-cache prefetch (pre-hit verification).
    /// Extracted from <see cref="DotNetDisassembleService"/> so that cache warm-up has a dedicated,
    /// single-responsibility home.  Works by iterating all candidate command/argument patterns for
    /// each assembly and recording a cache hit whenever any pattern matches.
    /// </para>
    /// </summary>
    internal sealed class ILCachePrefetcher
    {
        /// <summary>
        /// ilspycmd の IL 出力を有効にするスイッチ（例: -il）
        /// </summary>
        private const string ILSPY_FLAG_IL = "-il";

        /// <summary>
        /// 設定値。IL キャッシュ利用可否等を保持します。
        /// </summary>
        private readonly ConfigSettings _config;

        /// <summary>
        /// IL 逆アセンブル結果キャッシュインスタンス。無効化されている場合は null。
        /// </summary>
        private readonly ILCache _ilCache;

        /// <summary>
        /// ログ出力サービス。
        /// </summary>
        private readonly ILoggerService _logger;

        /// <summary>
        /// 逆アセンブラバージョン取得のキャッシュサービス。
        /// </summary>
        private readonly DotNetDisassemblerCache _dotNetDisassemblerCache;

        /// <summary>
        /// プリフェッチ中のキャッシュヒット件数。
        /// </summary>
        private int _ilCacheHits;

        /// <summary>
        /// プリフェッチ中のキャッシュヒット件数（読み取り専用スナップショット）。
        /// <para>
        /// Snapshot of cache hits recorded during prefetch. Uses <see cref="Volatile.Read"/>
        /// to ensure visibility across threads.
        /// </para>
        /// </summary>
        internal int IlCacheHits => Volatile.Read(ref _ilCacheHits);

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="config">アプリケーション設定。</param>
        /// <param name="ilCache">IL キャッシュ（無効時は null）。</param>
        /// <param name="logger">ログ出力サービス。</param>
        /// <param name="dotNetDisassemblerCache">逆アセンブラバージョン取得キャッシュ。</param>
        /// <exception cref="ArgumentNullException"><paramref name="config"/>、<paramref name="logger"/>、または <paramref name="dotNetDisassemblerCache"/> が null の場合。</exception>
        internal ILCachePrefetcher(
            ConfigSettings config,
            ILCache ilCache,
            ILoggerService logger,
            DotNetDisassemblerCache dotNetDisassemblerCache)
        {
            ArgumentNullException.ThrowIfNull(config);
            _config = config;
            _ilCache = ilCache;
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            ArgumentNullException.ThrowIfNull(dotNetDisassemblerCache);
            _dotNetDisassemblerCache = dotNetDisassemblerCache;
        }

        /// <summary>
        /// 指定されたアセンブリ群の IL キャッシュを非同期で事前取得します。
        /// キャッシュが無効、または対象が空の場合は即座に返ります。
        /// <para>
        /// Iterates all given assemblies in parallel (up to <paramref name="maxParallel"/> tasks),
        /// attempting a cache hit for every candidate command/argument pattern.
        /// Each hit increments <see cref="IlCacheHits"/>.  Transient errors are logged as
        /// warnings and do not abort the remaining prefetch work.
        /// </para>
        /// </summary>
        /// <param name="dotNetAssemblyFilesAbsolutePaths">対象アセンブリファイルの絶対パス一覧。</param>
        /// <param name="maxParallel">最大並列度。1 以上でなければなりません。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxParallel"/> が 0 以下の場合。</exception>
        internal async Task PrefetchIlCacheAsync(IEnumerable<string> dotNetAssemblyFilesAbsolutePaths, int maxParallel)
        {
            if (dotNetAssemblyFilesAbsolutePaths == null || !_config.EnableILCache || _ilCache == null)
            {
                return;
            }
            if (maxParallel <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxParallel), maxParallel, Constants.ERROR_MAX_PARALLEL);
            }

            var assemblies = dotNetAssemblyFilesAbsolutePaths as ICollection<string> ?? [.. dotNetAssemblyFilesAbsolutePaths];
            if (assemblies.Count == 0)
            {
                return;
            }

            _logger.LogMessage(
                AppLogLevel.Info,
                $"Prefetch IL cache: starting for {assemblies.Count} .NET assemblies ({nameof(maxParallel)}={maxParallel})",
                shouldOutputMessageToConsole: true);

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
                    // Throttle progress log output by both a count step and elapsed time.
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

            _logger.LogMessage(AppLogLevel.Info, $"Prefetch IL cache: completed. hits={IlCacheHits}", shouldOutputMessageToConsole: true);
        }

        /// <summary>
        /// 1 アセンブリについて全コマンド × 引数パターンを総当たりし、IL キャッシュヒットを確認します。
        /// いずれかのパターンでヒットした場合は <see cref="_ilCacheHits"/> をインクリメントします。
        /// <para>
        /// Tries every command/argument pattern for a single assembly.
        /// Increments <see cref="_ilCacheHits"/> on the first hit and moves on to the next assembly.
        /// </para>
        /// </summary>
        /// <param name="dotNetAssemblyFileAbsolutePath">キャッシュヒットを確認するアセンブリの絶対パス。</param>
        /// <param name="disassembleCommandAndItsVersionList">候補コマンドとそのバージョンのリスト。</param>
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
        /// <para>
        /// Enumerates cache-key patterns for a given assembly and command.
        /// Returns two patterns per command: one using the file-name only and one using the absolute path.
        /// The dotnet muxer also includes the legacy <c>dotnet dotnet-ildasm</c> form for backward compatibility.
        /// </para>
        /// </summary>
        /// <param name="disassembleCommand">逆アセンブラコマンド文字列。</param>
        /// <param name="disassemblerFileName">コマンドのファイル名部分（Path.GetFileName 結果）。</param>
        /// <param name="assemblyAbsolutePath">対象アセンブリの絶対パス。</param>
        /// <param name="assemblyNameOnly">対象アセンブリのファイル名のみ（Path.GetFileName 結果）。</param>
        /// <returns>キャッシュキーパターン文字列の列挙。</returns>
        private static IEnumerable<string> BuildPrefetchCacheKeyPatterns(
            string disassembleCommand,
            string disassemblerFileName,
            string assemblyAbsolutePath,
            string assemblyNameOnly)
        {
            if (DisassemblerHelper.IsDotnetMuxer(disassembleCommand))
            {
                // dotnet muxer は "dotnet ildasm" を正規形とし、旧表記 "dotnet dotnet-ildasm" も互換のため考慮する。
                // Include legacy "dotnet dotnet-ildasm" form for backward compatibility.
                return
                [
                    $"{Constants.DOTNET_MUXER} {Constants.ILDASM_LABEL} {assemblyNameOnly}",
                    $"{Constants.DOTNET_MUXER} {Constants.ILDASM_LABEL} {assemblyAbsolutePath}",
                    $"{Constants.DOTNET_MUXER} {Constants.DOTNET_ILDASM} {assemblyNameOnly}",
                    $"{Constants.DOTNET_MUXER} {Constants.DOTNET_ILDASM} {assemblyAbsolutePath}"
                ];
            }
            if (DisassemblerHelper.IsIlspyCommand(disassembleCommand))
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
        /// <para>
        /// Builds a list of (command, version) pairs for all candidate commands.
        /// Commands whose version cannot be obtained are skipped with a warning.
        /// </para>
        /// </summary>
        /// <returns>取得できた候補コマンドとバージョンのリスト。</returns>
        private async Task<List<(string Command, string Version)>> BuildDisassemblerVersionListAsync()
        {
            var result = new List<(string Command, string Version)>();
            foreach (var candidateCommand in DisassemblerHelper.CandidateDisassembleCommands())
            {
                try
                {
                    var versionQueryLabel = DisassemblerHelper.IsDotnetMuxer(candidateCommand)
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
    }
}
