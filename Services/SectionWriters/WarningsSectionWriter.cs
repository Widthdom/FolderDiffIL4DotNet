using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    // Warnings section writer extracted from ReportGenerateService.SectionWriters.cs.
    // ReportGenerateService.SectionWriters.cs から抽出した Warnings セクションライタ。
    public sealed partial class ReportGenerateService
    {
        /// <summary>
        /// Writes the Warnings section. Each warning bullet is immediately followed by its detail table.
        /// 警告セクションを書き込みます。各警告メッセージの直下に対応する詳細テーブルを配置します。
        /// </summary>
        private sealed class WarningsSectionWriter : IReportSectionWriter
        {
            public int Order => 1000;

            public bool IsEnabled(ReportWriteContext context)
                => context.HasSha256Mismatch
                || context.HasTimestampRegressionWarning
                || context.HasILFilterWarnings
                || LoadReviewChecklistItems(context).Count > 0;

            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                bool wroteWarnings = false;

                // SHA256Mismatch warning + detail table (grouped together)
                // SHA256Mismatch 警告 + 詳細テーブル（まとめて配置）
                if (ctx.HasSha256Mismatch)
                {
                    if (!wroteWarnings)
                    {
                        writer.WriteLine(REPORT_SECTION_WARNINGS);
                        wroteWarnings = true;
                    }

                    var sha256Files = ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary
                        .Where(kv => kv.Value == FileDiffResultLists.DiffDetailResult.SHA256Mismatch)
                        .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (sha256Files.Count > 0)
                    {
                        writer.WriteLine($"### [ ! ] {REPORT_LABEL_MODIFIED}{REPORT_SECTION_FILES_SUFFIX} — SHA256Mismatch: binary diff only — not a .NET assembly and not a recognized text file ({sha256Files.Count})");
                        writer.WriteLine();
                        writer.WriteLine("| Status | File Path | Timestamp | Diff Reason | Estimated Change | Disassembler | .NET SDK |");
                        writer.WriteLine("|:------:|-----------|:---------:|:-----------:|:----------------:|--------------|:--------:|");
                        foreach (var kv in sha256Files)
                        {
                            string tsCol = "";
                            if (ctx.Config.ShouldOutputFileTimestamps)
                            {
                                string oldTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.OldFolderAbsolutePath, kv.Key));
                                string newTs = Caching.TimestampCache.GetOrAdd(Path.Combine(ctx.NewFolderAbsolutePath, kv.Key));
                                tsCol = $"{oldTs}{REPORT_TIMESTAMP_ARROW}{newTs}";
                            }
                            var sdkDisplay = BuildSdkVersionDisplay(kv.Key, ctx.FileDiffResultLists);
                            writer.WriteLine($"| `{REPORT_MARKER_MODIFIED}` | {kv.Key} | {tsCol} | `{kv.Value}` | | | {sdkDisplay} |");
                        }
                    }
                }

                // IL filter string validation warnings
                // IL フィルタ文字列検証警告
                if (ctx.HasILFilterWarnings)
                {
                    if (!wroteWarnings)
                    {
                        writer.WriteLine(REPORT_SECTION_WARNINGS);
                        wroteWarnings = true;
                    }

                    writer.WriteLine();
                    var filterWarnings = ctx.FileDiffResultLists.ILFilterWarnings.OrderBy(w => w, StringComparer.Ordinal).ToList();
                    writer.WriteLine($"### [ ! ] IL filter validation warnings ({filterWarnings.Count})");
                    writer.WriteLine();
                    foreach (var warning in filterWarnings)
                    {
                        writer.WriteLine($"- {warning}");
                    }
                }

                // Timestamp-regressed warning + detail table (grouped together)
                // タイムスタンプ逆行 警告 + 詳細テーブル（まとめて配置）
                if (ctx.HasTimestampRegressionWarning)
                {
                    if (!wroteWarnings)
                    {
                        writer.WriteLine(REPORT_SECTION_WARNINGS);
                        wroteWarnings = true;
                    }

                    writer.WriteLine();
                    var tsWarnings = ctx.FileDiffResultLists.NewFileTimestampOlderThanOldWarnings.Values
                        .OrderBy(entry => ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary.TryGetValue(entry.FileRelativePath, out var d) ? GetModifiedSortOrder(d) : 3)
                        .ThenBy(entry => GetImportanceSortOrder(ctx.FileDiffResultLists.GetMaxImportance(entry.FileRelativePath)))
                        .ThenBy(entry => entry.FileRelativePath, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    writer.WriteLine($"### [ ! ] {REPORT_LABEL_MODIFIED}{REPORT_SECTION_FILES_SUFFIX} — new file timestamps older than old ({tsWarnings.Count})");
                    writer.WriteLine();
                    writer.WriteLine("| Status | File Path | Timestamp | Diff Reason | Estimated Change | Disassembler | .NET SDK |");
                    writer.WriteLine("|:------:|-----------|:---------:|:-----------:|:----------------:|--------------|:--------:|");
                    foreach (var warning in tsWarnings)
                    {
                        var fileRelativePath = warning.FileRelativePath;
                        var diffDetail = ctx.FileDiffResultLists.FileRelativePathToDiffDetailDictionary[fileRelativePath];
                        var diffDetailDisplay = BuildDiffDetailDisplay(fileRelativePath, diffDetail, ctx.FileDiffResultLists);
                        var disasmDisplay = BuildDisassemblerDisplay(fileRelativePath, diffDetail, ctx.FileDiffResultLists);
                        var tagDisplay = BuildChangeTagDisplay(fileRelativePath, ctx.FileDiffResultLists);
                        var sdkDisplay = BuildSdkVersionDisplay(fileRelativePath, ctx.FileDiffResultLists);
                        string tsCol = $"{warning.OldTimestamp}{REPORT_TIMESTAMP_ARROW}{warning.NewTimestamp}";
                        writer.WriteLine($"| `{REPORT_MARKER_MODIFIED}` | {fileRelativePath} | {tsCol} | {diffDetailDisplay} | {tagDisplay} | {disasmDisplay} | {sdkDisplay} |");
                    }
                }

                var checklistItems = LoadReviewChecklistItems(ctx);
                if (checklistItems.Count == 0)
                {
                    return;
                }

                writer.WriteLine();
                writer.WriteLine("## Review Checklist");
                writer.WriteLine();
                writer.WriteLine("| ✓ | Checklist Item | Notes |");
                writer.WriteLine("|:-:|----------------|-------|");
                foreach (var item in checklistItems)
                {
                    writer.WriteLine($"| ☐ | {FormatChecklistMarkdownCell(item)} | |");
                }
            }

            private static IReadOnlyList<string> LoadReviewChecklistItems(ReportWriteContext ctx)
            {
                string checklistFilePath;

                try
                {
                    Directory.CreateDirectory(AppDataPaths.GetDefaultHtmlReportDirectoryAbsolutePath());
                    checklistFilePath = AppDataPaths.GetDefaultHtmlReportChecklistFileAbsolutePath();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    ctx.Logger.LogMessage(
                        AppLogLevel.Warning,
                        $"Markdown review checklist directory could not be prepared and will be skipped: {ex.GetType().Name}: {ex.Message}",
                        shouldOutputMessageToConsole: false,
                        ex);
                    return Array.Empty<string>();
                }

                if (!File.Exists(checklistFilePath))
                {
                    return Array.Empty<string>();
                }

                try
                {
                    string json = File.ReadAllText(checklistFilePath);
                    var items = JsonSerializer.Deserialize<List<string>>(json);
                    if (items == null)
                    {
                        return Array.Empty<string>();
                    }

                    return items
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Select(NormalizeReviewChecklistItem)
                        .Where(item => item.Length > 0)
                        .ToList();
                }
                catch (JsonException ex)
                {
                    ctx.Logger.LogMessage(
                        AppLogLevel.Warning,
                        $"Markdown review checklist file '{checklistFilePath}' is invalid JSON and will be skipped: {ex.Message}",
                        shouldOutputMessageToConsole: false,
                        ex);
                    return Array.Empty<string>();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    ctx.Logger.LogMessage(
                        AppLogLevel.Warning,
                        $"Markdown review checklist file '{checklistFilePath}' could not be read and will be skipped: {ex.GetType().Name}: {ex.Message}",
                        shouldOutputMessageToConsole: false,
                        ex);
                    return Array.Empty<string>();
                }
            }

            private static string NormalizeReviewChecklistItem(string item)
                => item.Replace("\r\n", "\n", StringComparison.Ordinal)
                       .Replace('\r', '\n')
                       .Trim();

            private static string FormatChecklistMarkdownCell(string item)
                => string.Join(
                    "<br>",
                    item.Split('\n')
                        .Select(static line => WebUtility.HtmlEncode(line).Replace("|", "\\|", StringComparison.Ordinal)));
        }
    }
}
