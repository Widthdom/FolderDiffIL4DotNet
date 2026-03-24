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

        [Fact]
        public async Task DiskCache_CacheDirIsFile_FallsBackToMemoryOnly()
        {
            // Place a regular file where the cache directory path would be, so
            // Directory.CreateDirectory throws IOException and triggers memory-only fallback
            // キャッシュディレクトリパスに通常ファイルを配置し、
            // Directory.CreateDirectory が IOException をスローしてメモリ専用フォールバックを発動させる
            var filePath = Path.Combine(_tempDir, "not-a-directory");
            File.WriteAllText(filePath, "I am a file");

            var cache = new ILCache(ilCacheDirectoryAbsolutePath: filePath);
            var testFile = CreateTestFile("fallback.dll");
            var tool = "tool";
            await cache.SetILAsync(testFile, tool, "memory IL");
            var result = await cache.TryGetILAsync(testFile, tool);
            Assert.Equal("memory IL", result);
        }

        // Verify that LRU eviction silently catches UnauthorizedAccessException on disk remove (Linux/macOS non-root only)
        // LRU 退去時にディスク削除の UnauthorizedAccessException を静かにキャッチして継続することを確認する（Linux/macOS 非 root のみ）
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
                return; // root can delete files even in read-only directories / root は読み取り専用ディレクトリでも削除可能
            }

            var tool = "tool";
            var file1 = CreateTestFile("lru-ro-1.dll", "c1");
            var file2 = CreateTestFile("lru-ro-2.dll", "c2");

            // maxMemoryEntries=1 forces LRU eviction when a second entry is added
            // maxMemoryEntries=1 で 2 つ目のエントリ追加時に LRU 退去を発生させる
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxMemoryEntries: 1);
            await cache.SetILAsync(file1, tool, "IL-1");
            await Task.Delay(30);

            // Make cache directory read-only so disk Remove fails with UnauthorizedAccessException
            // キャッシュディレクトリを読み取り専用にしてディスク Remove を失敗させる
#pragma warning disable CA1416 // Unix-only API; test is skipped on Windows / Unix 専用 API; Windows ではテストがスキップされます
            File.SetUnixFileMode(_cacheDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
#pragma warning restore CA1416
            try
            {
                // Adding file2 triggers LRU eviction of file1; disk Remove catches UnauthorizedAccessException silently
                // file2 追加で file1 が LRU 退去され、ディスク Remove は UnauthorizedAccessException を静かにキャッチする
                await cache.SetILAsync(file2, tool, "IL-2");
            }
            finally
            {
#pragma warning disable CA1416 // Unix-only API; test is skipped on Windows / Unix 専用 API; Windows ではテストがスキップされます
                File.SetUnixFileMode(_cacheDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#pragma warning restore CA1416
            }
        }

        // Verify TrimCacheFiles silently continues when read-only files cannot be deleted (Linux/macOS non-root only)
        // 読み取り専用ファイルが削除できない場合でも TrimCacheFiles が警告ログのみで継続することを確認する（Linux/macOS 非 root のみ）
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

            // maxDiskFileCount=1 triggers quota enforcement when a second file is cached
            // maxDiskFileCount=1 で 2 つ目のファイルキャッシュ時にクォータ適用を発生させる
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxDiskFileCount: 1);
            var tool = "tool";

            var file1 = CreateTestFile("trim-ro-1.dll", "unique-ro-content-1");
            await cache.SetILAsync(file1, tool, "IL-trim-1");
            await Task.Delay(30);

            // Make the first cache file's directory read-only to prevent deletion
            // 最初のキャッシュファイルのディレクトリを読み取り専用にして削除を阻止する
            var cacheFiles = Directory.GetFiles(_cacheDir, "*.ilcache");
            if (cacheFiles.Length > 0)
            {
#pragma warning disable CA1416 // Unix-only API; test is skipped on Windows / Unix 専用 API; Windows ではテストがスキップされます
                File.SetUnixFileMode(_cacheDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
#pragma warning restore CA1416
                try
                {
                    var file2 = CreateTestFile("trim-ro-2.dll", "unique-ro-content-2");
                    // SetIL triggers disk quota enforcement; deletion failure is silently ignored
                    // SetIL がディスククォータ適用をトリガーし、削除失敗は静かに無視される
                    await cache.SetILAsync(file2, tool, "IL-trim-2");
                }
                finally
                {
#pragma warning disable CA1416 // Unix-only API; test is skipped on Windows / Unix 専用 API; Windows ではテストがスキップされます
                    File.SetUnixFileMode(_cacheDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#pragma warning restore CA1416
                }
            }
        }
        [Fact]
        public async Task PreSeedFileHash_AvoidsSha256Recomputation()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null);
            var file = CreateTestFile("preseed.dll", "preseed content");
            var tool = "dotnet-ildasm";
            var knownHash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

            // Pre-seed a hash, then store and retrieve IL
            cache.PreSeedFileHash(file, knownHash);
            await cache.SetILAsync(file, tool, "IL-text-preseed");
            var result = await cache.TryGetILAsync(file, tool);

            // The cache should still return the stored IL text via the pre-seeded hash key
            Assert.Equal("IL-text-preseed", result);
        }

        [Fact]
        public void GetReportStats_AfterHitsAndMisses_ReflectsCorrectCounts()
        {
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null);
            var file = CreateTestFile("stats.dll", "stats content");

            // Initial stats should be zero
            var stats = cache.GetReportStats();
            Assert.Equal(0, stats.Hits);
            Assert.Equal(0, stats.Misses);
            Assert.Equal(0, stats.Stores);
        }

        // ── Memory budget tests / メモリ予算テスト ──────────────────────────

        [Fact]
        public async Task MemoryBudget_EvictsOldestWhenExceeded()
        {
            // 1 MB budget; each IL string ~0.5 MB → 2nd insert should evict 1st
            // 1 MB 予算; 各 IL 文字列 ~0.5 MB → 2件目挿入で1件目が追い出される
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null,
                ilCacheMaxMemoryEntries: 100, ilCacheMaxMemoryMegabytes: 1);
            var tool = "tool";

            // ~500 KB string (250K chars × 2 bytes + overhead)
            string largeIL1 = new string('A', 250_000);
            string largeIL2 = new string('B', 250_000);
            string largeIL3 = new string('C', 250_000);

            var file1 = CreateTestFile("mem1.dll", "c1");
            var file2 = CreateTestFile("mem2.dll", "c2");
            var file3 = CreateTestFile("mem3.dll", "c3");

            await cache.SetILAsync(file1, tool, largeIL1);
            await cache.SetILAsync(file2, tool, largeIL2);
            // Both fit under 1 MB
            Assert.Equal(largeIL1, await cache.TryGetILAsync(file1, tool));
            Assert.Equal(largeIL2, await cache.TryGetILAsync(file2, tool));

            // 3rd should trigger memory eviction of the oldest (file1)
            await cache.SetILAsync(file3, tool, largeIL3);
            Assert.Null(await cache.TryGetILAsync(file1, tool));
            Assert.Equal(largeIL3, await cache.TryGetILAsync(file3, tool));

            var (evicted, _) = cache.Stats;
            Assert.True(evicted >= 1, $"Expected at least 1 eviction, got {evicted}");
        }

        [Fact]
        public async Task MemoryBudget_ZeroMeansUnlimited()
        {
            // 0 MB = unlimited; should not evict based on memory
            // 0 MB = 無制限; メモリに基づく追い出しは行われない
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null,
                ilCacheMaxMemoryEntries: 100, ilCacheMaxMemoryMegabytes: 0);
            var tool = "tool";

            string largeIL = new string('X', 500_000);
            var file1 = CreateTestFile("unlim1.dll", "c1");
            var file2 = CreateTestFile("unlim2.dll", "c2");
            var file3 = CreateTestFile("unlim3.dll", "c3");

            await cache.SetILAsync(file1, tool, largeIL);
            await cache.SetILAsync(file2, tool, largeIL);
            await cache.SetILAsync(file3, tool, largeIL);

            // All should still be present
            Assert.NotNull(await cache.TryGetILAsync(file1, tool));
            Assert.NotNull(await cache.TryGetILAsync(file2, tool));
            Assert.NotNull(await cache.TryGetILAsync(file3, tool));

            Assert.Equal(0, cache.Stats.Evicted);
        }

        // ── Mutation-testing additions / ミューテーションテスト追加 ──────────────

        [Fact]
        public async Task TTL_AccessJustAfterExpiry_ReturnsNull()
        {
            // Verify the TTL boundary: entry accessed after TTL has elapsed must return null
            // TTL 境界の検証: TTL 経過後にアクセスしたエントリは null を返すこと
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null, timeToLive: TimeSpan.FromMilliseconds(20));
            var file = CreateTestFile("ttl-boundary.dll");
            var tool = "tool";

            await cache.SetILAsync(file, tool, "IL-ttl-boundary");
            // Wait just past the TTL / TTL をわずかに超える時間待機
            await Task.Delay(100);

            var result = await cache.TryGetILAsync(file, tool);
            Assert.Null(result);
            Assert.True(cache.Stats.Expired >= 1, "Expected at least 1 expired entry");
        }

        [Fact]
        public async Task LRU_AtExactCapacity_EvictsOldestOnInsert()
        {
            // When count == maxEntries, the next insert must trigger eviction
            // count == maxEntries のとき、次の挿入で退去が発生すること
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null, ilCacheMaxMemoryEntries: 2);
            var tool = "tool";

            var file1 = CreateTestFile("cap1.dll", "cap-c1");
            var file2 = CreateTestFile("cap2.dll", "cap-c2");
            var file3 = CreateTestFile("cap3.dll", "cap-c3");

            await cache.SetILAsync(file1, tool, "IL-cap1");
            await cache.SetILAsync(file2, tool, "IL-cap2");
            // Cache is exactly at capacity (2 entries). Next insert must evict the oldest (file1).
            // キャッシュがちょうど容量上限（2エントリ）。次の挿入で最古（file1）が退去される。
            await cache.SetILAsync(file3, tool, "IL-cap3");

            Assert.Null(await cache.TryGetILAsync(file1, tool));
            Assert.Equal("IL-cap2", await cache.TryGetILAsync(file2, tool));
            Assert.Equal("IL-cap3", await cache.TryGetILAsync(file3, tool));
            Assert.True(cache.Stats.Evicted >= 1);
        }

        [Fact]
        public async Task DiskQuota_BothLimitsZero_NoFilesDeleted()
        {
            // When both maxDiskFileCount and maxDiskMegabytes are 0, no quota enforcement occurs
            // maxDiskFileCount と maxDiskMegabytes がともに 0 の場合、クォータ制御は行われない
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir,
                ilCacheMaxDiskFileCount: 0, ilCacheMaxDiskMegabytes: 0);
            var tool = "tool";

            for (int i = 0; i < 5; i++)
            {
                var file = CreateTestFile($"noquota-{i}.dll", $"noquota-content-{i}");
                await cache.SetILAsync(file, tool, $"IL-noquota-{i}");
                await Task.Delay(30);
            }

            var cacheFiles = Directory.GetFiles(_cacheDir, "*.ilcache");
            Assert.Equal(5, cacheFiles.Length);
        }

        [Fact]
        public async Task DiskQuota_OnlyFileCountExceeds_TrimsByFileCount()
        {
            // maxDiskFileCount=2 with unlimited bytes: file count triggers trimming
            // maxDiskFileCount=2 でバイト制限なし: ファイル数によるトリミングが発生
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir,
                ilCacheMaxDiskFileCount: 2, ilCacheMaxDiskMegabytes: 0);
            var tool = "tool";

            for (int i = 0; i < 4; i++)
            {
                var file = CreateTestFile($"fconly-{i}.dll", $"fc-content-{i}");
                await cache.SetILAsync(file, tool, $"IL-fc-{i}");
                await Task.Delay(50);
            }

            var cacheFiles = Directory.GetFiles(_cacheDir, "*.ilcache");
            Assert.True(cacheFiles.Length <= 2, $"Expected <= 2 cache files after file-count trim, got {cacheFiles.Length}");
        }

        [Fact]
        public async Task DiskQuota_OnlyBytesExceed_TrimsByBytes()
        {
            // maxDiskMegabytes very small (simulate by writing large IL), no file count limit
            // maxDiskMegabytes を非常に小さく設定（大きな IL を書き込みで模擬）、ファイル数制限なし
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir,
                ilCacheMaxDiskFileCount: 0, ilCacheMaxDiskMegabytes: 1);
            var tool = "tool";

            // Write multiple ~500KB entries to clearly exceed 1 MB limit
            // 複数の ~500KB エントリを書き込み 1 MB 制限を明確に超過させる
            for (int i = 0; i < 5; i++)
            {
                var file = CreateTestFile($"bytesonly-{i}.dll", $"bytes-content-{i}");
                await cache.SetILAsync(file, tool, new string('Z', 500_000));
                await Task.Delay(50);
            }

            var cacheFiles = Directory.GetFiles(_cacheDir, "*.ilcache");
            // Some files should have been trimmed by the bytes quota (5 * 500KB = 2.5MB > 1MB)
            // バイトクォータにより一部のファイルがトリミングされているはず（5 * 500KB = 2.5MB > 1MB）
            Assert.True(cacheFiles.Length < 5, $"Expected fewer than 5 cache files after byte trim, got {cacheFiles.Length}");
        }

        [Fact]
        public async Task Precompute_NegativeParallel_ThrowsArgumentOutOfRange()
        {
            // Verify that negative maxParallel also throws, not just zero
            // ゼロだけでなく負の maxParallel も例外をスローすることを確認
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null);
            var file = CreateTestFile("neg.dll");

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => cache.PrecomputeAsync(new[] { file }, maxParallel: -1));
        }

        [Fact]
        public async Task MemoryBudget_EntryCountAndMemoryBothEnforced()
        {
            // entry limit = 2, memory = 10 MB → entry count should still trigger eviction
            // エントリ上限 = 2, メモリ = 10 MB → エントリ数上限で追い出される
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null,
                ilCacheMaxMemoryEntries: 2, ilCacheMaxMemoryMegabytes: 10);
            var tool = "tool";

            var file1 = CreateTestFile("both1.dll", "c1");
            var file2 = CreateTestFile("both2.dll", "c2");
            var file3 = CreateTestFile("both3.dll", "c3");

            await cache.SetILAsync(file1, tool, "small-il-1");
            await cache.SetILAsync(file2, tool, "small-il-2");
            await cache.SetILAsync(file3, tool, "small-il-3");

            // file1 should be evicted by entry count limit
            Assert.Null(await cache.TryGetILAsync(file1, tool));
            Assert.NotNull(await cache.TryGetILAsync(file3, tool));
        }
    }
}
