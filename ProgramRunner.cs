using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Services.ILOutput;
using FolderDiffIL4DotNet.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace FolderDiffIL4DotNet
{
    /// <summary>
    /// アプリケーション実行全体を調停するランナー。
    /// </summary>
    public sealed class ProgramRunner
    {
        #region constants
        private const string REPORTS_ROOT_DIR_NAME = "Reports";
        private const string INITIALIZING_LOGGER = "Initializing logger...";
        private const string LOGGER_INITIALIZED = "Logger initialized.";
        private const string APPLICATION_VERSION = "Application version: {0}";
        private const string VALIDATING_ARGS = "Validating command line arguments...";
        private const string ERROR_INSUFFICIENT_ARGUMENTS = "Insufficient arguments.";
        private const string ERROR_ARGUMENTS_NULL_OR_EMPTY = "One or more required arguments are null or empty.";
        private const string ERROR_INVALID_ARGUMENTS_USAGE = "Invalid arguments. Usage: " + Constants.APP_NAME + $" <oldFolderAbsolutePath> <newFolderAbsolutePath> <reportLabel> [{NO_PAUSE}]";
        private const string ERROR_INVALID_REPORT_LABEL = "The value '{0}', provided as the third argument (reportLabel), is invalid as a folder name.";
        private const string ERROR_OLD_FOLDER_NOT_FOUND = "The old folder path does not exist: {0}";
        private const string ERROR_NEW_FOLDER_NOT_FOUND = "The new folder path does not exist: {0}";
        private const string ERROR_REPORT_FOLDER_EXISTS = "The report folder already exists: {0}. Provide a different report label.";
        private const string LOG_ARGS_VALIDATION_COMPLETED = "Command line arguments validation completed.";
        private const string LOG_LOADING_CONFIGURATION = "Loading configuration...";
        private const string LOG_CONFIGURATION_LOADED = "Configuration loaded successfully.";
        private const string LOG_APP_STARTING = "Starting " + Constants.APP_NAME + "...";
        private const string LOG_APP_FINISHED = Constants.APP_NAME + " finished without errors. See Reports folder for details.";
        private const string LOG_ERROR_DETAILS_PATH = "Error details logged to: {0}";
        private const string NO_PAUSE = "--no-pause";
        private const string PRESS_ANY_KEY = "Press any key to exit...";
        private const string ERROR_KEY_PROMPT = "An error occurred during key prompt.";
        private const int EXIT_CODE_SUCCESS = 0;
        private const int EXIT_CODE_ERROR = 1;
        private const int IL_CACHE_MAX_MEMORY_ENTRIES_DEFAULT = 2000;
        private const int IL_CACHE_STATS_LOG_INTERVAL_DEFAULT_SECONDS = 60;
        private const int IL_CACHE_TIME_TO_LIVE_DEFAULT_HOURS = 12;
        #endregion

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
            var exitCode = EXIT_CODE_SUCCESS;
            try
            {
                Console.WriteLine(INITIALIZING_LOGGER);
                _logger.Initialize();
                _logger.LogMessage(AppLogLevel.Info, LOGGER_INITIALIZED, shouldOutputMessageToConsole: true);

                var appVersion = SystemInfo.GetAppVersion(typeof(Program));
                _logger.LogMessage(AppLogLevel.Info, string.Format(APPLICATION_VERSION, appVersion), shouldOutputMessageToConsole: true);

                var computerName = SystemInfo.GetComputerName();

                _logger.LogMessage(AppLogLevel.Info, VALIDATING_ARGS, shouldOutputMessageToConsole: true);
                var runArguments = ValidateAndBuildRunArguments(args);

                _logger.LogMessage(AppLogLevel.Info, LOG_ARGS_VALIDATION_COMPLETED, shouldOutputMessageToConsole: true);

                ConsoleBanner.Print();
                Directory.CreateDirectory(runArguments.ReportsFolderAbsolutePath);

                _logger.LogMessage(AppLogLevel.Info, LOG_LOADING_CONFIGURATION, shouldOutputMessageToConsole: true);
                var config = await _configService.LoadConfigAsync();
                _logger.LogMessage(AppLogLevel.Info, LOG_CONFIGURATION_LOADED, shouldOutputMessageToConsole: true);

                _logger.CleanupOldLogFiles(config.MaxLogGenerations);
                TimestampCache.Clear();
                _logger.LogMessage(AppLogLevel.Info, LOG_APP_STARTING, shouldOutputMessageToConsole: true);

                var executionContext = BuildExecutionContext(runArguments, config);
                using var runProvider = BuildRunServiceProvider(config, executionContext);
                using var scope = runProvider.CreateScope();
                var scopedProvider = scope.ServiceProvider;
                var progressReporter = scopedProvider.GetRequiredService<ProgressReportService>();

                string elapsedTimeString;
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    await scopedProvider.GetRequiredService<IFolderDiffService>().ExecuteFolderDiffAsync();
                    stopwatch.Stop();
                    elapsedTimeString = FormatElapsedTime(stopwatch.Elapsed);
                    _logger.LogMessage(AppLogLevel.Info, string.Format(Constants.LOG_ELAPSED_TIME, elapsedTimeString), shouldOutputMessageToConsole: true);
                }
                finally
                {
                    progressReporter.Dispose();
                }

                scopedProvider.GetRequiredService<ReportGenerateService>().GenerateDiffReport(
                    executionContext.OldFolderAbsolutePath,
                    executionContext.NewFolderAbsolutePath,
                    executionContext.ReportsFolderAbsolutePath,
                    appVersion,
                    elapsedTimeString,
                    computerName,
                    config);

                _logger.LogMessage(AppLogLevel.Info, LOG_APP_FINISHED, shouldOutputMessageToConsole: true, ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                _logger.LogMessage(AppLogLevel.Error, ex.Message, shouldOutputMessageToConsole: true, ConsoleColor.Red, ex);
                _logger.LogMessage(AppLogLevel.Info, string.Format(LOG_ERROR_DETAILS_PATH, _logger.LogFileAbsolutePath), shouldOutputMessageToConsole: true);
                exitCode = EXIT_CODE_ERROR;
            }
            finally
            {
                if ((args?.Any(arg => string.Equals(arg, NO_PAUSE, StringComparison.OrdinalIgnoreCase)) ?? false)
                    || Console.IsInputRedirected
                    || Console.IsOutputRedirected
                    || Console.IsErrorRedirected)
                {
                    // do nothing
                }
                else
                {
                    try
                    {
                        Console.WriteLine(PRESS_ANY_KEY);
                        Console.ReadKey(true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogMessage(AppLogLevel.Error, ERROR_KEY_PROMPT, shouldOutputMessageToConsole: false, ex);
                    }
                }
            }

            return exitCode;
        }

        private RunArguments ValidateAndBuildRunArguments(string[] args)
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

            var oldFolderAbsolutePath = args[0];
            var newFolderAbsolutePath = args[1];
            var reportLabel = args[2];

            try
            {
                PathValidator.ValidateFolderNameOrThrow(reportLabel, nameof(reportLabel));
            }
            catch (ArgumentException)
            {
                _logger.LogMessage(AppLogLevel.Error, string.Format(ERROR_INVALID_REPORT_LABEL, reportLabel), shouldOutputMessageToConsole: true);
                throw;
            }

            string reportsRootDirAbsolutePath = Path.Combine(AppContext.BaseDirectory, REPORTS_ROOT_DIR_NAME);
            Directory.CreateDirectory(reportsRootDirAbsolutePath);
            string reportsFolderAbsolutePath = Path.Combine(reportsRootDirAbsolutePath, reportLabel);

            if (!Directory.Exists(oldFolderAbsolutePath))
            {
                throw new DirectoryNotFoundException(string.Format(ERROR_OLD_FOLDER_NOT_FOUND, oldFolderAbsolutePath));
            }
            if (!Directory.Exists(newFolderAbsolutePath))
            {
                throw new DirectoryNotFoundException(string.Format(ERROR_NEW_FOLDER_NOT_FOUND, newFolderAbsolutePath));
            }
            if (Directory.Exists(reportsFolderAbsolutePath))
            {
                throw new ArgumentException(string.Format(ERROR_REPORT_FOLDER_EXISTS, reportsFolderAbsolutePath));
            }

            return new RunArguments(oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath);
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
            services.AddScoped<IILTextOutputService, ILTextOutputService>();
            services.AddScoped<IDotNetDisassembleService, DotNetDisassembleService>();
            services.AddScoped<IILOutputService, ILOutputService>();
            services.AddScoped<IFileDiffService, FileDiffService>();
            services.AddScoped<IFolderDiffService, FolderDiffService>();
            return services.BuildServiceProvider();
        }

        private static ILCache CreateIlCache(ConfigSettings config, ILoggerService logger)
        {
            if (!config.EnableILCache)
            {
                return null;
            }

            return new ILCache(
                string.IsNullOrWhiteSpace(config.ILCacheDirectoryAbsolutePath) ? Path.Combine(AppContext.BaseDirectory, Constants.DEFAULT_IL_CACHE_DIR_NAME) : config.ILCacheDirectoryAbsolutePath,
                logger,
                ilCacheMaxMemoryEntries: IL_CACHE_MAX_MEMORY_ENTRIES_DEFAULT,
                timeToLive: TimeSpan.FromHours(IL_CACHE_TIME_TO_LIVE_DEFAULT_HOURS),
                statsLogIntervalSeconds: config.ILCacheStatsLogIntervalSeconds <= 0 ? IL_CACHE_STATS_LOG_INTERVAL_DEFAULT_SECONDS : config.ILCacheStatsLogIntervalSeconds,
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
    }
}
