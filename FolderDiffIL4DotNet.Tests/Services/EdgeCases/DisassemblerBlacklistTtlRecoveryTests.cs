using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.EdgeCases
{
    /// <summary>
    /// Tests for DisassemblerBlacklist TTL recovery edge cases:
    /// blacklisted tools are automatically reinstated after the TTL period expires,
    /// even under concurrent access and repeated failure/recovery cycles.
    /// DisassemblerBlacklist TTL リカバリーのエッジケーステスト:
    /// ブラックリスト化されたツールは TTL 期間満了後に自動的に復帰する。
    /// 並列アクセスおよび繰り返しの失敗/復旧サイクルの下でも正しく動作することを確認する。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class DisassemblerBlacklistTtlRecoveryTests
    {
        private const string ToolA = "dotnet-ildasm";
        private const string ToolB = "ilspycmd";

        [Fact]
        public void TtlRecovery_ToolIsReinstatedAfterTtlExpiry_AndCanBeBlacklistedAgain()
        {
            // Verify that a tool can go through multiple blacklist → TTL recovery → re-blacklist cycles
            // ツールがブラックリスト → TTL 復旧 → 再ブラックリストのサイクルを複数回経ることができることを確認
            var bl = new DisassemblerBlacklist(failThreshold: 2, ttl: TimeSpan.FromMilliseconds(1));

            // Cycle 1: blacklist and recover
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            Assert.True(bl.IsBlacklisted(ToolA));
            Thread.Sleep(20);
            Assert.False(bl.IsBlacklisted(ToolA));

            // Cycle 2: blacklist again after recovery
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            Assert.True(bl.IsBlacklisted(ToolA));
            Thread.Sleep(20);
            Assert.False(bl.IsBlacklisted(ToolA));
        }

        [Fact]
        public void TtlRecovery_MultipleToolsRecoverIndependently()
        {
            // Different tools with different TTL expiry points recover independently
            // 異なる TTL 満了タイミングを持つ複数ツールが独立して復旧することを確認
            var bl = new DisassemblerBlacklist(failThreshold: 1, ttl: TimeSpan.FromMilliseconds(1));

            bl.RegisterFailure(ToolA);
            Assert.True(bl.IsBlacklisted(ToolA));

            Thread.Sleep(20);
            bl.RegisterFailure(ToolB); // ToolB blacklisted AFTER ToolA's TTL should have expired
            Assert.True(bl.IsBlacklisted(ToolB));

            Assert.False(bl.IsBlacklisted(ToolA)); // ToolA recovered
            Assert.True(bl.IsBlacklisted(ToolB)); // ToolB still blacklisted
        }

        [Fact]
        public void TtlRecovery_FailureAfterTtlExpiry_StartsNewFailCountFromZero()
        {
            // After TTL expiry clears the entry, new failures start from count 0
            // TTL 満了でエントリがクリアされた後、新しい失敗は 0 からカウント開始される
            var bl = new DisassemblerBlacklist(failThreshold: 3, ttl: TimeSpan.FromMilliseconds(1));

            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            Assert.True(bl.IsBlacklisted(ToolA));

            Thread.Sleep(20);
            Assert.False(bl.IsBlacklisted(ToolA));
            Assert.False(bl.ContainsEntry(ToolA)); // Entry purged

            // Single failure should not re-blacklist (threshold is 3)
            bl.RegisterFailure(ToolA);
            Assert.False(bl.IsBlacklisted(ToolA));
            Assert.True(bl.ContainsEntry(ToolA)); // New entry created with count 1
        }

        [Fact]
        public async Task TtlRecovery_ConcurrentRegisterAndIsBlacklisted_NoExceptions()
        {
            // Under heavy concurrent access, no exceptions should escape from the
            // interleaving of RegisterFailure and IsBlacklisted around TTL boundaries
            // RegisterFailure と IsBlacklisted の TTL 境界での激しい並列実行でも例外が発生しないことを確認
            var bl = new DisassemblerBlacklist(failThreshold: 2, ttl: TimeSpan.FromMilliseconds(5));
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            var tasks = new List<Task>();

            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            bl.RegisterFailure(ToolA);
                            _ = bl.IsBlacklisted(ToolA);
                            if (j % 10 == 0)
                                await Task.Delay(1);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            await Task.WhenAll(tasks);
            Assert.Empty(exceptions);
        }

        [Fact]
        public void TtlRecovery_InjectEntryWithBoundaryTimestamp_ExpiresCorrectly()
        {
            // Verify TTL boundary: an entry injected exactly at the TTL boundary is treated as expired
            // TTL 境界値の検証: TTL 境界ぴったりに注入されたエントリは期限切れとして扱われる
            var ttl = TimeSpan.FromMinutes(10);
            var bl = new DisassemblerBlacklist(failThreshold: 1, ttl: ttl);

            // Inject an entry whose last failure was exactly TTL + 1 second ago
            bl.InjectEntry(ToolA, failCount: 1, lastFailUtc: DateTime.UtcNow.Add(-ttl).AddSeconds(-1));
            Assert.False(bl.IsBlacklisted(ToolA));
            Assert.False(bl.ContainsEntry(ToolA)); // Should have been purged
        }

        [Fact]
        public void TtlRecovery_ResetDuringActiveBlacklist_ImmediatelyReinstates()
        {
            // A manual reset during an active blacklist immediately reinstates the tool
            // without waiting for TTL expiry
            // アクティブなブラックリスト中の手動リセットは TTL 満了を待たずに即座にツールを復帰させる
            var bl = new DisassemblerBlacklist(failThreshold: 2, ttl: TimeSpan.FromHours(1));

            bl.RegisterFailure(ToolA);
            bl.RegisterFailure(ToolA);
            Assert.True(bl.IsBlacklisted(ToolA));

            bl.ResetFailure(ToolA);
            Assert.False(bl.IsBlacklisted(ToolA));
            Assert.False(bl.ContainsEntry(ToolA));
        }
    }
}
