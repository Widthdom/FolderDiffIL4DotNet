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
        public static string GetGreeting(int hour) => hour switch
        {
            >= 0 and < 3   => "I hope you're enjoying hobby time, not working.",
            >= 3 and < 5   => "I hope tomorrow is a day you can sleep in...",
            >= 5 and < 7   => "You're up early! Did you sleep well?",
            >= 7 and < 8   => "Leave the diff to me and go have breakfast!",
            >= 8 and < 10  => "Good morning! Have you had breakfast?",
            >= 10 and < 11 => "Breaks matter. How about a coffee?",
            >= 11 and < 12 => "Almost lunchtime!",
            >= 12 and < 13 => "Leave the diff to me and go have lunch!",
            >= 13 and < 14 => "Have you had lunch?",
            >= 14 and < 15 => "Breaks matter. How about a coffee?",
            >= 15 and < 17 => "Think you'll finish all your tasks today?",
            >= 17 and < 18 => "Almost dinnertime!",
            >= 18 and < 19 => "Leave the diff to me and go have dinner!",
            >= 19 and < 20 => "Have you had dinner?",
            >= 20 and < 21 => "Still have tasks left today? Thank you for your hard work.",
            >= 21 and < 22 => "Leave the diff to me and go take a shower!",
            >= 22 and < 23 => "Working late. Have you taken a shower?",
            >= 23          => "The day is almost over. Take care of your health!",
            _              => "Hello!",
        };
    }
}
