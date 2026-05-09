using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Plugin.Abstractions;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Runner
{
    /// <summary>
    /// Unit tests for <see cref="DiffPipelineExecutor"/>.
    /// <see cref="DiffPipelineExecutor"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class DiffPipelineExecutorTests : IDisposable
    {
        private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"DiffPipelineExecutorTests_{Guid.NewGuid():N}");

        public DiffPipelineExecutorTests()
        {
            Directory.CreateDirectory(_tempRoot);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempRoot))
                {
                    Directory.Delete(_tempRoot, recursive: true);
                }
            }
#pragma warning disable CA1031 // Cleanup is best-effort in tests / テストのクリーンアップはベストエフォート
            catch
            {
            }
#pragma warning restore CA1031
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DiffPipelineExecutor(null!));
        }

        [Fact]
        public void Constructor_ValidLogger_DoesNotThrow()
        {
            var logger = new TestLogger();
            var executor = new DiffPipelineExecutor(logger);
            Assert.NotNull(executor);
        }

        [Fact]
        public void FormatElapsedTime_Zero_ReturnsZeroString()
        {
            string result = DiffPipelineExecutor.FormatElapsedTime(TimeSpan.Zero);
            Assert.Equal("0h 0m 0.0s", result);
        }

        [Fact]
        public void FormatElapsedTime_SubSecond_ShowsTenths()
        {
            string result = DiffPipelineExecutor.FormatElapsedTime(TimeSpan.FromMilliseconds(350));
            Assert.Equal("0h 0m 0.3s", result);
        }

        [Fact]
        public void FormatElapsedTime_MinutesAndSeconds_FormatsCorrectly()
        {
            string result = DiffPipelineExecutor.FormatElapsedTime(new TimeSpan(0, 5, 30) + TimeSpan.FromMilliseconds(100));
            Assert.Equal("0h 5m 30.1s", result);
        }

        [Fact]
        public void FormatElapsedTime_MultiHour_FormatsCorrectly()
        {
            string result = DiffPipelineExecutor.FormatElapsedTime(new TimeSpan(2, 15, 45) + TimeSpan.FromMilliseconds(900));
            Assert.Equal("2h 15m 45.9s", result);
        }

        [Fact]
        public void FormatElapsedTime_TruncatesMilliseconds_DoesNotRound()
        {
            // 999ms should show .9 not 1.0 / 999ms は .9 であり 1.0 ではない
            string result = DiffPipelineExecutor.FormatElapsedTime(TimeSpan.FromMilliseconds(999));
            Assert.Equal("0h 0m 0.9s", result);
        }

        [Fact]
        public void DiffPipelineResult_RecordEquality_Works()
        {
            var a = new DiffPipelineResult(true, false, false, 10, 2, 1, 3);
            var b = new DiffPipelineResult(true, false, false, 10, 2, 1, 3);
            Assert.Equal(a, b);
        }

        [Fact]
        public void DiffPipelineResult_DifferentValues_NotEqual()
        {
            var a = new DiffPipelineResult(true, false, false, 10, 2, 1, 3);
            var b = new DiffPipelineResult(false, false, false, 10, 2, 1, 3);
            Assert.NotEqual(a, b);
        }

        [Fact]
        public async Task ExecuteAsync_WhenPostProcessActionThrows_LogsActionAndExceptionTypeButReturnsSuccess()
        {
            var logger = new TestLogger();
            var executor = new DiffPipelineExecutor(logger);
            string oldDir = Path.Combine(_tempRoot, "old");
            string newDir = Path.Combine(_tempRoot, "new");
            string reportDir = Path.Combine(_tempRoot, "reports");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            var config = new ConfigSettingsBuilder().Build();
            var plugin = new ThrowingPostProcessPlugin();

            var result = await executor.ExecuteAsync(
                oldDir,
                newDir,
                reportDir,
                config,
                appVersion: "1.2.3",
                computerName: "test-host",
                plugins: new[] { plugin });

            Assert.Equal(0, result.AddedCount);
            Assert.Equal(0, result.RemovedCount);
            Assert.Equal(0, result.ModifiedCount);
            Assert.Equal(0, result.UnchangedCount);

            var warning = Assert.Single(logger.Entries, entry =>
                entry.LogLevel == AppLogLevel.Warning
                && entry.Message.Contains("Post-process action", StringComparison.Ordinal));
            Assert.Contains(nameof(ThrowingPostProcessAction), warning.Message, StringComparison.Ordinal);
            Assert.Contains("position 1/1", warning.Message, StringComparison.Ordinal);
            Assert.Contains("Order=0", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"ReportsFolder='{reportDir}'", warning.Message, StringComparison.Ordinal);
            Assert.Contains("Added=0", warning.Message, StringComparison.Ordinal);
            Assert.Contains("Removed=0", warning.Message, StringComparison.Ordinal);
            Assert.Contains("Modified=0", warning.Message, StringComparison.Ordinal);
            Assert.Contains("Unchanged=0", warning.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(InvalidOperationException), warning.Message, StringComparison.Ordinal);
            Assert.NotNull(warning.Exception);
        }

        [Fact]
        public async Task ExecuteAsync_WhenChecklistChangesBetweenMarkdownAndHtml_UsesSingleSnapshotForBothReports()
        {
            using var appDataScope = CreateAppDataOverrideScope("shared-snapshot");
            WriteChecklistFile(appDataScope, "Initial checklist item");

            var logger = new TestLogger();
            var executor = new DiffPipelineExecutor(logger);
            var (oldDir, newDir, reportDir) = CreateRunDirectories("shared-snapshot-run");
            var configBuilder = new ConfigSettingsBuilder();
            configBuilder.SkipIL = true;
            configBuilder.ShouldIncludeReviewChecklist = true;
            var config = configBuilder.Build();
            var plugin = new ChecklistMutationPlugin(
                appDataScope.ReviewChecklistFileAbsolutePath,
                ["Mutated checklist item"]);

            await executor.ExecuteAsync(
                oldDir,
                newDir,
                reportDir,
                config,
                appVersion: "1.2.3",
                computerName: "test-host",
                plugins: [plugin]);

            var markdown = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            Assert.Contains("Initial checklist item", markdown, StringComparison.Ordinal);
            Assert.DoesNotContain("Mutated checklist item", markdown, StringComparison.Ordinal);
            Assert.Contains("Initial checklist item", html, StringComparison.Ordinal);
            Assert.DoesNotContain("Mutated checklist item", html, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ExecuteAsync_WhenChecklistFileIsMissing_SkipsSilentlyWithoutCreatingHtmlReportDirectory()
        {
            using var appDataScope = CreateAppDataOverrideScope("missing-checklist");

            var logger = new TestLogger();
            var executor = new DiffPipelineExecutor(logger);
            var (oldDir, newDir, reportDir) = CreateRunDirectories("missing-checklist-run");
            var configBuilder = new ConfigSettingsBuilder();
            configBuilder.SkipIL = true;
            var config = configBuilder.Build();

            await executor.ExecuteAsync(
                oldDir,
                newDir,
                reportDir,
                config,
                appVersion: "1.2.3",
                computerName: "test-host");

            string htmlReportSettingsDirectory = Path.Combine(appDataScope.ApplicationDataRootAbsolutePath, "HtmlReport");
            Assert.False(Directory.Exists(htmlReportSettingsDirectory));
            Assert.DoesNotContain(
                logger.Entries,
                entry => entry.Message.Contains("Review checklist", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ExecuteAsync_WhenChecklistOptInDisabled_DoesNotLoadUserLocalChecklist()
        {
            using var appDataScope = CreateAppDataOverrideScope("checklist-disabled");
            WriteChecklistFile(appDataScope, "Local checklist item");

            var logger = new TestLogger();
            var executor = new DiffPipelineExecutor(logger);
            var (oldDir, newDir, reportDir) = CreateRunDirectories("checklist-disabled-run");
            var configBuilder = new ConfigSettingsBuilder();
            configBuilder.SkipIL = true;
            var config = configBuilder.Build();

            await executor.ExecuteAsync(
                oldDir,
                newDir,
                reportDir,
                config,
                appVersion: "1.2.3",
                computerName: "test-host");

            var markdown = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));
            Assert.DoesNotContain("Local checklist item", markdown, StringComparison.Ordinal);
            Assert.DoesNotContain(
                logger.Entries,
                entry => entry.Message.Contains("Review checklist", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ExecuteAsync_WhenChecklistJsonIsInvalid_LogsSingleWarningAcrossMarkdownAndHtml()
        {
            using var appDataScope = CreateAppDataOverrideScope("invalid-checklist");
            string checklistPath = appDataScope.ReviewChecklistFileAbsolutePath;
            Directory.CreateDirectory(Path.GetDirectoryName(checklistPath)!);
            File.WriteAllText(checklistPath, "{ invalid-json");

            var logger = new TestLogger();
            var executor = new DiffPipelineExecutor(logger);
            var (oldDir, newDir, reportDir) = CreateRunDirectories("invalid-checklist-run");
            var configBuilder = new ConfigSettingsBuilder();
            configBuilder.SkipIL = true;
            configBuilder.ShouldIncludeReviewChecklist = true;
            var config = configBuilder.Build();

            await executor.ExecuteAsync(
                oldDir,
                newDir,
                reportDir,
                config,
                appVersion: "1.2.3",
                computerName: "test-host");

            var warning = Assert.Single(
                logger.Entries,
                entry => entry.LogLevel == AppLogLevel.Warning
                    && entry.Message.Contains("Review checklist file", StringComparison.Ordinal));
            Assert.Contains("invalid JSON", warning.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(JsonException), warning.Message, StringComparison.Ordinal);
            Assert.True(warning.ShouldOutputMessageToConsole);
        }


        private (string OldDir, string NewDir, string ReportDir) CreateRunDirectories(string name)
        {
            string oldDir = Path.Combine(_tempRoot, name, "old");
            string newDir = Path.Combine(_tempRoot, name, "new");
            string reportDir = Path.Combine(_tempRoot, name, "reports");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);
            return (oldDir, newDir, reportDir);
        }

        private static AppDataOverrideScope CreateAppDataOverrideScope(string name)
            => new(Path.Combine(Path.GetTempPath(), "fd-pipeline-appdata-" + name + "-" + Guid.NewGuid().ToString("N")));

        private static void WriteChecklistFile(AppDataOverrideScope appDataScope, params string[] items)
        {
            string checklistPath = appDataScope.ReviewChecklistFileAbsolutePath;
            Directory.CreateDirectory(Path.GetDirectoryName(checklistPath)!);
            File.WriteAllText(checklistPath, JsonSerializer.Serialize(items));
        }

        private sealed class ThrowingPostProcessPlugin : IPlugin
        {
            public PluginMetadata Metadata { get; } = new()
            {
                Id = "tests.throwing-postprocess",
                DisplayName = "Throwing PostProcess Test Plugin",
                Version = new Version(1, 0, 0),
                MinHostVersion = new Version(0, 0, 0)
            };

            public void ConfigureServices(IServiceCollection services, IReadOnlyDictionary<string, JsonElement> pluginConfig)
            {
                services.AddSingleton<IPostProcessAction, ThrowingPostProcessAction>();
            }
        }

        private sealed class ThrowingPostProcessAction : IPostProcessAction
        {
            public int Order => 0;

            public Task ExecuteAsync(PostProcessContext context, CancellationToken cancellationToken)
                => throw new InvalidOperationException("simulated post-process failure");
        }

        private sealed class ChecklistMutationPlugin : IPlugin
        {
            private readonly string _checklistFilePath;
            private readonly IReadOnlyList<string> _replacementItems;

            public ChecklistMutationPlugin(string checklistFilePath, IReadOnlyList<string> replacementItems)
            {
                _checklistFilePath = checklistFilePath;
                _replacementItems = replacementItems;
            }

            public PluginMetadata Metadata { get; } = new()
            {
                Id = "tests.checklist-mutation",
                DisplayName = "Checklist Mutation Test Plugin",
                Version = new Version(1, 0, 0),
                MinHostVersion = new Version(0, 0, 0)
            };

            public void ConfigureServices(IServiceCollection services, IReadOnlyDictionary<string, JsonElement> pluginConfig)
            {
                services.AddSingleton<IReportFormatter>(new ChecklistMutationFormatter(_checklistFilePath, _replacementItems));
            }
        }

        private sealed class ChecklistMutationFormatter : IReportFormatter
        {
            private readonly string _checklistFilePath;
            private readonly IReadOnlyList<string> _replacementItems;

            public ChecklistMutationFormatter(string checklistFilePath, IReadOnlyList<string> replacementItems)
            {
                _checklistFilePath = checklistFilePath;
                _replacementItems = replacementItems;
            }

            public string FormatId => "test-checklist-mutation";

            public int Order => 150;

            public bool IsEnabled(ReportGenerationContext context) => true;

            public void Generate(ReportGenerationContext context)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_checklistFilePath)!);
                File.WriteAllText(_checklistFilePath, JsonSerializer.Serialize(_replacementItems));
            }
        }
    }
}
