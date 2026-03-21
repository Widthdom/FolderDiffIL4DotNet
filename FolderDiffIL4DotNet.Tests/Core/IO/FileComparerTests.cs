using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.IO;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.IO
{
    public class FileComparerTests : IDisposable
    {
        private readonly string _tempDir;

        public FileComparerTests()
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

        [Fact]
        public async Task DiffFilesByHashAsync_IdenticalFiles_ReturnsTrue()
        {
            var file1 = CreateTempFile("a.txt", "hello world");
            var file2 = CreateTempFile("b.txt", "hello world");
            Assert.True(await FileComparer.DiffFilesByHashAsync(file1, file2));
        }

        [Fact]
        public async Task DiffFilesByHashAsync_DifferentContent_ReturnsFalse()
        {
            var file1 = CreateTempFile("a.txt", "hello");
            var file2 = CreateTempFile("b.txt", "world");
            Assert.False(await FileComparer.DiffFilesByHashAsync(file1, file2));
        }

        [Fact]
        public async Task DiffFilesByHashAsync_DifferentSize_ReturnsFalse()
        {
            var file1 = CreateTempFile("a.txt", "short");
            var file2 = CreateTempFile("b.txt", "this is a longer string");
            Assert.False(await FileComparer.DiffFilesByHashAsync(file1, file2));
        }

        [Fact]
        public async Task DiffFilesByHashAsync_EmptyFiles_ReturnsTrue()
        {
            var file1 = CreateTempFile("a.txt", "");
            var file2 = CreateTempFile("b.txt", "");
            Assert.True(await FileComparer.DiffFilesByHashAsync(file1, file2));
        }

        [Fact]
        public async Task DiffFilesByHashAsync_FileNotFound_ThrowsFileNotFoundException()
        {
            var file1 = CreateTempFile("a.txt", "hello");
            var file2 = Path.Combine(_tempDir, "nonexistent.txt");
            await Assert.ThrowsAsync<FileNotFoundException>(() => FileComparer.DiffFilesByHashAsync(file1, file2));
        }

        [Fact]
        public async Task DiffTextFilesAsync_IdenticalContent_ReturnsTrue()
        {
            var file1 = CreateTempFile("a.txt", "line1\nline2\nline3");
            var file2 = CreateTempFile("b.txt", "line1\nline2\nline3");
            Assert.True(await FileComparer.DiffTextFilesAsync(file1, file2));
        }

        [Fact]
        public async Task DiffTextFilesAsync_DifferentContent_ReturnsFalse()
        {
            var file1 = CreateTempFile("a.txt", "line1\nline2");
            var file2 = CreateTempFile("b.txt", "line1\nline3");
            Assert.False(await FileComparer.DiffTextFilesAsync(file1, file2));
        }

        [Fact]
        public async Task DiffTextFilesAsync_DifferentLineCount_ReturnsFalse()
        {
            var file1 = CreateTempFile("a.txt", "line1\nline2");
            var file2 = CreateTempFile("b.txt", "line1\nline2\nline3");
            Assert.False(await FileComparer.DiffTextFilesAsync(file1, file2));
        }

        [Fact]
        public async Task DiffTextFilesAsync_BothEmpty_ReturnsTrue()
        {
            var file1 = CreateTempFile("a.txt", "");
            var file2 = CreateTempFile("b.txt", "");
            Assert.True(await FileComparer.DiffTextFilesAsync(file1, file2));
        }

        [Fact]
        public void ComputeFileSha256Hex_EmptyFile_ReturnsKnownHash()
        {
            var file = CreateTempFile("empty.bin", Array.Empty<byte>());
            var hash = FileComparer.ComputeFileSha256Hex(file);
            // SHA256 of empty input / 空入力の SHA256
            Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
        }

        [Fact]
        public void ComputeFileSha256Hex_KnownContent_ReturnsExpectedHash()
        {
            var file = CreateTempFile("test.bin", System.Text.Encoding.UTF8.GetBytes("hello"));
            var hash = FileComparer.ComputeFileSha256Hex(file);
            // SHA256("hello") / SHA256("hello")
            Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", hash);
        }

        [Fact]
        public void ComputeFileSha256Hex_SameContentSameHash()
        {
            var file1 = CreateTempFile("a.bin", System.Text.Encoding.UTF8.GetBytes("test data"));
            var file2 = CreateTempFile("b.bin", System.Text.Encoding.UTF8.GetBytes("test data"));
            Assert.Equal(FileComparer.ComputeFileSha256Hex(file1), FileComparer.ComputeFileSha256Hex(file2));
        }

        [Fact]
        public void ComputeFileSha256Hex_DifferentContentDifferentHash()
        {
            var file1 = CreateTempFile("a.bin", System.Text.Encoding.UTF8.GetBytes("aaa"));
            var file2 = CreateTempFile("b.bin", System.Text.Encoding.UTF8.GetBytes("bbb"));
            Assert.NotEqual(FileComparer.ComputeFileSha256Hex(file1), FileComparer.ComputeFileSha256Hex(file2));
        }
    }
}
