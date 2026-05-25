using System.Collections.Generic;
using System.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for dependency change and vulnerability rendering in HtmlReportGenerateService.
    /// HtmlReportGenerateService の依存関係変更・脆弱性レンダリングテスト。
    /// </summary>
    public sealed partial class HtmlReportGenerateServiceTests
    {
        // ── Vulnerability cell rendering via HTML output ──

        [Fact]
        [Trait("Category", "Unit")]
        public void DependencyChanges_WithNewVersionVulnerability_RendersVulnBadge()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("vuln-new");
            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.TextMismatch, null);
            _resultLists.FileRelativePathToDependencyChanges["lib.dll"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Updated", "Pkg.A", "1.0.0", "2.0.0", ChangeImportance.Low,
                        Vulnerabilities: new VulnerabilityCheckResult
                        {
                            OldVersionVulnerabilities = new List<PackageVulnerability>(),
                            NewVersionVulnerabilities = new List<PackageVulnerability>
                            {
                                new("https://github.com/advisories/GHSA-abcd-1234-efgh", 3, "[1.0.0, 3.0.0)")
                            }
                        }),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: false);
            builder.ShouldIncludeDependencyChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Vulnerability badge with advisory link should be rendered
            // 脆弱性バッジとアドバイザリリンクがレンダリングされるべき
            Assert.Contains("vuln-badge", html);
            Assert.Contains("GHSA-abcd-1234-efgh", html);
            Assert.Contains("vuln-new", html);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void DependencyChanges_WithResolvedVulnerability_RendersResolvedBadge()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("vuln-resolved");
            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.TextMismatch, null);
            _resultLists.FileRelativePathToDependencyChanges["lib.dll"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Updated", "Pkg.B", "1.0.0", "3.0.0", ChangeImportance.Low,
                        Vulnerabilities: new VulnerabilityCheckResult
                        {
                            OldVersionVulnerabilities = new List<PackageVulnerability>
                            {
                                new("https://github.com/advisories/GHSA-old-vuln-0001", 2, "[1.0.0, 2.0.0)")
                            },
                            NewVersionVulnerabilities = new List<PackageVulnerability>()
                        }),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: false);
            builder.ShouldIncludeDependencyChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            Assert.Contains("vuln-resolved", html);
            Assert.Contains("GHSA-old-vuln-0001", html);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void DependencyChanges_WithNoVulnerabilities_RendersEmDash()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("vuln-none");
            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.TextMismatch, null);
            _resultLists.FileRelativePathToDependencyChanges["lib.dll"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Updated", "Pkg.C", "1.0.0", "2.0.0", ChangeImportance.Low,
                        Vulnerabilities: new VulnerabilityCheckResult
                        {
                            OldVersionVulnerabilities = new List<PackageVulnerability>(),
                            NewVersionVulnerabilities = new List<PackageVulnerability>()
                        }),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: false);
            builder.ShouldIncludeDependencyChangesInReport = true;
            builder.EnableNuGetVulnerabilityCheck = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Em-dash (&#x2014;) rendered for no-vulnerability entries / 脆弱性なしエントリには em-dash が表示
            Assert.Contains("&#x2014;", html);
            // The vuln count spans should not appear in the HTML body (only CSS vars reference the name)
            // 脆弱性カウント span は HTML body に出現しないべき（CSS 変数のみが名前を参照）
            Assert.DoesNotContain("<span class=\"vuln-new-count\">", html);
            Assert.DoesNotContain("<span class=\"vuln-resolved-count\">", html);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void DependencyChanges_WithUnsafeSchemeAdvisoryUrl_DoesNotRenderLink()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("vuln-unsafe-scheme");
            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.TextMismatch, null);
            _resultLists.FileRelativePathToDependencyChanges["lib.dll"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Updated", "Pkg.Evil", "1.0.0", "2.0.0", ChangeImportance.Low,
                        Vulnerabilities: new VulnerabilityCheckResult
                        {
                            OldVersionVulnerabilities = new List<PackageVulnerability>(),
                            NewVersionVulnerabilities = new List<PackageVulnerability>
                            {
                                new("javascript:alert(1)", 3, "[1.0.0, 3.0.0)")
                            }
                        }),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: false);
            builder.ShouldIncludeDependencyChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // javascript: scheme should NOT be rendered as <a href> / javascript: スキームは <a href> としてレンダリングしない
            Assert.DoesNotContain("href=\"javascript:", html);
            Assert.Contains("vuln-badge", html);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void DependencyChanges_WithEmptyAdvisoryUrl_ShowsSeverityLabel()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("vuln-empty-url");
            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.TextMismatch, null);
            _resultLists.FileRelativePathToDependencyChanges["lib.dll"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Updated", "Pkg.NoUrl", "1.0.0", "2.0.0", ChangeImportance.Low,
                        Vulnerabilities: new VulnerabilityCheckResult
                        {
                            OldVersionVulnerabilities = new List<PackageVulnerability>(),
                            NewVersionVulnerabilities = new List<PackageVulnerability>
                            {
                                new("", 2, "[1.0.0, 3.0.0)")
                            }
                        }),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: false);
            builder.ShouldIncludeDependencyChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // With empty URL, severity label should be shown instead of advisory ID link
            // 空 URL の場合、アドバイザリ ID リンクの代わりに重要度ラベルが表示される
            Assert.Contains("vuln-badge", html);
            // No advisory hyperlink should appear for the empty-URL vulnerability
            // 空 URL の脆弱性にはアドバイザリハイパーリンクが出現しないべき
            Assert.DoesNotContain("target=\"_blank\"", html.Substring(html.IndexOf("vuln-new")));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void DependencyChanges_WithReferencingAssemblies_RendersAssemblyNames()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("dep-refs");
            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.TextMismatch, null);
            _resultLists.FileRelativePathToDependencyChanges["lib.dll"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Updated", "Pkg.Shared", "1.0.0", "2.0.0", ChangeImportance.Low,
                        ReferencingAssemblies: new List<string> { "App.Web.dll", "App.Api.dll" }),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: false);
            builder.ShouldIncludeDependencyChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            Assert.Contains("App.Web.dll", html);
            Assert.Contains("App.Api.dll", html);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void DependencyChanges_VulnerabilitySummarySuffix_ShowsCounts()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("vuln-summary");
            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.TextMismatch, null);
            _resultLists.FileRelativePathToDependencyChanges["lib.dll"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Updated", "Pkg.Mixed", "1.0.0", "2.0.0", ChangeImportance.Low,
                        Vulnerabilities: new VulnerabilityCheckResult
                        {
                            OldVersionVulnerabilities = new List<PackageVulnerability>
                            {
                                new("https://example.com/old-1", 2, "[0.5.0, 1.5.0)")
                            },
                            NewVersionVulnerabilities = new List<PackageVulnerability>
                            {
                                new("https://example.com/new-1", 3, "[1.0.0, 3.0.0)")
                            }
                        }),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: false);
            builder.ShouldIncludeDependencyChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));
            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Summary should show both vuln and resolved counts / サマリーに脆弱性件数と解消件数の両方を表示
            Assert.Contains("vuln-new-count", html);
            Assert.Contains("vuln-resolved-count", html);
        }

        // ── IsAllowedUriScheme edge cases ──

        [Theory]
        [Trait("Category", "Unit")]
        [InlineData("HTTP://EXAMPLE.COM/advisory", true)]
        [InlineData("HTTPS://EXAMPLE.COM/advisory", true)]
        [InlineData("HtTpS://example.com/advisory", true)]
        [InlineData("data:text/html,<script>alert(1)</script>", false)]
        [InlineData("vbscript:MsgBox(1)", false)]
        [InlineData("ftp://files.example.com/advisory", false)]
        [InlineData("file:///etc/passwd", false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("not-a-url", false)]
        public void IsAllowedUriScheme_EdgeCases(string url, bool expected)
        {
            Assert.Equal(expected, HtmlReportGenerateService.IsAllowedUriScheme(url));
        }
    }
}
