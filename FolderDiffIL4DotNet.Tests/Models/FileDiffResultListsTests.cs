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
        private readonly FileDiffResultLists _sut = new();

        public FileDiffResultListsTests()
        {
            ClearAll();
        }

        public void Dispose()
        {
            ClearAll();
        }

        private void ClearAll()
        {
            _sut.ResetAll();
        }

        #region RecordDiffDetail

        [Fact]
        public void RecordDiffDetail_NewEntry_Stored()
        {
            _sut.RecordDiffDetail("file.cs", FileDiffResultLists.DiffDetailResult.MD5Match);

            Assert.True(_sut.FileRelativePathToDiffDetailDictionary.ContainsKey("file.cs"));
            Assert.Equal(FileDiffResultLists.DiffDetailResult.MD5Match, _sut.FileRelativePathToDiffDetailDictionary["file.cs"]);
        }

        [Fact]
        public void RecordDiffDetail_Overwrite_UpdatesValue()
        {
            _sut.RecordDiffDetail("file.cs", FileDiffResultLists.DiffDetailResult.MD5Match);
            _sut.RecordDiffDetail("file.cs", FileDiffResultLists.DiffDetailResult.ILMismatch);

            Assert.Equal(FileDiffResultLists.DiffDetailResult.ILMismatch, _sut.FileRelativePathToDiffDetailDictionary["file.cs"]);
        }

        [Fact]
        public void RecordDiffDetail_MultipleEntries_AllStored()
        {
            _sut.RecordDiffDetail("a.cs", FileDiffResultLists.DiffDetailResult.MD5Match);
            _sut.RecordDiffDetail("b.dll", FileDiffResultLists.DiffDetailResult.ILMatch);
            _sut.RecordDiffDetail("c.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            Assert.Equal(3, _sut.FileRelativePathToDiffDetailDictionary.Count);
        }

        [Fact]
        public void RecordDiffDetail_IlResult_WithDisassemblerLabel_Stored()
        {
            _sut.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            Assert.True(_sut.FileRelativePathToIlDisassemblerLabelDictionary.ContainsKey("a.dll"));
            Assert.Equal("dotnet-ildasm (version: 0.12.0)", _sut.FileRelativePathToIlDisassemblerLabelDictionary["a.dll"]);
        }

        [Fact]
        public void RecordDiffDetail_NonIlResult_ClearsExistingDisassemblerLabel()
        {
            _sut.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.ILMatch, "dotnet-ildasm (version: 0.12.0)");
            _sut.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.MD5Match);

            Assert.False(_sut.FileRelativePathToIlDisassemblerLabelDictionary.ContainsKey("a.dll"));
        }

        #endregion

        #region HasAnyMd5Mismatch

        [Fact]
        public void HasAnyMd5Mismatch_Empty_ReturnsFalse()
        {
            Assert.False(_sut.HasAnyMd5Mismatch);
        }

        [Fact]
        public void HasAnyMd5Mismatch_OnlyMatches_ReturnsFalse()
        {
            _sut.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.MD5Match);
            _sut.RecordDiffDetail("b.dll", FileDiffResultLists.DiffDetailResult.ILMatch);

            Assert.False(_sut.HasAnyMd5Mismatch);
        }

        [Fact]
        public void HasAnyMd5Mismatch_WithMismatch_ReturnsTrue()
        {
            _sut.RecordDiffDetail("a.dll", FileDiffResultLists.DiffDetailResult.MD5Match);
            _sut.RecordDiffDetail("b.dll", FileDiffResultLists.DiffDetailResult.MD5Mismatch);

            Assert.True(_sut.HasAnyMd5Mismatch);
        }

        #endregion

        #region RecordIgnoredFile

        [Fact]
        public void RecordIgnoredFile_OldOnly_StoresOldFlag()
        {
            _sut.RecordIgnoredFile("test.pdb", FileDiffResultLists.IgnoredFileLocation.Old);

            Assert.True(_sut.IgnoredFilesRelativePathToLocation.ContainsKey("test.pdb"));
            Assert.Equal(FileDiffResultLists.IgnoredFileLocation.Old, _sut.IgnoredFilesRelativePathToLocation["test.pdb"]);
        }

        [Fact]
        public void RecordIgnoredFile_BothOldAndNew_FlagsCombined()
        {
            _sut.RecordIgnoredFile("test.pdb", FileDiffResultLists.IgnoredFileLocation.Old);
            _sut.RecordIgnoredFile("test.pdb", FileDiffResultLists.IgnoredFileLocation.New);

            var flags = _sut.IgnoredFilesRelativePathToLocation["test.pdb"];
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
                _sut.RecordIgnoredFile(path, FileDiffResultLists.IgnoredFileLocation.Old));
        }

        [Fact]
        public void RecordIgnoredFile_CaseInsensitiveKey()
        {
            _sut.RecordIgnoredFile("Test.PDB", FileDiffResultLists.IgnoredFileLocation.Old);
            _sut.RecordIgnoredFile("test.pdb", FileDiffResultLists.IgnoredFileLocation.New);

            // Should be the same entry due to OrdinalIgnoreCase
            Assert.Single(_sut.IgnoredFilesRelativePathToLocation);
        }

        #endregion

        #region RecordDisassemblerToolVersion

        [Fact]
        public void RecordDisassemblerToolVersion_Normal_Recorded()
        {
            _sut.RecordDisassemblerToolVersion("dotnet-ildasm", "1.0.0");

            Assert.True(_sut.DisassemblerToolVersions.ContainsKey("dotnet-ildasm (version: 1.0.0)"));
        }

        [Fact]
        public void RecordDisassemblerToolVersion_FromCache_RecordedInCacheDictionary()
        {
            _sut.RecordDisassemblerToolVersion("dotnet-ildasm", "1.0.0", fromCache: true);

            Assert.Empty(_sut.DisassemblerToolVersions);
            Assert.True(_sut.DisassemblerToolVersionsFromCache.ContainsKey("dotnet-ildasm (version: 1.0.0)"));
        }

        [Fact]
        public void RecordDisassemblerToolVersion_NullOrWhitespace_Ignored()
        {
            _sut.RecordDisassemblerToolVersion(null, "1.0.0");
            _sut.RecordDisassemblerToolVersion("", "1.0.0");
            _sut.RecordDisassemblerToolVersion("   ", "1.0.0");

            Assert.Empty(_sut.DisassemblerToolVersions);
        }

        [Fact]
        public void RecordDisassemblerToolVersion_NoVersion_UsesToolNameOnly()
        {
            _sut.RecordDisassemblerToolVersion("ildasm", null);

            Assert.True(_sut.DisassemblerToolVersions.ContainsKey("ildasm"));
        }

        #endregion

        #region CollectionState

        [Fact]
        public void SetOldFilesAbsolutePath_ReplacesExistingEntries()
        {
            _sut.SetOldFilesAbsolutePath(new[] { "old-a.dll", "old-b.dll" });
            _sut.SetOldFilesAbsolutePath(new[] { "old-c.dll" });

            Assert.Single(_sut.OldFilesAbsolutePath);
            Assert.Equal("old-c.dll", _sut.OldFilesAbsolutePath.Single());
        }

        [Fact]
        public void AddUnchangedFileRelativePath_Parallel_AllEntriesRecorded()
        {
            const int total = 1000;
            Parallel.For(0, total, i =>
            {
                _sut.AddUnchangedFileRelativePath($"unchanged-{i}.dll");
            });

            Assert.Equal(total, _sut.UnchangedFilesRelativePath.Count);
        }

        [Fact]
        public void ResetAll_ClearsAllState()
        {
            _sut.SetOldFilesAbsolutePath(new[] { "old-a.dll" });
            _sut.SetNewFilesAbsolutePath(new[] { "new-a.dll" });
            _sut.AddUnchangedFileRelativePath("same.dll");
            _sut.AddAddedFileAbsolutePath("added.dll");
            _sut.AddRemovedFileAbsolutePath("removed.dll");
            _sut.AddModifiedFileRelativePath("modified.dll");
            _sut.RecordDiffDetail("same.dll", FileDiffResultLists.DiffDetailResult.MD5Match);
            _sut.RecordIgnoredFile("ignored.pdb", FileDiffResultLists.IgnoredFileLocation.Old);
            _sut.RecordDisassemblerToolVersion("dotnet-ildasm", "1.0.0");
            _sut.RecordDisassemblerToolVersion("dotnet-ildasm", "1.0.0", fromCache: true);

            _sut.ResetAll();

            Assert.Empty(_sut.OldFilesAbsolutePath);
            Assert.Empty(_sut.NewFilesAbsolutePath);
            Assert.Empty(_sut.UnchangedFilesRelativePath);
            Assert.Empty(_sut.AddedFilesAbsolutePath);
            Assert.Empty(_sut.RemovedFilesAbsolutePath);
            Assert.Empty(_sut.ModifiedFilesRelativePath);
            Assert.Empty(_sut.FileRelativePathToDiffDetailDictionary);
            Assert.Empty(_sut.FileRelativePathToIlDisassemblerLabelDictionary);
            Assert.Empty(_sut.IgnoredFilesRelativePathToLocation);
            Assert.Empty(_sut.DisassemblerToolVersions);
            Assert.Empty(_sut.DisassemblerToolVersionsFromCache);
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
