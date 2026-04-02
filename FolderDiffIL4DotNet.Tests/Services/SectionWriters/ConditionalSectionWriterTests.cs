using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.SectionWriters
{
    /// <summary>
    /// Unit tests for section writers with conditional IsEnabled (IgnoredFiles, Unchanged, ILCacheStats, Warnings).
    /// 条件付き IsEnabled を持つセクションライターのユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class ConditionalSectionWriterTests
    {
        // ── IgnoredFilesSectionWriter (Order=300) ──

        [Fact]
        public void IgnoredFiles_IsEnabled_WhenConfigEnabled()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(300);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeIgnoredFiles: true);
            Assert.True(writer.IsEnabled(ctx));
        }

        [Fact]
        public void IgnoredFiles_IsDisabled_WhenConfigDisabled()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(300);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeIgnoredFiles: false);
            Assert.False(writer.IsEnabled(ctx));
        }

        [Fact]
        public void IgnoredFiles_Write_ContainsIgnoredHeader()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(300);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeIgnoredFiles: true);
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Contains("Ignored", output);
        }

        // ── UnchangedFilesSectionWriter (Order=400) ──

        [Fact]
        public void Unchanged_IsEnabled_WhenConfigEnabled()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(400);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeUnchangedFiles: true);
            Assert.True(writer.IsEnabled(ctx));
        }

        [Fact]
        public void Unchanged_IsDisabled_WhenConfigDisabled()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(400);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeUnchangedFiles: false);
            Assert.False(writer.IsEnabled(ctx));
        }

        // ── ILCacheStatsSectionWriter (Order=900) ──

        [Fact]
        public void ILCacheStats_IsDisabled_WhenCacheNull()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(900);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeILCacheStats: true);
            // IlCache is null by default / IlCache はデフォルトで null
            Assert.False(writer.IsEnabled(ctx));
        }

        [Fact]
        public void ILCacheStats_IsDisabled_WhenConfigDisabled()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(900);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeILCacheStats: false);
            Assert.False(writer.IsEnabled(ctx));
        }

        // ── WarningsSectionWriter (Order=1000) ──

        [Fact]
        public void Warnings_IsDisabled_WhenNoWarnings()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            Assert.False(writer.IsEnabled(ctx));
        }

        [Fact]
        public void Warnings_IsEnabled_WhenSha256Mismatch()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext(hasSha256Mismatch: true);
            Assert.True(writer.IsEnabled(ctx));
        }

        [Fact]
        public void Warnings_IsEnabled_WhenTimestampRegression()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext(hasTimestampRegressionWarning: true);
            Assert.True(writer.IsEnabled(ctx));
        }

        [Fact]
        public void Warnings_Write_ContainsWarningKeyword()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext(hasSha256Mismatch: true);
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Contains("Warning", output);
        }
    }
}
