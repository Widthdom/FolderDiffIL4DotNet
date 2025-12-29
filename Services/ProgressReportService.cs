using System;
using System.IO;
using FolderDiffIL4DotNet.Utils;

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

        /// <summary>
        /// 進捗バーの固定幅。
        /// </summary>
        private const int FIXED_BAR_WIDTH = 32;

        /// <summary>
        /// 進捗停滞時の簡易スピナーフレーム。
        /// </summary>
        private static readonly char[] KeepAliveFrames = ['|', '/', '-', '\\'];

        /// <summary>
        /// スピナーまで含めた進捗バー以外の最大文字数。
        /// </summary>
        private const int BAR_SUFFIX_MAX_LENGTH = 12;
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

        /// <summary>
        /// 直近に描画した進捗バーの文字数。
        /// </summary>
        private int _lastRenderLength;

        /// <summary>
        /// 進捗バーのスピナーフレームインデックス。
        /// </summary>
        private int _keepAliveFrameIndex;

        /// <summary>
        /// 進捗バーの幅（初回計算後に固定）。
        /// </summary>
        private int _barWidth = -1;
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
                    RenderProgressBar(formattedPercentage, percentage, showKeepAlive: false);
                    _lastFormattedPercentage = formattedPercentage;
                }
                else if (shouldEmitKeepAlive)
                {
                    RenderProgressBar(formattedPercentage, percentage, showKeepAlive: true);
                }

                _lastPercentage = percentage;
            }
        }

        /// <summary>
        /// 進捗バーをコンソールへ描画し、内部状態を更新します。
        /// </summary>
        /// <param name="formattedPercentage">F2 形式でフォーマット済みの進捗文字列。</param>
        /// <param name="percentage">進捗率（0.00～100.00）。</param>
        /// <param name="showKeepAlive">停滞中のスピナー表示を有効にするか。</param>
        private void RenderProgressBar(string formattedPercentage, double percentage, bool showKeepAlive)
        {
            if (Console.IsOutputRedirected)
            {
                var format = showKeepAlive ? LOG_PROGRESS_KEEPALIVE : LOG_PROGRESS;
                WriteProgressLine(string.Format(format, formattedPercentage));
                _lastConsoleWriteUtc = DateTime.UtcNow;
                return;
            }

            string line = BuildProgressBarLine(formattedPercentage, percentage, showKeepAlive);
            bool finalizeLine = percentage >= 100.0;
            WriteInlineProgressLine(line, finalizeLine);
        }

        /// <summary>
        /// 進捗バーの 1 行表示を組み立てます。
        /// </summary>
        private string BuildProgressBarLine(string formattedPercentage, double percentage, bool showKeepAlive)
        {
            int barWidth = GetBarWidth();
            int filled = (int)Math.Floor(percentage / 100.0 * barWidth);
            if (filled < 0)
            {
                filled = 0;
            }
            else if (filled > barWidth)
            {
                filled = barWidth;
            }

            var barChars = new char[barWidth];
            for (int i = 0; i < barWidth; i++)
            {
                barChars[i] = i < filled ? '=' : '-';
            }

            var bar = new string(barChars);
            var percentText = $"{formattedPercentage}%";
            if (showKeepAlive)
            {
                char frame = KeepAliveFrames[_keepAliveFrameIndex++ % KeepAliveFrames.Length];
                return $"[{bar}] {percentText} {frame}";
            }
            return $"[{bar}] {percentText}";
        }

        /// <summary>
        /// 進捗バーの幅をコンソール幅から算出します。
        /// </summary>
        private int GetBarWidth()
        {
            if (_barWidth > 0)
            {
                return _barWidth;
            }

            _barWidth = FIXED_BAR_WIDTH;
            return _barWidth;
        }

        /// <summary>
        /// 進捗メッセージを1行で出力します（リダイレクト時のフォールバック）。
        /// </summary>
        private void WriteProgressLine(string message)
        {
            Console.WriteLine(message);
            Console.Out.Flush();
        }

        /// <summary>
        /// 進捗バーを同一行で更新します。
        /// </summary>
        private void WriteInlineProgressLine(string message, bool finalizeLine)
        {
            lock (ConsoleRenderCoordinator.RenderSyncRoot)
            {
                Console.Write("\r" + message);
                int padding = Math.Max(0, _lastRenderLength - message.Length);
                if (padding > 0)
                {
                    Console.Write(new string(' ', padding));
                }
                if (finalizeLine)
                {
                    Console.WriteLine();
                    _lastRenderLength = 0;
                }
                else
                {
                    _lastRenderLength = message.Length;
                }
                Console.Out.Flush();
            }
            _lastConsoleWriteUtc = DateTime.UtcNow;
            ConsoleRenderCoordinator.MarkProgressRendered();
        }
    }
}
