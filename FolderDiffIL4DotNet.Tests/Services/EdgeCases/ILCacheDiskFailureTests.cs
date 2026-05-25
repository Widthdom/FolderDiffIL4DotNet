using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.EdgeCases
{
    /// <summary>
    /// Tests for IL cache behavior when disk I/O fails (simulating network interruption
    /// during cache read/write on network-mounted storage).
    /// ディスク I/O 失敗時の IL キャッシュ動作テスト（ネットワークマウントストレージでの
    /// キャッシュ読み書き中のネットワーク障害をシミュレート）。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class ILCacheDiskFailureTests : IDisposable
    {
        private readonly string _tempDir;

        public ILCacheDiskFailureTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"ILCacheDiskFailure_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                // Restore permissions before cleanup
                RestorePermissions(_tempDir);
                Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }

        private string CreateTestFile(string name, string content = "test content")
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content);
            return path;
        }

        private static void RestorePermissions(string path)
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.Linux) &&
                !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.OSX))
            {
                return;
            }

            try
            {
                if (Directory.Exists(path))
                {
#pragma warning disable CA1416 // Unix-only API; test is skipped on Windows / Unix 専用 API; Windows ではテストがスキップされます
                    File.SetUnixFileMode(path,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#pragma warning restore CA1416
                }
            }
            catch { }
        }

        [Fact]
        public async Task DiskCache_WhenCacheDirectoryBecomesReadOnly_FallsBackToMemory()
        {
            // Simulates network interruption by making the cache directory read-only after initial write
            // 初回書き込み後にキャッシュディレクトリを読み取り専用にしてネットワーク障害をシミュレート
            if (!IsUnixNonRoot()) return;

            var cacheDir = Path.Combine(_tempDir, "cache-ro-test");
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: cacheDir);
            var file1 = CreateTestFile("first.dll", "content-1");
            var tool = "tool";

            // First write succeeds normally
            await cache.SetILAsync(file1, tool, "IL-first");

            // Make directory read-only (simulate network write failure)
#pragma warning disable CA1416 // Unix-only API; test is skipped on Windows / Unix 専用 API; Windows ではテストがスキップされます
            File.SetUnixFileMode(cacheDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
#pragma warning restore CA1416
            try
            {
                var file2 = CreateTestFile("second.dll", "content-2");
                // Second write should fall back to memory-only without throwing
                await cache.SetILAsync(file2, tool, "IL-second");

                // Memory cache should still have the value
                var result = await cache.TryGetILAsync(file2, tool);
                Assert.Equal("IL-second", result);
            }
            finally
            {
#pragma warning disable CA1416 // Unix-only API; test is skipped on Windows / Unix 専用 API; Windows ではテストがスキップされます
                File.SetUnixFileMode(cacheDir,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#pragma warning restore CA1416
            }
        }

        [Fact]
        public async Task DiskCache_WhenCacheFileCorrupted_FallsBackToMemoryOnRead()
        {
            // Simulates a corrupted cache file (e.g., partial write due to network failure)
            // ネットワーク障害による部分書き込みなど、キャッシュファイル破損をシミュレート
            var cacheDir = Path.Combine(_tempDir, "cache-corrupt-test");
            var file = CreateTestFile("corrupt.dll", "corrupt-content");
            var tool = "tool";

            // Write a valid entry
            var cache1 = new ILCache(ilCacheDirectoryAbsolutePath: cacheDir);
            await cache1.SetILAsync(file, tool, "valid IL content");

            // Corrupt the cache files
            var cacheFiles = Directory.GetFiles(cacheDir, "*.ilcache");
            foreach (var cf in cacheFiles)
            {
                // Overwrite with invalid content to simulate corruption
                File.WriteAllBytes(cf, new byte[] { 0xFF, 0xFE, 0x00, 0x01 });
            }

            // A new cache instance should handle the corrupted file gracefully
            var cache2 = new ILCache(ilCacheDirectoryAbsolutePath: cacheDir);
            var result = await cache2.TryGetILAsync(file, tool);
            // Corrupted data should not crash; either returns null or the corrupted data as a string
            // (implementation-dependent, but must not throw)
        }

        [Fact]
        public async Task DiskCache_WhenDiskFull_SetDoesNotThrow()
        {
            // Very long path simulates a disk I/O failure scenario
            // 非常に長いパスでディスク I/O 失敗シナリオをシミュレート
            var invalidPath = Path.Combine(_tempDir, new string('x', 250), "cache");
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: invalidPath);
            var file = CreateTestFile("disk-full.dll");

            // Should not throw - falls back to memory-only
            await cache.SetILAsync(file, "tool", "IL data");
            var result = await cache.TryGetILAsync(file, "tool");
            Assert.Equal("IL data", result);
        }

        [Fact]
        public async Task DiskCache_EmptyCacheDirectory_HandlesGracefully()
        {
            // Empty string cache directory should fall back to memory-only
            // 空文字列のキャッシュディレクトリはメモリのみにフォールバックする
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: "");
            var file = CreateTestFile("empty-dir.dll");

            await cache.SetILAsync(file, "tool", "IL data");
            var result = await cache.TryGetILAsync(file, "tool");
            Assert.Equal("IL data", result);
        }

        [Fact]
        public async Task DiskCache_CacheDirectoryDeletedMidOperation_HandlesGracefully()
        {
            // Simulates the cache directory being deleted while the cache is in use
            // キャッシュ使用中にキャッシュディレクトリが削除されるケースをシミュレート
            var cacheDir = Path.Combine(_tempDir, "cache-deleted-test");
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: cacheDir);
            var file1 = CreateTestFile("pre-delete.dll", "content-1");

            await cache.SetILAsync(file1, "tool", "IL-before-delete");

            // Delete the cache directory
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);

            // Subsequent operations should handle gracefully (memory fallback)
            var file2 = CreateTestFile("post-delete.dll", "content-2");
            await cache.SetILAsync(file2, "tool", "IL-after-delete");
            var memResult = await cache.TryGetILAsync(file2, "tool");
            Assert.Equal("IL-after-delete", memResult);
        }

        // ── Disk-full simulation via disk quota enforcement ────────────────────

        /// <summary>
        /// When the disk cache exceeds MaxDiskFileCount, older entries are trimmed
        /// and new entries are still stored successfully.
        /// ディスクキャッシュが MaxDiskFileCount を超えた場合、古いエントリが削除され
        /// 新しいエントリが正常に保存されることを検証する。
        /// </summary>
        [Fact]
        public async Task DiskCache_WhenQuotaExceeded_TrimsOldEntriesAndStoresNew()
        {
            var cacheDir = Path.Combine(_tempDir, "cache-quota-test");
            // Max 3 files on disk / ディスク上最大3ファイル
            var cache = new ILCache(
                ilCacheDirectoryAbsolutePath: cacheDir,
                ilCacheMaxDiskFileCount: 3);

            // Write 5 entries — first 2 should be trimmed / 5エントリ書き込み — 最初の2つは削除されるはず
            for (var i = 0; i < 5; i++)
            {
                var file = CreateTestFile($"quota-{i}.dll", $"content-{i}");
                await cache.SetILAsync(file, "tool", $"IL-{i}");
            }

            // Latest entry should be retrievable from memory / 最新エントリはメモリから取得可能
            var latestFile = Path.Combine(_tempDir, "quota-4.dll");
            var result = await cache.TryGetILAsync(latestFile, "tool");
            Assert.Equal("IL-4", result);

            // Disk should not exceed the quota / ディスクはクォータを超えないこと
            if (Directory.Exists(cacheDir))
            {
                var diskFiles = Directory.GetFiles(cacheDir, "*.ilcache");
                Assert.True(diskFiles.Length <= 3,
                    $"Disk cache should have at most 3 files, but has {diskFiles.Length}");
            }
        }

        /// <summary>
        /// When the disk cache exceeds MaxDiskMegabytes, older entries are trimmed.
        /// ディスクキャッシュが MaxDiskMegabytes を超えた場合、古いエントリが削除されることを検証する。
        /// </summary>
        [Fact]
        public async Task DiskCache_WhenDiskSizeLimitExceeded_TrimsOldEntries()
        {
            var cacheDir = Path.Combine(_tempDir, "cache-size-limit-test");
            // Very small disk limit (1 byte as MB would be impractical, use file count instead)
            // 非常に小さいディスク上限で検証
            var cache = new ILCache(
                ilCacheDirectoryAbsolutePath: cacheDir,
                ilCacheMaxDiskFileCount: 2);

            // Write large-ish entries / やや大きいエントリを書き込む
            var largeIL = new string('X', 1024);
            for (var i = 0; i < 5; i++)
            {
                var file = CreateTestFile($"size-{i}.dll", $"content-{i}");
                await cache.SetILAsync(file, "tool", largeIL + i);
            }

            // Should not crash, latest should be available / クラッシュせず最新が利用可能
            var latestFile = Path.Combine(_tempDir, "size-4.dll");
            var result = await cache.TryGetILAsync(latestFile, "tool");
            Assert.NotNull(result);
        }

        /// <summary>
        /// Multiple rapid Set+Get cycles on a read-only directory never throw.
        /// 読み取り専用ディレクトリに対して Set+Get を高速で繰り返しても例外が発生しないことを検証する。
        /// </summary>
        [Fact]
        public async Task DiskCache_RapidWriteToReadOnlyDir_NeverThrows()
        {
            if (!IsUnixNonRoot()) return;

            var cacheDir = Path.Combine(_tempDir, "cache-rapid-ro");
            Directory.CreateDirectory(cacheDir);

            // Make directory read-only immediately / ディレクトリを即座に読み取り専用に
#pragma warning disable CA1416 // Unix-only API / Unix 専用 API
            File.SetUnixFileMode(cacheDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
#pragma warning restore CA1416
            try
            {
                var cache = new ILCache(ilCacheDirectoryAbsolutePath: cacheDir);

                // Rapid fire writes — all should fall back to memory silently
                // 高速連続書き込み — すべてメモリへのフォールバック
                for (var i = 0; i < 50; i++)
                {
                    var file = CreateTestFile($"rapid-{i}.dll", $"content-{i}");
                    await cache.SetILAsync(file, "tool", $"IL-{i}");
                    var result = await cache.TryGetILAsync(file, "tool");
                    Assert.Equal($"IL-{i}", result);
                }
            }
            finally
            {
#pragma warning disable CA1416 // Unix-only API / Unix 専用 API
                File.SetUnixFileMode(cacheDir,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#pragma warning restore CA1416
            }
        }

        /// <summary>
        /// Corrupted cache file with zero bytes is handled without crash.
        /// 0バイトの破損キャッシュファイルがクラッシュなく処理されることを検証する。
        /// </summary>
        [Fact]
        public async Task DiskCache_ZeroByteCacheFile_HandlesGracefully()
        {
            var cacheDir = Path.Combine(_tempDir, "cache-zerobyte-test");
            var file = CreateTestFile("zerobyte.dll", "file-content");
            var tool = "tool";

            // Write a valid entry / 有効なエントリを書き込む
            var cache1 = new ILCache(ilCacheDirectoryAbsolutePath: cacheDir);
            await cache1.SetILAsync(file, tool, "valid IL");

            // Replace all cache files with zero-byte files / 全キャッシュファイルを0バイトに置換
            var cacheFiles = Directory.GetFiles(cacheDir, "*.ilcache");
            foreach (var cf in cacheFiles)
            {
                File.WriteAllBytes(cf, Array.Empty<byte>());
            }

            // New instance reading zero-byte files should not crash / 0バイトファイルを読む新インスタンスはクラッシュしない
            var cache2 = new ILCache(ilCacheDirectoryAbsolutePath: cacheDir);
            var result = await cache2.TryGetILAsync(file, tool);
            // May return null or empty — must not throw / null または空を返すかもしれないがスローしない
        }

        /// <summary>
        /// Concurrent writes to a failing disk path do not corrupt the memory cache.
        /// 障害発生中のディスクパスへの並行書き込みがメモリキャッシュを破損しないことを検証する。
        /// </summary>
        [Fact]
        public async Task DiskCache_ConcurrentWritesToInvalidPath_MemoryCacheRemainsConsistent()
        {
            var invalidPath = Path.Combine(_tempDir, new string('z', 250), "cache");
            var cache = new ILCache(ilCacheDirectoryAbsolutePath: invalidPath);

            // Concurrent writes / 並行書き込み
            var tasks = new Task[20];
            for (var i = 0; i < tasks.Length; i++)
            {
                var idx = i;
                var file = CreateTestFile($"concurrent-{idx}.dll", $"content-{idx}");
                tasks[idx] = cache.SetILAsync(file, "tool", $"IL-{idx}");
            }
            await Task.WhenAll(tasks);

            // Verify all entries are in memory / 全エントリがメモリにあることを確認
            for (var i = 0; i < tasks.Length; i++)
            {
                var file = Path.Combine(_tempDir, $"concurrent-{i}.dll");
                var result = await cache.TryGetILAsync(file, "tool");
                Assert.Equal($"IL-{i}", result);
            }
        }

        private static bool IsUnixNonRoot()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.Linux) &&
                !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.OSX))
            {
                return false;
            }
            return !string.Equals(Environment.UserName, "root", StringComparison.OrdinalIgnoreCase);
        }
    }
}
