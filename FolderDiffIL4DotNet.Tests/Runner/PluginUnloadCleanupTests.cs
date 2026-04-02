using System;
using System.Reflection;
using System.Runtime.Loader;
using FolderDiffIL4DotNet.Runner;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Runner
{
    /// <summary>
    /// Tests for plugin AssemblyLoadContext unloading and cleanup behavior.
    /// Verifies that collectible contexts can be unloaded, that GC reclaims memory,
    /// and that failure scenarios do not leave leaked resources.
    /// プラグイン AssemblyLoadContext のアンロードおよびクリーンアップ動作のテスト。
    /// コレクティブルコンテキストがアンロード可能であること、GC がメモリを回収すること、
    /// 失敗シナリオでリソースリークが発生しないことを検証します。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class PluginUnloadCleanupTests
    {
        private static string TestAssemblyPath => typeof(PluginUnloadCleanupTests).Assembly.Location;

        [Fact]
        public void Unload_CollectibleContext_DoesNotThrow()
        {
            // A collectible context should unload cleanly
            // コレクティブルコンテキストはクリーンにアンロードできること
            var ctx = new PluginAssemblyLoadContext(TestAssemblyPath);
            Assert.True(ctx.IsCollectible);

            var ex = Record.Exception(() => ctx.Unload());
            Assert.Null(ex);
        }

        [Fact]
        public void Unload_AfterLoadingAssembly_DoesNotThrow()
        {
            // Even after loading an assembly, unload should succeed
            // アセンブリ読み込み後でもアンロードが成功すること
            var ctx = new PluginAssemblyLoadContext(TestAssemblyPath);
            var asm = ctx.LoadFromAssemblyName(
                new AssemblyName("FolderDiffIL4DotNet.Plugin.Abstractions"));
            Assert.NotNull(asm);

            var ex = Record.Exception(() => ctx.Unload());
            Assert.Null(ex);
        }

        [Fact]
        public void MultipleContexts_Coexist_IndependentUnload()
        {
            // Multiple contexts can be created and unloaded independently
            // 複数コンテキストを独立して作成・アンロードできること
            var ctx1 = new PluginAssemblyLoadContext(TestAssemblyPath);
            var ctx2 = new PluginAssemblyLoadContext(TestAssemblyPath);

            Assert.True(ctx1.IsCollectible);
            Assert.True(ctx2.IsCollectible);

            // Unload first, second should still work
            // 1つ目をアンロードしても2つ目は動作すること
            ctx1.Unload();

            var asm = ctx2.LoadFromAssemblyName(
                new AssemblyName("FolderDiffIL4DotNet.Plugin.Abstractions"));
            Assert.NotNull(asm);

            ctx2.Unload();
        }

        [Fact]
        public void WeakReference_BecomesNull_AfterUnloadAndGC()
        {
            // After unloading a collectible context, GC should reclaim it
            // コレクティブルコンテキストのアンロード後、GC が回収すること
            var weakRef = CreateAndUnloadContext();

            // Force GC to collect the unloaded context
            // アンロードされたコンテキストを GC に回収させる
            for (int i = 0; i < 10; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                if (!weakRef.IsAlive) break;
            }

            Assert.False(weakRef.IsAlive);
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static WeakReference CreateAndUnloadContext()
        {
            var ctx = new PluginAssemblyLoadContext(TestAssemblyPath);
            var weakRef = new WeakReference(ctx);
            ctx.Unload();
            return weakRef;
        }

        [Fact]
        public void DoubleUnload_ThrowsInvalidOperationException()
        {
            // Calling Unload() twice on the same context should throw
            // 同一コンテキストで Unload() を2回呼ぶと例外が発生すること
            var ctx = new PluginAssemblyLoadContext(TestAssemblyPath);
            ctx.Unload();

            Assert.Throws<InvalidOperationException>(() => ctx.Unload());
        }

        [Fact]
        public void UnloadingEvent_Fires_OnUnload()
        {
            // The Unloading event should fire when context is unloaded
            // コンテキストのアンロード時に Unloading イベントが発火すること
            var ctx = new PluginAssemblyLoadContext(TestAssemblyPath);
            bool eventFired = false;
            ctx.Unloading += _ => eventFired = true;

            ctx.Unload();

            Assert.True(eventFired);
        }

        [Fact]
        public void SharedAssembly_PluginAbstractions_NotAffectedByUnload()
        {
            // Plugin.Abstractions loaded from default context should survive plugin unload
            // デフォルトコンテキストからロードされた Plugin.Abstractions はプラグインアンロード後も存続すること
            var ctx = new PluginAssemblyLoadContext(TestAssemblyPath);
            var asmBefore = ctx.LoadFromAssemblyName(
                new AssemblyName("FolderDiffIL4DotNet.Plugin.Abstractions"));

            ctx.Unload();

            // The assembly from default context should still be accessible
            // デフォルトコンテキストのアセンブリは引き続きアクセス可能であること
            var asmAfter = AssemblyLoadContext.Default.LoadFromAssemblyName(
                new AssemblyName("FolderDiffIL4DotNet.Plugin.Abstractions"));

            Assert.NotNull(asmAfter);
            Assert.Equal(asmBefore.FullName, asmAfter.FullName);
        }
    }
}
