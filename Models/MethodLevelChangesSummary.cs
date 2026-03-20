using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Summarises member-level changes detected between two builds of a .NET assembly,
    /// including types, methods, properties, and fields.
    /// .NET アセンブリの新旧ビルド間で検出されたメンバーレベルの変更要約を保持します。
    /// 型・メソッド・プロパティ・フィールドを含みます。
    /// </summary>
    public sealed class MethodLevelChangesSummary
    {
        // ── Types / 型 ──────────────────────────────────────────────────────

        /// <summary>Types added in the new assembly. / 新アセンブリで追加された型。</summary>
        public IReadOnlyList<string> AddedTypes { get; init; } = [];

        /// <summary>Types removed from the old assembly. / 旧アセンブリから削除された型。</summary>
        public IReadOnlyList<string> RemovedTypes { get; init; } = [];

        // ── Methods / メソッド ───────────────────────────────────────────────

        /// <summary>Total method count in the old assembly. / 旧アセンブリのメソッド総数。</summary>
        public int OldMethodCount { get; init; }

        /// <summary>Total method count in the new assembly. / 新アセンブリのメソッド総数。</summary>
        public int NewMethodCount { get; init; }

        /// <summary>Methods added in the new assembly (all access modifiers). / 新アセンブリで追加されたメソッド（全アクセス修飾子）。</summary>
        public IReadOnlyList<string> AddedMethods { get; init; } = [];

        /// <summary>Methods removed from the old assembly (all access modifiers). / 旧アセンブリから削除されたメソッド（全アクセス修飾子）。</summary>
        public IReadOnlyList<string> RemovedMethods { get; init; } = [];

        /// <summary>Methods whose IL body bytes differ between old and new. / IL ボディバイト列が新旧で異なるメソッド。</summary>
        public IReadOnlyList<string> BodyChangedMethods { get; init; } = [];

        // ── Properties / プロパティ ──────────────────────────────────────────

        /// <summary>Properties added in the new assembly. / 新アセンブリで追加されたプロパティ。</summary>
        public IReadOnlyList<string> AddedProperties { get; init; } = [];

        /// <summary>Properties removed from the old assembly. / 旧アセンブリから削除されたプロパティ。</summary>
        public IReadOnlyList<string> RemovedProperties { get; init; } = [];

        // ── Fields / フィールド ──────────────────────────────────────────────

        /// <summary>Fields added in the new assembly. / 新アセンブリで追加されたフィールド。</summary>
        public IReadOnlyList<string> AddedFields { get; init; } = [];

        /// <summary>Fields removed from the old assembly. / 旧アセンブリから削除されたフィールド。</summary>
        public IReadOnlyList<string> RemovedFields { get; init; } = [];

        /// <summary>Whether any changes were detected. / 何らかの変更が検出されたかどうか。</summary>
        public bool HasChanges =>
            AddedTypes.Count > 0 ||
            RemovedTypes.Count > 0 ||
            AddedMethods.Count > 0 ||
            RemovedMethods.Count > 0 ||
            BodyChangedMethods.Count > 0 ||
            AddedProperties.Count > 0 ||
            RemovedProperties.Count > 0 ||
            AddedFields.Count > 0 ||
            RemovedFields.Count > 0;
    }
}
