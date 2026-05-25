using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services.Caching
{
    /// <summary>
    /// Tests for the internal <see cref="DotNetDisassemblerCache"/> disassembler-info parsing and normalization.
    /// 内部クラス <see cref="DotNetDisassemblerCache"/> の逆アセンブラ情報解析・正規化のテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class DotNetDisassemblerCacheTests
    {
        [Fact]
        public void GetDisassemblerInfo_DotnetIldasmSubcommand_RecognizedAndNormalized()
        {
            var tuple = InvokeGetDisassemblerInfo("dotnet ildasm sample.dll");

            Assert.Equal("DotnetIldasm", tuple.KindName);
            Assert.Equal("dotnet ildasm", tuple.CacheKey);
            Assert.Equal("dotnet", tuple.Executable);
        }

        [Fact]
        public void GetDisassemblerInfo_LegacyDotnetDashToolSubcommand_StillSupported()
        {
            var tuple = InvokeGetDisassemblerInfo("dotnet dotnet-ildasm sample.dll");

            Assert.Equal("DotnetIldasm", tuple.KindName);
            Assert.Equal("dotnet ildasm", tuple.CacheKey);
            Assert.Equal("dotnet", tuple.Executable);
        }

        [Fact]
        public async Task GetDisassemblerVersionAsync_WhenVersionLookupProcessStartFails_LogsWarningWithExceptionType()
        {
            var emptyBinDir = Path.Combine(Path.GetTempPath(), "fd-disasmcache-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(emptyBinDir);
            var oldPath = Environment.GetEnvironmentVariable("PATH");

            try
            {
                Environment.SetEnvironmentVariable("PATH", emptyBinDir);
                var logger = new TestLogger(logFileAbsolutePath: "test.log");
                var cache = new DotNetDisassemblerCache(logger);

                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetDisassemblerVersionAsync("ilspycmd sample.dll"));

                Assert.Contains("Failed to obtain version string", exception.Message, StringComparison.Ordinal);
                Assert.Contains(
                    logger.Entries,
                    entry => entry.LogLevel == AppLogLevel.Warning
                        && entry.Message.Contains("Failed to get version", StringComparison.Ordinal)
                        && entry.Message.Contains("ExecutableIsPathRooted=False", StringComparison.Ordinal)
                        && entry.Message.Contains("ExecutableLooksPathLike=False", StringComparison.Ordinal)
                        && entry.Message.Contains("args='--version'", StringComparison.Ordinal)
                        && entry.Message.Contains("Win32Exception", StringComparison.Ordinal)
                        && entry.Exception is System.ComponentModel.Win32Exception);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", oldPath);
                if (Directory.Exists(emptyBinDir))
                {
                    Directory.Delete(emptyBinDir, recursive: true);
                }
            }
        }

        [Fact]
        public void PathShapeDiagnostics_LooksLikePath_WhenCommandContainsInvalidChars_ReturnsFalseInsteadOfThrowing()
        {
            var looksLikePath = PathShapeDiagnostics.LooksLikePath("bad\0tool");

            Assert.False(looksLikePath);
        }

        [Fact]
        public void PathShapeDiagnostics_DescribeState_WhenCommandContainsInvalidChars_DowngradesRootedAndPathLikeFlags()
        {
            var diagnostic = PathShapeDiagnostics.DescribeState("Executable", "bad\0tool");

            Assert.Contains("ExecutableIsPathRooted=Unknown", diagnostic, StringComparison.Ordinal);
            Assert.Contains("ExecutableLooksPathLike=False", diagnostic, StringComparison.Ordinal);
        }

        private static (string KindName, string CacheKey, string Executable) InvokeGetDisassemblerInfo(string command)
        {
            var method = typeof(DotNetDisassemblerCache).GetMethod("GetDisassemblerInfo", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var tuple = method.Invoke(null, [command]);
            Assert.NotNull(tuple);

            var tupleType = tuple.GetType();
            var kind = tupleType.GetField("Item1");
            var cacheKey = tupleType.GetField("Item2");
            var executable = tupleType.GetField("Item3");

            Assert.NotNull(kind);
            Assert.NotNull(cacheKey);
            Assert.NotNull(executable);

            return (
                KindName: kind.GetValue(tuple)?.ToString(),
                CacheKey: cacheKey.GetValue(tuple) as string,
                Executable: executable.GetValue(tuple) as string
            );
        }
    }
}
