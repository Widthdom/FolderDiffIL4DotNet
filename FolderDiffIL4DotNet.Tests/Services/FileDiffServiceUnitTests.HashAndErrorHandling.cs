using System;
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
    // Hash comparison and error handling tests for FileDiffService.
    // FileDiffService のハッシュ比較およびエラーハンドリングテスト。
    public sealed partial class FileDiffServiceUnitTests
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
            Assert.Equal(Path.Combine("/virtual/old", "sample.txt"), ilOutputService.PreSeedCalls.ElementAt(0).Path);
            Assert.Equal(Path.Combine("/virtual/new", "sample.txt"), ilOutputService.PreSeedCalls.ElementAt(1).Path);
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
            Assert.Equal(Path.Combine("/virtual/new", "nullhash1.dat"), ilOutputService.PreSeedCalls.ElementAt(0).Path);
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
            Assert.Equal(Path.Combine("/virtual/old", "nullhash2.dat"), ilOutputService.PreSeedCalls.ElementAt(0).Path);
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
    }
}
