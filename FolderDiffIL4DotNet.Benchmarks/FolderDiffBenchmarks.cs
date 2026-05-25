using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FolderDiffIL4DotNet.Core.IO;

namespace FolderDiffIL4DotNet.Benchmarks
{
    /// <summary>
    /// Benchmarks for folder enumeration and file hashing across various directory sizes.
    /// フォルダ列挙およびファイルハッシュ計算のベンチマーク（さまざまなディレクトリサイズ対象）。
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class FolderDiffBenchmarks
    {
        private string _smallDirPath = null!;
        private string _mediumDirPath = null!;
        private string _largeDirPath = null!;

        [GlobalSetup]
        public void Setup()
        {
            _smallDirPath = CreateTempFolderWithFiles("bench-small", fileCount: 100, fileSizeBytes: 1024);
            _mediumDirPath = CreateTempFolderWithFiles("bench-medium", fileCount: 1000, fileSizeBytes: 4096);
            _largeDirPath = CreateTempFolderWithFiles("bench-large", fileCount: 10000, fileSizeBytes: 512);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            TryDeleteDir(_smallDirPath);
            TryDeleteDir(_mediumDirPath);
            TryDeleteDir(_largeDirPath);
        }

        [Benchmark]
        public int EnumerateFiles_100()
        {
            return Directory.EnumerateFiles(_smallDirPath, "*", SearchOption.AllDirectories).Count();
        }

        [Benchmark]
        public int EnumerateFiles_1000()
        {
            return Directory.EnumerateFiles(_mediumDirPath, "*", SearchOption.AllDirectories).Count();
        }

        [Benchmark]
        public int EnumerateFiles_10000()
        {
            return Directory.EnumerateFiles(_largeDirPath, "*", SearchOption.AllDirectories).Count();
        }

        [Benchmark]
        public bool HashCompare_SmallFile()
        {
            var files = Directory.GetFiles(_smallDirPath).Take(2).ToArray();
            if (files.Length < 2) return false;
            return FileComparer.ComputeFileSha256Hex(files[0]) == FileComparer.ComputeFileSha256Hex(files[1]);
        }

        private static string CreateTempFolderWithFiles(string prefix, int fileCount, int fileSizeBytes)
        {
            var dir = Path.Combine(Path.GetTempPath(), $"fd-bench-{prefix}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            var content = new byte[fileSizeBytes];
            var rng = new Random(42);
            for (int i = 0; i < fileCount; i++)
            {
                rng.NextBytes(content);
                // Create a few subdirectories to simulate realistic folder structures
                string subDir = i % 10 == 0 ? Path.Combine(dir, $"sub{i / 100}") : dir;
                Directory.CreateDirectory(subDir);
                File.WriteAllBytes(Path.Combine(subDir, $"file_{i:D5}.bin"), content);
            }
            return dir;
        }

        private static void TryDeleteDir(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or DirectoryNotFoundException
                or NotSupportedException
                or ArgumentException)
            {
                // Best-effort cleanup: leaving a stray temp benchmark dir is preferable to
                // hiding a non-IO programmer error.
                // ベストエフォートな後片付け: 一時的なベンチマーク用ディレクトリが残るよりも
                // IO 以外のプログラマエラーを隠蔽する方が害が大きいため、IO 系のみ許容する。
            }
        }
    }
}
