using System;
using System.IO;
using System.Threading;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// コンソールに進捗状況を表示するクラス
    /// </summary>
    public sealed class ProgressReportService : IDisposable
    {
        /// <summary>
        /// 進捗表示
        /// </summary>
        private const string LOG_PROGRESS = "Progress: {0}%";

        /// <summary>
        /// 進捗表示（ラベル付き）
        /// </summary>
        private const string LOG_PROGRESS_LABELED = "{0}: {1}%";

        /// <summary>
        /// 進捗処理中表示
        /// </summary>
        private const string LOG_PROGRESS_KEEPALIVE = LOG_PROGRESS + " (processing...)";

        /// <summary>
        /// 進捗処理中表示（ラベル付き）
        /// </summary>
        private const string LOG_PROGRESS_KEEPALIVE_LABELED = LOG_PROGRESS_LABELED + " (processing...)";

        /// <summary>
        /// 進捗バーの固定幅。
        /// </summary>
        private const int FIXED_BAR_WIDTH = 32;

        /// <summary>
        /// 進捗停滞時の簡易スピナーフレーム。
        /// </summary>
        private static readonly char[] KeepAliveFrames = ['|', '/', '-', '\\'];

        /// <summary>
        /// 直前に出力したF2フォーマットの文字列（重複出力抑止用）
        /// </summary>
        private string _lastFormattedPercentage = null;

        /// <summary>
        /// 進捗バー前に表示するラベル。
        /// </summary>
        private string _labelPrefix;

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
        /// 進捗停滞時のスピナー更新間隔。
        /// </summary>
        private static readonly TimeSpan IdleSpinnerInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// 進捗値が変化した直近時刻（UTC）。
        /// </summary>
        private DateTime _lastProgressChangeUtc = DateTime.MinValue;

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

        /// <summary>
        /// 進捗停滞時にスピナーを動かすためのタイマー。
        /// </summary>
        private Timer _keepAliveTimer;

        /// <summary>
        /// タイマーの初期化済みフラグ。
        /// </summary>
        private bool _keepAliveTimerStarted;

        /// <summary>
        /// 破棄済みフラグ。
        /// </summary>
        private bool _disposed;

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
            if (_disposed)
            {
                return;
            }
            if (percentage < 0 || percentage > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percentage), $"Progress must be between 0.00 and 100.00. Actual: {percentage:F2}");
            }

            // 単調増加と重複出力の抑止をスレッドセーフに実施
            lock (_lock)
            {
                EnsureKeepAliveTimerStarted();

                // 逆行（前回値より小さい進捗）は出力しない（並列時の遅延到着を抑止）。
                if (percentage < _lastPercentage)
                {
                    return;
                }

                var formattedPercentage = percentage.ToString("F2");
                bool hasChanged = !string.Equals(formattedPercentage, _lastFormattedPercentage, StringComparison.Ordinal);
                var nowUtc = DateTime.UtcNow;
                bool shouldEmitKeepAlive = !hasChanged && nowUtc - _lastConsoleWriteUtc >= KeepAliveInterval;

                if (hasChanged)
                {
                    RenderProgressBar(formattedPercentage, percentage, showKeepAlive: false);
                    _lastFormattedPercentage = formattedPercentage;
                    _lastProgressChangeUtc = nowUtc;
                }
                else if (shouldEmitKeepAlive)
                {
                    RenderProgressBar(formattedPercentage, percentage, showKeepAlive: true);
                }

                _lastPercentage = percentage;
            }
        }

        /// <summary>
        /// 進捗表示のラベルを設定します。
        /// </summary>
        /// <param name="label">ラベル文字列（null/空白なら未設定）。</param>
        public void SetLabel(string label)
        {
            if (_disposed)
            {
                return;
            }
            lock (_lock)
            {
                _labelPrefix = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
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
                WriteProgressLine(BuildRedirectedProgressLine(formattedPercentage, showKeepAlive));
                _lastConsoleWriteUtc = DateTime.UtcNow;
                return;
            }

            string line = BuildProgressBarLine(formattedPercentage, percentage, showKeepAlive);
            bool finalizeLine = percentage >= 100.0;
            WriteInlineProgressLine(line, finalizeLine);
        }

        /// <summary>
        /// 進捗の更新が止まっている間もスピナーを動かすタイマーを起動します。
        /// </summary>
        private void EnsureKeepAliveTimerStarted()
        {
            if (_keepAliveTimerStarted || Console.IsOutputRedirected || _disposed)
            {
                return;
            }

            _keepAliveTimerStarted = true;
            _keepAliveTimer = new Timer(_ =>
            {
                lock (_lock)
                {
                    if (_disposed)
                    {
                        return;
                    }
                    if (_lastFormattedPercentage == null || _lastPercentage >= 100.0)
                    {
                        return;
                    }

                    if (DateTime.UtcNow - _lastProgressChangeUtc < KeepAliveInterval)
                    {
                        return;
                    }

                    RenderProgressBar(_lastFormattedPercentage, _lastPercentage, showKeepAlive: true);
                }
            }, null, KeepAliveInterval, IdleSpinnerInterval);
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
            var prefix = string.IsNullOrEmpty(_labelPrefix) ? string.Empty : _labelPrefix + " ";
            if (!string.IsNullOrEmpty(_labelPrefix))
            {
                var spinnerSegment = $"{KeepAliveFrames[_keepAliveFrameIndex++ % KeepAliveFrames.Length]} ";
                return $"{prefix}{spinnerSegment}[{bar}] {percentText}";
            }
            if (showKeepAlive)
            {
                char frame = KeepAliveFrames[_keepAliveFrameIndex++ % KeepAliveFrames.Length];
                return $"[{bar}] {percentText} {frame}";
            }
            return $"[{bar}] {percentText}";
        }

        /// <summary>
        /// リダイレクト時の進捗表示文字列を組み立てます。
        /// </summary>
        private string BuildRedirectedProgressLine(string formattedPercentage, bool showKeepAlive)
        {
            if (string.IsNullOrEmpty(_labelPrefix))
            {
                var format = showKeepAlive ? LOG_PROGRESS_KEEPALIVE : LOG_PROGRESS;
                return string.Format(format, formattedPercentage);
            }

            var labeledFormat = showKeepAlive ? LOG_PROGRESS_KEEPALIVE_LABELED : LOG_PROGRESS_LABELED;
            return string.Format(labeledFormat, _labelPrefix, formattedPercentage);
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

        /// <summary>
        /// 進捗表示のタイマーを停止して資源を解放します。
        /// 例外終了時に呼び出すことで、進捗スピナーが残り続ける状態を防ぎます。
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                _keepAliveTimer?.Dispose();
                _keepAliveTimer = null;

                if (!Console.IsOutputRedirected && _lastRenderLength > 0)
                {
                    lock (ConsoleRenderCoordinator.RenderSyncRoot)
                    {
                        Console.WriteLine();
                        Console.Out.Flush();
                    }
                    _lastRenderLength = 0;
                }
            }
        }
    }
}
