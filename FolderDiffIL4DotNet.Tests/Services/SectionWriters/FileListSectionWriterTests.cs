using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.SectionWriters
{
    /// <summary>
    /// Unit tests for file list section writers (Unchanged=400, Added=500, Removed=600, Modified=700).
    /// ファイル一覧セクションライター（Unchanged=400, Added=500, Removed=600, Modified=700）のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class FileListSectionWriterTests
    {
        // ── AddedFilesSectionWriter (Order=500) ──

        [Fact]
        public void Added_Order_Is500()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(500);
            Assert.Equal(500, writer.Order);
        }

        [Fact]
        public void Added_IsEnabled_AlwaysTrue()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(500);
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            Assert.True(writer.IsEnabled(ctx));
        }

        [Fact]
        public void Added_Write_ContainsAddedHeader()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(500);
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Contains("Added", output);
        }

        [Fact]
        public void Added_Write_EmptyList_OutputsNoFilesMessage()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(500);
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            // With no added files, output should indicate zero or "no" / 追加ファイルなしの場合、ゼロまたは "no" を示すべき
            Assert.True(output.Contains("0") || output.Contains("No ") || output.Contains("no "),
                "Expected zero/no indication for empty added files list");
        }

        // ── RemovedFilesSectionWriter (Order=600) ──

        [Fact]
        public void Removed_Order_Is600()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(600);
            Assert.Equal(600, writer.Order);
        }

        [Fact]
        public void Removed_IsEnabled_AlwaysTrue()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(600);
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            Assert.True(writer.IsEnabled(ctx));
        }

        [Fact]
        public void Removed_Write_ContainsRemovedHeader()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(600);
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Contains("Removed", output);
        }

        // ── ModifiedFilesSectionWriter (Order=700) ──

        [Fact]
        public void Modified_Order_Is700()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(700);
            Assert.Equal(700, writer.Order);
        }

        [Fact]
        public void Modified_IsEnabled_AlwaysTrue()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(700);
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            Assert.True(writer.IsEnabled(ctx));
        }

        [Fact]
        public void Modified_Write_ContainsModifiedHeader()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(700);
            var ctx = SectionWriterTestBase.CreateMinimalContext();
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Contains("Modified", output);
        }

        // ── UnchangedFilesSectionWriter (Order=400) ──

        [Fact]
        public void Unchanged_Order_Is400()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(400);
            Assert.Equal(400, writer.Order);
        }

        [Fact]
        public void Unchanged_IsEnabled_WhenConfigTrue()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(400);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeUnchangedFiles: true);
            Assert.True(writer.IsEnabled(ctx));
        }

        [Fact]
        public void Unchanged_IsDisabled_WhenConfigFalse()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(400);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeUnchangedFiles: false);
            Assert.False(writer.IsEnabled(ctx));
        }

        [Fact]
        public void Unchanged_Write_ContainsUnchangedHeader()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(400);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeUnchangedFiles: true);
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Contains("Unchanged", output);
        }

        [Fact]
        public void Unchanged_Write_ContainsTableHeader()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(400);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeUnchangedFiles: true);
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Contains("Diff Reason", output);
        }

        [Fact]
        public void Unchanged_Write_EmptyList_OutputsZeroCount()
        {
            var writer = SectionWriterTestBase.GetWriterByOrder(400);
            var ctx = SectionWriterTestBase.CreateMinimalContext(shouldIncludeUnchangedFiles: true);
            string output = SectionWriterTestBase.WriteToString(writer, ctx);
            Assert.Contains("(0)", output);
        }
    }
}
