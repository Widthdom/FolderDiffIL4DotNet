// ProgramRunnerTests.HelpVersion.cs — Help, version, and config flag early-exit tests (partial 2/4)
// ProgramRunnerTests.HelpVersion.cs — ヘルプ・バージョン・設定フラグの早期終了テスト（パーシャル 2/4）

using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests
{
    public sealed partial class ProgramRunnerTests
    {
        // -----------------------------------------------------------------------
        // --help / --version early-exit tests
        // --help / --version 早期終了テスト
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_HelpFlag_ExitsZeroWithoutInitializingLogger()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--help" });

                Assert.Equal(0, exitCode);
                var output = sw.ToString();
                Assert.Contains("Usage:", output, StringComparison.Ordinal);
                Assert.Contains("--config", output, StringComparison.Ordinal);
                Assert.Contains("--skip-il", output, StringComparison.Ordinal);
                // Logger should NOT have been initialized (no log messages)
                // ロガーは初期化されていないはず（ログメッセージなし）
                Assert.Empty(logger.Messages);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public async Task RunAsync_HelpFlag_OutputContainsPrintConfigTipSection()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--help" });

                Assert.Equal(0, exitCode);
                var output = sw.ToString();
                // The help text must include a "Tip:" section promoting --print-config
                // ヘルプテキストには --print-config を紹介する「Tip:」セクションが含まれなければならない
                Assert.Contains("Tip:", output, StringComparison.Ordinal);
                Assert.Contains("--print-config", output, StringComparison.Ordinal);
                Assert.Contains("effective configuration", output, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public async Task RunAsync_ConfigError_WritesPrintConfigHintToStderr()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-runner-stderr-hint-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origErr = Console.Error;
            using var errSw = new System.IO.StringWriter();
            Console.SetError(errSw);

            try
            {
                await WithMissingConfigFileAsync(async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { oldDir, newDir, "report_" + Guid.NewGuid().ToString("N"), "--no-pause" });

                    Assert.Equal(3, exitCode);
                    var stderrOutput = errSw.ToString();
                    // On configuration error, stderr should include the --print-config tip
                    // 設定エラー時、stderr に --print-config のヒントが含まれなければならない
                    Assert.Contains("--print-config", stderrOutput, StringComparison.Ordinal);
                });
            }
            finally
            {
                Console.SetError(origErr);
                TryDeleteDirectory(tempRoot);
            }
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public async Task RunAsync_HelpFlagVariants_AllExitZero(string flag)
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { flag });
                Assert.Equal(0, exitCode);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public async Task RunAsync_VersionFlag_ExitsZeroWithVersionOutput()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--version" });

                Assert.Equal(0, exitCode);
                var output = sw.ToString().Trim();
                Assert.False(string.IsNullOrWhiteSpace(output), "Version output should not be empty.");
                // Logger should NOT have been initialized
                // ロガーは初期化されていないはず
                Assert.Empty(logger.Messages);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        // -----------------------------------------------------------------------
        // Unknown flag -> exit code 2 (InvalidArguments)
        // 不明フラグ -> 終了コード 2（InvalidArguments）
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_UnknownFlag_ReturnsInvalidArgumentsExitCode()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-runner-unknown-flag-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());

            try
            {
                await WithConfigFileAsync("{}", async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { oldDir, newDir, "lbl_unknown_" + Guid.NewGuid().ToString("N"), "--unknown-flag", "--no-pause" });

                    Assert.Equal(2, exitCode);
                });
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }
    }
}
