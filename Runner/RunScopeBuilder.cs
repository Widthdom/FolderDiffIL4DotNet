using System;
using System.IO;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Services.ILOutput;
using Microsoft.Extensions.DependencyInjection;

namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// 1 回の差分実行スコープに必要な DI コンテナと実行コンテキストを構築する静的クラスです。
    /// </summary>
    internal static class RunScopeBuilder
    {
        /// <summary>
        /// 実行引数と設定から <see cref="DiffExecutionContext"/> を構築します。
        /// </summary>
        internal static DiffExecutionContext BuildExecutionContext(
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            ConfigSettings config)
        {
            bool detectedNetworkOld = config.AutoDetectNetworkShares && FileSystemUtility.IsLikelyNetworkPath(oldFolderAbsolutePath);
            bool detectedNetworkNew = config.AutoDetectNetworkShares && FileSystemUtility.IsLikelyNetworkPath(newFolderAbsolutePath);
            bool optimizeForNetworkShares = config.OptimizeForNetworkShares || detectedNetworkOld || detectedNetworkNew;

            return new DiffExecutionContext(
                oldFolderAbsolutePath,
                newFolderAbsolutePath,
                reportsFolderAbsolutePath,
                optimizeForNetworkShares,
                detectedNetworkOld,
                detectedNetworkNew);
        }

        /// <summary>
        /// 差分実行スコープ用の <see cref="ServiceProvider"/> を構築します。
        /// </summary>
        internal static ServiceProvider Build(ConfigSettings config, DiffExecutionContext executionContext, ILoggerService logger)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerService>(logger);
            services.AddSingleton(config);
            services.AddSingleton(executionContext);
            services.AddScoped<FileDiffResultLists>();
            services.AddScoped<DotNetDisassemblerCache>();
            services.AddScoped<ILCache>(sp => CreateIlCache(config, sp.GetRequiredService<ILoggerService>()));
            services.AddScoped<ProgressReportService>();
            services.AddScoped<ReportGenerateService>();
            services.AddScoped<HtmlReportGenerateService>();
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
        /// 設定に基づいて <see cref="ILCache"/> を生成します。キャッシュが無効な場合は null を返します。
        /// </summary>
        internal static ILCache CreateIlCache(ConfigSettings config, ILoggerService logger)
        {
            if (!config.EnableILCache)
            {
                return null;
            }

            return new ILCache(
                string.IsNullOrWhiteSpace(config.ILCacheDirectoryAbsolutePath) ? Path.Combine(AppContext.BaseDirectory, Constants.DEFAULT_IL_CACHE_DIR_NAME) : config.ILCacheDirectoryAbsolutePath,
                logger,
                ilCacheMaxMemoryEntries: Constants.IL_CACHE_MAX_MEMORY_ENTRIES_DEFAULT,
                timeToLive: TimeSpan.FromHours(Constants.IL_CACHE_TIME_TO_LIVE_DEFAULT_HOURS),
                statsLogIntervalSeconds: config.ILCacheStatsLogIntervalSeconds <= 0 ? Constants.IL_CACHE_STATS_LOG_INTERVAL_DEFAULT_SECONDS : config.ILCacheStatsLogIntervalSeconds,
                ilCacheMaxDiskFileCount: config.ILCacheMaxDiskFileCount,
                ilCacheMaxDiskMegabytes: config.ILCacheMaxDiskMegabytes);
        }
    }
}
