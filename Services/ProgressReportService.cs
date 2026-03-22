using System;
using System.IO;
using System.Linq;
using System.Threading;
using FolderDiffIL4DotNet.Core.Console;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Displays progress status on the console with an inline progress bar and keep-alive spinner.
    /// コンソールにインライン進捗バーとキープアライブスピナーで進捗状況を表示するクラス。
    /// </summary>
    public sealed class ProgressReportService : IDisposable
    {
        private const string LOG_PROGRESS = "Progress: {0}%";
        private const string LOG_PROGRESS_LABELED = "{0}: {1}%";
        private const string LOG_PROGRESS_KEEPALIVE = LOG_PROGRESS + " (processing...)";
        private const string LOG_PROGRESS_KEEPALIVE_LABELED = LOG_PROGRESS_LABELED + " (processing...)";
        private const int FIXED_BAR_WIDTH = 32;
        private readonly string[] _keepAliveFrames;
        private string? _lastFormattedPercentage = null;
        private string? _labelPrefix = string.Empty;
        private double _lastPercentage = double.NegativeInfinity;
        private DateTime _lastConsoleWriteUtc = DateTime.MinValue;
        private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan IdleSpinnerInterval = TimeSpan.FromSeconds(1);
        private DateTime _lastProgressChangeUtc = DateTime.MinValue;
        private readonly object _lock = new object();
        private int _lastRenderLength;
        private int _keepAliveFrameIndex;
        private int _barWidth = -1;
        private Timer? _keepAliveTimer;
        private bool _keepAliveTimerStarted;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of <see cref="ProgressReportService"/>.
        /// <see cref="ProgressReportService"/> の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="config">Read-only configuration settings. / 読み取り専用の設定。</param>
        public ProgressReportService(IReadOnlyConfigSettings config)
        {
            ArgumentNullException.ThrowIfNull(config);
            _keepAliveFrames = config.SpinnerFrames.ToArray();
        }

        /// <summary>
        /// Reports progress to the console. Outputs when the F2-formatted value changes;
        /// suppresses duplicates and enforces monotonic increase for thread safety.
        /// 進捗率をコンソールに表示します。F2フォーマット値が変化した場合のみ出力し、
        /// 重複抑制と単調増加保証をスレッドセーフに行います。
        /// </summary>
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

            lock (_lock)
            {
                EnsureKeepAliveTimerStarted();

                // Ignore backward progress (late arrivals from parallel tasks).
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
        /// Updates the label prefix displayed alongside the progress percentage.
        /// プログレス表示に添えるラベルプレフィックスを更新します。
        /// </summary>
        /// <param name="label">Label text, or <see langword="null"/>/whitespace to clear. / ラベルテキスト（null/空白でクリア）。</param>
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
                var spinnerSegment = $"{_keepAliveFrames[_keepAliveFrameIndex++ % _keepAliveFrames.Length]} ";
                return $"{prefix}{spinnerSegment}[{bar}] {percentText}";
            }
            if (showKeepAlive)
            {
                string frame = _keepAliveFrames[_keepAliveFrameIndex++ % _keepAliveFrames.Length];
                return $"[{bar}] {percentText} {frame}";
            }
            return $"[{bar}] {percentText}";
        }

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

        private int GetBarWidth()
        {
            if (_barWidth > 0)
            {
                return _barWidth;
            }

            _barWidth = FIXED_BAR_WIDTH;
            return _barWidth;
        }

        private void WriteProgressLine(string message)
        {
            Console.WriteLine(message);
            Console.Out.Flush();
        }

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
        /// Stops the keep-alive timer and releases resources.
        /// Call on abnormal exit to prevent the spinner from lingering.
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
