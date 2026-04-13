using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    // IL comparison, .NET detection, precompute, and semantic analysis tests for FileDiffService.
    // FileDiffService の IL 比較、.NET 検出、事前計算、セマンティック分析テスト。
    public sealed partial class FileDiffServiceUnitTests
    {
        [Fact]
        public async Task PrecomputeAsync_WhenSkipILIsTrue_DoesNotCallILOutputService()
        {
            var ilOutputService = new FakeILOutputService();
            var fileComparisonService = new FakeFileComparisonService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.SkipIL = true);

            await service.PrecomputeAsync(new[] { "/virtual/old/assembly.dll" }, maxParallel: 1);

            Assert.Equal(0, ilOutputService.PrecomputeCallCount);
        }

        [Fact]
        public async Task PrecomputeAsync_WhenSkipILIsFalse_DelegatesToILOutputService()
        {
            var ilOutputService = new FakeILOutputService();
            var fileComparisonService = new FakeFileComparisonService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.SkipIL = false);

            await service.PrecomputeAsync(new[] { "/virtual/old/assembly.dll" }, maxParallel: 1);

            Assert.Equal(1, ilOutputService.PrecomputeCallCount);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenSemanticAnalysisFails_DoesNotCrashAndRecordsNoChanges()
        {
            // When ILMismatch triggers TryAnalyzeAssemblySemanticChanges and the analysis
            // fails (e.g. non-existent or corrupt file), AssemblyMethodAnalyzer.Analyze
            // returns null via its internal catch-all. The outer TryAnalyzeAssemblySemanticChanges
            // handles null gracefully and records no semantic changes.
            // ILMismatch 時に TryAnalyzeAssemblySemanticChanges が発火し解析が失敗（存在しない
            // ファイル等）した場合、AssemblyMethodAnalyzer.Analyze が内部 catch-all で null を返す。
            // 外側の TryAnalyzeAssemblySemanticChanges は null を安全に処理し、セマンティック変更を
            // 記録しない。
            const string relativePath = "assembly.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                // IL diff returns not-equal, which triggers semantic analysis
                DiffResult = (AreEqual: false, DisassemblerLabel: "test-tool")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.ShouldIncludeAssemblySemanticChangesInReport = true);

            // Should complete without throwing — AssemblyMethodAnalyzer.Analyze catches
            // internally and returns null for non-existent paths.
            // AssemblyMethodAnalyzer.Analyze が内部で catch して null を返すため例外は発生しない。
            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.ILMismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);

            // No semantic changes should be recorded since analysis returned null
            // 解析が null を返したためセマンティック変更は記録されないこと
            Assert.False(resultLists.FileRelativePathToAssemblySemanticChanges.ContainsKey(relativePath));
            var warning = Assert.Single(logger.Entries, entry =>
                entry.LogLevel == AppLogLevel.Warning
                && entry.Message.Contains("Semantic analysis failed", StringComparison.Ordinal));
            Assert.NotNull(warning.Exception);
            Assert.Contains(warning.Exception!.GetType().Name, warning.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenSkipILIsTrueAndDotNetAssembly_SkipsILAndFallsThroughToTextOrBinaryDiff()
        {
            const string relativePath = "assembly.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                // Would normally trigger IL comparison:
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable),
                // Not a text extension, so neither text nor IL -> falls through to no-match
                TextDiffResult = false
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.SkipIL = true);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            // IL was skipped
            Assert.Empty(ilOutputService.DiffCalls);
            Assert.Empty(fileComparisonService.DotNetDetectionCalls);
            Assert.False(areEqual);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenDotNetDetectionFails_LogsWarningAndSkipsIL()
        {
            // When DetectDotNetExecutable returns a failure, the service should log a
            // warning and skip IL comparison (fall through to text/binary comparison).
            // DetectDotNetExecutable が失敗を返した場合、警告をログに記録し IL 比較をスキップすべき。
            const string relativePath = "detect-fail.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(
                    DotNetExecutableDetectionStatus.Failed,
                    new IOException("detection error"))
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.SHA256Mismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            Assert.Empty(ilOutputService.DiffCalls);
            Assert.Contains(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Warning
                    && entry.Message.Contains("Failed to detect", StringComparison.Ordinal));
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenILDiffReturnsEqual_RecordsILMatch()
        {
            // When IL comparison returns equal, DiffDetail should be ILMatch.
            // IL 比較が一致を返した場合、DiffDetail は ILMatch であるべき。
            const string relativePath = "matched.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffResult = (AreEqual: true, DisassemblerLabel: "dotnet-ildasm v1.0")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.True(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.ILMatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            Assert.Single(ilOutputService.DiffCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenILDiffThrowsInvalidOperationException_LogsErrorAndRethrows()
        {
            // InvalidOperationException from IL diff should be caught, logged as Error, and re-thrown.
            // IL 差分からの InvalidOperationException はキャッチされ、Error としてログされ、再スローされるべき。
            const string relativePath = "invalid-op.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffException = new InvalidOperationException("no disassembler")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.FilesAreEqualAsync(relativePath));

            Assert.Equal("no disassembler", ex.Message);
            Assert.Contains(logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Error
                    && entry.Message.Contains("IL diff failed", StringComparison.Ordinal)
                    && entry.Message.Contains("InvalidOperationException", StringComparison.Ordinal));
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenILMismatchAndSemanticAnalysisDisabled_DoesNotAnalyze()
        {
            // When ShouldIncludeAssemblySemanticChangesInReport is false, semantic analysis
            // should be skipped even on ILMismatch.
            // ShouldIncludeAssemblySemanticChangesInReport が false の場合、ILMismatch でも
            // セマンティック分析をスキップすべき。
            const string relativePath = "no-semantic.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffResult = (AreEqual: false, DisassemblerLabel: "test-tool")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.ShouldIncludeAssemblySemanticChangesInReport = false);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.ILMismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            Assert.False(resultLists.FileRelativePathToAssemblySemanticChanges.ContainsKey(relativePath));
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenILDiffReturnsNotEqual_RecordsILMismatch()
        {
            // When IL comparison returns not-equal, DiffDetail should be ILMismatch and
            // the method should return false.
            // IL 比較が不一致を返した場合、DiffDetail は ILMismatch で、メソッドは false を返すべき。
            const string relativePath = "changed.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffResult = (AreEqual: false, DisassemblerLabel: "test-disasm v2.0")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.ShouldIncludeAssemblySemanticChangesInReport = false);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.False(areEqual);
            Assert.Equal(FileDiffResultLists.DiffDetailResult.ILMismatch,
                resultLists.FileRelativePathToDiffDetailDictionary[relativePath]);
            // Disassembler label should be recorded / 逆アセンブララベルが記録されるべき
            Assert.Equal("test-disasm v2.0",
                resultLists.FileRelativePathToIlDisassemblerLabelDictionary[relativePath]);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenILMatchReturnsTrue_ReturnsTrue()
        {
            // Verify the return value path: when IL comparison returns equal, the method
            // must return true (not fall through to text comparison).
            // IL 比較が一致を返した場合、メソッドは true を返し（テキスト比較にフォールスルーしない）。
            const string relativePath = "il-match-return.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffResult = (AreEqual: true, DisassemblerLabel: "dotnet-ildasm v1.0")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.True(areEqual);
            // Must not fall through to text or binary comparison
            // テキストやバイナリ比較にフォールスルーしてはいけない
            Assert.Empty(fileComparisonService.TextDiffCalls);
            Assert.Empty(fileComparisonService.ReadChunkCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenILMismatchReturnsFalse_ReturnsFalse()
        {
            // Verify the return value path: when IL comparison returns not-equal, the method
            // must return false (not fall through to text comparison).
            // IL 比較が不一致を返した場合、メソッドは false を返し（テキスト比較にフォールスルーしない）。
            const string relativePath = "il-mismatch-return.dll";
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffResult = (AreEqual: false, DisassemblerLabel: "test-tool")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.ShouldIncludeAssemblySemanticChangesInReport = false);

            var areEqual = await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            Assert.False(areEqual);
            Assert.Empty(fileComparisonService.TextDiffCalls);
        }

        [Fact]
        public async Task FilesAreEqualAsync_WhenDotNetDetectionFailure_LogsWarningWithException()
        {
            // Verify the warning log includes the exception from the detection failure.
            // 検出失敗からの例外が警告ログに含まれることを検証。
            const string relativePath = "detect-ex.dll";
            var detectionException = new IOException("bad PE header");
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(
                    DotNetExecutableDetectionStatus.Failed,
                    detectionException)
            };
            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            await service.FilesAreEqualAsync(relativePath, maxParallel: 1);

            // Warning should contain the detection exception
            // 警告には検出例外が含まれるべき
            var warningEntry = Assert.Single(logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Warning
                    && entry.Message.Contains("Failed to detect", StringComparison.Ordinal));
            Assert.Same(detectionException, warningEntry.Exception);
            // IL diff should not be attempted / IL 差分は試行されないべき
            Assert.Empty(ilOutputService.DiffCalls);
        }

        [Fact]
        public async Task PrecomputeAsync_NullArgument_ThrowsArgumentNullException()
        {
            // PrecomputeAsync should validate its filesAbsolutePath argument.
            // PrecomputeAsync は filesAbsolutePath 引数をバリデーションすべき。
            var ilOutputService = new FakeILOutputService();
            var fileComparisonService = new FakeFileComparisonService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger);

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => service.PrecomputeAsync(null!, maxParallel: 1));
        }

        [Fact]
        public async Task PrecomputeAsync_WhenSkipILIsTrue_ReturnsCompletedTask()
        {
            // When SkipIL is true, PrecomputeAsync should return immediately (Task.CompletedTask).
            // SkipIL が true の場合、PrecomputeAsync は即座に戻るべき（Task.CompletedTask）。
            var ilOutputService = new FakeILOutputService();
            var fileComparisonService = new FakeFileComparisonService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.SkipIL = true);

            var task = service.PrecomputeAsync(new[] { "/virtual/old/a.dll", "/virtual/old/b.dll" }, maxParallel: 4);

            // Task should be completed synchronously / タスクは同期的に完了しているべき
            Assert.True(task.IsCompleted);
            await task;
            Assert.Equal(0, ilOutputService.PrecomputeCallCount);
        }

        [Fact]
        public async Task FilesAreEqualAsync_SemanticAnalysisCacheHit_WhenSameHashPairReused()
        {
            // When two different relative paths resolve to assemblies with the same SHA256 hashes,
            // the second analysis should be served from the in-memory cache.
            // 異なる相対パスが同じ SHA256 ハッシュを持つアセンブリに解決される場合、
            // 2 回目の解析はインメモリキャッシュから提供されるべき。
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                UseHashHexOverride = true,
                Hash1HexOverride = "aaa" + new string('0', 61),
                Hash2HexOverride = "bbb" + new string('0', 61),
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffResult = (AreEqual: false, DisassemblerLabel: "test-tool")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.ShouldIncludeAssemblySemanticChangesInReport = true);

            // First call — cache miss (analysis runs, returns null for non-existent files)
            // 1 回目 — キャッシュミス（解析実行、存在しないファイルのため null を返す）
            await service.FilesAreEqualAsync("first.dll", maxParallel: 1);

            // Second call with same hash pair — should hit cache
            // 同じハッシュペアの 2 回目 — キャッシュヒットすべき
            await service.FilesAreEqualAsync("second.dll", maxParallel: 1);

            Assert.Contains(logger.Messages, m => m.Contains("Semantic analysis cache hit") && m.Contains("second.dll"));
        }

        [Fact]
        public async Task PrecomputeAsync_WhenSkipILIsFalse_PassesFilesAndMaxParallel()
        {
            // When SkipIL is false, PrecomputeAsync delegates to ILOutputService.PrecomputeAsync.
            // SkipIL が false の場合、PrecomputeAsync は ILOutputService.PrecomputeAsync に委譲する。
            var ilOutputService = new FakeILOutputService();
            var fileComparisonService = new FakeFileComparisonService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config => config.SkipIL = false);

            var files = new[] { "/virtual/old/x.dll", "/virtual/old/y.dll", "/virtual/old/z.dll" };
            await service.PrecomputeAsync(files, maxParallel: 3);

            Assert.True(ilOutputService.PrecomputeCallCount > 0);
        }
    }
}
