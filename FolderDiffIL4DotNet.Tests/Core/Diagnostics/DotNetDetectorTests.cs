using System;
using System.IO;
using FolderDiffIL4DotNet.Core.Diagnostics;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.Diagnostics
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
        public void DetectDotNetExecutable_NonexistentFile_ReturnsFailedWithException()
        {
            var result = DotNetDetector.DetectDotNetExecutable(Path.Combine(_tempDir, "nonexistent.dll"));

            Assert.Equal(DotNetExecutableDetectionStatus.Failed, result.Status);
            Assert.False(result.IsDotNetExecutable);
            Assert.True(result.IsFailure);
            Assert.IsType<FileNotFoundException>(result.Exception);
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
        public void DetectDotNetExecutable_TextFile_ReturnsNotDotNet()
        {
            var file = CreateTempFile("plain.txt", "This is not a PE file");

            var result = DotNetDetector.DetectDotNetExecutable(file);

            Assert.Equal(DotNetExecutableDetectionStatus.NotDotNetExecutable, result.Status);
            Assert.False(result.IsDotNetExecutable);
            Assert.False(result.IsFailure);
            Assert.Null(result.Exception);
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

        /// <summary>
        /// MZ ヘッダはあるが、PE ヘッダオフセットがファイルサイズを超えている場合は NotDotNetExecutable を返します。
        /// </summary>
        [Fact]
        public void DetectDotNetExecutable_PEOffsetOutOfRange_ReturnsNotDotNet()
        {
            var bytes = new byte[64];
            // MZ magic
            bytes[0] = 0x4D; bytes[1] = 0x5A;
            // PE offset at 0x3C = 0xFF (way beyond end of file)
            bytes[0x3C] = 0xFF;
            var file = CreateTempFile("bad-offset.exe", bytes);

            var result = DotNetDetector.DetectDotNetExecutable(file);
            Assert.Equal(DotNetExecutableDetectionStatus.NotDotNetExecutable, result.Status);
        }

        /// <summary>
        /// MZ ヘッダはあり PE ヘッダオフセットは正常だが、PE シグネチャが "PE\0\0" でない場合は NotDotNetExecutable を返します。
        /// </summary>
        [Fact]
        public void DetectDotNetExecutable_WrongPESignature_ReturnsNotDotNet()
        {
            var pe = new byte[256];
            // MZ magic
            pe[0] = 0x4D; pe[1] = 0x5A;
            // PE header at offset 0x40
            pe[0x3C] = 0x40;
            // Wrong PE signature (not "PE\0\0")
            pe[0x40] = 0x4E; pe[0x41] = 0x45; pe[0x42] = 0x00; pe[0x43] = 0x00;
            var file = CreateTempFile("wrong-sig.exe", pe);

            var result = DotNetDetector.DetectDotNetExecutable(file);
            Assert.Equal(DotNetExecutableDetectionStatus.NotDotNetExecutable, result.Status);
        }

        /// <summary>
        /// PE32+（64 bit）ファイルの CLR ヘッダが全て 0 の場合は NotDotNetExecutable を返します。
        /// </summary>
        [Fact]
        public void DetectDotNetExecutable_PE32Plus_NoCLR_ReturnsNotDotNet()
        {
            var pe = new byte[512];
            // MZ magic
            pe[0] = 0x4D; pe[1] = 0x5A;
            // PE header at 0x80
            pe[0x3C] = 0x80;
            // PE signature at 0x80
            pe[0x80] = 0x50; pe[0x81] = 0x45; pe[0x82] = 0x00; pe[0x83] = 0x00;
            // Optional Header magic = PE32+ (0x020B) at 0x80 + 4 + 20 = 0x98
            pe[0x98] = 0x0B; pe[0x99] = 0x02;
            // CLR header for PE32+ is at optionalHeaderStart + 0x80 (= 0x98 + 0x80 = 0x118), all zeros = no CLR
            var file = CreateTempFile("native64.exe", pe);

            var result = DotNetDetector.DetectDotNetExecutable(file);
            Assert.Equal(DotNetExecutableDetectionStatus.NotDotNetExecutable, result.Status);
        }

        /// <summary>
        /// Optional Header の Magic が PE32/PE32+ 以外の未知の値の場合は NotDotNetExecutable を返します。
        /// </summary>
        [Fact]
        public void DetectDotNetExecutable_UnknownOptionalHeaderMagic_ReturnsNotDotNet()
        {
            var pe = new byte[512];
            // MZ magic
            pe[0] = 0x4D; pe[1] = 0x5A;
            // PE header at 0x80
            pe[0x3C] = 0x80;
            // PE signature
            pe[0x80] = 0x50; pe[0x81] = 0x45; pe[0x82] = 0x00; pe[0x83] = 0x00;
            // Unknown optional header magic (0xFFFF)
            pe[0x98] = 0xFF; pe[0x99] = 0xFF;
            var file = CreateTempFile("unknown-magic.exe", pe);

            var result = DotNetDetector.DetectDotNetExecutable(file);
            Assert.Equal(DotNetExecutableDetectionStatus.NotDotNetExecutable, result.Status);
        }
    }
}
