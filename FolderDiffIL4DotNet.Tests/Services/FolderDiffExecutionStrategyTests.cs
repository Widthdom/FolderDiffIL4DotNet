using System;
using System.Collections.Generic;
using System.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="FolderDiffExecutionStrategy"/> covering file enumeration, filtering, parallelism policy, and .NET assembly detection.
    /// <see cref="FolderDiffExecutionStrategy"/> のユニットテスト。ファイル列挙、フィルタリング、並列度ポリシー、.NET アセンブリ検出を検証します。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class FolderDiffExecutionStrategyTests
    {
        [Fact]
        public void EnumerateIncludedFiles_FiltersIgnoredFiles_AndRecordsIgnoredLocations()
        {
            const string oldDir = "/virtual/old";
            const string newDir = "/virtual/new";
            const string reportDir = "/virtual/report";

            var fileSystem = new FakeFileSystemService();
            fileSystem.SetFiles(oldDir,
                Path.Combine(oldDir, "keep.txt"),
                Path.Combine(oldDir, "ignored.pdb"));
            fileSystem.SetFiles(newDir,
                Path.Combine(newDir, "keep.txt"),
                Path.Combine(newDir, "ignored.pdb"));

            var resultLists = new FileDiffResultLists();
            var strategy = CreateStrategy(
                CreateConfig(),
                CreateExecutionContext(oldDir, newDir, reportDir),
                resultLists,
                fileSystem);

            var oldIncludedFiles = strategy.EnumerateIncludedFiles(oldDir, FileDiffResultLists.IgnoredFileLocation.Old);
            var newIncludedFiles = strategy.EnumerateIncludedFiles(newDir, FileDiffResultLists.IgnoredFileLocation.New);

            Assert.Equal(new[] { Path.Combine(oldDir, "keep.txt") }, oldIncludedFiles);
            Assert.Equal(new[] { Path.Combine(newDir, "keep.txt") }, newIncludedFiles);
            Assert.Equal(
                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New,
                resultLists.IgnoredFilesRelativePathToLocation["ignored.pdb"]);
        }

        [Fact]
        public void ComputeUnionFileCount_UsesRelativePathUnionAcrossRoots()
        {
            const string oldDir = "/virtual/old";
            const string newDir = "/virtual/new";
            const string reportDir = "/virtual/report";

            var strategy = CreateStrategy(
                CreateConfig(),
                CreateExecutionContext(oldDir, newDir, reportDir),
                new FileDiffResultLists(),
                new FakeFileSystemService());

            var count = strategy.ComputeUnionFileCount(
                new[]
                {
                    Path.Combine(oldDir, "shared.txt"),
                    Path.Combine(oldDir, "old-only.txt")
                },
                new[]
                {
                    Path.Combine(newDir, "shared.txt"),
                    Path.Combine(newDir, "new-only.txt")
                });

            Assert.Equal(3, count);
        }

        [Fact]
        public void ComputeUnionFileCount_CasingOnlyDifference_RespectsCurrentFileSystemSemantics()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-strategy-case-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);

            try
            {
                var strategy = CreateStrategy(
                    CreateConfig(),
                    CreateExecutionContext(oldDir, newDir, Path.Combine(tempRoot, "report")),
                    new FileDiffResultLists(),
                    new FakeFileSystemService());

                var count = strategy.ComputeUnionFileCount(
                    new[] { Path.Combine(oldDir, "Case.txt") },
                    new[] { Path.Combine(newDir, "case.txt") });

                Assert.Equal(IsCaseInsensitiveDirectory(oldDir) ? 1 : 2, count);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        [Fact]
        public void DetermineMaxParallel_WhenAutoAndNetworkOptimized_CapsByNetworkLimit()
        {
            var strategy = CreateStrategy(
                CreateConfig(maxParallelism: 0),
                CreateExecutionContext("/virtual/old", "/virtual/new", "/virtual/report", optimizeForNetworkShares: true),
                new FileDiffResultLists(),
                new FakeFileSystemService());

            Assert.Equal(Math.Min(Environment.ProcessorCount, 8), strategy.DetermineMaxParallel());
        }

        [Fact]
        public void DetermineMaxParallel_WhenAutoAndLocal_UsesProcessorCountTimesTwo()
        {
            // I/O-bound file comparison benefits from higher parallelism than CPU core count
            // I/O バウンドのファイル比較は CPU コア数より高い並列度で効率が上がる
            var strategy = CreateStrategy(
                CreateConfig(maxParallelism: 0),
                CreateExecutionContext("/virtual/old", "/virtual/new", "/virtual/report", optimizeForNetworkShares: false),
                new FileDiffResultLists(),
                new FakeFileSystemService());

            Assert.Equal(Environment.ProcessorCount * 2, strategy.DetermineMaxParallel());
        }

        [Fact]
        public void DetermineMaxParallel_WhenConfiguredPositive_ReturnsConfiguredValue()
        {
            var strategy = CreateStrategy(
                CreateConfig(maxParallelism: 12),
                CreateExecutionContext("/virtual/old", "/virtual/new", "/virtual/report", optimizeForNetworkShares: true),
                new FileDiffResultLists(),
                new FakeFileSystemService());

            Assert.Equal(12, strategy.DetermineMaxParallel());
        }

        [Fact]
        public void CountDotNetAssemblyCandidates_DeduplicatesPaths_CountsManagedFiles_AndReportsProgress()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-strategy-dotnet-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                string managedAssemblyPath = typeof(FolderDiffExecutionStrategy).Assembly.Location;
                string textFilePath = Path.Combine(tempRoot, "notes.txt");
                File.WriteAllText(textFilePath, "not a managed assembly");

                var strategy = CreateStrategy(
                    CreateConfig(),
                    CreateExecutionContext(Path.Combine(tempRoot, "old"), Path.Combine(tempRoot, "new"), Path.Combine(tempRoot, "report")),
                    new FileDiffResultLists(),
                    new FakeFileSystemService());

                var progressValues = new List<double>();

                int count = strategy.CountDotNetAssemblyCandidates(
                    new[] { managedAssemblyPath, managedAssemblyPath, textFilePath },
                    new[] { textFilePath, managedAssemblyPath },
                    progressValues.Add);

                Assert.Equal(1, count);
                Assert.Equal(new[] { 50.0, 100.0 }, progressValues);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        [Fact]
        public void CountDotNetAssemblyCandidates_EmptyInputs_ReturnsZeroWithoutReportingProgress()
        {
            var strategy = CreateStrategy(
                CreateConfig(),
                CreateExecutionContext("/virtual/old", "/virtual/new", "/virtual/report"),
                new FileDiffResultLists(),
                new FakeFileSystemService());

            var progressValues = new List<double>();

            int count = strategy.CountDotNetAssemblyCandidates(Array.Empty<string>(), Array.Empty<string>(), progressValues.Add);

            Assert.Equal(0, count);
            Assert.Empty(progressValues);
        }

        private static FolderDiffExecutionStrategy CreateStrategy(
            ConfigSettings config,
            DiffExecutionContext executionContext,
            FileDiffResultLists resultLists,
            IFileSystemService fileSystem)
            => new(config, executionContext, resultLists, fileSystem);

        private static ConfigSettings CreateConfig(int maxParallelism = 0) => new ConfigSettingsBuilder()
        {
            IgnoredExtensions = new List<string> { ".pdb" },
            ShouldIncludeIgnoredFiles = true,
            MaxParallelism = maxParallelism
        }.Build();

        private static DiffExecutionContext CreateExecutionContext(
            string oldDir,
            string newDir,
            string reportDir,
            bool optimizeForNetworkShares = false)
            => new(oldDir, newDir, reportDir, optimizeForNetworkShares, detectedNetworkOld: false, detectedNetworkNew: false);

        private static bool IsCaseInsensitiveDirectory(string directoryPath)
        {
            if (OperatingSystem.IsWindows())
            {
                return true;
            }

            string fullPath = Path.GetFullPath(directoryPath);
            string trimmedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string? parentPath = Path.GetDirectoryName(trimmedPath);
            string directoryName = Path.GetFileName(trimmedPath);
            if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(directoryName))
            {
                return OperatingSystem.IsMacOS();
            }

            string alternateName = directoryName[0] switch
            {
                >= 'a' and <= 'z' => char.ToUpperInvariant(directoryName[0]) + directoryName[1..],
                >= 'A' and <= 'Z' => char.ToLowerInvariant(directoryName[0]) + directoryName[1..],
                _ => directoryName,
            };

            return alternateName != directoryName && Directory.Exists(Path.Combine(parentPath, alternateName));
        }

        private sealed class FakeFileSystemService : IFileSystemService
        {
            private readonly Dictionary<string, IReadOnlyList<string>> _filesByRoot = new(StringComparer.OrdinalIgnoreCase);

            public void SetFiles(string rootFolderAbsolutePath, params string[] files)
                => _filesByRoot[rootFolderAbsolutePath] = files;

            public IEnumerable<string> EnumerateFiles(string rootFolderAbsolutePath, string searchPattern, SearchOption searchOption)
                => _filesByRoot.TryGetValue(rootFolderAbsolutePath, out var files)
                    ? files
                    : Array.Empty<string>();

            public void CreateDirectory(string path)
            {
            }

            public DateTime GetLastWriteTimeUtc(string path) => DateTime.UnixEpoch;
        }
    }
}
