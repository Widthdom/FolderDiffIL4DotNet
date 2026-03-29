using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet
{
    // Configuration loading, validation, and CLI override methods.
    // 設定読込・バリデーション・CLI オーバーライドメソッド。
    public sealed partial class ProgramRunner
    {
        private const string LOG_LOADING_CONFIGURATION = "Loading configuration...";
        private const string LOG_CONFIGURATION_LOADED = "Configuration loaded successfully.";

        /// <summary>
        /// Prints the effective configuration (after JSON load + environment variable overrides) to stdout as JSON.
        /// 有効な設定（JSON 読込 + 環境変数オーバーライド適用後）を JSON として標準出力に書き出します。
        /// </summary>
        /// <summary>
        /// Validates the configuration (JSON load + environment variable overrides + semantic validation) and reports results.
        /// 設定のバリデーション（JSON 読込 + 環境変数オーバーライド + セマンティック検証）を行い結果を報告します。
        /// </summary>
        private async Task<int> ValidateConfigAsync(string? configPath)
        {
            try
            {
                var builder = await _configService.LoadConfigBuilderAsync(configPath);
                var validationResult = builder.Validate();
                if (!validationResult.IsValid)
                {
                    Console.Error.WriteLine("Configuration validation failed:");
                    foreach (var error in validationResult.Errors)
                    {
                        Console.Error.WriteLine($"  - {error}");
                    }
                    return (int)ProgramExitCode.ConfigurationError;
                }

                // Build to verify immutable object construction succeeds
                // イミュータブルオブジェクトの構築が成功することを検証
                builder.Build();
                Console.WriteLine("Configuration is valid.");
                return 0;
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException
                or IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(ex.Message);
                return (int)ProgramExitCode.ConfigurationError;
            }
        }

        private async Task<int> PrintConfigAsync(string? configPath)
        {
            try
            {
                var builder = await _configService.LoadConfigBuilderAsync(configPath);
                Console.WriteLine(JsonSerializer.Serialize(builder, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException
                or IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(ex.Message);
                return (int)ProgramExitCode.ConfigurationError;
            }
        }

        /// <summary>
        /// Returns the configuration builder loading phase as a typed result.
        /// 設定ビルダー読込フェーズを型付き結果として返します。
        /// </summary>
        private async Task<StepResult<ConfigSettingsBuilder>> TryLoadConfigBuilderAsync(string? configPath)
        {
            try
            {
                var builder = await LoadConfigBuilderAsync(configPath);
                return StepResult<ConfigSettingsBuilder>.FromValue(builder);
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException
                or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return StepResult<ConfigSettingsBuilder>.FromFailure(CreateFailureResult(ProgramExitCode.ConfigurationError, ex));
            }
        }

        /// <summary>
        /// Validates and builds the immutable <see cref="ConfigSettings"/> from the builder.
        /// ビルダーを検証し、イミュータブルな <see cref="ConfigSettings"/> を構築します。
        /// </summary>
        private StepResult<ConfigSettings> TryBuildConfig(ConfigSettingsBuilder builder)
        {
            try
            {
                var validationResult = builder.Validate();
                if (!validationResult.IsValid)
                {
                    var details = string.Join(System.Environment.NewLine, validationResult.Errors);
                    throw new InvalidDataException($"{ConfigService.ERROR_CONFIG_VALIDATION_PREFIX}{System.Environment.NewLine}{details}");
                }

                return StepResult<ConfigSettings>.FromValue(builder.Build());
            }
            catch (Exception ex) when (ex is InvalidDataException)
            {
                return StepResult<ConfigSettings>.FromFailure(CreateFailureResult(ProgramExitCode.ConfigurationError, ex));
            }
        }

        private async Task<ConfigSettingsBuilder> LoadConfigBuilderAsync(string? configPath)
        {
            _logger.LogMessage(AppLogLevel.Info, LOG_LOADING_CONFIGURATION, shouldOutputMessageToConsole: true);
            var builder = await _configService.LoadConfigBuilderAsync(configPath);
            _logger.LogMessage(AppLogLevel.Info, LOG_CONFIGURATION_LOADED, shouldOutputMessageToConsole: true);
            _logger.CleanupOldLogFiles(builder.MaxLogGenerations);
            TimestampCache.Clear();
            _logger.LogMessage(AppLogLevel.Info, LOG_APP_STARTING, shouldOutputMessageToConsole: true);
            return builder;
        }

        /// <summary>
        /// Overrides <see cref="ConfigSettingsBuilder"/> values with CLI options, giving CLI flags priority over config.json.
        /// CLI オプションの値で <see cref="ConfigSettingsBuilder"/> を上書きします。config.json よりも CLI フラグを優先させます。
        /// </summary>
        private static void ApplyCliOverrides(ConfigSettingsBuilder builder, CliOptions opts)
        {
            if (opts.ThreadsOverride.HasValue)
            {
                builder.MaxParallelism = opts.ThreadsOverride.Value;
            }

            if (opts.NoIlCache)
            {
                builder.EnableILCache = false;
            }

            if (opts.SkipIL)
            {
                builder.SkipIL = true;
            }

            if (opts.NoTimestampWarnings)
            {
                builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = false;
            }

            if (opts.Coffee)
            {
                // Easter egg: replace spinner with coffee brewing animation / イースターエッグ: スピナーをコーヒー抽出アニメーションに差替
                // All frames are padded to equal width to prevent progress bar jitter / 全フレームを同じ幅に揃えてプログレスバーのガタつきを防止
                builder.SpinnerFrames = new System.Collections.Generic.List<string>
                {
                    "☕ Brewing   ",
                    "☕ Brewing.  ",
                    "☕ Brewing.. ",
                    "☕ Brewing...",
                };
            }
        }
    }
}
