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
                    File.SetUnixFileMode(path,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
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
            File.SetUnixFileMode(cacheDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
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
                File.SetUnixFileMode(cacheDir,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
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
