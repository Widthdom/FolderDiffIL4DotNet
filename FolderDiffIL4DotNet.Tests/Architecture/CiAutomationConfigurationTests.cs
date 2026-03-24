using System;
using System.IO;
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
