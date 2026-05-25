using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            TextWriter writer,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string appVersion,
            string elapsedTimeString,
            string computerName,
            IReadOnlyConfigSettings config)
        {
            writer.WriteLine($"<h1>{HtmlEncode("Folder Diff Report")}</h1>");
            writer.WriteLine("<div class=\"report-header\">");

            // Metadata cards row / メタデータカード行
            writer.WriteLine("<div class=\"header-cards\">");
            AppendHeaderCard(writer, "App Version", $"{Common.Constants.APP_NAME} {HtmlEncode(appVersion)}");
            AppendHeaderCard(writer, "Computer", HtmlEncode(computerName));
            if (config.ShouldOutputFileTimestamps)
                AppendHeaderCard(writer, "Timezone", HtmlEncode(DateTimeOffset.Now.ToString("zzz")));
            if (!string.IsNullOrWhiteSpace(elapsedTimeString))
                AppendHeaderCard(writer, "Elapsed Time", HtmlEncode(elapsedTimeString));
            writer.WriteLine("</div>");

            // Folder paths (always full width, fixed order) / フォルダパス（常に全幅、順序固定）
            writer.WriteLine($"<div class=\"header-path\"><div class=\"header-path-label\">Old Folder</div><div class=\"header-path-value\">{HtmlEncode(oldFolderAbsolutePath)}</div></div>");
            writer.WriteLine($"<div class=\"header-path\"><div class=\"header-path-label\">New Folder</div><div class=\"header-path-value\">{HtmlEncode(newFolderAbsolutePath)}</div></div>");

            // Disassembler availability with in-use marker / 逆アセンブラ可用性（使用中マーカー付き）
            AppendDisassemblerAvailabilitySection(writer, _fileDiffResultLists.DisassemblerAvailability, BuildDisassemblerHeaderText());
            AppendDisassemblerWarnings(writer);

            // Ignored Extensions (standalone rounded section) / 無視する拡張子（独立した角丸セクション）
            writer.WriteLine($"<div class=\"header-path\"><div class=\"header-path-label\">Ignored Extensions</div><div class=\"header-path-value\">{HtmlEncode(string.Join(", ", config.IgnoredExtensions))}</div></div>");

            // Text File Extensions (standalone rounded section) / テキストファイル拡張子（独立した角丸セクション）
            writer.WriteLine($"<div class=\"header-path\"><div class=\"header-path-label\">Text File Extensions</div><div class=\"header-path-value\">{HtmlEncode(string.Join(", ", config.TextFileExtensions))}</div></div>");

            // IL Ignored Strings (standalone rounded section, comma-separated) / IL 無視文字列（独立した角丸セクション、カンマ区切り）
            if (config.ShouldIgnoreILLinesContainingConfiguredStrings)
            {
                var ilIgnoreStrings = GetNormalizedIlIgnoreStrings(config);
                if (ilIgnoreStrings.Count == 0)
                {
                    writer.WriteLine($"<div class=\"header-path\"><div class=\"header-path-label\">{HtmlEncode("IL Ignored Strings")}</div><div class=\"header-path-value\">{HtmlEncode("Enabled, but no non-empty strings are configured.")}</div></div>");
                }
                else
                {
                    writer.WriteLine($"<div class=\"header-path\"><div class=\"header-path-label\">{HtmlEncode("IL Ignored Strings")}</div><div class=\"header-path-value\">");
                    foreach (var s in ilIgnoreStrings)
                    {
                        writer.WriteLine($"<div>{HtmlEncode($"\"{s}\"")}</div>");
                    }
                    writer.WriteLine("</div></div>");
                }
            }

            // MVID note — only shown when MVID lines are actually ignored / MVID ノート — MVID 行が実際に除外される場合のみ表示
            if (config.ShouldIgnoreMVID)
            {
                writer.WriteLine($"<div class=\"header-path\"><div class=\"header-path-label\">{HtmlEncode("IL Diff Note")}</div><div class=\"header-path-value\">{HtmlEncode("Lines starting with")} <code>{HtmlEncode(Constants.IL_MVID_LINE_PREFIX)}</code> {HtmlEncode("are auto-ignored (Module Version ID metadata).")}</div></div>");
            }

            writer.WriteLine("</div>"); // report-header
        }

        /// <summary>Appends a single header card element. / ヘッダーカード要素を1つ追加します。</summary>
        private static void AppendHeaderCard(TextWriter writer, string label, string value, string? cssClass = null)
        {
            var cls = cssClass != null ? $"header-card {cssClass}" : "header-card";
            writer.WriteLine($"  <div class=\"{cls}\"><div class=\"header-card-label\">{HtmlEncode(label)}</div><div class=\"header-card-value\">{value}</div></div>");
        }

        private void AppendIgnoredSection(
            TextWriter writer,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            IReadOnlyConfigSettings config)
        {
            var items = _fileDiffResultLists.IgnoredFilesRelativePathToLocation
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToList();
            if (items.Count == 0)
            {
                writer.WriteLine($"<h2>[ x ] {HtmlEncode("Ignored Files")} (0)</h2>");
                writer.WriteLine($"<p class=\"empty\">{HtmlEncode("(none)")}</p>");
                return;
            }

            writer.WriteLine($"<h2>[ x ] {HtmlEncode("Ignored Files")} ({items.Count})</h2>");
            writer.WriteLine("<details class=\"lazy-section\">");
            writer.WriteLine("<summary></summary>");
            AppendTableStart(writer, TH_BG_DEFAULT, "Location", "ign", hideClasses: "hide-disasm hide-tag hide-sdk");
            writer.WriteLine("<tbody>");
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
                AppendFileRow(writer, "ign", idx, displayPath, ts, location);
                idx++;
            }
            writer.WriteLine("</tbody></table></div>");
            writer.WriteLine("</details>");
        }

        private void AppendUnchangedSection(
            TextWriter writer,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            IReadOnlyConfigSettings config)
        {
            var items = _fileDiffResultLists.UnchangedFilesRelativePath
                .OrderBy(p => _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(p, out var d) ? GetUnchangedSortOrder(d) : 3)
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            if (items.Count == 0)
            {
                writer.WriteLine($"<h2>[ = ] {HtmlEncode("Unchanged Files")} (0)</h2>");
                writer.WriteLine($"<p class=\"empty\">{HtmlEncode("(none)")}</p>");
                return;
            }

            writer.WriteLine($"<h2>[ = ] {HtmlEncode("Unchanged Files")} ({items.Count})</h2>");
            writer.WriteLine("<details class=\"lazy-section\">");
            writer.WriteLine("<summary></summary>");
            AppendTableStart(writer, TH_BG_DEFAULT, "Diff Reason", "unch");
            writer.WriteLine("<tbody>");
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
                _fileDiffResultLists.FileRelativePathToSdkVersionDictionary.TryGetValue(path, out var sdkVer);
                AppendFileRow(writer, "unch", idx, path, ts, col6, asm ?? "", sdk: sdkVer ?? "");
                idx++;
            }
            writer.WriteLine("</tbody></table></div>");
            writer.WriteLine("</details>");
        }

        private void AppendAddedSection(TextWriter writer, IReadOnlyConfigSettings config)
        {
            var items = _fileDiffResultLists.AddedFilesAbsolutePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            writer.WriteLine($"<h2 style=\"color:{COLOR_ADDED}\">[ + ] {HtmlEncode("Added Files")} ({items.Count})</h2>");
            if (items.Count == 0) { writer.WriteLine($"<p class=\"empty\">{HtmlEncode("(none)")}</p>"); return; }

            AppendTableStart(writer, TH_BG_ADDED, "Diff Reason", "add", hideClasses: "hide-col6 hide-disasm hide-tag hide-sdk");
            writer.WriteLine("<tbody>");
            int idx = 0;
            foreach (var absPath in items)
            {
                string ts = config.ShouldOutputFileTimestamps
                    ? Caching.TimestampCache.GetOrAdd(absPath) : "";
                AppendFileRow(writer, "add", idx, absPath, ts, "");
                idx++;
            }
            writer.WriteLine("</tbody></table></div>");
        }

        private void AppendRemovedSection(TextWriter writer, IReadOnlyConfigSettings config)
        {
            var items = _fileDiffResultLists.RemovedFilesAbsolutePath
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            writer.WriteLine($"<h2 style=\"color:{COLOR_REMOVED}\">[ - ] {HtmlEncode("Removed Files")} ({items.Count})</h2>");
            if (items.Count == 0) { writer.WriteLine($"<p class=\"empty\">{HtmlEncode("(none)")}</p>"); return; }

            AppendTableStart(writer, TH_BG_REMOVED, "Diff Reason", "rem", hideClasses: "hide-col6 hide-disasm hide-tag hide-sdk");
            writer.WriteLine("<tbody>");
            int idx = 0;
            foreach (var absPath in items)
            {
                string ts = config.ShouldOutputFileTimestamps
                    ? Caching.TimestampCache.GetOrAdd(absPath) : "";
                AppendFileRow(writer, "rem", idx, absPath, ts, "");
                idx++;
            }
            writer.WriteLine("</tbody></table></div>");
        }

        private void AppendModifiedSection(
            TextWriter writer,
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
            writer.WriteLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ * ] {HtmlEncode("Modified Files")} ({items.Count})</h2>");
            if (items.Count == 0) { writer.WriteLine($"<p class=\"empty\">{HtmlEncode("(none)")}</p>"); return; }

            AppendTableStart(writer, TH_BG_MODIFIED, "Diff Reason", "mod");
            writer.WriteLine("<tbody>");
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
                _fileDiffResultLists.FileRelativePathToSdkVersionDictionary.TryGetValue(path, out var sdkVer);
                string col6 = BuildDiffDetailDisplay(diffDetail);
                string imp = GetImportanceLabel(path);
                string impLevels = GetImportanceLevelsLabel(path);
                string tag = GetChangeTagDisplay(path);
                AppendFileRow(writer, "mod", idx, path, ts, col6, asm ?? "", imp, impLevels, tag, sdkVer ?? "");

                // Method-level changes row (above IL diff)
                if (config.ShouldIncludeAssemblySemanticChangesInReport &&
                    diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch &&
                    _fileDiffResultLists.FileRelativePathToAssemblySemanticChanges.TryGetValue(path, out var semanticChanges))
                {
                    AppendAssemblySemanticChangesRow(writer, idx, path, semanticChanges, config);
                }

                // Dependency changes row for .deps.json files
                // .deps.json ファイルの依存関係変更行
                if (config.ShouldIncludeDependencyChangesInReport &&
                    _fileDiffResultLists.FileRelativePathToDependencyChanges.TryGetValue(path, out var depChanges))
                {
                    AppendDependencyChangesRow(writer, idx, depChanges, config);
                }

                if (config.EnableInlineDiff &&
                    (diffDetail == FileDiffResultLists.DiffDetailResult.TextMismatch ||
                     diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch))
                {
                    AppendInlineDiffRow(writer, idx, path, oldFolderAbsolutePath, newFolderAbsolutePath,
                        reportsFolderAbsolutePath, config, diffDetail, asm ?? "", ilCache);
                }

                idx++;
            }
            writer.WriteLine("</tbody></table></div>");
        }

        private void AppendSummarySection(TextWriter writer, IReadOnlyConfigSettings config)
        {
            writer.WriteLine($"<h2 class=\"section-heading\">{HtmlEncode("Summary")}</h2>");
            writer.WriteLine("<table class=\"stat-table\">");
            writer.WriteLine("<colgroup><col class=\"stat-col1\"><col class=\"stat-col2\"></colgroup>");
            writer.WriteLine($"  <thead><tr><th scope=\"col\" style=\"background:{TH_BG_DEFAULT}\">Category</th><th scope=\"col\" style=\"background:{TH_BG_DEFAULT}\">Count</th></tr></thead>");
            writer.WriteLine("  <tbody>");
            var stats = _fileDiffResultLists.SummaryStatistics;
            if (config.ShouldIncludeIgnoredFiles)
                writer.WriteLine($"    <tr><td class=\"stat-label\">{HtmlEncode("Ignored")}</td><td class=\"stat-value\">{stats.IgnoredCount}</td></tr>");
            writer.WriteLine($"    <tr><td class=\"stat-label\">{HtmlEncode("Unchanged")}</td><td class=\"stat-value\">{stats.UnchangedCount}</td></tr>");
            writer.WriteLine($"    <tr style=\"background:{TH_BG_ADDED}\"><td class=\"stat-label\">{HtmlEncode("Added")}</td><td class=\"stat-value\">{stats.AddedCount}</td></tr>");
            writer.WriteLine($"    <tr style=\"background:{TH_BG_REMOVED}\"><td class=\"stat-label\">{HtmlEncode("Removed")}</td><td class=\"stat-value\">{stats.RemovedCount}</td></tr>");
            writer.WriteLine($"    <tr style=\"background:{TH_BG_MODIFIED}\"><td class=\"stat-label\">{HtmlEncode("Modified")}</td><td class=\"stat-value\">{stats.ModifiedCount}</td></tr>");
            writer.WriteLine($"    <tr><td class=\"stat-label\">{HtmlEncode("Compared")}</td><td class=\"stat-value\">{_fileDiffResultLists.OldFilesAbsolutePath.Count} ({HtmlEncode("Old")}) vs {_fileDiffResultLists.NewFilesAbsolutePath.Count} ({HtmlEncode("New")})</td></tr>");
            writer.WriteLine("  </tbody>");
            writer.WriteLine("</table>");
        }

        private static void AppendILCacheStatsSection(TextWriter writer, ILCache ilCache)
        {
            var stats = ilCache.GetReportStats();
            writer.WriteLine($"<h2 class=\"section-heading\">{HtmlEncode("IL Cache Stats")}</h2>");
            writer.WriteLine("<table class=\"stat-table\">");
            writer.WriteLine("<colgroup><col class=\"stat-col1\"><col class=\"stat-col2\"></colgroup>");
            writer.WriteLine($"  <thead><tr><th scope=\"col\" style=\"background:{TH_BG_DEFAULT}\">Metric</th><th scope=\"col\" style=\"background:{TH_BG_DEFAULT}\">Value</th></tr></thead>");
            writer.WriteLine("  <tbody>");
            writer.WriteLine($"    <tr><td class=\"stat-label\">Hits</td><td class=\"stat-value\">{stats.Hits}</td></tr>");
            writer.WriteLine($"    <tr><td class=\"stat-label\">Misses</td><td class=\"stat-value\">{stats.Misses}</td></tr>");
            writer.WriteLine($"    <tr><td class=\"stat-label\">Hit Rate</td><td class=\"stat-value\">{stats.HitRatePct:F1}%</td></tr>");
            writer.WriteLine($"    <tr><td class=\"stat-label\">Stores</td><td class=\"stat-value\">{stats.Stores}</td></tr>");
            writer.WriteLine($"    <tr><td class=\"stat-label\">Evicted</td><td class=\"stat-value\">{stats.Evicted}</td></tr>");
            writer.WriteLine($"    <tr><td class=\"stat-label\">Expired</td><td class=\"stat-value\">{stats.Expired}</td></tr>");
            writer.WriteLine("  </tbody>");
            writer.WriteLine("</table>");
        }

        private void AppendWarningsSection(
            TextWriter writer,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            IReadOnlyConfigSettings config,
            ILCache? ilCache)
        {
            bool hasSha256 = _fileDiffResultLists.HasAnySha256Mismatch;
            bool hasTs  = _fileDiffResultLists.HasAnyNewFileTimestampOlderThanOldWarning;
            bool hasILFilter = _fileDiffResultLists.HasAnyILFilterWarning;
            if (!hasSha256 && !hasTs && !hasILFilter) return;

            writer.WriteLine($"<h2 class=\"section-heading\">{HtmlEncode("Warnings")}</h2>");

            // IL filter string validation warnings
            // IL フィルタ文字列検証警告
            if (hasILFilter)
            {
                var filterWarnings = _fileDiffResultLists.ILFilterWarnings.OrderBy(w => w, StringComparer.Ordinal).ToList();
                writer.WriteLine($"<h2 style=\"color:#e65100\">[ ! ] {HtmlEncode("IL filter validation warnings")} ({filterWarnings.Count})</h2>");
                writer.WriteLine("<ul class=\"warnings\">");
                foreach (var w in filterWarnings)
                {
                    writer.WriteLine($"  <li>{HtmlEncode(w)}</li>");
                }
                writer.WriteLine("</ul>");
            }

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
                    writer.WriteLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ ! ] {HtmlEncode("Modified Files")} &#x2014; {HtmlEncode("SHA256Mismatch: binary diff only — not a .NET assembly and not a recognized text file")} ({sha256Files.Count})</h2>");
                    AppendTableStart(writer, TH_BG_MODIFIED, "Diff Reason", "sha256w");
                    writer.WriteLine("<tbody>");
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
                        _fileDiffResultLists.FileRelativePathToSdkVersionDictionary.TryGetValue(kv.Key, out var sdk);
                        AppendFileRow(writer, "sha256w", idx, kv.Key, ts, col6, importance: imp, importanceLevels: impLevels, sdk: sdk ?? "");
                        idx++;
                    }
                    writer.WriteLine("</tbody></table></div>");
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
                writer.WriteLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ ! ] {HtmlEncode("Modified Files")} &#x2014; {HtmlEncode("new file timestamps older than old")} ({warnings.Count})</h2>");
                AppendTableStart(writer, TH_BG_MODIFIED, "Diff Reason", "tsw");
                writer.WriteLine("<tbody>");
                int idx = 0;
                foreach (var w in warnings)
                {
                    string ts = $"{HtmlEncode(w.OldTimestamp)}{TIMESTAMP_ARROW}{HtmlEncode(w.NewTimestamp)}";
                    _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(w.FileRelativePath, out var diffDetail);
                    _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(w.FileRelativePath, out var asm);
                    _fileDiffResultLists.FileRelativePathToSdkVersionDictionary.TryGetValue(w.FileRelativePath, out var sdkVer);
                    string col6 = BuildDiffDetailDisplay(diffDetail);
                    string imp = GetImportanceLabel(w.FileRelativePath);
                    string impLevels = GetImportanceLevelsLabel(w.FileRelativePath);
                    string tag = GetChangeTagDisplay(w.FileRelativePath);
                    AppendFileRow(writer, "tsw", idx, w.FileRelativePath, ts, col6, asm ?? "", imp, impLevels, tag, sdkVer ?? "");

                    if (config.EnableInlineDiff &&
                        (diffDetail == FileDiffResultLists.DiffDetailResult.TextMismatch ||
                         diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch))
                    {
                        AppendInlineDiffRow(writer, idx, w.FileRelativePath, oldFolderAbsolutePath, newFolderAbsolutePath,
                            reportsFolderAbsolutePath, config, diffDetail, asm ?? "", ilCache, sectionPrefix: "tsw");
                    }

                    idx++;
                }
                writer.WriteLine("</tbody></table></div>");
            }
        }
    }
}
