using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.EdgeCases
{
    /// <summary>
    /// Tests for symbolic links and circular directory reference handling.
    /// Verifies that the folder diff service handles symlink loops, dangling symlinks,
    /// and cross-device symlinks gracefully.
    /// シンボリックリンクおよび循環ディレクトリ参照のテスト。
    /// フォルダ差分サービスがシンボリックリンクループ、ダングリングシンボリックリンク、
    /// クロスデバイスシンボリックリンクを正常に処理することを確認する。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class SymlinkAndCircularDirectoryTests
    {
        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenSymlinkLoopCausesIOException_LogsAndRethrows()
        {
            // Symlink loop during file enumeration causes IOException ("Too many levels of symbolic links")
            // which should be logged and rethrown
            // ファイル列挙中のシンボリックリンクループは IOException を引き起こし、ログ後に再スローされる
            const string oldDir = "/virtual/old-symloop";
            const string newDir = "/virtual/new-symloop";
            const string reportDir = "/virtual/report-symloop";

            var fileSystem = new FakeFileSystemService
            {
                EnumerateFilesExceptionRoot = oldDir,
                EnumerateFilesException = new IOException("Too many levels of symbolic links")
            };
            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>());
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(1), progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService, resultLists, logger, fileSystem);

            var ex = await Assert.ThrowsAsync<IOException>(() => service.ExecuteFolderDiffAsync());

            Assert.Contains("Too many levels of symbolic links", ex.Message);
            Assert.Contains(logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error && entry.Exception is IOException);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenSymlinkLoopOnNewSide_LogsAndRethrows()
        {
            // Same test but the symlink loop is on the new-side folder
            // 同じテストだが、シンボリックリンクループが new 側フォルダで発生
            const string oldDir = "/virtual/old-symloop-new";
            const string newDir = "/virtual/new-symloop-new";
            const string reportDir = "/virtual/report-symloop-new";

            var fileSystem = new FakeFileSystemService
            {
                EnumerateFilesExceptionRoot = newDir,
                EnumerateFilesException = new IOException("Too many levels of symbolic links")
            };
            fileSystem.SetFiles(oldDir, Path.Combine(oldDir, "file.txt"));
            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>());
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(1), progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService, resultLists, logger, fileSystem);

            var ex = await Assert.ThrowsAsync<IOException>(() => service.ExecuteFolderDiffAsync());

            Assert.Contains("Too many levels of symbolic links", ex.Message);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenDanglingSymlinkCausesFileNotFound_ClassifiesAsRemoved()
        {
            // A dangling symlink in the new-side causes FileNotFoundException during comparison,
            // which should be handled gracefully by classifying the file as Removed
            // new 側のダングリングシンボリックリンクにより比較時に FileNotFoundException が発生し、
            // ファイルを Removed として分類することで正常に処理される
            const string oldDir = "/virtual/old-dangling";
            const string newDir = "/virtual/new-dangling";
            const string reportDir = "/virtual/report-dangling";

            var fileSystem = new FakeFileSystemService();
            fileSystem.SetFiles(oldDir, Path.Combine(oldDir, "dangling-target.txt"));
            fileSystem.SetFiles(newDir, Path.Combine(newDir, "dangling-target.txt"));

            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>())
            {
                FilesAreEqualException = new FileNotFoundException("Symlink target not found: dangling-target.txt")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(1), progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService, resultLists, logger, fileSystem);

            await service.ExecuteFolderDiffAsync();

            Assert.Contains(Path.Combine(oldDir, "dangling-target.txt"), resultLists.RemovedFilesAbsolutePath);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenAccessDeniedOnSymlink_LogsAndRethrows()
        {
            // UnauthorizedAccessException on a symlink target should be logged and rethrown
            // シンボリックリンクターゲットへの UnauthorizedAccessException はログ後に再スローされる
            const string oldDir = "/virtual/old-denied-sym";
            const string newDir = "/virtual/new-denied-sym";
            const string reportDir = "/virtual/report-denied-sym";

            var fileSystem = new FakeFileSystemService
            {
                EnumerateFilesExceptionRoot = oldDir,
                EnumerateFilesException = new UnauthorizedAccessException("Permission denied on symlink target")
            };
            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>());
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(1), progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService, resultLists, logger, fileSystem);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ExecuteFolderDiffAsync());
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_ParallelMode_WhenMultipleDanglingSymlinks_AllClassifiedAsRemoved()
        {
            // Multiple dangling symlinks in parallel mode should all be classified as Removed
            // 並列モードでの複数ダングリングシンボリックリンクはすべて Removed に分類される
            const string oldDir = "/virtual/old-multi-dangling";
            const string newDir = "/virtual/new-multi-dangling";
            const string reportDir = "/virtual/report-multi-dangling";

            var fileSystem = new FakeFileSystemService();
            fileSystem.SetFiles(oldDir,
                Path.Combine(oldDir, "link1.txt"),
                Path.Combine(oldDir, "link2.txt"),
                Path.Combine(oldDir, "link3.txt"));
            fileSystem.SetFiles(newDir,
                Path.Combine(newDir, "link1.txt"),
                Path.Combine(newDir, "link2.txt"),
                Path.Combine(newDir, "link3.txt"));

            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>())
            {
                FilesAreEqualException = new FileNotFoundException("symlink target removed")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 4), progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService, resultLists, logger, fileSystem);

            await service.ExecuteFolderDiffAsync();

            Assert.Equal(3, resultLists.RemovedFilesAbsolutePath.Count);
        }

        private static ConfigSettings CreateConfig(int maxParallelism) => new ConfigSettingsBuilder
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
        }.Build();

        private static DiffExecutionContext CreateExecutionContext(string oldDir, string newDir, string reportDir)
            => new(oldDir, newDir, reportDir, optimizeForNetworkShares: false, detectedNetworkOld: false, detectedNetworkNew: false);

        private sealed class FakeFileSystemService : IFileSystemService
        {
            private readonly Dictionary<string, IReadOnlyList<string>> _filesByRoot = new(StringComparer.OrdinalIgnoreCase);
            public string EnumerateFilesExceptionRoot { get; init; }
            public Exception EnumerateFilesException { get; init; }
            public List<string> CreatedDirectories { get; } = new();

            public void SetFiles(string root, params string[] files)
                => _filesByRoot[root] = files;

            public IEnumerable<string> EnumerateFiles(string root, string pattern, SearchOption option)
            {
                if (string.Equals(root, EnumerateFilesExceptionRoot, StringComparison.OrdinalIgnoreCase) && EnumerateFilesException != null)
                    throw EnumerateFilesException;
                if (!_filesByRoot.TryGetValue(root, out var files)) yield break;
                foreach (var f in files) yield return f;
            }

            public void CreateDirectory(string path) => CreatedDirectories.Add(path);

            public DateTime GetLastWriteTimeUtc(string path) => DateTime.UnixEpoch;
        }

        private sealed class FakeFileDiffService : IFileDiffService
        {
            public FakeFileDiffService(Dictionary<string, bool> equalityByRelativePath)
                => EqualityByRelativePath = equalityByRelativePath;

            public Dictionary<string, bool> EqualityByRelativePath { get; }
            public Exception FilesAreEqualException { get; init; }

            public Task PrecomputeAsync(IEnumerable<string> files, int maxParallel, System.Threading.CancellationToken ct = default)
                => Task.CompletedTask;

            public Task<bool> FilesAreEqualAsync(string relativePath, int maxParallel = 1, System.Threading.CancellationToken ct = default)
            {
                if (FilesAreEqualException != null) throw FilesAreEqualException;
                return Task.FromResult(EqualityByRelativePath[relativePath]);
            }
        }

        private sealed class TestLogger : ILoggerService
        {
            public string? LogFileAbsolutePath => null;
            public List<LogEntry> Entries { get; } = new();
            public void Initialize() { }
            public void CleanupOldLogFiles(int max) { }
            public void LogMessage(AppLogLevel level, string msg, bool console, Exception? ex = null)
                => LogMessage(level, msg, console, null, ex);
            public void LogMessage(AppLogLevel level, string msg, bool console, ConsoleColor? color, Exception? ex = null)
                => Entries.Add(new LogEntry(level, msg, ex));
        }

        private sealed record LogEntry(AppLogLevel LogLevel, string Message, Exception? Exception);
    }
}
