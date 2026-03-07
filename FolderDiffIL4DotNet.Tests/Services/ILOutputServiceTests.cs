using System.Collections.Generic;
using System.Reflection;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class ILOutputServiceTests
    {
        [Fact]
        public void ShouldExcludeIlLine_MvidPrefix_IsAlwaysExcluded()
        {
            var result = InvokeShouldExcludeIlLine("// MVID: 1234", shouldIgnoreContainingStrings: false, new List<string>());
            Assert.True(result);
        }

        [Fact]
        public void ShouldExcludeIlLine_ContainsConfiguredString_ExcludedOnlyWhenEnabled()
        {
            var line = ".custom instance void [buildserver] Foo::Bar()";
            var targets = new List<string> { "buildserver" };

            Assert.True(InvokeShouldExcludeIlLine(line, shouldIgnoreContainingStrings: true, targets));
            Assert.False(InvokeShouldExcludeIlLine(line, shouldIgnoreContainingStrings: false, targets));
        }

        [Fact]
        public void GetNormalizedIlIgnoreContainingStrings_RemovesEmptyTrimAndDuplicates()
        {
            var config = new ConfigSettings
            {
                ILIgnoreLineContainingStrings = new List<string> { "buildserver", " buildpath ", "", "buildserver", "   " }
            };

            var result = InvokeGetNormalizedIlIgnoreContainingStrings(config);

            Assert.Equal(new[] { "buildserver", "buildpath" }, result);
        }

        private static bool InvokeShouldExcludeIlLine(string line, bool shouldIgnoreContainingStrings, IReadOnlyCollection<string> ilIgnoreContainingStrings)
        {
            var method = typeof(ILOutputService).GetMethod("ShouldExcludeIlLine", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var result = method.Invoke(null, new object[] { line, shouldIgnoreContainingStrings, ilIgnoreContainingStrings });
            return Assert.IsType<bool>(result);
        }

        private static List<string> InvokeGetNormalizedIlIgnoreContainingStrings(ConfigSettings config)
        {
            var method = typeof(ILOutputService).GetMethod("GetNormalizedIlIgnoreContainingStrings", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var result = method.Invoke(null, new object[] { config });
            return Assert.IsType<List<string>>(result);
        }
    }
}
