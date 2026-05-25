using System;
using System.IO;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="DiffExecutionContext"/>.
    /// <see cref="DiffExecutionContext"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class DiffExecutionContextTests
    {
        // ── Constructor null validation / コンストラクタ null 検証 ──────

        [Fact]
        public void Constructor_NullOldFolder_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new DiffExecutionContext(null!, "/new", "/reports", false, false, false));
            Assert.Equal("oldFolderAbsolutePath", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullNewFolder_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new DiffExecutionContext("/old", null!, "/reports", false, false, false));
            Assert.Equal("newFolderAbsolutePath", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullReportsFolder_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new DiffExecutionContext("/old", "/new", null!, false, false, false));
            Assert.Equal("reportsFolderAbsolutePath", ex.ParamName);
        }

        // ── Path derivation / パス導出 ────────────────────────────────

        [Fact]
        public void Constructor_DerivesIlOutputPaths()
        {
            // Verify IL output paths are correctly derived from reports folder.
            // IL 出力パスがレポートフォルダから正しく導出されること。
            var ctx = new DiffExecutionContext("/old", "/new", "/reports", false, false, false);

            Assert.Equal("/old", ctx.OldFolderAbsolutePath);
            Assert.Equal("/new", ctx.NewFolderAbsolutePath);
            Assert.Equal("/reports", ctx.ReportsFolderAbsolutePath);
            Assert.Equal(Path.Combine("/reports", "IL"), ctx.IlOutputFolderAbsolutePath);
            Assert.Equal(Path.Combine("/reports", "IL", "old"), ctx.IlOldFolderAbsolutePath);
            Assert.Equal(Path.Combine("/reports", "IL", "new"), ctx.IlNewFolderAbsolutePath);
        }

        // ── Network flags / ネットワークフラグ ────────────────────────

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, true)]
        public void Constructor_PreservesNetworkFlags(bool optimize, bool oldNetwork, bool newNetwork)
        {
            var ctx = new DiffExecutionContext("/old", "/new", "/reports", optimize, oldNetwork, newNetwork);

            Assert.Equal(optimize, ctx.OptimizeForNetworkShares);
            Assert.Equal(oldNetwork, ctx.DetectedNetworkOld);
            Assert.Equal(newNetwork, ctx.DetectedNetworkNew);
        }
    }
}
