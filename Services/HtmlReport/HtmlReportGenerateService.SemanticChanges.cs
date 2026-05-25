using System.IO;
using System.Text;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Assembly-semantic-change detail row builders for <see cref="HtmlReportGenerateService"/>.
    /// <see cref="HtmlReportGenerateService"/> のアセンブリセマンティック変更詳細行ビルダーです。
    /// </summary>
    public sealed partial class HtmlReportGenerateService
    {
        private void AppendAssemblySemanticChangesRow(
            TextWriter writer,
            int idx,
            string assemblyPath,
            AssemblySemanticChangesSummary summary,
            IReadOnlyConfigSettings config,
            string sectionPrefix = "mod")
        {
            int recordNo = idx + 1;
            string detailsId = $"semantic_{sectionPrefix}_{idx}";
            string highSuffix = summary.HighImportanceCount > 0
                ? $" ({summary.HighImportanceCount} High)"
                : string.Empty;
            string deltaSuffix = BuildChangeDeltaSuffix(summary);
            string summaryLabel = $"      <summary class=\"diff-summary\">#{recordNo} {HtmlEncode("Show assembly semantic changes")}{highSuffix}{deltaSuffix}</summary>";
            string contentHtml = BuildAssemblySemanticChangesContentHtml(summary, sectionPrefix, idx);
            WriteDetailRowWithOptionalLazyContent(writer, detailsId, summaryLabel, contentHtml, config.InlineDiffLazyRender);
        }

        private string BuildAssemblySemanticChangesContentHtml(
            AssemblySemanticChangesSummary summary,
            string sectionPrefix,
            int idx)
        {
            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine("<div class=\"semantic-changes\">");
            if (summary.Entries.Count > 0)
            {
                contentBuilder.AppendLine($"<p class=\"sc-caveat\">{HtmlEncode("Note: The semantic summary is supplementary information. Always verify the final details in the inline IL diff below.")}</p>");
                AppendAssemblySemanticChangesTable(contentBuilder, summary, sectionPrefix, idx);
            }
            else
            {
                contentBuilder.AppendLine($"<p>{HtmlEncode("No structural changes detected. See IL diff for implementation-level differences.")}</p>");
            }

            contentBuilder.AppendLine("</div>");
            return contentBuilder.ToString();
        }

        private void AppendAssemblySemanticChangesTable(
            StringBuilder contentBuilder,
            AssemblySemanticChangesSummary summary,
            string sectionPrefix,
            int idx)
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
            AppendAssemblySemanticChangesTableHeader(contentBuilder);
            AppendAssemblySemanticChangeRows(contentBuilder, summary, sectionPrefix, idx);
            contentBuilder.AppendLine("</table>");
        }

        private void AppendAssemblySemanticChangesTableHeader(StringBuilder contentBuilder)
        {
            contentBuilder.AppendLine("<thead><tr>");
            contentBuilder.AppendLine($"  <th scope=\"col\" class=\"sc-col-cb\"><input type=\"checkbox\" class=\"cb-all-detail\" onchange=\"toggleAllInDetailTable(this)\" aria-label=\"{HtmlEncode("Toggle all checkboxes")}\"></th>");
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
        }

        private void AppendAssemblySemanticChangeRows(
            StringBuilder contentBuilder,
            AssemblySemanticChangesSummary summary,
            string sectionPrefix,
            int idx)
        {
            string prevType = string.Empty;
            int scRowIdx = 0;
            foreach (var e in summary.EntriesByImportance)
            {
                bool isContinuation = e.TypeName == prevType;
                prevType = e.TypeName;
                string rowOpen = BuildAssemblySemanticRowOpen(e, isContinuation);
                string rowHtml = BuildAssemblySemanticRowHtml(e, rowOpen, isContinuation, sectionPrefix, idx, scRowIdx);
                contentBuilder.AppendLine(rowHtml);
                scRowIdx++;
            }

            contentBuilder.AppendLine("</tbody>");
        }

        private string BuildAssemblySemanticRowOpen(MemberChangeEntry e, bool isContinuation)
        {
            string importanceAttr = $" data-sc-importance=\"{ImportanceToMarker(e.Importance)}\"";
            string groupAttrs = $" data-sc-typename=\"{HtmlEncode(e.TypeName)}\" data-sc-basetype=\"{HtmlEncode(e.BaseType)}\"";
            return isContinuation
                ? $"<tr class=\"group-cont\"{importanceAttr}{groupAttrs}>"
                : $"<tr{importanceAttr}{groupAttrs}>";
        }

        private string BuildAssemblySemanticRowHtml(
            MemberChangeEntry e,
            string rowOpen,
            bool isContinuation,
            string sectionPrefix,
            int idx,
            int rowIndex)
        {
            string classTd = isContinuation ? string.Empty : HtmlEncode(e.TypeName);
            string baseTypeTd = isContinuation ? string.Empty : HtmlEncode(e.BaseType);
            string accessTd = CodeWrapArrow(e.Access);
            string modifiersTd = CodeWrapArrow(e.Modifiers);
            string bodyTd = e.Body.Length > 0 ? $"<code>{HtmlEncode(e.Body)}</code>" : string.Empty;
            string cbId = $"sc_{sectionPrefix}_{idx}_{rowIndex}";
            string changeMarker = ChangeToMarker(e.Change);
            string statusCls = ChangeToStatusClass(e.Change);
            string statusAttr = statusCls.Length > 0 ? $" class=\"{statusCls}\"" : string.Empty;
            string importanceMarker = ImportanceToMarker(e.Importance);
            string importanceCls = ImportanceToClass(e.Importance);
            string importanceAttr = importanceCls.Length > 0 ? $" class=\"{importanceCls}\"" : string.Empty;
            return $"{rowOpen}<td class=\"sc-col-cb\"><input type=\"checkbox\" id=\"{cbId}\" aria-label=\"{HtmlEncode("Reviewed")} #{rowIndex + 1}\"></td><td>{classTd}</td><td>{baseTypeTd}</td><td{statusAttr}>{changeMarker}</td><td{importanceAttr}>{importanceMarker}</td><td><code>{HtmlEncode(e.MemberKind)}</code></td><td>{accessTd}</td><td>{modifiersTd}</td><td>{HtmlEncode(e.MemberType)}</td><td>{HtmlEncode(e.MemberName)}</td><td>{HtmlEncode(e.ReturnType)}</td><td>{HtmlEncode(e.Parameters)}</td><td>{bodyTd}</td></tr>";
        }

        private static string ChangeToMarker(string change)
            => change switch { "Added" => "[ + ]", "Removed" => "[ - ]", "Modified" => "[ * ]", _ => change };

        private static string ChangeToStatusClass(string change)
            => change switch { "Added" => "sc-status-added", "Removed" => "sc-status-removed", "Modified" => "sc-status-modified", _ => string.Empty };

        /// <summary>
        /// Builds the change delta suffix for the semantic changes summary label.
        /// Returns HTML like " (<span class="color-added">+2 methods</span>, <span class="color-removed">-1 type</span>)".
        /// セマンティック変更サマリーラベルの変更差分サフィックスを構築します。
        /// </summary>
        private static string BuildChangeDeltaSuffix(AssemblySemanticChangesSummary summary)
        {
            var parts = summary.GetChangeDeltaParts();
            if (parts.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append(" (");
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                var (prefix, count, kindLabel) = parts[i];
                string cssClass = prefix switch { "+" => "color-added", "-" => "color-removed", _ => string.Empty };
                string text = $"{prefix}{count} {kindLabel}";
                if (cssClass.Length > 0)
                {
                    sb.Append($"<span class=\"{cssClass}\">{HtmlEncode(text)}</span>");
                }
                else
                {
                    sb.Append(HtmlEncode(text));
                }
            }

            sb.Append(')');
            return sb.ToString();
        }
    }
}
