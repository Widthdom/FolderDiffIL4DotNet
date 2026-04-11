using System;
using System.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="SbomGenerateService"/> overwrite safety.
    /// <see cref="SbomGenerateService"/> の上書き安全性テスト。
    /// </summary>
    public sealed partial class SbomGenerateServiceTests
    {
        [Fact]
        public void GenerateSbom_WhenExistingFileIsReadOnly_OverwritesIt()
        {
            var logger = new TestLogger();
            var service = new SbomGenerateService(_resultLists, logger);
            var (oldDir, newDir, reportDir) = MakeDirs("readonly-overwrite");

            service.GenerateSbom(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host",
                new ConfigSettingsBuilder
                {
                    ShouldGenerateSbom = true,
                    SbomFormat = "CycloneDX"
                }.Build(),
                ilCache: null));

            var sbomPath = Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME);
            var firstContent = File.ReadAllText(sbomPath);

            service.GenerateSbom(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "2.0.0", elapsedTimeString: "0h 0m 2.0s",
                computerName: "test-host",
                new ConfigSettingsBuilder
                {
                    ShouldGenerateSbom = true,
                    SbomFormat = "CycloneDX"
                }.Build(),
                ilCache: null));

            var secondContent = File.ReadAllText(sbomPath);
            Assert.Contains("\"version\": \"2.0.0\"", secondContent, StringComparison.Ordinal);
            Assert.DoesNotContain("\"version\": \"1.0.0\"", secondContent, StringComparison.Ordinal);
            Assert.NotEqual(firstContent, secondContent);
            Assert.DoesNotContain(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
        }
    }
}
