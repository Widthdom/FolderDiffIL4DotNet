using FolderDiffIL4DotNet.Core.Console;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.Console
{
    [Trait("Category", "Unit")]
    public class ConsoleBannerTests
    {
        [Theory]
        [InlineData(0, "hobby time")]
        [InlineData(1, "hobby time")]
        [InlineData(2, "hobby time")]
        [InlineData(3, "sleep in")]
        [InlineData(4, "sleep in")]
        [InlineData(5, "Did you sleep well")]
        [InlineData(6, "Did you sleep well")]
        [InlineData(7, "Leave the diff to me")]
        [InlineData(8, "breakfast")]
        [InlineData(9, "breakfast")]
        [InlineData(10, "coffee")]
        [InlineData(11, "lunchtime")]
        [InlineData(12, "Leave the diff to me")]
        [InlineData(13, "had lunch")]
        [InlineData(14, "coffee")]
        [InlineData(15, "tasks today")]
        [InlineData(16, "tasks today")]
        [InlineData(17, "dinnertime")]
        [InlineData(18, "Leave the diff to me")]
        [InlineData(19, "had dinner")]
        [InlineData(20, "tasks left")]
        [InlineData(21, "go take a shower")]
        [InlineData(22, "Have you taken a shower")]
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

        [Fact]
        public void GetGreeting_MealTimeSlots_SuggestLeavingDiffToTool()
        {
            // Meal-adjacent slots (7, 12, 18) suggest leaving the diff to the tool
            // 食事時間帯（7時、12時、18時）は「Leave the diff to me」メッセージであることを確認
            Assert.Contains("Leave the diff to me", ConsoleBanner.GetGreeting(7));
            Assert.Contains("Leave the diff to me", ConsoleBanner.GetGreeting(12));
            Assert.Contains("Leave the diff to me", ConsoleBanner.GetGreeting(18));
        }

        [Fact]
        public void GetGreeting_CoffeeBreakSlots_ReturnSameMessage()
        {
            // 10:00 and 14:00 both suggest coffee break
            // 10時と14時の両方でコーヒーブレイクが提案されることを確認
            string morning = ConsoleBanner.GetGreeting(10);
            string afternoon = ConsoleBanner.GetGreeting(14);

            Assert.Equal(morning, afternoon);
            Assert.Contains("coffee", morning, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
