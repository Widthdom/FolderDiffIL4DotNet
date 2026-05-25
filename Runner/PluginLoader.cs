using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Plugin.Abstractions;
using FolderDiffIL4DotNet.Services;

namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// Discovers and loads plugin assemblies from configured search paths.
    /// Each plugin DLL is loaded in an isolated <see cref="PluginAssemblyLoadContext"/>
    /// to prevent dependency conflicts.
    /// <para>
    /// 設定されたサーチパスからプラグインアセンブリを発見・読み込みするローダー。
    /// 各プラグイン DLL は依存関係の競合を防ぐため、分離された
    /// <see cref="PluginAssemblyLoadContext"/> で読み込まれます。
    /// </para>
    /// </summary>
    internal sealed class PluginLoader
    {
        private readonly ILoggerService _logger;

        internal PluginLoader(ILoggerService logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <summary>
        /// Loads all enabled plugins from the specified search paths.
        /// 指定されたサーチパスから有効なプラグインをすべて読み込みます。
        /// </summary>
        /// <param name="searchPaths">Directories to scan for plugin subdirectories. / プラグインサブディレクトリをスキャンするディレクトリ。</param>
        /// <param name="enabledPluginIds">Set of plugin IDs to load. Empty = load all found. / 読み込むプラグイン ID のセット。空 = 見つかった全プラグインを読み込む。</param>
        /// <param name="hostVersion">Current host application version for compatibility check. / 互換性チェック用の現在のホストアプリバージョン。</param>
        /// <param name="strictMode">When true, only plugins whose SHA-256 hash matches <paramref name="trustedHashes"/> are loaded. / true の場合、SHA-256 ハッシュが一致するプラグインのみ読み込む。</param>
        /// <param name="trustedHashes">Map of plugin ID to trusted SHA-256 hash (hex). / プラグイン ID から信頼済み SHA-256 ハッシュへのマップ。</param>
        /// <returns>List of successfully loaded plugins. / 正常に読み込まれたプラグインのリスト。</returns>
        internal IReadOnlyList<IPlugin> LoadPlugins(
            IReadOnlyList<string> searchPaths,
            IReadOnlySet<string> enabledPluginIds,
            Version hostVersion,
            bool strictMode = false,
            IReadOnlyDictionary<string, string>? trustedHashes = null)
        {
            var plugins = new List<IPlugin>();
            var processedPluginDllPaths = new HashSet<string>(GetPluginPathComparer());

            foreach (var searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath))
                {
                    _logger.LogMessage(AppLogLevel.Info,
                        $"Plugin search path does not exist, skipping: {searchPath}",
                        shouldOutputMessageToConsole: false);
                    continue;
                }

                foreach (var pluginDir in TryEnumeratePluginDirectories(searchPath))
                {
                    var dirName = Path.GetFileName(pluginDir);
                    var candidateDll = Path.GetFullPath(Path.Combine(pluginDir, $"{dirName}.dll"));

                    if (!File.Exists(candidateDll))
                        continue;

                    if (!processedPluginDllPaths.Add(candidateDll))
                    {
                        _logger.LogMessage(AppLogLevel.Info,
                            $"Plugin DLL already processed from another search path, skipping duplicate: {candidateDll}",
                            shouldOutputMessageToConsole: false);
                        continue;
                    }

                    var plugin = TryLoadPlugin(candidateDll, enabledPluginIds, hostVersion, strictMode, trustedHashes);
                    if (plugin != null)
                    {
                        plugins.Add(plugin);
                    }
                }
            }

            return plugins;
        }

        private IEnumerable<string> TryEnumeratePluginDirectories(string searchPath)
        {
            try
            {
                return Directory.GetDirectories(searchPath);
            }
            catch (Exception ex) when (ex is DirectoryNotFoundException or IOException
                or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Failed to enumerate plugin search path '{searchPath}' ({PathShapeDiagnostics.DescribeState("PluginSearchPath", searchPath)}, {ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true, ex);
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Attempts to load a single plugin from the specified DLL path.
        /// 指定された DLL パスから単一プラグインの読み込みを試みます。
        /// </summary>
        private IPlugin? TryLoadPlugin(
            string pluginDllPath,
            IReadOnlySet<string> enabledPluginIds,
            Version hostVersion,
            bool strictMode,
            IReadOnlyDictionary<string, string>? trustedHashes)
        {
            try
            {
                string? actualHash = null;
                if (strictMode && !VerifyPluginHashBeforeLoad(pluginDllPath, trustedHashes, out actualHash))
                {
                    return null;
                }

                var loadContext = new PluginAssemblyLoadContext(pluginDllPath);
                var assembly = loadContext.LoadFromAssemblyPath(pluginDllPath);

                var pluginType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

                if (pluginType is null)
                {
                    _logger.LogMessage(AppLogLevel.Warning,
                        $"Plugin DLL '{pluginDllPath}' does not export an IPlugin implementation, skipping.",
                        shouldOutputMessageToConsole: false);
                    return null;
                }

                var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                var metadata = plugin.Metadata;

                // Filter by enabled list (empty = all enabled)
                // 有効リストでフィルタ（空 = すべて有効）
                if (enabledPluginIds.Count > 0 && !enabledPluginIds.Contains(metadata.Id))
                {
                    _logger.LogMessage(AppLogLevel.Info,
                        $"Plugin '{metadata.Id}' found but not in enabled list, skipping.",
                        shouldOutputMessageToConsole: false);
                    return null;
                }

                // Strict mode: verify DLL SHA-256 hash against trusted allowlist
                // 厳格モード: DLL の SHA-256 ハッシュを信頼済みリストと照合
                if (strictMode)
                {
                    if (!VerifyPluginHash(metadata.Id, actualHash!, trustedHashes))
                    {
                        return null;
                    }
                }

                // Host version compatibility check / ホストバージョン互換性チェック
                if (metadata.MinHostVersion > hostVersion)
                {
                    _logger.LogMessage(AppLogLevel.Warning,
                        $"Plugin '{metadata.Id}' requires host version >= {metadata.MinHostVersion} but current is {hostVersion}, skipping.",
                        shouldOutputMessageToConsole: true);
                    return null;
                }

                _logger.LogMessage(AppLogLevel.Info,
                    $"Loaded plugin: {metadata.DisplayName} v{metadata.Version} ({metadata.Id})",
                    shouldOutputMessageToConsole: true);

                return plugin;
            }
#pragma warning disable CA1031 // Plugin loading is best-effort / プラグイン読み込みはベストエフォート
            catch (Exception ex)
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Failed to load plugin from '{pluginDllPath}' ({PathShapeDiagnostics.DescribeState("PluginDllPath", pluginDllPath)}, {ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true, ex);
                return null;
            }
#pragma warning restore CA1031
        }

        /// <summary>
        /// Computes the SHA-256 hash of the plugin DLL before loading it and verifies that the hash
        /// exists somewhere in the trusted hash map. This prevents untrusted assemblies from being
        /// loaded at all when strict mode is enabled.
        /// プラグイン DLL の SHA-256 ハッシュを読み込み前に計算し、そのハッシュが
        /// 信頼済みハッシュマップのどこかに存在することを検証する。
        /// これにより、strict mode 有効時に未信頼アセンブリがそもそもロードされることを防ぐ。
        /// </summary>
        private bool VerifyPluginHashBeforeLoad(
            string pluginDllPath,
            IReadOnlyDictionary<string, string>? trustedHashes,
            out string? actualHash)
        {
            actualHash = null;

            if (trustedHashes == null || trustedHashes.Count == 0)
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Plugin DLL '{pluginDllPath}' rejected before load: strict mode is enabled but no trusted hashes are configured.",
                    shouldOutputMessageToConsole: true);
                return false;
            }

            var computedHash = ComputePluginHash(pluginDllPath);
            actualHash = computedHash;

            if (!trustedHashes.Values.Any(expectedHash =>
                    string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Plugin DLL '{pluginDllPath}' rejected before load: SHA-256 hash '{computedHash}' is not present in PluginTrustedHashes.",
                    shouldOutputMessageToConsole: true);
                return false;
            }

            _logger.LogMessage(AppLogLevel.Info,
                $"Plugin DLL '{pluginDllPath}' passed pre-load SHA-256 verification.",
                shouldOutputMessageToConsole: false);
            return true;
        }

        /// <summary>
        /// Verifies that the already-trusted DLL hash matches the entry configured for the resolved plugin ID.
        /// This catches misconfiguration without recomputing the file hash.
        /// すでに信頼済みと判定された DLL ハッシュが、解決済みプラグイン ID に対して
        /// 設定された値と一致することを検証する。これにより、ファイルハッシュを再計算せずに
        /// 設定ミスを検出する。
        /// </summary>
        private bool VerifyPluginHash(
            string pluginId,
            string actualHash,
            IReadOnlyDictionary<string, string>? trustedHashes)
        {
            if (trustedHashes == null || !trustedHashes.TryGetValue(pluginId, out var expectedHash))
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Plugin '{pluginId}' rejected: strict mode is enabled but no trusted hash is configured for this plugin ID.",
                    shouldOutputMessageToConsole: true);
                return false;
            }

            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Plugin '{pluginId}' rejected: SHA-256 hash mismatch. Expected '{expectedHash}', actual '{actualHash}'.",
                    shouldOutputMessageToConsole: true);
                return false;
            }

            _logger.LogMessage(AppLogLevel.Info,
                $"Plugin '{pluginId}' passed SHA-256 hash verification.",
                shouldOutputMessageToConsole: false);
            return true;
        }

        private static string ComputePluginHash(string pluginDllPath)
        {
            using var stream = File.OpenRead(pluginDllPath);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static StringComparer GetPluginPathComparer()
            => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    }
}
