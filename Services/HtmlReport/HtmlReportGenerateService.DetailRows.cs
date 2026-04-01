using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Core.Common;
using FolderDiffIL4DotNet.Core.Text;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services.Caching;

namespace FolderDiffIL4DotNet.Services
{
    // Detail row builders for inline diffs, assembly semantic changes, and dependency changes.
    // インライン差分、アセンブリセマンティック変更、依存関係変更の詳細行ビルダー。
    public sealed partial class HtmlReportGenerateService
    {
        private void AppendInlineDiffRow(
            TextWriter writer,
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

            var (oldLines, newLines) = ReadInlineDiffSourceLines(relPath, oldFolderAbsolutePath, newFolderAbsolutePath, reportsFolderAbsolutePath, diffDetail);
            if (oldLines == null || newLines == null) return;

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
                writer.WriteLine("<tr class=\"diff-row\">");
                writer.WriteLine($"  <td colspan=\"10\"><p class=\"diff-skipped\">#{recordNo} {encoded}</p></td>");
                writer.WriteLine("</tr>");
                return;
            }

            if (diffLines.Count > maxDiffLines)
            {
                writer.WriteLine("<tr class=\"diff-row\">");
                writer.WriteLine($"  <td colspan=\"10\"><p class=\"diff-skipped\">#{recordNo} Inline diff skipped: diff too large " +
                    $"({diffLines.Count} diff lines; limit is {maxDiffLines}). " +
                    $"Increase <code>InlineDiffMaxDiffLines</code> in config to enable. (current value: <code>{maxDiffLines}</code>)</p></td>");
                writer.WriteLine("</tr>");
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

            writer.WriteLine("<tr class=\"diff-row\">");
            writer.WriteLine($"  <td colspan=\"10\">");
            if (config.InlineDiffLazyRender)
            {
                string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(diffViewHtml));
                writer.WriteLine($"    <details id=\"{HtmlEncode(detailsId)}\" data-diff-html=\"{b64}\">");
                writer.WriteLine(summary);
                writer.WriteLine("    </details>");
            }
            else
            {
                writer.WriteLine($"    <details id=\"{HtmlEncode(detailsId)}\">");
                writer.WriteLine(summary);
                writer.Write(diffViewHtml);
                writer.WriteLine("    </details>");
            }
            writer.WriteLine("  </td>");
            writer.WriteLine("</tr>");
        }

        /// <summary>
        /// Reads old/new source lines for inline diff rendering.
        /// Returns null for both if lines could not be read.
        /// インライン差分レンダリング用の新旧ソース行を読み込みます。
        /// 読み込めなかった場合は両方 null を返します。
        /// </summary>
        private (string[]? oldLines, string[]? newLines) ReadInlineDiffSourceLines(
            string relPath,
            string oldFolderAbsolutePath,
            string newFolderAbsolutePath,
            string reportsFolderAbsolutePath,
            FileDiffResultLists.DiffDetailResult diffDetail)
        {
            if (diffDetail == FileDiffResultLists.DiffDetailResult.ILMismatch)
            {
                // ILMismatch: read IL text from the *_IL.txt files written during comparison
                // IL text files are always written in UTF-8 by ILTextOutputService
                // IL テキストファイルは ILTextOutputService により常に UTF-8 で書き出される
                string ilFileName = TextSanitizer.Sanitize(relPath) + "_" + Constants.LABEL_IL + ".txt";
                string oldILPath = Path.Combine(reportsFolderAbsolutePath, Constants.LABEL_IL, "old", ilFileName);
                string newILPath = Path.Combine(reportsFolderAbsolutePath, Constants.LABEL_IL, "new", ilFileName);
                if (!File.Exists(oldILPath) || !File.Exists(newILPath)) return (null, null); // IL text not written; skip
                return (File.ReadAllLines(oldILPath, Encoding.UTF8), File.ReadAllLines(newILPath, Encoding.UTF8));
            }
            else
            {
                // TextMismatch: read from disk with encoding auto-detection
                // to correctly handle non-UTF-8 files (e.g. Shift_JIS Japanese text)
                // TextMismatch: エンコーディング自動検出でディスクから読み込み
                // 非 UTF-8 ファイル（Shift_JIS 日本語テキスト等）を正しく処理する
                try
                {
                    string oldPath = Path.Combine(oldFolderAbsolutePath, relPath);
                    string newPath = Path.Combine(newFolderAbsolutePath, relPath);
                    var oldEncoding = EncodingDetector.DetectFileEncoding(oldPath);
                    var newEncoding = EncodingDetector.DetectFileEncoding(newPath);
                    return (File.ReadAllLines(oldPath, oldEncoding), File.ReadAllLines(newPath, newEncoding));
                }
                catch (Exception ex) when (ExceptionFilters.IsFileIoRecoverable(ex))
                {
                    _logger.LogMessage(AppLogLevel.Warning,
                        $"Inline diff skipped for '{relPath}': {ex.Message}",
                        shouldOutputMessageToConsole: false, ex);
                    return (null, null);
                }
            }
        }

        private void AppendAssemblySemanticChangesRow(
            TextWriter writer,
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
                contentBuilder.AppendLine("  <th scope=\"col\" class=\"sc-col-cb\">&#x2713;</th>");
                contentBuilder.AppendLine($"  <th scope=\"col\" class=\"th-resizable\" data-col-var=\"--sc-class-w\">{HtmlEncode("Class")}</th>");
                contentBuilder.AppendLine($"  <th scope=\"col\" class=\"th-resizable\" data-col-var=\"--sc-basetype-w\">{HtmlEncode("BaseType")}</th>");
                contentBuilder.AppendLine($"  <th scope=\"col\">{HtmlEncode("Status")}</th><th scope=\"col\">{HtmlEncode("Importance")}</th><th scope=\"col\">{HtmlEncode("Kind")}</th><th scope=\"col\">{HtmlEncode("Access")}</th><th scope=\"col\">{HtmlEncode("Modifiers")}</th>");
                contentBuilder.AppendLine($"  <th scope=\"col\" class=\"th-resizable\" data-col-var=\"--sc-type-w\">{HtmlEncode("Type")}</th>");
                contentBuilder.AppendLine($"  <th scope=\"col\" class=\"th-resizable\" data-col-var=\"--sc-name-w\">{HtmlEncode("Name")}</th>");
                contentBuilder.AppendLine($"  <th scope=\"col\" class=\"th-resizable\" data-col-var=\"--sc-rettype-w\">{HtmlEncode("ReturnType")}</th>");
                contentBuilder.AppendLine($"  <th scope=\"col\" class=\"th-resizable\" data-col-var=\"--sc-params-w\">{HtmlEncode("Parameters")}</th>");
                contentBuilder.AppendLine($"  <th scope=\"col\" class=\"th-resizable\" data-col-var=\"--sc-body-w\">{HtmlEncode("Body")}</th>");
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
                    string scImpAttr = $" data-sc-importance=\"{ImportanceToMarker(e.Importance)}\"";
                    // Store typename/basetype on every row so JS can restore group headers after filtering
                    // フィルタ後にグループヘッダーを復元できるよう、全行に typename/basetype を格納
                    string scGroupAttrs = $" data-sc-typename=\"{HtmlEncode(e.TypeName)}\" data-sc-basetype=\"{HtmlEncode(e.BaseType)}\"";
                    string trOpen = isCont ? $"<tr class=\"group-cont\"{scImpAttr}{scGroupAttrs}>" : $"<tr{scImpAttr}{scGroupAttrs}>";
                    string accessTd = CodeWrapArrow(e.Access);
                    string modifiersTd = CodeWrapArrow(e.Modifiers);
                    string bodyTd = e.Body.Length > 0 ? $"<code>{HtmlEncode(e.Body)}</code>" : "";
                    string cbId = $"sc_{sectionPrefix}_{idx}_{scRowIdx}";
                    string changeMarker = ChangeToMarker(e.Change);
                    string statusCls = ChangeToStatusClass(e.Change);
                    string statusAttr = statusCls.Length > 0 ? $" class=\"{statusCls}\"" : "";
                    string impMarker = ImportanceToMarker(e.Importance);
                    string impCls = ImportanceToClass(e.Importance);
                    string impAttr = impCls.Length > 0 ? $" class=\"{impCls}\"" : "";
                    contentBuilder.AppendLine($"{trOpen}<td class=\"sc-col-cb\"><input type=\"checkbox\" id=\"{cbId}\" aria-label=\"{HtmlEncode("Reviewed")} #{scRowIdx + 1}\"></td><td>{classTd}</td><td>{baseTypeTd}</td><td{statusAttr}>{changeMarker}</td><td{impAttr}>{impMarker}</td><td><code>{HtmlEncode(e.MemberKind)}</code></td><td>{accessTd}</td><td>{modifiersTd}</td><td>{HtmlEncode(e.MemberType)}</td><td>{HtmlEncode(e.MemberName)}</td><td>{HtmlEncode(e.ReturnType)}</td><td>{HtmlEncode(e.Parameters)}</td><td>{bodyTd}</td></tr>");
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
            string deltaSuffix = BuildChangeDeltaSuffix(summary);
            string summaryLabel = $"      <summary class=\"diff-summary\">#{recordNo} {HtmlEncode("Show assembly semantic changes")}{highSuffix}{deltaSuffix}</summary>";
            string contentHtml = contentBuilder.ToString();

            writer.WriteLine("<tr class=\"diff-row\">");
            writer.WriteLine("  <td colspan=\"10\">");
            if (config.InlineDiffLazyRender)
            {
                string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(contentHtml));
                writer.WriteLine($"    <details id=\"{HtmlEncode(detailsId)}\" data-diff-html=\"{b64}\">");
                writer.WriteLine(summaryLabel);
                writer.WriteLine("    </details>");
            }
            else
            {
                writer.WriteLine($"    <details id=\"{HtmlEncode(detailsId)}\">");
                writer.WriteLine(summaryLabel);
                writer.Write(contentHtml);
                writer.WriteLine("    </details>");
            }
            writer.WriteLine("  </td>");
            writer.WriteLine("</tr>");
        }

        private void AppendDependencyChangesRow(
            TextWriter writer,
            int idx,
            DependencyChangeSummary summary,
            IReadOnlyConfigSettings config,
            string sectionPrefix = "mod")
        {
            int recordNo = idx + 1;
            bool hasAnyVuln = summary.Entries.Any(e => e.Vulnerabilities != null);
            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine("<div class=\"dependency-changes\">");

            if (summary.Entries.Count > 0)
            {
                contentBuilder.AppendLine("<table class=\"semantic-changes-table dc-detail\">");
                contentBuilder.AppendLine("<colgroup>");
                contentBuilder.AppendLine("  <col class=\"sc-col-cb-g\">");
                contentBuilder.AppendLine("  <col class=\"dc-col-package-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-change-g\">");
                contentBuilder.AppendLine("  <col class=\"sc-col-importance-g\">");
                contentBuilder.AppendLine("  <col class=\"dc-col-ver-g\">");
                contentBuilder.AppendLine("  <col class=\"dc-col-ver-g\">");
                if (hasAnyVuln)
                    contentBuilder.AppendLine("  <col class=\"dc-col-vuln-g\">");
                contentBuilder.AppendLine("</colgroup>");
                contentBuilder.AppendLine("<thead><tr>");
                contentBuilder.AppendLine($"  <th class=\"sc-col-cb\">&#x2713;</th>");
                contentBuilder.AppendLine($"  <th>{HtmlEncode("Package")}</th>");
                contentBuilder.AppendLine($"  <th>{HtmlEncode("Status")}</th>");
                contentBuilder.AppendLine($"  <th>{HtmlEncode("Importance")}</th>");
                contentBuilder.AppendLine($"  <th>{HtmlEncode("Old Version")}</th>");
                contentBuilder.AppendLine($"  <th>{HtmlEncode("New Version")}</th>");
                if (hasAnyVuln)
                    contentBuilder.AppendLine($"  <th>{HtmlEncode("Vulnerabilities")}</th>");
                contentBuilder.AppendLine("</tr></thead>");
                contentBuilder.AppendLine("<tbody>");
                int dcRowIdx = 0;
                foreach (var e in summary.EntriesByImportance)
                {
                    string dcImpAttr = $" data-sc-importance=\"{ImportanceToMarker(e.Importance)}\"";
                    string changeMarker = DependencyChangeToMarker(e.Change);
                    string statusCls = DependencyChangeToStatusClass(e.Change);
                    string statusAttr = statusCls.Length > 0 ? $" class=\"{statusCls}\"" : "";
                    string impMarker = ImportanceToMarker(e.Importance);
                    string impCls = ImportanceToClass(e.Importance);
                    string impAttr = impCls.Length > 0 ? $" class=\"{impCls}\"" : "";
                    string cbId = $"dc_{sectionPrefix}_{idx}_{dcRowIdx}";
                    string oldVer = e.OldVersion.Length > 0 ? HtmlEncode(e.OldVersion) : "&#x2014;";
                    string newVer = e.NewVersion.Length > 0 ? HtmlEncode(e.NewVersion) : "&#x2014;";
                    string vulnCell = hasAnyVuln ? $"<td>{BuildVulnerabilityCell(e.Vulnerabilities)}</td>" : "";
                    contentBuilder.AppendLine($"<tr{dcImpAttr}><td class=\"sc-col-cb\"><input type=\"checkbox\" id=\"{cbId}\"></td><td>{HtmlEncode(e.PackageName)}</td><td{statusAttr}>{changeMarker}</td><td{impAttr}>{impMarker}</td><td>{oldVer}</td><td>{newVer}</td>{vulnCell}</tr>");
                    dcRowIdx++;
                }
                contentBuilder.AppendLine("</tbody></table>");
            }
            else
            {
                contentBuilder.AppendLine($"<p>{HtmlEncode("No dependency changes detected.")}</p>");
            }

            contentBuilder.AppendLine("</div>");

            string detailsId = $"deps_{sectionPrefix}_{idx}";
            string highSuffix = summary.HighImportanceCount > 0
                ? $" ({summary.HighImportanceCount} High)"
                : "";
            string vulnSuffix = BuildVulnerabilitySummarySuffix(summary);
            string summaryLabel = $"      <summary class=\"diff-summary\">#{recordNo} {HtmlEncode("Show dependency changes")}{highSuffix}{vulnSuffix}</summary>";
            string contentHtml = contentBuilder.ToString();

            writer.WriteLine("<tr class=\"diff-row\">");
            writer.WriteLine("  <td colspan=\"10\">");
            if (config.InlineDiffLazyRender)
            {
                string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(contentHtml));
                writer.WriteLine($"    <details id=\"{HtmlEncode(detailsId)}\" data-diff-html=\"{b64}\">");
                writer.WriteLine(summaryLabel);
                writer.WriteLine("    </details>");
            }
            else
            {
                writer.WriteLine($"    <details id=\"{HtmlEncode(detailsId)}\">");
                writer.WriteLine(summaryLabel);
                writer.Write(contentHtml);
                writer.WriteLine("    </details>");
            }
            writer.WriteLine("  </td>");
            writer.WriteLine("</tr>");
        }

        private static string DependencyChangeToMarker(string change)
            => change switch { "Added" => "[ + ]", "Removed" => "[ - ]", "Updated" => "[ * ]", _ => change };

        private static string DependencyChangeToStatusClass(string change)
            => change switch { "Added" => "sc-status-added", "Removed" => "sc-status-removed", "Updated" => "sc-status-modified", _ => "" };

        private static string ChangeToMarker(string change)
            => change switch { "Added" => "[ + ]", "Removed" => "[ - ]", "Modified" => "[ * ]", _ => change };

        private static string ChangeToStatusClass(string change)
            => change switch { "Added" => "sc-status-added", "Removed" => "sc-status-removed", "Modified" => "sc-status-modified", _ => "" };

        /// <summary>
        /// Builds the HTML content for a vulnerability cell in the dependency changes table.
        /// Shows advisory links with severity badges for new-version vulns (red) and resolved old-version vulns (green strikethrough).
        /// 依存関係変更テーブルの脆弱性セルの HTML を構築します。
        /// 新バージョンの脆弱性は赤バッジ、旧バージョンで解消済みの脆弱性は緑取り消し線で表示します。
        /// </summary>
        private static string BuildVulnerabilityCell(VulnerabilityCheckResult? vuln)
        {
            if (vuln == null || !vuln.HasAnyVulnerabilities)
                return "&#x2014;";

            var sb = new StringBuilder();

            // New version vulnerabilities (active risk) — always red to signal danger
            // 新バージョンの脆弱性（現在のリスク）— 危険を示すため常に赤系
            foreach (var v in vuln.NewVersionVulnerabilities)
            {
                string sevLabel = VulnerabilityCheckResult.SeverityToLabel(v.Severity);
                string advisoryId = ExtractAdvisoryId(v.AdvisoryUrl);
                sb.Append($"<span class=\"vuln-badge {NEW_VULN_CLASS}\" title=\"{HtmlEncode(sevLabel)}: {HtmlEncode(v.AdvisoryUrl)}\">");
                if (v.AdvisoryUrl.Length > 0)
                    sb.Append($"<a href=\"{HtmlEncode(v.AdvisoryUrl)}\" target=\"_blank\" rel=\"noopener\" style=\"color:inherit;text-decoration:underline\">{HtmlEncode(advisoryId)}</a>");
                else
                    sb.Append(HtmlEncode(sevLabel));
                sb.Append($" <small>({HtmlEncode(sevLabel)})</small></span> ");
            }

            // Old version vulnerabilities that are resolved / 旧バージョンの脆弱性（解消済み）
            if (vuln.HasResolvedVulnerabilities)
            {
                foreach (var v in vuln.OldVersionVulnerabilities)
                {
                    // Skip if the same advisory also affects the new version / 新バージョンにも該当する場合はスキップ
                    bool alsoAffectsNew = false;
                    foreach (var nv in vuln.NewVersionVulnerabilities)
                    {
                        if (string.Equals(nv.AdvisoryUrl, v.AdvisoryUrl, StringComparison.Ordinal))
                        {
                            alsoAffectsNew = true;
                            break;
                        }
                    }
                    if (alsoAffectsNew) continue;

                    string sevLabel = VulnerabilityCheckResult.SeverityToLabel(v.Severity);
                    string advisoryId = ExtractAdvisoryId(v.AdvisoryUrl);
                    sb.Append($"<span class=\"vuln-badge {RESOLVED_VULN_CLASS}\" title=\"Resolved: ");
                    sb.Append(HtmlEncode(v.AdvisoryUrl));
                    sb.Append("\">");
                    if (v.AdvisoryUrl.Length > 0)
                        sb.Append($"<a href=\"{HtmlEncode(v.AdvisoryUrl)}\" target=\"_blank\" rel=\"noopener\" style=\"color:inherit\">{HtmlEncode(advisoryId)}</a>");
                    else
                        sb.Append(HtmlEncode(sevLabel));
                    sb.Append($" <small>({HtmlEncode(sevLabel)})</small></span> ");
                }
            }

            return sb.Length > 0 ? sb.ToString().TrimEnd() : "&#x2014;";
        }

        /// <summary>
        /// Builds a summary suffix for the details summary label showing vulnerability counts.
        /// 脆弱性件数を示す details サマリラベルのサフィックスを構築します。
        /// </summary>
        private static string BuildVulnerabilitySummarySuffix(DependencyChangeSummary summary)
        {
            int vulnNew = summary.VulnerableNewVersionCount;
            int resolved = summary.ResolvedVulnerabilityCount;
            if (vulnNew == 0 && resolved == 0)
                return "";

            var parts = new System.Collections.Generic.List<string>();
            if (resolved > 0)
                parts.Add($"<span class=\"vuln-resolved-count\">{resolved} resolved</span>");
            if (vulnNew > 0)
                parts.Add($"<span class=\"vuln-new-count\">{vulnNew} vuln</span>");
            return " " + string.Join(" ", parts);
        }

        /// <summary>
        /// Extracts a short advisory ID from the advisory URL for display.
        /// 表示用にアドバイザリ URL から短い ID を抽出します。
        /// </summary>
        private static string ExtractAdvisoryId(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "Advisory";
            // GitHub Advisory URLs end with "GHSA-xxxx-xxxx-xxxx"
            int lastSlash = url.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < url.Length - 1)
                return url.Substring(lastSlash + 1);
            return "Advisory";
        }

        /// <summary>
        /// Builds the change delta suffix for the semantic changes summary label.
        /// Returns HTML like " (<span class="color-added">+2 methods</span>, <span class="color-removed">-1 type</span>)".
        /// セマンティック変更サマリーラベルの変更差分サフィックスを構築します。
        /// </summary>
        private static string BuildChangeDeltaSuffix(AssemblySemanticChangesSummary summary)
        {
            var parts = summary.GetChangeDeltaParts();
            if (parts.Count == 0) return "";

            var sb = new StringBuilder();
            sb.Append(" (");
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var (prefix, count, kindLabel) = parts[i];
                string cssClass = prefix switch { "+" => "color-added", "-" => "color-removed", _ => "" };
                string text = $"{prefix}{count} {kindLabel}";
                if (cssClass.Length > 0)
                    sb.Append($"<span class=\"{cssClass}\">{HtmlEncode(text)}</span>");
                else
                    sb.Append(HtmlEncode(text));
            }
            sb.Append(')');
            return sb.ToString();
        }

        // Vulnerability style constants / 脆弱性スタイル定数
        private const string NEW_VULN_CLASS = "vuln-new";
        private const string RESOLVED_VULN_CLASS = "vuln-resolved";
    }
}
