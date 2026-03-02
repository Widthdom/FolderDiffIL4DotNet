using System.Reflection;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class ProgressReportServiceTests
    {
        [Fact]
        public void Dispose_StopsTimer_AndFurtherCallsAreIgnored()
        {
            var service = new ProgressReportService();
            service.SetLabel("test");
            service.ReportProgress(0.0);

            service.Dispose();
            service.ReportProgress(1.0);
            service.SetLabel("updated");

            var timerField = typeof(ProgressReportService).GetField("_keepAliveTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(timerField);
            Assert.Null(timerField.GetValue(service));
        }
    }
}
