/// <summary>
/// Partial class containing mutation-killing tests for FileDiffResultLists.
/// FileDiffResultLists のミューテーションキリングテストを含むパーシャルクラス。
/// </summary>

using System.Collections.Generic;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Models
{
    public partial class FileDiffResultListsTests
    {
        // ── Mutation-killing: RecordIgnoredFile bitwise OR vs AND ──────────
        // ミューテーションキル: RecordIgnoredFile のビット OR vs AND

        [Fact]
        [Trait("Category", "Unit")]
        public void RecordIgnoredFile_BothFlags_ExactCombinedValue()
        {
            // Kills mutation: `existing | location` → `existing & location`
            // ミューテーションキル: `existing | location` → `existing & location`
            _sut.RecordIgnoredFile("both.pdb", FileDiffResultLists.IgnoredFileLocation.Old);
            _sut.RecordIgnoredFile("both.pdb", FileDiffResultLists.IgnoredFileLocation.New);

            var flags = _sut.IgnoredFilesRelativePathToLocation["both.pdb"];
            // Verify the exact combined value (Old | New), not just HasFlag
            // HasFlag だけでなく、正確な結合値 (Old | New) を検証
            Assert.Equal(
                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New,
                flags);
        }

        // ── Mutation-killing: RecordDiffDetail IL condition (|| vs &&) ─────
        // ミューテーションキル: RecordDiffDetail の IL 条件 (|| vs &&)

        [Fact]
        [Trait("Category", "Unit")]
        public void RecordDiffDetail_ILMatch_WithLabel_StoresLabel()
        {
            // Verify ILMatch (not just ILMismatch) stores disassembler label
            // Kills mutation: `||` → `&&` in IL check
            // ILMatch でも（ILMismatch だけでなく）逆アセンブララベルが格納されることを検証
            _sut.RecordDiffDetail("match.dll", FileDiffResultLists.DiffDetailResult.ILMatch, "dotnet-ildasm (version: 0.12.0)");

            Assert.True(_sut.FileRelativePathToIlDisassemblerLabelDictionary.ContainsKey("match.dll"));
            Assert.Equal("dotnet-ildasm (version: 0.12.0)", _sut.FileRelativePathToIlDisassemblerLabelDictionary["match.dll"]);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void RecordDiffDetail_ILMismatch_WithLabel_StoresLabel()
        {
            // Verify ILMismatch also stores disassembler label
            // ILMismatch でも逆アセンブララベルが格納されることを検証
            _sut.RecordDiffDetail("mismatch.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "ilspycmd (version: 8.0.0)");

            Assert.True(_sut.FileRelativePathToIlDisassemblerLabelDictionary.ContainsKey("mismatch.dll"));
            Assert.Equal("ilspycmd (version: 8.0.0)", _sut.FileRelativePathToIlDisassemblerLabelDictionary["mismatch.dll"]);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void RecordDiffDetail_TextMismatch_DoesNotStoreLabel()
        {
            // Non-IL result should not store disassembler label even if provided
            // 非 IL 結果は提供されても逆アセンブララベルを格納しない
            _sut.RecordDiffDetail("text.txt", FileDiffResultLists.DiffDetailResult.TextMismatch, "dotnet-ildasm");

            Assert.False(_sut.FileRelativePathToIlDisassemblerLabelDictionary.ContainsKey("text.txt"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void RecordDiffDetail_ILMatch_WithNullLabel_RemovesExistingLabel()
        {
            // IL result with null label should remove existing label
            // null ラベルの IL 結果は既存のラベルを削除する
            _sut.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm");
            _sut.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.ILMatch, null);

            Assert.False(_sut.FileRelativePathToIlDisassemblerLabelDictionary.ContainsKey("a.dll"));
        }

        // ── Mutation-killing: GetMaxImportance logic ──────────────────────
        // ミューテーションキル: GetMaxImportance ロジック

        [Fact]
        [Trait("Category", "Unit")]
        public void GetMaxImportance_NoSemanticChanges_ReturnsNull()
        {
            // Kills mutation: `== null` → `!= null` in importance check
            // ミューテーションキル: 重要度チェックの `== null` → `!= null`
            _sut.AddModifiedFileRelativePath("plain.dll");

            var result = _sut.GetMaxImportance("plain.dll");

            Assert.Null(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetMaxImportance_OnlyAssemblyChanges_ReturnsAssemblyMax()
        {
            // Kills mutation: removing the assembly max branch
            // ミューテーションキル: アセンブリ最大値ブランチの除去
            _sut.FileRelativePathToAssemblySemanticChanges["asm.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Removed", "MyApp.Svc", "", "public", "", "Method", "Execute", "", "void", "", "", ChangeImportance.High),
                }
            };

            var result = _sut.GetMaxImportance("asm.dll");

            Assert.NotNull(result);
            Assert.Equal(ChangeImportance.High, result!.Value);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetMaxImportance_OnlyDependencyChanges_ReturnsDependencyMax()
        {
            // Kills mutation: removing the dependency max branch
            // ミューテーションキル: 依存関係最大値ブランチの除去
            _sut.FileRelativePathToDependencyChanges["dep.deps.json"] = new DependencyChangeSummary
            {
                Entries = new[]
                {
                    new DependencyChangeEntry("Added", "PkgA", "", "1.0.0", ChangeImportance.Medium),
                }
            };

            var result = _sut.GetMaxImportance("dep.deps.json");

            Assert.NotNull(result);
            Assert.Equal(ChangeImportance.Medium, result!.Value);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetMaxImportance_BothChanges_DependencyHigherWins()
        {
            // Kills mutation: `depMax > max.Value ? depMax : max.Value` → inverted
            // ミューテーションキル: `depMax > max.Value ? depMax : max.Value` の反転
            _sut.FileRelativePathToAssemblySemanticChanges["both.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "MyApp.Svc", "", "private", "", "Method", "Helper", "", "void", "", "", ChangeImportance.Low),
                }
            };
            _sut.FileRelativePathToDependencyChanges["both.dll"] = new DependencyChangeSummary
            {
                Entries = new[]
                {
                    new DependencyChangeEntry("Removed", "OldPkg", "1.0.0", "", ChangeImportance.High),
                }
            };

            var result = _sut.GetMaxImportance("both.dll");

            Assert.NotNull(result);
            Assert.Equal(ChangeImportance.High, result!.Value);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetMaxImportance_BothChanges_AssemblyHigherWins()
        {
            // Ensure assembly importance wins when it's higher
            // アセンブリ重要度が高い場合にそれが勝つことを確認
            _sut.FileRelativePathToAssemblySemanticChanges["asm-wins.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Removed", "MyApp.Svc", "", "public", "", "Method", "Execute", "", "void", "", "", ChangeImportance.High),
                }
            };
            _sut.FileRelativePathToDependencyChanges["asm-wins.dll"] = new DependencyChangeSummary
            {
                Entries = new[]
                {
                    new DependencyChangeEntry("Updated", "Pkg", "1.0.0", "1.0.1", ChangeImportance.Low),
                }
            };

            var result = _sut.GetMaxImportance("asm-wins.dll");

            Assert.NotNull(result);
            Assert.Equal(ChangeImportance.High, result!.Value);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetMaxImportance_BothChanges_EqualImportance_ReturnsEither()
        {
            // When both have same importance, result should be that importance
            // 両方が同じ重要度の場合、その重要度が返される
            _sut.FileRelativePathToAssemblySemanticChanges["equal.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "MyApp.Svc", "", "public", "", "Method", "NewApi", "", "void", "", "", ChangeImportance.Medium),
                }
            };
            _sut.FileRelativePathToDependencyChanges["equal.dll"] = new DependencyChangeSummary
            {
                Entries = new[]
                {
                    new DependencyChangeEntry("Added", "NewPkg", "", "1.0.0", ChangeImportance.Medium),
                }
            };

            var result = _sut.GetMaxImportance("equal.dll");

            Assert.NotNull(result);
            Assert.Equal(ChangeImportance.Medium, result!.Value);
        }

        // ── Mutation-killing: GetAllImportanceLevels ──────────────────────
        // ミューテーションキル: GetAllImportanceLevels

        [Fact]
        [Trait("Category", "Unit")]
        public void GetAllImportanceLevels_NoChanges_ReturnsEmpty()
        {
            var result = _sut.GetAllImportanceLevels("none.dll");
            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetAllImportanceLevels_MultipleEntries_ReturnsDistinct()
        {
            _sut.FileRelativePathToAssemblySemanticChanges["multi.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Removed", "MyApp.Svc", "", "public", "", "Method", "A", "", "void", "", "", ChangeImportance.High),
                    new("Added", "MyApp.Svc", "", "private", "", "Method", "B", "", "void", "", "", ChangeImportance.Low),
                }
            };

            var result = _sut.GetAllImportanceLevels("multi.dll");

            Assert.Contains(ChangeImportance.High, result);
            Assert.Contains(ChangeImportance.Low, result);
        }

        // ── Mutation-killing: HasAnySha256Mismatch exact condition ─────────
        // ミューテーションキル: HasAnySha256Mismatch の正確な条件

        [Fact]
        [Trait("Category", "Unit")]
        public void HasAnySha256Mismatch_TextMismatch_ReturnsFalse()
        {
            // TextMismatch != SHA256Mismatch, so should return false
            // Kills mutation: `== SHA256Mismatch` → `!= SHA256Mismatch`
            // TextMismatch != SHA256Mismatch のため false を返すべき
            _sut.RecordDiffDetail("text.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            Assert.False(_sut.HasAnySha256Mismatch);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void HasAnySha256Mismatch_ILMismatch_ReturnsFalse()
        {
            // ILMismatch != SHA256Mismatch, so should return false
            // ILMismatch != SHA256Mismatch のため false を返すべき
            _sut.RecordDiffDetail("il.dll", FileDiffResultLists.DiffDetailResult.ILMismatch);

            Assert.False(_sut.HasAnySha256Mismatch);
        }

        // ── Mutation-killing: HasAnyNewFileTimestampOlderThanOldWarning ──
        // ミューテーションキル: HasAnyNewFileTimestampOlderThanOldWarning

        [Fact]
        [Trait("Category", "Unit")]
        public void HasAnyNewFileTimestampOlderThanOldWarning_Empty_ReturnsFalse()
        {
            Assert.False(_sut.HasAnyNewFileTimestampOlderThanOldWarning);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void HasAnyNewFileTimestampOlderThanOldWarning_WithWarning_ReturnsTrue()
        {
            _sut.RecordNewFileTimestampOlderThanOldWarning("file.txt", "2026-01-01", "2025-12-31");
            Assert.True(_sut.HasAnyNewFileTimestampOlderThanOldWarning);
        }

        // ── Mutation-killing: RecordDisassemblerToolVersion fromCache flag ──
        // ミューテーションキル: RecordDisassemblerToolVersion の fromCache フラグ

        [Fact]
        [Trait("Category", "Unit")]
        public void RecordDisassemblerToolVersion_FalseFromCache_RecordedInMainDictionary()
        {
            // Kills mutation: `fromCache ? ...FromCache : ...` → inverted ternary
            // ミューテーションキル: `fromCache ? ...FromCache : ...` → 三項演算子の反転
            _sut.RecordDisassemblerToolVersion("dotnet-ildasm", "0.12.0", fromCache: false);

            Assert.True(_sut.DisassemblerToolVersions.ContainsKey("dotnet-ildasm (version: 0.12.0)"));
            Assert.Empty(_sut.DisassemblerToolVersionsFromCache);
        }
    }
}
