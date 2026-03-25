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
    /// Mutation-testing-focused tests for stronger coverage.
    /// カバレッジ強化のためのミューテーションテスト。
    /// </summary>
    public sealed partial class HtmlReportGenerateServiceTests
    {
        // ── Mutation-testing-focused tests / ミューテーションテスト強化 ─────────

        /// <summary>
        /// Verifies that unchanged files with different DiffDetailResult values appear in correct sort order:
        /// SHA256Match first, then ILMatch, then TextMatch.
        /// 異なる DiffDetailResult を持つ Unchanged ファイルが正しいソート順（SHA256Match → ILMatch → TextMatch）で表示されることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_UnchangedSection_SortOrderByDiffDetail_SHA256ThenILThenText()
        {
            // Arrange: add unchanged files with each diff detail type in reverse order
            // 各 diff detail 種別の Unchanged ファイルを逆順で追加
            var (oldDir, newDir, reportDir) = MakeDirs("unch-sort-detail");
            var config = CreateConfig(enableInlineDiff: false);

            _resultLists.AddUnchangedFileRelativePath("text-file.config");
            _resultLists.RecordDiffDetail("text-file.config", FileDiffResultLists.DiffDetailResult.TextMatch);
            _resultLists.AddUnchangedFileRelativePath("il-file.dll");
            _resultLists.RecordDiffDetail("il-file.dll", FileDiffResultLists.DiffDetailResult.ILMatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.AddUnchangedFileRelativePath("sha-file.bin");
            _resultLists.RecordDiffDetail("sha-file.bin", FileDiffResultLists.DiffDetailResult.SHA256Match);

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Extract unchanged section only / Unchanged セクションのみ抽出
            int unchStart = html.IndexOf("Unchanged Files", StringComparison.Ordinal);
            int addedStart = html.IndexOf("Added Files", StringComparison.Ordinal);
            Assert.True(unchStart >= 0 && addedStart > unchStart);
            string unchSection = html[unchStart..addedStart];

            int shaIdx = unchSection.IndexOf("sha-file.bin", StringComparison.Ordinal);
            int ilIdx = unchSection.IndexOf("il-file.dll", StringComparison.Ordinal);
            int textIdx = unchSection.IndexOf("text-file.config", StringComparison.Ordinal);

            Assert.True(shaIdx < ilIdx, "SHA256Match should appear before ILMatch in unchanged section");
            Assert.True(ilIdx < textIdx, "ILMatch should appear before TextMatch in unchanged section");
        }

        /// <summary>
        /// Verifies that modified files with different DiffDetailResult values appear in correct sort order:
        /// TextMismatch first, then ILMismatch, then SHA256Mismatch.
        /// 異なる DiffDetailResult を持つ Modified ファイルが正しいソート順（TextMismatch → ILMismatch → SHA256Mismatch）で表示されることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_ModifiedSection_SortOrderByDiffDetail_TextThenILThenSHA256()
        {
            // Arrange: add modified files in reverse priority order
            // 優先度逆順で Modified ファイルを追加
            var (oldDir, newDir, reportDir) = MakeDirs("mod-sort-detail");
            var config = CreateConfig(enableInlineDiff: false);

            _resultLists.AddModifiedFileRelativePath("sha-file.bin");
            _resultLists.RecordDiffDetail("sha-file.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);
            _resultLists.AddModifiedFileRelativePath("il-file.dll");
            _resultLists.RecordDiffDetail("il-file.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.AddModifiedFileRelativePath("text-file.config");
            _resultLists.RecordDiffDetail("text-file.config", FileDiffResultLists.DiffDetailResult.TextMismatch);

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Extract modified section only / Modified セクションのみ抽出
            int modStart = html.IndexOf("Modified Files", StringComparison.Ordinal);
            int summaryStart = html.IndexOf("Summary</h2>", StringComparison.Ordinal);
            Assert.True(modStart >= 0 && summaryStart > modStart);
            string modSection = html[modStart..summaryStart];

            int textIdx = modSection.IndexOf("text-file.config", StringComparison.Ordinal);
            int ilIdx = modSection.IndexOf("il-file.dll", StringComparison.Ordinal);
            int shaIdx = modSection.IndexOf("sha-file.bin", StringComparison.Ordinal);

            Assert.True(textIdx < ilIdx, "TextMismatch should appear before ILMismatch in modified section");
            Assert.True(ilIdx < shaIdx, "ILMismatch should appear before SHA256Mismatch in modified section");
        }

        /// <summary>
        /// Verifies that modified files with High, Medium, Low importance appear in correct sort order (High first).
        /// High, Medium, Low 重要度を持つ Modified ファイルが正しいソート順（High が先頭）で表示されることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_ModifiedSection_ImportanceSortOrder_HighFirst()
        {
            // Arrange: add files with different importance levels, all ILMismatch for same diff-detail tier
            // 同一 diff-detail 階層の ILMismatch で、異なる重要度レベルのファイルを追加
            var (oldDir, newDir, reportDir) = MakeDirs("imp-sort");
            var config = CreateConfig(enableInlineDiff: false);

            _resultLists.AddModifiedFileRelativePath("low-imp.dll");
            _resultLists.RecordDiffDetail("low-imp.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.FileRelativePathToAssemblySemanticChanges["low-imp.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Modified", "NS.LowClass", "", "private", "", "Method", "InternalMethod", "", "void", "", "Changed", ChangeImportance.Low),
                },
            };

            _resultLists.AddModifiedFileRelativePath("high-imp.dll");
            _resultLists.RecordDiffDetail("high-imp.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.FileRelativePathToAssemblySemanticChanges["high-imp.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Removed", "NS.HighClass", "", "public", "", "Method", "PublicMethod", "", "void", "", "", ChangeImportance.High),
                },
            };

            _resultLists.AddModifiedFileRelativePath("medium-imp.dll");
            _resultLists.RecordDiffDetail("medium-imp.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.FileRelativePathToAssemblySemanticChanges["medium-imp.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "NS.MedClass", "", "public", "", "Method", "NewMethod", "", "void", "", "", ChangeImportance.Medium),
                },
            };

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Extract modified section only / Modified セクションのみ抽出
            int modStart = html.IndexOf("Modified Files", StringComparison.Ordinal);
            int summaryStart = html.IndexOf("Summary</h2>", StringComparison.Ordinal);
            string modSection = html[modStart..summaryStart];

            int highIdx = modSection.IndexOf("high-imp.dll", StringComparison.Ordinal);
            int medIdx = modSection.IndexOf("medium-imp.dll", StringComparison.Ordinal);
            int lowIdx = modSection.IndexOf("low-imp.dll", StringComparison.Ordinal);

            Assert.True(highIdx < medIdx, "High importance should appear before Medium in modified section");
            Assert.True(medIdx < lowIdx, "Medium importance should appear before Low in modified section");
        }

        /// <summary>
        /// Verifies that when ShouldOutputFileTimestamps=false, no Timestamp column header appears.
        /// ShouldOutputFileTimestamps=false の場合、Timestamp 列ヘッダが表示されないことを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_TimestampsDisabled_NoTimezoneCard()
        {
            // Arrange: timestamps disabled / タイムスタンプ無効
            var (oldDir, newDir, reportDir) = MakeDirs("no-ts");
            var builder = CreateConfigBuilder();
            builder.ShouldOutputFileTimestamps = false;
            var config = builder.Build();

            _resultLists.AddModifiedFileRelativePath("mod.txt");
            _resultLists.RecordDiffDetail("mod.txt", FileDiffResultLists.DiffDetailResult.TextMismatch);

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Timezone card should NOT appear when timestamps are disabled
            // タイムスタンプ無効時は Timezone カードが表示されないこと
            Assert.DoesNotContain("Timezone", html);
        }

        /// <summary>
        /// Verifies that when ShouldIncludeIgnoredFiles=false, the ignored section is absent from the report.
        /// ShouldIncludeIgnoredFiles=false の場合、Ignored セクションがレポートに含まれないことを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_IgnoredFilesDisabled_NoIgnoredSection()
        {
            // Arrange: add ignored file but disable section / Ignored ファイルを追加するがセクションは無効
            var (oldDir, newDir, reportDir) = MakeDirs("no-ign-section");
            _resultLists.RecordIgnoredFile("skip.pdb", FileDiffResultLists.IgnoredFileLocation.Old);

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = false;
            var config = builder.Build();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // The ignored section table should NOT appear (check for the section's data-section attribute, not the
            // heading text which also appears in JS string literals)
            // Ignored セクションテーブルが表示されないことを確認（JS 文字列リテラルにも含まれる見出しテキストではなく
            // data-section 属性で検証）
            Assert.DoesNotContain("data-section=\"ign\"", html);
        }

        /// <summary>
        /// Verifies that "Ignored" row is NOT in the summary table when ShouldIncludeIgnoredFiles=false.
        /// ShouldIncludeIgnoredFiles=false の場合、サマリーテーブルに "Ignored" 行が含まれないことを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_SummarySection_IgnoredRowAbsent_WhenIgnoredFilesDisabled()
        {
            // Arrange / テスト準備
            var (oldDir, newDir, reportDir) = MakeDirs("summary-no-ign");
            _resultLists.RecordIgnoredFile("skip.pdb", FileDiffResultLists.IgnoredFileLocation.Old);

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = false;
            var config = builder.Build();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Summary should NOT contain "Ignored" row / サマリーに "Ignored" 行が含まれないこと
            Assert.DoesNotContain("stat-label\">Ignored</td>", html);
            // But other summary rows should still exist / 他のサマリー行は存在すること
            Assert.Contains("stat-label\">Unchanged</td>", html);
            Assert.Contains("stat-label\">Added</td>", html);
        }

        /// <summary>
        /// Verifies that both SHA256 mismatch and timestamp regression warnings appear together.
        /// SHA256 ミスマッチとタイムスタンプ逆行の両方の警告が同時に表示されることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_WarningsSection_BothSha256AndTimestampWarnings()
        {
            // Arrange: create both warning types / 両方の警告タイプを作成
            var (oldDir, newDir, reportDir) = MakeDirs("warn-both");

            _resultLists.AddModifiedFileRelativePath("sha-warn.bin");
            _resultLists.RecordDiffDetail("sha-warn.bin", FileDiffResultLists.DiffDetailResult.SHA256Mismatch);

            _resultLists.AddModifiedFileRelativePath("ts-warn.dll");
            _resultLists.RecordDiffDetail("ts-warn.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");
            _resultLists.RecordNewFileTimestampOlderThanOldWarning("ts-warn.dll", "2026-03-15 10:00:00", "2026-03-15 09:00:00");

            var builder = CreateConfigBuilder();
            builder.ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp = true;
            var config = builder.Build();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Both warning headings should appear / 両方の警告見出しが表示されること
            Assert.Contains("Warnings</h2>", html);
            Assert.Contains("SHA256Mismatch: binary diff only", html);
            Assert.Contains("new file timestamps older than old", html);
            Assert.Contains("sha-warn.bin", html);
            Assert.Contains("ts-warn.dll", html);
        }

        /// <summary>
        /// Verifies that warnings section is absent when there are no SHA256 mismatches and no timestamp regressions.
        /// SHA256 ミスマッチもタイムスタンプ逆行もない場合、警告セクションが表示されないことを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_WarningsSection_AbsentWhenNoWarnings()
        {
            // Arrange: only ILMismatch (no SHA256Mismatch, no timestamp regression)
            // ILMismatch のみ（SHA256Mismatch なし、タイムスタンプ逆行なし）
            var (oldDir, newDir, reportDir) = MakeDirs("no-warnings");

            _resultLists.AddModifiedFileRelativePath("lib.dll");
            _resultLists.RecordDiffDetail("lib.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            var config = CreateConfig();
            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Warnings heading should NOT appear / 警告見出しが表示されないこと
            Assert.DoesNotContain("Warnings</h2>", html);
        }

        /// <summary>
        /// Verifies that unchanged files with equal old/new timestamps show a single timestamp (not arrow format).
        /// 新旧タイムスタンプが同一の Unchanged ファイルが単一タイムスタンプ（矢印形式でない）で表示されることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_UnchangedFiles_EqualTimestamps_ShowsSingleTimestamp()
        {
            // Arrange: create files with identical content and same modification time
            // 同一内容で同一更新日時のファイルを作成
            var (oldDir, newDir, reportDir) = MakeDirs("unch-equal-ts");
            var file = "same-ts.txt";
            File.WriteAllText(Path.Combine(oldDir, file), "content");
            File.WriteAllText(Path.Combine(newDir, file), "content");

            // Set the same last-write time for both files / 両ファイルに同一の最終書き込み時刻を設定
            var sharedTime = new DateTime(2026, 1, 15, 12, 0, 0);
            File.SetLastWriteTime(Path.Combine(oldDir, file), sharedTime);
            File.SetLastWriteTime(Path.Combine(newDir, file), sharedTime);

            _resultLists.AddUnchangedFileRelativePath(file);
            _resultLists.RecordDiffDetail(file, FileDiffResultLists.DiffDetailResult.SHA256Match);

            var builder = CreateConfigBuilder();
            builder.ShouldOutputFileTimestamps = true;
            var config = builder.Build();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Extract unchanged section / Unchanged セクションを抽出
            int unchStart = html.IndexOf("Unchanged Files", StringComparison.Ordinal);
            int addedStart = html.IndexOf("Added Files", StringComparison.Ordinal);
            string unchSection = html[unchStart..addedStart];

            // Equal timestamps should NOT use arrow format / 同一タイムスタンプは矢印形式を使わないこと
            Assert.DoesNotContain(" → ", unchSection);
        }

        /// <summary>
        /// Verifies that elapsed time is not displayed in the header when elapsedTimeString is null.
        /// elapsedTimeString が null の場合、ヘッダーに経過時間が表示されないことを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_HeaderSection_ElapsedTimeNull_NotDisplayed()
        {
            // Arrange / テスト準備
            var (oldDir, newDir, reportDir) = MakeDirs("no-elapsed");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Elapsed Time card should NOT appear when null / null の場合 Elapsed Time カードが表示されないこと
            Assert.DoesNotContain("Elapsed Time", html);
        }

        /// <summary>
        /// Verifies that when ShouldIgnoreILLinesContainingConfiguredStrings=true but the list is empty,
        /// the "no strings configured" message is shown.
        /// ShouldIgnoreILLinesContainingConfiguredStrings=true でリストが空の場合、
        /// 「文字列未設定」メッセージが表示されることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_HeaderSection_ILIgnoreEnabled_EmptyList_ShowsNoStringsMessage()
        {
            // Arrange: enable IL ignore but with empty list / IL 無視を有効にするが空リスト
            var (oldDir, newDir, reportDir) = MakeDirs("il-ignore-empty");
            var builder = CreateConfigBuilder();
            builder.ShouldIgnoreILLinesContainingConfiguredStrings = true;
            builder.ILIgnoreLineContainingStrings = new List<string>();
            var config = builder.Build();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Should show the "no strings configured" message / 「文字列未設定」メッセージが表示されること
            Assert.Contains("IL Ignored Strings", html);
            Assert.Contains("Enabled, but no non-empty strings are configured.", html);
        }

        /// <summary>
        /// Verifies that ignored files present in both old and new folders show "old/new" location.
        /// 旧新両方に存在する Ignored ファイルが "old/new" ロケーションで表示されることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_IgnoredSection_FileInBothOldAndNew_ShowsOldNewLocation()
        {
            // Arrange: file in both old and new with combined flags / 旧新両方のフラグでファイルを追加
            var (oldDir, newDir, reportDir) = MakeDirs("ign-both-sides");
            File.WriteAllText(Path.Combine(oldDir, "both.pdb"), "old");
            File.WriteAllText(Path.Combine(newDir, "both.pdb"), "new");
            _resultLists.RecordIgnoredFile("both.pdb",
                FileDiffResultLists.IgnoredFileLocation.Old | FileDiffResultLists.IgnoredFileLocation.New);

            var builder = CreateConfigBuilder();
            builder.ShouldIncludeIgnoredFiles = true;
            var config = builder.Build();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Should show "old/new" location for files in both folders
            // 両フォルダのファイルに "old/new" ロケーションが表示されること
            Assert.Contains("old/new", html);
            // When in both, display path should be relative (not absolute)
            // 両方に存在する場合、表示パスは相対パス（絶対パスではない）であること
            Assert.Contains("both.pdb", html);
        }

        /// <summary>
        /// Verifies that modified section with ILMismatch + semantic changes + dependency changes
        /// all appear together for the same file.
        /// ILMismatch + セマンティック変更 + 依存関係変更がすべて同一ファイルに対して同時に表示されることを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_ModifiedSection_ILMismatchWithSemanticAndDependencyChanges()
        {
            // Arrange: file with ILMismatch, semantic changes, AND dependency changes
            // ILMismatch、セマンティック変更、依存関係変更のあるファイル
            var (oldDir, newDir, reportDir) = MakeDirs("mod-full-combo");

            _resultLists.AddModifiedFileRelativePath("full.dll");
            _resultLists.RecordDiffDetail("full.dll", FileDiffResultLists.DiffDetailResult.ILMismatch, "dotnet-ildasm (version: 0.12.0)");

            _resultLists.FileRelativePathToAssemblySemanticChanges["full.dll"] = new AssemblySemanticChangesSummary
            {
                Entries = new List<MemberChangeEntry>
                {
                    new("Added", "NS.Service", "System.Object", "public", "", "Method", "DoWork", "", "void", "int count", "", ChangeImportance.Medium),
                },
            };

            _resultLists.FileRelativePathToDependencyChanges["full.dll"] = new DependencyChangeSummary
            {
                Entries = new List<DependencyChangeEntry>
                {
                    new("Updated", "Newtonsoft.Json", "12.0.3", "13.0.1", ChangeImportance.Low),
                    new("Added", "System.Text.Json", "", "8.0.0", ChangeImportance.Medium),
                },
            };

            var builder = CreateConfigBuilder(enableInlineDiff: false, lazyRender: false);
            builder.ShouldIncludeAssemblySemanticChangesInReport = true;
            builder.ShouldIncludeDependencyChangesInReport = true;
            var config = builder.Build();

            _service.GenerateDiffReportHtml(
                new ReportGenerationContext(oldDir, newDir, reportDir,
                    appVersion: "1.0", elapsedTimeString: null,
                    computerName: "test-host", config, ilCache: null));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // All three features should appear together / 3つの機能がすべて表示されること
            Assert.Contains("full.dll", html);
            Assert.Contains("ILMismatch", html);
            Assert.Contains("Show assembly semantic changes", html);
            Assert.Contains("Show dependency changes", html);
            Assert.Contains("Newtonsoft.Json", html);
            Assert.Contains("System.Text.Json", html);
            Assert.Contains("DoWork", html);
        }

        /// <summary>
        /// Verifies that disassembler availability section shows correct probe results with available and not-available probes.
        /// 逆アセンブラ利用可否セクションが利用可能・利用不可のプローブ結果を正しく表示することを確認する。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void GenerateDiffReportHtml_DisassemblerAvailability_ShowsAvailableAndNotAvailable()
        {
            // Arrange: set up probes with mixed availability / 利用可否が混在するプローブを設定
            _resultLists.DisassemblerAvailability = new List<DisassemblerProbeResult>
            {
                new("dotnet-ildasm", true, "0.14.0", "/usr/local/bin/dotnet-ildasm"),
                new("ilspycmd", false, null, null),
            };
            // Mark dotnet-ildasm as in-use by adding a tool version entry
            // dotnet-ildasm を使用中としてマーク
            _resultLists.RecordDisassemblerToolVersion("dotnet-ildasm", "0.14.0");

            var (oldDir, newDir, reportDir) = MakeDirs("disasm-probes");
            var config = CreateConfig();

            _service.GenerateDiffReportHtml(CreateReportContext(oldDir, newDir, reportDir, config));

            var html = File.ReadAllText(Path.Combine(reportDir, HtmlReportGenerateService.DIFF_REPORT_HTML_FILE_NAME));

            // Available probe should show green and version / 利用可能プローブが緑とバージョンで表示されること
            Assert.Contains("Available (0.14.0)", html);
            Assert.Contains("color:#22863a", html);
            // Not-available probe should show red / 利用不可プローブが赤で表示されること
            Assert.Contains("Not Available", html);
            Assert.Contains("color:#b31d28", html);
            // In-use marker should appear for the tool in use / 使用中のツールに In Use マーカーが表示されること
            Assert.Contains("In Use", html);
        }

    }
}
