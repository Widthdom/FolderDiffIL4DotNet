// ProgramRunnerTests.ReportFolderSuggestions.cs — report-folder suggestion output tests
// ProgramRunnerTests.ReportFolderSuggestions.cs — レポートフォルダ候補表示テスト

using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests
{
    public sealed partial class ProgramRunnerTests
    {
        [Fact]
        public void OutputExistingReportFolderSuggestions_WhenFoldersExist_WritesSortedNames()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var reportsRoot = Path.Combine(Path.GetTempPath(), "fd-report-suggestions-" + Guid.NewGuid().ToString("N"));
            var originalOut = Console.Out;
            var writer = new StringWriter();

            Directory.CreateDirectory(reportsRoot);
            Directory.CreateDirectory(Path.Combine(reportsRoot, "zeta"));
            Directory.CreateDirectory(Path.Combine(reportsRoot, "Alpha"));
            File.WriteAllText(Path.Combine(reportsRoot, "note.txt"), "ignored");

            try
            {
                Console.SetOut(writer);

                runner.OutputExistingReportFolderSuggestions(reportsRoot);

                var text = writer.ToString();
                Assert.Contains($"Existing report folders under '{reportsRoot}':", text, StringComparison.Ordinal);
                Assert.DoesNotContain("note.txt", text, StringComparison.Ordinal);
                Assert.True(text.IndexOf("Alpha", StringComparison.Ordinal) < text.IndexOf("zeta", StringComparison.Ordinal));
            }
            finally
            {
                Console.SetOut(originalOut);
                TryDeleteDirectory(reportsRoot);
            }
        }

        [Fact]
        public async Task RunAsync_WithExistingReportDirectory_WritesExistingReportFolderNamesToConsole()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-program-runner-tests-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            var reportLabel = "existing_" + Guid.NewGuid().ToString("N");
            var siblingLabel = "sibling_" + Guid.NewGuid().ToString("N");
            var reportsRoot = Path.Combine(AppContext.BaseDirectory, "Reports");
            var reportDir = Path.Combine(reportsRoot, reportLabel);
            var siblingReportDir = Path.Combine(reportsRoot, siblingLabel);
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var originalOut = Console.Out;
            var writer = new StringWriter();

            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);
            Directory.CreateDirectory(siblingReportDir);

            try
            {
                Console.SetOut(writer);

                await WithMissingConfigFileAsync(async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { oldDir, newDir, reportLabel, "--no-pause" });
                    Assert.Equal(2, exitCode);
                });

                var text = writer.ToString();
                Assert.Contains($"Existing report folders under '{reportsRoot}':", text, StringComparison.Ordinal);
                Assert.Contains(reportLabel, text, StringComparison.Ordinal);
                Assert.Contains(siblingLabel, text, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetOut(originalOut);
                TryDeleteDirectory(tempRoot);
                TryDeleteDirectory(reportDir);
                TryDeleteDirectory(siblingReportDir);
            }
        }
    }
}
