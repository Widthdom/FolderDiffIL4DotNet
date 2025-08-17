using System;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// コンソールに進捗状況を表示するクラス
    /// </summary>
    public sealed class ProgressReportService
    {
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
                throw new ArgumentOutOfRangeException(nameof(percentage), $"Progress must be between 0.00 and 100.00. Actual: {percentage:F2}");
            }

            // 単調増加と重複出力の抑止をスレッドセーフに実施
            lock (_lock)
            {
                // 逆行は出力しない（並列時の遅延到着を抑止）。
                if (percentage <= _lastPercentage)
                {
                    return;
                }

                var formattedPercentage = percentage.ToString("F2");
                if (!string.Equals(formattedPercentage, _lastFormattedPercentage, StringComparison.Ordinal))
                {
                    Console.WriteLine($"Progress: {formattedPercentage}%");
                    _lastFormattedPercentage = formattedPercentage;
                    _lastPercentage = percentage;
                }
            }
        }
    }
}
