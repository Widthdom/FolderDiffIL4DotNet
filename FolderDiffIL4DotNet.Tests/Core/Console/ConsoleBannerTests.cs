using FolderDiffIL4DotNet.Core.Console;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.Console
{
    [Trait("Category", "Unit")]
    public class ConsoleBannerTests
    {
        [Theory]
        [InlineData(0, "Can't sleep")]
        [InlineData(1, "Can't sleep")]
        [InlineData(2, "Can't sleep")]
        [InlineData(3, "still dark")]
        [InlineData(4, "still dark")]
        [InlineData(5, "up early")]
        [InlineData(6, "up early")]
        [InlineData(7, "breakfast")]
        [InlineData(8, "breakfast")]
        [InlineData(9, "lunchtime")]
        [InlineData(10, "lunchtime")]
        [InlineData(11, "had lunch")]
        [InlineData(12, "had lunch")]
        [InlineData(13, "coffee")]
        [InlineData(14, "coffee")]
        [InlineData(15, "sun is starting")]
        [InlineData(16, "sun is starting")]
        [InlineData(17, "done for the day")]
        [InlineData(18, "done for the day")]
        [InlineData(19, "dinner")]
        [InlineData(20, "dinner")]
        [InlineData(21, "overtime")]
        [InlineData(22, "overtime")]
        [InlineData(23, "day is almost over")]
        public void GetGreeting_ReturnsExpectedMessageForHour(int hour, string expectedSubstring)
        {
            string greeting = ConsoleBanner.GetGreeting(hour);

            Assert.Contains(expectedSubstring, greeting, System.StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(6)]
        [InlineData(12)]
        [InlineData(18)]
        [InlineData(23)]
        public void GetGreeting_NeverReturnsEmptyString(int hour)
        {
            string greeting = ConsoleBanner.GetGreeting(hour);

            Assert.False(string.IsNullOrWhiteSpace(greeting));
        }

        [Fact]
        public void GetGreeting_AllHoursCovered()
        {
            // Verify every hour 0-23 returns a non-fallback greeting
            // 0-23 の全時間帯で "Hello!" フォールバックでない挨拶が返ることを確認
            for (int h = 0; h < 24; h++)
            {
                string greeting = ConsoleBanner.GetGreeting(h);
                Assert.NotEqual("Hello!", greeting);
            }
        }
    }
}
