using FolderDiffIL4DotNet.Core.Console;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.Console
{
    [Trait("Category", "Unit")]
    public class ConsoleBannerTests
    {
        [Theory]
        [InlineData(0, 0, "hobby time")]
        [InlineData(1, 30, "hobby time")]
        [InlineData(2, 59, "hobby time")]
        [InlineData(3, 0, "sleep in")]
        [InlineData(4, 30, "sleep in")]
        [InlineData(5, 0, "Did you sleep well")]
        [InlineData(6, 30, "Did you sleep well")]
        [InlineData(7, 0, "breakfast")]
        [InlineData(7, 45, "breakfast")]
        [InlineData(8, 0, "breakfast")]
        [InlineData(9, 30, "breakfast")]
        [InlineData(10, 0, "coffee")]
        [InlineData(10, 45, "coffee")]
        [InlineData(11, 0, "lunchtime")]
        [InlineData(11, 30, "lunchtime")]
        [InlineData(12, 0, "Leave the diff to me")]
        [InlineData(12, 45, "Leave the diff to me")]
        [InlineData(13, 0, "had lunch")]
        [InlineData(13, 30, "had lunch")]
        [InlineData(14, 0, "coffee")]
        [InlineData(14, 45, "coffee")]
        [InlineData(15, 0, "tasks today")]
        [InlineData(16, 30, "tasks today")]
        [InlineData(17, 0, "dinnertime")]
        [InlineData(17, 30, "dinnertime")]
        [InlineData(18, 0, "Leave the diff to me")]
        [InlineData(18, 45, "Leave the diff to me")]
        [InlineData(19, 0, "had dinner")]
        [InlineData(19, 30, "had dinner")]
        [InlineData(20, 0, "tasks left")]
        [InlineData(20, 45, "tasks left")]
        [InlineData(21, 0, "go take a shower")]
        [InlineData(21, 45, "go take a shower")]
        [InlineData(22, 0, "shower")]
        [InlineData(22, 59, "shower")]
        [InlineData(23, 0, "shower")]
        [InlineData(23, 29, "shower")]
        [InlineData(23, 30, "day is almost over")]
        [InlineData(23, 59, "day is almost over")]
        public void GetGreeting_ReturnsExpectedMessageForTime(int hour, int minute, string expectedSubstring)
        {
            string greeting = ConsoleBanner.GetGreeting(hour, minute);

            Assert.Contains(expectedSubstring, greeting, System.StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(6, 0)]
        [InlineData(12, 0)]
        [InlineData(18, 0)]
        [InlineData(23, 0)]
        [InlineData(23, 30)]
        public void GetGreeting_NeverReturnsEmptyString(int hour, int minute)
        {
            string greeting = ConsoleBanner.GetGreeting(hour, minute);

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
        public void GetGreeting_2330Boundary_SwitchesMessage()
        {
            // 23:29 and 23:30 should return different messages
            // 23:29 と 23:30 で異なるメッセージが返ることを確認
            string before = ConsoleBanner.GetGreeting(23, 29);
            string after = ConsoleBanner.GetGreeting(23, 30);

            Assert.NotEqual(before, after);
            Assert.Contains("shower", before, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("day is almost over", after, System.StringComparison.OrdinalIgnoreCase);
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
