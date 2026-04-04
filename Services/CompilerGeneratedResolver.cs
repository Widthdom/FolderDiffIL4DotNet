using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Resolves compiler-generated type and member names to their user-authored origin.
    /// Handles async state machines, iterator classes, lambda capture classes,
    /// and auto-property backing fields.
    /// コンパイラ生成の型名・メンバー名を、ユーザーが記述した元のメンバーに解決します。
    /// async ステートマシン、イテレータクラス、ラムダキャプチャクラス、
    /// 自動プロパティバッキングフィールドに対応します。
    /// </summary>
    internal static class CompilerGeneratedResolver
    {
        // Async state machine: <MethodName>d__N or <MethodName>d
        // async ステートマシン: <MethodName>d__N または <MethodName>d
        private static readonly Regex s_asyncStateMachine = new(@"<(\w+)>d(__\d+)?$", RegexOptions.Compiled);

        // Display class (lambda capture): <>c__DisplayClassN or <>c
        // ディスプレイクラス（ラムダキャプチャ）: <>c__DisplayClassN または <>c
        private static readonly Regex s_displayClass = new(@"<>c(__DisplayClass\d+_\d+)?$", RegexOptions.Compiled);

        // Lambda method inside display class: <MethodName>b__N_M
        // ディスプレイクラス内のラムダメソッド: <MethodName>b__N_M
        private static readonly Regex s_lambdaMethod = new(@"<(\w+)>b__\d+(_\d+)?$", RegexOptions.Compiled);

        // Iterator/yield state machine: <MethodName>d__N (same pattern as async)
        // イテレータ/yield ステートマシン: <MethodName>d__N（async と同じパターン）

        // Auto-property backing field: <PropertyName>k__BackingField
        // 自動プロパティバッキングフィールド: <PropertyName>k__BackingField
        private static readonly Regex s_backingField = new(@"<(\w+)>k__BackingField$", RegexOptions.Compiled);

        // Compiler-generated nested type name: slash followed by '<' in the nested segment
        // コンパイラ生成のネストされた型名: ネストセグメントで '/' の後に '<' を含む
        private static readonly Regex s_compilerGeneratedType = new(@"/<", RegexOptions.Compiled);

        /// <summary>
        /// Post-processes semantic change entries to annotate compiler-generated members
        /// with their user-authored origin. Modifies entries in-place where applicable.
        /// セマンティック変更エントリを後処理し、コンパイラ生成メンバーにユーザー記述元の
        /// 注釈を付加します。該当エントリをインプレースで変更します。
        /// </summary>
        /// <param name="entries">The list of entries to post-process. / 後処理するエントリのリスト。</param>
        /// <returns>The same list with compiler-generated entries annotated. / コンパイラ生成エントリに注釈を付けた同じリスト。</returns>
        public static List<MemberChangeEntry> Annotate(List<MemberChangeEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i] = AnnotateEntry(entries[i]);
            }
            return entries;
        }

        /// <summary>
        /// Annotates a single entry if it represents a compiler-generated member.
        /// Returns the original entry unchanged if it is not compiler-generated.
        /// コンパイラ生成メンバーを表すエントリの場合に注釈を付けます。
        /// コンパイラ生成でない場合は元のエントリをそのまま返します。
        /// </summary>
        internal static MemberChangeEntry AnnotateEntry(MemberChangeEntry entry)
        {
            string annotatedTypeName = entry.TypeName;
            string annotatedMemberName = entry.MemberName;

            // --- TypeName-level annotations ---
            // --- 型名レベルの注釈 ---

            // Check if TypeName contains a compiler-generated nested type
            // TypeName にコンパイラ生成のネストされた型が含まれるかチェック
            int slashIdx = annotatedTypeName.LastIndexOf('/');
            if (slashIdx >= 0)
            {
                string nestedPart = annotatedTypeName[(slashIdx + 1)..];
                string parentType = annotatedTypeName[..slashIdx];

                // Async state machine type: ParentType/<MethodName>d__N
                // async ステートマシン型: ParentType/<MethodName>d__N
                var asyncMatch = s_asyncStateMachine.Match(nestedPart);
                if (asyncMatch.Success)
                {
                    string originMethod = asyncMatch.Groups[1].Value;
                    annotatedTypeName = $"{parentType}/{nestedPart} (state machine of {parentType}.{originMethod})";
                }
                else
                {
                    var displayMatch = s_displayClass.Match(nestedPart);
                    if (displayMatch.Success)
                    {
                        annotatedTypeName = $"{parentType}/{nestedPart} (closure of {parentType})";
                    }
                }
            }

            // --- MemberName-level annotations ---
            // --- メンバー名レベルの注釈 ---

            // Check backing field: <PropertyName>k__BackingField
            // バッキングフィールドをチェック: <PropertyName>k__BackingField
            var backingMatch = s_backingField.Match(annotatedMemberName);
            if (backingMatch.Success)
            {
                string propertyName = backingMatch.Groups[1].Value;
                annotatedMemberName = $"<{propertyName}>k__BackingField (backing field of {propertyName})";
            }
            else
            {
                // Check lambda method name (e.g. <DoWork>b__0)
                // ラムダメソッド名をチェック（例: <DoWork>b__0）
                var lambdaMatch = s_lambdaMethod.Match(annotatedMemberName);
                if (lambdaMatch.Success)
                {
                    string originMethod = lambdaMatch.Groups[1].Value;
                    annotatedMemberName = $"{annotatedMemberName} (lambda in {originMethod})";
                }
            }

            // Return annotated entry if any annotation was applied
            // 注釈が適用された場合は注釈付きエントリを返す
            if (annotatedTypeName != entry.TypeName || annotatedMemberName != entry.MemberName)
            {
                return entry with
                {
                    TypeName = annotatedTypeName,
                    MemberName = annotatedMemberName,
                };
            }

            return entry;
        }

        /// <summary>
        /// Determines whether a type name represents a compiler-generated type.
        /// 型名がコンパイラ生成の型を表すかどうかを判定します。
        /// </summary>
        /// <param name="typeName">Fully qualified type name (e.g. "MyNamespace.MyClass/&lt;DoWork&gt;d__0"). / 完全修飾型名。</param>
        /// <returns><see langword="true"/> if the type appears to be compiler-generated. / コンパイラ生成と思われる場合に <see langword="true"/>。</returns>
        public static bool IsCompilerGeneratedType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;
            return s_compilerGeneratedType.IsMatch(typeName);
        }

        /// <summary>
        /// Determines whether a member name represents a compiler-generated member.
        /// メンバー名がコンパイラ生成のメンバーを表すかどうかを判定します。
        /// </summary>
        public static bool IsCompilerGeneratedMember(string memberName)
        {
            if (string.IsNullOrEmpty(memberName)) return false;
            return s_backingField.IsMatch(memberName) || s_lambdaMethod.IsMatch(memberName);
        }
    }
}
