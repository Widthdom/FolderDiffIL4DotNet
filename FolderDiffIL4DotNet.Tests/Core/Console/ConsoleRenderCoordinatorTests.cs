using System;
using System.Threading;
using FolderDiffIL4DotNet.Core.Console;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.Console
{
    [Trait("Category", "Unit")]
    public class ConsoleRenderCoordinatorTests
    {
        [Fact]
        public void RenderSyncRoot_IsNotNull()
        {
            Assert.NotNull(ConsoleRenderCoordinator.RenderSyncRoot);
        }

        [Fact]
        public void MarkProgressRendered_DoesNotThrow()
        {
            ConsoleRenderCoordinator.MarkProgressRendered();
        }

        [Fact]
        public void ShouldRenderSpinner_BeforeFirstMark_ReturnsTrue()
        {
            // Reset _lastProgressRenderTicks to DateTime.MinValue via reflection
            // リフレクションで _lastProgressRenderTicks を DateTime.MinValue にリセット
            var field = typeof(ConsoleRenderCoordinator).GetField(
                "_lastProgressRenderTicks",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(null, DateTime.MinValue.Ticks);

            var result = ConsoleRenderCoordinator.ShouldRenderSpinner(TimeSpan.FromSeconds(1));
            Assert.True(result);
        }

        [Fact]
        public void ShouldRenderSpinner_JustAfterMark_WithLargeWindow_ReturnsFalse()
        {
            ConsoleRenderCoordinator.MarkProgressRendered();
            // 1-hour window -- rendering is suppressed immediately after mark
            // 1 時間のウィンドウ → 直後は描画を抑制
            var result = ConsoleRenderCoordinator.ShouldRenderSpinner(TimeSpan.FromHours(1));
            Assert.False(result);
        }

        [Fact]
        public void ShouldRenderSpinner_AfterWindowExpired_ReturnsTrue()
        {
            ConsoleRenderCoordinator.MarkProgressRendered();
            // Zero-second window -- expires immediately
            // 0 秒のウィンドウ → 即座に期限切れ
            var result = ConsoleRenderCoordinator.ShouldRenderSpinner(TimeSpan.Zero);
            Assert.True(result);
        }
    }
}
