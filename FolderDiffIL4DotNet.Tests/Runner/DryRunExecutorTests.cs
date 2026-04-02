using System;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Runner
{
    /// <summary>
    /// Unit tests for <see cref="DryRunExecutor"/>.
    /// <see cref="DryRunExecutor"/> のユニットテスト。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class DryRunExecutorTests
    {
        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DryRunExecutor(null!));
        }

        [Fact]
        public void Constructor_ValidLogger_DoesNotThrow()
        {
            var logger = new TestLogger();
            var executor = new DryRunExecutor(logger);
            Assert.NotNull(executor);
        }
    }
}
