/// <summary>
/// Partial class covering ilspycmd <c>-o</c> output-directory handling.
/// ilspycmd の <c>-o</c> 出力ディレクトリ処理を検証するパーシャルクラス。
/// </summary>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed partial class DotNetDisassembleServiceTests
    {
        /// <summary>
        /// When only ilspycmd's <c>-o</c> mode works, <see cref="DotNetDisassembleService.DisassembleAsync"/> returns the IL written into the output directory and removes that directory.
        /// ilspycmd の <c>-o</c> モードだけが成功する場合、<see cref="DotNetDisassembleService.DisassembleAsync"/> は出力ディレクトリに書き出された IL を返し、そのディレクトリを削除することを検証します。
        /// </summary>
        [Fact]
        public async Task DisassembleAsync_WhenIlspyStdoutModeFails_ReadsIlFromOutputDirectoryAndRemovesIt()
        {
            await RunWithIlspyOnlyStdoutFailureAsync("ilspy-o-text", async (service, dllPath) =>
            {
                var (ilText, command) = await service.DisassembleAsync(dllPath);

                Assert.Equal("IL_FROM_ILSPY_OUTPUT_DIRECTORY", ilText.Trim());
                Assert.Contains("ilspycmd", command, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(" -o ", command, StringComparison.Ordinal);
            });
        }

        /// <summary>
        /// Same as the text variant, for the line-based pair API used by IL comparison: both sides read IL from the output directory and no directory is left behind.
        /// テキスト版と同じ内容を、IL 比較で使う行ベースのペア API で検証します。両側とも出力ディレクトリから IL を読み、ディレクトリは残りません。
        /// </summary>
        [Fact]
        public async Task DisassemblePairAsLinesWithSameDisassemblerAsync_WhenIlspyStdoutModeFails_ReadsIlFromOutputDirectoryAndRemovesIt()
        {
            await RunWithIlspyOnlyStdoutFailureAsync("ilspy-o-lines", async (service, dllPath) =>
            {
                var newDllPath = Path.Combine(Path.GetDirectoryName(dllPath)!, "ilspy-o-lines-new.dll");
                await File.WriteAllTextAsync(newDllPath, "dummy-new");

                var (oldLines, oldCommand, newLines, newCommand) = await service.DisassemblePairAsLinesWithSameDisassemblerAsync(dllPath, newDllPath);

                Assert.Equal(new[] { "IL_FROM_ILSPY_OUTPUT_DIRECTORY" }, oldLines);
                Assert.Equal(new[] { "IL_FROM_ILSPY_OUTPUT_DIRECTORY" }, newLines);
                Assert.Contains(" -o ", oldCommand, StringComparison.Ordinal);
                Assert.Contains(" -o ", newCommand, StringComparison.Ordinal);
            });
        }

        /// <summary>
        /// ilspycmd names the <c>-o</c> output after the input file (<c>Foo.Bar.dll</c> → <c>Foo.Bar.il</c>), so only that file is accepted; a missing directory or a differently named file yields null.
        /// ilspycmd は <c>-o</c> の出力を入力ファイル名から付ける（<c>Foo.Bar.dll</c> → <c>Foo.Bar.il</c>）ため、そのファイルだけを受け付け、ディレクトリが無い場合や別名のファイルしか無い場合は null になることを検証します。
        /// </summary>
        [Fact]
        public void ResolveIlspyOutputFile_ReturnsInputNamedIlFileOnly()
        {
            var method = typeof(DotNetDisassembleService).GetMethod("ResolveIlspyOutputFile", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            string? Resolve(string directory) => method.Invoke(null, [directory, Path.Combine(_rootDir, "Foo.Bar.dll")]) as string;

            Assert.Null(Resolve(Path.Combine(_rootDir, "resolve-missing")));

            var otherNameDirectory = Path.Combine(_rootDir, "resolve-other-name");
            Directory.CreateDirectory(otherNameDirectory);
            File.WriteAllText(Path.Combine(otherNameDirectory, "Other.il"), "x");
            Assert.Null(Resolve(otherNameDirectory));

            var expectedDirectory = Path.Combine(_rootDir, "resolve-expected");
            Directory.CreateDirectory(expectedDirectory);
            File.WriteAllText(Path.Combine(expectedDirectory, "Other.il"), "x");
            File.WriteAllText(Path.Combine(expectedDirectory, "Foo.Bar.il"), "x");
            Assert.Equal(Path.Combine(expectedDirectory, "Foo.Bar.il"), Resolve(expectedDirectory));
        }

        /// <summary>
        /// Installs fake tools where only ilspycmd works, and only with <c>-o</c>; asserts no <c>ilspy_out_*</c> path is left in the temp directory.
        /// ilspycmd だけが <c>-o</c> 付きでのみ成功する偽ツールを配置し、一時ディレクトリに <c>ilspy_out_*</c> が残らないことを検証します。
        /// </summary>
        private async Task RunWithIlspyOnlyStdoutFailureAsync(string scenario, Func<DotNetDisassembleService, string, Task> act)
        {
            var ilspyOutputPathsBefore = SnapshotIlspyOutputPaths();
            await RunWithFakeToolsAsync(scenario, binDir =>
            {
                InstallFakeTool(binDir, "dotnet-ildasm", prefix => Environment.SetEnvironmentVariable(prefix + "EXIT", "1"));
                InstallFakeTool(binDir, "dotnet", prefix => Environment.SetEnvironmentVariable(prefix + "EXIT", "1"));
                InstallFakeTool(binDir, "ilspycmd", prefix =>
                {
                    Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "ilspycmd 9.1.0");
                    Environment.SetEnvironmentVariable(prefix + "OUTPUT", "IL_FROM_ILSPY_OUTPUT_DIRECTORY");
                    Environment.SetEnvironmentVariable(prefix + "STDOUT_EXIT", "70");
                });
            }, async binDir =>
            {
                var service = CreateService(CreateConfig(enableIlCache: false), null);
                var dllPath = Path.Combine(_rootDir, scenario + ".dll");
                await File.WriteAllTextAsync(dllPath, "dummy");

                await act(service, dllPath);

                Assert.Empty(SnapshotIlspyOutputPaths().Except(ilspyOutputPathsBefore));
            });
        }

        /// <summary>
        /// Returns the <c>ilspy_out_*</c> entries currently in the temp directory, for before/after leak checks.
        /// 一時ディレクトリにある <c>ilspy_out_*</c> エントリを返します（前後比較による残留チェック用）。
        /// </summary>
        private static HashSet<string> SnapshotIlspyOutputPaths()
            => new(Directory.EnumerateFileSystemEntries(Path.GetTempPath(), "ilspy_out_*"), StringComparer.Ordinal);
    }
}
