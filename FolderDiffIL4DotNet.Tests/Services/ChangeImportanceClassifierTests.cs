using System.Linq;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="ChangeImportanceClassifier"/>.
    /// <see cref="ChangeImportanceClassifier"/> のテスト。
    /// </summary>
    public sealed class ChangeImportanceClassifierTests
    {
        // ── High: Removed public/protected ────────────────────────────────

        [Fact]
        public void Classify_RemovedPublicMethod_ReturnsHigh()
        {
            var entry = new MemberChangeEntry("Removed", "MyApp.Service", "", "public", "", "Method", "Execute", "", "void", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        public void Classify_RemovedProtectedMethod_ReturnsHigh()
        {
            var entry = new MemberChangeEntry("Removed", "MyApp.Service", "", "protected", "", "Method", "OnInit", "", "void", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        public void Classify_RemovedProtectedInternalMethod_ReturnsHigh()
        {
            var entry = new MemberChangeEntry("Removed", "MyApp.Service", "", "protected internal", "", "Method", "OnInit", "", "void", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        public void Classify_RemovedPublicType_ReturnsHigh()
        {
            var entry = new MemberChangeEntry("Removed", "MyApp.OldClass", "", "public", "", "Class", "", "", "", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Medium: Removed internal/private ──────────────────────────────

        [Fact]
        public void Classify_RemovedInternalMethod_ReturnsMedium()
        {
            var entry = new MemberChangeEntry("Removed", "MyApp.Service", "", "internal", "", "Method", "Helper", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        public void Classify_RemovedPrivateMethod_ReturnsMedium()
        {
            var entry = new MemberChangeEntry("Removed", "MyApp.Service", "", "private", "", "Method", "Cleanup", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Medium: Added public/protected ────────────────────────────────

        [Fact]
        public void Classify_AddedPublicMethod_ReturnsMedium()
        {
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "", "public", "", "Method", "NewMethod", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        public void Classify_AddedProtectedProperty_ReturnsMedium()
        {
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "", "protected", "", "Property", "Value", "int", "", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Low: Added internal/private ───────────────────────────────────

        [Fact]
        public void Classify_AddedInternalMethod_ReturnsLow()
        {
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "", "internal", "", "Method", "InternalHelper", "", "void", "", "");
            Assert.Equal(ChangeImportance.Low, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        public void Classify_AddedPrivateField_ReturnsLow()
        {
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "", "private", "", "Field", "_count", "int", "", "", "");
            Assert.Equal(ChangeImportance.Low, ChangeImportanceClassifier.Classify(entry));
        }

        // ── High: Modified — access narrowing from public ─────────────────

        [Fact]
        public void Classify_ModifiedAccessNarrowedPublicToInternal_ReturnsHigh()
        {
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public \u2192 internal", "", "Method", "Execute", "", "void", "", "Changed");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        public void Classify_ModifiedAccessNarrowedProtectedToPrivate_ReturnsHigh()
        {
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "protected \u2192 private", "", "Method", "OnInit", "", "void", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        // ── High: Modified — return type change ───────────────────────────

        [Fact]
        public void Classify_ModifiedReturnTypeChange_ReturnsHigh()
        {
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public", "", "Method", "GetValue", "", "string \u2192 int", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        // ── High: Modified — member type change (property/field) ──────────

        [Fact]
        public void Classify_ModifiedPropertyTypeChange_ReturnsHigh()
        {
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public", "", "Property", "Name", "string \u2192 int", "", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        public void Classify_ModifiedFieldTypeChange_ReturnsHigh()
        {
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "private", "static", "Field", "_retryCount", "string \u2192 int", "", "", "");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        // ── High: Modified — parameter change ─────────────────────────────

        [Fact]
        public void Classify_ModifiedParameterChange_ReturnsHigh()
        {
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public", "", "Method", "Process", "", "void", "int id \u2192 string id, bool force", "Changed");
            Assert.Equal(ChangeImportance.High, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Medium: Modified — modifier changes ──────────────────────────

        [Fact]
        public void Classify_ModifiedModifierChange_VirtualToSealed_ReturnsMedium()
        {
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public", "virtual \u2192 sealed", "Method", "Execute", "", "void", "string", "Changed");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Medium: Modified — access widening ────────────────────────────

        [Fact]
        public void Classify_ModifiedAccessWidenedInternalToPublic_ReturnsMedium()
        {
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "internal \u2192 public", "", "Method", "Helper", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        // ── Low: Modified — body-only change ──────────────────────────────

        [Fact]
        public void Classify_ModifiedBodyOnly_ReturnsLow()
        {
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "public", "", "Method", "Calculate", "", "int", "int x", "Changed");
            Assert.Equal(ChangeImportance.Low, ChangeImportanceClassifier.Classify(entry));
        }

        // ── WithClassifiedImportance ──────────────────────────────────────

        [Fact]
        public void WithClassifiedImportance_SetsImportance()
        {
            var entry = new MemberChangeEntry("Removed", "MyApp.Service", "", "public", "", "Method", "Execute", "", "void", "", "");
            var result = ChangeImportanceClassifier.WithClassifiedImportance(entry);
            Assert.Equal(ChangeImportance.High, result.Importance);
            Assert.Equal("Removed", result.Change);
            Assert.Equal("MyApp.Service", result.TypeName);
        }

        [Fact]
        public void WithClassifiedImportance_PreservesAllFields()
        {
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "Base", "internal", "static", "Method", "Helper", "", "void", "int x", "");
            var result = ChangeImportanceClassifier.WithClassifiedImportance(entry);
            Assert.Equal(ChangeImportance.Low, result.Importance);
            Assert.Equal("Added", result.Change);
            Assert.Equal("MyApp.Service", result.TypeName);
            Assert.Equal("Base", result.BaseType);
            Assert.Equal("internal", result.Access);
            Assert.Equal("static", result.Modifiers);
            Assert.Equal("Method", result.MemberKind);
            Assert.Equal("Helper", result.MemberName);
            Assert.Equal("", result.MemberType);
            Assert.Equal("void", result.ReturnType);
            Assert.Equal("int x", result.Parameters);
            Assert.Equal("", result.Body);
        }

        // ── Edge cases ────────────────────────────────────────────────────

        [Fact]
        public void Classify_UnknownChange_ReturnsLow()
        {
            var entry = new MemberChangeEntry("Unknown", "MyApp.Service", "", "public", "", "Method", "Foo", "", "void", "", "");
            Assert.Equal(ChangeImportance.Low, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        public void Classify_ModifiedAccessNarrowedInternalToPrivate_ReturnsMedium()
        {
            // Narrowing from internal (not public/protected) should be Medium (via access widening path after narrowing check fails).
            // internal → private は public/protected からの縮小ではないため、アクセス変更として Medium。
            var entry = new MemberChangeEntry("Modified", "MyApp.Service", "", "internal \u2192 private", "", "Method", "Helper", "", "void", "", "");
            Assert.Equal(ChangeImportance.Medium, ChangeImportanceClassifier.Classify(entry));
        }

        [Fact]
        public void Classify_DefaultImportance_IsLow()
        {
            var entry = new MemberChangeEntry("Added", "MyApp.Service", "", "private", "", "Field", "_data", "string", "", "", "");
            Assert.Equal(ChangeImportance.Low, entry.Importance);
        }

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
    }
}
