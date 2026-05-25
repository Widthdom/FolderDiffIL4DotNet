using System;
using System.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Runner
{
    /// <summary>
    /// Unit tests for <see cref="RunScopeBuilder"/>.
    /// <see cref="RunScopeBuilder"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class RunScopeBuilderTests : IDisposable
    {
        private readonly TestLogger _logger = new();
        private readonly string _tempDir;

        public RunScopeBuilderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"RunScopeBuilderTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); }
#pragma warning disable CA1031 // Cleanup best effort / クリーンアップはベストエフォート
            catch { /* ignore */ }
#pragma warning restore CA1031
        }

        [Fact]
        public void BuildExecutionContext_LocalPaths_OptimizeForNetworkSharesFalse()
        {
            var config = new ConfigSettingsBuilder
            {
                AutoDetectNetworkShares = true,
                OptimizeForNetworkShares = false
            }.Build();

            var ctx = RunScopeBuilder.BuildExecutionContext(
                _tempDir, _tempDir, _tempDir, config);

            Assert.False(ctx.OptimizeForNetworkShares);
            Assert.False(ctx.DetectedNetworkOld);
            Assert.False(ctx.DetectedNetworkNew);
        }

        [Fact]
        public void BuildExecutionContext_OptimizeForNetworkSharesConfig_OverridesDetection()
        {
            var config = new ConfigSettingsBuilder
            {
                AutoDetectNetworkShares = false,
                OptimizeForNetworkShares = true
            }.Build();

            var ctx = RunScopeBuilder.BuildExecutionContext(
                _tempDir, _tempDir, _tempDir, config);

            Assert.True(ctx.OptimizeForNetworkShares);
        }

        [Fact]
        public void BuildExecutionContext_StorePaths_CorrectlySet()
        {
            string oldDir = Path.Combine(_tempDir, "old");
            string newDir = Path.Combine(_tempDir, "new");
            string reportDir = Path.Combine(_tempDir, "reports");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            Directory.CreateDirectory(reportDir);
            var config = new ConfigSettingsBuilder().Build();

            var ctx = RunScopeBuilder.BuildExecutionContext(
                oldDir, newDir, reportDir, config);

            Assert.Equal(oldDir, ctx.OldFolderAbsolutePath);
            Assert.Equal(newDir, ctx.NewFolderAbsolutePath);
            Assert.Equal(reportDir, ctx.ReportsFolderAbsolutePath);
        }

        [Fact]
        public void CreateIlCache_CacheDisabled_ReturnsNull()
        {
            var config = new ConfigSettingsBuilder { EnableILCache = false }.Build();

            var cache = RunScopeBuilder.CreateIlCache(config, _logger);

            Assert.Null(cache);
        }

        [Fact]
        public void CreateIlCache_CacheEnabled_ReturnsNonNull()
        {
            var config = new ConfigSettingsBuilder { EnableILCache = true }.Build();

            var cache = RunScopeBuilder.CreateIlCache(config, _logger);

            Assert.NotNull(cache);
        }

        [Fact]
        public void Build_ReturnsServiceProvider_WithRequiredServices()
        {
            var config = new ConfigSettingsBuilder().Build();
            var ctx = RunScopeBuilder.BuildExecutionContext(
                _tempDir, _tempDir, _tempDir, config);

            using var sp = RunScopeBuilder.Build(config, ctx, _logger);

            // Verify core services are resolvable / コアサービスが解決可能であることを検証
            Assert.NotNull(sp.GetService<FileDiffResultLists>());
            Assert.NotNull(sp.GetService<IReadOnlyConfigSettings>());
            Assert.NotNull(sp.GetService<DiffExecutionContext>());
        }
    }
}
