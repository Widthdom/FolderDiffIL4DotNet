# Changelog

All notable changes to this project will be documented in this file.

The English section comes first, followed by a Japanese translation.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## English

### [Unreleased]

#### Fixed

- Fixed HTML report inline diff numbering in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs): the `#N` prefix shown before `Show diff` / `Show IL diff` and inline-diff skip messages now uses the same one-based row number as the leftmost `#` column instead of the internal zero-based index. Added test `GenerateDiffReportHtml_InlineDiffSummary_UsesSameOneBasedNumberAsLeftmostColumn` to [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs), and updated [README.md](README.md) plus [testing guide](doc/TESTING_GUIDE.md).

### [1.3.0] - 2026-03-17

#### Changed

- Raised the default values of `InlineDiffMaxOutputLines` and `InlineDiffMaxDiffLines` from `500`/`1000` to **`10000`** each in [`ConfigSettings`](Models/ConfigSettings.cs), [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs), and [`TextDiffer`](FolderDiffIL4DotNet.Core/Text/TextDiffer.cs). Updated [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs), bilingual [README.md](README.md), and [developer guide](doc/DEVELOPER_GUIDE.md).

- Replaced the O(N×M) LCS algorithm in [`TextDiffer`](FolderDiffIL4DotNet.Core/Text/TextDiffer.cs) with **Myers diff** (O(D² + N + M) time, O(D²) space, where D = edit distance). The previous `m × n > 4 000 000` cell-count guard is replaced by a new [`InlineDiffMaxEditDistance`](README.md#configuration-table-en) config key (default `4 000`) that limits the number of inserted + deleted lines. Files with millions of lines now produce an inline diff as long as the actual change is small — for example, two 2 370 000-line IL files differing by 20 lines complete in milliseconds. Updated [`TextDifferTests`](FolderDiffIL4DotNet.Tests/Core/Text/TextDifferTests.cs): replaced `Compute_InputExceedsLcsLimit_ReturnsTruncatedMessage` with `Compute_EditDistanceExceedsLimit_ReturnsTruncatedMessage`; added `Compute_LargeFilesSmallEditDistance_ProducesCorrectDiff` and `Compute_VeryLargeFilesWithTinyDiff_ProducesInlineDiff`.

- Polished HTML report UX in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs): download button icon changed from `⇩` to `⤓` and label changed to "Download as reviewed"; the reviewed-file banner now reads `"Reviewed: <timestamp> — read-only"` instead of a plain lock icon; Added/Removed section heading and column header colours now follow the GitHub diff palette (`#22863a` green / `#b31d28` red / `#e6ffed` background for Added, `#ffeef0` background for Removed); the `No` column is widened to `3.2em` to accommodate files counts up to 999,999; empty `Diff Reason` cells no longer render a ghost `<code>` element. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.
- Fixed Timestamp column stability when File Path is resized in the HTML report: changed the main file-list tables from `width: auto` to `table-layout: fixed; width: 1px`, added a `syncTableWidths()` JavaScript function that sets each table's explicit pixel width to the sum of all column widths (using CSS custom properties `--col-reason-w`, `--col-notes-w`, `--col-path-w`, `--col-diff-w`), and calls it on `DOMContentLoaded` and after every column resize; wrapped column-header text in a `span.th-label { display: block; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }` element so header content clips reliably. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.
- Fixed reviewed-mode checkboxes appearing grey: replaced `cb.disabled = true` with `cb.style.pointerEvents = 'none'; cb.style.cursor = 'default';` so the browser's internal disabled-grey rendering is avoided and checkboxes retain their accent colour. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.
- Download-as-reviewed now bakes the current column widths as defaults into the reviewed file: `downloadReviewed()` reads the current effective CSS custom-property values for all five column-width variables, replaces the `:root` CSS rule in the exported HTML with those values, and removes the inline `style` attribute from the `<html>` element, so the reviewed snapshot opens with whatever column layout was active at sign-off time. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.
- "Clear all" in the HTML report now also resets column widths to defaults and collapses all inline diff `<details>` elements: `clearAll()` removes CSS custom-property overrides, calls `syncTableWidths()`, and calls `removeAttribute('open')` on every `<details>`. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.
- Changed `td.col-reason`, `td.col-ts`, and `td.col-diff` body cells to `text-align: center` in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) (`col-diff` shows "Location" (`old`/`new`/`old/new`) in the Ignored Files table and the diff type (`ILMismatch`, `TextMismatch`, etc.) in other tables); `td.col-path` (File Path) remains left-aligned; column headers and `td.col-notes` are unchanged. Updated test `GenerateDiffReportHtml_BodyCells_ColReasonPathTs_HaveCenterAlignment` in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) to match. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.
- Fixed `.reviewed-banner` text colour from `#2d7a2d` (green) to `#1f2328` (near-black) in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) so the reviewed-file timestamp banner is visually neutral. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.

#### Changed

- Restructured HTML report table columns in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs): the Timestamp column is narrowed from `22em` to `16em`; the Diff Reason column is narrowed from `20em` to `9em` and now shows only the diff type (e.g. `ILMismatch`, `TextMismatch`) without the disassembler label; a new 8th **Disassembler** column (`28em`, resizable) is added at the far right and displays the disassembler label and version string per-row. Updated JavaScript (`colVarNames`, `clearAll`, `syncTableWidths`) and CSS (`:root` custom properties, `col.col-*-g`, `td.col-*`) to match. Updated [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) to match the new `InlineDiffMaxDiffLines` threshold check.

- Replaced `InlineDiffMaxInputLines` with `InlineDiffMaxDiffLines` (default `1000`) in [`ConfigSettings`](Models/ConfigSettings.cs). Previously the config checked whether either input file exceeded a line-count threshold *before* computing the diff; the actual HTML output for inline diff only shows changed lines, so the input line count is not a meaningful proxy. The new setting checks the computed diff output line count *after* `TextDiffer.Compute()` and suppresses the inline diff display if it exceeds the threshold. Updated [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs), [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs), [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs), bilingual [README.md](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md).

#### Fixed

- Fixed ILMismatch inline diff never appearing in the HTML report: [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) was calling `ILCache.TryGetILAsync` with a normalised label (e.g. `ildasm (version: 1.0.0)`) that never matched the label stored at write time (e.g. `ildasm MyAssembly.dll (version: 1.0.0)`), so the look-up always returned `null` and the inline diff was silently skipped. Fixed by reading IL text directly from the `*_IL.txt` files produced by [`ILTextOutputService`](Services/ILOutput/ILTextOutputService.cs) (under `Reports/<label>/IL/old` and `Reports/<label>/IL/new`) when `ShouldOutputILText` is `true` (the default). Added test `GenerateDiffReportHtml_ILMismatch_WithILTextFiles_ShowsInlineDiff` to [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs).

- Fixed timestamp-regression warnings being emitted for **unchanged** files: [`FolderDiffService`](Services/FolderDiffService.cs) now calls `RecordNewFileTimestampOlderThanOldWarningIfNeeded` only in the `Modified` branch (after `FilesAreEqualAsync` returns `false`), not before the content comparison. Previously, an unchanged file whose `new`-side timestamp was older than the `old`-side timestamp would incorrectly appear in the `Warnings` section of [`diff_report.md`](doc/samples/diff_report.md). Updated warning messages in [`ReportGenerateService`](Services/ReportGenerateService.cs), [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs), and [`ProgramRunner`](ProgramRunner.cs) to read "**modified** files" instead of "files". Updated XML doc comments in [`FileDiffResultLists`](Models/FileDiffResultLists.cs) and [`FileTimestampRegressionWarning`](Models/FileTimestampRegressionWarning.cs). Updated [`FolderDiffServiceTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs): renamed `ExecuteFolderDiffAsync_WhenNewFileTimestampIsOlder_RecordsWarning` to use different-content files (so the file is classified as modified), added new test `ExecuteFolderDiffAsync_WhenUnchangedFileTimestampIsOlder_DoesNotRecordWarning` that verifies no warning is emitted for same-content files with an older new-side timestamp, updated `ExecuteFolderDiffAsync_WhenTimestampWarningDisabled_DoesNotRecordWarning` to use different-content files; updated [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) and [`ProgramTests`](FolderDiffIL4DotNet.Tests/ProgramTests.cs) to match the new message wording. Updated bilingual [README.md](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md).

- Improved [`config.json`](config.json) parse error reporting: [`ConfigService`](Services/ConfigService.cs) now emits a descriptive error that includes the line number and byte position from the underlying `JsonException`, plus an explicit hint that trailing commas after the last property or array element are not allowed in standard JSON. The error is logged to the run log file and printed to the console in red; the run exits with code `3`. Added 3 targeted unit tests to [`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) covering trailing commas in objects, trailing commas in arrays, and multiline JSON with line-number verification.
- Fixed garbled `?` characters in the banner on Windows: [`Program.cs`](Program.cs) now sets [`Console.OutputEncoding`](https://learn.microsoft.com/en-us/DOTNET/api/system.console.outputencoding?view=net-8.0) = `Encoding.UTF8` at the very start of `Main()` before any output, overriding the OEM code page (CP932/CP437) that Windows uses by default. On Linux and macOS the console is already UTF-8, so this change has no effect on those platforms.
- Fixed three CI pipeline test failures caused by recent HTML report changes in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs): (1) `GenerateDiffReportHtml_ILMismatch_NoInlineDiff` and `GenerateDiffReportHtml_TextMismatch_EnableInlineDiffFalse_NoDetailsElement` were asserting `DoesNotContain("<details")` but a JS comment contained the literal `<details` — fixed by rewriting the comment to avoid the substring; (2) `GenerateDiffReportHtml_Md5MismatchWarning_AppearsInWarningsSection` used a single exact-match string that broke when a `<span>` was inserted between the class attribute and the heading text — fixed by splitting into two separate `Contains` assertions; (3) colour assertions updated from `#2d7a2d`/`#b00020` to `#22863a`/`#b31d28` to match the GitHub diff palette.
- Fixed edit-distance-exceeded inline diff showing `+0 / -0` (looking like no difference): when `TextDiffer.Compute` returns a single `Truncated` line (triggered when edit distance `D > InlineDiffMaxEditDistance`), [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) now renders a plain visible `diff-skipped` row without a `<details>` expand arrow — consistent with the `InlineDiffMaxDiffLines`-exceeded case. Renamed test to `GenerateDiffReportHtml_TextMismatch_EditDistanceTooLarge_ShowsSkippedMessageWithoutExpandArrow` in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs).
- Restored coloured text for `[ + ] Added`, `[ - ] Removed`, `[ * ] Modified`, and `[ ! ] Timestamps Regressed` section headings in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) (green `#22863a` / red `#b31d28` / blue `#0051c3`). Re-added `COLOR_ADDED`, `COLOR_REMOVED`, `COLOR_MODIFIED` constants. Updated [CHANGELOG.md](CHANGELOG.md) and [README.md](README.md) (EN + JP) to remove the stale "plain black text" description.

#### Changed

- Inline diff `<summary>` label now prefixes the file index: `#1 Show diff (+N / -M)` / `#1 Show IL diff (+N / -M)` (previously `Show diff` / `Show IL diff`), making it easy to identify which file the diff belongs to without looking at the row above.
- Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match the current production output: 8-column layout (added Disassembler column with `col.col-disasm-g` / `td.col-disasm` CSS, `--col-disasm-w: 28em` CSS variable, and resizable `Disassembler` header); `--col-diff-w` corrected from `20em` to `9em`; Timestamp column corrected from `22em` to `16em`; `col-ts` CSS updated; all `colspan="7"` changed to `colspan="8"`; diff-summary labels updated to `#N Show diff` / `#N Show IL diff`; Diff Reason cells split (e.g. `ILMismatch` + [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) ` (version: 0.12.2)` → separate `ILMismatch` + Disassembler cell); JavaScript `colVarNames` / `clearAll` arrays and `syncTableWidths` formula updated. Added two sample rows demonstrating edit-distance-exceeded skip (`src/BigSchema.cs`) and diff-too-large skip (`src/LargeConfig.xml`). Updated [`doc/samples/diff_report.md`](doc/samples/diff_report.md) to include the two new Modified entries and updated file counts (Modified 8, Compared 17).
- Added "Inline diff skip behaviour" section to [`doc/DEVELOPER_GUIDE.md`](doc/DEVELOPER_GUIDE.md) (EN + JP): documents all three skip triggers (edit distance too large / `InlineDiffMaxOutputLines` mid-truncation / `InlineDiffMaxDiffLines` post-compute), the conditions, and the resulting HTML rendering difference (`<details>` vs. plain row).

#### Added

- Further refined HTML report UX in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs): page title `<h1>` changed to "Folder Diff Report"; frosted-glass controls bar background opacity reduced to `rgba(255,255,255,0.45)` for a more transparent look; `h1` font size increased to `2.0rem` and `Summary` / `IL Cache Stats` / `Warnings` section headings given a dedicated `h2.section-heading` style at `1.55rem` so they stand out from file-list section headings; `[ ! ] Modified Files — Timestamps Regressed` promoted from `h3` to `h2` to match the `[ * ] Modified Files` style; stat-table numbers font changed to the body font (removed monospace); stat-table indented `1.2em` from the left margin; "Show diff" label in inline-diff summaries changed to "Show IL diff" for `ILMismatch` entries; `+N` and `-N` in diff summaries are now coloured green and red with `diff-added-cnt` / `diff-removed-cnt` spans; diff-row `<tr>` rows (the collapsible row containing `<details>`) now have a light-blue background (`#eef5ff`); IL ignore-string notes in the header no longer wrap values in `<code>` tags; `WARNING:` text removed from Warnings list items — a yellow `⚠` icon (`warn-icon`) is now shown instead; column widths for OK Reason / Notes / File Path are now controlled via CSS custom properties (`--col-reason-w`, `--col-notes-w`, `--col-path-w`) backed by `<colgroup>` elements, making them synchronised across all tables; column headers for these three columns are now draggable resize handles (`initColResize` JS) that update the CSS variables so every table resizes together; reviewed-HTML download replaces the controls bar with a green "🔒 Reviewed — read-only" banner (`reviewed-banner`) instead of stripping it entirely, giving reviewers a clear visual indicator that the file is a signed-off snapshot. Updated [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs): updated the Warnings-section assertion to match the new `h2.section-heading` structure.
- Overhauled [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) with a comprehensive set of HTML report improvements: fixed page title to `diff_report`; added a sticky frosted-glass controls bar (`backdrop-filter: blur`) that fills the full viewport width; replaced the old buttons with Apple-style minimal pill buttons (same height, `display:inline-flex`, `border-radius:980px`); auto-saved timestamp now formats as `YYYY-MM-DD HH:mm:ss`; Old/New folder paths are plain text (no `<code>` wrapper); MVID note rendered as a regular `<li>` meta item; IL contains-ignore note added to HTML header; Legend moved inside `<ul class="meta">` as a nested bullet list; all 5 file tables now share a consistent 8-column layout (`# | ✓ | OK Reason | Notes | File Path | Timestamp | Diff Reason | Disassembler`) with record numbers, no cell placeholders, and resizable OK Reason/Notes/Path/Disassembler columns; Added/Removed/Modified table headers use light green/red/blue backgrounds with black text; Ignored/Unchanged tables use a neutral header; Summary and IL Cache Stats sections use `<table class="stat-table">` with right-aligned numeric values; timestamp-regressed files in Warnings are rendered as a table under a `[ ! ] Modified Files — Timestamps Regressed (N)` heading. Inline diff changes: `InlineDiffContextLines` default reduced from 3 to 0 (changed lines only, no surrounding context); hunk separator rows are shown when lines are omitted; ILMismatch entries now render an inline diff when `ShouldOutputILText` is `true` (the default) and the `*_IL.txt` files are present under `Reports/<label>/IL/old` and `Reports/<label>/IL/new`. Reviewed HTML download improvements: output filename is `diff_report_{yyyyMMdd}_reviewed.html`; page title becomes `diff_report_{yyyyMMdd}_reviewed`; the controls bar (`<!--CTRL-->…<!--/CTRL-->`) is stripped from the downloaded copy; all checkboxes are `disabled` and text inputs are `readOnly` (still selectable and copyable) in the reviewed copy. Updated unit tests in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) and [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) to match the new colors (`#2d7a2d`, `#b00020`), stat-table HTML structure, and `InlineDiffContextLines` default of `0`.
- Improved [`diff_report.md`](doc/samples/diff_report.md) section headers: [`ReportGenerateService`](Services/ReportGenerateService.cs) now appends the file count to each section heading — e.g. `## [ x ] Ignored Files (3)`, `## [ + ] Added Files (1)` — so the count is visible at a glance without reading the list. Changed the display path for single-side ignored files in `IgnoredFilesSectionWriter`: entries present only in `old` or only in `new` now show the absolute path (`/path/to/old/rel/file.pdb`), while entries present on both sides continue to show the relative path.

- Added `ShouldGenerateHtmlReport` (default `true`) to [`ConfigSettings`](Models/ConfigSettings.cs). When `true`, each run produces **`diff_report.html`** alongside `diff_report.md` in the same `Reports/<label>/` directory. The HTML file is a standalone self-contained review document — no server or browser extension needed. All file entries (Ignored, Unchanged, Added, Removed, Modified) are presented in an 8-column table; Removed / Added / Modified rows have an interactive checkbox, OK-Reason text input, and Notes text input for sign-off during product-release review. Column headers for Added / Removed / Modified use colour-coded backgrounds (green / red / blue); section headings for Added / Removed / Modified use colour-coded text (green / red / blue). The file includes embedded JavaScript for localStorage auto-save (keyed by `folderdiff-<label>`) and a **"Download reviewed version"** button that bakes the current review state into a new portable snapshot file. Set `"ShouldGenerateHtmlReport": false` in [`config.json`](config.json) to opt out. Implemented in the new [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs), registered in [`RunScopeBuilder`](Runner/RunScopeBuilder.cs), and called from [`ProgramRunner.GenerateReport()`](ProgramRunner.cs). Added 12 unit tests to [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs). Added sample files [`doc/samples/diff_report.md`](doc/samples/diff_report.md) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html); refactored [README.md](README.md) to link to these external samples and added a new bilingual `Interactive HTML Review Report` / `インタラクティブ HTML レビューレポート` section describing the review workflow. Updated bilingual [developer guide](doc/DEVELOPER_GUIDE.md) and [testing guide](doc/TESTING_GUIDE.md).

- Added `DisassemblerBlacklistTtlMinutes` (default `10`) to [`ConfigSettings`](Models/ConfigSettings.cs). The new property controls the blacklist TTL for a disassembler tool that has failed consecutively `DISASSEMBLE_FAIL_THRESHOLD` (3) times. Previously the TTL was hardcoded at 10 minutes. [`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) now reads this setting from config at construction time instead of using a static `TimeSpan.FromMinutes(10)`. Updated [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) to assert the new default and JSON round-trip behavior.
- Extracted [`DisassemblerBlacklist`](Services/DisassemblerBlacklist.cs) out of [`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) into its own class, encapsulating the [`ConcurrentDictionary`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2?view=net-8.0), TTL, and fail-threshold logic. Added `InjectEntry` / `ContainsEntry` test-only helpers on the class, and updated [`DotNetDisassembleServiceTests`](FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) to use instance-level reflection (`_blacklist`) instead of the old static-field access. New [`DisassemblerBlacklistTests`](FolderDiffIL4DotNet.Tests/Services/DisassemblerBlacklistTests.cs) covers threshold boundary, TTL expiry, `Clear`, `ResetFailure`, null-safe handling, and two concurrent-access scenarios (B-4).
- Introduced [`IReportSectionWriter`](Services/IReportSectionWriter.cs) interface and [`ReportWriteContext`](Services/ReportWriteContext.cs) context class. [`ReportGenerateService`](Services/ReportGenerateService.cs) now defines a static `_sectionWriters` list of 10 private nested-class implementations (`HeaderSectionWriter`, `LegendSectionWriter`, `IgnoredFilesSectionWriter`, `UnchangedFilesSectionWriter`, `AddedFilesSectionWriter`, `RemovedFilesSectionWriter`, `ModifiedFilesSectionWriter`, `SummarySectionWriter`, `ILCacheStatsSectionWriter`, `WarningsSectionWriter`) and iterates over them in `WriteReportSections`. Each section can be exercised in isolation by constructing a `ReportWriteContext` without needing the full service.
- Added Unicode-filename report tests to [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs): `GenerateDiffReport_UnicodeFileNames_AreIncludedInReport` and `GenerateDiffReport_UnicodeFileNames_InUnchangedSection` verify that Japanese, Umlauted-Latin, and Chinese relative paths appear verbatim in the Markdown report (B-2).
- Added large-file-count summary snapshot test `GenerateDiffReport_LargeFileCount_SummaryStatisticsAreCorrect` to [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs): seeds 10 500 unchanged files and asserts that `Unchanged` and `Compared` counts in the Summary section match the seeded count (B-3).

#### Changed

- Consolidated four identical parallel-text-diff fallback `catch` blocks ([`ArgumentOutOfRangeException`](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception?view=net-8.0), [`IOException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.ioexception?view=net-8.0), [`UnauthorizedAccessException`](https://learn.microsoft.com/en-us/dotnet/api/system.unauthorizedaccessexception?view=net-8.0), [`NotSupportedException`](https://learn.microsoft.com/en-us/dotnet/api/system.notsupportedexception?view=net-8.0)) in [`FileDiffService`](Services/FileDiffService.cs) into a single `catch (Exception ex) when (ex is … or …)` guard, eliminating the duplicated fallback body.
- Enhanced the IL-diff failure log in [`FileDiffService`](Services/FileDiffService.cs): the error message now appends `ex.Message` (which includes the disassembler command and inner cause) so the log line is self-contained without requiring users to read the stack trace.
- Added rationale comments to previously undocumented magic constants: `KEEP_ALIVE_INTERVAL_SECONDS = 5` and `LARGE_DISCOVERY_FILE_COUNT_LOG_THRESHOLD = 10000` in [`FolderDiffService`](Services/FolderDiffService.cs); `MAX_PARALLEL_NETWORK_LIMIT = 8` in [`FolderDiffExecutionStrategy`](Services/FolderDiffExecutionStrategy.cs); `DISASSEMBLE_FAIL_THRESHOLD = 3` and `DEFAULT_BLACKLIST_TTL_MINUTES = 10` in [`DotNetDisassembleService`](Services/DotNetDisassembleService.cs).

#### Added

- Added `DiffSummaryStatistics` record and `SummaryStatistics` computed property to [`FileDiffResultLists`](Models/FileDiffResultLists.cs). The property returns a single `DiffSummaryStatistics(AddedCount, RemovedCount, ModifiedCount, UnchangedCount, IgnoredCount)` snapshot instead of requiring callers to access five separate concurrent collections. Updated [`ReportGenerateService.WriteSummarySection()`](Services/ReportGenerateService.cs) to use `SummaryStatistics` instead of direct `.Count` accesses on each queue/dictionary. Added 4 unit tests to [`FileDiffResultListsTests`](FolderDiffIL4DotNet.Tests/Models/FileDiffResultListsTests.cs).
- Added `SpinnerFrames` to [`ConfigSettings`](Models/ConfigSettings.cs) — a `List<string>` where each element is one spinner animation frame, letting users replace the default four-frame `| / - \` rotation with any sequence including multi-character strings (e.g. block characters, emoji). [`ConsoleSpinner`](FolderDiffIL4DotNet.Core/Console/ConsoleSpinner.cs) changed its internal frame array from `char[]` to `string[]` to support multi-character frames; [`ProgressReportService`](Services/ProgressReportService.cs) and [`ReportGenerateService`](Services/ReportGenerateService.cs) now accept a `ConfigSettings` constructor parameter so they can read the configured frames at startup. Validation enforces at least one frame. Updated bilingual [README.md](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md).

#### Changed

- Consolidated the duplicate `"// MVID:"` literal into a single [`Constants.IL_MVID_LINE_PREFIX`](Common/Constants.cs) constant and removed the now-redundant `private const string MVID_PREFIX` definitions from both [`ReportGenerateService`](Services/ReportGenerateService.cs) and [`ILOutputService`](Services/ILOutputService.cs). No behaviour change; the string value is identical in both call sites and all references now use [`Constants.IL_MVID_LINE_PREFIX`](Common/Constants.cs).
- Improved timestamp display in [`diff_report.md`](doc/samples/diff_report.md): the format changed from `yyyy-MM-dd HH:mm:ss.fff zzz` (per-entry milliseconds and timezone offset) to `yyyy-MM-dd HH:mm:ss` (seconds only), the timezone offset is now written once in the report header as `Timestamps (timezone): +09:00` when `ShouldOutputFileTimestamps` is `true`, and each entry uses a bracket-and-arrow style — `[old → new]` for two timestamps and `[timestamp]` for a single timestamp — replacing the previous `<u>(updated_old: ..., updated_new: ...)</u>` markup. The `Warnings` section follows the same bracket-and-arrow format. For Unchanged files, two timestamps are now shown whenever old and new last-modified times differ, regardless of diff type (previously only `ILMatch` entries showed two timestamps). Updated bilingual [README.md](README.md) and related tests.

#### Added

- Filled four test gaps in [`FolderDiffIL4DotNet.Tests`](FolderDiffIL4DotNet.Tests/): (1) added `IsLikelyWindowsNetworkPath_ForwardSlashIpUncPath_ReturnsTrue` to [`FileSystemUtilityTests`](FolderDiffIL4DotNet.Tests/Core/IO/FileSystemUtilityTests.cs) and fixed [`FileSystemUtility.IsLikelyWindowsNetworkPath()`](FolderDiffIL4DotNet.Core/IO/FileSystemUtility.cs) to also detect `//`-format IP-based UNC paths (e.g. `//192.168.1.1/share`) as network paths on Windows; (2) added `ExecuteFolderDiffAsync_WhenEnumeratingFilesThrowsIOExceptionDueToSymlinkLoop_LogsAndRethrows` to [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs), verifying that an `IOException` raised during directory enumeration (e.g. an `ELOOP` error from a symlink cycle) is logged as an error and re-thrown; (3) added `ExecuteFolderDiffAsync_WhenNewFileDeletedBeforeComparison_ClassifiesAsRemovedWithWarning` (sequential and parallel variants) to [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) and changed [`FolderDiffService`](Services/FolderDiffService.cs) to catch [`FileNotFoundException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.filenotfoundexception?view=net-8.0) during per-file comparison, emit a warning, and classify the file as Removed rather than propagating the exception; (4) added `DisassembleAsync_AfterBlacklistTtlExpiry_RetriesToolAndSucceeds` to [`DotNetDisassembleServiceTests`](FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), verifying that a blacklisted disassembler tool whose 10-minute TTL has expired is removed from the blacklist and retried on the next call.
- Added three preflight checks to [`ProgramRunner.ValidateRunDirectories()`](ProgramRunner.cs) that run before configuration is loaded and all fail with exit code `2`: (1) **path-length check** — the constructed `Reports/<label>` path is validated against the OS limit (260 chars on Windows without long-path opt-in, 1024 on macOS, 4096 on Linux) via [`PathValidator.ValidateAbsolutePathLengthOrThrow()`](FolderDiffIL4DotNet.Core/IO/PathValidator.cs); (2) **disk-space check** — at least 100 MB of free space is verified on the target drive using `DriveInfo`, skipping best-effort when drive information is unavailable; (3) **write-permission check** — a temporary probe file is created and deleted in the `Reports/` parent directory to confirm write access before any output is produced. Added [`IOException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.ioexception?view=net-8.0) and [`UnauthorizedAccessException`](https://learn.microsoft.com/en-us/dotnet/api/system.unauthorizedaccessexception?view=net-8.0) catches to `TryValidateAndBuildRunArguments` so all three failures map cleanly to exit code `2`. Added 3 unit/integration tests to [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) and updated [README.md](README.md).
- Added `ShouldIncludeILCacheStatsInReport` (default `false`) to [`ConfigSettings`](Models/ConfigSettings.cs). When `true` and the IL cache is active, [`ReportGenerateService`](Services/ReportGenerateService.cs) appends an `IL Cache Stats` section between `Summary` and `Warnings` in [`diff_report.md`](doc/samples/diff_report.md), showing hits, misses, hit-rate, stores, evicted, and expired counts. Also added `_internalMisses` tracking to [`ILCache`](Services/Caching/ILCache.cs) (miss counter now incremented on full cache miss), a `GetReportStats()` method, and the `ILCacheReportStats` sealed record. Added 3 unit tests to [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) and updated [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs), [README.md](README.md), and [CHANGELOG.md](CHANGELOG.md).
- Expanded CLI options: `--help`/`-h` prints usage and exits with code `0` before any logger initialization; `--version` prints the application version and exits with code `0`; `--config <path>` loads a config file from an arbitrary path instead of the default `<exe>/[`config.json`](config.json)`; `--threads <N>` overrides `MaxParallelism` in [`ConfigSettings`](Models/ConfigSettings.cs) for the current run; `--no-il-cache` forces `EnableILCache = false` for the current run; `--skip-il` skips IL decompilation and IL diff entirely for .NET assemblies (new `SkipIL` property in [`ConfigSettings`](Models/ConfigSettings.cs), also respected by [`FileDiffService`](Services/FileDiffService.cs)); `--no-timestamp-warnings` suppresses timestamp-regression warnings. Unknown flags now produce exit code `2` with a descriptive message instead of silently being ignored. [`ConfigService.LoadConfigAsync()`](Services/ConfigService.cs) now accepts an optional `configFilePath` parameter. Added [`CliOptionsTests`](FolderDiffIL4DotNet.Tests/CliOptionsTests.cs) with 21 parser unit-test cases, and new integration tests in [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) and [`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs).
- Added [`ConfigSettings.Validate()`](Models/ConfigSettings.cs) and the companion `ConfigValidationResult` class; [`ConfigService.LoadConfigAsync()`](Services/ConfigService.cs) now calls `Validate()` immediately after deserialization and throws [`InvalidDataException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.invaliddataexception?view=net-8.0) listing all invalid settings when validation fails, so misconfigured runs are caught at startup with a clear error message instead of failing silently or causing undefined behavior later. Validated constraints: `MaxLogGenerations >= 1`; `TextDiffParallelThresholdKilobytes >= 1`; `TextDiffChunkSizeKilobytes >= 1`; and `TextDiffChunkSizeKilobytes < TextDiffParallelThresholdKilobytes`. Added validation unit tests to [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) (7 cases) and validation integration tests to [`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) (5 cases).

#### Fixed

- Fixed three CI pipeline failures: applied `PATH`/`HOME` isolation to `PrefetchIlCacheAsync_WhenSeededCacheExists_IncrementsHitCounter` in [`DotNetDisassembleServiceTests`](FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) so that the real [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) installed on the CI runner no longer overwrites the pre-seeded version cache entry; added `fetch-depth: 0` to the Checkout step in [`.github/workflows/codeql.yml`](.github/workflows/codeql.yml) so [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) can compute version height from the full commit history during the `csharp` autobuild; and added `continue-on-error: true` to the Analyze step to tolerate the SARIF upload rejection that occurs when the repository's GitHub Default Setup code scanning is also active for the `actions` language.
- Added targeted regression tests in [`FileDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs) and [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) to cover the partial-parallelism-reduction path in `DetermineEffectiveTextDiffParallelism`, the duplicate-path skip in `EnumerateDistinctPrecomputeBatches`, and the zero-batch-size fallback in `GetEffectiveIlPrecomputeBatchSize`; these three branches introduced in commit `e61ba70` were previously untested and caused branch coverage to drop below the `71%` CI threshold enforced in [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml), and refreshed the bilingual docs with the latest passing test count (`251`).

#### Changed

- Changed the elapsed-time display format in [`ProgramRunner.FormatElapsedTime()`](ProgramRunner.cs) from `HH:MM:SS.mmm` (e.g. `00:05:30.123`) to `{h}h {m}m {s.d}s` (e.g. `0h 5m 30.1s`), which disambiguates hours, minutes, and seconds at a glance. Seconds are shown with one decimal place (tenths, truncated). `FormatElapsedTime` is now `internal static` to allow direct unit testing; added 7 parametrized cases to [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs). Updated the elapsed-time example in [README.md](README.md).
- Replaced the Figgle-based banner in [`ConsoleBanner`](FolderDiffIL4DotNet.Core/Console/ConsoleBanner.cs) with a hardcoded ANSI Shadow Unicode block-character string, and removed the `Figgle` NuGet dependency from [`FolderDiffIL4DotNet.Core`](FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj).
- Added `TextDiffParallelMemoryLimitMegabytes` and `ILPrecomputeBatchSize` to [`ConfigSettings`](Models/ConfigSettings.cs), so large local text comparison can clamp chunk-parallel workers based on a configurable buffer budget while logging current managed-heap usage, and IL-related precompute now runs in batches instead of building one extra all-files list for very large trees; added regression coverage in [`FileDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs), [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs), and [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs), and refreshed the bilingual docs with the latest passing test count (`248`).
- Replaced the old top-level catch-all exit-code flattening in [`ProgramRunner`](ProgramRunner.cs) with typed phase results, so invalid arguments/input paths now return `2`, configuration load/parse failures return `3`, diff/report execution failures return `4`, and exit code `1` is reserved for unexpected internal errors; added regression coverage in [`ProgramTests`](FolderDiffIL4DotNet.Tests/ProgramTests.cs) and [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs), and refreshed the bilingual docs accordingly.
- Added repository-level release and security automation with [`.github/workflows/release.yml`](.github/workflows/release.yml), [`.github/workflows/codeql.yml`](.github/workflows/codeql.yml), and [`.github/dependabot.yml`](.github/dependabot.yml); added configuration regression coverage in [`CiAutomationConfigurationTests`](FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs); and refreshed the bilingual docs to distinguish the already-present coverage gate from the newly added GitHub Releases / CodeQL / Dependabot automation.
- Added a real-disassembler E2E test in [`RealDisassemblerE2ETests`](FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs), expanded filesystem-backed coverage for multi-megabyte text comparison and symlinked files in [`FileDiffServiceTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs) and [`FolderDiffServiceTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs), enforced CI total coverage gates at `73%` line / `71%` branch in [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml), and refreshed the bilingual docs with the latest passing test count (`240`) plus measured coverage (`74.04%` line / `71.63%` branch).
- Split the reusable helper layer out of `FolderDiffIL4DotNet` into the new [`FolderDiffIL4DotNet.Core`](FolderDiffIL4DotNet.Core/) project, reorganized the former `Utils` types into `Console` / `Diagnostics` / `IO` / `Text` namespaces, added architecture regression coverage in [`CoreSeparationTests`](FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs), and refreshed the bilingual docs plus latest passing test count (`237`).
- Centralized repeated byte-size and timestamp format literals in [`Common/Constants.cs`](Common/Constants.cs) via [`Constants.BYTES_PER_KILOBYTE`](Common/Constants.cs), [`Constants.TIMESTAMP_WITH_TIME_ZONE_FORMAT`](Common/Constants.cs), [`Constants.LOG_ENTRY_TIMESTAMP_FORMAT`](Common/Constants.cs), and [`Constants.LOG_FILE_DATE_FORMAT`](Common/Constants.cs); switched logging/timestamp helpers to the shared definitions, documented the rationale for the internal IL cache defaults used by [`ProgramRunner`](ProgramRunner.cs), and added regression coverage for the shared formats and cache-default wiring.
- Replaced broad `catch (Exception)` blocks in [`FolderDiffService`](Services/FolderDiffService.cs) and [`FileDiffService`](Services/FileDiffService.cs) with expected runtime exception handling plus separate unexpected-error logging, clarified the best-effort versus fatal exception policy around precompute/cache-cleanup/report-protection paths, and refreshed the bilingual docs and regression tests.
- Refactored [`ProgramRunner.RunAsync()`](ProgramRunner.cs) into phase-oriented helpers for logger startup, argument validation, configuration/runtime preparation, diff execution, report generation, and exit prompting, reducing the main orchestration method without changing observable behavior.
- Split OS-specific network-path detection branches out of [`FileSystemUtility.IsLikelyNetworkPath()`](FolderDiffIL4DotNet.Core/IO/FileSystemUtility.cs) and extracted report-write/protection helpers from [`ReportGenerateService.GenerateDiffReport()`](Services/ReportGenerateService.cs), improving readability while keeping behavior stable.
- Added focused regression coverage in [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) for validation-before-config-loading ordering, added a null-input case to [`FileSystemUtilityTests`](FolderDiffIL4DotNet.Tests/Core/IO/FileSystemUtilityTests.cs), and updated the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md) to reflect the refactor and latest passing test count (`230`).
- Refactored discovery filtering and auto-parallelism policy out of [`FolderDiffService`](Services/FolderDiffService.cs) into [`FolderDiffExecutionStrategy`](Services/FolderDiffExecutionStrategy.cs), reducing orchestration sprawl while keeping runtime behavior stable.
- Added focused unit coverage in [`FolderDiffExecutionStrategyTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffExecutionStrategyTests.cs) for ignored-file filtering, relative-path union counting, and network-aware auto-parallelism, and updated the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md) to reflect the new boundary and latest passing test count (`230`).
- Refactored [`ILCache`](Services/Caching/ILCache.cs) into a thinner coordinator backed by [`ILMemoryCache`](Services/Caching/ILMemoryCache.cs) and [`ILDiskCache`](Services/Caching/ILDiskCache.cs), keeping the public API stable while separating in-memory retention from disk persistence/quota handling.
- Added regression coverage in [`ILCacheTests`](FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs) for same-key updates at memory-capacity limits and for coordinated disk cleanup when LRU eviction removes an entry.
- Updated the [developer guide](doc/DEVELOPER_GUIDE.md) and [testing guide](doc/TESTING_GUIDE.md) to describe the split cache internals and reflect the latest passing test count (`230`).
- Replaced eager `Directory.GetFiles(...)` usage in [`FolderDiffService`](Services/FolderDiffService.cs) with lazy `Directory.EnumerateFiles(...)` behind [`IFileSystemService`](Services/IFileSystemService.cs), reducing discovery-side allocations for large trees and network shares while keeping folder-diff behavior unchanged.
- Added unit-test coverage for streaming file discovery in [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) and updated the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md) accordingly.
- Fixed missing link to [Developer Guide](doc/DEVELOPER_GUIDE.md).
- Clarified the `// MVID:` ignore rationale in the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and report note output, while tightening the English/Japanese wording so the high-level behavior stays aligned across both locales.
- Expanded documentation link coverage across the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md), and aligned locale-selectable external URLs to English/Japanese contexts.
- Added stable bilingual document anchors across the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), [testing guide](doc/TESTING_GUIDE.md), and [documentation index](index.md), and redirected config-related links to the README configuration-table sections so navigation lands reliably in common Markdown renderers.
- Added direct `.cs` source links for class references across the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md), excluding classes that would be split across partial definitions.
- Moved the report-level `MD5Mismatch` warning from `Summary` into the final `Warnings` section, ordered it before timestamp-regression warnings, and refreshed the related docs and regression tests.
- Introduced DocFX-based API documentation generation, added a documentation-site build path, and wired CI to publish the generated `DocumentationSite` artifact.
- Added [`IFileSystemService`](Services/IFileSystemService.cs) and [`IFileComparisonService`](Services/IFileComparisonService.cs) as low-level seams for folder discovery/output I/O and per-file comparison I/O, making permission and disk-failure paths unit-testable without changing production behavior.
- Split folder/file diff coverage more clearly into lightweight unit tests and temp-directory-backed integration tests, and expanded automated coverage for hash failures, IL-output failures, and large-text comparison paths.
- Updated the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md) in both English and Japanese to document the new service seams, test boundaries, and the latest passing test count (`219`).
- Moved aggregated `MD5Mismatch` console warnings into [`ProgramRunner`](ProgramRunner.cs), kept [`ReportGenerateService`](Services/ReportGenerateService.cs) report-only, and updated related docs and automated tests.
- Replaced one-off [`string.Format(...)`](https://learn.microsoft.com/en-us/dotnet/api/system.string.format?view=net-8.0) usage with interpolated strings, removed broad `#region` usage, and deleted now-unused format/message constants.
- Updated the developer and testing guides to reflect the current source-style expectations and latest passing test count.
- Made `.NET` executable detection distinguish `NotDotNetExecutable` from detection failure, log a warning for non-fatal detection failures, and let chunk-parallel text-diff exceptions bubble to the existing sequential fallback path instead of silently returning `false`.
- Enabled the `CA1031` analyzer for production code so broad exception catches are surfaced during normal builds, while excluding test cleanup code from the warning.
- Removed generic `throw new Exception(..., ex)` wrapping from [`FileSystemUtility`](FolderDiffIL4DotNet.Core/IO/FileSystemUtility.cs), using [`Exception`](https://learn.microsoft.com/en-us/dotnet/api/system.exception?view=net-8.0) only as the referenced outer type name here, so original exception types and stack traces are preserved, and added regression coverage plus bilingual guide updates.
- Moved configuration defaults into [`ConfigSettings`](Models/ConfigSettings.cs), normalized missing or `null` config values back to code-defined defaults, simplified the shipped [`config.json`](config.json) to an override-only shape, and refreshed bilingual docs plus config-focused tests.

### [1.2.2] - 2026-03-14

#### Added

- Added configurable warnings when a file in `new` has an older last-modified timestamp than the matching file in `old`, including console output before exit and a final `Warnings` section in [`diff_report.md`](doc/samples/diff_report.md).
- Added coverlet-based coverage collection in CI and expanded automated tests for [`Program`](Program.cs), logging, progress reporting, file-system helpers, and text-diff fallback paths.

#### Changed

- Extended console color emphasis so warning messages are also highlighted in yellow for consistency with the final success/failure messages.
- Updated configuration samples, documentation, and automated tests for timestamp-regression warnings.
- Reorganized runtime composition around [`ProgramRunner`](ProgramRunner.cs), [`DiffExecutionContext`](Services/DiffExecutionContext.cs), and interface-based services to improve diff-pipeline testability and reduce direct static-state coupling.
- Split the [developer guide](doc/DEVELOPER_GUIDE.md) and [testing guide](doc/TESTING_GUIDE.md) into dedicated documents and expanded the [README](README.md) with clearer installation examples and comparison-flow documentation.
- Expanded the [developer guide](doc/DEVELOPER_GUIDE.md) with execution lifecycle, DI boundaries, runtime-mode notes, Mermaid diagrams, and a clearer documentation map across [README](README.md) and [testing guide](doc/TESTING_GUIDE.md).

#### Fixed

- Limited exception handling during network-share detection and added warning logs when parallel text comparison falls back to sequential mode.

### [1.2.1] - 2026-03-09

#### Added

- Added configuration keys for text-diff parallel threshold and chunk size in KiB.
- Added focused tests for [`FolderDiffService`](Services/FolderDiffService.cs) and strengthened report-generation coverage.

#### Changed

- Standardized guard clauses on [`ArgumentNullException.ThrowIfNull`](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception.throwifnull?view=net-8.0).

#### Fixed

- Made [`FileDiffResultLists`](Models/FileDiffResultLists.cs) thread-safe and added `ResetAll` to reliably clear shared result state between runs.

### [1.2.0] - 2026-03-07

#### Added

- Added an optional IL-comparison filter that ignores lines containing configured substrings and reflects the behavior in report output.

#### Changed

- Changed the default IL disk-cache setting from unlimited (`0`) to 1000 files and 512 MB.

### [1.1.9] - 2026-03-07

#### Added

- Added a dedicated test project, CI test execution guidance, and broader automated coverage for cache, disassembler, and reporting behavior.
- Added an ASCII-art application banner after successful command-line validation.
- Added color emphasis only to the final success or failure console message.

#### Changed

- Unified [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) and [`dotnet ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) handling, tightened disassembler identity consistency checks, and improved disassembler reporting details.
- Refactored utility helpers into single-responsibility classes and split large methods in [`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) and [`FolderDiffService`](Services/FolderDiffService.cs).
- Applied smaller internal cleanups, including replacing `HashSet.Union` result creation with `UnionWith`.

#### Fixed

- Fixed report output so it records the actual disassembler used instead of listing every available tool.
- Prevented mixed disassembler usage and cache contamination during IL comparison.
- Removed redundant sequential re-comparison for small text files.
- Improved regression handling, report wording, and exit-code behavior around IL comparison.

### [1.1.8] - 2026-01-24

#### Added

- Added the actual reverse-engineering tool name and version to [`diff_report.md`](doc/samples/diff_report.md).

#### Changed

- Redesigned folder-diff progress display to show a label, spinner, and progress bar together, then refined the presentation.

### [1.1.7] - 2025-12-30

#### Added

- Added `README.en.md`, which was later consolidated back into `README.md` during the documentation restructure.
- Added license information to the documentation.

#### Changed

- Replaced spinner-only feedback with a coordinated progress-bar presentation for long-running work.

### [1.1.6] - 2025-12-11

#### Added

- Added the executing computer name to [`diff_report.md`](doc/samples/diff_report.md).

#### Changed

- Refactored constant definitions and cache internals for the disassembler and IL cache.

#### Fixed

- Removed redundant internal processing paths.

### [1.1.5] - 2025-12-08

#### Added

- Added spinner-based feedback for long-running operations.

#### Changed

- Performed broad internal refactoring and refreshed README wording.

### [1.1.4] - 2025-12-07

#### Added

- Added GitHub Actions automation for .NET builds.

### [1.1.3] - 2025-11-29

#### Added

- Added configuration and report support for listing ignored files.

### [1.1.2] - 2025-11-16

#### Added

- Added report warnings when any file is classified as `MD5Mismatch`.

#### Changed

- Documented .NET executable detection behavior in the README.

#### Fixed

- Reduced the initial silent period before the first progress update.
- Updated `.NET` executable detection to support both PE32 and PE32+ binaries.
- Included additional minor corrections shipped between `v1.1.1` and `v1.1.2`.

### [1.1.1] - 2025-09-14

#### Added

- Added a configuration option to include or suppress file timestamps in [`diff_report.md`](doc/samples/diff_report.md).

### [1.1.0] - 2025-09-12

#### Added

- Added network-share optimization support.
- Added more file extensions that are treated as text during comparison.

#### Changed

- Made [`IgnoredExtensions`](README.md#configuration-table-en) matching case-insensitive.

#### Removed

- Removed the unused `ShouldSkipPromptOnExit` configuration entry.

#### Fixed

- Corrected [`TextFileExtensions`](README.md#configuration-table-en) configuration values.
- Corrected README mistakes.
- Reduced early runtime silence before progress output begins.

### [1.0.1] - 2025-08-30

#### Added

- Added more file extensions to the text-comparison list.

#### Removed

- Removed generation of `ILlog.md` and `ILlog.html`.

### [1.0.0] - 2025-08-17

#### Added

- Initial release of `FolderDiffIL4DotNet` with folder comparison, Markdown report generation, IL-based `.NET` assembly comparison, caching, configuration loading, progress reporting, and logging.

## 日本語

このファイルは主要な変更を記録するためのものです。

前半は英語、後半は日本語です。
形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/)、バージョン管理は [Semantic Versioning](https://semver.org/lang/ja/) に準拠します。

### [Unreleased]

#### 修正

- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) の HTML レポートにおけるインライン差分の番号表示を修正しました。`Show diff` / `Show IL diff` の前や、インライン差分スキップ文言に表示される `#N` が内部の 0 始まりインデックスではなく、左端 `#` 列と同じ 1 始まりの行番号になるよう統一しました。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) にテスト `GenerateDiffReportHtml_InlineDiffSummary_UsesSameOneBasedNumberAsLeftmostColumn` を追加し、[README.md](README.md) と [テストガイド](doc/TESTING_GUIDE.md) も更新しました。

### [1.3.0] - 2026-03-17

#### 変更

- `InlineDiffMaxOutputLines` と `InlineDiffMaxDiffLines` の既定値を `500`/`1000` から **`10000`** に引き上げました（[`ConfigSettings`](Models/ConfigSettings.cs)・[`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs)・[`TextDiffer`](FolderDiffIL4DotNet.Core/Text/TextDiffer.cs)）。[`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs)、日英 [README.md](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md) を更新しました。

- [`TextDiffer`](FolderDiffIL4DotNet.Core/Text/TextDiffer.cs) の差分アルゴリズムを O(N×M) の LCS から **Myers diff**（O(D² + N + M) 時間・O(D²) 空間、D = 編集距離）に置き換えました。従来の `m × n > 4 000 000` セル数ガードを廃止し、新しい設定項目 [`InlineDiffMaxEditDistance`](README.md#configuration-table-ja)（既定値 `4 000`、挿入行数 + 削除行数の合計上限）に置き換えました。差分が少なければ数百万行のファイルもインライン差分を表示できます（例: 237 万行の IL ファイルを 20 行の差分で比較した場合、ミリ秒以内に完了）。[`TextDifferTests`](FolderDiffIL4DotNet.Tests/Core/Text/TextDifferTests.cs) を更新: `Compute_InputExceedsLcsLimit_ReturnsTruncatedMessage` を `Compute_EditDistanceExceedsLimit_ReturnsTruncatedMessage` に置換し、`Compute_LargeFilesSmallEditDistance_ProducesCorrectDiff` と `Compute_VeryLargeFilesWithTinyDiff_ProducesInlineDiff` を追加しました。

- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) の HTML レポート UX を改善しました。ダウンロードボタンのアイコンを `⇩` から `⤓` に変更し、ラベルを「Download as reviewed」に変更。レビュー済みファイルのバナー表示を `"Reviewed: <タイムスタンプ> — read-only"` に変更。Added/Removed のセクション見出し・列ヘッダ背景色を GitHub diff パレット（緑 `#22863a` / 赤 `#b31d28` / Added 背景 `#e6ffed` / Removed 背景 `#ffeef0`）に統一。`No` 列の幅を `3.2em` に拡大し、最大 999,999 件まで対応。`Diff Reason` 列が空のセルに幽霊 `<code>` 要素が残る問題を修正。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。
- HTML レポートの Timestamp 列がリサイズ時にガタガタする問題を修正しました。主要ファイルリストテーブルを `width: auto` から `table-layout: fixed; width: 1px` に変更し、`syncTableWidths()` JavaScript 関数を追加。CSS カスタムプロパティ（`--col-reason-w`、`--col-notes-w`、`--col-path-w`、`--col-diff-w`）の和として各テーブルの明示的なピクセル幅を設定し、`DOMContentLoaded` とリサイズ操作後に呼び出します。列ヘッダのテキストを `span.th-label { display: block; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }` でラップし、ヘッダ内容が確実にクリップされるようにしました。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。
- レビュー済みモードのチェックボックスがグレーアウトして見にくい問題を修正しました。`cb.disabled = true` を `cb.style.pointerEvents = 'none'; cb.style.cursor = 'default';` に変更し、ブラウザ固有のグレー描画を回避してアクセントカラーを維持するようにしました。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。
- 「Download as reviewed」が現在の列幅をデフォルトとして reviewed ファイルに焼き込むようになりました。`downloadReviewed()` が 5 つの列幅 CSS カスタムプロパティの現在の実効値を取得し、エクスポートした HTML の `:root` CSS ルールをその値で置き換えるとともに `<html>` 要素のインライン `style` 属性を削除します。これにより、reviewed スナップショットはサインオフ時の列幅レイアウトで開くようになります。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。
- 「Clear all」実行時に列幅をデフォルトに戻し、すべてのインライン差分 `<details>` を閉じるようになりました。`clearAll()` が CSS カスタムプロパティを削除、`syncTableWidths()` を呼び出し、全 `<details>` の `open` 属性を削除します。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。
- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) の `td.col-reason`・`td.col-ts`・`td.col-diff` のボディセルに `text-align: center` を追加しました（`col-diff` は Ignored Files テーブルでは「Location」（`old`/`new`/`old/new`）、他のテーブルでは差分タイプ（`ILMismatch`・`TextMismatch` など）を表示する列です）。`td.col-path`（File Path）は左揃えのまま。列ヘッダおよび `td.col-notes` は変更なし。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) のテスト `GenerateDiffReportHtml_BodyCells_ColReasonPathTs_HaveCenterAlignment` を合わせて更新しました。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。
- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) の `.reviewed-banner` テキスト色を `#2d7a2d`（緑）から `#1f2328`（ほぼ黒）に変更し、reviewed ファイルのタイムスタンプバナーを視覚的に中立な表示にしました。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。

#### 変更（続き）

- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) の HTML レポートテーブルの列構成を変更しました。Timestamp 列を `22em` から `16em` に縮小。Diff Reason 列を `20em` から `9em` に縮小し、逆アセンブラのラベル文字列を除いた差分タイプのみ（`ILMismatch`、`TextMismatch` など）を表示するよう変更。最右端に 8 列目 **Disassembler** 列（`28em`、リサイズ可能）を新設し、各行の逆アセンブララベルおよびバージョン文字列を表示。JavaScript（`colVarNames`、`clearAll`、`syncTableWidths`）および CSS（`:root` カスタムプロパティ、`col.col-*-g`、`td.col-*`）を対応する値に更新しました。

- [`ConfigSettings`](Models/ConfigSettings.cs) の `InlineDiffMaxInputLines` を `InlineDiffMaxDiffLines`（既定値 `1000`）に置き換えました。従来の設定は差分計算の*前*に入力ファイルの行数を閾値と比較していましたが、インライン差分の HTML 表示は変更行のみを出力するため、入力行数は適切な指標ではありませんでした。新しい設定は `TextDiffer.Compute()` による差分計算の*後*に差分出力行数を確認し、閾値を超えた場合にインライン差分の表示をスキップします。[`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs)・[`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs)・[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs)・[README.md](README.md)・[開発者ガイド](doc/DEVELOPER_GUIDE.md)・[テストガイド](doc/TESTING_GUIDE.md) を日英両言語で更新しました。

#### 修正

- ILMismatch のインライン差分が HTML レポートに一切表示されなかった問題を修正しました: [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) が `ILCache.TryGetILAsync` を正規化済みラベル（例: `ildasm (version: 1.0.0)`）で呼び出していましたが、書き込み時のラベル（例: `ildasm MyAssembly.dll (version: 1.0.0)`）と一致しないため常に `null` が返り、インライン差分がサイレントにスキップされていました。[`ILTextOutputService`](Services/ILOutput/ILTextOutputService.cs) が `Reports/<label>/IL/old` と `Reports/<label>/IL/new` に書き出した `*_IL.txt` ファイルを直接読み込む方式に変更し、`ShouldOutputILText` が `true`（既定値）のときに正しくインライン差分を表示するよう修正しました。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) にテスト `GenerateDiffReportHtml_ILMismatch_WithILTextFiles_ShowsInlineDiff` を追加しました。

- **Unchanged ファイルに対して更新日時逆転警告が出ていた問題を修正しました。** [`FolderDiffService`](Services/FolderDiffService.cs) が `RecordNewFileTimestampOlderThanOldWarningIfNeeded` を `Modified` 判定（`FilesAreEqualAsync` が `false` を返した後）のみで呼び出すよう変更しました。従来はコンテンツ比較の前にチェックしていたため、`new` 側の更新日時が古くても内容が同一の Unchanged ファイルが誤って `Warnings` セクションに出力されていました。[`ReportGenerateService`](Services/ReportGenerateService.cs)・[`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs)・[`ProgramRunner`](ProgramRunner.cs) の警告文を「**modified** files」と明記するよう更新しました。[`FileDiffResultLists`](Models/FileDiffResultLists.cs) および [`FileTimestampRegressionWarning`](Models/FileTimestampRegressionWarning.cs) の XML ドキュメントコメントも「Modified と判定されたファイル」に修正しました。[`FolderDiffServiceTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs) では、`ExecuteFolderDiffAsync_WhenNewFileTimestampIsOlder_RecordsWarning` を内容が異なるファイル（Modified に分類される）で書き直し `ExecuteFolderDiffAsync_WhenModifiedFileTimestampIsOlder_RecordsWarning` に改名、Unchanged ファイルで警告が出ないことを確認する `ExecuteFolderDiffAsync_WhenUnchangedFileTimestampIsOlder_DoesNotRecordWarning` を新規追加、`ExecuteFolderDiffAsync_WhenTimestampWarningDisabled_DoesNotRecordWarning` も内容が異なるファイルを使用するよう更新しました。[`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) と [`ProgramTests`](FolderDiffIL4DotNet.Tests/ProgramTests.cs) の文言アサーションも新しいメッセージに合わせて更新しました。[README.md](README.md)・[開発者ガイド](doc/DEVELOPER_GUIDE.md)・[テストガイド](doc/TESTING_GUIDE.md) を日英両言語で更新しました。

- [`config.json`](config.json) の解析エラー出力を改善しました。[`ConfigService`](Services/ConfigService.cs) が `JsonException` をキャッチした際、内部の例外から行番号・バイト位置を取得してエラーメッセージに付加し、最後のプロパティや配列要素の後のトレイリングカンマが標準 JSON では許可されないことを示すヒントを表示するようになりました。エラーは実行ログへ書き込まれ、コンソールには赤字で表示され、終了コード `3` で終了します。[`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) にオブジェクト末尾カンマ・配列末尾カンマ・複数行 JSON での行番号検証を行う 3 件のユニットテストを追加しました。
- Windows でバナー文字が `?` になる問題を修正しました。[`Program.cs`](Program.cs) の `Main()` 先頭（出力より前）で [`Console.OutputEncoding`](https://learn.microsoft.com/ja-jp/DOTNET/api/system.console.outputencoding?view=net-8.0) = `Encoding.UTF8` を設定し、Windows がデフォルトで使用する OEM コードページ（CP932/CP437）を上書きするようにしました。Linux / macOS ではコンソールがすでに UTF-8 のためこの変更は影響しません。
- 直近の HTML レポート変更により発生した CI パイプラインのテスト失敗 3 件を修正しました（[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs)）。(1) `GenerateDiffReportHtml_ILMismatch_NoInlineDiff` と `GenerateDiffReportHtml_TextMismatch_EnableInlineDiffFalse_NoDetailsElement` は `DoesNotContain("<details")` を検証していましたが、JS コメントに `<details` リテラルが含まれていたため、当該コメントを書き換えて修正。(2) `GenerateDiffReportHtml_Md5MismatchWarning_AppearsInWarningsSection` は見出しテキストの直前に `<span>` が挿入されたことで完全一致が崩れていたため、2 つの独立した `Contains` 検証に分割して修正。(3) 色定数を `#2d7a2d`/`#b00020` から `#22863a`/`#b31d28`（GitHub diff パレット）に変更したことに伴い、色の検証アサーションを更新しました。
- 編集距離超過スキップ時にインライン差分が `+0 / -0` と表示されてさも差異なしに見える問題を修正しました。`TextDiffer.Compute` が Truncated 1 行のみを返す場合（編集距離 `D > InlineDiffMaxEditDistance` のとき）、[`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) が `<details>` 展開矢印なしのプレーンな `diff-skipped` 行を直接表示するようになりました。これは `InlineDiffMaxDiffLines` 超過ケースの挙動と一致します。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) のテスト名を `GenerateDiffReportHtml_TextMismatch_EditDistanceTooLarge_ShowsSkippedMessageWithoutExpandArrow` に変更しました。
- `[ + ] Added`・`[ - ] Removed`・`[ * ] Modified`・`[ ! ] Timestamps Regressed` のセクション見出し文字色を復元しました（緑 `#22863a` / 赤 `#b31d28` / 青 `#0051c3`）。`COLOR_ADDED`・`COLOR_REMOVED`・`COLOR_MODIFIED` 定数を再追加しました。[CHANGELOG.md](CHANGELOG.md) と [README.md](README.md)（日英）の「プレーンな黒文字」という記述を削除しました。

#### 変更

- インライン差分の `<summary>` ラベルにファイル行番号プレフィックスを追加しました: `#1 Show diff (+N / -M)` / `#1 Show IL diff (+N / -M)`（従来は `Show diff` / `Show IL diff`）。どのファイルの差分かを、すぐ上の行を見なくても識別できるようになります。
- サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を現在の本番出力に合わせて更新しました: 8 列レイアウト（Disassembler 列 `col.col-disasm-g` / `td.col-disasm` CSS、`--col-disasm-w: 28em` CSS 変数、リサイズ可能な `Disassembler` ヘッダを追加）; `--col-diff-w` を `20em` から `9em` へ修正; Timestamp 列幅を `22em` から `16em` へ修正; すべての `colspan="7"` を `colspan="8"` へ変更; diff-summary ラベルを `#N Show diff` / `#N Show IL diff` 形式へ更新; Diff Reason セルを分割（例: `ILMismatch` + [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) ` (version: 0.12.2)` → `ILMismatch` + Disassembler セル）; JavaScript `colVarNames`・`clearAll` 配列と `syncTableWidths` 計算式を更新。編集距離超過スキップ（`src/BigSchema.cs`）と差分行数制限超過（`src/LargeConfig.xml`）のサンプル行を 2 件追加しました。[`doc/samples/diff_report.md`](doc/samples/diff_report.md) に対応する 2 件の Modified エントリとファイル件数（Modified 8、Compared 17）を反映しました。
- [`doc/DEVELOPER_GUIDE.md`](doc/DEVELOPER_GUIDE.md)（日英）に「インライン差分スキップの挙動」セクションを追加しました: 編集距離超過 / `InlineDiffMaxOutputLines` 途中打ち切り / `InlineDiffMaxDiffLines` 計算後超過の 3 トリガー・条件・HTML 表示の違い（`<details>` あり vs. プレーン行）を説明します。

#### 追加

- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) の HTML レポート UX をさらに改善しました。`<h1>` の表示テキストを「Folder Diff Report」に変更。frosted-glass コントロールバーの背景透明度を `rgba(255,255,255,0.45)` に上げてより透過感のある外観に。`h1` のフォントサイズを `2.0rem` へ拡大し、`Summary` / `IL Cache Stats` / `Warnings` の各セクション見出しに専用の `h2.section-heading`（`1.55rem`）スタイルを追加してファイルリスト見出しと差別化。`[ ! ] Modified Files — Timestamps Regressed` を `h3` から `h2` へ昇格させ `[ * ] Modified Files` と同一スタイルに。stat-table の数値フォントをボディフォントに変更（等幅フォント解除）。stat-table に左マージン `1.2em` を追加してインデント表示。ILMismatch のインライン差分サマリーラベルを「Show IL diff」に変更（TextMismatch は「Show diff」のまま）。diff サマリーの `+N` / `-N` をそれぞれ緑・赤の `diff-added-cnt` / `diff-removed-cnt` スパンで色付け。diff-row の `<tr>`（`<details>` を含む折り畳み行）に薄い青背景（`#eef5ff`）を適用。IL 無視文字列注記で値を `<code>` タグで囲むのをやめ、プレーンテキストで表示。Warnings リスト項目の `WARNING:` テキストを削除し、黄色の `⚠` アイコン（`warn-icon`）のみ表示に変更。OK Reason / Notes / File Path の列幅を CSS カスタムプロパティ（`--col-reason-w`、`--col-notes-w`、`--col-path-w`）と `<colgroup>` で管理し、全テーブル間で列幅を同期。これら 3 列のヘッダにドラッグ可能なリサイズハンドル（`initColResize` JS）を追加し、1 つのヘッダをドラッグするだけですべてのテーブルの同一列が同時にリサイズされる仕組みを実装。レビュー済み HTML ダウンロードでは、コントロールバーを削除するのではなく緑色の「🔒 Reviewed — read-only」バナー（`reviewed-banner`）に置き換え、レビュー済みスナップショットであることを視覚的に明示するよう変更。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) の Warnings セクション検証を新しい `h2.section-heading` 構造に合わせて更新しました。
- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) を全面的に刷新し、HTML レポートに多数の改善を施しました。ページタイトルを `diff_report` 固定に変更。コントロールバーをビューポート全幅にフィットするスティッキー frosted-glass スタイル（`backdrop-filter: blur`）に刷新。ボタンを Apple ミニマリスト風のピルボタン（同一の高さ、`display:inline-flex`、`border-radius:980px`）に変更。自動保存タイムスタンプを `YYYY-MM-DD HH:mm:ss` 形式に統一。Old/New フォルダパスをプレーンテキスト（`<code>` 不使用）で表示。MVID 注記を通常の `<li>` メタ項目として表示。IL contains-ignore 注記を HTML ヘッダに追加。Legend を `<ul class="meta">` 内のネストリストへ移動。5 種のファイルテーブルすべてを統一 8 列レイアウト（`# | ✓ | OK Reason | Notes | File Path | Timestamp | Diff Reason | Disassembler`）にそろえ、行番号・セルプレースホルダなし・OK Reason/Notes/Path/Disassembler 列をリサイズ可能に。Added/Removed/Modified テーブルヘッダはそれぞれ淡い緑・赤・青の背景（黒文字）に。Ignored/Unchanged はニュートラルなヘッダに。Summary と IL Cache Stats を `<table class="stat-table">` の右揃え数値テーブルで表示。Warnings セクションのタイムスタンプ逆転ファイルを `[ ! ] Modified Files — Timestamps Regressed (N)` 見出し付きのテーブルとして表示。インライン差分の変更点: `InlineDiffContextLines` の既定値を 3 から 0 へ変更（差分行のみ表示、前後コンテキストなし）。行省略時にハンクセパレーター行を表示。ILMismatch エントリについて、`ShouldOutputILText` が `true`（既定値）のとき `Reports/<label>/IL/old` と `Reports/<label>/IL/new` に書き出された `*_IL.txt` ファイルが存在する場合はインライン差分を表示。レビュー済み HTML ダウンロードの改善: 出力ファイル名を `diff_report_{yyyyMMdd}_reviewed.html` に変更。ページタイトルを `diff_report_{yyyyMMdd}_reviewed` に変更。コントロールバー（`<!--CTRL-->…<!--/CTRL-->`）をダウンロード版から削除。レビュー済みコピーでは全チェックボックスを `disabled`、テキスト入力を `readOnly`（テキスト選択・コピーは可能）に設定。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) と [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) を新しい色値（`#2d7a2d`、`#b00020`）・stat-table HTML 構造・`InlineDiffContextLines` 既定値 `0` に合わせて更新しました。
- [`diff_report.md`](doc/samples/diff_report.md) のセクション見出しを改善しました。[`ReportGenerateService`](Services/ReportGenerateService.cs) の各 section writer がファイル件数を見出し末尾に付与するようになりました（例: `## [ x ] Ignored Files (3)`、`## [ + ] Added Files (1)`）。また `IgnoredFilesSectionWriter` の表示パスを変更しました: `old` のみ、または `new` のみに存在する無視ファイルは絶対パス表示（例: `/path/to/old/rel/file.pdb`）、両側に存在するファイルは引き続き相対パス表示になります。

- [`ConfigSettings`](Models/ConfigSettings.cs) に `ShouldGenerateHtmlReport`（既定値 `true`）を追加しました。`true` のとき、各実行で `diff_report.md` と同じ `Reports/<label>/` ディレクトリに **`diff_report.html`** も生成されます。HTML ファイルはサーバや拡張機能不要のスタンドアロン自己完結型レビュードキュメントです。Ignored / Unchanged / Added / Removed / Modified の全ファイルエントリを 8 列テーブルで表示し、Removed / Added / Modified 行にはインタラクティブなチェックボックス・OK 理由テキスト入力・備考テキスト入力を備え、プロダクトリリースレビュー時のサインオフをブラウザ上で完結できます。Added / Removed / Modified の列ヘッダはそれぞれ緑・赤・青の背景色で色付けされ、セクション見出しも同様に緑・赤・青の文字色で表示されます。ファイルには `folderdiff-<label>` キーによる localStorage 自動保存と、現在のレビュー状態を新しいポータブルスナップショットファイルへ書き出す **「Download reviewed version」** ボタンの JavaScript が埋め込まれています。無効化するには [`config.json`](config.json) で `"ShouldGenerateHtmlReport": false` を設定します。新規 [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) として実装し、[`RunScopeBuilder`](Runner/RunScopeBuilder.cs) に登録、[`ProgramRunner.GenerateReport()`](ProgramRunner.cs) から呼び出します。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) に 12 件のユニットテストを追加しました。サンプルファイル [`doc/samples/diff_report.md`](doc/samples/diff_report.md) と [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を追加し、[README.md](README.md) を外部サンプルへのリンク方式にリファクタリングするとともに、レビューワークフローを説明する日英バイリンガルの `Interactive HTML Review Report` / `インタラクティブ HTML レビューレポート` セクションを追加しました。[開発者ガイド](doc/DEVELOPER_GUIDE.md) と [テストガイド](doc/TESTING_GUIDE.md) を更新しました。

- [`ConfigSettings`](Models/ConfigSettings.cs) に `DisassemblerBlacklistTtlMinutes`（既定値 `10`）を追加しました。このプロパティは、`DISASSEMBLE_FAIL_THRESHOLD`（3 回）以上連続失敗した逆アセンブラツールのブラックリスト有効期間（分）を制御します。従来は 10 分固定でしたが、[`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) が起動時に設定値を読み込むようになりました。[`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) に既定値と JSON ラウンドトリップを検証するテストを追加しました。
- [`DisassemblerBlacklist`](Services/DisassemblerBlacklist.cs) を [`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) から独立したクラスとして抽出しました。[`ConcurrentDictionary`](https://learn.microsoft.com/ja-jp/dotnet/api/system.collections.concurrent.concurrentdictionary-2?view=net-8.0)、TTL、失敗しきい値ロジックをカプセル化し、テスト専用ヘルパー `InjectEntry` / `ContainsEntry` を追加しました。[`DotNetDisassembleServiceTests`](FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) はインスタンスレベルのリフレクション（`_blacklist`）を使うように更新しました。新規 [`DisassemblerBlacklistTests`](FolderDiffIL4DotNet.Tests/Services/DisassemblerBlacklistTests.cs) は、しきい値境界、TTL 期限切れ、`Clear`、`ResetFailure`、null 安全性、並列アクセス 2 シナリオ（B-4）をカバーします。
- [`IReportSectionWriter`](Services/IReportSectionWriter.cs) インターフェイスと [`ReportWriteContext`](Services/ReportWriteContext.cs) コンテキストクラスを導入しました。[`ReportGenerateService`](Services/ReportGenerateService.cs) は `_sectionWriters` 静的リストに 10 個のプライベートネストクラス実装（`HeaderSectionWriter`、`LegendSectionWriter`、`IgnoredFilesSectionWriter`、`UnchangedFilesSectionWriter`、`AddedFilesSectionWriter`、`RemovedFilesSectionWriter`、`ModifiedFilesSectionWriter`、`SummarySectionWriter`、`ILCacheStatsSectionWriter`、`WarningsSectionWriter`）を持ち、`WriteReportSections` でそれらを順に呼び出します。各セクションはサービス全体を必要とせず `ReportWriteContext` を構築するだけで単独テストできます。
- [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) に Unicode ファイル名テストを追加しました: `GenerateDiffReport_UnicodeFileNames_AreIncludedInReport` と `GenerateDiffReport_UnicodeFileNames_InUnchangedSection` は、日本語・ウムラウト付きラテン文字・中国語の相対パスが Markdown レポートにそのまま含まれることを検証します（B-2）。
- [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) に大件数ファイルのサマリースナップショットテスト `GenerateDiffReport_LargeFileCount_SummaryStatisticsAreCorrect` を追加しました: 10 500 件の Unchanged ファイルを投入し、Summary セクションの `Unchanged` および `Compared` カウントが投入件数と一致することを検証します（B-3）。

#### 変更

- [`FileDiffService`](Services/FileDiffService.cs) に散在していた並列テキスト差分フォールバック用 `catch` ブロック 4 件（[`ArgumentOutOfRangeException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.argumentoutofrangeexception?view=net-8.0)、[`IOException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.ioexception?view=net-8.0)、[`UnauthorizedAccessException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.unauthorizedaccessexception?view=net-8.0)、[`NotSupportedException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.notsupportedexception?view=net-8.0)）を `catch (Exception ex) when (ex is … or …)` 形式の 1 件へ統合し、重複するフォールバック処理を排除しました。
- [`FileDiffService`](Services/FileDiffService.cs) の IL 差分失敗ログを改善しました: エラーメッセージに `ex.Message`（逆アセンブラコマンドと内部原因を含む）を追記し、スタックトレースを参照しなくてもログ行単独で原因が分かるようになりました。
- 従来コメントのなかったマジック定数に理由コメントを追記しました: [`FolderDiffService`](Services/FolderDiffService.cs) の `KEEP_ALIVE_INTERVAL_SECONDS = 5`（CI/SSH タイムアウト余裕値）と `LARGE_DISCOVERY_FILE_COUNT_LOG_THRESHOLD = 10000`（列挙フェーズのパフォーマンス指標）、[`FolderDiffExecutionStrategy`](Services/FolderDiffExecutionStrategy.cs) の `MAX_PARALLEL_NETWORK_LIMIT = 8`（NAS/SMB サーバの接続上限実測値）、[`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) の `DISASSEMBLE_FAIL_THRESHOLD = 3` と `DEFAULT_BLACKLIST_TTL_MINUTES = 10`。

#### 追加

- [`FileDiffResultLists`](Models/FileDiffResultLists.cs) に `DiffSummaryStatistics` レコードと `SummaryStatistics` 計算プロパティを追加しました。このプロパティは `DiffSummaryStatistics(AddedCount, RemovedCount, ModifiedCount, UnchangedCount, IgnoredCount)` として 5 つのカウントをまとめて返し、呼び出し側が 5 つの並行コレクションを個別に参照する必要をなくします。あわせて [`ReportGenerateService.WriteSummarySection()`](Services/ReportGenerateService.cs) を `SummaryStatistics` プロパティを使うように更新し、キュー/辞書への個別 `.Count` 呼び出しを削減しました。[`FileDiffResultListsTests`](FolderDiffIL4DotNet.Tests/Models/FileDiffResultListsTests.cs) にユニットテスト 4 件を追加しました。
- [`ConfigSettings`](Models/ConfigSettings.cs) に `SpinnerFrames`（`List<string>`）を追加しました。各要素がスピナーの 1 フレームとなり、デフォルトの `| / - \` 4 フレームローテーションをブロック文字や絵文字など複数文字を含む任意の文字列シーケンスに置き換えられます。[`ConsoleSpinner`](FolderDiffIL4DotNet.Core/Console/ConsoleSpinner.cs) の内部フレーム配列を `char[]` から `string[]` に変更して複数文字フレームに対応しました。[`ProgressReportService`](Services/ProgressReportService.cs) と [`ReportGenerateService`](Services/ReportGenerateService.cs) のコンストラクタに `ConfigSettings` パラメータを追加し、起動時に設定済みフレームを読み込めるようにしました。バリデーションは 1 件以上のフレームを必須とします。日英 [README.md](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md) を更新しました。

#### 変更

- `"// MVID:"` リテラルの重複定義を解消し、[`Constants.IL_MVID_LINE_PREFIX`](Common/Constants.cs) に一元化しました。[`ReportGenerateService`](Services/ReportGenerateService.cs) と [`ILOutputService`](Services/ILOutputService.cs) の両ファイルに存在していた `private const string MVID_PREFIX` を削除し、各参照箇所を [`Constants.IL_MVID_LINE_PREFIX`](Common/Constants.cs) に置き換えました。文字列値は同一のため動作変更はありません。
- [`diff_report.md`](doc/samples/diff_report.md) のタイムスタンプ表示を改善しました。フォーマットを `yyyy-MM-dd HH:mm:ss.fff zzz`（エントリごとにミリ秒＋タイムゾーンオフセット）から `yyyy-MM-dd HH:mm:ss`（秒精度）に変更し、タイムゾーンオフセットは `ShouldOutputFileTimestamps` が `true` の場合にレポートヘッダで `Timestamps (timezone): +09:00` として一括表示するようにしました。各エントリの表示は以前の `<u>(updated_old: ..., updated_new: ...)</u>` 形式からブラケット＋矢印形式（新旧両方: `[old → new]`、単一: `[timestamp]`）に統一しました。`Warnings` セクションも同様にブラケット＋矢印形式に統一しました。Unchanged ファイルについては、判定結果（`MD5Match` / `TextMatch` / `ILMatch`）によらず old と new の更新日時が異なる場合に新旧両方を表示するよう修正しました（従来は `ILMatch` のみ両方表示）。日英 [README.md](README.md) および関連テストを更新しました。

#### 追加

- [`FolderDiffIL4DotNet.Tests`](FolderDiffIL4DotNet.Tests/) の未カバーシナリオ 4 件を補完しました: (1) [`FileSystemUtilityTests`](FolderDiffIL4DotNet.Tests/Core/IO/FileSystemUtilityTests.cs) に `IsLikelyWindowsNetworkPath_ForwardSlashIpUncPath_ReturnsTrue` を追加し、[`FileSystemUtility.IsLikelyWindowsNetworkPath()`](FolderDiffIL4DotNet.Core/IO/FileSystemUtility.cs) が `//` 形式の IP ベース UNC パス（例: `//192.168.1.1/share`）も Windows ネットワークパスとして検出するよう修正しました; (2) [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) に `ExecuteFolderDiffAsync_WhenEnumeratingFilesThrowsIOExceptionDueToSymlinkLoop_LogsAndRethrows` を追加し、ディレクトリ列挙中に発生した `IOException`（シンボリックリンクループによる `ELOOP` エラーなど）がエラーログとともに再スローされることを検証します; (3) [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) に `ExecuteFolderDiffAsync_WhenNewFileDeletedBeforeComparison_ClassifiesAsRemovedWithWarning`（逐次・並列の両バリアント）を追加し、[`FolderDiffService`](Services/FolderDiffService.cs) がファイル比較中に [`FileNotFoundException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.filenotfoundexception?view=net-8.0) をキャッチした場合、例外を伝播させずに警告を記録して当該ファイルを Removed に分類するよう変更しました; (4) [`DotNetDisassembleServiceTests`](FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) に `DisassembleAsync_AfterBlacklistTtlExpiry_RetriesToolAndSucceeds` を追加し、10 分間のブラックリスト TTL が満了した逆アセンブラツールがブラックリストから削除されて次回呼び出しで再試行されることを検証します。
- [`ProgramRunner.ValidateRunDirectories()`](ProgramRunner.cs) に 3 つのプリフライトチェックを追加しました。いずれも設定読み込み前に実行され、失敗時は終了コード `2` を返します: (1) **パス長チェック** — 構築した `Reports/<label>` パスが OS の上限（Windows 標準 260 文字、macOS 1024 文字、Linux 4096 文字）を超えていないことを [`PathValidator.ValidateAbsolutePathLengthOrThrow()`](FolderDiffIL4DotNet.Core/IO/PathValidator.cs) で検証します; (2) **ディスク空き容量チェック** — `DriveInfo` を使ってレポートドライブに 100 MB 以上の空き容量があることを確認します（ドライブ情報を取得できない場合は best-effort でスキップ）; (3) **書き込み権限チェック** — `Reports/` 親ディレクトリに一時プローブファイルを作成・削除し、出力前に書き込み権限を確認します。あわせて `TryValidateAndBuildRunArguments` に [`IOException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.ioexception?view=net-8.0) と [`UnauthorizedAccessException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.unauthorizedaccessexception?view=net-8.0) の catch を追加し、3 つの失敗すべてが終了コード `2` に対応するようにしました。[`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) にユニット/統合テスト 3 件を追加し、[README.md](README.md) を更新しました。
- [`ConfigSettings`](Models/ConfigSettings.cs) に `ShouldIncludeILCacheStatsInReport`（既定値 `false`）を追加しました。`true` に設定し IL キャッシュが有効な場合、[`ReportGenerateService`](Services/ReportGenerateService.cs) は [`diff_report.md`](doc/samples/diff_report.md) の `Summary` と `Warnings` の間に `IL Cache Stats` セクションを追記します（ヒット数・ミス数・ヒット率・保存数・退避数・期限切れ数）。あわせて [`ILCache`](Services/Caching/ILCache.cs) にミス数追跡フィールド `_internalMisses`（完全なキャッシュミスの際にインクリメント）と `GetReportStats()` メソッド、`ILCacheReportStats` レコードを追加しました。[`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) に 3 件のユニットテストを追加し、[`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs)、[README.md](README.md)、[CHANGELOG.md](CHANGELOG.md) を更新しました。
- CLI オプションを拡充しました。`--help`/`-h` は使い方を表示してロガー初期化前にコード `0` で終了します。`--version` はアプリバージョンを表示してコード `0` で終了します。`--config <path>` はデフォルトの `<exe>/[`config.json`](config.json)` に代わり任意のパスから設定ファイルを読み込みます。`--threads <N>` は今回の実行に限り [`ConfigSettings`](Models/ConfigSettings.cs) の `MaxParallelism` を上書きします。`--no-il-cache` は今回の実行に限り `EnableILCache = false` に設定します。`--skip-il` は .NET アセンブリの IL 逆アセンブルと IL 差分比較をまるごとスキップします（[`ConfigSettings`](Models/ConfigSettings.cs) に新設した `SkipIL` プロパティとして保持され、[`FileDiffService`](Services/FileDiffService.cs) でも参照します）。`--no-timestamp-warnings` はタイムスタンプ逆転の警告を抑制します。未知のフラグを指定した場合は、これまで黙ってスルーされていた挙動を改め、説明付きで終了コード `2` を返します。[`ConfigService.LoadConfigAsync()`](Services/ConfigService.cs) にオプショナルな `configFilePath` パラメータを追加しました。[`CliOptionsTests`](FolderDiffIL4DotNet.Tests/CliOptionsTests.cs) にパーサー単体テスト 21 件を追加し、[`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) と [`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) にも統合テストを追加しました。
- [`ConfigSettings.Validate()`](Models/ConfigSettings.cs) と `ConfigValidationResult` クラスを追加しました。[`ConfigService.LoadConfigAsync()`](Services/ConfigService.cs) はデシリアライズ直後に `Validate()` を呼び出し、バリデーションが失敗した場合は全エラーを列挙した [`InvalidDataException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.invaliddataexception?view=net-8.0) をスローします。これにより、設定不正な実行は後から無言で失敗したり未定義の振る舞いを引き起こしたりする代わりに、起動時に分かりやすいエラーメッセージとして検出されます。検証対象の制約: `MaxLogGenerations >= 1`、`TextDiffParallelThresholdKilobytes >= 1`、`TextDiffChunkSizeKilobytes >= 1`、`TextDiffChunkSizeKilobytes < TextDiffParallelThresholdKilobytes`。あわせて [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) にバリデーション単体テスト（7 件）、[`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) にバリデーション統合テスト（5 件）を追加しました。

#### 修正

- CI パイプライン失敗を 3 件修正: [`DotNetDisassembleServiceTests`](FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) の `PrefetchIlCacheAsync_WhenSeededCacheExists_IncrementsHitCounter` に `PATH`/`HOME` 分離を適用し、CI ランナーにインストール済みの実 [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) がバージョンキャッシュの事前投入値を上書きする問題を修正; [`.github/workflows/codeql.yml`](.github/workflows/codeql.yml) の Checkout ステップに `fetch-depth: 0` を追加し、`csharp` の autobuild で [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) がフル履歴からバージョン計算できるよう修正; Analyze ステップに `continue-on-error: true` を追加し、リポジトリの GitHub Default Setup コードスキャンが有効なときに `actions` 言語の SARIF アップロードが拒否されてジョブが失敗する問題を回避。
- [`FileDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs) と [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) に回帰テストを追加し、`DetermineEffectiveTextDiffParallelism` の並列度部分低減経路、`EnumerateDistinctPrecomputeBatches` の重複パスのスキップ経路、`GetEffectiveIlPrecomputeBatchSize` のバッチサイズ 0 時のフォールバック経路をカバーしました。これら 3 つの分岐は commit `e61ba70` で追加されたが未テストのまま残っており、[`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml) で強制している分岐カバレッジ `71%` を下回る原因となっていました。あわせて日英ドキュメントへ最新の通過テスト件数（`251` 件）を反映しました。

#### 変更

- [`ProgramRunner.FormatElapsedTime()`](ProgramRunner.cs) の経過時間表示形式を `HH:MM:SS.mmm`（例: `00:05:30.123`）から `{h}h {m}m {s.d}s`（例: `0h 5m 30.1s`）に変更しました。時・分・秒が単位付きで表示されるため、従来の区切り文字だけでは判別しにくかった曖昧さが解消されます。秒は小数点以下 1 桁（1/10 秒単位、切り捨て）まで表示します。テスト容易性向上のため `FormatElapsedTime` を `internal static` に変更し、[`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) にパラメータ化テスト 7 件を追加しました。あわせて [README.md](README.md) の経過時間サンプル表記を更新しました。
- [`ConsoleBanner`](FolderDiffIL4DotNet.Core/Console/ConsoleBanner.cs) のバナーを Figgle ベースの出力から ANSI Shadow スタイルの Unicode ブロック文字ハードコード文字列に置き換え、[`FolderDiffIL4DotNet.Core`](FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj) から `Figgle` NuGet 依存を削除しました。
- [`ConfigSettings`](Models/ConfigSettings.cs) に `TextDiffParallelMemoryLimitMegabytes` と `ILPrecomputeBatchSize` を追加し、大きいローカルテキスト比較では設定したバッファ予算に応じてチャンク並列ワーカー数を抑えつつ current managed heap 使用量をログできるようにし、IL 関連の事前計算は大規模ツリーでも余分な全件リストを作らずバッチ実行するようにしました。あわせて [`FileDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs)、[`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs)、[`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) に回帰テストを追加し、日英ドキュメントへ最新の通過テスト件数（`248` 件）を反映しました。
- [`ProgramRunner`](ProgramRunner.cs) のトップレベル `catch` で全失敗を 1 つの終了コードへ潰していた挙動をやめ、フェーズ単位の型付き Result に置き換えました。これにより、引数/入力パス不正は `2`、設定読込/解析失敗は `3`、差分実行/レポート生成失敗は `4`、想定外の内部エラーだけを `1` として返します。あわせて [`ProgramTests`](FolderDiffIL4DotNet.Tests/ProgramTests.cs) と [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) に回帰テストを追加し、日英ドキュメントも更新しました。
- [`.github/workflows/release.yml`](.github/workflows/release.yml)、[`.github/workflows/codeql.yml`](.github/workflows/codeql.yml)、[`.github/dependabot.yml`](.github/dependabot.yml) を追加して、リポジトリ単位のリリース自動化とセキュリティ自動化を整備しました。あわせて [`CiAutomationConfigurationTests`](FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) で設定回帰テストを追加し、既存のカバレッジゲートと今回追加した GitHub Releases / CodeQL / Dependabot の役割差分が分かるよう日英ドキュメントを更新しました。
- 実逆アセンブラを使う E2E テスト [`RealDisassemblerE2ETests`](FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) を追加し、[`FileDiffServiceTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs) と [`FolderDiffServiceTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs) では複数 MiB のテキスト比較とシンボリックリンク経由ファイルの実ディレクトリ系カバレッジを拡充しました。あわせて [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml) に total 行 `73%` / 分岐 `71%` の CI カバレッジゲートを追加し、日英ドキュメントへ最新の通過テスト件数（`240` 件）と実測カバレッジ（行 `74.04%` / 分岐 `71.63%`）を反映しました。
- 再利用可能な helper 層を新しい [`FolderDiffIL4DotNet.Core`](FolderDiffIL4DotNet.Core/) プロジェクトへ分離し、従来の `Utils` 型を `Console` / `Diagnostics` / `IO` / `Text` 名前空間へ整理しました。あわせて [`CoreSeparationTests`](FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs) でアーキテクチャ境界の回帰テストを追加し、日英ドキュメントと最新の通過テスト件数（`237` 件）を更新しました。
- 繰り返し出ていたバイト換算値と日時フォーマットを [`Common/Constants.cs`](Common/Constants.cs) へ集約し、[`Constants.BYTES_PER_KILOBYTE`](Common/Constants.cs)、[`Constants.TIMESTAMP_WITH_TIME_ZONE_FORMAT`](Common/Constants.cs)、[`Constants.LOG_ENTRY_TIMESTAMP_FORMAT`](Common/Constants.cs)、[`Constants.LOG_FILE_DATE_FORMAT`](Common/Constants.cs) を共有定義として使うようにしました。あわせて [`ProgramRunner`](ProgramRunner.cs) が使う内部 IL キャッシュ既定値の採用理由をコード上に明記し、共有書式と既定値配線を確認する回帰テストを追加しました。
- [`FolderDiffService`](Services/FolderDiffService.cs) と [`FileDiffService`](Services/FileDiffService.cs) に残っていた広すぎる `catch (Exception)` を、想定される実行時例外の個別処理と想定外例外用ログへ置き換えました。あわせて、プリコンピュート・キャッシュ削除・レポート保護の best-effort 方針と、致命扱いで再スローする経路の境界を明文化し、日英ドキュメントと回帰テストを更新しました。
- [`ProgramRunner.RunAsync()`](ProgramRunner.cs) を、ロガー起動、引数検証、設定/実行準備、差分実行、レポート生成、終了プロンプトの各 helper へ分割し、外部挙動を変えずに主オーケストレーションの見通しを改善しました。
- [`FileSystemUtility.IsLikelyNetworkPath()`](FolderDiffIL4DotNet.Core/IO/FileSystemUtility.cs) から OS 別のネットワークパス判定を切り出し、[`ReportGenerateService.GenerateDiffReport()`](Services/ReportGenerateService.cs) でもレポート書き出しと読み取り専用保護の helper を抽出して、挙動を維持したまま可読性を上げました。
- [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) を追加して「引数検証が設定読込より先に失敗すること」を回帰テスト化し、[`FileSystemUtilityTests`](FolderDiffIL4DotNet.Tests/Core/IO/FileSystemUtilityTests.cs) に null 入力ケースを追加しました。あわせて [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md) を更新し、最新の通過テスト件数（`230` 件）を反映しました。
- [`FolderDiffService`](Services/FolderDiffService.cs) に埋め込まれていた列挙フィルタと自動並列度決定を [`FolderDiffExecutionStrategy`](Services/FolderDiffExecutionStrategy.cs) へ抽出し、実行時挙動を変えずにオーケストレーション責務を整理しました。
- [`FolderDiffExecutionStrategyTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffExecutionStrategyTests.cs) を追加し、無視ファイルの扱い、相対パス和集合件数、自動並列度のネットワーク考慮を回帰テスト化しました。あわせて [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md) を更新し、最新の通過テスト件数（`230` 件）を反映しました。
- [`ILCache`](Services/Caching/ILCache.cs) を、公開 API を維持したまま [`ILMemoryCache`](Services/Caching/ILMemoryCache.cs) と [`ILDiskCache`](Services/Caching/ILDiskCache.cs) を使う薄い調停役へ整理し、メモリ保持とディスク永続化/クォータ制御の責務を分離しました。
- [`ILCacheTests`](FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs) に、メモリ上限到達時の同一キー再保存と、LRU 退避時のディスクキャッシュ連動削除に対する回帰テストを追加しました。
- [開発者ガイド](doc/DEVELOPER_GUIDE.md) と [テストガイド](doc/TESTING_GUIDE.md) を更新し、キャッシュ内部の分離方針と最新の通過テスト件数（`230` 件）を反映しました。
- [`FolderDiffService`](Services/FolderDiffService.cs) 内で使っていた即時配列化の `Directory.GetFiles(...)` 相当を、[`IFileSystemService`](Services/IFileSystemService.cs) 越しの遅延列挙 `Directory.EnumerateFiles(...)` へ置き換えました。これにより、大量ファイルやネットワーク共有上の列挙で不要な配列確保を減らしつつ、フォルダ差分の振る舞いは維持しています。
- [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) にストリーミング列挙のテストを追加し、あわせて [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md) を更新しました。
- [開発者ガイド](doc/DEVELOPER_GUIDE.md)のリンク付与漏れを修正しました。
- [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、レポート注記にある `// MVID:` 無視理由の説明を整理し、あわせて日英の要約表現がずれないように調整しました。
- [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md) のリンク付与範囲を広げ、ロケール切替可能な外部 URL を英語文脈・日本語文脈に合わせて統一しました。
- [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md)、[ドキュメント index](index.md) に日英対応の安定アンカーを追加し、設定値まわりのリンクは README の設定表セクションへ寄せるようにして、一般的な Markdown レンダラでも着地先が安定するようにしました。
- [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md) にあるクラス参照へ、`partial` 分割を前提としないものを中心に対応する `.cs` ソースリンクを追加しました。
- レポート上の `MD5Mismatch` 警告を `Summary` から末尾の `Warnings` セクションへ移し、更新日時逆転警告より先に出すように変更しました。あわせて関連ドキュメントと回帰テストを更新しました。
- DocFX ベースの API ドキュメント自動生成を導入し、ドキュメントサイトの生成経路と `DocumentationSite` artifact 公開を CI に追加しました。
- [`IFileSystemService`](Services/IFileSystemService.cs) と [`IFileComparisonService`](Services/IFileComparisonService.cs) を追加し、フォルダ列挙/出力系 I/O とファイル単位比較 I/O の差し替え口を明確にしました。これにより、本番挙動を変えずに権限エラーやディスク系失敗をユニットテストできるようにしました。
- `FolderDiffService` / `FileDiffService` まわりのテストを、軽量ユニットテストと temp ディレクトリ前提の統合テストにより明確に分離し、ハッシュ失敗、IL 出力失敗、大きいテキスト比較経路の自動テストを拡充しました。
- [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md)の日英両記述を更新し、新しいサービス境界、テスト境界、最新の通過テスト件数（`219` 件）を反映しました。
- 集約後の `MD5Mismatch` コンソール警告を [`ProgramRunner`](ProgramRunner.cs) に移し、[`ReportGenerateService`](Services/ReportGenerateService.cs) はレポート専用の責務に整理しました。あわせて関連ドキュメントと自動テストを更新しました。
- 単発利用の [`string.Format(...)`](https://learn.microsoft.com/ja-jp/dotnet/api/system.string.format?view=net-8.0) を補間文字列へ置き換え、広範な `#region` 利用をやめ、不要になった書式・メッセージ定数を削除しました。
- 開発ガイドとテストガイドを更新し、現在のソースコード方針と最新の通過テスト件数を反映しました。
- `.NET` 実行可能判定で `NotDotNetExecutable` と判定失敗を区別するようにし、致命ではない判定失敗は warning を残して継続するようにしました。あわせて並列テキスト比較の例外は `false` に潰さず、既存の逐次比較フォールバック経路へ伝播させるようにしました。
- 本体コードで広すぎる例外捕捉を通常ビルド時に検出できるよう、`CA1031` アナライザーを有効化しました。テストの後片付け用 catch は warning 対象から外しています。
- [`FileSystemUtility`](FolderDiffIL4DotNet.Core/IO/FileSystemUtility.cs) での `throw new Exception(..., ex)` 形式の汎用ラップをやめ、ここで言う外側の型名 [`Exception`](https://learn.microsoft.com/ja-jp/dotnet/api/system.exception?view=net-8.0) への包み直しを避けることで、元の例外型とスタックトレースを維持するようにしました。あわせて回帰テストと日英ガイドを更新しました。
- 設定の既定値を [`ConfigSettings`](Models/ConfigSettings.cs) へ集約し、未指定や `null` の設定値をコード既定値へ正規化するようにしました。あわせて配布する [`config.json`](config.json) を override 専用の形に簡素化し、日英ドキュメントと設定まわりのテストを更新しました。

### [1.2.2] - 2026-03-14

#### 追加

- `new` 側ファイルの更新日時が対応する `old` 側より古い場合に、終了前のコンソール警告と [`diff_report.md`](doc/samples/diff_report.md) 末尾の `Warnings` セクションを出す設定付き機能を追加しました。
- CI に coverlet ベースのカバレッジ計測を追加し、[`Program`](Program.cs)、ロギング、進捗表示、ファイルシステム補助、テキスト差分フォールバック経路の自動テストを拡充しました。

#### 変更

- コンソール出力の色強調を見直し、最終的な成功・失敗メッセージに加えて警告メッセージも黄色で強調表示するようにしました。
- 更新日時逆転警告に合わせて、設定例、各種ドキュメント、自動テストを更新しました。
- [`ProgramRunner`](ProgramRunner.cs)、[`DiffExecutionContext`](Services/DiffExecutionContext.cs)、インターフェイスベースのサービス構成へ整理し、差分パイプラインのテスタビリティを向上させるとともに、静的状態への直接依存を減らしました。
- [開発者ガイド](doc/DEVELOPER_GUIDE.md) と [テストガイド](doc/TESTING_GUIDE.md) を専用ドキュメントとして分離し、[README](README.md) のインストール手順と比較フロー説明を拡充しました。
- [開発者ガイド](doc/DEVELOPER_GUIDE.md) に実行ライフサイクル、DI 境界、実行モード、Mermaid 図を追加し、[README](README.md) と [テストガイド](doc/TESTING_GUIDE.md) のドキュメント導線も整理しました。

#### 修正

- ネットワーク共有判定時の例外捕捉範囲を限定し、並列テキスト比較が逐次比較へフォールバックした際に警告ログを出すようにしました。

### [1.2.1] - 2026-03-09

#### 追加

- テキスト差分の並列化しきい値とチャンクサイズを KiB 単位で設定できる構成項目を追加しました。
- [`FolderDiffService`](Services/FolderDiffService.cs) 向けの専用テストを追加し、レポート生成まわりのテストを強化しました。

#### 変更

- ガード節の null チェックを [`ArgumentNullException.ThrowIfNull`](https://learn.microsoft.com/ja-jp/dotnet/api/system.argumentnullexception.throwifnull?view=net-8.0) に統一しました。

#### 修正

- [`FileDiffResultLists`](Models/FileDiffResultLists.cs) をスレッドセーフ化し、実行間で共有状態を確実に初期化できる `ResetAll` を追加しました。

### [1.2.0] - 2026-03-07

#### 追加

- IL 比較時に設定した文字列を含む行を無視できるオプションを追加し、その挙動をレポート出力にも反映しました。

#### 変更

- IL ディスクキャッシュの既定値を、無制限 (`0`) ではなく 1000 件 / 512 MB に設定しました。

### [1.1.9] - 2026-03-07

#### 追加

- 専用のテストプロジェクトを追加し、CI でのテスト実行手順とあわせて、キャッシュ・逆アセンブラ・レポートまわりの自動テストを拡充しました。
- コマンドライン引数の検証完了後に、ASCII アートのアプリ名バナーを表示するようにしました。
- 最終的な成功・失敗メッセージだけを色強調するようにしました。

#### 変更

- [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) と [`dotnet ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) の扱いを統一し、逆アセンブラ識別の整合性チェックとレポート表記を改善しました。
- ユーティリティ群を単一責任のクラスへ分割し、[`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) と [`FolderDiffService`](Services/FolderDiffService.cs) の長大メソッドを責務ごとに整理しました。
- `HashSet.Union` の結果生成を `UnionWith` に置き換えるなど、内部的な軽微改善を行いました。

#### 修正

- [`diff_report.md`](doc/samples/diff_report.md) に利用可能な全逆アセンブラではなく、実際に使われた逆アセンブラだけを記録するよう修正しました。
- IL 比較時に複数の逆アセンブラが混在したり、キャッシュが混線したりする可能性を解消しました。
- 小さいテキストファイル比較で逐次比較が二重実行される冗長処理を解消しました。
- IL 比較まわりの回帰対応、レポート文言、終了コード挙動を改善しました。

### [1.1.8] - 2026-01-24

#### 追加

- [`diff_report.md`](doc/samples/diff_report.md) に、実際に使われた逆アセンブルツール名とバージョンを出力するようにしました。

#### 変更

- フォルダ比較の進捗表示を「ラベル + スピナー + 進捗バー」の構成に刷新し、その後の軽微調整も取り込みました。

### [1.1.7] - 2025-12-30

#### 追加

- `README.en.md` を追加しました。その後、ドキュメント再編時に内容は `README.md` へ統合されました。
- ドキュメントにライセンス情報を追加しました。

#### 変更

- 長時間処理のフィードバックを、スピナー単体から進捗バーと協調する表示へ刷新しました。

### [1.1.6] - 2025-12-11

#### 追加

- [`diff_report.md`](doc/samples/diff_report.md) に実行コンピュータ名を出力するようにしました。

#### 変更

- 定数定義、および逆アセンブラキャッシュ / IL キャッシュ内部をリファクタリングしました。

#### 修正

- 冗長な内部処理を整理しました。

### [1.1.5] - 2025-12-08

#### 追加

- 長時間処理向けのスピナー表示を追加しました。

#### 変更

- 全体的な内部リファクタリングを行い、README の記述も見直しました。

### [1.1.4] - 2025-12-07

#### 追加

- GitHub Actions による .NET ビルド自動化を追加しました。

### [1.1.3] - 2025-11-29

#### 追加

- Ignored ファイルをレポートに記載できる設定と出力対応を追加しました。

### [1.1.2] - 2025-11-16

#### 追加

- `MD5Mismatch` と判定されたファイルが存在した場合に、レポートで警告できるようにしました。

#### 変更

- `.NET` 実行ファイル判定の仕様を README に追記しました。

#### 修正

- 最初の進捗表示が出るまでの無音区間を短縮しました。
- `.NET` 実行ファイル判定を PE32 / PE32+ の両方に対応させました。
- `v1.1.1` 以降に入っていた軽微修正も本版に含めました。

### [1.1.1] - 2025-09-14

#### 追加

- [`diff_report.md`](doc/samples/diff_report.md) に各ファイルの更新日時を出力するかどうかを切り替える設定を追加しました。

### [1.1.0] - 2025-09-12

#### 追加

- ネットワーク共有向けの最適化機能を追加しました。
- テキストとして扱う拡張子を追加しました。

#### 変更

- [`IgnoredExtensions`](README.md#configuration-table-ja) を大文字小文字を無視して評価するようにしました。

#### 削除

- 未使用だった `ShouldSkipPromptOnExit` 設定を削除しました。

#### 修正

- [`TextFileExtensions`](README.md#configuration-table-ja) の設定値誤りを是正しました。
- README の誤記を修正しました。
- 進捗出力開始前の無音区間を短縮しました。

### [1.0.1] - 2025-08-30

#### 追加

- テキスト比較対象とする拡張子を追加しました。

#### 削除

- `ILlog.md` / `ILlog.html` の生成を廃止しました。

### [1.0.0] - 2025-08-17

#### 追加

- `FolderDiffIL4DotNet` の初回リリース。フォルダ比較、Markdown レポート出力、`.NET` アセンブリの IL 比較、キャッシュ、設定読込、進捗表示、ログ出力を含みます。

[Unreleased]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.3.0...HEAD
[1.3.0]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.2.2...v1.3.0
[1.2.2]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.2.1...v1.2.2
[1.2.1]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.9...v1.2.0
[1.1.9]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.8...v1.1.9
[1.1.8]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.7...v1.1.8
[1.1.7]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.6...v1.1.7
[1.1.6]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.5...v1.1.6
[1.1.5]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.4...v1.1.5
[1.1.4]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.3...v1.1.4
[1.1.3]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.2...v1.1.3
[1.1.2]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Widthdom/FolderDiffIL4DotNet/tree/v1.0.0
