using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Helpers
{
    /// <summary>
    /// Verifies deterministic configuration for temporary Git repositories.
    /// 一時 Git リポジトリの決定的な設定を検証します。
    /// </summary>
    public sealed class TestGitRepositoryTests
    {
        /// <summary>
        /// Verifies that simulated global signing and user excludes do not affect local commits or tags.
        /// 模擬グローバル署名とユーザー除外設定がローカルの commit と tag に影響しないことを検証します。
        /// </summary>
        [SkippableFact]
        [Trait("Category", "Unit")]
        public async Task CreateAsync_WithGlobalSigningAndUserExcludes_IsolatesConfiguration()
        {
            Skip.IfNot(TestGitRepository.IsGitAvailable(), "git is required to validate test repository isolation.");

            const string simulatedGlobalConfig = """
                [commit]
                    gpgSign = true
                [tag]
                    gpgSign = true
                [gpg]
                    format = ssh
                [user]
                    signingKey = /definitely/missing/nildiff-test-signing-key
                """;

            using var repository = await TestGitRepository.CreateAsync(
                "fd-test-git-isolation-",
                simulatedGlobalConfig,
                "*.md");

            var globalSigning = await repository.RunGitAsync(
                "config",
                "--global",
                "--get",
                "commit.gpgsign");
            Assert.Equal("true", globalSigning.StandardOutput.Trim());

            var markerPath = Path.Combine(repository.RepositoryPath, "README.md");
            await File.WriteAllTextAsync(markerPath, "test");
            await repository.RunGitAsync("add", "README.md");
            await repository.RunGitAsync("commit", "-m", "initial");
            await repository.RunGitAsync("tag", "-a", "v1.0.0", "-m", "v1.0.0");

            var localCommitSigning = await repository.RunGitAsync(
                "config",
                "--local",
                "--get",
                "commit.gpgsign");
            var localTagSigning = await repository.RunGitAsync(
                "config",
                "--local",
                "--get",
                "tag.gpgsign");
            var commitSubject = await repository.RunGitAsync(
                "log",
                "-1",
                "--format=%s");
            var trackedFiles = await repository.RunGitAsync("ls-files");

            Assert.Equal("false", localCommitSigning.StandardOutput.Trim());
            Assert.Equal("false", localTagSigning.StandardOutput.Trim());
            Assert.Equal("initial", commitSubject.StandardOutput.Trim());
            Assert.Contains("README.md", trackedFiles.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(
                simulatedGlobalConfig,
                await File.ReadAllTextAsync(repository.IsolatedGlobalConfigPath));
        }
    }
}
