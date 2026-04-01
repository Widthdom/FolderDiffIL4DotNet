using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        /// <returns>List of successfully loaded plugins. / 正常に読み込まれたプラグインのリスト。</returns>
        internal IReadOnlyList<IPlugin> LoadPlugins(
            IReadOnlyList<string> searchPaths,
            IReadOnlySet<string> enabledPluginIds,
            Version hostVersion)
        {
            var plugins = new List<IPlugin>();

            foreach (var searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath))
                {
                    _logger.LogMessage(AppLogLevel.Info,
                        $"Plugin search path does not exist, skipping: {searchPath}",
                        shouldOutputMessageToConsole: false);
                    continue;
                }

                foreach (var pluginDir in Directory.GetDirectories(searchPath))
                {
                    var dirName = Path.GetFileName(pluginDir);
                    var candidateDll = Path.Combine(pluginDir, $"{dirName}.dll");

                    if (!File.Exists(candidateDll))
                        continue;

                    var plugin = TryLoadPlugin(candidateDll, enabledPluginIds, hostVersion);
                    if (plugin != null)
                    {
                        plugins.Add(plugin);
                    }
                }
            }

            return plugins;
        }

        /// <summary>
        /// Attempts to load a single plugin from the specified DLL path.
        /// 指定された DLL パスから単一プラグインの読み込みを試みます。
        /// </summary>
        private IPlugin? TryLoadPlugin(
            string pluginDllPath,
            IReadOnlySet<string> enabledPluginIds,
            Version hostVersion)
        {
            try
            {
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
                    $"Failed to load plugin from '{pluginDllPath}': {ex.Message}",
                    shouldOutputMessageToConsole: true, ex);
                return null;
            }
#pragma warning restore CA1031
        }
    }
}
