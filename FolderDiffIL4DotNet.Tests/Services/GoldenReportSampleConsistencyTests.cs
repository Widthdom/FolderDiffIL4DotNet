using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using FolderDiffIL4DotNet.Runner;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Verifies that the checked-in report samples reflect the maintainer-managed creator profile.
    /// commit 済みレポートサンプルがメンテナー管理の creator profile を反映していることを検証します。
    /// </summary>
    [Trait("Category", "Unit")]
    public sealed class GoldenReportSampleConsistencyTests
    {
        private const string SAMPLE_ONLY_NORMALIZATION_VALUE = "buildserver1_artifact";
        private static readonly string RepositoryRoot = FindRepositoryRoot();

        [Fact]
        public void MarkdownNormalizationTable_MatchesCreatorDefaultProfile()
        {
            string markdown = File.ReadAllText(
                Path.Combine(RepositoryRoot, "doc", "samples", "diff_report.md"));

            Assert.Equal(
                GetExpectedNormalizationValues(),
                ExtractMarkdownNormalizationValues(markdown));
        }

        [Fact]
        public void HtmlNormalizationTable_MatchesCreatorDefaultProfile()
        {
            string html = File.ReadAllText(
                Path.Combine(RepositoryRoot, "doc", "samples", "diff_report.html"));

            Assert.Equal(
                GetExpectedNormalizationValues(),
                ExtractHtmlNormalizationValues(html));
        }

        private static string[] GetExpectedNormalizationValues()
        {
            IReadOnlyList<string> profileValues = CreatorPrivilegeIlIgnoreProfiles.GetStringsOrThrow(
                CreatorPrivilegeIlIgnoreProfiles.DefaultProfileName);
            var expected = new string[profileValues.Count + 1];

            for (int index = 0; index < profileValues.Count; index++)
            {
                expected[index] = profileValues[index];
            }

            // The sample appends one user-configured value to demonstrate overlap warnings.
            // sample は overlap warning の例示用に、ユーザー設定値を1件追加します。
            expected[^1] = SAMPLE_ONLY_NORMALIZATION_VALUE;
            return expected;
        }

        private static string[] ExtractMarkdownNormalizationValues(string markdown)
        {
            const string header = "| Substring to Normalize (Escaped) |";
            int headerIndex = markdown.IndexOf(header, StringComparison.Ordinal);
            Assert.True(headerIndex >= 0, "Markdown normalization table header was not found.");

            int firstRowIndex = markdown.IndexOf('\n', headerIndex + header.Length);
            Assert.True(firstRowIndex >= 0, "Markdown normalization table separator was not found.");

            var values = new List<string>();
            using var reader = new StringReader(markdown[(firstRowIndex + 1)..]);
            // Skip the table separator.
            // table separator を読み飛ばします。
            _ = reader.ReadLine();

            string? line;
            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            {
                Assert.StartsWith("| ", line, StringComparison.Ordinal);
                Assert.EndsWith(" |", line, StringComparison.Ordinal);
                values.Add(DecodeMarkdownConfiguredValue(line[2..^2]));
            }

            return values.ToArray();
        }

        private static string DecodeMarkdownConfiguredValue(string value)
        {
            string decoded = WebUtility.HtmlDecode(value);
            var original = new StringBuilder(decoded.Length);

            for (int index = 0; index < decoded.Length; index++)
            {
                char current = decoded[index];
                if (current != '\\')
                {
                    original.Append(current);
                    continue;
                }

                Assert.True(
                    index + 1 < decoded.Length && decoded[index + 1] == '\\',
                    "Markdown configured-value backslashes must be doubled.");
                original.Append('\\');
                index++;
            }

            return original.ToString();
        }

        private static string[] ExtractHtmlNormalizationValues(string html)
        {
            const string tableMarker = "aria-label=\"Substring to Normalize\"";
            int markerIndex = html.IndexOf(tableMarker, StringComparison.Ordinal);
            Assert.True(markerIndex >= 0, "HTML normalization table was not found.");

            int bodyStart = html.IndexOf("<tbody>", markerIndex, StringComparison.Ordinal);
            int bodyEnd = html.IndexOf("</tbody>", bodyStart, StringComparison.Ordinal);
            Assert.True(bodyStart >= 0 && bodyEnd > bodyStart, "HTML normalization table body was not found.");

            string body = html.Substring(bodyStart, bodyEnd - bodyStart);
            MatchCollection matches = Regex.Matches(
                body,
                @"<tr><td><code>(.*?)</code></td></tr>",
                RegexOptions.CultureInvariant | RegexOptions.Singleline);
            var values = new string[matches.Count];

            for (int index = 0; index < matches.Count; index++)
            {
                values[index] = WebUtility.HtmlDecode(matches[index].Groups[1].Value);
            }

            return values;
        }

        private static string FindRepositoryRoot()
        {
            string? directory = AppContext.BaseDirectory;
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory, "FolderDiffIL4DotNet.sln")))
                {
                    return directory;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
