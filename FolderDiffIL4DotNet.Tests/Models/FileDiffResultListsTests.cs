using System;
using System.Linq;
using System.Threading.Tasks;
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
            FileDiffResultLists.ResetAll();
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

        #region CollectionState

        [Fact]
        public void SetOldFilesAbsolutePath_ReplacesExistingEntries()
        {
            FileDiffResultLists.SetOldFilesAbsolutePath(new[] { "old-a.dll", "old-b.dll" });
            FileDiffResultLists.SetOldFilesAbsolutePath(new[] { "old-c.dll" });

            Assert.Single(FileDiffResultLists.OldFilesAbsolutePath);
            Assert.Equal("old-c.dll", FileDiffResultLists.OldFilesAbsolutePath.Single());
        }

        [Fact]
        public void AddUnchangedFileRelativePath_Parallel_AllEntriesRecorded()
        {
            const int total = 1000;
            Parallel.For(0, total, i =>
            {
                FileDiffResultLists.AddUnchangedFileRelativePath($"unchanged-{i}.dll");
            });

            Assert.Equal(total, FileDiffResultLists.UnchangedFilesRelativePath.Count);
        }

        [Fact]
        public void ResetAll_ClearsAllState()
        {
            FileDiffResultLists.SetOldFilesAbsolutePath(new[] { "old-a.dll" });
            FileDiffResultLists.SetNewFilesAbsolutePath(new[] { "new-a.dll" });
            FileDiffResultLists.AddUnchangedFileRelativePath("same.dll");
            FileDiffResultLists.AddAddedFileAbsolutePath("added.dll");
            FileDiffResultLists.AddRemovedFileAbsolutePath("removed.dll");
            FileDiffResultLists.AddModifiedFileRelativePath("modified.dll");
            FileDiffResultLists.RecordDiffDetail("same.dll", FileDiffResultLists.DiffDetailResult.MD5Match);
            FileDiffResultLists.RecordIgnoredFile("ignored.pdb", FileDiffResultLists.IgnoredFileLocation.Old);
            FileDiffResultLists.RecordDisassemblerToolVersion("dotnet-ildasm", "1.0.0");
            FileDiffResultLists.RecordDisassemblerToolVersion("dotnet-ildasm", "1.0.0", fromCache: true);

            FileDiffResultLists.ResetAll();

            Assert.Empty(FileDiffResultLists.OldFilesAbsolutePath);
            Assert.Empty(FileDiffResultLists.NewFilesAbsolutePath);
            Assert.Empty(FileDiffResultLists.UnchangedFilesRelativePath);
            Assert.Empty(FileDiffResultLists.AddedFilesAbsolutePath);
            Assert.Empty(FileDiffResultLists.RemovedFilesAbsolutePath);
            Assert.Empty(FileDiffResultLists.ModifiedFilesRelativePath);
            Assert.Empty(FileDiffResultLists.FileRelativePathToDiffDetailDictionary);
            Assert.Empty(FileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary);
            Assert.Empty(FileDiffResultLists.IgnoredFilesRelativePathToLocation);
            Assert.Empty(FileDiffResultLists.DisassemblerToolVersions);
            Assert.Empty(FileDiffResultLists.DisassemblerToolVersionsFromCache);
        }

        [Fact]
        public void ResultQueueProperties_AreReadOnly()
        {
            Assert.False(typeof(FileDiffResultLists).GetProperty(nameof(FileDiffResultLists.OldFilesAbsolutePath))?.CanWrite);
            Assert.False(typeof(FileDiffResultLists).GetProperty(nameof(FileDiffResultLists.NewFilesAbsolutePath))?.CanWrite);
            Assert.False(typeof(FileDiffResultLists).GetProperty(nameof(FileDiffResultLists.UnchangedFilesRelativePath))?.CanWrite);
            Assert.False(typeof(FileDiffResultLists).GetProperty(nameof(FileDiffResultLists.AddedFilesAbsolutePath))?.CanWrite);
            Assert.False(typeof(FileDiffResultLists).GetProperty(nameof(FileDiffResultLists.RemovedFilesAbsolutePath))?.CanWrite);
            Assert.False(typeof(FileDiffResultLists).GetProperty(nameof(FileDiffResultLists.ModifiedFilesRelativePath))?.CanWrite);
        }

        #endregion
    }
}
