using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.EdgeCases
{
    /// <summary>
    /// Tests for ultra-large file comparison scenarios.
    /// Verifies that multi-MiB files are handled correctly via both sequential and chunk-parallel paths.
    /// 超大規模ファイル比較のシナリオテスト。
    /// 数 MiB のファイルが逐次およびチャンク並列パスの両方で正しく処理されることを確認する。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class LargeFileComparisonTests
    {
        [Fact]
        public async Task FilesAreEqualAsync_LargeIdenticalTextFiles_ComparedViaChunkParallel_ReturnsTrue()
        {
            // 4 MiB identical text files compared via parallel chunk comparison
            // 4 MiB の同一テキストファイルを並列チャンク比較で処理
            const string relativePath = "large-identical.txt";
            const int fileSize = 4 * 1024 * 1024; // 4 MiB
            var content = new string('A', fileSize);

            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            var oldPath = Path.Combine("/virtual/old", relativePath);
            var newPath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldPath, content);
            fileComparisonService.SetFileContent(newPath, content);

            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, resultLists, logger,
                config =>
                {
                    config.TextDiffParallelThresholdKilobytes = 64;
                    config.TextDiffChunkSizeKilobytes = 256;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 4);

            Assert.True(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            Assert.NotEmpty(fileComparisonService.ReadChunkCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_LargeDifferingTextFiles_DiffDetectedViaChunkParallel()
        {
            // 4 MiB text files with a single byte difference at the end
            // ファイル末尾に 1 バイトの違いがある 4 MiB テキストファイル
            const string relativePath = "large-differ-tail.txt";
            const int fileSize = 4 * 1024 * 1024;
            var contentA = new string('X', fileSize);
            var contentB = new string('X', fileSize - 1) + "Y";

            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            var oldPath = Path.Combine("/virtual/old", relativePath);
            var newPath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldPath, contentA);
            fileComparisonService.SetFileContent(newPath, contentB);

            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, resultLists, logger,
                config =>
                {
                    config.TextDiffParallelThresholdKilobytes = 64;
                    config.TextDiffChunkSizeKilobytes = 256;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 4);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
        }

        [Fact]
        public async Task FilesAreEqualAsync_LargeFilesWithDifferentSizes_ReturnsNotEqual()
        {
            // Files of different sizes should be detected as different
            // 異なるサイズのファイルは異なるとして検出される
            const string relativePath = "size-mismatch.txt";
            var contentA = new string('Z', 2 * 1024 * 1024);
            var contentB = new string('Z', 3 * 1024 * 1024);

            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            var oldPath = Path.Combine("/virtual/old", relativePath);
            var newPath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldPath, contentA);
            fileComparisonService.SetFileContent(newPath, contentB);

            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, resultLists, logger,
                config =>
                {
                    config.TextDiffParallelThresholdKilobytes = 64;
                    config.TextDiffChunkSizeKilobytes = 256;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 4);

            Assert.False(areEqual);
        }

        [Fact]
        public async Task FilesAreEqualAsync_EmptyFiles_ReturnsTrueViaSHA256()
        {
            // Empty files should match via SHA256 hash comparison
            // 空ファイルは SHA256 ハッシュ比較で一致する
            const string relativePath = "empty.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = true
            };

            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, resultLists, logger);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.True(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.SHA256Match,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
        }

        [Fact]
        public async Task FilesAreEqualAsync_VerySmallChunkSize_ManyChunksStillCompareCorrectly()
        {
            // 64 KB files with 1 KB chunk size = 64 chunks; tests chunk boundary correctness
            // 64 KB ファイルを 1 KB チャンクで分割 = 64 チャンク; チャンク境界の正確性をテスト
            const string relativePath = "many-chunks.txt";
            const int fileSize = 64 * 1024;
            var content = new string('M', fileSize);

            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            var oldPath = Path.Combine("/virtual/old", relativePath);
            var newPath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldPath, content);
            fileComparisonService.SetFileContent(newPath, content);

            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, resultLists, logger,
                config =>
                {
                    config.TextDiffParallelThresholdKilobytes = 1;
                    config.TextDiffChunkSizeKilobytes = 1;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 8);

            Assert.True(areEqual);
            Assert.True(fileComparisonService.ReadChunkCalls.Count >= 64,
                $"Expected >= 64 chunk reads, got {fileComparisonService.ReadChunkCalls.Count}");
        }

        private static FileDiffService CreateService(
            FakeFileComparisonService fileComparisonService,
            FileDiffResultLists resultLists,
            TestLogger logger,
            Action<ConfigSettingsBuilder>? configure = null)
        {
            var builder = new ConfigSettingsBuilder
            {
                TextFileExtensions = new List<string> { ".txt" },
                IgnoredExtensions = new List<string>(),
                ShouldOutputILText = false,
                EnableILCache = false,
                OptimizeForNetworkShares = false,
                TextDiffParallelThresholdKilobytes = ConfigSettings.DefaultTextDiffParallelThresholdKilobytes,
                TextDiffChunkSizeKilobytes = ConfigSettings.DefaultTextDiffChunkSizeKilobytes,
                TextDiffParallelMemoryLimitMegabytes = 0
            };
            configure?.Invoke(builder);
            var config = builder.Build();

            var executionContext = new DiffExecutionContext(
                "/virtual/old", "/virtual/new", "/virtual/report",
                optimizeForNetworkShares: false, detectedNetworkOld: false, detectedNetworkNew: false);
            var ilOutputService = new FakeILOutputService();
            return new FileDiffService(config, ilOutputService, executionContext, resultLists, logger, fileComparisonService);
        }

        private sealed class FakeFileComparisonService : IFileComparisonService
        {
            private readonly Dictionary<string, byte[]> _contentsByPath = new(StringComparer.OrdinalIgnoreCase);

            public bool HashResult { get; set; }
            public Exception HashException { get; set; }
            public bool TextDiffResult { get; set; }
            public Exception TextDiffException { get; set; }
            public DotNetExecutableDetectionResult DotNetDetectionResult { get; set; } =
                new(DotNetExecutableDetectionStatus.NotDotNetExecutable);

            public ConcurrentBag<(string, string)> HashCalls { get; } = new();
            public ConcurrentBag<string> DotNetDetectionCalls { get; } = new();
            public ConcurrentBag<(string, string)> TextDiffCalls { get; } = new();
            public ConcurrentBag<(string Path, long Offset, int Length)> ReadChunkCalls { get; } = new();

            public void SetFileContent(string path, string content)
                => _contentsByPath[path] = System.Text.Encoding.UTF8.GetBytes(content);

            public Task<bool> DiffFilesByHashAsync(string file1, string file2)
            {
                HashCalls.Add((file1, file2));
                return HashException != null ? throw HashException : Task.FromResult(HashResult);
            }

            public Task<(bool AreEqual, string? Hash1Hex, string? Hash2Hex)> DiffFilesByHashWithHexAsync(
                string file1, string file2)
            {
                HashCalls.Add((file1, file2));
                if (HashException != null) throw HashException;
                string? hash1 = HashResult ? "a".PadRight(64, '0') : "a".PadRight(64, '0');
                string? hash2 = HashResult ? "a".PadRight(64, '0') : "b".PadRight(64, '0');
                return Task.FromResult((HashResult, hash1, hash2));
            }

            public Task<bool> DiffTextFilesAsync(string file1, string file2)
            {
                TextDiffCalls.Add((file1, file2));
                return TextDiffException != null ? throw TextDiffException : Task.FromResult(TextDiffResult);
            }

            public DotNetExecutableDetectionResult DetectDotNetExecutable(string path)
            {
                DotNetDetectionCalls.Add(path);
                return DotNetDetectionResult;
            }

            public bool FileExists(string path) => _contentsByPath.ContainsKey(path);

            public long GetFileLength(string path)
            {
                if (_contentsByPath.TryGetValue(path, out var c)) return c.LongLength;
                throw new FileNotFoundException($"Not found: {path}", path);
            }

            public Task<int> ReadChunkAsync(string path, long offset, Memory<byte> buffer, System.Threading.CancellationToken ct)
            {
                ReadChunkCalls.Add((path, offset, buffer.Length));
                if (!_contentsByPath.TryGetValue(path, out var content))
                    throw new FileNotFoundException($"Not found: {path}", path);
                int start = checked((int)offset);
                if (start >= content.Length) return Task.FromResult(0);
                int count = Math.Min(buffer.Length, content.Length - start);
                content.AsMemory(start, count).CopyTo(buffer);
                return Task.FromResult(count);
            }
        }

        private sealed class FakeILOutputService : IILOutputService
        {
            public Task PrecomputeAsync(System.Collections.Generic.IEnumerable<string> filesAbsolutePaths, int maxParallel, System.Threading.CancellationToken ct = default)
                => Task.CompletedTask;
            public void PreSeedFileHash(string fileAbsolutePath, string sha256Hex) { }
            public Task<(bool AreEqual, string? DisassemblerLabel)> DiffDotNetAssembliesAsync(string fileRelativePath, string oldFolder, string newFolder, bool shouldOutput, System.Threading.CancellationToken ct = default)
                => Task.FromResult((false, (string?)null));
        }

    }
}
