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
        private const string REPORTS_ROOT_DIR_NAME = "Reports";
        private const string INITIALIZING_LOGGER = "Initializing logger...";
        private const string LOGGER_INITIALIZED = "Logger initialized.";
        private const string VALIDATING_ARGS = "Validating command line arguments...";
        private const string ERROR_INSUFFICIENT_ARGUMENTS = "Insufficient arguments.";
        private const string ERROR_ARGUMENTS_NULL_OR_EMPTY = "One or more required arguments are null or empty.";
        private const string ERROR_INVALID_ARGUMENTS_USAGE = "Invalid arguments. Usage: " + Constants.APP_NAME + $" <oldFolderAbsolutePath> <newFolderAbsolutePath> <reportLabel> [{NO_PAUSE}]";
        private const string LOG_ARGS_VALIDATION_COMPLETED = "Command line arguments validation completed.";
        private const string LOG_LOADING_CONFIGURATION = "Loading configuration...";
        private const string LOG_CONFIGURATION_LOADED = "Configuration loaded successfully.";
        private const string LOG_APP_STARTING = "Starting " + Constants.APP_NAME + "...";
        private const string LOG_APP_FINISHED = Constants.APP_NAME + " finished without errors. See Reports folder for details.";
        private const string NO_PAUSE = "--no-pause";
        private const string PRESS_ANY_KEY = "Press any key to exit...";
        private const string ERROR_KEY_PROMPT = "An error occurred during key prompt.";
        private const string WARNING_NEW_FILE_TIMESTAMP_OLDER_THAN_OLD = "One or more files in 'new' have older last-modified timestamps than the corresponding files in 'old'. See diff_report.md for details.";

        private readonly ILoggerService _logger;
        private readonly ConfigService _configService;

        public ProgramRunner(ILoggerService logger, ConfigService configService)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(configService);

            _logger = logger;
            _configService = configService;
        }

        public async Task<int> RunAsync(string[] args)
        {
            var result = await RunWithResultAsync(args);
            OutputCompletionWarnings(result.HasMd5MismatchWarnings, result.HasTimestampRegressionWarnings);
            PromptForExitKeyIfNeeded(args);
            return (int)result.ExitCode;
        }

        /// <summary>
        /// 実行全体を型付き結果へ変換し、公開 API である終了コードへ写像する境界処理です。
        /// </summary>
        /// <param name="args">コマンドライン引数。</param>
        /// <returns>成功/失敗種別と補助情報を含む実行結果。</returns>
        private async Task<ProgramRunResult> RunWithResultAsync(string[] args)
        {
            #pragma warning disable CA1031 // Top-level application boundary classifies unexpected failures after logging.
            try
            {
                var appVersion = InitializeLoggerAndGetAppVersion();
                var computerName = SystemInfo.GetComputerName();

                var runArgumentsResult = TryValidateAndBuildRunArguments(args);
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

                var configResult = await TryLoadConfigurationAsync();
                if (!configResult.IsSuccess)
                {
                    return configResult.Failure;
                }

                var completionStateResult = await TryExecuteRunAsync(runArguments, configResult.Value, appVersion, computerName);
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
        /// <returns>成功時は実行引数、失敗時は入力不正の実行結果。</returns>
        private StepResult<RunArguments> TryValidateAndBuildRunArguments(string[] args)
        {
            try
            {
                _logger.LogMessage(AppLogLevel.Info, VALIDATING_ARGS, shouldOutputMessageToConsole: true);
                ValidateRequiredArguments(args);

                var oldFolderAbsolutePath = args[0];
                var newFolderAbsolutePath = args[1];
                var reportLabel = args[2];
                ValidateReportLabel(reportLabel);
                string reportsFolderAbsolutePath = GetReportsFolderAbsolutePath(reportLabel);
                ValidateRunDirectories(oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath);
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
        }

        private static void ValidateRequiredArguments(string[] args)
        {
            try
            {
                if (args == null || args.Length < 3)
                {
                    throw new ArgumentException(ERROR_INSUFFICIENT_ARGUMENTS);
                }

                if (string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
                {
                    throw new ArgumentException(ERROR_ARGUMENTS_NULL_OR_EMPTY);
                }
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(ERROR_INVALID_ARGUMENTS_USAGE, ex);
            }
        }

        private void ValidateReportLabel(string reportLabel)
        {
            try
            {
                PathValidator.ValidateFolderNameOrThrow(reportLabel, nameof(reportLabel));
            }
            catch (ArgumentException)
            {
                _logger.LogMessage(AppLogLevel.Error, $"The value '{reportLabel}', provided as the third argument (reportLabel), is invalid as a folder name.", shouldOutputMessageToConsole: true);
                throw;
            }
        }

        private static string GetReportsFolderAbsolutePath(string reportLabel)
        {
            string reportsRootDirAbsolutePath = Path.Combine(AppContext.BaseDirectory, REPORTS_ROOT_DIR_NAME);
            Directory.CreateDirectory(reportsRootDirAbsolutePath);
            return Path.Combine(reportsRootDirAbsolutePath, reportLabel);
        }

        private static void ValidateRunDirectories(string oldFolderAbsolutePath, string newFolderAbsolutePath, string reportsFolderAbsolutePath)
        {
            if (!Directory.Exists(oldFolderAbsolutePath))
            {
                throw new DirectoryNotFoundException($"The old folder path does not exist: {oldFolderAbsolutePath}");
            }

            if (!Directory.Exists(newFolderAbsolutePath))
            {
                throw new DirectoryNotFoundException($"The new folder path does not exist: {newFolderAbsolutePath}");
            }

            if (Directory.Exists(reportsFolderAbsolutePath))
            {
                throw new ArgumentException($"The report folder already exists: {reportsFolderAbsolutePath}. Provide a different report label.");
            }
        }

        private DiffExecutionContext BuildExecutionContext(RunArguments runArguments, ConfigSettings config)
        {
            bool detectedNetworkOld = config.AutoDetectNetworkShares && FileSystemUtility.IsLikelyNetworkPath(runArguments.OldFolderAbsolutePath);
            bool detectedNetworkNew = config.AutoDetectNetworkShares && FileSystemUtility.IsLikelyNetworkPath(runArguments.NewFolderAbsolutePath);
            bool optimizeForNetworkShares = config.OptimizeForNetworkShares || detectedNetworkOld || detectedNetworkNew;

            return new DiffExecutionContext(
                runArguments.OldFolderAbsolutePath,
                runArguments.NewFolderAbsolutePath,
                runArguments.ReportsFolderAbsolutePath,
                optimizeForNetworkShares,
                detectedNetworkOld,
                detectedNetworkNew);
        }

        private ServiceProvider BuildRunServiceProvider(ConfigSettings config, DiffExecutionContext executionContext)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerService>(_logger);
            services.AddSingleton(config);
            services.AddSingleton(executionContext);
            services.AddScoped<FileDiffResultLists>();
            services.AddScoped<DotNetDisassemblerCache>();
            services.AddScoped<ILCache>(sp => CreateIlCache(config, sp.GetRequiredService<ILoggerService>()));
            services.AddScoped<ProgressReportService>();
            services.AddScoped<ReportGenerateService>();
            services.AddScoped<IFileSystemService, FileSystemService>();
            services.AddScoped<IFolderDiffExecutionStrategy, FolderDiffExecutionStrategy>();
            services.AddScoped<IFileComparisonService, FileComparisonService>();
            services.AddScoped<IILTextOutputService, ILTextOutputService>();
            services.AddScoped<IDotNetDisassembleService, DotNetDisassembleService>();
            services.AddScoped<IILOutputService, ILOutputService>();
            services.AddScoped<IFileDiffService, FileDiffService>();
            services.AddScoped<IFolderDiffService, FolderDiffService>();
            return services.BuildServiceProvider();
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
        /// <returns>成功時は読み込んだ設定、失敗時は設定不正の実行結果。</returns>
        private async Task<StepResult<ConfigSettings>> TryLoadConfigurationAsync()
        {
            try
            {
                var config = await LoadConfigurationAsync();
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

        private async Task<ConfigSettings> LoadConfigurationAsync()
        {
            _logger.LogMessage(AppLogLevel.Info, LOG_LOADING_CONFIGURATION, shouldOutputMessageToConsole: true);
            var config = await _configService.LoadConfigAsync();
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
            scopedProvider.GetRequiredService<ReportGenerateService>().GenerateDiffReport(
                executionContext.OldFolderAbsolutePath,
                executionContext.NewFolderAbsolutePath,
                executionContext.ReportsFolderAbsolutePath,
                appVersion,
                elapsedTimeString,
                computerName,
                config);
        }

        private void PromptForExitKeyIfNeeded(string[] args)
        {
            if (ShouldSkipExitPrompt(args))
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

        private static bool ShouldSkipExitPrompt(string[] args)
            => (args?.Any(arg => string.Equals(arg, NO_PAUSE, StringComparison.OrdinalIgnoreCase)) ?? false)
                || Console.IsInputRedirected
                || Console.IsOutputRedirected
                || Console.IsErrorRedirected;

        private static ILCache CreateIlCache(ConfigSettings config, ILoggerService logger)
        {
            if (!config.EnableILCache)
            {
                return null;
            }

            // 起動引数や config.json では露出していないメモリ件数と TTL は、
            // コンソール実行の再利用効率と常駐コストのバランスを取る共通既定値を使う。
            return new ILCache(
                string.IsNullOrWhiteSpace(config.ILCacheDirectoryAbsolutePath) ? Path.Combine(AppContext.BaseDirectory, Constants.DEFAULT_IL_CACHE_DIR_NAME) : config.ILCacheDirectoryAbsolutePath,
                logger,
                ilCacheMaxMemoryEntries: Constants.IL_CACHE_MAX_MEMORY_ENTRIES_DEFAULT,
                timeToLive: TimeSpan.FromHours(Constants.IL_CACHE_TIME_TO_LIVE_DEFAULT_HOURS),
                statsLogIntervalSeconds: config.ILCacheStatsLogIntervalSeconds <= 0 ? Constants.IL_CACHE_STATS_LOG_INTERVAL_DEFAULT_SECONDS : config.ILCacheStatsLogIntervalSeconds,
                ilCacheMaxDiskFileCount: config.ILCacheMaxDiskFileCount,
                ilCacheMaxDiskMegabytes: config.ILCacheMaxDiskMegabytes);
        }

        private static string FormatElapsedTime(TimeSpan elapsed)
        {
            string hourString = $"{(int)Math.Floor(elapsed.TotalHours):00}";
            string minuteString = $"{elapsed.Minutes:00}";
            string secondString = $"{elapsed.Seconds:00}";
            string millisecondString = $"{elapsed.Milliseconds:000}";
            return $"{hourString}:{minuteString}:{secondString}.{millisecondString}";
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
