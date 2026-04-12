using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.SectionWriters
{
    /// <summary>
    /// Unit tests for ILCacheStatsSectionWriter (Order=900).
    /// ILCacheStatsSectionWriter（Order=900）のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class ILCacheStatsSectionWriterTests
    {
        [Fact]
        public void Order_Is900()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(900);
            Assert.Equal(900, writer.Order);
        }

        [Fact]
        public void IsDisabled_WhenConfigFalse()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(900);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeILCacheStats: false);
            Assert.False(writer.IsEnabled(ctx));
        }

        [Fact]
        public void IsDisabled_WhenConfigTrueButCacheNull()
        {
            // IlCache is null in CreateMinimalContext, so even with config enabled it should be disabled
            // CreateMinimalContext の IlCache は null なので、設定有効でも無効になるべき
            var writer = SectionWriterTestBase.GetWriterByOrder(900);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeILCacheStats: true);
            Assert.False(writer.IsEnabled(ctx));
        }
    }
}
