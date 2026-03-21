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
    public sealed class AuditLogGenerateServiceTests : IDisposable
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

            _service.GenerateAuditLog(oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", shouldGenerate: true);

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

            _service.GenerateAuditLog(oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", shouldGenerate: false);

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

            _service.GenerateAuditLog(oldDir, newDir, reportDir,
                appVersion: "2.1.0", elapsedTimeString: "0h 5m 30.1s",
                computerName: "build-server-01", shouldGenerate: true);

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

            _service.GenerateAuditLog(oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", shouldGenerate: true);

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

            _service.GenerateAuditLog(oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", shouldGenerate: true);

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

            _service.GenerateAuditLog(oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", shouldGenerate: true);

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

            _service.GenerateAuditLog(oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", shouldGenerate: true);

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

        // ── Report integrity hashes / レポートインテグリティハッシュ ───────────────

        /// <summary>
        /// Verifies that reportSha256 is computed when diff_report.md exists.
        /// diff_report.md が存在する場合に reportSha256 が計算されることを確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_ComputesReportSha256_WhenReportExists()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("hash-md");

            // Create a mock diff_report.md / ダミーの diff_report.md を作成
            File.WriteAllText(Path.Combine(reportDir, "diff_report.md"), "# Test Report\nContent here.");

            _service.GenerateAuditLog(oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", shouldGenerate: true);

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<JsonElement>(json);
            var reportHash = record.GetProperty("reportSha256").GetString();

            Assert.False(string.IsNullOrEmpty(reportHash), "reportSha256 should be non-empty when diff_report.md exists");
            Assert.Equal(64, reportHash!.Length); // SHA256 produces 64 hex chars / SHA256 は 64 桁の16進文字
            Assert.Matches("^[0-9a-f]{64}$", reportHash);
        }

        /// <summary>
        /// Verifies that htmlReportSha256 is computed when diff_report.html exists.
        /// diff_report.html が存在する場合に htmlReportSha256 が計算されることを確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_ComputesHtmlReportSha256_WhenHtmlExists()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("hash-html");

            File.WriteAllText(Path.Combine(reportDir, "diff_report.html"), "<html><body>Test</body></html>");

            _service.GenerateAuditLog(oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", shouldGenerate: true);

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<JsonElement>(json);
            var htmlHash = record.GetProperty("htmlReportSha256").GetString();

            Assert.False(string.IsNullOrEmpty(htmlHash), "htmlReportSha256 should be non-empty when diff_report.html exists");
            Assert.Equal(64, htmlHash!.Length);
            Assert.Matches("^[0-9a-f]{64}$", htmlHash);
        }

        /// <summary>
        /// Verifies that reportSha256 is empty when diff_report.md does not exist.
        /// diff_report.md が存在しない場合に reportSha256 が空であることを確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_ReportSha256IsEmpty_WhenReportMissing()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("hash-missing");

            _service.GenerateAuditLog(oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", shouldGenerate: true);

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<JsonElement>(json);
            Assert.Equal(string.Empty, record.GetProperty("reportSha256").GetString());
            Assert.Equal(string.Empty, record.GetProperty("htmlReportSha256").GetString());
        }

        /// <summary>
        /// Verifies that modifying diff_report.md changes the reportSha256 value (tamper detection).
        /// diff_report.md を変更すると reportSha256 の値が変わること（改竄検知）を確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_DifferentReportContent_ProducesDifferentHash()
        {
            var reportDir1 = Path.Combine(_rootDir, "report-hash-diff1");
            var reportDir2 = Path.Combine(_rootDir, "report-hash-diff2");
            Directory.CreateDirectory(reportDir1);
            Directory.CreateDirectory(reportDir2);

            File.WriteAllText(Path.Combine(reportDir1, "diff_report.md"), "Content version 1");
            File.WriteAllText(Path.Combine(reportDir2, "diff_report.md"), "Content version 2");

            var hash1 = AuditLogGenerateService.ComputeFileHash(Path.Combine(reportDir1, "diff_report.md"));
            var hash2 = AuditLogGenerateService.ComputeFileHash(Path.Combine(reportDir2, "diff_report.md"));

            Assert.NotEqual(hash1, hash2);
        }

        // ── JSON structure / JSON 構造 ────────────────────────────────────────────

        /// <summary>
        /// Verifies that the generated JSON is valid and can be deserialized.
        /// 生成された JSON が有効でデシリアライズ可能であることを確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_ProducesValidJson()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("valid-json");

            _resultLists.AddModifiedFileRelativePath("src/app.dll");
            _resultLists.RecordDiffDetail("src/app.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm");

            _service.GenerateAuditLog(oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host", shouldGenerate: true);

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));

            // Should not throw / 例外が発生しないこと
            var record = JsonSerializer.Deserialize<AuditLogRecord>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(record);
            Assert.Equal("1.0.0", record!.AppVersion);
            Assert.Single(record.Files);
            Assert.Equal("Modified", record.Files[0].Category);
        }

        // ── ComputeFileHash static method / ComputeFileHash 静的メソッド ──────────

        /// <summary>
        /// Verifies that ComputeFileHash returns empty string for non-existent file.
        /// 存在しないファイルに対して ComputeFileHash が空文字を返すことを確認する。
        /// </summary>
        [Fact]
        public void ComputeFileHash_ReturnsEmpty_WhenFileDoesNotExist()
        {
            var hash = AuditLogGenerateService.ComputeFileHash(
                Path.Combine(_rootDir, "nonexistent.txt"));
            Assert.Equal(string.Empty, hash);
        }

        /// <summary>
        /// Verifies that ComputeFileHash returns consistent SHA256 for the same content.
        /// 同一内容に対して ComputeFileHash が一貫した SHA256 を返すことを確認する。
        /// </summary>
        [Fact]
        public void ComputeFileHash_ReturnsSameHash_ForSameContent()
        {
            var file1 = Path.Combine(_rootDir, "hash-test1.txt");
            var file2 = Path.Combine(_rootDir, "hash-test2.txt");
            File.WriteAllText(file1, "identical content");
            File.WriteAllText(file2, "identical content");

            var hash1 = AuditLogGenerateService.ComputeFileHash(file1);
            var hash2 = AuditLogGenerateService.ComputeFileHash(file2);

            Assert.Equal(hash1, hash2);
        }

        // ── Empty results / 空の結果 ──────────────────────────────────────────────

        /// <summary>
        /// Verifies that audit log is generated correctly when there are no files to compare.
        /// 比較するファイルがない場合でも監査ログが正しく生成されることを確認する。
        /// </summary>
        [Fact]
        public void GenerateAuditLog_EmptyResults_ProducesValidLog()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("empty");

            _service.GenerateAuditLog(oldDir, newDir, reportDir,
                appVersion: "1.0.0", elapsedTimeString: "0h 0m 0.0s",
                computerName: "test-host", shouldGenerate: true);

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<JsonElement>(json);
            var files = record.GetProperty("files");
            Assert.Equal(0, files.GetArrayLength());

            var summary = record.GetProperty("summary");
            Assert.Equal(0, summary.GetProperty("added").GetInt32());
            Assert.Equal(0, summary.GetProperty("removed").GetInt32());
            Assert.Equal(0, summary.GetProperty("modified").GetInt32());
            Assert.Equal(0, summary.GetProperty("unchanged").GetInt32());
            Assert.Equal(0, summary.GetProperty("ignored").GetInt32());
        }

        // ── Constructor null checks / コンストラクタの null チェック ─────────────────

        /// <summary>
        /// Verifies that constructor throws on null fileDiffResultLists.
        /// fileDiffResultLists が null の場合にコンストラクタが例外をスローすることを確認する。
        /// </summary>
        [Fact]
        public void Constructor_ThrowsOnNullResultLists()
        {
            Assert.Throws<ArgumentNullException>(() => new AuditLogGenerateService(null!, _logger));
        }

        /// <summary>
        /// Verifies that constructor throws on null logger.
        /// logger が null の場合にコンストラクタが例外をスローすることを確認する。
        /// </summary>
        [Fact]
        public void Constructor_ThrowsOnNullLogger()
        {
            Assert.Throws<ArgumentNullException>(() => new AuditLogGenerateService(_resultLists, null!));
        }

        // ── Helpers / ヘルパー ────────────────────────────────────────────────────

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
