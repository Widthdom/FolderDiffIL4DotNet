using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using FolderDiffIL4DotNet.Services.ILOutput;
using Microsoft.Extensions.DependencyInjection;

namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// Executes the diff pipeline: builds the scoped DI container, runs the folder diff,
    /// and generates all reports (Markdown, HTML, audit log).
    /// 差分パイプラインを実行する: スコープ付き DI コンテナを構築し、フォルダ差分を実行し、
    /// すべてのレポート（Markdown、HTML、監査ログ）を生成する。
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
            string computerName)
        {
            var executionContext = RunScopeBuilder.BuildExecutionContext(
                oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath, config);

            using var runProvider = RunScopeBuilder.Build(config, executionContext, _logger);
            using var scope = runProvider.CreateScope();
            return await ExecuteScopedRunAsync(scope.ServiceProvider, executionContext, appVersion, computerName, config);
        }

        private async Task<DiffPipelineResult> ExecuteScopedRunAsync(
            IServiceProvider scopedProvider,
            DiffExecutionContext executionContext,
            string appVersion,
            string computerName,
            ConfigSettings config)
        {
            var resultLists = scopedProvider.GetRequiredService<FileDiffResultLists>();
            resultLists.DisassemblerAvailability = DisassemblerHelper.ProbeAllCandidates();
            var elapsedTimeString = await ExecuteDiffAsync(scopedProvider);

            // Best-effort NuGet vulnerability enrichment (after all diffs complete)
            // ベストエフォートの NuGet 脆弱性チェック（全差分完了後に実行）
            if (config.EnableNuGetVulnerabilityCheck && config.ShouldIncludeDependencyChangesInReport)
                await EnrichDependencyVulnerabilitiesAsync(resultLists, _logger);

            GenerateReports(scopedProvider, executionContext, appVersion, elapsedTimeString, computerName, config);
            return new DiffPipelineResult(resultLists.HasAnySha256Mismatch, resultLists.HasAnyNewFileTimestampOlderThanOldWarning);
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

        private static void GenerateReports(
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
                    $"NuGet vulnerability enrichment failed: {ex.Message}",
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
    internal sealed record DiffPipelineResult(bool HasSha256MismatchWarnings, bool HasTimestampRegressionWarnings);
}
