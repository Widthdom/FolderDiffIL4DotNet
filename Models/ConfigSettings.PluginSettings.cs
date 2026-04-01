using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Plugin-related settings (partial of <see cref="ConfigSettings"/>).
    /// プラグイン関連設定（<see cref="ConfigSettings"/> の partial）。
    /// </summary>
    public sealed partial class ConfigSettings
    {
        // ── Plugin defaults / プラグインデフォルト ──────────────────────────

        /// <summary>Default plugin search paths. / デフォルトのプラグインサーチパス。</summary>
        internal static readonly string[] DefaultPluginSearchPathsValues = { "./plugins" };

        // ── Plugin properties / プラグインプロパティ ────────────────────────

        /// <summary>
        /// Directories to scan for plugin subdirectories.
        /// プラグインサブディレクトリをスキャンするディレクトリ。
        /// </summary>
        public IReadOnlyList<string> PluginSearchPaths { get; }

        /// <summary>
        /// Plugin IDs to load. Empty list means load all found plugins.
        /// 読み込むプラグイン ID。空リストの場合は見つかった全プラグインを読み込む。
        /// </summary>
        public IReadOnlyList<string> PluginEnabledIds { get; }

        /// <summary>
        /// Per-plugin configuration as raw JSON. Key is the plugin ID.
        /// プラグインごとの設定（生JSON）。キーはプラグイン ID。
        /// </summary>
        public IReadOnlyDictionary<string, System.Text.Json.JsonElement> PluginConfig { get; }

        // Factory methods / ファクトリメソッド
        internal static List<string> CreateDefaultPluginSearchPaths() => new(DefaultPluginSearchPathsValues);
    }
}
