// ProgramRunnerTests.FailOnDiff.cs — --fail-on-diff CLI gating integration tests
// ProgramRunnerTests.FailOnDiff.cs — --fail-on-diff CLI ゲートの統合テスト

using System;
using System.Collections.Generic;
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
        public async Task RunAsync_ReportableDifferencesWithoutFailOnDiff_ReturnsSuccess()
        {
            string tempRoot = CreateFailOnDiffTempRoot();
            try
            {
                string oldDir = CreateDirectory(tempRoot, "old");
                string newDir = CreateDirectory(tempRoot, "new");
                File.WriteAllText(Path.Combine(newDir, "added.txt"), "added");

                await WithConfigFileAsync("""{"SkipIL":true}""", async () =>
                {
                    var result = await RunFailOnDiffComparisonAsync(tempRoot, oldDir, newDir, includeFailOnDiff: false);

                    Assert.Equal(0, result.ExitCode);
                    Assert.True(File.Exists(Path.Combine(result.ReportDirectory, "diff_report.md")));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task RunAsync_FailOnDiffWithoutReportableDifferences_ReturnsSuccess()
        {
            string tempRoot = CreateFailOnDiffTempRoot();
            try
            {
                string oldDir = CreateDirectory(tempRoot, "old");
                string newDir = CreateDirectory(tempRoot, "new");
                File.WriteAllText(Path.Combine(oldDir, "same.txt"), "same");
                File.WriteAllText(Path.Combine(newDir, "same.txt"), "same");

                await WithConfigFileAsync("""{"SkipIL":true}""", async () =>
                {
                    var result = await RunFailOnDiffComparisonAsync(tempRoot, oldDir, newDir, includeFailOnDiff: true);

                    Assert.Equal(0, result.ExitCode);
                    Assert.True(File.Exists(Path.Combine(result.ReportDirectory, "diff_report.md")));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task RunAsync_FailOnDiffWithFinalAddedRemovedAndModifiedEntries_ReturnsFiveAfterGeneratingArtifacts()
        {
            string tempRoot = CreateFailOnDiffTempRoot();
            try
            {
                string oldDir = CreateDirectory(tempRoot, "old");
                string newDir = CreateDirectory(tempRoot, "new");
                File.WriteAllText(Path.Combine(oldDir, "removed.txt"), "removed");
                File.WriteAllText(Path.Combine(newDir, "added.txt"), "added");
                File.WriteAllText(Path.Combine(oldDir, "modified.txt"), "before");
                File.WriteAllText(Path.Combine(newDir, "modified.txt"), "after");

                await WithConfigFileAsync("""{"SkipIL":true}""", async () =>
                {
                    var result = await RunFailOnDiffComparisonAsync(tempRoot, oldDir, newDir, includeFailOnDiff: true);

                    Assert.Equal(5, result.ExitCode);
                    Assert.True(File.Exists(Path.Combine(result.ReportDirectory, "diff_report.md")));
                    Assert.True(File.Exists(Path.Combine(result.ReportDirectory, "diff_report.html")));
                    Assert.True(File.Exists(Path.Combine(result.ReportDirectory, AuditLogGenerateService.AUDIT_LOG_FILE_NAME)));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task RunAsync_FailOnDiffWithOnlyIgnoredExtensionDifference_ReturnsSuccess()
        {
            string tempRoot = CreateFailOnDiffTempRoot();
            try
            {
                string oldDir = CreateDirectory(tempRoot, "old");
                string newDir = CreateDirectory(tempRoot, "new");
                File.WriteAllText(Path.Combine(newDir, "ignored.tmp"), "ignored");

                await WithConfigFileAsync("""{"SkipIL":true,"IgnoredExtensions":[".tmp"]}""", async () =>
                {
                    var result = await RunFailOnDiffComparisonAsync(tempRoot, oldDir, newDir, includeFailOnDiff: true);

                    Assert.Equal(0, result.ExitCode);
                    Assert.True(File.Exists(Path.Combine(result.ReportDirectory, "diff_report.md")));
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        private static string CreateFailOnDiffTempRoot()
            => Path.Combine(Path.GetTempPath(), "fd-fail-on-diff-" + Guid.NewGuid().ToString("N"));

        private static string CreateDirectory(string root, string name)
        {
            string path = Path.Combine(root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private static async Task<(int ExitCode, string ReportDirectory)> RunFailOnDiffComparisonAsync(
            string tempRoot,
            string oldDir,
            string newDir,
            bool includeFailOnDiff)
        {
            string reportsRoot = CreateDirectory(tempRoot, "reports");
            string reportLabel = "report_" + Guid.NewGuid().ToString("N");
            var args = new List<string>
            {
                oldDir,
                newDir,
                reportLabel,
                "--skip-il",
                "--no-pause",
                "--no-banner",
                "--output",
                reportsRoot,
            };
            if (includeFailOnDiff)
            {
                args.Add("--fail-on-diff");
            }

            var runner = new ProgramRunner(new TestLogger(logFileAbsolutePath: "test.log"), new ConfigService());
            int exitCode = await runner.RunAsync(args.ToArray());
            return (exitCode, Path.Combine(reportsRoot, reportLabel));
        }
    }
}
