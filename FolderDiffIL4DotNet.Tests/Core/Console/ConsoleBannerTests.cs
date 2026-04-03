using FolderDiffIL4DotNet.Core.Console;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.Console
{
    [Trait("Category", "Unit")]
    public class ConsoleBannerTests
    {
        [Theory]
        [InlineData(0, 0, "趣味の時間")]
        [InlineData(1, 30, "趣味の時間")]
        [InlineData(2, 59, "趣味の時間")]
        [InlineData(3, 0, "寝坊できる日")]
        [InlineData(4, 30, "寝坊できる日")]
        [InlineData(5, 0, "よく眠れましたか")]
        [InlineData(6, 30, "よく眠れましたか")]
        [InlineData(7, 0, "朝ご飯を食べてください")]
        [InlineData(7, 45, "朝ご飯を食べてください")]
        [InlineData(8, 0, "朝ご飯は食べましたか")]
        [InlineData(9, 30, "朝ご飯は食べましたか")]
        [InlineData(10, 0, "コーヒータイム")]
        [InlineData(10, 45, "コーヒータイム")]
        [InlineData(11, 0, "お昼ご飯までもう少し")]
        [InlineData(11, 30, "お昼ご飯までもう少し")]
        [InlineData(12, 0, "お昼ご飯を食べてください")]
        [InlineData(12, 45, "お昼ご飯を食べてください")]
        [InlineData(13, 0, "お昼ご飯は食べましたか")]
        [InlineData(13, 30, "お昼ご飯は食べましたか")]
        [InlineData(14, 0, "コーヒータイム")]
        [InlineData(14, 45, "コーヒータイム")]
        [InlineData(15, 0, "タスクは全て終わりそう")]
        [InlineData(16, 30, "タスクは全て終わりそう")]
        [InlineData(17, 0, "晩ご飯までもう少し")]
        [InlineData(17, 30, "晩ご飯までもう少し")]
        [InlineData(18, 0, "晩ご飯を食べてください")]
        [InlineData(18, 45, "晩ご飯を食べてください")]
        [InlineData(19, 0, "晩ご飯は食べましたか")]
        [InlineData(19, 30, "晩ご飯は食べましたか")]
        [InlineData(20, 0, "今日中のタスク")]
        [InlineData(20, 45, "今日中のタスク")]
        [InlineData(21, 0, "シャワーを浴びてきて")]
        [InlineData(21, 45, "シャワーを浴びてきて")]
        [InlineData(22, 0, "シャワーは浴びましたか")]
        [InlineData(22, 59, "シャワーは浴びましたか")]
        [InlineData(23, 0, "シャワーは浴びましたか")]
        [InlineData(23, 29, "シャワーは浴びましたか")]
        [InlineData(23, 30, "日が変わってしまいます")]
        [InlineData(23, 59, "日が変わってしまいます")]
        public void GetGreeting_ReturnsExpectedMessageForTime(int hour, int minute, string expectedSubstring)
        {
            string greeting = ConsoleBanner.GetGreeting(hour, minute);

            Assert.Contains(expectedSubstring, greeting);
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
            Assert.Contains("シャワー", before);
            Assert.Contains("日が変わって", after);
        }

        [Fact]
        public void GetGreeting_MealTimeSlots_SuggestLeavingDiffToTool()
        {
            // Meal-adjacent slots (7, 12, 18) suggest leaving the diff to the tool
            // 食事時間帯（7時、12時、18時）は「私に任せて」メッセージであることを確認
            Assert.Contains("私に任せて", ConsoleBanner.GetGreeting(7));
            Assert.Contains("私に任せて", ConsoleBanner.GetGreeting(12));
            Assert.Contains("私に任せて", ConsoleBanner.GetGreeting(18));
        }

        [Fact]
        public void GetGreeting_CoffeeBreakSlots_ReturnSameMessage()
        {
            // 10:00 and 14:00 both suggest coffee break
            // 10時と14時の両方でコーヒータイムが提案されることを確認
            string morning = ConsoleBanner.GetGreeting(10);
            string afternoon = ConsoleBanner.GetGreeting(14);

            Assert.Equal(morning, afternoon);
            Assert.Contains("コーヒータイム", morning);
        }
    }
}
