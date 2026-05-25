using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.SectionWriters
{
    /// <summary>
    /// Unit tests for IgnoredFilesSectionWriter (Order=300).
    /// IgnoredFilesSectionWriter（Order=300）のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class IgnoredFilesSectionWriterTests
    {
        [Fact]
        public void Order_Is300()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(300);
            Assert.Equal(300, writer.Order);
        }

        [Fact]
        public void IsEnabled_WhenConfigTrue()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(300);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeIgnoredFiles: true);
            Assert.True(writer.IsEnabled(ctx));
        }

        [Fact]
        public void IsDisabled_WhenConfigFalse()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(300);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeIgnoredFiles: false);
            Assert.False(writer.IsEnabled(ctx));
        }

        [Fact]
        public void Write_EmptyList_OutputsNothing()
        {
            // IgnoredFilesSectionWriter returns early when no ignored files exist
            // 無視ファイルがない場合は早期リターンする
            var writer = SectionWriterTestBase.GetWriterByOrder(300);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeIgnoredFiles: true);
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Empty(output);
        }
    }
}
