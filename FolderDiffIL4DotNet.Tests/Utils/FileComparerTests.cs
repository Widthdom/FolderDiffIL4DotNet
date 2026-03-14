using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Utils;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Utils
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
        public void ComputeFileMd5Hex_EmptyFile_ReturnsKnownHash()
        {
            var file = CreateTempFile("empty.bin", Array.Empty<byte>());
            var hash = FileComparer.ComputeFileMd5Hex(file);
            // MD5 of empty input = d41d8cd98f00b204e9800998ecf8427e
            Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", hash);
        }

        [Fact]
        public void ComputeFileMd5Hex_KnownContent_ReturnsExpectedHash()
        {
            var file = CreateTempFile("test.bin", System.Text.Encoding.UTF8.GetBytes("hello"));
            var hash = FileComparer.ComputeFileMd5Hex(file);
            // MD5("hello") = 5d41402abc4b2a76b9719d911017c592
            Assert.Equal("5d41402abc4b2a76b9719d911017c592", hash);
        }

        [Fact]
        public void ComputeFileMd5Hex_SameContentSameHash()
        {
            var file1 = CreateTempFile("a.bin", System.Text.Encoding.UTF8.GetBytes("test data"));
            var file2 = CreateTempFile("b.bin", System.Text.Encoding.UTF8.GetBytes("test data"));
            Assert.Equal(FileComparer.ComputeFileMd5Hex(file1), FileComparer.ComputeFileMd5Hex(file2));
        }

        [Fact]
        public void ComputeFileMd5Hex_DifferentContentDifferentHash()
        {
            var file1 = CreateTempFile("a.bin", System.Text.Encoding.UTF8.GetBytes("aaa"));
            var file2 = CreateTempFile("b.bin", System.Text.Encoding.UTF8.GetBytes("bbb"));
            Assert.NotEqual(FileComparer.ComputeFileMd5Hex(file1), FileComparer.ComputeFileMd5Hex(file2));
        }
    }
}
