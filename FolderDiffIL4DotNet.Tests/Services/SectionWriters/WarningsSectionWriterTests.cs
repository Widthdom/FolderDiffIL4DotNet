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
        public void IsEnabled_WhenChecklistItemsExist()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext(
                reviewChecklistItems:
                [
                    "Confirm release checklist."
                ]);
            Assert.True(writer.IsEnabled(ctx));
        }

        [Fact]
        public void Write_WithILFilterWarnings_ContainsGenericSafetyHeading()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext(hasILFilterWarnings: true);
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Contains("IL substring configuration safety warnings", output);
            Assert.DoesNotContain("IL filter validation warnings", output);
        }

        [Fact]
        public void Write_WithILFilterWarnings_ContainsWarningMessage()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext(hasILFilterWarnings: true);
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Contains("ILNormalizeContainingStrings", output);
            Assert.Contains("overlap by containment", output);
        }

        [Fact]
        public void Write_WithValidatedConfiguredNormalizationWarning_PreservesSafeEscapes()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext(hasILFilterWarnings: true);
            string warning = Assert.Single(ILOutputService.ValidateILNormalizeContainingStrings(
                new[] { "buildserver1_", "buildserver1_artifact" }));
            ctx.FileDiffResultLists.ILFilterWarnings.Clear();
            ctx.FileDiffResultLists.ILFilterWarnings.Add(warning);

            string output = SectionWriterTestBase.WriteToString(writer, ctx);

            Assert.Contains("buildserver1&#95;", output);
            Assert.Contains("buildserver1&#95;artifact", output);
            Assert.DoesNotContain("buildserver1_", output);
        }

        [Fact]
        public void Write_WithChecklistOnly_ContainsReviewChecklistSection()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(1000);
            var ctx = SectionWriterTestBase.CreateMinimalContext(
                reviewChecklistItems:
                [
                    "Confirm release notes.",
                    "Verify upgrade guide.\nInclude rollback notes."
                ]);
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Contains("## Review Checklist", output);
            Assert.Contains("| ✓ | Checklist Item | Notes |", output);
            Assert.Contains("Confirm release notes.", output);
            Assert.Contains("Verify upgrade guide.<br>Include rollback notes.", output);
        }
    }
}
