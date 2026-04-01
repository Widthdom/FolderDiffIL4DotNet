using System;
using System.Collections.Generic;
using System.Linq;
using FolderDiffIL4DotNet.Core.Text;
using FolderDiffIL4DotNet.Models;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.PropertyBased
{
    /// <summary>
    /// Property-based tests using FsCheck to verify invariants across random inputs.
    /// FsCheck を使用してランダム入力における不変条件を検証するプロパティベーステスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class PropertyBasedTests
    {
        // ── TextDiffer properties / TextDiffer プロパティ ──

        [Property(MaxTest = 200, Arbitrary = new[] { typeof(NonNullStringArrayArbitrary) })]
        public Property TextDiffer_ContextLineCount_NeverExceedsTotalLines(string[] oldLines, string[] newLines)
        {
            // Property: the number of context lines in a diff never exceeds max(|old|, |new|)
            // プロパティ: 差分中のコンテキスト行数は max(|old|, |new|) を超えない
            var diff = TextDiffer.Compute(oldLines, newLines, contextLines: 3, maxOutputLines: 50000, maxEditDistance: 500);
            var contextCount = diff.Count(d => d.Kind == TextDiffer.Context);
            var maxSource = Math.Max(oldLines.Length, newLines.Length);
            return (contextCount <= maxSource).ToProperty();
        }

        [Property(MaxTest = 200, Arbitrary = new[] { typeof(NonNullStringArrayArbitrary) })]
        public Property TextDiffer_IdenticalInputs_ProducesNoChanges(string[] lines)
        {
            // Property: diff of identical inputs has no additions or removals
            // プロパティ: 同一入力の差分には追加/削除がない
            var diff = TextDiffer.Compute(lines, lines, contextLines: 0, maxOutputLines: 50000, maxEditDistance: 500);
            return diff.All(d => d.Kind != TextDiffer.Added && d.Kind != TextDiffer.Removed).ToProperty();
        }

        [Property(MaxTest = 200, Arbitrary = new[] { typeof(NonNullStringArrayArbitrary) })]
        public Property TextDiffer_Diff_RemovedCountMatchesOldOnlyLines(string[] oldLines, string[] newLines)
        {
            // Property: applying diff should reconstruct new lines from old lines
            // プロパティ: 差分を適用すると old lines から new lines を再構成できる
            var diff = TextDiffer.Compute(oldLines, newLines, contextLines: 3, maxOutputLines: 50000, maxEditDistance: 500);

            // If diff was truncated, skip this check / 差分が打ち切られた場合はスキップ
            if (diff.Any(d => d.Kind == TextDiffer.Truncated))
                return true.ToProperty();

            // Reconstruct new lines from diff: context and added lines form the new file
            // 差分から新ファイルを再構成: コンテキスト行と追加行が新ファイルを構成
            var reconstructed = diff
                .Where(d => d.Kind == TextDiffer.Context || d.Kind == TextDiffer.Added)
                .Select(d => d.Text)
                .ToArray();

            return reconstructed.SequenceEqual(newLines).ToProperty();
        }

        [Property(MaxTest = 100)]
        public Property TextDiffer_MaxOutputLines_Respected(PositiveInt maxLines)
        {
            // Property: output never exceeds maxOutputLines
            // プロパティ: 出力は maxOutputLines を超えない
            int limit = Math.Min(maxLines.Get, 10000);
            var old = Enumerable.Range(0, 50).Select(i => $"old line {i}").ToArray();
            var @new = Enumerable.Range(0, 50).Select(i => $"new line {i}").ToArray();
            var diff = TextDiffer.Compute(old, @new, contextLines: 3, maxOutputLines: limit, maxEditDistance: 500);
            return (diff.Count <= limit).ToProperty();
        }

        // ── TextSanitizer properties / TextSanitizer プロパティ ──

        [Property(MaxTest = 200)]
        public Property TextSanitizer_Sanitize_IsIdempotent(NonNull<string> input)
        {
            // Property: sanitizing twice produces the same result as sanitizing once
            // プロパティ: 2回サニタイズしても1回と同じ結果
            var once = TextSanitizer.Sanitize(input.Get);
            var twice = TextSanitizer.Sanitize(once);
            return (once == twice).ToProperty();
        }

        [Property(MaxTest = 200)]
        public Property TextSanitizer_Sanitize_NeverContainsPathSeparators(NonNull<string> input)
        {
            // Property: sanitized output never contains path separators or colons
            // プロパティ: サニタイズ結果にパス区切りやコロンが含まれない
            var result = TextSanitizer.Sanitize(input.Get);
            return (!result.Contains('\\') && !result.Contains('/') && !result.Contains(':') && !result.Contains("..")).ToProperty();
        }

        [Property(MaxTest = 200)]
        public Property TextSanitizer_Sanitize_PreservesEmptyAndNull()
        {
            // Property: null and empty strings produce empty output
            // プロパティ: null と空文字列は空の出力を返す
            return (TextSanitizer.Sanitize(null!) == string.Empty
                 && TextSanitizer.Sanitize(string.Empty) == string.Empty).ToProperty();
        }

        // ── FileDiffResultLists properties / FileDiffResultLists プロパティ ──

        [Property(MaxTest = 100)]
        public Property FileDiffResultLists_Statistics_SumEqualsTotal(
            NonNegativeInt addedCount,
            NonNegativeInt removedCount,
            NonNegativeInt unchangedCount,
            NonNegativeInt modifiedCount)
        {
            // Property: sum of all categories equals total compared
            // プロパティ: 全カテゴリの合計 = 比較合計
            int a = Math.Min(addedCount.Get, 100);
            int r = Math.Min(removedCount.Get, 100);
            int u = Math.Min(unchangedCount.Get, 100);
            int m = Math.Min(modifiedCount.Get, 100);

            var resultLists = new FileDiffResultLists();
            for (int i = 0; i < a; i++) resultLists.AddedFiles.Add($"added_{i}.dll");
            for (int i = 0; i < r; i++) resultLists.RemovedFiles.Add($"removed_{i}.dll");
            for (int i = 0; i < u; i++) resultLists.UnchangedFiles.Add($"unchanged_{i}.dll");
            for (int i = 0; i < m; i++) resultLists.ModifiedFiles.Add($"modified_{i}.dll");

            var stats = resultLists.SummaryStatistics;
            return (stats.AddedCount == a
                 && stats.RemovedCount == r
                 && stats.UnchangedCount == u
                 && stats.ModifiedCount == m).ToProperty();
        }

        // ── ConfigSettings round-trip properties / ConfigSettings ラウンドトリッププロパティ ──

        [Property(MaxTest = 50)]
        public Property ConfigSettings_MaxParallelism_ClampedToPositive(int value)
        {
            // Property: MaxParallelism is always >= 1 after Build
            // プロパティ: Build 後の MaxParallelism は常に >= 1
            var builder = new ConfigSettingsBuilder { MaxParallelism = value };
            var config = builder.Build();
            return (config.MaxParallelism >= 1).ToProperty();
        }

        [Property(MaxTest = 50)]
        public Property ConfigSettings_MaxLogGenerations_DefaultIsPositive()
        {
            // Property: default MaxLogGenerations is always positive
            // プロパティ: デフォルトの MaxLogGenerations は常に正の値
            var config = new ConfigSettingsBuilder().Build();
            return (config.MaxLogGenerations >= 1).ToProperty();
        }
    }

    /// <summary>
    /// Custom arbitrary for generating non-null string arrays with reasonable sizes.
    /// 適度なサイズの非 null 文字列配列を生成するカスタム Arbitrary。
    /// </summary>
    public static class NonNullStringArrayArbitrary
    {
        public static Arbitrary<string[]> StringArray()
        {
            return Gen.Choose(0, 30)
                .SelectMany(len =>
                    Gen.ArrayOf(len, Arb.Generate<NonNull<string>>().Select(s => s.Get)))
                .ToArbitrary();
        }
    }
}
