using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Responsible for IL-cache prefetch (pre-hit verification).
    /// Extracted from <see cref="DotNetDisassembleService"/> so that cache warm-up has a dedicated,
    /// single-responsibility home.  Works by iterating all candidate command/argument patterns for
    /// each assembly and recording a cache hit whenever any pattern matches.
    /// IL キャッシュのプリフェッチ（事前ヒット確認）処理を担当するクラス。
    /// <see cref="DotNetDisassembleService"/> から分離し、キャッシュウォームアップ専用の責務を持ちます。
    /// </summary>
    internal sealed class ILCachePrefetcher
    {
        private const string ILSPY_FLAG_IL = "-il";
        private readonly IReadOnlyConfigSettings _config;
        private readonly ILCache? _ilCache;
        private readonly ILoggerService _logger;
        private readonly DotNetDisassemblerCache _dotNetDisassemblerCache;
        private int _ilCacheHits;

        /// <summary>
        /// Thread-safe snapshot of cache hits recorded during prefetch. Uses <see cref="Volatile"/> to ensure cross-thread visibility.
        /// プリフェッチ中のキャッシュヒット件数（読み取り専用スナップショット）。<see cref="Volatile"/> でスレッド間可視性を保証。
        /// </summary>
        internal int IlCacheHits => Volatile.Read(ref _ilCacheHits);

        /// <exception cref="ArgumentNullException"><paramref name="config"/>, <paramref name="logger"/>, or <paramref name="dotNetDisassemblerCache"/> is null. / <paramref name="config"/>、<paramref name="logger"/>、または <paramref name="dotNetDisassemblerCache"/> が null の場合。</exception>
        internal ILCachePrefetcher(
            IReadOnlyConfigSettings config,
            ILCache? ilCache,
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
        /// Asynchronously prefetches IL cache for the given assemblies. Returns immediately when the cache is disabled or the set is empty.
        /// Iterates all given assemblies in parallel (up to <paramref name="maxParallel"/> tasks),
        /// attempting a cache hit for every candidate command/argument pattern.
        /// Each hit increments <see cref="IlCacheHits"/>.  Transient errors are logged as
        /// warnings and do not abort the remaining prefetch work.
        /// 指定されたアセンブリ群の IL キャッシュを非同期で事前取得します。
        /// キャッシュが無効、または対象が空の場合は即座に返ります。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxParallel"/> is 0 or negative. / <paramref name="maxParallel"/> が 0 以下の場合。</exception>
        internal async Task PrefetchIlCacheAsync(IEnumerable<string> dotNetAssemblyFilesAbsolutePaths, int maxParallel, CancellationToken cancellationToken = default)
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

            await Parallel.ForEachAsync(assemblies, new ParallelOptions { MaxDegreeOfParallelism = maxParallel, CancellationToken = cancellationToken }, async (dotNetAssemblyFileAbsolutePath, ct) =>
            {
                try
                {
                    await TryHitCacheForAssemblyAsync(dotNetAssemblyFileAbsolutePath, disassembleCommandAndItsVersionList);
                }
                catch (Exception ex) when (ExceptionFilters.IsFileIoOrOperationRecoverable(ex))
                {
                    _logger.LogMessage(AppLogLevel.Warning, $"Failed to prefetch IL cache for assembly '{dotNetAssemblyFileAbsolutePath}' ({ex.GetType().Name}): {ex.Message}", shouldOutputMessageToConsole: true, ex);
                }
                finally
                {
                    // Throttle progress log output by both a count step and elapsed time.
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

            _logger.LogMessage(AppLogLevel.Info, $"Prefetch IL cache: completed. hits={IlCacheHits}", shouldOutputMessageToConsole: true);
        }

        /// <summary>
        /// Tries every command/argument pattern for a single assembly and checks for IL cache hits.
        /// Increments <see cref="_ilCacheHits"/> on the first hit and moves on to the next assembly.
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
                    if (_ilCache != null && await _ilCache.TryGetILAsync(dotNetAssemblyFileAbsolutePath, fullLabel) != null)
                    {
                        Interlocked.Increment(ref _ilCacheHits);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Enumerates cache-key patterns (command + arguments) for a given assembly and command.
        /// Returns two patterns per command: one using the file-name only and one using the absolute path.
        /// The dotnet muxer also includes the legacy <c>dotnet dotnet-ildasm</c> form for backward compatibility.
        /// プリフェッチ用のキャッシュキーパターン（コマンド＋引数の文字列）をコマンド種別に応じて列挙します。
        /// ファイル名のみ版と絶対パス版の 2 パターンを返します。
        /// </summary>
        private static IEnumerable<string> BuildPrefetchCacheKeyPatterns(
            string disassembleCommand,
            string disassemblerFileName,
            string assemblyAbsolutePath,
            string assemblyNameOnly)
        {
            if (DisassemblerHelper.IsDotnetMuxer(disassembleCommand))
            {
                // Include legacy "dotnet dotnet-ildasm" form for backward compatibility.
                // dotnet muxer は "dotnet ildasm" を正規形とし、旧表記 "dotnet dotnet-ildasm" も互換のため考慮する。
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
                // ilspycmd uses two patterns with the -il switch.
                // ilspycmd は -il スイッチを付与した 2 パターン。
                return [$"{disassemblerFileName} {ILSPY_FLAG_IL} {assemblyNameOnly}", $"{disassemblerFileName} {ILSPY_FLAG_IL} {assemblyAbsolutePath}"];
            }
            // Others (ildasm, etc.) use two patterns with command + target only.
            // その他（ildasm 等）はコマンド＋ターゲットだけで 2 パターン。
            return [$"{disassemblerFileName} {assemblyNameOnly}", $"{disassemblerFileName} {assemblyAbsolutePath}"];
        }

        /// <summary>
        /// Builds a list of (command, version) pairs for all candidate disassembler commands.
        /// Commands whose version cannot be obtained are skipped with a warning.
        /// 逆アセンブラ候補コマンドのバージョンリストを構築します。
        /// バージョン取得に失敗したコマンドは警告ログを出力してスキップされます。
        /// </summary>
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
