using System;
using System.Collections.Generic;
using System.IO;
using FolderDiffIL4DotNet.Plugin.Abstractions;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Runner
{
    /// <summary>
    /// Unit tests for <see cref="PluginLoader"/>.
    /// <see cref="PluginLoader"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class PluginLoaderTests : IDisposable
    {
        private readonly TestLogger _logger = new();
        private readonly string _tempDir;
        private readonly PluginLoader _loader;

        public PluginLoaderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"PluginLoaderTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _loader = new PluginLoader(_logger);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort cleanup / ベストエフォートのクリーンアップ */ }
        }

        [Fact]
        public void LoadPlugins_NonExistentSearchPath_ReturnsEmptyList()
        {
            // Arrange / 準備
            var nonExistentPath = Path.Combine(_tempDir, "does_not_exist");
            var searchPaths = new List<string> { nonExistentPath };
            var enabledIds = new HashSet<string>();

            // Act / 実行
            var result = _loader.LoadPlugins(searchPaths, enabledIds, new Version(1, 0, 0));

            // Assert / 検証
            Assert.Empty(result);
        }

        [Fact]
        public void LoadPlugins_EmptySearchPath_ReturnsEmptyList()
        {
            // Arrange: empty directory with no plugin subdirectories
            // 準備: プラグインサブディレクトリのない空ディレクトリ
            var searchPaths = new List<string> { _tempDir };
            var enabledIds = new HashSet<string>();

            // Act / 実行
            var result = _loader.LoadPlugins(searchPaths, enabledIds, new Version(1, 0, 0));

            // Assert / 検証
            Assert.Empty(result);
        }

        [Fact]
        public void LoadPlugins_SubdirectoryWithoutMatchingDll_ReturnsEmptyList()
        {
            // Arrange: directory exists but no matching DLL (DirName/DirName.dll)
            // 準備: ディレクトリは存在するが一致する DLL がない
            var pluginDir = Path.Combine(_tempDir, "SomePlugin");
            Directory.CreateDirectory(pluginDir);
            File.WriteAllText(Path.Combine(pluginDir, "other.txt"), "not a dll");

            var searchPaths = new List<string> { _tempDir };
            var enabledIds = new HashSet<string>();

            // Act / 実行
            var result = _loader.LoadPlugins(searchPaths, enabledIds, new Version(1, 0, 0));

            // Assert / 検証
            Assert.Empty(result);
        }

        [Fact]
        public void LoadPlugins_InvalidDll_LogsWarningAndReturnsEmptyList()
        {
            // Arrange: directory with a DLL that isn't a valid .NET assembly
            // 準備: 有効な .NET アセンブリではない DLL を含むディレクトリ
            var pluginDir = Path.Combine(_tempDir, "BadPlugin");
            Directory.CreateDirectory(pluginDir);
            File.WriteAllBytes(Path.Combine(pluginDir, "BadPlugin.dll"), new byte[] { 0x00, 0x01, 0x02, 0x03 });

            var searchPaths = new List<string> { _tempDir };
            var enabledIds = new HashSet<string>();

            // Act / 実行
            var result = _loader.LoadPlugins(searchPaths, enabledIds, new Version(1, 0, 0));

            // Assert / 検証
            Assert.Empty(result);
            Assert.Contains(_logger.Messages, m => m.Contains("Failed to load plugin"));
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new PluginLoader(null!));
        }
    }
}
