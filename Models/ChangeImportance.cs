namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Importance level of a semantic change entry, used to highlight breaking changes.
    /// セマンティック変更エントリの重要度レベル。破壊的変更の強調表示に使用します。
    /// </summary>
    public enum ChangeImportance
    {
        /// <summary>
        /// Low impact: internal-only body changes, private member additions, etc.
        /// 低影響: 内部実装のみの変更、private メンバーの追加など。
        /// </summary>
        Low = 0,

        /// <summary>
        /// Medium impact: public member additions, modifier changes, access widening, internal removals, etc.
        /// 中影響: public メンバーの追加、修飾子変更、アクセス拡大、internal メンバーの削除など。
        /// </summary>
        Medium = 1,

        /// <summary>
        /// High impact (breaking change candidate): public/protected API removal, access narrowing, return type change, parameter change, etc.
        /// 高影響（破壊的変更候補）: public/protected API の削除、アクセス縮小、戻り値型変更、パラメータ変更など。
        /// </summary>
        High = 2
    }
}
