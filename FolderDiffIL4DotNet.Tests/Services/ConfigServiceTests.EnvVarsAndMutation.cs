/// <summary>
/// Partial class containing environment variable override and mutation-testing tests for ConfigService.
/// ConfigService の環境変数オーバーライドおよびミューテーションテストを含むパーシャルクラス。
/// </summary>

using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public partial class ConfigServiceTests
    {
        // ── Environment variable override tests / 環境変数オーバーライドテスト ──

        [Fact]
        public async Task LoadConfigBuilderAsync_EnvVarOverridesIntProperty_AppliesOverride()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_MAXPARALLELISM", "8") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();

                        Assert.Equal(8, builder.MaxParallelism);
                    });
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_EnvVarOverridesBoolProperty_TrueValue_AppliesOverride()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_ENABLEILCACHE", "false") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();

                        Assert.False(builder.EnableILCache);
                    });
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_EnvVarOverridesBoolProperty_OneZero_AppliesOverride()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_SHOULDGENERATEHTMLREPORT", "0") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();

                        Assert.False(builder.ShouldGenerateHtmlReport);
                    });
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_EnvVarOverridesShouldIncludeReviewChecklist_AppliesOverride()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_SHOULDINCLUDEREVIEWCHECKLIST", "true") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();

                        Assert.True(builder.ShouldIncludeReviewChecklist);
                    });
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_EnvVarOverridesStringProperty_AppliesOverride()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_ILCACHEDIRECTORYABSOLUTEPATH", "/tmp/custom-il-cache") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();

                        Assert.Equal("/tmp/custom-il-cache", builder.ILCacheDirectoryAbsolutePath);
                    });
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_EnvVarOverridesJsonValue_EnvVarWins()
        {
            const string json = """{ "MaxParallelism": 4 }""";

            await WithConfigFileAsync(json, async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_MAXPARALLELISM", "16") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();

                        Assert.Equal(16, builder.MaxParallelism);
                    });
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_EnvVarWithInvalidIntValue_IsIgnored()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_MAXPARALLELISM", "not-a-number") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();

                        Assert.Equal(0, builder.MaxParallelism);  // default
                    });
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_EnvVarWithInvalidBoolValue_IsIgnored()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_ENABLEILCACHE", "yes") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();

                        Assert.True(builder.EnableILCache);  // default unchanged
                    });
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_EnvVarOverridesInvalidValue_ValidationCatchesIt()
        {
            // Env var sets an invalid value (MaxLogGenerations=0), validation catches it
            // 環境変数が不正値（MaxLogGenerations=0）を設定し、バリデーションがそれを検出する
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_MAXLOGGENERATIONS", "0") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();
                        var result = builder.Validate();

                        Assert.False(result.IsValid);
                        Assert.Contains(result.Errors, e => e.Contains("MaxLogGenerations", StringComparison.Ordinal));
                    });
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_EnvVarOverridesILCacheMaxMemoryMegabytes_Zero_AppliesOverride()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_ILCACHEMAXMEMORYMEGABYTES", "0") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();

                        Assert.Equal(0, builder.ILCacheMaxMemoryMegabytes);
                    });
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_EnvVarOverridesILCacheMaxMemoryMegabytes_Negative_ValidationCatchesIt()
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_ILCACHEMAXMEMORYMEGABYTES", "-1") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();
                        var result = builder.Validate();

                        Assert.Equal(-1, builder.ILCacheMaxMemoryMegabytes);
                        Assert.False(result.IsValid);
                        Assert.Contains(result.Errors, e => e.Contains("ILCacheMaxMemoryMegabytes", StringComparison.Ordinal));
                    });
            });
        }

        [Fact]
        public void ApplyEnvironmentVariableOverrides_CaseInsensitiveBool_TrueVariants()
        {
            foreach (var trueVal in new[] { "true", "TRUE", "True", "1" })
            {
                var builder = new ConfigSettingsBuilder { ShouldGenerateHtmlReport = false };
                WithEnvVar("FOLDERDIFF_SHOULDGENERATEHTMLREPORT", trueVal,
                    () => ConfigService.ApplyEnvironmentVariableOverrides(builder));
                Assert.True(builder.ShouldGenerateHtmlReport, $"Expected true for value '{trueVal}'");
            }
        }

        [Fact]
        public void ApplyEnvironmentVariableOverrides_CaseInsensitiveBool_FalseVariants()
        {
            foreach (var falseVal in new[] { "false", "FALSE", "False", "0" })
            {
                var builder = new ConfigSettingsBuilder { ShouldGenerateHtmlReport = true };
                WithEnvVar("FOLDERDIFF_SHOULDGENERATEHTMLREPORT", falseVal,
                    () => ConfigService.ApplyEnvironmentVariableOverrides(builder));
                Assert.False(builder.ShouldGenerateHtmlReport, $"Expected false for value '{falseVal}'");
            }
        }

        [Fact]
        public void ApplyEnvironmentVariableOverrides_MultipleVars_AllApplied()
        {
            var builder = new ConfigSettingsBuilder();
            WithEnvVars(
                new[] {
                    ("FOLDERDIFF_MAXPARALLELISM", "12"),
                    ("FOLDERDIFF_SKIPIL", "true"),
                    ("FOLDERDIFF_SHOULDGENERATEHTMLREPORT", "false"),
                    ("FOLDERDIFF_SHOULDTREATTEXTBYTEDIFFERENCESASMISMATCH", "false"),
                    ("FOLDERDIFF_ILCACHEDIRECTORYABSOLUTEPATH", "/ci/cache"),
                },
                () => ConfigService.ApplyEnvironmentVariableOverrides(builder));

            Assert.Equal(12, builder.MaxParallelism);
            Assert.True(builder.SkipIL);
            Assert.False(builder.ShouldGenerateHtmlReport);
            Assert.False(builder.ShouldTreatTextByteDifferencesAsMismatch);
            Assert.Equal("/ci/cache", builder.ILCacheDirectoryAbsolutePath);
        }

        // ── Mutation-testing additions / ミューテーションテスト追加 ──────────────

        [Fact]
        public void TryApplyInt_EmptyString_NoOverride()
        {
            // When env var is set to empty string, int.TryParse fails and no override occurs
            // 環境変数が空文字の場合、int.TryParse が失敗しオーバーライドは行われない
            var builder = new ConfigSettingsBuilder { MaxParallelism = 4 };
            WithEnvVar("FOLDERDIFF_MAXPARALLELISM", string.Empty,
                () => ConfigService.ApplyEnvironmentVariableOverrides(builder));

            Assert.Equal(4, builder.MaxParallelism);
        }

        [Fact]
        public void ApplyEnvironmentVariableOverrides_NoEnvVarsSet_DefaultsUnchanged()
        {
            // When no FOLDERDIFF_ env vars are set, all defaults remain unchanged
            // FOLDERDIFF_ 環境変数が設定されていない場合、すべてのデフォルト値が変更されないこと
            var builder = new ConfigSettingsBuilder();
            var defaultMaxLog = builder.MaxLogGenerations;
            var defaultEnableILCache = builder.EnableILCache;
            var defaultShouldIncludeUnchanged = builder.ShouldIncludeUnchangedFiles;
            var defaultMaxParallelism = builder.MaxParallelism;

            // Ensure no FOLDERDIFF_ vars are set by clearing them
            // FOLDERDIFF_ 変数が設定されていないことを確認するためクリア
            WithEnvVars(
                new[] {
                    ("FOLDERDIFF_MAXLOGGENERATIONS", (string)null!),
                    ("FOLDERDIFF_ENABLEILCACHE", (string)null!),
                    ("FOLDERDIFF_SHOULDINCLUDEUNCHANGEDFILES", (string)null!),
                    ("FOLDERDIFF_MAXPARALLELISM", (string)null!),
                },
                () => ConfigService.ApplyEnvironmentVariableOverrides(builder));

            Assert.Equal(defaultMaxLog, builder.MaxLogGenerations);
            Assert.Equal(defaultEnableILCache, builder.EnableILCache);
            Assert.Equal(defaultShouldIncludeUnchanged, builder.ShouldIncludeUnchangedFiles);
            Assert.Equal(defaultMaxParallelism, builder.MaxParallelism);
        }

        // ── Env var edge cases: whitespace, overflow, partial matches ──
        // ── 環境変数エッジケース: 空白、オーバーフロー、部分一致 ──

        [Theory]
        [InlineData(" true")]
        [InlineData("true ")]
        [InlineData(" 1 ")]
        public async Task LoadConfigBuilderAsync_EnvVarBoolWithWhitespace_IsIgnored(string value)
        {
            // Bool parsing does NOT trim whitespace, so these should be silently ignored
            // ブール解析は空白をトリムしないため、これらはサイレントに無視されるべき
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_SKIPIL", value) },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();

                        Assert.False(builder.SkipIL);  // default unchanged / デフォルトのまま
                    });
            });
        }

        [Theory]
        [InlineData("2147483648")]   // int.MaxValue + 1 — TryParse fails
        [InlineData("2.5")]          // decimal — TryParse fails
        [InlineData("")]             // empty — TryParse fails
        public async Task LoadConfigBuilderAsync_EnvVarIntWithUnparsableValues_IsIgnored(string value)
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_MAXPARALLELISM", value) },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();

                        Assert.Equal(0, builder.MaxParallelism);  // default / デフォルト
                    });
            });
        }

        [Fact]
        public async Task LoadConfigBuilderAsync_EnvVarIntWithNegativeValue_IsApplied()
        {
            // int.TryParse succeeds for negative values — the env var IS applied
            // int.TryParse は負の値で成功する — 環境変数は適用される
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_MAXPARALLELISM", "-99999") },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();

                        Assert.Equal(-99999, builder.MaxParallelism);
                    });
            });
        }

        [Theory]
        [InlineData("truee")]
        [InlineData("tr")]
        [InlineData("01")]
        [InlineData("TRUE1")]
        public async Task LoadConfigBuilderAsync_EnvVarBoolPartialMatch_IsIgnored(string value)
        {
            await WithConfigFileAsync("{}", async () =>
            {
                await WithEnvVarsAsync(
                    new[] { ("FOLDERDIFF_ENABLEILCACHE", value) },
                    async () =>
                    {
                        var service = new ConfigService();
                        var builder = await service.LoadConfigBuilderAsync();

                        Assert.True(builder.EnableILCache);  // default unchanged / デフォルトのまま
                    });
            });
        }
    }
}
