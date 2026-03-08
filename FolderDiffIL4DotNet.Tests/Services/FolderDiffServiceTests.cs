using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class FolderDiffServiceTests : IDisposable
    {
        private readonly string _rootDir;

        public FolderDiffServiceTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-folderdiff-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
            FileDiffResultLists.ResetAll();
        }

        public void Dispose()
        {
            FileDiffResultLists.ResetAll();
            try
            {
                if (Directory.Exists(_rootDir))
                {
                    Directory.Delete(_rootDir, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup errors in tests
            }
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_SequentialMode_ClassifiesFilesAndRecordsIgnored()
        {
            var oldDir = Path.Combine(_rootDir, "old-sequential");
            var newDir = Path.Combine(_rootDir, "new-sequential");
            var reportDir = Path.Combine(_rootDir, "report-sequential");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            WriteFile(oldDir, "same.txt", "same");
            WriteFile(newDir, "same.txt", "same");
            WriteFile(oldDir, "modified.txt", "before");
            WriteFile(newDir, "modified.txt", "after");
            WriteFile(oldDir, "removed.txt", "removed");
            WriteFile(newDir, "added.txt", "added");
            WriteFile(oldDir, "ignored.pdb", "ignore-old");
            WriteFile(newDir, "ignored.pdb", "ignore-new");

            var config = CreateConfig(maxParallelism: 1);
            using var progressReporter = new ProgressReportService();
            var service = new FolderDiffService(config, progressReporter, oldDir, newDir, reportDir);

            await service.ExecuteFolderDiffAsync();

            Assert.Contains("same.txt", FileDiffResultLists.UnchangedFilesRelativePath);
            Assert.Contains("modified.txt", FileDiffResultLists.ModifiedFilesRelativePath);
            Assert.Contains(Path.Combine(oldDir, "removed.txt"), FileDiffResultLists.RemovedFilesAbsolutePath);
            Assert.Contains(Path.Combine(newDir, "added.txt"), FileDiffResultLists.AddedFilesAbsolutePath);
            Assert.Equal(
                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New,
                FileDiffResultLists.IgnoredFilesRelativePathToLocation["ignored.pdb"]);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.MD5Match, FileDiffResultLists.FileRelativePathToDiffDetailDictionary["same.txt"]);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMismatch, FileDiffResultLists.FileRelativePathToDiffDetailDictionary["modified.txt"]);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_ParallelMode_ClassifiesWithoutRegression()
        {
            var oldDir = Path.Combine(_rootDir, "old-parallel");
            var newDir = Path.Combine(_rootDir, "new-parallel");
            var reportDir = Path.Combine(_rootDir, "report-parallel");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            WriteFile(oldDir, Path.Combine("nested", "same.txt"), "same");
            WriteFile(newDir, Path.Combine("nested", "same.txt"), "same");
            WriteFile(oldDir, Path.Combine("nested", "modified.txt"), "before");
            WriteFile(newDir, Path.Combine("nested", "modified.txt"), "after");
            WriteFile(newDir, Path.Combine("nested", "added.txt"), "added");

            var config = CreateConfig(maxParallelism: 2);
            using var progressReporter = new ProgressReportService();
            var service = new FolderDiffService(config, progressReporter, oldDir, newDir, reportDir);

            await service.ExecuteFolderDiffAsync();

            Assert.Contains(Path.Combine("nested", "same.txt"), FileDiffResultLists.UnchangedFilesRelativePath);
            Assert.Contains(Path.Combine("nested", "modified.txt"), FileDiffResultLists.ModifiedFilesRelativePath);
            Assert.Contains(Path.Combine(newDir, "nested", "added.txt"), FileDiffResultLists.AddedFilesAbsolutePath);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.MD5Match, FileDiffResultLists.FileRelativePathToDiffDetailDictionary[Path.Combine("nested", "same.txt")]);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMismatch, FileDiffResultLists.FileRelativePathToDiffDetailDictionary[Path.Combine("nested", "modified.txt")]);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_ClearsPreviousRunStateAtStart()
        {
            FileDiffResultLists.AddModifiedFileRelativePath("stale.txt");
            FileDiffResultLists.RecordDiffDetail("stale.txt", FileDiffResultLists.DiffDetailResult.MD5Mismatch);

            var oldDir = Path.Combine(_rootDir, "old-empty");
            var newDir = Path.Combine(_rootDir, "new-empty");
            var reportDir = Path.Combine(_rootDir, "report-empty");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var config = CreateConfig(maxParallelism: 1);
            using var progressReporter = new ProgressReportService();
            var service = new FolderDiffService(config, progressReporter, oldDir, newDir, reportDir);

            await service.ExecuteFolderDiffAsync();

            Assert.Empty(FileDiffResultLists.ModifiedFilesRelativePath);
            Assert.Empty(FileDiffResultLists.FileRelativePathToDiffDetailDictionary);
            Assert.Empty(FileDiffResultLists.UnchangedFilesRelativePath);
            Assert.Empty(FileDiffResultLists.AddedFilesAbsolutePath);
            Assert.Empty(FileDiffResultLists.RemovedFilesAbsolutePath);
            Assert.Empty(FileDiffResultLists.IgnoredFilesRelativePathToLocation);
            Assert.Empty(FileDiffResultLists.OldFilesAbsolutePath);
            Assert.Empty(FileDiffResultLists.NewFilesAbsolutePath);
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
            MaxParallelism = maxParallelism,
            OptimizeForNetworkShares = false,
            AutoDetectNetworkShares = false
        };

        private static void WriteFile(string rootDir, string relativePath, string content)
        {
            var absolutePath = Path.Combine(rootDir, relativePath);
            var parentDir = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
            File.WriteAllText(absolutePath, content);
        }
    }
}
