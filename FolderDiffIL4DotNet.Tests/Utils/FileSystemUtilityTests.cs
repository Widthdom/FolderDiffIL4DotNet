using System;
using System.IO;
using FolderDiffIL4DotNet.Utils;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Utils
{
    public class FileSystemUtilityTests : IDisposable
    {
        private readonly string _tempDir;

        public FileSystemUtilityTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"FolderDiffTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        private string CreateTempFile(string name, string content)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content);
            return path;
        }

        #region DeleteFileSilent

        [Fact]
        public void DeleteFileSilent_ExistingFile_Deleted()
        {
            var file = CreateTempFile("delete_me.txt", "content");
            Assert.True(File.Exists(file));
            FileSystemUtility.DeleteFileSilent(file);
            Assert.False(File.Exists(file));
        }

        [Fact]
        public void DeleteFileSilent_NonexistentFile_DoesNotThrow()
        {
            FileSystemUtility.DeleteFileSilent(Path.Combine(_tempDir, "no_such_file.txt"));
        }

        [Fact]
        public void DeleteFileSilent_NullOrEmpty_DoesNotThrow()
        {
            FileSystemUtility.DeleteFileSilent(null);
            FileSystemUtility.DeleteFileSilent(string.Empty);
        }

        #endregion
    }
}
