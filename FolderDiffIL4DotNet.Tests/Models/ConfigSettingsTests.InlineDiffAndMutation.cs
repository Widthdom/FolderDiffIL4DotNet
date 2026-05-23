/// <summary>
/// Partial class containing InlineDiff settings, config sample validation, and IReadOnlyConfigSettings tests.
/// InlineDiff 設定、config サンプル検証、IReadOnlyConfigSettings テストを含むパーシャルクラス。
/// </summary>

using System;
using System.Text.Json;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Models
{
    public sealed partial class ConfigSettingsTests
    {
        // ── InlineDiff ─────────────────────────────────────────────────────────

        [Fact]
        public void Constructor_InlineDiffDefaults_AreCorrect()
        {
            var config = new ConfigSettingsBuilder().Build();

            Assert.True(config.EnableInlineDiff);
            Assert.Equal(4, config.InlineDiffContextLines);
            Assert.Equal(10000, config.InlineDiffMaxDiffLines);
            Assert.Equal(10000, config.InlineDiffMaxOutputLines);
        }

        [Fact]
        public void JsonDeserialize_InlineDiffOverrides_AreApplied()
        {
            const string json = """
                {
                  "EnableInlineDiff": false,
                  "InlineDiffContextLines": 5,
                  "InlineDiffMaxDiffLines": 2000,
                  "InlineDiffMaxOutputLines": 300
                }
                """;

            var config = JsonSerializer.Deserialize<ConfigSettingsBuilder>(json)!.Build();

            Assert.NotNull(config);
            Assert.False(config.EnableInlineDiff);
            Assert.Equal(5, config.InlineDiffContextLines);
            Assert.Equal(2000, config.InlineDiffMaxDiffLines);
            Assert.Equal(300, config.InlineDiffMaxOutputLines);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Validate_InlineDiffContextLines_Negative_ReturnsError(int value)
        {
            var builder = new ConfigSettingsBuilder { InlineDiffContextLines = value };

            var result = builder.Validate();

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("InlineDiffContextLines", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_InlineDiffContextLines_Zero_IsValid()
        {
            var builder = new ConfigSettingsBuilder { InlineDiffContextLines = 0 };

            var result = builder.Validate();

            Assert.True(result.IsValid);
        }

        /// <summary>
        /// Verifies that values in config.sample.jsonc match the code-defined defaults in ConfigSettings,
        /// preventing the sample from drifting out of sync with the actual defaults.
        /// config.sample.jsonc の値が ConfigSettings のコード定義デフォルトと一致することを検証し、
        /// サンプルと実際のデフォルト値の乖離を防止する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void ConfigSampleJsonc_ValuesMatchCodeDefinedDefaults()
        {
            // Locate config.sample.jsonc relative to the test assembly / テストアセンブリからの相対パスで config.sample.jsonc を探索
            var repoRoot = FindRepoRoot();
            var samplePath = System.IO.Path.Combine(repoRoot, "doc", "config.sample.jsonc");
            Assert.True(System.IO.File.Exists(samplePath), $"config.sample.jsonc not found at {samplePath}");

            // Strip JSONC comments to produce valid JSON / JSONC コメントを除去して有効な JSON に変換
            var jsonc = System.IO.File.ReadAllText(samplePath);
            var json = StripJsoncComments(jsonc);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Boolean settings / 真偽値設定
            AssertJsonBool(root, "ShouldIncludeUnchangedFiles", ConfigSettings.DefaultShouldIncludeUnchangedFiles);
            AssertJsonBool(root, "ShouldIncludeIgnoredFiles", ConfigSettings.DefaultShouldIncludeIgnoredFiles);
            AssertJsonBool(root, "ShouldIncludeAssemblySemanticChangesInReport", ConfigSettings.DefaultShouldIncludeAssemblySemanticChangesInReport);
            AssertJsonBool(root, "ShouldIncludeDependencyChangesInReport", ConfigSettings.DefaultShouldIncludeDependencyChangesInReport);
            AssertJsonBool(root, "ShouldIncludeReviewChecklist", ConfigSettings.DefaultShouldIncludeReviewChecklist);
            AssertJsonBool(root, "ShouldIncludeILCacheStatsInReport", ConfigSettings.DefaultShouldIncludeILCacheStatsInReport);
            AssertJsonBool(root, "ShouldGenerateHtmlReport", ConfigSettings.DefaultShouldGenerateHtmlReport);
            AssertJsonBool(root, "ShouldGenerateAuditLog", ConfigSettings.DefaultShouldGenerateAuditLog);
            AssertJsonBool(root, "ShouldGenerateSbom", ConfigSettings.DefaultShouldGenerateSbom);
            AssertJsonBool(root, "ShouldOutputILText", ConfigSettings.DefaultShouldOutputILText);
            AssertJsonBool(root, "ShouldOutputFileTimestamps", ConfigSettings.DefaultShouldOutputFileTimestamps);
            AssertJsonBool(root, "ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp", ConfigSettings.DefaultShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
            AssertJsonBool(root, "SkipIL", ConfigSettings.DefaultSkipIL);
            AssertJsonBool(root, "ShouldIgnoreILLinesContainingConfiguredStrings", ConfigSettings.DefaultShouldIgnoreILLinesContainingConfiguredStrings);
            AssertJsonBool(root, "EnableILCache", ConfigSettings.DefaultEnableILCache);
            AssertJsonBool(root, "OptimizeForNetworkShares", ConfigSettings.DefaultOptimizeForNetworkShares);
            AssertJsonBool(root, "AutoDetectNetworkShares", ConfigSettings.DefaultAutoDetectNetworkShares);
            AssertJsonBool(root, "ShouldTreatTextByteDifferencesAsMismatch", ConfigSettings.DefaultShouldTreatTextByteDifferencesAsMismatch);
            AssertJsonBool(root, "EnableInlineDiff", ConfigSettings.DefaultEnableInlineDiff);
            AssertJsonBool(root, "InlineDiffLazyRender", ConfigSettings.DefaultInlineDiffLazyRender);
            AssertJsonBool(root, "PluginStrictMode", ConfigSettings.DefaultPluginStrictMode);

            // Numeric settings / 数値設定
            AssertJsonInt(root, "MaxParallelism", ConfigSettings.DefaultMaxParallelism);
            AssertJsonInt(root, "TextDiffParallelThresholdKilobytes", ConfigSettings.DefaultTextDiffParallelThresholdKilobytes);
            AssertJsonInt(root, "TextDiffChunkSizeKilobytes", ConfigSettings.DefaultTextDiffChunkSizeKilobytes);
            AssertJsonInt(root, "TextDiffParallelMemoryLimitMegabytes", ConfigSettings.DefaultTextDiffParallelMemoryLimitMegabytes);
            AssertJsonInt(root, "ILCacheStatsLogIntervalSeconds", ConfigSettings.DefaultILCacheStatsLogIntervalSeconds);
            AssertJsonInt(root, "ILCacheMaxDiskFileCount", ConfigSettings.DefaultILCacheMaxDiskFileCount);
            AssertJsonInt(root, "ILCacheMaxDiskMegabytes", ConfigSettings.DefaultILCacheMaxDiskMegabytes);
            AssertJsonInt(root, "ILCacheMaxMemoryMegabytes", ConfigSettings.DefaultILCacheMaxMemoryMegabytes);
            AssertJsonInt(root, "ILPrecomputeBatchSize", ConfigSettings.DefaultILPrecomputeBatchSize);
            AssertJsonInt(root, "DisassemblerBlacklistTtlMinutes", ConfigSettings.DefaultDisassemblerBlacklistTtlMinutes);
            AssertJsonInt(root, "DisassemblerTimeoutSeconds", ConfigSettings.DefaultDisassemblerTimeoutSeconds);
            AssertJsonInt(root, "InlineDiffContextLines", ConfigSettings.DefaultInlineDiffContextLines);
            AssertJsonInt(root, "InlineDiffMaxEditDistance", ConfigSettings.DefaultInlineDiffMaxEditDistance);
            AssertJsonInt(root, "InlineDiffMaxDiffLines", ConfigSettings.DefaultInlineDiffMaxDiffLines);
            AssertJsonInt(root, "InlineDiffMaxOutputLines", ConfigSettings.DefaultInlineDiffMaxOutputLines);
            AssertJsonInt(root, "MaxLogGenerations", ConfigSettings.DefaultMaxLogGenerations);

            // String settings / 文字列設定
            Assert.True(root.TryGetProperty("SbomFormat", out var sbomFormatEl),
                "config.sample.jsonc is missing property 'SbomFormat'");
            Assert.Equal(ConfigSettings.DefaultSbomFormat, sbomFormatEl.GetString());

            // List defaults: IgnoredExtensions / リストデフォルト: IgnoredExtensions
            if (root.TryGetProperty("IgnoredExtensions", out var ignoredEl))
            {
                var actual = new System.Collections.Generic.List<string>();
                foreach (var item in ignoredEl.EnumerateArray())
                {
                    actual.Add(item.GetString()!);
                }
                Assert.Equal(ConfigSettings.DefaultIgnoredExtensionsValues, actual);
            }

            Assert.False(root.TryGetProperty("ILCacheMaxMemoryEntries", out _),
                "config.sample.jsonc still contains removed property 'ILCacheMaxMemoryEntries'");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Readme_ValidatedConstraints_StayInSyncWithCurrentConfigValidationRules()
        {
            var repoRoot = FindRepoRoot();
            var readmePath = System.IO.Path.Combine(repoRoot, "README.md");
            var userGuidePath = System.IO.Path.Combine(repoRoot, "USER_GUIDE.md");
            Assert.True(System.IO.File.Exists(readmePath), $"README.md not found at {readmePath}");
            Assert.True(System.IO.File.Exists(userGuidePath), $"USER_GUIDE.md not found at {userGuidePath}");

            var readme = System.IO.File.ReadAllText(readmePath);
            var userGuide = System.IO.File.ReadAllText(userGuidePath);

            Assert.Contains("For normal diff runs, after loading [`config.json`](config.json) and applying environment-variable plus runtime CLI overrides", readme, StringComparison.Ordinal);
            Assert.Contains("[USER_GUIDE.md](USER_GUIDE.md#readme-en-config)", readme, StringComparison.Ordinal);
            Assert.DoesNotContain("After loading [`config.json`](config.json), if any value is out of range", userGuide, StringComparison.Ordinal);
            Assert.Contains("`--validate-config` validates [`config.json`](config.json) plus `FOLDERDIFF_*` environment-variable overrides before any runtime CLI overrides are applied.", userGuide, StringComparison.Ordinal);
            Assert.Contains("`--print-config` prints the effective builder state after env-var and supported CLI overrides without semantic validation", userGuide, StringComparison.Ordinal);
            Assert.Contains("通常の diff 実行では、[`config.json`](config.json) の読み込み後、環境変数および実行時 CLI オーバーライドを適用した実効設定に範囲外の値がある場合", readme, StringComparison.Ordinal);
            Assert.Contains("[USER_GUIDE.md](USER_GUIDE.md#readme-ja-config)", readme, StringComparison.Ordinal);
            Assert.DoesNotContain("[`config.json`](config.json) の読み込み後、範囲外の値がある場合", userGuide, StringComparison.Ordinal);
            Assert.Contains("`--validate-config` は、[`config.json`](config.json) に `FOLDERDIFF_*` 環境変数オーバーライドを適用した状態を、実行時 CLI オーバーライド適用前に検証します。", userGuide, StringComparison.Ordinal);
            Assert.Contains("`--print-config` は、環境変数と対応 CLI オーバーライドを適用した builder 状態を、セマンティック検証なしでそのまま出力するため、範囲外を含む effective config の診断にも使えます。", userGuide, StringComparison.Ordinal);
            Assert.Contains("[`InlineDiffContextLines`](#config-en-inlinediffcontextlines) >= `0`", userGuide, StringComparison.Ordinal);
            Assert.Contains("[`ILCacheMaxMemoryMegabytes`](#config-en-ilcachemaxmemorymegabytes) >= `0`", userGuide, StringComparison.Ordinal);
            Assert.Contains("[`InlineDiffContextLines`](#config-ja-inlinediffcontextlines) >= `0`", userGuide, StringComparison.Ordinal);
            Assert.Contains("[`ILCacheMaxMemoryMegabytes`](#config-ja-ilcachemaxmemorymegabytes) >= `0`", userGuide, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void DeveloperGuide_ConfigValidationNotes_StayInSyncWithCurrentConfigValidationRules()
        {
            var repoRoot = FindRepoRoot();
            var devGuidePath = System.IO.Path.Combine(repoRoot, "doc", "DEVELOPER_GUIDE.md");
            Assert.True(System.IO.File.Exists(devGuidePath), $"DEVELOPER_GUIDE.md not found at {devGuidePath}");

            var devGuide = System.IO.File.ReadAllText(devGuidePath);

            Assert.Contains("[`InlineDiffContextLines`](../Models/ConfigSettings.cs) >= `0`", devGuide, StringComparison.Ordinal);
            Assert.Contains("[`ILCacheMaxMemoryMegabytes`](../Models/ConfigSettings.cs) >= `0`", devGuide, StringComparison.Ordinal);
            Assert.Contains("[`ConfigSettingsBuilder.Validate()`](../Models/ConfigSettingsBuilder.cs)", devGuide, StringComparison.Ordinal);
            Assert.DoesNotContain("ConfigSettings.Validate()", devGuide, StringComparison.Ordinal);
            Assert.Contains("ProgramRunner", devGuide, StringComparison.Ordinal);
            Assert.DoesNotContain("ConfigService throws", devGuide, StringComparison.Ordinal);
            Assert.DoesNotContain("ConfigService が全エラーを列挙した", devGuide, StringComparison.Ordinal);
            Assert.Contains("apply all `FOLDERDIFF_*` environment variable overrides plus supported runtime CLI overrides", devGuide, StringComparison.Ordinal);
            Assert.Contains("using that same resolution order after `FOLDERDIFF_*` environment-variable overrides but before runtime CLI overrides are applied", devGuide, StringComparison.Ordinal);
            Assert.Contains("すべての `FOLDERDIFF_*` 環境変数オーバーライドと対応する実行時 CLI オーバーライドを適用した builder 状態", devGuide, StringComparison.Ordinal);
            Assert.Contains("`FOLDERDIFF_*` 環境変数オーバーライド適用後・実行時 CLI オーバーライド適用前の builder をセマンティック検証", devGuide, StringComparison.Ordinal);
            Assert.Contains("[user guide configuration table](../USER_GUIDE.md#readme-en-config)", devGuide, StringComparison.Ordinal);
            Assert.Contains("[ユーザーガイドの設定表](../USER_GUIDE.md#readme-ja-config)", devGuide, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ProgramRunnerConfigComment_PrintConfigAsync_StaysInSyncWithPublishedContract()
        {
            var repoRoot = FindRepoRoot();
            var configRunnerPath = System.IO.Path.Combine(repoRoot, "Runner", "ProgramRunner.Config.cs");
            Assert.True(System.IO.File.Exists(configRunnerPath), $"ProgramRunner.Config.cs not found at {configRunnerPath}");

            var source = System.IO.File.ReadAllText(configRunnerPath);

            Assert.Contains("after JSON load, environment-variable overrides,", source, StringComparison.Ordinal);
            Assert.Contains("and supported CLI overrides) to stdout as JSON without semantic validation.", source, StringComparison.Ordinal);
            Assert.Contains("環境変数オーバーライド、および対応する CLI オーバーライド適用後の builder 状態", source, StringComparison.Ordinal);
            Assert.Contains("セマンティック検証なしで JSON として標準出力に書き出します。", source, StringComparison.Ordinal);
            Assert.DoesNotContain("after JSON load + environment variable overrides", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that ConfigSettings implements IReadOnlyConfigSettings and all interface properties are accessible.
        /// ConfigSettings が IReadOnlyConfigSettings を実装し、全インターフェースプロパティがアクセス可能であることを検証する。
        /// </summary>
        [Fact]
        public void ConfigSettings_ImplementsIReadOnlyConfigSettings()
        {
            var config = new ConfigSettingsBuilder().Build();

            IReadOnlyConfigSettings readOnly = config;

            Assert.NotNull(readOnly.IgnoredExtensions);
            Assert.NotNull(readOnly.TextFileExtensions);
            Assert.NotNull(readOnly.ILIgnoreLineContainingStrings);
            Assert.NotNull(readOnly.SpinnerFrames);
            Assert.Equal(config.MaxLogGenerations, readOnly.MaxLogGenerations);
            Assert.Equal(config.ShouldIncludeUnchangedFiles, readOnly.ShouldIncludeUnchangedFiles);
            Assert.Equal(config.MaxParallelism, readOnly.MaxParallelism);
            Assert.Equal(config.ShouldTreatTextByteDifferencesAsMismatch, readOnly.ShouldTreatTextByteDifferencesAsMismatch);
            Assert.Equal(config.EnableILCache, readOnly.EnableILCache);
            Assert.Equal(config.SkipIL, readOnly.SkipIL);
            Assert.Equal(config.EnableInlineDiff, readOnly.EnableInlineDiff);
            Assert.Equal(config.ShouldGenerateAuditLog, readOnly.ShouldGenerateAuditLog);
            Assert.Equal(config.ShouldIncludeReviewChecklist, readOnly.ShouldIncludeReviewChecklist);
        }

        /// <summary>
        /// Verifies that list properties on IReadOnlyConfigSettings return IReadOnlyList (no Add/Remove).
        /// IReadOnlyConfigSettings のリストプロパティが IReadOnlyList を返す（Add/Remove 不可）ことを検証する。
        /// </summary>
        [Fact]
        public void IReadOnlyConfigSettings_ListProperties_AreReadOnly()
        {
            var config = new ConfigSettingsBuilder().Build();
            IReadOnlyConfigSettings readOnly = config;

            // IReadOnlyList does not expose Add/Remove, verifying type constraint
            // IReadOnlyList は Add/Remove を公開しないため、型制約を確認
            System.Collections.Generic.IReadOnlyList<string> ignoredExts = readOnly.IgnoredExtensions;
            System.Collections.Generic.IReadOnlyList<string> textExts = readOnly.TextFileExtensions;
            System.Collections.Generic.IReadOnlyList<string> ilIgnore = readOnly.ILIgnoreLineContainingStrings;
            System.Collections.Generic.IReadOnlyList<string> spinnerFrames = readOnly.SpinnerFrames;

            Assert.NotEmpty(ignoredExts);
            Assert.NotEmpty(textExts);
            Assert.Empty(ilIgnore);
            Assert.NotEmpty(spinnerFrames);
        }
    }
}
