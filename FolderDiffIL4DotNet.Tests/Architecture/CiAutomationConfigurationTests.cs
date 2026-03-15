using System;
using System.IO;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Architecture
{
    /// <summary>
    /// Verifies that repository automation for CI, releases, and security scanning remains configured.
    /// </summary>
    public sealed class CiAutomationConfigurationTests
    {
        /// <summary>
        /// Verifies that the main CI workflow still enforces total coverage thresholds.
        /// </summary>
        [Fact]
        public void DotNetWorkflow_EnforcesCoverageThresholds()
        {
            var workflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "dotnet.yml"));

            Assert.Contains("line_threshold = 73.0", workflow, StringComparison.Ordinal);
            Assert.Contains("branch_threshold = 71.0", workflow, StringComparison.Ordinal);
            Assert.Contains("Enforce coverage thresholds", workflow, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that tagged builds create a GitHub release with attached publish and documentation artifacts.
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
        /// Resolves a repository-relative file path for configuration tests.
        /// </summary>
        /// <param name="segments">The repository-relative path segments.</param>
        /// <returns>The absolute path to the requested repository file.</returns>
        private static string GetRepositoryFilePath(params string[] segments)
        {
            var path = RepositoryRootPath;
            foreach (var segment in segments)
            {
                path = Path.Combine(path, segment);
            }

            return path;
        }

        /// <summary>
        /// Gets the absolute path to the repository root from the test output directory.
        /// </summary>
        private static string RepositoryRootPath =>
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
