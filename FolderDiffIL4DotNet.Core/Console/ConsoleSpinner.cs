using System;
using System.Threading;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Core.Console
{
    /// <summary>
    /// A simple console spinner that displays a rotating character alongside a label at a fixed interval,
    /// indicating to the user that processing is still in progress during otherwise silent periods.
    /// シンプルなコンソールスピナー。指定したラベルと共に一定間隔で回転する文字を表示し、
    /// 無音区間でもユーザーに処理継続中であることを示します。
    /// </summary>
    public sealed class ConsoleSpinner : IDisposable
    {
        private const int DEFAULT_INTERVAL_MILLISECONDS = 120;

        // Suppress spinner rendering briefly after a progress bar render to avoid visual glitches.
        // 進捗バー描画直後はスピナーを一時抑止し、表示の衝突を防ぐ。
        private static readonly TimeSpan SpinnerSuppressionInterval = TimeSpan.FromMilliseconds(800);
        private static readonly string[] DefaultFrames = ["|", "/", "-", "\\"];
        private readonly string _label;
        private readonly string[] _frames;
        private readonly TimeSpan _interval;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _animationTask;
        private int _frameIndex;
        private int _lastRenderLength;
        private bool _isStopped;

        /// <summary>
        /// Creates and starts a new spinner with the given label.
        /// 指定ラベルで新しいスピナーを作成・開始します。
        /// </summary>
        public ConsoleSpinner(string label, int intervalMilliseconds = DEFAULT_INTERVAL_MILLISECONDS, string[]? frames = null)
        {
            _label = label;
            _frames = frames ?? DefaultFrames;
            _interval = TimeSpan.FromMilliseconds(intervalMilliseconds);
            _animationTask = Task.Run(SpinAsync);
        }

        /// <summary>
        /// Internal animation loop that updates spinner frames at the configured interval.
        /// 指定間隔でスピナーフレームを更新しつつコンソールへ描画する内部ループ。
        /// </summary>
        private async Task SpinAsync()
        {
            if (System.Console.IsOutputRedirected)
            {
                return;
            }

            while (!_cts.IsCancellationRequested)
            {
                if (!ConsoleRenderCoordinator.ShouldRenderSpinner(SpinnerSuppressionInterval))
                {
                    try
                    {
                        await Task.Delay(_interval, _cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    continue;
                }

                var prefix = string.IsNullOrEmpty(_label) ? string.Empty : _label + " ";
                var frame = _frames[_frameIndex++ % _frames.Length];
                var text = $"{prefix}{frame}";
                _lastRenderLength = text.Length;
                lock (ConsoleRenderCoordinator.RenderSyncRoot)
                {
                    System.Console.Write($"\r{text}");
                    System.Console.Out.Flush();
                }
                try
                {
                    await Task.Delay(_interval, _cts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Erases the most recently rendered spinner line from the console.
        /// 直近で描画したスピナー行を消去します。
        /// </summary>
        private void ClearLine()
        {
            if (_lastRenderLength <= 0)
            {
                return;
            }
            lock (ConsoleRenderCoordinator.RenderSyncRoot)
            {
                System.Console.Write("\r" + new string(' ', _lastRenderLength) + "\r");
                System.Console.Out.Flush();
            }
        }

        /// <summary>
        /// Stops the spinner animation loop and clears any remaining rendered line.
        /// スピナー描画ループを停止し、残っているアニメーション行をクリアします。
        /// </summary>
        private void StopInternal()
        {
            if (_isStopped)
            {
                return;
            }
            _isStopped = true;
            _cts.Cancel();
            try
            {
                _animationTask.Wait();
            }
            catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
            {
                // ignore cancellation
            }
            ClearLine();
        }

        /// <summary>
        /// Stops the spinner and optionally prints a completion message.
        /// スピナーを停止し、任意の完了メッセージを出力します。
        /// </summary>
        public void Complete(string? completionMessage = null)
        {
            StopInternal();
            if (!string.IsNullOrEmpty(completionMessage))
            {
                System.Console.WriteLine(completionMessage);
            }
        }

        /// <summary>
        /// Stops the spinner and releases resources.
        /// スピナーを停止しリソースを解放します。
        /// </summary>
        public void Dispose()
        {
            StopInternal();
            _cts.Dispose();
        }
    }
}
