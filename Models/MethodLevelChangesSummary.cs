using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Summarises member-level changes detected between two builds of a .NET assembly.
    /// Each change is represented as a structured <see cref="MemberChangeEntry"/>.
    /// .NET アセンブリの新旧ビルド間で検出されたメンバーレベルの変更要約を保持します。
    /// 各変更は構造化された <see cref="MemberChangeEntry"/> として表現されます。
    /// </summary>
    public sealed class MethodLevelChangesSummary
    {
        /// <summary>All detected member-level changes. / 検出されたすべてのメンバーレベル変更。</summary>
        public IReadOnlyList<MemberChangeEntry> Entries { get; init; } = [];

        /// <summary>Total method count in the old assembly. / 旧アセンブリのメソッド総数。</summary>
        public int OldMethodCount { get; init; }

        /// <summary>Total method count in the new assembly. / 新アセンブリのメソッド総数。</summary>
        public int NewMethodCount { get; init; }

        /// <summary>Whether any changes were detected. / 何らかの変更が検出されたかどうか。</summary>
        public bool HasChanges => Entries.Count > 0;
    }
}
