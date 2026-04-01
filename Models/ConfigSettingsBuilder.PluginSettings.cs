using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Plugin-related settings (partial of <see cref="ConfigSettingsBuilder"/>).
    /// プラグイン関連設定（<see cref="ConfigSettingsBuilder"/> の partial）。
    /// </summary>
    public sealed partial class ConfigSettingsBuilder
    {
        /// <summary>
        /// Directories to scan for plugin subdirectories.
        /// プラグインサブディレクトリをスキャンするディレクトリ。
        /// </summary>
        [JsonPropertyName("PluginSearchPaths")]
        public List<string> PluginSearchPaths { get; set; } = ConfigSettings.CreateDefaultPluginSearchPaths();

        /// <summary>
        /// Plugin IDs to load. Empty list means load all found plugins.
        /// 読み込むプラグイン ID。空リストの場合は見つかった全プラグインを読み込む。
        /// </summary>
        [JsonPropertyName("PluginEnabledIds")]
        public List<string> PluginEnabledIds { get; set; } = new();

        /// <summary>
        /// Per-plugin configuration as raw JSON. Key is the plugin ID.
        /// プラグインごとの設定（生JSON）。キーはプラグイン ID。
        /// </summary>
        [JsonPropertyName("PluginConfig")]
        public Dictionary<string, JsonElement> PluginConfig { get; set; } = new();
    }
}
