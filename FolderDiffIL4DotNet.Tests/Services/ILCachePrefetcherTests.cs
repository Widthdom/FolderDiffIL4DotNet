using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class ILCachePrefetcherTests : IDisposable
    {
        private readonly string _rootDir;
        private readonly ILoggerService _logger = new LoggerService();
        private readonly DotNetDisassemblerCache _disassemblerCache;

        public ILCachePrefetcherTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-prefetcher-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
            _disassemblerCache = new DotNetDisassemblerCache(_logger);
        }

        public void Dispose()
        {
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

        // ── Argument validation / 引数バリデーション ──────────────────────────

        [Fact]
        public async Task PrefetchIlCacheAsync_NullInput_ReturnsWithoutError()
        {
            var prefetcher = CreatePrefetcher(enableIlCache: true);
            await prefetcher.PrefetchIlCacheAsync(null, maxParallel: 1);
            Assert.Equal(0, prefetcher.IlCacheHits);
        }

        [Fact]
        public async Task PrefetchIlCacheAsync_EmptyInput_ReturnsWithoutError()
        {
            var prefetcher = CreatePrefetcher(enableIlCache: true);
            await prefetcher.PrefetchIlCacheAsync(Array.Empty<string>(), maxParallel: 1);
            Assert.Equal(0, prefetcher.IlCacheHits);
        }

        [Fact]
        public async Task PrefetchIlCacheAsync_InvalidMaxParallel_ThrowsArgumentOutOfRangeException()
        {
            var prefetcher = CreatePrefetcher(enableIlCache: true);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => prefetcher.PrefetchIlCacheAsync(new[] { "dummy.dll" }, maxParallel: 0));
        }

        [Fact]
        public async Task PrefetchIlCacheAsync_NegativeMaxParallel_ThrowsArgumentOutOfRangeException()
        {
            var prefetcher = CreatePrefetcher(enableIlCache: true);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => prefetcher.PrefetchIlCacheAsync(new[] { "dummy.dll" }, maxParallel: -1));
        }

        // ── Cache disabled / キャッシュ無効時 ──────────────────────────────────

        [Fact]
        public async Task PrefetchIlCacheAsync_WhenCacheDisabled_ReturnsWithoutHits()
        {
            var prefetcher = CreatePrefetcher(enableIlCache: false);
            await prefetcher.PrefetchIlCacheAsync(new[] { "dummy.dll" }, maxParallel: 1);
            Assert.Equal(0, prefetcher.IlCacheHits);
        }

        [Fact]
        public async Task PrefetchIlCacheAsync_WhenIlCacheIsNull_ReturnsWithoutHits()
        {
            var prefetcher = CreatePrefetcherWithNullCache();
            await prefetcher.PrefetchIlCacheAsync(new[] { "dummy.dll" }, maxParallel: 1);
            Assert.Equal(0, prefetcher.IlCacheHits);
        }

        // ── Cache hit / キャッシュヒット ────────────────────────────────────────

        [SkippableFact]
        public async Task PrefetchIlCacheAsync_WhenMatchingCacheEntryExists_IncrementsHitCounter()
        {
            Skip.If(OperatingSystem.IsWindows(), "Fake shell scripts require Unix.");

            var binDir = Path.Combine(_rootDir, "bin");
            Directory.CreateDirectory(binDir);

            // dotnet-ildasm: fake tool where version lookup succeeds but disassembly fails
            // dotnet-ildasm: バージョン取得は成功するが逆アセンブルは失敗するフェイクツール
            WriteShellScript(Path.Combine(binDir, "dotnet-ildasm"), version: "2.0.0", exitCode: 1);

            var oldPath = Environment.GetEnvironmentVariable("PATH");
            var oldHome = Environment.GetEnvironmentVariable("HOME");
            try
            {
                Environment.SetEnvironmentVariable("PATH", binDir + Path.PathSeparator + oldPath);
                Environment.SetEnvironmentVariable("HOME", _rootDir);

                var cacheDir = Path.Combine(_rootDir, "cache");
                var ilCache = new ILCache(cacheDir, _logger);

                // Seed version info into the disassembler cache
                // バージョン情報をディスアセンブラキャッシュにシード
                var cache = new DotNetDisassemblerCache(_logger);
                SeedDisassemblerVersionCache(cache, Constants.DOTNET_ILDASM, "2.0.0");

                var assemblyPath = Path.Combine(_rootDir, "target.dll");
                await File.WriteAllTextAsync(assemblyPath, "dummy");

                // Set IL with the expected cache label so prefetch will find a hit
                // 予想されるキャッシュラベルで IL をセットし、プリフェッチがヒットするようにする
                var label = $"{Constants.DOTNET_ILDASM} {Path.GetFileName(assemblyPath)} (version: 2.0.0)";
                await ilCache.SetILAsync(assemblyPath, label, "CACHED_IL");

                var prefetcher = new ILCachePrefetcher(
                    CreateConfig(enableIlCache: true),
                    ilCache,
                    _logger,
                    cache);

                await prefetcher.PrefetchIlCacheAsync(new[] { assemblyPath }, maxParallel: 1);

                Assert.True(prefetcher.IlCacheHits >= 1, $"Expected at least 1 cache hit, got {prefetcher.IlCacheHits}");
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", oldPath);
                Environment.SetEnvironmentVariable("HOME", oldHome);
            }
        }

        // ── Constructor validation / コンストラクタバリデーション ──────────────

        [Fact]
        public void Constructor_NullConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ILCachePrefetcher(null, ilCache: null, _logger, _disassemblerCache));
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ILCachePrefetcher(CreateConfig(true), ilCache: null, logger: null, _disassemblerCache));
        }

        [Fact]
        public void Constructor_NullDisassemblerCache_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ILCachePrefetcher(CreateConfig(true), ilCache: null, _logger, dotNetDisassemblerCache: null));
        }

        [Fact]
        public void IlCacheHits_InitiallyZero()
        {
            var prefetcher = CreatePrefetcher(enableIlCache: true);
            Assert.Equal(0, prefetcher.IlCacheHits);
        }

        // ── Helpers / ヘルパー ──────────────────────────────────────────────────

        private ILCachePrefetcher CreatePrefetcher(bool enableIlCache)
        {
            var cacheDir = Path.Combine(_rootDir, "cache-" + Guid.NewGuid().ToString("N"));
            var ilCache = new ILCache(cacheDir, _logger);
            return new ILCachePrefetcher(CreateConfig(enableIlCache), ilCache, _logger, _disassemblerCache);
        }

        private ILCachePrefetcher CreatePrefetcherWithNullCache()
            => new ILCachePrefetcher(CreateConfig(enableIlCache: true), ilCache: null, _logger, _disassemblerCache);

        private static ConfigSettings CreateConfig(bool enableIlCache) => new ConfigSettingsBuilder()
        {
            EnableILCache = enableIlCache,
            IgnoredExtensions = new(),
            TextFileExtensions = new()
        }.Build();

        private static void SeedDisassemblerVersionCache(DotNetDisassemblerCache cache, string command, string version)
        {
            var field = typeof(DotNetDisassemblerCache).GetField("_disassemblerVersionCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field?.GetValue(cache) is System.Collections.Concurrent.ConcurrentDictionary<string, string> dict)
            {
                dict[command] = version;
            }
        }

        private static void WriteShellScript(string path, string version, int exitCode)
        {
            var script = $"#!/bin/sh\necho \"{version}\"\nexit {exitCode}";
            File.WriteAllText(path, script);
#pragma warning disable CA1416 // Unix-only; caller guards with Skip.If(IsWindows) / Unix 専用：呼び出し元が Skip.If(IsWindows) でガード
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
#pragma warning restore CA1416
        }
    }
}
