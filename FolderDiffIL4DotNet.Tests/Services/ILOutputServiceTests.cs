using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
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
    public sealed partial class ILOutputServiceTests
    {
        [Fact]
        public void ShouldExcludeIlLine_ContainsConfiguredString_ExcludedOnlyWhenEnabled()
        {
            var line = ".custom instance void [buildserver] Foo::Bar()";
            var targets = new List<string> { "buildserver" };

            Assert.True(InvokeShouldExcludeIlLine(line, shouldIgnoreContainingStrings: true, targets));
            Assert.False(InvokeShouldExcludeIlLine(line, shouldIgnoreContainingStrings: false, targets));
        }

        [Fact]
        public void ConfiguredSubstringHelper_GetEffectiveIgnoreLineSubstrings_RemovesEmptyTrimAndDuplicates()
        {
            var config = new ConfigSettingsBuilder
            {
                ILIgnoreLineContainingStrings = new List<string> { "buildserver", " buildpath ", "", "buildserver", "   " }
            }.Build();

            var result = ILConfiguredSubstringHelper.GetEffectiveIgnoreLineSubstrings(config.ILIgnoreLineContainingStrings);

            Assert.Equal(new[] { "buildserver", "buildpath" }, result);
        }

        [Fact]
        public void ConfiguredSubstringHelper_GetEffectiveNormalizationSubstrings_PreservesMeaningfulWhitespace()
        {
            var configuredStrings = new List<string> { " token ", "", "   ", " token ", "token" };

            var result = ILConfiguredSubstringHelper.GetEffectiveNormalizationSubstrings(configuredStrings);

            Assert.Equal(new[] { " token ", "token" }, result);
        }

        [Fact]
        public void ConfiguredSubstringHelper_GetEffectiveNormalizationSubstrings_RejectsRuntimeCountAboveLimit()
        {
            var configuredStrings = Enumerable.Range(
                    0,
                    ConfigSettings.MaxILNormalizeContainingStringsCount + 1)
                .Select(index => $"normalization-{index}")
                .ToList();

            var exception = Assert.Throws<InvalidOperationException>(
                () => ILConfiguredSubstringHelper.GetEffectiveNormalizationSubstrings(configuredStrings));

            Assert.Contains("Invalid runtime configuration", exception.Message, StringComparison.Ordinal);
            Assert.Contains("at most 256 values", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ConfiguredSubstringHelper_GetEffectiveNormalizationSubstrings_RejectsRuntimeValueAboveLengthLimit()
        {
            var configuredStrings = new[]
            {
                new string('x', ConfigSettings.MaxILNormalizeContainingStringLength + 1)
            };

            var exception = Assert.Throws<InvalidOperationException>(
                () => ILConfiguredSubstringHelper.GetEffectiveNormalizationSubstrings(configuredStrings));

            Assert.Contains("Invalid runtime configuration", exception.Message, StringComparison.Ordinal);
            Assert.Contains("4096 Unicode characters or fewer", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("// MVID: 12345678-1234-1234-1234-123456789ABC", "// MVID: <nildiff:normalized:mvid>")]
        [InlineData("  // Method begins at Relative Virtual Address (RVA) 0x2050", "  // Method begins at Relative Virtual Address (RVA) 0x<nildiff:normalized:rva>")]
        [InlineData("// Code size 14 (0xe)", "// Code size <nildiff:normalized:code-size>")]
        [InlineData(".custom instance void class [System.Windows.Forms]System.Windows.Forms.AxHost/TypeLibraryTimeStampAttribute::.ctor(string) = ( 01 00 08 )", ".custom instance void class [System.Windows.Forms]System.Windows.Forms.AxHost/TypeLibraryTimeStampAttribute::.ctor(string) = ( <nildiff:normalized:type-library-timestamp>")]
        [InlineData(".custom instance void [System.Windows.Forms]System.Windows.Forms.AxHost/TypeLibraryTimeStampAttribute::.ctor(string) = ( 01 00 08 )", ".custom instance void [System.Windows.Forms]System.Windows.Forms.AxHost/TypeLibraryTimeStampAttribute::.ctor(string) = ( <nildiff:normalized:type-library-timestamp>")]
        public void NormalizeIlLine_KnownBuildValues_ReplacesOnlyVariableSuffix(string line, string expected)
        {
            Assert.Equal(expected, ILOutputService.NormalizeIlLine(line, Array.Empty<string>()));
        }

        [Fact]
        public void NormalizeIlLine_KnownPrefixInsideStringLiteral_DoesNotNormalize()
        {
            const string line = "ldstr \"// Code size 14 (0xe)\"";

            Assert.Equal(line, ILOutputService.NormalizeIlLine(line, Array.Empty<string>()));
        }

        [Fact]
        public void StreamingFilteredSequenceEqual_ConfiguredNormalization_PreservesSurroundingDifferences()
        {
            var normalizeStrings = new[] { "buildserver1_", "buildserver2_" };

            Assert.True(ILOutputService.StreamingFilteredSequenceEqual(
                new[] { "ldstr buildserver1_artifact" },
                new[] { "ldstr buildserver2_artifact" },
                false,
                Array.Empty<string>(),
                ilNormalizeContainingStrings: normalizeStrings));
            Assert.False(ILOutputService.StreamingFilteredSequenceEqual(
                new[] { "ldstr old-buildserver1_artifact" },
                new[] { "ldstr new-buildserver2_artifact" },
                false,
                Array.Empty<string>(),
                ilNormalizeContainingStrings: normalizeStrings));
        }

        [Fact]
        public void StreamingFilteredSequenceEqual_ConfiguredNormalization_DoesNotCollideWithLiteralMarker()
        {
            Assert.False(ILOutputService.StreamingFilteredSequenceEqual(
                new[] { "ldstr \"buildserver1_\"" },
                new[] { $"ldstr \"{ILOutputService.CONFIGURED_NORMALIZED_VALUE}\"" },
                false,
                Array.Empty<string>(),
                ilNormalizeContainingStrings: new[] { "buildserver1_" }));
        }

        [Fact]
        public void StreamingFilteredSequenceEqual_ConfiguredNormalization_DoesNotRewriteInsertedMarker()
        {
            Assert.True(ILOutputService.StreamingFilteredSequenceEqual(
                new[] { "ldstr foo" },
                new[] { "ldstr configured" },
                false,
                Array.Empty<string>(),
                ilNormalizeContainingStrings: new[] { "foo", "configured" }));
        }

        [Fact]
        public void NormalizeIlLine_ConfiguredReplacementAtPerLineLimit_Succeeds()
        {
            string line = new string('x', ILOutputService.MAX_CONFIGURED_NORMALIZATION_REPLACEMENTS_PER_LINE);

            string result = ILOutputService.NormalizeIlLine(line, new[] { "x" });

            Assert.Equal(
                checked(ILOutputService.MAX_CONFIGURED_NORMALIZATION_REPLACEMENTS_PER_LINE
                    * ILOutputService.CONFIGURED_NORMALIZED_VALUE.Length),
                result.Length);
            Assert.DoesNotContain('x', result);
        }

        [Fact]
        public void NormalizeIlLine_ConfiguredReplacementAbovePerLineLimit_ThrowsInvalidDataException()
        {
            string line = new string(
                'x',
                ILOutputService.MAX_CONFIGURED_NORMALIZATION_REPLACEMENTS_PER_LINE + 1);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => ILOutputService.NormalizeIlLine(line, new[] { "x" }));

            Assert.Equal(
                "ILNormalizeContainingStrings: configured IL normalization exceeded the per-line replacement limit of 65536 matches.",
                exception.Message);
        }

        [Fact]
        public void NormalizeIlLine_LaterRuleExceedsCumulativePerLineReplacementLimit_ThrowsInvalidDataException()
        {
            string line = new string(
                'x',
                ILOutputService.MAX_CONFIGURED_NORMALIZATION_REPLACEMENTS_PER_LINE)
                + "y";

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => ILOutputService.NormalizeIlLine(line, new[] { "x", "y" }));

            Assert.Equal(
                "ILNormalizeContainingStrings: configured IL normalization exceeded the per-line replacement limit of 65536 matches.",
                exception.Message);
        }

        [Fact]
        public void NormalizeIlLine_ConfiguredReplacementAtOutputLimit_Succeeds()
        {
            string line = new string(
                'z',
                ILOutputService.MAX_CONFIGURED_NORMALIZED_LINE_UTF16_CODE_UNITS
                    - ILOutputService.CONFIGURED_NORMALIZED_VALUE.Length)
                + "x";

            string result = ILOutputService.NormalizeIlLine(line, new[] { "x" });

            Assert.Equal(ILOutputService.MAX_CONFIGURED_NORMALIZED_LINE_UTF16_CODE_UNITS, result.Length);
        }

        [Fact]
        public void NormalizeIlLine_IntermediateOutputAboveLimitButFinalOutputBelowLimit_Succeeds()
        {
            const int shrinkingTargetLength = 100;
            string shrinkingTarget = new string('z', shrinkingTargetLength);
            string line = "a"
                + shrinkingTarget
                + new string(
                    'q',
                    ILOutputService.MAX_CONFIGURED_NORMALIZED_LINE_UTF16_CODE_UNITS
                        - shrinkingTargetLength
                        - 1);

            string result = ILOutputService.NormalizeIlLine(
                line,
                new[] { "a", shrinkingTarget });

            Assert.Equal(
                ILOutputService.MAX_CONFIGURED_NORMALIZED_LINE_UTF16_CODE_UNITS
                    - shrinkingTargetLength
                    - 1
                    + (2 * ILOutputService.CONFIGURED_NORMALIZED_VALUE.Length),
                result.Length);
            Assert.StartsWith(
                ILOutputService.CONFIGURED_NORMALIZED_VALUE
                    + ILOutputService.CONFIGURED_NORMALIZED_VALUE,
                result,
                StringComparison.Ordinal);
        }

        [Fact]
        public void NormalizeIlLine_ConfiguredReplacementAboveOutputLimit_ThrowsBeforeMaterializingOutput()
        {
            string line = new string(
                'z',
                ILOutputService.MAX_CONFIGURED_NORMALIZED_LINE_UTF16_CODE_UNITS
                    - ILOutputService.CONFIGURED_NORMALIZED_VALUE.Length
                    + 1)
                + "x";

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => ILOutputService.NormalizeIlLine(line, new[] { "x" }));

            Assert.Equal(
                "ILNormalizeContainingStrings: configured IL normalization would exceed the per-line output limit of 4194304 UTF-16 code units.",
                exception.Message);
        }

        [Fact]
        public void NormalizeIlLine_HugeLineWithoutConfiguredMatch_IsReturnedWithoutApplyingOutputLimit()
        {
            string line = new string(
                'z',
                ILOutputService.MAX_CONFIGURED_NORMALIZED_LINE_UTF16_CODE_UNITS + 1);

            string result = ILOutputService.NormalizeIlLine(line, new[] { "x" });

            Assert.Same(line, result);
        }

        [Fact]
        public void FilterIlLines_ConfiguredNormalization_SelectsFirstUnusedMarkerSuffix()
        {
            var result = ILOutputService.FilterIlLines(
                new[]
                {
                    $"ldstr {ILOutputService.CONFIGURED_NORMALIZED_VALUE}",
                    "ldstr <nildiff:normalized:configured-value-1>",
                    "ldstr normalize-me"
                },
                false,
                Array.Empty<string>(),
                ilNormalizeContainingStrings: new[] { "normalize-me" });

            Assert.Equal("ldstr <nildiff:normalized:configured-value-2>", result[2]);
        }

        [Fact]
        public void FilterIlLines_ConfiguredNormalization_WritesStablePlaceholder()
        {
            var result = ILOutputService.FilterIlLines(
                new[] { "ldstr buildserver1_artifact" },
                false,
                Array.Empty<string>(),
                ilNormalizeContainingStrings: new[] { "buildserver1_" });

            Assert.Equal(new[] { "ldstr <nildiff:normalized:configured-value>artifact" }, result);
        }

        [Fact]
        public void StreamingFilteredSequenceEqual_KnownBuildValues_NormalizesOutsideCreatorMode()
        {
            var oldLines = new[]
            {
                "// Method begins at Relative Virtual Address (RVA) 0x2050",
                "// Code size 14 (0xe)"
            };
            var newLines = new[]
            {
                "// Method begins at Relative Virtual Address (RVA) 0x3070",
                "// Code size 18 (0x12)"
            };

            Assert.True(ILOutputService.StreamingFilteredSequenceEqual(
                oldLines, newLines, false, Array.Empty<string>(), Array.Empty<string>()));
        }

        [Fact]
        public void StreamingFilteredSequenceEqual_IlspyMultilineTypeLibraryTimestamp_NormalizesWholeBlob()
        {
            var oldLines = new[]
            {
                Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX,
                "    01 00 08 6F 6C 64 2D 62 75 69 6C 64 00 00",
                ")",
                "ret"
            };
            var newLines = new[]
            {
                Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX,
                "    01 00 12 6E 65 77 2D 62 75 69 6C 64 2D 76 61 6C",
                "    75 65 00 00",
                ")",
                "ret"
            };

            Assert.True(ILOutputService.StreamingFilteredSequenceEqual(
                oldLines, newLines, false, Array.Empty<string>(), Array.Empty<string>()));
        }

        [Fact]
        public void FilterIlLines_IlspyMultilineTypeLibraryTimestamp_WritesSingleStableMarker()
        {
            var lines = new[]
            {
                "  " + Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX,
                "    01 00 08 62 75 69 6C 64 2D 69 64 00 00",
                "  )",
                "  ret"
            };

            var result = ILOutputService.FilterIlLines(
                lines, false, Array.Empty<string>(), Array.Empty<string>());

            var expected = new[]
            {
                "  " + Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX
                    + " " + ILOutputService.TYPE_LIBRARY_TIMESTAMP_NORMALIZED_VALUE,
                "  ret"
            };
            Assert.Equal(expected, result);
        }

        [Fact]
        public void StreamingFilteredSequenceEqual_UnterminatedIlspyTimestamp_DoesNotConsumeRemainingIl()
        {
            var oldLines = new[]
            {
                Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX,
                "    01 00 01",
                "ret"
            };
            var newLines = new[]
            {
                Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX,
                "    01 00 02",
                "ret"
            };

            Assert.False(ILOutputService.StreamingFilteredSequenceEqual(
                oldLines, newLines, false, Array.Empty<string>(), Array.Empty<string>()));
        }

        [Fact]
        public void StreamingFilteredSequenceEqual_InvalidIlspyTimestampPayload_DoesNotConsumeLaterClosingParenthesis()
        {
            var oldLines = new[]
            {
                Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX,
                "not a byte payload",
                ")",
                "ret"
            };
            var newLines = new[]
            {
                Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX,
                "different non-payload IL",
                ")",
                "ret"
            };

            Assert.False(ILOutputService.StreamingFilteredSequenceEqual(
                oldLines, newLines, false, Array.Empty<string>(), Array.Empty<string>()));
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
            var result = (List<string>)method.Invoke(null, new object[] { ilText, false, ignoreStrings, Array.Empty<string>() });

            // MVID lines should be retained with normalized values.
            Assert.Equal(2, result.Count(line => line == "// MVID: <nildiff:normalized:mvid>"));
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
            var result = (List<string>)method.Invoke(null, new object[] { ilText, true, ignoreStrings, Array.Empty<string>() });

            Assert.Equal(new[] { "line1", "line3", "" }, result);
        }

        [Fact]
        public void SplitAndFilterIlLines_IlspyMultilineTypeLibraryTimestamp_WritesSingleStableMarker()
        {
            string ilText = string.Join(
                '\n',
                Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX,
                "    01 00 08 62 75 69 6C 64 2D 69 64 00 00",
                ")",
                "ret");
            var method = typeof(ILOutputService).GetMethod(
                "SplitAndFilterIlLines",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);
            var result = (List<string>)method.Invoke(
                null,
                new object[] { ilText, false, Array.Empty<string>(), Array.Empty<string>() });

            Assert.Equal(
                new[]
                {
                    Constants.IL_ILSPY_TYPE_LIBRARY_TIMESTAMP_LINE_PREFIX
                        + " " + ILOutputService.TYPE_LIBRARY_TIMESTAMP_NORMALIZED_VALUE,
                    "ret"
                },
                result);
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

        // --- StreamingFilteredSequenceEqual tests / StreamingFilteredSequenceEqual テスト ---

        [Fact]
        [Trait("Category", "Unit")]
        public void StreamingFilteredSequenceEqual_IdenticalLines_ReturnsTrue()
        {
            var lines1 = new List<string> { "class Foo {", "}", "  return 0" };
            var lines2 = new List<string> { "class Foo {", "}", "  return 0" };

            var result = ILOutputService.StreamingFilteredSequenceEqual(lines1, lines2, false, new List<string>(), Array.Empty<string>());

            Assert.True(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void StreamingFilteredSequenceEqual_DifferentLines_ReturnsFalse()
        {
            var lines1 = new List<string> { "class Foo {", "  return 0" };
            var lines2 = new List<string> { "class Foo {", "  return 1" };

            var result = ILOutputService.StreamingFilteredSequenceEqual(lines1, lines2, false, new List<string>(), Array.Empty<string>());

            Assert.False(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void StreamingFilteredSequenceEqual_SkipsExcludedMvidLines()
        {
            var lines1 = new List<string> { "// MVID: ABC", "class Foo {", "}" };
            var lines2 = new List<string> { "// MVID: XYZ", "class Foo {", "}" };

            var result = ILOutputService.StreamingFilteredSequenceEqual(lines1, lines2, false, new List<string>(), Array.Empty<string>());

            Assert.True(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void StreamingFilteredSequenceEqual_SkipsConfiguredIgnoreStrings()
        {
            var lines1 = new List<string> { "class Foo {", "line with buildserver", "}" };
            var lines2 = new List<string> { "class Foo {", "different buildserver line", "}" };
            var ignoreStrings = new List<string> { "buildserver" };

            var result = ILOutputService.StreamingFilteredSequenceEqual(lines1, lines2, true, ignoreStrings, Array.Empty<string>());

            Assert.True(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void StreamingFilteredSequenceEqual_DifferentLengthsAfterFilter_ReturnsFalse()
        {
            var lines1 = new List<string> { "class Foo {", "}" };
            var lines2 = new List<string> { "class Foo {", "}", "extra line" };

            var result = ILOutputService.StreamingFilteredSequenceEqual(lines1, lines2, false, new List<string>(), Array.Empty<string>());

            Assert.False(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void StreamingFilteredSequenceEqual_BothEmpty_ReturnsTrue()
        {
            var result = ILOutputService.StreamingFilteredSequenceEqual(
                new List<string>(), new List<string>(), false, new List<string>(), Array.Empty<string>());

            Assert.True(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void StreamingFilteredSequenceEqual_DifferentMvidLineCounts_ReturnsFalse()
        {
            var lines1 = new List<string> { "// MVID: A", "// MVID: B" };
            var lines2 = new List<string> { "// MVID: X" };

            var result = ILOutputService.StreamingFilteredSequenceEqual(lines1, lines2, false, new List<string>(), Array.Empty<string>());

            Assert.False(result);
        }

        // --- BlockAwareSequenceEqual tests / ブロック単位比較テスト ---

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_IdenticalLines_ReturnsTrue()
        {
            var lines = new List<string> { ".assembly test {}", ".method void Foo() {", "  ret", "}" };

            Assert.True(ILOutputService.BlockAwareSequenceEqual(lines, new List<string>(lines)));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_ReorderedMethods_ReturnsTrue()
        {
            var lines1 = new List<string>
            {
                ".assembly test {}",
                ".method public void Foo() cil managed",
                "{",
                "  ret",
                "}",
                ".method public void Bar() cil managed",
                "{",
                "  nop",
                "  ret",
                "}"
            };
            var lines2 = new List<string>
            {
                ".assembly test {}",
                ".method public void Bar() cil managed",
                "{",
                "  nop",
                "  ret",
                "}",
                ".method public void Foo() cil managed",
                "{",
                "  ret",
                "}"
            };

            Assert.True(ILOutputService.BlockAwareSequenceEqual(lines1, lines2));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_DifferentMethodBody_ReturnsFalse()
        {
            var lines1 = new List<string>
            {
                ".method public void Foo() cil managed",
                "{",
                "  ldc.i4.0",
                "  ret",
                "}"
            };
            var lines2 = new List<string>
            {
                ".method public void Foo() cil managed",
                "{",
                "  ldc.i4.1",
                "  ret",
                "}"
            };

            Assert.False(ILOutputService.BlockAwareSequenceEqual(lines1, lines2));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_DifferentBlockCount_ReturnsFalse()
        {
            var lines1 = new List<string>
            {
                ".method public void Foo() cil managed",
                "{",
                "  ret",
                "}"
            };
            var lines2 = new List<string>
            {
                ".method public void Foo() cil managed",
                "{",
                "  ret",
                "}",
                ".method public void Bar() cil managed",
                "{",
                "  ret",
                "}"
            };

            Assert.False(ILOutputService.BlockAwareSequenceEqual(lines1, lines2));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_BothEmpty_ReturnsTrue()
        {
            Assert.True(ILOutputService.BlockAwareSequenceEqual(new List<string>(), new List<string>()));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_DuplicateMethods_Reordered_ReturnsTrue()
        {
            // Two identical methods in different order — multiset comparison must handle duplicates
            // 同一メソッドが2つ、順序が異なる — マルチセット比較で重複を正しく処理
            var lines1 = new List<string>
            {
                ".method public void Foo() cil managed",
                "{",
                "  ret",
                "}",
                ".method public void Foo() cil managed",
                "{",
                "  ret",
                "}"
            };
            var lines2 = new List<string>(lines1); // Same content, same order

            Assert.True(ILOutputService.BlockAwareSequenceEqual(lines1, lines2));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_ContentSwappedBetweenMethods_ReturnsFalse()
        {
            // Two methods swap their bodies — signature-aware comparison detects this
            // 2つのメソッドのボディが入れ替わった場合 — シグネチャ対応比較で検知
            var lines1 = new List<string>
            {
                ".method public void Foo() cil managed",
                "{",
                "  ldc.i4.0",
                "  ret",
                "}",
                ".method public void Bar() cil managed",
                "{",
                "  ldc.i4.1",
                "  ret",
                "}"
            };
            var lines2 = new List<string>
            {
                ".method public void Foo() cil managed",
                "{",
                "  ldc.i4.1",
                "  ret",
                "}",
                ".method public void Bar() cil managed",
                "{",
                "  ldc.i4.0",
                "  ret",
                "}"
            };

            Assert.False(ILOutputService.BlockAwareSequenceEqual(lines1, lines2));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_OneMethodContentChangedToMatchAnother_ReturnsFalse()
        {
            // MethodA's content changes to match MethodB — must be detected as different
            // MethodA の内容が MethodB と同じになった場合 — 差分として検知すべき
            var lines1 = new List<string>
            {
                ".method public void Foo() cil managed",
                "{",
                "  ldc.i4.0",
                "  ret",
                "}",
                ".method public void Bar() cil managed",
                "{",
                "  ldc.i4.1",
                "  ret",
                "}"
            };
            var lines2 = new List<string>
            {
                ".method public void Foo() cil managed",
                "{",
                "  ldc.i4.1",
                "  ret",
                "}",
                ".method public void Bar() cil managed",
                "{",
                "  ldc.i4.1",
                "  ret",
                "}"
            };

            Assert.False(ILOutputService.BlockAwareSequenceEqual(lines1, lines2));
        }

        // --- Configured IL substring validation tests / 設定 IL 部分文字列検証テスト ---

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILIgnoreContainingStrings_NullInput_ReturnsEmpty()
        {
            var result = ILOutputService.ValidateILIgnoreContainingStrings(null!);
            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILIgnoreContainingStrings_EmptyInput_ReturnsEmpty()
        {
            var result = ILOutputService.ValidateILIgnoreContainingStrings(new List<string>());
            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILIgnoreContainingStrings_AllLongStrings_ReturnsEmpty()
        {
            var result = ILOutputService.ValidateILIgnoreContainingStrings(new List<string> { "buildserver", "// MVID" });
            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILIgnoreContainingStrings_ShortString_ReturnsWarning()
        {
            var result = ILOutputService.ValidateILIgnoreContainingStrings(new List<string> { "ret" });
            Assert.Single(result);
            Assert.Contains("ret", result[0]);
            Assert.Contains("3 chars", result[0]);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILIgnoreContainingStrings_MixedLengths_ReturnsWarningsForShortOnly()
        {
            var result = ILOutputService.ValidateILIgnoreContainingStrings(new List<string> { "ab", "buildserver", "x", "longstring" });
            Assert.Equal(2, result.Count);
            Assert.Contains(result, w => w.Contains("\"ab\""));
            Assert.Contains(result, w => w.Contains("\"x\""));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILIgnoreContainingStrings_ExactlyMinLength_NoWarning()
        {
            // 4 chars is the minimum length (IL_FILTER_STRING_MIN_LENGTH = 4), so should pass
            // 4 文字は最小長（IL_FILTER_STRING_MIN_LENGTH = 4）なので警告なし
            var result = ILOutputService.ValidateILIgnoreContainingStrings(new List<string> { "abcd" });
            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILNormalizeContainingStrings_ShortString_ReturnsNormalizationWarning()
        {
            var result = ILOutputService.ValidateILNormalizeContainingStrings(new List<string> { "abc", "buildserver" });

            string warning = Assert.Single(result);
            Assert.Contains("ILNormalizeContainingStrings", warning);
            Assert.Contains("\"abc\"", warning);
            Assert.Contains("3 chars", warning);
            Assert.Contains("normalize legitimate IL content", warning);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILNormalizeContainingStrings_SupplementaryCharacters_UsesRuneCount()
        {
            var result = ILOutputService.ValidateILNormalizeContainingStrings(
                new List<string> { "\U0001F600\U0001F603" });

            string warning = Assert.Single(result);
            Assert.Contains("&#128512;&#128515;", warning, StringComparison.Ordinal);
            Assert.Contains("2 chars", warning, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILIgnoreContainingStrings_SupplementaryCharacters_UsesRuneCount()
        {
            var result = ILOutputService.ValidateILIgnoreContainingStrings(
                new List<string> { "\U0001F600\U0001F603" });

            string warning = Assert.Single(result);
            Assert.Contains("&#128512;&#128515;", warning, StringComparison.Ordinal);
            Assert.Contains("2 chars", warning, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILNormalizeContainingStrings_UnsafeValues_UsesVisibleCommonMarkSafeEscapes()
        {
            const string unsafeValue = "safe\r\n\t\\\"#*_[x](y)<z>\u001B\u200D\U0001F600";
            const string escapedValue = "safe&#92;r&#92;n&#92;t&#92;&#92;&#34;&#35;&#42;&#95;&#91;x&#93;&#40;y&#41;&#60;z&#62;&#92;u001B&#92;u200D&#128512;";
            var result = ILOutputService.ValidateILNormalizeContainingStrings(
                new List<string> { unsafeValue, unsafeValue, $"prefix{unsafeValue}suffix" });

            Assert.Equal(2, result.Count);
            Assert.Contains(result, warning => warning.Contains("configured more than once", StringComparison.Ordinal));
            Assert.Contains(result, warning => warning.Contains("overlap by containment", StringComparison.Ordinal));
            Assert.All(result, warning =>
            {
                Assert.Contains(escapedValue, warning, StringComparison.Ordinal);
                Assert.DoesNotContain("\r", warning, StringComparison.Ordinal);
                Assert.DoesNotContain("\n", warning, StringComparison.Ordinal);
                Assert.DoesNotContain("\t", warning, StringComparison.Ordinal);
                Assert.DoesNotContain("\u001B", warning, StringComparison.Ordinal);
                Assert.DoesNotContain("\u200D", warning, StringComparison.Ordinal);
                Assert.DoesNotContain("\\", warning, StringComparison.Ordinal);
                Assert.DoesNotContain("\U0001F600", warning, StringComparison.Ordinal);
            });
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILNormalizeContainingStrings_UnpairedSurrogatesAndCombiningMarks_PreservesVisibleCodePoints()
        {
            const string unsafeValue = "a\uD800b\uDC00c\uFFFDd\u0301e\uFE0F";
            const string escapedValue = "a&#92;uD800b&#92;uDC00c&#65533;d&#92;u0301e&#92;uFE0F";

            var result = ILOutputService.ValidateILNormalizeContainingStrings(
                new List<string> { unsafeValue, unsafeValue });

            string warning = Assert.Single(result);
            Assert.Contains(escapedValue, warning, StringComparison.Ordinal);
            Assert.DoesNotContain("\uD800", warning, StringComparison.Ordinal);
            Assert.DoesNotContain("\uDC00", warning, StringComparison.Ordinal);
            Assert.DoesNotContain("\uFFFD", warning, StringComparison.Ordinal);
            Assert.DoesNotContain("\u0301", warning, StringComparison.Ordinal);
            Assert.DoesNotContain("\uFE0F", warning, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILNormalizeContainingStrings_DuplicateAndContainment_ReturnsDistinctWarnings()
        {
            var result = ILOutputService.ValidateILNormalizeContainingStrings(
                new List<string> { "buildserver1_", "buildserver1_artifact", "buildserver1_", "independent" });

            Assert.Equal(2, result.Count);
            Assert.Contains(result, warning => warning.Contains("configured more than once", StringComparison.Ordinal));
            Assert.Contains(result, warning => warning.Contains("overlap by containment", StringComparison.Ordinal)
                && warning.Contains("listed order", StringComparison.Ordinal)
                && warning.Contains("inserted markers are not reprocessed", StringComparison.Ordinal));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILNormalizeContainingStrings_NonOverlappingValues_ReturnsNoRelationshipWarning()
        {
            var result = ILOutputService.ValidateILNormalizeContainingStrings(
                new List<string> { "buildserver1_", "buildserver2_", @"\temp\develop\" });

            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILNormalizeContainingStrings_ManyOverlaps_CapsDetailsAndSummarizesSuppressedWarnings()
        {
            var configuredStrings = Enumerable.Range(4, ConfigSettings.MaxILNormalizeContainingStringsCount)
                .Select(length => new string('a', length))
                .ToList();

            var result = ILOutputService.ValidateILNormalizeContainingStrings(configuredStrings);

            Assert.Equal(101, result.Count);
            Assert.All(result.Take(100), warning => Assert.Contains("overlap by containment", warning, StringComparison.Ordinal));
            Assert.Contains("first 100 safety warning details", result[100], StringComparison.Ordinal);
            Assert.Contains("32540 additional warnings were suppressed", result[100], StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ValidateILNormalizeContainingStrings_ManyShortValues_SummaryNamesShortValues()
        {
            var configuredStrings = Enumerable.Range(0, ConfigSettings.MaxILNormalizeContainingStringsCount)
                .Select(index => index.ToString("X3", System.Globalization.CultureInfo.InvariantCulture))
                .ToList();

            var result = ILOutputService.ValidateILNormalizeContainingStrings(configuredStrings);

            Assert.Equal(101, result.Count);
            Assert.All(result.Take(100), warning => Assert.Contains("is very short", warning, StringComparison.Ordinal));
            Assert.Contains("156 additional warnings were suppressed", result[100], StringComparison.Ordinal);
            Assert.Contains("Reduce short, duplicate, or overlapping values", result[100], StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void NormalizeIlLine_OverlappingConfiguredValues_FollowsListOrder()
        {
            string shorterFirst = ILOutputService.NormalizeIlLine("ldstr abcd", new[] { "abc", "abcd" });
            string longerFirst = ILOutputService.NormalizeIlLine("ldstr abcd", new[] { "abcd", "abc" });

            Assert.Equal($"ldstr {ILOutputService.CONFIGURED_NORMALIZED_VALUE}d", shorterFirst);
            Assert.Equal($"ldstr {ILOutputService.CONFIGURED_NORMALIZED_VALUE}", longerFirst);
        }

        // --- FilterIlLines tests / FilterIlLines テスト ---

        [Fact]
        [Trait("Category", "Unit")]
        public void FilterIlLines_NormalizesMvidAndRemovesConfiguredStrings()
        {
            var lines = new List<string> { "// MVID: ABC", "class Foo {", "buildpath stuff", "}" };
            var ignoreStrings = new List<string> { "buildpath" };

            var result = ILOutputService.FilterIlLines(lines, true, ignoreStrings, Array.Empty<string>());

            Assert.Equal(new[] { "// MVID: <nildiff:normalized:mvid>", "class Foo {", "}" }, result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void FilterIlLines_NoExclusions_ReturnsAllLines()
        {
            var lines = new List<string> { "class Foo {", "  return 0", "}" };

            var result = ILOutputService.FilterIlLines(lines, false, new List<string>(), Array.Empty<string>());

            Assert.Equal(lines, result);
        }

        // --- SplitToLines tests / SplitToLines テスト ---

        [Fact]
        [Trait("Category", "Unit")]
        public void SplitToLines_BasicNewlines_ReturnsExpectedLines()
        {
            var text = "line1\nline2\nline3";

            var result = DotNetDisassembleService.SplitToLines(text);

            Assert.Equal(new[] { "line1", "line2", "line3" }, result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SplitToLines_CarriageReturnNewlines_ReturnsExpectedLines()
        {
            var text = "line1\r\nline2\r\nline3";

            var result = DotNetDisassembleService.SplitToLines(text);

            Assert.Equal(new[] { "line1", "line2", "line3" }, result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SplitToLines_EmptyString_ReturnsEmptyList()
        {
            Assert.Empty(DotNetDisassembleService.SplitToLines(string.Empty));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SplitToLines_NullString_ReturnsEmptyList()
        {
            Assert.Empty(DotNetDisassembleService.SplitToLines(null!));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SplitToLines_TrailingNewline_DoesNotAppendEmptyLine()
        {
            // StringReader.ReadLine returns null after the last newline, not an empty string.
            // StringReader.ReadLine は最後の改行の後に null を返し、空文字列は返さない。
            var text = "line1\nline2\n";

            var result = DotNetDisassembleService.SplitToLines(text);

            Assert.Equal(new[] { "line1", "line2" }, result);
        }

        // --- StreamingFilteredSequenceEqual matches SplitAndFilterIlLines + SequenceEqual / ストリーミング比較が従来手法と一致 ---

        [Fact]
        [Trait("Category", "Unit")]
        public void StreamingFilteredSequenceEqual_MatchesSplitAndFilterBehavior()
        {
            // Verify the streaming comparison produces the same result as the legacy
            // SplitAndFilterIlLines + SequenceEqual approach.
            // ストリーミング比較が従来の SplitAndFilterIlLines + SequenceEqual と同一結果を返すことを検証。
            var ilText1 = "// MVID: ABC\nclass Foo {\n}\n// MVID: DEF\n  return 0\n";
            var ilText2 = "// MVID: XYZ\nclass Foo {\n}\n// MVID: GHI\n  return 0\n";

            var splitAndFilter = typeof(ILOutputService).GetMethod("SplitAndFilterIlLines", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(splitAndFilter);

            var ignoreStrings = new List<string>();
            var legacy1 = (List<string>)splitAndFilter.Invoke(null, new object[] { ilText1, false, ignoreStrings, Array.Empty<string>() })!;
            var legacy2 = (List<string>)splitAndFilter.Invoke(null, new object[] { ilText2, false, ignoreStrings, Array.Empty<string>() })!;
            bool legacyResult = legacy1.SequenceEqual(legacy2);

            var lines1 = DotNetDisassembleService.SplitToLines(ilText1);
            var lines2 = DotNetDisassembleService.SplitToLines(ilText2);
            bool streamingResult = ILOutputService.StreamingFilteredSequenceEqual(lines1, lines2, false, ignoreStrings, Array.Empty<string>());

            Assert.Equal(legacyResult, streamingResult);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void StreamingFilteredSequenceEqual_DifferentMvidValues_NormalizesMvidLines()
        {
            var lines1 = new List<string> { "// MVID: ABC", "class Foo {", "}" };
            var lines2 = new List<string> { "// MVID: XYZ", "class Foo {", "}" };
            var result = ILOutputService.StreamingFilteredSequenceEqual(lines1, lines2, false, new List<string>(), Array.Empty<string>());
            Assert.True(result);
        }

        // --- Whitespace trimming tests / 空白トリミングテスト ---

        [Fact]
        [Trait("Category", "Unit")]
        public void StreamingFilteredSequenceEqual_WhitespaceDifferences_ReturnsTrue()
        {
            // Lines differing only in leading/trailing whitespace should be treated as equal.
            // 先頭・末尾空白のみ異なる行は等価として扱われるべき。
            var lines1 = new List<string> { "  .method public void Foo()", "    ldarg.0", "    ret" };
            var lines2 = new List<string> { ".method public void Foo()", "      ldarg.0", "  ret" };
            var result = ILOutputService.StreamingFilteredSequenceEqual(lines1, lines2, false, new List<string>(), Array.Empty<string>());
            Assert.True(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void StreamingFilteredSequenceEqual_ContentDiffBeyondWhitespace_ReturnsFalse()
        {
            // Lines differing in actual content (not just whitespace) must still be detected.
            // 実際の内容が異なる行（空白だけでなく）は引き続き検出されるべき。
            var lines1 = new List<string> { "  call void Foo()" };
            var lines2 = new List<string> { "  call void Bar()" };
            var result = ILOutputService.StreamingFilteredSequenceEqual(lines1, lines2, false, new List<string>(), Array.Empty<string>());
            Assert.False(result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BlockAwareSequenceEqual_IndentDifferences_ReturnsTrue()
        {
            // Block-aware comparison should also ignore indentation differences.
            // ブロック単位比較でもインデント差異を無視すべき。
            var lines1 = new List<string>
            {
                ".method public void Foo()",
                "{",
                "    ldarg.0",
                "    ret",
                "}"
            };
            var lines2 = new List<string>
            {
                "  .method public void Foo()",
                "  {",
                "      ldarg.0",
                "      ret",
                "  }"
            };
            Assert.True(ILOutputService.BlockAwareSequenceEqual(lines1, lines2));
        }

        private static ILOutputService CreateILOutputService(ConfigSettings config, string? ilOldFolder = null, string? ilNewFolder = null)
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
