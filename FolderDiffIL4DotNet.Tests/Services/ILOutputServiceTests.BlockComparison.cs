using System.Collections.Generic;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed partial class ILOutputServiceTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_TenThousandNestedClasses_UsesCompactContainerKeys()
        {
            const int depth = 10_000;
            var lines = new List<string>(depth * 3);
            for (int i = 0; i < depth; i++)
            {
                lines.Add(".class C");
                lines.Add("{");
            }
            for (int i = 0; i < depth; i++)
            {
                lines.Add("}");
            }

            Assert.True(ILOutputService.BlockAwareSequenceEqual(lines, new List<string>(lines)));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_MarshalBlobsSwappedBetweenMethods_ReturnsFalse()
        {
            var lines1 = BuildClassWithMultilineMarshalHeaders(
                ("Foo", "38 01 02 FF"),
                ("Bar", "39 02 03 EE"));
            var lines2 = BuildClassWithMultilineMarshalHeaders(
                ("Foo", "39 02 03 EE"),
                ("Bar", "38 01 02 FF"));

            Assert.False(ILOutputService.BlockAwareSequenceEqual(lines1, lines2));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_MethodsWithMarshalBlobsReordered_ReturnsTrue()
        {
            var lines1 = BuildClassWithMultilineMarshalHeaders(
                ("Foo", "38 01 02 FF"),
                ("Bar", "39 02 03 EE"));
            var lines2 = BuildClassWithMultilineMarshalHeaders(
                ("Bar", "39 02 03 EE"),
                ("Foo", "38 01 02 FF"));

            Assert.True(ILOutputService.BlockAwareSequenceEqual(lines1, lines2));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_MethodBodiesMovedBetweenMultilineHeaderClasses_ReturnsFalse()
        {
            var lines1 = BuildMultilineHeaderClassIl("ClassA", "ldc.i4.0");
            lines1.AddRange(BuildMultilineHeaderClassIl("ClassB", "ldc.i4.1"));
            var lines2 = BuildMultilineHeaderClassIl("ClassA", "ldc.i4.1");
            lines2.AddRange(BuildMultilineHeaderClassIl("ClassB", "ldc.i4.0"));

            Assert.False(ILOutputService.BlockAwareSequenceEqual(lines1, lines2));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_MethodsReorderedWithinMultilineHeaderClass_ReturnsTrue()
        {
            var lines1 = BuildMultilineHeaderClassIl(
                "ClassA",
                ("Foo", "ldc.i4.0"),
                ("Bar", "ldc.i4.1"));
            var lines2 = BuildMultilineHeaderClassIl(
                "ClassA",
                ("Bar", "ldc.i4.1"),
                ("Foo", "ldc.i4.0"));

            Assert.True(ILOutputService.BlockAwareSequenceEqual(lines1, lines2));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_MethodBodiesMovedBetweenNestedMultilineHeaderClasses_ReturnsFalse()
        {
            var lines1 = BuildOuterWithNestedMultilineHeaderClasses(
                ("NestedA", "ldc.i4.0"),
                ("NestedB", "ldc.i4.1"));
            var lines2 = BuildOuterWithNestedMultilineHeaderClasses(
                ("NestedA", "ldc.i4.1"),
                ("NestedB", "ldc.i4.0"));

            Assert.False(ILOutputService.BlockAwareSequenceEqual(lines1, lines2));
        }

        private static List<string> BuildClassWithMultilineMarshalHeaders(
            params (string MethodName, string MarshalBlob)[] methods)
        {
            var lines = new List<string>
            {
                ".class public auto ansi MarshalClass",
                "{"
            };

            foreach (var method in methods)
            {
                lines.Add("  .method public hidebysig");
                lines.Add("    instance void");
                lines.Add("    marshal({");
                lines.Add($"      {method.MarshalBlob}");
                lines.Add("    })");
                lines.Add($"    {method.MethodName}() cil managed");
                lines.Add("  {");
                lines.Add("    ret");
                lines.Add("  }");
            }

            lines.Add("}");
            return lines;
        }

        private static List<string> BuildMultilineHeaderClassIl(
            string className,
            string bodyInstruction)
        {
            return BuildMultilineHeaderClassIl(className, ("Foo", bodyInstruction));
        }

        private static List<string> BuildMultilineHeaderClassIl(
            string className,
            params (string MethodName, string BodyInstruction)[] methods)
        {
            var lines = new List<string>
            {
                ".class public auto ansi",
                $"  {className}",
                "  extends [System.Runtime]System.Object",
                "{"
            };

            foreach (var method in methods)
            {
                lines.Add($"  .method public void {method.MethodName}() cil managed");
                lines.Add("  {");
                lines.Add($"    {method.BodyInstruction}");
                lines.Add("    ret");
                lines.Add("  }");
            }

            lines.Add("}");
            return lines;
        }

        private static List<string> BuildOuterWithNestedMultilineHeaderClasses(
            params (string ClassName, string BodyInstruction)[] classes)
        {
            var lines = new List<string>
            {
                ".class public Outer",
                "{"
            };

            foreach (var nestedClass in classes)
            {
                lines.Add("  .class nested public");
                lines.Add($"    {nestedClass.ClassName}");
                lines.Add("  {");
                lines.Add("    .method public void Foo() cil managed");
                lines.Add("    {");
                lines.Add($"      {nestedClass.BodyInstruction}");
                lines.Add("      ret");
                lines.Add("    }");
                lines.Add("  }");
            }

            lines.Add("}");
            return lines;
        }
    }
}
