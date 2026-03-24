using System.Collections.Generic;
using System.Linq;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Summarises dependency changes detected between two .deps.json files.
    /// Each change is represented as a structured <see cref="DependencyChangeEntry"/>.
    /// 2 つの .deps.json ファイル間で検出された依存関係変更の要約を保持します。
    /// 各変更は構造化された <see cref="DependencyChangeEntry"/> として表現されます。
    /// </summary>
    public sealed class DependencyChangeSummary
    {
        /// <summary>All detected dependency changes. / 検出されたすべての依存関係変更。</summary>
        public IReadOnlyList<DependencyChangeEntry> Entries { get; init; } = [];

        /// <summary>Whether any changes were detected. / 何らかの変更が検出されたかどうか。</summary>
        public bool HasChanges => Entries.Count > 0;

        /// <summary>Number of entries with Change="Added". / Change="Added" のエントリ数。</summary>
        public int AddedCount => CountByChange("Added");

        /// <summary>Number of entries with Change="Removed". / Change="Removed" のエントリ数。</summary>
        public int RemovedCount => CountByChange("Removed");

        /// <summary>Number of entries with Change="Updated". / Change="Updated" のエントリ数。</summary>
        public int UpdatedCount => CountByChange("Updated");

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
        /// Returns entries sorted by Change (Added → Removed → Updated),
        /// then by Importance descending (High → Medium → Low), then by package name.
        /// Change（Added → Removed → Updated）、Importance 降順、パッケージ名順にソートされたエントリを返します。
        /// </summary>
        public IReadOnlyList<DependencyChangeEntry> EntriesByImportance =>
            Entries
                .OrderBy(e => ChangeOrder(e.Change))
                .ThenByDescending(e => e.Importance)
                .ThenBy(e => e.PackageName, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static int ChangeOrder(string change)
            => change switch { "Added" => 0, "Removed" => 1, "Updated" => 2, _ => 3 };

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
    }
}
