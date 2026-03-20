using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Text;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet.Services
{
    // Report section builders (Header, Ignored, Unchanged, Added, Removed, Modified, Summary, ILCacheStats, Warnings).
    // レポートセクション生成メソッド群。
    public sealed partial class HtmlReportGenerateService
    {
        // ── Report sections ──────────────────────────────────────────────────

        private void AppendHeaderSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            ConfigSettings config)
        {
            sb.AppendLine("<h1>Folder Diff Report</h1>");
            sb.AppendLine("<ul class=\"meta\">");
            sb.AppendLine($"  <li>App Version: FolderDiffIL4DotNet {HtmlEncode(appVersion)}</li>");
            sb.AppendLine($"  <li>Computer: {HtmlEncode(computerName)}</li>");
            sb.AppendLine($"  <li>Old: {HtmlEncode(oldFolderAbsolutePath)}</li>");
            sb.AppendLine($"  <li>New: {HtmlEncode(newFolderAbsolutePath)}</li>");
            sb.AppendLine($"  <li>Ignored Extensions: {HtmlEncode(string.Join(", ", config.IgnoredExtensions))}</li>");
            sb.AppendLine($"  <li>Text File Extensions: {HtmlEncode(string.Join(", ", config.TextFileExtensions))}</li>");
            sb.AppendLine($"  <li>IL Disassembler: {HtmlEncode(BuildDisassemblerHeaderText())}</li>");
            if (!string.IsNullOrWhiteSpace(elapsedTimeString))
                sb.AppendLine($"  <li>Elapsed Time: {HtmlEncode(elapsedTimeString)}</li>");
            if (config.ShouldOutputFileTimestamps)
                sb.AppendLine($"  <li>Timestamps (timezone): {HtmlEncode(DateTimeOffset.Now.ToString("zzz"))}</li>");

            // MVID note (same style as other meta items)
            sb.AppendLine($"  <li>Note: When diffing IL, lines starting with <code>{HtmlEncode(Constants.IL_MVID_LINE_PREFIX)}</code> (if present) are ignored because they contain disassembler-emitted Module Version ID metadata that can change on rebuild without meaning the executable IL changed.</li>");

            // IL contains-ignore note
            if (config.ShouldIgnoreILLinesContainingConfiguredStrings)
            {
                var ilIgnoreStrings = GetNormalizedIlIgnoreStrings(config);
                if (ilIgnoreStrings.Count == 0)
                {
                    sb.AppendLine("  <li>Note: IL line-ignore-by-contains is enabled, but no non-empty strings are configured.</li>");
                }
                else
                {
                    var plainItems = string.Join(", ", ilIgnoreStrings.Select(s => HtmlEncode($"\"{s}\"")));
                    sb.AppendLine($"  <li>Note: When diffing IL, lines containing any of the configured strings are ignored: {plainItems}.</li>");
                }
            }

            // Myers Diff Algorithm reference
            sb.AppendLine("  <li>Note: Inline diffs for ILMismatch and TextMismatch are computed using the " +
                "<a href=\"http://www.xmailserver.org/diff2.pdf\">" +
                "Myers Diff Algorithm (E.&nbsp;W.&nbsp;Myers, &ldquo;An O(ND) Difference Algorithm and Its Variations,&rdquo; <i>Algorithmica</i> 1(2), 1986)</a>.</li>");

            // Legend (as meta bullet items)
            sb.AppendLine("  <li>Legend:");
            sb.AppendLine("    <ul>");
            sb.AppendLine($"      <li><code>MD5Match</code> / <code>MD5Mismatch</code>: MD5 hash match / mismatch</li>");
            sb.AppendLine($"      <li><code>ILMatch</code> / <code>ILMismatch</code>: IL(Intermediate Language) match / mismatch</li>");
            sb.AppendLine($"      <li><code>TextMatch</code> / <code>TextMismatch</code>: Text match / mismatch</li>");
            sb.AppendLine("    </ul>");
            sb.AppendLine("  </li>");
            sb.AppendLine("</ul>");
        }

        private void AppendIgnoredSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            ConfigSettings config)
        {
            var items = _fileDiffResultLists.IgnoredFilesRelativePathToLocation
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2>[ x ] Ignored Files ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine("<p class=\"empty\">(none)</p>"); return; }

            AppendTableStart(sb, TH_BG_DEFAULT, "Location");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var entry in items)
            {
                bool hasOld = (entry.Value & FileDiffResultLists.IgnoredFileLocation.Old) != 0;
                bool hasNew = (entry.Value & FileDiffResultLists.IgnoredFileLocation.New) != 0;

                // 3-2: absolute path for single-side, relative for both-sides
                string displayPath = (hasOld && hasNew)
                    ? entry.Key
                    : hasOld ? Path.Combine(oldFolderAbsolutePath, entry.Key)
                             : Path.Combine(newFolderAbsolutePath, entry.Key);

                string ts = BuildIgnoredTimestamp(entry.Key, hasOld, hasNew,
                    oldFolderAbsolutePath, newFolderAbsolutePath, config.ShouldOutputFileTimestamps);
                string location = (hasOld && hasNew) ? "old/new" : hasOld ? "old" : "new";
                AppendFileRow(sb, "ign", idx, displayPath, ts, location);
                idx++;
            }
            sb.AppendLine("</tbody></table></div>");
        }

        private void AppendUnchangedSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            ConfigSettings config)
        {
            var items = _fileDiffResultLists.UnchangedFilesRelativePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2>[ = ] Unchanged Files ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine("<p class=\"empty\">(none)</p>"); return; }

            AppendTableStart(sb, TH_BG_DEFAULT, "Diff Reason");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var path in items)
            {
                string ts = "";
                if (config.ShouldOutputFileTimestamps)
                {
                    string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, path));
                    string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, path));
                    ts = oldTs != newTs ? $"[{oldTs}{TIMESTAMP_ARROW}{newTs}]" : $"[{newTs}]";
                }
                _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(path, out var diffDetail);
                string col6 = BuildDiffDetailDisplay(diffDetail);
                AppendFileRow(sb, "unch", idx, path, ts, col6);
                idx++;
            }
            sb.AppendLine("</tbody></table></div>");
        }

        private void AppendAddedSection(StringBuilder sb, ConfigSettings config)
        {
            var items = _fileDiffResultLists.AddedFilesAbsolutePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_ADDED}\">[ + ] Added Files ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine("<p class=\"empty\">(none)</p>"); return; }

            AppendTableStart(sb, TH_BG_ADDED, "Diff Reason");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var absPath in items)
            {
                string ts = config.ShouldOutputFileTimestamps
                    ? $"[{Caching.TimestampCache.GetOrAdd(absPath)}]" : "";
                AppendFileRow(sb, "add", idx, absPath, ts, "");
                idx++;
            }
            sb.AppendLine("</tbody></table></div>");
        }

        private void AppendRemovedSection(StringBuilder sb, ConfigSettings config)
        {
            var items = _fileDiffResultLists.RemovedFilesAbsolutePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_REMOVED}\">[ - ] Removed Files ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine("<p class=\"empty\">(none)</p>"); return; }

            AppendTableStart(sb, TH_BG_REMOVED, "Diff Reason");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var absPath in items)
            {
                string ts = config.ShouldOutputFileTimestamps
                    ? $"[{Caching.TimestampCache.GetOrAdd(absPath)}]" : "";
                AppendFileRow(sb, "rem", idx, absPath, ts, "");
                idx++;
            }
            sb.AppendLine("</tbody></table></div>");
        }

        private void AppendModifiedSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            ConfigSettings config,
            ILCache? ilCache)
        {
            var items = _fileDiffResultLists.ModifiedFilesRelativePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ * ] Modified Files ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine("<p class=\"empty\">(none)</p>"); return; }

            AppendTableStart(sb, TH_BG_MODIFIED, "Diff Reason");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var path in items)
            {
                string ts = "";
                if (config.ShouldOutputFileTimestamps)
                {
                    string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, path));
                    string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, path));
                    ts = $"[{oldTs}{TIMESTAMP_ARROW}{newTs}]";
                }
                _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(path, out var diffDetail);
                _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(path, out var asm);
                string col6 = BuildDiffDetailDisplay(diffDetail);
                AppendFileRow(sb, "mod", idx, path, ts, col6, asm ?? "");

                // Method-level changes row (above IL diff)
                if (config.ShouldIncludeMethodLevelChangesInReport &&
                    diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch &&
                    _fileDiffResultLists.FileRelativePathToMethodLevelChanges.TryGetValue(path, out var methodChanges) &&
                    methodChanges.HasChanges)
                {
                    AppendMethodLevelChangesRow(sb, idx, methodChanges, config);
                }

                if (config.EnableInlineDiff &&
                    (diffDetail == FileDiffResultLists.DiffDetailResult.TextMismatch ||
                     diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch))
                {
                    AppendInlineDiffRow(sb, idx, path, oldFolderAbsolutePath, newFolderAbsolutePath,
                        reportsFolderAbsolutePath, config, diffDetail, asm ?? "", ilCache);
                }

                idx++;
            }
            sb.AppendLine("</tbody></table></div>");
        }

        private void AppendInlineDiffRow(
            StringBuilder sb,
            int idx,
            string relPath,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            ConfigSettings config,
            FileDiffResultLists.DiffDetailResult diffDetail,
            string disassemblerLabel,
            ILCache? ilCache,
            string sectionPrefix = "mod")
        {
            int maxDiffLines    = config.InlineDiffMaxDiffLines    > 0 ? config.InlineDiffMaxDiffLines    : 10000;
            int maxOutput       = config.InlineDiffMaxOutputLines  > 0 ? config.InlineDiffMaxOutputLines  : 10000;
            int contextLines    = config.InlineDiffContextLines   >= 0 ? config.InlineDiffContextLines   : 0;
            int maxEditDistance = config.InlineDiffMaxEditDistance  > 0 ? config.InlineDiffMaxEditDistance : 4000;
            int recordNo = idx + 1;

            string[] oldLines, newLines;

            if (diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch)
            {
                // ILMismatch: read IL text from the *_IL.txt files written during comparison
                string ilFileName = TextSanitizer.Sanitize(relPath) + "_" + Constants.LABEL_IL + ".txt";
                string oldILPath = Path.Combine(reportsFolderAbsolutePath, Constants.LABEL_IL, "old", ilFileName);
                string newILPath = Path.Combine(reportsFolderAbsolutePath, Constants.LABEL_IL, "new", ilFileName);
                if (!File.Exists(oldILPath) || !File.Exists(newILPath)) return; // IL text not written; skip
                oldLines = File.ReadAllLines(oldILPath);
                newLines = File.ReadAllLines(newILPath);
            }
            else
            {
                // TextMismatch: read from disk
                try
                {
                    oldLines = File.ReadAllLines(Path.Combine(oldFolderAbsolutePath, relPath));
                    newLines = File.ReadAllLines(Path.Combine(newFolderAbsolutePath, relPath));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    _logger.LogMessage(AppLogLevel.Warning,
                        $"Inline diff skipped for '{relPath}': {ex.Message}",
                        shouldOutputMessageToConsole: false, ex);
                    return;
                }
            }

            IReadOnlyList<TextDiffer.DiffLine> diffLines;
#pragma warning disable CA1031 // ベストエフォートなインライン差分レンダリングのため全例外を握りつぶす意図的なキャッチ / Intentional catch-all for best-effort inline-diff rendering
            try
            {
                diffLines = TextDiffer.Compute(oldLines, newLines, contextLines, maxOutput, maxEditDistance);
            }
            catch (Exception ex)
            {
                _logger.LogMessage(AppLogLevel.Warning,
                    $"Inline diff computation failed for '{relPath}': {ex.Message}",
                    shouldOutputMessageToConsole: false, ex);
                return;
            }
#pragma warning restore CA1031

            if (diffLines.Count == 0) return;

            // Single Truncated line: edit distance too large — show message directly without expand arrow
            if (diffLines.Count == 1 && diffLines[0].Kind == TextDiffer.Truncated)
            {
                sb.AppendLine("<tr class=\"diff-row\">");
                sb.AppendLine($"  <td colspan=\"8\"><p class=\"diff-skipped\">#{recordNo} {HtmlEncode(diffLines[0].Text)}</p></td>");
                sb.AppendLine("</tr>");
                return;
            }

            if (diffLines.Count > maxDiffLines)
            {
                sb.AppendLine("<tr class=\"diff-row\">");
                sb.AppendLine($"  <td colspan=\"8\"><p class=\"diff-skipped\">#{recordNo} Inline diff skipped: diff too large " +
                    $"({diffLines.Count} diff lines; limit is {maxDiffLines}). " +
                    "Increase <code>InlineDiffMaxDiffLines</code> in config to enable.</p></td>");
                sb.AppendLine("</tr>");
                return;
            }

            int addedCount = 0, removedCount = 0;
            foreach (var line in diffLines)
            {
                if (line.Kind == TextDiffer.Added) addedCount++;
                else if (line.Kind == TextDiffer.Removed) removedCount++;
            }

            string detailsId = $"diff_{sectionPrefix}_{idx}";
            string diffLabel = diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch ? "Show IL diff" : "Show diff";
            string summary = $"      <summary class=\"diff-summary\">#{recordNo} {HtmlEncode(diffLabel)} (<span class=\"diff-added-cnt\">+{addedCount}</span> / <span class=\"diff-removed-cnt\">-{removedCount}</span>)</summary>";
            string diffViewHtml = BuildDiffViewHtml(diffLines);

            sb.AppendLine("<tr class=\"diff-row\">");
            sb.AppendLine("  <td colspan=\"8\">");
            if (config.InlineDiffLazyRender)
            {
                string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(diffViewHtml));
                sb.AppendLine($"    <details id=\"{HtmlEncode(detailsId)}\" data-diff-html=\"{b64}\">");
                sb.AppendLine(summary);
                sb.AppendLine("    </details>");
            }
            else
            {
                sb.AppendLine($"    <details id=\"{HtmlEncode(detailsId)}\">");
                sb.AppendLine(summary);
                sb.Append(diffViewHtml);
                sb.AppendLine("    </details>");
            }
            sb.AppendLine("  </td>");
            sb.AppendLine("</tr>");
        }

        private void AppendMethodLevelChangesRow(
            StringBuilder sb,
            int idx,
            MethodLevelChangesSummary summary,
            ConfigSettings config,
            string sectionPrefix = "mod")
        {
            int recordNo = idx + 1;

            int totalChanges = summary.AddedTypes.Count + summary.RemovedTypes.Count +
                               summary.AddedMethods.Count + summary.RemovedMethods.Count +
                               summary.BodyChangedMethods.Count +
                               summary.AddedProperties.Count + summary.RemovedProperties.Count +
                               summary.AddedFields.Count + summary.RemovedFields.Count;

            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine("<div class=\"method-changes\">");

            AppendMemberList(contentBuilder, "Types added", summary.AddedTypes);
            AppendMemberList(contentBuilder, "Types removed", summary.RemovedTypes);
            AppendMemberList(contentBuilder, "Methods added", summary.AddedMethods);
            AppendMemberList(contentBuilder, "Methods removed", summary.RemovedMethods);
            AppendMemberList(contentBuilder, "Methods with body changes", summary.BodyChangedMethods);
            AppendMemberList(contentBuilder, "Properties added", summary.AddedProperties);
            AppendMemberList(contentBuilder, "Properties removed", summary.RemovedProperties);
            AppendMemberList(contentBuilder, "Fields added", summary.AddedFields);
            AppendMemberList(contentBuilder, "Fields removed", summary.RemovedFields);

            contentBuilder.AppendLine($"<p>Method count: {summary.OldMethodCount} (old) → {summary.NewMethodCount} (new)</p>");
            contentBuilder.AppendLine("</div>");

            string detailsId = $"methods_{sectionPrefix}_{idx}";
            string summaryLabel = $"      <summary class=\"diff-summary\">#{recordNo} Show member changes ({totalChanges} change{(totalChanges == 1 ? "" : "s")})</summary>";
            string contentHtml = contentBuilder.ToString();

            sb.AppendLine("<tr class=\"diff-row\">");
            sb.AppendLine("  <td colspan=\"8\">");
            if (config.InlineDiffLazyRender)
            {
                string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(contentHtml));
                sb.AppendLine($"    <details id=\"{HtmlEncode(detailsId)}\" data-diff-html=\"{b64}\">");
                sb.AppendLine(summaryLabel);
                sb.AppendLine("    </details>");
            }
            else
            {
                sb.AppendLine($"    <details id=\"{HtmlEncode(detailsId)}\">");
                sb.AppendLine(summaryLabel);
                sb.Append(contentHtml);
                sb.AppendLine("    </details>");
            }
            sb.AppendLine("  </td>");
            sb.AppendLine("</tr>");
        }

        private static void AppendMemberList(StringBuilder sb, string label, IReadOnlyList<string> members)
        {
            if (members.Count == 0) return;
            sb.AppendLine($"<p><strong>{HtmlEncode(label)} ({members.Count}):</strong></p>");
            sb.AppendLine("<ul>");
            foreach (var member in members)
                sb.AppendLine($"  <li><code>{HtmlEncode(member)}</code></li>");
            sb.AppendLine("</ul>");
        }

        private void AppendSummarySection(StringBuilder sb, ConfigSettings config)
        {
            sb.AppendLine("<h2 class=\"section-heading\">Summary</h2>");
            sb.AppendLine("<table class=\"stat-table\">");
            sb.AppendLine("  <tbody>");
            var stats = _fileDiffResultLists.SummaryStatistics;
            if (config.ShouldIncludeIgnoredFiles)
                sb.AppendLine($"    <tr><td class=\"stat-label\">Ignored</td><td class=\"stat-value\">{stats.IgnoredCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Unchanged</td><td class=\"stat-value\">{stats.UnchangedCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Added</td><td class=\"stat-value\">{stats.AddedCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Removed</td><td class=\"stat-value\">{stats.RemovedCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Modified</td><td class=\"stat-value\">{stats.ModifiedCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Compared</td><td class=\"stat-value\">{_fileDiffResultLists.OldFilesAbsolutePath.Count} (Old) vs {_fileDiffResultLists.NewFilesAbsolutePath.Count} (New)</td></tr>");
            sb.AppendLine("  </tbody>");
            sb.AppendLine("</table>");
        }

        private static void AppendILCacheStatsSection(StringBuilder sb, ILCache ilCache)
        {
            var stats = ilCache.GetReportStats();
            sb.AppendLine("<h2 class=\"section-heading\">IL Cache Stats</h2>");
            sb.AppendLine("<table class=\"stat-table\">");
            sb.AppendLine("  <tbody>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Hits</td><td class=\"stat-value\">{stats.Hits}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Misses</td><td class=\"stat-value\">{stats.Misses}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Hit Rate</td><td class=\"stat-value\">{stats.HitRatePct:F1}%</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Stores</td><td class=\"stat-value\">{stats.Stores}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Evicted</td><td class=\"stat-value\">{stats.Evicted}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">Expired</td><td class=\"stat-value\">{stats.Expired}</td></tr>");
            sb.AppendLine("  </tbody>");
            sb.AppendLine("</table>");
        }

        private void AppendWarningsSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            ConfigSettings config,
            ILCache? ilCache)
        {
            bool hasMd5 = _fileDiffResultLists.HasAnyMd5Mismatch;
            bool hasTs  = _fileDiffResultLists.HasAnyNewFileTimestampOlderThanOldWarning;
            if (!hasMd5 && !hasTs) return;

            sb.AppendLine("<h2 class=\"section-heading\"><span class=\"warn-icon\">&#x26A0;</span> Warnings</h2>");
            sb.AppendLine("<ul class=\"warnings\">");
            if (hasMd5)
                sb.AppendLine($"  <li>{HtmlEncode(Constants.WARNING_MD5_MISMATCH)}</li>");
            if (hasTs)
            {
                var warnings = _fileDiffResultLists.NewFileTimestampOlderThanOldWarnings.Values
                    .OrderBy(w => w.FileRelativePath, StringComparer.OrdinalIgnoreCase).ToList();
                sb.AppendLine($"  <li>One or more <strong>modified</strong> files in <code>new</code> have older last-modified timestamps than the corresponding files in <code>old</code>.</li>");
                sb.AppendLine("</ul>");

                // Timestamp-regressed files table (same style as Modified Files)
                sb.AppendLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ ! ] Modified Files — Timestamps Regressed ({warnings.Count})</h2>");
                AppendTableStart(sb, TH_BG_MODIFIED, "Diff Reason");
                sb.AppendLine("<tbody>");
                int idx = 0;
                foreach (var w in warnings)
                {
                    string ts = $"[{HtmlEncode(w.OldTimestamp)}{TIMESTAMP_ARROW}{HtmlEncode(w.NewTimestamp)}]";
                    _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(w.FileRelativePath, out var diffDetail);
                    _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(w.FileRelativePath, out var asm);
                    string col6 = BuildDiffDetailDisplay(diffDetail);
                    AppendFileRow(sb, "tsw", idx, w.FileRelativePath, ts, col6, asm ?? "");

                    if (config.EnableInlineDiff &&
                        (diffDetail == FileDiffResultLists.DiffDetailResult.TextMismatch ||
                         diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch))
                    {
                        AppendInlineDiffRow(sb, idx, w.FileRelativePath, oldFolderAbsolutePath, newFolderAbsolutePath,
                            reportsFolderAbsolutePath, config, diffDetail, asm ?? "", ilCache, sectionPrefix: "tsw");
                    }

                    idx++;
                }
                sb.AppendLine("</tbody></table></div>");
                return;
            }
            sb.AppendLine("</ul>");
        }
    }
}
