/// <summary>
/// Partial class containing batching, deduplication, and edge-case tests for FolderDiffService.
/// FolderDiffService のバッチ処理、重複排除、エッジケーステストを含むパーシャルクラス。
/// </summary>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed partial class FolderDiffServiceUnitTests
    {
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
            var equalityByRelativePath = new Dictionary<string, bool>(System.StringComparer.Ordinal);

            for (int i = 0; i < matchingCount; i++)
            {
                var relativePath = System.IO.Path.Combine("matching", $"file-{i:D3}.txt");
                oldFiles.Add(System.IO.Path.Combine(oldDir, relativePath));
                newFiles.Add(System.IO.Path.Combine(newDir, relativePath));
                equalityByRelativePath[relativePath] = true;
            }

            for (int i = 0; i < modifiedCount; i++)
            {
                var relativePath = System.IO.Path.Combine("modified", $"file-{i:D3}.txt");
                oldFiles.Add(System.IO.Path.Combine(oldDir, relativePath));
                newFiles.Add(System.IO.Path.Combine(newDir, relativePath));
                equalityByRelativePath[relativePath] = false;
            }

            for (int i = 0; i < removedCount; i++)
            {
                oldFiles.Add(System.IO.Path.Combine(oldDir, "removed", $"file-{i:D3}.txt"));
            }

            for (int i = 0; i < addedCount; i++)
            {
                newFiles.Add(System.IO.Path.Combine(newDir, "added", $"file-{i:D3}.txt"));
            }

            fileSystem.SetFiles(oldDir, oldFiles.ToArray());
            fileSystem.SetFiles(newDir, newFiles.ToArray());

            var fileDiffService = new FakeFileDiffService(equalityByRelativePath);
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
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

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenPrecomputeBatchSizeIsSmall_SplitsDistinctTargetsAcrossMultipleCalls()
        {
            const string oldDir = "/virtual/old-batched";
            const string newDir = "/virtual/new-batched";
            const string reportDir = "/virtual/report-batched";

            var fileSystem = new FakeFileSystemService();
            fileSystem.SetFiles(
                oldDir,
                System.IO.Path.Combine(oldDir, "shared-a.txt"),
                System.IO.Path.Combine(oldDir, "shared-b.txt"),
                System.IO.Path.Combine(oldDir, "old-only.txt"));
            fileSystem.SetFiles(
                newDir,
                System.IO.Path.Combine(newDir, "shared-a.txt"),
                System.IO.Path.Combine(newDir, "shared-b.txt"),
                System.IO.Path.Combine(newDir, "new-only.txt"));

            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(System.StringComparer.Ordinal)
            {
                ["shared-a.txt"] = true,
                ["shared-b.txt"] = true
            });
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 1, ilPrecomputeBatchSize: 2),
                progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            await service.ExecuteFolderDiffAsync();

            Assert.Equal(3, fileDiffService.PrecomputeCalls.Count);
            Assert.All(fileDiffService.PrecomputeCalls, call => Assert.InRange(call.FilesAbsolutePath.Count, 1, 2));
            var allPrecomputeTargets = fileDiffService.PrecomputeCalls.SelectMany(call => call.FilesAbsolutePath).ToArray();
            Assert.Equal(6, allPrecomputeTargets.Length);
            Assert.Equal(6, allPrecomputeTargets.Distinct(System.StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenOldAndNewDirsAreSame_DeduplicatesAbsolutePathsAcrossPrecomputeBatches()
        {
            const string sameDir = "/virtual/same-dir";
            const string reportDir = "/virtual/report-dedup";

            var fileSystem = new FakeFileSystemService();
            fileSystem.SetFiles(
                sameDir,
                System.IO.Path.Combine(sameDir, "alpha.txt"),
                System.IO.Path.Combine(sameDir, "beta.txt"),
                System.IO.Path.Combine(sameDir, "gamma.txt"));

            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(System.StringComparer.Ordinal)
            {
                ["alpha.txt"] = true,
                ["beta.txt"] = true,
                ["gamma.txt"] = true
            });
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            // old dir == new dir: same absolute paths appear in both lists; precompute should deduplicate
            // old dir == new dir: 同じ絶対パスが両リストに出現し、precompute で重複排除される
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 1, ilPrecomputeBatchSize: 2),
                progressReporter,
                CreateExecutionContext(sameDir, sameDir, reportDir),
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            await service.ExecuteFolderDiffAsync();

            // 3 distinct files even though each appears in both old and new lists.
            // With batch size 2: batches [alpha, beta] and [gamma] = 2 PrecomputeAsync calls.
            // 3 ファイルが old/new 両方に出現するが重複排除され、バッチサイズ 2 で 2 回の PrecomputeAsync 呼び出しになる。
            Assert.Equal(2, fileDiffService.PrecomputeCalls.Count);
            var allPrecomputeTargets = fileDiffService.PrecomputeCalls.SelectMany(call => call.FilesAbsolutePath).ToArray();
            Assert.Equal(3, allPrecomputeTargets.Length);
            Assert.Equal(3, allPrecomputeTargets.Distinct(System.StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenCompleted_LogsFolderDiffCompletedViaLogger()
        {
            const string oldDir = "/virtual/old-logcheck";
            const string newDir = "/virtual/new-logcheck";
            const string reportDir = "/virtual/report-logcheck";

            var fileSystem = new FakeFileSystemService();
            fileSystem.SetFiles(oldDir, System.IO.Path.Combine(oldDir, "file.txt"));
            fileSystem.SetFiles(newDir, System.IO.Path.Combine(newDir, "file.txt"));

            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(System.StringComparer.Ordinal)
            {
                ["file.txt"] = true
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

            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Info
                    && entry.Message.Contains("Folder diff completed.", System.StringComparison.Ordinal));
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenILPrecomputeBatchSizeIsZero_UsesDefaultBatchSizeAndCallsPrecomputeOnce()
        {
            const string oldDir = "/virtual/old-zerobatch";
            const string newDir = "/virtual/new-zerobatch";
            const string reportDir = "/virtual/report-zerobatch";

            var fileSystem = new FakeFileSystemService();
            fileSystem.SetFiles(oldDir,
                System.IO.Path.Combine(oldDir, "file1.txt"),
                System.IO.Path.Combine(oldDir, "file2.txt"),
                System.IO.Path.Combine(oldDir, "file3.txt"));
            fileSystem.SetFiles(newDir,
                System.IO.Path.Combine(newDir, "file1.txt"),
                System.IO.Path.Combine(newDir, "file2.txt"),
                System.IO.Path.Combine(newDir, "file3.txt"));

            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(System.StringComparer.Ordinal)
            {
                ["file1.txt"] = true,
                ["file2.txt"] = true,
                ["file3.txt"] = true
            });
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            // ilPrecomputeBatchSize = 0 falls back to default (DefaultILPrecomputeBatchSize), so all 6 paths go in one batch
            // ilPrecomputeBatchSize = 0 はデフォルト（DefaultILPrecomputeBatchSize）にフォールバックし、全 6 パスが 1 バッチに入る
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 1, ilPrecomputeBatchSize: 0),
                progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            await service.ExecuteFolderDiffAsync();

            var precomputeCall = Assert.Single(fileDiffService.PrecomputeCalls);
            Assert.Equal(6, precomputeCall.FilesAbsolutePath.Count);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenCancelled_ThrowsOperationCanceledException()
        {
            const string oldDir = "/virtual/old-cancel";
            const string newDir = "/virtual/new-cancel";
            const string reportDir = "/virtual/report-cancel";

            var fileSystem = new FakeFileSystemService();
            fileSystem.SetFiles(oldDir, System.IO.Path.Combine(oldDir, "file.txt"));
            fileSystem.SetFiles(newDir, System.IO.Path.Combine(newDir, "file.txt"));

            var fileDiffService = new FakeFileDiffService(new Dictionary<string, bool>(System.StringComparer.Ordinal)
            {
                ["file.txt"] = true
            });
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            using var progressReporter = new ProgressReportService(new ConfigSettingsBuilder().Build());
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var service = new FolderDiffService(
                CreateConfig(maxParallelism: 1),
                progressReporter,
                CreateExecutionContext(oldDir, newDir, reportDir),
                fileDiffService,
                resultLists,
                logger,
                fileSystem);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteFolderDiffAsync(cts.Token));
        }
    }
}
