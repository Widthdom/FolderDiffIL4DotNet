namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Represents a single member-level change detected between two assembly builds.
    /// 2 つのアセンブリビルド間で検出された単一のメンバーレベル変更を表します。
    /// </summary>
    /// <param name="Change">Change kind: "Added", "Removed", "Modified". / 変更種別。</param>
    /// <param name="TypeName">Owning type name (or the type itself for Type entries). / 所属型名（Type エントリの場合は型名そのもの）。</param>
    /// <param name="Access">Access modifier. Empty for Type entries. / アクセス修飾子。Type の場合は空。</param>
    /// <param name="Modifiers">Other modifiers (static, abstract, virtual, sealed, override, etc.). / その他の修飾子。</param>
    /// <param name="MemberKind">Member kind: "Type", "Method", "Property", "Field". / メンバー種別。</param>
    /// <param name="MemberName">Member name (C# name; constructors use the class name, not .ctor). Empty for Type entries. / メンバー名（C# 名、コンストラクタは .ctor ではなくクラス名）。Type の場合は空。</param>
    /// <param name="MemberType">For Field/Property: the declared type (e.g. "string", "int"). Empty for Method/Type entries. / フィールド・プロパティの宣言型。メソッド・Type の場合は空。</param>
    /// <param name="Details">
    /// For methods: ReturnType (Type paramName, ...). Empty for Type/Field/Property entries.
    /// メソッドなら ReturnType (Type paramName, ...)。Type/Field/Property の場合は空。
    /// </param>
    public sealed record MemberChangeEntry(
        string Change,
        string TypeName,
        string Access,
        string Modifiers,
        string MemberKind,
        string MemberName,
        string MemberType,
        string Details);
}
