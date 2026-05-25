using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.SectionWriters
{
    /// <summary>
    /// Unit tests for LegendSectionWriter (Order=200).
    /// LegendSectionWriter（Order=200）のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class LegendSectionWriterTests
    {
        private readonly IReportSectionWriter _writer = SectionWriterTestBase.GetWriterByOrder(200);

        [Fact]
        public void Order_Is200()
        {
            Assert.Equal(200, _writer.Order);
        }

        [Fact]
        public void IsEnabled_AlwaysTrue()
        {
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            Assert.True(_writer.IsEnabled(ctx));
        }

        [Fact]
        public void Write_ContainsLegendSection()
        {
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            string output = SectionWriterTestBase.WriteToString(_writer, ctx);
            Assert.Contains("Legend", output);
        }

        [Fact]
        public void Write_ContainsDiffResultTypes()
        {
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            string output = SectionWriterTestBase.WriteToString(_writer, ctx);
            // Should explain SHA256Match, ILMatch, etc. / SHA256Match, ILMatch 等の説明を含むべき
            Assert.Contains("SHA256", output);
        }
    }
}
