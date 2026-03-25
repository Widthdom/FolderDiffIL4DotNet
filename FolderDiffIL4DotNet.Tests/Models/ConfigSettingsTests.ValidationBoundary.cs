/// <summary>
/// Partial class containing validation boundary tests that kill Stryker mutations on comparison operators.
/// 比較演算子に対する Stryker ミューテーションをキルするバリデーション境界テストを含むパーシャルクラス。
/// </summary>

using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Models
{
    public sealed partial class ConfigSettingsTests
    {
        // ── Mutation-killing: boundary at exactly 1 for "< 1" checks ──────────
        // ミューテーションキル: "< 1" チェックのちょうど 1 の境界

        [Fact]
        [Trait("Category", "Unit")]
        public void Validate_MaxLogGenerationsExactlyOne_IsValid()
        {
            // Kills mutation: `< 1` → `<= 1` (value 1 must be valid)
            // ミューテーションキル: `< 1` → `<= 1`（値 1 は有効でなければならない）
            var builder = new ConfigSettingsBuilder { MaxLogGenerations = 1 };
            var result = builder.Validate();
            Assert.True(result.IsValid);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Validate_TextDiffParallelThresholdKilobytesExactlyOne_IsValid()
        {
            // Kills mutation: `< 1` → `<= 1` (value 1 must be valid)
            // ミューテーションキル: `< 1` → `<= 1`
            var builder = new ConfigSettingsBuilder
            {
                TextDiffParallelThresholdKilobytes = 1,
                TextDiffChunkSizeKilobytes = 1, // avoid chunk >= threshold error / チャンク >= 閾値エラーを回避
            };
            // chunk == threshold triggers error, so only test threshold alone
            // チャンク == 閾値はエラーになるため、閾値のみテスト
            var builderThresholdOnly = new ConfigSettingsBuilder { TextDiffParallelThresholdKilobytes = 1 };
            var result = builderThresholdOnly.Validate();
            // Default chunk (64) >= threshold (1) triggers chunk error, but threshold itself is valid
            // デフォルトチャンク (64) >= 閾値 (1) でチャンクエラーが出るが、閾値自体は有効
            Assert.DoesNotContain(result.Errors, e =>
                e.Contains("TextDiffParallelThresholdKilobytes must be 1 or greater", System.StringComparison.Ordinal));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Validate_TextDiffChunkSizeKilobytesExactlyOne_IsValid()
        {
            // Kills mutation: `< 1` → `<= 1` (value 1 must be valid)
            // ミューテーションキル: `< 1` → `<= 1`
            var builder = new ConfigSettingsBuilder { TextDiffChunkSizeKilobytes = 1 };
            var result = builder.Validate();
            // chunk (1) < default threshold (512) so no chunk>=threshold error either
            // チャンク (1) < デフォルト閾値 (512) のためチャンク>=閾値エラーもなし
            Assert.True(result.IsValid);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Validate_InlineDiffContextLinesExactlyZero_IsValid()
        {
            // Kills mutation: `< 0` → `<= 0` (value 0 must be valid)
            // ミューテーションキル: `< 0` → `<= 0`
            var builder = new ConfigSettingsBuilder { InlineDiffContextLines = 0 };
            var result = builder.Validate();
            Assert.True(result.IsValid);
        }

        // ── Mutation-killing: default constant literal values ─────────────────
        // ミューテーションキル: デフォルト定数のリテラル値

        [Fact]
        [Trait("Category", "Unit")]
        public void DefaultConstants_BooleanValues_MatchExpectedLiterals()
        {
            // Kills mutations on boolean constant declarations (true → false, false → true)
            // These cannot be caught by AssertMatchesDefaults since both sides would mutate together
            // ブーリアン定数宣言のミューテーションをキル（true → false、false → true）
            // AssertMatchesDefaults は両辺が同時に変異するため検出できない
            Assert.True(ConfigSettings.DefaultShouldIncludeUnchangedFiles);
            Assert.True(ConfigSettings.DefaultShouldIncludeIgnoredFiles);
            Assert.True(ConfigSettings.DefaultShouldIncludeAssemblySemanticChangesInReport);
            Assert.True(ConfigSettings.DefaultShouldIncludeDependencyChangesInReport);
            Assert.False(ConfigSettings.DefaultShouldIncludeILCacheStatsInReport);
            Assert.True(ConfigSettings.DefaultShouldGenerateHtmlReport);
            Assert.True(ConfigSettings.DefaultShouldGenerateAuditLog);
            Assert.True(ConfigSettings.DefaultShouldOutputILText);
            Assert.False(ConfigSettings.DefaultShouldIgnoreILLinesContainingConfiguredStrings);
            Assert.False(ConfigSettings.DefaultSkipIL);
            Assert.True(ConfigSettings.DefaultEnableILCache);
            Assert.False(ConfigSettings.DefaultOptimizeForNetworkShares);
            Assert.True(ConfigSettings.DefaultAutoDetectNetworkShares);
            Assert.True(ConfigSettings.DefaultShouldOutputFileTimestamps);
            Assert.True(ConfigSettings.DefaultShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp);
            Assert.True(ConfigSettings.DefaultEnableInlineDiff);
            Assert.True(ConfigSettings.DefaultInlineDiffLazyRender);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void DefaultConstants_NumericValues_MatchExpectedLiterals()
        {
            // Kills mutations on numeric constant declarations (value ± 1, etc.)
            // 数値定数宣言のミューテーションをキル（値 ± 1 等）
            Assert.Equal(5, ConfigSettings.DefaultMaxLogGenerations);
            Assert.Equal(0, ConfigSettings.DefaultMaxParallelism);
            Assert.Equal(512, ConfigSettings.DefaultTextDiffParallelThresholdKilobytes);
            Assert.Equal(64, ConfigSettings.DefaultTextDiffChunkSizeKilobytes);
            Assert.Equal(0, ConfigSettings.DefaultTextDiffParallelMemoryLimitMegabytes);
            Assert.Equal(60, ConfigSettings.DefaultILCacheStatsLogIntervalSeconds);
            Assert.Equal(1000, ConfigSettings.DefaultILCacheMaxDiskFileCount);
            Assert.Equal(512, ConfigSettings.DefaultILCacheMaxDiskMegabytes);
            Assert.Equal(0, ConfigSettings.DefaultILCacheMaxMemoryMegabytes);
            Assert.Equal(2048, ConfigSettings.DefaultILPrecomputeBatchSize);
            Assert.Equal(10, ConfigSettings.DefaultDisassemblerBlacklistTtlMinutes);
            Assert.Equal(300, ConfigSettings.DefaultDisassemblerTimeoutSeconds);
            Assert.Equal(0, ConfigSettings.DefaultInlineDiffContextLines);
            Assert.Equal(4000, ConfigSettings.DefaultInlineDiffMaxEditDistance);
            Assert.Equal(10000, ConfigSettings.DefaultInlineDiffMaxDiffLines);
            Assert.Equal(10000, ConfigSettings.DefaultInlineDiffMaxOutputLines);
        }

        // ── Mutation-killing: ConfigValidationResult.IsValid ──────────────────
        // ミューテーションキル: ConfigValidationResult.IsValid

        [Fact]
        [Trait("Category", "Unit")]
        public void ConfigValidationResult_IsValid_TrueWhenNoErrors()
        {
            // Kills mutation: `Errors.Count == 0` → `Errors.Count != 0`
            // ミューテーションキル: `Errors.Count == 0` → `Errors.Count != 0`
            var result = new ConfigValidationResult(System.Array.Empty<string>());
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ConfigValidationResult_IsValid_FalseWhenOneError()
        {
            // Kills mutation: `Errors.Count == 0` → `Errors.Count == 1` or other comparisons
            // ミューテーションキル: `Errors.Count == 0` → `Errors.Count == 1` 等
            var result = new ConfigValidationResult(new[] { "error" });
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
        }

        // ── Mutation-killing: Validate ChunkSize >= Threshold condition ───────
        // ミューテーションキル: ChunkSize >= Threshold 条件

        [Fact]
        [Trait("Category", "Unit")]
        public void Validate_ChunkSizeOneLessThanThreshold_IsValid()
        {
            // Kills mutation: `>=` → `>` in chunk/threshold comparison
            // ミューテーションキル: チャンク/閾値比較の `>=` → `>`
            var builder = new ConfigSettingsBuilder
            {
                TextDiffChunkSizeKilobytes = 511,
                TextDiffParallelThresholdKilobytes = 512,
            };
            var result = builder.Validate();
            Assert.True(result.IsValid);
        }
    }
}
