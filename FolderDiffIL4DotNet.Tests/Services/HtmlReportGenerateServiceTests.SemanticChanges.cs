using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Services.Caching;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Assembly semantic changes and table styling tests.
    /// アセンブリ意味変更およびテーブルスタイリングテスト。
    /// </summary>
    public sealed partial class HtmlReportGenerateServiceTests
    {
        // ── Assembly Semantic Changes / アセンブリ意味変更 ─────────────────────

        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_ShowsInlineAboveILDiff()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("semantic-changes");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "MyApp.Service", "", "public", "", "Method", "NewMethod", "", "void", "string name", ""),
                    new("Modified", "MyApp.Service", "", "public", "virtual", "Method", "ExistingMethod", "", "bool", "int id", "Changed"),
                    new("Added", "MyApp.Service", "", "public", "", "Property", "NewProp", "string", "", "", ""),
                    new("Removed", "MyApp.Service", "", "private", "readonly", "Field", "_oldField", "int", "", "", ""),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: true);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.Contains("Show assembly semantic changes", html);
            Assert.Contains("semantic_mod_0", html);
            Assert.Contains("semantic-changes-table", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_ShowsCaveatNote()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("semantic-caveat");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "MyApp.Service", "", "public", "", "Method", "NewMethod", "", "void", "", ""),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: true);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Caveat note should be present in the semantic changes section (EN text)
            // セマンティック変更セクションに注意書きが存在すべき
            Assert.Contains("sc-caveat", html);
            Assert.Contains("supplementary information", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_CaveatCssExists()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("semantic-caveat-css");
            var config = CreateConfig();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // CSS rule for .sc-caveat should exist in the stylesheet
            Assert.Contains(".semantic-changes p.sc-caveat", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_NoCaveatWhenNoStructuralChanges()
        {
            // When no structural changes are detected (empty Entries), the caveat note
            // should NOT appear — the "No structural changes" message already directs
            // the user to the IL diff.
            // 構造的変更なし（Entries 空）の場合、注意書きは不要。
            var (oldDir, newDir, reportDir) = MakeDirs("semantic-no-caveat");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>(),
            };

            var builder = CreateConfigBuilder(enableInlineDiff: true);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // The "No structural changes" message should be present
            Assert.Contains("No structural changes detected", html);
            // But the caveat note should NOT be in the base64-encoded content
            // (it would be encoded, but we can check the decoded content isn't generated)
            Assert.DoesNotContain("supplementary information", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_NotShownWhenDisabled()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("semantic-changes-off");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "MyApp.Service", "", "public", "", "Method", "NewMethod", "", "void", "string name", ""),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: true);
            builder.ShouldIncludeAssemblySemanticChangesInReport = false;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            Assert.DoesNotContain("Show assembly semantic changes", html);
        }

        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_LazyRender_EncodesAsBase64()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("semantic-changes-lazy");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "Foo", "", "public", "", "Method", "Bar", "", "void", "", ""),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: true, lazyRender: true);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Should contain a data-diff-html attribute for the semantic changes row
            Assert.Contains("semantic_mod_0", html);
            Assert.Contains("Show assembly semantic changes", html);
            // Content should NOT be inline (lazy rendered) — table markup is base64-encoded
            Assert.DoesNotContain("<table class=\"semantic-changes-table", html);
            Assert.Contains("data-diff-html", html);
        }

        // ── Assembly Semantic Changes table styling / テーブルスタイル ────────

        /// <summary>
        /// Verifies the semantic-changes table header background uses the lighter gray (#98989d).
        /// semantic-changes テーブルヘッダ背景に薄い灰色 (#98989d) が使われることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_TableHeaderUsesLighterGray()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sc-header-color");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Semantic changes table header must use lighter gray (#98989d), not the old dark gray (#6b6b6e)
            // semantic-changes テーブルヘッダは旧暗灰色(#6b6b6e)ではなく薄灰色(#98989d)であること
            Assert.Contains("background: #98989d", html);
            Assert.DoesNotContain("background: #6b6b6e", html);
        }

        /// <summary>
        /// Verifies that the checkbox column header (&#x2713;) is present in the semantic-changes detail table.
        /// semantic-changes 詳細テーブルにチェック列ヘッダ (&#x2713;) が存在することを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_CheckboxHeaderPresent()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sc-cb-header");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "Foo", "", "public", "", "Method", "Bar", "", "void", "", ""),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: true, lazyRender: false);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Detail table must have checkbox header (✓) AND body checkboxes
            // 詳細テーブルにチェックヘッダ(✓)とボディのチェックボックスが両方存在すること
            Assert.Contains("<th scope=\"col\" class=\"sc-col-cb\">&#x2713;</th>", html);
            Assert.Contains("<td class=\"sc-col-cb\"><input type=\"checkbox\"", html);
        }

        /// <summary>
        /// Verifies that Kind, Body, Access, and Modifiers column body cells use code emphasis.
        /// Kind, Body, Access, Modifiers 列ボディが code 強調表示を使用することを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_KindBodyAccessModifiersUseCodeEmphasis()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sc-emphasis");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Modified", "MyApp.Svc", "", "public", "virtual", "Method", "Run", "", "void", "", "Changed"),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: true, lazyRender: false);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Kind, Body, Access, and Modifiers all use <code> emphasis
            // Kind, Body, Access, Modifiers はすべて <code> 強調表示を使用する
            Assert.Contains("<code>Method</code>", html);        // Kind
            Assert.Contains("<code>public</code>", html);        // Access
            Assert.Contains("<code>virtual</code>", html);       // Modifiers
            Assert.Contains("style=\"background:var(--color-modified-bg)\">[ * ]", html); // Status cell with modified bg (no code emphasis)
            Assert.Contains("<code>Changed</code>", html);       // Body
        }

        /// <summary>
        /// Verifies that Access column with arrow notation wraps each side in code tags individually.
        /// Access 列の矢印表記で各側を個別に code タグで囲むことを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_AccessArrowWrapsEachSideInCode()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sc-arrow-code");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["lib.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Modified", "MyApp.Svc", "", "public \u2192 internal", "virtual \u2192 sealed", "Method", "Run", "", "void", "", "Changed"),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: true, lazyRender: false);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            var config = builder.Build();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Arrow notation wraps each side individually: <code>old</code> → <code>new</code>
            // 矢印表記は各側を個別に囲む: <code>旧</code> → <code>新</code>
            Assert.Contains("<code>public</code> \u2192 <code>internal</code>", html);
            Assert.Contains("<code>virtual</code> \u2192 <code>sealed</code>", html);
        }

        /// <summary>
        /// Verifies the th.sc-col-cb CSS rule exists for proper checkbox column header styling.
        /// th.sc-col-cb CSS ルールが存在しチェック列ヘッダが正しくスタイリングされることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_ThScColCbCssRuleExists()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sc-th-css");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // Both th.sc-col-cb and td.sc-col-cb CSS rules must exist
            // th.sc-col-cb と td.sc-col-cb 両方の CSS ルールが存在すること
            Assert.Contains("table.semantic-changes-table th.sc-col-cb", html);
            Assert.Contains("table.semantic-changes-table td.sc-col-cb", html);
        }

        /// <summary>
        /// Verifies the semantic changes table td cells have white background styling.
        /// セマンティック変更テーブルの td セルに白背景スタイルが適用されていることを確認する。
        /// </summary>
        [Fact]
        public void GenerateDiffReportHtml_AssemblySemanticChanges_TdHasWhiteBackground()
        {
            var (oldDir, newDir, reportDir) = MakeDirs("sc-td-bg");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));
            // td cells in the semantic-changes-table must have white background
            // semantic-changes-table の td セルは白背景であること
            Assert.Contains("background: #fff", html);
        }

    }
}
