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
            sb.AppendLine($"<h1>{I18n("Folder Diff Report", "フォルダ差分レポート")}</h1>");
            sb.AppendLine("<ul class=\"meta\">");
            sb.AppendLine($"  <li>{I18n("App Version", "アプリバージョン")}: FolderDiffIL4DotNet {HtmlEncode(appVersion)}</li>");
            sb.AppendLine($"  <li>{I18n("Computer", "コンピュータ")}: {HtmlEncode(computerName)}</li>");
            sb.AppendLine($"  <li>{I18n("Old", "旧")}: {HtmlEncode(oldFolderAbsolutePath)}</li>");
            sb.AppendLine($"  <li>{I18n("New", "新")}: {HtmlEncode(newFolderAbsolutePath)}</li>");
            sb.AppendLine($"  <li>{I18n("Ignored Extensions", "除外拡張子")}: {HtmlEncode(string.Join(", ", config.IgnoredExtensions))}</li>");
            sb.AppendLine($"  <li>{I18n("Text File Extensions", "テキストファイル拡張子")}: {HtmlEncode(string.Join(", ", config.TextFileExtensions))}</li>");
            sb.AppendLine($"  <li>{I18n("IL Disassembler", "IL 逆アセンブラ")}: {HtmlEncode(BuildDisassemblerHeaderText())}</li>");
            if (!string.IsNullOrWhiteSpace(elapsedTimeString))
                sb.AppendLine($"  <li>{I18n("Elapsed Time", "処理時間")}: {HtmlEncode(elapsedTimeString)}</li>");
            if (config.ShouldOutputFileTimestamps)
                sb.AppendLine($"  <li>{I18n("Timestamps (timezone)", "タイムスタンプ (タイムゾーン)")}: {HtmlEncode(DateTimeOffset.Now.ToString("zzz"))}</li>");

            // MVID note (same style as other meta items)
            sb.AppendLine($"  <li>{I18n("Note", "注")}: {I18n("When diffing IL, lines starting with", "IL 差分比較時、")} <code>{HtmlEncode(Constants.IL_MVID_LINE_PREFIX)}</code> {I18n("(if present) are ignored because they contain disassembler-emitted Module Version ID metadata that can change on rebuild without meaning the executable IL changed.", "（存在する場合）で始まる行は、リビルド時に変更されうる逆アセンブラ出力のモジュールバージョン ID メタデータを含むため無視されます。")}</li>");

            // IL contains-ignore note
            if (config.ShouldIgnoreILLinesContainingConfiguredStrings)
            {
                var ilIgnoreStrings = GetNormalizedIlIgnoreStrings(config);
                if (ilIgnoreStrings.Count == 0)
                {
                    sb.AppendLine($"  <li>{I18n("Note", "注")}: {I18n("IL line-ignore-by-contains is enabled, but no non-empty strings are configured.", "IL 行の含有文字列無視が有効ですが、空でない文字列が設定されていません。")}</li>");
                }
                else
                {
                    sb.AppendLine($"  <li>{I18n("Note", "注")}: {I18n("When diffing IL, lines containing any of the configured strings are ignored:", "IL 差分比較時、設定された文字列を含む行は無視されます:")}");
                    sb.AppendLine("    <div class=\"il-ignore-scroll\"><table class=\"legend-table il-ignore-table\">");
                    sb.AppendLine("      <tbody>");
                    foreach (var s in ilIgnoreStrings)
                    {
                        sb.AppendLine($"        <tr><td>{HtmlEncode($"\"{s}\"")}</td></tr>");
                    }
                    sb.AppendLine("      </tbody>");
                    sb.AppendLine("    </table></div>");
                    sb.AppendLine("  </li>");
                }
            }

            // Myers Diff Algorithm reference
            sb.AppendLine($"  <li>{I18n("Note", "注")}: {I18n("Inline diffs for ILMismatch and TextMismatch are computed using the", "ILMismatch および TextMismatch のインライン差分は以下のアルゴリズムで計算されます:")} " +
                "<a href=\"http://www.xmailserver.org/diff2.pdf\">" +
                "Myers Diff Algorithm (E.&nbsp;W.&nbsp;Myers, &ldquo;An O(ND) Difference Algorithm and Its Variations,&rdquo; <i>Algorithmica</i> <b>1</b>(2), 1986)</a>.</li>");

            // Legend (as compact table)
            sb.AppendLine($"  <li>{I18n("Legend", "凡例")}:");
            sb.AppendLine("    <table class=\"legend-table\">");
            sb.AppendLine("      <tbody>");
            sb.AppendLine($"        <tr><td><code>SHA256Match</code> / <code>SHA256Mismatch</code></td><td>{I18n("SHA256 hash match / mismatch", "SHA256 ハッシュ 一致 / 不一致")}</td></tr>");
            sb.AppendLine($"        <tr><td><code>ILMatch</code> / <code>ILMismatch</code></td><td>{I18n("IL(Intermediate Language) match / mismatch", "IL（中間言語）一致 / 不一致")}</td></tr>");
            sb.AppendLine($"        <tr><td><code>TextMatch</code> / <code>TextMismatch</code></td><td>{I18n("Text match / mismatch", "テキスト 一致 / 不一致")}</td></tr>");
            sb.AppendLine("      </tbody>");
            sb.AppendLine("    </table>");
            sb.AppendLine("  </li>");
            sb.AppendLine("</ul>");
        }

        private void AppendIgnoredSection(
            StringBuilder sb,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            IReadOnlyConfigSettings config)
        {
            var items = _fileDiffResultLists.IgnoredFilesRelativePathToLocation
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2>[ x ] {I18n("Ignored Files", "除外ファイル")} ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine($"<p class=\"empty\">{I18n("(none)", "(なし)")}</p>"); return; }

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
            sb.AppendLine($"<h2>[ = ] {I18n("Unchanged Files", "変更なしファイル")} ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine($"<p class=\"empty\">{I18n("(none)", "(なし)")}</p>"); return; }

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
            sb.AppendLine($"<h2 style=\"color:{COLOR_ADDED}\">[ + ] {I18n("Added Files", "追加ファイル")} ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine($"<p class=\"empty\">{I18n("(none)", "(なし)")}</p>"); return; }

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
            sb.AppendLine($"<h2 style=\"color:{COLOR_REMOVED}\">[ - ] {I18n("Removed Files", "削除ファイル")} ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine($"<p class=\"empty\">{I18n("(none)", "(なし)")}</p>"); return; }

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
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ * ] {I18n("Modified Files", "変更ファイル")} ({items.Count})</h2>");
            if (items.Count == 0) { sb.AppendLine($"<p class=\"empty\">{I18n("(none)", "(なし)")}</p>"); return; }

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
                AppendFileRow(sb, "mod", idx, path, ts, col6, asm ?? "");

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
            string diffLabelEn = diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch ? "Show IL diff" : "Show diff";
            string diffLabelJa = diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch ? "IL 差分を表示" : "差分を表示";
            string summary = $"      <summary class=\"diff-summary\">#{recordNo} {I18n(diffLabelEn, diffLabelJa)} (<span class=\"diff-added-cnt\">+{addedCount}</span> / <span class=\"diff-removed-cnt\">-{removedCount}</span>)</summary>";
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
                contentBuilder.AppendLine($"<p class=\"sc-caveat\">{I18n("Note: The semantic summary is supplementary information. Always verify the final details in the inline IL diff below.", "注: セマンティックサマリーは補助情報です。最終確認は必ず下の IL インライン差分で行ってください。")}</p>");
            }

            if (summary.Entries.Count > 0)
            {
                contentBuilder.AppendLine("<table class=\"semantic-changes-table sc-detail\">");
                contentBuilder.AppendLine("<colgroup>");
                contentBuilder.AppendLine("  <col class=\"sc-col-cb-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-class-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-basetype-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-change-g\">");
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
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-class-w\">{I18n("Class", "クラス")}</th>");
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-basetype-w\">{I18n("BaseType", "基底型")}</th>");
                contentBuilder.AppendLine($"  <th>{I18n("Status", "状態")}</th><th>{I18n("Kind", "種別")}</th><th>{I18n("Access", "アクセス")}</th><th>{I18n("Modifiers", "修飾子")}</th>");
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-type-w\">{I18n("Type", "型")}</th>");
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-name-w\">{I18n("Name", "名前")}</th>");
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-rettype-w\">{I18n("ReturnType", "戻り値型")}</th>");
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-params-w\">{I18n("Parameters", "パラメータ")}</th>");
                contentBuilder.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-body-w\">{I18n("Body", "本体")}</th>");
                contentBuilder.AppendLine("</tr></thead>");
                contentBuilder.AppendLine("<tbody>");
                string prevType = "";
                int scRowIdx = 0;
                foreach (var e in summary.Entries)
                {
                    bool isCont = e.TypeName == prevType;
                    string classTd = !isCont ? HtmlEncode(e.TypeName) : "";
                    string baseTypeTd = !isCont ? HtmlEncode(e.BaseType) : "";
                    prevType = e.TypeName;
                    string trOpen = isCont ? "<tr class=\"group-cont\">" : "<tr>";
                    string accessTd = HtmlEncode(e.Access);
                    string modifiersTd = HtmlEncode(e.Modifiers);
                    string bodyTd = e.Body.Length > 0 ? $"<code>{HtmlEncode(e.Body)}</code>" : "";
                    string cbId = $"sc_{sectionPrefix}_{idx}_{scRowIdx}";
                    string changeMarker = ChangeToMarker(e.Change);
                    string statusBg = ChangeToStatusBg(e.Change);
                    string statusStyle = statusBg.Length > 0 ? $" style=\"background:{statusBg}\"" : "";
                    contentBuilder.AppendLine($"{trOpen}<td class=\"sc-col-cb\"><input type=\"checkbox\" id=\"{cbId}\"></td><td>{classTd}</td><td>{baseTypeTd}</td><td{statusStyle}>{changeMarker}</td><td><code>{HtmlEncode(e.MemberKind)}</code></td><td>{accessTd}</td><td>{modifiersTd}</td><td>{HtmlEncode(e.MemberType)}</td><td>{HtmlEncode(e.MemberName)}</td><td>{HtmlEncode(e.ReturnType)}</td><td>{HtmlEncode(e.Parameters)}</td><td>{bodyTd}</td></tr>");
                    scRowIdx++;
                }
                contentBuilder.AppendLine("</tbody></table>");
            }
            else
            {
                contentBuilder.AppendLine($"<p>{I18n("No structural changes detected. See IL diff for implementation-level differences.", "構造的な変更は検出されませんでした。実装レベルの差異については IL 差分を参照してください。")}</p>");
            }

            if (summary.Entries.Count > 0)
                AppendSummaryCountTable(contentBuilder, summary);
            contentBuilder.AppendLine("</div>");

            string detailsId = $"semantic_{sectionPrefix}_{idx}";
            string summaryLabel = $"      <summary class=\"diff-summary\">#{recordNo} {I18n("Show assembly semantic changes", "アセンブリ意味変更を表示")}</summary>";
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

        private static void AppendSummaryCountTable(StringBuilder sb, AssemblySemanticChangesSummary summary)
        {
            var counts = new Dictionary<(string TypeName, string Change), int>();
            foreach (var e in summary.Entries)
            {
                var key = (e.TypeName, e.Change);
                counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
            }

            sb.AppendLine("<table class=\"semantic-changes-table sc-count\">");
            sb.AppendLine("<colgroup>");
            sb.AppendLine("  <col class=\"sc-cnt-class-g\">");
            sb.AppendLine("  <col class=\"sc-cnt-change-g\">");
            sb.AppendLine("  <col class=\"sc-cnt-count-g\">");
            sb.AppendLine("</colgroup>");
            sb.AppendLine("<thead><tr>");
            sb.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--sc-cnt-class-w\">{I18n("Class", "クラス")}</th>");
            sb.AppendLine($"  <th>{I18n("Status", "状態")}</th><th>{I18n("Count", "件数")}</th>");
            sb.AppendLine("</tr></thead>");
            sb.AppendLine("<tbody>");
            string prevType = "";
            foreach (var ((typeName, change), count) in counts.OrderBy(kv => kv.Key.TypeName, StringComparer.Ordinal).ThenBy(kv => ChangeOrder(kv.Key.Change)))
            {
                bool isCont = typeName == prevType;
                string classTd = !isCont ? HtmlEncode(typeName) : "";
                prevType = typeName;
                string trOpen = isCont ? "<tr class=\"group-cont\">" : "<tr>";
                string cntStatusBg = ChangeToStatusBg(change);
                string cntStatusStyle = cntStatusBg.Length > 0 ? $" style=\"background:{cntStatusBg}\"" : "";
                sb.AppendLine($"{trOpen}<td>{classTd}</td><td{cntStatusStyle}>{ChangeToMarker(change)}</td><td>{count}</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        private static int ChangeOrder(string change)
            => change switch { "Added" => 0, "Removed" => 1, "Modified" => 2, _ => 3 };

        private static string ChangeToMarker(string change)
            => change switch { "Added" => "[ + ]", "Removed" => "[ - ]", "Modified" => "[ * ]", _ => change };

        private static string ChangeToStatusBg(string change)
            => change switch { "Added" => TH_BG_ADDED, "Removed" => TH_BG_REMOVED, "Modified" => TH_BG_MODIFIED, _ => "" };

        private void AppendSummarySection(StringBuilder sb, IReadOnlyConfigSettings config)
        {
            sb.AppendLine($"<h2 class=\"section-heading\">{I18n("Summary", "サマリー")}</h2>");
            sb.AppendLine("<table class=\"stat-table\">");
            sb.AppendLine("  <tbody>");
            var stats = _fileDiffResultLists.SummaryStatistics;
            if (config.ShouldIncludeIgnoredFiles)
                sb.AppendLine($"    <tr><td class=\"stat-label\">{I18n("Ignored", "除外")}</td><td class=\"stat-value\">{stats.IgnoredCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">{I18n("Unchanged", "変更なし")}</td><td class=\"stat-value\">{stats.UnchangedCount}</td></tr>");
            sb.AppendLine($"    <tr style=\"background:{TH_BG_ADDED}\"><td class=\"stat-label\">{I18n("Added", "追加")}</td><td class=\"stat-value\">{stats.AddedCount}</td></tr>");
            sb.AppendLine($"    <tr style=\"background:{TH_BG_REMOVED}\"><td class=\"stat-label\">{I18n("Removed", "削除")}</td><td class=\"stat-value\">{stats.RemovedCount}</td></tr>");
            sb.AppendLine($"    <tr style=\"background:{TH_BG_MODIFIED}\"><td class=\"stat-label\">{I18n("Modified", "変更")}</td><td class=\"stat-value\">{stats.ModifiedCount}</td></tr>");
            sb.AppendLine($"    <tr><td class=\"stat-label\">{I18n("Compared", "比較")}</td><td class=\"stat-value\">{_fileDiffResultLists.OldFilesAbsolutePath.Count} ({I18n("Old", "旧")}) vs {_fileDiffResultLists.NewFilesAbsolutePath.Count} ({I18n("New", "新")})</td></tr>");
            sb.AppendLine("  </tbody>");
            sb.AppendLine("</table>");
        }

        private static void AppendILCacheStatsSection(StringBuilder sb, ILCache ilCache)
        {
            var stats = ilCache.GetReportStats();
            sb.AppendLine($"<h2 class=\"section-heading\">{I18n("IL Cache Stats", "IL キャッシュ統計")}</h2>");
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
            IReadOnlyConfigSettings config,
            ILCache? ilCache)
        {
            bool hasSha256 = _fileDiffResultLists.HasAnySha256Mismatch;
            bool hasTs  = _fileDiffResultLists.HasAnyNewFileTimestampOlderThanOldWarning;
            if (!hasSha256 && !hasTs) return;

            sb.AppendLine($"<h2 class=\"section-heading\"><span class=\"warn-icon\">&#x26A0;</span> {I18n("Warnings", "警告")}</h2>");
            sb.AppendLine("<ul class=\"warnings\">");
            if (hasSha256)
                sb.AppendLine($"  <li>{I18n("One or more files were classified as SHA256Mismatch. Manual review is recommended because only an SHA256 hash comparison was possible.", "1つ以上のファイルが SHA256Mismatch として分類されました。SHA256 ハッシュ比較のみが可能だったため、手動レビューを推奨します。")}</li>");
            if (hasTs)
                sb.AppendLine($"  <li>{I18n("One or more modified files in new have older last-modified timestamps than the corresponding files in old.", "new 内の1つ以上の変更ファイルが、old 内の対応するファイルより古い更新日時を持っています。")}</li>");
            sb.AppendLine("</ul>");

            // SHA256Mismatch files table (same style as Modified Files)
            if (hasSha256)
            {
                var sha256Files = _fileDiffResultLists.FileRelativePathToDiffDetailDictionary
                    .Where(kv => kv.Value == FileDiffResultLists.DiffDetailResult.SHA256Mismatch)
                    .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (sha256Files.Count > 0)
                {
                    sb.AppendLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ ! ] {I18n("Modified Files", "変更ファイル")} &#x2014; {I18n("SHA256Mismatch (Manual Review Recommended)", "SHA256Mismatch（手動レビュー推奨）")} ({sha256Files.Count})</h2>");
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
                        AppendFileRow(sb, "sha256w", idx, kv.Key, ts, col6);
                        idx++;
                    }
                    sb.AppendLine("</tbody></table></div>");
                }
            }

            // Timestamp-regressed files table (same style as Modified Files)
            if (hasTs)
            {
                var warnings = _fileDiffResultLists.NewFileTimestampOlderThanOldWarnings.Values
                    .OrderBy(w => _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(w.FileRelativePath, out var d) ? GetModifiedSortOrder(d) : 3)
                    .ThenBy(w => w.FileRelativePath, StringComparer.OrdinalIgnoreCase).ToList();
                sb.AppendLine($"<h2 style=\"color:{COLOR_MODIFIED}\">[ ! ] {I18n("Modified Files", "変更ファイル")} &#x2014; {I18n("Timestamps Regressed", "タイムスタンプ逆行")} ({warnings.Count})</h2>");
                AppendTableStart(sb, TH_BG_MODIFIED, "Diff Reason", hideClasses: "hide-disasm");
                sb.AppendLine("<tbody>");
                int idx = 0;
                foreach (var w in warnings)
                {
                    string ts = $"{HtmlEncode(w.OldTimestamp)}{TIMESTAMP_ARROW}{HtmlEncode(w.NewTimestamp)}";
                    _fileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(w.FileRelativePath, out var diffDetail);
                    _fileDiffResultLists.FileRelativePathToIlDisassemblerLabelDictionary.TryGetValue(w.FileRelativePath, out var asm);
                    string col6 = BuildDiffDetailDisplay(diffDetail);
                    AppendFileRow(sb, "tsw", idx, w.FileRelativePath, ts, col6);

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
