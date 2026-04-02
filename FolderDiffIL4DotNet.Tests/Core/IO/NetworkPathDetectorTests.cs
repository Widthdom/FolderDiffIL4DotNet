using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Core.IO
{
    /// <summary>
    /// Tests for <see cref="NetworkPathDetector"/> UNC path detection and
    /// <see cref="DiffExecutionContext"/> network share optimization flags.
    /// <see cref="NetworkPathDetector"/> の UNC パス検出および
    /// <see cref="DiffExecutionContext"/> のネットワーク共有最適化フラグのテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class NetworkPathDetectorTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsLikelyNetworkPath_NullOrEmpty_ReturnsFalse(string? path)
        {
            Assert.False(NetworkPathDetector.IsLikelyNetworkPath(path!));
        }

        [Theory]
        [InlineData(@"\\server\share")]
        [InlineData(@"\\server\share\subfolder")]
        [InlineData(@"\\?\UNC\server\share")]
        [InlineData("//server/share")]
        public void IsLikelyNetworkPath_UncPaths_ReturnsTrue(string path)
        {
            // UNC paths should always be detected regardless of OS
            // UNC パスは OS に関わらず常に検出されるべき
            Assert.True(NetworkPathDetector.IsLikelyNetworkPath(path));
        }

        [Theory]
        [InlineData(@"C:\Users\Build")]
        [InlineData("/home/user/builds")]
        [InlineData("/tmp/diff")]
        public void IsLikelyNetworkPath_LocalPaths_ReturnsFalse(string path)
        {
            // Local paths should not be detected as network shares
            // ローカルパスはネットワーク共有として検出されないこと
            Assert.False(NetworkPathDetector.IsLikelyNetworkPath(path));
        }
    }

    /// <summary>
    /// Tests for <see cref="DiffExecutionContext"/> construction and network share flag propagation.
    /// <see cref="DiffExecutionContext"/> のコンストラクタおよびネットワーク共有フラグ伝播のテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class DiffExecutionContextNetworkTests
    {
        [Fact]
        public void Constructor_SetsNetworkOptimizationFlags()
        {
            var ctx = new DiffExecutionContext(
                oldFolderAbsolutePath: "/old",
                newFolderAbsolutePath: "/new",
                reportsFolderAbsolutePath: "/reports",
                optimizeForNetworkShares: true,
                detectedNetworkOld: true,
                detectedNetworkNew: false);

            Assert.True(ctx.OptimizeForNetworkShares);
            Assert.True(ctx.DetectedNetworkOld);
            Assert.False(ctx.DetectedNetworkNew);
        }

        [Fact]
        public void Constructor_DefaultOptimizationDisabled()
        {
            var ctx = new DiffExecutionContext(
                oldFolderAbsolutePath: "/old",
                newFolderAbsolutePath: "/new",
                reportsFolderAbsolutePath: "/reports",
                optimizeForNetworkShares: false,
                detectedNetworkOld: false,
                detectedNetworkNew: false);

            Assert.False(ctx.OptimizeForNetworkShares);
            Assert.False(ctx.DetectedNetworkOld);
            Assert.False(ctx.DetectedNetworkNew);
        }

        [Fact]
        public void Constructor_BothFoldersDetectedAsNetwork()
        {
            var ctx = new DiffExecutionContext(
                oldFolderAbsolutePath: @"\\server\old",
                newFolderAbsolutePath: @"\\server\new",
                reportsFolderAbsolutePath: "/reports",
                optimizeForNetworkShares: true,
                detectedNetworkOld: true,
                detectedNetworkNew: true);

            Assert.True(ctx.DetectedNetworkOld);
            Assert.True(ctx.DetectedNetworkNew);
            Assert.Equal(@"\\server\old", ctx.OldFolderAbsolutePath);
            Assert.Equal(@"\\server\new", ctx.NewFolderAbsolutePath);
        }

        [Fact]
        public void Constructor_NullPaths_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new DiffExecutionContext(null!, "/new", "/reports", false, false, false));
            Assert.Throws<System.ArgumentNullException>(() =>
                new DiffExecutionContext("/old", null!, "/reports", false, false, false));
            Assert.Throws<System.ArgumentNullException>(() =>
                new DiffExecutionContext("/old", "/new", null!, false, false, false));
        }

        [Fact]
        public void Constructor_DerivesIlOutputPaths()
        {
            var ctx = new DiffExecutionContext(
                oldFolderAbsolutePath: "/old",
                newFolderAbsolutePath: "/new",
                reportsFolderAbsolutePath: "/reports",
                optimizeForNetworkShares: false,
                detectedNetworkOld: false,
                detectedNetworkNew: false);

            Assert.Contains("IL", ctx.IlOutputFolderAbsolutePath);
            Assert.Contains("old", ctx.IlOldFolderAbsolutePath);
            Assert.Contains("new", ctx.IlNewFolderAbsolutePath);
        }
    }
}
