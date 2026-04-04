using System;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Runner
{
    /// <summary>
    /// Unit tests for <see cref="DiffPipelineExecutor"/>.
    /// <see cref="DiffPipelineExecutor"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class DiffPipelineExecutorTests
    {
        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DiffPipelineExecutor(null!));
        }

        [Fact]
        public void Constructor_ValidLogger_DoesNotThrow()
        {
            var logger = new TestLogger();
            var executor = new DiffPipelineExecutor(logger);
            Assert.NotNull(executor);
        }

        [Fact]
        public void FormatElapsedTime_Zero_ReturnsZeroString()
        {
            string result = DiffPipelineExecutor.FormatElapsedTime(TimeSpan.Zero);
            Assert.Equal("0h 0m 0.0s", result);
        }

        [Fact]
        public void FormatElapsedTime_SubSecond_ShowsTenths()
        {
            string result = DiffPipelineExecutor.FormatElapsedTime(TimeSpan.FromMilliseconds(350));
            Assert.Equal("0h 0m 0.3s", result);
        }

        [Fact]
        public void FormatElapsedTime_MinutesAndSeconds_FormatsCorrectly()
        {
            string result = DiffPipelineExecutor.FormatElapsedTime(new TimeSpan(0, 5, 30) + TimeSpan.FromMilliseconds(100));
            Assert.Equal("0h 5m 30.1s", result);
        }

        [Fact]
        public void FormatElapsedTime_MultiHour_FormatsCorrectly()
        {
            string result = DiffPipelineExecutor.FormatElapsedTime(new TimeSpan(2, 15, 45) + TimeSpan.FromMilliseconds(900));
            Assert.Equal("2h 15m 45.9s", result);
        }

        [Fact]
        public void FormatElapsedTime_TruncatesMilliseconds_DoesNotRound()
        {
            // 999ms should show .9 not 1.0 / 999ms は .9 であり 1.0 ではない
            string result = DiffPipelineExecutor.FormatElapsedTime(TimeSpan.FromMilliseconds(999));
            Assert.Equal("0h 0m 0.9s", result);
        }

        [Fact]
        public void DiffPipelineResult_RecordEquality_Works()
        {
            var a = new DiffPipelineResult(true, false, false, 10, 2, 1, 3);
            var b = new DiffPipelineResult(true, false, false, 10, 2, 1, 3);
            Assert.Equal(a, b);
        }

        [Fact]
        public void DiffPipelineResult_DifferentValues_NotEqual()
        {
            var a = new DiffPipelineResult(true, false, false, 10, 2, 1, 3);
            var b = new DiffPipelineResult(false, false, false, 10, 2, 1, 3);
            Assert.NotEqual(a, b);
        }
    }
}
