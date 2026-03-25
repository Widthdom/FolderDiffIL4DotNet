using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="AuditLogGenerateService"/>.
    /// <see cref="AuditLogGenerateService"/> のテスト。
    /// </summary>
    public sealed partial class AuditLogGenerateServiceTests : IDisposable
    {
        private readonly string _rootDir;
        private readonly FileDiffResultLists _resultLists = new();
        private readonly ILoggerService _logger = new LoggerService();
        private readonly AuditLogGenerateService _service;

        public AuditLogGenerateServiceTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-audit-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
            _service = new AuditLogGenerateService(_resultLists, _logger);
        }

        public void Dispose()
        {
            _resultLists.ResetAll();
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

        // ── Basic generation / 基本生成 ──────────────────────────────────────────

        /// <summary>
        /// Verifies that audit_log.json is created when shouldGenerate is true.
        /// shouldGenerate が true の場合に audit_log.json が生成されることを確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_CreatesFile_WhenEnabled()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("basic");

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var auditLogPath = Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME);
            Assert.True(File.Exists(auditLogPath), "audit_log.json should be created");
        }

        /// <summary>
        /// Verifies that audit_log.json is NOT created when shouldGenerate is false.
        /// shouldGenerate が false の場合に audit_log.json が生成されないことを確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_DoesNotCreateFile_WhenDisabled()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("disabled");

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateAuditLog: false));

            var auditLogPath = Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME);
            Assert.False(File.Exists(auditLogPath), "audit_log.json should not be created when disabled");
        }

        // ── Metadata / メタデータ ────────────────────────────────────────────────

        /// <summary>
        /// Verifies that run metadata (appVersion, computerName, paths, elapsed time) is recorded.
        /// 実行メタデータ（アプリバージョン、マシン名、パス、経過時間）が記録されることを確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_ContainsRunMetadata()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("metadata");

            _service.GenerateAuditLog(new ReportGenerationContext(
                oldDir, newDir, reportDir,
                appVersion: "2.1.0", elapsedTimeString: "0h 5m 30.1s",
                computerName: "build-server-01",
                new ConfigSettingsBuilder { ShouldGenerateAuditLog = true }.Build(),
                ilCache: null));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            Assert.Contains("\"appVersion\": \"2.1.0\"", json);
            Assert.Contains("\"computerName\": \"build-server-01\"", json);
            Assert.Contains("\"elapsedTime\": \"0h 5m 30.1s\"", json);
            Assert.Contains("\"oldFolderPath\":", json);
            Assert.Contains("\"newFolderPath\":", json);
            Assert.Contains("\"timestamp\":", json);
        }

        // ── Summary statistics / サマリー統計 ─────────────────────────────────────

        /// <summary>
        /// Verifies that summary statistics (Added/Removed/Modified/Unchanged/Ignored counts) are correct.
        /// サマリー統計（各カテゴリの件数）が正しいことを確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_SummaryStatisticsMatchResultLists()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("summary");

            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "added.txt"));
            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "removed.txt"));
            _resultLists.AddModifiedFileRelativePath("modified.dll");
            _resultLists.RecordDiffDetail("modified.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm");
            _resultLists.AddUnchangedFileRelativePath("unchanged.config");
            _resultLists.RecordDiffDetail("unchanged.config", FileDiffResultLists.DiffDetailResult.SHA256Match);
            _resultLists.RecordIgnoredFile("ignored.pdb", FileDiffResultLists.IgnoredFileLocation.Old);

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<JsonElement>(json);
            var summary = record.GetProperty("summary");
            Assert.Equal(1, summary.GetProperty("added").GetInt32());
            Assert.Equal(1, summary.GetProperty("removed").GetInt32());
            Assert.Equal(1, summary.GetProperty("modified").GetInt32());
            Assert.Equal(1, summary.GetProperty("unchanged").GetInt32());
            Assert.Equal(1, summary.GetProperty("ignored").GetInt32());
        }

        // ── File entries / ファイルエントリ ───────────────────────────────────────

        /// <summary>
        /// Verifies that per-file entries are recorded with correct category and diffDetail.
        /// ファイルごとのエントリが正しいカテゴリと diffDetail で記録されることを確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_FileEntriesContainCategoryAndDiffDetail()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("file-entries");

            _resultLists.AddModifiedFileRelativePath("lib/core.dll");
            _resultLists.RecordDiffDetail("lib/core.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.AddUnchangedFileRelativePath("lib/utils.dll");
            _resultLists.RecordDiffDetail("lib/utils.dll", FileDiffResultLists.DiffDetailResult.SHA256Match);

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            Assert.Contains("\"category\": \"Modified\"", json);
            Assert.Contains("\"diffDetail\": \"ILMismatch\"", json);
            Assert.Contains("\"disassembler\": \"dotnet-ildasm (version: 0.12.0)\"", json);
            Assert.Contains("\"category\": \"Unchanged\"", json);
            Assert.Contains("\"diffDetail\": \"SHA256Match\"", json);
        }

        /// <summary>
        /// Verifies that Added/Removed files have empty diffDetail and disassembler fields.
        /// Added/Removed ファイルの diffDetail と disassembler フィールドが空であることを確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_AddedRemovedFiles_HaveEmptyDiffDetail()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("added-removed");

            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "new-file.txt"));
            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "old-file.txt"));

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<JsonElement>(json);
            var files = record.GetProperty("files");

            foreach (var file in files.EnumerateArray())
            {
                var category = file.GetProperty("category").GetString();
                if (category == "Added" || category == "Removed")
                {
                    Assert.Equal(string.Empty, file.GetProperty("diffDetail").GetString());
                    Assert.Equal(string.Empty, file.GetProperty("disassembler").GetString());
                }
            }
        }

        // ── Added/Removed relative paths / Added/Removed の相対パス ────────────────

        /// <summary>
        /// Verifies that Added/Removed files record full relative paths (not just file names),
        /// so that files in subdirectories are uniquely identifiable in the audit log.
        /// Added/Removed ファイルが（ファイル名だけでなく）完全な相対パスで記録され、
        /// サブディレクトリ内のファイルが監査ログで一意に識別可能であることを確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_AddedRemovedFiles_RecordRelativePaths()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("rel-paths");

            // Create nested files / ネストされたファイルを作成
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "sub", "dir", "new-file.txt"));
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "root-file.txt"));
            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "lib", "old-lib.dll"));
            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "old-root.txt"));

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<JsonElement>(json);
            var files = record.GetProperty("files");

            var paths = new System.Collections.Generic.List<string>();
            foreach (var file in files.EnumerateArray())
            {
                paths.Add(file.GetProperty("relativePath").GetString()!);
            }

            // Added files should have relative paths from newDir / Added は newDir からの相対パス
            Assert.Contains(Path.Combine("sub", "dir", "new-file.txt"), paths);
            Assert.Contains("root-file.txt", paths);

            // Removed files should have relative paths from oldDir / Removed は oldDir からの相対パス
            Assert.Contains(Path.Combine("lib", "old-lib.dll"), paths);
            Assert.Contains("old-root.txt", paths);
        }

        // ── File entries sorted / ファイルエントリのソート ─────────────────────────

        /// <summary>
        /// Verifies that file entries are sorted by Category then by RelativePath.
        /// ファイルエントリがカテゴリ順、次に相対パス順でソートされることを確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_FileEntries_SortedByCategoryThenPath()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sorted");

            _resultLists.AddModifiedFileRelativePath("zzz.dll");
            _resultLists.RecordDiffDetail("zzz.dll", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "bbb.txt"));
            _resultLists.AddUnchangedFileRelativePath("aaa.config");
            _resultLists.RecordDiffDetail("aaa.config", FileDiffResultLists.DiffDetailResult.SHA256Match);

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<JsonElement>(json);
            var files = record.GetProperty("files");

            // Expected category order: Added < Modified < Unchanged (alphabetical)
            // 期待されるカテゴリ順: Added < Modified < Unchanged（アルファベット順）
            var categories = new List<string>();
            foreach (var file in files.EnumerateArray())
            {
                categories.Add(file.GetProperty("category").GetString()!);
            }

            Assert.Equal("Added", categories[0]);
            Assert.Equal("Modified", categories[1]);
            Assert.Equal("Unchanged", categories[2]);
        }

        // ── Helpers / ヘルパー ────────────────────────────────────────────────────

        private static ReportGenerationContext CreateReportContext(
            string oldDir, string newDir, string reportDir,
            bool shouldGenerateAuditLog = true)
            => new(oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host",
                new ConfigSettingsBuilder { ShouldGenerateAuditLog = shouldGenerateAuditLog }.Build(),
                ilCache: null);

        private (string oldDir, string newDir, string reportDir) MakeDirs(string label)
        {
            var oldDir = Path.Combine(_rootDir, $"old-{label}");
            var newDir = Path.Combine(_rootDir, $"new-{label}");
            var reportDir = Path.Combine(_rootDir, $"report-{label}");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);
            return (oldDir, newDir, reportDir);
        }
    }
}
