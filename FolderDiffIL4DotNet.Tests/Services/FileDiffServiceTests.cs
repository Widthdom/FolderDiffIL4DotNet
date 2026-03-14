using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Services.ILOutput;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class FileDiffServiceTests : IDisposable
    {
        private readonly string _rootDir;

        public FileDiffServiceTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-filediff-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
        }

        public void Dispose()
        {
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
        public async Task FilesAreEqualAsync_WhenPrimaryTextDiffThrows_LogsWarningAndFallsBackToSequentialDiff()
        {
            var oldDir = Path.Combine(_rootDir, "old");
            var newDir = Path.Combine(_rootDir, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);

            const string fileRelativePath = "sample.txt";
            var oldFileAbsolutePath = Path.Combine(oldDir, fileRelativePath);
            var newFileAbsolutePath = Path.Combine(newDir, fileRelativePath);
            File.WriteAllText(oldFileAbsolutePath, "old-content");
            File.WriteAllText(newFileAbsolutePath, "new");

            var config = new ConfigSettings
            {
                TextFileExtensions = new List<string> { ".txt" },
                IgnoredExtensions = new List<string>(),
                ShouldOutputILText = false,
                EnableILCache = false,
                OptimizeForNetworkShares = true
            };

            FileStream exclusiveLockStream = new FileStream(oldFileAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.None);
            var logger = new TestLogger(entry =>
            {
                if (entry.LogLevel == AppLogLevel.Warning && entry.Message.Contains("Falling back to sequential text diff", StringComparison.Ordinal))
                {
                    exclusiveLockStream?.Dispose();
                    exclusiveLockStream = null;
                }
            });

            var resultLists = new FileDiffResultLists();
            var executionContext = new DiffExecutionContext(
                oldDir,
                newDir,
                Path.Combine(_rootDir, "report"),
                optimizeForNetworkShares: true,
                detectedNetworkOld: false,
                detectedNetworkNew: false);

            try
            {
                var ilTextOutputService = new ILTextOutputService(executionContext, logger);
                var dotNetDisassembleService = new DotNetDisassembleService(config, ilCache: null, resultLists, logger, new DotNetDisassemblerCache(logger));
                var ilOutputService = new ILOutputService(config, executionContext, ilTextOutputService, dotNetDisassembleService, ilCache: null, logger);
                var service = new FileDiffService(config, ilOutputService, executionContext, resultLists, logger);

                var areEqual = await service.FilesAreEqualAsync(fileRelativePath, maxParallel: 1);

                Assert.False(areEqual);
                Assert.Equal(FileDiffResultLists.DiffDetailResult.TextMismatch, resultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath]);
                var warningLog = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
                Assert.Contains("Falling back to sequential text diff", warningLog.Message);
                Assert.IsType<IOException>(warningLog.Exception);
            }
            finally
            {
                exclusiveLockStream?.Dispose();
            }
        }

        private sealed class TestLogger : ILoggerService
        {
            private readonly Action<LogEntry> _onEntry;

            public TestLogger(Action<LogEntry> onEntry)
            {
                _onEntry = onEntry;
            }

            public string LogFileAbsolutePath => null;

            public List<LogEntry> Entries { get; } = new();

            public void Initialize() { }

            public void CleanupOldLogFiles(int maxLogGenerations) { }

            public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, Exception exception = null)
                => LogMessage(logLevel, message, shouldOutputMessageToConsole, consoleForegroundColor: null, exception);

            public void LogMessage(AppLogLevel logLevel, string message, bool shouldOutputMessageToConsole, ConsoleColor? consoleForegroundColor, Exception exception = null)
            {
                var entry = new LogEntry(logLevel, message, exception);
                Entries.Add(entry);
                _onEntry?.Invoke(entry);
            }
        }

        private sealed record LogEntry(AppLogLevel LogLevel, string Message, Exception Exception);
    }
}
