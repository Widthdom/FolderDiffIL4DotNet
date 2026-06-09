using System;
using System.Collections.Generic;
using System.IO;
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
        public async Task DiffDotNetAssembliesAsync_WhenOneIlSideIsEmpty_LogsLineCounts()
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
                new NoOpIlTextOutputService(),
                new EmptyOldDisassembleService(),
                ilCache: null,
                logger);

            var result = await service.DiffDotNetAssembliesAsync("lib/app.dll", "/virtual/old", "/virtual/new", shouldOutputIlText: true);

            Assert.False(result.AreEqual);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("IL comparison raw disassembly produced an empty line set", warning.Message, StringComparison.Ordinal);
            Assert.Contains("RawOldLines=0", warning.Message, StringComparison.Ordinal);
            Assert.Contains("RawNewLines=1", warning.Message, StringComparison.Ordinal);
            Assert.Contains("ShouldOutputIlText=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains("0 vs N lines", warning.Message, StringComparison.Ordinal);
        }

        private sealed class NoOpIlTextOutputService : IILTextOutputService
        {
            public Task WriteFullIlTextsAsync(string fileRelativePath, IEnumerable<string> il1LinesMvidExcluded, IEnumerable<string> il2LinesMvidExcluded)
                => Task.CompletedTask;
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
            public Task<(string oldIlText, string oldCommandString, string newIlText, string newCommandString)> DisassemblePairWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
                => Task.FromResult<(string, string, string, string)>((string.Empty, string.Empty, "new-il", string.Empty));

            public Task<(IReadOnlyList<string> oldIlLines, string oldCommandString, IReadOnlyList<string> newIlLines, string newCommandString)> DisassemblePairAsLinesWithSameDisassemblerAsync(
                string oldPath, string newPath, CancellationToken cancellationToken = default)
                => Task.FromResult<(IReadOnlyList<string>, string, IReadOnlyList<string>, string)>((Array.Empty<string>(), "dotnet ildasm old.dll (version: 1.0.0)", new[] { "new-il" }, "dotnet ildasm new.dll (version: 1.0.0)"));

            public Task PrefetchIlCacheAsync(IEnumerable<string> paths, int maxParallel, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }
}
