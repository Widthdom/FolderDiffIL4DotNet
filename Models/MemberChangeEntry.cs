namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Represents a single member-level change detected between two assembly builds.
    /// 2 つのアセンブリビルド間で検出された単一のメンバーレベル変更を表します。
    /// </summary>
    /// <param name="Change">Change kind: "Added", "Removed", "Modified". / 変更種別。</param>
    /// <param name="TypeName">Owning type name (or the type itself for Type entries). / 所属型名（Type エントリの場合は型名そのもの）。</param>
    /// <param name="Access">Access modifier: public, internal, protected, private, etc. For Modified entries, shows "old → new" when changed (e.g. "public → internal"). / アクセス修飾子。Modified エントリでは変更時に「旧 → 新」形式で表示。</param>
    /// <param name="Modifiers">Other modifiers (static, abstract, virtual, sealed, override, etc.). For Modified entries, shows "old → new" when changed. / その他の修飾子。Modified エントリでは変更時に「旧 → 新」形式で表示。</param>
    /// <param name="MemberKind">Member kind: "Class", "Record", "Struct", "Interface", "Enum", "Constructor", "StaticConstructor", "Method", "Property", "Field". / メンバー種別。</param>
    /// <param name="MemberName">Member name (C# name; constructors use the class name, not .ctor). Empty for Type entries. / メンバー名（C# 名、コンストラクタは .ctor ではなくクラス名）。Type の場合は空。</param>
    /// <param name="MemberType">For Field/Property: the declared type (e.g. "string", "int"). Empty for Method/Constructor/Type entries. For Modified entries, shows "old → new" when type changed (e.g. "System.String → System.Int32"). / フィールド・プロパティの宣言型。メソッド・コンストラクタ・Type の場合は空。Modified エントリでは型変更時に「旧 → 新」形式で表示。</param>
    /// <param name="ReturnType">For Method: the return type (e.g. "void", "string"). For Constructor: "void". Empty for Type/Field/Property entries. / メソッドの戻り値型。コンストラクタは "void"。Type/Field/Property の場合は空。</param>
    /// <param name="Parameters">For Method/Constructor: the parameter list without parentheses (e.g. "int page", "string name, int count = 0"). Empty string for no-arg methods. Empty for Type/Field/Property entries. / メソッド・コンストラクタのパラメータ一覧（括弧なし）。引数なしは空文字列。Type/Field/Property の場合は空。</param>
    /// <param name="BaseType">Base type and implemented interfaces of the owning type (e.g. "MyApp.BaseController, System.IDisposable"). Omits trivial bases (System.Object, System.ValueType, System.Enum). / 所属型の基底型および実装インターフェース。自明な基底型は省略。</param>
    /// <param name="Body">"Changed" when the method body or field initializer IL has changed; otherwise empty. / メソッドボディまたはフィールド初期化子の IL が変更された場合 "Changed"、それ以外は空。</param>
    /// <param name="Importance">Auto-assigned importance level for this change. Defaults to <see cref="ChangeImportance.Low"/>. / この変更に自動付与された重要度レベル。デフォルトは <see cref="ChangeImportance.Low"/>。</param>
    public sealed record MemberChangeEntry(
        string Change,
        string TypeName,
        string BaseType,
        string Access,
        string Modifiers,
        string MemberKind,
        string MemberName,
        string MemberType,
        string ReturnType,
        string Parameters,
        string Body,
        ChangeImportance Importance = ChangeImportance.Low);
}
