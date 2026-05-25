using System.Collections.Generic;
using System.Linq;

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

        /// <summary>Number of entries with Importance=High. / Importance=High のエントリ数。</summary>
        public int HighImportanceCount => CountByImportance(ChangeImportance.High);

        /// <summary>Number of entries with Importance=Medium. / Importance=Medium のエントリ数。</summary>
        public int MediumImportanceCount => CountByImportance(ChangeImportance.Medium);

        /// <summary>Number of entries with Importance=Low. / Importance=Low のエントリ数。</summary>
        public int LowImportanceCount => CountByImportance(ChangeImportance.Low);

        /// <summary>
        /// The highest importance level among all entries, or <see cref="ChangeImportance.Low"/> if empty.
        /// 全エントリ中の最高重要度レベル。空の場合は <see cref="ChangeImportance.Low"/>。
        /// </summary>
        public ChangeImportance MaxImportance
        {
            get
            {
                var max = ChangeImportance.Low;
                foreach (var e in Entries)
                    if (e.Importance > max) max = e.Importance;
                return max;
            }
        }

        /// <summary>
        /// Returns entries sorted by Change (Added → Removed → Modified),
        /// then by Importance descending (High → Medium → Low), then by original order.
        /// Change（Added → Removed → Modified）、次に Importance 降順（High → Medium → Low）でソートされたエントリを返します。
        /// </summary>
        public IReadOnlyList<MemberChangeEntry> EntriesByImportance =>
            Entries
                .OrderBy(e => ChangeOrder(e.Change))
                .ThenByDescending(e => e.Importance)
                .ToList();

        private static int ChangeOrder(string change)
            => change switch { "Added" => 0, "Removed" => 1, "Modified" => 2, _ => 3 };

        private int CountByChange(string change)
        {
            int count = 0;
            foreach (var e in Entries)
                if (string.Equals(e.Change, change, System.StringComparison.Ordinal))
                    count++;
            return count;
        }

        private int CountByImportance(ChangeImportance importance)
        {
            int count = 0;
            foreach (var e in Entries)
                if (e.Importance == importance)
                    count++;
            return count;
        }

        /// <summary>
        /// Returns a compact change delta summary by member kind (e.g. "+2 methods, -1 type, *3 methods").
        /// Added entries produce "+" prefix, Removed produce "-", Modified produce "*".
        /// メンバー種別ごとの変更差分サマリーを返します（例: "+2 methods, -1 type, *3 methods"）。
        /// Added は "+" プレフィックス、Removed は "-"、Modified は "*"。
        /// </summary>
        /// <returns>List of (prefix, count, kindLabel) tuples in order: Added, Removed, Modified. / Added→Removed→Modified順の (prefix, count, kindLabel) タプルリスト。</returns>
        public IReadOnlyList<(string Prefix, int Count, string KindLabel)> GetChangeDeltaParts()
        {
            var result = new List<(string, int, string)>();
            var groups = new Dictionary<(string Change, string Kind), int>();

            foreach (var e in Entries)
            {
                // Normalize kind to a display group / 種別を表示グループに正規化
                string kind = NormalizeKindGroup(e.MemberKind);
                var key = (e.Change, kind);
                groups.TryGetValue(key, out int count);
                groups[key] = count + 1;
            }

            // Emit in order: Added, Removed, Modified / Added→Removed→Modified の順に出力
            foreach (var change in new[] { "Added", "Removed", "Modified" })
            {
                string prefix = change switch { "Added" => "+", "Removed" => "-", _ => "*" };
                foreach (var kv in groups)
                {
                    if (kv.Key.Change != change) continue;
                    string plural = kv.Value == 1 ? kv.Key.Kind : kv.Key.Kind + "s";
                    result.Add((prefix, kv.Value, plural));
                }
            }

            return result;
        }

        /// <summary>
        /// Maps specific member kinds to broader display groups.
        /// 個別メンバー種別を広義の表示グループにマップします。
        /// </summary>
        private static string NormalizeKindGroup(string memberKind)
            => memberKind switch
            {
                "Class" or "Record" or "Struct" or "Interface" or "Enum" => "type",
                "Constructor" or "StaticConstructor" or "Method" => "method",
                "Property" => "property",
                "Field" => "field",
                _ => memberKind.ToLowerInvariant()
            };
    }
}
