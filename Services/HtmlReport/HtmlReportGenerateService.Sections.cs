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
    // レポートセクションビルダー（Header, Ignored, Unchanged, Added, Removed, Modified, Summary, ILCacheStats, Warnings）。
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

            // Metadata cards row / メタデータカード行
            sb.AppendLine("<div class=\"header-cards\">");
            AppendHeaderCard(sb, "App Version", $"FolderDiffIL4DotNet {HtmlEncode(appVersion)}");
            AppendHeaderCard(sb, "Computer", HtmlEncode(computerName));
            if (config.ShouldOutputFileTimestamps)
                AppendHeaderCard(sb, "Timezone", HtmlEncode(DateTimeOffset.Now.ToString("zzz")));
            if (!string.IsNullOrWhiteSpace(elapsedTimeString))
                AppendHeaderCard(sb, "Elapsed Time", HtmlEncode(elapsedTimeString));
            sb.AppendLine("</div>");

            // Folder paths (always full width, fixed order) / フォルダパス（常に全幅、順序固定）
            sb.AppendLine($"<div class=\"header-path\"><div class=\"header-path-label\">Old Folder</div><div class=\"header-path-value\">{HtmlEncode(oldFolderAbsolutePath)}</div></div>");
            sb.AppendLine($"<div class=\"header-path\"><div class=\"header-path-label\">New Folder</div><div class=\"header-path-value\">{HtmlEncode(newFolderAbsolutePath)}</div></div>");

            // Disassembler availability with in-use marker / 逆アセンブラ可用性（使用中マーカー付き）
            AppendDisassemblerAvailabilitySection(sb, _fileDiffResultLists.DisassemblerAvailability, BuildDisassemblerHeaderText());

            // Ignored Extensions (standalone rounded section) / 無視する拡張子（独立した角丸セクション）
            sb.AppendLine($"<div class=\"header-path\"><div class=\"header-path-label\">Ignored Extensions</div><div class=\"header-path-value\">{HtmlEncode(string.Join(", ", config.IgnoredExtensions))}</div></div>");

            // Text File Extensions (standalone rounded section) / テキストファイル拡張子（独立した角丸セクション）
            sb.AppendLine($"<div class=\"header-path\"><div class=\"header-path-label\">Text File Extensions</div><div class=\"header-path-value\">{HtmlEncode(string.Join(", ", config.TextFileExtensions))}</div></div>");

            // IL Ignored Strings (standalone rounded section, comma-separated) / IL 無視文字列（独立した角丸セクション、カンマ区切り）
            if (config.ShouldIgnoreILLinesContainingConfiguredStrings)
            {
                var ilIgnoreStrings = GetNormalizedIlIgnoreStrings(config);
                if (ilIgnoreStrings.Count == 0)
                {
                    sb.AppendLine($"<div class=\"header-path\"><div class=\"header-path-label\">{HtmlEncode("IL Ignored Strings")}</div><div class=\"header-path-value\">{HtmlEncode("Enabled, but no non-empty strings are configured.")}</div></div>");
                }
                else
                {
                    sb.AppendLine($"<div class=\"header-path\"><div class=\"header-path-label\">{HtmlEncode("IL Ignored Strings")}</div><div class=\"header-path-value\">");
                    foreach (var s in ilIgnoreStrings)
                    {
                        sb.AppendLine($"<div>{HtmlEncode($"\"{s}\"")}</div>");
                    }
                    sb.AppendLine("</div></div>");
                }
            }

            // MVID note (standalone rounded section) / MVID ノート（独立した角丸セクション）
            sb.AppendLine($"<div class=\"header-path\"><div class=\"header-path-label\">{HtmlEncode("IL Diff Note")}</div><div class=\"header-path-value\">{HtmlEncode("Lines starting with")} <code>{HtmlEncode(Constants.IL_MVID_LINE_PREFIX)}</code> {HtmlEncode("are auto-ignored (Module Version ID metadata).")}</div></div>");
            sb.AppendLine("</div>"); // report-header
        }

        /// <summary>Appends a single header card element. / ヘッダーカード要素を1つ追加します。</summary>
        private static void AppendHeaderCard(StringBuilder sb, string label, string value, string? cssClass = null)
        {
            var cls = cssClass != null ? $"header-card {cssClass}" : "header-card";
            sb.AppendLine($"  <div class=\"{cls}\"><div class=\"header-card-label\">{HtmlEncode(label)}</div><div class=\"header-card-value\">{value}</div></div>");
        }

        private void AppendIgnoredSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            IReadOnlyConfigSettings config)
        {
            var items = _fileDiffResultLists.IgnoredFilesRelativePathToLocation
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToList();
            if (items.Count == 0)
            {
                sb.AppendLine($"<h2>[ x ] {HtmlEncode("Ignored Files")} (0)</h2>");
                sb.AppendLine($"<p class=\"empty\">{HtmlEncode("(none)")}</p>");
                return;
            }

            sb.AppendLine($"<h2>[ x ] {HtmlEncode("Ignored Files")} ({items.Count})</h2>");
            sb.AppendLine("<details class=\"lazy-section\">");
            sb.AppendLine("<summary></summary>");
            AppendTableStart(sb, TH_BG_DEFAULT, "Location", hideClasses: "hide-disasm hide-tag");
            sb.AppendLine("<tbody>");
            int idx = 0;
            foreach (var entry in items)
            {
                bool hasOld = (entry.Value & FileDiffResultLists.IgnoredFileLocation.Old) != 0;
                bool hasNew = (entry.Value & FileDiffResultLists.IgnoredFileLocation.New) != 0;

                // 3-2: absolute path for single-side, relative for both-sides / 片方のみの場合は絶対パス、両方の場合は相対パス
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
            sb.AppendLine("</details>");
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
            if (items.Count == 0)
            {
                sb.AppendLine($"<h2>[ = ] {HtmlEncode("Unchanged Files")} (0)</h2>");
                sb.AppendLine($"<p class=\"empty\">{HtmlEncode("(none)")}</p>");
                return;
            }

            sb.AppendLine($"<h2>[ = ] {HtmlEncode("Unchanged Files")} ({items.Count})</h2>");
            sb.AppendLine("<details class=\"lazy-section\">");
            sb.AppendLine("<summary></summary>");
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
            sb.AppendLine("</details>");
        }

        private void AppendAddedSection(StringBuilder sb, IReadOnlyConfigSettings config)
        {
            var items = _fileDiffResultLists.AddedFilesAbsolutePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_ADDED}\">[ + ] {HtmlEncode("Added Files")} ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine($"<p class=\"empty\">{HtmlEncode("(none)")}</p>"); return; }

            AppendTableStart(sb, TH_BG_ADDED, "Diff Reason", hideClasses: "hide-col6 hide-disasm hide-tag");
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

            AppendTableStart(sb, TH_BG_REMOVED, "Diff Reason", hideClasses: "hide-col6 hide-disasm hide-tag");
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
                string impLevels = GetImportanceLevelsLabel(path);
                string tag = GetChangeTagDisplay(path);
                AppendFileRow(sb, "mod", idx, path, ts, col6, asm ?? "", imp, impLevels, tag);

                // Method-level changes row (above IL diff)
                if (config.ShouldIncludeAssemblySemanticChangesInReport &&
                    diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch &&
                    _fileDiffResultLists.FileRelativePathToAssemblySemanticChanges.TryGetValue(path, out var semanticChanges))
                {
                    AppendAssemblySemanticChangesRow(sb, idx, path, semanticChanges, config);
                }

                // Dependency changes row for .deps.json files
                // .deps.json ファイルの依存関係変更行
                if (config.ShouldIncludeDependencyChangesInReport &&
                    _fileDiffResultLists.FileRelativePathToDependencyChanges.TryGetValue(path, out var depChanges))
                {
                    AppendDependencyChangesRow(sb, idx, depChanges, config);
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

        private void AppendSummarySection(StringBuilder sb, IReadOnlyConfigSettings config)
        {
            sb.AppendLine($"<h2 class=\"section-heading\">{HtmlEncode("Summary")}</h2>");
            sb.AppendLine("<table class=\"stat-table\">");
            sb.AppendLine("<colgroup><col class=\"stat-col1\"><col class=\"stat-col2\"></colgroup>");
            sb.AppendLine($"  <thead><tr><th scope=\"col\" style=\"background:{TH_BG_DEFAULT}\">Category</th><th scope=\"col\" style=\"background:{TH_BG_DEFAULT}\">Count</th></tr></thead>");
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
            sb.AppendLine("<colgroup><col class=\"stat-col1\"><col class=\"stat-col2\"></colgroup>");
            sb.AppendLine($"  <thead><tr><th scope=\"col\" style=\"background:{TH_BG_DEFAULT}\">Metric</th><th scope=\"col\" style=\"background:{TH_BG_DEFAULT}\">Value</th></tr></thead>");
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

            sb.AppendLine($"<h2 class=\"section-heading\">{HtmlEncode("Warnings")}</h2>");

            // SHA256Mismatch warning + detail table (grouped together)
            // SHA256Mismatch 警告 + 詳細テーブル（まとめて配置）
            if (hasSha256)
            {
                var sha256Files = _fileDiffResultLists.FileRelativePathToDiffDetailDictionary
                    .Where(kv => kv.Value == FileDiffResultLists.DiffDetailResult.SHA256Mismatch)
                    .OrderBy(kv => GetImportanceSortOrder(_fileDiffResultLists.GetMaxImportance(kv.Key)))
                    .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (sha256Files.Count > 0)
                {
                    sb.AppendLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ ! ] {HtmlEncode("Modified Files")} &#x2014; {HtmlEncode("SHA256Mismatch: binary diff only — not a .NET assembly or disassembler unavailable")} ({sha256Files.Count})</h2>");
                    AppendTableStart(sb, TH_BG_MODIFIED, "Diff Reason");
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
                        string impLevels = GetImportanceLevelsLabel(kv.Key);
                        AppendFileRow(sb, "sha256w", idx, kv.Key, ts, col6, importance: imp, importanceLevels: impLevels);
                        idx++;
                    }
                    sb.AppendLine("</tbody></table></div>");
                }
            }

            // Timestamp-regressed warning + detail table (grouped together)
            // タイムスタンプ逆行 警告 + 詳細テーブル（まとめて配置）
            if (hasTs)
            {
                var warnings = _fileDiffResultLists.NewFileTimestampOlderThanOldWarnings.Values
                    .OrderBy(w => _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(w.FileRelativePath, out var d) ? GetModifiedSortOrder(d) : 3)
                    .ThenBy(w => GetImportanceSortOrder(_fileDiffResultLists.GetMaxImportance(w.FileRelativePath)))
                    .ThenBy(w => w.FileRelativePath, StringComparer.OrdinalIgnoreCase).ToList();
                sb.AppendLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ ! ] {HtmlEncode("Modified Files")} &#x2014; {HtmlEncode("new file timestamps older than old")} ({warnings.Count})</h2>");
                AppendTableStart(sb, TH_BG_MODIFIED, "Diff Reason");
                sb.AppendLine("<tbody>");
                int idx = 0;
                foreach (var w in warnings)
                {
                    string ts = $"{HtmlEncode(w.OldTimestamp)}{TIMESTAMP_ARROW}{HtmlEncode(w.NewTimestamp)}";
                    _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(w.FileRelativePath, out var diffDetail);
                    _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(w.FileRelativePath, out var asm);
                    string col6 = BuildDiffDetailDisplay(diffDetail);
                    string imp = GetImportanceLabel(w.FileRelativePath);
                    string impLevels = GetImportanceLevelsLabel(w.FileRelativePath);
                    string tag = GetChangeTagDisplay(w.FileRelativePath);
                    AppendFileRow(sb, "tsw", idx, w.FileRelativePath, ts, col6, asm ?? "", imp, impLevels, tag);

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
