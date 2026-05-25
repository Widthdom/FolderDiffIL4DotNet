/// <summary>
/// Partial class containing report integrity hash, JSON structure, and mutation-testing tests for AuditLogGenerateService.
/// レポートインテグリティハッシュ、JSON 構造、ミューテーションテストを含む AuditLogGenerateService のパーシャルクラス。
/// </summary>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed partial class AuditLogGenerateServiceTests
    {
        // ── Report integrity hashes / レポートインテグリティハッシュ ───────────────

        [Fact]
        public void GenerateAuditLog_ComputesReportSha256_WhenReportExists()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("hash-md");
            File.WriteAllText(Path.Combine(reportDir, "diff_report.md"), "# Test Report\nContent here.");

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<JsonElement>(json);
            var reportHash = record.GetProperty("reportSha256").GetString();

            Assert.False(string.IsNullOrEmpty(reportHash), "reportSha256 should be non-empty when diff_report.md exists");
            Assert.Equal(64, reportHash!.Length);
            Assert.Matches("^[0-9a-f]{64}$", reportHash);
        }

        [Fact]
        public void GenerateAuditLog_ComputesHtmlReportSha256_WhenHtmlExists()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("hash-html");
            File.WriteAllText(Path.Combine(reportDir, "diff_report.html"), "<html><body>Test</body></html>");

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<JsonElement>(json);
            var htmlHash = record.GetProperty("htmlReportSha256").GetString();

            Assert.False(string.IsNullOrEmpty(htmlHash), "htmlReportSha256 should be non-empty when diff_report.html exists");
            Assert.Equal(64, htmlHash!.Length);
            Assert.Matches("^[0-9a-f]{64}$", htmlHash);
        }

        [Fact]
        public void GenerateAuditLog_ReportSha256IsEmpty_WhenReportMissing()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("hash-missing");

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<JsonElement>(json);
            Assert.Equal(string.Empty, record.GetProperty("reportSha256").GetString());
            Assert.Equal(string.Empty, record.GetProperty("htmlReportSha256").GetString());
        }

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

        [Fact]
        public void GenerateAuditLog_ProducesValidJson()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("valid-json");

            _resultLists.AddModifiedFileRelativePath("src/app.dll");
            _resultLists.RecordDiffDetail("src/app.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm");

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<AuditLogRecord>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(record);
            Assert.Equal("1.0.0", record!.AppVersion);
            Assert.Single(record.Files);
            Assert.Equal("Modified", record.Files[0].Category);
        }

        [Fact]
        public void GenerateAuditLog_WritesUtf8WithoutBom()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("utf8-no-bom");

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var bytes = File.ReadAllBytes(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            Assert.True(bytes.Length >= 3);
            Assert.False(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        }

        [Fact]
        public void ComputeFileHash_ReturnsEmpty_WhenFileDoesNotExist()
        {
            var hash = AuditLogGenerateService.ComputeFileHash(
                Path.Combine(_rootDir, "nonexistent.txt"));
            Assert.Equal(string.Empty, hash);
        }

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

        [Fact]
        public void GenerateAuditLog_EmptyResults_ProducesValidLog()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("empty");

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

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

        [Fact]
        public void Constructor_ThrowsOnNullResultLists()
        {
            Assert.Throws<ArgumentNullException>(() => new AuditLogGenerateService(null!, _logger));
        }

        [Fact]
        public void Constructor_ThrowsOnNullLogger()
        {
            Assert.Throws<ArgumentNullException>(() => new AuditLogGenerateService(_resultLists, null!));
        }

        [Fact]
        public void GenerateAuditLog_IncludesDisassemblerAvailability_WhenProbed()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("disasm-avail");

            _resultLists.DisassemblerAvailability = new List<DisassemblerProbeResult>
            {
                new("dotnet-ildasm", true, "0.12.2", "/usr/local/bin/dotnet-ildasm"),
                new("ilspycmd", false, null, null),
            };

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("disassemblerAvailability", out var availArr));
            Assert.Equal(JsonValueKind.Array, availArr.ValueKind);
            Assert.Equal(2, availArr.GetArrayLength());

            var first = availArr[0];
            Assert.Equal("dotnet-ildasm", first.GetProperty("toolName").GetString());
            Assert.True(first.GetProperty("available").GetBoolean());
            Assert.Equal("0.12.2", first.GetProperty("version").GetString());

            var second = availArr[1];
            Assert.Equal("ilspycmd", second.GetProperty("toolName").GetString());
            Assert.False(second.GetProperty("available").GetBoolean());
            Assert.Equal("", second.GetProperty("version").GetString());
        }

        [Fact]
        public void GenerateAuditLog_DisassemblerAvailabilityIsNull_WhenNotProbed()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("no-probe");

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("disassemblerAvailability", out var availProp));
            Assert.Equal(JsonValueKind.Null, availProp.ValueKind);
        }

        [Fact]
        public void BuildFileEntries_ModifiedFile_NoDiffDetail_ProducesEmptyDiffDetail()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("no-diff-detail");

            _resultLists.AddModifiedFileRelativePath("nodiff.dll");

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<JsonElement>(json);
            var files = record.GetProperty("files");

            foreach (var file in files.EnumerateArray())
            {
                if (file.GetProperty("category").GetString() == "Modified")
                {
                    Assert.Equal(string.Empty, file.GetProperty("diffDetail").GetString());
                    Assert.Equal(string.Empty, file.GetProperty("disassembler").GetString());
                }
            }
        }

        [Fact]
        public void BuildFileEntries_ModifiedFile_NoDisassemblerLabel_ProducesEmptyDisassembler()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("no-disasm-label");

            _resultLists.AddModifiedFileRelativePath("no-disasm.dll");
            _resultLists.RecordDiffDetail("no-disasm.dll", FileDiffResultLists.DiffDetailResult.TextMismatch);

            _service.GenerateAuditLog(CreateReportContext(oldDir, newDir, reportDir));

            var json = File.ReadAllText(Path.Combine(reportDir, AuditLogGenerateService.AUDIT_LOG_FILE_NAME));
            var record = JsonSerializer.Deserialize<JsonElement>(json);
            var files = record.GetProperty("files");

            foreach (var file in files.EnumerateArray())
            {
                if (file.GetProperty("category").GetString() == "Modified")
                {
                    Assert.Equal("TextMismatch", file.GetProperty("diffDetail").GetString());
                    Assert.Equal(string.Empty, file.GetProperty("disassembler").GetString());
                }
            }
        }
    }
}
