using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    [Trait("Category", "Unit")]
    public sealed class FolderDiffServiceUnitTests
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
            using var progressReporter = new ProgressReportService();
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
            using var progressReporter = new ProgressReportService();
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
            using var progressReporter = new ProgressReportService();
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
            using var progressReporter = new ProgressReportService();
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
            using var progressReporter = new ProgressReportService();
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
            using var progressReporter = new ProgressReportService();
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

        [Fact]
        public async Task ExecuteFolderDiffAsync_WithHundredsOfFiles_CompletesParallelClassificationDeterministically()
        {
            const string oldDir = "/virtual/old-many";
            const string newDir = "/virtual/new-many";
            const string reportDir = "/virtual/report-many";
            const int matchingCount = 120;
            const int modifiedCount = 120;
            const int removedCount = 60;
            const int addedCount = 60;

            var fileSystem = new FakeFileSystemService();
            var oldFiles = new List<string>();
            var newFiles = new List<string>();
            var equalityByRelativePath = new Dictionary<string, bool>(StringComparer.Ordinal);

            for (int i = 0; i < matchingCount; i++)
            {
                var relativePath = Path.Combine("matching", $"file-{i:D3}.txt");
                oldFiles.Add(Path.Combine(oldDir, relativePath));
                newFiles.Add(Path.Combine(newDir, relativePath));
                equalityByRelativePath[relativePath] = true;
            }

            for (int i = 0; i < modifiedCount; i++)
            {
                var relativePath = Path.Combine("modified", $"file-{i:D3}.txt");
                oldFiles.Add(Path.Combine(oldDir, relativePath));
                newFiles.Add(Path.Combine(newDir, relativePath));
                equalityByRelativePath[relativePath] = false;
            }

            for (int i = 0; i < removedCount; i++)
            {
                oldFiles.Add(Path.Combine(oldDir, "removed", $"file-{i:D3}.txt"));
            }

            for (int i = 0; i < addedCount; i++)
            {
                newFiles.Add(Path.Combine(newDir, "added", $"file-{i:D3}.txt"));
            }

            fileSystem.SetFiles(oldDir, oldFiles.ToArray());
            fileSystem.SetFiles(newDir, newFiles.ToArray());

            var fileDiffService = new FakeFileDiffService(equalityByRelativePath);
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService();
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 4),
                progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            await service.ExecuteFolderDiffAsync();

            Assert.Equal(matchingCount, resultLists.UnchangedFilesRelativePath.Count);
            Assert.Equal(modifiedCount, resultLists.ModifiedFilesRelativePath.Count);
            Assert.Equal(removedCount, resultLists.RemovedFilesAbsolutePath.Count);
            Assert.Equal(addedCount, resultLists.AddedFilesAbsolutePath.Count);
            Assert.All(fileDiffService.FilesAreEqualCalls, call => Assert.Equal(4, call.MaxParallel));
        }

        private static ConfigSettings CreateConfig(int maxParallelism) => new()
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
            OptimizeForNetworkShares = false,
            AutoDetectNetworkShares = false
        };

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

            public Task PrecomputeAsync(IEnumerable<string> filesAbsolutePath, int maxParallel)
            {
                PrecomputeCalls.Enqueue(new PrecomputeCall(filesAbsolutePath.ToArray(), maxParallel));
                return Task.CompletedTask;
            }

            public Task<bool> FilesAreEqualAsync(string fileRelativePath, int maxParallel = 1)
            {
                FilesAreEqualCalls.Enqueue(new FilesAreEqualCall(fileRelativePath, maxParallel));
                if (FilesAreEqualException != null)
                {
                    throw FilesAreEqualException;
                }
                return Task.FromResult(EqualityByRelativePath[fileRelativePath]);
            }
        }

        private sealed class TestLogger : ILoggerService
        {
            public string LogFileAbsolutePath => null;

            public List<LogEntry> Entries { get; } = new();

            public void Initialize()
            {
            }

            public void CleanupOldLogFiles(int maxLogGenerations)
            {
            }

            public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, Exception exception = null)
                => LogMessage(logLevel, message, shouldOutputMessageToConsole, consoleForegroundColor: null, exception);

            public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, ConsoleColor? consoleForegroundColor, Exception exception = null)
                => Entries.Add(new LogEntry(logLevel, message, exception));
        }

        private sealed record PrecomputeCall(IReadOnlyList<string> FilesAbsolutePath, int MaxParallel);

        private sealed record FilesAreEqualCall(string FileRelativePath, int MaxParallel);

        private sealed record LogEntry(AppLogLevel LogLevel, string Message, Exception Exception);
    }
}
