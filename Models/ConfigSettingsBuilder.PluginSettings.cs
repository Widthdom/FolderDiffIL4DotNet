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

        /// <summary>
        /// When true, only plugins whose DLL SHA-256 hash matches <see cref="PluginTrustedHashes"/> are loaded.
        /// true の場合、DLL の SHA-256 ハッシュが <see cref="PluginTrustedHashes"/> と一致するプラグインのみを読み込む。
        /// </summary>
        [JsonPropertyName("PluginStrictMode")]
        public bool PluginStrictMode { get; set; }

        /// <summary>
        /// Map of plugin ID to expected SHA-256 hash (hex). Used when <see cref="PluginStrictMode"/> is true.
        /// プラグイン ID から期待される SHA-256 ハッシュ（16進数）へのマップ。<see cref="PluginStrictMode"/> が true の場合に使用。
        /// </summary>
        [JsonPropertyName("PluginTrustedHashes")]
        public Dictionary<string, string> PluginTrustedHashes { get; set; } = new();
    }
}
