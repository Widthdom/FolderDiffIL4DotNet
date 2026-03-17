using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.ILOutput;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    [Trait("Category", "Integration")]
    public sealed class FolderDiffServiceTests : IDisposable
    {
        private readonly string _rootDir;
        private readonly FileDiffResultLists _resultLists = new();
        private readonly ILoggerService _logger = new LoggerService();

        public FolderDiffServiceTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-folderdiff-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
            TimestampCache.Clear();
            _resultLists.ResetAll();
        }

        public void Dispose()
        {
            TimestampCache.Clear();
            _resultLists.ResetAll();
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
            using var progressReporter = new ProgressReportService(new ConfigSettings());
            var service = CreateService(config, progressReporter, oldDir, newDir, reportDir);

            await service.ExecuteFolderDiffAsync();

            Assert.Contains("same.txt", _resultLists.UnchangedFilesRelativePath);
            Assert.Contains("modified.txt", _resultLists.ModifiedFilesRelativePath);
            Assert.Contains(Path.Combine(oldDir, "removed.txt"), _resultLists.RemovedFilesAbsolutePath);
            Assert.Contains(Path.Combine(newDir, "added.txt"), _resultLists.AddedFilesAbsolutePath);
            Assert.Equal(
                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New,
                _resultLists.IgnoredFilesRelativePathToLocation["ignored.pdb"]);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.MD5Match, _resultLists.FileRelativePathToDiffDetailDictionary["same.txt"]);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMismatch, _resultLists.FileRelativePathToDiffDetailDictionary["modified.txt"]);
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
            using var progressReporter = new ProgressReportService(new ConfigSettings());
            var service = CreateService(config, progressReporter, oldDir, newDir, reportDir);

            await service.ExecuteFolderDiffAsync();

            Assert.Contains(Path.Combine("nested", "same.txt"), _resultLists.UnchangedFilesRelativePath);
            Assert.Contains(Path.Combine("nested", "modified.txt"), _resultLists.ModifiedFilesRelativePath);
            Assert.Contains(Path.Combine(newDir, "nested", "added.txt"), _resultLists.AddedFilesAbsolutePath);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.MD5Match, _resultLists.FileRelativePathToDiffDetailDictionary[Path.Combine("nested", "same.txt")]);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMismatch, _resultLists.FileRelativePathToDiffDetailDictionary[Path.Combine("nested", "modified.txt")]);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_ClearsPreviousRunStateAtStart()
        {
            _resultLists.AddModifiedFileRelativePath("stale.txt");
            _resultLists.RecordDiffDetail("stale.txt", FileDiffResultLists.DiffDetailResult.MD5Mismatch);

            var oldDir = Path.Combine(_rootDir, "old-empty");
            var newDir = Path.Combine(_rootDir, "new-empty");
            var reportDir = Path.Combine(_rootDir, "report-empty");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var config = CreateConfig(maxParallelism: 1);
            using var progressReporter = new ProgressReportService(new ConfigSettings());
            var service = CreateService(config, progressReporter, oldDir, newDir, reportDir);

            await service.ExecuteFolderDiffAsync();

            Assert.Empty(_resultLists.ModifiedFilesRelativePath);
            Assert.Empty(_resultLists.FileRelativePathToDiffDetailDictionary);
            Assert.Empty(_resultLists.UnchangedFilesRelativePath);
            Assert.Empty(_resultLists.AddedFilesAbsolutePath);
            Assert.Empty(_resultLists.RemovedFilesAbsolutePath);
            Assert.Empty(_resultLists.IgnoredFilesRelativePathToLocation);
            Assert.Empty(_resultLists.OldFilesAbsolutePath);
            Assert.Empty(_resultLists.NewFilesAbsolutePath);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_TextExtensionComparison_IsCaseInsensitive()
        {
            var oldDir = Path.Combine(_rootDir, "old-case-insensitive");
            var newDir = Path.Combine(_rootDir, "new-case-insensitive");
            var reportDir = Path.Combine(_rootDir, "report-case-insensitive");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            const string fileRelativePath = "sample.TxT";
            WriteFile(oldDir, fileRelativePath, "before");
            WriteFile(newDir, fileRelativePath, "after");

            var config = CreateConfig(maxParallelism: 1);
            config.TextFileExtensions = new List<string> { ".TXT" };
            using var progressReporter = new ProgressReportService(new ConfigSettings());
            var service = CreateService(config, progressReporter, oldDir, newDir, reportDir);

            await service.ExecuteFolderDiffAsync();

            Assert.Equal(
                FileDiffResultLists.DiffDetailResult.TextMismatch,
                _resultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath]);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenModifiedFileTimestampIsOlder_RecordsWarning()
        {
            var oldDir = Path.Combine(_rootDir, "old-timestamp-warning");
            var newDir = Path.Combine(_rootDir, "new-timestamp-warning");
            var reportDir = Path.Combine(_rootDir, "report-timestamp-warning");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            const string fileRelativePath = "timestamp.txt";
            WriteFile(oldDir, fileRelativePath, "old content");
            WriteFile(newDir, fileRelativePath, "new content");
            var oldFile = Path.Combine(oldDir, fileRelativePath);
            var newFile = Path.Combine(newDir, fileRelativePath);
            File.SetLastWriteTimeUtc(oldFile, new DateTime(2026, 3, 14, 1, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(newFile, new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc));

            var config = CreateConfig(maxParallelism: 1);
            using var progressReporter = new ProgressReportService(new ConfigSettings());
            var service = CreateService(config, progressReporter, oldDir, newDir, reportDir);

            await service.ExecuteFolderDiffAsync();

            var warning = Assert.Single(_resultLists.NewFileTimestampOlderThanOldWarnings.Values);
            Assert.Equal(fileRelativePath, warning.FileRelativePath);
            Assert.Equal(TimestampCache.GetOrAdd(oldFile), warning.OldTimestamp);
            Assert.Equal(TimestampCache.GetOrAdd(newFile), warning.NewTimestamp);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenUnchangedFileTimestampIsOlder_DoesNotRecordWarning()
        {
            var oldDir = Path.Combine(_rootDir, "old-timestamp-warning-unchanged");
            var newDir = Path.Combine(_rootDir, "new-timestamp-warning-unchanged");
            var reportDir = Path.Combine(_rootDir, "report-timestamp-warning-unchanged");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            const string fileRelativePath = "timestamp.txt";
            WriteFile(oldDir, fileRelativePath, "same");
            WriteFile(newDir, fileRelativePath, "same");
            File.SetLastWriteTimeUtc(Path.Combine(oldDir, fileRelativePath), new DateTime(2026, 3, 14, 1, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(Path.Combine(newDir, fileRelativePath), new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc));

            var config = CreateConfig(maxParallelism: 1);
            using var progressReporter = new ProgressReportService(new ConfigSettings());
            var service = CreateService(config, progressReporter, oldDir, newDir, reportDir);

            await service.ExecuteFolderDiffAsync();

            Assert.Empty(_resultLists.NewFileTimestampOlderThanOldWarnings);
            Assert.Contains(fileRelativePath, _resultLists.UnchangedFilesRelativePath);
        }

        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenTimestampWarningDisabled_DoesNotRecordWarning()
        {
            var oldDir = Path.Combine(_rootDir, "old-timestamp-warning-off");
            var newDir = Path.Combine(_rootDir, "new-timestamp-warning-off");
            var reportDir = Path.Combine(_rootDir, "report-timestamp-warning-off");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            const string fileRelativePath = "timestamp.txt";
            WriteFile(oldDir, fileRelativePath, "old content");
            WriteFile(newDir, fileRelativePath, "new content");
            File.SetLastWriteTimeUtc(Path.Combine(oldDir, fileRelativePath), new DateTime(2026, 3, 14, 1, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(Path.Combine(newDir, fileRelativePath), new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc));

            var config = CreateConfig(maxParallelism: 1);
            config.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = false;
            using var progressReporter = new ProgressReportService(new ConfigSettings());
            var service = CreateService(config, progressReporter, oldDir, newDir, reportDir);

            await service.ExecuteFolderDiffAsync();

            Assert.Empty(_resultLists.NewFileTimestampOlderThanOldWarnings);
        }

        /// <summary>
        /// シンボリックリンク経由のファイルも列挙・比較して相対パス分類できることを確認します。
        /// </summary>
        [Fact]
        public async Task ExecuteFolderDiffAsync_WhenComparingFileSymlinks_ClassifiesUsingLinkedContents()
        {
            var oldDir = Path.Combine(_rootDir, "old-symlink");
            var newDir = Path.Combine(_rootDir, "new-symlink");
            var reportDir = Path.Combine(_rootDir, "report-symlink");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            if (!TryCreateFileSymbolicLink(
                    sourceFileAbsolutePath: Path.Combine(oldDir, "target.txt"),
                    linkFileAbsolutePath: Path.Combine(oldDir, "linked.txt"),
                    content: "same")
                || !TryCreateFileSymbolicLink(
                    sourceFileAbsolutePath: Path.Combine(newDir, "target.txt"),
                    linkFileAbsolutePath: Path.Combine(newDir, "linked.txt"),
                    content: "same"))
            {
                return;
            }

            var config = CreateConfig(maxParallelism: 1);
            using var progressReporter = new ProgressReportService(new ConfigSettings());
            var service = CreateService(config, progressReporter, oldDir, newDir, reportDir);

            await service.ExecuteFolderDiffAsync();

            Assert.Contains("linked.txt", _resultLists.UnchangedFilesRelativePath);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.MD5Match, _resultLists.FileRelativePathToDiffDetailDictionary["linked.txt"]);
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
            OptimizeForNetworkShares = false,
            AutoDetectNetworkShares = false
        };

        private FolderDiffService CreateService(ConfigSettings config, ProgressReportService progressReporter, string oldDir, string newDir, string reportDir)
        {
            var executionContext = new DiffExecutionContext(
                oldDir,
                newDir,
                reportDir,
                optimizeForNetworkShares: false,
                detectedNetworkOld: false,
                detectedNetworkNew: false);
            var ilTextOutputService = new ILTextOutputService(executionContext, _logger);
            var dotNetDisassembleService = new DotNetDisassembleService(config, ilCache: null, _resultLists, _logger, new DotNetDisassemblerCache(_logger));
            var ilOutputService = new ILOutputService(config, executionContext, ilTextOutputService, dotNetDisassembleService, ilCache: null, _logger);
            var fileDiffService = new FileDiffService(config, ilOutputService, executionContext, _resultLists, _logger);
            return new FolderDiffService(config, progressReporter, executionContext, fileDiffService, _resultLists, _logger);
        }

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

        /// <summary>
        /// 実ファイルを作成したうえで、そのファイルを指すシンボリックリンクを生成します。
        /// </summary>
        /// <param name="sourceFileAbsolutePath">リンク先として使う実ファイルの絶対パスです。</param>
        /// <param name="linkFileAbsolutePath">生成するシンボリックリンクの絶対パスです。</param>
        /// <param name="content">実ファイルへ書き込む内容です。</param>
        /// <returns>リンク生成に成功した場合は true、権限やプラットフォーム制約で生成できない場合は false です。</returns>
        private static bool TryCreateFileSymbolicLink(string sourceFileAbsolutePath, string linkFileAbsolutePath, string content)
        {
            try
            {
                File.WriteAllText(sourceFileAbsolutePath, content);
                File.CreateSymbolicLink(linkFileAbsolutePath, sourceFileAbsolutePath);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (PlatformNotSupportedException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
