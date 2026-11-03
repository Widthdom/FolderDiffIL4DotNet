/// <summary>
/// Partial class containing unit tests for version/label helpers and IL cache prefetch scenarios.
/// バージョン/ラベルヘルパーおよび IL キャッシュプリフェッチシナリオのユニットテストを含むパーシャルクラス。
/// </summary>

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed partial class DotNetDisassembleServiceTests
    {
        [Fact]
        public async Task DisassembleAsync_WhenVersionLookupFails_UsesFingerprintAndAvoidsCrossVersionCacheMix()
        {
            var binDir = Path.Combine(_rootDir, "bin3");
            Directory.CreateDirectory(binDir);
            var cacheDir = Path.Combine(_rootDir, "ilcache");
            var counterPath = Path.Combine(_rootDir, "dotnet_ildasm_counter.txt");

            // First tool: version fails (exit 2), disassembly succeeds with counter
            InstallFakeTool(binDir, "dotnet-ildasm", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "VERSION_EXIT", "2");
                Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "");
                Environment.SetEnvironmentVariable(prefix + "OUTPUT", "IL_FROM_VERSION_FAILING_TOOL");
                Environment.SetEnvironmentVariable(prefix + "COUNTER_PATH", counterPath);
            });
            InstallFakeTool(binDir, "dotnet", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "EXIT", "1");
            });
            InstallFakeTool(binDir, "ilspycmd", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "EXIT", "1");
            });

            var oldPath = Environment.GetEnvironmentVariable("PATH");
            var oldHome = Environment.GetEnvironmentVariable("HOME");
            try
            {
                Environment.SetEnvironmentVariable("PATH", binDir + Path.PathSeparator + oldPath);
                Environment.SetEnvironmentVariable("HOME", _rootDir);

                var dllPath = Path.Combine(_rootDir, "cache-target.dll");
                await File.WriteAllTextAsync(dllPath, "dummy");

                var config = CreateConfig(enableIlCache: true);
                var service1 = CreateService(config, new ILCache(cacheDir, _logger));
                var (_, command1) = await service1.DisassembleAsync(dllPath);
                var countAfterFirstRun = File.Exists(counterPath) ? await CountLinesAsync(counterPath) : 0;
                Assert.Contains("fingerprint:", command1, StringComparison.OrdinalIgnoreCase);
                Assert.True(countAfterFirstRun > 0);

                // Simulate tool binary update by touching mtime to change fingerprint
                // ツールバイナリの更新をシミュレートし、mtime 変更でフィンガープリントを変える
                await Task.Delay(1100);
                var binaryPath = GetInstalledFakeBinaryPath(binDir, "dotnet-ildasm");
                File.SetLastWriteTimeUtc(binaryPath, DateTime.UtcNow);

                var service2 = CreateService(config, new ILCache(cacheDir, _logger));
                var (_, command2) = await service2.DisassembleAsync(dllPath);
                var countAfterSecondRun = File.Exists(counterPath) ? await CountLinesAsync(counterPath) : 0;

                Assert.Contains("fingerprint:", command2, StringComparison.OrdinalIgnoreCase);
                Assert.NotEqual(command1, command2);
                Assert.True(countAfterSecondRun > countAfterFirstRun);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", oldPath);
                Environment.SetEnvironmentVariable("HOME", oldHome);
                ClearFakeToolEnvVars("dotnet-ildasm");
                ClearFakeToolEnvVars("dotnet");
                ClearFakeToolEnvVars("ilspycmd");
            }
        }

        [Fact]
        public async Task PrefetchIlCacheAsync_NullInput_ReturnsWithoutThrowing()
        {
            var service = CreateService(CreateConfig(enableIlCache: true), new ILCache(Path.Combine(_rootDir, "prefetch-null"), _logger));
            await service.PrefetchIlCacheAsync(null, maxParallel: 1);
            Assert.Equal(0, service.IlCacheHits);
        }

        [Fact]
        public async Task PrefetchIlCacheAsync_InvalidMaxParallel_Throws()
        {
            var service = CreateService(CreateConfig(enableIlCache: true), new ILCache(Path.Combine(_rootDir, "prefetch-invalid"), _logger));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.PrefetchIlCacheAsync(new[] { "dummy.dll" }, maxParallel: 0));
        }

        [Fact]
        public async Task PrefetchIlCacheAsync_WhenSeededCacheExists_IncrementsHitCounter()
        {
            var binDir = Path.Combine(_rootDir, "prefetch-hit-bin");
            Directory.CreateDirectory(binDir);

            // dotnet-ildasm: version succeeds but disassembly fails
            // dotnet-ildasm: バージョン取得は成功するが逆アセンブルは失敗
            InstallFakeTool(binDir, "dotnet-ildasm", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "1.2.3");
                Environment.SetEnvironmentVariable(prefix + "EXIT", "1");
            });
            InstallFakeTool(binDir, "dotnet", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "EXIT", "1");
            });
            InstallFakeTool(binDir, "ilspycmd", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "EXIT", "1");
            });

            var oldPath = Environment.GetEnvironmentVariable("PATH");
            var oldHome = Environment.GetEnvironmentVariable("HOME");
            try
            {
                Environment.SetEnvironmentVariable("PATH", binDir + Path.PathSeparator + oldPath);
                Environment.SetEnvironmentVariable("HOME", _rootDir);

                var cacheDir = Path.Combine(_rootDir, "prefetch-hit-cache");
                var ilCache = new ILCache(cacheDir, _logger);
                var service = CreateService(CreateConfig(enableIlCache: true), ilCache);

                var assemblyPath = Path.Combine(_rootDir, "prefetch-target.dll");
                await File.WriteAllTextAsync(assemblyPath, "dummy");

                const string version = "1.2.3";
                SeedDisassemblerVersionCache(Constants.DOTNET_ILDASM, version);
                SeedDisassemblerVersionCache($"{Constants.DOTNET_MUXER} {Constants.ILDASM_LABEL}", version);
                SeedDisassemblerVersionCache(Constants.ILSPY_CMD, version);

                var label = $"{Constants.DOTNET_ILDASM} {Path.GetFileName(assemblyPath)} (version: {version})";
                await ilCache.SetILAsync(assemblyPath, label, "CACHED_IL");

                await service.PrefetchIlCacheAsync(new[] { assemblyPath }, maxParallel: 1);

                Assert.True(service.IlCacheHits >= 1);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", oldPath);
                Environment.SetEnvironmentVariable("HOME", oldHome);
                ClearFakeToolEnvVars("dotnet-ildasm");
                ClearFakeToolEnvVars("dotnet");
                ClearFakeToolEnvVars("ilspycmd");
            }
        }

        [Fact]
        public async Task TryCacheHitAsync_WhenCacheLookupThrowsRecoverableException_LogsWarningWithExceptionType()
        {
            var logger = new TestLogger(logFileAbsolutePath: "test.log");
            var ilCache = new ILCache(Path.Combine(_rootDir, "cache-hit-failure"), logger);
            var service = new DotNetDisassembleService(CreateConfig(enableIlCache: true), ilCache, _resultLists, logger, _dotNetDisassemblerCache);
            const string version = "9.1.0";
            SeedDisassemblerVersionCache(Constants.ILSPY_CMD, version);

            var method = typeof(DotNetDisassembleService).GetMethod("TryCacheHitAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var invalidPath = Path.Combine(_rootDir, "cache-target-dir");
            Directory.CreateDirectory(invalidPath);
            var task = Assert.IsAssignableFrom<Task<(bool Hit, string? IlText, string? Label)>>(
                method.Invoke(service, ["ilspycmd", invalidPath, new[] { "-il", "sample.dll" }, false]));

            var result = await task;

            Assert.False(result.Hit);
            Assert.Null(result.IlText);
            Assert.Null(result.Label);
            var warning = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Assert.Contains("Failed to get IL from cache", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"assembly '{invalidPath}'", warning.Message, StringComparison.Ordinal);
            Assert.Contains("command 'ilspycmd'", warning.Message, StringComparison.Ordinal);
            Assert.Contains("AssemblyIsPathRooted=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains("AssemblyLooksPathLike=True", warning.Message, StringComparison.Ordinal);
            Assert.Contains("CommandIsPathRooted=False", warning.Message, StringComparison.Ordinal);
            Assert.Contains("CommandLooksPathLike=False", warning.Message, StringComparison.Ordinal);
            Assert.True(warning.Exception is IOException or UnauthorizedAccessException);
            Assert.Contains(warning.Exception!.GetType().Name, warning.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("dotnet-ildasm sample.dll (version: dotnet ildasm 0.12.0)", "dotnet ildasm 0.12.0")]
        [InlineData("ilspycmd sample.dll (version: ilspycmd 9.1.0)", "ilspycmd 9.1.0")]
        [InlineData("dotnet-ildasm sample.dll", null)]
        [InlineData("dotnet-ildasm (version: )", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        [Trait("Category", "Unit")]
        public void ExtractVersionFromLabel_VariousLabels_ReturnsExpectedVersion(string? label, string? expected)
        {
            // Tests the ExtractVersionFromLabel helper for various branch paths.
            // ExtractVersionFromLabel ヘルパーの各分岐パスをテスト。
            var method = typeof(DotNetDisassembleService).GetMethod("ExtractVersionFromLabel", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var result = method.Invoke(null, [label]) as string;
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("dotnet-ildasm", "dotnet-ildasm")]
        [InlineData("dotnet", "dotnet-ildasm")]
        [InlineData("ilspycmd", "ilspycmd")]
        [InlineData("ildasm", "ildasm")]
        [InlineData("", "")]
        [InlineData(null, null)]
        [Trait("Category", "Unit")]
        public void NormalizeDisassemblerName_VariousCommands_ReturnsExpectedToolName(string? command, string? expected)
        {
            // Tests the NormalizeDisassemblerName helper for various tool name branches.
            // NormalizeDisassemblerName ヘルパーの各ツール名分岐をテスト。
            var method = typeof(DotNetDisassembleService).GetMethod("NormalizeDisassemblerName", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var result = method.Invoke(null, [command]) as string;
            Assert.Equal(expected, result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AreSameDisassemblerVersion_MatchingVersions_ReturnsTrue()
        {
            // Tests that two labels with the same version are considered equal.
            // 同じバージョンの2つのラベルが等しいと判定されることをテスト。
            var method = typeof(DotNetDisassembleService).GetMethod("AreSameDisassemblerVersion", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var result = (bool)method.Invoke(null, ["tool sample.dll (version: 1.0.0)", "tool other.dll (version: 1.0.0)"])!;
            Assert.True(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AreSameDisassemblerVersion_DifferentVersions_ReturnsFalse()
        {
            // Tests that two labels with different versions are not considered equal.
            // 異なるバージョンの2つのラベルが等しくないと判定されることをテスト。
            var method = typeof(DotNetDisassembleService).GetMethod("AreSameDisassemblerVersion", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var result = (bool)method.Invoke(null, ["tool sample.dll (version: 1.0.0)", "tool other.dll (version: 2.0.0)"])!;
            Assert.False(result);
        }

        /// <summary>
        /// ilspycmd treats <c>-o</c> as an output directory, so each <c>-o</c> value is a not-yet-created <c>ilspy_out_*</c> directory path that the arg set also tracks for reading and cleanup.
        /// ilspycmd は <c>-o</c> を出力ディレクトリとして扱うため、各 <c>-o</c> の値が未作成の <c>ilspy_out_*</c> ディレクトリパスであり、読み取りと削除のため引数セットでも保持されていることを検証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void BuildArgSets_IlspyCommand_PassesTemporaryOutputDirectoryToOutputFlag()
        {
            var method = typeof(DotNetDisassembleService).GetMethod("BuildArgSets", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var argSetsObject = method.Invoke(null, ["ilspycmd", "/tmp/sample.dll", null]);
            var argSets = Assert.IsAssignableFrom<System.Collections.IEnumerable>(argSetsObject);

            int outputArgSetCount = 0;
            foreach (var item in argSets)
            {
                var itemType = item.GetType();
                var args = Assert.IsType<string[]>(itemType.GetField("Item2")!.GetValue(item));
                var tempOutDirectory = itemType.GetField("Item3")!.GetValue(item) as string;
                var outputFlagIndex = Array.IndexOf(args, "-o");
                if (outputFlagIndex < 0)
                {
                    Assert.Null(tempOutDirectory);
                    continue;
                }

                outputArgSetCount++;
                Assert.Equal(tempOutDirectory, args[outputFlagIndex + 1]);
                Assert.StartsWith("ilspy_out_", Path.GetFileName(tempOutDirectory), StringComparison.Ordinal);
                Assert.Equal(string.Empty, Path.GetExtension(tempOutDirectory));
                Assert.False(Directory.Exists(tempOutDirectory));
            }

            Assert.Equal(2, outputArgSetCount);
        }
    }
}
