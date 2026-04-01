using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// Isolated assembly load context for a single plugin.
    /// Each plugin gets its own load context to prevent dependency version conflicts
    /// between plugins and between plugins and the host.
    /// The Plugin.Abstractions assembly is shared from the host (default context).
    /// <para>
    /// 単一プラグイン用の分離アセンブリロードコンテキスト。
    /// プラグイン間およびプラグイン・ホスト間の依存関係バージョン競合を防ぐため、
    /// 各プラグインが独自のロードコンテキストを持ちます。
    /// Plugin.Abstractions アセンブリはホスト（デフォルトコンテキスト）から共有されます。
    /// </para>
    /// </summary>
    internal sealed class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        /// <summary>
        /// Initializes a new plugin load context for the assembly at <paramref name="pluginAssemblyPath"/>.
        /// <paramref name="pluginAssemblyPath"/> のアセンブリ用プラグインロードコンテキストを初期化します。
        /// </summary>
        /// <param name="pluginAssemblyPath">Absolute path to the plugin's main DLL. / プラグインのメイン DLL の絶対パス。</param>
        public PluginAssemblyLoadContext(string pluginAssemblyPath)
            : base(isCollectible: true) // Unloadable / アンロード可能
        {
            _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
        }

        /// <inheritdoc />
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Share Plugin.Abstractions from host to ensure type identity across plugin boundary
            // プラグイン境界を跨ぐ型同一性を保証するため、Plugin.Abstractions はホストから共有
            if (assemblyName.Name == "FolderDiffIL4DotNet.Plugin.Abstractions")
                return null; // Fall back to default context / デフォルトコンテキストにフォールバック

            // Also share DI abstractions from host / DI 抽象化もホストから共有
            if (assemblyName.Name == "Microsoft.Extensions.DependencyInjection.Abstractions")
                return null;

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is not null ? LoadFromAssemblyPath(path) : null;
        }
    }
}
