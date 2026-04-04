using System.Collections.Generic;
using FolderDiffIL4DotNet.Services.ILOutput;
using Xunit;
using CoreParser = FolderDiffIL4DotNet.Core.IL.ILBlockParser;

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

        // --- Brace counting resilience tests / 波括弧カウント耐性テスト ---

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseBlocks_BracesInStringLiteral_DoNotAffectBlockBoundary()
        {
            // IL string operand containing braces should not confuse block parsing
            // 波括弧を含む IL 文字列オペランドがブロック解析を混乱させないこと
            var lines = new List<string>
            {
                ".method public void Test() cil managed",
                "{",
                "  ldstr \"JSON: {\\\"key\\\": \\\"value\\\"}\"",
                "  pop",
                "  ret",
                "}"
            };

            var result = ILBlockParser.ParseBlocks(lines);

            Assert.Single(result);
            Assert.Equal(6, result[0].Count);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseBlocks_BracesInComment_DoNotAffectBlockBoundary()
        {
            // Braces after // comment marker should be ignored
            // // コメントマーカー以降の波括弧は無視されるべき
            var lines = new List<string>
            {
                ".method public void Test() cil managed",
                "{",
                "  nop // closing brace } should not end block",
                "  ret",
                "}"
            };

            var result = ILBlockParser.ParseBlocks(lines);

            Assert.Single(result);
            Assert.Equal(5, result[0].Count);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseBlocks_MixedStringAndCommentBraces_ParsesCorrectly()
        {
            // Both string and comment braces in same method
            // 同一メソッド内に文字列とコメントの波括弧がある場合
            var lines = new List<string>
            {
                ".method public void Mixed() cil managed",
                "{",
                "  ldstr \"{hello}\"",
                "  pop // end of {block}",
                "  ret",
                "}"
            };

            var result = ILBlockParser.ParseBlocks(lines);

            Assert.Single(result);
            Assert.Equal(6, result[0].Count);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ParseBlocks_EscapedQuoteInString_ParsesCorrectly()
        {
            // Escaped quote inside string should not end the string context
            // 文字列内のエスケープされた引用符が文字列コンテキストを終了させないこと
            var lines = new List<string>
            {
                ".method public void EscapedQuote() cil managed",
                "{",
                "  ldstr \"she said \\\"hello}\\\" to me\"",
                "  ret",
                "}"
            };

            var result = ILBlockParser.ParseBlocks(lines);

            Assert.Single(result);
            Assert.Equal(5, result[0].Count);
        }

        // --- ExtractBlockSignature tests / ExtractBlockSignature テスト ---

        [Fact]
        [Trait("Category", "Unit")]
        public void ExtractBlockSignature_NullOrEmpty_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, CoreParser.ExtractBlockSignature(null!));
            Assert.Equal(string.Empty, CoreParser.ExtractBlockSignature(new List<string>()));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ExtractBlockSignature_PreambleBlock_ReturnsEmptyString()
        {
            var preamble = new List<string>
            {
                ".assembly extern mscorlib {}",
                ".module test.dll"
            };

            Assert.Equal(string.Empty, CoreParser.ExtractBlockSignature(preamble));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ExtractBlockSignature_MethodBlock_ReturnsDirectiveLine()
        {
            var block = new List<string>
            {
                ".method public void Foo() cil managed",
                "{",
                "  ret",
                "}"
            };

            Assert.Equal(".method public void Foo() cil managed", CoreParser.ExtractBlockSignature(block));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ExtractBlockSignature_ClassBlock_ReturnsDirectiveLine()
        {
            var block = new List<string>
            {
                ".class public auto ansi MyClass",
                "  extends [mscorlib]System.Object",
                "{",
                "}"
            };

            Assert.Equal(".class public auto ansi MyClass", CoreParser.ExtractBlockSignature(block));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ExtractBlockSignature_IndentedDirective_ReturnsTrimmedLine()
        {
            // Verify leading whitespace is trimmed from the signature
            // シグネチャの先頭空白がトリムされることを検証
            var block = new List<string>
            {
                "  .field public int32 _value",
            };

            Assert.Equal(".field public int32 _value", CoreParser.ExtractBlockSignature(block));
        }
    }
}
