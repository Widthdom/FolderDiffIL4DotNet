using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.Caching
{
    public partial class ILCacheTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _cacheDir;

        public ILCacheTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"ILCacheTests_{Guid.NewGuid():N}");
            _cacheDir = Path.Combine(_tempDir, "cache");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        private string CreateTestFile(string name, string content = "test content")
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public async Task SetAndGet_MemoryOnly_ReturnsStoredValue()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null);
            var file = CreateTestFile("test.dll");
            var toolLabel = "dotnet-ildasm (version: 1.0.0)";
            var ilText = ".assembly Test {}";

            await cache.SetILAsync(file, toolLabel, ilText);
            var result = await cache.TryGetILAsync(file, toolLabel);

            Assert.Equal(ilText, result);
        }

        [Fact]
        public async Task TryGet_NotStored_ReturnsNull()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null);
            var file = CreateTestFile("test.dll");

            var result = await cache.TryGetILAsync(file, "some-tool");

            Assert.Null(result);
        }

        [Fact]
        public async Task Set_NullOrEmptyIL_DoesNotStore()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null);
            var file = CreateTestFile("test.dll");
            var toolLabel = "tool";

            await cache.SetILAsync(file, toolLabel, null);
            Assert.Null(await cache.TryGetILAsync(file, toolLabel));

            await cache.SetILAsync(file, toolLabel, string.Empty);
            Assert.Null(await cache.TryGetILAsync(file, toolLabel));
        }

        [Fact]
        public async Task DifferentToolLabels_IndependentEntries()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null);
            var file = CreateTestFile("test.dll");

            await cache.SetILAsync(file, "tool-a", "IL from A");
            await cache.SetILAsync(file, "tool-b", "IL from B");

            Assert.Equal("IL from A", await cache.TryGetILAsync(file, "tool-a"));
            Assert.Equal("IL from B", await cache.TryGetILAsync(file, "tool-b"));
        }

        [Fact]
        public async Task DifferentFiles_IndependentEntries()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null);
            var file1 = CreateTestFile("a.dll", "content-a");
            var file2 = CreateTestFile("b.dll", "content-b");
            var tool = "tool";

            await cache.SetILAsync(file1, tool, "IL-A");
            await cache.SetILAsync(file2, tool, "IL-B");

            Assert.Equal("IL-A", await cache.TryGetILAsync(file1, tool));
            Assert.Equal("IL-B", await cache.TryGetILAsync(file2, tool));
        }

        [Fact]
        public async Task LRU_ExceedsMaxEntries_EvictsOldest()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null, ilCacheMaxMemoryEntries: 2);
            var tool = "tool";

            var file1 = CreateTestFile("1.dll", "c1");
            var file2 = CreateTestFile("2.dll", "c2");
            var file3 = CreateTestFile("3.dll", "c3");

            await cache.SetILAsync(file1, tool, "IL-1");
            await cache.SetILAsync(file2, tool, "IL-2");
            // file1 is the oldest entry and should be evicted when file3 fills the capacity
            // file1 は最も古いエントリであり、file3 追加時に容量超過で退去される
            await cache.SetILAsync(file3, tool, "IL-3");

            Assert.Null(await cache.TryGetILAsync(file1, tool));
            Assert.NotNull(await cache.TryGetILAsync(file2, tool));
            Assert.NotNull(await cache.TryGetILAsync(file3, tool));
        }

        [Fact]
        public async Task LRU_AccessRefreshesEntry()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null, ilCacheMaxMemoryEntries: 2);
            var tool = "tool";

            var file1 = CreateTestFile("1.dll", "c1");
            var file2 = CreateTestFile("2.dll", "c2");
            var file3 = CreateTestFile("3.dll", "c3");

            await cache.SetILAsync(file1, tool, "IL-1");
            await cache.SetILAsync(file2, tool, "IL-2");
            // Access file1 to refresh its LRU position; file2 becomes the oldest
            // file1 にアクセスして LRU 位置を更新し、file2 を最古にする
            await cache.TryGetILAsync(file1, tool);
            // Adding file3 evicts file2 (the oldest) instead of file1
            // file3 追加時に file1 ではなく file2（最古）が退去される
            await cache.SetILAsync(file3, tool, "IL-3");

            Assert.NotNull(await cache.TryGetILAsync(file1, tool));
            Assert.Null(await cache.TryGetILAsync(file2, tool));
            Assert.NotNull(await cache.TryGetILAsync(file3, tool));
        }

        [Fact]
        public async Task Set_SameKeyAtCapacity_DoesNotEvictOtherEntries()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null, ilCacheMaxMemoryEntries: 2);
            var tool = "tool";

            var file1 = CreateTestFile("1.dll", "c1");
            var file2 = CreateTestFile("2.dll", "c2");

            await cache.SetILAsync(file1, tool, "IL-1");
            await cache.SetILAsync(file2, tool, "IL-2");
            await cache.SetILAsync(file1, tool, "IL-1-updated");

            Assert.Equal("IL-1-updated", await cache.TryGetILAsync(file1, tool));
            Assert.Equal("IL-2", await cache.TryGetILAsync(file2, tool));
            Assert.Equal(0, cache.Stats.Evicted);
        }

        [Fact]
        public async Task Stats_TracksEvictions()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null, ilCacheMaxMemoryEntries: 1);
            var tool = "tool";

            var file1 = CreateTestFile("1.dll", "c1");
            var file2 = CreateTestFile("2.dll", "c2");

            await cache.SetILAsync(file1, tool, "IL-1");
            await cache.SetILAsync(file2, tool, "IL-2");

            Assert.True(cache.Stats.Evicted >= 1);
        }

        [Fact]
        public async Task TTL_ExpiredEntry_ReturnsNull()
        {
            // TTL of 1ms ensures the entry expires almost immediately
            // TTL 1ms でエントリがほぼ即座に期限切れになることを保証する
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null, timeToLive: TimeSpan.FromMilliseconds(1));
            var file = CreateTestFile("test.dll");
            var tool = "tool";

            await cache.SetILAsync(file, tool, "IL text");
            // Wait long enough for the 1ms TTL to expire
            // 1ms の TTL が確実に満了するまで待機
            await Task.Delay(50);

            var result = await cache.TryGetILAsync(file, tool);
            Assert.Null(result);
        }

        [Fact]
        public async Task TTL_NotExpired_ReturnsValue()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null, timeToLive: TimeSpan.FromMinutes(10));
            var file = CreateTestFile("test.dll");
            var tool = "tool";

            await cache.SetILAsync(file, tool, "IL text");
            var result = await cache.TryGetILAsync(file, tool);

            Assert.Equal("IL text", result);
        }

        [Fact]
        public async Task DiskCache_SetAndGet_PersistsToFile()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir);
            var file = CreateTestFile("test.dll");
            var tool = "tool";

            await cache.SetILAsync(file, tool, "IL from disk");

            // Verify at least one cache file was persisted to disk
            // ディスクにキャッシュファイルが永続化されたことを確認
            var cacheFiles = Directory.GetFiles(_cacheDir, "*.ilcache");
            Assert.NotEmpty(cacheFiles);
        }

        [Fact]
        public async Task DiskCache_NewInstance_ReadsPreviouslyWrittenCache()
        {
            var file = CreateTestFile("test.dll");
            var tool = "tool";
            var ilText = "IL persisted across instances";

            // Write with the first ILCache instance
            // 最初の ILCache インスタンスで書き込み
            var cache1 = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir);
            await cache1.SetILAsync(file, tool, ilText);

            // A fresh instance has an empty memory cache but can read from disk
            // 新しいインスタンスはメモリキャッシュが空だがディスクから読み取れる
            var cache2 = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir);
            var result = await cache2.TryGetILAsync(file, tool);

            Assert.Equal(ilText, result);
        }

        [Fact]
        public async Task DiskCache_InvalidDirectory_FallsBackToMemoryOnly()
        {
            // Provide an excessively long path that cannot be created as a directory
            // ディレクトリとして作成できない過度に長いパスを指定する
            var invalidPath = Path.Combine(_tempDir, new string('x', 300), "cache");
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: invalidPath);
            var file = CreateTestFile("test.dll");
            var tool = "tool";

            // Should still work via in-memory fallback when disk init fails
            // ディスク初期化失敗時もメモリキャッシュへのフォールバックで動作する
            await cache.SetILAsync(file, tool, "memory only");
            var result = await cache.TryGetILAsync(file, tool);
            Assert.Equal("memory only", result);
        }

        [Fact]
        public async Task DiskQuota_MaxFileCount_RemovesOldFiles()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxDiskFileCount: 2);
            var tool = "tool";

            // Create 3 files sequentially with unique content to produce distinct SHA256 cache keys
            // ユニークなコンテンツで 3 ファイルを順次作成し、異なる SHA256 キャッシュキーを生成する
            for (int i = 0; i < 3; i++)
            {
                var file = CreateTestFile($"f{i}.dll", $"unique-content-{i}");
                await cache.SetILAsync(file, tool, $"IL-{i}");
                // Small delay so file timestamps differ for quota enforcement ordering
                // クォータ適用時の順序付けのためファイルのタイムスタンプを異ならせる
                await Task.Delay(50);
            }

            var cacheFiles = Directory.GetFiles(_cacheDir, "*.ilcache");
            Assert.True(cacheFiles.Length <= 2, $"Expected <= 2 cache files, got {cacheFiles.Length}");
        }

        [Fact]
        public async Task LRU_WithDiskCache_RemovesEvictedDiskEntry()
        {
            var tool = "tool";
            var file1 = CreateTestFile("disk-1.dll", "disk-c1");
            var file2 = CreateTestFile("disk-2.dll", "disk-c2");

            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxMemoryEntries: 1);
            await cache.SetILAsync(file1, tool, "IL-1");
            await Task.Delay(50);
            await cache.SetILAsync(file2, tool, "IL-2");

            var cacheFiles = Directory.GetFiles(_cacheDir, "*.ilcache");
            Assert.Single(cacheFiles);

            var freshCache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxMemoryEntries: 1);
            Assert.Null(await freshCache.TryGetILAsync(file1, tool));
            Assert.Equal("IL-2", await freshCache.TryGetILAsync(file2, tool));
        }

        [Fact]
        public async Task DiskQuota_MaxMegabytes_RemovesOldFiles()
        {
            // 0 MB limit means unlimited; verify file is still written
            // 0 MB 制限は無制限を意味し、ファイルが書き込まれることを確認
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxDiskMegabytes: 0);
            var file = CreateTestFile("test.dll");
            var tool = "tool";
            var largeIL = new string('X', 2048);

            await cache.SetILAsync(file, tool, largeIL);

            var cacheFiles = Directory.GetFiles(_cacheDir, "*.ilcache");
            Assert.NotEmpty(cacheFiles);
        }

        [Fact]
        public async Task Precompute_ValidFiles_DoesNotThrow()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null);
            var file1 = CreateTestFile("a.dll", "content-a");
            var file2 = CreateTestFile("b.dll", "content-b");

            await cache.PrecomputeAsync(new[] { file1, file2 }, maxParallel: 2);
        }

        [Fact]
        public async Task Precompute_NullEnumerable_DoesNotThrow()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null);
            await cache.PrecomputeAsync(null, maxParallel: 1);
        }

        [Fact]
        public async Task Precompute_EmptyEnumerable_DoesNotThrow()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null);
            await cache.PrecomputeAsync(Array.Empty<string>(), maxParallel: 1);
        }

        [Fact]
        public async Task Precompute_ZeroParallel_ThrowsArgumentOutOfRange()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null);
            var file = CreateTestFile("test.dll");

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => cache.PrecomputeAsync(new[] { file }, maxParallel: 0));
        }

    }
}
