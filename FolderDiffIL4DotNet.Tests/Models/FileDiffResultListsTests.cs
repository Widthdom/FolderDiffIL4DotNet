using System;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Models
{
    [Collection("FileDiffResultLists")]
    public class FileDiffResultListsTests : IDisposable
    {
        public FileDiffResultListsTests()
        {
            ClearAll();
        }

        public void Dispose()
        {
            ClearAll();
        }

        private static void ClearAll()
        {
            FileDiffResultLists.OldFilesAbsolutePath = new System.Collections.Generic.List<string>();
            FileDiffResultLists.NewFilesAbsolutePath = new System.Collections.Generic.List<string>();
            FileDiffResultLists.UnchangedFilesRelativePath = new System.Collections.Generic.List<string>();
            FileDiffResultLists.AddedFilesAbsolutePath = new System.Collections.Generic.List<string>();
            FileDiffResultLists.RemovedFilesAbsolutePath = new System.Collections.Generic.List<string>();
            FileDiffResultLists.ModifiedFilesRelativePath = new System.Collections.Generic.List<string>();
            FileDiffResultLists.FileRelativePathToDiffDetailDictionary.Clear();
            FileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.Clear();
            FileDiffResultLists.IgnoredFilesRelativePathToLocation.Clear();
            FileDiffResultLists.DisassemblerToolVersions.Clear();
            FileDiffResultLists.DisassemblerToolVersionsFromCache.Clear();
        }

        #region RecordDiffDetail

        [Fact]
        public void RecordDiffDetail_NewEntry_Stored()
        {
            FileDiffResultLists.RecordDiffDetail("file.cs", FileDiffResultLists.DiffDetailResult.MD5Match);

            Assert.True(FileDiffResultLists.FileRelativePathToDiffDetailDictionary.ContainsKey("file.cs"));
            Assert.Equal(FileDiffResultLists.DiffDetailResult.MD5Match, FileDiffResultLists.FileRelativePathToDiffDetailDictionary["file.cs"]);
        }

        [Fact]
        public void RecordDiffDetail_Overwrite_UpdatesValue()
        {
            FileDiffResultLists.RecordDiffDetail("file.cs", FileDiffResultLists.DiffDetailResult.MD5Match);
            FileDiffResultLists.RecordDiffDetail("file.cs", FileDiffResultLists.DiffDetailResult.ILMismatch);

            Assert.Equal(FileDiffResultLists.DiffDetailResult.ILMismatch, FileDiffResultLists.FileRelativePathToDiffDetailDictionary["file.cs"]);
        }

        [Fact]
        public void RecordDiffDetail_MultipleEntries_AllStored()
        {
            FileDiffResultLists.RecordDiffDetail("a.cs", FileDiffResultLists.DiffDetailResult.MD5Match);
            FileDiffResultLists.RecordDiffDetail("b.dll", FileDiffResultLists.DiffDetailResult.ILMatch);
            FileDiffResultLists.RecordDiffDetail("c.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            Assert.Equal(3, FileDiffResultLists.FileRelativePathToDiffDetailDictionary.Count);
        }

        [Fact]
        public void RecordDiffDetail_IlResult_WithDisassemblerLabel_Stored()
        {
            FileDiffResultLists.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            Assert.True(FileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.ContainsKey("a.dll"));
            Assert.Equal("dotnet-ildasm (version: 0.12.0)", FileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary["a.dll"]);
        }

        [Fact]
        public void RecordDiffDetail_NonIlResult_ClearsExistingDisassemblerLabel()
        {
            FileDiffResultLists.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.ILMatch, "dotnet-ildasm (version: 0.12.0)");
            FileDiffResultLists.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.MD5Match);

            Assert.False(FileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.ContainsKey("a.dll"));
        }

        #endregion

        #region HasAnyMd5Mismatch

        [Fact]
        public void HasAnyMd5Mismatch_Empty_ReturnsFalse()
        {
            Assert.False(FileDiffResultLists.HasAnyMd5Mismatch);
        }

        [Fact]
        public void HasAnyMd5Mismatch_OnlyMatches_ReturnsFalse()
        {
            FileDiffResultLists.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.MD5Match);
            FileDiffResultLists.RecordDiffDetail("b.dll", FileDiffResultLists.DiffDetailResult.ILMatch);

            Assert.False(FileDiffResultLists.HasAnyMd5Mismatch);
        }

        [Fact]
        public void HasAnyMd5Mismatch_WithMismatch_ReturnsTrue()
        {
            FileDiffResultLists.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.MD5Match);
            FileDiffResultLists.RecordDiffDetail("b.dll", FileDiffResultLists.DiffDetailResult.MD5Mismatch);

            Assert.True(FileDiffResultLists.HasAnyMd5Mismatch);
        }

        #endregion

        #region RecordIgnoredFile

        [Fact]
        public void RecordIgnoredFile_OldOnly_StoresOldFlag()
        {
            FileDiffResultLists.RecordIgnoredFile("test.pdb", FileDiffResultLists.IgnoredFileLocation.Old);

            Assert.True(FileDiffResultLists.IgnoredFilesRelativePathToLocation.ContainsKey("test.pdb"));
            Assert.Equal(FileDiffResultLists.IgnoredFileLocation.Old, FileDiffResultLists.IgnoredFilesRelativePathToLocation["test.pdb"]);
        }

        [Fact]
        public void RecordIgnoredFile_BothOldAndNew_FlagsCombined()
        {
            FileDiffResultLists.RecordIgnoredFile("test.pdb", FileDiffResultLists.IgnoredFileLocation.Old);
            FileDiffResultLists.RecordIgnoredFile("test.pdb", FileDiffResultLists.IgnoredFileLocation.New);

            var flags = FileDiffResultLists.IgnoredFilesRelativePathToLocation["test.pdb"];
            Assert.True(flags.HasFlag(FileDiffResultLists.IgnoredFileLocation.Old));
            Assert.True(flags.HasFlag(FileDiffResultLists.IgnoredFileLocation.New));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RecordIgnoredFile_NullOrWhitespace_ThrowsArgumentException(string path)
        {
            Assert.Throws<ArgumentException>(() =>
                FileDiffResultLists.RecordIgnoredFile(path, FileDiffResultLists.IgnoredFileLocation.Old));
        }

        [Fact]
        public void RecordIgnoredFile_CaseInsensitiveKey()
        {
            FileDiffResultLists.RecordIgnoredFile("Test.PDB", FileDiffResultLists.IgnoredFileLocation.Old);
            FileDiffResultLists.RecordIgnoredFile("test.pdb", FileDiffResultLists.IgnoredFileLocation.New);

            // Should be the same entry due to OrdinalIgnoreCase
            Assert.Single(FileDiffResultLists.IgnoredFilesRelativePathToLocation);
        }

        #endregion

        #region RecordDisassemblerToolVersion

        [Fact]
        public void RecordDisassemblerToolVersion_Normal_Recorded()
        {
            FileDiffResultLists.RecordDisassemblerToolVersion("dotnet-ildasm", "1.0.0");

            Assert.True(FileDiffResultLists.DisassemblerToolVersions.ContainsKey("dotnet-ildasm (version: 1.0.0)"));
        }

        [Fact]
        public void RecordDisassemblerToolVersion_FromCache_RecordedInCacheDictionary()
        {
            FileDiffResultLists.RecordDisassemblerToolVersion("dotnet-ildasm", "1.0.0", fromCache: true);

            Assert.Empty(FileDiffResultLists.DisassemblerToolVersions);
            Assert.True(FileDiffResultLists.DisassemblerToolVersionsFromCache.ContainsKey("dotnet-ildasm (version: 1.0.0)"));
        }

        [Fact]
        public void RecordDisassemblerToolVersion_NullOrWhitespace_Ignored()
        {
            FileDiffResultLists.RecordDisassemblerToolVersion(null, "1.0.0");
            FileDiffResultLists.RecordDisassemblerToolVersion("", "1.0.0");
            FileDiffResultLists.RecordDisassemblerToolVersion("   ", "1.0.0");

            Assert.Empty(FileDiffResultLists.DisassemblerToolVersions);
        }

        [Fact]
        public void RecordDisassemblerToolVersion_NoVersion_UsesToolNameOnly()
        {
            FileDiffResultLists.RecordDisassemblerToolVersion("ildasm", null);

            Assert.True(FileDiffResultLists.DisassemblerToolVersions.ContainsKey("ildasm"));
        }

        #endregion
    }
}
