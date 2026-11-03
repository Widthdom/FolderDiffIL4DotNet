/// <summary>
/// Partial class covering disassembler runs that exit 0 without IL and cache entries that contain no IL.
/// 終了コード 0 で IL を出さない逆アセンブラ実行と、IL を含まないキャッシュエントリを検証するパーシャルクラス。
/// </summary>

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed partial class DotNetDisassembleServiceTests
    {
        private const string EMPTY_IL_STDERR = "EMPTY_IL_STDERR_DETAIL";
        private const string NO_IL_WARNING_FRAGMENT = "exited with code 0 but produced no IL";

        /// <summary>
        /// A primary tool that exits 0 without IL is treated as a failure: a console warning with its stderr is logged and <see cref="DotNetDisassembleService.DisassembleAsync"/> falls back to ilspycmd.
        /// IL を出さずに終了コード 0 で終わる優先ツールは失敗として扱われ、stderr 付きの警告がコンソールに出力されたうえで <see cref="DotNetDisassembleService.DisassembleAsync"/> が ilspycmd へフォールバックすることを検証します。
        /// </summary>
        [Fact]
        public async Task DisassembleAsync_WhenPrimaryToolExitsZeroWithoutIl_LogsStderrAndFallsBackToNextTool()
        {
            var logger = new TestLogger();
            await RunWithFakeToolsAsync("empty-fallback-text", ConfigureEmptyDotnetIldasmAndWorkingIlspy, async binDir =>
            {
                var service = new DotNetDisassembleService(CreateConfig(enableIlCache: false), ilCache: null, _resultLists, logger, _dotNetDisassemblerCache);
                var dllPath = Path.Combine(_rootDir, "empty-fallback-text.dll");
                await File.WriteAllTextAsync(dllPath, "dummy");

                var (ilText, command) = await service.DisassembleAsync(dllPath);

                Assert.Equal("IL_FROM_ILSPY", ilText.Trim());
                Assert.Contains("ilspycmd", command, StringComparison.OrdinalIgnoreCase);
                AssertNoIlWarning(logger, dllPath);
            });
        }

        /// <summary>
        /// Same as the text variant, for the line-based pair API used by IL comparison: both sides fall back to ilspycmd after the warning.
        /// テキスト版と同じ内容を、IL 比較で使う行ベースのペア API で検証します。警告の後、両側とも ilspycmd へフォールバックします。
        /// </summary>
        [Fact]
        public async Task DisassemblePairAsLinesWithSameDisassemblerAsync_WhenPrimaryToolExitsZeroWithoutIl_LogsStderrAndFallsBackToNextTool()
        {
            var logger = new TestLogger();
            await RunWithFakeToolsAsync("empty-fallback-lines", ConfigureEmptyDotnetIldasmAndWorkingIlspy, async binDir =>
            {
                var service = new DotNetDisassembleService(CreateConfig(enableIlCache: false), ilCache: null, _resultLists, logger, _dotNetDisassemblerCache);
                var oldDll = Path.Combine(_rootDir, "empty-fallback-old.dll");
                var newDll = Path.Combine(_rootDir, "empty-fallback-new.dll");
                await File.WriteAllTextAsync(oldDll, "dummy-old");
                await File.WriteAllTextAsync(newDll, "dummy-new");

                var (oldLines, oldCommand, newLines, newCommand) = await service.DisassemblePairAsLinesWithSameDisassemblerAsync(oldDll, newDll);

                Assert.Equal(new[] { "IL_FROM_ILSPY" }, oldLines);
                Assert.Equal(new[] { "IL_FROM_ILSPY" }, newLines);
                Assert.Contains("ilspycmd", oldCommand, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("ilspycmd", newCommand, StringComparison.OrdinalIgnoreCase);
                AssertNoIlWarning(logger, oldDll);
            });
        }

        /// <summary>
        /// When every tool exits 0 without IL, the pair API throws instead of returning empty IL; the ilspycmd <c>-o</c> warning reports the output directory with <c>(no IL file found)</c>, and no <c>ilspy_out_*</c> path is left behind.
        /// すべてのツールが IL を出さずに終了コード 0 で終わる場合、ペア API は空の IL を返さず例外を投げること、ilspycmd の <c>-o</c> の警告が出力ディレクトリと <c>(no IL file found)</c> を報告すること、<c>ilspy_out_*</c> が残らないことを検証します。
        /// </summary>
        [Fact]
        public async Task DisassemblePairAsLinesWithSameDisassemblerAsync_WhenEveryToolExitsZeroWithoutIl_ThrowsInsteadOfReturningEmptyIl()
        {
            var logger = new TestLogger();
            var ilspyOutputPathsBefore = SnapshotIlspyOutputPaths();
            await RunWithFakeToolsAsync("empty-everywhere", binDir =>
            {
                InstallFakeTool(binDir, "dotnet-ildasm", prefix => Environment.SetEnvironmentVariable(prefix + "EMPTY_OUTPUT", "1"));
                InstallFakeTool(binDir, "dotnet", prefix => Environment.SetEnvironmentVariable(prefix + "EXIT", "1"));
                // ilspycmd writes nothing to stdout and creates the -o directory without an IL file.
                // ilspycmd は stdout に何も出さず、-o ディレクトリは作るが IL ファイルは書かない。
                InstallFakeTool(binDir, "ilspycmd", prefix =>
                {
                    Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "ilspycmd 9.1.0");
                    Environment.SetEnvironmentVariable(prefix + "EMPTY_OUTPUT", "1");
                });
            }, async binDir =>
            {
                var service = new DotNetDisassembleService(CreateConfig(enableIlCache: false), ilCache: null, _resultLists, logger, _dotNetDisassemblerCache);
                var oldDll = Path.Combine(_rootDir, "empty-everywhere-old.dll");
                var newDll = Path.Combine(_rootDir, "empty-everywhere-new.dll");
                await File.WriteAllTextAsync(oldDll, "dummy-old");
                await File.WriteAllTextAsync(newDll, "dummy-new");

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.DisassemblePairAsLinesWithSameDisassemblerAsync(oldDll, newDll));

                Assert.Contains(logger.Entries, entry => entry.Message.Contains(NO_IL_WARNING_FRAGMENT, StringComparison.Ordinal)
                    && entry.Message.Contains("OutputDirectory:", StringComparison.Ordinal)
                    && entry.Message.Contains("(no IL file found)", StringComparison.Ordinal)
                    && entry.Message.Contains(" -o ", StringComparison.Ordinal));
                Assert.Empty(SnapshotIlspyOutputPaths().Except(ilspyOutputPathsBefore));
            });
        }

        /// <summary>
        /// An IL cache entry that holds only tool notices is not returned as empty IL: it is removed with a console warning, the IL is regenerated by the tool, and the cache then holds the regenerated IL.
        /// ツール通知行だけを含む IL キャッシュエントリは空の IL として返されず、コンソール警告とともに削除され、ツールで IL が再生成されて、キャッシュには再生成後の IL が入ることを検証します。
        /// </summary>
        [Fact]
        public async Task DisassembleAsync_WhenCachedIlContainsOnlyToolNotices_RemovesEntryAndRegeneratesIl()
        {
            var logger = new TestLogger();
            var counterPath = Path.Combine(_rootDir, "notice-cache-counter.txt");
            await RunWithFakeToolsAsync("notice-cache", binDir =>
            {
                InstallFakeTool(binDir, "dotnet-ildasm", prefix =>
                {
                    Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "1.2.3");
                    Environment.SetEnvironmentVariable(prefix + "OUTPUT", "IL_REGENERATED");
                    Environment.SetEnvironmentVariable(prefix + "COUNTER_PATH", counterPath);
                });
                InstallFakeTool(binDir, "dotnet", prefix => Environment.SetEnvironmentVariable(prefix + "EXIT", "1"));
                InstallFakeTool(binDir, "ilspycmd", prefix => Environment.SetEnvironmentVariable(prefix + "EXIT", "1"));
            }, async binDir =>
            {
                var ilCache = new ILCache(Path.Combine(_rootDir, "notice-cache-dir"), logger);
                var service = new DotNetDisassembleService(CreateConfig(enableIlCache: true), ilCache, _resultLists, logger, _dotNetDisassemblerCache);
                var dllPath = Path.Combine(_rootDir, "notice-cache.dll");
                await File.WriteAllTextAsync(dllPath, "dummy");

                SeedDisassemblerVersionCache(Constants.DOTNET_ILDASM, "1.2.3");
                var label = $"{Constants.DOTNET_ILDASM} {Path.GetFileName(dllPath)} (version: 1.2.3)";
                await ilCache.SetILAsync(dllPath, label,
                    "You are not using the latest version of the tool, please update.\nLatest version is '9.1.0.7988' (yours is '8.2.0.7535').");

                var (ilText, command) = await service.DisassembleAsync(dllPath);

                Assert.Equal("IL_REGENERATED", ilText.Trim());
                Assert.Equal(label, command);
                Assert.True(File.Exists(counterPath));
                Assert.Contains(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning
                    && entry.ShouldOutputMessageToConsole
                    && entry.Message.Contains("Ignored cached IL", StringComparison.Ordinal)
                    && entry.Message.Contains(dllPath, StringComparison.Ordinal));
                Assert.Equal("IL_REGENERATED", (await ilCache.TryGetILAsync(dllPath, label))?.Trim());
            });
        }

        /// <summary>
        /// <see cref="ILCache.RemoveIL"/> removes both the memory entry and the disk entry, so neither the same instance nor a new instance over the same directory finds it.
        /// <see cref="ILCache.RemoveIL"/> がメモリとディスクの両方のエントリを削除し、同じインスタンスでも同じディレクトリを使う新しいインスタンスでも見つからなくなることを検証します。
        /// </summary>
        [Fact]
        public async Task ILCacheRemoveIL_RemovesMemoryAndDiskEntries()
        {
            var cacheDir = Path.Combine(_rootDir, "remove-il-cache");
            var dllPath = Path.Combine(_rootDir, "remove-il.dll");
            await File.WriteAllTextAsync(dllPath, "dummy");
            var ilCache = new ILCache(cacheDir, _logger);
            await ilCache.SetILAsync(dllPath, "tool (version: 1)", "IL");

            ilCache.RemoveIL(dllPath, "tool (version: 1)");

            Assert.Null(await ilCache.TryGetILAsync(dllPath, "tool (version: 1)"));
            Assert.Null(await new ILCache(cacheDir, _logger).TryGetILAsync(dllPath, "tool (version: 1)"));
        }

        /// <summary>
        /// Both <c>ContainsIlContent</c> overloads (string and lines) return true only when some line is neither blank nor a known tool notice.
        /// <c>ContainsIlContent</c> の両オーバーロード（文字列版と行リスト版）が、空白行でも既知のツール通知行でもない行がある場合にだけ true を返すことを検証します。
        /// </summary>
        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData(" \r\n\t\n", false)]
        [InlineData("You are not using the latest version of the tool, please update.\r\nLatest version is '9.1.0' (yours is '8.2.0').\n", false)]
        [InlineData("You are not using the latest version of the tool, please update.\n  IL_0000: ret", true)]
        [InlineData("// IL code: Sample", true)]
        [InlineData("Latest version is a word without the version fragment", true)]
        public void ContainsIlContent_DetectsAnyLineThatIsNeitherBlankNorToolNotice(string? text, bool expected)
        {
            Assert.Equal(expected, DotNetDisassembleService.ContainsIlContent(text));
            var lines = DotNetDisassembleService.SplitToLines(text ?? string.Empty);
            Assert.Equal(expected, DotNetDisassembleService.ContainsIlContent(lines));
        }

        /// <summary>
        /// Installs a dotnet-ildasm fake that exits 0 with only stderr output, a failing dotnet muxer, and a working ilspycmd fake.
        /// stderr だけを出して終了コード 0 で終わる dotnet-ildasm、失敗する dotnet muxer、成功する ilspycmd の偽ツールを配置します。
        /// </summary>
        private static void ConfigureEmptyDotnetIldasmAndWorkingIlspy(string binDir)
        {
            InstallFakeTool(binDir, "dotnet-ildasm", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "dotnet ildasm 0.12.0");
                Environment.SetEnvironmentVariable(prefix + "EMPTY_OUTPUT", "1");
                Environment.SetEnvironmentVariable(prefix + "STDERR", EMPTY_IL_STDERR);
            });
            InstallFakeTool(binDir, "dotnet", prefix => Environment.SetEnvironmentVariable(prefix + "EXIT", "1"));
            InstallFakeTool(binDir, "ilspycmd", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "ilspycmd 9.1.0");
                Environment.SetEnvironmentVariable(prefix + "OUTPUT", "IL_FROM_ILSPY");
            });
        }

        /// <summary>
        /// Asserts that a console warning reported the dotnet-ildasm run for <paramref name="dllPath"/> as exiting 0 without IL, including its stderr.
        /// <paramref name="dllPath"/> に対する dotnet-ildasm の実行が「終了コード 0 で IL 無し」として、stderr 付きでコンソール警告されたことを検証します。
        /// </summary>
        private static void AssertNoIlWarning(TestLogger logger, string dllPath)
        {
            Assert.Contains(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning
                && entry.ShouldOutputMessageToConsole
                && entry.Message.Contains(NO_IL_WARNING_FRAGMENT, StringComparison.Ordinal)
                && entry.Message.Contains("dotnet-ildasm", StringComparison.Ordinal)
                && entry.Message.Contains(dllPath, StringComparison.Ordinal)
                && entry.Message.Contains(EMPTY_IL_STDERR, StringComparison.Ordinal));
        }
    }
}
