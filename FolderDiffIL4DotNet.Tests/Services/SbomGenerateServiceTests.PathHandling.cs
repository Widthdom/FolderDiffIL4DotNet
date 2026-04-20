using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Path-validation and recoverable-failure tests for <see cref="SbomGenerateService"/>.
    /// <see cref="SbomGenerateService"/> のパス検証と回復可能失敗のテスト。
    /// </summary>
    public sealed partial class SbomGenerateServiceTests
    {
        [Fact]
        public void GenerateSbom_WhenReportPathIsInvalid_LogsWarningAndDoesNotThrow()
        {
            var logger = new TestLogger();
            var service = new SbomGenerateService(_resultLists, logger);
            var oldDir = Path.Combine(_rootDir, "old-invalid-path");
            var newDir = Path.Combine(_rootDir, "new-invalid-path");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);

            var exception = Record.Exception(() =>
                service.GenerateSbom(new ReportGenerationContext(
                    oldDir, newDir, "\0invalid-report-dir",
                    appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                    computerName: "test-host",
                    new ConfigSettingsBuilder
                    {
                        ShouldGenerateSbom = true,
                        SbomFormat = "CycloneDX"
                    }.Build(),
                    ilCache: null)));

            Assert.Null(exception);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("CycloneDX", warning.Message, StringComparison.Ordinal);
            Assert.Contains("reports folder", warning.Message, StringComparison.Ordinal);
            Assert.Contains(SbomGenerateService.CYCLONEDX_FILE_NAME, warning.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(ArgumentException), warning.Message, StringComparison.Ordinal);
            Assert.NotNull(warning.Exception);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }

        [Fact]
        public void GenerateSbom_WhenComponentHashReadFails_LogsWarningAndStillWritesSbom()
        {
            var logger = new TestLogger();
            var service = new SbomGenerateService(_resultLists, logger);
            var (oldDir, newDir, reportDir) = MakeDirs("locked-component-hash");
            var addedPath = Path.Combine(newDir, "locked.dll");
            File.WriteAllText(addedPath, "locked-content");
            _resultLists.AddAddedFileAbsolutePath(addedPath);

            using var lockStream = new FileStream(addedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var exception = Record.Exception(() =>
                service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true, sbomFormat: "CycloneDX")));

            Assert.Null(exception);
            var sbomPath = Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME);
            Assert.True(File.Exists(sbomPath));
            var doc = JsonDocument.Parse(File.ReadAllText(sbomPath));
            var component = doc.RootElement.GetProperty("components")[0];
            var hashes = component.GetProperty("hashes");
            Assert.Equal(0, hashes.GetArrayLength());
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Failed to compute SBOM SHA256", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"Root='{newDir}'", warning.Message, StringComparison.Ordinal);
            Assert.Contains("IsPathRooted=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains(addedPath, warning.Message, StringComparison.Ordinal);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }

        [Fact]
        public void GenerateSbom_WhenRemovedComponentHashReadFails_LogsWarningAndStillWritesSbom()
        {
            var logger = new TestLogger();
            var service = new SbomGenerateService(_resultLists, logger);
            var (oldDir, newDir, reportDir) = MakeDirs("locked-removed-component-hash");
            var removedPath = Path.Combine(oldDir, "removed.dll");
            File.WriteAllText(removedPath, "removed-content");
            _resultLists.AddRemovedFileAbsolutePath(removedPath);

            using var lockStream = new FileStream(removedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var exception = Record.Exception(() =>
                service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true, sbomFormat: "CycloneDX")));

            Assert.Null(exception);
            var sbomPath = Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME);
            Assert.True(File.Exists(sbomPath));
            var doc = JsonDocument.Parse(File.ReadAllText(sbomPath));
            var component = doc.RootElement.GetProperty("components")[0];
            var hashes = component.GetProperty("hashes");
            Assert.Equal(0, hashes.GetArrayLength());
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Failed to compute SBOM SHA256", warning.Message, StringComparison.Ordinal);
            Assert.Contains("(Removed", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"Root='{oldDir}'", warning.Message, StringComparison.Ordinal);
            Assert.Contains("IsPathRooted=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains(removedPath, warning.Message, StringComparison.Ordinal);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }

        [Fact]
        public void GenerateSbom_WhenModifiedRelativePathIsInvalid_SkipsBadComponentAndStillWritesSbom()
        {
            var logger = new TestLogger();
            var service = new SbomGenerateService(_resultLists, logger);
            var (oldDir, newDir, reportDir) = MakeDirs("invalid-modified-component");
            File.WriteAllText(Path.Combine(newDir, "good.dll"), "good-content");
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "good.dll"));
            _resultLists.AddModifiedFileRelativePath("\0bad-relative-path");
            _resultLists.RecordDiffDetail("\0bad-relative-path", FileDiffResultLists.DiffDetailResult.TextMismatch);

            var exception = Record.Exception(() =>
                service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true, sbomFormat: "CycloneDX")));

            Assert.Null(exception);
            var sbomPath = Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME);
            Assert.True(File.Exists(sbomPath));
            var doc = JsonDocument.Parse(File.ReadAllText(sbomPath));
            var components = doc.RootElement.GetProperty("components");
            Assert.Equal(1, components.GetArrayLength());
            Assert.Equal("good.dll", components[0].GetProperty("name").GetString());
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Skipped SBOM component", warning.Message, StringComparison.Ordinal);
            Assert.Contains("Folder=new", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"Root='{newDir}'", warning.Message, StringComparison.Ordinal);
            Assert.Contains("IsPathRooted=", warning.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(ArgumentException), warning.Message, StringComparison.Ordinal);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }

        [Fact]
        public void TrySetReadOnly_WhenPathFails_LogsReportsFolderContext()
        {
            var logger = new TestLogger();
            var service = new SbomGenerateService(_resultLists, logger);
            var method = typeof(SbomGenerateService).GetMethod("TrySetReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            method.Invoke(service, ["/tmp/reports", "\0bad-sbom", global::FolderDiffIL4DotNet.Models.SbomFormat.CycloneDX]);

            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("reports folder '/tmp/reports'", warning.Message, StringComparison.Ordinal);
            Assert.Contains("CycloneDX", warning.Message, StringComparison.Ordinal);
            Assert.Contains("IsPathRooted=False", warning.Message, StringComparison.Ordinal);
            Assert.NotNull(warning.Exception);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }
    }
}
