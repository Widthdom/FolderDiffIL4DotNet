using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace FolderDiffIL4DotNet.Plugin.Abstractions
{
    /// <summary>
    /// Entry point for a FolderDiffIL4DotNet plugin.
    /// Each plugin assembly must export exactly one <see cref="IPlugin"/> implementation.
    /// <para>
    /// FolderDiffIL4DotNet プラグインのエントリポイント。
    /// 各プラグインアセンブリは <see cref="IPlugin"/> 実装を1つだけエクスポートすること。
    /// </para>
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Descriptive metadata for this plugin.
        /// このプラグインの記述的メタデータ。
        /// </summary>
        PluginMetadata Metadata { get; }

        /// <summary>
        /// Registers services into the host DI container for this diff run.
        /// Called once per plugin before the diff pipeline starts.
        /// この差分実行のためにホスト DI コンテナにサービスを登録する。
        /// 差分パイプライン開始前にプラグインごとに1回呼ばれます。
        /// </summary>
        /// <param name="services">The host's service collection. / ホストのサービスコレクション。</param>
        /// <param name="pluginConfig">
        /// Plugin-specific configuration from <c>config.json</c> Plugins.Config section.
        /// Empty dictionary when no plugin config is provided.
        /// <c>config.json</c> の Plugins.Config セクションからのプラグイン固有設定。
        /// プラグイン設定が未指定の場合は空の辞書。
        /// </param>
        void ConfigureServices(IServiceCollection services, IReadOnlyDictionary<string, JsonElement> pluginConfig);
    }

    /// <summary>
    /// Descriptive metadata for a plugin. Immutable after construction.
    /// プラグインの記述的メタデータ。構築後は不変。
    /// </summary>
    public sealed class PluginMetadata
    {
        /// <summary>
        /// Unique plugin identifier (reverse-domain recommended, e.g. "com.example.my-plugin").
        /// 一意のプラグ���ン識別子（逆ドメイン推奨、例: "com.example.my-plugin"）。
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// Human-readable display name.
        /// 人間が読める表示名。
        /// </summary>
        public required string DisplayName { get; init; }

        /// <summary>
        /// Plugin version.
        /// プラグインバージョン。
        /// </summary>
        public required Version Version { get; init; }

        /// <summary>
        /// Minimum host application version this plugin is compatible with.
        /// このプラグインが互換性を持つホストアプリの最低バージョン。
        /// </summary>
        public required Version MinHostVersion { get; init; }

        /// <summary>
        /// Optional description.
        /// 説明（省略可）。
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Optional author name.
        /// 著者名（省略可）。
        /// </summary>
        public string? Author { get; init; }
    }
}
