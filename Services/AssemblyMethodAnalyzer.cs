using System;
using System.Collections.Generic;
using System.Linq;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Compares two .NET assemblies at the metadata level using <see cref="System.Reflection.Metadata"/>
    /// to detect type, method, property, and field additions/removals/modifications.
    /// Modified detection includes: method IL body changes, access modifier changes (e.g. public → internal),
    /// modifier changes (e.g. adding/removing static/virtual), and property/field type changes.
    /// Returns structured <see cref="MemberChangeEntry"/> records for table-style rendering.
    /// <see cref="System.Reflection.Metadata"/> を使用して 2 つの .NET アセンブリのメタデータを比較し、
    /// 型・メソッド・プロパティ・フィールドの追加・削除・変更を検出します。
    /// 変更検出にはメソッド IL ボディ変更、アクセス修飾子変更（例: public → internal）、
    /// 修飾子変更（例: static/virtual の追加・削除）、プロパティ/フィールドの型変更を含みます。
    /// 表形式レンダリング向けの構造化 <see cref="MemberChangeEntry"/> レコードを返します。
    /// </summary>
    internal static partial class AssemblyMethodAnalyzer
    {
        /// <summary>
        /// Analyses two assembly files and returns a summary of assembly semantic changes.
        /// Returns <see langword="null"/> if analysis fails (best-effort).
        /// 2 つのアセンブリファイルを解析し、アセンブリセマンティック変更要約を返します。
        /// 解析に失敗した場合は <see langword="null"/> を返します（ベストエフォート）。
        /// </summary>
        public static AssemblySemanticChangesSummary? Analyze(string oldAssemblyPath, string newAssemblyPath)
        {
            try
            {
                var oldSnapshot = ReadAssemblySnapshot(oldAssemblyPath);
                var newSnapshot = ReadAssemblySnapshot(newAssemblyPath);

                var entries = new List<MemberChangeEntry>();

                CompareTypes(oldSnapshot, newSnapshot, entries);
                CompareMethods(oldSnapshot, newSnapshot, entries);
                CompareProperties(oldSnapshot, newSnapshot, entries);
                CompareFields(oldSnapshot, newSnapshot, entries);

                // Classify importance for each entry.
                // 各エントリの重要度を分類。
                for (int i = 0; i < entries.Count; i++)
                    entries[i] = ChangeImportanceClassifier.WithClassifiedImportance(entries[i]);

                // Sort: by TypeName, then by Change order (Added → Removed → Modified)
                entries.Sort((a, b) =>
                {
                    int cmp = StringComparer.Ordinal.Compare(a.TypeName, b.TypeName);
                    if (cmp != 0) return cmp;
                    return ChangeOrder(a.Change).CompareTo(ChangeOrder(b.Change));
                });

                return new AssemblySemanticChangesSummary
                {
                    Entries = entries,
                };
            }
#pragma warning disable CA1031 // ベストエフォート解析のため全例外をキャッチ / Catch-all for best-effort analysis
            catch
            {
                return null;
            }
#pragma warning restore CA1031
        }

        /// <summary>Sort order for Change column: Added=0, Removed=1, Modified=2. / Change 列のソート順。</summary>
        private static int ChangeOrder(string change)
            => change switch { "Added" => 0, "Removed" => 1, "Modified" => 2, _ => 3 };

        /// <summary>
        /// Looks up the BaseType string for a given type name from the preferred snapshot,
        /// falling back to the other snapshot if the type is not found in the preferred one.
        /// 指定した型名の BaseType 文字列を preferred スナップショットから検索し、
        /// 見つからない場合は fallback スナップショットから取得します。
        /// </summary>
        private static string LookupBaseType(string typeName, AssemblySnapshot preferred, AssemblySnapshot fallback)
        {
            if (preferred.TypeNames.TryGetValue(typeName, out var info)) return info.BaseType;
            if (fallback.TypeNames.TryGetValue(typeName, out info)) return info.BaseType;
            return "";
        }
    }
}
