using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// config.jsonの読み込みと設定の提供を行うサービス
    /// </summary>
    public sealed class ConfigService
    {
        /// <summary>
        /// 設定ファイル名
        /// </summary>
        private const string CONFIG_FILE_NAME = "config.json";

        /// <summary>
        /// Config 解析失敗（JSON 書式エラー）のメッセージプレフィックス
        /// </summary>
        private const string ERROR_CONFIG_PARSE_FAILED = "Failed to parse config.json — JSON syntax error";

        /// <summary>
        /// JSON 書式ミス（トレイリングカンマ等）に対するヒント文言
        /// </summary>
        private const string ERROR_CONFIG_PARSE_HINT =
            " Hint: standard JSON does not allow trailing commas after the last property or array element" +
            " (e.g. remove the comma in \"Key\": \"value\",}).";

        /// <summary>
        /// Configバリデーション失敗のメッセージプレフィックス
        /// </summary>
        internal const string ERROR_CONFIG_VALIDATION_PREFIX = "config.json contains invalid settings:";

        /// <summary>
        /// 環境変数オーバーライドのプレフィックス。
        /// </summary>
        internal const string ENV_VAR_PREFIX = "FOLDERDIFF_";

        /// <summary>
        /// config.jsonファイルから設定情報を非同期で読み込みます。
        /// このメソッドは、指定されたパス（または既定のアプリケーションベースディレクトリ）にある
        /// JSONファイルを読み取り、その内容を<see cref="ConfigSettings"/>オブジェクトにデシリアライズして返します。
        /// デシリアライズ後に設定値の整合性を検証します。
        /// </summary>
        /// <param name="configFilePath">
        /// 読み込む config.json の絶対パス。null または空文字列の場合は、アプリケーション実行ディレクトリ直下の
        /// config.json を使用します。
        /// </param>
        /// <returns>設定データを含む<see cref="ConfigSettings"/>オブジェクト</returns>
        /// <exception cref="FileNotFoundException">config.jsonファイルが指定された場所に存在しない場合にスローされます。</exception>
        /// <exception cref="InvalidDataException">config.jsonファイルが無効なJSON形式のため解析できない場合、または設定値が不正な場合にスローされます。</exception>
        /// <exception cref="IOException">config.jsonファイルの読み取り中にエラーが発生した場合にスローされます。</exception>
        public async Task<ConfigSettings> LoadConfigAsync(string configFilePath = null)
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
                var config = JsonSerializer.Deserialize<ConfigSettings>(json)
                    ?? throw new InvalidDataException(ERROR_CONFIG_PARSE_FAILED);

                ApplyEnvironmentVariableOverrides(config);

                var validationResult = config.Validate();
                if (!validationResult.IsValid)
                {
                    var details = string.Join(Environment.NewLine, validationResult.Errors);
                    throw new InvalidDataException($"{ERROR_CONFIG_VALIDATION_PREFIX}{Environment.NewLine}{details}");
                }

                return config;
            }
            catch (JsonException ex)
            {
                // 行番号・バイト位置を付与し、トレイリングカンマ等の典型的なミスへのヒントを添える。
                // Include line/position from JsonException and append a trailing-comma hint.
                var location = ex.LineNumber.HasValue
                    ? $" (line {ex.LineNumber.Value + 1}, position {(ex.BytePositionInLine ?? 0) + 1})"
                    : string.Empty;
                throw new InvalidDataException(
                    $"{ERROR_CONFIG_PARSE_FAILED}{location}: {ex.Message}{ERROR_CONFIG_PARSE_HINT}", ex);
            }
        }

        /// <summary>
        /// <c>FOLDERDIFF_</c> プレフィックスを持つ環境変数を読み取り、
        /// 対応する <see cref="ConfigSettings"/> プロパティを上書きします。
        /// JSON の既定値より後・バリデーションより前に適用されるため、
        /// 環境変数で設定した値もバリデーション対象になります。
        /// bool 値は <c>true</c>/<c>false</c>/<c>1</c>/<c>0</c>（大文字小文字不問）を受け付けます。
        /// </summary>
        /// <param name="config">上書き対象の設定オブジェクト。</param>
        internal static void ApplyEnvironmentVariableOverrides(ConfigSettings config)
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
