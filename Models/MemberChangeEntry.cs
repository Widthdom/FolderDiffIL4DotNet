namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Represents a single member-level change detected between two assembly builds.
    /// 2 つのアセンブリビルド間で検出された単一のメンバーレベル変更を表します。
    /// </summary>
    /// <param name="Change">Change kind: "+" (added), "-" (removed), "~" (body changed). / 変更種別。</param>
    /// <param name="TypeName">Owning type name (or the type itself for Type entries). / 所属型名（Type エントリの場合は型名そのもの）。</param>
    /// <param name="Access">Access modifier. Empty for Type entries. / アクセス修飾子。Type の場合は空。</param>
    /// <param name="MemberKind">Member kind: "Type", "Method", "Property", "Field". / メンバー種別。</param>
    /// <param name="MemberName">Member name. Empty for Type entries. / メンバー名。Type の場合は空。</param>
    /// <param name="Details">
    /// Additional details: method signature with parameter defaults for methods,
    /// type and default value for fields, type and accessor info for properties.
    /// 追加詳細：メソッドならパラメータ既定値を含むシグネチャ、フィールドなら型と既定値、プロパティなら型とアクセサ情報。
    /// </param>
    public sealed record MemberChangeEntry(
        string Change,
        string TypeName,
        string Access,
        string MemberKind,
        string MemberName,
        string Details);
}
