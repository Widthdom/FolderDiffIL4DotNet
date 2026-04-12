using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.SectionWriters
{
    /// <summary>
    /// Unit tests for WarningsSectionWriter (Order=1000).
    /// WarningsSectionWriter（Order=1000）のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class WarningsSectionWriterTests
    {
        [Fact]
        public void Order_Is1000()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            Assert.Equal(1000, writer.Order);
        }

        [Fact]
        public void IsDisabled_WhenNoWarnings()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            Assert.False(writer.IsEnabled(ctx));
        }

        [Fact]
        public void IsEnabled_WhenSha256Mismatch()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext(hasSha256Mismatch: true);
            Assert.True(writer.IsEnabled(ctx));
        }

        [Fact]
        public void IsEnabled_WhenTimestampRegression()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext(hasTimestampRegressionWarning: true);
            Assert.True(writer.IsEnabled(ctx));
        }

        [Fact]
        public void IsEnabled_WhenILFilterWarnings()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext(hasILFilterWarnings: true);
            Assert.True(writer.IsEnabled(ctx));
        }

        [Fact]
        public void Write_WithILFilterWarnings_ContainsWarningText()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext(hasILFilterWarnings: true);
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Contains("IL filter validation warnings", output);
        }

        [Fact]
        public void Write_WithILFilterWarnings_ContainsWarningMessage()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext(hasILFilterWarnings: true);
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Contains("ILIgnoreLineContainingStrings", output);
        }
    }
}
