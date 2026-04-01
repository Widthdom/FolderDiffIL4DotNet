using System.Linq;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="IReportSectionWriter"/> Order and IsEnabled contract.
    /// <see cref="IReportSectionWriter"/> の Order と IsEnabled 契約のテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class ReportSectionWriterOrderTests
    {
        [Fact]
        public void CreateBuiltInSectionWriters_ReturnsNonEmptyList()
        {
            var writers = ReportGenerateService.CreateBuiltInSectionWriters();
            Assert.NotEmpty(writers);
        }

        [Fact]
        public void CreateBuiltInSectionWriters_AllHaveUniqueOrder()
        {
            var writers = ReportGenerateService.CreateBuiltInSectionWriters();
            var orders = writers.Select(w => w.Order).ToList();
            Assert.Equal(orders.Count, orders.Distinct().Count());
        }

        [Fact]
        public void CreateBuiltInSectionWriters_OrdersAreStrictlyIncreasing()
        {
            var writers = ReportGenerateService.CreateBuiltInSectionWriters();
            var orders = writers.Select(w => w.Order).ToList();
            for (int i = 1; i < orders.Count; i++)
            {
                Assert.True(orders[i] > orders[i - 1],
                    $"Writer at index {i} (Order={orders[i]}) should have higher Order than index {i - 1} (Order={orders[i - 1]})");
            }
        }

        [Fact]
        public void CreateBuiltInSectionWriters_AllOrdersArePositive()
        {
            var writers = ReportGenerateService.CreateBuiltInSectionWriters();
            foreach (var writer in writers)
            {
                Assert.True(writer.Order > 0, $"Writer {writer.GetType().Name} has non-positive Order: {writer.Order}");
            }
        }

        [Fact]
        public void CreateBuiltInSectionWriters_CountIsExpected()
        {
            // There are 10 built-in section writers
            // 組み込みセクションライターは10個
            var writers = ReportGenerateService.CreateBuiltInSectionWriters();
            Assert.Equal(10, writers.Count);
        }
    }
}
