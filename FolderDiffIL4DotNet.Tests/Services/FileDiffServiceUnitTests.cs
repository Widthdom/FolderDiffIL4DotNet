using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
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

        private sealed class TestLogger : ILoggerService
        {
            public string? LogFileAbsolutePath => null;

            public List<LogEntry> Entries { get; } = new();

            public void Initialize()
            {
            }

            public void CleanupOldLogFiles(int maxLogGenerations)
            {
            }

            public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, Exception? exception = null)
                => LogMessage(logLevel, message, shouldOutputMessageToConsole, consoleForegroundColor: null, exception);

            public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, ConsoleColor? consoleForegroundColor, Exception? exception = null)
                => Entries.Add(new LogEntry(logLevel, message, exception));
        }

        private sealed record DiffCall(string FileRelativePath, string OldFolderAbsolutePath, string NewFolderAbsolutePath, bool ShouldOutputIlText);

        private sealed record LogEntry(AppLogLevel LogLevel, string Message, Exception? Exception);
    }
}
