using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Services.ILOutput;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed partial class ILOutputServiceTests
    {
        [Fact]
        public async Task PrecomputeAsync_WhenPrefetchPathEvaluationThrowsArgumentException_LogsWarningAndDoesNotThrow()
        {
            var config = new ConfigSettingsBuilder
            {
                OptimizeForNetworkShares = false,
                EnableILCache = true,
                IgnoredExtensions = new(),
                TextFileExtensions = new()
            }.Build();

            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var executionContext = new DiffExecutionContext(
                oldFolderAbsolutePath: "/tmp/fd-iloutput-old",
                newFolderAbsolutePath: "/tmp/fd-iloutput-new",
                reportsFolderAbsolutePath: "/tmp/fd-iloutput-report",
                optimizeForNetworkShares: false,
                detectedNetworkOld: false,
                detectedNetworkNew: false);
            var service = new ILOutputService(
                config,
                executionContext,
                new NoOpIlTextOutputService(),
                new ThrowingPrefetchDisassembleService(),
                new ILCache(ilCacheDirectoryAbsolutePath: null, logger: logger),
                logger);
            var tempFile = Path.GetTempFileName();

            try
            {
                var ex = await Record.ExceptionAsync(() => service.PrecomputeAsync(new[] { tempFile }, maxParallel: 1));

                Assert.Null(ex);
                var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
                Assert.Contains("Failed to precompute SHA256 hashes", warning.Message, StringComparison.Ordinal);
                Assert.Contains("ArgumentException", warning.Message, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task DiffDotNetAssembliesAsync_WhenIlTextOutputFails_LogsRelativeAndAbsolutePaths()
        {
            var config = new ConfigSettingsBuilder
            {
                ShouldIgnoreMVID = true,
                IgnoredExtensions = new(),
                TextFileExtensions = new()
            }.Build();
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var executionContext = new DiffExecutionContext(
                oldFolderAbsolutePath: "/tmp/fd-iloutput-old",
                newFolderAbsolutePath: "/tmp/fd-iloutput-new",
                reportsFolderAbsolutePath: "/tmp/fd-iloutput-report",
                optimizeForNetworkShares: false,
                detectedNetworkOld: false,
                detectedNetworkNew: false);
            var service = new ILOutputService(
                config,
                executionContext,
                new ThrowingIlTextOutputService(),
                new SuccessfulDisassembleService(),
                ilCache: null,
                logger);

            var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
                service.DiffDotNetAssembliesAsync("lib/app.dll", "/virtual/old", "/virtual/new", shouldOutputIlText: true));

            var error = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Error);
            var expectedOldPath = Path.Combine("/virtual/old", "lib/app.dll");
            var expectedNewPath = Path.Combine("/virtual/new", "lib/app.dll");
            Assert.Contains("Failed to output IL", error.Message, StringComparison.Ordinal);
            Assert.Contains("lib/app.dll", error.Message, StringComparison.Ordinal);
            Assert.Contains($"Old='{expectedOldPath}'", error.Message, StringComparison.Ordinal);
            Assert.Contains($"New='{expectedNewPath}'", error.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(DirectoryNotFoundException), error.Message, StringComparison.Ordinal);
            Assert.Same(exception, error.Exception);
        }

        [Fact]
        public async Task DiffDotNetAssembliesAsync_WhenOneIlSideStaysEmpty_ThrowsAfterRetryLimit()
        {
            var config = new ConfigSettingsBuilder
            {
                ShouldIgnoreMVID = true,
                IgnoredExtensions = new(),
                TextFileExtensions = new()
            }.Build();
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var executionContext = new DiffExecutionContext(
                oldFolderAbsolutePath: "/tmp/fd-iloutput-old",
                newFolderAbsolutePath: "/tmp/fd-iloutput-new",
                reportsFolderAbsolutePath: "/tmp/fd-iloutput-report",
                optimizeForNetworkShares: false,
                detectedNetworkOld: false,
                detectedNetworkNew: false);
            var disassembleService = new EmptyOldDisassembleService();
            var service = new ILOutputService(
                config,
                executionContext,
                new NoOpIlTextOutputService(),
                disassembleService,
                ilCache: null,
                logger);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DiffDotNetAssembliesAsync("lib/app.dll", "/virtual/old", "/virtual/new", shouldOutputIlText: true));

            Assert.Equal(5, disassembleService.LineDisassemblyCalls);
            var warnings = logger.Entries.Where(entry => entry.LogLevel == AppLogLevel.Warning).ToList();
            Assert.Equal(4, warnings.Count);
            Assert.Contains("IL comparison raw disassembly produced an empty line set", warnings[0].Message, StringComparison.Ordinal);
            Assert.Contains("RawOldLines=0", warnings[0].Message, StringComparison.Ordinal);
            Assert.Contains("RawNewLines=1", warnings[0].Message, StringComparison.Ordinal);
            Assert.Contains("ShouldOutputIlText=True", warnings[0].Message, StringComparison.Ordinal);
            Assert.Contains("Attempt=1/5", warnings[0].Message, StringComparison.Ordinal);
            Assert.Contains("WillRetry=True", warnings[0].Message, StringComparison.Ordinal);
            var error = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Error);
            Assert.Contains("IL comparison raw disassembly produced an empty line set", error.Message, StringComparison.Ordinal);
            Assert.Contains("Attempt=5/5", error.Message, StringComparison.Ordinal);
            Assert.Contains("WillRetry=False", error.Message, StringComparison.Ordinal);
            Assert.Contains("0 vs N lines", error.Message, StringComparison.Ordinal);
            Assert.Contains("IL comparison raw disassembly stayed empty after 5 attempts", exception.Message, StringComparison.Ordinal);
            Assert.Contains("RawOldLines=0", exception.Message, StringComparison.Ordinal);
            Assert.Contains("RawNewLines=1", exception.Message, StringComparison.Ordinal);
            Assert.Contains("Attempts=5", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task DiffDotNetAssembliesAsync_WhenRawIlSideRecovers_RetriesAndWritesRecoveredLines()
        {
            var config = new ConfigSettingsBuilder
            {
                ShouldIgnoreMVID = true,
                IgnoredExtensions = new(),
                TextFileExtensions = new()
            }.Build();
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var executionContext = new DiffExecutionContext(
                oldFolderAbsolutePath: "/tmp/fd-iloutput-old",
                newFolderAbsolutePath: "/tmp/fd-iloutput-new",
                reportsFolderAbsolutePath: "/tmp/fd-iloutput-report",
                optimizeForNetworkShares: false,
                detectedNetworkOld: false,
                detectedNetworkNew: false);
            var ilTextOutputService = new CapturingIlTextOutputService();
            var disassembleService = new EmptyOldThenSuccessfulDisassembleService();
            var service = new ILOutputService(
                config,
                executionContext,
                ilTextOutputService,
                disassembleService,
                ilCache: null,
                logger);

            var result = await service.DiffDotNetAssembliesAsync("lib/app.dll", "/virtual/old", "/virtual/new", shouldOutputIlText: true);

            Assert.False(result.AreEqual);
            Assert.Equal(2, disassembleService.LineDisassemblyCalls);
            Assert.Equal(new[] { "recovered-old-il" }, ilTextOutputService.OldLines);
            Assert.Equal(new[] { "recovered-new-il" }, ilTextOutputService.NewLines);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("IL comparison raw disassembly produced an empty line set", warning.Message, StringComparison.Ordinal);
            Assert.Contains("Attempt=1/5", warning.Message, StringComparison.Ordinal);
            Assert.Contains("WillRetry=True", warning.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task DiffDotNetAssembliesAsync_WhenFilteredIlSideIsEmpty_LogsWithoutRetrying()
        {
            var config = new ConfigSettingsBuilder
            {
                ShouldIgnoreMVID = true,
                IgnoredExtensions = new(),
                TextFileExtensions = new()
            }.Build();
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var executionContext = new DiffExecutionContext(
                oldFolderAbsolutePath: "/tmp/fd-iloutput-old",
                newFolderAbsolutePath: "/tmp/fd-iloutput-new",
                reportsFolderAbsolutePath: "/tmp/fd-iloutput-report",
                optimizeForNetworkShares: false,
                detectedNetworkOld: false,
                detectedNetworkNew: false);
            var ilTextOutputService = new CapturingIlTextOutputService();
            var disassembleService = new FilteredEmptyOldDisassembleService();
            var service = new ILOutputService(
                config,
                executionContext,
                ilTextOutputService,
                disassembleService,
                ilCache: null,
                logger);

            var result = await service.DiffDotNetAssembliesAsync("lib/app.dll", "/virtual/old", "/virtual/new", shouldOutputIlText: true);

            Assert.False(result.AreEqual);
            Assert.Equal(1, disassembleService.LineDisassemblyCalls);
            Assert.Empty(ilTextOutputService.OldLines ?? Array.Empty<string>());
            Assert.Equal(new[] { "new-il" }, ilTextOutputService.NewLines);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("IL comparison filtered IL produced an empty line set", warning.Message, StringComparison.Ordinal);
            Assert.Contains("RawOldLines=1", warning.Message, StringComparison.Ordinal);
            Assert.Contains("FilteredOldLines=0", warning.Message, StringComparison.Ordinal);
            Assert.Contains("Attempt=1/5", warning.Message, StringComparison.Ordinal);
            Assert.Contains("WillRetry=False", warning.Message, StringComparison.Ordinal);
        }

        private sealed class NoOpIlTextOutputService : IILTextOutputService
        {
            public Task WriteFullIlTextsAsync(string fileRelativePath, IEnumerable<string> il1LinesMvidExcluded, IEnumerable<string> il2LinesMvidExcluded)
                => Task.CompletedTask;
        }

        private sealed class CapturingIlTextOutputService : IILTextOutputService
        {
            public IReadOnlyList<string>? OldLines { get; private set; }

            public IReadOnlyList<string>? NewLines { get; private set; }

            public Task WriteFullIlTextsAsync(string fileRelativePath, IEnumerable<string> il1LinesMvidExcluded, IEnumerable<string> il2LinesMvidExcluded)
            {
                OldLines = il1LinesMvidExcluded.ToArray();
                NewLines = il2LinesMvidExcluded.ToArray();
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingIlTextOutputService : IILTextOutputService
        {
            public Task WriteFullIlTextsAsync(string fileRelativePath, IEnumerable<string> il1LinesMvidExcluded, IEnumerable<string> il2LinesMvidExcluded)
                => throw new DirectoryNotFoundException("missing IL output folder");
        }

        private sealed class ThrowingPrefetchDisassembleService : IDotNetDisassembleService
        {
            public Task<(string oldIlText, string oldCommandString, string newIlText, string newCommandString)> DisassemblePairWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
                => Task.FromResult<(string, string, string, string)>((string.Empty, string.Empty, string.Empty, string.Empty));

            public Task<(IReadOnlyList<string> oldIlLines, string oldCommandString, IReadOnlyList<string> newIlLines, string newCommandString)> DisassemblePairAsLinesWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
                => Task.FromResult<(IReadOnlyList<string>, string, IReadOnlyList<string>, string)>((Array.Empty<string>(), string.Empty, Array.Empty<string>(), string.Empty));

            public Task PrefetchIlCacheAsync(IEnumerable<string> paths, int maxParallel, CancellationToken cancellationToken = default)
                => throw new ArgumentException("bad path");
        }

        private sealed class SuccessfulDisassembleService : IDotNetDisassembleService
        {
            public Task<(string oldIlText, string oldCommandString, string newIlText, string newCommandString)> DisassemblePairWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
                => Task.FromResult<(string, string, string, string)>((string.Empty, string.Empty, string.Empty, string.Empty));

            public Task<(IReadOnlyList<string> oldIlLines, string oldCommandString, IReadOnlyList<string> newIlLines, string newCommandString)> DisassemblePairAsLinesWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
                => Task.FromResult<(IReadOnlyList<string>, string, IReadOnlyList<string>, string)>((new[] { "same-il" }, "dotnet ildasm old.dll (version: 1.0.0)", new[] { "different-il" }, "dotnet ildasm new.dll (version: 1.0.0)"));

            public Task PrefetchIlCacheAsync(IEnumerable<string> paths, int maxParallel, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        private sealed class EmptyOldDisassembleService : IDotNetDisassembleService
        {
            public int LineDisassemblyCalls { get; private set; }

            public Task<(string oldIlText, string oldCommandString, string newIlText, string newCommandString)> DisassemblePairWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
                => Task.FromResult<(string, string, string, string)>((string.Empty, string.Empty, "new-il", string.Empty));

            public Task<(IReadOnlyList<string> oldIlLines, string oldCommandString, IReadOnlyList<string> newIlLines, string newCommandString)> DisassemblePairAsLinesWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
            {
                LineDisassemblyCalls++;
                return Task.FromResult<(IReadOnlyList<string>, string, IReadOnlyList<string>, string)>((Array.Empty<string>(), "dotnet ildasm old.dll (version: 1.0.0)", new[] { "new-il" }, "dotnet ildasm new.dll (version: 1.0.0)"));
            }

            public Task PrefetchIlCacheAsync(IEnumerable<string> paths, int maxParallel, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        private sealed class EmptyOldThenSuccessfulDisassembleService : IDotNetDisassembleService
        {
            public int LineDisassemblyCalls { get; private set; }

            public Task<(string oldIlText, string oldCommandString, string newIlText, string newCommandString)> DisassemblePairWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
                => Task.FromResult<(string, string, string, string)>((string.Empty, string.Empty, string.Empty, string.Empty));

            public Task<(IReadOnlyList<string> oldIlLines, string oldCommandString, IReadOnlyList<string> newIlLines, string newCommandString)> DisassemblePairAsLinesWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
            {
                LineDisassemblyCalls++;
                IReadOnlyList<string> oldLines = LineDisassemblyCalls == 1
                    ? Array.Empty<string>()
                    : new[] { "recovered-old-il" };
                IReadOnlyList<string> newLines = LineDisassemblyCalls == 1
                    ? new[] { "new-il" }
                    : new[] { "recovered-new-il" };
                return Task.FromResult<(IReadOnlyList<string>, string, IReadOnlyList<string>, string)>((oldLines, "dotnet ildasm old.dll (version: 1.0.0)", newLines, "dotnet ildasm new.dll (version: 1.0.0)"));
            }

            public Task PrefetchIlCacheAsync(IEnumerable<string> paths, int maxParallel, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        private sealed class FilteredEmptyOldDisassembleService : IDotNetDisassembleService
        {
            public int LineDisassemblyCalls { get; private set; }

            public Task<(string oldIlText, string oldCommandString, string newIlText, string newCommandString)> DisassemblePairWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
                => Task.FromResult<(string, string, string, string)>((string.Empty, string.Empty, string.Empty, string.Empty));

            public Task<(IReadOnlyList<string> oldIlLines, string oldCommandString, IReadOnlyList<string> newIlLines, string newCommandString)> DisassemblePairAsLinesWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
            {
                LineDisassemblyCalls++;
                IReadOnlyList<string> oldLines = new[] { "// MVID: old" };
                IReadOnlyList<string> newLines = new[] { "new-il" };
                return Task.FromResult<(IReadOnlyList<string>, string, IReadOnlyList<string>, string)>((oldLines, "dotnet ildasm old.dll (version: 1.0.0)", newLines, "dotnet ildasm new.dll (version: 1.0.0)"));
            }

            public Task PrefetchIlCacheAsync(IEnumerable<string> paths, int maxParallel, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }
}
