using System;
using System.IO;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="AssemblySdkVersionReader"/>.
    /// <see cref="AssemblySdkVersionReader"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class AssemblySdkVersionReaderTests : IDisposable
    {
        private readonly string _tempDir;

        public AssemblySdkVersionReaderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"SdkVersionReaderTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        // ── FormatTargetFramework ──

        [Fact]
        public void FormatTargetFramework_NetCoreApp_FormatsCorrectly()
        {
            // ".NETCoreApp,Version=v8.0" → ".NET 8.0"
            var result = AssemblySdkVersionReader.FormatTargetFramework(".NETCoreApp,Version=v8.0");
            Assert.Equal(".NET 8.0", result);
        }

        [Fact]
        public void FormatTargetFramework_NetFramework_FormatsCorrectly()
        {
            // ".NETFramework,Version=v4.7.2" → ".NET Framework 4.7.2"
            var result = AssemblySdkVersionReader.FormatTargetFramework(".NETFramework,Version=v4.7.2");
            Assert.Equal(".NET Framework 4.7.2", result);
        }

        [Fact]
        public void FormatTargetFramework_NetStandard_FormatsCorrectly()
        {
            // ".NETStandard,Version=v2.1" → ".NET Standard 2.1"
            var result = AssemblySdkVersionReader.FormatTargetFramework(".NETStandard,Version=v2.1");
            Assert.Equal(".NET Standard 2.1", result);
        }

        [Fact]
        public void FormatTargetFramework_UnknownFramework_UsesRawName()
        {
            // Unknown framework identifier preserves name / 不明なフレームワーク識別子は名前を保持
            var result = AssemblySdkVersionReader.FormatTargetFramework(".CustomFramework,Version=v1.0");
            Assert.Equal(".CustomFramework 1.0", result);
        }

        [Fact]
        public void FormatTargetFramework_NoComma_ReturnsRawValue()
        {
            // No comma means no parseable format / カンマなしはパース不可
            var result = AssemblySdkVersionReader.FormatTargetFramework("net8.0");
            Assert.Equal("net8.0", result);
        }

        [Fact]
        public void FormatTargetFramework_NoVersionPrefix_FallsBackToEqualsSign()
        {
            // "SomeFramework,CustomVersion=3.0" — no "Version=v" prefix / "Version=v" プレフィックスなし
            var result = AssemblySdkVersionReader.FormatTargetFramework("SomeFramework,CustomVersion=3.0");
            Assert.Equal("SomeFramework 3.0", result);
        }

        // ── ReadTargetFramework ──

        [Fact]
        public void ReadTargetFramework_RealAssembly_ReturnsNonNull()
        {
            // Reading a real .NET assembly should return a framework string / 実アセンブリからフレームワーク文字列を返す
            var assemblyPath = typeof(AssemblySdkVersionReaderTests).Assembly.Location;
            var result = AssemblySdkVersionReader.ReadTargetFramework(assemblyPath);
            Assert.NotNull(result);
            Assert.Contains(".NET", result);
        }

        [Fact]
        public void ReadTargetFramework_NonExistentFile_ReturnsNull()
        {
            // Non-existent file should return null (best-effort) / 存在しないファイルは null を返す
            var result = AssemblySdkVersionReader.ReadTargetFramework(Path.Combine(_tempDir, "nonexistent.dll"));
            Assert.Null(result);
        }

        [Fact]
        public void ReadTargetFramework_NonAssemblyFile_ReturnsNull()
        {
            // A plain text file should return null / テキストファイルは null を返す
            var filePath = Path.Combine(_tempDir, "notanassembly.dll");
            File.WriteAllText(filePath, "This is not a .NET assembly");
            var result = AssemblySdkVersionReader.ReadTargetFramework(filePath);
            Assert.Null(result);
        }

        [Fact]
        public void ReadTargetFramework_EmptyFile_ReturnsNull()
        {
            // Empty file should return null / 空ファイルは null を返す
            var filePath = Path.Combine(_tempDir, "empty.dll");
            File.WriteAllBytes(filePath, Array.Empty<byte>());
            var result = AssemblySdkVersionReader.ReadTargetFramework(filePath);
            Assert.Null(result);
        }

        // ── ReadPairDisplayString ──

        [Fact]
        public void ReadPairDisplayString_BothSameAssembly_ReturnsSingleVersion()
        {
            // Same assembly for both → single version string / 同一アセンブリなら単一バージョン
            var assemblyPath = typeof(AssemblySdkVersionReaderTests).Assembly.Location;
            var result = AssemblySdkVersionReader.ReadPairDisplayString(assemblyPath, assemblyPath);
            Assert.NotNull(result);
            Assert.DoesNotContain("→", result);
        }

        [Fact]
        public void ReadPairDisplayString_BothNonExistent_ReturnsNull()
        {
            // Both paths unreadable → null / 両方読めない → null
            var path1 = Path.Combine(_tempDir, "fake1.dll");
            var path2 = Path.Combine(_tempDir, "fake2.dll");
            var result = AssemblySdkVersionReader.ReadPairDisplayString(path1, path2);
            Assert.Null(result);
        }

        [Fact]
        public void ReadPairDisplayString_OldNonExistent_ReturnsNewVersion()
        {
            // Only new is readable → returns new version / 新のみ読める → 新バージョンを返す
            var fakePath = Path.Combine(_tempDir, "fake.dll");
            var realPath = typeof(AssemblySdkVersionReaderTests).Assembly.Location;
            var result = AssemblySdkVersionReader.ReadPairDisplayString(fakePath, realPath);
            Assert.NotNull(result);
        }

        [Fact]
        public void ReadPairDisplayString_NewNonExistent_ReturnsOldVersion()
        {
            // Only old is readable → returns old version / 旧のみ読める → 旧バージョンを返す
            var fakePath = Path.Combine(_tempDir, "fake.dll");
            var realPath = typeof(AssemblySdkVersionReaderTests).Assembly.Location;
            var result = AssemblySdkVersionReader.ReadPairDisplayString(realPath, fakePath);
            Assert.NotNull(result);
        }
    }
}
