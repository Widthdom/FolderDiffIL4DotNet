using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Tests.Helpers;
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
            var releaseWorkflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "release.yml"));
            var runSettings = File.ReadAllText(GetRepositoryFilePath("coverlet.runsettings"));

            Assert.Contains("line_threshold = 80.0", workflow, StringComparison.Ordinal);
            Assert.Contains("branch_threshold = 75.0", workflow, StringComparison.Ordinal);
            Assert.Contains("Enforce coverage thresholds", workflow, StringComparison.Ordinal);
            Assert.Contains("[nildiff]*", runSettings, StringComparison.Ordinal);
            Assert.Contains("[FolderDiffIL4DotNet.Core]*", runSettings, StringComparison.Ordinal);
            Assert.Contains("--settings coverlet.runsettings", workflow, StringComparison.Ordinal);
            Assert.Contains("--settings coverlet.runsettings", releaseWorkflow, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that documentation-only changes still run the main CI workflow.
        /// ドキュメントのみの変更でもメイン CI ワークフローが実行されることを検証します。
        /// </summary>
        [Fact]
        public void DotNetWorkflow_DoesNotSkipDocumentationOnlyChanges()
        {
            var workflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "dotnet.yml"));

            Assert.DoesNotContain("paths-ignore:", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("'doc/**'", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("'**.md'", workflow, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that hidden spinner Easter eggs are not advertised by current user-facing documentation.
        /// 非表示のスピナーイースターエッグが現行のユーザー向け文書で案内されていないことを検証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void UserFacingDocumentation_DoesNotAdvertiseHiddenSpinnerOptions()
        {
            string[] hiddenOptions =
            {
                "--coffee",
                "--beer",
                "--matcha",
                "--whisky",
                "--wine",
                "--ramen",
                "--sushi",
                "--random-spinner",
            };
            string[] topLevelDocuments =
            {
                GetRepositoryFilePath("README.md"),
                GetRepositoryFilePath("USER_GUIDE.md"),
                GetRepositoryFilePath("PACKAGE_README.md"),
                GetRepositoryFilePath("CONTRIBUTING.md"),
                GetRepositoryFilePath("SUPPORT.md"),
                GetRepositoryFilePath("SECURITY.md"),
                GetRepositoryFilePath("index.md"),
                GetRepositoryFilePath("api", "index.md"),
                GetRepositoryFilePath("FolderDiffIL4DotNet.Core", "PACKAGE_README.md"),
                GetRepositoryFilePath("FolderDiffIL4DotNet.Plugin.Abstractions", "PACKAGE_README.md"),
                GetRepositoryFilePath("docfx.json"),
                GetRepositoryFilePath("toc.yml"),
            };

            foreach (var documentPath in topLevelDocuments)
            {
                AssertDocumentDoesNotAdvertiseHiddenOptions(documentPath, hiddenOptions);
            }

            foreach (var documentPath in Directory.GetFiles(GetRepositoryFilePath("doc"), "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(documentPath);
                if (extension is ".md" or ".html" or ".json" or ".jsonc" or ".yml" or ".yaml" or ".xml" or ".txt")
                {
                    AssertDocumentDoesNotAdvertiseHiddenOptions(documentPath, hiddenOptions);
                }
            }
        }

        /// <summary>
        /// Verifies that developers and CI share an exact SDK, formatting rules, and a blocking format gate.
        /// 開発環境と CI が同一の SDK・フォーマット規則・ブロッキング形式検証を共有することを検証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void FormattingBaseline_PinsSdkAndIsEnforcedByCi()
        {
            var workflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "dotnet.yml"));
            var editorConfig = File.ReadAllText(GetRepositoryFilePath(".editorconfig"));
            var gitAttributes = File.ReadAllText(GetRepositoryFilePath(".gitattributes"));
            var contributing = File.ReadAllText(GetRepositoryFilePath("CONTRIBUTING.md"));
            var globalJson = JsonDocument.Parse(File.ReadAllText(GetRepositoryFilePath("global.json"))).RootElement;
            var sdk = globalJson.GetProperty("sdk");

            Assert.Equal("8.0.423", sdk.GetProperty("version").GetString());
            Assert.Equal("disable", sdk.GetProperty("rollForward").GetString());
            Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
            Assert.Contains("global-json-file: global.json", workflow, StringComparison.Ordinal);
            Assert.Contains("name: Verify formatting", workflow, StringComparison.Ordinal);
            Assert.Contains(
                "dotnet format FolderDiffIL4DotNet.sln --verify-no-changes --no-restore --verbosity minimal",
                workflow,
                StringComparison.Ordinal);
            Assert.DoesNotContain("continue-on-error: true\n        run: dotnet format", workflow, StringComparison.Ordinal);
            Assert.Contains("end_of_line = lf", editorConfig, StringComparison.Ordinal);
            Assert.Contains("indent_size = 4", editorConfig, StringComparison.Ordinal);
            Assert.Contains("generated_code = true", editorConfig, StringComparison.Ordinal);
            Assert.Contains("[*.{bat,cmd}]\nend_of_line = crlf", editorConfig, StringComparison.Ordinal);
            Assert.Contains("* text=auto eol=lf", gitAttributes, StringComparison.Ordinal);
            Assert.Contains("*.bat text eol=crlf", gitAttributes, StringComparison.Ordinal);
            Assert.Contains(
                "dotnet format FolderDiffIL4DotNet.sln --verify-no-changes --no-restore",
                contributing,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that npm metadata used for JavaScript tests does not drift from repository metadata.
        /// JavaScript テスト用 npm メタデータがリポジトリメタデータからずれないことを検証します。
        /// </summary>
        [Fact]
        public void PackageJson_MetadataMatchesRepository()
        {
            var packageJson = JsonDocument.Parse(File.ReadAllText(GetRepositoryFilePath("package.json"))).RootElement;
            var versionJson = JsonDocument.Parse(File.ReadAllText(GetRepositoryFilePath("version.json"))).RootElement;

            Assert.Equal("MIT", packageJson.GetProperty("license").GetString());
            Assert.Equal(versionJson.GetProperty("version").GetString(), packageJson.GetProperty("version").GetString());
            Assert.DoesNotContain("local_proxy", packageJson.GetProperty("repository").GetProperty("url").GetString(), StringComparison.Ordinal);
            Assert.Contains("nildiff", packageJson.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that CI restores the locked npm dependency graph, runs every Jest test, and blocks high-severity audit findings on a pinned Node.js version.
        /// CI が固定 Node.js バージョンで npm のロック済み依存グラフを復元し、全 Jest テストを実行して High 以上の監査検出をブロックすることを検証します。
        /// </summary>
        [Fact]
        public void DotNetWorkflow_RunsPinnedJavaScriptTestsAndAuditGate()
        {
            var workflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "dotnet.yml"));
            var nodeVersion = File.ReadAllText(GetRepositoryFilePath(".node-version")).Trim();
            var packageJson = JsonDocument.Parse(File.ReadAllText(GetRepositoryFilePath("package.json"))).RootElement;
            var auditGate = File.ReadAllText(GetRepositoryFilePath("scripts", "npm-audit-gate.js"));
            var auditException = JsonDocument.Parse(
                File.ReadAllText(GetRepositoryFilePath("npm-audit-exceptions.json")))
                .RootElement
                .GetProperty("exceptions")[0];

            Assert.Equal("24.18.0", nodeVersion);
            Assert.Contains("javascript-tests:", workflow, StringComparison.Ordinal);
            Assert.Contains("uses: actions/setup-node@v6", workflow, StringComparison.Ordinal);
            Assert.Contains("node-version-file: .node-version", workflow, StringComparison.Ordinal);
            Assert.Contains("cache: npm", workflow, StringComparison.Ordinal);
            Assert.Contains("cache-dependency-path: package-lock.json", workflow, StringComparison.Ordinal);
            Assert.Contains("run: npm ci", workflow, StringComparison.Ordinal);
            Assert.Contains("run: npm run test:js", workflow, StringComparison.Ordinal);
            Assert.Contains("run: npm run audit:high", workflow, StringComparison.Ordinal);
            Assert.Equal(
                "node scripts/npm-audit-gate.js",
                packageJson.GetProperty("scripts").GetProperty("audit:high").GetString());
            Assert.Contains("runAudit(repositoryRoot, ['--omit=dev'])", auditGate, StringComparison.Ordinal);
            Assert.Contains("result.expiredExceptions.length > 0", auditGate, StringComparison.Ordinal);
            Assert.Equal("GHSA-mh99-v99m-4gvg", auditException.GetProperty("advisory").GetString());
            Assert.Equal(1124334, auditException.GetProperty("source").GetInt32());
            Assert.Equal("brace-expansion", auditException.GetProperty("package").GetString());
            Assert.Equal("high", auditException.GetProperty("severity").GetString());
            Assert.Equal("2026-08-31", auditException.GetProperty("expires").GetString());
            Assert.False(string.IsNullOrWhiteSpace(auditException.GetProperty("rationale").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(auditException.GetProperty("scope").GetString()));
        }

        /// <summary>
        /// Verifies that CI audits direct and transitive NuGet packages and fails on High/Critical findings.
        /// CI が NuGet の直接・推移的 package を監査し、High/Critical の検出を失敗させることを検証します。
        /// </summary>
        [Fact]
        public void DotNetWorkflow_AuditsDirectAndTransitiveNuGetPackages()
        {
            var workflow = File.ReadAllText(GetRepositoryFilePath(".github", "workflows", "dotnet.yml"));
            var auditGate = File.ReadAllText(GetRepositoryFilePath("scripts", "nuget_audit_gate.py"));
            var testProject = File.ReadAllText(
                GetRepositoryFilePath("FolderDiffIL4DotNet.Tests", "FolderDiffIL4DotNet.Tests.csproj"));

            Assert.Contains("Test NuGet audit gate", workflow, StringComparison.Ordinal);
            Assert.Contains(
                "python3 -m unittest discover -s scripts/tests -p 'test_*.py'",
                workflow,
                StringComparison.Ordinal);
            Assert.Contains("Audit NuGet dependencies", workflow, StringComparison.Ordinal);
            Assert.Contains(
                "python3 scripts/nuget_audit_gate.py --solution FolderDiffIL4DotNet.sln",
                workflow,
                StringComparison.Ordinal);
            Assert.Contains("\"--vulnerable\"", auditGate, StringComparison.Ordinal);
            Assert.Contains("\"--include-transitive\"", auditGate, StringComparison.Ordinal);
            Assert.Contains("BLOCKING_SEVERITIES = {\"high\", \"critical\"}", auditGate, StringComparison.Ordinal);
            Assert.Contains("NUGET_AUDIT_SOURCE = \"https://api.nuget.org/v3/index.json\"", auditGate, StringComparison.Ordinal);
            Assert.Contains("GITHUB_STEP_SUMMARY", auditGate, StringComparison.Ordinal);
            Assert.Contains("FsCheck.Xunit\" Version=\"3.3.3\"", testProject, StringComparison.Ordinal);
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
            Assert.Contains("line_threshold = 80.0", workflow, StringComparison.Ordinal);
            Assert.Contains("branch_threshold = 75.0", workflow, StringComparison.Ordinal);
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
            // workflow_dispatch must resolve the tag input to refs/tags/<tag_name>
            // so that branch names (e.g. "main") fail at checkout instead of
            // falling through into tag-assumed downstream steps.
            // workflow_dispatch では tag 入力を refs/tags/<tag_name> に解決し、
            // "main" のようなブランチ名が指定された場合にタグ前提の後続ステップへ
            // 進む前に checkout 段階で失敗させます。
            Assert.Contains(
                "ref: ${{ inputs.tag_name && format('refs/tags/{0}', inputs.tag_name) || github.ref }}",
                workflow,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "ref: ${{ inputs.tag_name || github.ref }}",
                workflow,
                StringComparison.Ordinal);
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
            Skip.IfNot(TestGitRepository.IsGitAvailable(), "git is required to validate the release workflow tag-resolution script.");

            using var repository = await TestGitRepository.CreateAsync("fd-release-tag-resolution-");
            await File.WriteAllTextAsync(Path.Combine(repository.RepositoryPath, "README.md"), "test");
            await repository.RunGitAsync("add", "README.md");
            await repository.RunGitAsync("commit", "-m", "initial");
            await repository.RunGitAsync("tag", "v1.0.0");

            const string script = """
                CURRENT_TAG=$(git describe --tags --exact-match HEAD --match 'v*')
                PREV_TAG=$(git describe --first-parent --tags --abbrev=0 HEAD^ --match 'v*' 2>/dev/null || true)
                if [ -z "$PREV_TAG" ]; then
                  echo "changed=true"
                else
                  echo "changed=false"
                fi
                """;

            var result = await repository.RunCommandAsync("bash", "-eo", "pipefail", "-c", script);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("changed=true", result.StandardOutput, StringComparison.Ordinal);
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
            Skip.IfNot(TestGitRepository.IsGitAvailable(), "git is required to validate the release workflow tag-resolution script.");

            using var repository = await TestGitRepository.CreateAsync("fd-release-prev-tag-");
            var coreDir = Path.Combine(repository.RepositoryPath, "FolderDiffIL4DotNet.Core");
            Directory.CreateDirectory(coreDir);
            var markerPath = Path.Combine(coreDir, "marker.txt");

            await File.WriteAllTextAsync(markerPath, "v1.0.0");
            await repository.RunGitAsync("add", ".");
            await repository.RunGitAsync("commit", "-m", "v1.0.0");
            await repository.RunGitAsync("tag", "v1.0.0");

            await File.WriteAllTextAsync(markerPath, "v1.1.0");
            await repository.RunGitAsync("commit", "-am", "v1.1.0");
            await repository.RunGitAsync("tag", "v1.1.0");

            await File.WriteAllTextAsync(markerPath, "v2.0.0");
            await repository.RunGitAsync("commit", "-am", "v2.0.0");
            await repository.RunGitAsync("tag", "v2.0.0");

            await repository.RunGitAsync("checkout", "v1.1.0");

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

            var result = await repository.RunCommandAsync("bash", "-eo", "pipefail", "-c", script);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("prev=v1.0.0", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("prev=v1.1.0", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("prev=v2.0.0", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("changed=true", result.StandardOutput, StringComparison.Ordinal);
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
            Skip.IfNot(TestGitRepository.IsGitAvailable(), "git is required to validate the release workflow tag-resolution script.");

            using var repository = await TestGitRepository.CreateAsync("fd-release-merge-prev-tag-");
            var coreDir = Path.Combine(repository.RepositoryPath, "FolderDiffIL4DotNet.Core");
            Directory.CreateDirectory(coreDir);
            var markerPath = Path.Combine(coreDir, "marker.txt");

            await File.WriteAllTextAsync(markerPath, "v1.2.0");
            await repository.RunGitAsync("add", ".");
            await repository.RunGitAsync("commit", "-m", "v1.2.0");
            await repository.RunGitAsync("tag", "v1.2.0");
            await repository.RunGitAsync("branch", "maintenance");

            await File.WriteAllTextAsync(markerPath, "v2.0.0");
            await repository.RunGitAsync("commit", "-am", "v2.0.0");
            await repository.RunGitAsync("tag", "v2.0.0");

            await repository.RunGitAsync("checkout", "maintenance");
            await repository.RunGitAsync("merge", "--no-ff", "main", "-m", "merge main");

            await File.WriteAllTextAsync(markerPath, "v1.2.1");
            await repository.RunGitAsync("commit", "-am", "v1.2.1");
            await repository.RunGitAsync("tag", "v1.2.1");

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

            var result = await repository.RunCommandAsync("bash", "-eo", "pipefail", "-c", script);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("current=v1.2.1", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("prev=v1.2.0", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("prev=v2.0.0", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("changed=true", result.StandardOutput, StringComparison.Ordinal);
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
            Assert.DoesNotContain("continue-on-error: true", codeqlWorkflow, StringComparison.Ordinal);

            Assert.Contains("package-ecosystem: \"nuget\"", dependabotConfig, StringComparison.Ordinal);
            Assert.Contains("package-ecosystem: \"github-actions\"", dependabotConfig, StringComparison.Ordinal);
            Assert.Contains("package-ecosystem: \"npm\"", dependabotConfig, StringComparison.Ordinal);
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

            Assert.Contains("core_class_line_threshold = 85.0", workflow, StringComparison.Ordinal);
            Assert.Contains("core_class_branch_threshold = 65.0", workflow, StringComparison.Ordinal);
            Assert.Contains("FileDiffService", workflow, StringComparison.Ordinal);
            Assert.Contains("FolderDiffService", workflow, StringComparison.Ordinal);
            Assert.Contains("FileComparisonService", workflow, StringComparison.Ordinal);
            Assert.Contains("coverage data was not found", workflow, StringComparison.Ordinal);
            Assert.Contains("Core class coverage threshold check failed", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("no coverage data found (skipped)", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("Core class coverage warnings (non-blocking)", workflow, StringComparison.Ordinal);
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
        /// Verifies that documentation files (AGENT_GUIDE.md, TESTING_GUIDE.md, DEVELOPER_GUIDE.md)
        /// reference the same coverage thresholds as the CI workflow.
        /// ドキュメント（AGENT_GUIDE.md, TESTING_GUIDE.md, DEVELOPER_GUIDE.md）が CI ワークフローと
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

            // Verify AGENT_GUIDE.md references correct thresholds / AGENT_GUIDE.md の閾値を検証
            var agentGuide = File.ReadAllText(GetRepositoryFilePath("AGENT_GUIDE.md"));
            Assert.Contains($"line >= {lineThreshold}%", agentGuide, StringComparison.Ordinal);
            Assert.Contains($"branch >= {branchThreshold}%", agentGuide, StringComparison.Ordinal);
            Assert.Contains($"行 >= {lineThreshold}%", agentGuide, StringComparison.Ordinal);
            Assert.Contains($"分岐 >= {branchThreshold}%", agentGuide, StringComparison.Ordinal);

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

        private static void AssertDocumentDoesNotAdvertiseHiddenOptions(
            string documentPath,
            string[] hiddenOptions)
        {
            var contents = File.ReadAllText(documentPath);
            var relativePath = Path.GetRelativePath(RepositoryRootPath, documentPath);
            foreach (var hiddenOption in hiddenOptions)
            {
                Assert.False(
                    contents.Contains(hiddenOption, StringComparison.Ordinal),
                    $"{relativePath} advertises hidden spinner option {hiddenOption}.");
            }
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

        private static string RepositoryRootPath =>
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
