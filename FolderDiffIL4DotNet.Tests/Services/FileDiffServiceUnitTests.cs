using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="FileDiffService"/> using fake I/O collaborators (no real disk access).
    /// フェイク I/O 協力オブジェクトを使用した <see cref="FileDiffService"/> のユニットテスト（実ディスクアクセスなし）。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class FileDiffServiceUnitTests
    {
        [Fact]
        public async Task FilesAreEqualAsync_WhenHashMatches_ReturnsTrueAndShortCircuitsFurtherWork()
        {
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = true
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var areEqual = await service.FilesAreEqualAsync("sample.txt", maxParallel: 4);

            Assert.True(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.SHA256Match, resultLists.FileRelativePathToDiffDetailDictionary["sample.txt"]);
            Assert.Single(fileComparisonService.HashCalls);
            Assert.Empty(fileComparisonService.DotNetDetectionCalls);
            Assert.Empty(fileComparisonService.TextDiffCalls);
            Assert.Empty(fileComparisonService.ReadChunkCalls);
            Assert.Empty(ilOutputService.DiffCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenHashMatches_SeedsILCacheWithBothHashes()
        {
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = true
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            await service.FilesAreEqualAsync("sample.txt", maxParallel: 4);

            // Both file hashes should have been seeded into the IL cache
            Assert.Equal(2, ilOutputService.PreSeedCalls.Count);
            Assert.Equal(Path.Combine("/virtual/old", "sample.txt"), ilOutputService.PreSeedCalls[0].Path);
            Assert.Equal(Path.Combine("/virtual/new", "sample.txt"), ilOutputService.PreSeedCalls[1].Path);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenHashDiffers_SeedsILCacheWithBothHashes()
        {
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            await service.FilesAreEqualAsync("binary.dat", maxParallel: 1);

            // Even when hashes differ, both computed hashes should be seeded
            Assert.Equal(2, ilOutputService.PreSeedCalls.Count);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenHashDiffThrowsUnauthorizedAccessException_LogsErrorAndRethrows()
        {
            var fileComparisonService = new FakeFileComparisonService
            {
                HashException = new UnauthorizedAccessException("permission denied")
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.FilesAreEqualAsync("secret.bin"));

            Assert.Equal("permission denied", exception.Message);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error
                    && entry.Message.Contains("An error occurred while diffing", StringComparison.Ordinal));
            Assert.Empty(resultLists.FileRelativePathToDiffDetailDictionary);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenIlOutputThrowsIOException_LogsErrorAndRethrows()
        {
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffException = new IOException("disk full")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var exception = await Assert.ThrowsAsync<IOException>(() => service.FilesAreEqualAsync("assembly.dll"));

            Assert.Equal("disk full", exception.Message);
            Assert.Single(ilOutputService.DiffCalls);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error
                    && entry.Message.Contains("An error occurred while diffing", StringComparison.Ordinal));
            Assert.Empty(resultLists.FileRelativePathToDiffDetailDictionary);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenHashDiffThrowsDirectoryNotFoundException_LogsErrorAndRethrows()
        {
            var fileComparisonService = new FakeFileComparisonService
            {
                HashException = new DirectoryNotFoundException("parent directory missing")
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.FilesAreEqualAsync("missing.bin"));

            Assert.Equal("parent directory missing", exception.Message);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error
                    && entry.Exception is DirectoryNotFoundException
                    && entry.Message.Contains("An error occurred while diffing", StringComparison.Ordinal));
            Assert.Empty(resultLists.FileRelativePathToDiffDetailDictionary);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenHashDiffThrowsUnexpectedException_LogsUnexpectedErrorAndRethrows()
        {
            var fileComparisonService = new FakeFileComparisonService
            {
                HashException = new FormatException("bad format")
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var exception = await Assert.ThrowsAsync<FormatException>(() => service.FilesAreEqualAsync("broken.bin"));

            Assert.Equal("bad format", exception.Message);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error
                    && entry.Exception is FormatException
                    && entry.Message.Contains("An unexpected error occurred while diffing", StringComparison.Ordinal));
            Assert.Empty(resultLists.FileRelativePathToDiffDetailDictionary);
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
            Assert.IsType<UnauthorizedAccessException>(warning.Exception);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error
                    && entry.Message.Contains("An error occurred while diffing", StringComparison.Ordinal));
            Assert.Empty(resultLists.FileRelativePathToDiffDetailDictionary);
        }

        [Fact]
        public async Task PrecomputeAsync_WhenSkipILIsTrue_DoesNotCallILOutputService()
        {
            var ilOutputService = new FakeILOutputService();
            var fileComparisonService = new FakeFileComparisonService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.SkipIL = true);

            await service.PrecomputeAsync(new[] { "/virtual/old/assembly.dll" }, maxParallel: 1);

            Assert.Equal(0, ilOutputService.PrecomputeCallCount);
        }

        [Fact]
        public async Task PrecomputeAsync_WhenSkipILIsFalse_DelegatesToILOutputService()
        {
            var ilOutputService = new FakeILOutputService();
            var fileComparisonService = new FakeFileComparisonService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.SkipIL = false);

            await service.PrecomputeAsync(new[] { "/virtual/old/assembly.dll" }, maxParallel: 1);

            Assert.Equal(1, ilOutputService.PrecomputeCallCount);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenSemanticAnalysisFails_DoesNotCrashAndRecordsNoChanges()
        {
            // When ILMismatch triggers TryAnalyzeAssemblySemanticChanges and the analysis
            // fails (e.g. non-existent or corrupt file), AssemblyMethodAnalyzer.Analyze
            // returns null via its internal catch-all. The outer TryAnalyzeAssemblySemanticChanges
            // handles null gracefully and records no semantic changes.
            // ILMismatch 時に TryAnalyzeAssemblySemanticChanges が発火し解析が失敗（存在しない
            // ファイル等）した場合、AssemblyMethodAnalyzer.Analyze が内部 catch-all で null を返す。
            // 外側の TryAnalyzeAssemblySemanticChanges は null を安全に処理し、セマンティック変更を
            // 記録しない。
            const string relativePath = "assembly.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                // IL diff returns not-equal, which triggers semantic analysis
                DiffResult = (AreEqual: false, DisassemblerLabel: "test-tool")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.ShouldIncludeAssemblySemanticChangesInReport = true);

            // Should complete without throwing — AssemblyMethodAnalyzer.Analyze catches
            // internally and returns null for non-existent paths.
            // AssemblyMethodAnalyzer.Analyze が内部で catch して null を返すため例外は発生しない。
            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.ILMismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);

            // No semantic changes should be recorded since analysis returned null
            // 解析が null を返したためセマンティック変更は記録されないこと
            Assert.False(resultLists.FileRelativePathToAssemblySemanticChanges.ContainsKey(relativePath));
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenSkipILIsTrueAndDotNetAssembly_SkipsILAndFallsThroughToTextOrBinaryDiff()
        {
            const string relativePath = "assembly.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                // Would normally trigger IL comparison:
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable),
                // Not a text extension, so neither text nor IL → falls through to no-match
                TextDiffResult = false
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.SkipIL = true);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            // IL was skipped
            Assert.Empty(ilOutputService.DiffCalls);
            Assert.Empty(fileComparisonService.DotNetDetectionCalls);
            Assert.False(areEqual);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenDotNetDetectionFails_LogsWarningAndSkipsIL()
        {
            // When DetectDotNetExecutable returns a failure, the service should log a
            // warning and skip IL comparison (fall through to text/binary comparison).
            // DetectDotNetExecutable が失敗を返した場合、警告をログに記録し IL 比較をスキップすべき。
            const string relativePath = "detect-fail.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(
                    DotNetExecutableDetectionStatus.Failed,
                    new IOException("detection error"))
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.SHA256Mismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            Assert.Empty(ilOutputService.DiffCalls);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Warning
                    && entry.Message.Contains("Failed to detect", StringComparison.Ordinal));
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenILDiffReturnsEqual_RecordsILMatch()
        {
            // When IL comparison returns equal, DiffDetail should be ILMatch.
            // IL 比較が一致を返した場合、DiffDetail は ILMatch であるべき。
            const string relativePath = "matched.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffResult = (AreEqual: true, DisassemblerLabel: "dotnet-ildasm v1.0")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.True(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.ILMatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            Assert.Single(ilOutputService.DiffCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenILDiffThrowsInvalidOperationException_LogsErrorAndRethrows()
        {
            // InvalidOperationException from IL diff should be caught, logged as Error, and re-thrown.
            // IL 差分からの InvalidOperationException はキャッチされ、Error としてログされ、再スローされるべき。
            const string relativePath = "invalid-op.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffException = new InvalidOperationException("no disassembler")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.FilesAreEqualAsync(relativePath));

            Assert.Equal("no disassembler", ex.Message);
            Assert.Contains(logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error
                    && entry.Message.Contains("IL diff failed", StringComparison.Ordinal));
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
        public async Task FilesAreEqualAsync_WhenILMismatchAndSemanticAnalysisDisabled_DoesNotAnalyze()
        {
            // When ShouldIncludeAssemblySemanticChangesInReport is false, semantic analysis
            // should be skipped even on ILMismatch.
            // ShouldIncludeAssemblySemanticChangesInReport が false の場合、ILMismatch でも
            // セマンティック分析をスキップすべき。
            const string relativePath = "no-semantic.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffResult = (AreEqual: false, DisassemblerLabel: "test-tool")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.ShouldIncludeAssemblySemanticChangesInReport = false);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.ILMismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            Assert.False(resultLists.FileRelativePathToAssemblySemanticChanges.ContainsKey(relativePath));
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
        public async Task PrecomputeAsync_NullArgument_ThrowsArgumentNullException()
        {
            // PrecomputeAsync should validate its filesAbsolutePath argument.
            // PrecomputeAsync は filesAbsolutePath 引数をバリデーションすべき。
            var ilOutputService = new FakeILOutputService();
            var fileComparisonService = new FakeFileComparisonService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => service.PrecomputeAsync(null!, maxParallel: 1));
        }

        private static FileDiffService CreateService(
            FakeFileComparisonService fileComparisonService,
            FakeILOutputService ilOutputService,
            FileDiffResultLists resultLists,
            TestLogger logger,
            bool optimizeForNetworkShares = false,
            Action<ConfigSettingsBuilder>? configure = null)
        {
            var builder = new ConfigSettingsBuilder
            {
                TextFileExtensions = new List<string> { ".txt" },
                IgnoredExtensions = new List<string>(),
                ShouldOutputILText = false,
                EnableILCache = false,
                OptimizeForNetworkShares = optimizeForNetworkShares,
                TextDiffParallelThresholdKilobytes = ConfigSettings.DefaultTextDiffParallelThresholdKilobytes,
                TextDiffChunkSizeKilobytes = ConfigSettings.DefaultTextDiffChunkSizeKilobytes,
                TextDiffParallelMemoryLimitMegabytes = 0
            };
            configure?.Invoke(builder);
            var config = builder.Build();

            var executionContext = new DiffExecutionContext(
                "/virtual/old",
                "/virtual/new",
                "/virtual/report",
                optimizeForNetworkShares: optimizeForNetworkShares,
                detectedNetworkOld: false,
                detectedNetworkNew: false);
            return new FileDiffService(config, ilOutputService, executionContext, resultLists, logger, fileComparisonService);
        }

        /// <summary>
        /// Fake file comparison service that returns preconfigured results without touching real files.
        /// 実ファイルにアクセスせず事前設定された結果を返すフェイク比較サービス。
        /// </summary>
        private sealed class FakeFileComparisonService : IFileComparisonService
        {
            private readonly Dictionary<string, byte[]> _fileContentsByPath = new(StringComparer.OrdinalIgnoreCase);

            public bool HashResult { get; set; }

            public Exception HashException { get; set; }

            public bool TextDiffResult { get; set; }

            public Exception TextDiffException { get; set; }

            public Exception ReadChunkException { get; set; }

            public DotNetExecutableDetectionResult DotNetDetectionResult { get; set; } =
                new(DotNetExecutableDetectionStatus.NotDotNetExecutable);

            public List<(string File1, string File2)> HashCalls { get; } = new();

            public List<string> DotNetDetectionCalls { get; } = new();

            public List<(string File1, string File2)> TextDiffCalls { get; } = new();

            public List<(string Path, long Offset, int Length)> ReadChunkCalls { get; } = new();

            public void SetFileContent(string path, string content)
                => _fileContentsByPath[path] = System.Text.Encoding.UTF8.GetBytes(content);

            public Task<bool> DiffFilesByHashAsync(string file1AbsolutePath, string file2AbsolutePath)
            {
                HashCalls.Add((file1AbsolutePath, file2AbsolutePath));
                if (HashException != null)
                {
                    throw HashException;
                }
                return Task.FromResult(HashResult);
            }

            public Task<(bool AreEqual, string? Hash1Hex, string? Hash2Hex)> DiffFilesByHashWithHexAsync(
                string file1AbsolutePath, string file2AbsolutePath)
            {
                HashCalls.Add((file1AbsolutePath, file2AbsolutePath));
                if (HashException != null)
                {
                    throw HashException;
                }
                string? hash1 = HashResult ? "a".PadRight(64, '0') : "a".PadRight(64, '0');
                string? hash2 = HashResult ? "a".PadRight(64, '0') : "b".PadRight(64, '0');
                return Task.FromResult((HashResult, hash1, hash2));
            }

            public Task<bool> DiffTextFilesAsync(string file1AbsolutePath, string file2AbsolutePath)
            {
                TextDiffCalls.Add((file1AbsolutePath, file2AbsolutePath));
                if (TextDiffException != null)
                {
                    throw TextDiffException;
                }
                return Task.FromResult(TextDiffResult);
            }

            public DotNetExecutableDetectionResult DetectDotNetExecutable(string fileAbsolutePath)
            {
                DotNetDetectionCalls.Add(fileAbsolutePath);
                return DotNetDetectionResult;
            }

            public bool FileExists(string fileAbsolutePath)
                => _fileContentsByPath.ContainsKey(fileAbsolutePath);

            public long GetFileLength(string fileAbsolutePath)
            {
                if (_fileContentsByPath.TryGetValue(fileAbsolutePath, out var content))
                {
                    return content.LongLength;
                }

                throw new FileNotFoundException($"File not found: {fileAbsolutePath}", fileAbsolutePath);
            }

            public Task<int> ReadChunkAsync(string fileAbsolutePath, long offset, Memory<byte> buffer, CancellationToken cancellationToken)
            {
                ReadChunkCalls.Add((fileAbsolutePath, offset, buffer.Length));
                if (ReadChunkException != null)
                {
                    throw ReadChunkException;
                }
                if (!_fileContentsByPath.TryGetValue(fileAbsolutePath, out var content))
                {
                    throw new FileNotFoundException($"File not found: {fileAbsolutePath}", fileAbsolutePath);
                }

                int start = checked((int)offset);
                if (start >= content.Length)
                {
                    return Task.FromResult(0);
                }

                int count = Math.Min(buffer.Length, content.Length - start);
                content.AsMemory(start, count).CopyTo(buffer);
                return Task.FromResult(count);
            }
        }

        /// <summary>
        /// Fake IL output service that records calls and returns preconfigured results.
        /// 呼び出しを記録し事前設定された結果を返すフェイク IL 出力サービス。
        /// </summary>
        private sealed class FakeILOutputService : IILOutputService
        {
            public (bool AreEqual, string? DisassemblerLabel) DiffResult { get; set; }

            public Exception DiffException { get; set; }

            public List<DiffCall> DiffCalls { get; } = new();

            public int PrecomputeCallCount { get; private set; }

            public List<(string Path, string Hash)> PreSeedCalls { get; } = new();

            public Task PrecomputeAsync(IEnumerable<string> filesAbsolutePaths, int maxParallel, CancellationToken cancellationToken = default)
            {
                PrecomputeCallCount++;
                return Task.CompletedTask;
            }

            public void PreSeedFileHash(string fileAbsolutePath, string sha256Hex)
            {
                PreSeedCalls.Add((fileAbsolutePath, sha256Hex));
            }

            public Task<(bool AreEqual, string? DisassemblerLabel)> DiffDotNetAssembliesAsync(string fileRelativePath, string oldFolderAbsolutePath, string newFolderAbsolutePath, bool shouldOutputIlText, CancellationToken cancellationToken = default)
            {
                DiffCalls.Add(new DiffCall(fileRelativePath, oldFolderAbsolutePath, newFolderAbsolutePath, shouldOutputIlText));
                if (DiffException != null)
                {
                    throw DiffException;
                }
                return Task.FromResult(DiffResult);
            }
        }

        private sealed record DiffCall(string FileRelativePath, string OldFolderAbsolutePath, string NewFolderAbsolutePath, bool ShouldOutputIlText);
    }
}
