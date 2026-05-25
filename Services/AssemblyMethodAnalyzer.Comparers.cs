using System;
using System.Collections.Generic;
using System.Linq;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Comparison methods that detect additions, removals, and modifications
    /// for each member category (types, methods, properties, fields).
    /// 各メンバーカテゴリ（型・メソッド・プロパティ・フィールド）の追加・削除・変更を
    /// 検出する比較メソッド群です。
    /// </summary>
    internal static partial class AssemblyMethodAnalyzer
    {
        /// <summary>
        /// Compares type definitions between two snapshots and appends added/removed entries.
        /// 2 つのスナップショット間の型定義を比較し、追加・削除エントリを追記します。
        /// </summary>
        private static void CompareTypes(AssemblySnapshot oldSnapshot, AssemblySnapshot newSnapshot, List<MemberChangeEntry> entries)
        {
            foreach (var t in newSnapshot.TypeNames.Keys.Except(oldSnapshot.TypeNames.Keys, StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal))
            {
                var info = newSnapshot.TypeNames[t];
                entries.Add(new MemberChangeEntry("Added", t, info.BaseType, info.Access, info.Modifiers, info.Kind, "", "", "", "", ""));
            }
            foreach (var t in oldSnapshot.TypeNames.Keys.Except(newSnapshot.TypeNames.Keys, StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal))
            {
                var info = oldSnapshot.TypeNames[t];
                entries.Add(new MemberChangeEntry("Removed", t, info.BaseType, info.Access, info.Modifiers, info.Kind, "", "", "", "", ""));
            }
        }

        /// <summary>
        /// Compares method definitions (including constructors) and appends added/removed/modified entries.
        /// Detects IL body changes, access modifier changes, and modifier changes.
        /// メソッド定義（コンストラクタ含む）を比較し、追加・削除・変更エントリを追記します。
        /// IL ボディ変更・アクセス修飾子変更・修飾子変更を検出します。
        /// </summary>
        private static void CompareMethods(AssemblySnapshot oldSnapshot, AssemblySnapshot newSnapshot, List<MemberChangeEntry> entries)
        {
            foreach (var key in newSnapshot.Methods.Keys.Except(oldSnapshot.Methods.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
            {
                var m = newSnapshot.Methods[key];
                string kind = ToMemberKind(m.MethodName);
                entries.Add(new MemberChangeEntry("Added", m.TypeName, LookupBaseType(m.TypeName, newSnapshot, oldSnapshot), m.Access, m.Modifiers, kind, ToCSharpMethodName(m.MethodName, m.TypeName), "", m.ReturnType, m.Parameters, ""));
            }
            foreach (var key in oldSnapshot.Methods.Keys.Except(newSnapshot.Methods.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
            {
                var m = oldSnapshot.Methods[key];
                string kind = ToMemberKind(m.MethodName);
                entries.Add(new MemberChangeEntry("Removed", m.TypeName, LookupBaseType(m.TypeName, oldSnapshot, newSnapshot), m.Access, m.Modifiers, kind, ToCSharpMethodName(m.MethodName, m.TypeName), "", m.ReturnType, m.Parameters, ""));
            }
            foreach (var key in oldSnapshot.Methods.Keys.Intersect(newSnapshot.Methods.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
            {
                var oldM = oldSnapshot.Methods[key];
                var newM = newSnapshot.Methods[key];
                bool bodyChanged = !oldM.IlBytes.AsSpan().SequenceEqual(newM.IlBytes.AsSpan());
                bool accessChanged = !string.Equals(oldM.Access, newM.Access, StringComparison.Ordinal);
                bool modifiersChanged = !string.Equals(oldM.Modifiers, newM.Modifiers, StringComparison.Ordinal);

                if (bodyChanged || accessChanged || modifiersChanged)
                {
                    string kind = ToMemberKind(newM.MethodName);
                    string body = bodyChanged ? "Changed" : "";
                    string accessDisplay = accessChanged ? $"{oldM.Access} → {newM.Access}" : newM.Access;
                    string modifiersDisplay = modifiersChanged ? $"{oldM.Modifiers} → {newM.Modifiers}" : newM.Modifiers;
                    entries.Add(new MemberChangeEntry("Modified", newM.TypeName, LookupBaseType(newM.TypeName, newSnapshot, oldSnapshot), accessDisplay, modifiersDisplay, kind, ToCSharpMethodName(newM.MethodName, newM.TypeName), "", newM.ReturnType, newM.Parameters, body));
                }
            }
        }

        /// <summary>
        /// Compares property definitions and appends added/removed/modified entries.
        /// Detects property type changes, access modifier changes, and modifier changes.
        /// プロパティ定義を比較し、追加・削除・変更エントリを追記します。
        /// プロパティ型変更・アクセス修飾子変更・修飾子変更を検出します。
        /// </summary>
        private static void CompareProperties(AssemblySnapshot oldSnapshot, AssemblySnapshot newSnapshot, List<MemberChangeEntry> entries)
        {
            foreach (var key in newSnapshot.Properties.Keys.Except(oldSnapshot.Properties.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
            {
                var p = newSnapshot.Properties[key];
                entries.Add(new MemberChangeEntry("Added", p.TypeName, LookupBaseType(p.TypeName, newSnapshot, oldSnapshot), p.Access, p.Modifiers, "Property", p.PropertyName, p.PropertyType, "", "", ""));
            }
            foreach (var key in oldSnapshot.Properties.Keys.Except(newSnapshot.Properties.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
            {
                var p = oldSnapshot.Properties[key];
                entries.Add(new MemberChangeEntry("Removed", p.TypeName, LookupBaseType(p.TypeName, oldSnapshot, newSnapshot), p.Access, p.Modifiers, "Property", p.PropertyName, p.PropertyType, "", "", ""));
            }
            foreach (var key in oldSnapshot.Properties.Keys.Intersect(newSnapshot.Properties.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
            {
                var oldP = oldSnapshot.Properties[key];
                var newP = newSnapshot.Properties[key];
                bool typeChanged = !string.Equals(oldP.PropertyType, newP.PropertyType, StringComparison.Ordinal);
                bool accessChanged = !string.Equals(oldP.Access, newP.Access, StringComparison.Ordinal);
                bool modifiersChanged = !string.Equals(oldP.Modifiers, newP.Modifiers, StringComparison.Ordinal);

                if (typeChanged || accessChanged || modifiersChanged)
                {
                    string accessDisplay = accessChanged ? $"{oldP.Access} → {newP.Access}" : newP.Access;
                    string modifiersDisplay = modifiersChanged ? $"{oldP.Modifiers} → {newP.Modifiers}" : newP.Modifiers;
                    string typeDisplay = typeChanged ? $"{oldP.PropertyType} → {newP.PropertyType}" : newP.PropertyType;
                    entries.Add(new MemberChangeEntry("Modified", newP.TypeName, LookupBaseType(newP.TypeName, newSnapshot, oldSnapshot), accessDisplay, modifiersDisplay, "Property", newP.PropertyName, typeDisplay, "", "", ""));
                }
            }
        }

        /// <summary>
        /// Compares field definitions and appends added/removed/modified entries.
        /// Detects field type changes, access modifier changes, and modifier changes.
        /// フィールド定義を比較し、追加・削除・変更エントリを追記します。
        /// フィールド型変更・アクセス修飾子変更・修飾子変更を検出します。
        /// </summary>
        private static void CompareFields(AssemblySnapshot oldSnapshot, AssemblySnapshot newSnapshot, List<MemberChangeEntry> entries)
        {
            foreach (var key in newSnapshot.Fields.Keys.Except(oldSnapshot.Fields.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
            {
                var f = newSnapshot.Fields[key];
                entries.Add(new MemberChangeEntry("Added", f.TypeName, LookupBaseType(f.TypeName, newSnapshot, oldSnapshot), f.Access, f.Modifiers, "Field", f.FieldName, StripColonPrefix(f.Details), "", "", ""));
            }
            foreach (var key in oldSnapshot.Fields.Keys.Except(newSnapshot.Fields.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
            {
                var f = oldSnapshot.Fields[key];
                entries.Add(new MemberChangeEntry("Removed", f.TypeName, LookupBaseType(f.TypeName, oldSnapshot, newSnapshot), f.Access, f.Modifiers, "Field", f.FieldName, StripColonPrefix(f.Details), "", "", ""));
            }
            foreach (var key in oldSnapshot.Fields.Keys.Intersect(newSnapshot.Fields.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
            {
                var oldF = oldSnapshot.Fields[key];
                var newF = newSnapshot.Fields[key];
                bool detailsChanged = !string.Equals(oldF.Details, newF.Details, StringComparison.Ordinal);
                bool accessChanged = !string.Equals(oldF.Access, newF.Access, StringComparison.Ordinal);
                bool modifiersChanged = !string.Equals(oldF.Modifiers, newF.Modifiers, StringComparison.Ordinal);

                if (detailsChanged || accessChanged || modifiersChanged)
                {
                    string accessDisplay = accessChanged ? $"{oldF.Access} → {newF.Access}" : newF.Access;
                    string modifiersDisplay = modifiersChanged ? $"{oldF.Modifiers} → {newF.Modifiers}" : newF.Modifiers;
                    string typeDisplay = detailsChanged ? $"{StripColonPrefix(oldF.Details)} → {StripColonPrefix(newF.Details)}" : StripColonPrefix(newF.Details);
                    entries.Add(new MemberChangeEntry("Modified", newF.TypeName, LookupBaseType(newF.TypeName, newSnapshot, oldSnapshot), accessDisplay, modifiersDisplay, "Field", newF.FieldName, typeDisplay, "", "", ""));
                }
            }
        }

        /// <summary>Determine the member kind from the IL method name: .ctor → Constructor, .cctor → StaticConstructor, else → Method. / IL メソッド名からメンバー種別を判定。</summary>
        private static string ToMemberKind(string ilMethodName)
            => ilMethodName switch
            {
                ".ctor" => "Constructor",
                ".cctor" => "StaticConstructor",
                _ => "Method"
            };

        /// <summary>Convert IL method name to C# name: .ctor/.cctor → simple class name. / IL メソッド名を C# 名に変換。</summary>
        private static string ToCSharpMethodName(string ilMethodName, string typeName)
        {
            if (ilMethodName is ".ctor" or ".cctor")
            {
                // Extract simple class name from potentially nested or namespaced type name
                int slashIdx = typeName.LastIndexOf('/');
                string leaf = slashIdx >= 0 ? typeName[(slashIdx + 1)..] : typeName;
                int dotIdx = leaf.LastIndexOf('.');
                return dotIdx >= 0 ? leaf[(dotIdx + 1)..] : leaf;
            }
            return ilMethodName;
        }

        /// <summary>Strip leading ": " prefix from field/property details to extract the type portion. / フィールド・プロパティの詳細から先頭 ": " を除去して型部分を抽出。</summary>
        private static string StripColonPrefix(string details)
            => details.StartsWith(": ", StringComparison.Ordinal) ? details[2..] : details;
    }
}
