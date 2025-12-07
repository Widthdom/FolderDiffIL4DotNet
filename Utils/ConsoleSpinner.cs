using System;
using System.Threading;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Utils
{
    /// <summary>
    /// シンプルなコンソールスピナー。指定したラベルと共に一定間隔で回転する文字を表示します。
    /// 無音区間でもユーザーに処理継続中であることを示す用途を想定しています。
    /// </summary>
    public sealed class ConsoleSpinner : IDisposable
    {
        #region private constants
        /// <summary>
        /// 既定のフレーム更新間隔（ミリ秒）。
        /// </summary>
        private const int DEFAULT_INTERVAL_MILLISECONDS = 120;
        #endregion

        #region private readonly member variables
        /// <summary>
        /// デフォルトのスピナーフレーム。
        /// </summary>
        private static readonly char[] DefaultFrames = ['|', '/', '-', '\\'];

        /// <summary>
        /// スピナー前に表示するラベル。
        /// </summary>
        private readonly string _label;

        /// <summary>
        /// スピナーに使用するフレーム集合。
        /// </summary>
        private readonly char[] _frames;

        /// <summary>
        /// フレーム更新間隔。
        /// </summary>
        private readonly TimeSpan _interval;

        /// <summary>
        /// アニメーション停止用のキャンセルトークン。
        /// </summary>
        private readonly CancellationTokenSource _cts = new();

        /// <summary>
        /// スピナーアニメーションを実行するタスク。
        /// </summary>
        private readonly Task _animationTask;
        #endregion

        #region private member variables
        /// <summary>
        /// 現在表示しているフレームインデックス。
        /// </summary>
        private int _frameIndex;

        /// <summary>
        /// 直近に描画したコンテンツの文字数（消去用）。
        /// </summary>
        private int _lastRenderLength;

        /// <summary>
        /// 停止済みかどうか。
        /// </summary>
        private bool _isStopped;
        #endregion

        /// <summary>
        /// スピナーを開始します。
        /// </summary>
        /// <param name="label">スピナーの前に表示するラベル。</param>
        /// <param name="intervalMilliseconds">フレーム更新間隔（ミリ秒）。</param>
        /// <param name="frames">スピナーで使用するフレーム文字（省略時は | / - \）。</param>
        public ConsoleSpinner(string label, int intervalMilliseconds = DEFAULT_INTERVAL_MILLISECONDS, char[] frames = null)
        {
            _label = label;
            _frames = frames ?? DefaultFrames;
            _interval = TimeSpan.FromMilliseconds(intervalMilliseconds);
            _animationTask = Task.Run(SpinAsync);
        }

        /// <summary>
        /// 指定間隔でスピナーフレームを更新しつつコンソールへ描画する内部ループ。
        /// </summary>
        private async Task SpinAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                var prefix = string.IsNullOrEmpty(_label) ? string.Empty : _label + " ";
                var frame = _frames[_frameIndex++ % _frames.Length];
                var text = $"{prefix}{frame}";
                _lastRenderLength = text.Length;
                Console.Write($"\r{text}");
                Console.Out.Flush();
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
        /// 直近で描画したスピナー行を消去します。
        /// </summary>
        private void ClearLine()
        {
            if (_lastRenderLength <= 0)
            {
                return;
            }
            Console.Write("\r" + new string(' ', _lastRenderLength) + "\r");
            Console.Out.Flush();
        }

        /// <summary>
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
        /// スピナーを停止し、任意の完了メッセージを出力します。
        /// </summary>
        /// <param name="completionMessage">停止後に出力するメッセージ。</param>
        public void Complete(string completionMessage = null)
        {
            StopInternal();
            if (!string.IsNullOrEmpty(completionMessage))
            {
                Console.WriteLine(completionMessage);
            }
        }

        /// <summary>
        /// スピナーを停止します。
        /// </summary>
        public void Dispose()
        {
            StopInternal();
            _cts.Dispose();
        }
    }
}
