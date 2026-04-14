using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Architecture
{
    /// <summary>
    /// Verifies that repository automation for CI, releases, and security scanning remains configured.
    /// リポジトリの CI・リリース・セキュリティスキャン自動化設定が維持されていることを検証します。
    /// </summary>
    public sealed class CiAutomationConfigurationTests
    {
        /// <summary>
        /// Verifies that the main CI workflow still enforces total coverage thresholds.
        /// メイン CI ワークフローが合計カバレッジしきい値を引き続き強制していることを検証します。
        /// </summary>
        [Fact]
        public void DotNetWorkflow_EnforcesCoverageThresholds()
        {
            var workflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "dotnet.yml"));

            Assert.Contains("line_threshold = 80.0", workflow, StringComparison.Ordinal);
            Assert.Contains("branch_threshold = 75.0", workflow, StringComparison.Ordinal);
            Assert.Contains("Enforce coverage thresholds", workflow, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that tagged builds create a GitHub release with attached publish and documentation artifacts.
        /// タグ付きビルドが公開・ドキュメント成果物を添付した GitHub リリースを作成することを検証します。
        /// </summary>
        [Fact]
        public void ReleaseWorkflow_CreatesGitHubReleaseFromVersionTags()
        {
            var workflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "release.yml"));

            Assert.Contains("name: Release", workflow, StringComparison.Ordinal);
            Assert.Contains("tags:", workflow, StringComparison.Ordinal);
            Assert.Contains("- \"v*\"", workflow, StringComparison.Ordinal);
            Assert.Contains("contents: write", workflow, StringComparison.Ordinal);
            Assert.Contains("dotnet publish FolderDiffIL4DotNet.csproj", workflow, StringComparison.Ordinal);
            Assert.Contains("gh release create", workflow, StringComparison.Ordinal);
            Assert.Contains("DocumentationSite", workflow, StringComparison.Ordinal);
            Assert.Contains("Pack global tool NuGet package", workflow, StringComparison.Ordinal);
            Assert.Contains("Publish global tool to nuget.org", workflow, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that CI/release workflows force the real-disassembler E2E gate on the release path.
        /// CI/リリースのワークフローが実逆アセンブラ E2E ゲートをリリース経路で強制していることを検証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void Workflows_EnableRealDisassemblerE2EInCi()
        {
            var dotnetWorkflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "dotnet.yml"));
            var releaseWorkflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "release.yml"));

            Assert.Contains("FOLDERDIFF_RUN_E2E: true", dotnetWorkflow, StringComparison.Ordinal);
            Assert.Contains("FOLDERDIFF_RUN_E2E: true", releaseWorkflow, StringComparison.Ordinal);
            Assert.Contains(".dotnet/tools", dotnetWorkflow, StringComparison.Ordinal);
            Assert.Contains(".dotnet/tools", releaseWorkflow, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that the benchmark regression workflow detects performance degradation on PRs.
        /// ベンチマークリグレッションワークフローが PR でパフォーマンス劣化を検知することを検証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void BenchmarkRegressionWorkflow_DetectsPerformanceDegradation()
        {
            var workflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "benchmark-regression.yml"));

            Assert.Contains("name: Performance Regression Test", workflow, StringComparison.Ordinal);
            Assert.Contains("pull_request:", workflow, StringComparison.Ordinal);
            Assert.Contains("benchmark-action/github-action-benchmark@v1", workflow, StringComparison.Ordinal);
            Assert.Contains("alert-threshold: '200%'", workflow, StringComparison.Ordinal);
            Assert.Contains("fail-on-alert:", workflow, StringComparison.Ordinal);
            Assert.Contains("FolderDiffIL4DotNet.Benchmarks", workflow, StringComparison.Ordinal);
            Assert.Contains("--exporters json", workflow, StringComparison.Ordinal);
            Assert.Contains("combined-report.json", workflow, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that CodeQL and Dependabot are enabled for repository security maintenance.
        /// リポジトリのセキュリティ保守のため CodeQL と Dependabot が有効であることを検証します。
        /// </summary>
        [Fact]
        public void SecurityAutomation_EnablesCodeQlAndDependabot()
        {
            var codeqlWorkflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "codeql.yml"));
            var dependabotConfig = File.ReadAllText(GetRepositoryFilePath(".github", "dependabot.yml"));

            Assert.Contains("github/codeql-action/init@v3", codeqlWorkflow, StringComparison.Ordinal);
            Assert.Contains("github/codeql-action/analyze@v3", codeqlWorkflow, StringComparison.Ordinal);
            Assert.Contains("- csharp", codeqlWorkflow, StringComparison.Ordinal);
            Assert.Contains("- actions", codeqlWorkflow, StringComparison.Ordinal);
            Assert.Contains("schedule:", codeqlWorkflow, StringComparison.Ordinal);

            Assert.Contains("package-ecosystem: \"nuget\"", dependabotConfig, StringComparison.Ordinal);
            Assert.Contains("package-ecosystem: \"github-actions\"", dependabotConfig, StringComparison.Ordinal);
            Assert.Contains("interval: \"weekly\"", dependabotConfig, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that the CI workflow enforces higher per-class coverage thresholds for core diff logic.
        /// CI ワークフローがコア差分ロジックに対してクラス単位の高い閾値を強制していることを検証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void DotNetWorkflow_EnforcesPerClassCoverageThresholds()
        {
            var workflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "dotnet.yml"));

            Assert.Contains("core_class_line_threshold = 90.0", workflow, StringComparison.Ordinal);
            Assert.Contains("core_class_branch_threshold = 85.0", workflow, StringComparison.Ordinal);
            Assert.Contains("FileDiffService", workflow, StringComparison.Ordinal);
            Assert.Contains("FolderDiffService", workflow, StringComparison.Ordinal);
            Assert.Contains("FileComparisonService", workflow, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that mutation testing publishes reviewer-visible summaries and per-run artifacts.
        /// ミューテーションテストがレビューア向けサマリーと run ごとの成果物を公開することを検証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void DotNetWorkflow_MutationTestingPublishesSummaryArtifactsAndPrComment()
        {
            var workflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "dotnet.yml"));

            Assert.Contains("Generate mutation visibility summary", workflow, StringComparison.Ordinal);
            Assert.Contains("python3 scripts/generate-mutation-summary.py", workflow, StringComparison.Ordinal);
            Assert.Contains("Post mutation summary to job summary", workflow, StringComparison.Ordinal);
            Assert.Contains("Post mutation summary to pull request", workflow, StringComparison.Ordinal);
            Assert.Contains("continue-on-error: true", workflow, StringComparison.Ordinal);
            Assert.Contains("actions/github-script@v7", workflow, StringComparison.Ordinal);
            Assert.Contains("issues: write", workflow, StringComparison.Ordinal);
            Assert.Contains("require('./scripts/update-mutation-pr-comment.js')", workflow, StringComparison.Ordinal);
            Assert.Contains("upsertMutationSummaryComment", workflow, StringComparison.Ordinal);
            Assert.Contains("mutation-summary.md", workflow, StringComparison.Ordinal);
            Assert.Contains("mutation-summary.json", workflow, StringComparison.Ordinal);
            Assert.Contains("StrykerSummary-${{ github.run_number }}-${{ github.run_attempt }}", workflow, StringComparison.Ordinal);
            Assert.Contains("StrykerReport-${{ github.run_number }}-${{ github.run_attempt }}", workflow, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that documentation files (CLAUDE.md, TESTING_GUIDE.md, DEVELOPER_GUIDE.md)
        /// reference the same coverage thresholds as the CI workflow.
        /// ドキュメント（CLAUDE.md, TESTING_GUIDE.md, DEVELOPER_GUIDE.md）が CI ワークフローと
        /// 同じカバレッジ閾値を参照していることを検証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void DocumentationThresholds_MatchCiWorkflow()
        {
            // Extract actual thresholds from the CI workflow / CI ワークフローから実際の閾値を取得
            var workflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "dotnet.yml"));
            var lineMatch = Regex.Match(workflow, @"line_threshold\s*=\s*(\d+(?:\.\d+)?)");
            var branchMatch = Regex.Match(workflow, @"branch_threshold\s*=\s*(\d+(?:\.\d+)?)");
            Assert.True(lineMatch.Success, "Could not find line_threshold in dotnet.yml");
            Assert.True(branchMatch.Success, "Could not find branch_threshold in dotnet.yml");

            var lineThreshold = lineMatch.Groups[1].Value.TrimEnd('0').TrimEnd('.');
            var branchThreshold = branchMatch.Groups[1].Value.TrimEnd('0').TrimEnd('.');

            // Verify CLAUDE.md references correct thresholds / CLAUDE.md の閾値を検証
            var claudeMd = File.ReadAllText(GetRepositoryFilePath("CLAUDE.md"));
            Assert.Contains($"line >= {lineThreshold}%", claudeMd, StringComparison.Ordinal);
            Assert.Contains($"branch >= {branchThreshold}%", claudeMd, StringComparison.Ordinal);
            Assert.Contains($"行 >= {lineThreshold}%", claudeMd, StringComparison.Ordinal);
            Assert.Contains($"ブランチ >= {branchThreshold}%", claudeMd, StringComparison.Ordinal);

            // Verify TESTING_GUIDE.md references correct thresholds / TESTING_GUIDE.md の閾値を検証
            var testingGuide = File.ReadAllText(GetRepositoryFilePath("doc", "TESTING_GUIDE.md"));
            Assert.Contains($"`{lineThreshold}%` line", testingGuide, StringComparison.Ordinal);
            Assert.Contains($"`{branchThreshold}%` branch", testingGuide, StringComparison.Ordinal);
            Assert.Contains($"行 `{lineThreshold}%`", testingGuide, StringComparison.Ordinal);
            Assert.Contains($"分岐 `{branchThreshold}%`", testingGuide, StringComparison.Ordinal);

            // Verify DEVELOPER_GUIDE.md references correct thresholds / DEVELOPER_GUIDE.md の閾値を検証
            var devGuide = File.ReadAllText(GetRepositoryFilePath("doc", "DEVELOPER_GUIDE.md"));
            Assert.Contains($"`{lineThreshold}%` line", devGuide, StringComparison.Ordinal);
            Assert.Contains($"`{branchThreshold}%` branch", devGuide, StringComparison.Ordinal);
            Assert.Contains($"行 `{lineThreshold}%`", devGuide, StringComparison.Ordinal);
            Assert.Contains($"分岐 `{branchThreshold}%`", devGuide, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that mutation-test thresholds and visibility docs match the Stryker configuration.
        /// ミューテーションテストの閾値と可視化ドキュメントが Stryker 設定と一致することを検証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void MutationTestingDocumentation_MatchesStrykerConfig()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(GetRepositoryFilePath("stryker-config.json")));
            var thresholds = document.RootElement.GetProperty("stryker-config").GetProperty("thresholds");
            var high = thresholds.GetProperty("high").GetInt32().ToString();
            var low = thresholds.GetProperty("low").GetInt32().ToString();
            var @break = thresholds.GetProperty("break").GetInt32().ToString();

            var summaryScript = File.ReadAllText(GetRepositoryFilePath("scripts", "generate-mutation-summary.py"));
            Assert.Contains("load_thresholds", summaryScript, StringComparison.Ordinal);
            Assert.Contains("stryker-config.json", summaryScript, StringComparison.Ordinal);
            Assert.DoesNotContain("THRESHOLDS = {", summaryScript, StringComparison.Ordinal);

            var testingGuide = File.ReadAllText(GetRepositoryFilePath("doc", "TESTING_GUIDE.md"));
            Assert.Contains($"{high}/{low}/{@break} thresholds", testingGuide, StringComparison.Ordinal);
            Assert.Contains($"{high}/{low}/{@break} 閾値", testingGuide, StringComparison.Ordinal);
            Assert.Contains("StrykerSummary-", testingGuide, StringComparison.Ordinal);
            Assert.Contains("StrykerReport-", testingGuide, StringComparison.Ordinal);

            var devGuide = File.ReadAllText(GetRepositoryFilePath("doc", "DEVELOPER_GUIDE.md"));
            Assert.Contains($"{high}/{low}/{@break}", devGuide, StringComparison.Ordinal);
            Assert.Contains("StrykerSummary-", devGuide, StringComparison.Ordinal);
            Assert.Contains("StrykerReport-", devGuide, StringComparison.Ordinal);
        }

        private static string GetRepositoryFilePath(params string[] segments)
        {
            var path = RepositoryRootPath;
            foreach (var segment in segments)
            {
                path = Path.Combine(path, segment);
            }

            return path;
        }

        private static string RepositoryRootPath =>
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
