using System;
using System.Collections.Concurrent;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// 逆アセンブラツールのブラックリスト管理を担当します。
    /// 連続失敗が閾値を超えたツールを TTL 期間スキップし、期間満了後に自動復旧します。
    /// </summary>
    public sealed class DisassemblerBlacklist
    {
        /// <summary>
        /// ツール毎の連続失敗回数と最終失敗時刻（UTC）。
        /// </summary>
        private readonly ConcurrentDictionary<string, (int FailCount, DateTime LastFailUtc)> _failCountAndTime = new();

        /// <summary>
        /// ブラックリスト化を判定する連続失敗閾値。
        /// </summary>
        private readonly int _failThreshold;

        /// <summary>
        /// ブラックリスト有効期間。
        /// </summary>
        private readonly TimeSpan _ttl;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="failThreshold">ブラックリスト化する連続失敗回数の閾値。</param>
        /// <param name="ttl">ブラックリスト有効期間。</param>
        public DisassemblerBlacklist(int failThreshold, TimeSpan ttl)
        {
            _failThreshold = failThreshold;
            _ttl = ttl;
        }

        /// <summary>
        /// 指定ツールがブラックリスト化されているかを判定します。
        /// TTL が満了している場合はエントリを削除してブラックリスト解除します。
        /// </summary>
        /// <param name="disassembleCommand">コマンド名。</param>
        /// <returns>ブラックリスト中の場合は <c>true</c>。</returns>
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
        /// 指定ツールの失敗回数をインクリメントし、ブラックリスト判定データを更新します。
        /// </summary>
        /// <param name="disassembleCommand">コマンド名。</param>
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
        /// 指定ツールの失敗カウントをリセット（ブラックリスト解除）します。
        /// </summary>
        /// <param name="disassembleCommand">コマンド名。</param>
        public void ResetFailure(string disassembleCommand)
        {
            if (string.IsNullOrWhiteSpace(disassembleCommand))
            {
                return;
            }
            _failCountAndTime.TryRemove(disassembleCommand, out _);
        }

        /// <summary>
        /// すべてのブラックリストエントリを削除します。
        /// </summary>
        public void Clear() => _failCountAndTime.Clear();

        /// <summary>
        /// テスト用: 指定ツールにブラックリストエントリを直接設定します。
        /// </summary>
        /// <param name="disassembleCommand">コマンド名。</param>
        /// <param name="failCount">設定する失敗回数。</param>
        /// <param name="lastFailUtc">設定する最終失敗時刻（UTC）。</param>
        internal void InjectEntry(string disassembleCommand, int failCount, DateTime lastFailUtc)
        {
            _failCountAndTime[disassembleCommand] = (failCount, lastFailUtc);
        }

        /// <summary>
        /// テスト用: 指定ツールのエントリが存在するかを確認します。
        /// </summary>
        internal bool ContainsEntry(string disassembleCommand)
            => _failCountAndTime.ContainsKey(disassembleCommand);
    }
}
