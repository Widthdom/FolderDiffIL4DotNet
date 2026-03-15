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
                // ignore cleanup errors in tests
            }
        }

        [Fact]
        public async Task DisassembleAsync_UsesPerFileFallback_WhenPrimaryToolFailsForSpecificFile()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            var binDir = Path.Combine(_rootDir, "bin");
            Directory.CreateDirectory(binDir);

            WriteExecutable(binDir, "dotnet-ildasm", """
#!/bin/sh
if [ "$1" = "--version" ] || [ "$1" = "-v" ]; then
  echo "dotnet ildasm 0.12.0"
  exit 0
fi
case "$1" in
  *bad.dll) exit 90 ;;
esac
echo "IL_FROM_DOTNET_ILDASM"
exit 0
""");
            WriteExecutable(binDir, "dotnet", """
#!/bin/sh
exit 1
""");
            WriteExecutable(binDir, "ilspycmd", """
#!/bin/sh
if [ "$1" = "--version" ] || [ "$1" = "-v" ] || [ "$1" = "-h" ]; then
  echo "ilspycmd 9.1.0"
  exit 0
fi
echo "IL_FROM_ILSPY"
exit 0
""");

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
            var method = typeof(DotNetDisassembleService).GetMethod("BuildPrefetchCacheKeyPatterns", BindingFlags.Static | BindingFlags.NonPublic);
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
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            _resultLists.DisassemblerToolVersions.Clear();
            _resultLists.DisassemblerToolVersionsFromCache.Clear();

            var binDir = Path.Combine(_rootDir, "bin-pair");
            Directory.CreateDirectory(binDir);

            WriteExecutable(binDir, "dotnet-ildasm", """
#!/bin/sh
if [ "$1" = "--version" ] || [ "$1" = "-v" ]; then
  echo "dotnet ildasm 0.12.0"
  exit 0
fi
case "$1" in
  *bad.dll) exit 90 ;;
esac
echo "IL_FROM_DOTNET_ILDASM"
exit 0
""");
            WriteExecutable(binDir, "dotnet", """
#!/bin/sh
exit 1
""");
            WriteExecutable(binDir, "ilspycmd", """
#!/bin/sh
if [ "$1" = "--version" ] || [ "$1" = "-v" ] || [ "$1" = "-h" ]; then
  echo "ilspycmd 9.1.0"
  exit 0
fi
echo "IL_FROM_ILSPY"
exit 0
""");

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
            }
        }

        [Fact]
        public async Task DisassembleAsync_BlacklistsConsecutiveFailures_AndSkipsFailedTool()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            var binDir = Path.Combine(_rootDir, "bin2");
            Directory.CreateDirectory(binDir);
            var counterPath = Path.Combine(_rootDir, "dotnet_ildasm_count.txt");

            WriteExecutable(binDir, "dotnet-ildasm", $"""
#!/bin/sh
if [ "$1" = "--version" ] || [ "$1" = "-v" ]; then
  echo "dotnet ildasm 0.12.0"
  exit 0
fi
echo x >> "{counterPath}"
exit 91
""");
            WriteExecutable(binDir, "dotnet", """
#!/bin/sh
exit 1
""");
            WriteExecutable(binDir, "ilspycmd", """
#!/bin/sh
if [ "$1" = "--version" ] || [ "$1" = "-v" ] || [ "$1" = "-h" ]; then
  echo "ilspycmd 9.1.0"
  exit 0
fi
echo "IL_FROM_ILSPY"
exit 0
""");

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
            }
        }

        /// <summary>
        /// ブラックリスト TTL（10 分）が満了した後、同ツールが再度試行されて成功することを確認します。
        /// _disassembleFailCountAndTime に "失敗回数 >= 閾値 かつ 最終失敗時刻が 11 分前" のエントリを直接挿入し、
        /// TTL 失効でエントリが消去されてから逆アセンブルが成功することを検証します。
        /// </summary>
        [Fact]
        public async Task DisassembleAsync_AfterBlacklistTtlExpiry_RetriesToolAndSucceeds()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            var binDir = Path.Combine(_rootDir, "bin-ttl");
            Directory.CreateDirectory(binDir);

            // dotnet-ildasm は常に成功するスクリプト
            WriteExecutable(binDir, "dotnet-ildasm", """
#!/bin/sh
if [ "$1" = "--version" ] || [ "$1" = "-v" ]; then
  echo "dotnet ildasm 1.0.0"
  exit 0
fi
echo "IL_FROM_DOTNET_ILDASM"
exit 0
""");
            WriteExecutable(binDir, "dotnet", """
#!/bin/sh
exit 1
""");
            WriteExecutable(binDir, "ilspycmd", """
#!/bin/sh
exit 1
""");

            var oldPath = Environment.GetEnvironmentVariable("PATH");
            var oldHome = Environment.GetEnvironmentVariable("HOME");
            try
            {
                Environment.SetEnvironmentVariable("PATH", binDir + Path.PathSeparator + oldPath);
                Environment.SetEnvironmentVariable("HOME", _rootDir);

                var config = CreateConfig(enableIlCache: false);
                var service = CreateService(config, null);

                // TTL 失効済みエントリ（失敗回数 = 閾値 = 3、最終失敗 = 11 分前）を直接挿入
                var failField = typeof(DotNetDisassembleService).GetField("_disassembleFailCountAndTime", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(failField);
                var dict = failField.GetValue(null) as System.Collections.Concurrent.ConcurrentDictionary<string, (int FailCount, DateTime LastFailUtc)>;
                Assert.NotNull(dict);
                dict[FolderDiffIL4DotNet.Common.Constants.DOTNET_ILDASM] = (FailCount: 3, LastFailUtc: DateTime.UtcNow.AddMinutes(-11));

                var dllPath = Path.Combine(_rootDir, "ttl-target.dll");
                await File.WriteAllTextAsync(dllPath, "dummy");

                // TTL 失効後のため dotnet-ildasm が再試行されて成功する
                var (_, command) = await service.DisassembleAsync(dllPath);

                Assert.Contains("dotnet-ildasm", command, StringComparison.OrdinalIgnoreCase);
                // エントリは IsDisassemblerBlacklisted 内で削除されているはず
                Assert.False(dict.ContainsKey(FolderDiffIL4DotNet.Common.Constants.DOTNET_ILDASM));
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", oldPath);
                Environment.SetEnvironmentVariable("HOME", oldHome);
            }
        }

        [Fact]
        public async Task DisassembleAsync_WhenVersionLookupFails_UsesFingerprintAndAvoidsCrossVersionCacheMix()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            var binDir = Path.Combine(_rootDir, "bin3");
            Directory.CreateDirectory(binDir);
            var cacheDir = Path.Combine(_rootDir, "ilcache");
            var counterPath = Path.Combine(_rootDir, "dotnet_ildasm_counter.txt");

            WriteExecutable(binDir, "dotnet-ildasm", BuildVersionFailingDisassemblerScript(counterPath, "#REV-A"));
            WriteExecutable(binDir, "dotnet", """
#!/bin/sh
exit 1
""");
            WriteExecutable(binDir, "ilspycmd", """
#!/bin/sh
exit 1
""");

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

                // 実体更新（サイズ/更新時刻を変える）を模擬
                await Task.Delay(1100);
                WriteExecutable(binDir, "dotnet-ildasm", BuildVersionFailingDisassemblerScript(counterPath, "#REV-B-LONGER"));

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
            if (OperatingSystem.IsWindows()) { return; }

            var binDir = Path.Combine(_rootDir, "prefetch-hit-bin");
            Directory.CreateDirectory(binDir);
            WriteExecutable(binDir, "dotnet-ildasm", """
                #!/bin/sh
                if [ "$1" = "--version" ] || [ "$1" = "-v" ]; then
                  echo "1.2.3"
                  exit 0
                fi
                exit 1
                """);
            WriteExecutable(binDir, "dotnet", "#!/bin/sh\nexit 1\n");
            WriteExecutable(binDir, "ilspycmd", "#!/bin/sh\nexit 1\n");

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
            }
        }

        private static ConfigSettings CreateConfig(bool enableIlCache) => new()
        {
            EnableILCache = enableIlCache,
            IgnoredExtensions = new(),
            TextFileExtensions = new()
        };

        private static void WriteExecutable(string directory, string fileName, string content)
        {
            var path = Path.Combine(directory, fileName);
            File.WriteAllText(path, content);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
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

        private static string BuildVersionFailingDisassemblerScript(string counterPath, string revisionMarker) => $"""
#!/bin/sh
if [ "$1" = "--version" ] || [ "$1" = "-v" ]; then
  exit 2
fi
echo x >> "{counterPath}"
echo "IL_FROM_VERSION_FAILING_TOOL"
exit 0
{revisionMarker}
""";

        private static void ResetDisassemblerFailureState()
        {
            var field = typeof(DotNetDisassembleService).GetField("_disassembleFailCountAndTime", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var dictionary = field.GetValue(null) as ConcurrentDictionary<string, (int FailCount, DateTime LastFailUtc)>;
            Assert.NotNull(dictionary);
            dictionary.Clear();
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
