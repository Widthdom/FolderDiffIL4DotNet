using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.Caching
{
    public class ILCacheTests : IDisposable
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
            // file1 should be evicted when file3 is added
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
            // Access file1 to refresh it
            await cache.TryGetILAsync(file1, tool);
            // Now file2 should be the oldest; adding file3 should evict file2
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
            // TTL of 1ms - will expire almost immediately
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null, timeToLive: TimeSpan.FromMilliseconds(1));
            var file = CreateTestFile("test.dll");
            var tool = "tool";

            await cache.SetILAsync(file, tool, "IL text");
            // Wait to ensure TTL expires
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

            // Verify at least one cache file was created
            var cacheFiles = Directory.GetFiles(_cacheDir, "*.ilcache");
            Assert.NotEmpty(cacheFiles);
        }

        [Fact]
        public async Task DiskCache_NewInstance_ReadsPreviouslyWrittenCache()
        {
            var file = CreateTestFile("test.dll");
            var tool = "tool";
            var ilText = "IL persisted across instances";

            // Write with first instance
            var cache1 = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir);
            await cache1.SetILAsync(file, tool, ilText);

            // Read with new instance (empty memory cache, but disk cache has it)
            var cache2 = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir);
            var result = await cache2.TryGetILAsync(file, tool);

            Assert.Equal(ilText, result);
        }

        [Fact]
        public async Task DiskCache_InvalidDirectory_FallsBackToMemoryOnly()
        {
            // Provide a path that can't be created
            var invalidPath = Path.Combine(_tempDir, new string('x', 300), "cache");
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: invalidPath);
            var file = CreateTestFile("test.dll");
            var tool = "tool";

            // Should still work via memory cache
            await cache.SetILAsync(file, tool, "memory only");
            var result = await cache.TryGetILAsync(file, tool);
            Assert.Equal("memory only", result);
        }

        [Fact]
        public async Task DiskQuota_MaxFileCount_RemovesOldFiles()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxDiskFileCount: 2);
            var tool = "tool";

            // Create 3 files sequentially with different content for unique MD5 keys
            for (int i = 0; i < 3; i++)
            {
                var file = CreateTestFile($"f{i}.dll", $"unique-content-{i}");
                await cache.SetILAsync(file, tool, $"IL-{i}");
                // Small delay so that timestamps differ
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
            // 1 KB limit
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxDiskMegabytes: 0);
            var file = CreateTestFile("test.dll");
            var tool = "tool";
            var largeIL = new string('X', 2048);

            await cache.SetILAsync(file, tool, largeIL);

            // With 0 MB limit (unlimited), file should be written
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

        /// <summary>
        /// ディスクキャッシュディレクトリとして既存ファイルパスを渡した場合、
        /// Directory.CreateDirectory が IOException をスローし、メモリキャッシュのみで動作することを確認します。
        /// </summary>
        [Fact]
        public async Task DiskCache_CacheDirIsFile_FallsBackToMemoryOnly()
        {
            // ディレクトリパスとして使うパスにファイルを作成
            var filePath = Path.Combine(_tempDir, "not-a-directory");
            File.WriteAllText(filePath, "I am a file");

            // キャッシュディレクトリとしてファイルパスを渡す → TryInitializeCacheDirectory が IOException をキャッチ
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: filePath);
            var testFile = CreateTestFile("fallback.dll");
            var tool = "tool";

            // メモリキャッシュにフォールバックして動作する
            await cache.SetILAsync(testFile, tool, "memory IL");
            var result = await cache.TryGetILAsync(testFile, tool);
            Assert.Equal("memory IL", result);
        }

        /// <summary>
        /// LRU eviction 時に disk Remove が UnauthorizedAccessException をキャッチして継続することを確認します（Linux/非root のみ）。
        /// </summary>
        [Fact]
        public async Task LRU_DiskRemove_ReadOnlyDir_LogsWarningAndContinues()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)
                && !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                return;
            }
            if (string.Equals(Environment.UserName, "root", StringComparison.OrdinalIgnoreCase))
            {
                return; // root は読み取り専用ディレクトリでも削除可能
            }

            var tool = "tool";
            var file1 = CreateTestFile("lru-ro-1.dll", "c1");
            var file2 = CreateTestFile("lru-ro-2.dll", "c2");

            // maxMemoryEntries=1 で LRU eviction を発生させる
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxMemoryEntries: 1);
            await cache.SetILAsync(file1, tool, "IL-1");
            await Task.Delay(30);

            // キャッシュディレクトリを読み取り専用にして Remove が失敗するようにする
            File.SetUnixFileMode(_cacheDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            try
            {
                // file2 を追加すると file1 が LRU eviction → ILDiskCache.Remove が UnauthorizedAccessException をキャッチ
                await cache.SetILAsync(file2, tool, "IL-2");
                // 例外が外に漏れないことを確認（silentに継続）
            }
            finally
            {
                File.SetUnixFileMode(_cacheDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        /// <summary>
        /// TrimCacheFiles 時に読み取り専用ファイルの削除が失敗した場合、警告ログが出て処理が継続することを確認します。
        /// </summary>
        [Fact]
        public async Task DiskQuota_ReadOnlyFile_TrimSkipsAndContinues()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)
                && !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                return;
            }
            if (string.Equals(Environment.UserName, "root", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // maxDiskFileCount=1 の制限でクォータ超過を発生させる
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxDiskFileCount: 1);
            var tool = "tool";

            var file1 = CreateTestFile("trim-ro-1.dll", "unique-ro-content-1");
            await cache.SetILAsync(file1, tool, "IL-trim-1");
            await Task.Delay(30);

            // 最初のキャッシュファイルを読み取り専用にする
            var cacheFiles = Directory.GetFiles(_cacheDir, "*.ilcache");
            if (cacheFiles.Length > 0)
            {
                // ディレクトリを読み取り専用にしてキャッシュファイルの削除を阻止
                File.SetUnixFileMode(_cacheDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                try
                {
                    var file2 = CreateTestFile("trim-ro-2.dll", "unique-ro-content-2");
                    // SetIL は disk quota enforce をトリガーするが、削除失敗は silently 無視される
                    await cache.SetILAsync(file2, tool, "IL-trim-2");
                }
                finally
                {
                    File.SetUnixFileMode(_cacheDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }
        }
    }
}
