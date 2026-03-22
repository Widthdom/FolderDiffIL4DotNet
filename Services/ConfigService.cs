using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Service for loading and providing settings from config.json.
    /// config.json の読み込みと設定の提供を行うサービス。
    /// </summary>
    public sealed class ConfigService
    {
        private const string CONFIG_FILE_NAME = "config.json";
        private const string ERROR_CONFIG_PARSE_FAILED = "Failed to parse config.json — JSON syntax error";
        private const string ERROR_CONFIG_PARSE_HINT =
            " Hint: standard JSON does not allow trailing commas after the last property or array element" +
            " (e.g. remove the comma in \"Key\": \"value\",}).";
        internal const string ERROR_CONFIG_VALIDATION_PREFIX = "config.json contains invalid settings:";
        internal const string ENV_VAR_PREFIX = "FOLDERDIFF_";

        /// <summary>
        /// Asynchronously loads settings from config.json at the given path (or the application base directory),
        /// deserialises them into a <see cref="ConfigSettingsBuilder"/>, applies environment variable overrides,
        /// and returns the mutable builder so that CLI overrides can be applied before calling <see cref="ConfigSettingsBuilder.Build"/>.
        /// config.json を指定パス（または既定のアプリケーションベースディレクトリ）から非同期で読み込み、
        /// <see cref="ConfigSettingsBuilder"/> にデシリアライズし、環境変数オーバーライドを適用した後、
        /// CLI オーバーライドの適用と <see cref="ConfigSettingsBuilder.Build"/> 呼び出しのためにミュータブルなビルダーを返します。
        /// </summary>
        public async Task<ConfigSettingsBuilder> LoadConfigBuilderAsync(string? configFilePath = null)
        {
            try
            {
                string configFileAbsolutePath = string.IsNullOrWhiteSpace(configFilePath)
                    ? Path.Combine(AppContext.BaseDirectory, CONFIG_FILE_NAME)
                    : configFilePath;
                if (!File.Exists(configFileAbsolutePath))
                {
                    throw new FileNotFoundException($"Config file not found: {configFileAbsolutePath}");
                }

                string json = await File.ReadAllTextAsync(configFileAbsolutePath);
                var builder = JsonSerializer.Deserialize<ConfigSettingsBuilder>(json)
                    ?? throw new InvalidDataException(ERROR_CONFIG_PARSE_FAILED);

                ApplyEnvironmentVariableOverrides(builder);

                return builder;
            }
            catch (JsonException ex)
            {
                // Include line/position from JsonException and append a trailing-comma hint.
                // 行番号・バイト位置を付与し、トレイリングカンマ等の典型的なミスへのヒントを添える。
                var location = ex.LineNumber.HasValue
                    ? $" (line {ex.LineNumber.Value + 1}, position {(ex.BytePositionInLine ?? 0) + 1})"
                    : string.Empty;
                throw new InvalidDataException(
                    $"{ERROR_CONFIG_PARSE_FAILED}{location}: {ex.Message}{ERROR_CONFIG_PARSE_HINT}", ex);
            }
        }

        /// <summary>
        /// Reads environment variables prefixed with <c>FOLDERDIFF_</c> and overrides the corresponding
        /// <see cref="ConfigSettingsBuilder"/> properties. Applied after JSON defaults but before validation,
        /// so environment-variable values are also subject to validation.
        /// Booleans accept <c>true</c>/<c>false</c>/<c>1</c>/<c>0</c> (case-insensitive).
        /// <c>FOLDERDIFF_</c> プレフィックスを持つ環境変数を読み取り、対応する <see cref="ConfigSettingsBuilder"/> プロパティを上書きします。
        /// JSON 既定値の後・バリデーションの前に適用されるため、環境変数の値もバリデーション対象です。
        /// bool 値は <c>true</c>/<c>false</c>/<c>1</c>/<c>0</c>（大文字小文字不問）を受け付けます。
        /// </summary>
        internal static void ApplyEnvironmentVariableOverrides(ConfigSettingsBuilder config)
        {
            const string P = ENV_VAR_PREFIX;

            TryApplyInt(P + "MAXLOGGENERATIONS",                           v => config.MaxLogGenerations = v);
            TryApplyBool(P + "SHOULDINCLUDEUNCHANGEDFILES",                v => config.ShouldIncludeUnchangedFiles = v);
            TryApplyBool(P + "SHOULDINCLUDEIGNOREDFILES",                  v => config.ShouldIncludeIgnoredFiles = v);
            TryApplyBool(P + "SHOULDINCLUDEILCACHESTATSINREPORT",          v => config.ShouldIncludeILCacheStatsInReport = v);
            TryApplyBool(P + "SHOULDGENERATEHTMLREPORT",                   v => config.ShouldGenerateHtmlReport = v);
            TryApplyBool(P + "SHOULDOUTPUTILTEXT",                         v => config.ShouldOutputILText = v);
            TryApplyBool(P + "SHOULDIGNOREILLINESCONFIGUREDSTRINGS",       v => config.ShouldIgnoreILLinesContainingConfiguredStrings = v);
            TryApplyBool(P + "SHOULDOUTPUTFILETIMESTAMPS",                 v => config.ShouldOutputFileTimestamps = v);
            TryApplyBool(P + "SHOULDWARNWHENNEWFILETIMESTAMPISOLDER",      v => config.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = v);
            TryApplyInt(P + "MAXPARALLELISM",                              v => config.MaxParallelism = v);
            TryApplyInt(P + "TEXTDIFFPARALLELTHRESHOLDKILOBYTES",          v => config.TextDiffParallelThresholdKilobytes = v);
            TryApplyInt(P + "TEXTDIFFCHUNKSIZEKILOBYTES",                  v => config.TextDiffChunkSizeKilobytes = v);
            TryApplyInt(P + "TEXTDIFFPARALLELMEMORYLIMITMEGABYTES",        v => config.TextDiffParallelMemoryLimitMegabytes = v);
            TryApplyBool(P + "ENABLEILCACHE",                              v => config.EnableILCache = v);
            TryApplyString(P + "ILCACHEDIRECTORYABSOLUTEPATH",             v => config.ILCacheDirectoryAbsolutePath = v);
            TryApplyInt(P + "ILCACHESTATSLOGINTERVALSECONDS",              v => config.ILCacheStatsLogIntervalSeconds = v);
            TryApplyInt(P + "ILCACHEMAXDISKFILECOUNT",                     v => config.ILCacheMaxDiskFileCount = v);
            TryApplyInt(P + "ILCACHEMAXDISKMEGABYTES",                     v => config.ILCacheMaxDiskMegabytes = v);
            TryApplyInt(P + "ILPRECOMPUTEBATCHSIZE",                       v => config.ILPrecomputeBatchSize = v);
            TryApplyBool(P + "OPTIMIZEFORNETWORKSHARES",                   v => config.OptimizeForNetworkShares = v);
            TryApplyBool(P + "AUTODETECTNETWORKSHARES",                    v => config.AutoDetectNetworkShares = v);
            TryApplyInt(P + "DISASSEMBLERBLACKLISTTTLMINUTES",             v => config.DisassemblerBlacklistTtlMinutes = v);
            TryApplyBool(P + "SKIPIL",                                     v => config.SkipIL = v);
            TryApplyBool(P + "ENABLEINLINEDIFF",                           v => config.EnableInlineDiff = v);
            TryApplyInt(P + "INLINEDIFFCONTEXTLINES",                      v => config.InlineDiffContextLines = v);
            TryApplyInt(P + "INLINEDIFFMAXEDITDISTANCE",                   v => config.InlineDiffMaxEditDistance = v);
            TryApplyInt(P + "INLINEDIFFMAXDIFFLINES",                      v => config.InlineDiffMaxDiffLines = v);
            TryApplyInt(P + "INLINEDIFFMAXOUTPUTLINES",                    v => config.InlineDiffMaxOutputLines = v);
            TryApplyBool(P + "INLINEDIFFLAZYRENDER",                        v => config.InlineDiffLazyRender = v);
        }

        private static void TryApplyInt(string envVarName, Action<int> apply)
        {
            var raw = Environment.GetEnvironmentVariable(envVarName);
            if (raw != null && int.TryParse(raw, out var parsed))
            {
                apply(parsed);
            }
        }

        private static void TryApplyBool(string envVarName, Action<bool> apply)
        {
            var raw = Environment.GetEnvironmentVariable(envVarName);
            if (raw == null) return;

            if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1")
            {
                apply(true);
            }
            else if (raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw == "0")
            {
                apply(false);
            }
        }

        private static void TryApplyString(string envVarName, Action<string> apply)
        {
            var raw = Environment.GetEnvironmentVariable(envVarName);
            if (raw != null)
            {
                apply(raw);
            }
        }
    }
}
