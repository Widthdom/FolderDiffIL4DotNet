using System;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// コンソールに進捗状況を表示するクラス
    /// </summary>
    public sealed class ProgressReportService
    {
        #region constants
        /// <summary>
        /// 進捗範囲エラー
        /// </summary>
        private const string ERROR_PROGRESS_OUT_OF_RANGE = "Progress must be between 0.00 and 100.00. Actual: {0:F2}";

        /// <summary>
        /// 進捗表示
        /// </summary>
        private const string LOG_PROGRESS = "Progress: {0}%";

        /// <summary>
        /// 進捗処理中表示
        /// </summary>
        private const string LOG_PROGRESS_KEEPALIVE = LOG_PROGRESS + " (processing...)";
        #endregion

        #region private member variables
        /// <summary>
        /// 直前に出力したF2フォーマットの文字列（重複出力抑止用）
        /// </summary>
        private string _lastFormattedPercentage = null;

        /// <summary>
        /// 直前に出力した実数値（単調増加保証用）。未出力時は NegativeInfinity。
        /// </summary>
        private double _lastPercentage = double.NegativeInfinity;

        /// <summary>
        /// Keep-alive 出力のために直近で標準出力へ書き込んだ時刻（UTC）。
        /// </summary>
        private DateTime _lastConsoleWriteUtc = DateTime.MinValue;
        /// <summary>
        /// 進捗値が変わらない場合にも間隔ごとに動作中であることを知らせる。
        /// </summary>
        private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// スレッドセーフに出力制御するためのロック。
        /// </summary>
        private readonly object _lock = new object();
        #endregion

        /// <summary>
        /// 進捗率をコンソールに表示します。小数点以下2桁（F2）で出力します。
        /// </summary>
        /// <param name="percentage">進捗率（0.00～100.00）。0未満または100を超える値は無効です。</param>
        /// <remarks>
        /// 小数点以下2桁（F2）の表示値が前回と異なる場合に出力します。
        /// 例: 70.01% → 70.02% → 70.03% ... と 0.01% 刻みで詳細に表示されます（同じ値の重複出力は抑制）。
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="percentage"/> が 0.00～100.00 の範囲外の場合にスローされます。
        /// </exception>
        public void ReportProgress(double percentage)
        {
            if (percentage < 0 || percentage > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percentage), string.Format(ERROR_PROGRESS_OUT_OF_RANGE, percentage));
            }

            // 単調増加と重複出力の抑止をスレッドセーフに実施
            lock (_lock)
            {
                // 逆行（前回値より小さい進捗）は出力しない（並列時の遅延到着を抑止）。
                if (percentage < _lastPercentage)
                {
                    return;
                }

                var formattedPercentage = percentage.ToString("F2");
                bool hasChanged = !string.Equals(formattedPercentage, _lastFormattedPercentage, StringComparison.Ordinal);
                bool shouldEmitKeepAlive = !hasChanged && DateTime.UtcNow - _lastConsoleWriteUtc >= KeepAliveInterval;

                if (hasChanged)
                {
                    WriteProgress(LOG_PROGRESS, formattedPercentage);
                }
                else if (shouldEmitKeepAlive)
                {
                    WriteProgress(LOG_PROGRESS_KEEPALIVE, formattedPercentage);
                }

                _lastPercentage = percentage;
            }
        }

        /// <summary>
        /// フォーマット済みの進捗メッセージをコンソールへ出力し、内部状態を更新します。
        /// </summary>
        /// <param name="format"><see cref="Constants"/> に定義されたメッセージテンプレート。</param>
        /// <param name="formattedPercentage">F2 形式でフォーマット済みの進捗文字列。</param>
        private void WriteProgress(string format, string formattedPercentage)
        {
            Console.WriteLine(string.Format(format, formattedPercentage));
            _lastFormattedPercentage = formattedPercentage;
            _lastConsoleWriteUtc = DateTime.UtcNow;
            Console.Out.Flush();
        }
    }
}
