using System;
using System.IO;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Console;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Runner;
using FolderDiffIL4DotNet.Services;

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
        private const string LOG_APP_STARTING = "Starting " + Constants.APP_NAME + "...";
        private const string LOG_APP_FINISHED = Constants.APP_NAME + " finished without errors. See Reports folder for details.";
        private const string PRESS_ANY_KEY = "Press any key to exit...";
        private const string ERROR_KEY_PROMPT = "An error occurred during key prompt.";
        private const string WARNING_NEW_FILE_TIMESTAMP_OLDER_THAN_OLD = "One or more modified files in 'new' have older timestamps than the corresponding files in 'old'. See diff_report for details.";
        private const string TIP_PRINT_CONFIG = "Tip: Run with --print-config to display the effective configuration as JSON.";

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

            if (opts.ShowBanner)
            {
                ConsoleBanner.Print();
                return 0;
            }

            if (opts.PrintConfig)
            {
                return await PrintConfigAsync(opts.ConfigPath);
            }

            if (opts.ValidateConfig)
            {
                return await ValidateConfigAsync(opts.ConfigPath);
            }

            if (opts.Wizard)
            {
                return await RunWizardAsync(opts);
            }

            var result = await RunWithResultAsync(args, opts);
            OutputCompletionWarnings(result.HasSha256MismatchWarnings, result.HasTimestampRegressionWarnings);

            // Ring terminal bell on completion if requested / 要求された場合、完了時にターミナルベルを鳴らす
            if (opts.Bell)
            {
                // Use both BEL character and Console.Beep for maximum compatibility
                // BEL 文字と Console.Beep の両方を使用し、最大限の互換性を確保
                Console.Write("\a");
                Console.Out.Flush();
                try
                {
                    Console.Beep();
                }
                #pragma warning disable CA1031 // Beep may throw on platforms without audio support
                catch (PlatformNotSupportedException)
                {
                    // Console.Beep() is not supported on some platforms (e.g. macOS, some Linux)
                    // BEL character fallback above should still work
                }
                #pragma warning restore CA1031
            }

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
                // Apply log format before logger initialization / ロガー初期化前にログ形式を適用
                if (opts.LogFormatOverride != null)
                {
                    _logger.Format = opts.LogFormatOverride.Equals("json", System.StringComparison.OrdinalIgnoreCase)
                        ? Services.LogFormat.Json
                        : Services.LogFormat.Text;
                }

                var appVersion = InitializeLoggerAndGetAppVersion();
                var computerName = SystemInfo.GetComputerName();

                // Railway-oriented pipeline: each step short-circuits on failure.
                // Railway 指向パイプライン: 各ステップは失敗時にショートサーキットします。
                var argsResult = TryValidateAndBuildRunArguments(args, opts);

                // Dry-run does not need the Reports directory / ドライランでは Reports ディレクトリ不要
                if (!opts.DryRun)
                {
                    argsResult = argsResult
                        .Bind(runArgs => TryPrepareReportsDirectory(runArgs.ReportsFolderAbsolutePath)
                            .Bind(_ => StepResult<RunArguments>.FromValue(runArgs)));
                }

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

                // Dry-run: enumerate and show statistics, then exit / ドライラン: 列挙と統計表示のみで終了
                if (opts.DryRun)
                {
                    if (!pipelineResult.IsSuccess)
                    {
                        return pipelineResult.Failure!;
                    }

                    var ctx = pipelineResult.Value!;
                    var dryRunExecutor = new Runner.DryRunExecutor(_logger);
                    dryRunExecutor.Execute(ctx.RunArgs.OldFolderAbsolutePath, ctx.RunArgs.NewFolderAbsolutePath, ctx.Config);
                    return ProgramRunResult.Success(new RunCompletionState(false, false));
                }

                var completionStateResult = await pipelineResult
                    .BindAsync(ctx => TryExecuteRunAsync(ctx.RunArgs, ctx.Config, appVersion, computerName));

                if (!completionStateResult.IsSuccess)
                {
                    return completionStateResult.Failure!;
                }

                var completionState = completionStateResult.Value!;
                OutputCompletionSummaryChart(completionState);
                _logger.LogMessage(AppLogLevel.Info, LOG_APP_FINISHED, shouldOutputMessageToConsole: true, ConsoleColor.Green);
                return ProgramRunResult.Success(completionState);
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

        private static void OutputCompletionSummaryChart(RunCompletionState state)
        {
            int total = state.UnchangedCount + state.AddedCount + state.RemovedCount + state.ModifiedCount;
            if (total == 0)
            {
                return;
            }

            Console.WriteLine();
            OutputSummaryBar("Unchanged", state.UnchangedCount, total, null);
            OutputSummaryBar("Added",     state.AddedCount,     total, ConsoleColor.Green);
            OutputSummaryBar("Removed",   state.RemovedCount,   total, ConsoleColor.Red);
            OutputSummaryBar("Modified",  state.ModifiedCount,  total, ConsoleColor.Cyan);
            Console.WriteLine();
        }

        private static void OutputSummaryBar(string label, int count, int total, ConsoleColor? color)
        {
            const int BAR_WIDTH = 30;
            const int LABEL_WIDTH = 10;
            int filled = (int)Math.Round((double)count / total * BAR_WIDTH);
            if (filled > BAR_WIDTH) filled = BAR_WIDTH;

            var bar = new string('█', filled) + new string('░', BAR_WIDTH - filled);
            var pct = (100.0 * count / total).ToString("F1");

            if (color.HasValue)
            {
                // Color label, bar, count, and percentage / ステータスカテゴリはラベル・バー・件数・割合に色を適用
                var prevColor = Console.ForegroundColor;
                Console.ForegroundColor = color.Value;
                Console.Write($"  {label.PadRight(LABEL_WIDTH)} {bar} {count,5}");
                Console.ForegroundColor = prevColor;
                Console.Write($"/{total} (");
                Console.ForegroundColor = color.Value;
                Console.Write($"{pct,5}%");
                Console.ForegroundColor = prevColor;
                Console.WriteLine(")");
            }
            else
            {
                // Default color for Unchanged / Unchanged はデフォルト色
                Console.Write($"  {label.PadRight(LABEL_WIDTH)} {bar}");
                Console.WriteLine($" {count,5}/{total} ({pct,5}%)");
            }
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
        /// Returns the diff execution and report generation phase as a typed result.
        /// Delegates to <see cref="DiffPipelineExecutor"/> for the actual pipeline work.
        /// 差分実行とレポート生成フェーズを型付き結果として返します。
        /// 実際のパイプライン処理は <see cref="DiffPipelineExecutor"/> に委譲します。
        /// </summary>
        private async Task<StepResult<RunCompletionState>> TryExecuteRunAsync(
            RunArguments runArguments,
            ConfigSettings config,
            string appVersion,
            string computerName)
        {
            try
            {
                var executor = new DiffPipelineExecutor(_logger);
                var pipelineResult = await executor.ExecuteAsync(
                    runArguments.OldFolderAbsolutePath,
                    runArguments.NewFolderAbsolutePath,
                    runArguments.ReportsFolderAbsolutePath,
                    config,
                    appVersion,
                    computerName);
                var completionState = new RunCompletionState(
                    pipelineResult.HasSha256MismatchWarnings,
                    pipelineResult.HasTimestampRegressionWarnings,
                    pipelineResult.UnchangedCount,
                    pipelineResult.AddedCount,
                    pipelineResult.RemovedCount,
                    pipelineResult.ModifiedCount);
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

        private static void PrepareReportsDirectory(string reportsFolderAbsolutePath)
        {
            ConsoleBanner.Print();
            Directory.CreateDirectory(reportsFolderAbsolutePath);
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
        /// Delegates to <see cref="DiffPipelineExecutor.FormatElapsedTime"/>.
        /// 経過時間を人間が判読しやすい形式（例: <c>0h 5m 30.1s</c>）に変換します。
        /// <see cref="DiffPipelineExecutor.FormatElapsedTime"/> に委譲します。
        /// </summary>
        internal static string FormatElapsedTime(TimeSpan elapsed)
            => DiffPipelineExecutor.FormatElapsedTime(elapsed);

    }
}
