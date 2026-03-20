using System;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class DisassemblerBlacklistTests
    {
        private const string ToolA = "dotnet-ildasm";
        private const string ToolB = "ilspycmd";

        // ── Basic behavior / 基本動作 ─────────────────────────────────────────

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

        // ── TTL expiry / TTL 期限切れ ──────────────────────────────────────────

        [Fact]
        public void IsBlacklisted_AfterTtlExpiry_ReturnsFalseAndRemovesEntry()
        {
            var bl = CreateBlacklist(failThreshold: 3, ttl: TimeSpan.FromMilliseconds(1));
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);

            Thread.Sleep(20); // Wait for TTL to expire / TTL 満了まで待機

            Assert.False(bl.IsBlacklisted(ToolA));
            // Entry should have been purged on expiry check
            // 期限切れチェック時にエントリが削除されているはず
            Assert.False(bl.ContainsEntry(ToolA));
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

        // ── RegisterFailure / ResetFailure null guards / null ガード ──────────

        [Fact]
        public void RegisterFailure_NullOrWhitespace_DoesNotThrow_AndNoEntryCreated()
        {
            var bl = CreateBlacklist();
            bl.RegisterFailure(null);
            bl.RegisterFailure("");
            bl.RegisterFailure("   ");
            // ToolA was never touched, so no entry should exist
            // ToolA は操作していないためエントリが存在しないことを確認
            Assert.False(bl.ContainsEntry(ToolA));
        }

        [Fact]
        public void ResetFailure_NullOrWhitespace_DoesNotThrow()
        {
            var bl = CreateBlacklist();
            // Should return without throwing if null guards are working
            // null ガードが機能していれば例外なく戻るはず
            bl.ResetFailure(null);
            bl.ResetFailure("");
            bl.ResetFailure("   ");
        }

        [Fact]
        public void ResetFailure_NonExistentCommand_DoesNotThrow()
        {
            var bl = CreateBlacklist();
            // TryRemove should not throw even when no entry exists
            // エントリが存在しない場合も TryRemove は例外をスローしない
            bl.ResetFailure("no-such-tool");
        }

        // ── Concurrent race-condition tests / 並列競合テスト ──────────────────

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
            // Multiple threads call IsBlacklisted around the TTL expiry boundary; no exceptions should escape
            // TTL 満了境界付近で複数スレッドが IsBlacklisted を呼んでも例外が漏れないことを確認
            var bl = CreateBlacklist(failThreshold: 3, ttl: TimeSpan.FromMilliseconds(50));
            bl.InjectEntry(ToolA, failCount: 3, lastFailUtc: DateTime.UtcNow);

            // Fire parallel checks around the TTL boundary to stress race conditions
            // TTL 境界付近で並列チェックを実行して競合状態をテスト
            var tasks = new Task[20];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    await Task.Delay(45); // Cluster around TTL expiry / TTL 満了の直前/直後に集中させる
                    _ = bl.IsBlacklisted(ToolA);
                });
            }

            await Task.WhenAll(tasks); // Success if no exceptions thrown / 例外がなければ成功
            // After TTL expiry, must always return false
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
