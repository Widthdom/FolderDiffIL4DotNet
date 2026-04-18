using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Plugin.Abstractions;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Services.ILOutput;
using Microsoft.Extensions.DependencyInjection;

namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// Executes the diff pipeline: builds the scoped DI container, runs the folder diff,
    /// and generates all reports (Markdown, HTML, audit log, SBOM).
    /// 差分パイプラインを実行する: スコープ付き DI コンテナを構築し、フォルダ差分を実行し、
    /// すべてのレポート（Markdown、HTML、監査ログ、SBOM）を生成する。
    /// </summary>
    internal sealed class DiffPipelineExecutor
    {
        private readonly ILoggerService _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="DiffPipelineExecutor"/>.
        /// <see cref="DiffPipelineExecutor"/> の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="logger">Logger for diagnostic output. / 診断出力用ロガー。</param>
        internal DiffPipelineExecutor(ILoggerService logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <summary>
        /// Runs the complete diff pipeline: context creation, DI setup, diff execution, and report generation.
        /// 完全な差分パイプラインを実行する: コンテキスト作成、DI セットアップ、差分実行、レポート生成。
        /// </summary>
        /// <param name="oldFolderAbsolutePath">Absolute path to the baseline (old) folder. / 基準（旧）フォルダの絶対パス。</param>
        /// <param name="newFolderAbsolutePath">Absolute path to the comparison (new) folder. / 比較（新）フォルダの絶対パス。</param>
        /// <param name="reportsFolderAbsolutePath">Absolute path to the report output folder. / レポート出力フォルダの絶対パス。</param>
        /// <param name="config">Immutable configuration for this run. / この実行の不変設定。</param>
        /// <param name="appVersion">Application version string. / アプリケーションバージョン文字列。</param>
        /// <param name="computerName">Name of the computer executing the run. / 実行マシン名。</param>
        /// <param name="plugins">Optional loaded plugins to register in the DI scope. / DI スコープに登録する読み込み済みプラグイン（任意）。</param>
        /// <returns>
        /// A <see cref="DiffPipelineResult"/> containing warning flags from the completed run.
        /// 完了した実行の警告フラグを含む <see cref="DiffPipelineResult"/>。
        /// </returns>
        internal async Task<DiffPipelineResult> ExecuteAsync(
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            ConfigSettings config,
            string appVersion,
            string computerName,
            IReadOnlyList<IPlugin>? plugins = null)
        {
            var executionContext = RunScopeBuilder.BuildExecutionContext(
                oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath, config);

            using var runProvider = RunScopeBuilder.Build(config, executionContext, _logger, plugins);
            using var scope = runProvider.CreateScope();
            return await ExecuteScopedRunAsync(scope.ServiceProvider, executionContext, appVersion, computerName, config);
        }

        /// <summary>
        /// Total number of progress phases across the entire pipeline
        /// (discovery, scanning, IL precompute, diff classification, report generation).
        /// パイプライン全体のフェーズ総数
        /// （ファイル列挙、アセンブリスキャン、IL プリコンピュート、差分分類、レポート生成）。
        /// </summary>
        private const int TOTAL_PHASES = 5;

        private const string PHASE_LABEL_GENERATING_REPORTS = "Generating reports";

        private async Task<DiffPipelineResult> ExecuteScopedRunAsync(
            IServiceProvider scopedProvider,
            DiffExecutionContext executionContext,
            string appVersion,
            string computerName,
            ConfigSettings config)
        {
            var resultLists = scopedProvider.GetRequiredService<FileDiffResultLists>();
            resultLists.DisassemblerAvailability = DisassemblerHelper.ProbeAllCandidates();

            // When no disassembler is available and IL comparison is enabled, write install suggestions to stderr.
            // 逆アセンブラ未検出かつ IL 比較が有効な場合、インストール提案を stderr に出力。
            if (!config.SkipIL && resultLists.DisassemblerAvailability.All(p => !p.Available))
            {
                var suggestion = DisassemblerHelper.BuildInstallSuggestion();
                Console.Error.Write(suggestion);
                _logger.LogMessage(AppLogLevel.Warning, "No disassembler tool detected. IL comparison will not be available.", shouldOutputMessageToConsole: true);
            }

            var progressReporter = scopedProvider.GetRequiredService<ProgressReportService>();
            progressReporter.TotalPhases = TOTAL_PHASES;

            try
            {
                var elapsedTimeString = await ExecuteDiffAsync(scopedProvider, progressReporter);

                // Best-effort NuGet vulnerability enrichment (after all diffs complete)
                // ベストエフォートの NuGet 脆弱性チェック（全差分完了後に実行）
                if (config.EnableNuGetVulnerabilityCheck && config.ShouldIncludeDependencyChangesInReport)
                    await EnrichDependencyVulnerabilitiesAsync(resultLists, _logger);

                GenerateReports(scopedProvider, executionContext, appVersion, elapsedTimeString, computerName, config, progressReporter);
                var stats = resultLists.SummaryStatistics;
                var pipelineResult = new DiffPipelineResult(
                    resultLists.HasAnySha256Mismatch,
                    resultLists.HasAnyNewFileTimestampOlderThanOldWarning,
                    resultLists.HasAnyILFilterWarning,
                    stats.UnchangedCount,
                    stats.AddedCount,
                    stats.RemovedCount,
                    stats.ModifiedCount);

                // Run plugin post-process actions (best-effort, after all reports complete)
                // プラグインのポストプロセスアクションを実行（ベストエフォート、全レポート完了後）
                await RunPostProcessActionsAsync(scopedProvider, executionContext, appVersion, pipelineResult);

                return pipelineResult;
            }
            finally
            {
                progressReporter.Dispose();
            }
        }

        private async Task<string> ExecuteDiffAsync(IServiceProvider scopedProvider, ProgressReportService progressReporter)
        {
            var stopwatch = Stopwatch.StartNew();
            await scopedProvider.GetRequiredService<IFolderDiffService>().ExecuteFolderDiffAsync();
            stopwatch.Stop();
            var elapsedTimeString = FormatElapsedTime(stopwatch.Elapsed);
            _logger.LogMessage(AppLogLevel.Info, $"Elapsed Time: {elapsedTimeString}", shouldOutputMessageToConsole: true);
            return elapsedTimeString;
        }

        private void GenerateReports(
            IServiceProvider scopedProvider,
            DiffExecutionContext executionContext,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            ConfigSettings config,
            ProgressReportService progressReporter)
        {
            // Begin report generation as the final phase with sub-step progress.
            // レポート生成を最終フェーズとしてサブステップ進捗付きで開始する。
            progressReporter.BeginPhase(PHASE_LABEL_GENERATING_REPORTS);

            var ilCache = scopedProvider.GetService<ILCache>();
            var reviewChecklistItems = ReviewChecklistLoader.Load(_logger);
            var reportContext = new ReportGenerationContext(
                executionContext.OldFolderAbsolutePath,
                executionContext.NewFolderAbsolutePath,
                executionContext.ReportsFolderAbsolutePath,
                appVersion,
                elapsedTimeString,
                computerName,
                config,
                ilCache,
                reviewChecklistItems);

            // Execute all registered report formatters in order.
            // 登録済みの全レポートフォーマッターを順序通りに実行する。
            var formatters = scopedProvider.GetServices<IReportFormatter>()
                .OrderBy(f => f.Order)
                .ToList();
            int formatterCount = formatters.Count;
            for (int i = 0; i < formatterCount; i++)
            {
                var formatter = formatters[i];
                if (formatter.IsEnabled(reportContext))
                {
                    formatter.Generate(reportContext);
                }
                progressReporter.ReportProgress((i + 1) * 100.0 / formatterCount);
            }
        }

        /// <summary>
        /// Runs all registered <see cref="IPostProcessAction"/> implementations after report generation.
        /// Best-effort: failures are logged but do not affect the pipeline result.
        /// 全レポート生成後に登録済みの <see cref="IPostProcessAction"/> を実行します。
        /// ベストエフォート: 失敗はログ出力のみでパイプライン結果には影響しません。
        /// </summary>
        private async Task RunPostProcessActionsAsync(
            IServiceProvider scopedProvider,
            DiffExecutionContext executionContext,
            string appVersion,
            DiffPipelineResult pipelineResult)
        {
            var actions = scopedProvider.GetServices<IPostProcessAction>()
                .OrderBy(a => a.Order)
                .ToList();

            if (actions.Count == 0) return;

            var context = new PostProcessContext
            {
                ReportsFolderAbsolutePath = executionContext.ReportsFolderAbsolutePath,
                OldFolderAbsolutePath = executionContext.OldFolderAbsolutePath,
                NewFolderAbsolutePath = executionContext.NewFolderAbsolutePath,
                AppVersion = appVersion,
                AddedCount = pipelineResult.AddedCount,
                RemovedCount = pipelineResult.RemovedCount,
                ModifiedCount = pipelineResult.ModifiedCount,
                UnchangedCount = pipelineResult.UnchangedCount,
                HasSha256MismatchWarnings = pipelineResult.HasSha256MismatchWarnings
            };

            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                IPostProcessAction action = actions[actionIndex];
                try
                {
                    await action.ExecuteAsync(context, CancellationToken.None);
                }
#pragma warning disable CA1031 // Post-process actions are best-effort / ポストプロセスアクションはベストエフォート
                catch (Exception ex)
                {
                    _logger.LogMessage(AppLogLevel.Warning,
                        $"Post-process action '{action.GetType().Name}' failed at position {actionIndex + 1}/{actions.Count} (Order={action.Order}, {ex.GetType().Name}): {ex.Message}",
                        shouldOutputMessageToConsole: true, ex);
                }
#pragma warning restore CA1031
            }
        }

        /// <summary>
        /// Enriches all dependency change summaries with NuGet vulnerability data.
        /// Best-effort: failures are logged but do not block report generation.
        /// すべての依存関係変更サマリを NuGet 脆弱性データで拡充します。
        /// ベストエフォート: 失敗はログ出力のみでレポート生成をブロックしません。
        /// </summary>
        private static async Task EnrichDependencyVulnerabilitiesAsync(FileDiffResultLists resultLists, ILoggerService logger)
        {
            try
            {
                // Collect all unique entries across all .deps.json files
                // すべての .deps.json ファイルのエントリを収集
                var allEntries = resultLists.FileRelativePathToDependencyChanges.Values
                    .SelectMany(s => s.Entries)
                    .ToList();

                if (allEntries.Count == 0)
                    return;

                using var vulnService = new NuGetVulnerabilityService(logger);
                var vulnResults = await vulnService.CheckVulnerabilitiesAsync(allEntries);

                if (vulnResults.Count == 0)
                    return;

                // Enrich each DependencyChangeSummary with vulnerability data
                // 各 DependencyChangeSummary を脆弱性データで拡充
                foreach (var kvp in resultLists.FileRelativePathToDependencyChanges)
                {
                    var summary = kvp.Value;
                    bool anyEnriched = false;
                    var enrichedEntries = new List<DependencyChangeEntry>(summary.Entries.Count);

                    foreach (var entry in summary.Entries)
                    {
                        if (vulnResults.TryGetValue(entry.PackageName, out var vulnResult))
                        {
                            enrichedEntries.Add(entry with { Vulnerabilities = vulnResult });
                            anyEnriched = true;
                        }
                        else
                        {
                            enrichedEntries.Add(entry);
                        }
                    }

                    if (anyEnriched)
                    {
                        resultLists.FileRelativePathToDependencyChanges[kvp.Key] =
                            new DependencyChangeSummary { Entries = enrichedEntries };
                    }
                }
            }
#pragma warning disable CA1031 // Best-effort enrichment / ベストエフォートの拡充
            catch (Exception ex)
            {
                logger.LogMessage(AppLogLevel.Warning,
                    $"NuGet vulnerability enrichment failed ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true, ex);
            }
#pragma warning restore CA1031
        }

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
    }

    /// <summary>
    /// Result of a completed diff pipeline execution, carrying warning flags.
    /// 完了した差分パイプライン実行の結果。警告フラグを保持する。
    /// </summary>
    internal sealed record DiffPipelineResult(
        bool HasSha256MismatchWarnings,
        bool HasTimestampRegressionWarnings,
        bool HasILFilterWarnings,
        int UnchangedCount,
        int AddedCount,
        int RemovedCount,
        int ModifiedCount);
}
