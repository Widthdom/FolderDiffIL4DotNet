using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    [Trait("Category", "Unit")]
    public sealed class ILOutputCorpusTests
    {
        private static readonly string CorpusRoot = Path.Combine(
            FindRepositoryRoot(), "FolderDiffIL4DotNet.Tests", "Fixtures", "ILCorpus");

        public static IEnumerable<object[]> DisassemblerFixtures()
        {
            yield return new object[] { "dotnet-ildasm", "dotnet-ildasm-0.12.2.il" };
            yield return new object[] { "ilspycmd", "ilspycmd-9.1.0.7988.il" };
        }

        [Theory]
        [MemberData(nameof(DisassemblerFixtures))]
        public void RealIlFixture_CoversRepresentativeLanguageAndMetadataShapes(
            string disassembler,
            string fixtureFileName)
        {
            var text = ReadFixture(fixtureFileName);
            var lines = File.ReadAllLines(Path.Combine(CorpusRoot, fixtureFileName));

            Assert.True(lines.Length > 500, $"{disassembler} fixture appears truncated.");
            Assert.Contains("RepresentativeType`1", text, StringComparison.Ordinal);
            Assert.Contains(".ctor", text, StringComparison.Ordinal);
            Assert.Contains(".cctor", text, StringComparison.Ordinal);
            Assert.Contains("<ComputeAsync>d__", text, StringComparison.Ordinal);
            Assert.Contains("<CountTo>d__", text, StringComparison.Ordinal);
            Assert.Contains("<>c__DisplayClass", text, StringComparison.Ordinal);
            Assert.Contains("Convert", text, StringComparison.Ordinal);
            Assert.Contains(".property", text, StringComparison.Ordinal);
            Assert.Contains(".event", text, StringComparison.Ordinal);
            Assert.Contains(".field", text, StringComparison.Ordinal);
            Assert.Contains("ICorpusComContract", text, StringComparison.Ordinal);
            Assert.Contains("GuidAttribute", text, StringComparison.Ordinal);
            Assert.Contains("InterfaceTypeAttribute", text, StringComparison.Ordinal);
            Assert.Contains("DispIdAttribute", text, StringComparison.Ordinal);
        }

        [Theory]
        [MemberData(nameof(DisassemblerFixtures))]
        public void RealIlFixture_PreservesClassAndNestedMethodStructure(
            string disassembler,
            string fixtureFileName)
        {
            var text = ReadFixture(fixtureFileName);
            int classStart = text.IndexOf(".class public auto ansi sealed ILCorpus.Sample.RepresentativeType`1", StringComparison.Ordinal);
            int constructor = text.IndexOf(".ctor", classStart, StringComparison.Ordinal);
            int ordinaryMethod = text.IndexOf(" Add", constructor, StringComparison.Ordinal);
            int asyncStateMachine = text.IndexOf("<ComputeAsync>d__", classStart, StringComparison.Ordinal);

            Assert.True(classStart >= 0, $"{disassembler} fixture must contain the representative class.");
            Assert.True(constructor > classStart, $"{disassembler} fixture must nest constructors in the class.");
            Assert.True(ordinaryMethod > constructor, $"{disassembler} fixture must contain multiple class methods.");
            Assert.True(asyncStateMachine > classStart, $"{disassembler} fixture must contain an async state-machine type.");
        }

        [Fact]
        public void DotNetIldasmFixture_ContainsSignedAssemblyAndAssemblyReferenceEvidence()
        {
            var text = ReadFixture("dotnet-ildasm-0.12.2.il");
            var project = File.ReadAllText(Path.Combine(CorpusRoot, "Source", "ILCorpus.Sample.csproj"));

            Assert.Contains("<SignAssembly>true</SignAssembly>", project, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(CorpusRoot, "Source", "ILCorpus.TestKey.snk")));
            Assert.Contains(".corflags 0x00000009", text, StringComparison.Ordinal);
            Assert.Contains(".assembly extern System.Runtime", text, StringComparison.Ordinal);
            Assert.Contains(".publickeytoken = (", text, StringComparison.Ordinal);
        }

        [Fact]
        public void DotNetIldasmFixture_ContainsItsActualCommentAndAttributeSyntax()
        {
            var text = ReadFixture("dotnet-ildasm-0.12.2.il");

            Assert.Contains("// MVID: {", text, StringComparison.Ordinal);
            Assert.Contains("// Method begins at Relative Virtual Address (RVA) 0x", text, StringComparison.Ordinal);
            Assert.Contains("// Code size ", text, StringComparison.Ordinal);
            Assert.Contains(".custom instance void class [System.Runtime]", text, StringComparison.Ordinal);
        }

        [Fact]
        public void IlspyFixture_ContainsItsActualCommentAndAttributeSyntax()
        {
            var text = ReadFixture("ilspycmd-9.1.0.7988.il");

            Assert.Contains("// Method begins at RVA 0x", text, StringComparison.Ordinal);
            Assert.Contains("// Code size: ", text, StringComparison.Ordinal);
            Assert.Contains(".custom instance void [System.Runtime]", text, StringComparison.Ordinal);
            Assert.DoesNotContain(".custom instance void class [System.Runtime]", text, StringComparison.Ordinal);
            Assert.Contains("abstract import beforefieldinit ILCorpus.Sample.ICorpusComContract", text, StringComparison.Ordinal);
            Assert.DoesNotContain("// Method begins at Relative Virtual Address (RVA) 0x", text, StringComparison.Ordinal);
        }

        private static string ReadFixture(string fixtureFileName)
            => File.ReadAllText(Path.Combine(CorpusRoot, fixtureFileName));

        private static string FindRepositoryRoot()
        {
            string? directory = AppContext.BaseDirectory;
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory, "FolderDiffIL4DotNet.sln")))
                {
                    return directory;
                }

                directory = Path.GetDirectoryName(directory);
            }

            throw new DirectoryNotFoundException("Could not locate the repository root for IL corpus fixtures.");
        }
    }
}
