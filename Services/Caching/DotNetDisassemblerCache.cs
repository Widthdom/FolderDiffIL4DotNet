using System;
using System.ComponentModel;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Common;
using FolderDiffIL4DotNet.Core.Diagnostics;

namespace FolderDiffIL4DotNet.Services.Caching
{
    /// <summary>
    /// Retrieves and caches disassembler (ildasm / ilspycmd etc.) version strings to avoid repeated process-launch overhead.
    /// 逆アセンブラ（ildasm/ilspycmd 等）のバージョン文字列を取得し、プロセス起動コストを避けるためにキャッシュするコンポーネント。
    /// </summary>
    public sealed class DotNetDisassemblerCache
    {
        private const string ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION = "Failed to determine disassembler version";
        private const string ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION_EMPTY = ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION + ": empty command label.";
        private const string ERROR_FAILED_TO_GET_VERSION = "Failed to obtain version string for";
        private const string FLAG_VERSION_LONG = "--version";
        private const string FLAG_VERSION_SHORT = "-v";
        private const string FLAG_HELP_SHORT = "-h";

        private enum DisassemblerKind
        {
            Unknown = 0,
            DotnetIldasm,
            Ildasm,
            Ilspy
        }
        private readonly ConcurrentDictionary<string, string> _disassemblerVersionCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILoggerService _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="DotNetDisassemblerCache"/>.
        /// <see cref="DotNetDisassemblerCache"/> の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="logger">Logger for diagnostic output. / 診断出力用ロガー。</param>
        public DotNetDisassemblerCache(ILoggerService logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <summary>
        /// Determines the disassembler kind from a command label (e.g. "dotnet ildasm file.dll") and returns a cached version string.
        /// コマンドラベルから逆アセンブラ種別を判定し、バージョン文字列を取得して返す（キャッシュあり）。
        /// </summary>
        /// <exception cref="InvalidOperationException">コマンドラベルが無効、またはバージョン取得に失敗した場合。</exception>
        public async Task<string> GetDisassemblerVersionAsync(string disassembleCommandWithArguments)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommandWithArguments))
            {
                throw new InvalidOperationException(ERROR_FAILED_TO_DETERMINE_DISASSEMBLER_VERSION_EMPTY);
            }

            var (disassemblerKind, disassemblerVersionCacheKey, disassemblerExe) = GetDisassemblerInfo(disassembleCommandWithArguments);
            return disassemblerKind switch
            {
                DisassemblerKind.DotnetIldasm => await GetVersionForDotnetIldasmAsync(disassemblerVersionCacheKey!, disassemblerExe!),
                DisassemblerKind.Ildasm => await GetVersionForIldasmAsync(disassemblerVersionCacheKey!, disassemblerExe!),
                DisassemblerKind.Ilspy => await GetVersionForIlspyAsync(disassemblerVersionCacheKey!, disassemblerExe!),
                _ => throw new InvalidOperationException($"Failed to determine disassembler version for label: '{disassembleCommandWithArguments}'.")
            };
        }

        /// <summary>
        /// Extracts the disassembler kind, cache key, and executable from a command label.
        /// コマンドラベルから逆アセンブラ種別・キャッシュキー・実行ファイル名を抽出します。
        /// </summary>
        private static (DisassemblerKind disassemblerKind, string? disassemblerVersionCacheKey, string? disassemblerExe) GetDisassemblerInfo(string disassembleCommandWithArguments)
        {
            var tokens = ProcessHelper.TokenizeCommand(disassembleCommandWithArguments);
            if (tokens.Count == 0)
            {
                throw new InvalidOperationException($"Failed to determine disassembler version: invalid command label '{disassembleCommandWithArguments}'.");
            }

            if (string.Equals(tokens[0], Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Count >= 2 &&
                    (string.Equals(tokens[1], Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(tokens[1], Constants.DOTNET_ILDASM, StringComparison.OrdinalIgnoreCase)))
                {
                    return (disassemblerKind: DisassemblerKind.DotnetIldasm, disassemblerVersionCacheKey: $"{Constants.DOTNET_MUXER} {Constants.ILDASM_LABEL}", disassemblerExe: Constants.DOTNET_MUXER);
                }
            }

            var exe = tokens[0];
            return Path.GetFileName(exe)?.ToLowerInvariant() switch
            {
                Constants.DOTNET_ILDASM => (disassemblerKind: DisassemblerKind.Ildasm, disassemblerVersionCacheKey: Constants.DOTNET_ILDASM, disassemblerExe: exe),
                Constants.ILSPY_CMD => (disassemblerKind: DisassemblerKind.Ilspy, disassemblerVersionCacheKey: Constants.ILSPY_CMD, disassemblerExe: exe),
                _ => (disassemblerKind: DisassemblerKind.Unknown, disassemblerVersionCacheKey: null, disassemblerExe: null)
            };
        }

        /// <summary>
        /// Retrieves the version string for `dotnet ildasm` commands.
        /// `dotnet ildasm` 系コマンドのバージョン文字列を取得します。
        /// </summary>
        private Task<string> GetVersionForDotnetIldasmAsync(string disassemblerVersionCacheKey, string disassemblerExe)
        {
            var attempts = new (string[] args, bool useFirstLine)[]
            {
                ([Constants.ILDASM_LABEL, FLAG_VERSION_LONG], false),
                ([Constants.ILDASM_LABEL, FLAG_VERSION_SHORT], false),
                ([Constants.DOTNET_ILDASM, FLAG_VERSION_LONG], false),
                ([Constants.DOTNET_ILDASM, FLAG_VERSION_SHORT], false)
            };
            return GetVersionWithFallbacksAsync(disassemblerVersionCacheKey, disassemblerExe, attempts, Constants.DOTNET_ILDASM);
        }

        /// <summary>
        /// Retrieves the version string for `ildasm` commands.
        /// `ildasm` コマンドのバージョン文字列を取得します。
        /// </summary>
        private Task<string> GetVersionForIldasmAsync(string disassemblerVersionCacheKey, string disassemblerExe)
        {
            var attempts = new (string[] args, bool useFirstLine)[]
            {
                ([FLAG_VERSION_LONG], false),
                ([FLAG_VERSION_SHORT], false)
            };
            return GetVersionWithFallbacksAsync(disassemblerVersionCacheKey, disassemblerExe, attempts, Constants.ILDASM_LABEL);
        }

        /// <summary>
        /// Retrieves the version string for `ilspycmd` commands.
        /// `ilspycmd` コマンドのバージョン文字列を取得します。
        /// </summary>
        private Task<string> GetVersionForIlspyAsync(string disassemblerVersionCacheKey, string disassemblerExe)
        {
            var attempts = new (string[] args, bool useFirstLine)[]
            {
                ([FLAG_VERSION_LONG], false),
                ([FLAG_VERSION_SHORT], false),
                ([FLAG_HELP_SHORT], true)
            };
            return GetVersionWithFallbacksAsync(disassemblerVersionCacheKey, disassemblerExe, attempts, Constants.ILSPY_CMD);
        }

        /// <summary>
        /// Tries multiple argument sets and returns the first successful version string; falls back to cache on failure.
        /// 複数の引数候補でバージョン取得を試行し、最初に成功した結果を返します。
        /// </summary>
        private async Task<string> GetVersionWithFallbacksAsync(
            string disassemblerVersionCacheKey,
            string disassemblerExe,
            IEnumerable<(string[] args, bool useFirstLine)> attempts,
            string toolName)
        {
            foreach (var (args, useFirstLine) in attempts)
            {
                var rawVersion = await TryGetDisassemblerVersionAsync(disassemblerVersionCacheKey, disassemblerExe, args);
                if (string.IsNullOrWhiteSpace(rawVersion))
                {
                    continue;
                }

                var processedVersion = useFirstLine
                    ? rawVersion.Split('\n').FirstOrDefault()?.Trim()
                    : rawVersion;

                if (!string.IsNullOrWhiteSpace(processedVersion))
                {
                    _disassemblerVersionCache[disassemblerVersionCacheKey] = processedVersion;
                    return processedVersion;
                }
            }

            if (_disassemblerVersionCache.TryGetValue(disassemblerVersionCacheKey, out var cachedDisassemblerVersion))
            {
                return cachedDisassemblerVersion;
            }

            throw new InvalidOperationException($"{ERROR_FAILED_TO_GET_VERSION} '{toolName}' ({nameof(disassemblerVersionCacheKey)}='{disassemblerVersionCacheKey}').");
        }

        /// <summary>
        /// Launches the disassembler with the given arguments and attempts to capture the version string.
        /// 指定引数で逆アセンブラを起動し、バージョン文字列の取得を試みます。
        /// </summary>
        private async Task<string?> TryGetDisassemblerVersionAsync(string disassemblerVersionCacheKey, string disassemblerExe, string[] args)
        {
            try
            {
                return await ProcessHelper.TryGetProcessOutputAsync(disassemblerExe, args);
            }
            catch (Exception ex) when (ExceptionFilters.IsProcessExecutionRecoverable(ex))
            {
                _logger.LogMessage(
                    AppLogLevel.Warning,
                    $"Failed to get version ({nameof(disassemblerVersionCacheKey)}='{disassemblerVersionCacheKey}', {nameof(disassemblerExe)}='{disassemblerExe}', ExecutableIsPathRooted={DescribePathRootedState(disassemblerExe)}, ExecutableLooksPathLike={PathShapeDiagnostics.LooksLikePath(disassemblerExe)}, args='{string.Join(" ", args)}') ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true,
                    ex);
            }
            return null;
        }

        private static string DescribePathRootedState(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "Unknown";
            }

            try
            {
                return Path.IsPathRooted(path).ToString();
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                return "Unknown";
            }
        }

    }
}
