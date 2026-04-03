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
            var now = DateTime.Now;
            System.Console.WriteLine();
            System.Console.WriteLine(Banner);
            PrintGreeting(now.Hour, now.Minute);
        }

        /// <summary>
        /// Prints a friendly greeting based on the local time.
        /// ローカル時刻に応じた挨拶メッセージを出力します。
        /// </summary>
        public static void PrintGreeting(int hour, int minute = 0)
        {
            string greeting = GetGreeting(hour, minute);
            System.Console.WriteLine($"  {greeting}");
            System.Console.WriteLine();
        }

        /// <summary>
        /// Returns a greeting message for the given time (hour 0–23, minute 0–59).
        /// 指定された時刻（時 0–23、分 0–59）に対する挨拶メッセージを返します。
        /// </summary>
        public static string GetGreeting(int hour, int minute = 0) => hour switch
        {
            >= 0 and < 3   => "あなたがお仕事でなく趣味の時間を過ごしていることを願っています。",
            >= 3 and < 5   => "明日はあなたが寝坊できる日だといいのですが...",
            >= 5 and < 7   => "朝早いですね。よく眠れましたか？",
            >= 7 and < 8   => "フォルダ比較は私に任せて朝ご飯を食べてくださいね。",
            >= 8 and < 10  => "おはようございます。朝ご飯は食べましたか？",
            >= 10 and < 11 => "休憩も大事です。コーヒータイムにしませんか？",
            >= 11 and < 12 => "お昼ご飯までもう少しでしょうか？",
            >= 12 and < 13 => "フォルダ比較は私に任せてお昼ご飯を食べてくださいね。",
            >= 13 and < 14 => "お昼ご飯は食べましたか？",
            >= 14 and < 15 => "休憩も大事です。コーヒータイムにしませんか？",
            >= 15 and < 17 => "今日のタスクは全て終わりそうでしょうか？",
            >= 17 and < 18 => "晩ご飯までもう少しでしょうか？",
            >= 18 and < 19 => "フォルダ比較は私に任せて晩ご飯を食べてくださいね。",
            >= 19 and < 20 => "晩ご飯は食べましたか？",
            >= 20 and < 21 => "今日中のタスクがまだ残っているのでしょうか？お疲れ様です。",
            >= 21 and < 22 => "フォルダ比較は私に任せてシャワーを浴びてきてください。",
            >= 22          => minute < 30 || hour < 23
                ? "夜分遅くまでお疲れ様です。シャワーは浴びましたか？"
                : "そろそろ日が変わってしまいます。健康に気をつけてくださいね。",
            _              => "Hello!",
        };
    }
}
