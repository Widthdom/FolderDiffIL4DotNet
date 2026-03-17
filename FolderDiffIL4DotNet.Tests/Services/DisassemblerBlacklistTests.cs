using System;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// <see cref="DisassemblerBlacklist"/> の単体テスト。
    /// </summary>
    public sealed class DisassemblerBlacklistTests
    {
        private const string ToolA = "dotnet-ildasm";
        private const string ToolB = "ilspycmd";

        // ── 基本動作 ─────────────────────────────────────────────────────────

        [Fact]
        public void IsBlacklisted_NoFailures_ReturnsFalse()
        {
            var bl = CreateBlacklist();
            Assert.False(bl.IsBlacklisted(ToolA));
        }

        [Fact]
        public void IsBlacklisted_BelowThreshold_ReturnsFalse()
        {
            var bl = CreateBlacklist(failThreshold: 3);
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            Assert.False(bl.IsBlacklisted(ToolA));
        }

        [Fact]
        public void IsBlacklisted_AtThreshold_ReturnsTrue()
        {
            var bl = CreateBlacklist(failThreshold: 3);
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            Assert.True(bl.IsBlacklisted(ToolA));
        }

        [Fact]
        public void IsBlacklisted_AfterReset_ReturnsFalse()
        {
            var bl = CreateBlacklist(failThreshold: 3);
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            bl.ResetFailure(ToolA);
            Assert.False(bl.IsBlacklisted(ToolA));
        }

        [Fact]
        public void IsBlacklisted_AfterClear_ReturnsFalse()
        {
            var bl = CreateBlacklist(failThreshold: 2);
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            bl.Clear();
            Assert.False(bl.IsBlacklisted(ToolA));
        }

        [Fact]
        public void IsBlacklisted_DifferentTools_AreIndependent()
        {
            var bl = CreateBlacklist(failThreshold: 2);
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);

            Assert.True(bl.IsBlacklisted(ToolA));
            Assert.False(bl.IsBlacklisted(ToolB));
        }

        [Fact]
        public void IsBlacklisted_NullOrWhitespace_ReturnsFalse()
        {
            var bl = CreateBlacklist();
            Assert.False(bl.IsBlacklisted(null));
            Assert.False(bl.IsBlacklisted(""));
            Assert.False(bl.IsBlacklisted("   "));
        }

        // ── TTL 期限切れ ──────────────────────────────────────────────────────

        [Fact]
        public void IsBlacklisted_AfterTtlExpiry_ReturnsFalseAndRemovesEntry()
        {
            var bl = CreateBlacklist(failThreshold: 3, ttl: TimeSpan.FromMilliseconds(1));
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);

            Thread.Sleep(20); // TTL 満了まで待機

            Assert.False(bl.IsBlacklisted(ToolA));
            Assert.False(bl.ContainsEntry(ToolA)); // エントリが削除されたか確認
        }

        [Fact]
        public void IsBlacklisted_WithinTtl_ReturnsTrue()
        {
            var bl = CreateBlacklist(failThreshold: 3, ttl: TimeSpan.FromHours(1));
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);

            Assert.True(bl.IsBlacklisted(ToolA));
        }

        // ── InjectEntry ───────────────────────────────────────────────────────

        [Fact]
        public void InjectEntry_OverridesState_IsBlacklistedReflectsInjected()
        {
            var bl = CreateBlacklist(failThreshold: 3, ttl: TimeSpan.FromHours(1));
            bl.InjectEntry(ToolA, failCount: 3, lastFailUtc: DateTime.UtcNow);
            Assert.True(bl.IsBlacklisted(ToolA));
        }

        [Fact]
        public void InjectEntry_ExpiredTimestamp_IsBlacklistedReturnsFalse()
        {
            var bl = CreateBlacklist(failThreshold: 3, ttl: TimeSpan.FromMinutes(10));
            bl.InjectEntry(ToolA, failCount: 3, lastFailUtc: DateTime.UtcNow.AddMinutes(-11));
            Assert.False(bl.IsBlacklisted(ToolA));
            Assert.False(bl.ContainsEntry(ToolA));
        }

        // ── B-4: 並列競合テスト ───────────────────────────────────────────────

        [Fact]
        public void RegisterFailure_Concurrent_DoesNotThrow_AndCountsAccumulate()
        {
            const int threadCount = 32;
            const int callsPerThread = 50;
            var bl = CreateBlacklist(failThreshold: int.MaxValue, ttl: TimeSpan.FromHours(1));

            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            var threads = new Thread[threadCount];
            for (int t = 0; t < threadCount; t++)
            {
                threads[t] = new Thread(() =>
                {
                    try
                    {
                        for (int i = 0; i < callsPerThread; i++)
                        {
                            bl.RegisterFailure(ToolA);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }

            foreach (var thread in threads) thread.Start();
            foreach (var thread in threads) thread.Join();

            Assert.Empty(exceptions);
            Assert.True(bl.ContainsEntry(ToolA));
        }

        [Fact]
        public async Task IsBlacklisted_ConcurrentTtlExpiry_NoExceptionAndEventuallyReturnsFalse()
        {
            // B-4: TTL 満了と同時に複数スレッドが IsBlacklisted を呼んだ場合に例外なく完了すること
            var bl = CreateBlacklist(failThreshold: 3, ttl: TimeSpan.FromMilliseconds(50));
            bl.InjectEntry(ToolA, failCount: 3, lastFailUtc: DateTime.UtcNow);

            // TTL が切れるギリギリのタイミングで並列チェック
            var tasks = new Task[20];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    await Task.Delay(45); // TTL 満了の直前/直後に集中させる
                    _ = bl.IsBlacklisted(ToolA);
                });
            }

            await Task.WhenAll(tasks); // 例外がなければ成功
            // TTL 満了後は必ず false を返す
            Thread.Sleep(20);
            Assert.False(bl.IsBlacklisted(ToolA));
        }

        private static DisassemblerBlacklist CreateBlacklist(
            int failThreshold = 3,
            TimeSpan? ttl = null)
            => new(failThreshold, ttl ?? TimeSpan.FromMinutes(10));
    }
}
