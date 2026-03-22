using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.EdgeCases
{
    /// <summary>
    /// Race condition stress tests under concurrent folder diff execution.
    /// Verifies deterministic classification when many files are compared in parallel
    /// with varying latencies and occasional transient failures.
    /// 並列フォルダ差分実行での競合状態ストレステスト。
    /// 様々な遅延と一時的な障害を伴う多数のファイル並列比較で
    /// 決定論的な分類が行われることを確認する。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class FolderDiffConcurrencyStressTests
    {
        [Fact]
        public async Task ExecuteFolderDiffAsync_HighParallelism_500Files_DeterministicClassification()
        {
            // 500 files compared with high parallelism should produce deterministic results
            // 高並列度で 500 ファイルを比較した結果が決定論的であることを確認
            const string oldDir = "/virtual/old-stress";
            const string newDir = "/virtual/new-stress";
            const string reportDir = "/virtual/report-stress";
            const int fileCount = 500;

            var fileSystem = new FakeFileSystemService();
            var oldFiles = new List<string>();
            var newFiles = new List<string>();
            var equalityMap = new Dictionary<string, bool>(StringComparer.Ordinal);

            for (int i = 0; i < fileCount; i++)
            {
                var relativePath = $"file-{i:D4}.txt";
                oldFiles.Add(Path.Combine(oldDir, relativePath));
                newFiles.Add(Path.Combine(newDir, relativePath));
                equalityMap[relativePath] = i % 3 != 0; // Every 3rd file is modified
            }

            fileSystem.SetFiles(oldDir, oldFiles.ToArray());
            fileSystem.SetFiles(newDir, newFiles.ToArray());

            var fileDiffService = new FakeFileDiffService(equalityMap);
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 8), progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService, resultLists, logger, fileSystem);

            await service.ExecuteFolderDiffAsync();

            int expectedModified = Enumerable.Range(0, fileCount).Count(i => i % 3 == 0);
            int expectedUnchanged = fileCount - expectedModified;

            Assert.Equal(expectedUnchanged, resultLists.UnchangedFilesRelativePath.Count);
            Assert.Equal(expectedModified, resultLists.ModifiedFilesRelativePath.Count);
            Assert.Empty(resultLists.RemovedFilesAbsolutePath);
            Assert.Empty(resultLists.AddedFilesAbsolutePath);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WithSimulatedLatency_CompletesCorrectly()
        {
            // Simulated random latency in file comparison should not cause missed or duplicate classifications
            // ファイル比較でのランダム遅延シミュレーションが分類の欠落や重複を引き起こさない
            const string oldDir = "/virtual/old-latency";
            const string newDir = "/virtual/new-latency";
            const string reportDir = "/virtual/report-latency";
            const int fileCount = 50;

            var fileSystem = new FakeFileSystemService();
            var oldFiles = new List<string>();
            var newFiles = new List<string>();
            var equalityMap = new Dictionary<string, bool>(StringComparer.Ordinal);

            for (int i = 0; i < fileCount; i++)
            {
                var relativePath = $"latency-{i:D3}.txt";
                oldFiles.Add(Path.Combine(oldDir, relativePath));
                newFiles.Add(Path.Combine(newDir, relativePath));
                equalityMap[relativePath] = i % 2 == 0;
            }

            fileSystem.SetFiles(oldDir, oldFiles.ToArray());
            fileSystem.SetFiles(newDir, newFiles.ToArray());

            var fileDiffService = new FakeFileDiffService(equalityMap)
            {
                SimulateLatency = true
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 4), progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService, resultLists, logger, fileSystem);

            await service.ExecuteFolderDiffAsync();

            Assert.Equal(fileCount / 2, resultLists.UnchangedFilesRelativePath.Count);
            Assert.Equal(fileCount / 2, resultLists.ModifiedFilesRelativePath.Count);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_MixOfAddedRemovedModifiedUnchanged_HighParallelism()
        {
            // A realistic mix of all four categories under high parallelism
            // 高並列度での全 4 カテゴリの現実的な混合テスト
            const string oldDir = "/virtual/old-mix";
            const string newDir = "/virtual/new-mix";
            const string reportDir = "/virtual/report-mix";

            var fileSystem = new FakeFileSystemService();
            var oldFiles = new List<string>();
            var newFiles = new List<string>();
            var equalityMap = new Dictionary<string, bool>(StringComparer.Ordinal);

            // Shared files: 100 matching, 80 modified
            // Use Path.Combine for sub-paths to ensure consistent path separators on Windows.
            // Windows でのパスセパレータ一貫性のため Path.Combine でサブパスを構成する。
            for (int i = 0; i < 100; i++)
            {
                var rel = Path.Combine("shared", $"match-{i:D3}.txt");
                oldFiles.Add(Path.Combine(oldDir, rel));
                newFiles.Add(Path.Combine(newDir, rel));
                equalityMap[rel] = true;
            }
            for (int i = 0; i < 80; i++)
            {
                var rel = Path.Combine("shared", $"changed-{i:D3}.txt");
                oldFiles.Add(Path.Combine(oldDir, rel));
                newFiles.Add(Path.Combine(newDir, rel));
                equalityMap[rel] = false;
            }

            // 40 removed (old only)
            for (int i = 0; i < 40; i++)
                oldFiles.Add(Path.Combine(oldDir, Path.Combine("removed", $"file-{i:D3}.txt")));

            // 30 added (new only)
            for (int i = 0; i < 30; i++)
                newFiles.Add(Path.Combine(newDir, Path.Combine("added", $"file-{i:D3}.txt")));

            fileSystem.SetFiles(oldDir, oldFiles.ToArray());
            fileSystem.SetFiles(newDir, newFiles.ToArray());

            var fileDiffService = new FakeFileDiffService(equalityMap);
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 8), progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService, resultLists, logger, fileSystem);

            await service.ExecuteFolderDiffAsync();

            Assert.Equal(100, resultLists.UnchangedFilesRelativePath.Count);
            Assert.Equal(80, resultLists.ModifiedFilesRelativePath.Count);
            Assert.Equal(40, resultLists.RemovedFilesAbsolutePath.Count);
            Assert.Equal(30, resultLists.AddedFilesAbsolutePath.Count);
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
            public void SetFiles(string root, params string[] files) => _filesByRoot[root] = files;

            public IEnumerable<string> EnumerateFiles(string root, string pattern, SearchOption option)
            {
                if (!_filesByRoot.TryGetValue(root, out var files)) yield break;
                foreach (var f in files) yield return f;
            }

            public void CreateDirectory(string path) { }
            public DateTime GetLastWriteTimeUtc(string path) => DateTime.UnixEpoch;
        }

        private sealed class FakeFileDiffService : IFileDiffService
        {
            private readonly Dictionary<string, bool> _equalityByRelativePath;
            private int _callIndex;

            public FakeFileDiffService(Dictionary<string, bool> equalityByRelativePath)
                => _equalityByRelativePath = equalityByRelativePath;

            public bool SimulateLatency { get; init; }

            public ConcurrentQueue<string> FilesAreEqualCalls { get; } = new();

            public Task PrecomputeAsync(IEnumerable<string> files, int maxParallel, CancellationToken ct = default)
                => Task.CompletedTask;

            public async Task<bool> FilesAreEqualAsync(string relativePath, int maxParallel = 1, CancellationToken ct = default)
            {
                FilesAreEqualCalls.Enqueue(relativePath);
                if (SimulateLatency)
                {
                    var idx = Interlocked.Increment(ref _callIndex);
                    await Task.Delay(idx % 5); // 0-4ms random-ish latency
                }
                return _equalityByRelativePath[relativePath];
            }
        }

        private sealed class TestLogger : ILoggerService
        {
            public string? LogFileAbsolutePath => null;
            public void Initialize() { }
            public void CleanupOldLogFiles(int max) { }
            public void LogMessage(AppLogLevel level, string msg, bool console, Exception? ex = null) { }
            public void LogMessage(AppLogLevel level, string msg, bool console, ConsoleColor? color, Exception? ex = null) { }
        }
    }
}
