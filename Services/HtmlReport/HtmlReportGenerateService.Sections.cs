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
            IReadOnlyConfigSettings config)
        {
            sb.AppendLine($"<h1>{HtmlEncode("Folder Diff Report")}</h1>");
            sb.AppendLine("<div class=\"report-header\">");

            // Key metrics as card grid / キーメトリクスをカードグリッドで表示
            sb.AppendLine("<div class=\"header-grid\">");
            AppendHeaderCard(sb, "App Version", $"FolderDiffIL4DotNet {HtmlEncode(appVersion)}");
            AppendHeaderCard(sb, "Computer", HtmlEncode(computerName));
            AppendHeaderCard(sb, "IL Disassembler", HtmlEncode(BuildDisassemblerHeaderText()));
            if (!string.IsNullOrWhiteSpace(elapsedTimeString))
                AppendHeaderCard(sb, "Elapsed Time", HtmlEncode(elapsedTimeString));
            if (config.ShouldOutputFileTimestamps)
                AppendHeaderCard(sb, "Timezone", HtmlEncode(DateTimeOffset.Now.ToString("zzz")));
            sb.AppendLine("</div>");

            // Folder paths / フォルダパス
            sb.AppendLine("<div class=\"header-paths\">");
            sb.AppendLine($"  <div class=\"header-path\"><span class=\"header-path-label\">Old</span><span class=\"header-path-value\">{HtmlEncode(oldFolderAbsolutePath)}</span></div>");
            sb.AppendLine($"  <div class=\"header-path\"><span class=\"header-path-label\">New</span><span class=\"header-path-value\">{HtmlEncode(newFolderAbsolutePath)}</span></div>");
            sb.AppendLine("</div>");

            // Disassembler availability table / 逆アセンブラ可用性テーブル
            AppendDisassemblerAvailabilityTable(sb, _fileDiffResultLists.DisassemblerAvailability);

            // Configuration details (always visible) / 設定詳細（常時表示）
            sb.AppendLine("<div class=\"header-config\">");
            sb.AppendLine($"  <div class=\"header-config-title\">{HtmlEncode("Configuration Details")}</div>");
            AppendHeaderDetailRow(sb, "Ignored Extensions", HtmlEncode(string.Join(", ", config.IgnoredExtensions)));
            AppendHeaderDetailRow(sb, "Text File Extensions", HtmlEncode(string.Join(", ", config.TextFileExtensions)));
            if (config.ShouldIgnoreILLinesContainingConfiguredStrings)
            {
                var ilIgnoreStrings = GetNormalizedIlIgnoreStrings(config);
                if (ilIgnoreStrings.Count == 0)
                {
                    AppendHeaderDetailRow(sb, "IL Line Ignore", HtmlEncode("Enabled, but no non-empty strings are configured."));
                }
                else
                {
                    sb.AppendLine("    <div class=\"header-detail-row\">");
                    sb.AppendLine($"      <div class=\"header-detail-label\">{HtmlEncode("IL Ignored Strings")}</div>");
                    sb.AppendLine("      <div class=\"header-detail-value\">");
                    sb.AppendLine("        <div class=\"il-ignore-scroll\"><table class=\"legend-table il-ignore-table\">");
                    sb.AppendLine($"          <thead><tr><th style=\"background:{TH_BG_DEFAULT}\">{HtmlEncode("Ignored String")}</th></tr></thead>");
                    sb.AppendLine("          <tbody>");
                    foreach (var s in ilIgnoreStrings)
                    {
                        sb.AppendLine($"            <tr><td>{HtmlEncode($"\"{s}\"")}</td></tr>");
                    }
                    sb.AppendLine("          </tbody>");
                    sb.AppendLine("        </table></div>");
                    sb.AppendLine("      </div>");
                    sb.AppendLine("    </div>");
                }
            }
            sb.AppendLine("</div>");

            // Notes / ノート
            sb.AppendLine("<div class=\"header-notes\">");
            sb.AppendLine($"  <p class=\"header-note\">{HtmlEncode("When diffing IL, lines starting with")} <code>{HtmlEncode(Constants.IL_MVID_LINE_PREFIX)}</code> {HtmlEncode("(if present) are ignored because they contain disassembler-emitted Module Version ID metadata that can change on rebuild without meaning the executable IL changed.")}</p>");
            sb.AppendLine($"  <p class=\"header-note\">{HtmlEncode("Inline diffs for ILMismatch and TextMismatch are computed using the")} " +
                "<a href=\"http://www.xmailserver.org/diff2.pdf\">" +
                "Myers Diff Algorithm (E.&nbsp;W.&nbsp;Myers, &ldquo;An O(ND) Difference Algorithm and Its Variations,&rdquo; <i>Algorithmica</i> <b>1</b>(2), 1986)</a>.</p>");
            sb.AppendLine("</div>");

            // Legends side by side / 凡例を横並びで表示
            sb.AppendLine("<div class=\"header-legends\">");

            // Legend — Diff Detail
            sb.AppendLine("<div>");
            sb.AppendLine($"  <div class=\"header-legend-title\">{HtmlEncode("Legend — Diff Detail")}</div>");
            sb.AppendLine("  <table class=\"legend-table\">");
            sb.AppendLine($"    <thead><tr><th style=\"background:{TH_BG_DEFAULT}\">{HtmlEncode("Label")}</th><th style=\"background:{TH_BG_DEFAULT}\">{HtmlEncode("Description")}</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            sb.AppendLine($"      <tr><td><code>SHA256Match</code> / <code>SHA256Mismatch</code></td><td>{HtmlEncode("SHA256 hash match / mismatch")}</td></tr>");
            sb.AppendLine($"      <tr><td><code>ILMatch</code> / <code>ILMismatch</code></td><td>{HtmlEncode("IL(Intermediate Language) match / mismatch")}</td></tr>");
            sb.AppendLine($"      <tr><td><code>TextMatch</code> / <code>TextMismatch</code></td><td>{HtmlEncode("Text match / mismatch")}</td></tr>");
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine("</div>");

            // Legend — Change Importance
            sb.AppendLine("<div>");
            sb.AppendLine($"  <div class=\"header-legend-title\">{HtmlEncode("Legend — Change Importance")}</div>");
            sb.AppendLine("  <table class=\"legend-table\">");
            sb.AppendLine($"    <thead><tr><th style=\"background:{TH_BG_DEFAULT}\">{HtmlEncode("Label")}</th><th style=\"background:{TH_BG_DEFAULT}\">{HtmlEncode("Description")}</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            sb.AppendLine($"      <tr><td style=\"color:#d1242f;font-weight:bold\">High</td><td>{HtmlEncode("Breaking change candidate: public/protected API removal, access narrowing, return-type / parameter / member-type change")}</td></tr>");
            sb.AppendLine($"      <tr><td style=\"color:#d97706;font-weight:bold\">Medium</td><td>{HtmlEncode("Notable change: public/protected member addition, modifier change, access widening, internal removal")}</td></tr>");
            sb.AppendLine($"      <tr><td>Low</td><td>{HtmlEncode("Low-impact change: body-only modification, internal/private member addition")}</td></tr>");
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine("</div>");

            sb.AppendLine("</div>"); // header-legends
            sb.AppendLine("</div>"); // report-header
        }

        /// <summary>Appends a single header card element. / ヘッダーカード要素を1つ追加します。</summary>
        private static void AppendHeaderCard(StringBuilder sb, string label, string value)
        {
            sb.AppendLine($"  <div class=\"header-card\"><div class=\"header-card-label\">{HtmlEncode(label)}</div><div class=\"header-card-value\">{value}</div></div>");
        }

        /// <summary>Appends a detail row inside the collapsible configuration section. / 折りたたみ設定セクション内に詳細行を追加します。</summary>
        private static void AppendHeaderDetailRow(StringBuilder sb, string label, string value)
        {
            sb.AppendLine($"    <div class=\"header-detail-row\"><div class=\"header-detail-label\">{HtmlEncode(label)}</div><div class=\"header-detail-value\">{value}</div></div>");
        }

        private void AppendIgnoredSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            IReadOnlyConfigSettings config)
        {
            var items = _fileDiffResultLists.IgnoredFilesRelativePathToLocation
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2>[ x ] {HtmlEncode("Ignored Files")} ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine($"<p class=\"empty\">{HtmlEncode("(none)")}</p>"); return; }

            AppendTableStart(sb, TH_BG_DEFAULT, "Location", hideClasses: "hide-disasm");
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
            IReadOnlyConfigSettings config)
        {
            var items = _fileDiffResultLists.UnchangedFilesRelativePath
                .OrderBy(p => _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(p, out var d) ? GetUnchangedSortOrder(d) : 3)
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2>[ = ] {HtmlEncode("Unchanged Files")} ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine($"<p class=\"empty\">{HtmlEncode("(none)")}</p>"); return; }

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
                    ts = oldTs != newTs ? $"{oldTs}{TIMESTAMP_ARROW}{newTs}" : newTs;
                }
                _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(path, out var diffDetail);
                string col6 = BuildDiffDetailDisplay(diffDetail);
                _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(path, out var asm);
                AppendFileRow(sb, "unch", idx, path, ts, col6, asm ?? "");
                idx++;
            }
            sb.AppendLine("</tbody></table></div>");
        }

        private void AppendAddedSection(StringBuilder sb, IReadOnlyConfigSettings config)
        {
            var items = _fileDiffResultLists.AddedFilesAbsolutePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_ADDED}\">[ + ] {HtmlEncode("Added Files")} ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine($"<p class=\"empty\">{HtmlEncode("(none)")}</p>"); return; }

            AppendTableStart(sb, TH_BG_ADDED, "Diff Reason", hideClasses: "hide-col6 hide-disasm");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var absPath in items)
            {
                string ts = config.ShouldOutputFileTimestamps
                    ? Caching.TimestampCache.GetOrAdd(absPath) : "";
                AppendFileRow(sb, "add", idx, absPath, ts, "");
                idx++;
            }
            sb.AppendLine("</tbody></table></div>");
        }

        private void AppendRemovedSection(StringBuilder sb, IReadOnlyConfigSettings config)
        {
            var items = _fileDiffResultLists.RemovedFilesAbsolutePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_REMOVED}\">[ - ] {HtmlEncode("Removed Files")} ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine($"<p class=\"empty\">{HtmlEncode("(none)")}</p>"); return; }

            AppendTableStart(sb, TH_BG_REMOVED, "Diff Reason", hideClasses: "hide-col6 hide-disasm");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var absPath in items)
            {
                string ts = config.ShouldOutputFileTimestamps
                    ? Caching.TimestampCache.GetOrAdd(absPath) : "";
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
            IReadOnlyConfigSettings config,
            ILCache? ilCache)
        {
            var items = _fileDiffResultLists.ModifiedFilesRelativePath
                .OrderBy(p => _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(p, out var d) ? GetModifiedSortOrder(d) : 3)
                .ThenBy(p => GetImportanceSortOrder(_fileDiffResultLists.GetMaxImportance(p)))
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ * ] {HtmlEncode("Modified Files")} ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine($"<p class=\"empty\">{HtmlEncode("(none)")}</p>"); return; }

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
                    ts = $"{oldTs}{TIMESTAMP_ARROW}{newTs}";
                }
                _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(path, out var diffDetail);
                _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(path, out var asm);
                string col6 = BuildDiffDetailDisplay(diffDetail);
                string imp = GetImportanceLabel(path);
                AppendFileRow(sb, "mod", idx, path, ts, col6, asm ?? "", imp);

                // Method-level changes row (above IL diff)
                if (config.ShouldIncludeAssemblySemanticChangesInReport &&
                    diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch &&
                    _fileDiffResultLists.FileRelativePathToAssemblySemanticChanges.TryGetValue(path, out var semanticChanges))
                {
                    AppendAssemblySemanticChangesRow(sb, idx, path, semanticChanges, config);
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
            IReadOnlyConfigSettings config,
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
                string encoded = HtmlEncode(diffLines[0].Text)
                    .Replace("InlineDiffMaxEditDistance", "<code>InlineDiffMaxEditDistance</code>");
                encoded += $" (current value: <code>{maxEditDistance}</code>)";
                sb.AppendLine("<tr class=\"diff-row\">");
                sb.AppendLine($"  <td colspan=\"8\"><p class=\"diff-skipped\">#{recordNo} {encoded}</p></td>");
                sb.AppendLine("</tr>");
                return;
            }

            if (diffLines.Count > maxDiffLines)
            {
                sb.AppendLine("<tr class=\"diff-row\">");
                sb.AppendLine($"  <td colspan=\"8\"><p class=\"diff-skipped\">#{recordNo} Inline diff skipped: diff too large " +
                    $"({diffLines.Count} diff lines; limit is {maxDiffLines}). " +
                    $"Increase <code>InlineDiffMaxDiffLines</code> in config to enable. (current value: <code>{maxDiffLines}</code>)</p></td>");
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
            sb.AppendLine($"  <td colspan=\"8\">");
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

        private void AppendAssemblySemanticChangesRow(
            StringBuilder sb,
            int idx,
            string assemblyPath,
            AssemblySemanticChangesSummary summary,
            IReadOnlyConfigSettings config,
            string sectionPrefix = "mod")
        {
            int recordNo = idx + 1;
            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine("<div class=\"semantic-changes\">");

            if (summary.Entries.Count > 0)
            {
                contentBuilder.AppendLine($"<p class=\"sc-caveat\">{HtmlEncode("Note: The semantic summary is supplementary information. Always verify the final details in the inline IL diff below.")}</p>");
            }

            if (summary.Entries.Count > 0)
            {
                contentBuilder.AppendLine("<table class=\"semantic-changes-table sc-detail\">");
                contentBuilder.AppendLine("<colgroup>");
                contentBuilder.AppendLine("  <col class=\"sc-col-cb-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-class-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-basetype-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-change-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-importance-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-kind-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-access-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-mods-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-type-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-name-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-rettype-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-params-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-body-g\">");
                contentBuilder.AppendLine("</colgroup>");
                contentBuilder.AppendLine("<thead><tr>");
                contentBuilder.AppendLine("  <th class=\"sc-col-cb\">&#x2713;</th>");
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-class-w\">{HtmlEncode("Class")}</th>");
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-basetype-w\">{HtmlEncode("BaseType")}</th>");
                contentBuilder.AppendLine($"  <th>{HtmlEncode("Status")}</th><th>{HtmlEncode("Importance")}</th><th>{HtmlEncode("Kind")}</th><th>{HtmlEncode("Access")}</th><th>{HtmlEncode("Modifiers")}</th>");
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-type-w\">{HtmlEncode("Type")}</th>");
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-name-w\">{HtmlEncode("Name")}</th>");
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-rettype-w\">{HtmlEncode("ReturnType")}</th>");
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-params-w\">{HtmlEncode("Parameters")}</th>");
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-body-w\">{HtmlEncode("Body")}</th>");
                contentBuilder.AppendLine("</tr></thead>");
                contentBuilder.AppendLine("<tbody>");
                string prevType = "";
                int scRowIdx = 0;
                foreach (var e in summary.EntriesByImportance)
                {
                    bool isCont = e.TypeName == prevType;
                    string classTd = !isCont ? HtmlEncode(e.TypeName) : "";
                    string baseTypeTd = !isCont ? HtmlEncode(e.BaseType) : "";
                    prevType = e.TypeName;
                    string trOpen = isCont ? "<tr class=\"group-cont\">" : "<tr>";
                    string accessTd = CodeWrapArrow(e.Access);
                    string modifiersTd = CodeWrapArrow(e.Modifiers);
                    string bodyTd = e.Body.Length > 0 ? $"<code>{HtmlEncode(e.Body)}</code>" : "";
                    string cbId = $"sc_{sectionPrefix}_{idx}_{scRowIdx}";
                    string changeMarker = ChangeToMarker(e.Change);
                    string statusBg = ChangeToStatusBg(e.Change);
                    string statusStyle = statusBg.Length > 0 ? $" style=\"background:{statusBg}\"" : "";
                    string impMarker = ImportanceToMarker(e.Importance);
                    string impStyleVal = ImportanceToStyle(e.Importance);
                    string impStyle = impStyleVal.Length > 0 ? $" style=\"{impStyleVal}\"" : "";
                    contentBuilder.AppendLine($"{trOpen}<td class=\"sc-col-cb\"><input type=\"checkbox\" id=\"{cbId}\"></td><td>{classTd}</td><td>{baseTypeTd}</td><td{statusStyle}>{changeMarker}</td><td{impStyle}>{impMarker}</td><td><code>{HtmlEncode(e.MemberKind)}</code></td><td>{accessTd}</td><td>{modifiersTd}</td><td>{HtmlEncode(e.MemberType)}</td><td>{HtmlEncode(e.MemberName)}</td><td>{HtmlEncode(e.ReturnType)}</td><td>{HtmlEncode(e.Parameters)}</td><td>{bodyTd}</td></tr>");
                    scRowIdx++;
                }
                contentBuilder.AppendLine("</tbody></table>");
            }
            else
            {
                contentBuilder.AppendLine($"<p>{HtmlEncode("No structural changes detected. See IL diff for implementation-level differences.")}</p>");
            }

            contentBuilder.AppendLine("</div>");

            string detailsId = $"semantic_{sectionPrefix}_{idx}";
            string highSuffix = summary.HighImportanceCount > 0
                ? $" ({summary.HighImportanceCount} High)"
                : "";
            string summaryLabel = $"      <summary class=\"diff-summary\">#{recordNo} {HtmlEncode("Show assembly semantic changes")}{highSuffix}</summary>";
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

        private static string ChangeToMarker(string change)
            => change switch { "Added" => "[ + ]", "Removed" => "[ - ]", "Modified" => "[ * ]", _ => change };

        private static string ChangeToStatusBg(string change)
            => change switch { "Added" => TH_BG_ADDED, "Removed" => TH_BG_REMOVED, "Modified" => TH_BG_MODIFIED, _ => "" };

        private void AppendSummarySection(StringBuilder sb, IReadOnlyConfigSettings config)
        {
            sb.AppendLine($"<h2 class=\"section-heading\">{HtmlEncode("Summary")}</h2>");
            sb.AppendLine("<table class=\"stat-table\">");
            sb.AppendLine($"  <thead><tr><th style=\"background:{TH_BG_DEFAULT}\">Category</th><th style=\"background:{TH_BG_DEFAULT}\">Count</th></tr></thead>");
            sb.AppendLine("  <tbody>");
            var stats = _fileDiffResultLists.SummaryStatistics;
            if (config.ShouldIncludeIgnoredFiles)
                sb.AppendLine($"    <tr><td class=\"stat-label\">{HtmlEncode("Ignored")}</td><td class=\"stat-value\">{stats.IgnoredCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">{HtmlEncode("Unchanged")}</td><td class=\"stat-value\">{stats.UnchangedCount}</td></tr>");
            sb.AppendLine($"    <tr style=\"background:{TH_BG_ADDED}\"><td class=\"stat-label\">{HtmlEncode("Added")}</td><td class=\"stat-value\">{stats.AddedCount}</td></tr>");
            sb.AppendLine($"    <tr style=\"background:{TH_BG_REMOVED}\"><td class=\"stat-label\">{HtmlEncode("Removed")}</td><td class=\"stat-value\">{stats.RemovedCount}</td></tr>");
            sb.AppendLine($"    <tr style=\"background:{TH_BG_MODIFIED}\"><td class=\"stat-label\">{HtmlEncode("Modified")}</td><td class=\"stat-value\">{stats.ModifiedCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">{HtmlEncode("Compared")}</td><td class=\"stat-value\">{_fileDiffResultLists.OldFilesAbsolutePath.Count} ({HtmlEncode("Old")}) vs {_fileDiffResultLists.NewFilesAbsolutePath.Count} ({HtmlEncode("New")})</td></tr>");
            sb.AppendLine("  </tbody>");
            sb.AppendLine("</table>");
        }

        private static void AppendILCacheStatsSection(StringBuilder sb, ILCache ilCache)
        {
            var stats = ilCache.GetReportStats();
            sb.AppendLine($"<h2 class=\"section-heading\">{HtmlEncode("IL Cache Stats")}</h2>");
            sb.AppendLine("<table class=\"stat-table\">");
            sb.AppendLine($"  <thead><tr><th style=\"background:{TH_BG_DEFAULT}\">Metric</th><th style=\"background:{TH_BG_DEFAULT}\">Value</th></tr></thead>");
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
            IReadOnlyConfigSettings config,
            ILCache? ilCache)
        {
            bool hasSha256 = _fileDiffResultLists.HasAnySha256Mismatch;
            bool hasTs  = _fileDiffResultLists.HasAnyNewFileTimestampOlderThanOldWarning;
            if (!hasSha256 && !hasTs) return;

            sb.AppendLine($"<h2 class=\"section-heading\"><span class=\"warn-icon\">&#x26A0;</span> {HtmlEncode("Warnings")}</h2>");

            // SHA256Mismatch warning + detail table (grouped together)
            // SHA256Mismatch 警告 + 詳細テーブル（まとめて配置）
            if (hasSha256)
            {
                sb.AppendLine("<ul class=\"warnings\">");
                sb.AppendLine($"  <li>{HtmlEncode("One or more files were classified as SHA256Mismatch. Manual review is recommended because only an SHA256 hash comparison was possible.")}</li>");
                sb.AppendLine("</ul>");

                var sha256Files = _fileDiffResultLists.FileRelativePathToDiffDetailDictionary
                    .Where(kv => kv.Value == FileDiffResultLists.DiffDetailResult.SHA256Mismatch)
                    .OrderBy(kv => GetImportanceSortOrder(_fileDiffResultLists.GetMaxImportance(kv.Key)))
                    .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (sha256Files.Count > 0)
                {
                    sb.AppendLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ ! ] {HtmlEncode("Modified Files")} &#x2014; {HtmlEncode("SHA256Mismatch (Manual Review Recommended)")} ({sha256Files.Count})</h2>");
                    AppendTableStart(sb, TH_BG_MODIFIED, "Diff Reason", hideClasses: "hide-disasm");
                    sb.AppendLine("<tbody>");
                    int idx = 0;
                    foreach (var kv in sha256Files)
                    {
                        string ts = "";
                        if (config.ShouldOutputFileTimestamps)
                        {
                            string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolderAbsolutePath, kv.Key));
                            string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolderAbsolutePath, kv.Key));
                            ts = $"{oldTs}{TIMESTAMP_ARROW}{newTs}";
                        }
                        string col6 = BuildDiffDetailDisplay(kv.Value);
                        string imp = GetImportanceLabel(kv.Key);
                        AppendFileRow(sb, "sha256w", idx, kv.Key, ts, col6, importance: imp);
                        idx++;
                    }
                    sb.AppendLine("</tbody></table></div>");
                }
            }

            // Timestamp-regressed warning + detail table (grouped together)
            // タイムスタンプ逆行 警告 + 詳細テーブル（まとめて配置）
            if (hasTs)
            {
                sb.AppendLine("<ul class=\"warnings\">");
                sb.AppendLine($"  <li>{HtmlEncode("One or more modified files in new have older last-modified timestamps than the corresponding files in old.")}</li>");
                sb.AppendLine("</ul>");

                var warnings = _fileDiffResultLists.NewFileTimestampOlderThanOldWarnings.Values
                    .OrderBy(w => _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(w.FileRelativePath, out var d) ? GetModifiedSortOrder(d) : 3)
                    .ThenBy(w => GetImportanceSortOrder(_fileDiffResultLists.GetMaxImportance(w.FileRelativePath)))
                    .ThenBy(w => w.FileRelativePath, StringComparer.OrdinalIgnoreCase).ToList();
                sb.AppendLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ ! ] {HtmlEncode("Modified Files")} &#x2014; {HtmlEncode("Timestamps Regressed")} ({warnings.Count})</h2>");
                AppendTableStart(sb, TH_BG_MODIFIED, "Diff Reason", hideClasses: "hide-disasm");
                sb.AppendLine("<tbody>");
                int idx = 0;
                foreach (var w in warnings)
                {
                    string ts = $"{HtmlEncode(w.OldTimestamp)}{TIMESTAMP_ARROW}{HtmlEncode(w.NewTimestamp)}";
                    _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(w.FileRelativePath, out var diffDetail);
                    _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(w.FileRelativePath, out var asm);
                    string col6 = BuildDiffDetailDisplay(diffDetail);
                    string imp = GetImportanceLabel(w.FileRelativePath);
                    AppendFileRow(sb, "tsw", idx, w.FileRelativePath, ts, col6, importance: imp);

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
            }
        }
    }
}
