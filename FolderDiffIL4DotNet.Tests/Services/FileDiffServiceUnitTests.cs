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

        [Fact]
        public async Task FilesAreEqualAsync_WhenHash1HexIsNull_DoesNotPreSeedFile1ButPreSeedsFile2()
        {
            // When the hash computation returns null for hash1Hex, PreSeedFileHash should
            // NOT be called for file1 but should still be called for file2 (non-null).
            // hash1Hex が null の場合、file1 の PreSeedFileHash は呼ばれず、file2（非 null）のみ呼ばれるべき。
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                UseHashHexOverride = true,
                Hash1HexOverride = null,
                Hash2HexOverride = "b".PadRight(64, '0'),
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            await service.FilesAreEqualAsync("nullhash1.dat", maxParallel: 1);

            // Only file2 should have been pre-seeded / file2 のみ事前登録されるべき
            Assert.Single(ilOutputService.PreSeedCalls);
            Assert.Equal(Path.Combine("/virtual/new", "nullhash1.dat"), ilOutputService.PreSeedCalls[0].Path);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenHash2HexIsNull_PreSeedsFile1ButDoesNotPreSeedFile2()
        {
            // When the hash computation returns null for hash2Hex, PreSeedFileHash should
            // be called for file1 (non-null) but NOT for file2.
            // hash2Hex が null の場合、file1（非 null）の PreSeedFileHash は呼ばれ、file2 は呼ばれないべき。
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                UseHashHexOverride = true,
                Hash1HexOverride = "a".PadRight(64, '0'),
                Hash2HexOverride = null,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            await service.FilesAreEqualAsync("nullhash2.dat", maxParallel: 1);

            // Only file1 should have been pre-seeded / file1 のみ事前登録されるべき
            Assert.Single(ilOutputService.PreSeedCalls);
            Assert.Equal(Path.Combine("/virtual/old", "nullhash2.dat"), ilOutputService.PreSeedCalls[0].Path);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenBothHashHexAreNull_DoesNotPreSeedEither()
        {
            // When the hash computation returns null for both hash1Hex and hash2Hex,
            // PreSeedFileHash should NOT be called at all.
            // hash1Hex と hash2Hex の両方が null の場合、PreSeedFileHash はいっさい呼ばれないべき。
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                UseHashHexOverride = true,
                Hash1HexOverride = null,
                Hash2HexOverride = null,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            await service.FilesAreEqualAsync("nullboth.dat", maxParallel: 1);

            // Neither file should have been pre-seeded / どちらも事前登録されないべき
            Assert.Empty(ilOutputService.PreSeedCalls);
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
        public async Task FilesAreEqualAsync_WhenNonTextExtensionAndHashDiffers_RecordsSHA256Mismatch()
        {
            // When the file extension is not in TextFileExtensions and hash differs,
            // the result should be SHA256Mismatch (fall-through to the final return false).
            // ファイル拡張子が TextFileExtensions に含まれず、ハッシュが異なる場合、
            // 結果は SHA256Mismatch（最終的な return false へのフォールスルー）になるべき。
            const string relativePath = "binary.dat";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.SHA256Mismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            // No text diff or IL diff should be attempted / テキスト差分も IL 差分も試行されないべき
            Assert.Empty(fileComparisonService.TextDiffCalls);
            Assert.Empty(ilOutputService.DiffCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenILDiffReturnsNotEqual_RecordsILMismatch()
        {
            // When IL comparison returns not-equal, DiffDetail should be ILMismatch and
            // the method should return false.
            // IL 比較が不一致を返した場合、DiffDetail は ILMismatch で、メソッドは false を返すべき。
            const string relativePath = "changed.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffResult = (AreEqual: false, DisassemblerLabel: "test-disasm v2.0")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.ShouldIncludeAssemblySemanticChangesInReport = false);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.ILMismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            // Disassembler label should be recorded / 逆アセンブララベルが記録されるべき
            Assert.Equal("test-disasm v2.0",
                resultLists.FileRelativePathToIlDisassemblerLabelDictionary[relativePath]);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
        {
            // FilesAreEqualAsync should throw OperationCanceledException when
            // the cancellation token is already cancelled.
            // キャンセルトークンが既にキャンセル済みの場合、OperationCanceledException をスローすべき。
            var fileComparisonService = new FakeFileComparisonService { HashResult = true };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => service.FilesAreEqualAsync("cancel.txt", maxParallel: 1, cancellationToken: cts.Token));

            // No hash computation should have been attempted / ハッシュ計算は試行されないべき
            Assert.Empty(fileComparisonService.HashCalls);
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
        public async Task FilesAreEqualAsync_WhenILMatchReturnsTrue_ReturnsTrue()
        {
            // Verify the return value path: when IL comparison returns equal, the method
            // must return true (not fall through to text comparison).
            // IL 比較が一致を返した場合、メソッドは true を返し（テキスト比較にフォールスルーしない）。
            const string relativePath = "il-match-return.dll";
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
            // Must not fall through to text or binary comparison
            // テキストやバイナリ比較にフォールスルーしてはいけない
            Assert.Empty(fileComparisonService.TextDiffCalls);
            Assert.Empty(fileComparisonService.ReadChunkCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenILMismatchReturnsFalse_ReturnsFalse()
        {
            // Verify the return value path: when IL comparison returns not-equal, the method
            // must return false (not fall through to text comparison).
            // IL 比較が不一致を返した場合、メソッドは false を返し（テキスト比較にフォールスルーしない）。
            const string relativePath = "il-mismatch-return.dll";
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
            Assert.Empty(fileComparisonService.TextDiffCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenDotNetDetectionFailure_LogsWarningWithException()
        {
            // Verify the warning log includes the exception from the detection failure.
            // 検出失敗からの例外が警告ログに含まれることを検証。
            const string relativePath = "detect-ex.dll";
            var detectionException = new IOException("bad PE header");
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(
                    DotNetExecutableDetectionStatus.Failed,
                    detectionException)
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            // Warning should contain the detection exception
            // 警告には検出例外が含まれるべき
            var warningEntry = Assert.Single(logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Warning
                    && entry.Message.Contains("Failed to detect", StringComparison.Ordinal));
            Assert.Same(detectionException, warningEntry.Exception);
            // IL diff should not be attempted / IL 差分は試行されないべき
            Assert.Empty(ilOutputService.DiffCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenHashEqual_ReturnsTrueWithoutCheckingDotNetOrText()
        {
            // Verify SHA256 match short-circuits all subsequent checks (DotNet detection, text diff).
            // SHA256 一致で後続のすべてのチェック（DotNet 検出、テキスト差分）が短絡されることを検証。
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = true,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable),
                TextDiffResult = false
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffResult = (AreEqual: false, DisassemblerLabel: "should-not-reach")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var areEqual = await service.FilesAreEqualAsync("shortcircuit.dll", maxParallel: 4);

            Assert.True(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.SHA256Match,
                resultLists.FileRelativePathToDiffDetailDictionary["shortcircuit.dll"]);
            // None of the downstream checks should be called
            // 後段のチェックはいずれも呼ばれないべき
            Assert.Empty(fileComparisonService.DotNetDetectionCalls);
            Assert.Empty(fileComparisonService.TextDiffCalls);
            Assert.Empty(ilOutputService.DiffCalls);
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

        [Fact]
        public async Task PrecomputeAsync_WhenSkipILIsTrue_ReturnsCompletedTask()
        {
            // When SkipIL is true, PrecomputeAsync should return immediately (Task.CompletedTask).
            // SkipIL が true の場合、PrecomputeAsync は即座に戻るべき（Task.CompletedTask）。
            var ilOutputService = new FakeILOutputService();
            var fileComparisonService = new FakeFileComparisonService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.SkipIL = true);

            var task = service.PrecomputeAsync(new[] { "/virtual/old/a.dll", "/virtual/old/b.dll" }, maxParallel: 4);

            // Task should be completed synchronously / タスクは同期的に完了しているべき
            Assert.True(task.IsCompleted);
            await task;
            Assert.Equal(0, ilOutputService.PrecomputeCallCount);
        }

        [Fact]
        public async Task PrecomputeAsync_WhenSkipILIsFalse_PassesFilesAndMaxParallel()
        {
            // When SkipIL is false, PrecomputeAsync delegates to ILOutputService.PrecomputeAsync.
            // SkipIL が false の場合、PrecomputeAsync は ILOutputService.PrecomputeAsync に委譲する。
            var ilOutputService = new FakeILOutputService();
            var fileComparisonService = new FakeFileComparisonService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.SkipIL = false);

            var files = new[] { "/virtual/old/x.dll", "/virtual/old/y.dll", "/virtual/old/z.dll" };
            await service.PrecomputeAsync(files, maxParallel: 3);

            Assert.True(ilOutputService.PrecomputeCallCount > 0);
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

            /// <summary>
            /// When set to true, overrides the Hash1Hex/Hash2Hex return values with <see cref="Hash1HexOverride"/>/<see cref="Hash2HexOverride"/>.
            /// true に設定すると、Hash1Hex/Hash2Hex の戻り値を <see cref="Hash1HexOverride"/>/<see cref="Hash2HexOverride"/> で上書きします。
            /// </summary>
            public bool UseHashHexOverride { get; set; }

            /// <summary>
            /// Custom Hash1Hex value returned when <see cref="UseHashHexOverride"/> is true. Null triggers the null-check path.
            /// <see cref="UseHashHexOverride"/> が true の場合に返すカスタム Hash1Hex 値。null は null チェックパスを起動します。
            /// </summary>
            public string? Hash1HexOverride { get; set; }

            /// <summary>
            /// Custom Hash2Hex value returned when <see cref="UseHashHexOverride"/> is true. Null triggers the null-check path.
            /// <see cref="UseHashHexOverride"/> が true の場合に返すカスタム Hash2Hex 値。null は null チェックパスを起動します。
            /// </summary>
            public string? Hash2HexOverride { get; set; }

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
                string? hash1;
                string? hash2;
                if (UseHashHexOverride)
                {
                    hash1 = Hash1HexOverride;
                    hash2 = Hash2HexOverride;
                }
                else
                {
                    hash1 = HashResult ? "a".PadRight(64, '0') : "a".PadRight(64, '0');
                    hash2 = HashResult ? "a".PadRight(64, '0') : "b".PadRight(64, '0');
                }
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
