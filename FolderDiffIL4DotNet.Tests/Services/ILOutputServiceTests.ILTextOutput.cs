using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.ILOutput;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Additional tests for <see cref="ILTextOutputService"/> behavior inside <see cref="ILOutputServiceTests"/>.
    /// <see cref="ILOutputServiceTests"/> 内で扱う <see cref="ILTextOutputService"/> の追加テスト。
    /// </summary>
    public sealed partial class ILOutputServiceTests
    {
        [Fact]
        public async Task WriteFullIlTextsAsync_WhenExistingOutputsAreReadOnly_OverwritesThem()
        {
            var rootDir = Path.Combine(Path.GetTempPath(), "fd-iltext-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootDir);

            try
            {
                var oldDir = Path.Combine(rootDir, "old");
                var newDir = Path.Combine(rootDir, "new");
                var reportDir = Path.Combine(rootDir, "reports");
                Directory.CreateDirectory(oldDir);
                Directory.CreateDirectory(newDir);
                Directory.CreateDirectory(reportDir);

                var context = new DiffExecutionContext(
                    oldDir, newDir, reportDir,
                    optimizeForNetworkShares: false,
                    detectedNetworkOld: false,
                    detectedNetworkNew: false);
                Directory.CreateDirectory(context.IlOldFolderAbsolutePath);
                Directory.CreateDirectory(context.IlNewFolderAbsolutePath);

                var service = new ILTextOutputService(context, new TestLogger());

                await service.WriteFullIlTextsAsync("lib/app.dll", new[] { "old-v1" }, new[] { "new-v1" });

                var oldOutputPath = Assert.Single(Directory.GetFiles(context.IlOldFolderAbsolutePath));
                var newOutputPath = Assert.Single(Directory.GetFiles(context.IlNewFolderAbsolutePath));
                Assert.Contains("old-v1", File.ReadAllText(oldOutputPath), StringComparison.Ordinal);
                Assert.Contains("new-v1", File.ReadAllText(newOutputPath), StringComparison.Ordinal);

                await service.WriteFullIlTextsAsync("lib/app.dll", new[] { "old-v2" }, new[] { "new-v2" });

                Assert.Equal(oldOutputPath, Assert.Single(Directory.GetFiles(context.IlOldFolderAbsolutePath)));
                Assert.Equal(newOutputPath, Assert.Single(Directory.GetFiles(context.IlNewFolderAbsolutePath)));
                Assert.Contains("old-v2", File.ReadAllText(oldOutputPath), StringComparison.Ordinal);
                Assert.DoesNotContain("old-v1", File.ReadAllText(oldOutputPath), StringComparison.Ordinal);
                Assert.Contains("new-v2", File.ReadAllText(newOutputPath), StringComparison.Ordinal);
                Assert.DoesNotContain("new-v1", File.ReadAllText(newOutputPath), StringComparison.Ordinal);
            }
            finally
            {
                TryDeleteDirectory(rootDir);
            }
        }

        [Fact]
        public async Task WriteFullIlTextsAsync_WhenWriteFails_LogsFileContextAndRethrows()
        {
            var rootDir = Path.Combine(Path.GetTempPath(), "fd-iltext-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootDir);

            try
            {
                var oldDir = Path.Combine(rootDir, "old");
                var newDir = Path.Combine(rootDir, "new");
                var reportDir = Path.Combine(rootDir, "reports");
                Directory.CreateDirectory(oldDir);
                Directory.CreateDirectory(newDir);
                Directory.CreateDirectory(reportDir);

                var context = new DiffExecutionContext(
                    oldDir, newDir, reportDir,
                    optimizeForNetworkShares: false,
                    detectedNetworkOld: false,
                    detectedNetworkNew: false);
                var logger = new TestLogger();
                var service = new ILTextOutputService(context, logger);

                var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
                    service.WriteFullIlTextsAsync("lib/app.dll", new[] { "old-il" }, new[] { "new-il" }));

                var error = Assert.Single(logger.Entries, entry => entry.LogLevel == AppLogLevel.Error);
                Assert.Contains("lib/app.dll", error.Message, StringComparison.Ordinal);
                Assert.Contains(nameof(DirectoryNotFoundException), error.Message, StringComparison.Ordinal);
                Assert.NotNull(error.Exception);
                Assert.Same(exception, error.Exception);
            }
            finally
            {
                TryDeleteDirectory(rootDir);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup errors / クリーンアップエラーを無視
            }
        }
    }
}
