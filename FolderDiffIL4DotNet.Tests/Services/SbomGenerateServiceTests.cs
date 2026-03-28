using System;
using System.IO;
using System.Text.Json;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="SbomGenerateService"/>.
    /// <see cref="SbomGenerateService"/> のテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed partial class SbomGenerateServiceTests : IDisposable
    {
        private readonly string _rootDir;
        private readonly FileDiffResultLists _resultLists = new();
        private readonly ILoggerService _logger = new LoggerService();
        private readonly SbomGenerateService _service;

        public SbomGenerateServiceTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-sbom-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
            _service = new SbomGenerateService(_resultLists, _logger);
        }

        public void Dispose()
        {
            _resultLists.ResetAll();
            try
            {
                if (Directory.Exists(_rootDir))
                    Directory.Delete(_rootDir, recursive: true);
            }
            catch
            {
                // ignore cleanup errors / クリーンアップエラーを無視
            }
        }

        // ── Basic generation / 基本生成 ──────────────────────────────────────────

        [Fact]
        public void GenerateSbom_CreatesFile_WhenEnabled_CycloneDX()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("basic-cdx");

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true, sbomFormat: "CycloneDX"));

            var sbomPath = Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME);
            Assert.True(File.Exists(sbomPath), "sbom.cdx.json should be created");
        }

        [Fact]
        public void GenerateSbom_CreatesFile_WhenEnabled_SPDX()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("basic-spdx");

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true, sbomFormat: "SPDX"));

            var sbomPath = Path.Combine(reportDir, SbomGenerateService.SPDX_FILE_NAME);
            Assert.True(File.Exists(sbomPath), "sbom.spdx.json should be created");
        }

        [Fact]
        public void GenerateSbom_DoesNotCreateFile_WhenDisabled()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("disabled");

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: false));

            Assert.False(File.Exists(Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME)));
            Assert.False(File.Exists(Path.Combine(reportDir, SbomGenerateService.SPDX_FILE_NAME)));
        }

        // ── CycloneDX format / CycloneDX 形式 ────────────────────────────────────

        [Fact]
        public void GenerateSbom_CycloneDX_ContainsRequiredFields()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("cdx-fields");

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true, sbomFormat: "CycloneDX"));

            var json = File.ReadAllText(Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME));
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("CycloneDX", root.GetProperty("bomFormat").GetString());
            Assert.Equal("1.5", root.GetProperty("specVersion").GetString());
            Assert.True(root.GetProperty("serialNumber").GetString()!.StartsWith("urn:uuid:"));
            Assert.Equal(1, root.GetProperty("version").GetInt32());
            Assert.True(root.TryGetProperty("metadata", out _));
            Assert.True(root.TryGetProperty("components", out _));
        }

        [Fact]
        public void GenerateSbom_CycloneDX_IncludesToolMetadata()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("cdx-tool");

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true, appVersion: "2.0.0"));

            var json = File.ReadAllText(Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME));
            var doc = JsonDocument.Parse(json);
            var tools = doc.RootElement.GetProperty("metadata").GetProperty("tools");
            Assert.Equal(1, tools.GetArrayLength());
            Assert.Equal("FolderDiffIL4DotNet", tools[0].GetProperty("name").GetString());
            Assert.Equal("2.0.0", tools[0].GetProperty("version").GetString());
        }

        [Fact]
        public void GenerateSbom_CycloneDX_ListsComponents()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("cdx-components");

            // Create actual files for hash computation / ハッシュ計算のため実ファイルを作成
            File.WriteAllText(Path.Combine(newDir, "added.dll"), "new-dll-content");
            File.WriteAllText(Path.Combine(oldDir, "removed.txt"), "old-content");

            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "added.dll"));
            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "removed.txt"));
            _resultLists.AddModifiedFileRelativePath("changed.config");
            _resultLists.RecordDiffDetail("changed.config", FileDiffResultLists.DiffDetailResult.TextMismatch);

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true));

            var json = File.ReadAllText(Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME));
            var doc = JsonDocument.Parse(json);
            var components = doc.RootElement.GetProperty("components");

            Assert.Equal(3, components.GetArrayLength());
        }

        [Fact]
        public void GenerateSbom_CycloneDX_AssemblyFiles_HaveLibraryType()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("cdx-type");

            File.WriteAllText(Path.Combine(newDir, "app.dll"), "dll-content");
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "app.dll"));

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true));

            var json = File.ReadAllText(Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME));
            var doc = JsonDocument.Parse(json);
            var component = doc.RootElement.GetProperty("components")[0];
            Assert.Equal("library", component.GetProperty("type").GetString());
        }

        [Fact]
        public void GenerateSbom_CycloneDX_NonAssemblyFiles_HaveFileType()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("cdx-filetype");

            File.WriteAllText(Path.Combine(newDir, "readme.md"), "# readme");
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "readme.md"));

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true));

            var json = File.ReadAllText(Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME));
            var doc = JsonDocument.Parse(json);
            var component = doc.RootElement.GetProperty("components")[0];
            Assert.Equal("file", component.GetProperty("type").GetString());
        }

        [Fact]
        public void GenerateSbom_CycloneDX_Components_IncludeStatusProperty()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("cdx-status");

            File.WriteAllText(Path.Combine(newDir, "new.txt"), "content");
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "new.txt"));

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true));

            var json = File.ReadAllText(Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME));
            var doc = JsonDocument.Parse(json);
            var props = doc.RootElement.GetProperty("components")[0].GetProperty("properties");

            bool foundStatus = false;
            foreach (var prop in props.EnumerateArray())
            {
                if (prop.GetProperty("name").GetString() == "folderdiff:status")
                {
                    Assert.Equal("Added", prop.GetProperty("value").GetString());
                    foundStatus = true;
                }
            }
            Assert.True(foundStatus, "Should have folderdiff:status property");
        }

        // ── SPDX format / SPDX 形式 ──────────────────────────────────────────────

        [Fact]
        public void GenerateSbom_SPDX_ContainsRequiredFields()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("spdx-fields");

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true, sbomFormat: "SPDX"));

            var json = File.ReadAllText(Path.Combine(reportDir, SbomGenerateService.SPDX_FILE_NAME));
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("SPDX-2.3", root.GetProperty("spdxVersion").GetString());
            Assert.Equal("CC0-1.0", root.GetProperty("dataLicense").GetString());
            Assert.Equal("SPDXRef-DOCUMENT", root.GetProperty("spdxid").GetString());
            Assert.Equal("FolderDiffIL4DotNet-SBOM", root.GetProperty("name").GetString());
            Assert.True(root.TryGetProperty("creationInfo", out _));
            Assert.True(root.TryGetProperty("packages", out _));
        }

        [Fact]
        public void GenerateSbom_SPDX_ListsPackages()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("spdx-packages");

            File.WriteAllText(Path.Combine(newDir, "app.dll"), "dll");
            File.WriteAllText(Path.Combine(oldDir, "old.dll"), "old");

            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "app.dll"));
            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "old.dll"));

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true, sbomFormat: "SPDX"));

            var json = File.ReadAllText(Path.Combine(reportDir, SbomGenerateService.SPDX_FILE_NAME));
            var doc = JsonDocument.Parse(json);
            var packages = doc.RootElement.GetProperty("packages");

            Assert.Equal(2, packages.GetArrayLength());
        }

        [Fact]
        public void GenerateSbom_SPDX_Packages_HaveSpdxIds()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("spdx-ids");

            File.WriteAllText(Path.Combine(newDir, "a.dll"), "a");
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "a.dll"));

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true, sbomFormat: "SPDX"));

            var json = File.ReadAllText(Path.Combine(reportDir, SbomGenerateService.SPDX_FILE_NAME));
            var doc = JsonDocument.Parse(json);
            var pkg = doc.RootElement.GetProperty("packages")[0];

            Assert.StartsWith("SPDXRef-Package-", pkg.GetProperty("spdxid").GetString());
        }

        [Fact]
        public void GenerateSbom_SPDX_Packages_IncludeStatusInComment()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("spdx-comment");

            File.WriteAllText(Path.Combine(newDir, "lib.dll"), "lib");
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "lib.dll"));

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true, sbomFormat: "SPDX"));

            var json = File.ReadAllText(Path.Combine(reportDir, SbomGenerateService.SPDX_FILE_NAME));
            var doc = JsonDocument.Parse(json);
            var comment = doc.RootElement.GetProperty("packages")[0].GetProperty("comment").GetString();

            Assert.Contains("Status: Added", comment);
        }

        // ── Component extraction / コンポーネント抽出 ─────────────────────────────

        [Fact]
        public void BuildComponentList_IncludesAllCategories()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("all-categories");

            File.WriteAllText(Path.Combine(newDir, "added.dll"), "a");
            File.WriteAllText(Path.Combine(oldDir, "removed.dll"), "r");
            File.WriteAllText(Path.Combine(newDir, "modified.dll"), "m");
            File.WriteAllText(Path.Combine(newDir, "unchanged.dll"), "u");

            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "added.dll"));
            _resultLists.AddRemovedFileAbsolutePath(Path.Combine(oldDir, "removed.dll"));
            _resultLists.AddModifiedFileRelativePath("modified.dll");
            _resultLists.AddUnchangedFileRelativePath("unchanged.dll");

            var context = CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true);
            var components = _service.BuildComponentList(context);

            Assert.Equal(4, components.Count);
            Assert.Contains(components, c => c.Status == "Added");
            Assert.Contains(components, c => c.Status == "Removed");
            Assert.Contains(components, c => c.Status == "Modified");
            Assert.Contains(components, c => c.Status == "Unchanged");
        }

        [Fact]
        public void BuildComponentList_SortedByStatusThenPath()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sorted");

            _resultLists.AddModifiedFileRelativePath("zzz.dll");
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "bbb.txt"));
            _resultLists.AddUnchangedFileRelativePath("aaa.config");

            var context = CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true);
            var components = _service.BuildComponentList(context);

            Assert.Equal(3, components.Count);
            Assert.Equal("Added", components[0].Status);
            Assert.Equal("Modified", components[1].Status);
            Assert.Equal("Unchanged", components[2].Status);
        }

        [Fact]
        public void BuildComponentList_EmptyResults_ReturnsEmptyList()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("empty");
            var context = CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true);
            var components = _service.BuildComponentList(context);
            Assert.Empty(components);
        }

        [Fact]
        public void BuildComponentList_ModifiedFile_IncludesDiffDetail()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("diff-detail");

            _resultLists.AddModifiedFileRelativePath("core.dll");
            _resultLists.RecordDiffDetail("core.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm");

            var context = CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true);
            var components = _service.BuildComponentList(context);

            Assert.Single(components);
            Assert.Equal("ILMismatch", components[0].DiffDetail);
        }

        // ── Null/error handling / Null/エラー処理 ─────────────────────────────────

        [Fact]
        public void Constructor_ThrowsOnNullResultLists()
        {
            Assert.Throws<ArgumentNullException>(() => new SbomGenerateService(null!, _logger));
        }

        [Fact]
        public void Constructor_ThrowsOnNullLogger()
        {
            Assert.Throws<ArgumentNullException>(() => new SbomGenerateService(_resultLists, null!));
        }

        [Fact]
        public void GenerateSbom_ThrowsOnNullContext()
        {
            Assert.Throws<ArgumentNullException>(() => _service.GenerateSbom(null!));
        }

        // ── Format parsing / 形式解析 ────────────────────────────────────────────

        [Theory]
        [InlineData("CycloneDX", SbomFormat.CycloneDX)]
        [InlineData("cyclonedx", SbomFormat.CycloneDX)]
        [InlineData("SPDX", SbomFormat.SPDX)]
        [InlineData("spdx", SbomFormat.SPDX)]
        [InlineData("unknown", SbomFormat.CycloneDX)]
        [InlineData("", SbomFormat.CycloneDX)]
        public void ParseSbomFormat_ParsesCorrectly(string input, SbomFormat expected)
        {
            Assert.Equal(expected, SbomGenerateService.ParseSbomFormat(input));
        }

        // ── SHA256 hash / SHA256 ハッシュ ────────────────────────────────────────

        [Fact]
        public void GenerateSbom_CycloneDX_Components_IncludeHashesForExistingFiles()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("cdx-hashes");

            File.WriteAllText(Path.Combine(newDir, "hashed.dll"), "content-for-hash");
            _resultLists.AddAddedFileAbsolutePath(Path.Combine(newDir, "hashed.dll"));

            _service.GenerateSbom(CreateReportContext(oldDir, newDir, reportDir, shouldGenerateSbom: true));

            var json = File.ReadAllText(Path.Combine(reportDir, SbomGenerateService.CYCLONEDX_FILE_NAME));
            var doc = JsonDocument.Parse(json);
            var hashes = doc.RootElement.GetProperty("components")[0].GetProperty("hashes");

            Assert.Equal(1, hashes.GetArrayLength());
            Assert.Equal("SHA-256", hashes[0].GetProperty("alg").GetString());
            var hashValue = hashes[0].GetProperty("content").GetString();
            Assert.Equal(64, hashValue!.Length);
            Assert.Matches("^[0-9a-f]{64}$", hashValue);
        }

        // ── Helpers / ヘルパー ────────────────────────────────────────────────────

        private static ReportGenerationContext CreateReportContext(
            string oldDir, string newDir, string reportDir,
            bool shouldGenerateSbom = false,
            string sbomFormat = "CycloneDX",
            string appVersion = "1.0.0")
            => new(oldDir, newDir, reportDir,
                appVersion: appVersion, elapsedTimeString: "0h 0m 1.0s",
                computerName: "test-host",
                new ConfigSettingsBuilder
                {
                    ShouldGenerateSbom = shouldGenerateSbom,
                    SbomFormat = sbomFormat
                }.Build(),
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
