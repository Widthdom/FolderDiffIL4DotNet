using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class DotNetDisassembleServiceTests : IDisposable
    {
        private readonly string _rootDir;
        private readonly FileDiffResultLists _resultLists = new();
        private readonly ILoggerService _logger = new LoggerService();
        private readonly DotNetDisassemblerCache _dotNetDisassemblerCache;

        public DotNetDisassembleServiceTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-disasm-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
            _dotNetDisassemblerCache = new DotNetDisassemblerCache(_logger);
            ResetDisassemblerFailureState();
            ResetDisassemblerVersionCacheState();
            _resultLists.ResetAll();
        }

        public void Dispose()
        {
            ResetDisassemblerFailureState();
            ResetDisassemblerVersionCacheState();
            _resultLists.ResetAll();
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

        [Fact]
        public async Task DisassembleAsync_UsesPerFileFallback_WhenPrimaryToolFailsForSpecificFile()
        {
            var binDir = Path.Combine(_rootDir, "bin");
            Directory.CreateDirectory(binDir);

            // dotnet-ildasm: version succeeds but disassembly fails for files matching "bad.dll"
            // dotnet-ildasm: バージョン取得は成功するが "bad.dll" に一致するファイルの逆アセンブルは失敗する
            InstallFakeTool(binDir, "dotnet-ildasm", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "dotnet ildasm 0.12.0");
                Environment.SetEnvironmentVariable(prefix + "OUTPUT", "IL_FROM_DOTNET_ILDASM");
                Environment.SetEnvironmentVariable(prefix + "FAIL_PATTERN", "bad.dll");
                Environment.SetEnvironmentVariable(prefix + "FAIL_EXIT", "90");
            });
            // dotnet muxer: always fails / dotnet muxer: 常に失敗
            InstallFakeTool(binDir, "dotnet", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "VERSION_EXIT", "1");
                Environment.SetEnvironmentVariable(prefix + "EXIT", "1");
            });
            // ilspycmd: always succeeds / ilspycmd: 常に成功
            InstallFakeTool(binDir, "ilspycmd", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "ilspycmd 9.1.0");
                Environment.SetEnvironmentVariable(prefix + "OUTPUT", "IL_FROM_ILSPY");
            });

            var oldPath = Environment.GetEnvironmentVariable("PATH");
            var oldHome = Environment.GetEnvironmentVariable("HOME");
            try
            {
                Environment.SetEnvironmentVariable("PATH", binDir + Path.PathSeparator + oldPath);
                Environment.SetEnvironmentVariable("HOME", _rootDir);

                var config = CreateConfig(enableIlCache: false);
                var service = CreateService(config, null);

                var goodDll = Path.Combine(_rootDir, "good.dll");
                var badDll = Path.Combine(_rootDir, "bad.dll");
                await File.WriteAllTextAsync(goodDll, "dummy");
                await File.WriteAllTextAsync(badDll, "dummy");

                var (_, command1) = await service.DisassembleAsync(goodDll);
                var (_, command2) = await service.DisassembleAsync(badDll);

                Assert.Contains("dotnet-ildasm", command1, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("ilspycmd", command2, StringComparison.OrdinalIgnoreCase);
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
        public void BuildArgSets_DotnetMuxer_UsesIldasmSubcommand()
        {
            var method = typeof(DotNetDisassembleService).GetMethod("BuildArgSets", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var argSetsObject = method.Invoke(null, ["dotnet", "/tmp/sample.dll", null]);
            var argSets = Assert.IsAssignableFrom<System.Collections.IEnumerable>(argSetsObject);

            bool inspected = false;
            foreach (var item in argSets)
            {
                var itemType = item.GetType();
                var argsField = itemType.GetField("Item2");
                Assert.NotNull(argsField);
                var args = Assert.IsType<string[]>(argsField.GetValue(item));
                Assert.NotEmpty(args);
                Assert.Equal("ildasm", args[0]);
                inspected = true;
            }

            Assert.True(inspected);
        }

        [Fact]
        public void BuildPrefetchCacheKeyPatterns_DotnetMuxer_IncludesCanonicalAndLegacyLabels()
        {
            // BuildPrefetchCacheKeyPatterns has been moved to ILCachePrefetcher
            // BuildPrefetchCacheKeyPatterns は ILCachePrefetcher へ移動済み
            var method = typeof(ILCachePrefetcher).GetMethod("BuildPrefetchCacheKeyPatterns", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var resultObject = method.Invoke(null, ["dotnet", "dotnet", "/tmp/sample.dll", "sample.dll"]);
            var labels = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<string>>(resultObject);
            var labelList = new System.Collections.Generic.List<string>(labels);

            Assert.Contains("dotnet ildasm sample.dll", labelList);
            Assert.Contains("dotnet ildasm /tmp/sample.dll", labelList);
            Assert.Contains("dotnet dotnet-ildasm sample.dll", labelList);
            Assert.Contains("dotnet dotnet-ildasm /tmp/sample.dll", labelList);
        }

        [Fact]
        public async Task DisassemblePairWithSameDisassemblerAsync_UsesSingleFallbackToolForBothSides()
        {
            _resultLists.DisassemblerToolVersions.Clear();
            _resultLists.DisassemblerToolVersionsFromCache.Clear();

            var binDir = Path.Combine(_rootDir, "bin-pair");
            Directory.CreateDirectory(binDir);

            InstallFakeTool(binDir, "dotnet-ildasm", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "dotnet ildasm 0.12.0");
                Environment.SetEnvironmentVariable(prefix + "OUTPUT", "IL_FROM_DOTNET_ILDASM");
                Environment.SetEnvironmentVariable(prefix + "FAIL_PATTERN", "bad.dll");
                Environment.SetEnvironmentVariable(prefix + "FAIL_EXIT", "90");
            });
            InstallFakeTool(binDir, "dotnet", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "EXIT", "1");
            });
            InstallFakeTool(binDir, "ilspycmd", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "ilspycmd 9.1.0");
                Environment.SetEnvironmentVariable(prefix + "OUTPUT", "IL_FROM_ILSPY");
            });

            var oldPath = Environment.GetEnvironmentVariable("PATH");
            var oldHome = Environment.GetEnvironmentVariable("HOME");
            try
            {
                Environment.SetEnvironmentVariable("PATH", binDir + Path.PathSeparator + oldPath);
                Environment.SetEnvironmentVariable("HOME", _rootDir);

                var config = CreateConfig(enableIlCache: false);
                var service = CreateService(config, null);

                var goodDll = Path.Combine(_rootDir, "good-pair.dll");
                var badDll = Path.Combine(_rootDir, "bad.dll");
                await File.WriteAllTextAsync(goodDll, "dummy");
                await File.WriteAllTextAsync(badDll, "dummy");

                var (_, oldCommand, _, newCommand) = await service.DisassemblePairWithSameDisassemblerAsync(goodDll, badDll);

                Assert.Contains("ilspycmd", oldCommand, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("ilspycmd", newCommand, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("dotnet-ildasm", oldCommand, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("dotnet-ildasm", newCommand, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("dotnet-ildasm", string.Join(",", _resultLists.DisassemblerToolVersions.Keys), StringComparison.OrdinalIgnoreCase);
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
        public async Task DisassembleAsync_BlacklistsConsecutiveFailures_AndSkipsFailedTool()
        {
            var binDir = Path.Combine(_rootDir, "bin2");
            Directory.CreateDirectory(binDir);
            var counterPath = Path.Combine(_rootDir, "dotnet_ildasm_count.txt");

            // dotnet-ildasm: version succeeds but disassembly always fails (exit 91) to trigger blacklisting
            // dotnet-ildasm: バージョン取得は成功するが逆アセンブルは常に失敗（exit 91）し、ブラックリスト登録を発動させる
            InstallFakeTool(binDir, "dotnet-ildasm", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "dotnet ildasm 0.12.0");
                Environment.SetEnvironmentVariable(prefix + "EXIT", "91");
                Environment.SetEnvironmentVariable(prefix + "COUNTER_PATH", counterPath);
            });
            InstallFakeTool(binDir, "dotnet", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "EXIT", "1");
            });
            InstallFakeTool(binDir, "ilspycmd", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "ilspycmd 9.1.0");
                Environment.SetEnvironmentVariable(prefix + "OUTPUT", "IL_FROM_ILSPY");
            });

            var oldPath = Environment.GetEnvironmentVariable("PATH");
            var oldHome = Environment.GetEnvironmentVariable("HOME");
            try
            {
                Environment.SetEnvironmentVariable("PATH", binDir + Path.PathSeparator + oldPath);
                Environment.SetEnvironmentVariable("HOME", _rootDir);

                var config = CreateConfig(enableIlCache: false);
                var service = CreateService(config, null);

                int countAfter1 = 0;
                int countAfter2 = 0;
                int countAfter3 = 0;
                for (int i = 1; i <= 3; i++)
                {
                    var dllPath = Path.Combine(_rootDir, $"f{i}.dll");
                    await File.WriteAllTextAsync(dllPath, "dummy");
                    var (_, command) = await service.DisassembleAsync(dllPath);
                    Assert.Contains("ilspycmd", command, StringComparison.OrdinalIgnoreCase);
                    var currentCount = File.Exists(counterPath) ? await CountLinesAsync(counterPath) : 0;
                    if (i == 1)
                    {
                        countAfter1 = currentCount;
                    }
                    else if (i == 2)
                    {
                        countAfter2 = currentCount;
                    }
                    else
                    {
                        countAfter3 = currentCount;
                    }
                }

                Assert.True(countAfter1 > 0);
                Assert.True(countAfter2 > countAfter1);
                Assert.Equal(countAfter2, countAfter3);
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

        // After the blacklist TTL (10 min) expires, the same tool should be retried and succeed.
        // Injects a pre-expired entry (fail count >= threshold, last fail 11 min ago) to verify TTL-based retry.
        // ブラックリスト TTL（10 分）満了後に同ツールが再試行されて成功することを確認する。
        // TTL 失効済みエントリ（失敗回数 >= 閾値、最終失敗 11 分前）を挿入して TTL ベースのリトライを検証する。
        [Fact]
        public async Task DisassembleAsync_AfterBlacklistTtlExpiry_RetriesToolAndSucceeds()
        {
            var binDir = Path.Combine(_rootDir, "bin-ttl");
            Directory.CreateDirectory(binDir);

            // dotnet-ildasm: always succeeds / dotnet-ildasm: 常に成功
            InstallFakeTool(binDir, "dotnet-ildasm", prefix =>
            {
                Environment.SetEnvironmentVariable(prefix + "VERSION_OUTPUT", "dotnet ildasm 1.0.0");
                Environment.SetEnvironmentVariable(prefix + "OUTPUT", "IL_FROM_DOTNET_ILDASM");
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

                var config = CreateConfig(enableIlCache: false);
                var service = CreateService(config, null);

                // Inject a TTL-expired entry (fail count = threshold = 3, last fail = 11 min ago)
                // TTL 失効済みエントリ（失敗回数 = 閾値 = 3、最終失敗 = 11 分前）を直接挿入
                var blacklistField = typeof(DotNetDisassembleService).GetField("_blacklist", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(blacklistField);
                var blacklist = blacklistField.GetValue(service) as DisassemblerBlacklist;
                Assert.NotNull(blacklist);
                blacklist.InjectEntry(Constants.DOTNET_ILDASM, failCount: 3, lastFailUtc: DateTime.UtcNow.AddMinutes(-11));

                var dllPath = Path.Combine(_rootDir, "ttl-target.dll");
                await File.WriteAllTextAsync(dllPath, "dummy");

                // After TTL expiry, dotnet-ildasm is retried and succeeds
                // TTL 失効後のため dotnet-ildasm が再試行されて成功する
                var (_, command) = await service.DisassembleAsync(dllPath);

                Assert.Contains("dotnet-ildasm", command, StringComparison.OrdinalIgnoreCase);
                // The expired entry should have been purged inside IsBlacklisted
                // 期限切れエントリは IsBlacklisted 内で削除されているはず
                Assert.False(blacklist.ContainsEntry(Constants.DOTNET_ILDASM));
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

        // ── Helpers / ヘルパー ──────────────────────────────────────────────────

        private static ConfigSettings CreateConfig(bool enableIlCache) => new()
        {
            EnableILCache = enableIlCache,
            IgnoredExtensions = new(),
            TextFileExtensions = new()
        };

        private static void InstallFakeTool(string binDir, string toolName, Action<string> configureEnv)
        {
            var exeName = OperatingSystem.IsWindows() ? "FakeDisassembler.exe" : "FakeDisassembler";
            var srcPath = Path.Combine(AppContext.BaseDirectory, exeName);

            // Skip if FakeDisassembler binary is not available (e.g. non-test-runner contexts)
            // FakeDisassembler バイナリが利用不可の場合はスキップ
            if (!File.Exists(srcPath))
            {
                return;
            }

            var destName = OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;
            var destPath = Path.Combine(binDir, destName);
            File.Copy(srcPath, destPath, overwrite: true);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(destPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            // Copy managed DLL and runtime config alongside the AppHost so it can execute
            // AppHost が実行できるようにマネージド DLL とランタイム設定を同フォルダにコピー
            foreach (var suffix in new[] { ".dll", ".runtimeconfig.json", ".deps.json" })
            {
                var runtimeFile = Path.Combine(AppContext.BaseDirectory, "FakeDisassembler" + suffix);
                if (File.Exists(runtimeFile))
                {
                    File.Copy(runtimeFile, Path.Combine(binDir, "FakeDisassembler" + suffix), overwrite: true);
                }
            }

            var prefix = GetEnvPrefix(toolName);
            configureEnv(prefix);
        }

        private static string GetEnvPrefix(string toolName)
            => "FD_FAKE_" + toolName.ToUpperInvariant().Replace("-", "_") + "_";

        private static string GetInstalledFakeBinaryPath(string binDir, string toolName)
        {
            var destName = OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;
            return Path.Combine(binDir, destName);
        }

        private static void ClearFakeToolEnvVars(string toolName)
        {
            var prefix = GetEnvPrefix(toolName);
            foreach (var suffix in new[] { "VERSION_EXIT", "VERSION_OUTPUT", "OUTPUT", "EXIT",
                                           "FAIL_PATTERN", "FAIL_EXIT", "COUNTER_PATH" })
            {
                Environment.SetEnvironmentVariable(prefix + suffix, null);
            }
        }

        private static async Task<int> CountLinesAsync(string path)
        {
            var count = 0;
            using var reader = new StreamReader(path);
            while (await reader.ReadLineAsync() != null)
            {
                count++;
            }
            return count;
        }

        private static void ResetDisassemblerFailureState()
        {
            // DisassemblerBlacklist is per-instance; no shared static state to clear. Kept as no-op for symmetry.
            // DisassemblerBlacklist はインスタンス単位で共有静的状態はない。対称性のため no-op として保持。
        }

        private void ResetDisassemblerVersionCacheState()
        {
            var field = typeof(DotNetDisassemblerCache).GetField("_disassemblerVersionCache", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var dictionary = field.GetValue(_dotNetDisassemblerCache) as ConcurrentDictionary<string, string>;
            Assert.NotNull(dictionary);
            dictionary.Clear();
        }

        private void SeedDisassemblerVersionCache(string key, string version)
        {
            var field = typeof(DotNetDisassemblerCache).GetField("_disassemblerVersionCache", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var dictionary = field.GetValue(_dotNetDisassemblerCache) as ConcurrentDictionary<string, string>;
            Assert.NotNull(dictionary);
            dictionary[key] = version;
        }

        private DotNetDisassembleService CreateService(ConfigSettings config, ILCache ilCache)
            => new(config, ilCache, _resultLists, _logger, _dotNetDisassemblerCache);
    }
}
