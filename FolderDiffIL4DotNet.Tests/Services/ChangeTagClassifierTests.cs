using System.Collections.Generic;
using System.Linq;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="ChangeTagClassifier"/>.
    /// <see cref="ChangeTagClassifier"/> のテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class ChangeTagClassifierTests
    {
        // ── Null / empty inputs ──────────────────────────────────────────────

        [Fact]
        public void Classify_BothNull_ReturnsEmpty()
        {
            var result = ChangeTagClassifier.Classify(null, null);
            Assert.Empty(result);
        }

        [Fact]
        public void Classify_EmptySemanticChanges_ReturnsEmpty()
        {
            var summary = new AssemblySemanticChangesSummary { Entries = new List<MemberChangeEntry>() };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Empty(result);
        }

        // ── Simple patterns ──────────────────────────────────────────────────

        [Fact]
        public void Classify_AddedPublicMethod_ReturnsMethodAdd()
        {
            var entries = new List<MemberChangeEntry>
            {
                new("Added", "MyApp.Service", "", "public", "", "Method", "NewFeature", "", "void", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.MethodAdd, result);
        }

        [Fact]
        public void Classify_RemovedPublicMethod_ReturnsMethodRemove()
        {
            var entries = new List<MemberChangeEntry>
            {
                new("Removed", "MyApp.Service", "", "public", "", "Method", "OldFeature", "", "void", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.MethodRemove, result);
        }

        [Fact]
        public void Classify_AddedType_ReturnsTypeAdd()
        {
            var entries = new List<MemberChangeEntry>
            {
                new("Added", "MyApp.NewClass", "", "public", "", "Class", "", "", "", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.TypeAdd, result);
        }

        [Fact]
        public void Classify_RemovedType_ReturnsTypeRemove()
        {
            var entries = new List<MemberChangeEntry>
            {
                new("Removed", "MyApp.OldClass", "", "public", "", "Class", "", "", "", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.TypeRemove, result);
        }

        [Fact]
        public void Classify_BodyOnlyChange_ReturnsBodyEdit()
        {
            var entries = new List<MemberChangeEntry>
            {
                new("Modified", "MyApp.Service", "", "public", "", "Method", "Process", "", "void", "", "Changed")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.BodyEdit, result);
        }

        [Fact]
        public void Classify_AccessOnlyChange_ReturnsAccess()
        {
            var entries = new List<MemberChangeEntry>
            {
                new("Modified", "MyApp.Service", "", "public \u2192 internal", "", "Method", "Helper", "", "void", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.Access, result);
        }

        [Fact]
        public void Classify_SignatureChange_ReturnsSignature()
        {
            var entries = new List<MemberChangeEntry>
            {
                new("Modified", "MyApp.Service", "", "public", "", "Method", "Process", "", "void \u2192 int", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.Signature, result);
        }

        // ── Compound patterns ────────────────────────────────────────────────

        [Fact]
        public void Classify_ExtractPattern_ModifiedMethodAndNewPrivateInSameType()
        {
            var entries = new List<MemberChangeEntry>
            {
                // Existing method body changed / 既存メソッドの本体変更
                new("Modified", "MyApp.Service", "", "public", "", "Method", "Process", "", "void", "", "Changed"),
                // New private method added in same type / 同一型に新 private メソッド追加
                new("Added", "MyApp.Service", "", "private", "", "Method", "ProcessHelper", "", "void", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.Extract, result);
            // The new private method should be consumed by Extract, so MethodAdd should not appear
            // Extract に消費されるので MethodAdd は出ないはず
            Assert.DoesNotContain(ChangeTag.MethodAdd, result);
        }

        [Fact]
        public void Classify_ExtractPattern_NewInternalMethodAlsoDetected()
        {
            var entries = new List<MemberChangeEntry>
            {
                new("Modified", "MyApp.Service", "", "public", "", "Method", "Run", "", "void", "", "Changed"),
                new("Added", "MyApp.Service", "", "internal", "", "Method", "RunCore", "", "void", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.Extract, result);
        }

        [Fact]
        public void Classify_InlinePattern_RemovedPrivateAndModifiedInSameType()
        {
            var entries = new List<MemberChangeEntry>
            {
                // Existing method body changed (inlined the removed method) / 既存メソッドの本体変更（削除メソッドをインライン化）
                new("Modified", "MyApp.Service", "", "public", "", "Method", "Process", "", "void", "", "Changed"),
                // Private method removed from same type / 同一型から private メソッド削除
                new("Removed", "MyApp.Service", "", "private", "", "Method", "OldHelper", "", "void", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.Inline, result);
            Assert.DoesNotContain(ChangeTag.MethodRemove, result);
        }

        [Fact]
        public void Classify_MovePattern_MethodRemovedFromOneTypeAndAddedToAnother()
        {
            var entries = new List<MemberChangeEntry>
            {
                new("Removed", "MyApp.OldService", "", "public", "", "Method", "Execute", "", "void", "", ""),
                new("Added", "MyApp.NewService", "", "public", "", "Method", "Execute", "", "void", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.Move, result);
            Assert.DoesNotContain(ChangeTag.MethodAdd, result);
            Assert.DoesNotContain(ChangeTag.MethodRemove, result);
        }

        [Fact]
        public void Classify_RenamePattern_SameTypeAndSignatureDifferentName()
        {
            var entries = new List<MemberChangeEntry>
            {
                new("Removed", "MyApp.Service", "", "public", "", "Method", "OldName", "", "void", "int page", ""),
                new("Added", "MyApp.Service", "", "public", "", "Method", "NewName", "", "void", "int page", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.Rename, result);
            Assert.DoesNotContain(ChangeTag.MethodAdd, result);
            Assert.DoesNotContain(ChangeTag.MethodRemove, result);
        }

        // ── Dependency patterns ──────────────────────────────────────────────

        [Fact]
        public void Classify_DependencyChangesOnly_ReturnsDepUpdate()
        {
            var depSummary = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Updated", "Newtonsoft.Json", "12.0.0", "13.0.0")
                }
            };
            var result = ChangeTagClassifier.Classify(null, depSummary);
            Assert.Single(result);
            Assert.Equal(ChangeTag.DepUpdate, result[0]);
        }

        [Fact]
        public void Classify_DependencyChangesWithSemanticChanges_NoDepUpdate()
        {
            var entries = new List<MemberChangeEntry>
            {
                new("Modified", "MyApp.Service", "", "public", "", "Method", "Process", "", "void", "", "Changed")
            };
            var semanticSummary = new AssemblySemanticChangesSummary { Entries = entries };
            var depSummary = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Updated", "Newtonsoft.Json", "12.0.0", "13.0.0")
                }
            };
            var result = ChangeTagClassifier.Classify(semanticSummary, depSummary);
            // When semantic changes exist, DepUpdate is not added
            // セマンティック変更がある場合、DepUpdate は追加されない
            Assert.DoesNotContain(ChangeTag.DepUpdate, result);
        }

        // ── Multiple tags ────────────────────────────────────────────────────

        [Fact]
        public void Classify_MultiplePatterns_ReturnsAllTags()
        {
            var entries = new List<MemberChangeEntry>
            {
                // Type added / 型追加
                new("Added", "MyApp.NewClass", "", "public", "", "Class", "", "", "", "", ""),
                // Body edit / ボディ変更
                new("Modified", "MyApp.Service", "", "public", "", "Method", "Run", "", "void", "", "Changed"),
                // New public method / 新 public メソッド
                new("Added", "MyApp.Service", "", "public", "", "Method", "NewFeature", "", "string", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.TypeAdd, result);
            Assert.Contains(ChangeTag.MethodAdd, result);
            Assert.Contains(ChangeTag.BodyEdit, result);
        }

        // ── FormatTags ───────────────────────────────────────────────────────

        [Fact]
        public void FormatTags_EmptyList_ReturnsEmptyString()
        {
            Assert.Equal("", ChangeTagClassifier.FormatTags(new List<ChangeTag>()));
        }

        [Fact]
        public void FormatTags_SingleTag_ReturnsLabel()
        {
            var tags = new List<ChangeTag> { ChangeTag.Extract };
            Assert.Equal("Possible Extract", ChangeTagClassifier.FormatTags(tags));
        }

        [Fact]
        public void FormatTags_MultipleTags_ReturnsCommaSeparated()
        {
            var tags = new List<ChangeTag> { ChangeTag.Extract, ChangeTag.MethodAdd };
            Assert.Equal("Possible Extract, +Method", ChangeTagClassifier.FormatTags(tags));
        }

        // ── GetLabel ─────────────────────────────────────────────────────────

        [Theory]
        [InlineData(ChangeTag.MethodAdd, "+Method")]
        [InlineData(ChangeTag.MethodRemove, "-Method")]
        [InlineData(ChangeTag.TypeAdd, "+Type")]
        [InlineData(ChangeTag.TypeRemove, "-Type")]
        [InlineData(ChangeTag.Extract, "Possible Extract")]
        [InlineData(ChangeTag.Inline, "Possible Inline")]
        [InlineData(ChangeTag.Move, "Possible Move")]
        [InlineData(ChangeTag.Rename, "Possible Rename")]
        [InlineData(ChangeTag.Signature, "Signature")]
        [InlineData(ChangeTag.Access, "Access")]
        [InlineData(ChangeTag.BodyEdit, "BodyEdit")]
        [InlineData(ChangeTag.DepUpdate, "DepUpdate")]
        public void GetLabel_ReturnsExpectedLabel(ChangeTag tag, string expected)
        {
            Assert.Equal(expected, ChangeTagClassifier.GetLabel(tag));
        }

        // ── Edge cases ───────────────────────────────────────────────────────

        [Fact]
        public void Classify_ExtractNotTriggeredForPublicAddedMethod()
        {
            var entries = new List<MemberChangeEntry>
            {
                new("Modified", "MyApp.Service", "", "public", "", "Method", "Process", "", "void", "", "Changed"),
                // Public method added — should NOT be consumed by Extract
                // public メソッド追加 — Extract に消費されるべきでない
                new("Added", "MyApp.Service", "", "public", "", "Method", "NewPublicMethod", "", "void", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.DoesNotContain(ChangeTag.Extract, result);
            Assert.Contains(ChangeTag.MethodAdd, result);
            Assert.Contains(ChangeTag.BodyEdit, result);
        }

        [Fact]
        public void Classify_MoveNotTriggeredForSameType()
        {
            var entries = new List<MemberChangeEntry>
            {
                // Same type — this is a Rename candidate, not Move
                // 同一型 — Move ではなく Rename 候補
                new("Removed", "MyApp.Service", "", "public", "", "Method", "OldName", "", "void", "", ""),
                new("Added", "MyApp.Service", "", "public", "", "Method", "NewName", "", "void", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.DoesNotContain(ChangeTag.Move, result);
            Assert.Contains(ChangeTag.Rename, result);
        }

        [Fact]
        public void Classify_PropertySignatureChange_ReturnsSignature()
        {
            var entries = new List<MemberChangeEntry>
            {
                new("Modified", "MyApp.Config", "", "public", "", "Property", "Timeout", "int \u2192 TimeSpan", "", "", "")
            };
            var summary = new AssemblySemanticChangesSummary { Entries = entries };
            var result = ChangeTagClassifier.Classify(summary, null);
            Assert.Contains(ChangeTag.Signature, result);
        }
    }
}
