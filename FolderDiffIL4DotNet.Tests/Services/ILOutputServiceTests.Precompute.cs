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

        private sealed class NoOpIlTextOutputService : IILTextOutputService
        {
            public Task WriteFullIlTextsAsync(string fileRelativePath, IEnumerable<string> il1LinesMvidExcluded, IEnumerable<string> il2LinesMvidExcluded)
                => Task.CompletedTask;
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
    }
}
