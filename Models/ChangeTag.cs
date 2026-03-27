namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Represents an estimated change pattern tag for an assembly-level change.
    /// アセンブリレベルの変更に対する推定変更パターンタグを表します。
    /// </summary>
    public enum ChangeTag
    {
        /// <summary>New method(s) added. / メソッド追加。</summary>
        MethodAdd,

        /// <summary>Method(s) removed. / メソッド削除。</summary>
        MethodRemove,

        /// <summary>New type(s) added. / 型追加。</summary>
        TypeAdd,

        /// <summary>Type(s) removed. / 型削除。</summary>
        TypeRemove,

        /// <summary>Method body extracted to a new private/internal method within the same type. / 同一型内でメソッド本体を新しい private/internal メソッドに抽出。</summary>
        Extract,

        /// <summary>Private/internal method inlined into another method within the same type. / 同一型内で private/internal メソッドを別メソッドにインライン化。</summary>
        Inline,

        /// <summary>Method moved between types (removed from one type, added to another with the same signature). / 型間でメソッドを移動（同シグネチャで一方から削除・他方に追加）。</summary>
        Move,

        /// <summary>Method renamed (removed and added with the same IL body within the same type). / メソッドのリネーム（同一型内で同一 IL ボディで削除・追加）。</summary>
        Rename,

        /// <summary>Method or property signature changed (parameter types/count or return type). / メソッドまたはプロパティのシグネチャ変更（パラメータ型/数または戻り値型）。</summary>
        Signature,

        /// <summary>Access modifier changed (e.g. public → internal). / アクセス修飾子変更（例: public → internal）。</summary>
        Access,

        /// <summary>Only the method body IL changed, with no structural changes. / メソッド本体の IL のみ変更、構造的な変更なし。</summary>
        BodyEdit,

        /// <summary>Only dependency package versions changed, no code changes. / 依存パッケージバージョンのみ変更、コード変更なし。</summary>
        DepUpdate
    }
}
