using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Architecture
{
    /// <summary>
    /// Verifies that the mutation-summary helper script produces stable markdown/json outputs.
    /// ミューテーションサマリー用 helper script が安定した markdown/json 出力を生成することを検証します。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class MutationSummaryScriptTests : IDisposable
    {
        private readonly string _rootDir;

        public MutationSummaryScriptTests()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), "fd-mutation-summary-script-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_rootDir))
                {
                    Directory.Delete(_rootDir, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup errors / クリーンアップエラーを無視
            }
        }

        /// <summary>
        /// Verifies that the script writes an unavailable summary when no Stryker report exists.
        /// Stryker レポートがない場合に unavailable サマリーを書き出すことを検証します。
        /// </summary>
        [SkippableFact]
        public async Task GenerateMutationSummary_WithMissingReport_WritesUnavailableSummary()
        {
            Skip.IfNot(TryGetPythonLauncher(out _, out _), "Python launcher is required to run mutation summary script tests.");

            var outputRoot = Path.Combine(_rootDir, "missing-report");
            Directory.CreateDirectory(outputRoot);

            await RunScriptAsync(outputRoot, "https://example.invalid/runs/missing", "StrykerSummary-10-1", "StrykerReport-10-1");

            var summaryJson = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputRoot, "mutation-summary.json")));
            Assert.True(summaryJson.RootElement.GetProperty("mutationScore").ValueKind == JsonValueKind.Null);
            Assert.Equal("unavailable", summaryJson.RootElement.GetProperty("scoreBand").GetString());
            Assert.Equal("https://example.invalid/runs/missing", summaryJson.RootElement.GetProperty("runUrl").GetString());
            Assert.Equal("StrykerSummary-10-1", summaryJson.RootElement.GetProperty("artifactNames").GetProperty("summary").GetString());

            var summaryMarkdown = await File.ReadAllTextAsync(Path.Combine(outputRoot, "mutation-summary.md"));
            Assert.Contains("Mutation score: unavailable", summaryMarkdown, StringComparison.Ordinal);
            Assert.Contains("StrykerSummary-*", summaryMarkdown, StringComparison.Ordinal);
            Assert.Contains("https://example.invalid/runs/missing", summaryMarkdown, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that the script extracts score and survivor counts from a Stryker JSON report.
        /// Stryker JSON レポートからスコアと survivor 件数を抽出できることを検証します。
        /// </summary>
        [SkippableFact]
        public async Task GenerateMutationSummary_WithValidReport_WritesScoreAndSurvivorCounts()
        {
            Skip.IfNot(TryGetPythonLauncher(out _, out _), "Python launcher is required to run mutation summary script tests.");
            var thresholds = LoadStrykerThresholds();

            var outputRoot = Path.Combine(_rootDir, "valid-report");
            var reportsDir = Path.Combine(outputRoot, "session", "reports");
            Directory.CreateDirectory(reportsDir);
            await File.WriteAllTextAsync(
                Path.Combine(reportsDir, "mutation-report.json"),
                """
                {
                  "mutationScore": 82.5,
                  "files": {
                    "Sample.cs": {
                      "mutants": [
                        { "id": 1, "status": "Killed" },
                        { "id": 2, "status": "Survived" },
                        { "id": 3, "status": "Timeout" },
                        { "id": 4, "status": "Survived" }
                      ]
                    }
                  }
                }
                """);

            await RunScriptAsync(outputRoot, "https://example.invalid/runs/valid", "StrykerSummary-11-1", "StrykerReport-11-1");

            var summaryJson = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputRoot, "mutation-summary.json")));
            Assert.Equal(82.5, summaryJson.RootElement.GetProperty("mutationScore").GetDouble());
            Assert.Equal("high", summaryJson.RootElement.GetProperty("scoreBand").GetString());
            Assert.Equal(2, summaryJson.RootElement.GetProperty("survivorCount").GetInt32());
            Assert.Equal(thresholds.High, summaryJson.RootElement.GetProperty("thresholds").GetProperty("high").GetDouble());
            Assert.Equal(thresholds.Low, summaryJson.RootElement.GetProperty("thresholds").GetProperty("low").GetDouble());
            Assert.Equal(thresholds.Break, summaryJson.RootElement.GetProperty("thresholds").GetProperty("break").GetDouble());
            Assert.Equal(2, summaryJson.RootElement.GetProperty("statusCounts").GetProperty("Survived").GetInt32());
            Assert.Equal(1, summaryJson.RootElement.GetProperty("statusCounts").GetProperty("Killed").GetInt32());
            Assert.Equal(1, summaryJson.RootElement.GetProperty("statusCounts").GetProperty("Timeout").GetInt32());

            var summaryMarkdown = await File.ReadAllTextAsync(Path.Combine(outputRoot, "mutation-summary.md"));
            Assert.Contains(
                FormattableString.Invariant($"Mutation score: **82.50%** (high band; thresholds high/low/break = {FormatThreshold(thresholds.High)}/{FormatThreshold(thresholds.Low)}/{FormatThreshold(thresholds.Break)})"),
                summaryMarkdown,
                StringComparison.Ordinal);
            Assert.Contains("Survived mutants: **2**", summaryMarkdown, StringComparison.Ordinal);
            Assert.Contains("| `Survived` | 2 |", summaryMarkdown, StringComparison.Ordinal);
            Assert.Contains("StrykerReport-*", summaryMarkdown, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that a valid zero-mutant report stays valid instead of degrading to unavailable.
        /// 0 mutant の有効なレポートが unavailable に劣化しないことを検証します。
        /// </summary>
        [SkippableFact]
        public async Task GenerateMutationSummary_WithZeroMutants_KeepsValidSummary()
        {
            Skip.IfNot(TryGetPythonLauncher(out _, out _), "Python launcher is required to run mutation summary script tests.");
            var thresholds = LoadStrykerThresholds();

            var outputRoot = Path.Combine(_rootDir, "zero-mutants");
            var reportsDir = Path.Combine(outputRoot, "session", "reports");
            Directory.CreateDirectory(reportsDir);
            await File.WriteAllTextAsync(
                Path.Combine(reportsDir, "mutation-report.json"),
                """
                {
                  "mutationScore": 100.0,
                  "files": {
                    "Zero.cs": {
                      "mutants": []
                    }
                  }
                }
                """);

            await RunScriptAsync(outputRoot, "https://example.invalid/runs/zero", "StrykerSummary-11-2", "StrykerReport-11-2");

            var summaryJson = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputRoot, "mutation-summary.json")));
            Assert.Equal(100.0, summaryJson.RootElement.GetProperty("mutationScore").GetDouble());
            Assert.Equal("high", summaryJson.RootElement.GetProperty("scoreBand").GetString());
            Assert.Equal(0, summaryJson.RootElement.GetProperty("survivorCount").GetInt32());
            Assert.Equal(thresholds.High, summaryJson.RootElement.GetProperty("thresholds").GetProperty("high").GetDouble());
            Assert.Equal(thresholds.Low, summaryJson.RootElement.GetProperty("thresholds").GetProperty("low").GetDouble());
            Assert.Equal(thresholds.Break, summaryJson.RootElement.GetProperty("thresholds").GetProperty("break").GetDouble());
            Assert.Empty(summaryJson.RootElement.GetProperty("statusCounts").EnumerateObject());

            var summaryMarkdown = await File.ReadAllTextAsync(Path.Combine(outputRoot, "mutation-summary.md"));
            Assert.Contains("Mutation score: **100.00%**", summaryMarkdown, StringComparison.Ordinal);
            Assert.DoesNotContain("Mutation score: unavailable", summaryMarkdown, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that a malformed report degrades to the unavailable summary instead of failing the script.
        /// 壊れたレポートでも script 全体を失敗させず unavailable サマリーへ劣化することを検証します。
        /// </summary>
        [SkippableFact]
        public async Task GenerateMutationSummary_WithMalformedReport_WritesUnavailableSummaryWithLoadError()
        {
            Skip.IfNot(TryGetPythonLauncher(out _, out _), "Python launcher is required to run mutation summary script tests.");

            var outputRoot = Path.Combine(_rootDir, "malformed-report");
            var reportsDir = Path.Combine(outputRoot, "session", "reports");
            Directory.CreateDirectory(reportsDir);
            await File.WriteAllTextAsync(Path.Combine(reportsDir, "mutation-report.json"), "{ \"mutationScore\": 80,");

            await RunScriptAsync(outputRoot, "https://example.invalid/runs/malformed", "StrykerSummary-12-1", "StrykerReport-12-1");

            var summaryJson = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputRoot, "mutation-summary.json")));
            Assert.True(summaryJson.RootElement.GetProperty("mutationScore").ValueKind == JsonValueKind.Null);
            Assert.Equal("unavailable", summaryJson.RootElement.GetProperty("scoreBand").GetString());
            Assert.True(summaryJson.RootElement.TryGetProperty("loadError", out var loadError));
            Assert.Contains("JSONDecodeError", loadError.GetString(), StringComparison.Ordinal);

            var summaryMarkdown = await File.ReadAllTextAsync(Path.Combine(outputRoot, "mutation-summary.md"));
            Assert.Contains("Mutation score: unavailable", summaryMarkdown, StringComparison.Ordinal);
            Assert.Contains("Report load error:", summaryMarkdown, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that the newest report wins when multiple report directories exist.
        /// 複数のレポートディレクトリがある場合に最新レポートが優先されることを検証します。
        /// </summary>
        [SkippableFact]
        public async Task GenerateMutationSummary_WithMultipleReports_UsesNewestReport()
        {
            Skip.IfNot(TryGetPythonLauncher(out _, out _), "Python launcher is required to run mutation summary script tests.");

            var outputRoot = Path.Combine(_rootDir, "multiple-reports");
            var olderReportsDir = Path.Combine(outputRoot, "older", "reports");
            var newerReportsDir = Path.Combine(outputRoot, "newer", "reports");
            Directory.CreateDirectory(olderReportsDir);
            Directory.CreateDirectory(newerReportsDir);

            var olderReportPath = Path.Combine(olderReportsDir, "mutation-report.json");
            var newerReportPath = Path.Combine(newerReportsDir, "mutation-report.json");

            await File.WriteAllTextAsync(
                olderReportPath,
                """
                {
                  "mutationScore": 10.0,
                  "files": {
                    "Old.cs": {
                      "mutants": [
                        { "id": 1, "status": "Survived" }
                      ]
                    }
                  }
                }
                """);
            await File.WriteAllTextAsync(
                newerReportPath,
                """
                {
                  "mutationScore": 90.0,
                  "files": {
                    "New.cs": {
                      "mutants": [
                        { "id": 1, "status": "Killed" }
                      ]
                    }
                  }
                }
                """);

            var olderTime = DateTimeOffset.UtcNow.AddMinutes(-5);
            var newerTime = DateTimeOffset.UtcNow;
            File.SetLastWriteTimeUtc(olderReportPath, olderTime.UtcDateTime);
            File.SetLastWriteTimeUtc(newerReportPath, newerTime.UtcDateTime);

            await RunScriptAsync(outputRoot, "https://example.invalid/runs/multi", "StrykerSummary-13-1", "StrykerReport-13-1");

            var summaryJson = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputRoot, "mutation-summary.json")));
            Assert.Equal(90.0, summaryJson.RootElement.GetProperty("mutationScore").GetDouble());
            Assert.Equal(newerReportPath, summaryJson.RootElement.GetProperty("reportPath").GetString());

            var summaryMarkdown = await File.ReadAllTextAsync(Path.Combine(outputRoot, "mutation-summary.md"));
            Assert.Contains("Mutation score: **90.00%**", summaryMarkdown, StringComparison.Ordinal);
            Assert.Contains($"`{newerReportPath}`", summaryMarkdown, StringComparison.Ordinal);
        }

        private static async Task RunScriptAsync(string outputRoot, string runUrl, string summaryArtifactName, string reportArtifactName)
        {
            if (!TryGetPythonLauncher(out var pythonFileName, out var pythonPrefixArgs))
            {
                throw new InvalidOperationException("Python launcher was not found.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonFileName,
                WorkingDirectory = RepositoryRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in pythonPrefixArgs)
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.ArgumentList.Add(GetRepositoryFilePath("scripts", "generate-mutation-summary.py"));
            startInfo.ArgumentList.Add("--output-root");
            startInfo.ArgumentList.Add(outputRoot);
            startInfo.ArgumentList.Add("--run-url");
            startInfo.ArgumentList.Add(runUrl);
            startInfo.ArgumentList.Add("--summary-artifact-name");
            startInfo.ArgumentList.Add(summaryArtifactName);
            startInfo.ArgumentList.Add("--report-artifact-name");
            startInfo.ArgumentList.Add(reportArtifactName);

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start mutation summary script.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Mutation summary script failed with exit code {process.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
            }
        }

        private static (double High, double Low, double Break) LoadStrykerThresholds()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(GetRepositoryFilePath("stryker-config.json")));
            var thresholds = document.RootElement.GetProperty("stryker-config").GetProperty("thresholds");
            return (
                thresholds.GetProperty("high").GetDouble(),
                thresholds.GetProperty("low").GetDouble(),
                thresholds.GetProperty("break").GetDouble());
        }

        private static string FormatThreshold(double value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);

        private static bool TryGetPythonLauncher(out string fileName, out string[] prefixArguments)
        {
            var candidates = new (string FileName, string[] PrefixArguments)[]
            {
                ("python3", Array.Empty<string>()),
                ("python", Array.Empty<string>()),
                ("py", new[] { "-3" })
            };

            foreach (var candidate in candidates)
            {
                if (CanRunCommand(candidate.FileName, candidate.PrefixArguments, "--version"))
                {
                    fileName = candidate.FileName;
                    prefixArguments = candidate.PrefixArguments;
                    return true;
                }
            }

            fileName = string.Empty;
            prefixArguments = Array.Empty<string>();
            return false;
        }

        private static bool CanRunCommand(string fileName, string[] prefixArguments, params string[] arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                foreach (var argument in prefixArguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                foreach (var argument in arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return false;
                }

                if (!process.WaitForExit(10000))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // ignore kill failures when probing availability / 利用可能性チェック時の kill 失敗を無視
                    }

                    return false;
                }

                return process.HasExited && process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static string GetRepositoryFilePath(params string[] segments)
        {
            var path = RepositoryRootPath;
            foreach (var segment in segments)
            {
                path = Path.Combine(path, segment);
            }

            return path;
        }

        private static string RepositoryRootPath =>
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
