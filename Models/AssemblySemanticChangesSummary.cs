using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Summarises assembly semantic changes detected between two builds of a .NET assembly.
    /// Each change is represented as a structured <see cref="MemberChangeEntry"/>.
    /// Modified entries cover method IL body changes, access modifier changes, modifier changes,
    /// and property/field type changes.
    /// .NET アセンブリの新旧ビルド間で検出されたセマンティック変更要約を保持します。
    /// 各変更は構造化された <see cref="MemberChangeEntry"/> として表現されます。
    /// Modified エントリはメソッド IL ボディ変更、アクセス修飾子変更、修飾子変更、
    /// プロパティ/フィールドの型変更を含みます。
    /// </summary>
    public sealed class AssemblySemanticChangesSummary
    {
        /// <summary>All detected assembly semantic changes. / 検出されたすべてのセマンティック変更。</summary>
        public IReadOnlyList<MemberChangeEntry> Entries { get; init; } = [];

        /// <summary>Whether any changes were detected. / 何らかの変更が検出されたかどうか。</summary>
        public bool HasChanges => Entries.Count > 0;

        /// <summary>Number of entries with Change="Added". / Change="Added" のエントリ数。</summary>
        public int AddedCount => CountByChange("Added");

        /// <summary>Number of entries with Change="Removed". / Change="Removed" のエントリ数。</summary>
        public int RemovedCount => CountByChange("Removed");

        /// <summary>Number of entries with Change="Modified". / Change="Modified" のエントリ数。</summary>
        public int ModifiedCount => CountByChange("Modified");

        private int CountByChange(string change)
        {
            int count = 0;
            foreach (var e in Entries)
                if (string.Equals(e.Change, change, System.StringComparison.Ordinal))
                    count++;
            return count;
        }
    }
}
