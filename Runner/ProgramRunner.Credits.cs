namespace FolderDiffIL4DotNet
{
    // Credits partial: contains the credits text constant (easter egg).
    // クレジット部分: クレジットテキスト定数を格納（イースターエッグ）。
    public sealed partial class ProgramRunner
    {
        private const string CREDITS_TEXT =
            "\n" +
            "  ╔══════════════════════════════════════════════════════════════════════╗\n" +
            "  ║                     FolderDiffIL4DotNet Credits                      ║\n" +
            "  ╚══════════════════════════════════════════════════════════════════════╝\n" +
            "\n" +
            "  Crafted with care for release engineers\n" +
            "  who compare folders at 2 AM.\n" +
            "\n" +
            "  ── Core Technology ─────────────────────────────────────────────────────\n" +
            "\n" +
            "  .NET 8                     Runtime & SDK\n" +
            "  System.Reflection.Metadata Assembly analysis\n" +
            "  dotnet-ildasm              IL disassembly\n" +
            "  ilspycmd                   IL disassembly (fallback)\n" +
            "\n" +
            "  ── Open Source Libraries ───────────────────────────────────────────────\n" +
            "\n" +
            "  Nerdbank.GitVersioning     Semantic versioning\n" +
            "  coverlet.collector         Code coverage\n" +
            "  xUnit                      Test framework\n" +
            "  FsCheck.Xunit              Property-based testing\n" +
            "  Stryker.NET                Mutation testing\n" +
            "  DocFX                      API documentation\n" +
            "  BenchmarkDotNet            Performance benchmarks\n" +
            "\n" +
            "  ── NuGet Packages ──────────────────────────────────────────────────────\n" +
            "\n" +
            "  FolderDiffIL4DotNet.Core\n" +
            "    File comparison, assembly detection, Myers diff,\n" +
            "    encoding detection, and process helpers.\n" +
            "    https://www.nuget.org/packages/FolderDiffIL4DotNet.Core\n" +
            "\n" +
            "  FolderDiffIL4DotNet.Plugin.Abstractions\n" +
            "    Interfaces for building custom plugins:\n" +
            "    report formatters, section writers,\n" +
            "    file comparison hooks, and more.\n" +
            "    https://www.nuget.org/packages/FolderDiffIL4DotNet.Plugin.Abstractions\n" +
            "\n" +
            "  ── Distribution ────────────────────────────────────────────────────────\n" +
            "\n" +
            "  NuGet                      Package distribution\n" +
            "  GitHub Actions             CI/CD pipelines\n" +
            "\n" +
            "  ── Special Thanks ──────────────────────────────────────────────────────\n" +
            "\n" +
            "  The .NET open-source community\n" +
            "  Everyone who reviews 500 files before a deadline\n" +
            "  Coffee, matcha, beer, whisky, wine, ramen, and sushi\n" +
            "  for keeping us going\n" +
            "\n" +
            "  ── Philosophy ──────────────────────────────────────────────────────────\n" +
            "\n" +
            "  \"Signal over noise.\"\n" +
            "\n" +
            "  Every line of this tool exists to help you\n" +
            "  ship with confidence.\n" +
            "\n" +
            "  https://github.com/Widthdom/FolderDiffIL4DotNet\n";
    }
}
