using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.Caching
{
    /// <summary>
    /// Direct tests for <see cref="ILDiskCache"/> guard clauses and diagnostic logging.
    /// <see cref="ILDiskCache"/> のガード節と診断ログを直接検証するテスト。
    /// </summary>
    public sealed class ILDiskCacheTests : IDisposable
    {
        private readonly string _rootDir;
        private readonly string _cacheDir;

        public ILDiskCacheTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), $"ILDiskCacheTests_{Guid.NewGuid():N}");
            _cacheDir = Path.Combine(_rootDir, "cache");
            Directory.CreateDirectory(_rootDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_rootDir))
                {
                    Directory.Delete(_rootDir, recursive: true);
                }
            }
            catch
            {
                // Best-effort test cleanup / テスト後片付けはベストエフォート
            }
        }

        [Fact]
        public void Constructor_WhenCacheDirectoryIsInvalid_LogsWarningAndDisablesDiskLayer()
        {
            var logger = new TestLogger();
            var invalidCacheDir = Path.Combine(_rootDir, "bad\0cache");

            var exception = Record.Exception(() => _ = new ILDiskCache(invalidCacheDir, logger, maxDiskFileCount: 0, maxDiskMegabytes: 0));

            Assert.Null(exception);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Failed to create IL cache directory", warning.Message, StringComparison.Ordinal);
            Assert.Contains("IsPathRooted=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(ArgumentException), warning.Message, StringComparison.Ordinal);
            Assert.NotNull(warning.Exception);
        }

        [Fact]
        public async Task TryReadAsync_WhenCacheKeyIsWhitespace_LogsWarningAndReturnsNull()
        {
            var logger = new TestLogger();
            var cache = new ILDiskCache(_cacheDir, logger, maxDiskFileCount: 0, maxDiskMegabytes: 0);

            var result = await cache.TryReadAsync("   ");

            Assert.Null(result);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Skipped IL disk cache read", warning.Message, StringComparison.Ordinal);
            Assert.Contains(_cacheDir, warning.Message, StringComparison.Ordinal);
            Assert.Contains("DirectoryIsPathRooted=True", warning.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task WriteAsync_WhenCacheKeyIsWhitespace_LogsWarningAndSkipsWriting()
        {
            var logger = new TestLogger();
            var cache = new ILDiskCache(_cacheDir, logger, maxDiskFileCount: 0, maxDiskMegabytes: 0);

            await cache.WriteAsync(" ", "IL-text");

            Assert.Empty(Directory.GetFiles(_cacheDir, "*.ilcache"));
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Skipped IL disk cache write", warning.Message, StringComparison.Ordinal);
            Assert.Contains(_cacheDir, warning.Message, StringComparison.Ordinal);
            Assert.Contains("DirectoryIsPathRooted=True", warning.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Remove_WhenCacheKeyIsWhitespace_LogsWarningAndDoesNotThrow()
        {
            var logger = new TestLogger();
            var cache = new ILDiskCache(_cacheDir, logger, maxDiskFileCount: 0, maxDiskMegabytes: 0);

            var exception = Record.Exception(() => cache.Remove("\t"));

            Assert.Null(exception);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Skipped IL disk cache remove", warning.Message, StringComparison.Ordinal);
            Assert.Contains(_cacheDir, warning.Message, StringComparison.Ordinal);
            Assert.Contains("DirectoryIsPathRooted=True", warning.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task TryReadAsync_WhenCacheDirectoryBecomesInvalid_LogsCacheDirectoryAndKeyLength()
        {
            var logger = new TestLogger();
            var cache = new ILDiskCache(_cacheDir, logger, maxDiskFileCount: 0, maxDiskMegabytes: 0);
            const string cacheKey = "read-key";
            var invalidCacheDir = Path.Combine(Path.GetPathRoot(_rootDir)!, new string('r', 5000));
            SetPrivateField(cache, "_cacheDirectoryAbsolutePath", invalidCacheDir);

            var result = await cache.TryReadAsync(cacheKey);

            Assert.Null(result);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Failed to read IL cache file", warning.Message, StringComparison.Ordinal);
            Assert.Contains(invalidCacheDir, warning.Message, StringComparison.Ordinal);
            Assert.Contains("CacheDirectoryIsPathRooted=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains("CacheFileIsPathRooted=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"cacheKeyLength={cacheKey.Length}", warning.Message, StringComparison.Ordinal);
            Assert.NotNull(warning.Exception);
        }

        [Fact]
        public async Task WriteAsync_WhenCacheDirectoryBecomesInvalid_LogsCacheDirectoryAndKeyLength()
        {
            var logger = new TestLogger();
            var cache = new ILDiskCache(_cacheDir, logger, maxDiskFileCount: 0, maxDiskMegabytes: 0);
            const string cacheKey = "write-key";
            var invalidCacheDir = Path.Combine(Path.GetPathRoot(_rootDir)!, new string('w', 5000));
            SetPrivateField(cache, "_cacheDirectoryAbsolutePath", invalidCacheDir);

            await cache.WriteAsync(cacheKey, "IL-text");

            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Failed to write IL cache file", warning.Message, StringComparison.Ordinal);
            Assert.Contains(invalidCacheDir, warning.Message, StringComparison.Ordinal);
            Assert.Contains("CacheDirectoryIsPathRooted=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains("CacheFileIsPathRooted=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"cacheKeyLength={cacheKey.Length}", warning.Message, StringComparison.Ordinal);
            Assert.NotNull(warning.Exception);
        }

        [Fact]
        public void Remove_WhenCacheDirectoryBecomesInvalid_LogsCacheDirectoryAndKeyLength()
        {
            var logger = new TestLogger();
            var cache = new ILDiskCache(_cacheDir, logger, maxDiskFileCount: 0, maxDiskMegabytes: 0);
            const string cacheKey = "delete-key";
            var invalidCacheDir = Path.Combine(Path.GetPathRoot(_rootDir)!, new string('d', 5000));
            SetPrivateField(cache, "_cacheDirectoryAbsolutePath", invalidCacheDir);

            var exception = Record.Exception(() => cache.Remove(cacheKey));

            Assert.Null(exception);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Failed to remove disk cache file", warning.Message, StringComparison.Ordinal);
            Assert.Contains(invalidCacheDir, warning.Message, StringComparison.Ordinal);
            Assert.Contains("CacheDirectoryIsPathRooted=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains("CacheFileIsPathRooted=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"cacheKeyLength={cacheKey.Length}", warning.Message, StringComparison.Ordinal);
            Assert.NotNull(warning.Exception);
        }

        [Fact]
        public async Task WriteAsync_WhenQuotaTrimRemovesFiles_LogsDirectoryAndConfiguredLimits()
        {
            var logger = new TestLogger();
            var cache = new ILDiskCache(_cacheDir, logger, maxDiskFileCount: 1, maxDiskMegabytes: 0);

            await cache.WriteAsync("key-1", "IL-1");
            await Task.Delay(50);
            await cache.WriteAsync("key-2", "IL-2");

            Assert.Single(Directory.GetFiles(_cacheDir, "*.ilcache"));
            var info = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Info);
            Assert.Contains("Disk quota trim: directory='", info.Message, StringComparison.Ordinal);
            Assert.Contains(_cacheDir, info.Message, StringComparison.Ordinal);
            Assert.Contains("removed=1", info.Message, StringComparison.Ordinal);
            Assert.Contains("maxFiles=1", info.Message, StringComparison.Ordinal);
            Assert.Contains("maxBytes=0", info.Message, StringComparison.Ordinal);
        }

        private static void SetPrivateField(object target, string fieldName, string value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(target, value);
        }
    }
}
