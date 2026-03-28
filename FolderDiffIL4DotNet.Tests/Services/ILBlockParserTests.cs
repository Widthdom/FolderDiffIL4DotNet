using System.Collections.Generic;
using FolderDiffIL4DotNet.Services.ILOutput;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="ILBlockParser"/> covering IL block parsing into logical blocks.
    /// <see cref="ILBlockParser"/> の IL ブロック分割ロジックをテストします。
    /// </summary>
    public sealed class ILBlockParserTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        public void ParseBlocks_EmptyInput_ReturnsSingleEmptyResult()
        {
            var result = ILBlockParser.ParseBlocks(new List<string>());

            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseBlocks_PreambleOnly_ReturnsSingleBlock()
        {
            var lines = new List<string>
            {
                ".assembly extern mscorlib {}",
                ".module test.dll"
            };

            var result = ILBlockParser.ParseBlocks(lines);

            Assert.Single(result);
            Assert.Equal(lines, result[0]);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseBlocks_SingleMethod_ReturnsPreambleAndMethodBlock()
        {
            var lines = new List<string>
            {
                ".assembly extern mscorlib {}",
                ".method public hidebysig static void Main() cil managed",
                "{",
                "  .maxstack 1",
                "  ret",
                "}"
            };

            var result = ILBlockParser.ParseBlocks(lines);

            Assert.Equal(2, result.Count);
            // Preamble
            Assert.Single(result[0]);
            Assert.Equal(".assembly extern mscorlib {}", result[0][0]);
            // Method block
            Assert.Equal(5, result[1].Count);
            Assert.StartsWith(".method", result[1][0]);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseBlocks_TwoMethods_ReturnsThreeBlocks()
        {
            var lines = new List<string>
            {
                ".assembly test {}",
                ".method public void Foo() cil managed",
                "{",
                "  ret",
                "}",
                ".method public void Bar() cil managed",
                "{",
                "  ret",
                "}"
            };

            var result = ILBlockParser.ParseBlocks(lines);

            Assert.Equal(3, result.Count); // preamble + 2 methods
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseBlocks_NestedBraces_HandlesCorrectly()
        {
            var lines = new List<string>
            {
                ".method public void Nested() cil managed",
                "{",
                "  .try",
                "  {",
                "    nop",
                "  }",
                "  catch [mscorlib]System.Exception",
                "  {",
                "    pop",
                "  }",
                "  ret",
                "}"
            };

            var result = ILBlockParser.ParseBlocks(lines);

            // Should be one block (the method with nested braces)
            Assert.Single(result);
            Assert.Equal(12, result[0].Count);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseBlocks_ClassBlock_ParsedCorrectly()
        {
            var lines = new List<string>
            {
                ".class public auto ansi beforefieldinit MyClass",
                "  extends [mscorlib]System.Object",
                "{",
                "  .method public void Test() cil managed",
                "  {",
                "    ret",
                "  }",
                "}"
            };

            var result = ILBlockParser.ParseBlocks(lines);

            Assert.Single(result);
            Assert.Equal(8, result[0].Count);
        }
    }
}
