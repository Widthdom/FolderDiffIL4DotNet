using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Console;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Services.ILOutput;
using Microsoft.Extensions.DependencyInjection;

namespace FolderDiffIL4DotNet
{
    /// <summary>
    /// Runner that orchestrates the entire application execution.
    /// アプリケーション実行全体を調停するランナー。
    /// </summary>
    public sealed partial class ProgramRunner
    {
        private const string INITIALIZING_LOGGER = "Initializing logger...";
        private const string LOGGER_INITIALIZED = "Logger initialized.";
        private const string VALIDATING_ARGS = "Validating command line arguments...";
        private const string LOG_ARGS_VALIDATION_COMPLETED = "Command line arguments validation completed.";
        private const string LOG_LOADING_CONFIGURATION = "Loading configuration...";
        private const string LOG_CONFIGURATION_LOADED = "Configuration loaded successfully.";
        private const string LOG_APP_STARTING = "Starting " + Constants.APP_NAME + "...";
        private const string LOG_APP_FINISHED = Constants.APP_NAME + " finished without errors. See Reports folder for details.";
        private const string PRESS_ANY_KEY = "Press any key to exit...";
        private const string ERROR_KEY_PROMPT = "An error occurred during key prompt.";
        private const string WARNING_NEW_FILE_TIMESTAMP_OLDER_THAN_OLD = "One or more modified files in 'new' have older last-modified timestamps than the corresponding files in 'old'. See diff_report.md for details.";
        private const string TIP_PRINT_CONFIG = "Tip: Run with --print-config to display the effective configuration as JSON.";
        private const string HELP_TEXT =
            "Usage: " + Constants.APP_NAME + " <oldFolder> <newFolder> <reportLabel> [options]\n\n" +
            "Arguments:\n" +
            "  <oldFolder>    Absolute path to the baseline (old) folder.\n" +
            "  <newFolder>    Absolute path to the comparison (new) folder.\n" +
            "  <reportLabel>  Label used as the subfolder name under Reports/.\n\n" +
            "Options:\n" +
            "  --help, -h                  Show this help message and exit.\n" +
            "  --version                   Show the application version and exit.\n" +
            "  --print-config              Print the effective configuration as JSON and exit.\n" +
            "  --no-pause                  Skip key-wait at process end.\n" +
            "  --config <path>             Path to config.json (default: <exe>/config.json).\n" +
            "  --threads <N>               Override MaxParallelism (0 = auto).\n" +
            "  --no-il-cache               Disable the IL cache for this run.\n" +
            "  --skip-il                   Skip IL comparison for .NET assemblies.\n" +
            "  --no-timestamp-warnings     Suppress timestamp-regression warnings.\n\n" +
            "Environment variables (override config.json values):\n" +
            "  FOLDERDIFF_MAXPARALLELISM=<N>               Override MaxParallelism.\n" +
            "  FOLDERDIFF_ENABLEILCACHE=<true|false>       Enable/disable the IL cache.\n" +
            "  FOLDERDIFF_ILCACHEDIRECTORYABSOLUTEPATH=<p> IL cache directory path.\n" +
            "  FOLDERDIFF_SKIPIL=<true|false>              Skip IL comparison.\n" +
            "  FOLDERDIFF_SHOULDGENERATEHTMLREPORT=<t|f>   Generate HTML report.\n" +
            "  Any FOLDERDIFF_<PROPERTYNAME>=<value> key overrides the matching\n" +
            "  config.json property (bool: true/false/1/0, int: integer).\n\n" +
            "Exit codes:\n" +
            "  0  Success.\n" +
            "  2  Invalid arguments or input paths.\n" +
            "  3  Configuration load or parse error.\n" +
            "  4  Diff execution or report generation failure.\n" +
            "  1  Unexpected internal error.\n\n" +
            "Tip:\n" +
            "  Use --print-config to display the effective configuration\n" +
            "  (config.json + environment variable overrides) as JSON and exit.\n" +
            "  This is useful for verifying which settings are active before a run.";

        private readonly ILoggerService _logger;
        private readonly ConfigService _configService;

        /// <summary>
        /// Initializes a new instance of <see cref="ProgramRunner"/>.
        /// <see cref="ProgramRunner"/> の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="logger">Logger for diagnostic output. / 診断出力用ロガー。</param>
        /// <param name="configService">Service for loading configuration files. / 設定ファイル読込サービス。</param>
        public ProgramRunner(ILoggerService logger, ConfigService configService)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(configService);

            _logger = logger;
            _configService = configService;
        }

        /// <summary>
        /// Executes the main application flow and returns the process exit code.
        /// アプリケーションのメインフローを実行し、終了コードを返します。
        /// </summary>
        public async Task<int> RunAsync(string[] args)
        {
            var opts = CliParser.Parse(args);

            if (opts.ShowHelp)
            {
                Console.WriteLine(HELP_TEXT);
                return 0;
            }

            if (opts.ShowVersion)
            {
                Console.WriteLine(SystemInfo.GetAppVersion(typeof(Program)));
                return 0;
            }

            if (opts.PrintConfig)
            {
                return await PrintConfigAsync(opts.ConfigPath);
            }

            var result = await RunWithResultAsync(args, opts);
            OutputCompletionWarnings(result.HasSha256MismatchWarnings, result.HasTimestampRegressionWarnings);
            PromptForExitKeyIfNeeded(opts);
            return (int)result.ExitCode;
        }

        /// <summary>
        /// Converts the entire run into a typed result and maps it to the public API exit code at the application boundary.
        /// 実行全体を型付き結果へ変換し、公開 API である終了コードへ写像する境界処理です。
        /// </summary>
        private async Task<ProgramRunResult> RunWithResultAsync(string[] args, CliOptions opts)
        {
            #pragma warning disable CA1031 // Top-level application boundary classifies unexpected failures after logging.
            try
            {
                var appVersion = InitializeLoggerAndGetAppVersion();
                var computerName = SystemInfo.GetComputerName();

                // Railway-oriented pipeline: each step short-circuits on failure.
                // Railway 指向パイプライン: 各ステップは失敗時にショートサーキットします。
                var argsResult = TryValidateAndBuildRunArguments(args, opts)
                    .Bind(runArgs => TryPrepareReportsDirectory(runArgs.ReportsFolderAbsolutePath)
                        .Bind(_ => StepResult<RunArguments>.FromValue(runArgs)));

                var pipelineResult = await argsResult
                    .BindAsync(async runArgs =>
                    {
                        var builderResult = await TryLoadConfigBuilderAsync(opts.ConfigPath);
                        return builderResult.Bind(builder =>
                        {
                            ApplyCliOverrides(builder, opts);
                            return TryBuildConfig(builder);
                        }).Bind(config => StepResult<(RunArguments RunArgs, ConfigSettings Config)>.FromValue((runArgs, config)));
                    });

                var completionStateResult = await pipelineResult
                    .BindAsync(ctx => TryExecuteRunAsync(ctx.RunArgs, ctx.Config, appVersion, computerName));

                if (!completionStateResult.IsSuccess)
                {
                    return completionStateResult.Failure!;
                }

                _logger.LogMessage(AppLogLevel.Info, LOG_APP_FINISHED, shouldOutputMessageToConsole: true, ConsoleColor.Green);
                return ProgramRunResult.Success(completionStateResult.Value!);
            }
            catch (Exception ex)
            {
                return CreateFailureResult(ProgramExitCode.UnexpectedError, ex);
            }
            #pragma warning restore CA1031
        }

        private string InitializeLoggerAndGetAppVersion()
        {
            Console.WriteLine(INITIALIZING_LOGGER);
            _logger.Initialize();
            _logger.LogMessage(AppLogLevel.Info, LOGGER_INITIALIZED, shouldOutputMessageToConsole: true);

            var appVersion = SystemInfo.GetAppVersion(typeof(Program));
            _logger.LogMessage(AppLogLevel.Info, $"Application version: {appVersion}", shouldOutputMessageToConsole: true);
            return appVersion;
        }

        private void OutputCompletionWarnings(bool hasSha256MismatchWarnings, bool hasTimestampRegressionWarnings)
        {
            if (hasSha256MismatchWarnings)
            {
                _logger.LogMessage(AppLogLevel.Warning, Constants.WARNING_SHA256_MISMATCH, shouldOutputMessageToConsole: true, ConsoleColor.Yellow);
            }

            if (!hasTimestampRegressionWarnings)
            {
                return;
            }

            _logger.LogMessage(AppLogLevel.Warning, WARNING_NEW_FILE_TIMESTAMP_OLDER_THAN_OLD, shouldOutputMessageToConsole: true, ConsoleColor.Yellow);
        }

        /// <summary>
        /// Returns the CLI argument validation phase as a typed result.
        /// CLI 引数検証フェーズを型付き結果として返します。
        /// </summary>
        private StepResult<RunArguments> TryValidateAndBuildRunArguments(string[] args, CliOptions opts)
        {
            try
            {
                _logger.LogMessage(AppLogLevel.Info, VALIDATING_ARGS, shouldOutputMessageToConsole: true);
                if (opts.ParseError != null)
                {
                    throw new ArgumentException(opts.ParseError);
                }

                RunPreflightValidator.ValidateRequiredArguments(args);

                var oldFolderAbsolutePath = args[0];
                var newFolderAbsolutePath = args[1];
                var reportLabel = args[2];
                RunPreflightValidator.ValidateReportLabel(_logger, reportLabel);
                string reportsFolderAbsolutePath = RunPreflightValidator.GetReportsFolderAbsolutePath(reportLabel);
                RunPreflightValidator.ValidateRunDirectories(_logger, oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath);
                _logger.LogMessage(AppLogLevel.Info, LOG_ARGS_VALIDATION_COMPLETED, shouldOutputMessageToConsole: true);
                return StepResult<RunArguments>.FromValue(new RunArguments(oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath));
            }
            catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException
                or IOException or UnauthorizedAccessException)
            {
                return StepResult<RunArguments>.FromFailure(CreateFailureResult(ProgramExitCode.InvalidArguments, ex));
            }
        }

        private static DiffExecutionContext BuildExecutionContext(RunArguments runArguments, ConfigSettings config)
        {
            return RunScopeBuilder.BuildExecutionContext(
                runArguments.OldFolderAbsolutePath,
                runArguments.NewFolderAbsolutePath,
                runArguments.ReportsFolderAbsolutePath,
                config);
        }

        private ServiceProvider BuildRunServiceProvider(ConfigSettings config, DiffExecutionContext executionContext)
        {
            return RunScopeBuilder.Build(config, executionContext, _logger);
        }

        /// <summary>
        /// Returns the report output directory initialization as a typed result.
        /// レポート出力先ディレクトリの初期化を型付き結果として返します。
        /// </summary>
        private StepResult<bool> TryPrepareReportsDirectory(string reportsFolderAbsolutePath)
        {
            try
            {
                PrepareReportsDirectory(reportsFolderAbsolutePath);
                return StepResult<bool>.FromValue(true);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException
                or UnauthorizedAccessException or NotSupportedException)
            {
                return StepResult<bool>.FromFailure(CreateFailureResult(ProgramExitCode.ExecutionFailed, ex));
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

        /// <summary>
        /// Returns the diff execution and report generation phase as a typed result.
        /// 差分実行とレポート生成フェーズを型付き結果として返します。
        /// </summary>
        private async Task<StepResult<RunCompletionState>> TryExecuteRunAsync(
            RunArguments runArguments,
            ConfigSettings config,
            string appVersion,
            string computerName)
        {
            try
            {
                var completionState = await RunPipelineAsync(runArguments, config, appVersion, computerName);
                return StepResult<RunCompletionState>.FromValue(completionState);
            }
            catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException
                or FileNotFoundException or InvalidOperationException
                or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return StepResult<RunCompletionState>.FromFailure(CreateFailureResult(ProgramExitCode.ExecutionFailed, ex));
            }
        }

        /// <summary>
        /// Converts a failure category into a typed result with logging.
        /// 失敗種別をログ出力付きの型付き結果へ変換します。
        /// </summary>
        private ProgramRunResult CreateFailureResult(ProgramExitCode exitCode, Exception exception)
        {
            _logger.LogMessage(AppLogLevel.Error, exception.Message, shouldOutputMessageToConsole: true, ConsoleColor.Red, exception);
            _logger.LogMessage(AppLogLevel.Info, $"Error details logged to: {_logger.LogFileAbsolutePath}", shouldOutputMessageToConsole: true);

            if (exitCode == ProgramExitCode.ConfigurationError)
            {
                Console.Error.WriteLine(TIP_PRINT_CONFIG);
            }

            return ProgramRunResult.Failure(exitCode);
        }

        private async Task<RunCompletionState> RunPipelineAsync(RunArguments runArguments, ConfigSettings config, string appVersion, string computerName)
        {
            var executionContext = BuildExecutionContext(runArguments, config);
            using var runProvider = BuildRunServiceProvider(config, executionContext);
            using var scope = runProvider.CreateScope();
            return await ExecuteScopedRunAsync(scope.ServiceProvider, executionContext, appVersion, computerName, config);
        }

        private static void PrepareReportsDirectory(string reportsFolderAbsolutePath)
        {
            ConsoleBanner.Print();
            Directory.CreateDirectory(reportsFolderAbsolutePath);
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

        private async Task<RunCompletionState> ExecuteScopedRunAsync(
            IServiceProvider scopedProvider,
            DiffExecutionContext executionContext,
            string appVersion,
            string computerName,
            ConfigSettings config)
        {
            var resultLists = scopedProvider.GetRequiredService<FileDiffResultLists>();
            resultLists.DisassemblerAvailability = DisassemblerHelper.ProbeAllCandidates();
            var elapsedTimeString = await ExecuteDiffAsync(scopedProvider);
            GenerateReport(scopedProvider, executionContext, appVersion, elapsedTimeString, computerName, config);
            return new RunCompletionState(resultLists.HasAnySha256Mismatch, resultLists.HasAnyNewFileTimestampOlderThanOldWarning);
        }

        private async Task<string> ExecuteDiffAsync(IServiceProvider scopedProvider)
        {
            var progressReporter = scopedProvider.GetRequiredService<ProgressReportService>();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await scopedProvider.GetRequiredService<IFolderDiffService>().ExecuteFolderDiffAsync();
                stopwatch.Stop();
                var elapsedTimeString = FormatElapsedTime(stopwatch.Elapsed);
                _logger.LogMessage(AppLogLevel.Info, $"Elapsed Time: {elapsedTimeString}", shouldOutputMessageToConsole: true);
                return elapsedTimeString;
            }
            finally
            {
                progressReporter.Dispose();
            }
        }

        private static void GenerateReport(
            IServiceProvider scopedProvider,
            DiffExecutionContext executionContext,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            ConfigSettings config)
        {
            var ilCache = scopedProvider.GetService<ILCache>();
            var reportContext = new ReportGenerationContext(
                executionContext.OldFolderAbsolutePath,
                executionContext.NewFolderAbsolutePath,
                executionContext.ReportsFolderAbsolutePath,
                appVersion,
                elapsedTimeString,
                computerName,
                config,
                ilCache);
            scopedProvider.GetRequiredService<ReportGenerateService>().GenerateDiffReport(reportContext);
            scopedProvider.GetRequiredService<HtmlReportGenerateService>().GenerateDiffReportHtml(reportContext);
            scopedProvider.GetRequiredService<AuditLogGenerateService>().GenerateAuditLog(reportContext);
        }

        private void PromptForExitKeyIfNeeded(CliOptions opts)
        {
            if (ShouldSkipExitPrompt(opts))
            {
                return;
            }

            try
            {
                Console.WriteLine(PRESS_ANY_KEY);
                Console.ReadKey(true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
                _logger.LogMessage(AppLogLevel.Error, ERROR_KEY_PROMPT, shouldOutputMessageToConsole: false, ex);
            }
        }

        private static bool ShouldSkipExitPrompt(CliOptions opts)
            => opts.NoPause
                || Console.IsInputRedirected
                || Console.IsOutputRedirected
                || Console.IsErrorRedirected;

        /// <summary>
        /// Formats elapsed time in a human-readable form (e.g. <c>0h 5m 30.1s</c>).
        /// Seconds are shown to one decimal place (tenths, truncated).
        /// 経過時間を人間が判読しやすい形式（例: <c>0h 5m 30.1s</c>）に変換します。
        /// 秒は小数点以下 1 桁（1/10 秒単位、切り捨て）まで表示します。
        /// </summary>
        internal static string FormatElapsedTime(TimeSpan elapsed)
        {
            int hours = (int)Math.Floor(elapsed.TotalHours);
            int minutes = elapsed.Minutes;
            int seconds = elapsed.Seconds;
            int tenths = elapsed.Milliseconds / 100;
            return $"{hours}h {minutes}m {seconds}.{tenths}s";
        }

        /// <summary>
        /// Prints the effective configuration (after JSON load + environment variable overrides) to stdout as JSON.
        /// 有効な設定（JSON 読込 + 環境変数オーバーライド適用後）を JSON として標準出力に書き出します。
        /// </summary>
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
        }
    }
}
