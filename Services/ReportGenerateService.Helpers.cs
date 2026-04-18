using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Common;
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Helper methods used by <see cref="ReportGenerateService"/> and its section writers.
    /// <see cref="ReportGenerateService"/> と各セクションライターで共有する補助メソッド群です。
    /// </summary>
    public sealed partial class ReportGenerateService
    {
        private void LogReportOutputFailure(string diffReportAbsolutePath, Exception exception)
            => _logger.LogMessage(AppLogLevel.Error,
                $"Failed to output report to '{diffReportAbsolutePath}' ({exception.GetType().Name}): {exception.Message}",
                shouldOutputMessageToConsole: true,
                exception);

        private static void PrepareOutputPathForOverwrite(string outputFileAbsolutePath)
        {
            if (!File.Exists(outputFileAbsolutePath))
            {
                return;
            }

            var attributes = File.GetAttributes(outputFileAbsolutePath);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(outputFileAbsolutePath, attributes & ~FileAttributes.ReadOnly);
            }

            File.Delete(outputFileAbsolutePath);
        }

        private void TrySetReportReadOnly(string diffReportAbsolutePath)
        {
            try
            {
                FileSystemUtility.TrySetReadOnly(diffReportAbsolutePath);
            }
            catch (Exception ex) when (ExceptionFilters.IsPathOrFileIoRecoverable(ex))
            {
                LogReportProtectionWarning(diffReportAbsolutePath, ex);
            }
        }

        private void LogReportProtectionWarning(string diffReportAbsolutePath, Exception ex)
        {
            _logger.LogMessage(AppLogLevel.Warning,
                $"Failed to mark report as read-only: '{diffReportAbsolutePath}' ({ex.GetType().Name}): {ex.Message}",
                shouldOutputMessageToConsole: true,
                ex);
        }

        private static string GetIgnoredFileLocationLabel(FileDiffResultLists.IgnoredFileLocation location)
            => location switch
            {
                FileDiffResultLists.IgnoredFileLocation.Old => REPORT_LOCATION_OLD,
                FileDiffResultLists.IgnoredFileLocation.New => REPORT_LOCATION_NEW,
                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New => REPORT_LOCATION_BOTH,
                _ => string.Empty
            };

        private static string? BuildIgnoredFileTimestampInfo(
            KeyValuePair<string, FileDiffResultLists.IgnoredFileLocation> entry,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath)
        {
            bool hasOld = (entry.Value & FileDiffResultLists.IgnoredFileLocation.Old) != 0;
            bool hasNew = (entry.Value & FileDiffResultLists.IgnoredFileLocation.New) != 0;
            if (!hasOld && !hasNew)
            {
                return null;
            }

            if (hasOld && hasNew)
            {
                string oldTs = TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, entry.Key));
                string newTs = TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, entry.Key));
                return $"{oldTs}{REPORT_TIMESTAMP_ARROW}{newTs}";
            }

            return hasOld
                ? TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, entry.Key))
                : TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, entry.Key));
        }

        private static string BuildDisassemblerHeaderText(FileDiffResultLists fileDiffResultLists)
        {
            var observedLabels = fileDiffResultLists.DisassemblerToolVersions.Keys
                .Concat(fileDiffResultLists.DisassemblerToolVersionsFromCache.Keys)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(GetDisassemblerDisplayOrder)
                .ThenByDescending(label => label.IndexOf("(version:", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenBy(label => label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return observedLabels.Count == 0
                ? REPORT_DISASSEMBLER_NOT_USED
                : string.Join(REPORT_LIST_SEPARATOR, observedLabels);
        }

        private static int GetDisassemblerDisplayOrder(string label)
        {
            string toolName = ExtractToolName(label);
            if (string.Equals(toolName, Constants.DOTNET_ILDASM, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(toolName, Constants.ILDASM_LABEL, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(toolName, Constants.ILSPY_CMD, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return 3;
        }

        private static string ExtractToolName(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return string.Empty;
            }

            int versionIndex = label.IndexOf(" (version:", StringComparison.OrdinalIgnoreCase);
            return versionIndex >= 0 ? label.Substring(0, versionIndex).Trim() : label.Trim();
        }

        private static string BuildDiffDetailDisplay(
            string fileRelativePath,
            FileDiffResultLists.DiffDetailResult diffDetail,
            FileDiffResultLists fileDiffResultLists)
        {
            ChangeImportance? importance = fileDiffResultLists.GetMaxImportance(fileRelativePath);
            return importance == null
                ? $"`{diffDetail}`"
                : $"`{diffDetail}` `{importance.Value}`";
        }

        /// <summary>
        /// Builds a Markdown vulnerability column value for a dependency change entry.
        /// 依存関係変更エントリの Markdown 脆弱性カラム値を構築します。
        /// </summary>
        private static string BuildMarkdownVulnColumn(VulnerabilityCheckResult? vuln)
        {
            if (vuln == null || !vuln.HasAnyVulnerabilities)
            {
                return "—";
            }

            var parts = new List<string>();
            foreach (var v in vuln.NewVersionVulnerabilities)
            {
                string sev = VulnerabilityCheckResult.SeverityToLabel(v.Severity);
                parts.Add($"⚠ {sev}");
            }

            if (vuln.HasResolvedVulnerabilities)
            {
                foreach (var v in vuln.OldVersionVulnerabilities)
                {
                    bool alsoNew = vuln.NewVersionVulnerabilities.Any(
                        nv => string.Equals(nv.AdvisoryUrl, v.AdvisoryUrl, StringComparison.Ordinal));
                    if (alsoNew)
                    {
                        continue;
                    }

                    string sev = VulnerabilityCheckResult.SeverityToLabel(v.Severity);
                    parts.Add($"~~{sev}~~");
                }
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "—";
        }

        /// <summary>
        /// Builds a Markdown referencing assemblies column value for a dependency change entry.
        /// 依存関係変更エントリの Markdown 参照アセンブリカラム値を構築します。
        /// </summary>
        private static string BuildMarkdownRefsColumn(IReadOnlyList<string>? refs)
            => refs is { Count: > 0 } ? string.Join(", ", refs) : "—";

        private static string BuildDisassemblerDisplay(
            string fileRelativePath,
            FileDiffResultLists.DiffDetailResult diffDetail,
            FileDiffResultLists fileDiffResultLists)
        {
            if ((diffDetail == FileDiffResultLists.DiffDetailResult.ILMatch || diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch) &&
                fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(fileRelativePath, out string? label) &&
                !string.IsNullOrWhiteSpace(label))
            {
                return $"`{label}`";
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the .NET SDK / target framework display string for a file, or empty if not available.
        /// ファイルの .NET SDK / ターゲットフレームワーク表示文字列を返します。利用不可の場合は空文字。
        /// </summary>
        private static string BuildSdkVersionDisplay(string fileRelativePath, FileDiffResultLists fileDiffResultLists)
        {
            if (!fileDiffResultLists.FileRelativePathToSdkVersionDictionary.TryGetValue(fileRelativePath, out string? sdkVersion) ||
                string.IsNullOrWhiteSpace(sdkVersion))
            {
                return string.Empty;
            }

            int arrowIdx = sdkVersion.IndexOf(" → ", StringComparison.Ordinal);
            if (arrowIdx >= 0)
            {
                string oldPart = sdkVersion.Substring(0, arrowIdx);
                string newPart = sdkVersion.Substring(arrowIdx + " → ".Length);
                return $"`{oldPart}` → `{newPart}`";
            }

            return $"`{sdkVersion}`";
        }

        /// <summary>
        /// Returns the display order for Unchanged files: SHA256Match → ILMatch → TextMatch.
        /// Unchanged ファイルの表示順序を返します: SHA256Match → ILMatch → TextMatch。
        /// </summary>
        private static int GetUnchangedSortOrder(FileDiffResultLists.DiffDetailResult detail)
            => detail switch
            {
                FileDiffResultLists.DiffDetailResult.SHA256Match => 0,
                FileDiffResultLists.DiffDetailResult.ILMatch => 1,
                FileDiffResultLists.DiffDetailResult.TextMatch => 2,
                _ => 3
            };

        /// <summary>
        /// Returns the display order for Modified files: TextMismatch → ILMismatch → SHA256Mismatch.
        /// Modified ファイルの表示順序を返します: TextMismatch → ILMismatch → SHA256Mismatch。
        /// </summary>
        private static int GetModifiedSortOrder(FileDiffResultLists.DiffDetailResult detail)
            => detail switch
            {
                FileDiffResultLists.DiffDetailResult.TextMismatch => 0,
                FileDiffResultLists.DiffDetailResult.ILMismatch => 1,
                FileDiffResultLists.DiffDetailResult.SHA256Mismatch => 2,
                _ => 3
            };

        /// <summary>
        /// Returns a sort ordinal for <see cref="ChangeImportance"/> (High=0 first).
        /// <see cref="ChangeImportance"/> のソート序数を返します（High=0 が先頭）。
        /// </summary>
        private static int GetImportanceSortOrder(ChangeImportance? importance)
            => importance switch
            {
                ChangeImportance.High => 0,
                ChangeImportance.Medium => 1,
                ChangeImportance.Low => 2,
                _ => 3
            };

        private static string BuildChangeTagDisplay(string fileRelativePath, FileDiffResultLists fileDiffResultLists)
        {
            if (fileDiffResultLists.FileRelativePathToChangeTags.TryGetValue(fileRelativePath, out var tags) && tags.Count > 0)
            {
                // Wrap each tag label individually in backticks / 各タグラベルを個別にバッククォートで囲む
                return string.Join(", ", tags.Select(t => $"`{ChangeTagClassifier.GetLabel(t)}`"));
            }

            return string.Empty;
        }

        private static List<string> GetNormalizedIlIgnoreContainingStrings(IReadOnlyConfigSettings config)
        {
            if (config?.ILIgnoreLineContainingStrings == null)
            {
                return new List<string>();
            }

            return config.ILIgnoreLineContainingStrings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static string FormatChecklistMarkdownCell(string item)
            => string.Join(
                "<br>",
                item.Split('\n')
                    .Select(static line => WebUtility.HtmlEncode(line).Replace("|", "\\|", StringComparison.Ordinal)));

        /// <summary>
        /// Writes the Disassembler Availability table to the Markdown report header.
        /// Markdown レポートヘッダに逆アセンブラ利用可否テーブルを書き込みます。
        /// </summary>
        private static void WriteDisassemblerAvailabilityTable(
            StreamWriter writer,
            IReadOnlyList<DisassemblerProbeResult>? probeResults,
            string inUseHeaderText)
        {
            if (probeResults == null || probeResults.Count == 0)
            {
                return;
            }

            writer.WriteLine("### Disassembler Availability");
            writer.WriteLine();
            writer.WriteLine("| Tool | Available | Version | In Use |");
            writer.WriteLine("|------|:---------:|---------|:------:|");
            foreach (var probe in probeResults)
            {
                bool isInUse = !string.IsNullOrWhiteSpace(inUseHeaderText) &&
                    inUseHeaderText.IndexOf(probe.ToolName, StringComparison.OrdinalIgnoreCase) >= 0;
                string available = probe.Available ? "Yes" : "No";
                string version = probe.Available && !string.IsNullOrWhiteSpace(probe.Version)
                    ? probe.Version
                    : REPORT_DISASSEMBLER_NOT_USED;
                string inUseCol = isInUse ? "Yes" : "No";
                writer.WriteLine($"| {probe.ToolName} | {available} | {version} | {inUseCol} |");
            }
        }

        /// <summary>
        /// Writes warning banners for disassembler issues: no disassembler available, or mixed tool usage.
        /// 逆アセンブラの問題に関する警告バナーを出力: 逆アセンブラ未検出、または複数ツール混在使用。
        /// </summary>
        private static void WriteDisassemblerWarnings(StreamWriter writer, FileDiffResultLists fileDiffResultLists)
        {
            var probeResults = fileDiffResultLists.DisassemblerAvailability;
            if (probeResults != null && probeResults.Count > 0 && !probeResults.Any(p => p.Available))
            {
                writer.WriteLine();
                writer.WriteLine("> **⚠ Warning**: No disassembler tool is available. .NET assembly comparison will fail if any .dll/.exe files with differing SHA256 hashes are detected. Install `dotnet-ildasm` or `ilspycmd` to enable IL-level comparison.");
                writer.WriteLine();
            }

            var allLabels = fileDiffResultLists.DisassemblerToolVersions.Keys
                .Concat(fileDiffResultLists.DisassemblerToolVersionsFromCache.Keys)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var distinctToolNames = allLabels
                .Select(ExtractToolName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (distinctToolNames.Count > 1)
            {
                writer.WriteLine();
                writer.WriteLine($"> **⚠ Warning**: Multiple disassembler tools were used across file comparisons in this run ({string.Join(", ", allLabels)}). Each file pair is compared using the same tool, but IL output format may differ between tools, reducing cross-file consistency. This typically occurs when the preferred tool fails on certain assemblies and the fallback is used. If caused by stale cache entries from a previous tool, use --clear-cache to resolve.");
                writer.WriteLine();
            }
        }
    }
}
