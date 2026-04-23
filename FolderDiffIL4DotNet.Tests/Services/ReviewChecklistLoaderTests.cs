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
            Assert.Contains(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, warning.Message, StringComparison.Ordinal);
            Assert.Contains("OverridePresent=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains("OverrideValueType=String", warning.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(InvalidOperationException), warning.Message, StringComparison.Ordinal);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Load_WhenChecklistPathOverrideIsMalformed_LogsWarningAndReturnsEmpty()
        {
            object? originalOverride = AppContext.GetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY);
            try
            {
                AppContext.SetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, "\0review-checklist");
                var logger = new TestLogger();

                var items = ReviewChecklistLoader.Load(logger);

                Assert.Empty(items);
                var warning = Assert.Single(
                    logger.Entries,
                    entry => entry.LogLevel == AppLogLevel.Warning
                        && entry.Message.Contains("Review checklist path could not be resolved", StringComparison.Ordinal));
                Assert.Contains(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, warning.Message, StringComparison.Ordinal);
                Assert.Contains("OverridePresent=True", warning.Message, StringComparison.Ordinal);
                Assert.Contains("OverrideValueType=String", warning.Message, StringComparison.Ordinal);
                Assert.Contains(nameof(ArgumentException), warning.Message, StringComparison.Ordinal);
                Assert.True(warning.ShouldOutputMessageToConsole);
            }
            finally
            {
                AppContext.SetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, originalOverride);
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Load_WhenChecklistPathIsUnreadable_LogsWarningWithConsistentExceptionFormatAndReturnsEmpty()
        {
            using var appDataScope = new AppDataOverrideScope(
                Path.Combine(Path.GetTempPath(), "fd-review-checklist-loader-" + Guid.NewGuid().ToString("N")));
            var checklistPath = AppDataPaths.GetDefaultReviewChecklistFileAbsolutePath();
            Directory.CreateDirectory(Path.GetDirectoryName(checklistPath)!);
            File.WriteAllText(checklistPath, "[\"Item\"]");

            var logger = new TestLogger();

            using var lockStream = new FileStream(checklistPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var items = ReviewChecklistLoader.Load(logger);

            Assert.Empty(items);
            var warning = Assert.Single(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Warning
                    && entry.Message.Contains("could not be read and will be skipped", StringComparison.Ordinal));
            Assert.Contains($"'{checklistPath}'", warning.Message, StringComparison.Ordinal);
            Assert.Contains("ChecklistFileIsPathRooted=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains("ChecklistFileLooksPathLike=True", warning.Message, StringComparison.Ordinal);
            Assert.DoesNotContain($": {nameof(UnauthorizedAccessException)}:", warning.Message, StringComparison.Ordinal);
            Assert.True(
                warning.Message.Contains($", {nameof(UnauthorizedAccessException)}):", StringComparison.Ordinal)
                || warning.Message.Contains($", {nameof(IOException)}):", StringComparison.Ordinal));
            Assert.True(warning.ShouldOutputMessageToConsole);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Load_WhenChecklistJsonIsInvalid_LogsJsonCoordinatesAndReturnsEmpty()
        {
            using var appDataScope = new AppDataOverrideScope(
                Path.Combine(Path.GetTempPath(), "fd-review-checklist-loader-" + Guid.NewGuid().ToString("N")));
            var checklistPath = AppDataPaths.GetDefaultReviewChecklistFileAbsolutePath();
            Directory.CreateDirectory(Path.GetDirectoryName(checklistPath)!);
            File.WriteAllText(checklistPath, "{\n  invalid-json\n}", System.Text.Encoding.UTF8);

            var logger = new TestLogger();

            var items = ReviewChecklistLoader.Load(logger);

            Assert.Empty(items);
            var warning = Assert.Single(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Warning
                    && entry.Message.Contains("invalid JSON", StringComparison.Ordinal));
            Assert.Contains("LineNumber=", warning.Message, StringComparison.Ordinal);
            Assert.Contains("BytePositionInLine=", warning.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(System.Text.Json.JsonException), warning.Message, StringComparison.Ordinal);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }
    }
}
