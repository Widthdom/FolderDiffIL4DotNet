using BenchmarkDotNet.Running;

namespace FolderDiffIL4DotNet.Benchmarks
{
    /// <summary>
    /// Entry point for running performance benchmarks.
    /// パフォーマンスベンチマーク実行のエントリポイント。
    /// </summary>
    /// <remarks>
    /// Usage (run from repository root):
    ///   dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks
    ///
    /// To run a specific benchmark class:
    ///   dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks -- --filter *TextDiffer*
    /// </remarks>
    public static class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
