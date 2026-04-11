using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="DotNetDisassemblerProvider"/>.
    /// <see cref="DotNetDisassemblerProvider"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class DotNetDisassemblerProviderTests
    {
        [Fact]
        public void Priority_ReturnsZero()
        {
            var provider = CreateProvider();
            Assert.Equal(0, provider.Priority);
        }

        [Fact]
        public void DisplayName_IsNotEmpty()
        {
            var provider = CreateProvider();
            Assert.False(string.IsNullOrWhiteSpace(provider.DisplayName));
        }

        [Fact]
        public void CanHandle_NonDllOrExe_ReturnsFalse()
        {
            var provider = CreateProvider();
            Assert.False(provider.CanHandle("/path/to/file.txt"));
            Assert.False(provider.CanHandle("/path/to/file.json"));
            Assert.False(provider.CanHandle("/path/to/file.config"));
        }

        [Fact]
        public void CanHandle_DllButNotDotNet_ReturnsFalse()
        {
            var fakeComparison = new FakeFileComparisonServiceForProvider
            {
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            var provider = CreateProvider(fileComparisonService: fakeComparison);
            Assert.False(provider.CanHandle("/path/to/native.dll"));
        }

        [Fact]
        public void CanHandle_DotNetDll_ReturnsTrue()
        {
            var fakeComparison = new FakeFileComparisonServiceForProvider
            {
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var provider = CreateProvider(fileComparisonService: fakeComparison);
            Assert.True(provider.CanHandle("/path/to/managed.dll"));
        }

        [Fact]
        public void CanHandle_DotNetExe_ReturnsTrue()
        {
            var fakeComparison = new FakeFileComparisonServiceForProvider
            {
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var provider = CreateProvider(fileComparisonService: fakeComparison);
            Assert.True(provider.CanHandle("/path/to/app.exe"));
        }

        [Fact]
        public void CanHandle_WhenDetectionThrowsRecoverableException_ReturnsFalse()
        {
            var fakeComparison = new FakeFileComparisonServiceForProvider
            {
                ThrowOnDetect = new ArgumentException("bad path")
            };
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var provider = CreateProvider(fileComparisonService: fakeComparison, logger: logger);

            Assert.False(provider.CanHandle("bad\0path.dll"));
            Assert.Contains(logger.Messages, m => m.Contains("managed-assembly detection failed", StringComparison.Ordinal));
        }

        [Fact]
        public async Task DisassembleAsync_DelegatesAndReturnsSuccess()
        {
            var fakeDisassemble = new FakeDisassembleService
            {
                OldIlText = "// IL code here",
                OldCommandString = "dotnet-ildasm --force /path/to/test.dll"
            };
            var provider = CreateProvider(disassembleService: fakeDisassemble);

            // Act / 実行
            var result = await provider.DisassembleAsync("/path/to/test.dll", CancellationToken.None);

            // Assert / 検証
            Assert.True(result.Success);
            Assert.Equal("// IL code here", result.Text);
            Assert.Contains("dotnet-ildasm", result.CommandString);
        }

        [Fact]
        public async Task DisassembleAsync_ServiceThrows_ReturnsFailure()
        {
            var fakeDisassemble = new FakeDisassembleService
            {
                ThrowOnDisassemble = new InvalidOperationException("Tool not found")
            };
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var provider = CreateProvider(disassembleService: fakeDisassemble, logger: logger);

            // Act / 実行
            var result = await provider.DisassembleAsync("/path/to/test.dll", CancellationToken.None);

            // Assert / 検証
            Assert.False(result.Success);
            Assert.Contains("Tool not found", result.CommandString);
            Assert.Contains("InvalidOperationException", result.CommandString);
            Assert.Contains("InvalidOperationException", result.VersionLabel);
            Assert.Contains(logger.Messages, m => m.Contains("provider failed", StringComparison.Ordinal));
        }

        [Fact]
        public void CanHandle_NullFilePath_ThrowsArgumentNullException()
        {
            var provider = CreateProvider();
            Assert.Throws<ArgumentNullException>(() => provider.CanHandle(null!));
        }

        // ── Factory / ファクトリ ──

        private static DotNetDisassemblerProvider CreateProvider(
            FakeDisassembleService? disassembleService = null,
            FakeFileComparisonServiceForProvider? fileComparisonService = null,
            TestLogger? logger = null)
        {
            return new DotNetDisassemblerProvider(
                disassembleService ?? new FakeDisassembleService(),
                fileComparisonService ?? new FakeFileComparisonServiceForProvider(),
                logger ?? new TestLogger(logFileAbsolutePath: "test.log"));
        }

        // ── Fakes / フェイク ──

        private sealed class FakeDisassembleService : IDotNetDisassembleService
        {
            public string OldIlText { get; set; } = "";
            public string OldCommandString { get; set; } = "";
            public string NewIlText { get; set; } = "";
            public string NewCommandString { get; set; } = "";
            public Exception? ThrowOnDisassemble { get; set; }

            public Task<(string oldIlText, string oldCommandString, string newIlText, string newCommandString)> DisassemblePairWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
            {
                if (ThrowOnDisassemble != null) throw ThrowOnDisassemble;
                return Task.FromResult((OldIlText, OldCommandString, NewIlText, NewCommandString));
            }

            public Task<(IReadOnlyList<string> oldIlLines, string oldCommandString, IReadOnlyList<string> newIlLines, string newCommandString)> DisassemblePairAsLinesWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
            {
                if (ThrowOnDisassemble != null) throw ThrowOnDisassemble;
                IReadOnlyList<string> oldLines = OldIlText.Split('\n');
                IReadOnlyList<string> newLines = NewIlText.Split('\n');
                return Task.FromResult((oldLines, OldCommandString, newLines, NewCommandString));
            }

            public Task PrefetchIlCacheAsync(IEnumerable<string> paths, int maxParallel, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        private sealed class FakeFileComparisonServiceForProvider : IFileComparisonService
        {
            public DotNetExecutableDetectionResult DotNetDetectionResult { get; set; } =
                new(DotNetExecutableDetectionStatus.NotDotNetExecutable);
            public Exception? ThrowOnDetect { get; set; }

            public Task<bool> DiffFilesByHashAsync(string file1, string file2) => Task.FromResult(false);
            public Task<(bool, string?, string?)> DiffFilesByHashWithHexAsync(string file1, string file2)
                => Task.FromResult<(bool, string?, string?)>((false, null, null));
            public Task<bool> DiffTextFilesAsync(string file1, string file2) => Task.FromResult(false);
            public DotNetExecutableDetectionResult DetectDotNetExecutable(string path)
            {
                if (ThrowOnDetect != null)
                {
                    throw ThrowOnDetect;
                }

                return DotNetDetectionResult;
            }
            public bool FileExists(string path) => false;
            public long GetFileLength(string path) => 0;
            public Task<int> ReadChunkAsync(string path, long offset, Memory<byte> buffer, CancellationToken ct)
                => Task.FromResult(0);
        }
    }
}
