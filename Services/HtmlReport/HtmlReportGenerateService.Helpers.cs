using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FolderDiffIL4DotNet.Core.Text;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    // Table helpers and utility methods.
    // テーブルヘルパーおよびユーティリティメソッド群。
    public sealed partial class HtmlReportGenerateService
    {
        // ── Table helpers ────────────────────────────────────────────────────

        private static void AppendTableStart(TextWriter writer, string headerBgColor, string col6Header,
            string hideClasses = "")
        {
            string bg = headerBgColor ?? TH_BG_DEFAULT;
            string tableCls = string.IsNullOrEmpty(hideClasses) ? "" : $" class=\"{hideClasses}\"";
            writer.WriteLine("<div class=\"table-scroll\">");
            writer.WriteLine($"<table{tableCls}>");
            writer.WriteLine("<colgroup>");
            writer.WriteLine("  <col class=\"col-no-g\">");
            writer.WriteLine("  <col class=\"col-cb-g\">");
            writer.WriteLine("  <col class=\"col-reason-g\">");
            writer.WriteLine("  <col class=\"col-notes-g\">");
            writer.WriteLine("  <col class=\"col-status-g\">");
            writer.WriteLine("  <col class=\"col-path-g\">");
            writer.WriteLine("  <col class=\"col-ts-g\">");
            writer.WriteLine("  <col class=\"col-diff-g\">");
            writer.WriteLine("  <col class=\"col-tag-g\">");
            writer.WriteLine("  <col class=\"col-disasm-g\">");
            writer.WriteLine("</colgroup>");
            writer.WriteLine($"<thead><tr style=\"background:{bg}\">");
            writer.WriteLine($"  <th scope=\"col\" class=\"col-no\">#</th>");
            writer.WriteLine($"  <th scope=\"col\" class=\"col-cb\">&#x2713;</th>");
            writer.WriteLine($"  <th scope=\"col\" class=\"th-resizable\" data-col-var=\"--col-reason-w\">{HtmlEncode("Justification")}</th>");
            writer.WriteLine($"  <th scope=\"col\" class=\"th-resizable\" data-col-var=\"--col-notes-w\">{HtmlEncode("Notes")}</th>");
            writer.WriteLine($"  <th scope=\"col\" class=\"col-status\">{HtmlEncode("Status")}</th>");
            writer.WriteLine($"  <th scope=\"col\" class=\"th-resizable\" data-col-var=\"--col-path-w\">{HtmlEncode("File Path")}</th>");
            writer.WriteLine($"  <th scope=\"col\">{HtmlEncode("Timestamp")}</th>");
            writer.WriteLine($"  <th scope=\"col\" class=\"col-diff-hd\">{HtmlEncode(col6Header)}</th>");
            writer.WriteLine($"  <th scope=\"col\" class=\"col-tag-hd th-resizable\" data-col-var=\"--col-tag-w\">{HtmlEncode("Estimated Change")}</th>");
            writer.WriteLine($"  <th scope=\"col\" class=\"col-disasm-hd th-resizable\" data-col-var=\"--col-disasm-w\">{HtmlEncode("Disassembler")}</th>");
            writer.WriteLine("</tr></thead>");
        }

        private static void AppendFileRow(
            TextWriter writer,
            string sectionPrefix,
            int idx,
            string path,
            string timestamp,
            string col6,
            string disasm = "",
            string importance = "",
            string importanceLevels = "",
            string changeTag = "")
        {
            string cbId     = $"cb_{sectionPrefix}_{idx}";
            string reasonId = $"reason_{sectionPrefix}_{idx}";
            string notesId  = $"notes_{sectionPrefix}_{idx}";
            int recordNo    = idx + 1;
            string impAttr = string.IsNullOrEmpty(importance) ? "" : $" data-importance=\"{HtmlEncode(importance)}\"";
            // All distinct importance levels for filtering (comma-separated)
            // フィルタリング用の全重要度レベル（カンマ区切り）
            string impsAttr = string.IsNullOrEmpty(importanceLevels) ? "" : $" data-importances=\"{HtmlEncode(importanceLevels)}\"";
            // Normalize diff detail to category for filtering / フィルタリング用に diff detail をカテゴリに正規化
            string diffCat = NormalizeDiffCategory(col6);
            string diffAttr = string.IsNullOrEmpty(diffCat) ? "" : $" data-diff=\"{diffCat}\"";
            writer.WriteLine($"<tr data-section=\"{sectionPrefix}\"{impAttr}{impsAttr}{diffAttr}>");
            writer.WriteLine($"  <td class=\"col-no\">{recordNo}</td>");
            writer.WriteLine($"  <td class=\"col-cb\"><input type=\"checkbox\" id=\"{cbId}\" aria-label=\"{HtmlEncode("Reviewed")} #{recordNo}\"></td>");
            writer.WriteLine($"  <td class=\"col-reason\"><input type=\"text\" id=\"{reasonId}\" aria-label=\"{HtmlEncode("Justification")} #{recordNo}\"></td>");
            writer.WriteLine($"  <td class=\"col-notes\"><input type=\"text\" id=\"{notesId}\" aria-label=\"{HtmlEncode("Notes")} #{recordNo}\"></td>");
            // Status column — marker based on section / Status列 — セクションに基づくマーカー
            string statusMarker = sectionPrefix switch
            {
                "ign" => "[ x ]",
                "unch" => "[ = ]",
                "add" => "[ + ]",
                "rem" => "[ - ]",
                "mod" or "sha256w" or "tsw" => "[ * ]",
                _ => ""
            };
            writer.WriteLine($"  <td class=\"col-status\">{HtmlEncode(statusMarker)}</td>");
            writer.WriteLine($"  <td class=\"col-path\"><div class=\"path-wrap\"><span class=\"path-text\">{HtmlEncode(path)}</span><button class=\"btn-copy-path\" onclick=\"copyPath(this)\" title=\"Copy\" aria-label=\"{HtmlEncode("Copy file path")}\"><svg aria-hidden=\"true\" width=\"12\" height=\"12\" viewBox=\"0 0 16 16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\"><rect x=\"5.5\" y=\"5.5\" width=\"9\" height=\"9\" rx=\"1.5\"/><path d=\"M5 10.5H2.5A1.5 1.5 0 011 9V2.5A1.5 1.5 0 012.5 1H9A1.5 1.5 0 0110.5 2.5V5\"/></svg></button></div></td>");
            writer.WriteLine($"  <td class=\"col-ts\">{HtmlEncode(timestamp)}</td>");
            string col6Cell = string.IsNullOrEmpty(col6) ? "" : $"<code>{HtmlEncode(col6)}</code>";
            if (!string.IsNullOrEmpty(importance))
                col6Cell += $" <code>{HtmlEncode(importance)}</code>";
            else if (sectionPrefix == "mod" && diffCat == "ILMismatch")
                col6Cell += " <code>Unknown</code>";
            writer.WriteLine($"  <td class=\"col-diff\">{col6Cell}</td>");
            string tagCell = FormatChangeTagHtml(changeTag);
            writer.WriteLine($"  <td class=\"col-tag\">{tagCell}</td>");
            string disasmCell = string.IsNullOrEmpty(disasm) ? "" : $"<code>{HtmlEncode(disasm)}</code>";
            writer.WriteLine($"  <td class=\"col-disasm\">{disasmCell}</td>");
            writer.WriteLine("</tr>");
        }

        private static string BuildDiffViewHtml(IReadOnlyList<TextDiffer.DiffLine> diffLines)
        {
            var dsb = new StringBuilder();
            dsb.AppendLine("      <div class=\"diff-view\">");
            dsb.AppendLine("        <table class=\"diff-table\">");
            dsb.AppendLine("          <tbody>");

            foreach (var line in diffLines)
            {
                switch (line.Kind)
                {
                    case TextDiffer.HunkHeader:
                        dsb.AppendLine("            <tr class=\"diff-hunk-tr\">");
                        dsb.AppendLine("              <td class=\"diff-ln\"></td>");
                        dsb.AppendLine("              <td class=\"diff-ln\"></td>");
                        dsb.AppendLine($"              <td class=\"diff-hunk-td\">{HtmlEncode(line.Text)}</td>");
                        dsb.AppendLine("            </tr>");
                        break;
                    case TextDiffer.Removed:
                        dsb.AppendLine("            <tr class=\"diff-del-tr\">");
                        dsb.AppendLine($"              <td class=\"diff-ln diff-old-ln\">{line.OldLineNo}</td>");
                        dsb.AppendLine("              <td class=\"diff-ln diff-new-ln\"></td>");
                        dsb.AppendLine($"              <td class=\"diff-del-td\">-{HtmlEncode(line.Text)}</td>");
                        dsb.AppendLine("            </tr>");
                        break;
                    case TextDiffer.Added:
                        dsb.AppendLine("            <tr class=\"diff-add-tr\">");
                        dsb.AppendLine("              <td class=\"diff-ln diff-old-ln\"></td>");
                        dsb.AppendLine($"              <td class=\"diff-ln diff-new-ln\">{line.NewLineNo}</td>");
                        dsb.AppendLine($"              <td class=\"diff-add-td\">+{HtmlEncode(line.Text)}</td>");
                        dsb.AppendLine("            </tr>");
                        break;
                    case TextDiffer.Context:
                        dsb.AppendLine("            <tr class=\"diff-ctx-tr\">");
                        dsb.AppendLine($"              <td class=\"diff-ln diff-old-ln\">{line.OldLineNo}</td>");
                        dsb.AppendLine($"              <td class=\"diff-ln diff-new-ln\">{line.NewLineNo}</td>");
                        dsb.AppendLine($"              <td class=\"diff-ctx-td\"> {HtmlEncode(line.Text)}</td>");
                        dsb.AppendLine("            </tr>");
                        break;
                    case TextDiffer.Truncated:
                        dsb.AppendLine("            <tr class=\"diff-trunc-tr\">");
                        dsb.AppendLine("              <td class=\"diff-ln\"></td>");
                        dsb.AppendLine("              <td class=\"diff-ln\"></td>");
                        dsb.AppendLine($"              <td class=\"diff-trunc-td\">{HtmlEncode(line.Text)}</td>");
                        dsb.AppendLine("            </tr>");
                        break;
                }
            }

            dsb.AppendLine("          </tbody>");
            dsb.AppendLine("        </table>");
            dsb.AppendLine("      </div>");
            return dsb.ToString();
        }

        private static string BuildIgnoredTimestamp(
            string relPath,
            bool hasOld, bool hasNew,
            string oldFolder, string newFolder,
            bool shouldOutput)
        {
            if (!shouldOutput) return "";
            if (hasOld && hasNew)
            {
                string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolder, relPath));
                string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(newFolder, relPath));
                return $"{oldTs}{TIMESTAMP_ARROW}{newTs}";
            }
            if (hasOld) return Caching.TimestampCache.GetOrAdd(Path.Combine(oldFolder, relPath));
            if (hasNew) return Caching.TimestampCache.GetOrAdd(Path.Combine(newFolder, relPath));
            return "";
        }

        private string BuildDisassemblerHeaderText()
        {
            var labels = _fileDiffResultLists.DisassemblerToolVersions.Keys
                .Concat(_fileDiffResultLists.DisassemblerToolVersionsFromCache.Keys)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return labels.Count == 0 ? "N/A" : string.Join(", ", labels);
        }

        private static string BuildDiffDetailDisplay(
            FileDiffResultLists.DiffDetailResult diffDetail)
        {
            return diffDetail.ToString();
        }

        /// <summary>
        /// Returns the file-level max importance label, or empty if unavailable.
        /// ファイルレベルの最大重要度ラベルを返します（存在しない場合は空文字列）。
        /// </summary>
        private string GetImportanceLabel(string fileRelativePath)
        {
            var importance = _fileDiffResultLists.GetMaxImportance(fileRelativePath);
            return importance != null ? ImportanceToLabel(importance.Value) : "";
        }

        /// <summary>
        /// Returns comma-separated importance level labels for filtering, or empty if none exist.
        /// フィルタリング用のカンマ区切り重要度レベルラベルを返します（存在しない場合は空文字列）。
        /// </summary>
        private string GetImportanceLevelsLabel(string fileRelativePath)
        {
            var levels = _fileDiffResultLists.GetAllImportanceLevels(fileRelativePath);
            if (levels.Count == 0) return "";
            return string.Join(",", levels.OrderDescending().Select(ImportanceToLabel));
        }

        /// <summary>
        /// Returns a short display label for a <see cref="ChangeImportance"/> value.
        /// <see cref="ChangeImportance"/> 値の短い表示ラベルを返します。
        /// </summary>
        private static string ImportanceToLabel(ChangeImportance importance)
            => importance switch
            {
                ChangeImportance.High => "High",
                ChangeImportance.Medium => "Medium",
                ChangeImportance.Low => "Low",
                _ => ""
            };

        /// <summary>
        /// Returns the formatted change tag display string for a file, or empty if no tags.
        /// ファイルの変更タグ表示文字列を返します（タグなしの場合は空文字列）。
        /// </summary>
        private string GetChangeTagDisplay(string fileRelativePath)
        {
            if (_fileDiffResultLists.FileRelativePathToChangeTags.TryGetValue(fileRelativePath, out var tags))
                return ChangeTagClassifier.FormatTags(tags);
            return "";
        }

        /// <summary>
        /// Formats change tags as HTML with each tag individually wrapped in <c>&lt;code&gt;</c>.
        /// 変更タグを個別に <c>&lt;code&gt;</c> で囲んだ HTML を返します。
        /// </summary>
        private static string FormatChangeTagHtml(string changeTag)
        {
            if (string.IsNullOrEmpty(changeTag)) return "";
            var parts = changeTag.Split(new[] { ", " }, StringSplitOptions.None);
            return string.Join(", ", parts.Select(p => $"<code>{HtmlEncode(p)}</code>"));
        }

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
                _ => 3 // null / no semantic changes
            };

        /// <summary>
        /// Returns the inline style for an importance level (text color + bold, no background).
        /// 重要度レベルのインラインスタイル（文字色＋太字、背景なし）を返します。
        /// </summary>
        private static string ImportanceToClass(ChangeImportance importance)
            => importance switch
            {
                ChangeImportance.High => "imp-high",
                ChangeImportance.Medium => "imp-medium",
                _ => ""
            };

        /// <summary>
        /// Returns the display marker for an importance level.
        /// 重要度レベルの表示マーカーを返します。
        /// </summary>
        private static string ImportanceToMarker(ChangeImportance importance)
            => importance switch
            {
                ChangeImportance.High => "High",
                ChangeImportance.Medium => "Medium",
                ChangeImportance.Low => "Low",
                _ => ""
            };

        /// <summary>
        /// Returns the diff detail value for use as a data-diff attribute (e.g. "SHA256Match", "ILMismatch").
        /// data-diff 属性に使用する diff detail 値を返します。
        /// </summary>
        private static string NormalizeDiffCategory(string col6)
        {
            if (string.IsNullOrEmpty(col6)) return "";
            // Return the exact value if it's a known diff detail / 既知の diff detail ならそのまま返す
            return col6 switch
            {
                "SHA256Match" or "SHA256Mismatch" or "ILMatch" or "ILMismatch" or "TextMatch" or "TextMismatch" => col6,
                _ => ""
            };
        }

        /// <summary>
        /// Wraps a value in <c>&lt;code&gt;</c> tags. If the value contains " → ", each side is wrapped individually.
        /// 値を &lt;code&gt; タグで囲みます。" → " を含む場合は両側を個別に囲みます。
        /// </summary>
        private static string CodeWrapArrow(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            int idx = raw.IndexOf(" → ", StringComparison.Ordinal);
            if (idx >= 0)
                return $"<code>{HtmlEncode(raw[..idx])}</code> → <code>{HtmlEncode(raw[(idx + 3)..])}</code>";
            return $"<code>{HtmlEncode(raw)}</code>";
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

        private static List<string> GetNormalizedIlIgnoreStrings(IReadOnlyConfigSettings config)
        {
            if (config?.ILIgnoreLineContainingStrings == null) return new List<string>();
            return config.ILIgnoreLineContainingStrings
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Appends the Disassembler Availability section as a standalone rounded block.
        /// 逆アセンブラ利用可否を独立した角丸セクションとして追加します。
        /// </summary>
        private static void AppendDisassemblerAvailabilitySection(TextWriter writer, IReadOnlyList<DisassemblerProbeResult>? probeResults, string inUseHeaderText)
        {
            if (probeResults == null || probeResults.Count == 0)
            {
                return;
            }
            writer.WriteLine("<div class=\"header-path\">");
            writer.WriteLine($"  <div class=\"header-path-label\">{HtmlEncode("Disassembler Availability")}</div>");
            writer.WriteLine("  <div class=\"header-path-value\">");
            foreach (var probe in probeResults)
            {
                // Check if this tool is the one actually used / このツールが実際に使用されたかチェック
                bool isInUse = !string.IsNullOrWhiteSpace(inUseHeaderText)
                    && inUseHeaderText.IndexOf(probe.ToolName, StringComparison.OrdinalIgnoreCase) >= 0;
                var status = probe.Available
                    ? (string.IsNullOrWhiteSpace(probe.Version)
                        ? "<span class=\"status-available\">Available</span>"
                        : $"<span class=\"status-available\">Available ({HtmlEncode(probe.Version)})</span>")
                    : "<span class=\"status-unavailable\">Not Available</span>";
                var inUseLabel = isInUse ? " — In Use" : "";
                writer.WriteLine($"    <div>{HtmlEncode(probe.ToolName)}: {status}{inUseLabel}</div>");
            }
            writer.WriteLine("  </div>");
            writer.WriteLine("</div>");
        }

        /// <summary>
        /// Appends warning banners for disassembler issues (no tool available, mixed tool usage).
        /// 逆アセンブラの問題に関する警告バナーを追加（ツール未検出、複数ツール混在）。
        /// </summary>
        private void AppendDisassemblerWarnings(TextWriter writer)
        {
            // Warning: no disassembler available / 警告: 逆アセンブラが利用不可
            var probeResults = _fileDiffResultLists.DisassemblerAvailability;
            if (probeResults != null && probeResults.Count > 0 && !probeResults.Any(p => p.Available))
            {
                writer.WriteLine("<div class=\"header-path warn-danger\">");
                writer.WriteLine($"  <div class=\"header-path-label\">{HtmlEncode("⚠ Warning")}</div>");
                writer.WriteLine($"  <div class=\"header-path-value\">{HtmlEncode("No disassembler tool is available. .NET assembly comparison will fail if any .dll/.exe files with differing SHA256 hashes are detected. Install dotnet-ildasm or ilspycmd to enable IL-level comparison.")}</div>");
                writer.WriteLine("</div>");
            }

            // Warning: multiple different disassembler tools used / 警告: 異なる逆アセンブラツールが混在
            var allLabels = _fileDiffResultLists.DisassemblerToolVersions.Keys
                .Concat(_fileDiffResultLists.DisassemblerToolVersionsFromCache.Keys)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var distinctToolNames = allLabels
                .Select(label =>
                {
                    var vIdx = label.IndexOf(" (version:", StringComparison.OrdinalIgnoreCase);
                    return vIdx >= 0 ? label.Substring(0, vIdx).Trim() : label.Trim();
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctToolNames.Count > 1)
            {
                writer.WriteLine("<div class=\"header-path warn-caution\">");
                writer.WriteLine($"  <div class=\"header-path-label\">{HtmlEncode("⚠ Warning")}</div>");
                writer.WriteLine($"  <div class=\"header-path-value\">{HtmlEncode($"Multiple disassembler tools were used across file comparisons in this run ({string.Join(", ", allLabels)}). Each file pair is compared using the same tool, but IL output format may differ between tools, reducing cross-file consistency. Consider clearing the IL cache to ensure a single tool is used for all comparisons.")}</div>");
                writer.WriteLine("</div>");
            }
        }

        /// <summary>Appends a filter table row with checkbox, label, and description. / チェックボックス、ラベル、説明を含むフィルターテーブル行を追加します。</summary>
        private static void AppendFilterTableRow(TextWriter writer, string id, string labelHtml, string description)
        {
            writer.WriteLine($"<tr><td class=\"ft-cb\"><input type=\"checkbox\" id=\"{id}\" checked onchange=\"applyFilters()\"></td><td class=\"ft-label\"><label for=\"{id}\">{labelHtml}</label></td><td class=\"ft-desc\">{description}</td></tr>");
        }

        /// <summary>
        /// Appends filter table cells (cb + label + desc) without <c>tr</c> wrapping, for 2-column layouts.
        /// 2段組みレイアウト用に、<c>tr</c> なしでフィルターテーブルセル（cb + label + desc）を追加します。
        /// </summary>
        private static void AppendFilterTableCells(TextWriter writer, (string Id, string Display, string Description) entry)
        {
            writer.Write($"<td class=\"ft-cb\"><input type=\"checkbox\" id=\"{entry.Id}\" checked onchange=\"applyFilters()\"></td><td class=\"ft-label\"><label for=\"{entry.Id}\">{entry.Display}</label></td><td class=\"ft-desc\">{HtmlEncode(entry.Description)}</td>");
        }

        /// <summary>
        /// Returns a short description for a <see cref="ChangeTag"/> value, used in the legend table.
        /// <see cref="ChangeTag"/> 値の短い説明を返します（凡例テーブル用）。
        /// </summary>
        private static string GetChangeTagDescription(ChangeTag tag)
            => tag switch
            {
                ChangeTag.MethodAdd => "New method added",
                ChangeTag.MethodRemove => "Method removed",
                ChangeTag.TypeAdd => "New type added",
                ChangeTag.TypeRemove => "Type removed",
                ChangeTag.Extract => "Method body extracted to new private/internal method",
                ChangeTag.Inline => "Private/internal method inlined into another method",
                ChangeTag.Move => "Method moved between types",
                ChangeTag.Rename => "Method renamed (same signature and IL body)",
                ChangeTag.Signature => "Method/property signature changed",
                ChangeTag.Access => "Access modifier changed",
                ChangeTag.BodyEdit => "Method body IL changed only",
                ChangeTag.DepUpdate => "Dependency package version changed only",
                _ => ""
            };

        // ── Utilities ────────────────────────────────────────────────────────

        internal static string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            // WebUtility.HtmlEncode does not encode backticks; encode them explicitly
            // to prevent template-literal injection in embedded JavaScript contexts.
            // WebUtility.HtmlEncode はバッククォートをエンコードしないため、
            // 埋め込み JavaScript コンテキストでのテンプレートリテラル注入を防ぐために明示的にエンコードする。
            return System.Net.WebUtility.HtmlEncode(text).Replace("`", "&#96;");
        }

    }
}
