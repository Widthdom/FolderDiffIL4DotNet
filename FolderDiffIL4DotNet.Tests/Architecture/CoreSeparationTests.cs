using System;
using System.Linq;
using FolderDiffIL4DotNet;
using FolderDiffIL4DotNet.Core.Console;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Core.Text;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Architecture
{
    /// <summary>
    /// Verifies that reusable utility types remain in the Core assembly and the legacy Utils namespace is not re-introduced.
    /// 再利用可能なユーティリティ型が Core アセンブリに存在し続け、レガシーの Utils 名前空間が再導入されていないことを検証します。
    /// </summary>
    public sealed class CoreSeparationTests
    {
        // All utility types must be defined in FolderDiffIL4DotNet.Core, not in the main assembly.
        // すべてのユーティリティ型はメインアセンブリではなく FolderDiffIL4DotNet.Core に定義されている必要があります。
        [Fact]
        public void UtilityTypes_AreDefinedInCoreAssembly()
        {
            var coreAssembly = typeof(FileComparer).Assembly;
            Type[] utilityTypes =
            [
                typeof(ConsoleBanner),
                typeof(ConsoleRenderCoordinator),
                typeof(ConsoleSpinner),
                typeof(DotNetDetector),
                typeof(FileComparer),
                typeof(FileSystemUtility),
                typeof(PathValidator),
                typeof(ProcessHelper),
                typeof(SystemInfo),
                typeof(TextSanitizer)
            ];

            Assert.Equal("FolderDiffIL4DotNet.Core", coreAssembly.GetName().Name);
            Assert.All(utilityTypes, utilityType => Assert.Same(coreAssembly, utilityType.Assembly));
        }

        // The main assembly must not re-introduce the legacy FolderDiffIL4DotNet.Utils namespace.
        // メインアセンブリにレガシーの FolderDiffIL4DotNet.Utils 名前空間が再導入されていないことを確認します。
        [Fact]
        public void MainAssembly_DoesNotDefineLegacyUtilsNamespace()
        {
            var mainAssembly = typeof(ProgramRunner).Assembly;

            Assert.DoesNotContain(
                mainAssembly.GetTypes(),
                static type => type.Namespace is not null && type.Namespace.StartsWith("FolderDiffIL4DotNet.Utils", StringComparison.Ordinal));
        }
    }
}
