# Changelog

All notable changes to this project will be documented in this file.

The English section comes first, followed by a Japanese translation.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## English

### [Unreleased]

#### Added

- **Interactive HTML report filtering** — Added a filter bar to the HTML report (`diff_report.html`) enabling users to narrow down file rows by multiple criteria: **Importance** (High / Medium / Low checkboxes), **File Type** (DLL / EXE / Config / Resource / Other), **Unchecked only** (show only rows whose checkbox is not ticked), and a **free-text search** box for file paths. All filter controls use bilingual labels (English / Japanese). The filter bar is placed inside the `<!--CTRL-->...<!--/CTRL-->` markers, so it is automatically stripped in reviewed (read-only) HTML. Filtering state is excluded from `collectState()` / localStorage auto-save via the `__filterIds__` array. `downloadReviewed()` clears all `filter-hidden` / `filter-hidden-parent` CSS classes from table rows before capturing `outerHTML`, ensuring the reviewed HTML always shows all rows regardless of active filters at the time of download. Implementation details: each `<tr>` emitted by `AppendFileRow()` now carries `data-section`, `data-ext`, and (when applicable) `data-importance` attributes for client-side filtering. CSS classes `tr.filter-hidden` and `tr.diff-row.filter-hidden-parent` hide rows with `display: none !important`. New JS functions: `getFileTypeCategory(ext)`, `applyFilters()`, `resetFilters()`. Updated [`diff_report.css`](Services/HtmlReport/diff_report.css), [`diff_report.js`](Services/HtmlReport/diff_report.js), [`HtmlReportGenerateService.cs`](Services/HtmlReportGenerateService.cs), [`HtmlReportGenerateService.Helpers.cs`](Services/HtmlReport/HtmlReportGenerateService.Helpers.cs), [`doc/samples/diff_report.html`](doc/samples/diff_report.html). Added 10 tests in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs): `GenerateDiffReportHtml_ContainsFilterBar`, `GenerateDiffReportHtml_FilterBarIsInsideCtrlMarkers`, `GenerateDiffReportHtml_FileRowsHaveDataSectionAttribute`, `GenerateDiffReportHtml_FileRowsHaveDataExtAttribute`, `GenerateDiffReportHtml_ModifiedRowsWithImportance_HaveDataImportanceAttribute`, `GenerateDiffReportHtml_FilterBarCss_ContainsFilterHiddenRule`, `GenerateDiffReportHtml_JsContainsApplyFiltersFunction`, `GenerateDiffReportHtml_JsCollectState_ExcludesFilterIds`, `GenerateDiffReportHtml_DownloadReviewed_ClearsFilterHiddenClasses`.

- **Auto-assign Change Importance to semantic change entries** — Each `MemberChangeEntry` detected by `AssemblyMethodAnalyzer` is now automatically classified as `High`, `Medium`, or `Low` importance by the new rule-based `ChangeImportanceClassifier`. Classification rules: `High` = public/protected API removal, access narrowing from public/protected, return-type change, member-type change, parameter change; `Medium` = public/protected member addition, internal/private removal, modifier change, access widening; `Low` = body-only change, internal/private member addition. New model: [`ChangeImportance`](Models/ChangeImportance.cs) enum (`Low=0`, `Medium=1`, `High=2`). New service: [`ChangeImportanceClassifier`](Services/ChangeImportanceClassifier.cs) with `Classify(MemberChangeEntry)` and `WithClassifiedImportance(MemberChangeEntry)` methods. `MemberChangeEntry` record gains an optional `Importance` parameter (default: `Low`) for backward compatibility. `AssemblySemanticChangesSummary` gains `HighImportanceCount`, `MediumImportanceCount`, `LowImportanceCount`, `MaxImportance`, and `EntriesByImportance` properties. Report changes: the Modified Files table Diff Reason column now appends the file-level max importance after `ILMismatch` (e.g. `ILMismatch` `High`); all three Modified Files tables (`[ * ] Modified Files`, `[ ! ] SHA256Mismatch`, `[ ! ] Timestamps Regressed`) are sorted by DiffDetail → Importance → Path; the semantic changes detail table adds an `Importance` column; the summary count table columns change from (Class, Status, Count) to (Class, Status, High, Medium, Low, Total); the Legend section adds `High`, `Medium`, `Low` entries with bilingual descriptions. Applies to both Markdown and HTML reports. Updated [`doc/samples/diff_report.md`](doc/samples/diff_report.md) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html). Added [`ChangeImportanceClassifierTests`](FolderDiffIL4DotNet.Tests/Services/ChangeImportanceClassifierTests.cs) (20+ test methods) and extended [`AssemblySemanticChangesSummaryTests`](FolderDiffIL4DotNet.Tests/Models/AssemblySemanticChangesSummaryTests.cs) with importance-related assertions.

- **Disassembler Availability report in header** — The report header now includes a "Disassembler Availability" table that lists every candidate IL disassembler tool (e.g. `dotnet-ildasm`, `ilspycmd`) and whether it was available or unavailable in the current environment, along with its version when available. This makes it easy to understand at a glance which tools influenced the comparison and the confidence level of IL-based results. At startup, `DisassemblerHelper.ProbeAllCandidates()` probes each candidate by running `--version` and stores the results in `FileDiffResultLists.DisassemblerAvailability` (new property). The table is rendered in Markdown (`diff_report.md`), HTML (`diff_report.html`), and as a `disassemblerAvailability` JSON array in the audit log (`audit_log.json`). New model: [`DisassemblerProbeResult`](Models/DisassemblerProbeResult.cs) (`ToolName`, `Available`, `Version`, `Path`). New audit model: [`AuditLogDisassemblerAvailability`](Models/AuditLogEntry.cs). Updated [`doc/samples/diff_report.md`](doc/samples/diff_report.md), [`doc/samples/diff_report.html`](doc/samples/diff_report.html), and [`doc/samples/audit_log.json`](doc/samples/audit_log.json). Added tests `GenerateDiffReport_HeaderShowsDisassemblerAvailabilityTable`, `GenerateDiffReport_HeaderOmitsAvailabilityTable_WhenProbeResultsAreNull` in [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs), `GenerateDiffReportHtml_HeaderShowsDisassemblerAvailabilityTable`, `GenerateDiffReportHtml_HeaderOmitsAvailabilityTable_WhenProbeResultsAreNull` in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs), `GenerateAuditLog_IncludesDisassemblerAvailability_WhenProbed`, `GenerateAuditLog_DisassemblerAvailabilityIsNull_WhenNotProbed` in [`AuditLogGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/AuditLogGenerateServiceTests.cs), and `ProbeAllCandidates_ReturnsNonEmptyList_WithUniqueToolNames`, `ProbeAllCandidates_IncludesExpectedToolNames`, `ProbeAllCandidates_AllResultsHaveNonEmptyToolName` in [`DisassemblerHelperTests`](FolderDiffIL4DotNet.Tests/Services/DisassemblerHelperTests.cs).

- **Audit log and tamper detection (`audit_log.json` + reviewed HTML integrity)** — Added `AuditLogGenerateService` that generates a structured JSON audit log alongside the diff reports. The audit log records per-file comparison results (category, diff detail, disassembler used), run metadata (app version, computer name, old/new paths, ISO 8601 timestamp, elapsed time), summary statistics, and SHA256 integrity hashes of the generated `diff_report.md` and `diff_report.html` for tamper detection. Generation is controlled by the new `ShouldGenerateAuditLog` config setting (default: `true`). New model classes: `AuditLogRecord`, `AuditLogFileEntry`, `AuditLogSummary` in `Models/AuditLogEntry.cs`. The service is registered in `RunScopeBuilder` and invoked after HTML report generation in `ProgramRunner.GenerateReport()`. Added sample [`doc/samples/audit_log.json`](doc/samples/audit_log.json). Updated `IReadOnlyConfigSettings` interface with `ShouldGenerateAuditLog` property. The "Download as reviewed" workflow now also computes a SHA256 hash of the reviewed HTML using the Web Crypto API, embeds it inside the file via a placeholder technique, and downloads a companion `.sha256` verification file. The reviewed HTML's header includes a "Verify integrity" button that re-reads the file, recomputes the hash, and displays a pass/fail dialog. The `.sha256` file follows the `sha256sum`/`shasum` format and can be verified on any OS (Linux: `sha256sum -c`, macOS: `shasum -a 256 -c`, Windows: `Get-FileHash` in PowerShell). Submitting the reviewed HTML together with the `.sha256` file constitutes a tamper-proof audit record. Added 18 tests: 17 in [`AuditLogGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/AuditLogGenerateServiceTests.cs) and 1 in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs). Updated `ConfigSettingsTests` to assert the new default.

- **Test coverage measurement infrastructure and mutation testing** — Added [`coverlet.runsettings`](coverlet.runsettings) for fine-grained coverlet configuration (include/exclude filters, deterministic report output, branch coverage, multi-format output). Added [`.config/dotnet-tools.json`](.config/dotnet-tools.json) manifest registering `dotnet-reportgenerator-globaltool` and `dotnet-stryker` as local tool dependencies. Added [`stryker-config.json`](stryker-config.json) for Stryker.NET mutation testing with `Standard` mutation level, `80/60/50` high/low/break thresholds, and `html`/`json`/`progress`/`cleartext` reporters. Updated [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml): CI now uses `dotnet tool restore` for reproducible local tool versions, `--settings coverlet.runsettings` for test runs, and a new `mutation-testing` job (workflow_dispatch only) that runs Stryker and uploads results as artifacts. The `mutation-testing` job is gated to manual dispatch to avoid slowing down regular CI, while still making mutation testing a one-click operation.

- **Edge case tests for error handling, concurrency, and boundary conditions** — Added 5 new test classes under `FolderDiffIL4DotNet.Tests/Services/EdgeCases/`: [`DisassemblerBlacklistTtlRecoveryTests`](FolderDiffIL4DotNet.Tests/Services/EdgeCases/DisassemblerBlacklistTtlRecoveryTests.cs) (7 tests for TTL recovery cycles, concurrent register/check, boundary timestamps, manual reset during active blacklist); [`ILCacheConcurrencyTests`](FolderDiffIL4DotNet.Tests/Services/EdgeCases/ILCacheConcurrencyTests.cs) (5 tests for concurrent Set/Get with memory-only and disk-backed cache, LRU eviction under contention, TTL expiry races, concurrent PrecomputeAsync); [`ILCacheDiskFailureTests`](FolderDiffIL4DotNet.Tests/Services/EdgeCases/ILCacheDiskFailureTests.cs) (5 tests simulating network-mounted storage failures: read-only directory, corrupted cache files, invalid paths, mid-operation directory deletion); [`LargeFileComparisonTests`](FolderDiffIL4DotNet.Tests/Services/EdgeCases/LargeFileComparisonTests.cs) (6 tests for 4 MiB identical/differing files via chunk-parallel, size mismatch detection, empty files, many small chunks); [`SymlinkAndCircularDirectoryTests`](FolderDiffIL4DotNet.Tests/Services/EdgeCases/SymlinkAndCircularDirectoryTests.cs) (5 tests for symlink loops on old/new sides, dangling symlinks classified as Removed, access denied on symlink targets, parallel mode with multiple dangling symlinks); [`FolderDiffConcurrencyStressTests`](FolderDiffIL4DotNet.Tests/Services/EdgeCases/FolderDiffConcurrencyStressTests.cs) (3 tests for deterministic classification with 500 files at 8x parallelism, simulated random latency, realistic mix of all four file categories under high parallelism).

- **`--print-config` discoverability and configuration error guidance** — The `--help` text now includes a dedicated "Tip:" section at the bottom that highlights `--print-config` as the recommended way to inspect the effective configuration before a run. When a configuration error occurs (exit code `3`), a hint is written to stderr: `Tip: Run with --print-config to display the effective configuration as JSON.` This enables users to quickly diagnose which settings are active and what overrides have been applied. Added tests `RunAsync_HelpFlag_OutputContainsPrintConfigTipSection` and `RunAsync_ConfigError_WritesPrintConfigHintToStderr` in [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs).

- **Railway-oriented `StepResult<T>` pipeline** — Refactored `ProgramRunner.RunWithResultAsync` from repetitive `if(!IsSuccess) return Failure` patterns to a functional railway-oriented pipeline using the new `Bind<TNext>` (synchronous) and `BindAsync<TNext>` (asynchronous) methods on [`StepResult<T>`](Runner/ProgramRunner.Types.cs). Each execution phase (argument validation → reports directory preparation → config loading → CLI overrides → config build → execution) is chained via `Bind`/`BindAsync`, with automatic short-circuit on failure. No behavioural change; purely an internal code quality improvement.

- **Complete XML documentation and remove CS1591/CS1573 suppression** — Added XML documentation comments (`<summary>`, `<param>`, `<returns>`, `<typeparam>`) to all public types, constructors, properties, methods, and enum members that were previously missing documentation. Removed `<NoWarn>CS1591;CS1573</NoWarn>` from both [`FolderDiffIL4DotNet.csproj`](FolderDiffIL4DotNet.csproj) and [`FolderDiffIL4DotNet.Core.csproj`](FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj). With `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` enabled, any future public API without documentation will fail the build. Affected files: `AuditLogEntry.cs`, `ConfigSettings.cs`, `FileDiffResultLists.cs`, `ProgramRunner.Types.cs`, `AppLogLevel.cs`, `AuditLogGenerateService.cs`, `DotNetDisassemblerCache.cs`, `ILCache.cs`, `DiffExecutionContext.cs`, `DisassemblerBlacklist.cs`, `DotNetDisassembleService.cs`, `FileDiffService.cs`, `FolderDiffExecutionStrategy.cs`, `FolderDiffService.cs`, `HtmlReportGenerateService.cs`, `ILTextOutputService.cs`, `ILOutputService.cs`, `ProgressReportService.cs`, `ReportGenerateService.cs`, `ReportGenerationContext.cs`.

- **Harden HTML report with `System.Net.WebUtility.HtmlEncode` and Content-Security-Policy meta tag** — Replaced the custom 5-character manual `HtmlEncode` method (`&` → `&amp;`, `<` → `&lt;`, `>` → `&gt;`, `"` → `&quot;`, `'` → `&#39;`) in [`HtmlReportGenerateService.Helpers.cs`](Services/HtmlReport/HtmlReportGenerateService.Helpers.cs) with `System.Net.WebUtility.HtmlEncode`, which provides comprehensive entity encoding covering backticks, non-ASCII characters, and other edge cases. Added a `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src 'self'">` tag to the HTML `<head>` in [`HtmlReportGenerateService.cs`](Services/HtmlReportGenerateService.cs), limiting the impact of any potential XSS by blocking external resource loading, form submissions, and other unsafe defaults. Updated [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to include the CSP meta tag. Added tests `HtmlEncode_EscapesBacktickAndNonAsciiCharacters`, `HtmlEncode_PreservesNormalTextUnchanged`, `HtmlEncode_HandlesUnicodeCharacters`, `GenerateDiffReportHtml_ContainsContentSecurityPolicyMetaTag`, `GenerateDiffReportHtml_CspMetaTagAppearsBetweenCharsetAndViewport` in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs).

#### Performance

- **Eliminate SHA256 double computation** — `FileDiffService.FilesAreEqualAsync` now calls the new `DiffFilesByHashWithHexAsync` method (on [`FileComparer`](FolderDiffIL4DotNet.Core/IO/FileComparer.cs) / [`IFileComparisonService`](Services/IFileComparisonService.cs)) which returns the computed SHA256 hex strings alongside the equality result. The computed hashes are immediately seeded into the IL cache via `IILOutputService.PreSeedFileHash` → `ILCache.PreSeedFileHash` → `ILMemoryCache.PreSeedFileHash`, preventing `ILMemoryCache.GetFileHash` from recomputing SHA256 when building IL cache keys for the same files. This eliminates redundant file I/O for large .NET assemblies where both hash comparison and IL cache lookup occur. New interface methods: `IFileComparisonService.DiffFilesByHashWithHexAsync`, `IILOutputService.PreSeedFileHash`. New implementation methods: `FileComparer.DiffFilesByHashWithHexAsync`, `ILOutputService.PreSeedFileHash`, `ILCache.PreSeedFileHash`, `ILMemoryCache.PreSeedFileHash`. Added tests `FilesAreEqualAsync_WhenHashMatches_SeedsILCacheWithBothHashes`, `FilesAreEqualAsync_WhenHashDiffers_SeedsILCacheWithBothHashes` in [`FileDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs), `PreSeedFileHash_AvoidsSha256Recomputation` in [`ILCacheTests`](FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs), `PreSeedFileHash_WhenCacheIsNull_DoesNotThrow` in [`ILOutputServiceTests`](FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs).

- **Single-pass IL line split and filter** — Replaced the four-allocation pipeline (`ilText.Split('\n').ToList()` → `Where(filter).ToList()`) in `ILOutputService.DiffDotNetAssembliesAsync` with a single-pass `SplitAndFilterIlLines` method that splits and filters in one iteration, producing a single `List<string>` directly. This halves the number of intermediate list allocations and avoids creating the unfiltered intermediate list entirely. Added tests `SplitAndFilterIlLines_CombinesSplitAndFilter_MatchesSplitThenWhereBehavior`, `SplitAndFilterIlLines_WithConfiguredIgnoreStrings_ExcludesMatchingLines` in [`ILOutputServiceTests`](FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs).

- **BenchmarkDotNet CI integration** — Added a `benchmark` job to [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml) that runs the [`FolderDiffIL4DotNet.Benchmarks`](FolderDiffIL4DotNet.Benchmarks/) project with JSON and GitHub exporters and uploads `BenchmarkDotNet.Artifacts/` as a CI artifact. The job runs only on `workflow_dispatch` to avoid impacting regular CI pipeline duration.

#### Changed

- **Immutable ConfigSettings via Builder pattern** — `ConfigSettings` is now fully immutable: all properties are read-only and list properties return `IReadOnlyList<string>`. A new [`ConfigSettingsBuilder`](Models/ConfigSettingsBuilder.cs) class serves as the mutable intermediary for JSON deserialization, environment variable overrides, and CLI overrides. The config loading flow is now: `ConfigService.LoadConfigBuilderAsync()` deserializes `config.json` into `ConfigSettingsBuilder` → `ApplyEnvironmentVariableOverrides` applies `FOLDERDIFF_*` env vars to the builder → `ProgramRunner.ApplyCliOverrides` applies CLI flags to the builder → `ConfigSettingsBuilder.Validate()` checks value ranges → `ConfigSettingsBuilder.Build()` produces an immutable `ConfigSettings`. This fixes a latent bug where validation ran before CLI overrides, allowing invalid CLI values to bypass checks. The DI container now registers `ConfigSettings` only as `IReadOnlyConfigSettings`, preventing downstream services from casting to a mutable type. Updated all test files to construct config via `new ConfigSettingsBuilder { ... }.Build()`.

- **Consolidate report generation parameters into ReportGenerationContext DTO** — Introduced [`ReportGenerationContext`](Services/ReportGenerationContext.cs) to consolidate the 8 parameters (`oldFolderAbsolutePath`, `newFolderAbsolutePath`, `reportsFolderAbsolutePath`, `appVersion`, `elapsedTimeString`, `computerName`, `config`, `ilCache`) that were duplicated across `ReportGenerateService.GenerateDiffReport`, `HtmlReportGenerateService.GenerateDiffReportHtml`, and `AuditLogGenerateService.GenerateAuditLog`. Each service method now accepts a single `ReportGenerationContext` parameter. `ProgramRunner.GenerateReport` constructs one context instance and passes it to all three services. Updated corresponding test files.

- **Interleave warning messages with detail tables in Warnings section** — Each warning bullet point is now immediately followed by its corresponding detail table, instead of listing all warning messages first and then all detail tables. When both SHA256Mismatch and Timestamp Regression warnings exist, the layout is: SHA256Mismatch warning → SHA256Mismatch detail table → Timestamp Regression warning → Timestamps Regressed detail table. This change applies to both Markdown and HTML reports. In the HTML report, each warning message is rendered in its own `<ul class="warnings">` element directly above its detail table. Updated [`doc/samples/diff_report.md`](doc/samples/diff_report.md) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html). Added tests `GenerateDiffReport_Sha256MismatchDetailTable_AppearsImmediatelyAfterSha256Warning` in [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) and `GenerateDiffReportHtml_Sha256MismatchDetailTable_AppearsImmediatelyAfterSha256Warning` in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs).

- **Streamline report table columns per section** — Markdown: Removed the `Disassembler` column from tables where it is not meaningful: `[ x ] Ignored Files`, `SHA256Mismatch (Manual Review Recommended)` warning table, and `Timestamps Regressed` warning table. Removed both the `Legend`/`Diff Reason` and `Disassembler` columns from `[ + ] Added Files` and `[ - ] Removed Files` tables. HTML: All tables retain all 8 columns in the DOM for cross-table column-width synchronization stability; unwanted columns are hidden via CSS classes (`hide-disasm`, `hide-col6`) that set `width: 0`, `visibility: hidden`, and `border-color: transparent`. The `[ = ] Unchanged Files` table retains the `Disassembler` column (ILMatch rows display the disassembler version) and now also populates it in the HTML report. The `[ * ] Modified Files` table is unchanged. `AppendTableStart` helper accepts a `hideClasses` parameter to apply CSS hide classes to the `<table>` element; header `<th>` elements for col6 and Disassembler now carry `col-diff-hd` / `col-disasm-hd` classes for CSS targeting. `syncTableWidths()` skips hidden columns when calculating table width. Updated [`doc/samples/diff_report.md`](doc/samples/diff_report.md) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html). Added tests `GenerateDiffReport_ColumnStructure_PerTableColumns`, `GenerateDiffReport_WarningsColumnStructure_NoDisassemblerColumn` in [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) and `GenerateDiffReportHtml_ColumnStructure_PerTableColumns` in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs).

- **Add column headers to Summary and IL Cache Stats HTML tables** — The `stat-table` tables (Summary and IL Cache Stats) now include a `<thead>` row with column headers (`Category | Count` for Summary, `Metric | Value` for IL Cache Stats). The `<th>` elements use `border: 1px solid #bbb` to match the border thickness and color of the `[ x ] Ignored Files` and other main table headers. Updated [`doc/samples/diff_report.html`](doc/samples/diff_report.html) and [`diff_report.css`](Services/HtmlReport/diff_report.css).

- **Center-align Legend column body in Markdown tables** — The Legend (Diff Reason) column in all Markdown report tables is now center-aligned (`:------:` separator) instead of left-aligned. Affected tables: `[ x ] Ignored Files`, `[ = ] Unchanged Files`, `[ * ] Modified Files`, `[ ! ] SHA256Mismatch`, and `[ ! ] Timestamps Regressed`. Updated output logic in [`ReportGenerateService.SectionWriters.cs`](Services/ReportGenerateService.SectionWriters.cs) and sample [`doc/samples/diff_report.md`](doc/samples/diff_report.md). Updated assertion in [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs).

- **Remove summary count table from semantic changes** — Removed the per-assembly `sc-count` summary table (Class / Status / High / Medium / Low / Total) from the semantic changes output. The table added visual noise without actionable value; the detail table already contains all information. Removed `AppendSummaryCountTable` method, `ChangeOrder` helper, and the call site from [`HtmlReportGenerateService.Sections.cs`](Services/HtmlReport/HtmlReportGenerateService.Sections.cs). Removed `sc-count` / `sc-cnt-*` CSS rules from [`diff_report.css`](Services/HtmlReport/diff_report.css). Removed `cntW` calculation, `table.sc-count` selector, and `--sc-cnt-class-w` CSS variable references from [`diff_report.js`](Services/HtmlReport/diff_report.js). Updated [`doc/samples/diff_report.html`](doc/samples/diff_report.html).

- **Change Importance column styling to text color** — Replaced background-fill styling on the Importance column in the semantic changes detail table with text-only styling: `High` is rendered in red bold (`color:#d1242f;font-weight:bold`), `Medium` in vivid orange bold (`color:#d97706;font-weight:bold`), and `Low` is unstyled. Removed `TH_BG_IMPORTANCE_HIGH` / `TH_BG_IMPORTANCE_MEDIUM` constants. Renamed `ImportanceToStatusBg` to `ImportanceToStyle` in [`HtmlReportGenerateService.Helpers.cs`](Services/HtmlReport/HtmlReportGenerateService.Helpers.cs). Updated [`doc/samples/diff_report.html`](doc/samples/diff_report.html).

- **Simplify button and banner icons to monotone** — Removed the `▲` icon from the "Fold all details" button in [`HtmlReportGenerateService.cs`](Services/HtmlReportGenerateService.cs). Replaced emoji icons in the reviewed-mode banner: removed the `🔒` lock from the "Reviewed:" text, replaced the `🔍` magnifying glass on the "Verify integrity" button with a monotone `✓` check mark. Updated [`diff_report.js`](Services/HtmlReport/diff_report.js) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html).

- **Sort semantic changes detail table by Status then Importance** — The semantic changes detail table now sorts entries by Status (`[ + ]` Added → `[ - ]` Removed → `[ * ]` Modified) first, then by Importance descending (`High` → `Medium` → `Low`) within each status group. Previously, entries were sorted only by Importance descending. Updated `EntriesByImportance` in [`AssemblySemanticChangesSummary`](Models/AssemblySemanticChangesSummary.cs) to use `.OrderBy(ChangeOrder).ThenByDescending(Importance)`. Updated test `EntriesByImportance_SortsByChangeThenImportance` in [`AssemblySemanticChangesSummaryTests`](FolderDiffIL4DotNet.Tests/Models/AssemblySemanticChangesSummaryTests.cs). Updated [`doc/samples/diff_report.html`](doc/samples/diff_report.html) base64 blocks.

- **Style Legend (Change Importance) labels with color instead of code tags** — In the Legend (Change Importance) table, the Label column body cells now use the same colored-text styling as the detail table: `High` in red bold, `Medium` in orange bold, `Low` unstyled. Previously, labels were wrapped in `<code>` tags. Updated [`HtmlReportGenerateService.Sections.cs`](Services/HtmlReport/HtmlReportGenerateService.Sections.cs) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html).

- **Add column header to IL ignore-strings table** — The "Note: When diffing IL, lines containing any of the configured strings are ignored:" table now includes a `<thead>` with "Ignored String" header, styled with `background:#f5f5f7` matching the Legend (Diff Detail) table header design. Updated [`HtmlReportGenerateService.Sections.cs`](Services/HtmlReport/HtmlReportGenerateService.Sections.cs) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html).

- **Align Summary and IL Cache Stats table header backgrounds** — The `<th>` elements in the Summary and IL Cache Stats `stat-table` tables now include `style="background:#f5f5f7"`, matching the Legend (Diff Detail) table header background. Updated [`HtmlReportGenerateService.Sections.cs`](Services/HtmlReport/HtmlReportGenerateService.Sections.cs) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html).

- **Wrap Access and Modifiers columns in `<code>` tags with arrow-aware formatting** — The Access and Modifiers columns in the semantic changes detail table now wrap values in `<code>` tags. For arrow-containing values (e.g. `public → private`), each side is wrapped individually: `<code>public</code> → <code>private</code>`. Added `CodeWrapArrow` helper in [`HtmlReportGenerateService.Helpers.cs`](Services/HtmlReport/HtmlReportGenerateService.Helpers.cs). Updated [`doc/samples/diff_report.html`](doc/samples/diff_report.html) base64 blocks.

- **Widen Access column in semantic changes table** — Increased `col.sc-col-access-g` from `8em` to `16em` in [`diff_report.css`](Services/HtmlReport/diff_report.css) to accommodate arrow notation values like `public → private` with `<code>` wrapping. Updated [`doc/samples/diff_report.html`](doc/samples/diff_report.html).

- **Document Disassembler Availability edge cases in Developer Guide** — Added bilingual (EN/JP) documentation to [`doc/DEVELOPER_GUIDE.md`](doc/DEVELOPER_GUIDE.md) describing the Disassembler Availability table behavior for edge cases: all-text-files scenario, `SkipIL` mode, no tools available, and null/empty probe results.

- **Simplify Verify integrity for `.sha256` files** — When verifying a `.sha256` file, the reviewed HTML no longer needs a second file picker because the HTML is "self" — it already has the final hash embedded. Added a `__finalSha256__` constant that is populated at download time via a second placeholder technique (`fff...f`). The `.sha256` verification path now compares the file content directly against `__finalSha256__` and shows a pass/fail result. Updated [`diff_report.js`](Services/HtmlReport/diff_report.js) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html).

#### Fixed

- **Legend table header border color mismatch** — Changed `legend-table th` border from `1px solid #ddd` to `1px solid #bbb` in both [`diff_report.css`](Services/HtmlReport/diff_report.css) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html), matching the standard table header border color used by `[ x ] Ignored Files` and other file listing tables.

- **Missing importance column width in semantic changes table** — Added `col.sc-col-importance-g { width: 7em; }` CSS rule that was present in the source CSS but missing from [`doc/samples/diff_report.html`](doc/samples/diff_report.html). Also added the importance column (7em) to the `syncScTableWidths()` `detW` calculation in [`diff_report.js`](Services/HtmlReport/diff_report.js), fixing an underestimated detail table width.

- **Sample HTML JavaScript out of sync with source** — Synchronized the JavaScript in [`doc/samples/diff_report.html`](doc/samples/diff_report.html) with the source [`diff_report.js`](Services/HtmlReport/diff_report.js): corrected `DOMContentLoaded` initialization order (`initColResize` → `syncTableWidths` → `syncScTableWidths` → `setupLazyDiff`); added `:not(.legend-table):not(.il-ignore-table)` exclusions to the `syncTableWidths()` selector; added `if (w > 0)` guard before setting table width; restored missing code comments; reordered function definitions to match source.

- **Stale base64 semantic changes block in sample HTML** — Replaced a manually crafted base64 block that incorrectly showed both a caveat note and "No semantic changes detected for this assembly." with the correct output: "No structural changes detected. See IL diff for implementation-level differences." without the caveat note. The caveat note is only shown when structural changes exist, matching the code logic in [`HtmlReportGenerateService.Sections.cs`](Services/HtmlReport/HtmlReportGenerateService.Sections.cs).

- **"an SHA256" article typo** — Corrected "only an SHA256 hash comparison" to "only a SHA256 hash comparison" in [`doc/samples/diff_report.md`](doc/samples/diff_report.md) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match the constant in [`Constants.cs`](Common/Constants.cs).

- **Strict IOException handling in preflight write-permission check** — `CheckReportsParentWritableOrThrow` previously caught `IOException` and silently returned, which could mask environment-specific permission problems (e.g. read-only filesystem mounts, network-share write failures, path-related I/O errors) that are not covered by the upstream disk-space check. The method now logs the cause-specific `IOException` details via `ILoggerService` and re-throws as a new `IOException` with a descriptive message, enabling fail-fast diagnostics. Both `ValidateRunDirectories` and `CheckReportsParentWritableOrThrow` now accept an `ILoggerService` parameter, consistent with the existing `ValidateReportLabel` pattern. Updated existing tests (`CheckReportsParentWritableOrThrow_WhenDirectoryIsReadOnly_ThrowsUnauthorizedAccessException`, `CheckReportsParentWritableOrThrow_NonexistentParent_DoesNotThrow`, `ValidateRunDirectories_*`) to pass the logger argument. Added new tests `CheckReportsParentWritableOrThrow_WritableDirectory_DoesNotThrow` and `CheckReportsParentWritableOrThrow_WhenDirectoryIsReadOnly_LogsAndThrowsIOException` in [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs).

- **Shorthand type names in sample HTML** — Replaced C# alias type names (`string`, `int`, `void`) with fully qualified .NET names (`System.String`, `System.Int32`, `System.Void`) in all base64-encoded semantic changes blocks in [`doc/samples/diff_report.html`](doc/samples/diff_report.html). The `SimpleSignatureTypeProvider` always outputs fully qualified names, so the sample must match.

- **Missing parameter name in sample HTML** — Fixed the Execute method entry in [`doc/samples/diff_report.html`](doc/samples/diff_report.html) base64 block where the Parameters column showed `System.String` without a parameter name. Corrected to `System.String command = null` to demonstrate both parameter naming and default value syntax.

- **Undefined `scheduleSave()` function call** — Fixed `collapseAll()` in [`diff_report.js`](Services/HtmlReport/diff_report.js) which called `scheduleSave()` — a function that was never defined. Replaced with `autoSave()` which is the correct existing function. Updated [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.

- **CI test assertion mismatch after `<code>` wrapping change** — Test `GenerateDiffReportHtml_AssemblySemanticChanges_KindBodyUseCodeEmphasis_AccessModifiersDoNot` was asserting that Access/Modifiers columns should NOT be wrapped in `<code>` tags, but the output logic now wraps them. Renamed test to `KindBodyAccessModifiersUseCodeEmphasis` and updated assertions to expect `<code>` wrapping. Added new test `AccessArrowWrapsEachSideInCode` verifying arrow-aware formatting in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs).

- **Simplify Verify integrity to `.sha256`-only verification** — Removed the HTML self-verification path from `verifyIntegrity()`. Since the reviewed HTML is "self", only the companion `.sha256` file needs to be selected. Added a `__finalSha256__` constant populated at download time via a second placeholder technique (`fff...f`); the `.sha256` verification path compares the file content directly against `__finalSha256__`. The file picker uses `input.accept = '.sha256'` to restrict selection; a pre-created hidden `<input>` element is added to the DOM at `DOMContentLoaded` time so the accept filter is reliably applied on the first click (some browsers ignore `accept` on dynamically created inputs that are clicked immediately). An `onchange` guard rejects non-`.sha256` files as a fallback. Updated [`diff_report.js`](Services/HtmlReport/diff_report.js) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html).

### [1.6.0] - 2026-03-21

#### Changed

- **Immutable ConfigSettings via IReadOnlyConfigSettings** — Introduced `IReadOnlyConfigSettings` interface exposing all `ConfigSettings` properties as read-only (list properties return `IReadOnlyList<string>`). `ConfigSettings` now implements this interface. All downstream service constructors (`FolderDiffService`, `FileDiffService`, `ILOutputService`, `DotNetDisassembleService`, `ILCachePrefetcher`, `ProgressReportService`, `FolderDiffExecutionStrategy`, `ReportGenerateService`, `HtmlReportGenerateService`) and `ReportWriteContext` accept `IReadOnlyConfigSettings` instead of the mutable `ConfigSettings`. Only `ProgramRunner.ApplyCliOverrides` retains mutable access. Added tests `ConfigSettings_ImplementsIReadOnlyConfigSettings` and `IReadOnlyConfigSettings_ListProperties_AreReadOnly` in `ConfigSettingsTests`.

- **Logger thread safety** — Added a `lock` around log file writes in `LoggerService.LogMessage()` to prevent `IOException` when multiple threads call the logger concurrently during parallel diff processing. The lock object (`_fileWriteLock`) serialises only the file I/O section; console output remains unguarded as it is inherently thread-safe.

- **Consolidated redundant exception handling with exception filters** — Replaced repetitive per-type `catch` blocks that performed identical actions with C# exception filters (`catch (Exception ex) when (ex is X or Y or Z)`). Affected files: `ProgramRunner.cs`, `FolderDiffService.cs`, `FileDiffService.cs`, `LoggerService.cs`, `ILOutputService.cs`, `ILDiskCache.cs`, `ILCache.cs`, `DotNetDisassemblerCache.cs`, `ILCachePrefetcher.cs`, `ILTextOutputService.cs`, `DotNetDisassembleService.cs`, `DotNetDisassembleService.VersionLabel.cs`, `ReportGenerateService.cs`, `HtmlReportGenerateService.cs`, `DotNetDetector.cs`. No behavioural change; purely a code-size and maintainability improvement.

- **Extract HTML report CSS/JS to embedded resources** — Moved the CSS stylesheet and JavaScript from inline C# string literals in `HtmlReportGenerateService.Css.cs` / `HtmlReportGenerateService.Js.cs` to standalone files (`Services/HtmlReport/diff_report.css`, `Services/HtmlReport/diff_report.js`) compiled as `<EmbeddedResource>`. At runtime, `LoadEmbeddedResource()` reads the resources via `Assembly.GetManifestResourceStream()`. The JS file uses `{{STORAGE_KEY}}` / `{{REPORT_DATE}}` placeholders that are replaced at report generation time. No behavioural change to the generated HTML report. Added tests `LoadEmbeddedResource_CssResource_ReturnsNonEmptyString`, `LoadEmbeddedResource_JsResource_ReturnsNonEmptyString`, `LoadEmbeddedResource_JsResource_ContainsPlaceholders`, `LoadEmbeddedResource_InvalidResource_ThrowsFileNotFoundException` in `HtmlReportGenerateServiceTests`.

- **Migrate MD5 to SHA256 for file hash comparison** — Replaced all `MD5` usage with `SHA256` across the entire codebase. `FileComparer.DiffFilesByHashAsync()` now uses `SHA256.Create()` instead of `MD5.Create()`. `ComputeFileMd5Hex()` renamed to `ComputeFileSha256Hex()`. Enum values `MD5Match`/`MD5Mismatch` renamed to `SHA256Match`/`SHA256Mismatch`. Property `HasAnyMd5Mismatch` renamed to `HasAnySha256Mismatch`. `WARNING_MD5_MISMATCH` renamed to `WARNING_SHA256_MISMATCH`. All report labels, sample reports, README, DEVELOPER_GUIDE, TESTING_GUIDE, and tests updated. SHA256 provides stronger collision resistance and leverages SHA-NI hardware acceleration on modern CPUs. The IL cache is key-compatible (file-content hash + tool label) and requires no migration; old MD5-keyed entries expire naturally via TTL.

- **Replace direct Console.WriteLine with logger in FolderDiffService** — Replaced the `Console.WriteLine(LOG_FOLDER_DIFF_COMPLETED)` / `Console.Out.Flush()` call in `FolderDiffService.ExecuteFolderDiffAsync()` with `_logger.LogMessage(AppLogLevel.Info, ..., shouldOutputMessageToConsole: true)`. This routes the "Folder diff completed." message through the same `ILoggerService` pipeline as all other log messages, ensuring it is written to both the log file and the console. The `ConsoleRenderCoordinator.RenderSyncRoot` lock and the `using FolderDiffIL4DotNet.Core.Console` directive were removed as no longer needed. Added test `ExecuteFolderDiffAsync_WhenCompleted_LogsFolderDiffCompletedViaLogger` in [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs).

- **Propagate CancellationToken through diff pipeline** — Added `CancellationToken cancellationToken = default` parameter to all async methods in the diff pipeline: `IFolderDiffService.ExecuteFolderDiffAsync`, `IFileDiffService.PrecomputeAsync`, `IFileDiffService.FilesAreEqualAsync`, `IILOutputService.PrecomputeAsync`, `IILOutputService.DiffDotNetAssembliesAsync`, `IDotNetDisassembleService.DisassemblePairWithSameDisassemblerAsync`, `IDotNetDisassembleService.PrefetchIlCacheAsync`. The token is propagated from `FolderDiffService` through `FileDiffService`, `ILOutputService`, `DotNetDisassembleService`, and `ILCachePrefetcher`. `Parallel.ForEachAsync` calls now pass the token via `ParallelOptions.CancellationToken`. `cancellationToken.ThrowIfCancellationRequested()` is called at key loop boundaries in sequential diff classification, precompute batching, and per-file comparison entry. All default parameters ensure backward compatibility. Added test `ExecuteFolderDiffAsync_WhenCancelled_ThrowsOperationCanceledException` in [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs).

- **Add bilingual (EN/JP) code comments throughout** — Added bilingual XML doc comments and inline comments to all test files that previously lacked Japanese translations. Affected files: `CoreSeparationTests`, `ProcessHelperTests`, `AssemblySemanticChangesSummaryTests`, `DotNetDisassemblerCacheTests`, `FileDiffServiceUnitTests`, `FolderDiffExecutionStrategyTests`, `ILOutputServiceTests`, `ProgressReportServiceTests`. All main source files already had 100% bilingual coverage; this change completes the test layer.

- **Replace test magic numbers with ConfigSettings defaults** — Introduced `public const` default-value constants on `ConfigSettings` (e.g. `DefaultTextDiffParallelThresholdKilobytes`, `DefaultTextDiffChunkSizeKilobytes`, `DefaultILPrecomputeBatchSize`, `DefaultDisassemblerBlacklistTtlMinutes`, `DefaultInlineDiffMaxDiffLines`, etc.) and updated all property initialisers to reference them. Test files (`ConfigSettingsTests`, `FileDiffServiceUnitTests`, `FolderDiffServiceUnitTests`, `ConfigServiceTests`, `TextDifferTests`) now use these named constants instead of bare numeric literals, eliminating scattered magic numbers and ensuring tests automatically stay in sync when defaults change.

#### Fixed

- **Method access modifier change detection** — Access modifier changes (e.g. `public` → `internal`) and modifier changes (e.g. adding/removing `static`, `virtual`) are now detected as `Modified` entries in the Assembly Semantic Changes table. Previously, the method match key excluded access modifiers and the intersection comparison only checked IL body bytes, so access-only or modifier-only changes were invisible in the semantic summary.

- **Property/Field type and modifier change detection** — Type changes (e.g. `string` → `int`), access modifier changes, and modifier changes for properties and fields are now detected as `Modified` entries. Previously, property and field keys were name-based only, with no `Modified` comparison for matching keys, so same-name type changes or access changes were not reported.

#### Added

- **MD5Mismatch detail table in Warnings section** — When one or more files are classified as `MD5Mismatch`, the Warnings section now includes a detail table titled `[ ! ] Modified Files — MD5Mismatch (Manual Review Recommended)` listing all affected files with their timestamps, diff detail, and disassembler columns. The table uses the same column layout and blue color scheme as the existing `[ ! ] Modified Files — Timestamps Regressed` table. Files are sorted alphabetically by path. Applies to both Markdown (`diff_report.md`) and HTML (`diff_report.html`) reports. The MD5Mismatch table appears before the Timestamps Regressed table when both warnings are present. Updated [`doc/samples/diff_report.md`](doc/samples/diff_report.md) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html). Added tests `Md5MismatchWarning_IncludesDetailTable`, `Md5MismatchTable_AppearsBeforeTimestampRegressedTable` in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) and `WritesMd5MismatchDetailTable_WhenMd5MismatchExists`, `Md5MismatchTable_AppearsBeforeTimestampRegressedTable` in [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs).

- **Semantic summary caveat note** — Added a caveat note to the HTML report's Assembly Semantic Changes section reminding users that the semantic summary is supplementary and to verify details in the IL diff. Styled with `.sc-caveat` CSS class (italic, grey). Updated [`doc/samples/diff_report.html`](doc/samples/diff_report.html). Added tests `ShowsCaveatNote`, `CaveatCssExists` in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs).

#### Removed

- **Assembly Semantic Changes section in Markdown report** — The `## Assembly Semantic Changes` section has been removed from the Markdown report (`diff_report.md`). Assembly Semantic Changes are now shown only in the HTML report as expandable inline rows. Removed `AssemblySemanticChangesSectionWriter` and its helper methods from `ReportGenerateService.SectionWriters.cs`. Removed the corresponding section from [`doc/samples/diff_report.md`](doc/samples/diff_report.md). Updated tests in [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) to assert the section does not appear.

#### Changed

- **Modified entry old→new display** — For `Modified` entries in the Assembly Semantic Changes table, the Access, Modifiers, and Type columns now show old and new values in `old → new` format when the value has changed (e.g. `public → internal`, `System.String → System.Int32`). When unchanged, only the current value is shown.

- **Access and Modifiers columns — no code emphasis** — The Access and Modifiers column body cells in the Assembly Semantic Changes table no longer use `<code>` emphasis. These columns may contain `old → new` arrow notation, and applying monospace emphasis to such transitional text was visually inconsistent. Kind and Body columns still use `<code>` emphasis. Updated [`doc/samples/diff_report.html`](doc/samples/diff_report.html) (added a row demonstrating Modifiers `old → new`). Updated test `KindBodyUseCodeEmphasis_AccessModifiersDoNot` in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs).

- **Unchanged Files table sort order** — Rows are now sorted by diff-detail result (`MD5Match` → `ILMatch` → `TextMatch`), then by File Path ascending. Previously sorted by File Path only. Applies to both Markdown and HTML reports.

- **Modified Files table sort order** — Rows are now sorted by diff-detail result (`TextMismatch` → `ILMismatch` → `MD5Mismatch`), then by File Path ascending. Previously sorted by File Path only. Applies to both Markdown and HTML reports.

- **Modified Files — Timestamps Regressed table sort order** — Same sort order as Modified Files table (`TextMismatch` → `ILMismatch` → `MD5Mismatch`, then path). Previously sorted by File Path only. Applies to both Markdown and HTML reports.

### [1.5.0] - 2026-03-21

#### Added

- Added **Assembly Semantic Changes** — member-level change detection for `ILMismatch` assemblies using `System.Reflection.Metadata`. For each modified .NET assembly, the report now shows type/method/property/field additions, removals, and method body changes. Controlled by the new `ShouldIncludeAssemblySemanticChangesInReport` config setting (default: `true`).
  - **Report placement**: shown as an expandable inline row above the IL diff in the HTML report.
  - **Table columns** (12 including checkbox): `✓` (checkbox), `Class` (fully qualified type name), `BaseType` (base class + interfaces), `Status` (`Added`/`Removed`/`Modified` with `[ + ]`/`[ - ]`/`[ * ]` markers), `Kind` (`Class`/`Record`/`Struct`/`Interface`/`Enum`/`Constructor`/`StaticConstructor`/`Method`/`Property`/`Field`), `Access` (`public`/`internal`/`protected`/`private`), `Modifiers` (`static`/`abstract`/`virtual`/`override`/`sealed`/`readonly`/`const`/`static literal`/`static readonly`), `Type` (declared type for Field/Property only), `Name`, `ReturnType`, `Parameters` (displayed without parentheses), `Body` (`Changed` when method body or field initializer IL has changed; otherwise empty).
  - **Summary count table**: a second table grouped by Class showing `Added`/`Removed`/`Modified` counts. Consecutive rows for the same class merge the Class cell.
  - **Record detection**: records are identified heuristically by the presence of an `EqualityContract` property.
  - **HTML styling**: table header background `#98989d` (light grey), data cell (`td`) background `#fff` (white) for contrast against the `diff-row` grey, per-row checkbox with auto-save, resizable columns via CSS custom properties and drag handles.
  - **New files**: [`AssemblyMethodAnalyzer`](Services/AssemblyMethodAnalyzer.cs), [`AssemblySemanticChangesSummary`](Models/AssemblySemanticChangesSummary.cs) (with computed `AddedCount`/`RemovedCount`/`ModifiedCount` properties).
  - **Tests**: [`AssemblyMethodAnalyzerTests`](FolderDiffIL4DotNet.Tests/Services/AssemblyMethodAnalyzerTests.cs), [`AssemblySemanticChangesSummaryTests`](FolderDiffIL4DotNet.Tests/Models/AssemblySemanticChangesSummaryTests.cs), and new assertions in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) (`TableHeaderUsesLighterGray`, `ThScColCbCssRuleExists`, `TdHasWhiteBackground`).
  - **Docs**: added bilingual **Assembly Semantic Changes** section to [README.md](README.md), [`AssemblyMethodAnalyzerTests`](FolderDiffIL4DotNet.Tests/Services/AssemblyMethodAnalyzerTests.cs) and [`AssemblySemanticChangesSummaryTests`](FolderDiffIL4DotNet.Tests/Models/AssemblySemanticChangesSummaryTests.cs) to [TESTING_GUIDE.md](doc/TESTING_GUIDE.md), updated [`doc/samples/diff_report.html`](doc/samples/diff_report.html).

#### Changed

- **Legend section** — Converted from bullet list to table format in both Markdown (`| Label | Description |`) and HTML (`<table class="legend-table">`). Added CSS for `table.legend-table` with borders and padding.

- **IL Cache Stats and Summary sections** — Converted from bullet list to table format in Markdown report (`| Category | Count |` and `| Metric | Value |`). Added visible borders (`1px solid #ddd`) to `table.stat-table td` in HTML report CSS.

- **File listing sections** — Converted Ignored, Unchanged, Added, Removed, and Modified file sections from checkbox bullet lists to tables in Markdown report with columns: `| Status | File Path | Timestamp | Legend | Disassembler |`.

- **`InlineDiffMaxEditDistance` highlighting** — Added `<code>` tag wrapping for `InlineDiffMaxEditDistance` in HTML report truncation messages, matching the existing `InlineDiffMaxDiffLines` code styling.

- **Diff row background color** — Changed `tr.diff-row` background from `#f6f8fa` to `#edf0f4` for better contrast with white semantic changes tables.

- **Myers Diff citation** — Bolded the volume number "1" after "Algorithmica" (`<b>1</b>(2)` / `**1**(2)`) across all Markdown and HTML report files.

- **Clipboard copy button** — Added a per-row clipboard copy button (two overlapping squares icon) to each File Path cell in HTML report tables. The `copyPath(btn)` function copies the individual file path to clipboard with a checkmark feedback animation. Replaced the previous column-header-level copy button.

- **Row hover highlight** — Added light purple hover highlight (`#f3eef8`) to file table rows and semantic changes table rows. Stat tables (Summary, IL Cache Stats), Legend table, and IL ignore strings table are excluded from hover highlighting.

- **Summary table row colors** — Added background colors to Summary table rows in the HTML report: Added (`#e6ffed` green), Removed (`#ffeef0` red), Modified (`#e3f2fd` blue), matching the corresponding section header colors.

- **Legend table width** — Constrained the Legend table width in HTML report (`max-width: 44em`) to prevent it from stretching too wide since its content is fixed text.

- **IL ignore strings table** — Converted the IL line-ignore-by-contains strings from an inline comma-separated list to a table format in both Markdown (`| Ignored String |`) and HTML (`<table class="legend-table">`), with one string per row for better readability.

- **Timestamp brackets removed** — Removed the `[` and `]` brackets from Timestamp column values in Markdown report tables, since brackets are unnecessary when timestamps are already in table cells.

### [1.4.1] - 2026-03-20

#### Added

- Added a [Myers Diff Algorithm](http://www.xmailserver.org/diff2.pdf) reference note to the HTML report header: a clickable citation of E. W. Myers, "An O(ND) Difference Algorithm and Its Variations," *Algorithmica* **1**(2), 1986, noting that inline diffs for `ILMismatch` and `TextMismatch` are computed using this algorithm. Updated [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match. Added test `GenerateDiffReportHtml_Header_ContainsMyersDiffAlgorithmReference` to [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs).

#### Changed

- Decomposed 4 large classes into partial class files for maintainability without changing public API: [`ProgramRunner`](ProgramRunner.cs) (extracted `ProgramRunner.Types.cs`), [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) (extracted `Sections.cs`, `Helpers.cs`, `Css.cs`, `Js.cs` under `Services/HtmlReport/`), [`FolderDiffService`](Services/FolderDiffService.cs) (extracted `ILPrecompute.cs`, `DiffClassification.cs`), [`ReportGenerateService`](Services/ReportGenerateService.cs) (extracted `SectionWriters.cs`).

- Enabled `<Nullable>enable</Nullable>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in both [`FolderDiffIL4DotNet.csproj`](FolderDiffIL4DotNet.csproj) and [`FolderDiffIL4DotNet.Core.csproj`](FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj). All nullable reference type annotations (`?` suffixes, `null!` initializers) have been applied across both projects (31 files), and the temporary `<NoWarn>` suppressions for CS8600–8604/CS8618/CS8625 have been removed. XML doc warnings (CS1591, CS1573) remain suppressed until a full documentation pass.

#### Added

- Added [`FolderDiffIL4DotNet.Benchmarks`](FolderDiffIL4DotNet.Benchmarks/) project with [BenchmarkDotNet](https://www.nuget.org/packages/BenchmarkDotNet/) 0.14.0. Includes [`TextDifferBenchmarks`](FolderDiffIL4DotNet.Benchmarks/TextDifferBenchmarks.cs) (small/medium/large IL-like diff) and [`FolderDiffBenchmarks`](FolderDiffIL4DotNet.Benchmarks/FolderDiffBenchmarks.cs) (file enumeration and hash comparison). Run with `dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks`.

- E2E disassembler tests now require `FOLDERDIFF_RUN_E2E=true` environment variable in addition to tool availability. This gives CI pipelines explicit control over E2E test execution.

#### Fixed

- Fixed HTML report Timestamp column being truncated on macOS in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs). The column was declared `width: 16em` with `overflow: hidden`, which clipped the dual-timestamp format `[YYYY-MM-DD HH:MM:SS → YYYY-MM-DD HH:MM:SS]` (~300 px) on macOS due to its wider font metrics (SF Pro), while Windows happened to fit. The fix widens `col.col-ts-g` from `16em` to `22em` and removes the redundant `width: 16em` and `overflow: hidden` declarations from `td.col-ts`, relying on the `<col>` width and `white-space: nowrap` to keep timestamps on one line without clipping. Updated the CSS assertion in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs). Synced [`doc/samples/diff_report.html`](doc/samples/diff_report.html).

### [1.4.0] - 2026-03-20

#### Added

- Added `--print-config` CLI flag to [`ProgramRunner`](ProgramRunner.cs). Running `FolderDiffIL4DotNet --print-config` (optionally combined with `--config <path>`) loads the effective configuration — [`config.json`](config.json) deserialized and all `FOLDERDIFF_*` environment variable overrides applied — then serializes it as indented JSON to standard output and exits with code 0. This makes it easy to inspect the full default and overridden config without reading source code, and to generate a pre-populated [`config.json`](config.json) by redirecting the output. Config load errors (missing file, invalid JSON) exit with code 3 and print the error to stderr. Implementation adds `PrintConfig` to [`CliOptions`](Runner/CliOptions.cs) and [`CliParser`](Runner/CliParser.cs), adds `PrintConfigAsync` to [`ProgramRunner`](ProgramRunner.cs), and updates `--help` output. Also fixed the missing `FOLDERDIFF_INLINEDIFFLAZYRENDER` env var override in [`ConfigService.ApplyEnvironmentVariableOverrides`](Services/ConfigService.cs) (the [`InlineDiffLazyRender`](Models/ConfigSettings.cs) property was added in a prior release but its env var entry was omitted). Added 4 tests to [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) (`PrintConfigFlag_ExitsZeroAndOutputsJson`, `PrintConfigFlag_ReflectsEnvVarOverride`, `PrintConfigFlag_WithCustomConfigPath_ReflectsCustomValues`, `PrintConfigFlag_WithMissingConfig_ReturnsConfigurationError`). Updated bilingual [README.md](README.md) and [CHANGELOG.md](CHANGELOG.md).

- Added a `test-windows` job to [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml) that runs the full test suite on `windows-latest` in parallel with the existing Ubuntu `build` job. The Windows job restores, builds, installs [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/), and runs tests with `DOTNET_ROLL_FORWARD=Major`. This ensures the E2E disassembler test ([`RealDisassemblerE2ETests`](FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs)) that was previously always skipped in CI (skipped on Linux because `dotnet-ildasm` was not found, or skipped because the tool version does not match) now executes on every push. Updated bilingual [doc/TESTING_GUIDE.md](doc/TESTING_GUIDE.md) and [doc/DEVELOPER_GUIDE.md](doc/DEVELOPER_GUIDE.md).

- Added [`InlineDiffLazyRender`](Models/ConfigSettings.cs) config setting (default `true`). When enabled, inline diff tables are Base64-encoded and stored in a `data-diff-html` attribute on each `<details>` element instead of being rendered as live DOM children. JavaScript decodes and injects the HTML into the DOM only when the user expands that row. For reports with many modified files this eliminates millions of initial DOM nodes — for example 5 000 modified files × 200 diff rows × 3 cells = ~3 M fewer nodes — making the initial page load and interactions (Clear all, column resize, localStorage save) dramatically faster. The `setupLazyDiff()` and `decodeDiffHtml()` JavaScript functions are always included in the HTML and are no-ops when there are no `data-diff-html` attributes. Setting [`InlineDiffLazyRender`](Models/ConfigSettings.cs) = `false` restores the previous behaviour where all diff content is embedded directly in the DOM, which keeps it findable via the browser's _Find in page_ function. Implementation in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) (new `BuildDiffViewHtml` helper, lazy/non-lazy branch in `AppendInlineDiffRow`, `setupLazyDiff` / `decodeDiffHtml` JS). Added property to [`ConfigSettings`](Models/ConfigSettings.cs). Added 4 tests to [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) (`LazyRender_DiffContentStoredInDataAttribute`, `LazyRender_DataAttributeDecodesCorrectly`, `LazyRender_False_DiffContentIsInline`, `LazyRender_JsSetupFunctionPresent`). Updated `CreateConfig` helper to accept `lazyRender` parameter (default `false` to preserve existing test semantics). Updated bilingual [README.md](README.md) and [CHANGELOG.md](CHANGELOG.md).

- Added environment variable overrides for all scalar config settings. Any non-list property in [`config.json`](config.json) can now be overridden at runtime without modifying the file by setting `FOLDERDIFF_<PROPERTYNAME>` (e.g. `FOLDERDIFF_MAXPARALLELISM=4`, `FOLDERDIFF_ENABLEILCACHE=false`, `FOLDERDIFF_ILCACHEDIRECTORYABSOLUTEPATH=/tmp/il-cache`). Bool values accept `true`/`false` (case-insensitive) and `1`/`0`; unrecognised values are silently ignored. Overrides are applied after JSON deserialization and before validation, so env-var values are subject to the same constraints as JSON values. Implementation in [`ConfigService.ApplyEnvironmentVariableOverrides`](Services/ConfigService.cs). Added 10 tests to [`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) covering int/bool/string overrides, env-var-wins-over-JSON, invalid values ignored, validation still runs, and case-insensitive bool variants. Added env var summary to `--help` output in [`ProgramRunner`](ProgramRunner.cs). Added bilingual "Environment Variable Overrides" section to [README.md](README.md).

- Added standalone Myers diff algorithm guide [`doc/MYERS_DIFF_ALGORITHM.md`](doc/MYERS_DIFF_ALGORITHM.md) (EN + JP): a comprehensive bilingual explanation of the Myers diff algorithm as implemented in [`TextDiffer.cs`](FolderDiffIL4DotNet.Core/Text/TextDiffer.cs), covering the edit graph model, diagonal / D-path theory, the forward pass (V array and greedy snake extension), backtracking from snapshots, worked example with full trace, O(D² + N + M) complexity analysis with concrete figures for 1 000 000-line IL files, implementation notes (offset trick, snapshot optimisation, early termination), and a comparison table of LCS / Myers / Patience / Histogram algorithms. The previous inline algorithm sections in README.md were replaced with a link to this guide.

- Improved branch coverage from 71.6 % to 83.7 % by adding 11 targeted tests covering previously untested branches in [`DisassemblerBlacklist`](Services/DisassemblerBlacklist.cs) and [`DisassemblerHelper`](Services/DisassemblerHelper.cs). New tests in [`DisassemblerBlacklistTests`](FolderDiffIL4DotNet.Tests/Services/DisassemblerBlacklistTests.cs): `RegisterFailure_NullOrWhitespace_DoesNotThrow_AndNoEntryCreated`, `ResetFailure_NullOrWhitespace_DoesNotThrow`, `ResetFailure_NonExistentCommand_DoesNotThrow`. New tests in [`DisassemblerHelperTests`](FolderDiffIL4DotNet.Tests/Services/DisassemblerHelperTests.cs): `ResolveExecutablePath_RelativePathWithSeparator_NonExistent_ReturnsNull`, `ResolveExecutablePath_RelativePathWithSeparator_Existing_ReturnsFullPath`, `ResolveExecutablePath_WhitespacePathVariable_ReturnsNull`, `ResolveExecutablePath_PathWithEmptyEntries_SkipsEmptyAndReturnsNull`, `ResolveExecutablePath_CommandFoundInPath_ReturnsAbsolutePath`, plus three Windows-only `EnumerateExecutableNames` tests verifying that commands already ending in `.exe`/`.cmd`/`.bat` do not accumulate duplicate extension variants. `DisassemblerBlacklist` branch coverage reaches 100 %; `DisassemblerHelper` moves from 53 % to 80 %.

#### Fixed

- Changed the default IL disk cache directory from the executable directory (`<exe>/ILCache`) to the OS-standard user-local data directory: `%LOCALAPPDATA%\FolderDiffIL4DotNet\ILCache` on Windows and `~/.local/share/FolderDiffIL4DotNet/ILCache` on macOS/Linux. The previous default caused startup failures in read-only or container deployments and silently wrote cache files into the installation directory in multi-user environments. The fix is in [`RunScopeBuilder.CreateIlCache`](Runner/RunScopeBuilder.cs) (changed `AppContext.BaseDirectory` to `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)`). Updated XML doc comment on [`ILCacheDirectoryAbsolutePath`](Models/ConfigSettings.cs) in [`ConfigSettings`](Models/ConfigSettings.cs), added test `CreateIlCache_WhenPathIsEmpty_DefaultsToLocalApplicationDataSubfolder` to [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs), and updated bilingual [README.md](README.md).

- Fixed HTML report inline diff numbering in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs): the `#N` prefix shown before `Show diff` / `Show IL diff` and inline-diff skip messages now uses the same one-based row number as the leftmost `#` column instead of the internal zero-based index. Added test `GenerateDiffReportHtml_InlineDiffSummary_UsesSameOneBasedNumberAsLeftmostColumn` to [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs), and updated [README.md](README.md) plus [testing guide](doc/TESTING_GUIDE.md).

### [1.3.0] - 2026-03-17

#### Changed

- Raised the default values of [`InlineDiffMaxOutputLines`](Models/ConfigSettings.cs) and [`InlineDiffMaxDiffLines`](Models/ConfigSettings.cs) from `500`/`1000` to **`10000`** each in [`ConfigSettings`](Models/ConfigSettings.cs), [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs), and [`TextDiffer`](FolderDiffIL4DotNet.Core/Text/TextDiffer.cs). Updated [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs), bilingual [README.md](README.md), and [developer guide](doc/DEVELOPER_GUIDE.md).

- Replaced the O(N×M) LCS algorithm in [`TextDiffer`](FolderDiffIL4DotNet.Core/Text/TextDiffer.cs) with **Myers diff** (O(D² + N + M) time, O(D²) space, where D = edit distance). The previous `m × n > 4 000 000` cell-count guard is replaced by a new [`InlineDiffMaxEditDistance`](Models/ConfigSettings.cs) config key (default `4 000`) that limits the number of inserted + deleted lines. Files with millions of lines now produce an inline diff as long as the actual change is small — for example, two 2 370 000-line IL files differing by 20 lines complete in milliseconds. Updated [`TextDifferTests`](FolderDiffIL4DotNet.Tests/Core/Text/TextDifferTests.cs): replaced `Compute_InputExceedsLcsLimit_ReturnsTruncatedMessage` with `Compute_EditDistanceExceedsLimit_ReturnsTruncatedMessage`; added `Compute_LargeFilesSmallEditDistance_ProducesCorrectDiff` and `Compute_VeryLargeFilesWithTinyDiff_ProducesInlineDiff`.

- Polished HTML report UX in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs): download button icon changed from `⇩` to `⤓` and label changed to "Download as reviewed"; the reviewed-file banner now reads `"Reviewed: <timestamp> — read-only"` instead of a plain lock icon; Added/Removed section heading and column header colours now follow the GitHub diff palette (`#22863a` green / `#b31d28` red / `#e6ffed` background for Added, `#ffeef0` background for Removed); the `No` column is widened to `3.2em` to accommodate files counts up to 999,999; empty `Diff Reason` cells no longer render a ghost `<code>` element. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.
- Fixed Timestamp column stability when File Path is resized in the HTML report: changed the main file-list tables from `width: auto` to `table-layout: fixed; width: 1px`, added a `syncTableWidths()` JavaScript function that sets each table's explicit pixel width to the sum of all column widths (using CSS custom properties `--col-reason-w`, `--col-notes-w`, `--col-path-w`, `--col-diff-w`), and calls it on `DOMContentLoaded` and after every column resize; wrapped column-header text in a `span.th-label { display: block; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }` element so header content clips reliably. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.
- Fixed reviewed-mode checkboxes appearing grey: replaced `cb.disabled = true` with `cb.style.pointerEvents = 'none'; cb.style.cursor = 'default';` so the browser's internal disabled-grey rendering is avoided and checkboxes retain their accent colour. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.
- Download-as-reviewed now bakes the current column widths as defaults into the reviewed file: `downloadReviewed()` reads the current effective CSS custom-property values for all five column-width variables, replaces the `:root` CSS rule in the exported HTML with those values, and removes the inline `style` attribute from the `<html>` element, so the reviewed snapshot opens with whatever column layout was active at sign-off time. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.
- "Clear all" in the HTML report now also resets column widths to defaults and collapses all inline diff `<details>` elements: `clearAll()` removes CSS custom-property overrides, calls `syncTableWidths()`, and calls `removeAttribute('open')` on every `<details>`. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.
- Changed `td.col-reason`, `td.col-ts`, and `td.col-diff` body cells to `text-align: center` in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) (`col-diff` shows "Location" (`old`/`new`/`old/new`) in the Ignored Files table and the diff type (`ILMismatch`, `TextMismatch`, etc.) in other tables); `td.col-path` (File Path) remains left-aligned; column headers and `td.col-notes` are unchanged. Updated test `GenerateDiffReportHtml_BodyCells_ColReasonPathTs_HaveCenterAlignment` in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) to match. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.
- Fixed `.reviewed-banner` text colour from `#2d7a2d` (green) to `#1f2328` (near-black) in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) so the reviewed-file timestamp banner is visually neutral. Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match.

#### Changed

- Restructured HTML report table columns in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs): the Timestamp column is narrowed from `22em` to `16em`; the Diff Reason column is narrowed from `20em` to `9em` and now shows only the diff type (e.g. `ILMismatch`, `TextMismatch`) without the disassembler label; a new 8th **Disassembler** column (`28em`, resizable) is added at the far right and displays the disassembler label and version string per-row. Updated JavaScript (`colVarNames`, `clearAll`, `syncTableWidths`) and CSS (`:root` custom properties, `col.col-*-g`, `td.col-*`) to match. Updated [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) to match the new [`InlineDiffMaxDiffLines`](Models/ConfigSettings.cs) threshold check.

- Replaced [`InlineDiffMaxInputLines`](Models/ConfigSettings.cs) with [`InlineDiffMaxDiffLines`](Models/ConfigSettings.cs) (default `1000`) in [`ConfigSettings`](Models/ConfigSettings.cs). Previously the config checked whether either input file exceeded a line-count threshold *before* computing the diff; the actual HTML output for inline diff only shows changed lines, so the input line count is not a meaningful proxy. The new setting checks the computed diff output line count *after* `TextDiffer.Compute()` and suppresses the inline diff display if it exceeds the threshold. Updated [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs), [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs), [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs), bilingual [README.md](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md).

#### Fixed

- Fixed ILMismatch inline diff never appearing in the HTML report: [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) was calling `ILCache.TryGetILAsync` with a normalised label (e.g. `ildasm (version: 1.0.0)`) that never matched the label stored at write time (e.g. `ildasm MyAssembly.dll (version: 1.0.0)`), so the look-up always returned `null` and the inline diff was silently skipped. Fixed by reading IL text directly from the `*_IL.txt` files produced by [`ILTextOutputService`](Services/ILOutput/ILTextOutputService.cs) (under `Reports/<label>/IL/old` and `Reports/<label>/IL/new`) when [`ShouldOutputILText`](Models/ConfigSettings.cs) is `true` (the default). Added test `GenerateDiffReportHtml_ILMismatch_WithILTextFiles_ShowsInlineDiff` to [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs).

- Fixed timestamp-regression warnings being emitted for **unchanged** files: [`FolderDiffService`](Services/FolderDiffService.cs) now calls `RecordNewFileTimestampOlderThanOldWarningIfNeeded` only in the `Modified` branch (after `FilesAreEqualAsync` returns `false`), not before the content comparison. Previously, an unchanged file whose `new`-side timestamp was older than the `old`-side timestamp would incorrectly appear in the `Warnings` section of [`diff_report.md`](doc/samples/diff_report.md). Updated warning messages in [`ReportGenerateService`](Services/ReportGenerateService.cs), [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs), and [`ProgramRunner`](ProgramRunner.cs) to read "**modified** files" instead of "files". Updated XML doc comments in [`FileDiffResultLists`](Models/FileDiffResultLists.cs) and [`FileTimestampRegressionWarning`](Models/FileTimestampRegressionWarning.cs). Updated [`FolderDiffServiceTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs): renamed `ExecuteFolderDiffAsync_WhenNewFileTimestampIsOlder_RecordsWarning` to use different-content files (so the file is classified as modified), added new test `ExecuteFolderDiffAsync_WhenUnchangedFileTimestampIsOlder_DoesNotRecordWarning` that verifies no warning is emitted for same-content files with an older new-side timestamp, updated `ExecuteFolderDiffAsync_WhenTimestampWarningDisabled_DoesNotRecordWarning` to use different-content files; updated [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) and [`ProgramTests`](FolderDiffIL4DotNet.Tests/ProgramTests.cs) to match the new message wording. Updated bilingual [README.md](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md).

- Improved [`config.json`](config.json) parse error reporting: [`ConfigService`](Services/ConfigService.cs) now emits a descriptive error that includes the line number and byte position from the underlying `JsonException`, plus an explicit hint that trailing commas after the last property or array element are not allowed in standard JSON. The error is logged to the run log file and printed to the console in red; the run exits with code `3`. Added 3 targeted unit tests to [`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) covering trailing commas in objects, trailing commas in arrays, and multiline JSON with line-number verification.
- Fixed garbled `?` characters in the banner on Windows: [`Program.cs`](Program.cs) now sets [`Console.OutputEncoding`](https://learn.microsoft.com/en-us/DOTNET/api/system.console.outputencoding?view=net-8.0) = `Encoding.UTF8` at the very start of `Main()` before any output, overriding the OEM code page (CP932/CP437) that Windows uses by default. On Linux and macOS the console is already UTF-8, so this change has no effect on those platforms.
- Fixed three CI pipeline test failures caused by recent HTML report changes in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs): (1) `GenerateDiffReportHtml_ILMismatch_NoInlineDiff` and `GenerateDiffReportHtml_TextMismatch_EnableInlineDiffFalse_NoDetailsElement` were asserting `DoesNotContain("<details")` but a JS comment contained the literal `<details` — fixed by rewriting the comment to avoid the substring; (2) `GenerateDiffReportHtml_Md5MismatchWarning_AppearsInWarningsSection` used a single exact-match string that broke when a `<span>` was inserted between the class attribute and the heading text — fixed by splitting into two separate `Contains` assertions; (3) colour assertions updated from `#2d7a2d`/`#b00020` to `#22863a`/`#b31d28` to match the GitHub diff palette.
- Fixed edit-distance-exceeded inline diff showing `+0 / -0` (looking like no difference): when `TextDiffer.Compute` returns a single `Truncated` line (triggered when edit distance `D` > [`InlineDiffMaxEditDistance`](Models/ConfigSettings.cs)), [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) now renders a plain visible `diff-skipped` row without a `<details>` expand arrow — consistent with the [`InlineDiffMaxDiffLines`](Models/ConfigSettings.cs)-exceeded case. Renamed test to `GenerateDiffReportHtml_TextMismatch_EditDistanceTooLarge_ShowsSkippedMessageWithoutExpandArrow` in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs).
- Restored coloured text for `[ + ] Added`, `[ - ] Removed`, `[ * ] Modified`, and `[ ! ] Timestamps Regressed` section headings in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) (green `#22863a` / red `#b31d28` / blue `#0051c3`). Re-added `COLOR_ADDED`, `COLOR_REMOVED`, `COLOR_MODIFIED` constants. Updated [CHANGELOG.md](CHANGELOG.md) and [README.md](README.md) (EN + JP) to remove the stale "plain black text" description.

#### Changed

- Inline diff `<summary>` label now prefixes the file index: `#1 Show diff (+N / -M)` / `#1 Show IL diff (+N / -M)` (previously `Show diff` / `Show IL diff`), making it easy to identify which file the diff belongs to without looking at the row above.
- Updated sample [`doc/samples/diff_report.html`](doc/samples/diff_report.html) to match the current production output: 8-column layout (added Disassembler column with `col.col-disasm-g` / `td.col-disasm` CSS, `--col-disasm-w: 28em` CSS variable, and resizable `Disassembler` header); `--col-diff-w` corrected from `20em` to `9em`; Timestamp column corrected from `22em` to `16em`; `col-ts` CSS updated; all `colspan="7"` changed to `colspan="8"`; diff-summary labels updated to `#N Show diff` / `#N Show IL diff`; Diff Reason cells split (e.g. `ILMismatch` + [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) ` (version: 0.12.2)` → separate `ILMismatch` + Disassembler cell); JavaScript `colVarNames` / `clearAll` arrays and `syncTableWidths` formula updated. Added two sample rows demonstrating edit-distance-exceeded skip (`src/BigSchema.cs`) and diff-too-large skip (`src/LargeConfig.xml`). Updated [`doc/samples/diff_report.md`](doc/samples/diff_report.md) to include the two new Modified entries and updated file counts (Modified 8, Compared 17).
- Added "Inline diff skip behaviour" section to [`doc/DEVELOPER_GUIDE.md`](doc/DEVELOPER_GUIDE.md) (EN + JP): documents all three skip triggers (edit distance too large / [`InlineDiffMaxOutputLines`](Models/ConfigSettings.cs) mid-truncation / [`InlineDiffMaxDiffLines`](Models/ConfigSettings.cs) post-compute), the conditions, and the resulting HTML rendering difference (`<details>` vs. plain row).

#### Added

- Further refined HTML report UX in [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs): page title `<h1>` changed to "Folder Diff Report"; frosted-glass controls bar background opacity reduced to `rgba(255,255,255,0.45)` for a more transparent look; `h1` font size increased to `2.0rem` and `Summary` / `IL Cache Stats` / `Warnings` section headings given a dedicated `h2.section-heading` style at `1.55rem` so they stand out from file-list section headings; `[ ! ] Modified Files — Timestamps Regressed` promoted from `h3` to `h2` to match the `[ * ] Modified Files` style; stat-table numbers font changed to the body font (removed monospace); stat-table indented `1.2em` from the left margin; "Show diff" label in inline-diff summaries changed to "Show IL diff" for `ILMismatch` entries; `+N` and `-N` in diff summaries are now coloured green and red with `diff-added-cnt` / `diff-removed-cnt` spans; diff-row `<tr>` rows (the collapsible row containing `<details>`) now have a light-blue background (`#eef5ff`); IL ignore-string notes in the header no longer wrap values in `<code>` tags; `WARNING:` text removed from Warnings list items — a yellow `⚠` icon (`warn-icon`) is now shown instead; column widths for OK Reason / Notes / File Path are now controlled via CSS custom properties (`--col-reason-w`, `--col-notes-w`, `--col-path-w`) backed by `<colgroup>` elements, making them synchronised across all tables; column headers for these three columns are now draggable resize handles (`initColResize` JS) that update the CSS variables so every table resizes together; reviewed-HTML download replaces the controls bar with a green "🔒 Reviewed — read-only" banner (`reviewed-banner`) instead of stripping it entirely, giving reviewers a clear visual indicator that the file is a signed-off snapshot. Updated [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs): updated the Warnings-section assertion to match the new `h2.section-heading` structure.
- Overhauled [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) with a comprehensive set of HTML report improvements: fixed page title to `diff_report`; added a sticky frosted-glass controls bar (`backdrop-filter: blur`) that fills the full viewport width; replaced the old buttons with Apple-style minimal pill buttons (same height, `display:inline-flex`, `border-radius:980px`); auto-saved timestamp now formats as `YYYY-MM-DD HH:mm:ss`; Old/New folder paths are plain text (no `<code>` wrapper); MVID note rendered as a regular `<li>` meta item; IL contains-ignore note added to HTML header; Legend moved inside `<ul class="meta">` as a nested bullet list; all 5 file tables now share a consistent 8-column layout (`# | ✓ | OK Reason | Notes | File Path | Timestamp | Diff Reason | Disassembler`) with record numbers, no cell placeholders, and resizable OK Reason/Notes/Path/Disassembler columns; Added/Removed/Modified table headers use light green/red/blue backgrounds with black text; Ignored/Unchanged tables use a neutral header; Summary and IL Cache Stats sections use `<table class="stat-table">` with right-aligned numeric values; timestamp-regressed files in Warnings are rendered as a table under a `[ ! ] Modified Files — Timestamps Regressed (N)` heading. Inline diff changes: [`InlineDiffContextLines`](Models/ConfigSettings.cs) default reduced from 3 to 0 (changed lines only, no surrounding context); hunk separator rows are shown when lines are omitted; ILMismatch entries now render an inline diff when [`ShouldOutputILText`](Models/ConfigSettings.cs) is `true` (the default) and the `*_IL.txt` files are present under `Reports/<label>/IL/old` and `Reports/<label>/IL/new`. Reviewed HTML download improvements: output filename is `diff_report_{yyyyMMdd}_reviewed.html`; page title becomes `diff_report_{yyyyMMdd}_reviewed`; the controls bar (`<!--CTRL-->…<!--/CTRL-->`) is stripped from the downloaded copy; all checkboxes are `disabled` and text inputs are `readOnly` (still selectable and copyable) in the reviewed copy. Updated unit tests in [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) and [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) to match the new colors (`#2d7a2d`, `#b00020`), stat-table HTML structure, and [`InlineDiffContextLines`](Models/ConfigSettings.cs) default of `0`.
- Improved [`diff_report.md`](doc/samples/diff_report.md) section headers: [`ReportGenerateService`](Services/ReportGenerateService.cs) now appends the file count to each section heading — e.g. `## [ x ] Ignored Files (3)`, `## [ + ] Added Files (1)` — so the count is visible at a glance without reading the list. Changed the display path for single-side ignored files in `IgnoredFilesSectionWriter`: entries present only in `old` or only in `new` now show the absolute path (`/path/to/old/rel/file.pdb`), while entries present on both sides continue to show the relative path.

- Added `ShouldGenerateHtmlReport` (default `true`) to [`ConfigSettings`](Models/ConfigSettings.cs). When `true`, each run produces **`diff_report.html`** alongside `diff_report.md` in the same `Reports/<label>/` directory. The HTML file is a standalone self-contained review document — no server or browser extension needed. All file entries (Ignored, Unchanged, Added, Removed, Modified) are presented in an 8-column table; Removed / Added / Modified rows have an interactive checkbox, OK-Reason text input, and Notes text input for sign-off during product-release review. Column headers for Added / Removed / Modified use colour-coded backgrounds (green / red / blue); section headings for Added / Removed / Modified use colour-coded text (green / red / blue). The file includes embedded JavaScript for localStorage auto-save (keyed by `folderdiff-<label>`) and a **"Download reviewed version"** button that bakes the current review state into a new portable snapshot file. Set `"ShouldGenerateHtmlReport": false` in [`config.json`](config.json) to opt out. Implemented in the new [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs), registered in [`RunScopeBuilder`](Runner/RunScopeBuilder.cs), and called from [`ProgramRunner.GenerateReport()`](ProgramRunner.cs). Added 12 unit tests to [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs). Added sample files [`doc/samples/diff_report.md`](doc/samples/diff_report.md) and [`doc/samples/diff_report.html`](doc/samples/diff_report.html); refactored [README.md](README.md) to link to these external samples and added a new bilingual `Interactive HTML Review Report` / `インタラクティブ HTML レビューレポート` section describing the review workflow. Updated bilingual [developer guide](doc/DEVELOPER_GUIDE.md) and [testing guide](doc/TESTING_GUIDE.md).

- Added [`DisassemblerBlacklistTtlMinutes`](Models/ConfigSettings.cs) (default `10`) to [`ConfigSettings`](Models/ConfigSettings.cs). The new property controls the blacklist TTL for a disassembler tool that has failed consecutively `DISASSEMBLE_FAIL_THRESHOLD` (3) times. Previously the TTL was hardcoded at 10 minutes. [`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) now reads this setting from config at construction time instead of using a static `TimeSpan.FromMinutes(10)`. Updated [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) to assert the new default and JSON round-trip behavior.
- Extracted [`DisassemblerBlacklist`](Services/DisassemblerBlacklist.cs) out of [`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) into its own class, encapsulating the [`ConcurrentDictionary`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2?view=net-8.0), TTL, and fail-threshold logic. Added `InjectEntry` / `ContainsEntry` test-only helpers on the class, and updated [`DotNetDisassembleServiceTests`](FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) to use instance-level reflection (`_blacklist`) instead of the old static-field access. New [`DisassemblerBlacklistTests`](FolderDiffIL4DotNet.Tests/Services/DisassemblerBlacklistTests.cs) covers threshold boundary, TTL expiry, `Clear`, `ResetFailure`, null-safe handling, and two concurrent-access scenarios (B-4).
- Introduced [`IReportSectionWriter`](Services/IReportSectionWriter.cs) interface and [`ReportWriteContext`](Services/ReportWriteContext.cs) context class. [`ReportGenerateService`](Services/ReportGenerateService.cs) now defines a static `_sectionWriters` list of 11 private nested-class implementations (`HeaderSectionWriter`, `LegendSectionWriter`, `IgnoredFilesSectionWriter`, `UnchangedFilesSectionWriter`, `AddedFilesSectionWriter`, `RemovedFilesSectionWriter`, `ModifiedFilesSectionWriter`, `SummarySectionWriter`, `AssemblySemanticChangesSectionWriter`, `ILCacheStatsSectionWriter`, `WarningsSectionWriter`) and iterates over them in `WriteReportSections`. Each section can be exercised in isolation by constructing a `ReportWriteContext` without needing the full service.
- Added Unicode-filename report tests to [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs): `GenerateDiffReport_UnicodeFileNames_AreIncludedInReport` and `GenerateDiffReport_UnicodeFileNames_InUnchangedSection` verify that Japanese, Umlauted-Latin, and Chinese relative paths appear verbatim in the Markdown report (B-2).
- Added large-file-count summary snapshot test `GenerateDiffReport_LargeFileCount_SummaryStatisticsAreCorrect` to [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs): seeds 10 500 unchanged files and asserts that `Unchanged` and `Compared` counts in the Summary section match the seeded count (B-3).

#### Changed

- Consolidated four identical parallel-text-diff fallback `catch` blocks ([`ArgumentOutOfRangeException`](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception?view=net-8.0), [`IOException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.ioexception?view=net-8.0), [`UnauthorizedAccessException`](https://learn.microsoft.com/en-us/dotnet/api/system.unauthorizedaccessexception?view=net-8.0), [`NotSupportedException`](https://learn.microsoft.com/en-us/dotnet/api/system.notsupportedexception?view=net-8.0)) in [`FileDiffService`](Services/FileDiffService.cs) into a single `catch (Exception ex) when (ex is … or …)` guard, eliminating the duplicated fallback body.
- Enhanced the IL-diff failure log in [`FileDiffService`](Services/FileDiffService.cs): the error message now appends `ex.Message` (which includes the disassembler command and inner cause) so the log line is self-contained without requiring users to read the stack trace.
- Added rationale comments to previously undocumented magic constants: `KEEP_ALIVE_INTERVAL_SECONDS` = `5` and `LARGE_DISCOVERY_FILE_COUNT_LOG_THRESHOLD` = `10000` in [`FolderDiffService`](Services/FolderDiffService.cs); `MAX_PARALLEL_NETWORK_LIMIT` = `8` in [`FolderDiffExecutionStrategy`](Services/FolderDiffExecutionStrategy.cs); `DISASSEMBLE_FAIL_THRESHOLD` = `3` and `DEFAULT_BLACKLIST_TTL_MINUTES` = `10` in [`DotNetDisassembleService`](Services/DotNetDisassembleService.cs).

#### Added

- Added `DiffSummaryStatistics` record and `SummaryStatistics` computed property to [`FileDiffResultLists`](Models/FileDiffResultLists.cs). The property returns a single `DiffSummaryStatistics(AddedCount, RemovedCount, ModifiedCount, UnchangedCount, IgnoredCount)` snapshot instead of requiring callers to access five separate concurrent collections. Updated [`ReportGenerateService.WriteSummarySection()`](Services/ReportGenerateService.cs) to use `SummaryStatistics` instead of direct `.Count` accesses on each queue/dictionary. Added 4 unit tests to [`FileDiffResultListsTests`](FolderDiffIL4DotNet.Tests/Models/FileDiffResultListsTests.cs).
- Added [`SpinnerFrames`](Models/ConfigSettings.cs) to [`ConfigSettings`](Models/ConfigSettings.cs) — a `List<string>` where each element is one spinner animation frame, letting users replace the default four-frame `| / - \` rotation with any sequence including multi-character strings (e.g. block characters, emoji). [`ConsoleSpinner`](FolderDiffIL4DotNet.Core/Console/ConsoleSpinner.cs) changed its internal frame array from `char[]` to `string[]` to support multi-character frames; [`ProgressReportService`](Services/ProgressReportService.cs) and [`ReportGenerateService`](Services/ReportGenerateService.cs) now accept a `ConfigSettings` constructor parameter so they can read the configured frames at startup. Validation enforces at least one frame. Updated bilingual [README.md](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md).

#### Changed

- Consolidated the duplicate `"// MVID:"` literal into a single [`Constants.IL_MVID_LINE_PREFIX`](Common/Constants.cs) constant and removed the now-redundant `private const string MVID_PREFIX` definitions from both [`ReportGenerateService`](Services/ReportGenerateService.cs) and [`ILOutputService`](Services/ILOutputService.cs). No behaviour change; the string value is identical in both call sites and all references now use [`Constants.IL_MVID_LINE_PREFIX`](Common/Constants.cs).
- Improved timestamp display in [`diff_report.md`](doc/samples/diff_report.md): the format changed from `yyyy-MM-dd HH:mm:ss.fff zzz` (per-entry milliseconds and timezone offset) to `yyyy-MM-dd HH:mm:ss` (seconds only), the timezone offset is now written once in the report header as `Timestamps (timezone): +09:00` when [`ShouldOutputFileTimestamps`](Models/ConfigSettings.cs) is `true`, and each entry uses a bracket-and-arrow style — `[old → new]` for two timestamps and `[timestamp]` for a single timestamp — replacing the previous `<u>(updated_old: ..., updated_new: ...)</u>` markup. The `Warnings` section follows the same bracket-and-arrow format. For Unchanged files, two timestamps are now shown whenever old and new last-modified times differ, regardless of diff type (previously only `ILMatch` entries showed two timestamps). Updated bilingual [README.md](README.md) and related tests.

#### Added

- Filled four test gaps in [`FolderDiffIL4DotNet.Tests`](FolderDiffIL4DotNet.Tests/): (1) added `IsLikelyWindowsNetworkPath_ForwardSlashIpUncPath_ReturnsTrue` to [`FileSystemUtilityTests`](FolderDiffIL4DotNet.Tests/Core/IO/FileSystemUtilityTests.cs) and fixed [`FileSystemUtility.IsLikelyWindowsNetworkPath()`](FolderDiffIL4DotNet.Core/IO/FileSystemUtility.cs) to also detect `//`-format IP-based UNC paths (e.g. `//192.168.1.1/share`) as network paths on Windows; (2) added `ExecuteFolderDiffAsync_WhenEnumeratingFilesThrowsIOExceptionDueToSymlinkLoop_LogsAndRethrows` to [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs), verifying that an `IOException` raised during directory enumeration (e.g. an `ELOOP` error from a symlink cycle) is logged as an error and re-thrown; (3) added `ExecuteFolderDiffAsync_WhenNewFileDeletedBeforeComparison_ClassifiesAsRemovedWithWarning` (sequential and parallel variants) to [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) and changed [`FolderDiffService`](Services/FolderDiffService.cs) to catch [`FileNotFoundException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.filenotfoundexception?view=net-8.0) during per-file comparison, emit a warning, and classify the file as Removed rather than propagating the exception; (4) added `DisassembleAsync_AfterBlacklistTtlExpiry_RetriesToolAndSucceeds` to [`DotNetDisassembleServiceTests`](FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), verifying that a blacklisted disassembler tool whose 10-minute TTL has expired is removed from the blacklist and retried on the next call.
- Added three preflight checks to [`ProgramRunner.ValidateRunDirectories()`](ProgramRunner.cs) that run before configuration is loaded and all fail with exit code `2`: (1) **path-length check** — the constructed `Reports/<label>` path is validated against the OS limit (260 chars on Windows without long-path opt-in, 1024 on macOS, 4096 on Linux) via [`PathValidator.ValidateAbsolutePathLengthOrThrow()`](FolderDiffIL4DotNet.Core/IO/PathValidator.cs); (2) **disk-space check** — at least 100 MB of free space is verified on the target drive using `DriveInfo`, skipping best-effort when drive information is unavailable; (3) **write-permission check** — a temporary probe file is created and deleted in the `Reports/` parent directory to confirm write access before any output is produced. Added [`IOException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.ioexception?view=net-8.0) and [`UnauthorizedAccessException`](https://learn.microsoft.com/en-us/dotnet/api/system.unauthorizedaccessexception?view=net-8.0) catches to `TryValidateAndBuildRunArguments` so all three failures map cleanly to exit code `2`. Added 3 unit/integration tests to [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) and updated [README.md](README.md).
- Added [`ShouldIncludeILCacheStatsInReport`](Models/ConfigSettings.cs) (default `false`) to [`ConfigSettings`](Models/ConfigSettings.cs). When `true` and the IL cache is active, [`ReportGenerateService`](Services/ReportGenerateService.cs) appends an `IL Cache Stats` section between `Summary` and `Warnings` in [`diff_report.md`](doc/samples/diff_report.md), showing hits, misses, hit-rate, stores, evicted, and expired counts. Also added `_internalMisses` tracking to [`ILCache`](Services/Caching/ILCache.cs) (miss counter now incremented on full cache miss), a `GetReportStats()` method, and the `ILCacheReportStats` sealed record. Added 3 unit tests to [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) and updated [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs), [README.md](README.md), and [CHANGELOG.md](CHANGELOG.md).
- Expanded CLI options: `--help`/`-h` prints usage and exits with code `0` before any logger initialization; `--version` prints the application version and exits with code `0`; `--config <path>` loads a config file from an arbitrary path instead of the default `<exe>/config.json`; `--threads <N>` overrides [`MaxParallelism`](Models/ConfigSettings.cs) in [`ConfigSettings`](Models/ConfigSettings.cs) for the current run; `--no-il-cache` forces [`EnableILCache`](Models/ConfigSettings.cs) = `false` for the current run; `--skip-il` skips IL decompilation and IL diff entirely for .NET assemblies (new [`SkipIL`](Models/ConfigSettings.cs) property in [`ConfigSettings`](Models/ConfigSettings.cs), also respected by [`FileDiffService`](Services/FileDiffService.cs)); `--no-timestamp-warnings` suppresses timestamp-regression warnings. Unknown flags now produce exit code `2` with a descriptive message instead of silently being ignored. [`ConfigService.LoadConfigAsync()`](Services/ConfigService.cs) now accepts an optional `configFilePath` parameter. Added [`CliOptionsTests`](FolderDiffIL4DotNet.Tests/CliOptionsTests.cs) with 21 parser unit-test cases, and new integration tests in [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) and [`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs).
- Added [`ConfigSettings.Validate()`](Models/ConfigSettings.cs) and the companion `ConfigValidationResult` class; [`ConfigService.LoadConfigAsync()`](Services/ConfigService.cs) now calls `Validate()` immediately after deserialization and throws [`InvalidDataException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.invaliddataexception?view=net-8.0) listing all invalid settings when validation fails, so misconfigured runs are caught at startup with a clear error message instead of failing silently or causing undefined behavior later. Validated constraints: [`MaxLogGenerations`](Models/ConfigSettings.cs) >= `1`; [`TextDiffParallelThresholdKilobytes`](Models/ConfigSettings.cs) >= `1`; [`TextDiffChunkSizeKilobytes`](Models/ConfigSettings.cs) >= `1`; and [`TextDiffChunkSizeKilobytes`](Models/ConfigSettings.cs) < [`TextDiffParallelThresholdKilobytes`](Models/ConfigSettings.cs). Added validation unit tests to [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) (7 cases) and validation integration tests to [`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) (5 cases).

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

- Made [`IgnoredExtensions`](Models/ConfigSettings.cs) matching case-insensitive.

#### Removed

- Removed the unused `ShouldSkipPromptOnExit` configuration entry.

#### Fixed

- Corrected [`TextFileExtensions`](Models/ConfigSettings.cs) configuration values.
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

#### 追加

- **HTML レポートのインタラクティブフィルタリング機能** — HTML レポート（`diff_report.html`）にフィルターバーを追加し、複数の条件でファイル行を絞り込めるようにしました。**重要度**（High / Medium / Low チェックボックス）、**ファイル種別**（DLL / EXE / Config / Resource / Other）、**未チェックのみ**（チェックボックスが未チェックの行のみ表示）、および**ファイルパス検索**（フリーテキスト入力）。すべてのフィルタコントロールは日英バイリンガルラベルを使用。フィルターバーは `<!--CTRL-->...<!--/CTRL-->` マーカー内に配置されるため、レビュー済み（読み取り専用）HTML では自動的に除去されます。フィルタリング状態は `__filterIds__` 配列により `collectState()` / localStorage 自動保存から除外。`downloadReviewed()` は `outerHTML` キャプチャ前にすべての `filter-hidden` / `filter-hidden-parent` CSS クラスをテーブル行から削除し、ダウンロード時のフィルタ状態に関係なくレビュー済み HTML ですべての行が表示されることを保証。実装詳細: `AppendFileRow()` が出力する各 `<tr>` に `data-section`、`data-ext`、（該当する場合）`data-importance` 属性を付与しクライアント側フィルタリングに使用。CSS クラス `tr.filter-hidden` と `tr.diff-row.filter-hidden-parent` で `display: none !important` により行を非表示。新規 JS 関数: `getFileTypeCategory(ext)`、`applyFilters()`、`resetFilters()`。[`diff_report.css`](Services/HtmlReport/diff_report.css)、[`diff_report.js`](Services/HtmlReport/diff_report.js)、[`HtmlReportGenerateService.cs`](Services/HtmlReportGenerateService.cs)、[`HtmlReportGenerateService.Helpers.cs`](Services/HtmlReport/HtmlReportGenerateService.Helpers.cs)、[`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) にテスト 10 件を追加。

- **セマンティック変更エントリへの「変更の重要度」自動付与** — `AssemblyMethodAnalyzer` が検出した各 `MemberChangeEntry` に対し、新しいルールベース分類器 `ChangeImportanceClassifier` が自動的に `High`・`Medium`・`Low` の重要度を付与するようになりました。分類ルール: `High` = public/protected API の削除、public/protected からのアクセス縮小、戻り値型の変更、メンバー型の変更、パラメータの変更。`Medium` = public/protected メンバーの追加、internal/private の削除、修飾子の変更、アクセス拡大。`Low` = ボディのみの変更、internal/private メンバーの追加。新規モデル: [`ChangeImportance`](Models/ChangeImportance.cs) 列挙型（`Low=0`, `Medium=1`, `High=2`）。新規サービス: [`ChangeImportanceClassifier`](Services/ChangeImportanceClassifier.cs)（`Classify(MemberChangeEntry)` と `WithClassifiedImportance(MemberChangeEntry)` メソッド）。`MemberChangeEntry` レコードに省略可能な `Importance` パラメータ（既定: `Low`）を追加し、後方互換性を維持。`AssemblySemanticChangesSummary` に `HighImportanceCount`・`MediumImportanceCount`・`LowImportanceCount`・`MaxImportance`・`EntriesByImportance` プロパティを追加。レポート変更: Modified Files テーブルの Diff Reason 列で `ILMismatch` の後ろにファイルレベルの最大重要度を追記（例: `ILMismatch` `High`）。3 つの Modified Files テーブル（`[ * ] Modified Files`・`[ ! ] SHA256Mismatch`・`[ ! ] Timestamps Regressed`）を DiffDetail → Importance → パスの順でソート。セマンティック変更詳細テーブルに `Importance` 列を追加。集計テーブルの列構成を (Class, Status, Count) から (Class, Status, High, Medium, Low, Total) に変更。凡例セクションに `High`・`Medium`・`Low` を日英バイリンガルの説明付きで追加。Markdown および HTML レポートの両方に適用。[`doc/samples/diff_report.md`](doc/samples/diff_report.md) および [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。[`ChangeImportanceClassifierTests`](FolderDiffIL4DotNet.Tests/Services/ChangeImportanceClassifierTests.cs)（20 以上のテストメソッド）を新規追加、[`AssemblySemanticChangesSummaryTests`](FolderDiffIL4DotNet.Tests/Models/AssemblySemanticChangesSummaryTests.cs) に重要度関連アサーションを追加。

- **レポートヘッダに逆アセンブラ利用可否テーブルを追加** — レポートヘッダに「Disassembler Availability」テーブルを追加しました。すべての候補 IL 逆アセンブラツール（例: `dotnet-ildasm`、`ilspycmd`）について、現在の環境で利用可能か否か、および利用可能な場合はそのバージョンを一覧表示します。これにより、どのツールが比較に影響を与えたか、IL ベースの結果の信頼度を一目で把握できます。起動時に `DisassemblerHelper.ProbeAllCandidates()` が各候補を `--version` 実行でプローブし、結果を `FileDiffResultLists.DisassemblerAvailability`（新規プロパティ）に格納します。テーブルは Markdown（`diff_report.md`）、HTML（`diff_report.html`）、および監査ログ（`audit_log.json`）の `disassemblerAvailability` JSON 配列としてレンダリングされます。新規モデル: [`DisassemblerProbeResult`](Models/DisassemblerProbeResult.cs)（`ToolName`, `Available`, `Version`, `Path`）。新規監査モデル: [`AuditLogDisassemblerAvailability`](Models/AuditLogEntry.cs)。[`doc/samples/diff_report.md`](doc/samples/diff_report.md)、[`doc/samples/diff_report.html`](doc/samples/diff_report.html)、[`doc/samples/audit_log.json`](doc/samples/audit_log.json) を更新。テスト `GenerateDiffReport_HeaderShowsDisassemblerAvailabilityTable`、`GenerateDiffReport_HeaderOmitsAvailabilityTable_WhenProbeResultsAreNull` を [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) に、`GenerateDiffReportHtml_HeaderShowsDisassemblerAvailabilityTable`、`GenerateDiffReportHtml_HeaderOmitsAvailabilityTable_WhenProbeResultsAreNull` を [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) に、`GenerateAuditLog_IncludesDisassemblerAvailability_WhenProbed`、`GenerateAuditLog_DisassemblerAvailabilityIsNull_WhenNotProbed` を [`AuditLogGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/AuditLogGenerateServiceTests.cs) に、`ProbeAllCandidates_ReturnsNonEmptyList_WithUniqueToolNames`、`ProbeAllCandidates_IncludesExpectedToolNames`、`ProbeAllCandidates_AllResultsHaveNonEmptyToolName` を [`DisassemblerHelperTests`](FolderDiffIL4DotNet.Tests/Services/DisassemblerHelperTests.cs) に追加。

- **監査ログと改竄検知 (`audit_log.json` + レビュー済み HTML 整合性検証)** — 差分レポートと合わせて構造化 JSON 監査ログを生成する `AuditLogGenerateService` を追加。監査ログにはファイルごとの比較結果（カテゴリ、diff 詳細、使用した逆アセンブラ）、実行メタデータ（アプリバージョン、マシン名、旧/新パス、ISO 8601 タイムスタンプ、経過時間）、サマリー統計、および改竄検知用の `diff_report.md` / `diff_report.html` の SHA256 インテグリティハッシュを記録。新設定 `ShouldGenerateAuditLog`（既定: `true`）で生成を制御。新モデルクラス: `AuditLogRecord`、`AuditLogFileEntry`、`AuditLogSummary`（`Models/AuditLogEntry.cs`）。`RunScopeBuilder` にサービスを登録し、`ProgramRunner.GenerateReport()` で HTML レポート生成後に呼び出し。サンプル [`doc/samples/audit_log.json`](doc/samples/audit_log.json) を追加。`IReadOnlyConfigSettings` インターフェースに `ShouldGenerateAuditLog` プロパティを追加。「Download as reviewed」ワークフローでも Web Crypto API を用いてレビュー済み HTML の SHA256 ハッシュを計算し、プレースホルダ方式でファイル自体に埋め込んだうえ、コンパニオン `.sha256` 検証ファイルをダウンロード。レビュー済み HTML のヘッダーに「Verify integrity」ボタンを追加し、クリックするとファイルを再読み込み → ハッシュ再計算 → 合格/不合格ダイアログが表示される。`.sha256` ファイルは `sha256sum`/`shasum` 形式に従い、全 OS で検証可能（Linux: `sha256sum -c`、macOS: `shasum -a 256 -c`、Windows: PowerShell の `Get-FileHash`）。レビュー済み HTML と `.sha256` ファイルを一緒に提出することで、改竄のない監査記録となる。テストを 18 件追加: [`AuditLogGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/AuditLogGenerateServiceTests.cs) に 17 件、[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) に 1 件。`ConfigSettingsTests` に新既定値のアサーションを追加。

- **テストカバレッジ計測基盤とミューテーションテストの導入** — coverlet の詳細設定を行う [`coverlet.runsettings`](coverlet.runsettings) を追加（include/exclude フィルタ、決定論的レポート出力、ブランチカバレッジ、マルチフォーマット出力）。`dotnet-reportgenerator-globaltool` と `dotnet-stryker` をローカルツール依存関係として登録する [`.config/dotnet-tools.json`](.config/dotnet-tools.json) マニフェストを追加。Stryker.NET ミューテーションテスト設定ファイル [`stryker-config.json`](stryker-config.json) を追加（`Standard` ミューテーションレベル、`80/60/50` の high/low/break 閾値、`html`/`json`/`progress`/`cleartext` レポーター）。[`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml) を更新: CI は再現可能なローカルツールバージョン取得のため `dotnet tool restore` を使用、テスト実行で `--settings coverlet.runsettings` を使用、新しい `mutation-testing` ジョブ（workflow_dispatch のみ）が Stryker を実行し結果をアーティファクトとしてアップロード。`mutation-testing` ジョブは手動ディスパッチに限定し通常 CI を遅延させず、ワンクリックでミューテーションテストを実行可能に。

- **`--print-config` の発見性向上と設定エラー時のガイダンス** — `--help` テキストの末尾に「Tip:」セクションを追加し、`--print-config` を実行前の有効設定確認に推奨する方法として紹介。設定エラー（終了コード `3`）発生時には stderr に `Tip: Run with --print-config to display the effective configuration as JSON.` のヒントを出力し、どの設定が有効か・どのオーバーライドが適用されたかを迅速に診断可能に。テスト `RunAsync_HelpFlag_OutputContainsPrintConfigTipSection` および `RunAsync_ConfigError_WritesPrintConfigHintToStderr` を [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) に追加。

- **Railway 指向 `StepResult<T>` パイプライン** — `ProgramRunner.RunWithResultAsync` を、反復的な `if(!IsSuccess) return Failure` パターンから、[`StepResult<T>`](Runner/ProgramRunner.Types.cs) の新しい `Bind<TNext>`（同期）および `BindAsync<TNext>`（非同期）メソッドを使用した関数型 Railway 指向パイプラインにリファクタリング。各実行フェーズ（引数検証 → レポートディレクトリ準備 → 設定読み込み → CLI オーバーライド → 設定ビルド → 実行）を `Bind`/`BindAsync` でチェーンし、失敗時は自動ショートサーキット。動作変更なし、内部コード品質の改善のみ。

- **XML ドキュメント完備と CS1591/CS1573 抑制の除去** — ドキュメントが欠けていたすべての public 型・コンストラクタ・プロパティ・メソッド・列挙型メンバーに XML ドキュメントコメント（`<summary>`、`<param>`、`<returns>`、`<typeparam>`）を追加。[`FolderDiffIL4DotNet.csproj`](FolderDiffIL4DotNet.csproj) と [`FolderDiffIL4DotNet.Core.csproj`](FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj) の両方から `<NoWarn>CS1591;CS1573</NoWarn>` を削除。`<GenerateDocumentationFile>true</GenerateDocumentationFile>` と `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` が有効な状態で、今後ドキュメントのない public API はビルド失敗となります。対象ファイル: `AuditLogEntry.cs`、`ConfigSettings.cs`、`FileDiffResultLists.cs`、`ProgramRunner.Types.cs`、`AppLogLevel.cs`、`AuditLogGenerateService.cs`、`DotNetDisassemblerCache.cs`、`ILCache.cs`、`DiffExecutionContext.cs`、`DisassemblerBlacklist.cs`、`DotNetDisassembleService.cs`、`FileDiffService.cs`、`FolderDiffExecutionStrategy.cs`、`FolderDiffService.cs`、`HtmlReportGenerateService.cs`、`ILTextOutputService.cs`、`ILOutputService.cs`、`ProgressReportService.cs`、`ReportGenerateService.cs`、`ReportGenerationContext.cs`。

- **`System.Net.WebUtility.HtmlEncode` と Content-Security-Policy メタタグによる HTML レポート堅牢化** — [`HtmlReportGenerateService.Helpers.cs`](Services/HtmlReport/HtmlReportGenerateService.Helpers.cs) のカスタム 5 文字手動 `HtmlEncode` メソッド（`&` → `&amp;`、`<` → `&lt;`、`>` → `&gt;`、`"` → `&quot;`、`'` → `&#39;`）を `System.Net.WebUtility.HtmlEncode` に置換。バッククォート・非 ASCII 文字・その他のエッジケースを含む包括的なエンティティエンコーディングを提供。[`HtmlReportGenerateService.cs`](Services/HtmlReportGenerateService.cs) の HTML `<head>` に `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src 'self'">` タグを追加し、外部リソース読み込み・フォーム送信等を遮断することで XSS の影響範囲を限定。[`doc/samples/diff_report.html`](doc/samples/diff_report.html) に CSP メタタグを追加。テスト `HtmlEncode_EscapesBacktickAndNonAsciiCharacters`、`HtmlEncode_PreservesNormalTextUnchanged`、`HtmlEncode_HandlesUnicodeCharacters`、`GenerateDiffReportHtml_ContainsContentSecurityPolicyMetaTag`、`GenerateDiffReportHtml_CspMetaTagAppearsBetweenCharsetAndViewport` を [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) に追加。

- **エラー処理・並列実行・境界条件のエッジケーステスト** — `FolderDiffIL4DotNet.Tests/Services/EdgeCases/` 配下に 5 つの新テストクラスを追加: [`DisassemblerBlacklistTtlRecoveryTests`](FolderDiffIL4DotNet.Tests/Services/EdgeCases/DisassemblerBlacklistTtlRecoveryTests.cs)（TTL 復旧サイクル、並列 register/check、境界タイムスタンプ、アクティブブラックリスト中の手動リセットに関する 7 テスト）; [`ILCacheConcurrencyTests`](FolderDiffIL4DotNet.Tests/Services/EdgeCases/ILCacheConcurrencyTests.cs)（メモリのみ/ディスクバック付き並列 Set/Get、競合下の LRU 退去、TTL 期限切れレース、並列 PrecomputeAsync に関する 5 テスト）; [`ILCacheDiskFailureTests`](FolderDiffIL4DotNet.Tests/Services/EdgeCases/ILCacheDiskFailureTests.cs)（ネットワークマウントストレージ障害シミュレーション: 読み取り専用ディレクトリ、破損キャッシュファイル、無効パス、操作中ディレクトリ削除に関する 5 テスト）; [`LargeFileComparisonTests`](FolderDiffIL4DotNet.Tests/Services/EdgeCases/LargeFileComparisonTests.cs)（4 MiB 同一/差異ファイルのチャンク並列比較、サイズ不一致検出、空ファイル、多数の小チャンクに関する 6 テスト）; [`SymlinkAndCircularDirectoryTests`](FolderDiffIL4DotNet.Tests/Services/EdgeCases/SymlinkAndCircularDirectoryTests.cs)（old/new 側のシンボリックリンクループ、Removed 分類されるダングリングシンボリックリンク、シンボリックリンクターゲットへのアクセス拒否、並列モードでの複数ダングリングシンボリックリンクに関する 5 テスト）; [`FolderDiffConcurrencyStressTests`](FolderDiffIL4DotNet.Tests/Services/EdgeCases/FolderDiffConcurrencyStressTests.cs)（8 並列での 500 ファイル決定論的分類、ランダム遅延シミュレーション、高並列度での全 4 ファイルカテゴリ混合の現実的テストに関する 3 テスト）。

#### パフォーマンス

- **SHA256 二重計算の排除** — `FileDiffService.FilesAreEqualAsync` が新しい `DiffFilesByHashWithHexAsync` メソッド（[`FileComparer`](FolderDiffIL4DotNet.Core/IO/FileComparer.cs) / [`IFileComparisonService`](Services/IFileComparisonService.cs)）を呼び出すようになりました。このメソッドは等価判定結果と共に計算済み SHA256 16 進文字列を返します。計算済みハッシュは `IILOutputService.PreSeedFileHash` → `ILCache.PreSeedFileHash` → `ILMemoryCache.PreSeedFileHash` を通じて IL キャッシュに即座にシード登録され、同じファイルの IL キャッシュキー生成時に `ILMemoryCache.GetFileHash` が SHA256 を再計算することを防ぎます。ハッシュ比較と IL キャッシュ参照の両方が発生する大規模 .NET アセンブリでの冗長なファイル I/O を排除します。新規インターフェースメソッド: `IFileComparisonService.DiffFilesByHashWithHexAsync`、`IILOutputService.PreSeedFileHash`。新規実装メソッド: `FileComparer.DiffFilesByHashWithHexAsync`、`ILOutputService.PreSeedFileHash`、`ILCache.PreSeedFileHash`、`ILMemoryCache.PreSeedFileHash`。テスト `FilesAreEqualAsync_WhenHashMatches_SeedsILCacheWithBothHashes`、`FilesAreEqualAsync_WhenHashDiffers_SeedsILCacheWithBothHashes`（[`FileDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs)）、`PreSeedFileHash_AvoidsSha256Recomputation`（[`ILCacheTests`](FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs)）、`PreSeedFileHash_WhenCacheIsNull_DoesNotThrow`（[`ILOutputServiceTests`](FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs)）を追加。

- **IL 行分割・フィルタの 1 パス化** — `ILOutputService.DiffDotNetAssembliesAsync` の 4 回のリスト割り当てパイプライン（`ilText.Split('\n').ToList()` → `Where(filter).ToList()`）を、分割とフィルタを 1 回のイテレーションで行い `List<string>` を直接生成する `SplitAndFilterIlLines` メソッドに置き換えました。中間リスト割り当てを半減し、フィルタ前の中間リスト生成を完全に回避します。テスト `SplitAndFilterIlLines_CombinesSplitAndFilter_MatchesSplitThenWhereBehavior`、`SplitAndFilterIlLines_WithConfiguredIgnoreStrings_ExcludesMatchingLines`（[`ILOutputServiceTests`](FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs)）を追加。

- **BenchmarkDotNet CI 統合** — [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml) に `benchmark` ジョブを追加しました。[`FolderDiffIL4DotNet.Benchmarks`](FolderDiffIL4DotNet.Benchmarks/) プロジェクトを JSON および GitHub エクスポーター付きで実行し、`BenchmarkDotNet.Artifacts/` を CI アーティファクトとしてアップロードします。通常の CI パイプライン時間に影響を与えないよう `workflow_dispatch` のみで実行されます。

#### 変更

- **Builder パターンによるイミュータブル ConfigSettings** — `ConfigSettings` を完全にイミュータブル化しました。すべてのプロパティが読み取り専用となり、リストプロパティは `IReadOnlyList<string>` を返します。新しい [`ConfigSettingsBuilder`](Models/ConfigSettingsBuilder.cs) クラスが JSON デシリアライズ・環境変数オーバーライド・CLI オーバーライド用のミュータブルな中間体として機能します。設定読み込みフローは次のようになりました: `ConfigService.LoadConfigBuilderAsync()` が `config.json` を `ConfigSettingsBuilder` へデシリアライズ → `ApplyEnvironmentVariableOverrides` が `FOLDERDIFF_*` 環境変数をビルダーに適用 → `ProgramRunner.ApplyCliOverrides` が CLI フラグをビルダーに適用 → `ConfigSettingsBuilder.Validate()` で値の範囲を検証 → `ConfigSettingsBuilder.Build()` でイミュータブルな `ConfigSettings` を生成。これにより、バリデーションが CLI オーバーライド適用前に実行されていた潜在的バグも修正されました。DI コンテナは `ConfigSettings` を `IReadOnlyConfigSettings` としてのみ登録するようになり、下流サービスがミュータブルな型へキャストすることを防止します。すべてのテストファイルを `new ConfigSettingsBuilder { ... }.Build()` によるコンフィグ構築に更新しました。

- **レポート生成パラメータの ReportGenerationContext DTO への集約** — [`ReportGenerationContext`](Services/ReportGenerationContext.cs) を導入し、`ReportGenerateService.GenerateDiffReport`・`HtmlReportGenerateService.GenerateDiffReportHtml`・`AuditLogGenerateService.GenerateAuditLog` で重複していた 8 個のパラメータ（`oldFolderAbsolutePath`・`newFolderAbsolutePath`・`reportsFolderAbsolutePath`・`appVersion`・`elapsedTimeString`・`computerName`・`config`・`ilCache`）を統合しました。各サービスメソッドは単一の `ReportGenerationContext` パラメータを受け取るようになりました。`ProgramRunner.GenerateReport` で 1 つのコンテキストインスタンスを構築し、3 つのサービスに渡します。対応するテストファイルを更新しました。

- **警告セクションの警告メッセージと詳細テーブルのインターリーブ配置** — 各警告メッセージの直下に対応する詳細テーブルを配置するよう変更しました。従来はすべての警告メッセージを先に一括で列挙し、その後に詳細テーブルをまとめて出力していました。SHA256Mismatch 警告とタイムスタンプ逆行警告の両方がある場合、レイアウトは: SHA256Mismatch 警告 → SHA256Mismatch 詳細テーブル → タイムスタンプ逆行警告 → Timestamps Regressed 詳細テーブルとなります。Markdown と HTML の両レポートに適用。HTML レポートでは、各警告メッセージが独立した `<ul class="warnings">` 要素として詳細テーブルの直上にレンダリングされます。[`doc/samples/diff_report.md`](doc/samples/diff_report.md) および [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。テスト `GenerateDiffReport_Sha256MismatchDetailTable_AppearsImmediatelyAfterSha256Warning` を [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) に、`GenerateDiffReportHtml_Sha256MismatchDetailTable_AppearsImmediatelyAfterSha256Warning` を [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) に追加。

- **レポートテーブル列のセクション別最適化** — Markdown: 意味のないテーブルから `Disassembler` 列を削除: `[ x ] Ignored Files`、`SHA256Mismatch (Manual Review Recommended)` 警告テーブル、`Timestamps Regressed` 警告テーブル。`[ + ] Added Files` と `[ - ] Removed Files` テーブルからは `Legend`/`Diff Reason` 列と `Disassembler` 列の両方を削除。HTML: テーブル間列幅同期の安定性のため、すべてのテーブルが DOM 上で 8 列すべてを保持。不要な列は CSS クラス（`hide-disasm`、`hide-col6`）により `width: 0`、`visibility: hidden`、`border-color: transparent` で視覚的に非表示化。`[ = ] Unchanged Files` テーブルは `Disassembler` 列を維持し（ILMatch 行に逆アセンブラバージョンを表示）、HTML レポートでも値を出力するよう修正。`[ * ] Modified Files` テーブルは変更なし。`AppendTableStart` ヘルパーに `hideClasses` パラメータを追加し CSS hide クラスを `<table>` 要素に適用。col6 と Disassembler のヘッダ `<th>` に CSS ターゲット用の `col-diff-hd`/`col-disasm-hd` クラスを付与。`syncTableWidths()` は非表示列をスキップしてテーブル幅を計算。[`doc/samples/diff_report.md`](doc/samples/diff_report.md) および [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。テスト `GenerateDiffReport_ColumnStructure_PerTableColumns`、`GenerateDiffReport_WarningsColumnStructure_NoDisassemblerColumn` を [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) に、`GenerateDiffReportHtml_ColumnStructure_PerTableColumns` を [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) に追加。

- **Summary および IL Cache Stats HTML テーブルに列ヘッダを追加** — `stat-table`（Summary と IL Cache Stats）に `<thead>` 行を追加し、列ヘッダ（Summary: `Category | Count`、IL Cache Stats: `Metric | Value`）を表示するようにしました。`<th>` 要素は `border: 1px solid #bbb` を使用し、`[ x ] Ignored Files` やその他のファイル一覧テーブルのヘッダと同じ枠線太さ・色に統一しました。[`doc/samples/diff_report.html`](doc/samples/diff_report.html) および [`diff_report.css`](Services/HtmlReport/diff_report.css) を更新。

- **Markdown テーブルの Legend 列ボディを中央揃えに変更** — Markdown レポートの全テーブルで Legend（Diff Reason）列のボディを中央揃え（`:------:` セパレータ）に変更しました。対象テーブル: `[ x ] Ignored Files`、`[ = ] Unchanged Files`、`[ * ] Modified Files`、`[ ! ] SHA256Mismatch`、`[ ! ] Timestamps Regressed`。[`ReportGenerateService.SectionWriters.cs`](Services/ReportGenerateService.SectionWriters.cs) の出力ロジックおよびサンプル [`doc/samples/diff_report.md`](doc/samples/diff_report.md) を更新。[`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) のアサーションを更新。

- **セマンティック変更から集計テーブルを削除** — アセンブリごとの `sc-count` 集計テーブル（Class / Status / High / Medium / Low / Total）をセマンティック変更出力から削除しました。詳細テーブルに全情報が含まれているため、集計テーブルは視覚的ノイズでした。[`HtmlReportGenerateService.Sections.cs`](Services/HtmlReport/HtmlReportGenerateService.Sections.cs) から `AppendSummaryCountTable` メソッド、`ChangeOrder` ヘルパー、呼び出し箇所を削除。[`diff_report.css`](Services/HtmlReport/diff_report.css) から `sc-count` / `sc-cnt-*` CSS ルールを削除。[`diff_report.js`](Services/HtmlReport/diff_report.js) から `cntW` 計算、`table.sc-count` セレクタ、`--sc-cnt-class-w` CSS 変数参照を削除。[`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。

- **Importance 列のスタイルをテキスト色に変更** — セマンティック変更詳細テーブルの Importance 列のスタイルを背景塗りつぶしからテキストのみに変更: `High` は赤太字（`color:#d1242f;font-weight:bold`）、`Medium` は鮮やかなオレンジ太字（`color:#d97706;font-weight:bold`）、`Low` はスタイルなし。`TH_BG_IMPORTANCE_HIGH` / `TH_BG_IMPORTANCE_MEDIUM` 定数を削除。[`HtmlReportGenerateService.Helpers.cs`](Services/HtmlReport/HtmlReportGenerateService.Helpers.cs) の `ImportanceToStatusBg` を `ImportanceToStyle` にリネーム。[`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。

- **ボタン・バナーアイコンのモノトーン化** — [`HtmlReportGenerateService.cs`](Services/HtmlReportGenerateService.cs) の「Fold all details」ボタンから `▲` アイコンを削除。レビュー済みバナーのアイコンを変更: 「Reviewed:」テキストから `🔒` 鍵アイコンを削除、「Verify integrity」ボタンの `🔍` 虫眼鏡アイコンをモノトーンの `✓` チェックマークに置換。[`diff_report.js`](Services/HtmlReport/diff_report.js) および [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。

- **セマンティック変更詳細テーブルを Status → Importance の順でソート** — セマンティック変更詳細テーブルのソート順を、Status（`[ + ]` Added → `[ - ]` Removed → `[ * ]` Modified）を第一キー、次に各 Status グループ内で Importance 降順（`High` → `Medium` → `Low`）に変更しました。従来は Importance 降順のみでソートしていました。[`AssemblySemanticChangesSummary`](Models/AssemblySemanticChangesSummary.cs) の `EntriesByImportance` を `.OrderBy(ChangeOrder).ThenByDescending(Importance)` に更新。テスト `EntriesByImportance_SortsByChangeThenImportance` を [`AssemblySemanticChangesSummaryTests`](FolderDiffIL4DotNet.Tests/Models/AssemblySemanticChangesSummaryTests.cs) で更新。[`doc/samples/diff_report.html`](doc/samples/diff_report.html) の base64 ブロックを更新。

- **凡例（Change Importance）テーブルのラベルを色付きスタイルに変更** — 凡例（Change Importance）テーブルの Label 列ボディセルを詳細テーブルと同じ色付きテキストスタイルに変更: `High` は赤太字、`Medium` はオレンジ太字、`Low` はスタイルなし。従来は `<code>` タグで囲んでいました。[`HtmlReportGenerateService.Sections.cs`](Services/HtmlReport/HtmlReportGenerateService.Sections.cs) および [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。

- **IL ignore-strings テーブルに列ヘッダを追加** — 「Note: When diffing IL, lines containing any of the configured strings are ignored:」テーブルに `<thead>` を追加し、「Ignored String」ヘッダを表示。スタイルは `background:#f5f5f7` で Legend (Diff Detail) テーブルヘッダのデザインに統一。[`HtmlReportGenerateService.Sections.cs`](Services/HtmlReport/HtmlReportGenerateService.Sections.cs) および [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。

- **Summary・IL Cache Stats テーブルヘッダの背景色を統一** — Summary および IL Cache Stats の `stat-table` テーブルの `<th>` 要素に `style="background:#f5f5f7"` を追加し、Legend (Diff Detail) テーブルヘッダの背景色と統一しました。[`HtmlReportGenerateService.Sections.cs`](Services/HtmlReport/HtmlReportGenerateService.Sections.cs) および [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。

- **Access・Modifiers 列を `<code>` タグで囲む（矢印対応フォーマット）** — セマンティック変更詳細テーブルの Access・Modifiers 列の値を `<code>` タグで囲むようにしました。矢印を含む値（例: `public → private`）の場合は各側を個別に囲みます: `<code>public</code> → <code>private</code>`。`CodeWrapArrow` ヘルパーを [`HtmlReportGenerateService.Helpers.cs`](Services/HtmlReport/HtmlReportGenerateService.Helpers.cs) に追加。[`doc/samples/diff_report.html`](doc/samples/diff_report.html) の base64 ブロックを更新。

- **セマンティック変更テーブルの Access 列幅を拡大** — [`diff_report.css`](Services/HtmlReport/diff_report.css) の `col.sc-col-access-g` を `8em` から `16em` に拡大し、`<code>` タグ付きの矢印表記（例: `public → private`）に対応するスペースを確保。[`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。

- **開発者ガイドに Disassembler Availability エッジケースを文書化** — [`doc/DEVELOPER_GUIDE.md`](doc/DEVELOPER_GUIDE.md) に Disassembler Availability テーブルのエッジケース動作（全テキストファイル、`SkipIL` モード、ツール未検出、null/空のプローブ結果）についてバイリンガル（EN/JP）ドキュメントを追加。

- **`.sha256` ファイルの Verify integrity を簡素化** — `.sha256` ファイルの検証時に、レビュー済み HTML 自体が最終ハッシュを埋め込んでいるため、2 つ目のファイル選択ダイアログが不要になりました。ダウンロード時に第 2 プレースホルダ方式（`fff...f`）で `__finalSha256__` 定数に最終ハッシュを埋め込みます。`.sha256` 検証パスではファイル内容を `__finalSha256__` と直接比較し、合格/不合格の結果を表示します。[`diff_report.js`](Services/HtmlReport/diff_report.js) および [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。

#### 修正

- **凡例テーブルヘッダの枠線色の不一致** — `legend-table th` の枠線を `1px solid #ddd` から `1px solid #bbb` に変更し、[`diff_report.css`](Services/HtmlReport/diff_report.css) と [`doc/samples/diff_report.html`](doc/samples/diff_report.html) の両方で `[ x ] Ignored Files` 等のファイル一覧テーブルのヘッダ枠線色と一致させました。

- **セマンティック変更テーブルの Importance 列幅定義の欠落** — ソース CSS に存在する `col.sc-col-importance-g { width: 7em; }` ルールが [`doc/samples/diff_report.html`](doc/samples/diff_report.html) から欠落していた問題を修正。また `syncScTableWidths()` の `detW` 計算に Importance 列（7em）を追加し、詳細テーブル幅の過小評価を修正しました。[`diff_report.js`](Services/HtmlReport/diff_report.js) を更新。

- **サンプル HTML の JavaScript とソースの不整合** — [`doc/samples/diff_report.html`](doc/samples/diff_report.html) の JavaScript をソース [`diff_report.js`](Services/HtmlReport/diff_report.js) と同期: `DOMContentLoaded` の初期化順序を修正（`initColResize` → `syncTableWidths` → `syncScTableWidths` → `setupLazyDiff`）、`syncTableWidths()` のセレクタに `:not(.legend-table):not(.il-ignore-table)` 除外を追加、テーブル幅設定前の `if (w > 0)` ガードを追加、欠落していたコメントを復元、関数定義順序をソースに合わせて整理。

- **サンプル HTML の古い base64 セマンティック変更ブロック** — 手動作成された base64 ブロックがキャビートノートと「No semantic changes detected for this assembly.」を同時に表示していた問題を修正。コードロジック（[`HtmlReportGenerateService.Sections.cs`](Services/HtmlReport/HtmlReportGenerateService.Sections.cs)）と一致するよう、キャビートノートなしで「No structural changes detected. See IL diff for implementation-level differences.」のみ表示する正しい出力に置換しました。

- **「an SHA256」冠詞の誤り** — [`doc/samples/diff_report.md`](doc/samples/diff_report.md) と [`doc/samples/diff_report.html`](doc/samples/diff_report.html) の「only an SHA256 hash comparison」を [`Constants.cs`](Common/Constants.cs) の定数と一致するよう「only a SHA256 hash comparison」に修正。

- **プリフライト書込権限チェックの IOException ハンドリング厳密化** — `CheckReportsParentWritableOrThrow` が `IOException` を catch して暗黙的に return していたため、読み取り専用ファイルシステムマウントやネットワーク共有の書込失敗など、ディスク容量チェックではカバーされない環境固有の権限問題を取りこぼす可能性がありました。IOException 発生時に `ILoggerService` 経由で原因別の詳細をログ出力し、説明的なメッセージ付きの新しい `IOException` として再スローすることで fail-fast 動作を実現しました。`ValidateRunDirectories` と `CheckReportsParentWritableOrThrow` の両メソッドに `ILoggerService` パラメータを追加し、既存の `ValidateReportLabel` パターンと一貫性を持たせました。既存テスト（`CheckReportsParentWritableOrThrow_WhenDirectoryIsReadOnly_ThrowsUnauthorizedAccessException`、`CheckReportsParentWritableOrThrow_NonexistentParent_DoesNotThrow`、`ValidateRunDirectories_*`）をロガー引数に対応するよう更新。新テスト `CheckReportsParentWritableOrThrow_WritableDirectory_DoesNotThrow` および `CheckReportsParentWritableOrThrow_WhenDirectoryIsReadOnly_LogsAndThrowsIOException` を [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) に追加。

- **サンプル HTML の短縮型名の修正** — [`doc/samples/diff_report.html`](doc/samples/diff_report.html) の全 base64 エンコードセマンティック変更ブロックで、C# エイリアス型名（`string`、`int`、`void`）を完全修飾 .NET 名（`System.String`、`System.Int32`、`System.Void`）に修正。`SimpleSignatureTypeProvider` は常に完全修飾名を出力するため、サンプルもそれと一致させる必要がありました。

- **サンプル HTML のパラメータ名の欠落** — [`doc/samples/diff_report.html`](doc/samples/diff_report.html) の base64 ブロックで Execute メソッドの Parameters 列がパラメータ名なしの `System.String` を表示していた問題を修正。`System.String command = null` に修正し、パラメータ命名とデフォルト値構文の両方を示すようにしました。

- **未定義の `scheduleSave()` 関数呼び出し** — [`diff_report.js`](Services/HtmlReport/diff_report.js) の `collapseAll()` が未定義の `scheduleSave()` を呼び出していた問題を修正。正しい既存関数 `autoSave()` に置換。[`doc/samples/diff_report.html`](doc/samples/diff_report.html) も同様に更新。

- **`<code>` ラッピング変更後の CI テストアサーション不整合** — テスト `GenerateDiffReportHtml_AssemblySemanticChanges_KindBodyUseCodeEmphasis_AccessModifiersDoNot` が Access/Modifiers 列を `<code>` タグで囲まないことを検証していたが、出力ロジックの変更でタグ付きになりアサーション失敗。テスト名を `KindBodyAccessModifiersUseCodeEmphasis` にリネームし、`<code>` ラッピングを期待するようアサーションを更新。矢印対応フォーマットを検証する新テスト `AccessArrowWrapsEachSideInCode` を [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) に追加。

- **Verify integrity を `.sha256` 専用検証に簡素化** — `verifyIntegrity()` から HTML 自己検証パスを削除。レビュー済み HTML は「自分自身」であるため、コンパニオン `.sha256` ファイルのみ選択すれば十分です。ダウンロード時に第 2 プレースホルダ方式（`fff...f`）で `__finalSha256__` 定数に最終ハッシュを埋め込み、`.sha256` 検証パスではファイル内容を `__finalSha256__` と直接比較。ファイルピッカーは `input.accept = '.sha256'` で `.sha256` のみに制限。`DOMContentLoaded` 時に隠し `<input>` 要素を事前作成し、初回クリックから accept フィルタが確実に適用されるようにしました（一部ブラウザは動的作成した input の accept を即時反映しないため）。`onchange` ガードで非 `.sha256` ファイルを拒否するフォールバックも追加。[`diff_report.js`](Services/HtmlReport/diff_report.js) および [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。

### [1.6.0] - 2026-03-21

#### 変更

- **IReadOnlyConfigSettings による ConfigSettings の不変化** — `ConfigSettings` の全プロパティを読み取り専用で公開する `IReadOnlyConfigSettings` インターフェースを導入（リストプロパティは `IReadOnlyList<string>` として返却）。`ConfigSettings` がこのインターフェースを実装。下流の全サービスコンストラクタ（`FolderDiffService`、`FileDiffService`、`ILOutputService`、`DotNetDisassembleService`、`ILCachePrefetcher`、`ProgressReportService`、`FolderDiffExecutionStrategy`、`ReportGenerateService`、`HtmlReportGenerateService`）および `ReportWriteContext` がミュータブルな `ConfigSettings` の代わりに `IReadOnlyConfigSettings` を受け取るよう変更。`ProgramRunner.ApplyCliOverrides` のみミュータブルアクセスを保持。`ConfigSettingsTests` に `ConfigSettings_ImplementsIReadOnlyConfigSettings` および `IReadOnlyConfigSettings_ListProperties_AreReadOnly` テストを追加。

- **ロガーのスレッドセーフティ改善** — `LoggerService.LogMessage()` のログファイル書き込み部分に `lock` を追加し、並列差分処理時に複数スレッドが同時にロガーを呼び出した場合の `IOException` を防止しました。ロックオブジェクト（`_fileWriteLock`）はファイル I/O セクションのみを直列化し、コンソール出力はスレッドセーフであるため非ガードのままです。

- **冗長な例外ハンドリングを例外フィルターで集約** — 同一処理を行う型別 `catch` ブロックの繰り返しを、C# 例外フィルター（`catch (Exception ex) when (ex is X or Y or Z)`）に統合しました。対象ファイル: `ProgramRunner.cs`、`FolderDiffService.cs`、`FileDiffService.cs`、`LoggerService.cs`、`ILOutputService.cs`、`ILDiskCache.cs`、`ILCache.cs`、`DotNetDisassemblerCache.cs`、`ILCachePrefetcher.cs`、`ILTextOutputService.cs`、`DotNetDisassembleService.cs`、`DotNetDisassembleService.VersionLabel.cs`、`ReportGenerateService.cs`、`HtmlReportGenerateService.cs`、`DotNetDetector.cs`。動作の変更はなく、コード量の削減と保守性の向上が目的です。

- **HTML レポートの CSS/JS を埋め込みリソースに抽出** — `HtmlReportGenerateService.Css.cs` / `HtmlReportGenerateService.Js.cs` 内のインライン C# 文字列リテラルとして記述されていた CSS スタイルシートと JavaScript を、スタンドアロンファイル（`Services/HtmlReport/diff_report.css`、`Services/HtmlReport/diff_report.js`）に分離し `<EmbeddedResource>` としてコンパイル。実行時は `LoadEmbeddedResource()` が `Assembly.GetManifestResourceStream()` 経由でリソースを読み込みます。JS ファイルは `{{STORAGE_KEY}}` / `{{REPORT_DATE}}` プレースホルダーを使用し、レポート生成時に置換されます。生成される HTML レポートへの動作変更はありません。`HtmlReportGenerateServiceTests` に `LoadEmbeddedResource_CssResource_ReturnsNonEmptyString`、`LoadEmbeddedResource_JsResource_ReturnsNonEmptyString`、`LoadEmbeddedResource_JsResource_ContainsPlaceholders`、`LoadEmbeddedResource_InvalidResource_ThrowsFileNotFoundException` テストを追加。

- **ファイルハッシュ比較を MD5 から SHA256 に移行** — コードベース全体の `MD5` 使用を `SHA256` に置換。`FileComparer.DiffFilesByHashAsync()` が `MD5.Create()` の代わりに `SHA256.Create()` を使用。`ComputeFileMd5Hex()` を `ComputeFileSha256Hex()` にリネーム。列挙値 `MD5Match`/`MD5Mismatch` を `SHA256Match`/`SHA256Mismatch` にリネーム。プロパティ `HasAnyMd5Mismatch` を `HasAnySha256Mismatch` にリネーム。`WARNING_MD5_MISMATCH` を `WARNING_SHA256_MISMATCH` にリネーム。レポートラベル、サンプルレポート、README、DEVELOPER_GUIDE、TESTING_GUIDE、テストをすべて更新。SHA256 はより強い衝突耐性を提供し、最新 CPU の SHA-NI ハードウェアアクセラレーションを活用します。IL キャッシュはキー互換（ファイル内容ハッシュ + ツールラベル）のため移行不要で、古い MD5 キーのエントリは TTL により自然に期限切れとなります。

- **FolderDiffService の直接 Console.WriteLine をロガーに置換** — `FolderDiffService.ExecuteFolderDiffAsync()` 内の `Console.WriteLine(LOG_FOLDER_DIFF_COMPLETED)` / `Console.Out.Flush()` 呼び出しを `_logger.LogMessage(AppLogLevel.Info, ..., shouldOutputMessageToConsole: true)` に置換しました。これにより "Folder diff completed." メッセージが他のすべてのログメッセージと同じ `ILoggerService` パイプラインを通り、ログファイルとコンソールの両方に出力されます。不要になった `ConsoleRenderCoordinator.RenderSyncRoot` ロックおよび `using FolderDiffIL4DotNet.Core.Console` ディレクティブを削除しました。[`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) に `ExecuteFolderDiffAsync_WhenCompleted_LogsFolderDiffCompletedViaLogger` テストを追加。

- **差分パイプライン全体に CancellationToken を伝播** — 差分パイプラインの全非同期メソッドに `CancellationToken cancellationToken = default` パラメータを追加: `IFolderDiffService.ExecuteFolderDiffAsync`、`IFileDiffService.PrecomputeAsync`、`IFileDiffService.FilesAreEqualAsync`、`IILOutputService.PrecomputeAsync`、`IILOutputService.DiffDotNetAssembliesAsync`、`IDotNetDisassembleService.DisassemblePairWithSameDisassemblerAsync`、`IDotNetDisassembleService.PrefetchIlCacheAsync`。トークンは `FolderDiffService` → `FileDiffService` → `ILOutputService` → `DotNetDisassembleService` → `ILCachePrefetcher` へ伝播。`Parallel.ForEachAsync` 呼び出しでは `ParallelOptions.CancellationToken` でトークンを渡すよう変更。逐次差分分類のループ境界、事前計算バッチ処理、ファイル単位比較エントリの主要箇所で `cancellationToken.ThrowIfCancellationRequested()` を呼び出し。全パラメータにデフォルト値を設定し後方互換性を維持。[`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) に `ExecuteFolderDiffAsync_WhenCancelled_ThrowsOperationCanceledException` テストを追加。

- **コード全体にバイリンガル（EN/JP）コメントを追加** — 日本語翻訳が欠けていたすべてのテストファイルにバイリンガル XML doc コメントおよびインラインコメントを追加。対象ファイル: `CoreSeparationTests`、`ProcessHelperTests`、`AssemblySemanticChangesSummaryTests`、`DotNetDisassemblerCacheTests`、`FileDiffServiceUnitTests`、`FolderDiffExecutionStrategyTests`、`ILOutputServiceTests`、`ProgressReportServiceTests`。メインソースファイルはすでにバイリンガルカバレッジ 100% であったため、この変更でテスト層を完了。

- **テストのマジックナンバーを ConfigSettings 既定値定数に置換** — `ConfigSettings` に `public const` の既定値定数（例: `DefaultTextDiffParallelThresholdKilobytes`、`DefaultTextDiffChunkSizeKilobytes`、`DefaultILPrecomputeBatchSize`、`DefaultDisassemblerBlacklistTtlMinutes`、`DefaultInlineDiffMaxDiffLines` など）を追加し、すべてのプロパティ初期化子でそれらを参照するよう変更。テストファイル（`ConfigSettingsTests`、`FileDiffServiceUnitTests`、`FolderDiffServiceUnitTests`、`ConfigServiceTests`、`TextDifferTests`）で裸の数値リテラルの代わりに名前付き定数を使用し、散在するマジックナンバーを排除。既定値の変更時にテストが自動的に追従します。

#### 修正

- **メソッドのアクセス修飾子変更検出** — アクセス修飾子の変更（例: `public` → `internal`）および修飾子の変更（例: `static`、`virtual` の追加・削除）が、Assembly Semantic Changes テーブルで `Modified` エントリとして検出されるようになりました。以前は、メソッドの一致キーにアクセス修飾子が含まれず、交差比較が IL ボディバイトのみを確認していたため、アクセス修飾子のみまたは修飾子のみの変更はセマンティックサマリーに表示されませんでした。

- **Property/Field の型・修飾子変更検出** — 型の変更（例: `string` → `int`）、アクセス修飾子の変更、修飾子の変更がプロパティおよびフィールドで `Modified` エントリとして検出されるようになりました。以前は、プロパティ・フィールドのキーが名前ベースのみで、一致するキーに対する `Modified` 比較がなかったため、同名の型変更やアクセス修飾子変更が報告されませんでした。

#### 追加

- **警告セクションに MD5Mismatch 詳細テーブルを追加** — 1つ以上のファイルが `MD5Mismatch` に分類された場合、警告セクションに `[ ! ] Modified Files — MD5Mismatch (Manual Review Recommended)` というタイトルの詳細テーブルを表示し、該当ファイルのタイムスタンプ、diff 詳細、逆アセンブラ情報を一覧します。既存の `[ ! ] Modified Files — Timestamps Regressed` テーブルと同じ列レイアウト・青色スキームを使用。ファイルはパスのアルファベット順でソート。Markdown（`diff_report.md`）および HTML（`diff_report.html`）の両レポートに適用。両方の警告が存在する場合、MD5Mismatch テーブルは Timestamps Regressed テーブルの前に表示されます。[`doc/samples/diff_report.md`](doc/samples/diff_report.md) と [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) に `Md5MismatchWarning_IncludesDetailTable`、`Md5MismatchTable_AppearsBeforeTimestampRegressedTable` テストを追加。[`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) に `WritesMd5MismatchDetailTable_WhenMd5MismatchExists`、`Md5MismatchTable_AppearsBeforeTimestampRegressedTable` テストを追加。

- **セマンティックサマリーの注意書き** — HTML レポートの Assembly Semantic Changes セクションに、セマンティックサマリーは補助情報であり最終確認は IL diff で行うべき旨の注意書きを追加。`.sc-caveat` CSS クラスでスタイリング（イタリック、グレー）。[`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) に `ShowsCaveatNote`、`CaveatCssExists` テストを追加。

#### 削除

- **Markdown レポートの Assembly Semantic Changes セクション** — Markdown レポート（`diff_report.md`）から `## Assembly Semantic Changes` セクションを削除しました。Assembly Semantic Changes は HTML レポートの展開可能なインライン行としてのみ表示されます。`ReportGenerateService.SectionWriters.cs` から `AssemblySemanticChangesSectionWriter` とそのヘルパーメソッドを削除。[`doc/samples/diff_report.md`](doc/samples/diff_report.md) から該当セクションを削除。[`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) のテストを更新し、セクションが出力されないことを確認。

#### 変更

- **Modified エントリの旧→新表示** — Assembly Semantic Changes テーブルの `Modified` エントリで、Access、Modifiers、Type 列に値が変更された場合は `旧 → 新` 形式で表示するよう変更（例: `public → internal`、`System.String → System.Int32`）。変更がない場合は現在の値のみ表示。

- **Access・Modifiers 列の code 強調表示を廃止** — Assembly Semantic Changes テーブルの Access 列および Modifiers 列ボディセルで `<code>` 強調表示を使用しないよう変更。これらの列は `旧 → 新` の矢印表記を含む可能性があり、等幅強調を適用すると視覚的に不整合となるため。Kind 列および Body 列は引き続き `<code>` 強調表示を使用。[`doc/samples/diff_report.html`](doc/samples/diff_report.html) を更新（Modifiers `旧 → 新` を示す行を追加）。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) のテスト `KindBodyUseCodeEmphasis_AccessModifiersDoNot` を更新。

- **Unchanged Files テーブルのソート順** — diff-detail 結果（`MD5Match` → `ILMatch` → `TextMatch`）でソートし、次にファイルパスの昇順でソートするよう変更。以前はファイルパスのみでソートしていました。Markdown および HTML レポートの両方に適用。

- **Modified Files テーブルのソート順** — diff-detail 結果（`TextMismatch` → `ILMismatch` → `MD5Mismatch`）でソートし、次にファイルパスの昇順でソートするよう変更。以前はファイルパスのみでソートしていました。Markdown および HTML レポートの両方に適用。

- **Modified Files — Timestamps Regressed テーブルのソート順** — Modified Files テーブルと同じソート順（`TextMismatch` → `ILMismatch` → `MD5Mismatch`、次にパス）に変更。以前はファイルパスのみでソートしていました。Markdown および HTML レポートの両方に適用。

### [1.5.0] - 2026-03-21

#### 追加

- **Assembly Semantic Changes** を追加 — `System.Reflection.Metadata` を使用した `ILMismatch` アセンブリのメンバーレベル変更検出。変更のあった各 .NET アセンブリについて、型・メソッド・プロパティ・フィールドの増減およびメソッドボディの変更をレポートに出力します。新しい設定項目 `ShouldIncludeAssemblySemanticChangesInReport`（既定: `true`）で制御可能。
  - **レポート配置**: HTML レポートでは IL diff の上に展開可能なインライン行として表示。
  - **テーブル列**（チェックボックス含む 12 列）: `✓`（チェックボックス）、`Class`（完全修飾型名）、`BaseType`（基底クラス＋インターフェース）、`Status`（`Added`/`Removed`/`Modified`、`[ + ]`/`[ - ]`/`[ * ]` マーカー付き）、`Kind`（`Class`/`Record`/`Struct`/`Interface`/`Enum`/`Constructor`/`StaticConstructor`/`Method`/`Property`/`Field`）、`Access`（`public`/`internal`/`protected`/`private`）、`Modifiers`（`static`/`abstract`/`virtual`/`override`/`sealed`/`readonly`/`const`/`static literal`/`static readonly`）、`Type`（Field/Property の宣言型のみ）、`Name`、`ReturnType`、`Parameters`（括弧なしで表示）、`Body`（メソッドボディまたはフィールド初期化子の IL 変更時に `Changed`、それ以外は空欄）。
  - **集計テーブル**: Class 別に `Added`/`Removed`/`Modified` の件数を表示する第二テーブル。同一クラスの連続行は Class セルを結合。
  - **Record 検出**: `EqualityContract` プロパティの有無で Record 型をヒューリスティックに判定。
  - **HTML スタイル**: テーブルヘッダ背景 `#98989d`（ライトグレー）、データセル（`td`）背景 `#fff`（白）で `diff-row` グレーとのコントラストを確保、行単位チェックボックス（auto-save 対応）、CSS カスタムプロパティとドラッグハンドルによるリサイズ可能な列。
  - **新規ファイル**: [`AssemblyMethodAnalyzer`](Services/AssemblyMethodAnalyzer.cs)、[`AssemblySemanticChangesSummary`](Models/AssemblySemanticChangesSummary.cs)（算出プロパティ `AddedCount`/`RemovedCount`/`ModifiedCount`）。
  - **テスト**: [`AssemblyMethodAnalyzerTests`](FolderDiffIL4DotNet.Tests/Services/AssemblyMethodAnalyzerTests.cs)、[`AssemblySemanticChangesSummaryTests`](FolderDiffIL4DotNet.Tests/Models/AssemblySemanticChangesSummaryTests.cs)、[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) に新規アサーション（`TableHeaderUsesLighterGray`、`ThScColCbCssRuleExists`、`TdHasWhiteBackground`）。
  - **ドキュメント**: [README.md](README.md) にバイリンガルの **Assembly Semantic Changes** セクションを追加、[TESTING_GUIDE.md](doc/TESTING_GUIDE.md) に [`AssemblyMethodAnalyzerTests`](FolderDiffIL4DotNet.Tests/Services/AssemblyMethodAnalyzerTests.cs)・[`AssemblySemanticChangesSummaryTests`](FolderDiffIL4DotNet.Tests/Models/AssemblySemanticChangesSummaryTests.cs) を追加、[`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期。

#### 変更

- **凡例セクション** — Markdown（`| Label | Description |`）および HTML（`<table class="legend-table">`）の両方で箇条書きからテーブル形式に変換。`table.legend-table` の CSS（ボーダー、パディング）を追加。

- **IL Cache Stats および Summary セクション** — Markdown レポートで箇条書きからテーブル形式（`| Category | Count |`、`| Metric | Value |`）に変換。HTML レポートの `table.stat-table td` に可視ボーダー（`1px solid #ddd`）を追加。

- **ファイル一覧セクション** — Ignored、Unchanged、Added、Removed、Modified のファイルセクションを Markdown レポートでチェックボックス箇条書きからテーブルに変換（列: `| Status | File Path | Timestamp | Legend | Disassembler |`）。

- **`InlineDiffMaxEditDistance` ハイライト** — HTML レポートの切り詰めメッセージで `InlineDiffMaxEditDistance` に `<code>` タグを追加し、既存の `InlineDiffMaxDiffLines` のコードスタイルと統一。

- **差分行の背景色** — `tr.diff-row` の背景を `#f6f8fa` から `#edf0f4` に変更し、白いセマンティック変更テーブルとのコントラストを向上。

- **Myers Diff 引用** — すべての Markdown および HTML レポートファイルで "Algorithmica" の後の巻番号 "1" を太字化（`<b>1</b>(2)` / `**1**(2)`）。

- **クリップボードコピーボタン** — HTML レポートテーブルの各 File Path セルに行単位のクリップボードコピーボタン（重なった四角アイコン）を追加。`copyPath(btn)` 関数が個別のファイルパスをクリップボードにコピーし、チェックマークのフィードバックアニメーションを表示。従来の列ヘッダレベルのコピーボタンを置き換え。

- **行ホバーハイライト** — ファイルテーブル行およびセマンティック変更テーブル行にライトパープルのホバーハイライト（`#f3eef8`）を追加。統計テーブル（Summary、IL Cache Stats）、凡例テーブル、IL 無視文字列テーブルはホバーハイライト対象外。

- **Summary テーブル行色分け** — HTML レポートの Summary テーブルの行に背景色を追加: Added（`#e6ffed` 緑）、Removed（`#ffeef0` 赤）、Modified（`#e3f2fd` 青）。対応するセクションヘッダ色と統一。

- **凡例テーブル幅** — HTML レポートの凡例テーブルの幅を制限（`max-width: 44em`）。内容が固定テキストのため、過度に横に伸びることを防止。

- **IL 無視文字列テーブル** — IL 行含有文字列無視のリストを、インラインのカンマ区切りリストから Markdown（`| Ignored String |`）および HTML（`<table class="legend-table">`）の両方でテーブル形式に変換。1行1文字列で可読性を向上。

- **タイムスタンプ括弧削除** — Markdown レポートテーブルの Timestamp 列値から `[` と `]` の括弧を削除。テーブルセル内では括弧は不要なため。

### [1.4.1] - 2026-03-20

#### 追加

- HTML レポートヘッダに [Myers Diff Algorithm](http://www.xmailserver.org/diff2.pdf) の参照注記を追加: `ILMismatch` および `TextMismatch` のインライン差分が本アルゴリズムで計算されていることを示すクリック可能な引用（E. W. Myers, "An O(ND) Difference Algorithm and Its Variations," *Algorithmica* **1**(2), 1986）。[`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期。テスト `GenerateDiffReportHtml_Header_ContainsMyersDiffAlgorithmReference` を [`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) に追加。

#### 変更

- 大規模クラス 4 件を partial class ファイルに分割し、公開 API を変更せずに保守性を向上: [`ProgramRunner`](ProgramRunner.cs)（`ProgramRunner.Types.cs` を抽出）、[`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs)（`Sections.cs`・`Helpers.cs`・`Css.cs`・`Js.cs` を `Services/HtmlReport/` 配下に抽出）、[`FolderDiffService`](Services/FolderDiffService.cs)（`ILPrecompute.cs`・`DiffClassification.cs` を抽出）、[`ReportGenerateService`](Services/ReportGenerateService.cs)（`SectionWriters.cs` を抽出）。

- [`FolderDiffIL4DotNet.csproj`](FolderDiffIL4DotNet.csproj) と [`FolderDiffIL4DotNet.Core.csproj`](FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj) に `<Nullable>enable</Nullable>` と `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` を追加。両プロジェクト全体（31 ファイル）に nullable 参照型アノテーション（`?` サフィックス、`null!` 初期化子）を適用し、CS8600–8604/CS8618/CS8625 の一時抑制 `<NoWarn>` を削除済み。XML ドキュメント警告（CS1591, CS1573）はドキュメント整備完了まで引き続き抑制。

#### 追加

- [`FolderDiffIL4DotNet.Benchmarks`](FolderDiffIL4DotNet.Benchmarks/) プロジェクトを [BenchmarkDotNet](https://www.nuget.org/packages/BenchmarkDotNet/) 0.14.0 で追加。[`TextDifferBenchmarks`](FolderDiffIL4DotNet.Benchmarks/TextDifferBenchmarks.cs)（小・中・大規模 IL 風差分）と [`FolderDiffBenchmarks`](FolderDiffIL4DotNet.Benchmarks/FolderDiffBenchmarks.cs)（ファイル列挙・ハッシュ比較）を収録。実行: `dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks`。

- E2E 逆アセンブラテストにツール利用可能性に加えて `FOLDERDIFF_RUN_E2E=true` 環境変数を必須にしました。CI パイプラインで E2E テスト実行を明示的に制御できます。

#### 修正

- macOS で HTML レポートの Timestamp 列が見切れる問題を修正しました（[`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs)）。列幅が `16em` 固定かつ `overflow: hidden` だったため、`[YYYY-MM-DD HH:MM:SS → YYYY-MM-DD HH:MM:SS]` 形式の二重タイムスタンプ（約 300 px）が macOS の SF Pro フォントの文字幅により切れていました。Windows では偶然収まっていましたが、macOS では再現していました。修正として `col.col-ts-g` の幅を `16em` から `22em` に拡大し、`td.col-ts` から不要な `width: 16em` と `overflow: hidden` を削除しました。`<col>` の幅と `white-space: nowrap` の組み合わせでタイムスタンプを一行に保持します。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) の CSS アサーションを更新しました。[`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。

### [1.4.0] - 2026-03-20

#### 追加

- `--print-config` CLI フラグを [`ProgramRunner`](ProgramRunner.cs) に追加しました。`FolderDiffIL4DotNet --print-config`（`--config <path>` との組み合わせも可）を実行すると、有効なコンフィグ（[`config.json`](config.json) をデシリアライズし `FOLDERDIFF_*` 環境変数オーバーライドを適用した最終状態）をインデント付き JSON として標準出力に出力し、終了コード 0 で終了します。ソースを読まずにデフォルト値や上書き後の設定を確認でき、出力をリダイレクトすることで [`config.json`](config.json) のひな形も生成できます。設定読込エラー（ファイルなし・JSON 不正）は終了コード 3 で stderr にエラー内容を出力します。実装では [`CliOptions`](Runner/CliOptions.cs)・[`CliParser`](Runner/CliParser.cs) に `PrintConfig` を追加し、[`ProgramRunner`](ProgramRunner.cs) に `PrintConfigAsync` メソッドと `--help` 表示の更新を追加しました。また、前バージョンで追加した [`InlineDiffLazyRender`](Models/ConfigSettings.cs) プロパティに対する環境変数オーバーライドエントリ（`FOLDERDIFF_INLINEDIFFLAZYRENDER`）が [`ConfigService.ApplyEnvironmentVariableOverrides`](Services/ConfigService.cs) に漏れていたため、あわせて修正しました。[`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) に 4 件のテストを追加しました（`PrintConfigFlag_ExitsZeroAndOutputsJson`・`PrintConfigFlag_ReflectsEnvVarOverride`・`PrintConfigFlag_WithCustomConfigPath_ReflectsCustomValues`・`PrintConfigFlag_WithMissingConfig_ReturnsConfigurationError`）。日英 [README.md](README.md) を更新しました。

- [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml) に `test-windows` ジョブを追加しました。既存の Ubuntu `build` ジョブと並行して `windows-latest` 上でフルテストスイートを実行します。restore / build / [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) インストール後、`DOTNET_ROLL_FORWARD=Major` 付きでテストを実行します。これにより、CI では常にスキップされていた E2E 逆アセンブラテスト（[`RealDisassemblerE2ETests`](FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs)）が push のたびにフルで実行されるようになりました。日英 [doc/TESTING_GUIDE.md](doc/TESTING_GUIDE.md) と [doc/DEVELOPER_GUIDE.md](doc/DEVELOPER_GUIDE.md) を更新しました。

- [`InlineDiffLazyRender`](Models/ConfigSettings.cs) 設定（既定値 `true`）を追加しました。有効時、インライン差分テーブルの HTML を Base64 エンコードして各 `<details>` 要素の `data-diff-html` 属性に格納し、JavaScript がユーザーの展開操作時にデコードして DOM に挿入します。Modified ファイルが大量にある場合の初期 DOM ノード数を大幅に削減できます（例: 5,000 件 × 200 diff 行 × 3 セル ≈ 300 万ノード削減）。初期ページロードや操作（Clear all・列リサイズ・localStorage 保存）が劇的に高速化します。`setupLazyDiff()` / `decodeDiffHtml()` JavaScript 関数は常に HTML に含まれ、`data-diff-html` 属性がない場合は何も行いません。[`InlineDiffLazyRender`](Models/ConfigSettings.cs) = `false` にすると旧来の動作（全差分を DOM に直接埋め込み）に戻り、ブラウザの「ページ内検索」で折りたたまれた差分内容も検索できます。実装は [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs)（新メソッド `BuildDiffViewHtml`・`AppendInlineDiffRow` に遅延/非遅延分岐・JS `setupLazyDiff`/`decodeDiffHtml` 追加）。[`ConfigSettings`](Models/ConfigSettings.cs) にプロパティを追加。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) に 4 件のテストを追加、`CreateConfig` ヘルパーに `lazyRender` パラメータを追加。日英 [README.md](README.md) を更新しました。

- [`config.json`](config.json) の全スカラー設定を環境変数で実行時に上書きできるようになりました。リスト型以外のプロパティはすべて `FOLDERDIFF_<プロパティ名>` 形式の環境変数で上書き可能です（例: `FOLDERDIFF_MAXPARALLELISM=4`、`FOLDERDIFF_ENABLEILCACHE=false`、`FOLDERDIFF_ILCACHEDIRECTORYABSOLUTEPATH=/tmp/il-cache`）。bool 値は `true`/`false`（大文字小文字不問）と `1`/`0` を受け付けます。型に合わない値は警告なしで無視されます。環境変数は JSON 読み込み後・バリデーション前に適用されるため、JSON と同じ制約チェックの対象になります。実装は [`ConfigService.ApplyEnvironmentVariableOverrides`](Services/ConfigService.cs)。[`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) に 10 件のテストを追加（int/bool/string オーバーライド・環境変数優先・不正値無視・バリデーション通過・大文字小文字不問 bool）。[`ProgramRunner`](ProgramRunner.cs) の `--help` 出力に環境変数の概要を追加。日英 [README.md](README.md) に「環境変数によるオーバーライド」セクションを追加しました。

- ブランチカバレッジを 71.6 % から 83.7 % に改善しました。[`DisassemblerBlacklist`](Services/DisassemblerBlacklist.cs) と [`DisassemblerHelper`](Services/DisassemblerHelper.cs) の未到達分岐を網羅する 11 件のテストを追加しました。[`DisassemblerBlacklistTests`](FolderDiffIL4DotNet.Tests/Services/DisassemblerBlacklistTests.cs) の新テスト: `RegisterFailure_NullOrWhitespace_DoesNotThrow_AndNoEntryCreated`、`ResetFailure_NullOrWhitespace_DoesNotThrow`、`ResetFailure_NonExistentCommand_DoesNotThrow`。[`DisassemblerHelperTests`](FolderDiffIL4DotNet.Tests/Services/DisassemblerHelperTests.cs) の新テスト: `ResolveExecutablePath_RelativePathWithSeparator_NonExistent_ReturnsNull`、`ResolveExecutablePath_RelativePathWithSeparator_Existing_ReturnsFullPath`、`ResolveExecutablePath_WhitespacePathVariable_ReturnsNull`、`ResolveExecutablePath_PathWithEmptyEntries_SkipsEmptyAndReturnsNull`、`ResolveExecutablePath_CommandFoundInPath_ReturnsAbsolutePath`、および Windows 専用の `EnumerateExecutableNames` テスト 3 件（`.exe`/`.cmd`/`.bat` 拡張子を持つコマンド名に対して重複拡張子が追加されないことを検証）。`DisassemblerBlacklist` のブランチカバレッジは 100 %、`DisassemblerHelper` は 53 % から 80 % に向上しました。

- Myers diff アルゴリズムの解説ドキュメント [`doc/MYERS_DIFF_ALGORITHM.md`](doc/MYERS_DIFF_ALGORITHM.md)（日英バイリンガル）を追加しました。[`TextDiffer.cs`](FolderDiffIL4DotNet.Core/Text/TextDiffer.cs) の実装に即した包括的な解説で、編集グラフモデル・対角線と D パス理論・前向きパス（V 配列と貪欲スネーク延長）・スナップショットからのバックトラック・全手順付きの具体例・100 万行 IL ファイルを用いた O(D² + N + M) 計算量の分析・実装上の要点（オフセットトリック・スナップショット最適化・早期打ち切り）・LCS / Myers / Patience / Histogram 各アルゴリズムの比較表を網羅しています。README.md の従来のアルゴリズム解説インライン節はこのガイドへのリンクに置き換えました。

#### 修正

- IL ディスクキャッシュのデフォルトディレクトリを実行ファイル隣（`<exe>/ILCache`）から OS 標準のユーザーローカルデータディレクトリへ変更しました。Windows では `%LOCALAPPDATA%\FolderDiffIL4DotNet\ILCache`、macOS/Linux では `~/.local/share/FolderDiffIL4DotNet/ILCache` が使用されます。従来のデフォルトはコンテナや読み取り専用デプロイ環境で起動失敗を引き起こし、マルチユーザー環境ではインストールディレクトリにキャッシュファイルを書き込む問題がありました。変更は [`RunScopeBuilder.CreateIlCache`](Runner/RunScopeBuilder.cs)（`AppContext.BaseDirectory` → `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)`）。[`ConfigSettings`](Models/ConfigSettings.cs) の [`ILCacheDirectoryAbsolutePath`](Models/ConfigSettings.cs) XML コメントを更新し、[`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) にテスト `CreateIlCache_WhenPathIsEmpty_DefaultsToLocalApplicationDataSubfolder` を追加、日英 [README.md](README.md) を更新しました。

- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) の HTML レポートにおけるインライン差分の番号表示を修正しました。`Show diff` / `Show IL diff` の前や、インライン差分スキップ文言に表示される `#N` が内部の 0 始まりインデックスではなく、左端 `#` 列と同じ 1 始まりの行番号になるよう統一しました。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) にテスト `GenerateDiffReportHtml_InlineDiffSummary_UsesSameOneBasedNumberAsLeftmostColumn` を追加し、[README.md](README.md) と [テストガイド](doc/TESTING_GUIDE.md) も更新しました。

### [1.3.0] - 2026-03-17

#### 変更

- [`InlineDiffMaxOutputLines`](Models/ConfigSettings.cs) と [`InlineDiffMaxDiffLines`](Models/ConfigSettings.cs) の既定値を `500`/`1000` から **`10000`** に引き上げました（[`ConfigSettings`](Models/ConfigSettings.cs)・[`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs)・[`TextDiffer`](FolderDiffIL4DotNet.Core/Text/TextDiffer.cs)）。[`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs)、日英 [README.md](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md) を更新しました。

- [`TextDiffer`](FolderDiffIL4DotNet.Core/Text/TextDiffer.cs) の差分アルゴリズムを O(N×M) の LCS から **Myers diff**（O(D² + N + M) 時間・O(D²) 空間、D = 編集距離）に置き換えました。従来の `m × n > 4 000 000` セル数ガードを廃止し、新しい設定項目 [`InlineDiffMaxEditDistance`](Models/ConfigSettings.cs)（既定値 `4 000`、挿入行数 + 削除行数の合計上限）に置き換えました。差分が少なければ数百万行のファイルもインライン差分を表示できます（例: 237 万行の IL ファイルを 20 行の差分で比較した場合、ミリ秒以内に完了）。[`TextDifferTests`](FolderDiffIL4DotNet.Tests/Core/Text/TextDifferTests.cs) を更新: `Compute_InputExceedsLcsLimit_ReturnsTruncatedMessage` を `Compute_EditDistanceExceedsLimit_ReturnsTruncatedMessage` に置換し、`Compute_LargeFilesSmallEditDistance_ProducesCorrectDiff` と `Compute_VeryLargeFilesWithTinyDiff_ProducesInlineDiff` を追加しました。

- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) の HTML レポート UX を改善しました。ダウンロードボタンのアイコンを `⇩` から `⤓` に変更し、ラベルを「Download as reviewed」に変更。レビュー済みファイルのバナー表示を `"Reviewed: <タイムスタンプ> — read-only"` に変更。Added/Removed のセクション見出し・列ヘッダ背景色を GitHub diff パレット（緑 `#22863a` / 赤 `#b31d28` / Added 背景 `#e6ffed` / Removed 背景 `#ffeef0`）に統一。`No` 列の幅を `3.2em` に拡大し、最大 999,999 件まで対応。`Diff Reason` 列が空のセルに幽霊 `<code>` 要素が残る問題を修正。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。
- HTML レポートの Timestamp 列がリサイズ時にガタガタする問題を修正しました。主要ファイルリストテーブルを `width: auto` から `table-layout: fixed; width: 1px` に変更し、`syncTableWidths()` JavaScript 関数を追加。CSS カスタムプロパティ（`--col-reason-w`、`--col-notes-w`、`--col-path-w`、`--col-diff-w`）の和として各テーブルの明示的なピクセル幅を設定し、`DOMContentLoaded` とリサイズ操作後に呼び出します。列ヘッダのテキストを `span.th-label { display: block; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }` でラップし、ヘッダ内容が確実にクリップされるようにしました。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。
- レビュー済みモードのチェックボックスがグレーアウトして見にくい問題を修正しました。`cb.disabled = true` を `cb.style.pointerEvents = 'none'; cb.style.cursor = 'default';` に変更し、ブラウザ固有のグレー描画を回避してアクセントカラーを維持するようにしました。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。
- 「Download as reviewed」が現在の列幅をデフォルトとして reviewed ファイルに焼き込むようになりました。`downloadReviewed()` が 5 つの列幅 CSS カスタムプロパティの現在の実効値を取得し、エクスポートした HTML の `:root` CSS ルールをその値で置き換えるとともに `<html>` 要素のインライン `style` 属性を削除します。これにより、reviewed スナップショットはサインオフ時の列幅レイアウトで開くようになります。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。
- 「Clear all」実行時に列幅をデフォルトに戻し、すべてのインライン差分 `<details>` を閉じるようになりました。`clearAll()` が CSS カスタムプロパティを削除、`syncTableWidths()` を呼び出し、全 `<details>` の `open` 属性を削除します。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。
- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) の `td.col-reason`・`td.col-ts`・`td.col-diff` のボディセルに `text-align: center` を追加しました（`col-diff` は Ignored Files テーブルでは「Location」（`old`/`new`/`old/new`）、他のテーブルでは差分タイプ（`ILMismatch`・`TextMismatch` など）を表示する列です）。`td.col-path`（File Path）は左揃えのまま。列ヘッダおよび `td.col-notes` は変更なし。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) のテスト `GenerateDiffReportHtml_BodyCells_ColReasonPathTs_HaveCenterAlignment` を合わせて更新しました。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。
- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) の `.reviewed-banner` テキスト色を `#2d7a2d`（緑）から `#1f2328`（ほぼ黒）に変更し、reviewed ファイルのタイムスタンプバナーを視覚的に中立な表示にしました。サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を同期しました。

#### 変更（続き）

- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) の HTML レポートテーブルの列構成を変更しました。Timestamp 列を `22em` から `16em` に縮小。Diff Reason 列を `20em` から `9em` に縮小し、逆アセンブラのラベル文字列を除いた差分タイプのみ（`ILMismatch`、`TextMismatch` など）を表示するよう変更。最右端に 8 列目 **Disassembler** 列（`28em`、リサイズ可能）を新設し、各行の逆アセンブララベルおよびバージョン文字列を表示。JavaScript（`colVarNames`、`clearAll`、`syncTableWidths`）および CSS（`:root` カスタムプロパティ、`col.col-*-g`、`td.col-*`）を対応する値に更新しました。

- [`ConfigSettings`](Models/ConfigSettings.cs) の [`InlineDiffMaxInputLines`](Models/ConfigSettings.cs) を [`InlineDiffMaxDiffLines`](Models/ConfigSettings.cs)（既定値 `1000`）に置き換えました。従来の設定は差分計算の*前*に入力ファイルの行数を閾値と比較していましたが、インライン差分の HTML 表示は変更行のみを出力するため、入力行数は適切な指標ではありませんでした。新しい設定は `TextDiffer.Compute()` による差分計算の*後*に差分出力行数を確認し、閾値を超えた場合にインライン差分の表示をスキップします。[`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs)・[`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs)・[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs)・[README.md](README.md)・[開発者ガイド](doc/DEVELOPER_GUIDE.md)・[テストガイド](doc/TESTING_GUIDE.md) を日英両言語で更新しました。

#### 修正

- ILMismatch のインライン差分が HTML レポートに一切表示されなかった問題を修正しました: [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) が `ILCache.TryGetILAsync` を正規化済みラベル（例: `ildasm (version: 1.0.0)`）で呼び出していましたが、書き込み時のラベル（例: `ildasm MyAssembly.dll (version: 1.0.0)`）と一致しないため常に `null` が返り、インライン差分がサイレントにスキップされていました。[`ILTextOutputService`](Services/ILOutput/ILTextOutputService.cs) が `Reports/<label>/IL/old` と `Reports/<label>/IL/new` に書き出した `*_IL.txt` ファイルを直接読み込む方式に変更し、[`ShouldOutputILText`](Models/ConfigSettings.cs) が `true`（既定値）のときに正しくインライン差分を表示するよう修正しました。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) にテスト `GenerateDiffReportHtml_ILMismatch_WithILTextFiles_ShowsInlineDiff` を追加しました。

- **Unchanged ファイルに対して更新日時逆転警告が出ていた問題を修正しました。** [`FolderDiffService`](Services/FolderDiffService.cs) が `RecordNewFileTimestampOlderThanOldWarningIfNeeded` を `Modified` 判定（`FilesAreEqualAsync` が `false` を返した後）のみで呼び出すよう変更しました。従来はコンテンツ比較の前にチェックしていたため、`new` 側の更新日時が古くても内容が同一の Unchanged ファイルが誤って `Warnings` セクションに出力されていました。[`ReportGenerateService`](Services/ReportGenerateService.cs)・[`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs)・[`ProgramRunner`](ProgramRunner.cs) の警告文を「**modified** files」と明記するよう更新しました。[`FileDiffResultLists`](Models/FileDiffResultLists.cs) および [`FileTimestampRegressionWarning`](Models/FileTimestampRegressionWarning.cs) の XML ドキュメントコメントも「Modified と判定されたファイル」に修正しました。[`FolderDiffServiceTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs) では、`ExecuteFolderDiffAsync_WhenNewFileTimestampIsOlder_RecordsWarning` を内容が異なるファイル（Modified に分類される）で書き直し `ExecuteFolderDiffAsync_WhenModifiedFileTimestampIsOlder_RecordsWarning` に改名、Unchanged ファイルで警告が出ないことを確認する `ExecuteFolderDiffAsync_WhenUnchangedFileTimestampIsOlder_DoesNotRecordWarning` を新規追加、`ExecuteFolderDiffAsync_WhenTimestampWarningDisabled_DoesNotRecordWarning` も内容が異なるファイルを使用するよう更新しました。[`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) と [`ProgramTests`](FolderDiffIL4DotNet.Tests/ProgramTests.cs) の文言アサーションも新しいメッセージに合わせて更新しました。[README.md](README.md)・[開発者ガイド](doc/DEVELOPER_GUIDE.md)・[テストガイド](doc/TESTING_GUIDE.md) を日英両言語で更新しました。

- [`config.json`](config.json) の解析エラー出力を改善しました。[`ConfigService`](Services/ConfigService.cs) が `JsonException` をキャッチした際、内部の例外から行番号・バイト位置を取得してエラーメッセージに付加し、最後のプロパティや配列要素の後のトレイリングカンマが標準 JSON では許可されないことを示すヒントを表示するようになりました。エラーは実行ログへ書き込まれ、コンソールには赤字で表示され、終了コード `3` で終了します。[`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) にオブジェクト末尾カンマ・配列末尾カンマ・複数行 JSON での行番号検証を行う 3 件のユニットテストを追加しました。
- Windows でバナー文字が `?` になる問題を修正しました。[`Program.cs`](Program.cs) の `Main()` 先頭（出力より前）で [`Console.OutputEncoding`](https://learn.microsoft.com/ja-jp/DOTNET/api/system.console.outputencoding?view=net-8.0) = `Encoding.UTF8` を設定し、Windows がデフォルトで使用する OEM コードページ（CP932/CP437）を上書きするようにしました。Linux / macOS ではコンソールがすでに UTF-8 のためこの変更は影響しません。
- 直近の HTML レポート変更により発生した CI パイプラインのテスト失敗 3 件を修正しました（[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs)）。(1) `GenerateDiffReportHtml_ILMismatch_NoInlineDiff` と `GenerateDiffReportHtml_TextMismatch_EnableInlineDiffFalse_NoDetailsElement` は `DoesNotContain("<details")` を検証していましたが、JS コメントに `<details` リテラルが含まれていたため、当該コメントを書き換えて修正。(2) `GenerateDiffReportHtml_Md5MismatchWarning_AppearsInWarningsSection` は見出しテキストの直前に `<span>` が挿入されたことで完全一致が崩れていたため、2 つの独立した `Contains` 検証に分割して修正。(3) 色定数を `#2d7a2d`/`#b00020` から `#22863a`/`#b31d28`（GitHub diff パレット）に変更したことに伴い、色の検証アサーションを更新しました。
- 編集距離超過スキップ時にインライン差分が `+0 / -0` と表示されてさも差異なしに見える問題を修正しました。`TextDiffer.Compute` が Truncated 1 行のみを返す場合（編集距離 `D` > [`InlineDiffMaxEditDistance`](Models/ConfigSettings.cs) のとき）、[`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) が `<details>` 展開矢印なしのプレーンな `diff-skipped` 行を直接表示するようになりました。これは [`InlineDiffMaxDiffLines`](Models/ConfigSettings.cs) 超過ケースの挙動と一致します。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) のテスト名を `GenerateDiffReportHtml_TextMismatch_EditDistanceTooLarge_ShowsSkippedMessageWithoutExpandArrow` に変更しました。
- `[ + ] Added`・`[ - ] Removed`・`[ * ] Modified`・`[ ! ] Timestamps Regressed` のセクション見出し文字色を復元しました（緑 `#22863a` / 赤 `#b31d28` / 青 `#0051c3`）。`COLOR_ADDED`・`COLOR_REMOVED`・`COLOR_MODIFIED` 定数を再追加しました。[CHANGELOG.md](CHANGELOG.md) と [README.md](README.md)（日英）の「プレーンな黒文字」という記述を削除しました。

#### 変更

- インライン差分の `<summary>` ラベルにファイル行番号プレフィックスを追加しました: `#1 Show diff (+N / -M)` / `#1 Show IL diff (+N / -M)`（従来は `Show diff` / `Show IL diff`）。どのファイルの差分かを、すぐ上の行を見なくても識別できるようになります。
- サンプル [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を現在の本番出力に合わせて更新しました: 8 列レイアウト（Disassembler 列 `col.col-disasm-g` / `td.col-disasm` CSS、`--col-disasm-w: 28em` CSS 変数、リサイズ可能な `Disassembler` ヘッダを追加）; `--col-diff-w` を `20em` から `9em` へ修正; Timestamp 列幅を `22em` から `16em` へ修正; すべての `colspan="7"` を `colspan="8"` へ変更; diff-summary ラベルを `#N Show diff` / `#N Show IL diff` 形式へ更新; Diff Reason セルを分割（例: `ILMismatch` + [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) ` (version: 0.12.2)` → `ILMismatch` + Disassembler セル）; JavaScript `colVarNames`・`clearAll` 配列と `syncTableWidths` 計算式を更新。編集距離超過スキップ（`src/BigSchema.cs`）と差分行数制限超過（`src/LargeConfig.xml`）のサンプル行を 2 件追加しました。[`doc/samples/diff_report.md`](doc/samples/diff_report.md) に対応する 2 件の Modified エントリとファイル件数（Modified 8、Compared 17）を反映しました。
- [`doc/DEVELOPER_GUIDE.md`](doc/DEVELOPER_GUIDE.md)（日英）に「インライン差分スキップの挙動」セクションを追加しました: 編集距離超過 / [`InlineDiffMaxOutputLines`](Models/ConfigSettings.cs) 途中打ち切り / [`InlineDiffMaxDiffLines`](Models/ConfigSettings.cs) 計算後超過の 3 トリガー・条件・HTML 表示の違い（`<details>` あり vs. プレーン行）を説明します。

#### 追加

- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) の HTML レポート UX をさらに改善しました。`<h1>` の表示テキストを「Folder Diff Report」に変更。frosted-glass コントロールバーの背景透明度を `rgba(255,255,255,0.45)` に上げてより透過感のある外観に。`h1` のフォントサイズを `2.0rem` へ拡大し、`Summary` / `IL Cache Stats` / `Warnings` の各セクション見出しに専用の `h2.section-heading`（`1.55rem`）スタイルを追加してファイルリスト見出しと差別化。`[ ! ] Modified Files — Timestamps Regressed` を `h3` から `h2` へ昇格させ `[ * ] Modified Files` と同一スタイルに。stat-table の数値フォントをボディフォントに変更（等幅フォント解除）。stat-table に左マージン `1.2em` を追加してインデント表示。ILMismatch のインライン差分サマリーラベルを「Show IL diff」に変更（TextMismatch は「Show diff」のまま）。diff サマリーの `+N` / `-N` をそれぞれ緑・赤の `diff-added-cnt` / `diff-removed-cnt` スパンで色付け。diff-row の `<tr>`（`<details>` を含む折り畳み行）に薄い青背景（`#eef5ff`）を適用。IL 無視文字列注記で値を `<code>` タグで囲むのをやめ、プレーンテキストで表示。Warnings リスト項目の `WARNING:` テキストを削除し、黄色の `⚠` アイコン（`warn-icon`）のみ表示に変更。OK Reason / Notes / File Path の列幅を CSS カスタムプロパティ（`--col-reason-w`、`--col-notes-w`、`--col-path-w`）と `<colgroup>` で管理し、全テーブル間で列幅を同期。これら 3 列のヘッダにドラッグ可能なリサイズハンドル（`initColResize` JS）を追加し、1 つのヘッダをドラッグするだけですべてのテーブルの同一列が同時にリサイズされる仕組みを実装。レビュー済み HTML ダウンロードでは、コントロールバーを削除するのではなく緑色の「🔒 Reviewed — read-only」バナー（`reviewed-banner`）に置き換え、レビュー済みスナップショットであることを視覚的に明示するよう変更。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) の Warnings セクション検証を新しい `h2.section-heading` 構造に合わせて更新しました。
- [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) を全面的に刷新し、HTML レポートに多数の改善を施しました。ページタイトルを `diff_report` 固定に変更。コントロールバーをビューポート全幅にフィットするスティッキー frosted-glass スタイル（`backdrop-filter: blur`）に刷新。ボタンを Apple ミニマリスト風のピルボタン（同一の高さ、`display:inline-flex`、`border-radius:980px`）に変更。自動保存タイムスタンプを `YYYY-MM-DD HH:mm:ss` 形式に統一。Old/New フォルダパスをプレーンテキスト（`<code>` 不使用）で表示。MVID 注記を通常の `<li>` メタ項目として表示。IL contains-ignore 注記を HTML ヘッダに追加。Legend を `<ul class="meta">` 内のネストリストへ移動。5 種のファイルテーブルすべてを統一 8 列レイアウト（`# | ✓ | OK Reason | Notes | File Path | Timestamp | Diff Reason | Disassembler`）にそろえ、行番号・セルプレースホルダなし・OK Reason/Notes/Path/Disassembler 列をリサイズ可能に。Added/Removed/Modified テーブルヘッダはそれぞれ淡い緑・赤・青の背景（黒文字）に。Ignored/Unchanged はニュートラルなヘッダに。Summary と IL Cache Stats を `<table class="stat-table">` の右揃え数値テーブルで表示。Warnings セクションのタイムスタンプ逆転ファイルを `[ ! ] Modified Files — Timestamps Regressed (N)` 見出し付きのテーブルとして表示。インライン差分の変更点: [`InlineDiffContextLines`](Models/ConfigSettings.cs) の既定値を 3 から 0 へ変更（差分行のみ表示、前後コンテキストなし）。行省略時にハンクセパレーター行を表示。ILMismatch エントリについて、[`ShouldOutputILText`](Models/ConfigSettings.cs) が `true`（既定値）のとき `Reports/<label>/IL/old` と `Reports/<label>/IL/new` に書き出された `*_IL.txt` ファイルが存在する場合はインライン差分を表示。レビュー済み HTML ダウンロードの改善: 出力ファイル名を `diff_report_{yyyyMMdd}_reviewed.html` に変更。ページタイトルを `diff_report_{yyyyMMdd}_reviewed` に変更。コントロールバー（`<!--CTRL-->…<!--/CTRL-->`）をダウンロード版から削除。レビュー済みコピーでは全チェックボックスを `disabled`、テキスト入力を `readOnly`（テキスト選択・コピーは可能）に設定。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) と [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) を新しい色値（`#2d7a2d`、`#b00020`）・stat-table HTML 構造・[`InlineDiffContextLines`](Models/ConfigSettings.cs) 既定値 `0` に合わせて更新しました。
- [`diff_report.md`](doc/samples/diff_report.md) のセクション見出しを改善しました。[`ReportGenerateService`](Services/ReportGenerateService.cs) の各 section writer がファイル件数を見出し末尾に付与するようになりました（例: `## [ x ] Ignored Files (3)`、`## [ + ] Added Files (1)`）。また `IgnoredFilesSectionWriter` の表示パスを変更しました: `old` のみ、または `new` のみに存在する無視ファイルは絶対パス表示（例: `/path/to/old/rel/file.pdb`）、両側に存在するファイルは引き続き相対パス表示になります。

- [`ConfigSettings`](Models/ConfigSettings.cs) に `ShouldGenerateHtmlReport`（既定値 `true`）を追加しました。`true` のとき、各実行で `diff_report.md` と同じ `Reports/<label>/` ディレクトリに **`diff_report.html`** も生成されます。HTML ファイルはサーバや拡張機能不要のスタンドアロン自己完結型レビュードキュメントです。Ignored / Unchanged / Added / Removed / Modified の全ファイルエントリを 8 列テーブルで表示し、Removed / Added / Modified 行にはインタラクティブなチェックボックス・OK 理由テキスト入力・備考テキスト入力を備え、プロダクトリリースレビュー時のサインオフをブラウザ上で完結できます。Added / Removed / Modified の列ヘッダはそれぞれ緑・赤・青の背景色で色付けされ、セクション見出しも同様に緑・赤・青の文字色で表示されます。ファイルには `folderdiff-<label>` キーによる localStorage 自動保存と、現在のレビュー状態を新しいポータブルスナップショットファイルへ書き出す **「Download reviewed version」** ボタンの JavaScript が埋め込まれています。無効化するには [`config.json`](config.json) で `"ShouldGenerateHtmlReport": false` を設定します。新規 [`HtmlReportGenerateService`](Services/HtmlReportGenerateService.cs) として実装し、[`RunScopeBuilder`](Runner/RunScopeBuilder.cs) に登録、[`ProgramRunner.GenerateReport()`](ProgramRunner.cs) から呼び出します。[`HtmlReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs) に 12 件のユニットテストを追加しました。サンプルファイル [`doc/samples/diff_report.md`](doc/samples/diff_report.md) と [`doc/samples/diff_report.html`](doc/samples/diff_report.html) を追加し、[README.md](README.md) を外部サンプルへのリンク方式にリファクタリングするとともに、レビューワークフローを説明する日英バイリンガルの `Interactive HTML Review Report` / `インタラクティブ HTML レビューレポート` セクションを追加しました。[開発者ガイド](doc/DEVELOPER_GUIDE.md) と [テストガイド](doc/TESTING_GUIDE.md) を更新しました。

- [`ConfigSettings`](Models/ConfigSettings.cs) に [`DisassemblerBlacklistTtlMinutes`](Models/ConfigSettings.cs)（既定値 `10`）を追加しました。このプロパティは、`DISASSEMBLE_FAIL_THRESHOLD`（3 回）以上連続失敗した逆アセンブラツールのブラックリスト有効期間（分）を制御します。従来は 10 分固定でしたが、[`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) が起動時に設定値を読み込むようになりました。[`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) に既定値と JSON ラウンドトリップを検証するテストを追加しました。
- [`DisassemblerBlacklist`](Services/DisassemblerBlacklist.cs) を [`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) から独立したクラスとして抽出しました。[`ConcurrentDictionary`](https://learn.microsoft.com/ja-jp/dotnet/api/system.collections.concurrent.concurrentdictionary-2?view=net-8.0)、TTL、失敗しきい値ロジックをカプセル化し、テスト専用ヘルパー `InjectEntry` / `ContainsEntry` を追加しました。[`DotNetDisassembleServiceTests`](FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) はインスタンスレベルのリフレクション（`_blacklist`）を使うように更新しました。新規 [`DisassemblerBlacklistTests`](FolderDiffIL4DotNet.Tests/Services/DisassemblerBlacklistTests.cs) は、しきい値境界、TTL 期限切れ、`Clear`、`ResetFailure`、null 安全性、並列アクセス 2 シナリオ（B-4）をカバーします。
- [`IReportSectionWriter`](Services/IReportSectionWriter.cs) インターフェイスと [`ReportWriteContext`](Services/ReportWriteContext.cs) コンテキストクラスを導入しました。[`ReportGenerateService`](Services/ReportGenerateService.cs) は `_sectionWriters` 静的リストに 11 個のプライベートネストクラス実装（`HeaderSectionWriter`、`LegendSectionWriter`、`IgnoredFilesSectionWriter`、`UnchangedFilesSectionWriter`、`AddedFilesSectionWriter`、`RemovedFilesSectionWriter`、`ModifiedFilesSectionWriter`、`SummarySectionWriter`、`AssemblySemanticChangesSectionWriter`、`ILCacheStatsSectionWriter`、`WarningsSectionWriter`）を持ち、`WriteReportSections` でそれらを順に呼び出します。各セクションはサービス全体を必要とせず `ReportWriteContext` を構築するだけで単独テストできます。
- [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) に Unicode ファイル名テストを追加しました: `GenerateDiffReport_UnicodeFileNames_AreIncludedInReport` と `GenerateDiffReport_UnicodeFileNames_InUnchangedSection` は、日本語・ウムラウト付きラテン文字・中国語の相対パスが Markdown レポートにそのまま含まれることを検証します（B-2）。
- [`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) に大件数ファイルのサマリースナップショットテスト `GenerateDiffReport_LargeFileCount_SummaryStatisticsAreCorrect` を追加しました: 10 500 件の Unchanged ファイルを投入し、Summary セクションの `Unchanged` および `Compared` カウントが投入件数と一致することを検証します（B-3）。

#### 変更

- [`FileDiffService`](Services/FileDiffService.cs) に散在していた並列テキスト差分フォールバック用 `catch` ブロック 4 件（[`ArgumentOutOfRangeException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.argumentoutofrangeexception?view=net-8.0)、[`IOException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.ioexception?view=net-8.0)、[`UnauthorizedAccessException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.unauthorizedaccessexception?view=net-8.0)、[`NotSupportedException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.notsupportedexception?view=net-8.0)）を `catch (Exception ex) when (ex is … or …)` 形式の 1 件へ統合し、重複するフォールバック処理を排除しました。
- [`FileDiffService`](Services/FileDiffService.cs) の IL 差分失敗ログを改善しました: エラーメッセージに `ex.Message`（逆アセンブラコマンドと内部原因を含む）を追記し、スタックトレースを参照しなくてもログ行単独で原因が分かるようになりました。
- 従来コメントのなかったマジック定数に理由コメントを追記しました: [`FolderDiffService`](Services/FolderDiffService.cs) の `KEEP_ALIVE_INTERVAL_SECONDS` = `5`（CI/SSH タイムアウト余裕値）と `LARGE_DISCOVERY_FILE_COUNT_LOG_THRESHOLD` = `10000`（列挙フェーズのパフォーマンス指標）、[`FolderDiffExecutionStrategy`](Services/FolderDiffExecutionStrategy.cs) の `MAX_PARALLEL_NETWORK_LIMIT` = `8`（NAS/SMB サーバの接続上限実測値）、[`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) の `DISASSEMBLE_FAIL_THRESHOLD` = `3` と `DEFAULT_BLACKLIST_TTL_MINUTES` = `10`。

#### 追加

- [`FileDiffResultLists`](Models/FileDiffResultLists.cs) に `DiffSummaryStatistics` レコードと `SummaryStatistics` 計算プロパティを追加しました。このプロパティは `DiffSummaryStatistics(AddedCount, RemovedCount, ModifiedCount, UnchangedCount, IgnoredCount)` として 5 つのカウントをまとめて返し、呼び出し側が 5 つの並行コレクションを個別に参照する必要をなくします。あわせて [`ReportGenerateService.WriteSummarySection()`](Services/ReportGenerateService.cs) を `SummaryStatistics` プロパティを使うように更新し、キュー/辞書への個別 `.Count` 呼び出しを削減しました。[`FileDiffResultListsTests`](FolderDiffIL4DotNet.Tests/Models/FileDiffResultListsTests.cs) にユニットテスト 4 件を追加しました。
- [`ConfigSettings`](Models/ConfigSettings.cs) に [`SpinnerFrames`](Models/ConfigSettings.cs)（`List<string>`）を追加しました。各要素がスピナーの 1 フレームとなり、デフォルトの `| / - \` 4 フレームローテーションをブロック文字や絵文字など複数文字を含む任意の文字列シーケンスに置き換えられます。[`ConsoleSpinner`](FolderDiffIL4DotNet.Core/Console/ConsoleSpinner.cs) の内部フレーム配列を `char[]` から `string[]` に変更して複数文字フレームに対応しました。[`ProgressReportService`](Services/ProgressReportService.cs) と [`ReportGenerateService`](Services/ReportGenerateService.cs) のコンストラクタに `ConfigSettings` パラメータを追加し、起動時に設定済みフレームを読み込めるようにしました。バリデーションは 1 件以上のフレームを必須とします。日英 [README.md](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md) を更新しました。

#### 変更

- `"// MVID:"` リテラルの重複定義を解消し、[`Constants.IL_MVID_LINE_PREFIX`](Common/Constants.cs) に一元化しました。[`ReportGenerateService`](Services/ReportGenerateService.cs) と [`ILOutputService`](Services/ILOutputService.cs) の両ファイルに存在していた `private const string MVID_PREFIX` を削除し、各参照箇所を [`Constants.IL_MVID_LINE_PREFIX`](Common/Constants.cs) に置き換えました。文字列値は同一のため動作変更はありません。
- [`diff_report.md`](doc/samples/diff_report.md) のタイムスタンプ表示を改善しました。フォーマットを `yyyy-MM-dd HH:mm:ss.fff zzz`（エントリごとにミリ秒＋タイムゾーンオフセット）から `yyyy-MM-dd HH:mm:ss`（秒精度）に変更し、タイムゾーンオフセットは [`ShouldOutputFileTimestamps`](Models/ConfigSettings.cs) が `true` の場合にレポートヘッダで `Timestamps (timezone): +09:00` として一括表示するようにしました。各エントリの表示は以前の `<u>(updated_old: ..., updated_new: ...)</u>` 形式からブラケット＋矢印形式（新旧両方: `[old → new]`、単一: `[timestamp]`）に統一しました。`Warnings` セクションも同様にブラケット＋矢印形式に統一しました。Unchanged ファイルについては、判定結果（`MD5Match` / `TextMatch` / `ILMatch`）によらず old と new の更新日時が異なる場合に新旧両方を表示するよう修正しました（従来は `ILMatch` のみ両方表示）。日英 [README.md](README.md) および関連テストを更新しました。

#### 追加

- [`FolderDiffIL4DotNet.Tests`](FolderDiffIL4DotNet.Tests/) の未カバーシナリオ 4 件を補完しました: (1) [`FileSystemUtilityTests`](FolderDiffIL4DotNet.Tests/Core/IO/FileSystemUtilityTests.cs) に `IsLikelyWindowsNetworkPath_ForwardSlashIpUncPath_ReturnsTrue` を追加し、[`FileSystemUtility.IsLikelyWindowsNetworkPath()`](FolderDiffIL4DotNet.Core/IO/FileSystemUtility.cs) が `//` 形式の IP ベース UNC パス（例: `//192.168.1.1/share`）も Windows ネットワークパスとして検出するよう修正しました; (2) [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) に `ExecuteFolderDiffAsync_WhenEnumeratingFilesThrowsIOExceptionDueToSymlinkLoop_LogsAndRethrows` を追加し、ディレクトリ列挙中に発生した `IOException`（シンボリックリンクループによる `ELOOP` エラーなど）がエラーログとともに再スローされることを検証します; (3) [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) に `ExecuteFolderDiffAsync_WhenNewFileDeletedBeforeComparison_ClassifiesAsRemovedWithWarning`（逐次・並列の両バリアント）を追加し、[`FolderDiffService`](Services/FolderDiffService.cs) がファイル比較中に [`FileNotFoundException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.filenotfoundexception?view=net-8.0) をキャッチした場合、例外を伝播させずに警告を記録して当該ファイルを Removed に分類するよう変更しました; (4) [`DotNetDisassembleServiceTests`](FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) に `DisassembleAsync_AfterBlacklistTtlExpiry_RetriesToolAndSucceeds` を追加し、10 分間のブラックリスト TTL が満了した逆アセンブラツールがブラックリストから削除されて次回呼び出しで再試行されることを検証します。
- [`ProgramRunner.ValidateRunDirectories()`](ProgramRunner.cs) に 3 つのプリフライトチェックを追加しました。いずれも設定読み込み前に実行され、失敗時は終了コード `2` を返します: (1) **パス長チェック** — 構築した `Reports/<label>` パスが OS の上限（Windows 標準 260 文字、macOS 1024 文字、Linux 4096 文字）を超えていないことを [`PathValidator.ValidateAbsolutePathLengthOrThrow()`](FolderDiffIL4DotNet.Core/IO/PathValidator.cs) で検証します; (2) **ディスク空き容量チェック** — `DriveInfo` を使ってレポートドライブに 100 MB 以上の空き容量があることを確認します（ドライブ情報を取得できない場合は best-effort でスキップ）; (3) **書き込み権限チェック** — `Reports/` 親ディレクトリに一時プローブファイルを作成・削除し、出力前に書き込み権限を確認します。あわせて `TryValidateAndBuildRunArguments` に [`IOException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.ioexception?view=net-8.0) と [`UnauthorizedAccessException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.unauthorizedaccessexception?view=net-8.0) の catch を追加し、3 つの失敗すべてが終了コード `2` に対応するようにしました。[`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) にユニット/統合テスト 3 件を追加し、[README.md](README.md) を更新しました。
- [`ConfigSettings`](Models/ConfigSettings.cs) に [`ShouldIncludeILCacheStatsInReport`](Models/ConfigSettings.cs)（既定値 `false`）を追加しました。`true` に設定し IL キャッシュが有効な場合、[`ReportGenerateService`](Services/ReportGenerateService.cs) は [`diff_report.md`](doc/samples/diff_report.md) の `Summary` と `Warnings` の間に `IL Cache Stats` セクションを追記します（ヒット数・ミス数・ヒット率・保存数・退避数・期限切れ数）。あわせて [`ILCache`](Services/Caching/ILCache.cs) にミス数追跡フィールド `_internalMisses`（完全なキャッシュミスの際にインクリメント）と `GetReportStats()` メソッド、`ILCacheReportStats` レコードを追加しました。[`ReportGenerateServiceTests`](FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs) に 3 件のユニットテストを追加し、[`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs)、[README.md](README.md)、[CHANGELOG.md](CHANGELOG.md) を更新しました。
- CLI オプションを拡充しました。`--help`/`-h` は使い方を表示してロガー初期化前にコード `0` で終了します。`--version` はアプリバージョンを表示してコード `0` で終了します。`--config <path>` はデフォルトの `<exe>/config.json` に代わり任意のパスから設定ファイルを読み込みます。`--threads <N>` は今回の実行に限り [`ConfigSettings`](Models/ConfigSettings.cs) の [`MaxParallelism`](Models/ConfigSettings.cs) を上書きします。`--no-il-cache` は今回の実行に限り [`EnableILCache`](Models/ConfigSettings.cs) = `false` に設定します。`--skip-il` は .NET アセンブリの IL 逆アセンブルと IL 差分比較をまるごとスキップします（[`ConfigSettings`](Models/ConfigSettings.cs) に新設した [`SkipIL`](Models/ConfigSettings.cs) プロパティとして保持され、[`FileDiffService`](Services/FileDiffService.cs) でも参照します）。`--no-timestamp-warnings` はタイムスタンプ逆転の警告を抑制します。未知のフラグを指定した場合は、これまで黙ってスルーされていた挙動を改め、説明付きで終了コード `2` を返します。[`ConfigService.LoadConfigAsync()`](Services/ConfigService.cs) にオプショナルな `configFilePath` パラメータを追加しました。[`CliOptionsTests`](FolderDiffIL4DotNet.Tests/CliOptionsTests.cs) にパーサー単体テスト 21 件を追加し、[`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) と [`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) にも統合テストを追加しました。
- [`ConfigSettings.Validate()`](Models/ConfigSettings.cs) と `ConfigValidationResult` クラスを追加しました。[`ConfigService.LoadConfigAsync()`](Services/ConfigService.cs) はデシリアライズ直後に `Validate()` を呼び出し、バリデーションが失敗した場合は全エラーを列挙した [`InvalidDataException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.invaliddataexception?view=net-8.0) をスローします。これにより、設定不正な実行は後から無言で失敗したり未定義の振る舞いを引き起こしたりする代わりに、起動時に分かりやすいエラーメッセージとして検出されます。検証対象の制約: [`MaxLogGenerations`](Models/ConfigSettings.cs) >= `1`、[`TextDiffParallelThresholdKilobytes`](Models/ConfigSettings.cs) >= `1`、[`TextDiffChunkSizeKilobytes`](Models/ConfigSettings.cs) >= `1`、[`TextDiffChunkSizeKilobytes`](Models/ConfigSettings.cs) < [`TextDiffParallelThresholdKilobytes`](Models/ConfigSettings.cs)。あわせて [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) にバリデーション単体テスト（7 件）、[`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) にバリデーション統合テスト（5 件）を追加しました。

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

- [`IgnoredExtensions`](Models/ConfigSettings.cs) を大文字小文字を無視して評価するようにしました。

#### 削除

- 未使用だった `ShouldSkipPromptOnExit` 設定を削除しました。

#### 修正

- [`TextFileExtensions`](Models/ConfigSettings.cs) の設定値誤りを是正しました。
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

[Unreleased]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.6.0...HEAD
[1.6.0]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.5.0...v1.6.0
[1.5.0]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.4.1...v1.5.0
[1.4.1]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.3.0...v1.4.0
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
