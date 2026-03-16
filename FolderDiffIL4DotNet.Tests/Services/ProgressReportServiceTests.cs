using System;
using System.Reflection;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class ProgressReportServiceTests
    {
        [Fact]
        public void Dispose_StopsTimer_AndFurtherCallsAreIgnored()
        {
            var service = new ProgressReportService(new ConfigSettings());
            service.SetLabel("test");
            service.ReportProgress(0.0);

            service.Dispose();
            service.ReportProgress(1.0);
            service.SetLabel("updated");

            var timerField = typeof(ProgressReportService).GetField("_keepAliveTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(timerField);
            Assert.Null(timerField.GetValue(service));
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(100.01)]
        public void ReportProgress_OutOfRange_Throws(double invalidPercentage)
        {
            var service = new ProgressReportService(new ConfigSettings());
            Assert.Throws<ArgumentOutOfRangeException>(() => service.ReportProgress(invalidPercentage));
        }

        [Fact]
        public void ReportProgress_WhenProgressGoesBackward_IgnoresUpdate()
        {
            var service = new ProgressReportService(new ConfigSettings());

            service.ReportProgress(10.0);
            service.ReportProgress(5.0);

            var lastPercentage = GetPrivateField<double>(service, "_lastPercentage");
            Assert.Equal(10.0, lastPercentage);
        }

        [Fact]
        public void SetLabel_TrimsWhitespace_AndBlankResetsLabel()
        {
            var service = new ProgressReportService(new ConfigSettings());

            service.SetLabel("  diffing  ");
            Assert.Equal("diffing", GetPrivateField<string>(service, "_labelPrefix"));

            service.SetLabel("   ");
            Assert.Null(GetPrivateField<string>(service, "_labelPrefix"));
        }

        [Fact]
        public void BuildRedirectedProgressLine_UsesLabelAndKeepAliveFormat()
        {
            var service = new ProgressReportService(new ConfigSettings());
            service.SetLabel("Phase1");

            var result = InvokePrivate<string>(
                service,
                "BuildRedirectedProgressLine",
                "50.00",
                true);

            Assert.Equal("Phase1: 50.00% (processing...)", result);
        }

        [Fact]
        public void BuildProgressBarLine_WithLabel_ContainsSpinnerAndPercentage()
        {
            var service = new ProgressReportService(new ConfigSettings());
            service.SetLabel("Scan");

            var result = InvokePrivate<string>(
                service,
                "BuildProgressBarLine",
                "25.00",
                25.0,
                true);

            Assert.Contains("Scan", result);
            Assert.Contains("25.00%", result);
            Assert.Contains("[", result);
            Assert.Contains("]", result);
        }

        [Fact]
        public void BuildProgressBarLine_WithoutLabelAndKeepAlive_AppendsSpinnerFrame()
        {
            var service = new ProgressReportService(new ConfigSettings());

            var result = InvokePrivate<string>(
                service,
                "BuildProgressBarLine",
                "12.34",
                12.34,
                true);

            Assert.Contains("12.34%", result);
            Assert.EndsWith("|", result);
        }

        [Fact]
        public void BuildProgressBarLine_WithCustomSpinnerFrames_UsesFirstConfiguredFrame()
        {
            var config = new ConfigSettings { SpinnerFrames = [">>", "<<", "=="] };
            var service = new ProgressReportService(config);

            var result = InvokePrivate<string>(
                service,
                "BuildProgressBarLine",
                "50.00",
                50.0,
                true);

            Assert.Contains("50.00%", result);
            Assert.EndsWith(">>", result);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (T)field.GetValue(target);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var result = method.Invoke(target, args);
            return Assert.IsType<T>(result);
        }
    }
}
