// ProgramRunnerTests.HelpVersion.cs — Help, version, and config flag early-exit tests (partial 2/4)
// ProgramRunnerTests.HelpVersion.cs — ヘルプ・バージョン・設定フラグの早期終了テスト（パーシャル 2/4）

using System;
using System.IO;
using System.Reflection;
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
        public async Task RunAsync_HelpFlag_OutputContainsDryRunOption()
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
                Assert.Contains("--dry-run", output, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public async Task RunAsync_HelpFlag_OutputContainsClearCacheOption()
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
                Assert.Contains("--clear-cache", output, StringComparison.Ordinal);
                Assert.Contains("--creator", output, StringComparison.Ordinal);
                Assert.Contains("--creator-il-ignore-profile", output, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public async Task RunAsync_DryRunFlag_ExitsZeroWithPreviewOutput()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-dryrun-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            // Create sample files / テスト用ファイルを作成
            File.WriteAllText(Path.Combine(oldDir, "file1.dll"), "old-dll-content");
            File.WriteAllText(Path.Combine(oldDir, "file2.txt"), "old-txt-content");
            File.WriteAllText(Path.Combine(newDir, "file1.dll"), "new-dll-content");
            File.WriteAllText(Path.Combine(newDir, "file3.xml"), "new-xml-content");
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                await WithConfigFileAsync("{}", async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { oldDir, newDir, "dryrun_" + Guid.NewGuid().ToString("N"), "--dry-run", "--no-pause" });

                    Assert.Equal(0, exitCode);
                    var output = sw.ToString();
                    Assert.Contains("Dry Run Preview", output, StringComparison.Ordinal);
                    Assert.Contains("Old folder", output, StringComparison.Ordinal);
                    Assert.Contains("New folder", output, StringComparison.Ordinal);
                    Assert.Contains("Files in old folder", output, StringComparison.Ordinal);
                    Assert.Contains("Union", output, StringComparison.Ordinal);
                });
            }
            finally
            {
                Console.SetOut(origOut);
                TryDeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task RunAsync_DryRunFlag_DoesNotCreateReportsDirectory()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "fd-dryrun-noreport-" + Guid.NewGuid().ToString("N"));
            var oldDir = Path.Combine(tempRoot, "old");
            var newDir = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            var reportLabel = "dryrun_noreport_" + Guid.NewGuid().ToString("N");
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                await WithConfigFileAsync("{}", async () =>
                {
                    var exitCode = await runner.RunAsync(new[] { oldDir, newDir, reportLabel, "--dry-run", "--no-pause" });

                    Assert.Equal(0, exitCode);
                    // Reports directory must NOT have been created / Reports ディレクトリが作成されていないこと
                    var reportsDir = Path.Combine(AppContext.BaseDirectory, "Reports", reportLabel);
                    Assert.False(Directory.Exists(reportsDir), "Dry run should not create the Reports directory.");
                });
            }
            finally
            {
                Console.SetOut(origOut);
                TryDeleteDirectory(tempRoot);
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
        // Completion summary chart
        // 完了サマリーチャート
        // -----------------------------------------------------------------------

        [Fact]
        public void OutputCompletionSummaryChart_WritesBarForEachCategory()
        {
            var stateType = typeof(ProgramRunner).GetNestedType("RunCompletionState", BindingFlags.NonPublic);
            Assert.NotNull(stateType);
            var state = Activator.CreateInstance(stateType, new object[] { false, false, false, 100, 20, 5, 30 });

            var method = typeof(ProgramRunner).GetMethod("OutputCompletionSummaryChart", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);
            try
            {
                method.Invoke(null, new[] { state });
                var output = sw.ToString();

                // Verify order: Unchanged, Added, Removed, Modified
                // 順序確認: Unchanged, Added, Removed, Modified
                int idxUnchanged = output.IndexOf("Unchanged", StringComparison.Ordinal);
                int idxAdded = output.IndexOf("Added", StringComparison.Ordinal);
                int idxRemoved = output.IndexOf("Removed", StringComparison.Ordinal);
                int idxModified = output.IndexOf("Modified", StringComparison.Ordinal);
                Assert.True(idxUnchanged < idxAdded, "Unchanged should appear before Added");
                Assert.True(idxAdded < idxRemoved, "Added should appear before Removed");
                Assert.True(idxRemoved < idxModified, "Removed should appear before Modified");

                // Verify counts appear / 件数が出力されること
                Assert.Contains("100", output, StringComparison.Ordinal);
                Assert.Contains("20", output, StringComparison.Ordinal);
                Assert.Contains("5", output, StringComparison.Ordinal);
                Assert.Contains("30", output, StringComparison.Ordinal);

                // Verify bar characters / バー文字が含まれること
                Assert.Contains("█", output, StringComparison.Ordinal);
                Assert.Contains("░", output, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public void OutputCompletionSummaryChart_ZeroTotal_WritesNothing()
        {
            var stateType = typeof(ProgramRunner).GetNestedType("RunCompletionState", BindingFlags.NonPublic);
            Assert.NotNull(stateType);
            var state = Activator.CreateInstance(stateType, new object[] { false, false, false, 0, 0, 0, 0 });

            var method = typeof(ProgramRunner).GetMethod("OutputCompletionSummaryChart", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);
            try
            {
                method.Invoke(null, new[] { state });
                Assert.Equal(string.Empty, sw.ToString());
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public void OutputCompletionSummaryChart_BarsAreLeftAligned()
        {
            var stateType = typeof(ProgramRunner).GetNestedType("RunCompletionState", BindingFlags.NonPublic);
            Assert.NotNull(stateType);
            var state = Activator.CreateInstance(stateType, new object[] { false, false, false, 50, 10, 5, 35 });

            var method = typeof(ProgramRunner).GetMethod("OutputCompletionSummaryChart", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);
            try
            {
                method.Invoke(null, new[] { state });
                var lines = sw.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                // All bar lines should start at the same column (bar character position)
                // すべてのバー行のバー文字開始位置が揃っていること
                int? firstBarPos = null;
                foreach (var line in lines)
                {
                    int barPos = line.IndexOf('█');
                    if (barPos < 0) barPos = line.IndexOf('░');
                    if (barPos < 0) continue;

                    if (firstBarPos == null)
                        firstBarPos = barPos;
                    else
                        Assert.Equal(firstBarPos.Value, barPos);
                }
                Assert.NotNull(firstBarPos);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        // -----------------------------------------------------------------------
        // --credits early-exit test
        // --credits 早期終了テスト
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_CreditsFlag_ExitsZeroWithCreditsOutput()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--credits" });

                Assert.Equal(0, exitCode);
                var output = sw.ToString();
                Assert.Contains("FolderDiffIL4DotNet Credits", output, StringComparison.Ordinal);
                Assert.Contains("Signal over noise", output, StringComparison.Ordinal);
                Assert.Contains("Core Technology", output, StringComparison.Ordinal);
                Assert.Contains("Open Source Libraries", output, StringComparison.Ordinal);
                Assert.Contains("Special Thanks", output, StringComparison.Ordinal);
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
        // --banner early-exit test
        // --banner 早期終了テスト
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_BannerFlag_ExitsZeroWithBannerOutput()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--banner" });

                Assert.Equal(0, exitCode);
                var output = sw.ToString();
                // The banner contains block characters from the ASCII art
                // バナーには ASCII アートのブロック文字が含まれる
                Assert.Contains("███████", output, StringComparison.Ordinal);
                Assert.Contains("██████╔╝", output, StringComparison.Ordinal);
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
        // --open-reports / --open-config / --open-logs early-exit tests
        // --open-reports / --open-config / --open-logs 早期終了テスト
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_OpenReportsFlag_ExitsZeroAndPrintsPath()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--open-reports" });

                Assert.Equal(0, exitCode);
                var output = sw.ToString();
                Assert.Contains("Opening folder:", output, StringComparison.Ordinal);
                Assert.Contains("Reports", output, StringComparison.Ordinal);
                // Logger should NOT have been initialized / ロガーは初期化されていないはず
                Assert.Empty(logger.Messages);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public async Task RunAsync_OpenConfigFlag_ExitsZeroAndPrintsPath()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--open-config" });

                Assert.Equal(0, exitCode);
                var output = sw.ToString();
                Assert.Contains("Opening folder:", output, StringComparison.Ordinal);
                // Logger should NOT have been initialized / ロガーは初期化されていないはず
                Assert.Empty(logger.Messages);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public async Task RunAsync_OpenLogsFlag_ExitsZeroAndPrintsPath()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--open-logs" });

                Assert.Equal(0, exitCode);
                var output = sw.ToString();
                Assert.Contains("Opening folder:", output, StringComparison.Ordinal);
                Assert.Contains("Logs", output, StringComparison.Ordinal);
                // Logger should NOT have been initialized / ロガーは初期化されていないはず
                Assert.Empty(logger.Messages);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        [Fact]
        public async Task RunAsync_OpenReportsWithOutput_OpensCustomDirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-open-reports-" + Guid.NewGuid().ToString("N"));
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--open-reports", "--output", tempDir });

                Assert.Equal(0, exitCode);
                var output = sw.ToString();
                // Should print the custom path, not the default Reports/ path
                // デフォルトの Reports/ パスではなく、カスタムパスが出力されるべき
                Assert.Contains(Path.GetFullPath(tempDir), output, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetOut(origOut);
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task RunAsync_OpenConfigWithConfigPath_OpensConfigParentDirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-open-config-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var configPath = Path.Combine(tempDir, "config.json");
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--open-config", "--config", configPath });

                Assert.Equal(0, exitCode);
                var output = sw.ToString();
                // Should open the parent directory of the config file
                // config ファイルの親ディレクトリを開くべき
                Assert.Contains(Path.GetFullPath(tempDir), output, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetOut(origOut);
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task RunAsync_OpenConfigWithRelativeConfigPath_OpensCurrentDirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fd-open-config-rel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            var originalCurrentDirectory = Environment.CurrentDirectory;
            using var sw = new System.IO.StringWriter();
            Console.SetOut(sw);
            Environment.CurrentDirectory = tempDir;

            try
            {
                var exitCode = await runner.RunAsync(new[] { "--open-config", "--config", "custom.json" });

                Assert.Equal(0, exitCode);
                var output = sw.ToString();
                Assert.Contains(Path.GetFullPath(tempDir), output, StringComparison.Ordinal);
                Assert.DoesNotContain(Path.Combine(tempDir, "custom.json"), output, StringComparison.Ordinal);
            }
            finally
            {
                Environment.CurrentDirectory = originalCurrentDirectory;
                Console.SetOut(origOut);
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task RunAsync_HelpFlag_OutputContainsOpenFolderOptions()
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
                Assert.Contains("--open-reports", output, StringComparison.Ordinal);
                Assert.Contains("--open-config", output, StringComparison.Ordinal);
                Assert.Contains("--open-logs", output, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }

        // -----------------------------------------------------------------------
        // --wizard with redirected stdin -> exit code 2
        // --wizard でリダイレクト stdin → 終了コード 2
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_WizardFlag_WithRedirectedStdin_ExitsWithInvalidArguments()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            var origErr = Console.Error;
            using var swOut = new System.IO.StringWriter();
            using var swErr = new System.IO.StringWriter();
            Console.SetOut(swOut);
            Console.SetError(swErr);

            try
            {
                // In test environment, stdin is always redirected, so wizard should refuse to run.
                // テスト環境では stdin は常にリダイレクトされているため、ウィザードは実行を拒否する。
                var exitCode = await runner.RunAsync(new[] { "--wizard" });

                Assert.Equal(2, exitCode);
                var errOutput = swErr.ToString();
                Assert.Contains("--wizard requires an interactive terminal", errOutput, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetOut(origOut);
                Console.SetError(origErr);
            }
        }

        // -----------------------------------------------------------------------
        // --clear-cache with redirected stdin -> exit code 2
        // --clear-cache でリダイレクト stdin → 終了コード 2
        // -----------------------------------------------------------------------

        [Fact]
        public async Task RunAsync_ClearCacheFlag_WithRedirectedStdin_ExitsWithInvalidArguments()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var runner = new ProgramRunner(logger, new ConfigService());
            var origOut = Console.Out;
            var origErr = Console.Error;
            using var swOut = new System.IO.StringWriter();
            using var swErr = new System.IO.StringWriter();
            Console.SetOut(swOut);
            Console.SetError(swErr);

            try
            {
                // In test environment, stdin is always redirected, so --clear-cache should refuse to run.
                // テスト環境では stdin は常にリダイレクトされているため、--clear-cache は実行を拒否する。
                var exitCode = await runner.RunAsync(new[] { "--clear-cache" });

                Assert.Equal(2, exitCode);
                var errOutput = swErr.ToString();
                Assert.Contains("--clear-cache requires an interactive terminal", errOutput, StringComparison.Ordinal);
            }
            finally
            {
                Console.SetOut(origOut);
                Console.SetError(origErr);
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
