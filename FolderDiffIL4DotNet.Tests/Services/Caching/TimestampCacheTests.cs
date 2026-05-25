using System;
using System.IO;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.Caching
{
    /// <summary>
    /// Unit tests for <see cref="TimestampCache"/> static cache.
    /// <see cref="TimestampCache"/> 静的キャッシュのユニットテスト。
    /// </summary>
    /// <remarks>
    /// Non-parallel: TimestampCache uses a static Dictionary shared across tests.
    /// 非並列: TimestampCache はテスト間で共有される静的 Dictionary を使用。
    /// </remarks>
    [Collection("TimestampCache NonParallel")]
    [Trait("Category", "Unit")]
    public sealed class TimestampCacheTests : IDisposable
    {
        private readonly string _tempDir;

        public TimestampCacheTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"TimestampCacheTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            TimestampCache.Clear();
        }

        public void Dispose()
        {
            TimestampCache.Clear();
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public void GetOrAdd_ValidFile_ReturnsCachedTimestamp()
        {
            // First call computes, second returns cached value / 初回は計算、2回目はキャッシュ値を返す
            var filePath = Path.Combine(_tempDir, "test.txt");
            File.WriteAllText(filePath, "content");

            string first = TimestampCache.GetOrAdd(filePath);
            string second = TimestampCache.GetOrAdd(filePath);

            Assert.NotNull(first);
            Assert.NotEmpty(first);
            Assert.Equal(first, second);
        }

        [Fact]
        public void GetOrAdd_Null_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => TimestampCache.GetOrAdd(null!));
        }

        [Fact]
        public void GetOrAdd_Empty_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => TimestampCache.GetOrAdd(string.Empty));
        }

        [Fact]
        public void Clear_RemovesCachedEntries()
        {
            // After Clear, the cache should recompute / Clear 後はキャッシュが再計算される
            var filePath = Path.Combine(_tempDir, "clear_test.txt");
            File.WriteAllText(filePath, "content");

            string before = TimestampCache.GetOrAdd(filePath);
            Assert.NotNull(before);

            TimestampCache.Clear();

            // Modify the file's timestamp so we can detect re-computation
            // ファイルのタイムスタンプを変更して再計算を検出
            File.SetLastWriteTime(filePath, DateTime.Now.AddHours(1));
            string after = TimestampCache.GetOrAdd(filePath);
            Assert.NotNull(after);
            // After modifying the timestamp and clearing cache, value should differ
            // タイムスタンプ変更＋キャッシュクリア後は値が異なるべき
            Assert.NotEqual(before, after);
        }

        [Fact]
        public void GetOrAdd_CaseInsensitiveKey()
        {
            // Cache uses case-insensitive key comparison / キャッシュは大文字小文字を区別しない
            var filePath = Path.Combine(_tempDir, "CaseTest.txt");
            File.WriteAllText(filePath, "content");

            string upper = TimestampCache.GetOrAdd(filePath.ToUpperInvariant());
            string lower = TimestampCache.GetOrAdd(filePath.ToLowerInvariant());

            // Both should resolve to the same cached entry / 同一キャッシュエントリに解決
            Assert.Equal(upper, lower);
        }
    }
}
