using System.IO;

namespace FolderDiffIL4DotNet.Services
{
    // IL-cache-stats section writer extracted from ReportGenerateService.SectionWriters.cs.
    // ReportGenerateService.SectionWriters.cs から抽出した IL Cache Stats セクションライタ。
    public sealed partial class ReportGenerateService
    {
        /// <summary>Writes the IL Cache Stats section (only when enabled and ilCache is non-null). / IL Cache Stats セクションを書き込みます。</summary>
        private sealed class ILCacheStatsSectionWriter : IReportSectionWriter
        {
            public int Order => 900;

            public bool IsEnabled(ReportWriteContext context) => context.Config.ShouldIncludeILCacheStatsInReport && context.IlCache != null;

            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                var stats = ctx.IlCache.GetReportStats();
                writer.WriteLine(REPORT_SECTION_IL_CACHE_STATS);
                writer.WriteLine();
                writer.WriteLine("| Metric | Value |");
                writer.WriteLine("|--------|------:|");
                writer.WriteLine($"| Hits | {stats.Hits} |");
                writer.WriteLine($"| Misses | {stats.Misses} |");
                writer.WriteLine($"| Hit Rate | {stats.HitRatePct:F1}% |");
                writer.WriteLine($"| Stores | {stats.Stores} |");
                writer.WriteLine($"| Evicted | {stats.Evicted} |");
                writer.WriteLine($"| Expired | {stats.Expired} |");
                writer.WriteLine();
            }
        }
    }
}
