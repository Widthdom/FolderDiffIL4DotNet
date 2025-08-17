using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services.Caching
{
    /// <summary>
    /// 逆アセンブラ（ildasm/ilspycmd 等）のバージョン文字列を取得し、プロセス起動コストを避けるためにキャッシュするコンポーネント。
    /// </summary>
    public static class DotNetDisassemblerCache
    {
        private enum DisassemblerKind
        {
            Unknown = 0,
            DotnetIldasm,
            Ildasm,
            Ilspy
        }

        private static readonly ConcurrentDictionary<string, string> disassemblerVersionCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// コマンドラベル（例: "dotnet ildasm file.dll"）から逆アセンブラ種別を判定し、バージョン文字列を取得して返す（キャッシュあり）。
        /// </summary>
        public static async Task<string> GetDisassemblerVersionAsync(string disassembleCommandWithArguments)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommandWithArguments))
            {
                throw new InvalidOperationException("Failed to determine disassembler version: empty command label.");
            }

            var (disassemblerKind, disassemblerVersionCacheKey, disassemblerExe) = GetDisassemblerInfo(disassembleCommandWithArguments);
            return disassemblerKind switch
            {
                DisassemblerKind.DotnetIldasm => await GetVersionForDotnetIldasmAsync(disassemblerVersionCacheKey, disassemblerExe),
                DisassemblerKind.Ildasm => await GetVersionForIldasmAsync(disassemblerVersionCacheKey, disassemblerExe),
                DisassemblerKind.Ilspy => await GetVersionForIlspyAsync(disassemblerVersionCacheKey, disassemblerExe),
                _ => throw new InvalidOperationException($"Failed to determine disassembler version for label: '{disassembleCommandWithArguments}'.")
            };
        }

        /// <summary>
        /// コマンドラベル（例: "dotnet ildasm file.dll"）から逆アセンブラ種別を判定し、バージョン文字列を取得して返す（キャッシュあり）。
        /// </summary>
        private static (DisassemblerKind disassemblerKind, string disassemblerVersionCacheKey, string disassemblerExe) GetDisassemblerInfo(string disassembleCommandWithArguments)
        {
            var tokens = Utility.TokenizeCommand(disassembleCommandWithArguments);
            if (tokens.Count == 0)
            {
                throw new InvalidOperationException($"Failed to determine disassembler version: invalid command label '{disassembleCommandWithArguments}'.");
            }

            if (string.Equals(tokens[0], Constants.DOTNET_MUXER, StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Count >= 2 && string.Equals(tokens[1], Constants.DOTNET_ILDASM, StringComparison.OrdinalIgnoreCase))
                {
                    return (disassemblerKind: DisassemblerKind.DotnetIldasm, disassemblerVersionCacheKey: $"{Constants.DOTNET_MUXER} {Constants.DOTNET_ILDASM}", disassemblerExe: Constants.DOTNET_MUXER);
                }
            }

            var exe = tokens[0];
            return Path.GetFileName(exe)?.ToLowerInvariant() switch
            {
                Constants.DOTNET_ILDASM => (disassemblerKind: DisassemblerKind.Ildasm, disassemblerVersionCacheKey: Constants.DOTNET_ILDASM, disassemblerExe: exe),
                Constants.ILSPY => (disassemblerKind: DisassemblerKind.Ilspy, disassemblerVersionCacheKey: Constants.ILSPY, disassemblerExe: exe),
                _ => (disassemblerKind: DisassemblerKind.Unknown, disassemblerVersionCacheKey: null, disassemblerExe: null)
            };
        }

        /// <summary>
        /// dotnet ildasm のバージョン情報を取得（キャッシュ）します。
        /// </summary>
        private static async Task<string> GetVersionForDotnetIldasmAsync(string disassemblerVersionCacheKey, string disassemblerExe)
        {
            var disassemblerVersion = await TryGetDisassemblerVersionAsync(disassemblerVersionCacheKey, disassemblerExe, [Constants.DOTNET_ILDASM, Constants.FLAG_VERSION_LONG]);
            if (!string.IsNullOrWhiteSpace(disassemblerVersion))
            {
                disassemblerVersionCache[disassemblerVersionCacheKey] = disassemblerVersion;
                return disassemblerVersion;
            }

            disassemblerVersion = await TryGetDisassemblerVersionAsync(disassemblerVersionCacheKey, disassemblerExe, [Constants.DOTNET_ILDASM, Constants.FLAG_VERSION_SHORT]);
            if (!string.IsNullOrWhiteSpace(disassemblerVersion))
            {
                disassemblerVersionCache[disassemblerVersionCacheKey] = disassemblerVersion;
                return disassemblerVersion;
            }

            if (disassemblerVersionCache.TryGetValue(disassemblerVersionCacheKey, out var cachedDisassemblerVersion))
            {
                return cachedDisassemblerVersion;
            }

            throw new InvalidOperationException($"Failed to obtain version string for '{Constants.DOTNET_ILDASM}' (disassemblerVersionCacheKey='{disassemblerVersionCacheKey}').");
        }

        /// <summary>
        /// ildasm のバージョン情報を取得（キャッシュ）します。
        /// </summary>
        private static async Task<string> GetVersionForIldasmAsync(string disassemblerVersionCacheKey, string disassemblerExe)
        {
            var disassemblerVersion = await TryGetDisassemblerVersionAsync(disassemblerVersionCacheKey, disassemblerExe, [Constants.FLAG_VERSION_LONG]);
            if (!string.IsNullOrWhiteSpace(disassemblerVersion))
            {
                disassemblerVersionCache[disassemblerVersionCacheKey] = disassemblerVersion;
                return disassemblerVersion;
            }

            disassemblerVersion = await TryGetDisassemblerVersionAsync(disassemblerVersionCacheKey, disassemblerExe, [Constants.FLAG_VERSION_SHORT]);
            if (!string.IsNullOrWhiteSpace(disassemblerVersion))
            {
                disassemblerVersionCache[disassemblerVersionCacheKey] = disassemblerVersion;
                return disassemblerVersion;
            }

            if (disassemblerVersionCache.TryGetValue(disassemblerVersionCacheKey, out var cachedDisassemblerVersion))
            {
                return cachedDisassemblerVersion;
            }

            throw new InvalidOperationException($"Failed to obtain version string for '{Constants.DOTNET_ILDASM}' (disassemblerVersionCacheKey='{disassemblerVersionCacheKey}').");
        }

        private static async Task<string> GetVersionForIlspyAsync(string disassemblerVersionCacheKey, string disassemblerExe)
        {
            var disassemblerVersion = await TryGetDisassemblerVersionAsync(disassemblerVersionCacheKey, disassemblerExe, [Constants.FLAG_VERSION_LONG]);
            if (!string.IsNullOrWhiteSpace(disassemblerVersion))
            {
                disassemblerVersionCache[disassemblerVersionCacheKey] = disassemblerVersion;
                return disassemblerVersion;
            }

            disassemblerVersion = await TryGetDisassemblerVersionAsync(disassemblerVersionCacheKey, disassemblerExe, [Constants.FLAG_VERSION_SHORT]);
            if (!string.IsNullOrWhiteSpace(disassemblerVersion))
            {
                disassemblerVersionCache[disassemblerVersionCacheKey] = disassemblerVersion;
                return disassemblerVersion;
            }

            disassemblerVersion = await TryGetDisassemblerVersionAsync(disassemblerVersionCacheKey, disassemblerExe, [Constants.FLAG_HELP_SHORT]);
            if (!string.IsNullOrWhiteSpace(disassemblerVersion))
            {
                var disassemblerVersionfirstLine = disassemblerVersion.Split('\n').FirstOrDefault()?.Trim();
                disassemblerVersionCache[disassemblerVersionCacheKey] = disassemblerVersionfirstLine;
                return disassemblerVersionfirstLine;
            }

            if (disassemblerVersionCache.TryGetValue(disassemblerVersionCacheKey, out var cachedDisassemblerVersion))
            {
                return cachedDisassemblerVersion;
            }

            throw new InvalidOperationException($"Failed to obtain version string for '{Constants.ILSPY}' (disassemblerVersionCacheKey='{disassemblerVersionCacheKey}').");
        }

        /// <summary>
        /// 指定された逆アセンブラのバージョン情報を取得します。
        /// </summary>
        private static async Task<string> TryGetDisassemblerVersionAsync(string disassemblerVersionCacheKey, string disassemblerExe, string[] args)
        {
            try
            {
                return await Utility.TryGetProcessOutputAsync(disassemblerExe, args);
            }
            catch (Exception ex)
            {
                LoggerService.LogMessage($"[WARNING] Failed to get version (disassemblerVersionCacheKey='{disassemblerVersionCacheKey}', disassemblerExe='{disassemblerExe}'): {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }
            return null;
        }
    }
}
