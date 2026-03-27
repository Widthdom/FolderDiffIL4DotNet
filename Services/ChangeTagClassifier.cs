using System;
using System.Collections.Generic;
using System.Linq;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Estimates change pattern tags for a file based on its semantic and dependency change data.
    /// Patterns are inferred heuristically from <see cref="AssemblySemanticChangesSummary"/> and
    /// <see cref="DependencyChangeSummary"/> — they are best-effort estimations, not guarantees.
    /// セマンティック変更データおよび依存関係変更データからファイルの変更パターンタグを推定します。
    /// パターンは <see cref="AssemblySemanticChangesSummary"/> と <see cref="DependencyChangeSummary"/>
    /// からヒューリスティックに推定されます — ベストエフォートの推定であり、保証ではありません。
    /// </summary>
    internal static class ChangeTagClassifier
    {
        // Arrow separator used in Modified entries to show old → new values
        // Modified エントリで旧 → 新値を表示するために使用する矢印セパレータ
        private const string ARROW = " \u2192 ";

        /// <summary>
        /// Display labels for each <see cref="ChangeTag"/> value, kept short for table column display.
        /// 各 <see cref="ChangeTag"/> 値の表示ラベル。テーブル列表示用に短く保ちます。
        /// </summary>
        private static readonly IReadOnlyDictionary<ChangeTag, string> _labels = new Dictionary<ChangeTag, string>
        {
            [ChangeTag.MethodAdd] = "+Method",
            [ChangeTag.MethodRemove] = "-Method",
            [ChangeTag.TypeAdd] = "+Type",
            [ChangeTag.TypeRemove] = "-Type",
            [ChangeTag.Extract] = "Extract",
            [ChangeTag.Inline] = "Inline",
            [ChangeTag.Move] = "Move",
            [ChangeTag.Rename] = "Rename",
            [ChangeTag.Signature] = "Signature",
            [ChangeTag.Access] = "Access",
            [ChangeTag.BodyEdit] = "BodyEdit",
            [ChangeTag.DepUpdate] = "DepUpdate",
        };

        /// <summary>
        /// Returns the short display label for a <see cref="ChangeTag"/>.
        /// <see cref="ChangeTag"/> の短い表示ラベルを返します。
        /// </summary>
        public static string GetLabel(ChangeTag tag)
            => _labels.TryGetValue(tag, out var label) ? label : tag.ToString();

        /// <summary>
        /// Returns all tag-label pairs for legend display.
        /// 凡例表示用のすべてのタグ-ラベルペアを返します。
        /// </summary>
        public static IReadOnlyDictionary<ChangeTag, string> AllLabels => _labels;

        /// <summary>
        /// Classifies change tags for a file from its semantic and dependency change data.
        /// Returns an empty list when no patterns can be identified.
        /// セマンティック変更データおよび依存関係変更データからファイルの変更タグを分類します。
        /// パターンが識別できない場合は空リストを返します。
        /// </summary>
        public static IReadOnlyList<ChangeTag> Classify(
            AssemblySemanticChangesSummary? semanticChanges,
            DependencyChangeSummary? dependencyChanges)
        {
            var tags = new List<ChangeTag>();

            if (semanticChanges != null && semanticChanges.HasChanges)
            {
                ClassifySemanticChanges(semanticChanges, tags);
            }

            if (dependencyChanges != null && dependencyChanges.HasChanges)
            {
                // DepUpdate: dependency changes exist and either no semantic changes or semantic changes are all body-only
                // DepUpdate: 依存関係変更あり、かつセマンティック変更なし、またはセマンティック変更がすべてボディのみ
                if (semanticChanges == null || !semanticChanges.HasChanges)
                {
                    tags.Add(ChangeTag.DepUpdate);
                }
            }

            return tags;
        }

        /// <summary>
        /// Formats a list of change tags as a comma-separated display string.
        /// Returns empty string when the list is empty.
        /// 変更タグリストをカンマ区切りの表示文字列にフォーマットします。
        /// リストが空の場合は空文字列を返します。
        /// </summary>
        public static string FormatTags(IReadOnlyList<ChangeTag> tags)
        {
            if (tags.Count == 0) return "";
            return string.Join(", ", tags.Select(GetLabel));
        }

        private static void ClassifySemanticChanges(AssemblySemanticChangesSummary summary, List<ChangeTag> tags)
        {
            var entries = summary.Entries;

            // Partition entries by type and change kind for pattern detection
            // パターン検出のためにエントリを型と変更種別で分割
            var addedTypes = new List<MemberChangeEntry>();
            var removedTypes = new List<MemberChangeEntry>();
            var addedMethods = new List<MemberChangeEntry>();
            var removedMethods = new List<MemberChangeEntry>();
            var modifiedMethods = new List<MemberChangeEntry>();
            var signatureChanges = new List<MemberChangeEntry>();
            var accessChanges = new List<MemberChangeEntry>();
            var bodyOnlyChanges = new List<MemberChangeEntry>();

            foreach (var e in entries)
            {
                bool isType = IsTypeMember(e.MemberKind);
                bool isMethod = IsMethodLike(e.MemberKind);

                switch (e.Change)
                {
                    case "Added":
                        if (isType) addedTypes.Add(e);
                        else if (isMethod) addedMethods.Add(e);
                        break;
                    case "Removed":
                        if (isType) removedTypes.Add(e);
                        else if (isMethod) removedMethods.Add(e);
                        break;
                    case "Modified":
                        if (isMethod || IsPropertyOrField(e.MemberKind))
                        {
                            if (HasSignatureChange(e))
                                signatureChanges.Add(e);
                            else if (HasAccessOnlyChange(e))
                                accessChanges.Add(e);
                            else if (HasBodyOnlyChange(e))
                                bodyOnlyChanges.Add(e);
                            else
                                modifiedMethods.Add(e);
                        }
                        break;
                }
            }

            // ── Detect compound patterns first ──

            // Extract: existing method body changed + new private/internal method added in same type
            // Extract: 既存メソッドの本体変更 + 同一型内に新 private/internal メソッド追加
            DetectExtractPattern(addedMethods, bodyOnlyChanges, modifiedMethods, tags);

            // Inline: private/internal method removed + another method body changed in same type
            // Inline: private/internal メソッド削除 + 同一型内の別メソッドの本体変更
            DetectInlinePattern(removedMethods, bodyOnlyChanges, modifiedMethods, tags);

            // Move: method removed from type A + added to type B with same member name
            // Move: 型 A からメソッド削除 + 同メンバー名で型 B に追加
            DetectMovePattern(addedMethods, removedMethods, tags);

            // Rename: method removed + method added with same MemberKind in same type (remaining after Extract/Inline/Move)
            // Rename: 同一型内でメソッド削除 + 同 MemberKind のメソッド追加（Extract/Inline/Move の残り）
            DetectRenamePattern(addedMethods, removedMethods, tags);

            // ── Then detect simple patterns from remaining entries ──

            if (addedTypes.Count > 0)
                tags.Add(ChangeTag.TypeAdd);
            if (removedTypes.Count > 0)
                tags.Add(ChangeTag.TypeRemove);
            if (addedMethods.Count > 0)
                tags.Add(ChangeTag.MethodAdd);
            if (removedMethods.Count > 0)
                tags.Add(ChangeTag.MethodRemove);
            if (signatureChanges.Count > 0)
                tags.Add(ChangeTag.Signature);
            if (accessChanges.Count > 0)
                tags.Add(ChangeTag.Access);
            if (bodyOnlyChanges.Count > 0 || modifiedMethods.Count > 0)
                tags.Add(ChangeTag.BodyEdit);
        }

        private static void DetectExtractPattern(
            List<MemberChangeEntry> addedMethods,
            List<MemberChangeEntry> bodyOnlyChanges,
            List<MemberChangeEntry> modifiedMethods,
            List<ChangeTag> tags)
        {
            // All body-changed methods plus generically-modified methods form the "modified body" pool
            // 全ボディ変更メソッドと汎用的に Modified なメソッドが「ボディ変更プール」を構成
            var allModifiedByType = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in bodyOnlyChanges) allModifiedByType.Add(m.TypeName);
            foreach (var m in modifiedMethods) allModifiedByType.Add(m.TypeName);

            bool found = false;
            for (int i = addedMethods.Count - 1; i >= 0; i--)
            {
                var added = addedMethods[i];
                if (!IsPrivateOrInternal(added.Access)) continue;
                if (!allModifiedByType.Contains(added.TypeName)) continue;

                // Match: new private/internal method in a type that has modified methods
                // マッチ: Modified メソッドのある型内に新しい private/internal メソッド
                found = true;
                addedMethods.RemoveAt(i);
            }

            if (found)
                tags.Add(ChangeTag.Extract);
        }

        private static void DetectInlinePattern(
            List<MemberChangeEntry> removedMethods,
            List<MemberChangeEntry> bodyOnlyChanges,
            List<MemberChangeEntry> modifiedMethods,
            List<ChangeTag> tags)
        {
            var allModifiedByType = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in bodyOnlyChanges) allModifiedByType.Add(m.TypeName);
            foreach (var m in modifiedMethods) allModifiedByType.Add(m.TypeName);

            bool found = false;
            for (int i = removedMethods.Count - 1; i >= 0; i--)
            {
                var removed = removedMethods[i];
                if (!IsPrivateOrInternal(removed.Access)) continue;
                if (!allModifiedByType.Contains(removed.TypeName)) continue;

                found = true;
                removedMethods.RemoveAt(i);
            }

            if (found)
                tags.Add(ChangeTag.Inline);
        }

        private static void DetectMovePattern(
            List<MemberChangeEntry> addedMethods,
            List<MemberChangeEntry> removedMethods,
            List<ChangeTag> tags)
        {
            bool found = false;
            for (int i = removedMethods.Count - 1; i >= 0; i--)
            {
                var removed = removedMethods[i];
                // Look for an added method in a *different* type with the same name and kind
                // *別の* 型に同名・同種のメソッド追加を探す
                for (int j = addedMethods.Count - 1; j >= 0; j--)
                {
                    var added = addedMethods[j];
                    if (string.Equals(added.TypeName, removed.TypeName, StringComparison.Ordinal)) continue;
                    if (!string.Equals(added.MemberName, removed.MemberName, StringComparison.Ordinal)) continue;
                    if (!string.Equals(added.MemberKind, removed.MemberKind, StringComparison.Ordinal)) continue;

                    found = true;
                    addedMethods.RemoveAt(j);
                    removedMethods.RemoveAt(i);
                    break;
                }
            }

            if (found)
                tags.Add(ChangeTag.Move);
        }

        private static void DetectRenamePattern(
            List<MemberChangeEntry> addedMethods,
            List<MemberChangeEntry> removedMethods,
            List<ChangeTag> tags)
        {
            bool found = false;
            for (int i = removedMethods.Count - 1; i >= 0; i--)
            {
                var removed = removedMethods[i];
                for (int j = addedMethods.Count - 1; j >= 0; j--)
                {
                    var added = addedMethods[j];
                    if (!string.Equals(added.TypeName, removed.TypeName, StringComparison.Ordinal)) continue;
                    if (!string.Equals(added.MemberKind, removed.MemberKind, StringComparison.Ordinal)) continue;
                    // Same return type and parameters suggest a rename rather than unrelated add/remove
                    // 同一の戻り値型とパラメータはリネームを示唆
                    if (!string.Equals(added.ReturnType, removed.ReturnType, StringComparison.Ordinal)) continue;
                    if (!string.Equals(added.Parameters, removed.Parameters, StringComparison.Ordinal)) continue;

                    found = true;
                    addedMethods.RemoveAt(j);
                    removedMethods.RemoveAt(i);
                    break;
                }
            }

            if (found)
                tags.Add(ChangeTag.Rename);
        }

        private static bool IsTypeMember(string memberKind)
            => memberKind is "Class" or "Record" or "Struct" or "Interface" or "Enum";

        private static bool IsMethodLike(string memberKind)
            => memberKind is "Method" or "Constructor" or "StaticConstructor";

        private static bool IsPropertyOrField(string memberKind)
            => memberKind is "Property" or "Field";

        private static bool HasSignatureChange(MemberChangeEntry e)
            => (!string.IsNullOrEmpty(e.ReturnType) && e.ReturnType.Contains(ARROW, StringComparison.Ordinal))
            || (!string.IsNullOrEmpty(e.Parameters) && e.Parameters.Contains(ARROW, StringComparison.Ordinal))
            || (!string.IsNullOrEmpty(e.MemberType) && e.MemberType.Contains(ARROW, StringComparison.Ordinal));

        private static bool HasAccessOnlyChange(MemberChangeEntry e)
            => !string.IsNullOrEmpty(e.Access) && e.Access.Contains(ARROW, StringComparison.Ordinal)
            && string.IsNullOrEmpty(e.Body)
            && !HasSignatureChange(e);

        private static bool HasBodyOnlyChange(MemberChangeEntry e)
            => string.Equals(e.Body, "Changed", StringComparison.Ordinal)
            && (string.IsNullOrEmpty(e.Access) || !e.Access.Contains(ARROW, StringComparison.Ordinal))
            && !HasSignatureChange(e);

        private static bool IsPrivateOrInternal(string access)
        {
            if (string.IsNullOrEmpty(access)) return false;
            // For Added entries, access is just the modifier (e.g. "private", "internal")
            // Added エントリの場合、access は修飾子のみ（例: "private", "internal"）
            return access.StartsWith("private", StringComparison.OrdinalIgnoreCase)
                || access.StartsWith("internal", StringComparison.OrdinalIgnoreCase);
        }
    }
}
