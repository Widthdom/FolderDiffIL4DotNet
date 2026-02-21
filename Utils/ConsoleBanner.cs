using System;
using Figgle;

namespace FolderDiffIL4DotNet.Utils
{
    /// <summary>
    /// アプリ起動時のバナーを出力するクラス。
    /// </summary>
    internal static class ConsoleBanner
    {
        internal static void Print()
        {
            Console.WriteLine(FiggleFonts.Big.Render("FolderDiff").TrimEnd());
            Console.WriteLine(FiggleFonts.Big.Render("IL4DotNet").TrimEnd());
            Console.WriteLine();
        }
    }
}
