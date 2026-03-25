using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Text comparison edge-case tests for <see cref="FileDiffService"/> (memory limits, case sensitivity, chunk differences, negative config values).
    /// <see cref="FileDiffService"/> のテキスト比較エッジケーステスト（メモリ制限、大文字小文字、チャンク差分、負の設定値）。
    /// </summary>
    public sealed partial class FileDiffServiceUnitTests
    {
        [Fact]
        public async Task FilesAreEqualAsync_WhenMemoryLimitIsZero_UsesUnlimitedParallelism()
        {
            // DetermineEffectiveTextDiffParallelism with memoryLimit <= 0 means unlimited,
            // so it should use the requested parallelism without reduction.
            // memoryLimit <= 0 は制限なしを意味し、要求された並列度をそのまま使用すべき。
            const string relativePath = "unlimited.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
            };
            var oldPath = Path.Combine("/virtual/old", relativePath);
            var newPath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldPath, new string('M', 2048));
            fileComparisonService.SetFileContent(newPath, new string('M', 2048));

            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(
                fileComparisonService,
                ilOutputService,
                resultLists,
                logger,
                optimizeForNetworkShares: false,
                configure: config =>
                {
                    config.TextDiffParallelThresholdKilobytes = 1;
                    config.TextDiffChunkSizeKilobytes = 1;
                    // 0 means unlimited / 0 は制限なしを意味する
                    config.TextDiffParallelMemoryLimitMegabytes = 0;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 4);

            Assert.True(areEqual);
            // Parallel chunk comparison should be used (no memory limit applied)
            // 並列チャンク比較が使用されるべき（メモリ制限なし）
            Assert.NotEmpty(fileComparisonService.ReadChunkCalls);
            Assert.Empty(fileComparisonService.TextDiffCalls);
            // No memory budget log entry should appear / メモリ予算ログエントリは出力されないべき
            Assert.DoesNotContain(logger.Entries,
                entry => entry.Message.Contains("Text diff memory budget applied", StringComparison.Ordinal));
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenTextExtensionMatchesCaseInsensitively_PerformsTextComparison()
        {
            // Verify that text extension matching is case-insensitive (.TXT matches .txt).
            // テキスト拡張子の照合が大文字小文字を区別しないことを検証（.TXT が .txt に一致）。
            const string relativePath = "file.TXT";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
                TextDiffResult = true
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                optimizeForNetworkShares: true);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.True(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            Assert.Single(fileComparisonService.TextDiffCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenRequestedMaxParallelIsZero_UsesSequentialTextDiff()
        {
            // DetermineEffectiveTextDiffParallelism with requestedMaxParallel == 0 returns 0,
            // which triggers the "effectiveMaxParallel == 1" fallback (actually returns 0 which
            // is <= 1), using sequential text diff.
            // requestedMaxParallel == 0 の場合、DetermineEffectiveTextDiffParallelism は 0 を返し、
            // 逐次テキスト比較にフォールバックする。
            const string relativePath = "zero-parallel.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
                TextDiffResult = true
            };
            var oldPath = Path.Combine("/virtual/old", relativePath);
            var newPath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldPath, new string('Z', 2048));
            fileComparisonService.SetFileContent(newPath, new string('Z', 2048));

            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(
                fileComparisonService,
                ilOutputService,
                resultLists,
                logger,
                optimizeForNetworkShares: false,
                configure: config =>
                {
                    config.TextDiffParallelThresholdKilobytes = 1;
                    config.TextDiffChunkSizeKilobytes = 1;
                });

            // maxParallel=0 triggers requestedMaxParallel <= 1 early return path
            // maxParallel=0 は requestedMaxParallel <= 1 の早期リターンパスをトリガーする
            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 0);

            Assert.True(areEqual);
            Assert.Single(fileComparisonService.TextDiffCalls);
            Assert.Empty(fileComparisonService.ReadChunkCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenParallelChunkFilesHaveDifferentSizes_ReturnsFalse()
        {
            // DiffTextFilesParallelAsync returns false immediately when file sizes differ.
            // ファイルサイズが異なる場合、DiffTextFilesParallelAsync は即座に false を返すべき。
            const string relativePath = "size-diff.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            var oldPath = Path.Combine("/virtual/old", relativePath);
            var newPath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldPath, new string('A', 2048));
            fileComparisonService.SetFileContent(newPath, new string('A', 1024));

            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(
                fileComparisonService,
                ilOutputService,
                resultLists,
                logger,
                optimizeForNetworkShares: false,
                configure: config =>
                {
                    config.TextDiffParallelThresholdKilobytes = 1;
                    config.TextDiffChunkSizeKilobytes = 1;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 4);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            // No chunk reads needed since size mismatch short-circuits
            // サイズ不一致で短絡するためチャンク読み取り不要
            Assert.Empty(fileComparisonService.ReadChunkCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenParallelChunkFilesHaveDifferentContent_ReturnsFalse()
        {
            // DiffTextFilesParallelAsync detects byte-level differences across chunks.
            // DiffTextFilesParallelAsync はチャンク間のバイトレベル差異を検出すべき。
            const string relativePath = "content-diff.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            var oldPath = Path.Combine("/virtual/old", relativePath);
            var newPath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldPath, new string('A', 2048));
            fileComparisonService.SetFileContent(newPath, new string('B', 2048));

            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(
                fileComparisonService,
                ilOutputService,
                resultLists,
                logger,
                optimizeForNetworkShares: false,
                configure: config =>
                {
                    config.TextDiffParallelThresholdKilobytes = 1;
                    config.TextDiffChunkSizeKilobytes = 1;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 2);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            // At least one chunk read should have occurred before detecting the difference
            // 差異検出前に少なくとも1回のチャンク読み取りが発生すべき
            Assert.NotEmpty(fileComparisonService.ReadChunkCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenParallelChunkFileDoesNotExist_ReturnsFalse()
        {
            // DiffTextFilesParallelAsync returns false when either file does not exist.
            // いずれかのファイルが存在しない場合、DiffTextFilesParallelAsync は false を返すべき。
            const string relativePath = "missing-one.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            // Only set content for old file, not new
            // 旧ファイルのみコンテンツを設定し、新ファイルは未設定
            var oldPath = Path.Combine("/virtual/old", relativePath);
            fileComparisonService.SetFileContent(oldPath, new string('C', 2048));

            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(
                fileComparisonService,
                ilOutputService,
                resultLists,
                logger,
                optimizeForNetworkShares: false,
                configure: config =>
                {
                    config.TextDiffParallelThresholdKilobytes = 1;
                    config.TextDiffChunkSizeKilobytes = 1;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 2);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenNegativeMemoryLimit_TreatsAsUnlimited()
        {
            // A negative TextDiffParallelMemoryLimitMegabytes should be treated as unlimited
            // (same as 0), so parallel chunk comparison should proceed without reduction.
            // 負の TextDiffParallelMemoryLimitMegabytes は制限なし（0 と同様）として扱われ、
            // 並列チャンク比較が減少なしで実行されるべき。
            const string relativePath = "neg-limit.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
            };
            var oldPath = Path.Combine("/virtual/old", relativePath);
            var newPath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldPath, new string('N', 2048));
            fileComparisonService.SetFileContent(newPath, new string('N', 2048));

            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(
                fileComparisonService,
                ilOutputService,
                resultLists,
                logger,
                optimizeForNetworkShares: false,
                configure: config =>
                {
                    config.TextDiffParallelThresholdKilobytes = 1;
                    config.TextDiffChunkSizeKilobytes = 1;
                    config.TextDiffParallelMemoryLimitMegabytes = -5;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 4);

            Assert.True(areEqual);
            // Parallel chunk comparison should be used (negative limit treated as unlimited)
            // 並列チャンク比較が使用されるべき（負の制限は無制限として扱われる）
            Assert.NotEmpty(fileComparisonService.ReadChunkCalls);
            Assert.Empty(fileComparisonService.TextDiffCalls);
            Assert.DoesNotContain(logger.Entries,
                entry => entry.Message.Contains("Text diff memory budget applied", StringComparison.Ordinal));
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenConfiguredThresholdIsNegative_UsesDefaultThreshold()
        {
            // GetEffectiveBytesFromConfiguredKilobytes returns default when configuredKilobytes <= 0.
            // configuredKilobytes <= 0 の場合、GetEffectiveBytesFromConfiguredKilobytes は既定値を返す。
            const string relativePath = "neg-threshold.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
                TextDiffResult = true
            };
            var oldPath = Path.Combine("/virtual/old", relativePath);
            var newPath = Path.Combine("/virtual/new", relativePath);
            // Small file below default threshold (512 KB)
            // デフォルト閾値（512 KB）未満の小さいファイル
            fileComparisonService.SetFileContent(oldPath, new string('T', 100));
            fileComparisonService.SetFileContent(newPath, new string('T', 100));

            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(
                fileComparisonService,
                ilOutputService,
                resultLists,
                logger,
                optimizeForNetworkShares: false,
                configure: config =>
                {
                    config.TextDiffParallelThresholdKilobytes = -1;
                    config.TextDiffChunkSizeKilobytes = -1;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 4);

            Assert.True(areEqual);
            // File is small, so sequential text diff should be used with default threshold
            // ファイルが小さいため、デフォルト閾値で逐次テキスト比較が使用されるべき
            Assert.Single(fileComparisonService.TextDiffCalls);
            Assert.Empty(fileComparisonService.ReadChunkCalls);
        }
    }
}
