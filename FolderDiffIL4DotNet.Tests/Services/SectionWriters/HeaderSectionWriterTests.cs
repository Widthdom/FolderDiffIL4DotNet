using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.SectionWriters
{
    /// <summary>
    /// Unit tests for HeaderSectionWriter (Order=100).
    /// HeaderSectionWriter（Order=100）のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class HeaderSectionWriterTests
    {
        private readonly IReportSectionWriter _writer = SectionWriterTestBase.GetWriterByOrder(100);

        [Fact]
        public void Order_Is100()
        {
            Assert.Equal(100, _writer.Order);
        }

        [Fact]
        public void IsEnabled_AlwaysTrue()
        {
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            Assert.True(_writer.IsEnabled(ctx));
        }

        [Fact]
        public void Write_ContainsReportTitle()
        {
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            string output = SectionWriterTestBase.WriteToString(_writer, ctx);
            Assert.Contains("Folder Diff Report", output);
        }

        [Fact]
        public void Write_ContainsAppVersion()
        {
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            string output = SectionWriterTestBase.WriteToString(_writer, ctx);
            Assert.Contains("1.0.0-test", output);
        }

        [Fact]
        public void Write_ContainsPaths()
        {
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            string output = SectionWriterTestBase.WriteToString(_writer, ctx);
            Assert.Contains("/old", output);
            Assert.Contains("/new", output);
        }

        [Fact]
        public void Write_ContainsComputerName()
        {
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            string output = SectionWriterTestBase.WriteToString(_writer, ctx);
            Assert.Contains("TESTPC", output);
        }
    }
}
