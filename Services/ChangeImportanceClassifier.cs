using System;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Rule-based classifier that assigns a <see cref="ChangeImportance"/> level
    /// to a <see cref="MemberChangeEntry"/> based on its semantic properties.
    /// <see cref="MemberChangeEntry"/> のセマンティック属性からルールベースで
    /// <see cref="ChangeImportance"/> レベルを判定する分類器。
    /// </summary>
    internal static class ChangeImportanceClassifier
    {
        // Visibility ordinal: higher = more visible.
        // 可視性序数: 大きいほど可視範囲が広い。
        private static int VisibilityOrdinal(string access)
        {
            if (access.Length == 0) return -1;
            // Normalized comparison (case-insensitive, trimmed).
            var a = access.Trim();
            if (a.Equals("public", StringComparison.OrdinalIgnoreCase)) return 5;
            if (a.Equals("protected internal", StringComparison.OrdinalIgnoreCase)) return 4;
            if (a.Equals("protected", StringComparison.OrdinalIgnoreCase)) return 3;
            if (a.Equals("internal", StringComparison.OrdinalIgnoreCase)) return 2;
            if (a.Equals("private protected", StringComparison.OrdinalIgnoreCase)) return 1;
            if (a.Equals("private", StringComparison.OrdinalIgnoreCase)) return 0;
            return -1;
        }

        /// <summary>
        /// Returns <see langword="true"/> when the access modifier represents a publicly visible API
        /// (public or protected).
        /// アクセス修飾子が公開 API（public または protected）を表す場合に <see langword="true"/> を返します。
        /// </summary>
        private static bool IsPublicOrProtected(string access)
        {
            var ord = VisibilityOrdinal(access.Trim());
            return ord >= 3; // protected (3), protected internal (4), public (5)
        }

        /// <summary>
        /// Classifies a single <see cref="MemberChangeEntry"/> and returns its importance level.
        /// <see cref="MemberChangeEntry"/> を分類し、その重要度レベルを返します。
        /// </summary>
        public static ChangeImportance Classify(MemberChangeEntry entry)
        {
            bool hasArrow = false;

            switch (entry.Change)
            {
                case "Removed":
                    // Removal of public/protected API is a breaking change.
                    // public/protected API の削除は破壊的変更。
                    return IsPublicOrProtected(entry.Access) ? ChangeImportance.High : ChangeImportance.Medium;

                case "Added":
                    // New public/protected API may affect interface implementers.
                    // 新しい public/protected API はインターフェース実装者に影響する可能性。
                    return IsPublicOrProtected(entry.Access) ? ChangeImportance.Medium : ChangeImportance.Low;

                case "Modified":
                    // Check for access narrowing (breaking) via arrow notation "old → new".
                    // アクセス縮小（破壊的）をアロー表記 "old → new" で検査。
                    if (ContainsArrow(entry.Access))
                    {
                        var (oldAccess, newAccess) = SplitArrow(entry.Access);
                        int oldOrd = VisibilityOrdinal(oldAccess);
                        int newOrd = VisibilityOrdinal(newAccess);
                        if (oldOrd > newOrd && oldOrd >= 3) // narrowing from public/protected
                            return ChangeImportance.High;
                    }

                    // Return type change (breaking).
                    // 戻り値型変更（破壊的）。
                    if (ContainsArrow(entry.ReturnType))
                        return ChangeImportance.High;

                    // Property/Field type change (breaking).
                    // プロパティ/フィールド型変更（破壊的）。
                    if (ContainsArrow(entry.MemberType))
                        return ChangeImportance.High;

                    // Parameter change (breaking).
                    // パラメータ変更（破壊的）。
                    if (ContainsArrow(entry.Parameters))
                        return ChangeImportance.High;

                    // Modifier changes (e.g. virtual → sealed) — notable but not always breaking.
                    // 修飾子変更（例: virtual → sealed）— 注目すべきだが必ずしも破壊的ではない。
                    hasArrow = ContainsArrow(entry.Modifiers);
                    if (hasArrow)
                        return ChangeImportance.Medium;

                    // Access widening (not breaking but notable).
                    // アクセス拡大（破壊的ではないが注目すべき）。
                    if (ContainsArrow(entry.Access))
                        return ChangeImportance.Medium;

                    // Body-only change (implementation detail).
                    // Body のみの変更（実装詳細）。
                    return ChangeImportance.Low;

                default:
                    return ChangeImportance.Low;
            }
        }

        /// <summary>
        /// Returns a new <see cref="MemberChangeEntry"/> with the <see cref="ChangeImportance"/> set
        /// by the classification rules.
        /// 分類ルールに基づいて <see cref="ChangeImportance"/> を設定した新しい
        /// <see cref="MemberChangeEntry"/> を返します。
        /// </summary>
        public static MemberChangeEntry WithClassifiedImportance(MemberChangeEntry entry)
            => entry with { Importance = Classify(entry) };

        /// <summary>Checks if a field value contains the arrow separator " → ". / フィールド値にアロー区切り " → " が含まれるかを確認します。</summary>
        private static bool ContainsArrow(string value)
            => value.Contains(" \u2192 ", StringComparison.Ordinal);

        /// <summary>Splits an arrow-separated value into old and new parts. / アロー区切り値を旧と新に分割します。</summary>
        private static (string Old, string New) SplitArrow(string value)
        {
            int idx = value.IndexOf(" \u2192 ", StringComparison.Ordinal);
            if (idx < 0) return (value, value);
            return (value.Substring(0, idx).Trim(), value.Substring(idx + 3).Trim());
        }
    }
}
