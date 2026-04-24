using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Plugin.Abstractions;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="IFileComparisonHook"/> integration in <see cref="FileDiffService"/>.
    /// <see cref="FileDiffService"/> における <see cref="IFileComparisonHook"/> 統合のテスト。
    /// </summary>
    public sealed partial class FileDiffServiceUnitTests
    {
        [Fact]
        public async Task FilesAreEqualAsync_BeforeCompareHookOverrides_SkipsBuiltInComparison()
        {
            // Arrange: hook returns AreEqual=true, so built-in comparison should be skipped
            // 準備: フックが AreEqual=true を返すため、組み込み比較はスキップされる
            var fakeComparison = new FakeFileComparisonService { HashResult = false };
            var fakeIl = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();

            var hook = new FakeFileComparisonHook
            {
                BeforeResult = new FileComparisonHookResult
                {
                    AreEqual = true,
                    DiffDetailLabel = "CustomMatch"
                }
            };

            var service = CreateServiceWithHooks(fakeComparison, fakeIl, resultLists, logger, new[] { hook });

            // Act / 実行
            var result = await service.FilesAreEqualAsync("test.dll");

            // Assert / 検証
            Assert.True(result);
            Assert.Empty(fakeComparison.HashCalls); // built-in comparison was not called / 組み込み比較は呼ばれていない
            Assert.Single(hook.BeforeCompareCalls);
            Assert.Single(hook.AfterCompareCalls);
            Assert.True(hook.AfterCompareCalls.First().AreEqual);
        }

        [Fact]
        public async Task FilesAreEqualAsync_BeforeCompareHookReturnsNotEqual_SkipsBuiltInComparison()
        {
            // Arrange / 準備
            var fakeComparison = new FakeFileComparisonService { HashResult = true };
            var fakeIl = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();

            var hook = new FakeFileComparisonHook
            {
                BeforeResult = new FileComparisonHookResult
                {
                    AreEqual = false,
                    DiffDetailLabel = "CustomDiff"
                }
            };

            var service = CreateServiceWithHooks(fakeComparison, fakeIl, resultLists, logger, new[] { hook });

            // Act / 実行
            var result = await service.FilesAreEqualAsync("test.dll");

            // Assert / 検証
            Assert.False(result);
            Assert.Empty(fakeComparison.HashCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_BeforeCompareHookReturnsNull_ProceedsToBuiltIn()
        {
            // Arrange: hook returns null, so built-in comparison should proceed
            // 準備: フックが null を返すため、組み込み比較に進む
            var fakeComparison = new FakeFileComparisonService { HashResult = true };
            var fakeIl = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();

            var hook = new FakeFileComparisonHook { BeforeResult = null };

            var service = CreateServiceWithHooks(fakeComparison, fakeIl, resultLists, logger, new[] { hook });

            // Act / 実行
            var result = await service.FilesAreEqualAsync("test.dll");

            // Assert / 検証
            Assert.True(result);
            Assert.Single(fakeComparison.HashCalls); // built-in comparison was called / 組み込み比較が呼ばれた
            Assert.Single(hook.AfterCompareCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_BeforeCompareHookThrows_LogsAndProceedsToBuiltIn()
        {
            // Arrange: hook throws, so pipeline should continue to built-in comparison
            // 準備: フックが例外を投げるため、パイプラインは組み込み比較に進む
            var fakeComparison = new FakeFileComparisonService { HashResult = true };
            var fakeIl = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();

            var hook = new FakeFileComparisonHook
            {
                BeforeException = new InvalidOperationException("Hook error")
            };

            var service = CreateServiceWithHooks(fakeComparison, fakeIl, resultLists, logger, new[] { hook });

            // Act / 実行
            var result = await service.FilesAreEqualAsync("test.dll");

            // Assert / 検証
            Assert.True(result);
            Assert.Single(fakeComparison.HashCalls);
            Assert.Contains(logger.Messages,
                m => m.Contains("Plugin BeforeCompare hook 'FakeFileComparisonHook' failed", StringComparison.Ordinal)
                    && m.Contains("Order=0", StringComparison.Ordinal)
                    && m.Contains("OldRoot='/virtual/old'", StringComparison.Ordinal)
                    && m.Contains("NewRoot='/virtual/new'", StringComparison.Ordinal)
                    && m.Contains("InvalidOperationException", StringComparison.Ordinal));
        }

        [Fact]
        public async Task FilesAreEqualAsync_AfterCompareHookThrows_LogsButDoesNotAffectResult()
        {
            // Arrange: AfterCompare throws, but the result should still be returned
            // 準備: AfterCompare が例外を投げるが、結果は影響を受けない
            var fakeComparison = new FakeFileComparisonService { HashResult = true };
            var fakeIl = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();

            var hook = new FakeFileComparisonHook
            {
                AfterException = new InvalidOperationException("AfterCompare error")
            };

            var service = CreateServiceWithHooks(fakeComparison, fakeIl, resultLists, logger, new[] { hook });

            // Act / 実行
            var result = await service.FilesAreEqualAsync("test.dll");

            // Assert / 検証
            Assert.True(result);
            Assert.Contains(logger.Messages,
                m => m.Contains("Plugin AfterCompare hook 'FakeFileComparisonHook' failed", StringComparison.Ordinal)
                    && m.Contains("Order=0", StringComparison.Ordinal)
                    && m.Contains("OldRoot='/virtual/old'", StringComparison.Ordinal)
                    && m.Contains("NewRoot='/virtual/new'", StringComparison.Ordinal)
                    && m.Contains("InvalidOperationException", StringComparison.Ordinal));
        }

        [Fact]
        public async Task FilesAreEqualAsync_BeforeCompareHookThrowsOperationCanceledException_PropagatesCancellation()
        {
            // Arrange / 準備
            var fakeComparison = new FakeFileComparisonService { HashResult = true };
            var fakeIl = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();

            var hook = new FakeFileComparisonHook
            {
                BeforeException = new OperationCanceledException("Hook canceled")
            };

            var service = CreateServiceWithHooks(fakeComparison, fakeIl, resultLists, logger, new[] { hook });

            // Act / 実行
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.FilesAreEqualAsync("test.dll"));

            // Assert / 検証
            Assert.Empty(fakeComparison.HashCalls);
            Assert.DoesNotContain(logger.Messages, m => m.Contains("Plugin BeforeCompare hook failed", StringComparison.Ordinal));
        }

        [Fact]
        public async Task FilesAreEqualAsync_AfterCompareHookThrowsOperationCanceledException_PropagatesCancellation()
        {
            // Arrange / 準備
            var fakeComparison = new FakeFileComparisonService { HashResult = true };
            var fakeIl = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();

            var hook = new FakeFileComparisonHook
            {
                AfterException = new OperationCanceledException("AfterCompare canceled")
            };

            var service = CreateServiceWithHooks(fakeComparison, fakeIl, resultLists, logger, new[] { hook });

            // Act / 実行
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.FilesAreEqualAsync("test.dll"));

            // Assert / 検証
            Assert.Single(fakeComparison.HashCalls);
            Assert.DoesNotContain(logger.Messages, m => m.Contains("Plugin AfterCompare hook failed", StringComparison.Ordinal));
        }

        [Fact]
        public async Task FilesAreEqualAsync_MultipleHooks_FirstOverrideWins()
        {
            // Arrange: two hooks, first returns override, second should not be called
            // 準備: 2つのフック、最初がオーバーライドを返し、2番目は呼ばれない
            var fakeComparison = new FakeFileComparisonService { HashResult = false };
            var fakeIl = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();

            var hook1 = new FakeFileComparisonHook
            {
                OrderValue = 1,
                BeforeResult = new FileComparisonHookResult { AreEqual = true, DiffDetailLabel = "Hook1" }
            };
            var hook2 = new FakeFileComparisonHook
            {
                OrderValue = 2,
                BeforeResult = new FileComparisonHookResult { AreEqual = false, DiffDetailLabel = "Hook2" }
            };

            var service = CreateServiceWithHooks(fakeComparison, fakeIl, resultLists, logger, new[] { hook2, hook1 }); // out of order to test sorting

            // Act / 実行
            var result = await service.FilesAreEqualAsync("test.dll");

            // Assert / 検証
            Assert.True(result); // hook1 (lower Order) wins / hook1（低い Order）が優先
            Assert.Single(hook1.BeforeCompareCalls);
            Assert.Empty(hook2.BeforeCompareCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_NoHooks_NormalComparison()
        {
            // Arrange: no hooks registered, standard comparison path
            // 準備: フック未登録、標準比較パス
            var fakeComparison = new FakeFileComparisonService { HashResult = true };
            var fakeIl = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();

            var service = CreateServiceWithHooks(fakeComparison, fakeIl, resultLists, logger, Array.Empty<IFileComparisonHook>());

            // Act / 実行
            var result = await service.FilesAreEqualAsync("test.dll");

            // Assert / 検証
            Assert.True(result);
            Assert.Single(fakeComparison.HashCalls);
        }

        // ── Helper: factory with hooks / フック付きファクトリ ──

        private static FileDiffService CreateServiceWithHooks(
            FakeFileComparisonService fileComparisonService,
            FakeILOutputService ilOutputService,
            FileDiffResultLists resultLists,
            TestLogger logger,
            IEnumerable<IFileComparisonHook> hooks,
            bool optimizeForNetworkShares = false)
        {
            var builder = new ConfigSettingsBuilder
            {
                TextFileExtensions = new List<string> { ".txt" },
                IgnoredExtensions = new List<string>(),
                ShouldOutputILText = false,
                EnableILCache = false,
                OptimizeForNetworkShares = optimizeForNetworkShares,
                TextDiffParallelThresholdKilobytes = ConfigSettings.DefaultTextDiffParallelThresholdKilobytes,
                TextDiffChunkSizeKilobytes = ConfigSettings.DefaultTextDiffChunkSizeKilobytes,
                TextDiffParallelMemoryLimitMegabytes = 0
            };
            var config = builder.Build();

            var executionContext = new DiffExecutionContext(
                "/virtual/old",
                "/virtual/new",
                "/virtual/report",
                optimizeForNetworkShares: optimizeForNetworkShares,
                detectedNetworkOld: false,
                detectedNetworkNew: false);
            return new FileDiffService(config, ilOutputService, executionContext, resultLists, logger, fileComparisonService, hooks);
        }

        // ── Fake hook implementation / フェイクフック実装 ──

        /// <summary>
        /// Fake <see cref="IFileComparisonHook"/> for testing hook invocation and result override.
        /// フック呼び出しと結果オーバーライドのテスト用フェイク <see cref="IFileComparisonHook"/>。
        /// </summary>
        private sealed class FakeFileComparisonHook : IFileComparisonHook
        {
            public int OrderValue { get; set; } = 0;
            public int Order => OrderValue;

            public FileComparisonHookResult? BeforeResult { get; set; }
            public Exception? BeforeException { get; set; }
            public Exception? AfterException { get; set; }

            // Thread-safe: hooks may be called from Parallel.ForEachAsync in FolderDiffService / スレッドセーフ: FolderDiffService の Parallel.ForEachAsync から呼ばれる可能性がある
            public ConcurrentBag<FileComparisonHookContext> BeforeCompareCalls { get; } = new();
            public ConcurrentBag<(FileComparisonHookContext Context, bool AreEqual)> AfterCompareCalls { get; } = new();

            public Task<FileComparisonHookResult?> BeforeCompareAsync(FileComparisonHookContext context, CancellationToken cancellationToken)
            {
                BeforeCompareCalls.Add(context);
                if (BeforeException != null) throw BeforeException;
                return Task.FromResult(BeforeResult);
            }

            public Task AfterCompareAsync(FileComparisonHookContext context, bool areEqual, CancellationToken cancellationToken)
            {
                AfterCompareCalls.Add((context, areEqual));
                if (AfterException != null) throw AfterException;
                return Task.CompletedTask;
            }
        }
    }
}
