using System;
using System.Diagnostics;
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
        private const int FIXED_BAR_WIDTH = 32;
        private const int MAX_ESTIMATED_TOTAL_MINUTES = (99 * 60) + 59;
        private const string ETA_PLACEHOLDER = "ETA --:-- (+-- h -- m)";
        private readonly string[] _keepAliveFrames;
        private readonly ILoggerService? _logger;
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
        private int _totalPhases;
        private int _currentPhase;
        private Stopwatch? _phaseStopwatch;

        /// <summary>
        /// Initializes a new instance of <see cref="ProgressReportService"/>.
        /// <see cref="ProgressReportService"/> の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="config">Read-only configuration settings. / 読み取り専用の設定。</param>
        public ProgressReportService(IReadOnlyConfigSettings config)
            : this(config, logger: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ProgressReportService"/> with optional logger for phase timing.
        /// フェーズタイミング用のオプションロガー付きで <see cref="ProgressReportService"/> を初期化します。
        /// </summary>
        /// <param name="config">Read-only configuration settings. / 読み取り専用の設定。</param>
        /// <param name="logger">Optional logger for phase elapsed time output. / フェーズ経過時間出力用のオプションロガー。</param>
        public ProgressReportService(IReadOnlyConfigSettings config, ILoggerService? logger)
        {
            ArgumentNullException.ThrowIfNull(config);
            _keepAliveFrames = config.SpinnerFrames.ToArray();
            _logger = logger;
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
        /// Resets progress tracking state so that progress can restart from 0%.
        /// Use this when transitioning between distinct phases (e.g. precompute → diff classification).
        /// 進捗追跡状態をリセットし、0% から再スタートできるようにします。
        /// フェーズ間の遷移時（例: プリコンピュート → 差分分類）に使用します。
        /// </summary>
        public void ResetProgress()
        {
            if (_disposed)
            {
                return;
            }
            lock (_lock)
            {
                _lastPercentage = double.NegativeInfinity;
                _lastFormattedPercentage = null;
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

        /// <summary>
        /// Gets or sets the total number of phases. When greater than zero, <see cref="BeginPhase"/>
        /// prefixes labels with <c>[current/total]</c> and logs per-phase elapsed time.
        /// フェーズ総数を取得・設定します。0 より大きい場合、<see cref="BeginPhase"/> がラベルに
        /// <c>[current/total]</c> プレフィックスを付与し、フェーズごとの経過時間をログ出力します。
        /// </summary>
        public int TotalPhases
        {
            get => _totalPhases;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _totalPhases = value;
            }
        }

        /// <summary>
        /// Begins a new numbered phase: logs the previous phase's elapsed time, resets progress to 0%,
        /// and sets the label with a <c>[current/total]</c> prefix (e.g. <c>[2/5] Diffing folders</c>).
        /// 新しい番号付きフェーズを開始します。前フェーズの経過時間をログ出力し、進捗を 0% にリセットし、
        /// ラベルに <c>[current/total]</c> プレフィックスを付与します（例: <c>[2/5] Diffing folders</c>）。
        /// </summary>
        /// <param name="label">Phase label text. / フェーズラベルテキスト。</param>
        public void BeginPhase(string label)
        {
            if (_disposed)
            {
                return;
            }

            lock (_lock)
            {
                LogPreviousPhaseElapsed();
                _currentPhase++;
                _phaseStopwatch = Stopwatch.StartNew();

                var formattedLabel = _totalPhases > 0
                    ? $"[{_currentPhase}/{_totalPhases}] {label}"
                    : label;

                _lastPercentage = double.NegativeInfinity;
                _lastFormattedPercentage = null;
                _labelPrefix = formattedLabel;
            }

            ReportProgress(0.0);
        }

        /// <summary>
        /// Logs the elapsed time of the previous phase (if any).
        /// 前フェーズの経過時間をログ出力します（存在する場合）。
        /// </summary>
        private void LogPreviousPhaseElapsed()
        {
            if (_phaseStopwatch == null || _logger == null || string.IsNullOrEmpty(_labelPrefix))
            {
                return;
            }

            _phaseStopwatch.Stop();
            var elapsed = _phaseStopwatch.Elapsed;
            var elapsedText = FormatPhaseElapsed(elapsed);
            _logger.LogMessage(
                AppLogLevel.Info,
                $"Phase completed: {_labelPrefix} ({elapsedText})",
                shouldOutputMessageToConsole: false);
        }

        /// <summary>
        /// Formats a phase elapsed time as a compact human-readable string.
        /// フェーズ経過時間をコンパクトな人間可読文字列にフォーマットします。
        /// </summary>
        internal static string FormatPhaseElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalMinutes >= 1)
            {
                return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}.{elapsed.Milliseconds / 100}s";
            }
            return $"{elapsed.TotalSeconds:F1}s";
        }

        /// <summary>
        /// Estimates the remaining time for the current progress value.
        /// 現在の進捗率に対する残り時間を推定します。
        /// </summary>
        internal static TimeSpan? EstimateRemaining(TimeSpan elapsed, double percentage)
        {
            if (elapsed < TimeSpan.Zero ||
                double.IsNaN(percentage) ||
                double.IsInfinity(percentage) ||
                percentage <= 0.0)
            {
                return null;
            }

            if (percentage >= 100.0)
            {
                return TimeSpan.Zero;
            }

            var remainingRatio = (100.0 - percentage) / percentage;
            if (remainingRatio < 0.0 || double.IsNaN(remainingRatio) || double.IsInfinity(remainingRatio))
            {
                return null;
            }

            var remainingSeconds = elapsed.TotalSeconds * remainingRatio;
            if (remainingSeconds < 0.0 || double.IsNaN(remainingSeconds) || double.IsInfinity(remainingSeconds))
            {
                return null;
            }

            return TimeSpan.FromSeconds(remainingSeconds);
        }

        /// <summary>
        /// Formats a fixed-width ETA segment with both completion clock time and remaining duration.
        /// 完了見込み時刻と残り時間を固定長で表す ETA セグメントをフォーマットします。
        /// </summary>
        internal static string FormatEta(DateTimeOffset nowLocal, TimeSpan? remaining)
        {
            if (!remaining.HasValue)
            {
                return ETA_PLACEHOLDER;
            }

            var remainingValue = remaining.Value;
            if (remainingValue < TimeSpan.Zero)
            {
                remainingValue = TimeSpan.Zero;
            }

            var roundedMinutes = (int)Math.Ceiling(remainingValue.TotalMinutes);
            if (roundedMinutes < 0)
            {
                roundedMinutes = 0;
            }
            else if (roundedMinutes > MAX_ESTIMATED_TOTAL_MINUTES)
            {
                roundedMinutes = MAX_ESTIMATED_TOTAL_MINUTES;
            }

            var etaClock = nowLocal.AddMinutes(roundedMinutes).ToString("HH:mm");
            var etaHours = roundedMinutes / 60;
            var etaMinutes = roundedMinutes % 60;
            return $"ETA {etaClock} (+{etaHours:00} h {etaMinutes:00} m)";
        }

        private void RenderProgressBar(string formattedPercentage, double percentage, bool showKeepAlive)
        {
            if (Console.IsOutputRedirected)
            {
                WriteProgressLine(BuildRedirectedProgressLine(formattedPercentage, percentage, showKeepAlive));
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
                barChars[i] = i < filled ? '█' : '░';
            }

            var bar = new string(barChars);
            var percentText = $"{formattedPercentage,6}%";
            var etaText = BuildEtaText(percentage);
            var prefix = string.IsNullOrEmpty(_labelPrefix)
                ? string.Empty
                : _labelPrefix.PadRight(ConsoleRenderCoordinator.STATUS_LABEL_WIDTH) + " ";
            if (!string.IsNullOrEmpty(_labelPrefix))
            {
                var spinnerSegment = $"{_keepAliveFrames[_keepAliveFrameIndex++ % _keepAliveFrames.Length]} ";
                return $"{prefix}{spinnerSegment}{bar} {percentText} {etaText}";
            }
            if (showKeepAlive)
            {
                string frame = _keepAliveFrames[_keepAliveFrameIndex++ % _keepAliveFrames.Length];
                return $"{bar} {percentText} {etaText} {frame}";
            }
            return $"{bar} {percentText} {etaText}";
        }

        private string BuildRedirectedProgressLine(string formattedPercentage, double percentage, bool showKeepAlive)
        {
            var etaText = BuildEtaText(percentage);
            if (string.IsNullOrEmpty(_labelPrefix))
            {
                var message = $"Progress: {formattedPercentage}% {etaText}";
                return showKeepAlive ? message + " (processing...)" : message;
            }

            var labeledMessage = $"{_labelPrefix}: {formattedPercentage}% {etaText}";
            return showKeepAlive ? labeledMessage + " (processing...)" : labeledMessage;
        }

        private string BuildEtaText(double percentage)
        {
            if (_phaseStopwatch == null)
            {
                return ETA_PLACEHOLDER;
            }

            return FormatEta(DateTimeOffset.Now, EstimateRemaining(_phaseStopwatch.Elapsed, percentage));
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

                LogPreviousPhaseElapsed();
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
