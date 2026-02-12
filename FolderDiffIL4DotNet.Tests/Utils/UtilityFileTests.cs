using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Utils;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Utils
{
    public class UtilityFileTests : IDisposable
    {
        private readonly string _tempDir;

        public UtilityFileTests()
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

        private string CreateTempFile(string name, byte[] content)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllBytes(path, content);
            return path;
        }

        #region DiffFilesByHashAsync

        [Fact]
        public async Task DiffFilesByHashAsync_IdenticalFiles_ReturnsTrue()
        {
            var file1 = CreateTempFile("a.txt", "hello world");
            var file2 = CreateTempFile("b.txt", "hello world");
            Assert.True(await Utility.DiffFilesByHashAsync(file1, file2));
        }

        [Fact]
        public async Task DiffFilesByHashAsync_DifferentContent_ReturnsFalse()
        {
            var file1 = CreateTempFile("a.txt", "hello");
            var file2 = CreateTempFile("b.txt", "world");
            Assert.False(await Utility.DiffFilesByHashAsync(file1, file2));
        }

        [Fact]
        public async Task DiffFilesByHashAsync_DifferentSize_ReturnsFalse()
        {
            var file1 = CreateTempFile("a.txt", "short");
            var file2 = CreateTempFile("b.txt", "this is a longer string");
            Assert.False(await Utility.DiffFilesByHashAsync(file1, file2));
        }

        [Fact]
        public async Task DiffFilesByHashAsync_EmptyFiles_ReturnsTrue()
        {
            var file1 = CreateTempFile("a.txt", "");
            var file2 = CreateTempFile("b.txt", "");
            Assert.True(await Utility.DiffFilesByHashAsync(file1, file2));
        }

        [Fact]
        public async Task DiffFilesByHashAsync_FileNotFound_ThrowsFileNotFoundException()
        {
            var file1 = CreateTempFile("a.txt", "hello");
            var file2 = Path.Combine(_tempDir, "nonexistent.txt");
            await Assert.ThrowsAsync<FileNotFoundException>(() => Utility.DiffFilesByHashAsync(file1, file2));
        }

        #endregion

        #region DiffTextFilesAsync

        [Fact]
        public async Task DiffTextFilesAsync_IdenticalContent_ReturnsTrue()
        {
            var file1 = CreateTempFile("a.txt", "line1\nline2\nline3");
            var file2 = CreateTempFile("b.txt", "line1\nline2\nline3");
            Assert.True(await Utility.DiffTextFilesAsync(file1, file2));
        }

        [Fact]
        public async Task DiffTextFilesAsync_DifferentContent_ReturnsFalse()
        {
            var file1 = CreateTempFile("a.txt", "line1\nline2");
            var file2 = CreateTempFile("b.txt", "line1\nline3");
            Assert.False(await Utility.DiffTextFilesAsync(file1, file2));
        }

        [Fact]
        public async Task DiffTextFilesAsync_DifferentLineCount_ReturnsFalse()
        {
            var file1 = CreateTempFile("a.txt", "line1\nline2");
            var file2 = CreateTempFile("b.txt", "line1\nline2\nline3");
            Assert.False(await Utility.DiffTextFilesAsync(file1, file2));
        }

        [Fact]
        public async Task DiffTextFilesAsync_BothEmpty_ReturnsTrue()
        {
            var file1 = CreateTempFile("a.txt", "");
            var file2 = CreateTempFile("b.txt", "");
            Assert.True(await Utility.DiffTextFilesAsync(file1, file2));
        }

        #endregion

        #region ComputeFileMd5Hex

        [Fact]
        public void ComputeFileMd5Hex_EmptyFile_ReturnsKnownHash()
        {
            var file = CreateTempFile("empty.bin", Array.Empty<byte>());
            var hash = Utility.ComputeFileMd5Hex(file);
            // MD5 of empty input = d41d8cd98f00b204e9800998ecf8427e
            Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", hash);
        }

        [Fact]
        public void ComputeFileMd5Hex_KnownContent_ReturnsExpectedHash()
        {
            var file = CreateTempFile("test.bin", System.Text.Encoding.UTF8.GetBytes("hello"));
            var hash = Utility.ComputeFileMd5Hex(file);
            // MD5("hello") = 5d41402abc4b2a76b9719d911017c592
            Assert.Equal("5d41402abc4b2a76b9719d911017c592", hash);
        }

        [Fact]
        public void ComputeFileMd5Hex_SameContentSameHash()
        {
            var file1 = CreateTempFile("a.bin", System.Text.Encoding.UTF8.GetBytes("test data"));
            var file2 = CreateTempFile("b.bin", System.Text.Encoding.UTF8.GetBytes("test data"));
            Assert.Equal(Utility.ComputeFileMd5Hex(file1), Utility.ComputeFileMd5Hex(file2));
        }

        [Fact]
        public void ComputeFileMd5Hex_DifferentContentDifferentHash()
        {
            var file1 = CreateTempFile("a.bin", System.Text.Encoding.UTF8.GetBytes("aaa"));
            var file2 = CreateTempFile("b.bin", System.Text.Encoding.UTF8.GetBytes("bbb"));
            Assert.NotEqual(Utility.ComputeFileMd5Hex(file1), Utility.ComputeFileMd5Hex(file2));
        }

        #endregion

        #region IsDotNetExecutable

        [Fact]
        public void IsDotNetExecutable_TextFile_ReturnsFalse()
        {
            var file = CreateTempFile("test.txt", "This is not a PE file");
            Assert.False(Utility.IsDotNetExecutable(file));
        }

        [Fact]
        public void IsDotNetExecutable_EmptyFile_ReturnsFalse()
        {
            var file = CreateTempFile("empty.dll", Array.Empty<byte>());
            Assert.False(Utility.IsDotNetExecutable(file));
        }

        [Fact]
        public void IsDotNetExecutable_TooSmallFile_ReturnsFalse()
        {
            var file = CreateTempFile("tiny.dll", new byte[] { 0x4D, 0x5A });
            Assert.False(Utility.IsDotNetExecutable(file));
        }

        [Fact]
        public void IsDotNetExecutable_NonexistentFile_ReturnsFalse()
        {
            Assert.False(Utility.IsDotNetExecutable(Path.Combine(_tempDir, "nonexistent.dll")));
        }

        [Fact]
        public void IsDotNetExecutable_RandomBytes_ReturnsFalse()
        {
            var random = new Random(42);
            var bytes = new byte[1024];
            random.NextBytes(bytes);
            var file = CreateTempFile("random.dll", bytes);
            Assert.False(Utility.IsDotNetExecutable(file));
        }

        [Fact]
        public void IsDotNetExecutable_MinimalMZHeader_NoCLR_ReturnsFalse()
        {
            // Minimal valid PE header without CLR data
            var pe = new byte[512];
            // DOS header: MZ
            pe[0] = 0x4D; pe[1] = 0x5A;
            // PE header offset at 0x3C
            pe[0x3C] = 0x80;
            // PE signature at offset 0x80
            pe[0x80] = 0x50; pe[0x81] = 0x45; pe[0x82] = 0x00; pe[0x83] = 0x00;
            // Optional header magic (PE32)
            pe[0x98] = 0x0B; pe[0x99] = 0x01;
            // CLR RVA and size at offset 0x98 + 0x70 = 0x108 (all zeros = no CLR)
            var file = CreateTempFile("native.exe", pe);
            Assert.False(Utility.IsDotNetExecutable(file));
        }

        #endregion

        #region DeleteFileSilent

        [Fact]
        public void DeleteFileSilent_ExistingFile_Deleted()
        {
            var file = CreateTempFile("delete_me.txt", "content");
            Assert.True(File.Exists(file));
            Utility.DeleteFileSilent(file);
            Assert.False(File.Exists(file));
        }

        [Fact]
        public void DeleteFileSilent_NonexistentFile_DoesNotThrow()
        {
            Utility.DeleteFileSilent(Path.Combine(_tempDir, "no_such_file.txt"));
        }

        [Fact]
        public void DeleteFileSilent_NullOrEmpty_DoesNotThrow()
        {
            Utility.DeleteFileSilent(null);
            Utility.DeleteFileSilent(string.Empty);
        }

        #endregion
    }
}
