using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    /// アプリケーション実行全体を調停するランナー。
    /// </summary>
    public sealed class ProgramRunner
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
        private const string HELP_TEXT =
            "Usage: " + Constants.APP_NAME + " <oldFolder> <newFolder> <reportLabel> [options]\n\n" +
            "Arguments:\n" +
            "  <oldFolder>    Absolute path to the baseline (old) folder.\n" +
            "  <newFolder>    Absolute path to the comparison (new) folder.\n" +
            "  <reportLabel>  Label used as the subfolder name under Reports/.\n\n" +
            "Options:\n" +
            "  --help, -h                  Show this help message and exit.\n" +
            "  --version                   Show the application version and exit.\n" +
            "  --no-pause                  Skip key-wait at process end.\n" +
            "  --config <path>             Path to config.json (default: <exe>/config.json).\n" +
            "  --threads <N>               Override MaxParallelism (0 = auto).\n" +
            "  --no-il-cache               Disable the IL cache for this run.\n" +
            "  --skip-il                   Skip IL comparison for .NET assemblies.\n" +
            "  --no-timestamp-warnings     Suppress timestamp-regression warnings.\n\n" +
            "Exit codes:\n" +
            "  0  Success.\n" +
            "  2  Invalid arguments or input paths.\n" +
            "  3  Configuration load or parse error.\n" +
            "  4  Diff execution or report generation failure.\n" +
            "  1  Unexpected internal error.";

        private readonly ILoggerService _logger;
        private readonly ConfigService _configService;

        /// <summary>
        /// 依存サービスを受け取り初期化します。
        /// </summary>
        /// <param name="logger">ログ出力サービス。</param>
        /// <param name="configService">設定読み込みサービス。</param>
        public ProgramRunner(ILoggerService logger, ConfigService configService)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(configService);

            _logger = logger;
            _configService = configService;
        }

        /// <summary>
        /// アプリケーションのメインフローを実行し、終了コードを返します。
        /// </summary>
        /// <param name="args">コマンドライン引数。</param>
        /// <returns>プロセス終了コード。</returns>
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

            var result = await RunWithResultAsync(args, opts);
            OutputCompletionWarnings(result.HasMd5MismatchWarnings, result.HasTimestampRegressionWarnings);
            PromptForExitKeyIfNeeded(opts);
            return (int)result.ExitCode;
        }

        /// <summary>
        /// 実行全体を型付き結果へ変換し、公開 API である終了コードへ写像する境界処理です。
        /// </summary>
        /// <param name="args">コマンドライン引数。</param>
        /// <param name="opts">解析済み CLI オプション。</param>
        /// <returns>成功/失敗種別と補助情報を含む実行結果。</returns>
        private async Task<ProgramRunResult> RunWithResultAsync(string[] args, CliOptions opts)
        {
            #pragma warning disable CA1031 // Top-level application boundary classifies unexpected failures after logging.
            try
            {
                var appVersion = InitializeLoggerAndGetAppVersion();
                var computerName = SystemInfo.GetComputerName();

                var runArgumentsResult = TryValidateAndBuildRunArguments(args, opts);
                if (!runArgumentsResult.IsSuccess)
                {
                    return runArgumentsResult.Failure;
                }

                var runArguments = runArgumentsResult.Value;
                var prepareReportsDirectoryResult = TryPrepareReportsDirectory(runArguments.ReportsFolderAbsolutePath);
                if (!prepareReportsDirectoryResult.IsSuccess)
                {
                    return prepareReportsDirectoryResult.Failure;
                }

                var configResult = await TryLoadConfigurationAsync(opts.ConfigPath);
                if (!configResult.IsSuccess)
                {
                    return configResult.Failure;
                }

                var config = configResult.Value;
                ApplyCliOverrides(config, opts);

                var completionStateResult = await TryExecuteRunAsync(runArguments, config, appVersion, computerName);
                if (!completionStateResult.IsSuccess)
                {
                    return completionStateResult.Failure;
                }

                _logger.LogMessage(AppLogLevel.Info, LOG_APP_FINISHED, shouldOutputMessageToConsole: true, ConsoleColor.Green);
                return ProgramRunResult.Success(completionStateResult.Value);
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

        private void OutputCompletionWarnings(bool hasMd5MismatchWarnings, bool hasTimestampRegressionWarnings)
        {
            if (hasMd5MismatchWarnings)
            {
                _logger.LogMessage(AppLogLevel.Warning, Constants.WARNING_MD5_MISMATCH, shouldOutputMessageToConsole: true, ConsoleColor.Yellow);
            }

            if (!hasTimestampRegressionWarnings)
            {
                return;
            }

            _logger.LogMessage(AppLogLevel.Warning, WARNING_NEW_FILE_TIMESTAMP_OLDER_THAN_OLD, shouldOutputMessageToConsole: true, ConsoleColor.Yellow);
        }

        /// <summary>
        /// CLI 引数検証フェーズを型付き結果として返します。
        /// </summary>
        /// <param name="args">コマンドライン引数。</param>
        /// <param name="opts">解析済み CLI オプション。</param>
        /// <returns>成功時は実行引数、失敗時は入力不正の実行結果。</returns>
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
                RunPreflightValidator.ValidateRunDirectories(oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath);
                _logger.LogMessage(AppLogLevel.Info, LOG_ARGS_VALIDATION_COMPLETED, shouldOutputMessageToConsole: true);
                return StepResult<RunArguments>.FromValue(new RunArguments(oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath));
            }
            catch (ArgumentException ex)
            {
                return StepResult<RunArguments>.FromFailure(CreateFailureResult(ProgramExitCode.InvalidArguments, ex));
            }
            catch (DirectoryNotFoundException ex)
            {
                return StepResult<RunArguments>.FromFailure(CreateFailureResult(ProgramExitCode.InvalidArguments, ex));
            }
            catch (IOException ex)
            {
                return StepResult<RunArguments>.FromFailure(CreateFailureResult(ProgramExitCode.InvalidArguments, ex));
            }
            catch (UnauthorizedAccessException ex)
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
        /// レポート出力先ディレクトリの初期化を型付き結果として返します。
        /// </summary>
        /// <param name="reportsFolderAbsolutePath">今回のレポート出力先ディレクトリ。</param>
        /// <returns>成功/失敗を表す結果。</returns>
        private StepResult<bool> TryPrepareReportsDirectory(string reportsFolderAbsolutePath)
        {
            try
            {
                PrepareReportsDirectory(reportsFolderAbsolutePath);
                return StepResult<bool>.FromValue(true);
            }
            catch (ArgumentException ex)
            {
                return StepResult<bool>.FromFailure(CreateFailureResult(ProgramExitCode.ExecutionFailed, ex));
            }
            catch (IOException ex)
            {
                return StepResult<bool>.FromFailure(CreateFailureResult(ProgramExitCode.ExecutionFailed, ex));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StepResult<bool>.FromFailure(CreateFailureResult(ProgramExitCode.ExecutionFailed, ex));
            }
            catch (NotSupportedException ex)
            {
                return StepResult<bool>.FromFailure(CreateFailureResult(ProgramExitCode.ExecutionFailed, ex));
            }
        }

        /// <summary>
        /// 設定読込フェーズを型付き結果として返します。
        /// </summary>
        /// <param name="configPath">config.json の絶対パス。null の場合は既定パスを使用。</param>
        /// <returns>成功時は読み込んだ設定、失敗時は設定不正の実行結果。</returns>
        private async Task<StepResult<ConfigSettings>> TryLoadConfigurationAsync(string configPath)
        {
            try
            {
                var config = await LoadConfigurationAsync(configPath);
                return StepResult<ConfigSettings>.FromValue(config);
            }
            catch (FileNotFoundException ex)
            {
                return StepResult<ConfigSettings>.FromFailure(CreateFailureResult(ProgramExitCode.ConfigurationError, ex));
            }
            catch (InvalidDataException ex)
            {
                return StepResult<ConfigSettings>.FromFailure(CreateFailureResult(ProgramExitCode.ConfigurationError, ex));
            }
            catch (IOException ex)
            {
                return StepResult<ConfigSettings>.FromFailure(CreateFailureResult(ProgramExitCode.ConfigurationError, ex));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StepResult<ConfigSettings>.FromFailure(CreateFailureResult(ProgramExitCode.ConfigurationError, ex));
            }
            catch (NotSupportedException ex)
            {
                return StepResult<ConfigSettings>.FromFailure(CreateFailureResult(ProgramExitCode.ConfigurationError, ex));
            }
        }

        /// <summary>
        /// 差分実行とレポート生成フェーズを型付き結果として返します。
        /// </summary>
        /// <param name="runArguments">検証済みの実行引数。</param>
        /// <param name="config">読込済み設定。</param>
        /// <param name="appVersion">アプリケーションバージョン。</param>
        /// <param name="computerName">実行マシン名。</param>
        /// <returns>成功時は完了状態、失敗時は実行失敗の結果。</returns>
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
            catch (ArgumentException ex)
            {
                return StepResult<RunCompletionState>.FromFailure(CreateFailureResult(ProgramExitCode.ExecutionFailed, ex));
            }
            catch (DirectoryNotFoundException ex)
            {
                return StepResult<RunCompletionState>.FromFailure(CreateFailureResult(ProgramExitCode.ExecutionFailed, ex));
            }
            catch (FileNotFoundException ex)
            {
                return StepResult<RunCompletionState>.FromFailure(CreateFailureResult(ProgramExitCode.ExecutionFailed, ex));
            }
            catch (InvalidOperationException ex)
            {
                return StepResult<RunCompletionState>.FromFailure(CreateFailureResult(ProgramExitCode.ExecutionFailed, ex));
            }
            catch (IOException ex)
            {
                return StepResult<RunCompletionState>.FromFailure(CreateFailureResult(ProgramExitCode.ExecutionFailed, ex));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StepResult<RunCompletionState>.FromFailure(CreateFailureResult(ProgramExitCode.ExecutionFailed, ex));
            }
            catch (NotSupportedException ex)
            {
                return StepResult<RunCompletionState>.FromFailure(CreateFailureResult(ProgramExitCode.ExecutionFailed, ex));
            }
        }

        /// <summary>
        /// 失敗種別をログ出力付きの型付き結果へ変換します。
        /// </summary>
        /// <param name="exitCode">失敗を表す終了コード。</param>
        /// <param name="exception">元例外。</param>
        /// <returns>ログ済みの失敗結果。</returns>
        private ProgramRunResult CreateFailureResult(ProgramExitCode exitCode, Exception exception)
        {
            _logger.LogMessage(AppLogLevel.Error, exception.Message, shouldOutputMessageToConsole: true, ConsoleColor.Red, exception);
            _logger.LogMessage(AppLogLevel.Info, $"Error details logged to: {_logger.LogFileAbsolutePath}", shouldOutputMessageToConsole: true);
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

        private async Task<ConfigSettings> LoadConfigurationAsync(string configPath)
        {
            _logger.LogMessage(AppLogLevel.Info, LOG_LOADING_CONFIGURATION, shouldOutputMessageToConsole: true);
            var config = await _configService.LoadConfigAsync(configPath);
            _logger.LogMessage(AppLogLevel.Info, LOG_CONFIGURATION_LOADED, shouldOutputMessageToConsole: true);
            _logger.CleanupOldLogFiles(config.MaxLogGenerations);
            TimestampCache.Clear();
            _logger.LogMessage(AppLogLevel.Info, LOG_APP_STARTING, shouldOutputMessageToConsole: true);
            return config;
        }

        private async Task<RunCompletionState> ExecuteScopedRunAsync(
            IServiceProvider scopedProvider,
            DiffExecutionContext executionContext,
            string appVersion,
            string computerName,
            ConfigSettings config)
        {
            var resultLists = scopedProvider.GetRequiredService<FileDiffResultLists>();
            var elapsedTimeString = await ExecuteDiffAsync(scopedProvider);
            GenerateReport(scopedProvider, executionContext, appVersion, elapsedTimeString, computerName, config);
            return new RunCompletionState(resultLists.HasAnyMd5Mismatch, resultLists.HasAnyNewFileTimestampOlderThanOldWarning);
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
            scopedProvider.GetRequiredService<ReportGenerateService>().GenerateDiffReport(
                executionContext.OldFolderAbsolutePath,
                executionContext.NewFolderAbsolutePath,
                executionContext.ReportsFolderAbsolutePath,
                appVersion,
                elapsedTimeString,
                computerName,
                config,
                ilCache);
            scopedProvider.GetRequiredService<HtmlReportGenerateService>().GenerateDiffReportHtml(
                executionContext.OldFolderAbsolutePath,
                executionContext.NewFolderAbsolutePath,
                executionContext.ReportsFolderAbsolutePath,
                appVersion,
                elapsedTimeString,
                computerName,
                config,
                ilCache);
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
            catch (InvalidOperationException ex)
            {
                _logger.LogMessage(AppLogLevel.Error, ERROR_KEY_PROMPT, shouldOutputMessageToConsole: false, ex);
            }
            catch (IOException ex)
            {
                _logger.LogMessage(AppLogLevel.Error, ERROR_KEY_PROMPT, shouldOutputMessageToConsole: false, ex);
            }
        }

        private static bool ShouldSkipExitPrompt(CliOptions opts)
            => opts.NoPause
                || Console.IsInputRedirected
                || Console.IsOutputRedirected
                || Console.IsErrorRedirected;

        private static ILCache CreateIlCache(ConfigSettings config, ILoggerService logger)
        {
            return RunScopeBuilder.CreateIlCache(config, logger);
        }

        /// <summary>
        /// 経過時間を人間が判読しやすい形式（例: <c>0h 5m 30.1s</c>）に変換します。
        /// 秒は小数点以下 1 桁（1/10 秒単位、切り捨て）まで表示します。
        /// </summary>
        /// <param name="elapsed">変換する経過時間。</param>
        /// <returns><c>{h}h {m}m {s.d}s</c> 形式の文字列。</returns>
        internal static string FormatElapsedTime(TimeSpan elapsed)
        {
            int hours = (int)Math.Floor(elapsed.TotalHours);
            int minutes = elapsed.Minutes;
            int seconds = elapsed.Seconds;
            int tenths = elapsed.Milliseconds / 100;
            return $"{hours}h {minutes}m {seconds}.{tenths}s";
        }

        /// <summary>
        /// CLI オプションの値で <see cref="ConfigSettings"/> を上書きします。
        /// config.json よりも CLI フラグを優先させます。
        /// </summary>
        /// <param name="config">対象の設定オブジェクト。</param>
        /// <param name="opts">解析済み CLI オプション。</param>
        private static void ApplyCliOverrides(ConfigSettings config, CliOptions opts)
        {
            if (opts.ThreadsOverride.HasValue)
            {
                config.MaxParallelism = opts.ThreadsOverride.Value;
            }

            if (opts.NoIlCache)
            {
                config.EnableILCache = false;
            }

            if (opts.SkipIL)
            {
                config.SkipIL = true;
            }

            if (opts.NoTimestampWarnings)
            {
                config.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = false;
            }
        }

        private sealed record RunArguments(string OldFolderAbsolutePath, string NewFolderAbsolutePath, string ReportsFolderAbsolutePath);

        private sealed record RunCompletionState(bool HasMd5MismatchWarnings, bool HasTimestampRegressionWarnings);

        /// <summary>
        /// コンソールアプリの公開終了コードを定義します。
        /// </summary>
        private enum ProgramExitCode
        {
            /// <summary>
            /// 正常終了です。
            /// </summary>
            Success = 0,

            /// <summary>
            /// CLI 引数または入力パスが不正です。
            /// </summary>
            InvalidArguments = 2,

            /// <summary>
            /// 設定ファイルの不備または読込失敗です。
            /// </summary>
            ConfigurationError = 3,

            /// <summary>
            /// 差分実行またはレポート生成に失敗しました。
            /// </summary>
            ExecutionFailed = 4,

            /// <summary>
            /// 分類不能な想定外エラーです。
            /// </summary>
            UnexpectedError = 1
        }

        /// <summary>
        /// 実行全体の成功/失敗を表す結果モデルです。
        /// </summary>
        private sealed class ProgramRunResult
        {
            /// <summary>
            /// 失敗時に返す共通の警告なし状態です。
            /// </summary>
            private static readonly RunCompletionState _noWarnings = new(false, false);

            /// <summary>
            /// 実行結果の終了コードです。
            /// </summary>
            public ProgramExitCode ExitCode { get; }

            /// <summary>
            /// MD5 不一致の終了時警告有無です。
            /// </summary>
            public bool HasMd5MismatchWarnings { get; }

            /// <summary>
            /// 更新日時逆転の終了時警告有無です。
            /// </summary>
            public bool HasTimestampRegressionWarnings { get; }

            /// <summary>
            /// 成功時の結果を生成します。
            /// </summary>
            /// <param name="completionState">集約済みの完了状態。</param>
            /// <returns>成功結果。</returns>
            public static ProgramRunResult Success(RunCompletionState completionState)
                => new(ProgramExitCode.Success, completionState);

            /// <summary>
            /// 失敗時の結果を生成します。
            /// </summary>
            /// <param name="exitCode">失敗種別の終了コード。</param>
            /// <returns>失敗結果。</returns>
            public static ProgramRunResult Failure(ProgramExitCode exitCode)
                => new(exitCode, _noWarnings);

            /// <summary>
            /// 実行結果を初期化します。
            /// </summary>
            /// <param name="exitCode">終了コード。</param>
            /// <param name="completionState">終了時警告の集約状態。</param>
            private ProgramRunResult(ProgramExitCode exitCode, RunCompletionState completionState)
            {
                ExitCode = exitCode;
                HasMd5MismatchWarnings = completionState.HasMd5MismatchWarnings;
                HasTimestampRegressionWarnings = completionState.HasTimestampRegressionWarnings;
            }
        }

        /// <summary>
        /// 各実行フェーズの成功値または失敗結果を保持する簡易 Result 型です。
        /// </summary>
        /// <typeparam name="TValue">成功時の値型。</typeparam>
        private sealed class StepResult<TValue>
        {
            /// <summary>
            /// フェーズが成功したかどうかを示します。
            /// </summary>
            public bool IsSuccess { get; }

            /// <summary>
            /// 成功時の値です。
            /// </summary>
            public TValue Value { get; }

            /// <summary>
            /// 失敗時の実行結果です。
            /// </summary>
            public ProgramRunResult Failure { get; }

            /// <summary>
            /// 成功値から結果を生成します。
            /// </summary>
            /// <param name="value">成功時の値。</param>
            /// <returns>成功結果。</returns>
            public static StepResult<TValue> FromValue(TValue value)
                => new(true, value, null);

            /// <summary>
            /// 失敗結果から結果を生成します。
            /// </summary>
            /// <param name="failure">失敗結果。</param>
            /// <returns>失敗結果。</returns>
            public static StepResult<TValue> FromFailure(ProgramRunResult failure)
                => new(false, default, failure);

            /// <summary>
            /// フェーズ結果を初期化します。
            /// </summary>
            /// <param name="isSuccess">成功可否。</param>
            /// <param name="value">成功時の値。</param>
            /// <param name="failure">失敗時の実行結果。</param>
            private StepResult(bool isSuccess, TValue value, ProgramRunResult failure)
            {
                IsSuccess = isSuccess;
                Value = value;
                Failure = failure;
            }
        }
    }
}
