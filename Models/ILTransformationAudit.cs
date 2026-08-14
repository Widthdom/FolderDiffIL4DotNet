using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// Old/new configured IL transformation applications for one compared file.
    /// 比較対象1ファイルに対するold/new別の設定IL変換適用記録。
    /// </summary>
    public sealed class ILTransformationAudit
    {
        /// <summary>Applications on the old-side raw IL. / old側raw ILへの適用記録。</summary>
        public IReadOnlyList<ILRuleApplicationAudit> Old { get; init; } = new List<ILRuleApplicationAudit>();

        /// <summary>Applications on the new-side raw IL. / new側raw ILへの適用記録。</summary>
        public IReadOnlyList<ILRuleApplicationAudit> New { get; init; } = new List<ILRuleApplicationAudit>();
    }

    /// <summary>
    /// Application summary for one configured IL rule on one comparison side.
    /// 比較片側における1つの設定IL規則の適用サマリー。
    /// </summary>
    public sealed class ILRuleApplicationAudit
    {
        /// <summary>Rule identifier derived from operation and effective application index. The numeric part has a minimum width of four digits. / 操作と実効適用位置から生成した規則ID。数値部は4桁を最小幅とする。</summary>
        public string RuleId { get; init; } = string.Empty;

        /// <summary>Operation name: IgnoreLine or NormalizeSubstring. / 操作名: IgnoreLine または NormalizeSubstring。</summary>
        public string Operation { get; init; } = string.Empty;

        /// <summary>Configured ordinal substring pattern. / 設定されたordinal部分文字列パターン。</summary>
        public string Pattern { get; init; } = string.Empty;

        /// <summary>Number of raw IL lines to which this rule was applied. / この規則を適用したraw IL行数。</summary>
        public int AppliedLineCount { get; init; }
    }
}
