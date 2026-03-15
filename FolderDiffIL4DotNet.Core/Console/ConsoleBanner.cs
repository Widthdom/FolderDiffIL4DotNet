using System;
using Figgle;

namespace FolderDiffIL4DotNet.Core.Console
{
    /// <summary>
    /// アプリ起動時のバナーを出力するクラス。
    /// </summary>
    public static class ConsoleBanner
    {
        /// <summary>
        /// アプリ起動時の ASCII アートバナーをコンソールへ出力します。
        /// </summary>
        public static void Print()
        {
            System.Console.WriteLine(FiggleFonts.Big.Render("FolderDiff").TrimEnd());
            System.Console.WriteLine(FiggleFonts.Big.Render("IL4DotNet").TrimEnd());
            System.Console.WriteLine();
        }
    }
}
