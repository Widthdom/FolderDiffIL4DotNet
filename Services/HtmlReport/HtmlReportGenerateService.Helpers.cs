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

        private static void AppendTableStart(StringBuilder sb, string headerBgColor, string col6Header,
            string hideClasses = "")
        {
            string bg = headerBgColor ?? TH_BG_DEFAULT;
            string tableCls = string.IsNullOrEmpty(hideClasses) ? "" : $" class=\"{hideClasses}\"";
            sb.AppendLine("<div class=\"table-scroll\">");
            sb.AppendLine($"<table{tableCls}>");
            sb.AppendLine("<colgroup>");
            sb.AppendLine("  <col class=\"col-no-g\">");
            sb.AppendLine("  <col class=\"col-cb-g\">");
            sb.AppendLine("  <col class=\"col-reason-g\">");
            sb.AppendLine("  <col class=\"col-notes-g\">");
            sb.AppendLine("  <col class=\"col-path-g\">");
            sb.AppendLine("  <col class=\"col-ts-g\">");
            sb.AppendLine("  <col class=\"col-diff-g\">");
            sb.AppendLine("  <col class=\"col-disasm-g\">");
            sb.AppendLine("</colgroup>");
            sb.AppendLine($"<thead><tr style=\"background:{bg}\">");
            sb.AppendLine($"  <th class=\"col-no\">#</th>");
            sb.AppendLine($"  <th class=\"col-cb\">&#x2713;</th>");
            sb.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--col-reason-w\">{I18n("Justification", "判定根拠")}</th>");
            sb.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--col-notes-w\">{I18n("Notes", "備考")}</th>");
            sb.AppendLine($"  <th class=\"th-resizable\" data-col-var=\"--col-path-w\">{I18n("File Path", "ファイルパス")}</th>");
            sb.AppendLine($"  <th>{I18n("Timestamp", "タイムスタンプ")}</th>");
            sb.AppendLine($"  <th class=\"col-diff-hd\">{I18n(col6Header, GetCol6HeaderJa(col6Header))}</th>");
            sb.AppendLine($"  <th class=\"col-disasm-hd th-resizable\" data-col-var=\"--col-disasm-w\">{I18n("Disassembler", "逆アセンブラ")}</th>");
            sb.AppendLine("</tr></thead>");
        }

        private static void AppendFileRow(
            StringBuilder sb,
            string sectionPrefix,
            int idx,
            string path,
            string timestamp,
            string col6,
            string disasm = "",
            string importance = "")
        {
            string cbId     = $"cb_{sectionPrefix}_{idx}";
            string reasonId = $"reason_{sectionPrefix}_{idx}";
            string notesId  = $"notes_{sectionPrefix}_{idx}";
            int recordNo    = idx + 1;
            sb.AppendLine("<tr>");
            sb.AppendLine($"  <td class=\"col-no\">{recordNo}</td>");
            sb.AppendLine($"  <td class=\"col-cb\"><input type=\"checkbox\" id=\"{cbId}\"></td>");
            sb.AppendLine($"  <td class=\"col-reason\"><input type=\"text\" id=\"{reasonId}\"></td>");
            sb.AppendLine($"  <td class=\"col-notes\"><input type=\"text\" id=\"{notesId}\"></td>");
            sb.AppendLine($"  <td class=\"col-path\"><div class=\"path-wrap\"><span class=\"path-text\">{HtmlEncode(path)}</span><button class=\"btn-copy-path\" onclick=\"copyPath(this)\" title=\"Copy\"><svg width=\"12\" height=\"12\" viewBox=\"0 0 16 16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\"><rect x=\"5.5\" y=\"5.5\" width=\"9\" height=\"9\" rx=\"1.5\"/><path d=\"M5 10.5H2.5A1.5 1.5 0 011 9V2.5A1.5 1.5 0 012.5 1H9A1.5 1.5 0 0110.5 2.5V5\"/></svg></button></div></td>");
            sb.AppendLine($"  <td class=\"col-ts\">{HtmlEncode(timestamp)}</td>");
            string col6Cell = string.IsNullOrEmpty(col6) ? "" : $"<code>{HtmlEncode(col6)}</code>";
            if (!string.IsNullOrEmpty(importance))
                col6Cell += $" <code>{HtmlEncode(importance)}</code>";
            sb.AppendLine($"  <td class=\"col-diff\">{col6Cell}</td>");
            string disasmCell = string.IsNullOrEmpty(disasm) ? "" : $"<code>{HtmlEncode(disasm)}</code>";
            sb.AppendLine($"  <td class=\"col-disasm\">{disasmCell}</td>");
            sb.AppendLine("</tr>");
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
        private static string ImportanceToStyle(ChangeImportance importance)
            => importance switch
            {
                ChangeImportance.High => "color:#d1242f;font-weight:bold",
                ChangeImportance.Medium => "color:#d97706;font-weight:bold",
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
        /// Appends the Disassembler Availability table to the HTML report header.
        /// HTML レポートヘッダに逆アセンブラ利用可否テーブルを追加します。
        /// </summary>
        private static void AppendDisassemblerAvailabilityTable(StringBuilder sb, IReadOnlyList<DisassemblerProbeResult>? probeResults)
        {
            if (probeResults == null || probeResults.Count == 0)
            {
                return;
            }
            sb.AppendLine($"  <li>{I18n("Disassembler Availability", "逆アセンブラ利用可否")}:");
            sb.AppendLine("    <table class=\"legend-table\">");
            sb.AppendLine($"      <thead><tr><th style=\"background:{TH_BG_DEFAULT}\">Tool</th><th style=\"background:{TH_BG_DEFAULT}\">Available</th><th style=\"background:{TH_BG_DEFAULT}\">Version</th></tr></thead>");
            sb.AppendLine("      <tbody>");
            foreach (var probe in probeResults)
            {
                var available = probe.Available
                    ? "<span style=\"color:#22863a\">Yes</span>"
                    : "<span style=\"color:#b31d28\">No</span>";
                var version = probe.Available && !string.IsNullOrWhiteSpace(probe.Version)
                    ? HtmlEncode(probe.Version)
                    : "N/A";
                sb.AppendLine($"        <tr><td>{HtmlEncode(probe.ToolName)}</td><td style=\"text-align:center\">{available}</td><td>{version}</td></tr>");
            }
            sb.AppendLine("      </tbody>");
            sb.AppendLine("    </table>");
            sb.AppendLine("  </li>");
        }

        // ── Utilities ────────────────────────────────────────────────────────

        internal static string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return System.Net.WebUtility.HtmlEncode(text);
        }

        internal static string I18n(string en, string ja)
            => HtmlEncode(en);

        private static string GetCol6HeaderJa(string col6Header)
            => col6Header switch
            {
                "Location" => "場所",
                "Diff Reason" => "差分理由",
                _ => col6Header
            };
    }
}
