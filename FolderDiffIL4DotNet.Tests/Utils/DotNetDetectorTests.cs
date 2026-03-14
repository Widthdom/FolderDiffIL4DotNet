using System;
using System.IO;
using FolderDiffIL4DotNet.Utils;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Utils
{
    public class DotNetDetectorTests : IDisposable
    {
        private readonly string _tempDir;

        public DotNetDetectorTests()
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
        public void IsDotNetExecutable_TextFile_ReturnsFalse()
        {
            var file = CreateTempFile("test.txt", "This is not a PE file");
            Assert.False(DotNetDetector.IsDotNetExecutable(file));
        }

        [Fact]
        public void IsDotNetExecutable_EmptyFile_ReturnsFalse()
        {
            var file = CreateTempFile("empty.dll", Array.Empty<byte>());
            Assert.False(DotNetDetector.IsDotNetExecutable(file));
        }

        [Fact]
        public void IsDotNetExecutable_TooSmallFile_ReturnsFalse()
        {
            var file = CreateTempFile("tiny.dll", new byte[] { 0x4D, 0x5A });
            Assert.False(DotNetDetector.IsDotNetExecutable(file));
        }

        [Fact]
        public void IsDotNetExecutable_NonexistentFile_ReturnsFalse()
        {
            Assert.False(DotNetDetector.IsDotNetExecutable(Path.Combine(_tempDir, "nonexistent.dll")));
        }

        [Fact]
        public void IsDotNetExecutable_RandomBytes_ReturnsFalse()
        {
            var random = new Random(42);
            var bytes = new byte[1024];
            random.NextBytes(bytes);
            var file = CreateTempFile("random.dll", bytes);
            Assert.False(DotNetDetector.IsDotNetExecutable(file));
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
            Assert.False(DotNetDetector.IsDotNetExecutable(file));
        }
    }
}
