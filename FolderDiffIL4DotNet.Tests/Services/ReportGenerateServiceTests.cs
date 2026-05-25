using System;
using System.Collections.Generic;
using System.IO;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="ReportGenerateService"/> (base partial: fields, constructor, Dispose, helpers).
    /// <see cref="ReportGenerateService"/> のテスト（基底 partial: フィールド、コンストラクタ、Dispose、ヘルパー）。
    /// </summary>
    public sealed partial class ReportGenerateServiceTests : IDisposable
    {
        private readonly string _rootDir;
        private readonly FileDiffResultLists _resultLists = new();
        private readonly ILoggerService _logger = new LoggerService();
        private readonly ReportGenerateService _service;

        public ReportGenerateServiceTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-report-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
            _service = new ReportGenerateService(_resultLists, _logger, ReportGenerateService.CreateBuiltInSectionWriters());
            ClearResultLists();
        }

        public void Dispose()
        {
            ClearResultLists();
            try
            {
                if (Directory.Exists(_rootDir))
                {
                    Directory.Delete(_rootDir, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup errors / クリーンアップエラーを無視
            }
        }

        private static ReportGenerationContext CreateReportContext(
            string oldDir, string newDir, string reportDir,
            ConfigSettings config, ILCache? ilCache = null, IReadOnlyList<string>? reviewChecklistItems = null)
            => new(oldDir, newDir, reportDir,
                appVersion: "test", elapsedTimeString: "00:00:01.000",
                computerName: "test-host", config, ilCache, reviewChecklistItems);

        private static ConfigSettingsBuilder CreateConfigBuilder() => new()
        {
            IgnoredExtensions = new List<string>(),
            TextFileExtensions = new List<string>(),
            ShouldIncludeUnchangedFiles = true,
            ShouldIncludeIgnoredFiles = false,
            ShouldOutputFileTimestamps = false,
            ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = true
        };

        private static ConfigSettings CreateConfig() => CreateConfigBuilder().Build();

        private void ClearResultLists()
        {
            _resultLists.ResetAll();
        }
    }
}
