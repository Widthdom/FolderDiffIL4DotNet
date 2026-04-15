using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
            Assert.Contains("packages: write", workflow, StringComparison.Ordinal);
            Assert.Contains("dotnet publish FolderDiffIL4DotNet.csproj", workflow, StringComparison.Ordinal);
            Assert.Contains("gh release create", workflow, StringComparison.Ordinal);
            Assert.Contains("DocumentationSite", workflow, StringComparison.Ordinal);
            Assert.Contains("Pack global tool NuGet package", workflow, StringComparison.Ordinal);
            Assert.Contains("Publish Core to GitHub Packages", workflow, StringComparison.Ordinal);
            Assert.Contains("Publish Plugin.Abstractions to GitHub Packages", workflow, StringComparison.Ordinal);
            Assert.Contains("Publish global tool to GitHub Packages", workflow, StringComparison.Ordinal);
            Assert.Contains("https://nuget.pkg.github.com/${{ github.repository_owner }}/index.json", workflow, StringComparison.Ordinal);
            Assert.Contains("Authenticate GitHub Packages source", workflow, StringComparison.Ordinal);
            Assert.Contains("dotnet nuget add source", workflow, StringComparison.Ordinal);
            Assert.Contains("--name github", workflow, StringComparison.Ordinal);
            Assert.Contains("--username \"${{ github.actor }}\"", workflow, StringComparison.Ordinal);
            Assert.Contains("--password \"${{ secrets.GITHUB_TOKEN }}\"", workflow, StringComparison.Ordinal);
            Assert.Contains("Warn if GitHub Packages auth failed", workflow, StringComparison.Ordinal);
            Assert.Contains("Publish global tool to nuget.org", workflow, StringComparison.Ordinal);
            Assert.Matches(
                new Regex(@"- name: Publish Core to GitHub Packages\s+id: core-gpr-publish\s+if: steps\.core-diff\.outputs\.changed == 'true' && steps\.github-auth\.outcome == 'success'\s+continue-on-error: true\s+run: dotnet nuget push ""nupkgs/FolderDiffIL4DotNet\.Core\.\*\.nupkg"" --source github --skip-duplicate",
                    RegexOptions.Singleline),
                workflow);
            Assert.Matches(
                new Regex(@"- name: Publish Plugin\.Abstractions to GitHub Packages\s+id: plugin-gpr-publish\s+if: steps\.plugin-diff\.outputs\.changed == 'true' && steps\.github-auth\.outcome == 'success'\s+continue-on-error: true\s+run: dotnet nuget push ""nupkgs/FolderDiffIL4DotNet\.Plugin\.Abstractions\.\*\.nupkg"" --source github --skip-duplicate",
                    RegexOptions.Singleline),
                workflow);
            Assert.Matches(
                new Regex(@"- name: Publish Core to nuget\.org\s+if: steps\.core-diff\.outputs\.changed == 'true'",
                    RegexOptions.Singleline),
                workflow);
            Assert.Matches(
                new Regex(@"- name: Publish Plugin\.Abstractions to nuget\.org\s+if: steps\.plugin-diff\.outputs\.changed == 'true'",
                    RegexOptions.Singleline),
                workflow);
            Assert.Matches(
                new Regex(@"- name: Publish global tool to nuget\.org\s+run: dotnet nuget push ""nupkgs/nildiff\.\*\.nupkg"".*?- name: Publish global tool to GitHub Packages\s+id: tool-gpr-publish\s+if: steps\.github-auth\.outcome == 'success'\s+continue-on-error: true\s+run: dotnet nuget push ""nupkgs/nildiff\.\*\.nupkg"" --source github --skip-duplicate",
                    RegexOptions.Singleline),
                workflow);
            Assert.Matches(
                new Regex(@"- name: Publish Core to nuget\.org.*?- name: Publish global tool to nuget\.org.*?- name: Authenticate GitHub Packages source.*?- name: Publish Core to GitHub Packages",
                    RegexOptions.Singleline),
                workflow);
            Assert.Matches(
                new Regex(@"- name: Publish Plugin\.Abstractions to nuget\.org.*?- name: Publish global tool to nuget\.org.*?- name: Authenticate GitHub Packages source.*?- name: Publish Plugin\.Abstractions to GitHub Packages",
                    RegexOptions.Singleline),
                workflow);
            Assert.Matches(
                new Regex(@"- name: Restore Core dependencies.*?- name: Publish global tool to nuget\.org.*?- name: Authenticate GitHub Packages source",
                    RegexOptions.Singleline),
                workflow);
            Assert.Contains("CURRENT_TAG=$(git describe --tags --exact-match HEAD --match 'v*')", workflow, StringComparison.Ordinal);
            Assert.Contains("PREV_TAG=$(git describe --first-parent --tags --abbrev=0 HEAD^ --match 'v*' 2>/dev/null || true)", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("Check if Core exists on GitHub Packages", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("Check if Plugin.Abstractions exists on GitHub Packages", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("owner_path=\"users\"", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("owner_path=\"orgs\"", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("exists=unknown", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("steps.core-gpr.outputs.exists", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("steps.plugin-gpr.outputs.exists", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("grep -Fxv \"$CURRENT_TAG\"", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("git for-each-ref --merged HEAD --sort=-version:refname --format='%(refname:short)' refs/tags/v*", workflow, StringComparison.Ordinal);
            Assert.DoesNotMatch(
                new Regex(@"- name: Authenticate GitHub Packages source.*?- name: Restore Core dependencies",
                    RegexOptions.Singleline),
                workflow);
        }

        /// <summary>
        /// Verifies that the release workflow's previous-tag logic survives bash `-eo pipefail`
        /// even when the current tag is the only reachable `v*` tag.
        /// リリース workflow の前回タグ解決が、current しか reachable な `v*` タグがない場合でも
        /// bash `-eo pipefail` で失敗せずに動作することを検証します。
        /// </summary>
        [SkippableFact]
        [Trait("Category", "Unit")]
        public async Task ReleaseWorkflow_PreviousTagResolution_WithCurrentTagOnly_DoesNotFailUnderPipefail()
        {
            Skip.IfNot(CanRunCommand("bash", "--version"), "bash is required to validate the release workflow tag-resolution script.");
            Skip.IfNot(CanRunCommand("git", "--version"), "git is required to validate the release workflow tag-resolution script.");

            var repoRoot = Path.Combine(Path.GetTempPath(), "fd-release-tag-resolution-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoRoot);

            try
            {
                await RunProcessAsync("git", repoRoot, "init");
                await RunProcessAsync("git", repoRoot, "config", "user.email", "ci@example.invalid");
                await RunProcessAsync("git", repoRoot, "config", "user.name", "CI Test");
                await File.WriteAllTextAsync(Path.Combine(repoRoot, "README.md"), "test");
                await RunProcessAsync("git", repoRoot, "add", "README.md");
                await RunProcessAsync("git", repoRoot, "commit", "-m", "initial");
                await RunProcessAsync("git", repoRoot, "tag", "v1.0.0");

                const string script = """
                    CURRENT_TAG=$(git describe --tags --exact-match HEAD --match 'v*')
                    PREV_TAG=$(git describe --first-parent --tags --abbrev=0 HEAD^ --match 'v*' 2>/dev/null || true)
                    if [ -z "$PREV_TAG" ]; then
                      echo "changed=true"
                    else
                      echo "changed=false"
                    fi
                    """;

                var result = await RunProcessAsync("bash", repoRoot, "-eo", "pipefail", "-c", script);
                Assert.Equal(0, result.ExitCode);
                Assert.Contains("changed=true", result.StandardOutput, StringComparison.Ordinal);
            }
            finally
            {
                TryDeleteDirectory(repoRoot);
            }
        }

        /// <summary>
        /// Verifies that an older manually dispatched tag still resolves the previous tag
        /// on the current first-parent release line.
        /// 古い既存タグを手動実行した場合でも、現在の first-parent リリース系列上にある
        /// 直前のタグを解決することを検証します。
        /// </summary>
        [SkippableFact]
        [Trait("Category", "Unit")]
        public async Task ReleaseWorkflow_PreviousTagResolution_WithOlderDispatchedTag_UsesPreviousReachableTag()
        {
            Skip.IfNot(CanRunCommand("bash", "--version"), "bash is required to validate the release workflow tag-resolution script.");
            Skip.IfNot(CanRunCommand("git", "--version"), "git is required to validate the release workflow tag-resolution script.");

            var repoRoot = Path.Combine(Path.GetTempPath(), "fd-release-prev-tag-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoRoot);

            try
            {
                var coreDir = Path.Combine(repoRoot, "FolderDiffIL4DotNet.Core");
                Directory.CreateDirectory(coreDir);
                var markerPath = Path.Combine(coreDir, "marker.txt");

                await RunProcessAsync("git", repoRoot, "init");
                await RunProcessAsync("git", repoRoot, "config", "user.email", "ci@example.invalid");
                await RunProcessAsync("git", repoRoot, "config", "user.name", "CI Test");

                await File.WriteAllTextAsync(markerPath, "v1.0.0");
                await RunProcessAsync("git", repoRoot, "add", ".");
                await RunProcessAsync("git", repoRoot, "commit", "-m", "v1.0.0");
                await RunProcessAsync("git", repoRoot, "tag", "v1.0.0");

                await File.WriteAllTextAsync(markerPath, "v1.1.0");
                await RunProcessAsync("git", repoRoot, "commit", "-am", "v1.1.0");
                await RunProcessAsync("git", repoRoot, "tag", "v1.1.0");

                await File.WriteAllTextAsync(markerPath, "v2.0.0");
                await RunProcessAsync("git", repoRoot, "commit", "-am", "v2.0.0");
                await RunProcessAsync("git", repoRoot, "tag", "v2.0.0");

                await RunProcessAsync("git", repoRoot, "checkout", "v1.1.0");

                const string script = """
                    CURRENT_TAG=$(git describe --tags --exact-match HEAD --match 'v*')
                    PREV_TAG=$(git describe --first-parent --tags --abbrev=0 HEAD^ --match 'v*' 2>/dev/null || true)

                    echo "prev=$PREV_TAG"
                    if [ -z "$PREV_TAG" ]; then
                      echo "changed=true"
                    elif git diff --quiet "${PREV_TAG}..HEAD" -- FolderDiffIL4DotNet.Core/; then
                      echo "changed=false"
                    else
                      echo "changed=true"
                    fi
                    """;

                var result = await RunProcessAsync("bash", repoRoot, "-eo", "pipefail", "-c", script);
                Assert.Equal(0, result.ExitCode);
                Assert.Contains("prev=v1.0.0", result.StandardOutput, StringComparison.Ordinal);
                Assert.DoesNotContain("prev=v1.1.0", result.StandardOutput, StringComparison.Ordinal);
                Assert.DoesNotContain("prev=v2.0.0", result.StandardOutput, StringComparison.Ordinal);
                Assert.Contains("changed=true", result.StandardOutput, StringComparison.Ordinal);
            }
            finally
            {
                TryDeleteDirectory(repoRoot);
            }
        }

        /// <summary>
        /// Verifies that a maintenance release line still resolves its previous maintenance tag
        /// after merging a newer mainline release branch.
        /// main 系のより新しいリリースを取り込んだ後でも、保守リリース系列では
        /// 直前の保守タグを解決することを検証します。
        /// </summary>
        [SkippableFact]
        [Trait("Category", "Unit")]
        public async Task ReleaseWorkflow_PreviousTagResolution_WithMergedMainlineRelease_UsesPreviousFirstParentTag()
        {
            Skip.IfNot(CanRunCommand("bash", "--version"), "bash is required to validate the release workflow tag-resolution script.");
            Skip.IfNot(CanRunCommand("git", "--version"), "git is required to validate the release workflow tag-resolution script.");

            var repoRoot = Path.Combine(Path.GetTempPath(), "fd-release-merge-prev-tag-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoRoot);

            try
            {
                var coreDir = Path.Combine(repoRoot, "FolderDiffIL4DotNet.Core");
                Directory.CreateDirectory(coreDir);
                var markerPath = Path.Combine(coreDir, "marker.txt");

                await RunProcessAsync("git", repoRoot, "init", "-b", "main");
                await RunProcessAsync("git", repoRoot, "config", "user.email", "ci@example.invalid");
                await RunProcessAsync("git", repoRoot, "config", "user.name", "CI Test");

                await File.WriteAllTextAsync(markerPath, "v1.2.0");
                await RunProcessAsync("git", repoRoot, "add", ".");
                await RunProcessAsync("git", repoRoot, "commit", "-m", "v1.2.0");
                await RunProcessAsync("git", repoRoot, "tag", "v1.2.0");
                await RunProcessAsync("git", repoRoot, "branch", "maintenance");

                await File.WriteAllTextAsync(markerPath, "v2.0.0");
                await RunProcessAsync("git", repoRoot, "commit", "-am", "v2.0.0");
                await RunProcessAsync("git", repoRoot, "tag", "v2.0.0");

                await RunProcessAsync("git", repoRoot, "checkout", "maintenance");
                await RunProcessAsync("git", repoRoot, "merge", "--no-ff", "main", "-m", "merge main");

                await File.WriteAllTextAsync(markerPath, "v1.2.1");
                await RunProcessAsync("git", repoRoot, "commit", "-am", "v1.2.1");
                await RunProcessAsync("git", repoRoot, "tag", "v1.2.1");

                const string script = """
                    CURRENT_TAG=$(git describe --tags --exact-match HEAD --match 'v*')
                    PREV_TAG=$(git describe --first-parent --tags --abbrev=0 HEAD^ --match 'v*' 2>/dev/null || true)
                    echo "current=$CURRENT_TAG"
                    echo "prev=$PREV_TAG"
                    if [ -z "$PREV_TAG" ]; then
                      echo "changed=true"
                    elif git diff --quiet "${PREV_TAG}..HEAD" -- FolderDiffIL4DotNet.Core/; then
                      echo "changed=false"
                    else
                      echo "changed=true"
                    fi
                    """;

                var result = await RunProcessAsync("bash", repoRoot, "-eo", "pipefail", "-c", script);
                Assert.Equal(0, result.ExitCode);
                Assert.Contains("current=v1.2.1", result.StandardOutput, StringComparison.Ordinal);
                Assert.Contains("prev=v1.2.0", result.StandardOutput, StringComparison.Ordinal);
                Assert.DoesNotContain("prev=v2.0.0", result.StandardOutput, StringComparison.Ordinal);
                Assert.Contains("changed=true", result.StandardOutput, StringComparison.Ordinal);
            }
            finally
            {
                TryDeleteDirectory(repoRoot);
            }
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

        private static bool CanRunCommand(string fileName, params string[] arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                foreach (var argument in arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return false;
                }

                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunProcessAsync(string fileName, string workingDirectory, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start process '{fileName}'.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Process '{fileName}' failed with exit code {process.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
            }

            return (process.ExitCode, stdout, stderr);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup errors / クリーンアップエラーを無視
            }
        }

        private static string RepositoryRootPath =>
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
