using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.SectionWriters
{
    /// <summary>
    /// Unit tests for SummarySectionWriter (Order=800).
    /// SummarySectionWriter（Order=800）のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class SummarySectionWriterTests
    {
        private readonly IReportSectionWriter _writer = SectionWriterTestBase.GetWriterByOrder(800);

        [Fact]
        public void Order_Is800()
        {
            Assert.Equal(800, _writer.Order);
        }

        [Fact]
        public void IsEnabled_AlwaysTrue()
        {
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            Assert.True(_writer.IsEnabled(ctx));
        }

        [Fact]
        public void Write_ContainsSummaryHeader()
        {
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            string output = SectionWriterTestBase.WriteToString(_writer, ctx);
            Assert.Contains("Summary", output);
        }

        [Fact]
        public void Write_ContainsComparedRow()
        {
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            string output = SectionWriterTestBase.WriteToString(_writer, ctx);
            // Summary section contains the Compared row with file counts
            // Summary セクションには比較対象ファイル数の行を含む
            Assert.Contains("Compared", output);
        }

        [Fact]
        public void Write_ContainsCounts()
        {
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            string output = SectionWriterTestBase.WriteToString(_writer, ctx);
            // Should contain numerical summary (all zeros for empty result lists)
            // 数値サマリーを含むべき（空の結果リストでは全ゼロ）
            Assert.Contains("0", output);
        }
    }
}
