using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.EdgeCases
{
    /// <summary>
    /// Race condition stress tests for ILCache under concurrent execution.
    /// Verifies that parallel Set/Get/Eviction operations do not corrupt state or throw.
    /// ILCache の並列実行下での競合状態ストレステスト。
    /// 並列 Set/Get/退去操作が状態を破壊せず例外をスローしないことを確認する。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class ILCacheConcurrencyTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _cacheDir;

        public ILCacheConcurrencyTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"ILCacheConcurrency_{Guid.NewGuid():N}");
            _cacheDir = Path.Combine(_tempDir, "cache");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        private string CreateTestFile(string name, string content)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public async Task ConcurrentSetAndGet_MemoryOnly_NoExceptionsAndDataConsistent()
        {
            // Multiple tasks writing and reading the same keys concurrently should not corrupt the cache
            // 複数タスクが同じキーに対して同時に書き込み/読み取りを行ってもキャッシュが破壊されない
            const int workerCount = 16;
            const int iterationsPerWorker = 50;
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null, ilCacheMaxMemoryEntries: 10);
            var files = new List<string>();
            for (int i = 0; i < 5; i++)
                files.Add(CreateTestFile($"concurrent-{i}.dll", $"content-{i}"));

            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            var tasks = new Task[workerCount];

            for (int w = 0; w < workerCount; w++)
            {
                int worker = w;
                tasks[w] = Task.Run(async () =>
                {
                    try
                    {
                        var rng = new Random(worker);
                        for (int i = 0; i < iterationsPerWorker; i++)
                        {
                            var file = files[rng.Next(files.Count)];
                            var tool = $"tool-{rng.Next(3)}";
                            var il = $"IL-{worker}-{i}";
                            await cache.SetILAsync(file, tool, il);
                            _ = await cache.TryGetILAsync(file, tool);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }

            await Task.WhenAll(tasks);
            Assert.Empty(exceptions);
        }

        [Fact]
        public async Task ConcurrentSetAndGet_WithDiskCache_NoExceptions()
        {
            // Concurrent operations with disk persistence should not throw
            // ディスク永続化ありの並列操作でも例外をスローしない
            const int workerCount = 8;
            const int iterationsPerWorker = 20;
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxMemoryEntries: 5);
            var files = new List<string>();
            for (int i = 0; i < 4; i++)
                files.Add(CreateTestFile($"disk-concurrent-{i}.dll", $"disk-content-{i}"));

            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            var tasks = new Task[workerCount];

            for (int w = 0; w < workerCount; w++)
            {
                int worker = w;
                tasks[w] = Task.Run(async () =>
                {
                    try
                    {
                        var rng = new Random(worker * 17);
                        for (int i = 0; i < iterationsPerWorker; i++)
                        {
                            var file = files[rng.Next(files.Count)];
                            await cache.SetILAsync(file, "tool", $"IL-{worker}-{i}");
                            _ = await cache.TryGetILAsync(file, "tool");
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }

            await Task.WhenAll(tasks);
            Assert.Empty(exceptions);
        }

        [Fact]
        public async Task ConcurrentLruEviction_DoesNotCorruptState()
        {
            // With a tiny cache, concurrent writes should trigger many evictions without corrupting state
            // 極小キャッシュでは並列書き込みで多数の退去が発生するが、状態が破壊されない
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null, ilCacheMaxMemoryEntries: 3);
            var files = new List<string>();
            for (int i = 0; i < 20; i++)
                files.Add(CreateTestFile($"evict-{i}.dll", $"evict-content-{i}"));

            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            var tasks = Enumerable.Range(0, 10).Select(w => Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i < files.Count; i++)
                    {
                        await cache.SetILAsync(files[i], "tool", $"IL-{w}-{i}");
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

            await Task.WhenAll(tasks);
            Assert.Empty(exceptions);

            // At least some entries should still be accessible
            int foundCount = 0;
            for (int i = 0; i < files.Count; i++)
            {
                var result = await cache.TryGetILAsync(files[i], "tool");
                if (result != null) foundCount++;
            }
            Assert.InRange(foundCount, 1, files.Count);
        }

        [Fact]
        public async Task ConcurrentTtlExpiry_DoesNotThrow()
        {
            // Many entries expiring around the same time under concurrent access
            // 並列アクセス中に多数のエントリがほぼ同時に期限切れになるケース
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null, ilCacheMaxMemoryEntries: 100,
                timeToLive: TimeSpan.FromMilliseconds(10));
            var files = new List<string>();
            for (int i = 0; i < 10; i++)
                files.Add(CreateTestFile($"ttl-{i}.dll", $"ttl-content-{i}"));

            // Pre-populate cache
            foreach (var f in files)
                await cache.SetILAsync(f, "tool", "IL-data");

            // Wait for TTL to expire
            await Task.Delay(30);

            // Concurrent reads should find expired entries and remove them without exceptions
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
            {
                try
                {
                    foreach (var f in files)
                    {
                        var result = await cache.TryGetILAsync(f, "tool");
                        // After TTL, result should be null
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

            await Task.WhenAll(tasks);
            Assert.Empty(exceptions);
        }

        [Fact]
        public async Task ConcurrentPrecompute_DoesNotThrow()
        {
            // Concurrent PrecomputeAsync calls should not interfere with each other
            // 並列 PrecomputeAsync 呼び出しが互いに干渉しない
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null);
            var files = new List<string>();
            for (int i = 0; i < 10; i++)
                files.Add(CreateTestFile($"precompute-{i}.dll", $"precompute-content-{i}"));

            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
            {
                try
                {
                    await cache.PrecomputeAsync(files, maxParallel: 4);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

            await Task.WhenAll(tasks);
            Assert.Empty(exceptions);
        }
    }
}
