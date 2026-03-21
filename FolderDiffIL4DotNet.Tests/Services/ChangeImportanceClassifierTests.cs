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
    }
}
