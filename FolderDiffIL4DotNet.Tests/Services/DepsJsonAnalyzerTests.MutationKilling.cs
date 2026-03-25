/// <summary>
/// Partial class containing mutation-killing tests for DepsJsonAnalyzer.
/// DepsJsonAnalyzer のミューテーションキルテストを含むパーシャルクラス。
/// </summary>

using System;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed partial class DepsJsonAnalyzerTests
    {
        // ── Mutation-killing: Analyze with only Added dependencies ──────
        // ミューテーションキル: Added 依存のみの Analyze

        [Fact]
        public void Analyze_OnlyAddedDependencies_AllEntriesAreAdded()
        {
            // Old has no libraries, new has two → all Added
            // 旧にはライブラリなし、新に2つ → すべて Added
            var oldFile = CreateDepsJson("old.deps.json", @"{""libraries"":{}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{""PackageA/1.0.0"":{},""PackageB/2.0.0"":{}}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            Assert.True(result!.HasChanges);
            Assert.Equal(2, result.AddedCount);
            Assert.Equal(0, result.RemovedCount);
            Assert.Equal(0, result.UpdatedCount);
            Assert.All(result.Entries, e => Assert.Equal("Added", e.Change));
        }

        // ── Mutation-killing: Analyze with only Removed dependencies ────
        // ミューテーションキル: Removed 依存のみの Analyze

        [Fact]
        public void Analyze_OnlyRemovedDependencies_AllEntriesAreRemoved()
        {
            // Old has two libraries, new has none → all Removed
            // 旧に2つのライブラリ、新にはなし → すべて Removed
            var oldFile = CreateDepsJson("old.deps.json", @"{""libraries"":{""PackageA/1.0.0"":{},""PackageB/2.0.0"":{}}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            Assert.True(result!.HasChanges);
            Assert.Equal(0, result.AddedCount);
            Assert.Equal(2, result.RemovedCount);
            Assert.Equal(0, result.UpdatedCount);
            Assert.All(result.Entries, e => Assert.Equal("Removed", e.Change));
        }

        // ── Mutation-killing: Analyze with only Updated dependencies ────
        // ミューテーションキル: Updated 依存のみの Analyze

        [Fact]
        public void Analyze_OnlyUpdatedDependencies_AllEntriesAreUpdated()
        {
            // Same packages, different versions → all Updated
            // 同じパッケージ、異なるバージョン → すべて Updated
            var oldFile = CreateDepsJson("old.deps.json", @"{""libraries"":{""A/1.0.0"":{},""B/2.0.0"":{}}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{""A/1.1.0"":{},""B/3.0.0"":{}}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            Assert.True(result!.HasChanges);
            Assert.Equal(0, result.AddedCount);
            Assert.Equal(0, result.RemovedCount);
            Assert.Equal(2, result.UpdatedCount);
            Assert.All(result.Entries, e => Assert.Equal("Updated", e.Change));
        }

        // ── Mutation-killing: Same version should NOT record as update ──
        // ミューテーションキル: 同一バージョンは更新として記録しない

        [Fact]
        public void Analyze_SameVersion_NotRecordedAsUpdate()
        {
            // Identical version strings → no change recorded
            // 同一バージョン文字列 → 変更は記録されない
            var oldFile = CreateDepsJson("old.deps.json", @"{""libraries"":{""PackageA/1.2.3"":{}}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{""PackageA/1.2.3"":{}}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            Assert.False(result!.HasChanges);
            Assert.Empty(result.Entries);
            Assert.Equal(0, result.UpdatedCount);
        }

        // ── Mutation-killing: Mix of all three change types ─────────────
        // ミューテーションキル: 3種類の変更すべてのミックス

        [Fact]
        public void Analyze_MixOfAllChangeTypes_CorrectCounts()
        {
            // A: updated (1.0→2.0), B: removed, C: unchanged, D: added
            // A: 更新 (1.0→2.0), B: 削除, C: 変更なし, D: 追加
            var oldFile = CreateDepsJson("old.deps.json", @"{""libraries"":{""A/1.0.0"":{},""B/1.0.0"":{},""C/1.0.0"":{}}}");
            var newFile = CreateDepsJson("new.deps.json", @"{""libraries"":{""A/2.0.0"":{},""C/1.0.0"":{},""D/1.0.0"":{}}}");

            var result = DepsJsonAnalyzer.Analyze(oldFile, newFile);

            Assert.NotNull(result);
            Assert.True(result!.HasChanges);
            Assert.Equal(1, result.AddedCount);
            Assert.Equal(1, result.RemovedCount);
            Assert.Equal(1, result.UpdatedCount);
            Assert.Equal(3, result.Entries.Count);
        }

        // ── Mutation-killing: ClassifyVersionChange boundary values ─────
        // ミューテーションキル: ClassifyVersionChange の境界値

        [Fact]
        public void ClassifyImportance_MajorVersionDecrease_IsHigh()
        {
            // Major version decrease is also a major change → High
            // メジャーバージョン減少も重要な変更 → High
            var entry = new DependencyChangeEntry("Updated", "PackageA", "3.0.0", "2.0.0");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.High, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_MinorVersionDecrease_IsMedium()
        {
            // Minor version decrease with same major → Medium
            // メジャー同一でマイナーバージョン減少 → Medium
            var entry = new DependencyChangeEntry("Updated", "PackageA", "1.5.0", "1.2.0");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.Medium, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_PatchOnlyChange_SameMajorAndMinor_IsLow()
        {
            // Same major and minor, different patch → Low
            // メジャーとマイナーが同一、パッチのみ異なる → Low
            var entry = new DependencyChangeEntry("Updated", "PackageA", "1.0.3", "1.0.7");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.Low, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_MajorAndMinorBothChange_HighFromMajor()
        {
            // Both major and minor differ → major check triggers first → High
            // メジャーとマイナーの両方が異なる → メジャーチェックが先にトリガー → High
            var entry = new DependencyChangeEntry("Updated", "PackageA", "1.2.3", "2.5.0");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.High, classified.Importance);
        }

        // ── Mutation-killing: ParseVersionParts edge cases ──────────────
        // ミューテーションキル: ParseVersionParts のエッジケース

        [Fact]
        public void ClassifyImportance_EmptyOldVersion_ComparesAgainstZeros()
        {
            // Empty old version → parsed as (0,0,0), any non-zero new version → major change
            // 空の旧バージョン → (0,0,0) としてパース、0 でない新バージョン → メジャー変更
            var entry = new DependencyChangeEntry("Updated", "PackageA", "", "1.0.0");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.High, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_EmptyNewVersion_ComparesAgainstZeros()
        {
            // Empty new version → parsed as (0,0,0), old version non-zero → major change
            // 空の新バージョン → (0,0,0) としてパース、旧バージョンが 0 でない → メジャー変更
            var entry = new DependencyChangeEntry("Updated", "PackageA", "2.0.0", "");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.High, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_PreReleaseSuffix_StrippedCorrectly()
        {
            // Pre-release suffix is stripped before comparison → same core version = Low
            // プレリリースサフィックスは比較前に除去 → 同一コアバージョン = Low
            var entry = new DependencyChangeEntry("Updated", "PackageA", "1.2.3-alpha", "1.2.3-beta");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.Low, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_MajorOnlyVersion_ParsedCorrectly()
        {
            // "5" → (5,0,0), "6" → (6,0,0) → major change → High
            // "5" → (5,0,0), "6" → (6,0,0) → メジャー変更 → High
            var entry = new DependencyChangeEntry("Updated", "PackageA", "5", "6");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.High, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_MajorOnlySameVersion_IsLow()
        {
            // "5" → (5,0,0) on both sides → same version → Low
            // 両辺 "5" → (5,0,0) → 同一バージョン → Low
            var entry = new DependencyChangeEntry("Updated", "PackageA", "5", "5");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.Low, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_NonNumericParts_TreatedAsZero()
        {
            // Non-numeric parts → parsed as 0 → effectively (0,0,0) both → Low
            // 非数値パーツ → 0 としてパース → 実質的に両方 (0,0,0) → Low
            var entry = new DependencyChangeEntry("Updated", "PackageA", "abc.def.ghi", "xyz.uvw.rst");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.Low, classified.Importance);
        }

        [Fact]
        public void ClassifyImportance_NonNumericVsNumeric_IsMajorChange()
        {
            // Non-numeric "abc" → (0,0,0), numeric "1.0.0" → (1,0,0) → major change → High
            // 非数値 "abc" → (0,0,0), 数値 "1.0.0" → (1,0,0) → メジャー変更 → High
            var entry = new DependencyChangeEntry("Updated", "PackageA", "abc", "1.0.0");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.High, classified.Importance);
        }

        // ── Mutation-killing: HasChanges property ───────────────────────
        // ミューテーションキル: HasChanges プロパティ

        [Fact]
        public void DependencyChangeSummary_HasChanges_TrueWhenEntriesExist()
        {
            // HasChanges returns true when Entries.Count > 0
            // Entries.Count > 0 のとき HasChanges は true を返す
            var summary = new DependencyChangeSummary
            {
                Entries = new[] { new DependencyChangeEntry("Added", "A", "", "1.0.0", ChangeImportance.Medium) }
            };
            Assert.True(summary.HasChanges);
            Assert.Single(summary.Entries);
        }

        [Fact]
        public void DependencyChangeSummary_HasChanges_FalseWhenEmpty()
        {
            // HasChanges returns false when Entries is empty
            // Entries が空のとき HasChanges は false を返す
            var summary = new DependencyChangeSummary { Entries = Array.Empty<DependencyChangeEntry>() };
            Assert.False(summary.HasChanges);
            Assert.Empty(summary.Entries);
        }

        // ── Mutation-killing: ClassifyImportance default/unknown change type ──
        // ミューテーションキル: ClassifyImportance のデフォルト/不明な変更種別

        [Fact]
        public void ClassifyImportance_UnknownChangeType_ReturnsLow()
        {
            // Unknown change type falls to default case → Low
            // 不明な変更種別はデフォルトケースに落ちて Low を返す
            var entry = new DependencyChangeEntry("Unknown", "PackageA", "1.0.0", "2.0.0");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal(ChangeImportance.Low, classified.Importance);
        }

        // ── Mutation-killing: ClassifyImportance preserves all fields ────
        // ミューテーションキル: ClassifyImportance が全フィールドを保持する

        [Fact]
        public void ClassifyImportance_PreservesAllFields()
        {
            // Verify that ClassifyImportance returns a record with all original fields intact
            // ClassifyImportance が元のフィールドをすべて保持したレコードを返すことを検証
            var entry = new DependencyChangeEntry("Removed", "MyPackage", "3.2.1", "");
            var classified = DepsJsonAnalyzer.ClassifyImportance(entry);
            Assert.Equal("Removed", classified.Change);
            Assert.Equal("MyPackage", classified.PackageName);
            Assert.Equal("3.2.1", classified.OldVersion);
            Assert.Equal("", classified.NewVersion);
            Assert.Equal(ChangeImportance.High, classified.Importance);
        }

        // ── Mutation-killing: MaxImportance with empty entries ──────────
        // ミューテーションキル: 空エントリの MaxImportance

        [Fact]
        public void DependencyChangeSummary_MaxImportance_ReturnsLowWhenEmpty()
        {
            // Empty entries should return Low as default MaxImportance
            // 空のエントリではデフォルト MaxImportance として Low を返す
            var summary = new DependencyChangeSummary { Entries = Array.Empty<DependencyChangeEntry>() };
            Assert.Equal(ChangeImportance.Low, summary.MaxImportance);
        }

        // ── Mutation-killing: Importance count properties ───────────────
        // ミューテーションキル: 重要度カウントプロパティ

        [Fact]
        public void DependencyChangeSummary_ImportanceCounts_AllZeroWhenEmpty()
        {
            // All importance counts should be zero when no entries
            // エントリがない場合、すべての重要度カウントはゼロ
            var summary = new DependencyChangeSummary { Entries = Array.Empty<DependencyChangeEntry>() };
            Assert.Equal(0, summary.HighImportanceCount);
            Assert.Equal(0, summary.MediumImportanceCount);
            Assert.Equal(0, summary.LowImportanceCount);
        }

        [Fact]
        public void DependencyChangeSummary_ChangeCounts_AllZeroWhenEmpty()
        {
            // All change counts should be zero when no entries
            // エントリがない場合、すべての変更カウントはゼロ
            var summary = new DependencyChangeSummary { Entries = Array.Empty<DependencyChangeEntry>() };
            Assert.Equal(0, summary.AddedCount);
            Assert.Equal(0, summary.RemovedCount);
            Assert.Equal(0, summary.UpdatedCount);
        }
    }
}
