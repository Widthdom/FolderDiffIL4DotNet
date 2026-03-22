using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Services.ILOutput;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="ILOutputService"/> covering IL line exclusion, precompute argument validation, and network-share optimization skip.
    /// <see cref="ILOutputService"/> のテスト。IL 行除外、事前計算の引数バリデーション、ネットワーク共有最適化時のスキップを検証します。
    /// </summary>
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
            var config = new ConfigSettingsBuilder
            {
                ILIgnoreLineContainingStrings = new List<string> { "buildserver", " buildpath ", "", "buildserver", "   " }
            }.Build();

            var result = InvokeGetNormalizedIlIgnoreContainingStrings(config);

            Assert.Equal(new[] { "buildserver", "buildpath" }, result);
        }

        [Theory]
        [InlineData("dotnet ildasm sample.dll (version: 9.0.0)", "dotnet-ildasm (version: 9.0.0)")]
        [InlineData("ilspycmd -il sample.dll (version: 8.2.1)", "ilspycmd (version: 8.2.1)")]
        [InlineData("dotnet-ildasm sample.dll", "dotnet-ildasm")]
        public void BuildToolAndVersionLabel_ReturnsExpectedLabel(string command, string expected)
        {
            var method = typeof(ILOutputService).GetMethod("BuildToolAndVersionLabel", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var result = method.Invoke(null, new object[] { command });

            Assert.Equal(expected, Assert.IsType<string>(result));
        }

        [Fact]
        public void BuildComparisonDisassemblerLabel_WhenLabelsMismatch_Throws()
        {
            var method = typeof(ILOutputService).GetMethod("BuildComparisonDisassemblerLabel", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var ex = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(null, new object[]
                {
                    "dotnet ildasm sample.dll (version: 1.0.0)",
                    "ilspycmd -il sample.dll (version: 2.0.0)"
                }));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        [Fact]
        public void BuildComparisonDisassemblerLabel_WhenOnlyOneSideHasLabel_ReturnsAvailableOne()
        {
            var method = typeof(ILOutputService).GetMethod("BuildComparisonDisassemblerLabel", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var result = method.Invoke(null, new object[] { null, "ilspycmd -il sample.dll (version: 8.2.1)" });
            Assert.Equal("ilspycmd (version: 8.2.1)", Assert.IsType<string>(result));
        }

        [Fact]
        public void BuildComparisonDisassemblerLabel_WhenBothMatch_IgnoresCaseAndReturnsLabel()
        {
            var method = typeof(ILOutputService).GetMethod("BuildComparisonDisassemblerLabel", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var result = method.Invoke(null, new object[]
            {
                "dotnet ildasm sample.dll (version: 1.0.0)",
                "DOTNET ILDASM sample.dll (version: 1.0.0)"
            });
            Assert.Equal("dotnet-ildasm (version: 1.0.0)", Assert.IsType<string>(result));
        }

        [Fact]
        public async Task PrecomputeAsync_WhenOptimizeForNetworkShares_ExitsWithoutThrowing()
        {
            var config = new ConfigSettingsBuilder
            {
                OptimizeForNetworkShares = true,
                EnableILCache = true,
                IgnoredExtensions = new(),
                TextFileExtensions = new()
            }.Build();

            var service = CreateILOutputService(config);
            await service.PrecomputeAsync(new[] { "/tmp/non-existent.dll" }, maxParallel: 0);
        }

        [Fact]
        public async Task PrecomputeAsync_WithInvalidMaxParallel_ThrowsWhenNotNetworkOptimized()
        {
            var config = new ConfigSettingsBuilder
            {
                OptimizeForNetworkShares = false,
                EnableILCache = false,
                IgnoredExtensions = new(),
                TextFileExtensions = new()
            }.Build();

            var service = CreateILOutputService(config);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.PrecomputeAsync(Array.Empty<string>(), maxParallel: 0));
        }

        [Fact]
        public void SplitAndFilterIlLines_CombinesSplitAndFilter_MatchesSplitThenWhereBehavior()
        {
            // Verify that the optimized single-pass method produces the same result as the
            // original Split → Where → ToList chain.
            var ilText = "// MVID: ABC\nclass Foo {\n}\n// MVID: DEF\n  return 0\n";
            var ignoreStrings = new List<string>();

            var method = typeof(ILOutputService).GetMethod("SplitAndFilterIlLines", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var result = (List<string>)method.Invoke(null, new object[] { ilText, false, ignoreStrings });

            // MVID lines should be excluded; non-MVID lines retained (including empty trailing line from final \n)
            Assert.DoesNotContain(result, line => line.StartsWith("// MVID:", StringComparison.Ordinal));
            Assert.Contains("class Foo {", result);
            Assert.Contains("}", result);
            Assert.Contains("  return 0", result);
        }

        [Fact]
        public void SplitAndFilterIlLines_WithConfiguredIgnoreStrings_ExcludesMatchingLines()
        {
            var ilText = "line1\nline2 buildserver\nline3\n";
            var ignoreStrings = new List<string> { "buildserver" };

            var method = typeof(ILOutputService).GetMethod("SplitAndFilterIlLines", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var result = (List<string>)method.Invoke(null, new object[] { ilText, true, ignoreStrings });

            Assert.Equal(new[] { "line1", "line3", "" }, result);
        }

        [Fact]
        public void PreSeedFileHash_WhenCacheIsNull_DoesNotThrow()
        {
            var config = new ConfigSettingsBuilder
            {
                OptimizeForNetworkShares = false,
                EnableILCache = false,
                IgnoredExtensions = new(),
                TextFileExtensions = new()
            }.Build();
            var service = CreateILOutputService(config);

            // Should be a no-op (ILCache is null) and not throw
            service.PreSeedFileHash("/some/path.dll", "a".PadRight(64, '0'));
        }

        [Fact]
        public async Task PrecomputeAsync_WithCacheDisabled_ReturnsWithoutThrowing()
        {
            var config = new ConfigSettingsBuilder
            {
                OptimizeForNetworkShares = false,
                EnableILCache = false,
                IgnoredExtensions = new(),
                TextFileExtensions = new()
            }.Build();
            var service = CreateILOutputService(config);
            await service.PrecomputeAsync(new[] { "/tmp/non-existent.dll" }, maxParallel: 1);
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

        private static ILOutputService CreateILOutputService(ConfigSettings config, string ilOldFolder = null, string ilNewFolder = null)
        {
            var logger = new LoggerService();
            var oldDir = ilOldFolder ?? Path.Combine(Path.GetTempPath(), "fd-iloutput-old-" + Guid.NewGuid().ToString("N"));
            var newDir = ilNewFolder ?? Path.Combine(Path.GetTempPath(), "fd-iloutput-new-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);

            var executionContext = new DiffExecutionContext(
                oldDir,
                newDir,
                Path.Combine(Path.GetTempPath(), "fd-iloutput-report-" + Guid.NewGuid().ToString("N")),
                optimizeForNetworkShares: config.OptimizeForNetworkShares,
                detectedNetworkOld: false,
                detectedNetworkNew: false);
            var resultLists = new FileDiffResultLists();
            var ilTextOutputService = new ILTextOutputService(executionContext, logger);
            var dotNetDisassembleService = new DotNetDisassembleService(config, ilCache: null, resultLists, logger, new DotNetDisassemblerCache(logger));
            return new ILOutputService(config, executionContext, ilTextOutputService, dotNetDisassembleService, ilCache: null, logger);
        }
    }
}
