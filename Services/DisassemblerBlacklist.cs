using System;
using System.Collections.Concurrent;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Manages a blacklist of disassembler tools. Tools exceeding a consecutive-failure threshold are skipped for a TTL period, then automatically reinstated.
    /// 逆アセンブラツールのブラックリスト管理を担当します。連続失敗が閾値を超えたツールを TTL 期間スキップし、期間満了後に自動復旧します。
    /// </summary>
    public sealed class DisassemblerBlacklist
    {
        private readonly ConcurrentDictionary<string, (int FailCount, DateTime LastFailUtc)> _failCountAndTime = new();
        private readonly int _failThreshold;
        private readonly TimeSpan _ttl;

        /// <summary>
        /// Initializes a new instance of <see cref="DisassemblerBlacklist"/>.
        /// <see cref="DisassemblerBlacklist"/> の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="failThreshold">Number of consecutive failures before a tool is blacklisted. / ブラックリスト化までの連続失敗回数。</param>
        /// <param name="ttl">Time-to-live after which a blacklisted tool is automatically reinstated. / ブラックリスト自動解除までの有効期間。</param>
        public DisassemblerBlacklist(int failThreshold, TimeSpan ttl)
        {
            _failThreshold = failThreshold;
            _ttl = ttl;
        }

        /// <summary>
        /// Returns whether the specified tool is currently blacklisted. Removes the entry and lifts the blacklist if the TTL has expired.
        /// 指定ツールがブラックリスト化されているかを判定します。TTL が満了している場合はエントリを削除してブラックリスト解除します。
        /// </summary>
        public bool IsBlacklisted(string disassembleCommand)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommand))
            {
                return false;
            }
            if (!_failCountAndTime.TryGetValue(disassembleCommand, out var info))
            {
                return false;
            }
            if (info.FailCount < _failThreshold)
            {
                return false;
            }
            if ((DateTime.UtcNow - info.LastFailUtc) > _ttl)
            {
                _failCountAndTime.TryRemove(disassembleCommand, out _);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Increments the failure count for the specified tool and updates its blacklist data.
        /// 指定ツールの失敗回数をインクリメントし、ブラックリスト判定データを更新します。
        /// </summary>
        public void RegisterFailure(string disassembleCommand)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommand))
            {
                return;
            }
            _failCountAndTime.AddOrUpdate(
                disassembleCommand,
                _ => (1, DateTime.UtcNow),
                (_, old) => (old.FailCount + 1, DateTime.UtcNow));
        }

        /// <summary>
        /// Resets the failure count for the specified tool (lifts the blacklist).
        /// 指定ツールの失敗カウントをリセット（ブラックリスト解除）します。
        /// </summary>
        public void ResetFailure(string disassembleCommand)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommand))
            {
                return;
            }
            _failCountAndTime.TryRemove(disassembleCommand, out _);
        }

        /// <summary>
        /// Clears all blacklist entries.
        /// すべてのブラックリストエントリを削除します。
        /// </summary>
        public void Clear() => _failCountAndTime.Clear();

        /// <summary>
        /// Test helper: directly injects a blacklist entry for the specified tool.
        /// テスト用: 指定ツールにブラックリストエントリを直接設定します。
        /// </summary>
        internal void InjectEntry(string disassembleCommand, int failCount, DateTime lastFailUtc)
        {
            _failCountAndTime[disassembleCommand] = (failCount, lastFailUtc);
        }

        /// <summary>
        /// Test helper: checks whether an entry exists for the specified tool.
        /// テスト用: 指定ツールのエントリが存在するかを確認します。
        /// </summary>
        internal bool ContainsEntry(string disassembleCommand)
            => _failCountAndTime.ContainsKey(disassembleCommand);
    }
}
