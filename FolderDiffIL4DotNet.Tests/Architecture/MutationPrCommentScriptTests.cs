using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Architecture
{
    /// <summary>
    /// Verifies that the PR-comment helper targets only bot-owned sticky comments.
    /// PR コメント helper が bot 所有の sticky comment だけを対象にすることを検証します。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class MutationPrCommentScriptTests : IDisposable
    {
        private readonly string _rootDir;

        public MutationPrCommentScriptTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-mutation-pr-comment-script-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_rootDir))
                {
                    Directory.Delete(_rootDir, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup errors / クリーンアップエラーを無視
            }
        }

        /// <summary>
        /// Verifies that user-authored comments with the marker are ignored.
        /// marker を含んでもユーザーコメントは更新対象にしないことを検証します。
        /// </summary>
        [SkippableFact]
        public async Task SelectExistingMutationComment_IgnoresNonBotComments()
        {
            Skip.IfNot(TryGetNodeLauncher(out _), "Node.js is required to run mutation PR comment script tests.");

            var commentsPath = Path.Combine(_rootDir, "comments.json");
            await File.WriteAllTextAsync(
                commentsPath,
                """
                [
                  { "id": 10, "body": "<!-- folderdiff-mutation-summary -->\nuser", "user": { "login": "maintainer" } },
                  { "id": 11, "body": "plain comment", "user": { "login": "github-actions[bot]" } }
                ]
                """);

            var selectedId = await SelectCommentIdAsync(commentsPath);
            Assert.Equal("null", selectedId);
        }

        /// <summary>
        /// Verifies that the latest bot-owned sticky comment is selected.
        /// bot 所有の sticky comment が複数ある場合は最新のものを選ぶことを検証します。
        /// </summary>
        [SkippableFact]
        public async Task SelectExistingMutationComment_UsesLatestBotComment()
        {
            Skip.IfNot(TryGetNodeLauncher(out _), "Node.js is required to run mutation PR comment script tests.");

            var commentsPath = Path.Combine(_rootDir, "comments.json");
            await File.WriteAllTextAsync(
                commentsPath,
                """
                [
                  { "id": 20, "body": "<!-- folderdiff-mutation-summary -->\nolder", "user": { "login": "github-actions[bot]" } },
                  { "id": 21, "body": "<!-- folderdiff-mutation-summary -->\nuser", "user": { "login": "maintainer" } },
                  { "id": 22, "body": "<!-- folderdiff-mutation-summary -->\nnewer", "user": { "login": "github-actions[bot]" } }
                ]
                """);

            var selectedId = await SelectCommentIdAsync(commentsPath);
            Assert.Equal("22", selectedId);
        }

        /// <summary>
        /// Verifies that the helper updates the latest bot-owned sticky comment in place.
        /// helper が最新の bot 所有 sticky comment をその場で更新することを検証します。
        /// </summary>
        [SkippableFact]
        public async Task UpsertMutationSummaryComment_WithExistingBotComment_UpdatesComment()
        {
            Skip.IfNot(TryGetNodeLauncher(out _), "Node.js is required to run mutation PR comment script tests.");

            var summaryPath = Path.Combine(_rootDir, "summary.md");
            await File.WriteAllTextAsync(summaryPath, "## Mutation Testing Results\n\n- Mutation score: **90.00%**\n");

            var commentsPath = Path.Combine(_rootDir, "update-comments.json");
            await File.WriteAllTextAsync(
                commentsPath,
                """
                [
                  { "id": 40, "body": "<!-- folderdiff-mutation-summary -->\nolder", "user": { "login": "github-actions[bot]" } },
                  { "id": 41, "body": "<!-- folderdiff-mutation-summary -->\nforeign", "user": { "login": "maintainer" } },
                  { "id": 42, "body": "<!-- folderdiff-mutation-summary -->\ncurrent", "user": { "login": "github-actions[bot]" } }
                ]
                """);

            using var result = await RunUpsertAsync(commentsPath, summaryPath);
            Assert.Equal("updated", result.RootElement.GetProperty("result").GetProperty("action").GetString());
            Assert.Equal(42, result.RootElement.GetProperty("result").GetProperty("commentId").GetInt32());

            var updateCall = Assert.Single(result.RootElement.GetProperty("calls").EnumerateArray());
            Assert.Equal("update", updateCall.GetProperty("kind").GetString());
            Assert.Equal(42, updateCall.GetProperty("args").GetProperty("comment_id").GetInt32());
            Assert.Equal("Widthdom", updateCall.GetProperty("args").GetProperty("owner").GetString());
            Assert.Equal("FolderDiffIL4DotNet", updateCall.GetProperty("args").GetProperty("repo").GetString());

            var body = updateCall.GetProperty("args").GetProperty("body").GetString();
            Assert.NotNull(body);
            Assert.StartsWith("<!-- folderdiff-mutation-summary -->", body, StringComparison.Ordinal);
            Assert.Contains("Mutation score: **90.00%**", body, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that the helper creates a new bot comment when no sticky bot comment exists.
        /// 既存の sticky bot comment がない場合に新規コメントを作成することを検証します。
        /// </summary>
        [SkippableFact]
        public async Task UpsertMutationSummaryComment_WithoutExistingBotComment_CreatesComment()
        {
            Skip.IfNot(TryGetNodeLauncher(out _), "Node.js is required to run mutation PR comment script tests.");

            var summaryPath = Path.Combine(_rootDir, "summary-create.md");
            await File.WriteAllTextAsync(summaryPath, "## Mutation Testing Results\n\n- Mutation score: **61.00%**\n");

            var commentsPath = Path.Combine(_rootDir, "create-comments.json");
            await File.WriteAllTextAsync(
                commentsPath,
                """
                [
                  { "id": 50, "body": "<!-- folderdiff-mutation-summary -->\nforeign", "user": { "login": "maintainer" } },
                  { "id": 51, "body": "plain comment", "user": { "login": "github-actions[bot]" } }
                ]
                """);

            using var result = await RunUpsertAsync(commentsPath, summaryPath);
            Assert.Equal("created", result.RootElement.GetProperty("result").GetProperty("action").GetString());
            Assert.Equal(99, result.RootElement.GetProperty("result").GetProperty("commentId").GetInt32());

            var createCall = Assert.Single(result.RootElement.GetProperty("calls").EnumerateArray());
            Assert.Equal("create", createCall.GetProperty("kind").GetString());
            Assert.Equal(123, createCall.GetProperty("args").GetProperty("issue_number").GetInt32());
            Assert.Equal("Widthdom", createCall.GetProperty("args").GetProperty("owner").GetString());
            Assert.Equal("FolderDiffIL4DotNet", createCall.GetProperty("args").GetProperty("repo").GetString());

            var body = createCall.GetProperty("args").GetProperty("body").GetString();
            Assert.NotNull(body);
            Assert.StartsWith("<!-- folderdiff-mutation-summary -->", body, StringComparison.Ordinal);
            Assert.Contains("Mutation score: **61.00%**", body, StringComparison.Ordinal);
        }

        private async Task<string> SelectCommentIdAsync(string commentsPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "node",
                WorkingDirectory = RepositoryRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add(
                "const fs=require('fs');" +
                "const mod=require(process.argv[1]);" +
                "const comments=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));" +
                "const result=mod.selectExistingMutationComment(comments,'<!-- folderdiff-mutation-summary -->');" +
                "process.stdout.write(result ? String(result.id) : 'null');");
            startInfo.ArgumentList.Add(GetRepositoryFilePath("scripts", "update-mutation-pr-comment.js"));
            startInfo.ArgumentList.Add(commentsPath);

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Node.js for mutation PR comment test.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Mutation PR comment helper test failed with exit code {process.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
            }

            return stdout.Trim();
        }

        private async Task<JsonDocument> RunUpsertAsync(string commentsPath, string summaryPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "node",
                WorkingDirectory = RepositoryRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add(
                "const fs=require('fs');" +
                "const mod=require(process.argv[1]);" +
                "const comments=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));" +
                "const summaryPath=process.argv[3];" +
                "const calls=[];" +
                "const github={" +
                  "paginate: async () => comments," +
                  "rest:{issues:{" +
                    "listComments: async () => comments," +
                    "updateComment: async (args) => { calls.push({ kind: 'update', args }); return { data: { id: args.comment_id } }; }," +
                    "createComment: async (args) => { calls.push({ kind: 'create', args }); return { data: { id: 99 } }; }" +
                  "}}" +
                "};" +
                "const context={ issue:{ number:123 }, repo:{ owner:'Widthdom', repo:'FolderDiffIL4DotNet' } };" +
                "(async () => {" +
                  "const result=await mod.upsertMutationSummaryComment({ github, context, summaryPath });" +
                  "process.stdout.write(JSON.stringify({ result, calls }));" +
                "})().catch((error) => {" +
                  "console.error(error && error.stack ? error.stack : String(error));" +
                  "process.exit(1);" +
                "});");
            startInfo.ArgumentList.Add(GetRepositoryFilePath("scripts", "update-mutation-pr-comment.js"));
            startInfo.ArgumentList.Add(commentsPath);
            startInfo.ArgumentList.Add(summaryPath);

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Node.js for mutation PR comment upsert test.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Mutation PR comment helper upsert test failed with exit code {process.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
            }

            return JsonDocument.Parse(stdout);
        }

        private static bool TryGetNodeLauncher(out string fileName)
        {
            fileName = "node";
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
                startInfo.ArgumentList.Add("--version");

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return false;
                }

                if (!process.WaitForExit(10000))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // ignore kill failures when probing availability / 利用可能性チェック時の kill 失敗を無視
                    }

                    return false;
                }

                return process.HasExited && process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
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
