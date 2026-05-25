using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FolderDiffIL4DotNet.Core.Text;

namespace FolderDiffIL4DotNet.Benchmarks
{
    /// <summary>
    /// Benchmarks for <see cref="TextDiffer.Compute"/> covering small, medium, and large IL-like files.
    /// <see cref="TextDiffer.Compute"/> のベンチマーク（小・中・大規模の IL 風ファイル対象）。
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class TextDifferBenchmarks
    {
        private string[] _smallOld = null!;
        private string[] _smallNew = null!;
        private string[] _mediumOld = null!;
        private string[] _mediumNew = null!;
        private string[] _largeOld = null!;
        private string[] _largeNew = null!;

        [GlobalSetup]
        public void Setup()
        {
            // Small: 100 lines, 5 changed
            _smallOld = Enumerable.Range(1, 100).Select(i => $".method public hidebysig instance void Method{i}() cil managed").ToArray();
            _smallNew = _smallOld.ToArray();
            _smallNew[10] = ".method public hidebysig instance void Method10_v2() cil managed";
            _smallNew[30] = ".method public hidebysig instance void Method30_v2() cil managed";
            _smallNew[50] = ".method public hidebysig instance void Method50_v2() cil managed";
            _smallNew[70] = ".method public hidebysig instance void Method70_v2() cil managed";
            _smallNew[90] = ".method public hidebysig instance void Method90_v2() cil managed";

            // Medium: 10,000 lines, 20 changed
            _mediumOld = Enumerable.Range(1, 10000).Select(i => $"  IL_{i:X4}:  ldarg.0").ToArray();
            _mediumNew = _mediumOld.ToArray();
            var rng = new Random(42);
            for (int i = 0; i < 20; i++)
            {
                int idx = rng.Next(_mediumNew.Length);
                _mediumNew[idx] = $"  IL_{idx:X4}:  ldarg.1  // changed";
            }

            // Large: 1,000,000 lines, 10 changed (simulates large IL file with tiny diff)
            _largeOld = Enumerable.Range(1, 1_000_000).Select(i => $"  IL_{i:X8}:  nop").ToArray();
            _largeNew = _largeOld.ToArray();
            for (int i = 0; i < 10; i++)
            {
                int idx = i * 100_000;
                _largeNew[idx] = $"  IL_{idx:X8}:  ret  // patched";
            }
        }

        [Benchmark]
        public int SmallFile_5Changes()
        {
            var result = TextDiffer.Compute(_smallOld, _smallNew, contextLines: 3, maxOutputLines: 10000, maxEditDistance: 4000);
            return result.Count;
        }

        [Benchmark]
        public int MediumFile_20Changes()
        {
            var result = TextDiffer.Compute(_mediumOld, _mediumNew, contextLines: 3, maxOutputLines: 10000, maxEditDistance: 4000);
            return result.Count;
        }

        [Benchmark]
        public int LargeFile_10Changes()
        {
            var result = TextDiffer.Compute(_largeOld, _largeNew, contextLines: 3, maxOutputLines: 10000, maxEditDistance: 4000);
            return result.Count;
        }
    }
}
