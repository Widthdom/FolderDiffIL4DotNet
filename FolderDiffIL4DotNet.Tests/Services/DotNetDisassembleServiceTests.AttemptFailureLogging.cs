/// <summary>
/// Partial class covering how failed disassembly attempts are logged before the next fallback is tried.
/// 次のフォールバックへ進む前に、失敗した逆アセンブル試行がどのようにログ出力されるかを検証するパーシャルクラス。
/// </summary>

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed partial class DotNetDisassembleServiceTests
    {
        /// <summary>
        /// A timed-out attempt is logged as a console warning that names the file and keeps the <c>DisassemblerTimeoutSeconds</c> guidance, and disassembly falls back to ilspycmd.
        /// タイムアウトした試行が、ファイル名と <c>DisassemblerTimeoutSeconds</c> の案内を含むコンソール警告として記録され、逆アセンブルが ilspycmd へフォールバックすることを検証します。
        /// </summary>
        [Fact]
        public async Task DisassemblePairAsLinesWithSameDisassemblerAsync_WhenAttemptTimesOut_LogsTimeoutGuidanceToConsoleAndFallsBack()
        {
            var logger = new TestLogger();
            await RunWithFakeToolsAsync("timeout-guidance", binDir =>
            {
                // dotnet-ildasm sleeps past the 2-second timeout; the margin keeps the working ilspycmd fake below it on slow runners.
                // dotnet-ildasm は 2 秒のタイムアウトを超えてスリープする。余裕を持たせ、遅いランナーでも成功側の ilspycmd 偽ツールは超えないようにする。
                InstallFakeTool(binDir, "dotnet-ildasm", prefix =>
                {
                    Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "dotnet ildasm 0.12.0");
                    Environment.SetEnvironmentVariable(prefix + "SLEEP_MS", "20000");
                });
                InstallFakeTool(binDir, "dotnet", prefix => Environment.SetEnvironmentVariable(prefix + "EXIT", "1"));
                InstallFakeTool(binDir, "ilspycmd", prefix =>
                {
                    Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "ilspycmd 9.1.0");
                    Environment.SetEnvironmentVariable(prefix + "OUTPUT", "IL_FROM_ILSPY");
                });
            }, async binDir =>
            {
                var config = new ConfigSettingsBuilder
                {
                    EnableILCache = false,
                    IgnoredExtensions = new(),
                    TextFileExtensions = new(),
                    DisassemblerTimeoutSeconds = 2
                }.Build();
                var service = new DotNetDisassembleService(config, ilCache: null, _resultLists, logger, _dotNetDisassemblerCache);
                // The dotnet muxer is routed to the same sleeping fake; disable it up front to keep the test to two timeouts.
                // dotnet muxer は同じスリープする偽ツールへルーティングされるため、事前に無効化してタイムアウトを 2 回に抑える。
                var blacklist = (DisassemblerBlacklist)typeof(DotNetDisassembleService)
                    .GetField("_blacklist", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(service)!;
                blacklist.InjectEntry(Constants.DOTNET_MUXER, failCount: 3, lastFailUtc: DateTime.UtcNow);
                var oldDll = Path.Combine(_rootDir, "timeout-old.dll");
                var newDll = Path.Combine(_rootDir, "timeout-new.dll");
                await File.WriteAllTextAsync(oldDll, "dummy-old");
                await File.WriteAllTextAsync(newDll, "dummy-new");

                var (oldLines, oldCommand, _, _) = await service.DisassemblePairAsLinesWithSameDisassemblerAsync(oldDll, newDll);

                Assert.Equal(new[] { "IL_FROM_ILSPY" }, oldLines);
                Assert.Contains("ilspycmd", oldCommand, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning
                    && entry.ShouldOutputMessageToConsole
                    && entry.Message.Contains("Disassembler attempt failed", StringComparison.Ordinal)
                    && entry.Message.Contains(oldDll, StringComparison.Ordinal)
                    && entry.Message.Contains(nameof(TimeoutException), StringComparison.Ordinal)
                    && entry.Message.Contains("timed out after 2 seconds", StringComparison.Ordinal)
                    && entry.Message.Contains("DisassemblerTimeoutSeconds", StringComparison.Ordinal));
            });
        }

        /// <summary>
        /// Non-zero exits are routine fallback steps: each attempt is logged with its file and stderr to the log file only, never to the console, and disassembly falls back to ilspycmd.
        /// 非 0 終了はフォールバックの通常過程であり、各試行がファイル名と stderr 付きでログファイルにのみ記録されてコンソールには出ず、逆アセンブルが ilspycmd へフォールバックすることを検証します。
        /// </summary>
        [Fact]
        public async Task DisassembleAsync_WhenAttemptExitsNonZero_LogsFailureToFileOnlyAndFallsBack()
        {
            var logger = new TestLogger();
            await RunWithFakeToolsAsync("nonzero-file-only", binDir =>
            {
                InstallFakeTool(binDir, "dotnet-ildasm", prefix =>
                {
                    Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "dotnet ildasm 0.12.0");
                    Environment.SetEnvironmentVariable(prefix + "EXIT", "91");
                    Environment.SetEnvironmentVariable(prefix + "STDERR", "NONZERO_STDERR_DETAIL");
                });
                InstallFakeTool(binDir, "dotnet", prefix => Environment.SetEnvironmentVariable(prefix + "EXIT", "1"));
                InstallFakeTool(binDir, "ilspycmd", prefix =>
                {
                    Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "ilspycmd 9.1.0");
                    Environment.SetEnvironmentVariable(prefix + "OUTPUT", "IL_FROM_ILSPY");
                });
            }, async binDir =>
            {
                var service = new DotNetDisassembleService(CreateConfig(enableIlCache: false), ilCache: null, _resultLists, logger, _dotNetDisassemblerCache);
                var dllPath = Path.Combine(_rootDir, "nonzero-file-only.dll");
                await File.WriteAllTextAsync(dllPath, "dummy");

                var (ilText, _) = await service.DisassembleAsync(dllPath);

                Assert.Equal("IL_FROM_ILSPY", ilText.Trim());
                // Every non-zero attempt (file-name and absolute-path argument sets, plus the dotnet muxer routed to the same fake) is logged to the file only.
                // 非 0 終了の各試行（ファイル名・絶対パスの引数セットと、同じ偽ツールへルーティングされる dotnet muxer）はファイルのみに記録される。
                var nonZeroEntries = logger.Entries.Where(entry => entry.Message.Contains("(exit 91)", StringComparison.Ordinal)).ToList();
                Assert.NotEmpty(nonZeroEntries);
                Assert.All(nonZeroEntries, entry =>
                {
                    Assert.Equal(AppLogLevel.Warning, entry.LogLevel);
                    Assert.False(entry.ShouldOutputMessageToConsole);
                    Assert.Contains("Disassembler attempt failed", entry.Message, StringComparison.Ordinal);
                    Assert.Contains(dllPath, entry.Message, StringComparison.Ordinal);
                    Assert.Contains("NONZERO_STDERR_DETAIL", entry.Message, StringComparison.Ordinal);
                });
                Assert.DoesNotContain(logger.Entries, entry => entry.ShouldOutputMessageToConsole
                    && entry.Message.Contains("Disassembler attempt failed", StringComparison.Ordinal));
            });
        }
    }
}
