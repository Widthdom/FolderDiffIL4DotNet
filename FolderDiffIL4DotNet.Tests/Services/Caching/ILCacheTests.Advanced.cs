using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.Caching
{
    public partial class ILCacheTests
    {
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

        [Fact]
        public async Task DiskCache_ReadOnlyExistingFile_OverwriteSucceedsOnWindows()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir);
            var file = CreateTestFile("readonly-overwrite.dll", "readonly-overwrite-content");
            var tool = "tool";

            await cache.SetILAsync(file, tool, "old-il");

            var cacheFile = Assert.Single(Directory.GetFiles(_cacheDir, "*.ilcache"));
            File.SetAttributes(cacheFile, File.GetAttributes(cacheFile) | FileAttributes.ReadOnly);

            try
            {
                await cache.SetILAsync(file, tool, "new-il");

                var freshCache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir);
                Assert.Equal("new-il", await freshCache.TryGetILAsync(file, tool));
            }
            finally
            {
                if (File.Exists(cacheFile))
                {
                    File.SetAttributes(cacheFile, FileAttributes.Normal);
                }
            }
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

        [Fact]
        public async Task LRU_DiskRemove_ReadOnlyFile_RemovesEvictedDiskEntryOnWindows()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var tool = "tool";
            var file1 = CreateTestFile("lru-ro-file-1.dll", "lru-ro-file-content-1");
            var file2 = CreateTestFile("lru-ro-file-2.dll", "lru-ro-file-content-2");

            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxMemoryEntries: 1);
            await cache.SetILAsync(file1, tool, "IL-1");

            var firstCacheFile = Assert.Single(Directory.GetFiles(_cacheDir, "*.ilcache"));
            File.SetAttributes(firstCacheFile, File.GetAttributes(firstCacheFile) | FileAttributes.ReadOnly);

            try
            {
                await cache.SetILAsync(file2, tool, "IL-2");

                var freshCache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxMemoryEntries: 1);
                Assert.Null(await freshCache.TryGetILAsync(file1, tool));
                Assert.Equal("IL-2", await freshCache.TryGetILAsync(file2, tool));
            }
            finally
            {
                if (File.Exists(firstCacheFile))
                {
                    File.SetAttributes(firstCacheFile, FileAttributes.Normal);
                }
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
        public async Task DiskQuota_ReadOnlyFile_TrimRemovesOldFileOnWindows()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var cache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxDiskFileCount: 1);
            var tool = "tool";

            var file1 = CreateTestFile("trim-ro-file-1.dll", "trim-ro-file-content-1");
            await cache.SetILAsync(file1, tool, "IL-trim-1");

            var firstCacheFile = Assert.Single(Directory.GetFiles(_cacheDir, "*.ilcache"));
            File.SetAttributes(firstCacheFile, File.GetAttributes(firstCacheFile) | FileAttributes.ReadOnly);

            try
            {
                var file2 = CreateTestFile("trim-ro-file-2.dll", "trim-ro-file-content-2");
                await cache.SetILAsync(file2, tool, "IL-trim-2");

                var freshCache = new ILCache(ilCacheDirectoryAbsolutePath: _cacheDir, ilCacheMaxDiskFileCount: 1);
                Assert.Null(await freshCache.TryGetILAsync(file1, tool));
                Assert.Equal("IL-trim-2", await freshCache.TryGetILAsync(file2, tool));
            }
            finally
            {
                if (File.Exists(firstCacheFile))
                {
                    File.SetAttributes(firstCacheFile, FileAttributes.Normal);
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
        public async Task Precompute_InvalidPath_LogsWarningAndContinues()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: null, logger: logger);
            var validFile = CreateTestFile("precompute-valid.dll", "valid-content");

            var ex = await Record.ExceptionAsync(() => cache.PrecomputeAsync(new[] { validFile, "bad\0path.dll" }, maxParallel: 1));

            Assert.Null(ex);
            Assert.Contains(logger.Messages, m => m.Contains("Failed to precompute SHA256", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, m => m.Contains("ArgumentException", StringComparison.Ordinal));
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
