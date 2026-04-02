using System.Reflection;
using FolderDiffIL4DotNet.Runner;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Runner
{
    /// <summary>
    /// Unit tests for <see cref="PluginAssemblyLoadContext"/>.
    /// <see cref="PluginAssemblyLoadContext"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class PluginAssemblyLoadContextTests
    {
        [Fact]
        public void Constructor_CreatesCollectibleContext()
        {
            // Use the test assembly's own path as a valid assembly path / テストアセンブリ自身のパスを有効なパスとして使用
            string assemblyPath = typeof(PluginAssemblyLoadContextTests).Assembly.Location;
            var ctx = new PluginAssemblyLoadContext(assemblyPath);

            Assert.True(ctx.IsCollectible);
            ctx.Unload();
        }

        [Fact]
        public void Load_PluginAbstractions_ReturnsNull()
        {
            // Plugin.Abstractions should fall back to default context / Plugin.Abstractions はデフォルトコンテキストにフォールバックすべき
            string assemblyPath = typeof(PluginAssemblyLoadContextTests).Assembly.Location;
            var ctx = new PluginAssemblyLoadContext(assemblyPath);

            // Loading by name should resolve from default context (returns null internally)
            // 名前でロードするとデフォルトコンテキストから解決（内部的にnull返却）
            var asm = ctx.LoadFromAssemblyName(new AssemblyName("FolderDiffIL4DotNet.Plugin.Abstractions"));

            Assert.NotNull(asm); // Resolved from default context / デフォルトコンテキストから解決
            Assert.Equal("FolderDiffIL4DotNet.Plugin.Abstractions", asm.GetName().Name);
            ctx.Unload();
        }

        [Fact]
        public void Load_DiAbstractions_ReturnsNull()
        {
            // DI Abstractions should also fall back to default context / DI Abstractions もデフォルトコンテキストにフォールバック
            string assemblyPath = typeof(PluginAssemblyLoadContextTests).Assembly.Location;
            var ctx = new PluginAssemblyLoadContext(assemblyPath);

            var asm = ctx.LoadFromAssemblyName(
                new AssemblyName("Microsoft.Extensions.DependencyInjection.Abstractions"));

            Assert.NotNull(asm);
            ctx.Unload();
        }
    }
}
