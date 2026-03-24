using System;
using System.IO;
using System.Text;
using FolderDiffIL4DotNet.Core.Text;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.Text
{
    [Trait("Category", "Unit")]
    public class EncodingDetectorTests : IDisposable
    {
        private readonly string _tempDir;

        public EncodingDetectorTests()
        {
            EncodingDetector.RegisterCodePages();
            _tempDir = Path.Combine(Path.GetTempPath(), "EncodingDetectorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private string CreateTempFile(string name, byte[] content)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllBytes(path, content);
            return path;
        }

        [Fact]
        public void DetectFileEncoding_Utf8WithBom_ReturnsUtf8()
        {
            var bom = new byte[] { 0xEF, 0xBB, 0xBF };
            var content = Encoding.UTF8.GetBytes("テスト日本語");
            var bytes = new byte[bom.Length + content.Length];
            Buffer.BlockCopy(bom, 0, bytes, 0, bom.Length);
            Buffer.BlockCopy(content, 0, bytes, bom.Length, content.Length);

            var path = CreateTempFile("utf8_bom.txt", bytes);
            var encoding = EncodingDetector.DetectFileEncoding(path);

            // Should detect UTF-8 from BOM / BOM から UTF-8 を検出すべき
            Assert.Equal(Encoding.UTF8.CodePage, encoding.CodePage);
        }

        [Fact]
        public void DetectFileEncoding_Utf8WithoutBom_ReturnsUtf8()
        {
            var content = Encoding.UTF8.GetBytes("テスト日本語 test");
            var path = CreateTempFile("utf8_no_bom.txt", content);
            var encoding = EncodingDetector.DetectFileEncoding(path);

            Assert.Equal(Encoding.UTF8.CodePage, encoding.CodePage);
        }

        [Fact]
        public void DetectFileEncoding_AsciiOnly_ReturnsUtf8()
        {
            var content = Encoding.ASCII.GetBytes("Hello World 123");
            var path = CreateTempFile("ascii.txt", content);
            var encoding = EncodingDetector.DetectFileEncoding(path);

            // Pure ASCII is valid UTF-8 / 純粋な ASCII は有効な UTF-8
            Assert.Equal(Encoding.UTF8.CodePage, encoding.CodePage);
        }

        [Fact]
        public void DetectFileEncoding_Utf16LeBom_ReturnsUtf16Le()
        {
            var bom = new byte[] { 0xFF, 0xFE };
            var content = Encoding.Unicode.GetBytes("テスト");
            var bytes = new byte[bom.Length + content.Length];
            Buffer.BlockCopy(bom, 0, bytes, 0, bom.Length);
            Buffer.BlockCopy(content, 0, bytes, bom.Length, content.Length);

            var path = CreateTempFile("utf16le.txt", bytes);
            var encoding = EncodingDetector.DetectFileEncoding(path);

            Assert.Equal(Encoding.Unicode.CodePage, encoding.CodePage);
        }

        [Fact]
        public void DetectFileEncoding_Utf16BeBom_ReturnsUtf16Be()
        {
            var bom = new byte[] { 0xFE, 0xFF };
            var content = Encoding.BigEndianUnicode.GetBytes("テスト");
            var bytes = new byte[bom.Length + content.Length];
            Buffer.BlockCopy(bom, 0, bytes, 0, bom.Length);
            Buffer.BlockCopy(content, 0, bytes, bom.Length, content.Length);

            var path = CreateTempFile("utf16be.txt", bytes);
            var encoding = EncodingDetector.DetectFileEncoding(path);

            Assert.Equal(Encoding.BigEndianUnicode.CodePage, encoding.CodePage);
        }

        [Fact]
        public void DetectFileEncoding_ShiftJis_ReturnsNonUtf8()
        {
            // Shift_JIS encoded Japanese text / Shift_JIS エンコードの日本語テキスト
            var shiftJis = Encoding.GetEncoding(932);
            var content = shiftJis.GetBytes("日本語のテキストファイル");
            var path = CreateTempFile("shiftjis.txt", content);
            var encoding = EncodingDetector.DetectFileEncoding(path);

            // Should NOT return UTF-8 since Shift_JIS bytes are invalid UTF-8
            // Shift_JIS バイト列は無効な UTF-8 なので UTF-8 を返すべきではない
            Assert.NotEqual(Encoding.UTF8.CodePage, encoding.CodePage);
        }

        [Fact]
        public void DetectFileEncoding_ShiftJis_CanDecodeJapaneseCorrectly()
        {
            // Verifies that a Shift_JIS file is readable with the detected encoding
            // Shift_JIS ファイルが検出されたエンコーディングで正しく読めることを検証
            var shiftJis = Encoding.GetEncoding(932);
            const string originalText = "日本語のテキストファイル\r\nテスト行";
            var content = shiftJis.GetBytes(originalText);
            var path = CreateTempFile("shiftjis_roundtrip.txt", content);

            var encoding = EncodingDetector.DetectFileEncoding(path);
            var readBack = File.ReadAllText(path, encoding);

            Assert.Equal(originalText, readBack);
        }

        [Fact]
        public void DetectFileEncoding_EmptyFile_ReturnsUtf8()
        {
            var path = CreateTempFile("empty.txt", Array.Empty<byte>());
            var encoding = EncodingDetector.DetectFileEncoding(path);

            Assert.Equal(Encoding.UTF8.CodePage, encoding.CodePage);
        }

        [Fact]
        public void DetectEncodingFromBytes_NullishBytes_ReturnsUtf8()
        {
            var encoding = EncodingDetector.DetectEncodingFromBytes(Array.Empty<byte>());
            Assert.Equal(Encoding.UTF8.CodePage, encoding.CodePage);
        }

        [Fact]
        public void DetectFileEncoding_SmallSampleSize_StillDetects()
        {
            // Verify detection works with a small sample / 小さいサンプルでも検出できることを検証
            var shiftJis = Encoding.GetEncoding(932);
            var content = shiftJis.GetBytes("テスト");
            var path = CreateTempFile("small_sample.txt", content);

            var encoding = EncodingDetector.DetectFileEncoding(path, sampleSize: 16);
            Assert.NotEqual(Encoding.UTF8.CodePage, encoding.CodePage);
        }
    }
}
