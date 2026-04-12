using System.Reflection;
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
