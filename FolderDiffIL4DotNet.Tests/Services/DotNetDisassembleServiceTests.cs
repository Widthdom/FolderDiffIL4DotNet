using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class DotNetDisassembleServiceTests : IDisposable
    {
        private readonly string _rootDir;

        public DotNetDisassembleServiceTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-disasm-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
            ResetDisassemblerFailureState();
        }

        public void Dispose()
        {
            ResetDisassemblerFailureState();
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
                var service = new DotNetDisassembleService(config, ilCache: null);

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
                var service = new DotNetDisassembleService(config, ilCache: null);

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
                var service1 = new DotNetDisassembleService(config, new ILCache(cacheDir));
                var (_, command1) = await service1.DisassembleAsync(dllPath);
                var countAfterFirstRun = File.Exists(counterPath) ? await CountLinesAsync(counterPath) : 0;
                Assert.Contains("fingerprint:", command1, StringComparison.OrdinalIgnoreCase);
                Assert.True(countAfterFirstRun > 0);

                // 実体更新（サイズ/更新時刻を変える）を模擬
                await Task.Delay(1100);
                WriteExecutable(binDir, "dotnet-ildasm", BuildVersionFailingDisassemblerScript(counterPath, "#REV-B-LONGER"));

                var service2 = new DotNetDisassembleService(config, new ILCache(cacheDir));
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
    }
}
