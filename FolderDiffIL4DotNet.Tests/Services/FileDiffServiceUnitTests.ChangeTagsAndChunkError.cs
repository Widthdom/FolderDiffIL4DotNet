using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    // Change tag classification and parallel chunk error handling tests for FileDiffService.
    // FileDiffService の変更タグ分類および並列チャンクエラーハンドリングテスト。
    public sealed partial class FileDiffServiceUnitTests
    {
        /// <summary>
        /// Verifies that TryClassifyChangeTags is exercised when an IL mismatch triggers
        /// semantic analysis with the report flag enabled.
        /// レポートフラグ有効時に IL 不一致がセマンティック分析を発火し、
        /// TryClassifyChangeTags が実行されることを検証する。
        /// </summary>
        [Fact]
        public async Task FilesAreEqualAsync_WhenILMismatchWithSemanticFlag_RecordsDiffDetail()
        {
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffResult = (false, "dotnet-ildasm (version: 0.12.0)")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config =>
                {
                    config.ShouldIncludeAssemblySemanticChangesInReport = true;
                });

            bool result = await service.FilesAreEqualAsync("lib.dll");

            Assert.False(result);
            Assert.True(resultLists.FileRelativePathToDiffDetailDictionary.ContainsKey("lib.dll"));
            Assert.Equal(FileDiffResultLists.DiffDetailResult.ILMismatch,
                resultLists.FileRelativePathToDiffDetailDictionary["lib.dll"]);
        }

        /// <summary>
        /// Verifies that ReadChunkException during parallel text comparison is caught and
        /// logged as a warning, not propagated as an unhandled exception.
        /// 並列テキスト比較中の ReadChunkException がキャッチされ警告ログに記録され、
        /// 未処理例外として伝播しないことを検証する。
        /// </summary>
        [Fact]
        public async Task FilesAreEqualAsync_WhenReadChunkThrows_FallsBackToSequentialDiff()
        {
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.NotDotNetExecutable),
                TextDiffResult = false,
                ReadChunkException = new IOException("Simulated disk read failure")
            };
            // Set large file content (> threshold) so parallel path is attempted
            // 並列パスが試行されるよう閾値超のファイルコンテンツを設定
            string largeContent = new string('A', 512 * 1024);
            fileComparisonService.SetFileContent("/virtual/old/data.txt", largeContent);
            fileComparisonService.SetFileContent("/virtual/new/data.txt", largeContent);

            var ilOutputService = new FakeILOutputService();
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config =>
                {
                    config.TextDiffParallelThresholdKilobytes = 1;
                    config.TextDiffChunkSizeKilobytes = 64;
                    config.TextDiffParallelMemoryLimitMegabytes = 100;
                });

            bool result = await service.FilesAreEqualAsync("data.txt");

            // ReadChunkException causes IOException, caught by outer handler,
            // falls back to sequential DiffTextFilesAsync which returns TextDiffResult (false)
            // ReadChunkException は IOException を引き起こし外側ハンドラでキャッチ、逐次比較にフォールバック
            Assert.False(result);

            // Sequential fallback should have been called (TextDiffCalls populated)
            // 逐次フォールバックが呼ばれたはず（TextDiffCalls に記録される）
            Assert.True(fileComparisonService.TextDiffCalls.Count > 0,
                "Expected sequential text diff fallback after ReadChunkException");
        }

        /// <summary>
        /// Verifies that when semantic analysis is enabled but the analyzed files don't exist
        /// on disk (so analysis returns empty), no change tags are populated.
        /// セマンティック分析が有効だが解析対象ファイルがディスクに存在しない場合（分析は空を返す）、
        /// 変更タグが設定されないことを検証する。
        /// </summary>
        [Fact]
        public async Task FilesAreEqualAsync_WhenSemanticChangesEmpty_NoChangeTagsPopulated()
        {
            var fileComparisonService = new FakeFileComparisonService
            {
                HashResult = false,
                DotNetDetectionResult = new DotNetExecutableDetectionResult(DotNetExecutableDetectionStatus.DotNetExecutable)
            };
            var ilOutputService = new FakeILOutputService
            {
                DiffResult = (false, "dotnet-ildasm (version: 0.12.0)")
            };
            var resultLists = new FileDiffResultLists();
            var logger = new TestLogger();
            var service = CreateService(fileComparisonService, ilOutputService, resultLists, logger,
                configure: config =>
                {
                    config.ShouldIncludeAssemblySemanticChangesInReport = true;
                });

            await service.FilesAreEqualAsync("empty.dll");

            // No change tags because semantic analysis returned empty result (non-existent files)
            // セマンティック分析が空結果（存在しないファイル）を返したため変更タグなし
            Assert.False(resultLists.FileRelativePathToChangeTags.ContainsKey("empty.dll"));
        }
    }
}
