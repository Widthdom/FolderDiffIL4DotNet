using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FolderDiffIL4DotNet.Core.Text;

namespace FolderDiffIL4DotNet.Benchmarks
{
    /// <summary>
    /// Benchmarks for IL-related operations: sanitization, encoding detection, and line filtering.
    /// IL 関連操作のベンチマーク: サニタイズ、エンコーディング検出、行フィルタリング。
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class ILComparisonBenchmarks
    {
        private string _longPath = null!;
        private string _unicodePath = null!;
        private string _shortPath = null!;

        [GlobalSetup]
        public void Setup()
        {
            _shortPath = "bin/Release/net8.0/MyApp.dll";
            _longPath = string.Join("/", Enumerable.Range(0, 20).Select(i => $"folder_{i}")) + "/MyAssembly.dll";
            _unicodePath = "ビルド出力/リリース/アプリケーション.dll";
        }

        [Benchmark]
        public string Sanitize_ShortPath()
        {
            return TextSanitizer.Sanitize(_shortPath);
        }

        [Benchmark]
        public string Sanitize_LongPath()
        {
            return TextSanitizer.Sanitize(_longPath);
        }

        [Benchmark]
        public string Sanitize_UnicodePath()
        {
            return TextSanitizer.Sanitize(_unicodePath);
        }

        [Benchmark]
        public int TextDiffer_IdenticalLargeFile()
        {
            // Identical files should be fast (no diff to compute)
            // 同一ファイルは高速であるべき（差分計算なし）
            var lines = Enumerable.Range(1, 50000).Select(i => $"  IL_{i:X8}:  nop").ToArray();
            var result = TextDiffer.Compute(lines, lines, contextLines: 0, maxOutputLines: 100, maxEditDistance: 100);
            return result.Count;
        }

        [Benchmark]
        public int TextDiffer_CompletelyDifferentSmallFiles()
        {
            // Worst case: completely different content triggers max edit distance
            // 最悪ケース: 完全に異なる内容で最大編集距離に到達
            var old = Enumerable.Range(1, 100).Select(i => $"old line {i}").ToArray();
            var @new = Enumerable.Range(1, 100).Select(i => $"new line {i}").ToArray();
            var result = TextDiffer.Compute(old, @new, contextLines: 3, maxOutputLines: 10000, maxEditDistance: 500);
            return result.Count;
        }
    }
}
