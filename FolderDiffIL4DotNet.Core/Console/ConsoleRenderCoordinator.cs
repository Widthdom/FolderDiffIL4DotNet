using System;
using System.Threading;

namespace FolderDiffIL4DotNet.Core.Console
{
    /// <summary>
    /// Coordinates console spinner and progress-bar rendering to prevent visual conflicts.
    /// コンソールのスピナー/進捗バー描画を調停します。
    /// </summary>
    public static class ConsoleRenderCoordinator
    {
        private static readonly object RenderLock = new object();
        private static long _lastProgressRenderTicks = DateTime.MinValue.Ticks;

        /// <summary>
        /// Synchronization lock for console rendering.
        /// コンソール描画の同期用ロック。
        /// </summary>
        public static object RenderSyncRoot => RenderLock;

        /// <summary>
        /// Records the timestamp of the most recent progress render.
        /// 直近の進捗描画時刻を記録します。
        /// </summary>
        public static void MarkProgressRendered()
        {
            Interlocked.Exchange(ref _lastProgressRenderTicks, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// Suppresses spinner rendering immediately after a progress render to avoid visual glitches.
        /// 進捗描画直後はスピナー描画を抑止します。
        /// </summary>
        public static bool ShouldRenderSpinner(TimeSpan suppressionWindow)
        {
            long lastTicks = Interlocked.Read(ref _lastProgressRenderTicks);
            if (lastTicks == DateTime.MinValue.Ticks)
            {
                return true;
            }
            return DateTime.UtcNow.Ticks - lastTicks >= suppressionWindow.Ticks;
        }
    }
}
