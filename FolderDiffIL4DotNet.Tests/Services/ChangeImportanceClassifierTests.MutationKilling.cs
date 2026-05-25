using System.Linq;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Mutation-killing tests for <see cref="ChangeImportanceClassifier"/>.
    /// <see cref="ChangeImportanceClassifier"/> のミューテーションキリングテスト。
    /// </summary>
    public sealed partial class ChangeImportanceClassifierTests
    {
        // ── Mutation-killing: Removed branch — all access modifiers ─────
        // ミューテーションキル: Removed ブランチ — 全アクセス修飾子

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_RemovedPrivateProtectedMethod_ReturnsMedium()
        {
            // private protected has ordinal 1, below threshold of 3 → Medium
            // private protected は序数 1、閾値 3 未満 → Medium
            var entry = new MemberChangeEntry("Removed", "MyApp.Service", "", "private protected", "", "Method", "Helper", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Mutation-killing: Added branch — all access modifiers ───────
        // ミューテーションキル: Added ブランチ — 全アクセス修飾子

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_AddedProtectedInternalMethod_ReturnsMedium()
        {
            // protected internal has ordinal 4, >= 3 → Medium for Added
            // protected internal は序数 4、>= 3 → Added の場合 Medium
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "", "protected internal", "", "Method", "OnInit", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_AddedPrivateProtectedMethod_ReturnsLow()
        {
            // private protected has ordinal 1, below 3 → Low for Added
            // private protected は序数 1、3 未満 → Added の場合 Low
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "", "private protected", "", "Method", "Helper", "", "void", "", "");
            Assert.Equal(ChangeImportance.Low, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Mutation-killing: Modified — access narrowing boundary ──────
        // ミューテーションキル: Modified — アクセス縮小の境界

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedAccessNarrowedPublicToPrivate_ReturnsHigh()
        {
            // public (5) → private (0): oldOrd > newOrd && oldOrd >= 3 → High
            // public (5) → private (0): oldOrd > newOrd かつ oldOrd >= 3 → High
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public → private", "", "Method", "Execute", "", "void", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedAccessNarrowedProtectedInternalToInternal_ReturnsHigh()
        {
            // protected internal (4) → internal (2): narrowing from >= 3 → High
            // protected internal (4) → internal (2): >= 3 からの縮小 → High
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "protected internal → internal", "", "Method", "Helper", "", "void", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedAccessNarrowedProtectedToPrivateProtected_ReturnsHigh()
        {
            // protected (3) → private protected (1): narrowing from exactly 3 → High
            // protected (3) → private protected (1): ちょうど 3 からの縮小 → High
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "protected → private protected", "", "Method", "OnInit", "", "void", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedAccessWidenedPrivateToProtected_ReturnsMedium()
        {
            // private (0) → protected (3): widening, not narrowing → Medium
            // private (0) → protected (3): 拡大であり縮小ではない → Medium
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "private → protected", "", "Method", "OnInit", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedAccessWidenedPrivateProtectedToProtected_ReturnsMedium()
        {
            // private protected (1) → protected (3): widening → Medium
            // private protected (1) → protected (3): 拡大 → Medium
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "private protected → protected", "", "Method", "Helper", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Mutation-killing: Modified — signature field arrows ─────────
        // ミューテーションキル: Modified — シグネチャフィールドのアロー

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedReturnTypeArrowOnly_ReturnsHigh()
        {
            // Only ReturnType contains arrow, no other fields → High from return type check
            // ReturnType のみにアローあり、他フィールドなし → 戻り値型チェックで High
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public", "", "Method", "Get", "", "int → string", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedMemberTypeArrowOnly_ReturnsHigh()
        {
            // Only MemberType contains arrow → High from member type check
            // MemberType のみにアローあり → メンバー型チェックで High
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public", "", "Property", "Value", "int → long", "", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedParametersArrowOnly_ReturnsHigh()
        {
            // Only Parameters contains arrow → High from parameter check
            // Parameters のみにアローあり → パラメータチェックで High
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public", "", "Method", "Run", "", "void", "int x → string x", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedModifiersArrowOnly_ReturnsMedium()
        {
            // Only Modifiers contains arrow, no other arrow fields → Medium from modifier check
            // Modifiers のみにアローあり、他にアローフィールドなし → 修飾子チェックで Medium
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public", "static → virtual", "Method", "Execute", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedBodyOnlyNoArrows_ReturnsLow()
        {
            // No arrows in any field, just body changed → Low (body-only)
            // どのフィールドにもアローなし、body のみ変更 → Low（body のみ）
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "internal", "static", "Method", "Process", "", "void", "int x", "Changed");
            Assert.Equal(ChangeImportance.Low, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Mutation-killing: Unknown/default change type ───────────────
        // ミューテーションキル: 不明/デフォルトの変更種別

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_RenamedChangeType_ReturnsLow()
        {
            // "Renamed" is not a recognized change type → default case → Low
            // "Renamed" は認識されない変更種別 → デフォルトケース → Low
            var entry = new MemberChangeEntry("Renamed", "MyApp.Service", "", "public", "", "Method", "Foo", "", "void", "", "");
            Assert.Equal(ChangeImportance.Low, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Mutation-killing: WithClassifiedImportance correctness ──────
        // ミューテーションキル: WithClassifiedImportance の正確性

        [Fact]
        [Trait("Category", "Unit")]
        public void WithClassifiedImportance_AddedPublic_SetsMedium()
        {
            // Verify importance is correctly set, not just default Low / 重要度がデフォルト Low ではなく正しく設定されることを検証
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "", "public", "", "Method", "NewApi", "", "void", "", "");
            var result = ChangeImportanceClassifier.WithClassifiedImportance(entry);
            Assert.Equal(ChangeImportance.Medium, result.Importance);
            Assert.NotEqual(ChangeImportance.Low, result.Importance);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void WithClassifiedImportance_ModifiedReturnType_SetsHigh()
        {
            // Verify High importance flows through WithClassifiedImportance / High 重要度が WithClassifiedImportance を通じて正しく設定されることを検証
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public", "", "Method", "Get", "", "int → string", "", "");
            var result = ChangeImportanceClassifier.WithClassifiedImportance(entry);
            Assert.Equal(ChangeImportance.High, result.Importance);
        }

        // ── Mutation-killing: VisibilityOrdinal boundary values ─────────
        // ミューテーションキル: VisibilityOrdinal の境界値

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_EmptyAccess_TreatedAsNonPublic()
        {
            // Empty access string → ordinal -1, not public/protected
            // 空アクセス文字列 → 序数 -1、public/protected ではない
            var removedEntry = new MemberChangeEntry("Removed", "MyApp.Service", "", "", "", "Method", "Foo", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(removedEntry));

            var addedEntry = new MemberChangeEntry("Added", "MyApp.Service", "", "", "", "Method", "Foo", "", "void", "", "");
            Assert.Equal(ChangeImportance.Low, ChangeImportanceClassifier.Classify(addedEntry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_UnknownAccessModifier_TreatedAsNonPublic()
        {
            // Unrecognized access modifier → ordinal -1, not public/protected
            // 認識されないアクセス修飾子 → 序数 -1、public/protected ではない
            var entry = new MemberChangeEntry("Removed", "MyApp.Service", "", "file", "", "Method", "Foo", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ProtectedIsExactlyAtBoundary_IsPublicOrProtected()
        {
            // protected has ordinal 3, which is exactly >= 3 boundary → counts as public/protected
            // protected は序数 3、これはちょうど >= 3 の境界 → public/protected として扱われる
            var removedEntry = new MemberChangeEntry("Removed", "MyApp.Service", "", "protected", "", "Method", "OnInit", "", "void", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(removedEntry));

            // internal has ordinal 2, just below boundary → NOT public/protected
            // internal は序数 2、境界のちょうど下 → public/protected ではない
            var removedInternal = new MemberChangeEntry("Removed", "MyApp.Service", "", "internal", "", "Method", "Helper", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(removedInternal));
        }

        // ── Mutation-killing: GetMaxImportance with multiple entries ────
        // ミューテーションキル: 複数エントリでの最大重要度

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_MultipleEntries_HighestWins()
        {
            // Verify that when classifying multiple entries, the highest importance is correctly identified
            // 複数エントリの分類時に最大重要度が正しく特定されることを検証
            var entries = new[]
            {
                new MemberChangeEntry("Added", "MyApp.Service", "", "private", "", "Field", "_x", "int", "", "", ""),
                new MemberChangeEntry("Removed", "MyApp.Service", "", "public", "", "Method", "Execute", "", "void", "", ""),
                new MemberChangeEntry("Added", "MyApp.Service", "", "public", "", "Method", "NewApi", "", "void", "", ""),
            };

            var classified = entries.Select(ChangeImportanceClassifier.WithClassifiedImportance).ToList();

            Assert.Equal(ChangeImportance.Low, classified[0].Importance);
            Assert.Equal(ChangeImportance.High, classified[1].Importance);
            Assert.Equal(ChangeImportance.Medium, classified[2].Importance);

            // Max importance across all entries should be High / 全エントリの最大重要度は High
            var max = classified.Max(e => e.Importance);
            Assert.Equal(ChangeImportance.High, max);
        }

        // ── Mutation-killing: Access narrowing — oldOrd > newOrd condition ──
        // ミューテーションキル: アクセス縮小 — oldOrd > newOrd 条件

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedAccessSameLevel_ReturnsLow()
        {
            // Same ordinal on both sides (public → public) → not narrowing, no other changes → body-only Low
            // 両辺同一序数 (public → public) → 縮小ではない、他に変更なし → body のみ Low
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public → public", "", "Method", "Execute", "", "void", "", "Changed");
            // access arrow exists but no narrowing (equal), falls through to access widening check (also equal, no narrowing), then body-only
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Mutation-killing: VisibilityOrdinal exhaustive ordinal values ──
        // ミューテーションキル: VisibilityOrdinal の全序数値を網羅

        [Theory]
        [Trait("Category", "Unit")]
        [InlineData("public", ChangeImportance.High)]
        [InlineData("protected internal", ChangeImportance.High)]
        [InlineData("protected", ChangeImportance.High)]
        [InlineData("internal", ChangeImportance.Medium)]
        [InlineData("private protected", ChangeImportance.Medium)]
        [InlineData("private", ChangeImportance.Medium)]
        public void Classify_RemovedAllAccessModifiers_ReturnsExpectedImportance(string access, ChangeImportance expected)
        {
            // Each access modifier tested via Removed branch to verify ordinal boundaries
            // Removed ブランチを通じて各アクセス修飾子の序数境界を検証
            var entry = new MemberChangeEntry("Removed", "MyApp.Service", "", access, "", "Method", "Foo", "", "void", "", "");
            Assert.Equal(expected, ChangeImportanceClassifier.Classify(entry));
        }

        [Theory]
        [Trait("Category", "Unit")]
        [InlineData("public", ChangeImportance.Medium)]
        [InlineData("protected internal", ChangeImportance.Medium)]
        [InlineData("protected", ChangeImportance.Medium)]
        [InlineData("internal", ChangeImportance.Low)]
        [InlineData("private protected", ChangeImportance.Low)]
        [InlineData("private", ChangeImportance.Low)]
        public void Classify_AddedAllAccessModifiers_ReturnsExpectedImportance(string access, ChangeImportance expected)
        {
            // Each access modifier tested via Added branch to verify ordinal boundaries
            // Added ブランチを通じて各アクセス修飾子の序数境界を検証
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "", access, "", "Method", "Bar", "", "void", "", "");
            Assert.Equal(expected, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Mutation-killing: GetMaxImportance returns correct max ──────────
        // ミューテーションキル: GetMaxImportance が正しい最大値を返す

        [Fact]
        [Trait("Category", "Unit")]
        public void GetMaxImportance_AllLow_ReturnsLow()
        {
            // When all entries are Low, max should be Low (not Medium or High)
            // すべてのエントリが Low の場合、最大値は Low（Medium や High ではない）
            var entries = new[]
            {
                new MemberChangeEntry("Added", "MyApp.Service", "", "private", "", "Field", "_a", "int", "", "", ""),
                new MemberChangeEntry("Added", "MyApp.Service", "", "internal", "", "Field", "_b", "int", "", "", ""),
            };
            var classified = entries.Select(ChangeImportanceClassifier.WithClassifiedImportance).ToList();
            Assert.All(classified, c => Assert.Equal(ChangeImportance.Low, c.Importance));
            Assert.Equal(ChangeImportance.Low, classified.Max(e => e.Importance));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetMaxImportance_MediumAndLow_ReturnsMedium()
        {
            // Medium is the highest when no High entries exist
            // High エントリがない場合、Medium が最大値
            var entries = new[]
            {
                new MemberChangeEntry("Added", "MyApp.Service", "", "private", "", "Field", "_a", "int", "", "", ""),
                new MemberChangeEntry("Added", "MyApp.Service", "", "public", "", "Method", "Api", "", "void", "", ""),
            };
            var classified = entries.Select(ChangeImportanceClassifier.WithClassifiedImportance).ToList();
            Assert.Equal(ChangeImportance.Low, classified[0].Importance);
            Assert.Equal(ChangeImportance.Medium, classified[1].Importance);
            Assert.Equal(ChangeImportance.Medium, classified.Max(e => e.Importance));
        }

        // ── Mutation-killing: Modified access narrowing — boundary at ordinal 3 ──
        // ミューテーションキル: Modified アクセス縮小 — 序数 3 の境界

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedAccessNarrowedInternalToPrivate_NotFromPublicOrProtected_ReturnsMedium()
        {
            // internal (2) → private (0): oldOrd < 3 so narrowing check fails, falls to access widening (arrow present) → Medium
            // internal (2) → private (0): oldOrd < 3 のため縮小チェック不成立、アクセス変更として Medium
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "internal → private", "", "Method", "Helper", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedAccessWidenedPrivateProtectedToInternal_ReturnsMedium()
        {
            // private protected (1) → internal (2): widening, both below boundary → Medium via access arrow
            // private protected (1) → internal (2): 拡大、両方とも境界未満 → アクセスアローで Medium
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "private protected → internal", "", "Method", "Helper", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Mutation-killing: ContainsArrow false paths (no arrow in fields) ──
        // ミューテーションキル: ContainsArrow の false パス（フィールドにアローなし）

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedNoArrowsEmptyBody_ReturnsLow()
        {
            // No arrows in any field, empty body → falls through all checks → Low
            // どのフィールドにもアローなし、body も空 → すべてのチェックを通過 → Low
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public", "", "Method", "Noop", "", "void", "", "");
            Assert.Equal(ChangeImportance.Low, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Mutation-killing: Multiple arrow fields — priority order ────────
        // ミューテーションキル: 複数アローフィールド — 優先順位

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedReturnTypeAndParameters_BothArrows_ReturnsHighFromReturnType()
        {
            // Return type arrow is checked before parameters — ensures correct priority
            // 戻り値型アローはパラメータより先にチェックされる — 正しい優先順位を保証
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public", "", "Method", "Get", "", "int → string", "int x → string x", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ModifiedMemberTypeAndModifiers_BothArrows_ReturnsHighFromMemberType()
        {
            // MemberType arrow is checked before Modifiers — ensures correct priority
            // MemberType アローは Modifiers より先にチェックされる — 正しい優先順位を保証
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public", "static → virtual", "Property", "Value", "int → long", "", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Mutation-killing: default case in switch ────────────────────────
        // ミューテーションキル: switch のデフォルトケース

        [Theory]
        [Trait("Category", "Unit")]
        [InlineData("Renamed")]
        [InlineData("Moved")]
        [InlineData("")]
        public void Classify_UnrecognizedChangeType_ReturnsLow(string changeType)
        {
            // Any unrecognized Change value should fall through to default → Low
            // 認識されない Change 値はデフォルトに落ちて Low を返す
            var entry = new MemberChangeEntry(changeType, "MyApp.Service", "", "public", "", "Method", "Foo", "", "void", "", "");
            Assert.Equal(ChangeImportance.Low, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Mutation-killing: WithClassifiedImportance returns correct importance for each level ──
        // ミューテーションキル: WithClassifiedImportance が各レベルの正しい重要度を返す

        [Fact]
        [Trait("Category", "Unit")]
        public void WithClassifiedImportance_LowImportance_ReturnsLowNotDefault()
        {
            // Verify that Low importance is explicitly classified, not just the record default
            // Low 重要度がレコードのデフォルトではなく明示的に分類されることを検証
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "", "private", "", "Method", "Helper", "", "void", "", "");
            var result = ChangeImportanceClassifier.WithClassifiedImportance(entry);
            Assert.Equal(ChangeImportance.Low, result.Importance);
            Assert.Equal("Added", result.Change);
            Assert.Equal("private", result.Access);
        }

        // ── Mutation-killing: IsPublicOrProtected boundary — ordinal 2 vs 3 ──
        // ミューテーションキル: IsPublicOrProtected 境界 — 序数 2 対 3

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_InternalIsNotPublicOrProtected_AddedReturnsLow()
        {
            // internal (ordinal=2) is just below >= 3 boundary → NOT public/protected → Low for Added
            // internal（序数=2）は >= 3 境界のちょうど下 → public/protected ではない → Added で Low
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "", "internal", "", "Method", "Helper", "", "void", "", "");
            Assert.Equal(ChangeImportance.Low, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Classify_ProtectedIsPublicOrProtected_AddedReturnsMedium()
        {
            // protected (ordinal=3) is exactly at >= 3 boundary → IS public/protected → Medium for Added
            // protected（序数=3）はちょうど >= 3 境界 → public/protected である → Added で Medium
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "", "protected", "", "Method", "OnInit", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }
    }
}
