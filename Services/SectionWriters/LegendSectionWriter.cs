using System.IO;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    // Legend section writer extracted from ReportGenerateService.SectionWriters.cs.
    // ReportGenerateService.SectionWriters.cs から抽出した凡例セクションライタ。
    public sealed partial class ReportGenerateService
    {
        private const string REPORT_CHANGE_TAG_LEGEND_HEADER = "### Legend — Estimated Change";

        /// <summary>Writes the Legend section for diff-detail labels. / 判定根拠ラベルの凡例を書き込みます。</summary>
        private sealed class LegendSectionWriter : IReportSectionWriter
        {
            public int Order => 200;

            public bool IsEnabled(ReportWriteContext context) => true;

            public void Write(StreamWriter writer, ReportWriteContext ctx)
            {
                writer.WriteLine(REPORT_LEGEND_HEADER);
                writer.WriteLine();
                writer.WriteLine("| Label | Description |");
                writer.WriteLine("|-------|-------------|");
                writer.WriteLine($"| `{FileDiffResultLists.DiffDetailResult.SHA256Match}` / `{FileDiffResultLists.DiffDetailResult.SHA256Mismatch}` | Byte-for-byte match / mismatch (SHA256) |");
                writer.WriteLine($"| `{FileDiffResultLists.DiffDetailResult.ILMatch}` / `{FileDiffResultLists.DiffDetailResult.ILMismatch}` | IL(Intermediate Language) match / mismatch |");
                writer.WriteLine($"| `{FileDiffResultLists.DiffDetailResult.TextMatch}` / `{FileDiffResultLists.DiffDetailResult.TextMismatch}` | Text-based match / mismatch |");
                writer.WriteLine();
                writer.WriteLine(REPORT_IMPORTANCE_LEGEND_HEADER);
                writer.WriteLine();
                writer.WriteLine("| Label | Description |");
                writer.WriteLine("|-------|-------------|");
                writer.WriteLine($"| `High` | Breaking change candidate: public/protected API removal, access narrowing, return-type / parameter / member-type change |");
                writer.WriteLine($"| `Medium` | Notable change: public/protected member addition, modifier change, access widening, internal removal |");
                writer.WriteLine($"| `Low` | Low-impact change: body-only modification, internal/private member addition |");
                writer.WriteLine();
                writer.WriteLine(REPORT_CHANGE_TAG_LEGEND_HEADER);
                writer.WriteLine();
                writer.WriteLine("| Label | Description |");
                writer.WriteLine("|-------|-------------|");
                foreach (var (tag, label) in ChangeTagClassifier.AllLabels)
                {
                    writer.WriteLine($"| `{label}` | {GetChangeTagDescriptionForMarkdown(tag)} |");
                }
            }

            private static string GetChangeTagDescriptionForMarkdown(ChangeTag tag)
                => tag switch
                {
                    ChangeTag.MethodAdd => "New method added",
                    ChangeTag.MethodRemove => "Method removed",
                    ChangeTag.TypeAdd => "New type added",
                    ChangeTag.TypeRemove => "Type removed",
                    ChangeTag.Extract => "Possible method body extraction to a new private/internal method",
                    ChangeTag.Inline => "Possible private/internal method inlining into another method",
                    ChangeTag.Move => "Possible method move between types",
                    ChangeTag.Rename => "Possible method rename (same signature and IL body)",
                    ChangeTag.Signature => "Method/property signature changed",
                    ChangeTag.Access => "Access modifier changed",
                    ChangeTag.BodyEdit => "Method body IL changed only",
                    ChangeTag.DepUpdate => "Dependency package version changed only",
                    _ => ""
                };
        }
    }
}
