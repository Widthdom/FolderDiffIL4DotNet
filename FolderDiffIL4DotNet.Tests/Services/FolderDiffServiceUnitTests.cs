using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    [Trait("Category", "Unit")]
    public sealed partial class FolderDiffServiceUnitTests
    {
        [Fact]
        public async Task ExecuteFolderDiffAsync_UsesFileDiffResultsToClassifyFilesWithoutTouchingRealDisk()
        {
            const string oldDir = "/virtual/old";
            const string newDir = "/virtual/new";
            const string reportDir = "/virtual/report";

            var fileSystem = new FakeFileSystemService();
            fileSystem.SetFiles(oldDir,
                Path.Combine(oldDir, "same.txt"),
                Path.Combine(oldDir, "modified.txt"),
                Path.Combine(oldDir, "removed.txt"),
                Path.Combine(oldDir, "ignored.pdb"));
            fileSystem.SetFiles(newDir,
                Path.Combine(newDir, "same.txt"),
                Path.Combine(newDir, "modified.txt"),
                Path.Combine(newDir, "added.txt"),
                Path.Combine(newDir, "ignored.pdb"));

            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["same.txt"] = true,
                ["modified.txt"] = false
            });
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var executionContext = CreateExecutionContext(oldDir, newDir, reportDir);
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 1),
                progressReporter,
                executionContext,
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            await service.ExecuteFolderDiffAsync();

            Assert.Contains("same.txt", resultLists.UnchangedFilesRelativePath);
            Assert.Contains("modified.txt", resultLists.ModifiedFilesRelativePath);
            Assert.Contains(Path.Combine(oldDir, "removed.txt"), resultLists.RemovedFilesAbsolutePath);
            Assert.Contains(Path.Combine(newDir, "added.txt"), resultLists.AddedFilesAbsolutePath);
            Assert.Equal(
                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New,
                resultLists.IgnoredFilesRelativePathToLocation["ignored.pdb"]);

            var precomputeCall = Assert.Single(fileDiffService.PrecomputeCalls);
            Assert.Equal(6, precomputeCall.FilesAbsolutePath.Count);
            Assert.Contains(Path.Combine(oldDir, "same.txt"), precomputeCall.FilesAbsolutePath);
            Assert.Contains(Path.Combine(newDir, "added.txt"), precomputeCall.FilesAbsolutePath);
            Assert.Equal(1, precomputeCall.MaxParallel);

            Assert.Equal(executionContext.IlOutputFolderAbsolutePath, Assert.Single(fileSystem.CreatedDirectories));
        }

        // Verify that an IOException (e.g. symlink loop ELOOP) during file enumeration is logged and rethrown
        // ファイル列挙時の IOException（例: シンボリックリンクループ ELOOP）がエラーログとともに再スローされることを確認する
        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenEnumeratingFilesThrowsIOExceptionDueToSymlinkLoop_LogsAndRethrows()
        {
            const string oldDir = "/virtual/old-symlink-loop";
            const string newDir = "/virtual/new-symlink-loop";
            const string reportDir = "/virtual/report-symlink-loop";

            var fileSystem = new FakeFileSystemService
            {
                EnumerateFilesExceptionRoot = oldDir,
                EnumerateFilesException = new IOException("Too many levels of symbolic links")
            };
            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(StringComparer.Ordinal));
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 1),
                progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            var exception = await Assert.ThrowsAsync<IOException>(() => service.ExecuteFolderDiffAsync());

            Assert.Equal("Too many levels of symbolic links", exception.Message);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error
                    && entry.Exception is IOException
                    && entry.Message.Contains($"An error occurred while diffing '{oldDir}' and '{newDir}'.", StringComparison.Ordinal));
        }

        // When a new-side file is deleted between enumeration and comparison, FileNotFoundException
        // is swallowed and the file is classified as Removed
        // new 側のファイルが列挙後・比較前に削除された場合、FileNotFoundException を握りつぶして Removed に分類する
        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenNewFileDeletedBeforeComparison_ClassifiesAsRemovedWithWarning()
        {
            const string oldDir = "/virtual/old-deleted-new";
            const string newDir = "/virtual/new-deleted-new";
            const string reportDir = "/virtual/report-deleted-new";

            var fileSystem = new FakeFileSystemService();
            fileSystem.SetFiles(oldDir,
                Path.Combine(oldDir, "ghost.txt"));
            fileSystem.SetFiles(newDir,
                Path.Combine(newDir, "ghost.txt"));

            // FilesAreEqualAsync throws FileNotFoundException to simulate file deletion before comparison
            // FilesAreEqualAsync が FileNotFoundException をスローして「比較前に削除」を模擬する
            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(StringComparer.Ordinal))
            {
                FilesAreEqualException = new FileNotFoundException("File not found: ghost.txt")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 1),
                progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            await service.ExecuteFolderDiffAsync();

            Assert.Contains(Path.Combine(oldDir, "ghost.txt"), resultLists.RemovedFilesAbsolutePath);
            Assert.Empty(resultLists.ModifiedFilesRelativePath);
            Assert.Empty(resultLists.UnchangedFilesRelativePath);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Warning
                    && entry.Message.Contains("ghost.txt", StringComparison.Ordinal));
        }

        // Same as above but in parallel mode: file deleted between enumeration and comparison is classified as Removed
        // 並列モードでも列挙後・比較前に削除されたファイルは Removed に分類される
        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenNewFileDeletedBeforeComparison_ParallelMode_ClassifiesAsRemovedWithWarning()
        {
            const string oldDir = "/virtual/old-deleted-parallel";
            const string newDir = "/virtual/new-deleted-parallel";
            const string reportDir = "/virtual/report-deleted-parallel";

            var fileSystem = new FakeFileSystemService();
            fileSystem.SetFiles(oldDir,
                Path.Combine(oldDir, "gone.txt"));
            fileSystem.SetFiles(newDir,
                Path.Combine(newDir, "gone.txt"));

            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(StringComparer.Ordinal))
            {
                FilesAreEqualException = new FileNotFoundException("File not found: gone.txt")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 2),
                progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            await service.ExecuteFolderDiffAsync();

            Assert.Contains(Path.Combine(oldDir, "gone.txt"), resultLists.RemovedFilesAbsolutePath);
            Assert.Empty(resultLists.ModifiedFilesRelativePath);
            Assert.Empty(resultLists.UnchangedFilesRelativePath);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenEnumeratingFilesThrowsUnauthorizedAccessException_LogsAndRethrows()
        {
            const string oldDir = "/virtual/old-denied";
            const string newDir = "/virtual/new-denied";
            const string reportDir = "/virtual/report-denied";

            var fileSystem = new FakeFileSystemService
            {
                EnumerateFilesExceptionRoot = oldDir,
                EnumerateFilesException = new UnauthorizedAccessException("access denied")
            };
            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(StringComparer.Ordinal));
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 1),
                progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ExecuteFolderDiffAsync());

            Assert.Equal("access denied", exception.Message);
            Assert.Empty(fileDiffService.PrecomputeCalls);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error
                    && entry.Message.Contains($"An error occurred while diffing '{oldDir}' and '{newDir}'.", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_ConsumesStreamingFileEnumerationAndStillFiltersIgnoredFiles()
        {
            const string oldDir = "/virtual/old-stream";
            const string newDir = "/virtual/new-stream";
            const string reportDir = "/virtual/report-stream";

            var fileSystem = new FakeFileSystemService();
            fileSystem.SetFiles(oldDir,
                Path.Combine(oldDir, "same.txt"),
                Path.Combine(oldDir, "ignored.pdb"));
            fileSystem.SetFiles(newDir,
                Path.Combine(newDir, "same.txt"),
                Path.Combine(newDir, "ignored.pdb"));

            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["same.txt"] = true
            });
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 1),
                progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            await service.ExecuteFolderDiffAsync();

            Assert.Contains(oldDir, fileSystem.EnumerateFilesCalls);
            Assert.Contains(newDir, fileSystem.EnumerateFilesCalls);
            Assert.Equal(4, fileSystem.YieldedFileCount);
            Assert.Single(resultLists.UnchangedFilesRelativePath, "same.txt");
            Assert.Equal(
                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New,
                resultLists.IgnoredFilesRelativePathToLocation["ignored.pdb"]);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenPreparingIlOutputDirectoryThrowsIOException_LogsAndRethrows()
        {
            const string oldDir = "/virtual/old-io";
            const string newDir = "/virtual/new-io";
            const string reportDir = "/virtual/report-io";

            var executionContext = CreateExecutionContext(oldDir, newDir, reportDir);
            var fileSystem = new FakeFileSystemService
            {
                CreateDirectoryExceptionPath = executionContext.IlOutputFolderAbsolutePath,
                CreateDirectoryException = new IOException("disk full")
            };
            fileSystem.SetFiles(oldDir, Path.Combine(oldDir, "sample.txt"));
            fileSystem.SetFiles(newDir, Path.Combine(newDir, "sample.txt"));

            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(StringComparer.Ordinal))
            {
                EqualityByRelativePath =
                {
                    ["sample.txt"] = true
                }
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 1),
                progressReporter,
                executionContext,
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            var exception = await Assert.ThrowsAsync<IOException>(() => service.ExecuteFolderDiffAsync());

            Assert.Equal("disk full", exception.Message);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error
                    && entry.Message.Contains($"An error occurred while diffing '{oldDir}' and '{newDir}'.", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenCreatingIlOutputDirectoryThrowsDirectoryNotFoundException_LogsAndRethrows()
        {
            const string oldDir = "/virtual/old-missing-parent";
            const string newDir = "/virtual/new-missing-parent";
            const string reportDir = "/virtual/report-missing-parent";

            var executionContext = CreateExecutionContext(oldDir, newDir, reportDir);
            var fileSystem = new FakeFileSystemService
            {
                CreateDirectoryExceptionPath = executionContext.IlOutputFolderAbsolutePath,
                CreateDirectoryException = new DirectoryNotFoundException("parent directory missing")
            };
            fileSystem.SetFiles(oldDir, Path.Combine(oldDir, "sample.txt"));
            fileSystem.SetFiles(newDir, Path.Combine(newDir, "sample.txt"));

            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(StringComparer.Ordinal))
            {
                EqualityByRelativePath =
                {
                    ["sample.txt"] = true
                }
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 1),
                progressReporter,
                executionContext,
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.ExecuteFolderDiffAsync());

            Assert.Equal("parent directory missing", exception.Message);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error
                    && entry.Exception is DirectoryNotFoundException
                    && entry.Message.Contains($"An error occurred while diffing '{oldDir}' and '{newDir}'.", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenFileDiffThrowsUnexpectedException_LogsUnexpectedErrorAndRethrows()
        {
            const string oldDir = "/virtual/old-unexpected";
            const string newDir = "/virtual/new-unexpected";
            const string reportDir = "/virtual/report-unexpected";

            var fileSystem = new FakeFileSystemService();
            fileSystem.SetFiles(oldDir, Path.Combine(oldDir, "sample.txt"));
            fileSystem.SetFiles(newDir, Path.Combine(newDir, "sample.txt"));

            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(StringComparer.Ordinal))
            {
                FilesAreEqualException = new FormatException("unexpected compare failure")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 1),
                progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            var exception = await Assert.ThrowsAsync<FormatException>(() => service.ExecuteFolderDiffAsync());

            Assert.Equal("unexpected compare failure", exception.Message);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error
                    && entry.Exception is FormatException
                    && entry.Message.Contains($"An unexpected error occurred while diffing '{oldDir}' and '{newDir}'.", StringComparison.Ordinal));
        }

        private static ConfigSettings CreateConfig(int maxParallelism, int ilPrecomputeBatchSize = ConfigSettings.DefaultILPrecomputeBatchSize) => new ConfigSettingsBuilder()
        {
            IgnoredExtensions = new List<string> { ".pdb" },
            TextFileExtensions = new List<string> { ".txt" },
            ShouldIncludeUnchangedFiles = true,
            ShouldIncludeIgnoredFiles = true,
            ShouldOutputILText = false,
            ShouldIgnoreILLinesContainingConfiguredStrings = false,
            ILIgnoreLineContainingStrings = new List<string>(),
            ShouldOutputFileTimestamps = false,
            ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = true,
            MaxParallelism = maxParallelism,
            EnableILCache = false,
            ILPrecomputeBatchSize = ilPrecomputeBatchSize,
            OptimizeForNetworkShares = false,
            AutoDetectNetworkShares = false
        }.Build();

        private static DiffExecutionContext CreateExecutionContext(string oldDir, string newDir, string reportDir)
            => new(oldDir, newDir, reportDir, optimizeForNetworkShares: false, detectedNetworkOld: false, detectedNetworkNew: false);

        private sealed class FakeFileSystemService : IFileSystemService
        {
            private readonly Dictionary<string, IReadOnlyList<string>> _filesByRoot = new(StringComparer.OrdinalIgnoreCase);

            public string EnumerateFilesExceptionRoot { get; init; }

            public Exception EnumerateFilesException { get; init; }

            public string CreateDirectoryExceptionPath { get; init; }

            public Exception CreateDirectoryException { get; init; }

            public List<string> CreatedDirectories { get; } = new();

            public List<string> EnumerateFilesCalls { get; } = new();

            public Dictionary<string, DateTime> LastWriteTimesUtc { get; } = new(StringComparer.OrdinalIgnoreCase);

            public int YieldedFileCount { get; private set; }

            public void SetFiles(string rootFolderAbsolutePath, params string[] files)
                => _filesByRoot[rootFolderAbsolutePath] = files;

            public IEnumerable<string> EnumerateFiles(string rootFolderAbsolutePath, string searchPattern, SearchOption searchOption)
            {
                if (string.Equals(rootFolderAbsolutePath, EnumerateFilesExceptionRoot, StringComparison.OrdinalIgnoreCase) && EnumerateFilesException != null)
                {
                    throw EnumerateFilesException;
                }

                EnumerateFilesCalls.Add(rootFolderAbsolutePath);
                return EnumerateFilesIterator(rootFolderAbsolutePath);
            }

            private IEnumerable<string> EnumerateFilesIterator(string rootFolderAbsolutePath)
            {
                if (!_filesByRoot.TryGetValue(rootFolderAbsolutePath, out var files))
                {
                    yield break;
                }

                foreach (var file in files)
                {
                    YieldedFileCount++;
                    yield return file;
                }
            }

            public void CreateDirectory(string path)
            {
                if (string.Equals(path, CreateDirectoryExceptionPath, StringComparison.OrdinalIgnoreCase) && CreateDirectoryException != null)
                {
                    throw CreateDirectoryException;
                }

                CreatedDirectories.Add(path);
            }

            public DateTime GetLastWriteTimeUtc(string path)
                => LastWriteTimesUtc.TryGetValue(path, out var timestamp)
                    ? timestamp
                    : DateTime.UnixEpoch;
        }

        private sealed class FakeFileDiffService : IFileDiffService
        {
            public FakeFileDiffService(Dictionary<string, bool> equalityByRelativePath)
            {
                EqualityByRelativePath = equalityByRelativePath;
            }

            public Dictionary<string, bool> EqualityByRelativePath { get; }

            public Exception FilesAreEqualException { get; init; }

            public ConcurrentQueue<PrecomputeCall> PrecomputeCalls { get; } = new();

            public ConcurrentQueue<FilesAreEqualCall> FilesAreEqualCalls { get; } = new();

            public Task PrecomputeAsync(IEnumerable<string> filesAbsolutePath, int maxParallel, CancellationToken cancellationToken = default)
            {
                PrecomputeCalls.Enqueue(new PrecomputeCall(filesAbsolutePath.ToArray(), maxParallel));
                return Task.CompletedTask;
            }

            public Task<bool> FilesAreEqualAsync(string fileRelativePath, int maxParallel = 1, CancellationToken cancellationToken = default)
            {
                FilesAreEqualCalls.Enqueue(new FilesAreEqualCall(fileRelativePath, maxParallel));
                if (FilesAreEqualException != null)
                {
                    throw FilesAreEqualException;
                }
                return Task.FromResult(EqualityByRelativePath[fileRelativePath]);
            }
        }

        private sealed record PrecomputeCall(IReadOnlyList<string> FilesAbsolutePath, int MaxParallel);

        private sealed record FilesAreEqualCall(string FileRelativePath, int MaxParallel);
    }
}
