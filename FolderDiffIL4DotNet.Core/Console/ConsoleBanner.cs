using System;

namespace FolderDiffIL4DotNet.Core.Console
{
    /// <summary>
    /// Prints the ASCII-art banner and a time-based greeting at application startup.
    /// アプリ起動時の ASCII アートバナーと時刻ベースの挨拶を出力するクラス。
    /// </summary>
    public static class ConsoleBanner
    {
        private const string Banner = """
            ███████╗ ██████╗ ██╗     ██████╗ ███████╗██████╗
            ██╔════╝██╔═══██╗██║     ██╔══██╗██╔════╝██╔══██╗
            █████╗  ██║   ██║██║     ██║  ██║█████╗  ██████╔╝
            ██╔══╝  ██║   ██║██║     ██║  ██║██╔══╝  ██╔══██╗
            ██║     ██║   ██║██║     ██║  ██║██║     ██║  ██║
            ██║     ╚██████╔╝███████╗██████╔╝███████╗██║  ██║
            ╚═╝      ╚═════╝ ╚══════╝╚═════╝ ╚══════╝╚═╝  ╚═╝

            ██████╗ ██╗███████╗███████╗██╗██╗     ██╗  ██╗
            ██╔══██╗██║██╔════╝██╔════╝██║██║     ██║  ██║
            ██║  ██║██║█████╗  █████╗  ██║██║     ███████║
            ██║  ██║██║██╔══╝  ██╔══╝  ██║██║     ╚════██║
            ██║  ██║██║██║     ██║     ██║██║          ██║
            ██████╔╝██║██║     ██║     ██║███████╗     ██║
            ╚═════╝ ╚═╝╚═╝     ╚═╝     ╚═╝╚══════╝     ╚═╝

            ██████╗  ██████╗ ████████╗███╗   ██╗███████╗████████╗
            ██╔══██╗██╔═══██╗╚══██╔══╝███║   ██║██╔════╝╚══██╔══╝
            ██║  ██║██║   ██║   ██║   ████╗  ██║█████╗     ██║
            ██║  ██║██║   ██║   ██║   ██╔██╗ ██║██╔══╝     ██║
            ██║  ██║██║   ██║   ██║   ██║╚██╗██║██║        ██║
            ██████╔╝╚██████╔╝   ██║   ██║ ╚████║███████╗   ██║
            ╚═════╝  ╚═════╝    ╚═╝   ╚═╝  ╚═══╝╚══════╝   ╚═╝

            """;

        /// <summary>
        /// Outputs the ASCII-art banner and a time-based greeting to the console.
        /// ASCII アートバナーと時刻ベースの挨拶をコンソールへ出力します。
        /// </summary>
        public static void Print()
        {
            System.Console.WriteLine();
            System.Console.WriteLine(Banner);
            PrintGreeting(DateTime.Now.Hour);
        }

        /// <summary>
        /// Prints a friendly greeting based on the local hour of day.
        /// ローカル時刻に応じた挨拶メッセージを出力します。
        /// </summary>
        /// <summary>
        /// Visible for testing. / テスト用に公開。
        /// </summary>
        public static void PrintGreeting(int hour)
        {
            string greeting = GetGreeting(hour);
            System.Console.WriteLine($"  {greeting}");
            System.Console.WriteLine();
        }

        /// <summary>
        /// Returns a greeting message for the given hour (0–23).
        /// 指定された時刻（0–23）に対する挨拶メッセージを返します。
        /// </summary>
        /// <summary>
        /// Visible for testing. / テスト用に公開。
        /// </summary>
        public static string GetGreeting(int hour) => hour switch
        {
            >= 0 and < 3  => "Can't sleep? Hope you can sleep in tomorrow.",
            >= 3 and < 5  => "It's still dark outside, isn't it?",
            >= 5 and < 7  => "You're up early! Did you sleep well?",
            >= 7 and < 9  => "Good morning! Have you had breakfast yet?",
            >= 9 and < 11 => "Almost lunchtime, hang in there!",
            >= 11 and < 13 => "Have you had lunch yet?",
            >= 13 and < 15 => "Breaks are important too. How about a coffee?",
            >= 15 and < 17 => "The sun is starting to set, isn't it?",
            >= 17 and < 19 => "Almost done for the day?",
            >= 19 and < 21 => "Have you had dinner yet?",
            >= 21 and < 23 => "Working overtime? Thank you for your hard work.",
            >= 23          => "The day is almost over. Take care of yourself!",
            _              => "Hello!",
        };
    }
}
