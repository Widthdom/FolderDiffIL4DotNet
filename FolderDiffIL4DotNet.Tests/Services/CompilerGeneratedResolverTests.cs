using System.Collections.Generic;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="CompilerGeneratedResolver"/> covering annotation of
    /// compiler-generated types and members with their user-authored origin.
    /// <see cref="CompilerGeneratedResolver"/> のテスト。コンパイラ生成の型・メンバーに
    /// ユーザー記述元の注釈を付ける機能を検証します。
    /// </summary>
    public sealed class CompilerGeneratedResolverTests
    {
        // --- AnnotateEntry tests / AnnotateEntry テスト ---

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_AsyncStateMachine_AnnotatesTypeName()
        {
            var entry = MakeEntry(typeName: "MyNamespace.MyClass/<DoWork>d__0", memberName: "MoveNext");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Contains("state machine of MyNamespace.MyClass.DoWork", result.TypeName);
            Assert.Equal("MoveNext", result.MemberName);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_AsyncStateMachineWithoutNumber_AnnotatesTypeName()
        {
            var entry = MakeEntry(typeName: "MyClass/<RunAsync>d", memberName: "MoveNext");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Contains("state machine of MyClass.RunAsync", result.TypeName);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_DisplayClass_AnnotatesTypeName()
        {
            var entry = MakeEntry(typeName: "MyClass/<>c__DisplayClass5_0", memberName: "field1");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Contains("closure of MyClass", result.TypeName);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_DisplayClassSimple_AnnotatesTypeName()
        {
            var entry = MakeEntry(typeName: "MyClass/<>c", memberName: "<Process>b__0");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Contains("closure of MyClass", result.TypeName);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_LambdaMethod_AnnotatesMemberName()
        {
            var entry = MakeEntry(typeName: "MyClass", memberName: "<DoWork>b__5_0");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Contains("lambda in DoWork", result.MemberName);
            Assert.Equal("MyClass", result.TypeName);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_BackingField_AnnotatesMemberName()
        {
            var entry = MakeEntry(typeName: "MyClass", memberName: "<Name>k__BackingField", memberKind: "Field");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Contains("backing field of Name", result.MemberName);
            Assert.Equal("MyClass", result.TypeName);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_LocalFunction_AnnotatesMemberName()
        {
            // Local function: <ProcessData>g__Validate|0_0
            // ローカル関数: <ProcessData>g__Validate|0_0
            var entry = MakeEntry(typeName: "MyClass", memberName: "<ProcessData>g__Validate|0_0");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Contains("local function Validate in ProcessData", result.MemberName);
            Assert.Equal("MyClass", result.TypeName);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_LocalFunctionNestedIndex_AnnotatesMemberName()
        {
            // Local function with nested index: <Run>g__Helper|2_1
            // ネストインデックス付きローカル関数: <Run>g__Helper|2_1
            var entry = MakeEntry(typeName: "MyClass", memberName: "<Run>g__Helper|2_1");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Contains("local function Helper in Run", result.MemberName);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_RecordClone_AnnotatesMemberName()
        {
            // Record clone method: <Clone>$
            // record クローンメソッド: <Clone>$
            var entry = MakeEntry(typeName: "MyRecord", memberName: "<Clone>$");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Contains("record clone method", result.MemberName);
            Assert.Equal("MyRecord", result.TypeName);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_RecordPrintMembers_AnnotatesMemberName()
        {
            var entry = MakeEntry(typeName: "MyRecord", memberName: "PrintMembers");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Contains("record synthesized", result.MemberName);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_RecordOpEquality_AnnotatesMemberName()
        {
            var entry = MakeEntry(typeName: "MyRecord", memberName: "op_Equality");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Contains("record synthesized", result.MemberName);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_RecordOpInequality_AnnotatesMemberName()
        {
            var entry = MakeEntry(typeName: "MyRecord", memberName: "op_Inequality");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Contains("record synthesized", result.MemberName);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_RegularMember_ReturnsUnchanged()
        {
            var entry = MakeEntry(typeName: "MyClass", memberName: "Process");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Equal("MyClass", result.TypeName);
            Assert.Equal("Process", result.MemberName);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AnnotateEntry_EmptyMemberName_ReturnsUnchanged()
        {
            var entry = MakeEntry(typeName: "MyClass", memberName: "");

            var result = CompilerGeneratedResolver.AnnotateEntry(entry);

            Assert.Equal("MyClass", result.TypeName);
            Assert.Equal("", result.MemberName);
        }

        // --- Annotate (batch) tests / Annotate（バッチ）テスト ---

        [Fact]
        [Trait("Category", "Unit")]
        public void Annotate_MixedEntries_AnnotatesOnlyCompilerGenerated()
        {
            var entries = new List<MemberChangeEntry>
            {
                MakeEntry(typeName: "MyClass", memberName: "Process"),
                MakeEntry(typeName: "MyClass/<DoWork>d__0", memberName: "MoveNext"),
                MakeEntry(typeName: "MyClass", memberName: "<Name>k__BackingField", memberKind: "Field"),
            };

            CompilerGeneratedResolver.Annotate(entries);

            Assert.Equal("Process", entries[0].MemberName);
            Assert.DoesNotContain("state machine", entries[0].TypeName);
            Assert.Contains("state machine of MyClass.DoWork", entries[1].TypeName);
            Assert.Contains("backing field of Name", entries[2].MemberName);
        }

        // --- IsCompilerGeneratedType tests / IsCompilerGeneratedType テスト ---

        [Theory]
        [Trait("Category", "Unit")]
        [InlineData("MyClass/<DoWork>d__0", true)]
        [InlineData("MyClass/<>c__DisplayClass5_0", true)]
        [InlineData("MyClass/<>c", true)]
        [InlineData("MyNamespace.MyClass", false)]
        [InlineData("", false)]
        public void IsCompilerGeneratedType_ReturnsExpected(string typeName, bool expected)
        {
            Assert.Equal(expected, CompilerGeneratedResolver.IsCompilerGeneratedType(typeName));
        }

        // --- IsCompilerGeneratedMember tests / IsCompilerGeneratedMember テスト ---

        [Theory]
        [Trait("Category", "Unit")]
        [InlineData("<Name>k__BackingField", true)]
        [InlineData("<DoWork>b__5_0", true)]
        [InlineData("<ProcessData>g__Validate|0_0", true)]
        [InlineData("<Clone>$", true)]
        [InlineData("PrintMembers", true)]
        [InlineData("op_Equality", true)]
        [InlineData("op_Inequality", true)]
        [InlineData("Process", false)]
        [InlineData("", false)]
        public void IsCompilerGeneratedMember_ReturnsExpected(string memberName, bool expected)
        {
            Assert.Equal(expected, CompilerGeneratedResolver.IsCompilerGeneratedMember(memberName));
        }

        // --- Helper / ヘルパー ---

        private static MemberChangeEntry MakeEntry(
            string typeName = "TestType",
            string memberName = "TestMember",
            string memberKind = "Method",
            string change = "Modified")
        {
            return new MemberChangeEntry(
                Change: change,
                TypeName: typeName,
                BaseType: "",
                Access: "public",
                Modifiers: "",
                MemberKind: memberKind,
                MemberName: memberName,
                MemberType: "",
                ReturnType: "",
                Parameters: "",
                Body: "");
        }
    }
}
