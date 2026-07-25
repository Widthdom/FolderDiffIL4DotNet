using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FolderDiffIL4DotNet.Tests.Helpers
{
    /// <summary>
    /// Creates and runs a temporary Git repository with isolated, non-interactive configuration.
    /// 分離された非対話設定で一時 Git リポジトリを作成・実行します。
    /// </summary>
    internal sealed class TestGitRepository : IDisposable
    {
        private static readonly TimeSpan s_processTimeout = TimeSpan.FromSeconds(30);

        private static readonly string[] s_inheritedGitEnvironmentVariables =
        {
            "GIT_DIR",
            "GIT_WORK_TREE",
            "GIT_INDEX_FILE",
            "GIT_OBJECT_DIRECTORY",
            "GIT_ALTERNATE_OBJECT_DIRECTORIES",
            "GIT_COMMON_DIR",
            "GIT_NAMESPACE",
            "GIT_PREFIX",
            "GIT_CEILING_DIRECTORIES",
            "GIT_DISCOVERY_ACROSS_FILESYSTEM",
            "GIT_CONFIG",
            "GIT_CONFIG_SYSTEM",
            "GIT_CONFIG_GLOBAL",
            "GIT_CONFIG_NOSYSTEM",
            "GIT_CONFIG_COUNT",
            "GIT_CONFIG_PARAMETERS",
            "GIT_AUTHOR_NAME",
            "GIT_AUTHOR_EMAIL",
            "GIT_AUTHOR_DATE",
            "GIT_COMMITTER_NAME",
            "GIT_COMMITTER_EMAIL",
            "GIT_COMMITTER_DATE"
        };

        private readonly string _testRootPath;
        private readonly IReadOnlyDictionary<string, string> _environmentVariables;
        private bool _disposed;

        private TestGitRepository(
            string testRootPath,
            string repositoryPath,
            string isolatedGlobalConfigPath)
        {
            _testRootPath = testRootPath;
            RepositoryPath = repositoryPath;
            IsolatedGlobalConfigPath = isolatedGlobalConfigPath;
            var isolatedHomePath = Path.Combine(testRootPath, "home");
            var isolatedXdgConfigPath = Path.Combine(testRootPath, "xdg-config");
            var isolatedTemplatePath = Path.Combine(testRootPath, "templates");
            _environmentVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HOME"] = isolatedHomePath,
                ["XDG_CONFIG_HOME"] = isolatedXdgConfigPath,
                ["GIT_CONFIG_GLOBAL"] = isolatedGlobalConfigPath,
                ["GIT_CONFIG_NOSYSTEM"] = "1",
                ["GIT_CONFIG_COUNT"] = "0",
                ["GIT_ATTR_NOSYSTEM"] = "1",
                ["GIT_TEMPLATE_DIR"] = isolatedTemplatePath,
                ["GIT_TERMINAL_PROMPT"] = "0",
                ["GCM_INTERACTIVE"] = "Never",
                ["GIT_EDITOR"] = "true",
                ["GIT_SEQUENCE_EDITOR"] = "true",
                ["GIT_MERGE_AUTOEDIT"] = "no",
                ["GIT_PAGER"] = "cat"
            };
        }

        /// <summary>
        /// Gets the isolated worktree path.
        /// 分離された作業ツリーのパスを取得します。
        /// </summary>
        internal string RepositoryPath { get; }

        /// <summary>
        /// Gets the test-only global Git configuration path.
        /// テスト専用のグローバル Git 設定パスを取得します。
        /// </summary>
        internal string IsolatedGlobalConfigPath { get; }

        /// <summary>
        /// Returns whether Git can be started in the current environment.
        /// 現在の環境で Git を起動できるかを返します。
        /// </summary>
        internal static bool IsGitAvailable()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("--version");

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

        /// <summary>
        /// Creates an initialized repository with deterministic local identity and signing disabled.
        /// 決定的なローカル ID と署名無効化を設定した初期化済みリポジトリを作成します。
        /// </summary>
        /// <param name="directoryPrefix">
        /// Unique temporary-directory prefix.
        /// 一意な一時ディレクトリ接頭辞。
        /// </param>
        /// <param name="simulatedGlobalConfig">
        /// Optional isolated global configuration used by regression tests.
        /// 回帰テストで使う任意の分離グローバル設定。
        /// </param>
        /// <param name="simulatedUserExcludes">
        /// Optional test-only default user excludes.
        /// テスト専用の任意のデフォルトユーザー除外設定。
        /// </param>
        internal static async Task<TestGitRepository> CreateAsync(
            string directoryPrefix,
            string simulatedGlobalConfig = "",
            string simulatedUserExcludes = "")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directoryPrefix);

            var testRootPath = Path.Combine(
                Path.GetTempPath(),
                directoryPrefix + Guid.NewGuid().ToString("N"));
            var repositoryPath = Path.Combine(testRootPath, "repository");
            var isolatedGlobalConfigPath = Path.Combine(testRootPath, "global.gitconfig");
            var hooksPath = Path.Combine(testRootPath, "hooks");
            var homePath = Path.Combine(testRootPath, "home");
            var xdgConfigPath = Path.Combine(testRootPath, "xdg-config");
            var templatePath = Path.Combine(testRootPath, "templates");
            var excludesPath = Path.Combine(testRootPath, "excludes");
            var attributesPath = Path.Combine(testRootPath, "attributes");

            Directory.CreateDirectory(repositoryPath);
            Directory.CreateDirectory(hooksPath);
            Directory.CreateDirectory(homePath);
            Directory.CreateDirectory(Path.Combine(xdgConfigPath, "git"));
            Directory.CreateDirectory(templatePath);
            await File.WriteAllTextAsync(isolatedGlobalConfigPath, simulatedGlobalConfig);
            await File.WriteAllTextAsync(
                Path.Combine(xdgConfigPath, "git", "ignore"),
                simulatedUserExcludes);
            await File.WriteAllTextAsync(excludesPath, string.Empty);
            await File.WriteAllTextAsync(attributesPath, string.Empty);

            var repository = new TestGitRepository(
                testRootPath,
                repositoryPath,
                isolatedGlobalConfigPath);

            try
            {
                await repository.RunGitAsync("init", "-b", "main");
                await repository.RunGitAsync("config", "--local", "user.email", "ci@example.invalid");
                await repository.RunGitAsync("config", "--local", "user.name", "CI Test");
                await repository.RunGitAsync("config", "--local", "user.useConfigOnly", "true");
                await repository.RunGitAsync("config", "--local", "commit.gpgSign", "false");
                await repository.RunGitAsync("config", "--local", "tag.gpgSign", "false");
                await repository.RunGitAsync("config", "--local", "core.hooksPath", hooksPath);
                await repository.RunGitAsync("config", "--local", "core.excludesFile", excludesPath);
                await repository.RunGitAsync("config", "--local", "core.attributesFile", attributesPath);
                return repository;
            }
            catch
            {
                repository.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Runs Git inside the isolated repository.
        /// 分離されたリポジトリ内で Git を実行します。
        /// </summary>
        internal Task<(int ExitCode, string StandardOutput, string StandardError)> RunGitAsync(
            params string[] arguments)
            => RunCommandAsync("git", arguments);

        /// <summary>
        /// Runs a command with the repository's isolated Git environment.
        /// リポジトリの分離 Git 環境を引き継いでコマンドを実行します。
        /// </summary>
        internal async Task<(int ExitCode, string StandardOutput, string StandardError)> RunCommandAsync(
            string fileName,
            params string[] arguments)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = RepositoryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var variableName in s_inheritedGitEnvironmentVariables)
            {
                startInfo.Environment.Remove(variableName);
            }

            foreach (var environmentVariable in _environmentVariables)
            {
                startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
            }

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
            using var timeoutSource = new CancellationTokenSource(s_processTimeout);

            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);
                throw new TimeoutException(
                    $"Process '{fileName}' did not exit within {s_processTimeout.TotalSeconds:0} seconds.");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Process '{fileName}' failed with exit code {process.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
            }

            return (process.ExitCode, stdout, stderr);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                if (Directory.Exists(_testRootPath))
                {
                    Directory.Delete(_testRootPath, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors.
                // クリーンアップエラーを無視します。
            }
        }

        private static void TryKillProcess(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
            catch
            {
                // Preserve the timeout as the actionable failure.
                // 実行可能な失敗として timeout を維持します。
            }
        }
    }
}
