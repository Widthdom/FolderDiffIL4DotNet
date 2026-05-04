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
    // Text comparison tests (sequential, parallel, memory budget, network shares) for FileDiffService.
    // FileDiffService のテキスト比較テスト（逐次、並列、メモリ予算、ネットワーク共有）。
    public sealed partial class FileDiffServiceUnitTests
    {
        [Fact]
        public async Task FilesAreEqualAsync_WhenStrictTextByteComparisonEnabled_TreatsHashMismatchAsTextMismatch()
        {
            const string relativePath = "newline-only.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
                TextDiffResult = true
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(
                fileComparisonService,
                ilOutputService,
                resultLists,
                logger,
                optimizeForNetworkShares: true,
                configure: config => config.ShouldTreatTextByteDifferencesAsMismatch = true);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            Assert.Empty(fileComparisonService.TextDiffCalls);
            Assert.Empty(fileComparisonService.ReadChunkCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenLargeTextFilesMatch_UsesParallelChunkComparison()
        {
            const string relativePath = "large.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            var oldFileAbsolutePath = Path.Combine("/virtual/old", relativePath);
            var newFileAbsolutePath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldFileAbsolutePath, new string('A', 2048));
            fileComparisonService.SetFileContent(newFileAbsolutePath, new string('A', 2048));

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

            Assert.True(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMatch, resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            Assert.NotEmpty(fileComparisonService.ReadChunkCalls);
            Assert.Empty(fileComparisonService.TextDiffCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenTextDiffMemoryBudgetCannotFitParallelBuffers_FallsBackToSequentialTextDiff()
        {
            const string relativePath = "memory-limited.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
                TextDiffResult = true
            };
            var oldFileAbsolutePath = Path.Combine("/virtual/old", relativePath);
            var newFileAbsolutePath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldFileAbsolutePath, new string('B', 2048));
            fileComparisonService.SetFileContent(newFileAbsolutePath, new string('B', 2048));

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
                    config.TextDiffChunkSizeKilobytes = 1024;
                    config.TextDiffParallelMemoryLimitMegabytes = 1;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 4);

            Assert.True(areEqual);
            Assert.Single(fileComparisonService.TextDiffCalls);
            Assert.Empty(fileComparisonService.ReadChunkCalls);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Info
                    && entry.Message.Contains("Text diff memory budget applied", StringComparison.Ordinal)
                    && entry.Message.Contains("Falling back to sequential text diff", StringComparison.Ordinal));
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenTextDiffMemoryBudgetPartiallyLimitsParallelism_ReducesParallelismWithoutFallingBack()
        {
            const string relativePath = "partial-limit.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
            };
            var oldFileAbsolutePath = Path.Combine("/virtual/old", relativePath);
            var newFileAbsolutePath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldFileAbsolutePath, new string('D', 2048));
            fileComparisonService.SetFileContent(newFileAbsolutePath, new string('D', 2048));

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
                    // 1 MB chunk: bytesPerWorker = 2 MB, budget 4 MB → maxWorkers = 2 < requestedMaxParallel (8)
                    config.TextDiffChunkSizeKilobytes = 1024;
                    config.TextDiffParallelMemoryLimitMegabytes = 4;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 8);

            Assert.True(areEqual);
            Assert.Empty(fileComparisonService.TextDiffCalls);
            Assert.NotEmpty(fileComparisonService.ReadChunkCalls);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Info
                    && entry.Message.Contains("Text diff memory budget applied", StringComparison.Ordinal)
                    && entry.Message.Contains("Reducing chunk-parallel", StringComparison.Ordinal));
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenSequentialTextCompareThrowsUnauthorizedAccessException_LogsWarningThenErrorAndRethrows()
        {
            const string relativePath = "locked.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
                TextDiffException = new UnauthorizedAccessException("text access denied")
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(
                fileComparisonService,
                ilOutputService,
                resultLists,
                logger,
                optimizeForNetworkShares: true);

            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.FilesAreEqualAsync(relativePath, maxParallel: 1));

            Assert.Equal("text access denied", exception.Message);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Falling back to sequential text diff", warning.Message);
            Assert.Contains("UnauthorizedAccessException", warning.Message);
            Assert.IsType<UnauthorizedAccessException>(warning.Exception);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error
                    && entry.Message.Contains("An error occurred while diffing", StringComparison.Ordinal)
                    && entry.Message.Contains("RelativePath='locked.txt'", StringComparison.Ordinal)
                    && entry.Message.Contains("Stage='comparing text'", StringComparison.Ordinal)
                    && entry.Message.Contains("MaxParallel=1", StringComparison.Ordinal));
            Assert.Empty(resultLists.FileRelativePathToDiffDetailDictionary);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenTextFileMatchesSequentially_RecordsTextMatch()
        {
            // Sequential text comparison (network-optimized) should record TextMatch on equality.
            // 逐次テキスト比較（ネットワーク最適化時）は一致時に TextMatch を記録すべき。
            const string relativePath = "file.txt";
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
        public async Task FilesAreEqualAsync_WhenTextFileDiffersSequentially_RecordsTextMismatch()
        {
            // Sequential text comparison should record TextMismatch when files differ.
            // 逐次テキスト比較でファイルが異なる場合 TextMismatch を記録すべき。
            const string relativePath = "diff.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
                TextDiffResult = false
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                optimizeForNetworkShares: true);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenSmallTextFile_UsesSequentialComparison()
        {
            // Files below the parallel threshold should use sequential text comparison
            // even when not in network-optimized mode.
            // 並列閾値未満のファイルはネットワーク最適化モードでなくても逐次テキスト比較を使用すべき。
            const string relativePath = "small.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
                TextDiffResult = true
            };
            var oldPath = Path.Combine("/virtual/old", relativePath);
            var newPath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldPath, "small");
            fileComparisonService.SetFileContent(newPath, "small");

            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                optimizeForNetworkShares: false);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 4);

            Assert.True(areEqual);
            Assert.Single(fileComparisonService.TextDiffCalls);
            Assert.Empty(fileComparisonService.ReadChunkCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenOptimizeForNetworkSharesTrue_UsesSequentialTextDiffOnly()
        {
            // Under network-share optimisation, CompareAsTextAsync should use sequential
            // text diff (DiffTextFilesAsync) and never use parallel chunk comparison.
            // ネットワーク共有最適化時は逐次テキスト比較（DiffTextFilesAsync）のみを使用し、
            // 並列チャンク比較は使用しないべき。
            const string relativePath = "network.txt";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
                TextDiffResult = true
            };
            // Set up large files that would normally trigger parallel comparison
            // 通常なら並列比較をトリガーする大きなファイルを設定
            var oldPath = Path.Combine("/virtual/old", relativePath);
            var newPath = Path.Combine("/virtual/new", relativePath);
            fileComparisonService.SetFileContent(oldPath, new string('X', 4096));
            fileComparisonService.SetFileContent(newPath, new string('X', 4096));

            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(
                fileComparisonService,
                ilOutputService,
                resultLists,
                logger,
                optimizeForNetworkShares: true,
                configure: config =>
                {
                    config.TextDiffParallelThresholdKilobytes = 1;
                    config.TextDiffChunkSizeKilobytes = 1;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 4);

            Assert.True(areEqual);
            // Sequential text diff should be used / 逐次テキスト比較が使用されるべき
            Assert.Single(fileComparisonService.TextDiffCalls);
            // Parallel chunk comparison should NOT be used / 並列チャンク比較は使用されないべき
            Assert.Empty(fileComparisonService.ReadChunkCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenRequestedMaxParallelIsOne_UsesSequentialTextDiff()
        {
            // DetermineEffectiveTextDiffParallelism with requestedMaxParallel <= 1 returns
            // as-is, and the caller falls back to sequential diff.
            // requestedMaxParallel <= 1 の場合、DetermineEffectiveTextDiffParallelism はそのまま返し、
            // 呼び出し元は逐次比較にフォールバックする。
            const string relativePath = "single-thread.txt";
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

            // maxParallel=1 triggers the requestedMaxParallel <= 1 early return
            // maxParallel=1 は requestedMaxParallel <= 1 の早期リターンをトリガーする
            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.True(areEqual);
            // Sequential text diff should be used since parallelism is 1
            // 並列度が 1 のため逐次テキスト比較が使用されるべき
            Assert.Single(fileComparisonService.TextDiffCalls);
            Assert.Empty(fileComparisonService.ReadChunkCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenDependencyAnalysisFails_LogsWarningWithExceptionType()
        {
            const string relativePath = "app.deps.json";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
                TextDiffResult = false
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(
                fileComparisonService,
                ilOutputService,
                resultLists,
                logger,
                optimizeForNetworkShares: true,
                configure: config =>
                {
                    config.TextFileExtensions = new() { ".json" };
                    config.ShouldIncludeDependencyChangesInReport = true;
                });

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.False(areEqual);
            var warning = Assert.Single(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Warning
                    && entry.Message.Contains("Dependency change analysis failed", StringComparison.Ordinal));
            var expectedOldPath = Path.Combine("/virtual/old", relativePath);
            var expectedNewPath = Path.Combine("/virtual/new", relativePath);
            Assert.NotNull(warning.Exception);
            Assert.Contains(warning.Exception.GetType().Name, warning.Message, StringComparison.Ordinal);
            Assert.Contains($"Old='{expectedOldPath}'", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"New='{expectedNewPath}'", warning.Message, StringComparison.Ordinal);
            Assert.Empty(resultLists.FileRelativePathToDependencyChanges);
        }

    }
}
