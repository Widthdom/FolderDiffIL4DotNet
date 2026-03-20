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
    /// <param name="MemberName">Member name. Empty for Type entries. / メンバー名。Type の場合は空。</param>
    /// <param name="Details">
    /// Additional details in C# declaration order: ReturnType (Type paramName, ...) for methods,
    /// : Type for fields/properties.
    /// 追加詳細（C# 宣言順）：メソッドなら ReturnType (Type paramName, ...)、フィールド/プロパティなら : Type。
    /// </param>
    public sealed record MemberChangeEntry(
        string Change,
        string TypeName,
        string Access,
        string Modifiers,
        string MemberKind,
        string MemberName,
        string Details);
}
