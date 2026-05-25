using System.Collections.Generic;
using System.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for Markdown report dependency vulnerability/refs columns,
    /// SDK version arrow display, and change tag display helpers.
    /// Markdown レポートの依存関係脆弱性/参照カラム、
    /// SDK バージョンアロー表示、変更タグ表示ヘルパーのテスト。
    /// </summary>
    public sealed partial class ReportGenerateServiceTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_DependencyChanges_WithVulnerabilities_ShowsSeverityMarkers()
        {
            var oldDir = Path.Combine(_rootDir, "old-vuln-md");
            var newDir = Path.Combine(_rootDir, "new-vuln-md");
            var reportDir = Path.Combine(_rootDir, "report-vuln-md");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("app.deps.json");
            _resultLists.RecordDiffDetail("app.deps.json", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.FileRelativePathToDependencyChanges["app.deps.json"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Updated", "Pkg.A", "1.0.0", "2.0.0", ChangeImportance.Low,
                        Vulnerabilities: new VulnerabilityCheckResult
                        {
                            OldVersionVulnerabilities = new List<PackageVulnerability>(),
                            NewVersionVulnerabilities = new List<PackageVulnerability>
                            {
                                new("https://github.com/advisories/GHSA-xxxx", 3, "[1.0.0, 3.0.0)")
                            }
                        }),
                }
            };

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeDependencyChangesInReport = true;
            builder.EnableNuGetVulnerabilityCheck = true;
            var config = builder.Build();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Markdown vulnerability column should show severity warning symbol
            // Markdown 脆弱性カラムに重要度警告シンボルが表示されるべき
            Assert.Contains("⚠", reportText);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_DependencyChanges_WithResolvedVulnerabilities_ShowsStrikethrough()
        {
            var oldDir = Path.Combine(_rootDir, "old-vuln-resolved-md");
            var newDir = Path.Combine(_rootDir, "new-vuln-resolved-md");
            var reportDir = Path.Combine(_rootDir, "report-vuln-resolved-md");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("app.deps.json");
            _resultLists.RecordDiffDetail("app.deps.json", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.FileRelativePathToDependencyChanges["app.deps.json"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Updated", "Pkg.B", "1.0.0", "3.0.0", ChangeImportance.Low,
                        Vulnerabilities: new VulnerabilityCheckResult
                        {
                            OldVersionVulnerabilities = new List<PackageVulnerability>
                            {
                                new("https://example.com/old", 2, "[0.5.0, 1.5.0)")
                            },
                            NewVersionVulnerabilities = new List<PackageVulnerability>()
                        }),
                }
            };

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeDependencyChangesInReport = true;
            builder.EnableNuGetVulnerabilityCheck = true;
            var config = builder.Build();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Resolved vulnerabilities shown with strikethrough / 解消済み脆弱性は取り消し線で表示
            Assert.Contains("~~", reportText);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_DependencyChanges_WithReferencingAssemblies_ShowsCommaList()
        {
            var oldDir = Path.Combine(_rootDir, "old-refs-md");
            var newDir = Path.Combine(_rootDir, "new-refs-md");
            var reportDir = Path.Combine(_rootDir, "report-refs-md");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("app.deps.json");
            _resultLists.RecordDiffDetail("app.deps.json", FileDiffResultLists.DiffDetailResult.TextMismatch);
            _resultLists.FileRelativePathToDependencyChanges["app.deps.json"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Updated", "Shared.Lib", "1.0.0", "2.0.0", ChangeImportance.Low,
                        ReferencingAssemblies: new List<string> { "App.Web.dll", "App.Api.dll" }),
                }
            };

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeDependencyChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Referencing assemblies shown as comma-separated list / 参照アセンブリがカンマ区切りリストで表示
            Assert.Contains("App.Web.dll", reportText);
            Assert.Contains("App.Api.dll", reportText);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_ModifiedFiles_SdkVersionArrow_SplitIntoBactickParts()
        {
            var oldDir = Path.Combine(_rootDir, "old-sdk-arrow");
            var newDir = Path.Combine(_rootDir, "new-sdk-arrow");
            var reportDir = Path.Combine(_rootDir, "report-sdk-arrow");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.FileRelativePathToSdkVersionDictionary["lib.dll"] = ".NET 8.0 → .NET 9.0";

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // SDK version with arrow should be split into backtick-wrapped parts
            // アロー付き SDK バージョンはバッククォートで囲まれた部分に分割されるべき
            Assert.Contains("`.NET 8.0` → `.NET 9.0`", reportText);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReport_ModifiedFiles_ChangeTagDisplay_ShowsLabels()
        {
            var oldDir = Path.Combine(_rootDir, "old-changetag");
            var newDir = Path.Combine(_rootDir, "new-changetag");
            var reportDir = Path.Combine(_rootDir, "report-changetag");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.FileRelativePathToChangeTags["lib.dll"] = new List<ChangeTag>
            {
                ChangeTag.MethodAdd,
                ChangeTag.Signature,
            };

            var config = CreateConfig();
            _service.GenerateDiffReport(CreateReportContext(oldDir, newDir, reportDir, config));

            var reportText = File.ReadAllText(Path.Combine(reportDir, "diff_report.md"));

            // Change tags should be displayed with their labels / 変更タグはラベルで表示されるべき
            Assert.Contains("`+Method`", reportText);
            Assert.Contains("`Signature`", reportText);
        }
    }
}
