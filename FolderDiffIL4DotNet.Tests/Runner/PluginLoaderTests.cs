using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
#pragma warning disable CA1031 // Cleanup best effort / クリーンアップのベストエフォート
            catch { /* best-effort cleanup / ベストエフォートのクリーンアップ */ }
#pragma warning restore CA1031
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

        // -----------------------------------------------------------------------
        // Strict mode (SHA-256 hash verification) tests
        // 厳格モード（SHA-256 ハッシュ検証）テスト
        // -----------------------------------------------------------------------

        [Fact]
        public void LoadPlugins_StrictModeNoTrustedHashes_ReturnsEmptyAndLogsWarning()
        {
            // Arrange: directory with an invalid DLL, strict mode enabled but no trusted hashes
            // 準備: 無効な DLL を含むディレクトリ、厳格モード有効だが信頼済みハッシュなし
            var pluginDir = Path.Combine(_tempDir, "StrictPlugin");
            Directory.CreateDirectory(pluginDir);
            File.WriteAllBytes(Path.Combine(pluginDir, "StrictPlugin.dll"), new byte[] { 0x4D, 0x5A, 0x00, 0x01 });

            var searchPaths = new List<string> { _tempDir };
            var enabledIds = new HashSet<string>();

            // Act / 実行
            var result = _loader.LoadPlugins(searchPaths, enabledIds, new Version(1, 0, 0),
                strictMode: true, trustedHashes: null);

            // Assert / 検証 — plugin loading is rejected before assembly load
            Assert.Empty(result);
            Assert.Contains(_logger.Messages, m => m.Contains("rejected before load", StringComparison.Ordinal));
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("Failed to load plugin", StringComparison.Ordinal));
        }

        [Fact]
        public void LoadPlugins_StrictModeWithEmptyTrustedHashes_ReturnsEmptyAndLogsWarning()
        {
            // Arrange: directory with a DLL, strict mode enabled but empty hash map
            // 準備: DLL を含むディレクトリ、厳格モード有効だが空のハッシュマップ
            var pluginDir = Path.Combine(_tempDir, "NoHashPlugin");
            Directory.CreateDirectory(pluginDir);
            File.WriteAllBytes(Path.Combine(pluginDir, "NoHashPlugin.dll"), new byte[] { 0x4D, 0x5A, 0x00, 0x01 });

            var searchPaths = new List<string> { _tempDir };
            var enabledIds = new HashSet<string>();
            var trustedHashes = new Dictionary<string, string>();

            // Act / 実行
            var result = _loader.LoadPlugins(searchPaths, enabledIds, new Version(1, 0, 0),
                strictMode: true, trustedHashes: trustedHashes);

            // Assert / 検証 — plugin loading is rejected before assembly load
            Assert.Empty(result);
            Assert.Contains(_logger.Messages, m => m.Contains("rejected before load", StringComparison.Ordinal));
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("Failed to load plugin", StringComparison.Ordinal));
        }

        [Fact]
        public void LoadPlugins_StrictModeHashMismatch_RejectsBeforeAssemblyLoad()
        {
            // Arrange: invalid DLL whose hash is not in the trusted map
            // 準備: 信頼済みハッシュマップに存在しないハッシュを持つ無効 DLL
            var pluginDir = Path.Combine(_tempDir, "HashMismatchPlugin");
            Directory.CreateDirectory(pluginDir);
            var pluginBytes = new byte[] { 0x4D, 0x5A, 0x11, 0x22, 0x33, 0x44 };
            File.WriteAllBytes(Path.Combine(pluginDir, "HashMismatchPlugin.dll"), pluginBytes);

            var searchPaths = new List<string> { _tempDir };
            var enabledIds = new HashSet<string>();
            var trustedHashes = new Dictionary<string, string>
            {
                ["some-other-plugin"] = Convert.ToHexString(SHA256.HashData(new byte[] { 0x10, 0x20, 0x30 }))
            };

            // Act / 実行
            var result = _loader.LoadPlugins(searchPaths, enabledIds, new Version(1, 0, 0),
                strictMode: true, trustedHashes: trustedHashes);

            // Assert / 検証
            Assert.Empty(result);
            Assert.Contains(_logger.Messages, m => m.Contains("rejected before load", StringComparison.Ordinal));
            Assert.DoesNotContain(_logger.Messages, m => m.Contains("Failed to load plugin", StringComparison.Ordinal));
        }

        [Fact]
        public void LoadPlugins_StrictModeDisabled_DoesNotRequireHashes()
        {
            // Arrange: invalid DLL, strict mode disabled — should fail from bad format, not hash check
            // 準備: 無効な DLL、厳格モード無効 — ハッシュチェックではなくフォーマット不正で失敗すべき
            var pluginDir = Path.Combine(_tempDir, "NoStrictPlugin");
            Directory.CreateDirectory(pluginDir);
            File.WriteAllBytes(Path.Combine(pluginDir, "NoStrictPlugin.dll"), new byte[] { 0x4D, 0x5A, 0x00, 0x01 });

            var searchPaths = new List<string> { _tempDir };
            var enabledIds = new HashSet<string>();

            // Act / 実行
            var result = _loader.LoadPlugins(searchPaths, enabledIds, new Version(1, 0, 0),
                strictMode: false, trustedHashes: null);

            // Assert / 検証 — fails due to bad DLL format, not hash rejection
            Assert.Empty(result);
            Assert.Contains(_logger.Messages, m => m.Contains("Failed to load plugin"));
        }
    }
}
