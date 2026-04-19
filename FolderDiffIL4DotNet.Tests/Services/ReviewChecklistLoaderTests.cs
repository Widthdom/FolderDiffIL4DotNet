using System;
using System.IO;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="ReviewChecklistLoader"/>.
    /// <see cref="ReviewChecklistLoader"/> の単体テストです。
    /// </summary>
    public sealed class ReviewChecklistLoaderTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        public void Load_WhenChecklistPathResolutionFails_LogsWarningWithExceptionTypeAndReturnsEmpty()
        {
            using var appDataScope = new AppDataOverrideScope(
                Path.Combine(Path.GetTempPath(), "fd-review-checklist-loader-" + Guid.NewGuid().ToString("N")));
            AppContext.SetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, " ");

            var logger = new TestLogger();

            var items = ReviewChecklistLoader.Load(logger);

            Assert.Empty(items);
            var warning = Assert.Single(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Warning
                    && entry.Message.Contains("Review checklist path could not be resolved", StringComparison.Ordinal));
            Assert.Contains(nameof(InvalidOperationException), warning.Message, StringComparison.Ordinal);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }
    }
}
