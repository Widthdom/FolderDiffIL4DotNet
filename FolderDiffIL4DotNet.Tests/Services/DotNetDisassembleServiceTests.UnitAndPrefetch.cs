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

        [Fact]
        [Trait("Category", "Unit")]
        public void BuildArgSets_IlspyCommand_IncludesOutputFileArguments()
        {
            // Tests that ilspycmd arg sets include the -o flag with temp output path.
            // ilspycmd の引数セットに -o フラグと一時出力パスが含まれることをテスト。
            var method = typeof(DotNetDisassembleService).GetMethod("BuildArgSets", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var argSetsObject = method.Invoke(null, ["ilspycmd", "/tmp/sample.dll", null]);
            var argSets = Assert.IsAssignableFrom<System.Collections.IEnumerable>(argSetsObject);

            bool hasOutputArg = false;
            foreach (var item in argSets)
            {
                var itemType = item.GetType();
                var argsField = itemType.GetField("Item2");
                Assert.NotNull(argsField);
                var args = Assert.IsType<string[]>(argsField.GetValue(item));
                if (Array.IndexOf(args, "-o") >= 0)
                {
                    hasOutputArg = true;
                }
            }

            Assert.True(hasOutputArg, "ilspycmd arg sets should include -o output flag");
        }
    }
}
