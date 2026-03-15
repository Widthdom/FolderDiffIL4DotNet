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
    public sealed class CoreSeparationTests
    {
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
