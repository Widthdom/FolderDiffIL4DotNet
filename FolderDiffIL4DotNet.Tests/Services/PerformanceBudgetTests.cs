using System;
using System.Diagnostics;
using System.Linq;
using FolderDiffIL4DotNet.Core.Text;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Performance budget tests that verify key operations complete within expected time bounds.
    /// These are NOT micro-benchmarks — they are coarse guardrails to catch severe regressions.
    /// Run with <c>dotnet test --filter "Category=Performance"</c>.
    /// <para>
    /// 主要操作が期待される時間内に完了することを検証するパフォーマンスバジェットテスト。
    /// マイクロベンチマークではなく、重大な回帰を検出するための粗いガードレール。
    /// <c>dotnet test --filter "Category=Performance"</c> で実行。
    /// </para>
    /// </summary>
    [Trait("Category", "Performance")]
    public sealed class PerformanceBudgetTests
    {
        /// <summary>
        /// TextDiffer on identical 50K lines should complete in under 500ms.
        /// 同一の5万行に対する TextDiffer は 500ms 以内に完了すること。
        /// </summary>
        [Fact]
        public void TextDiffer_IdenticalLargeInput_CompletesWithinBudget()
        {
            var lines = Enumerable.Range(1, 50000).Select(i => $"  IL_{i:X8}:  nop").ToArray();

            var sw = Stopwatch.StartNew();
            var result = TextDiffer.Compute(lines, lines, contextLines: 0, maxOutputLines: 100, maxEditDistance: 100);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 500,
                $"TextDiffer on 50K identical lines took {sw.ElapsedMilliseconds}ms (budget: 500ms)");
            Assert.Empty(result.Where(d => d.Kind == TextDiffer.Added || d.Kind == TextDiffer.Removed));
        }

        /// <summary>
        /// TextDiffer on 10K lines with 20 changes should complete in under 2000ms.
        /// 20箇所の変更がある1万行に対する TextDiffer は 2000ms 以内に完了すること。
        /// </summary>
        [Fact]
        public void TextDiffer_MediumInputWithChanges_CompletesWithinBudget()
        {
            var old = Enumerable.Range(1, 10000).Select(i => $"  IL_{i:X4}:  ldarg.0").ToArray();
            var @new = old.ToArray();
            var rng = new Random(42);
            for (int i = 0; i < 20; i++)
            {
                int idx = rng.Next(@new.Length);
                @new[idx] = $"  IL_{idx:X4}:  ldarg.1  // changed";
            }

            var sw = Stopwatch.StartNew();
            TextDiffer.Compute(old, @new, contextLines: 3, maxOutputLines: 10000, maxEditDistance: 4000);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 2000,
                $"TextDiffer on 10K lines with 20 changes took {sw.ElapsedMilliseconds}ms (budget: 2000ms)");
        }

        /// <summary>
        /// TextSanitizer on 100K iterations of a typical path should complete in under 500ms.
        /// 典型的なパスの10万回反復に対する TextSanitizer は 500ms 以内に完了すること。
        /// </summary>
        [Fact]
        public void TextSanitizer_BulkSanitize_CompletesWithinBudget()
        {
            var path = "C:\\Users\\dev\\source\\repos\\MyProject\\bin\\Release\\net8.0\\MyAssembly.dll";

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100_000; i++)
            {
                TextSanitizer.Sanitize(path);
            }
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 500,
                $"TextSanitizer.Sanitize 100K iterations took {sw.ElapsedMilliseconds}ms (budget: 500ms)");
        }

        /// <summary>
        /// FileDiffResultLists with 10K files should compute statistics instantly.
        /// 1万ファイルの FileDiffResultLists は統計を即座に計算できること。
        /// </summary>
        [Fact]
        public void FileDiffResultLists_LargeDataSet_StatisticsComputedQuickly()
        {
            var resultLists = new FileDiffResultLists();
            for (int i = 0; i < 10000; i++)
            {
                resultLists.UnchangedFiles.Add($"file_{i:D5}.dll");
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                _ = resultLists.SummaryStatistics;
            }
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 100,
                $"SummaryStatistics 1K iterations on 10K files took {sw.ElapsedMilliseconds}ms (budget: 100ms)");
        }
    }
}
