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

        /// <summary>Default plugin search paths. Empty means plugins are opt-in. / デフォルトのプラグインサーチパス。空はプラグインが opt-in であることを意味します。</summary>
        internal static readonly string[] DefaultPluginSearchPathsValues = System.Array.Empty<string>();

        /// <summary>Default value for <see cref="PluginStrictMode"/>. / <see cref="PluginStrictMode"/> の既定値。</summary>
        public const bool DefaultPluginStrictMode = true;

        // ── Plugin properties / プラグインプロパティ ────────────────────────

        /// <summary>
        /// Directories to scan for plugin subdirectories.
        /// プラグインサブディレクトリをスキャンするディレクトリ。
        /// </summary>
        public IReadOnlyList<string> PluginSearchPaths { get; }

        /// <summary>
        /// Plugin IDs to load from the configured search paths. Empty list means load all found plugins only after search paths are explicitly configured.
        /// 設定済み検索パスから読み込むプラグイン ID。空リストは、検索パスを明示設定した場合に限り見つかった全プラグインを読み込む。
        /// </summary>
        public IReadOnlyList<string> PluginEnabledIds { get; }

        /// <summary>
        /// Per-plugin configuration as raw JSON. Key is the plugin ID.
        /// プラグインごとの設定（生JSON）。キーはプラグイン ID。
        /// </summary>
        public IReadOnlyDictionary<string, System.Text.Json.JsonElement> PluginConfig { get; }

        /// <summary>
        /// When true, only plugins whose DLL SHA-256 hash appears in <see cref="PluginTrustedHashes"/>
        /// are loaded. Untrusted DLLs are logged as warnings and skipped.
        /// true の場合、<see cref="PluginTrustedHashes"/> に DLL の SHA-256 ハッシュが含まれる
        /// プラグインのみを読み込む。信頼されていない DLL は警告ログに記録しスキップする。
        /// </summary>
        public bool PluginStrictMode { get; }

        /// <summary>
        /// Map of plugin ID to expected SHA-256 hash (hex, case-insensitive).
        /// Only used when <see cref="PluginStrictMode"/> is true.
        /// プラグイン ID から期待される SHA-256 ハッシュ（16進数、大文字小文字不問）へのマップ。
        /// <see cref="PluginStrictMode"/> が true の場合のみ使用。
        /// </summary>
        public IReadOnlyDictionary<string, string> PluginTrustedHashes { get; }

        // Factory methods / ファクトリメソッド
        internal static List<string> CreateDefaultPluginSearchPaths() => new(DefaultPluginSearchPathsValues);
    }
}
