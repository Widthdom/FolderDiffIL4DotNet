# Developer Guide

This guide is for maintainers who need to change runtime behavior, extend the diff pipeline, or keep CI and tests aligned with implementation changes.

Related documents:
- [README.md](../README.md#readme-en-doc-map): product overview, installation, usage, and configuration reference
- [doc/TESTING_GUIDE.md](TESTING_GUIDE.md#testing-en-run-tests): test strategy, local commands, and isolation rules
- [api/index.md](../api/index.md): generated API reference landing page
- [docfx.json](../docfx.json): DocFX metadata/build configuration
- [.github/workflows/dotnet.yml](../.github/workflows/dotnet.yml): CI pipeline definition
- [SECURITY.md](../SECURITY.md): threat model, STRIDE analysis, and security mitigations
- [doc/PERFORMANCE_GUIDE.md](PERFORMANCE_GUIDE.md#perf-en-memory): memory management, benchmark baselines, and tuning recommendations

<a id="guide-en-map"></a>
## Document Map

| If you need to... | Start here |
| --- | --- |
| Understand the end-to-end execution flow | [Execution Lifecycle](#guide-en-execution-lifecycle) |
| Trace service boundaries and DI scopes | [Dependency Injection Layout](#guide-en-di-layout) |
| Change file classification behavior | [Comparison Pipeline](#guide-en-comparison-pipeline) |
| Understand or adjust configuration keys and runtime mode | [Configuration and Runtime Modes](#guide-en-config-runtime) |
| Tune performance or network-share behavior | [Performance and Runtime Modes](#guide-en-performance-runtime) |
| Refresh the generated API reference site | [Documentation Site and API Reference](#guide-en-doc-site) |
| Update build, test, or artifact behavior | [CI and Release Notes](#guide-en-ci-release) |
| Safely extend the codebase | [Change Checklist](#guide-en-change-checklist) |

## Local Development

Prerequisites:
- [`.NET SDK 8.x`](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) pinned via [`global.json`](../global.json)
- One IL disassembler available on `PATH`
  - [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) or [`dotnet ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) preferred
  - [`ilspycmd`](https://www.nuget.org/packages/ilspycmd/) supported as fallback

Common commands:

```bash
dotnet restore FolderDiffIL4DotNet.sln
dotnet build FolderDiffIL4DotNet.sln --configuration Release
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false
npm run test:js   # Jest unit tests for diff_report.js / diff_report.js の Jest ユニットテスト
```

Refresh the documentation site locally:

```bash
dotnet tool update --global docfx --version '2.*'
export PATH="$PATH:$HOME/.dotnet/tools"
docfx metadata docfx.json
docfx build docfx.json
```

Debugging a local run:

```bash
dotnet run -- "/absolute/path/to/old" "/absolute/path/to/new" "dev-run" --no-pause

# Quick help / version check
dotnet run -- --help
dotnet run -- --version

# Print effective config (including env var overrides)
dotnet run -- --print-config
dotnet run -- --print-config --config /etc/cfg.json

# Override threads, skip IL, use custom config
dotnet run -- "/path/old" "/path/new" "label" --threads 4 --skip-il --config /etc/cfg.json --no-pause
```

Generated during a run — see [README § Generated Artifacts](../README.md#readme-en-generated-artifacts) for the full list. In addition to those report files, an `ILCache/` directory is created under the OS-standard user-local data directory (`%LOCALAPPDATA%\FolderDiffIL4DotNet\ILCache` on Windows, `~/.local/share/FolderDiffIL4DotNet/ILCache` on macOS/Linux) when [`EnableILCache`](../Models/ConfigSettings.cs) is `true` and [`ILCacheDirectoryAbsolutePath`](../Models/ConfigSettings.cs) is not configured.

## Partial Class File Layout

Large service classes are split into partial class files to keep each file focused. The class name and namespace are unchanged — only the file layout differs:

| Class | Main file | Partial files |
| --- | --- | --- |
| `ProgramRunner` | [`ProgramRunner.cs`](../ProgramRunner.cs) | [`Runner/ProgramRunner.Types.cs`](../Runner/ProgramRunner.Types.cs) (nested types: `RunArguments`, `RunCompletionState`, `ProgramExitCode`, `ProgramRunResult`, `StepResult<T>`), [`Runner/ProgramRunner.HelpText.cs`](../Runner/ProgramRunner.HelpText.cs) (CLI help message), [`Runner/ProgramRunner.Config.cs`](../Runner/ProgramRunner.Config.cs) (config loading, validation, CLI overrides) |
| `ConfigSettings` | [`Models/ConfigSettings.cs`](../Models/ConfigSettings.cs) | [`Models/ConfigSettings.ReportSettings.cs`](../Models/ConfigSettings.ReportSettings.cs) (report output control), [`Models/ConfigSettings.ILSettings.cs`](../Models/ConfigSettings.ILSettings.cs) (IL comparison, cache, disassembler), [`Models/ConfigSettings.DiffSettings.cs`](../Models/ConfigSettings.DiffSettings.cs) (parallelism, network, inline diff) |
| `HtmlReportGenerateService` | [`Services/HtmlReportGenerateService.cs`](../Services/HtmlReportGenerateService.cs) | [`Services/HtmlReport/HtmlReportGenerateService.Sections.cs`](../Services/HtmlReport/HtmlReportGenerateService.Sections.cs) (report section builders), [`…DetailRows.cs`](../Services/HtmlReport/HtmlReportGenerateService.DetailRows.cs) (inline diff, semantic changes, dependency changes detail rows), [`…Helpers.cs`](../Services/HtmlReport/HtmlReportGenerateService.Helpers.cs), [`…Css.cs`](../Services/HtmlReport/HtmlReportGenerateService.Css.cs) (loads [`diff_report.css`](../Services/HtmlReport/diff_report.css) embedded resource), [`…Js.cs`](../Services/HtmlReport/HtmlReportGenerateService.Js.cs) (loads and concatenates 9 JS modules from [`Services/HtmlReport/js/`](../Services/HtmlReport/js/) with `{{STORAGE_KEY}}`/`{{REPORT_DATE}}` placeholders) |
| `FolderDiffService` | [`Services/FolderDiffService.cs`](../Services/FolderDiffService.cs) | [`Services/FolderDiffService.ILPrecompute.cs`](../Services/FolderDiffService.ILPrecompute.cs), [`…DiffClassification.cs`](../Services/FolderDiffService.DiffClassification.cs) |
| `ReportGenerateService` | [`Services/ReportGenerateService.cs`](../Services/ReportGenerateService.cs) | [`Services/SectionWriters/HeaderSectionWriter.cs`](../Services/SectionWriters/HeaderSectionWriter.cs), [`…LegendSectionWriter.cs`](../Services/SectionWriters/LegendSectionWriter.cs), [`…IgnoredFilesSectionWriter.cs`](../Services/SectionWriters/IgnoredFilesSectionWriter.cs), [`…UnchangedFilesSectionWriter.cs`](../Services/SectionWriters/UnchangedFilesSectionWriter.cs), [`…AddedFilesSectionWriter.cs`](../Services/SectionWriters/AddedFilesSectionWriter.cs), [`…RemovedFilesSectionWriter.cs`](../Services/SectionWriters/RemovedFilesSectionWriter.cs), [`…ModifiedFilesSectionWriter.cs`](../Services/SectionWriters/ModifiedFilesSectionWriter.cs), [`…SummarySectionWriter.cs`](../Services/SectionWriters/SummarySectionWriter.cs), [`…ILCacheStatsSectionWriter.cs`](../Services/SectionWriters/ILCacheStatsSectionWriter.cs), [`…WarningsSectionWriter.cs`](../Services/SectionWriters/WarningsSectionWriter.cs) |
| `AssemblyMethodAnalyzer` | [`Services/AssemblyMethodAnalyzer.cs`](../Services/AssemblyMethodAnalyzer.cs) | [`Services/AssemblyMethodAnalyzer.Comparers.cs`](../Services/AssemblyMethodAnalyzer.Comparers.cs) (type/method/property/field comparison), [`…MetadataHelpers.cs`](../Services/AssemblyMethodAnalyzer.MetadataHelpers.cs) (snapshot construction, signature building), [`…AccessHelpers.cs`](../Services/AssemblyMethodAnalyzer.AccessHelpers.cs) (access/modifier extraction, type kind detection, IL byte reading), [`…SignatureProvider.cs`](../Services/AssemblyMethodAnalyzer.SignatureProvider.cs) (generic context, signature type provider) |
| `DepsJsonAnalyzer` | [`Services/DepsJsonAnalyzer.cs`](../Services/DepsJsonAnalyzer.cs) | (single file) Structured dependency change analysis for `.deps.json` files |
| `NuGetVulnerabilityService` | [`Services/NuGetVulnerabilityService.cs`](../Services/NuGetVulnerabilityService.cs) | (single file) Fetches NuGet V3 vulnerability data and checks package versions against known advisories |
| `NuGetVersionRange` | [`Services/NuGetVersionRange.cs`](../Services/NuGetVersionRange.cs) | (single file) NuGet version range interval notation parser |
| `FileDiffService` | [`Services/FileDiffService.cs`](../Services/FileDiffService.cs) | [`Services/FileDiffService.TextComparison.cs`](../Services/FileDiffService.TextComparison.cs) (sequential/chunk-parallel text comparison, memory-budget-aware parallelism) |
| `DotNetDisassembleService` | [`Services/DotNetDisassembleService.cs`](../Services/DotNetDisassembleService.cs) | [`Services/DotNetDisassembleService.VersionLabel.cs`](../Services/DotNetDisassembleService.VersionLabel.cs) (version/label management, tool fingerprinting, process execution, usage recording), [`Services/DotNetDisassembleService.Streaming.cs`](../Services/DotNetDisassembleService.Streaming.cs) (line-based streaming disassembly, avoids LOH string allocations) |

## Nullable Reference Types

Both [`FolderDiffIL4DotNet.csproj`](../FolderDiffIL4DotNet.csproj) and [`FolderDiffIL4DotNet.Core.csproj`](../FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj) enable `<Nullable>enable</Nullable>` with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. All nullable warnings (CS8600–8604, CS8618, CS8625) are enforced — there are no suppressions.

### Annotation conventions

| Pattern | When to use | Example |
| --- | --- | --- |
| `string?` return type | Method can return `null` on miss/failure | `string? TryGetPathRoot(...)` |
| `string? param = null` | Optional parameter that callers may omit | `ValidateFolderNameOrThrow(string folderName, string? paramName = null)` |
| `out string? param` | `out` parameter assigned `null` on failure path | `TryGetFileSystemInfoOnMac(string path, out string? fsType, out uint flags)` |
| `TValue?` property on generic type | Value may be `default` when `IsSuccess` is false | `StepResult<TValue>.Value` |
| `= null!` on `init` properties | Required-at-init properties on context/DTO classes where the compiler cannot verify initialization | `ReportWriteContext.OldFolderAbsolutePath { get; init; } = null!;` |
| `ILCache?` field / parameter | Nullable service injected via DI (null when feature is disabled) | `private readonly ILCache? _ilCache;` |

### Guidelines for new code

- **Always annotate** — do not add new `<NoWarn>` entries for nullable codes. If the compiler warns, fix the annotation or add a null check.
- **Prefer `?` over `null!`** — use `null!` only for `init`-only properties that are guaranteed to be set by the caller's object initializer. For all other cases, use `?` to express nullability honestly.
- **Use `ArgumentNullException.ThrowIfNull()`** for required non-null parameters at public/internal API boundaries.
- **Guard before dereference** — when calling a `Try*` method that returns `T?`, check for `null` before using the result.
- **Test project is excluded** — [`FolderDiffIL4DotNet.Tests.csproj`](../FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj) does not enable `<Nullable>` because test doubles and mock setups would require excessive annotation for little safety benefit.

## Railway-Oriented Execution Pipeline

`ProgramRunner.RunWithResultAsync` uses a railway-oriented pipeline built on the [`StepResult<T>`](../Runner/ProgramRunner.Types.cs) type. Each execution phase returns a `StepResult`, and the pipeline chains them via `Bind` (synchronous) and `BindAsync` (asynchronous). On failure, subsequent steps are short-circuited automatically — no explicit `if (!IsSuccess) return Failure` checks are needed.

```
TryValidateAndBuildRunArguments
  .Bind → TryPrepareReportsDirectory
  .BindAsync → TryLoadConfigBuilderAsync
    .Bind → ApplyCliOverrides + TryBuildConfig
  .BindAsync → TryExecuteRunAsync
```

When adding a new execution phase, wrap the result in `StepResult<T>.FromValue(value)` on success or return `StepResult<T>.FromFailure(ProgramRunResult.Failure(exitCode))` on failure, then chain it with `.Bind()`/`.BindAsync()`.

## XML Documentation Enforcement

Both [`FolderDiffIL4DotNet.csproj`](../FolderDiffIL4DotNet.csproj) and [`FolderDiffIL4DotNet.Core.csproj`](../FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj) enable `<GenerateDocumentationFile>true</GenerateDocumentationFile>` with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. The CS1591 (missing XML comment) and CS1573 (mismatched param tag) suppressions have been removed, so any new public type, method, property, or parameter without a `<summary>` / `<param>` / `<returns>` tag will fail the build. Bilingual XML doc comments (English first, then Japanese) are the project convention.

## HTML Report Security

The generated `diff_report.html` applies two layers of XSS mitigation:

1. **HTML encoding** — All user-supplied data (file paths, timestamps, version strings, disassembler output) is encoded via `System.Net.WebUtility.HtmlEncode` in [`HtmlReportGenerateService.Helpers.cs`](../Services/HtmlReport/HtmlReportGenerateService.Helpers.cs), followed by explicit backtick replacement (`` ` `` → `&#96;`). `WebUtility.HtmlEncode` does not encode backtick characters, so the extra `.Replace()` step prevents template-literal injection in embedded JavaScript contexts.

2. **Content-Security-Policy** — A `<meta http-equiv="Content-Security-Policy">` tag in the `<head>` restricts the execution environment: `default-src 'none'` blocks everything by default; `style-src 'unsafe-inline'` and `script-src 'unsafe-inline'` allow only the report's own inline styles/scripts; `img-src 'self'` allows only same-origin images. This prevents loading of external scripts, stylesheets, fonts, frames, and form targets.

When modifying the HTML report output, ensure that any new dynamic data is passed through `HtmlEncode()` and that the CSP meta tag remains in [`HtmlReportGenerateService.cs`](../Services/HtmlReportGenerateService.cs) `AppendHtmlHead()`. The sample report at [`doc/samples/diff_report.html`](samples/diff_report.html) must also be kept in sync.

## HTML Report Filtering

The HTML report includes a client-side filter zone that allows users to narrow down file rows by multiple criteria. The filter zone is preserved in reviewed HTML so reviewers can also filter.

### Server-side (C#)

- [`AppendFileRow()`](../Services/HtmlReport/HtmlReportGenerateService.Helpers.cs) emits `data-section`, `data-diff`, and (when applicable) `data-importance` attributes on each `<tr>`.
- The **button row** (Download as reviewed, Fold all details, Reset filters, Clear all) is inside `<!--CTRL-->...<!--/CTRL-->` markers in [`HtmlReportGenerateService.cs`](../Services/HtmlReportGenerateService.cs) and is replaced with a reviewed banner by `downloadReviewed()`. The reviewed banner includes Verify integrity, Download as Excel-compatible HTML, Fold all details, and Reset filters buttons.
- The **filter zone** (Diff Detail, Change Importance, Unchecked only, Search) is **outside** the CTRL markers so it persists in reviewed HTML.

### CSS ([`diff_report.css`](../Services/HtmlReport/diff_report.css))

- **CSS custom properties (`:root` variables)** — 60+ colour/surface/border tokens defined in `:root`. All colour references throughout the stylesheet use `var(--color-*)` instead of hardcoded hex values. This enables theming and keeps the palette in a single location.
- **Dark mode** — `@media (prefers-color-scheme: dark)` overrides the `:root` variables with a GitHub-dark-inspired palette (`#0d1117` background, `#e6edf3` text, etc.). The report switches automatically based on the browser/OS colour scheme. A manual toggle button (Light / Dark / System) in the controls bar applies `html[data-theme="light"]` or `html[data-theme="dark"]` attribute selectors that take precedence over `@media`. The preference is stored in localStorage with a per-report key (`{storageKey}-theme`).
- **Utility classes** — Theme-aware classes used by the C# generator instead of inline `style` attributes: `.imp-high`, `.imp-medium` (importance labels), `.status-available`, `.status-unavailable` (disassembler status), `.vuln-new`, `.vuln-resolved` (vulnerability badges), `.vuln-new-count`, `.vuln-resolved-count` (vulnerability counts), `.warn-danger`, `.warn-caution` (warning banners).
- `tr.filter-hidden` / `tr.diff-row.filter-hidden-parent` — hide rows with `display: none !important`.
- `.filter-table-dbl` — Change Importance table class; its `tbody td` height is set to `calc(var(--ft-row-h) * 2)` for row alignment with Diff Detail.
- `.filter-search-wrap` — wrapper for the search input with visible border and clear button.

### JavaScript ([`Services/HtmlReport/js/`](../Services/HtmlReport/js/))

The JavaScript is split into 9 module files under `Services/HtmlReport/js/`, concatenated at generation time by `HtmlReportGenerateService.Js.cs`:

| Module | Responsibility |
| --- | --- |
| `diff_report_state.js` | Constants, template placeholders, `formatTs`, `readSavedStateFromStorage`, `collectState`, `autoSave`, `updateProgress` |
| `diff_report_export.js` | `downloadReviewed`, `verifyIntegrity`, `collapseAll`, `clearAll` |
| `diff_report_diffview.js` | `decodeDiffHtml`, `toggleDiffView` (side-by-side diff) |
| `diff_report_lazy.js` | `setupLazyDiff`, `setupLazySection`, `forceDecodeLazySections` |
| `diff_report_layout.js` | `syncTableWidths`, `syncScTableWidths`, `initColResizeSingle`, `syncFilterRowHeight`, `wrapInputWithClear`, `initClearButtons`, `initColResize` |
| `diff_report_filter.js` | `applyFilters`, `resetFilters`, `copyPath` |
| `diff_report_excel.js` | `downloadAsPdf`, `downloadExcelCompatibleHtml`, `buildExcelRow`, `esc` |
| `diff_report_theme.js` | `initTheme`, `cycleTheme`, `applyTheme`, `getStoredTheme` (Light/Dark/System toggle) |
| `diff_report_init.js` | `DOMContentLoaded` handler, keyboard navigation |

- `applyFilters()` — reads all filter controls and applies `filter-hidden` / `filter-hidden-parent` CSS classes to rows.
- `resetFilters()` — restores all checkboxes and clears the search box.
- `__filterIds__` — array of filter input IDs excluded from `collectState()` / localStorage auto-save.
- `syncFilterRowHeight()` — measures a Diff Detail row height and sets `--ft-row-h` CSS variable so Change Importance rows are exactly 2× height.
- `wrapInputWithClear(inp)` — wraps the search input with a clear button (⊗) that dispatches both `input` and `change` events.
- `downloadReviewed()` — clears all `filter-hidden` / `filter-hidden-parent` classes and inline table widths before capturing `outerHTML`, then restores live page state via `syncTableWidths()`.
- `downloadExcelCompatibleHtml()` — generates a simplified HTML `<table>` with Excel XML namespace declarations and downloads it as `diff_report_YYYYMMDD_reviewed_Excel-compatible.html`. The table includes header metadata, all file sections with color-coded titles, per-file review state, legend, and summary. Available only in reviewed HTML.
- `buildExcelRow(tr)` — extracts cell data from a file `<tr>` row and builds an 11-column Excel-compatible table row.

### Design decisions

1. Filter state is intentionally excluded from `collectState()` and localStorage. This means filters reset on page reload and are never saved to the reviewed HTML state.
2. The filter zone is **outside** `<!--CTRL-->...<!--/CTRL-->` markers so that reviewed HTML retains full filter functionality. Filter checkboxes and the search input are excluded from the reviewed-mode read-only enforcement (`__filterIds__` check).
3. Importance filtering only applies to rows that have a `data-importance` attribute. Rows without importance (e.g. "No structural changes detected") pass through the filter.
4. The `btn-input-clear` CSS class is used for the search input clear button. Do **not** use `btn-clear` — that class is already used for toolbar buttons (Fold all details, Reset filters, Clear all) and caused a collision that hid the toolbar buttons entirely.
5. `downloadReviewed()` must clear inline `style="width:..."` from tables before `outerHTML` capture. Otherwise, stale pixel widths from `syncTableWidths()` are baked into the reviewed HTML and cause column width mismatch on reviewed load.

## Performance Benchmarks

The [`FolderDiffIL4DotNet.Benchmarks`](../FolderDiffIL4DotNet.Benchmarks/) project uses [BenchmarkDotNet](https://www.nuget.org/packages/BenchmarkDotNet/) to measure performance:

```bash
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks -- --filter *TextDiffer*
```

Benchmark classes:
- [`TextDifferBenchmarks`](../FolderDiffIL4DotNet.Benchmarks/TextDifferBenchmarks.cs): Myers diff on small (100 lines), medium (10K), and large (1M) IL-like files.
- [`FolderDiffBenchmarks`](../FolderDiffIL4DotNet.Benchmarks/FolderDiffBenchmarks.cs): file enumeration (100 / 1K / 10K files) and SHA256 hash comparison.

**CI integration:** The `benchmark` job in [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml) runs all benchmarks on `workflow_dispatch` and uploads `BenchmarkDotNet.Artifacts/` as a CI artifact with JSON and GitHub exporters.

**Regression detection:** The [`.github/workflows/benchmark-regression.yml`](../.github/workflows/benchmark-regression.yml) workflow runs automatically on every PR to `main` and on `push` to `main`. It combines JSON results from all benchmark classes into a single report and uses [`benchmark-action/github-action-benchmark@v1`](https://github.com/benchmark-action/github-action-benchmark) to compare against the stored baseline in the `gh-benchmarks` branch. If any benchmark degrades by more than 50% (alert threshold `150%`), the job fails and a PR comment is posted. On push to `main`, the results are stored as the new baseline.

### SHA256 Hash Pre-Seeding

When IL cache is enabled, `FileDiffService` seeds computed SHA256 hashes into `ILMemoryCache` via `PreSeedFileHash` after the initial hash comparison. This avoids recomputing SHA256 during IL cache key construction (`BuildILCacheKey`), which would otherwise re-read the file.

### IL Line Split-and-Filter Optimization

`ILOutputService.SplitAndFilterIlLines` combines the `Split('\n')` and `Where(filter)` steps into a single pass, producing one `List<string>` directly instead of creating four intermediate lists.

## Source Style Notes

Keep internal formatting choices simple and local:
- Prefer interpolated strings for fixed-format messages that are only used once.
- Keep shared format templates only when the same message shape is intentionally reused in multiple places.
- Place domain-independent helpers under [`FolderDiffIL4DotNet.Core/`](../FolderDiffIL4DotNet.Core/) and keep [`FolderDiffIL4DotNet/Services`](../Services/) focused on folder-diff behavior.
- Promote cross-project byte-size and timestamp literals into [`FolderDiffIL4DotNet.Core/Common/CoreConstants.cs`](../FolderDiffIL4DotNet.Core/Common/CoreConstants.cs), while keeping app-specific literals in [`Common/Constants.cs`](../Common/Constants.cs). IL-domain string constants such as [`Constants.IL_MVID_LINE_PREFIX`](../Common/Constants.cs) belong in [`Common/Constants.cs`](../Common/Constants.cs) and must not be duplicated across service files.
- Avoid adding new `#region` blocks unless they solve a concrete readability problem that file structure and naming do not already solve.

## Architecture Overview

```mermaid
flowchart TD
    A["Program.cs"] --> B["ProgramRunner"]
    B --> C["ConfigService"]
    B --> T["DiffPipelineExecutor"]
    T --> D["DiffExecutionContext"]
    T --> E["Run-scoped ServiceProvider"]
    E --> F["FolderDiffService"]
    E --> Q["FolderDiffExecutionStrategy"]
    E --> G["ReportGenerateService"]
    E --> H["ProgressReportService"]
    F --> I["IFileSystemService"]
    F --> Q
    F --> J["FileDiffService"]
    J --> K["IFileComparisonService"]
    J --> L["ILOutputService"]
    L --> M["DotNetDisassembleService"]
    M --> R["ILCachePrefetcher"]
    M --> S["DisassemblerHelper"]
    R --> S
    L --> N["ILCache"]
    L --> O["ILTextOutputService"]
    F --> P["FileDiffResultLists"]
    G --> P
    click P href "../Models/FileDiffResultLists.cs" "Open FileDiffResultLists"
```

Design intent:
- [`Program.cs`](../Program.cs) stays minimal and owns only application-root service registration.
- [`ProgramRunner`](../ProgramRunner.cs) is the orchestration boundary for one console execution: CLI dispatch, argument validation, config loading, and exit-code mapping.
- [`DiffPipelineExecutor`](../Runner/DiffPipelineExecutor.cs) owns the diff execution pipeline: builds the scoped DI container, runs the folder diff, and generates all reports.
- [`DryRunExecutor`](../Runner/DryRunExecutor.cs) handles the `--dry-run` preview: enumerates files and displays statistics without running comparisons or generating reports.
- [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) carries immutable run-specific paths and mode decisions.
- [`FolderDiffIL4DotNet.Core`](../FolderDiffIL4DotNet.Core/) is the reusable helper-library boundary for console rendering, diagnostics, filesystem helpers, and text sanitization with no folder-diff domain policy.
- Core pipeline services use constructor injection and interfaces instead of static mutable state or ad hoc object creation.
- [`IFileSystemService`](../Services/IFileSystemService.cs) and [`IFileComparisonService`](../Services/IFileComparisonService.cs) are the low-level seams that keep discovery/compare I/O unit-testable without changing the production decision tree. [`IFileSystemService.EnumerateFiles(...)`](../Services/IFileSystemService.cs) specifically preserves lazy discovery so large trees do not require an eager `string[]` snapshot before filtering.
- [`FolderDiffExecutionStrategy`](../Services/FolderDiffExecutionStrategy.cs) centralizes inclusion filtering, ignored-file recording, and auto-parallelism policy so those rules are no longer embedded directly inside [`FolderDiffService`](../Services/FolderDiffService.cs).
- [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) is the run-scoped aggregation hub shared by diffing and reporting.
- [`DotNetDisassembleService`](../Services/DotNetDisassembleService.cs) is responsible for disassembly execution and cache hit/store tracking. IL-cache prefetch is delegated to [`ILCachePrefetcher`](../Services/ILCachePrefetcher.cs), which encapsulates the prefetch-only responsibility. Shared static helpers (command identification, candidate enumeration, executable path resolution) live in [`DisassemblerHelper`](../Services/DisassemblerHelper.cs) to avoid duplication between the two classes.
- [`FolderDiffService`](../Services/FolderDiffService.cs) keeps the pre-compute keep-alive spinner as a dedicated `CreateKeepAliveTask()` method so `PrecomputeIlCachesAsync()` focuses on orchestration rather than background-task lifecycle.

<a id="guide-en-execution-lifecycle"></a>
## Execution Lifecycle

### Startup Sequence

```mermaid
sequenceDiagram
    participant CLI as CLI
    participant Program as Program.cs
    participant Runner as ProgramRunner
    participant Config as ConfigService
    participant Scope as Run Scope
    participant Diff as FolderDiffService
    participant Report as ReportGenerateService

    CLI->>Program: Main(args)
    Program->>Runner: RunAsync(args)
    Runner->>Runner: initialize logger and print version
    Runner->>Runner: validate args and create Reports/<label>
    Runner->>Config: LoadConfigAsync()
    Runner->>Runner: build DiffExecutionContext
    Runner->>Scope: BuildRunServiceProvider()
    Runner->>Diff: ExecuteFolderDiffAsync()
    Diff-->>Runner: return aggregated diff results
    Runner->>Report: GenerateDiffReport(...)
    Runner->>Runner: output aggregated completion warnings
    Runner->>Runner: output completion summary chart
    Runner-->>CLI: typed exit code (0/2/3/4/1)
```

The diff phase returns [`FileDiffResultLists`](../Models/FileDiffResultLists.cs), which is then consumed by report generation and completion-warning output.

### What happens inside `RunAsync`

1. Parse CLI options (`--help`, `--version`, `--print-config`, `--no-pause`, `--config`, `--threads`, `--no-il-cache`, `--skip-il`, `--no-timestamp-warnings`, `--coffee`, `--beer`, `--matcha`, `--bell`).
2. If `--help` or `--version` is present, print and exit immediately with code `0` — no logger initialization occurs.
2a. If `--print-config` is present (optionally combined with `--config <path>`), load the effective configuration — [`config.json`](../config.json) deserialized and all `FOLDERDIFF_*` environment variable overrides applied — serialize it as indented JSON to standard output, and exit with code `0`. Config load errors exit with code `3`.
3. Initialize logging and print application version.
4. Validate `old`, `new`, and `reportLabel` arguments. Unknown CLI flags surface here as exit code `2`.
5. Create `Reports/<label>` early and fail if the label already exists.
6. Load the config file — from the path given to `--config` if supplied, otherwise from [`AppContext.BaseDirectory`](https://learn.microsoft.com/en-us/dotnet/api/system.appcontext.basedirectory?view=net-8.0) — and deserialize it into a mutable [`ConfigSettingsBuilder`](../Models/ConfigSettingsBuilder.cs). Immediately after deserialization, [`ConfigService.ApplyEnvironmentVariableOverrides`](../Services/ConfigService.cs) applies any `FOLDERDIFF_<PROPERTYNAME>` environment variable overrides (e.g. `FOLDERDIFF_MAXPARALLELISM=4`) to the builder.
7. Apply CLI overrides on top of the builder: `--threads` sets [`MaxParallelism`](../Models/ConfigSettingsBuilder.cs); `--no-il-cache` sets [`EnableILCache`](../Models/ConfigSettingsBuilder.cs) `= false`; `--skip-il` sets [`SkipIL`](../Models/ConfigSettingsBuilder.cs) `= true`; `--no-timestamp-warnings` sets [`ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp`](../Models/ConfigSettingsBuilder.cs) `= false`. Then [`ConfigSettingsBuilder.Validate()`](../Models/ConfigSettingsBuilder.cs) is called; if any value is out of range, the run fails with exit code `3`. Finally, [`ConfigSettingsBuilder.Build()`](../Models/ConfigSettingsBuilder.cs) produces an immutable [`ConfigSettings`](../Models/ConfigSettings.cs) instance used for the remainder of the run.
8. Clear transient shared helpers such as [`TimestampCache`](../Services/Caching/TimestampCache.cs).
9. Compute [`DiffExecutionContext`](../Services/DiffExecutionContext.cs), including network-share decisions.
10. Build the run-scoped DI container.
11. Run the folder diff and finish progress display.
12. Generate [`diff_report.md`](samples/diff_report.md) from aggregated results.
13. Generate [`diff_report.html`](samples/diff_report.html) from aggregated results when [`ShouldGenerateHtmlReport`](../Models/ConfigSettings.cs) is `true` (default). The HTML file is a self-contained interactive review document with localStorage auto-save and a download function that bakes the current review state into a portable snapshot.
14. Convert the phase result into a process exit code: `0` on success, `2` for invalid CLI/input paths, `3` for configuration load/parse/validation failures, `4` for diff/report execution failures, and `1` only for unexpected internal errors.

The implementation keeps `RunAsync()` short by treating those steps as explicit phases and delegating each phase to focused private helpers.

Failure behavior:
- [`ProgramRunner`](../ProgramRunner.cs) now uses small typed step results at the application boundary instead of flattening every failure into one catch-all exit code.
- Argument validation, unknown flags, and missing input paths map to exit code `2`.
- [`ConfigService`](../Services/ConfigService.cs) failures such as missing [`config.json`](../config.json), parse failures, config-read I/O errors, or settings that fail [`ConfigSettingsBuilder.Validate()`](../Models/ConfigSettingsBuilder.cs) map to exit code `3`.
- Diff execution and report-generation failures, including fatal IL comparison failures surfaced as [`InvalidOperationException`](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception?view=net-8.0), map to exit code `4`.
- Exit code `1` is reserved for unexpected internal errors that escape the explicit phase classification.
- [`InvalidOperationException`](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception?view=net-8.0) originating from IL comparison is treated as a fatal exception and stops the whole run.
- [`FolderDiffService.ExecuteFolderDiffAsync()`](../Services/FolderDiffService.cs) logs and rethrows expected runtime exceptions such as path-validation errors, [`DirectoryNotFoundException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.directorynotfoundexception?view=net-8.0), [`IOException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.ioexception?view=net-8.0), [`UnauthorizedAccessException`](https://learn.microsoft.com/en-us/dotnet/api/system.unauthorizedaccessexception?view=net-8.0), and [`NotSupportedException`](https://learn.microsoft.com/en-us/dotnet/api/system.notsupportedexception?view=net-8.0); only truly unexpected exceptions use the separate "unexpected error" log wording.
- The preflight write-permission check ([`CheckReportsParentWritableOrThrow`](../Runner/RunPreflightValidator.cs)) logs and re-throws both [`UnauthorizedAccessException`](https://learn.microsoft.com/en-us/dotnet/api/system.unauthorizedaccessexception?view=net-8.0) and [`IOException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.ioexception?view=net-8.0) with cause-specific messages. No I/O error is silently swallowed.
- Read-only protection on output files remains best-effort and warning-only.

<a id="guide-en-di-layout"></a>
## Dependency Injection Layout

### Root container

Registered in [`Program.cs`](../Program.cs):
- [`ILoggerService`](../Services/ILoggerService.cs) -> [`LoggerService`](../Services/LoggerService.cs)
- [`ConfigService`](../Services/ConfigService.cs)
- [`ProgramRunner`](../ProgramRunner.cs)

This root container is intentionally small. It should not accumulate run-specific services.

### Run-scoped container

Registered in [`RunScopeBuilder.Build(...)`](../Runner/RunScopeBuilder.cs):
- Singletons inside the run scope
- [`IReadOnlyConfigSettings`](../Models/IReadOnlyConfigSettings.cs) (immutable [`ConfigSettings`](../Models/ConfigSettings.cs) built from [`ConfigSettingsBuilder`](../Models/ConfigSettingsBuilder.cs))
- [`DiffExecutionContext`](../Services/DiffExecutionContext.cs)
- [`ILoggerService`](../Services/ILoggerService.cs) (shared logger instance)
- Scoped services
- [`FileDiffResultLists`](../Models/FileDiffResultLists.cs)
- [`DotNetDisassemblerCache`](../Services/Caching/DotNetDisassemblerCache.cs)
- [`ILCache`](../Services/Caching/ILCache.cs) (nullable when disabled)
- [`ProgressReportService`](../Services/ProgressReportService.cs)
- [`ReportGenerateService`](../Services/ReportGenerateService.cs)
- [`HtmlReportGenerateService`](../Services/HtmlReportGenerateService.cs)
- [`IFileSystemService`](../Services/IFileSystemService.cs) / [`FileSystemService`](../Services/FileSystemService.cs)
- [`IFolderDiffExecutionStrategy`](../Services/IFolderDiffExecutionStrategy.cs) / [`FolderDiffExecutionStrategy`](../Services/FolderDiffExecutionStrategy.cs)
- [`IFileComparisonService`](../Services/IFileComparisonService.cs) / [`FileComparisonService`](../Services/FileComparisonService.cs)
- [`IILTextOutputService`](../Services/ILOutput/IILTextOutputService.cs) / [`ILTextOutputService`](../Services/ILOutput/ILTextOutputService.cs)
- [`IDotNetDisassembleService`](../Services/IDotNetDisassembleService.cs) / [`DotNetDisassembleService`](../Services/DotNetDisassembleService.cs)
- [`IILOutputService`](../Services/IILOutputService.cs) / [`ILOutputService`](../Services/ILOutputService.cs)
- [`IFileDiffService`](../Services/IFileDiffService.cs) / [`FileDiffService`](../Services/FileDiffService.cs)
- [`IFolderDiffService`](../Services/IFolderDiffService.cs) / [`FolderDiffService`](../Services/FolderDiffService.cs)

Why this matters:
- Each execution gets a newly created [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) for diff results plus newly created disassembler-related state and caches for keeping old/new on the same disassembler, so nothing is carried over from the previous run.
- Tests can replace interfaces without mutating static fields.
- Runtime path decisions are explicit and immutable once the run starts.

## Core Responsibilities

| File | Responsibility | Notes |
| --- | --- | --- |
| [`Program.cs`](../Program.cs) | Application entry point | Must remain thin |
| [`ProgramRunner.cs`](../ProgramRunner.cs) | CLI dispatch, argument validation, config loading, exit-code mapping | Help text in [`ProgramRunner.HelpText.cs`](../Runner/ProgramRunner.HelpText.cs), config loading/validation in [`ProgramRunner.Config.cs`](../Runner/ProgramRunner.Config.cs) |
| [`Runner/DiffPipelineExecutor.cs`](../Runner/DiffPipelineExecutor.cs) | Diff execution pipeline and report generation | Builds scoped DI container, runs diff, generates Markdown/HTML/audit-log reports |
| [`Runner/DryRunExecutor.cs`](../Runner/DryRunExecutor.cs) | `--dry-run` pre-execution preview | Enumerates files, counts union/assembly candidates, shows extension breakdown without running comparison |
| [`FolderDiffIL4DotNet.Core/`](../FolderDiffIL4DotNet.Core/) | Reusable console/diagnostics/IO/text helpers | No folder-diff domain logic |
| [`FolderDiffIL4DotNet.Core/Text/EncodingDetector.cs`](../FolderDiffIL4DotNet.Core/Text/EncodingDetector.cs) | File encoding auto-detection (BOM, UTF-8 validation, ANSI fallback) | Used by inline diff to correctly read non-UTF-8 files (e.g. Shift_JIS); requires `System.Text.Encoding.CodePages` |
| [`Services/DiffExecutionContext.cs`](../Services/DiffExecutionContext.cs) | Immutable run paths and network-mode decisions | No mutable state |
| [`Services/FolderDiffService.cs`](../Services/FolderDiffService.cs) | Folder-diff orchestration and result routing | Owns progress and added/removed routing |
| [`Services/FolderDiffExecutionStrategy.cs`](../Services/FolderDiffExecutionStrategy.cs) | Discovery filtering and auto-parallelism policy | Applies ignored extensions and network-aware auto parallelism |
| [`Services/IFileSystemService.cs`](../Services/IFileSystemService.cs) + [`Services/FileSystemService.cs`](../Services/FileSystemService.cs) | Discovery/output filesystem abstraction | Enables folder-level unit tests and lazy file discovery |
| [`Services/FileDiffService.cs`](../Services/FileDiffService.cs) | Per-file decision tree | SHA256 -> IL -> text -> fallback |
| [`Services/IFileComparisonService.cs`](../Services/IFileComparisonService.cs) + [`Services/FileComparisonService.cs`](../Services/FileComparisonService.cs) | Per-file compare/detect I/O abstraction | Enables file-level unit tests |
| [`Services/ILOutputService.cs`](../Services/ILOutputService.cs) | IL compare flow, line filtering, block-aware order-independent comparison, optional IL dump writing | Enforces same disassembler identity; falls back to block-level multiset comparison when line order differs |
| [`Services/ILOutput/ILBlockParser.cs`](../Services/ILOutput/ILBlockParser.cs) | Parses IL disassembly output into logical blocks (methods, classes, properties) | Used by `ILOutputService.BlockAwareSequenceEqual` for order-independent comparison |
| [`Services/AssemblyMethodAnalyzer.cs`](../Services/AssemblyMethodAnalyzer.cs) | Method-level change detection via `System.Reflection.Metadata` | Best-effort; returns `null` on failure. Detects type/method/property/field additions, removals, and modifications (access modifier changes, modifier changes, type changes, IL body changes). Each entry is auto-classified by [`ChangeImportanceClassifier`](../Services/ChangeImportanceClassifier.cs) |
| [`Services/ChangeImportanceClassifier.cs`](../Services/ChangeImportanceClassifier.cs) | Rule-based importance classifier for `MemberChangeEntry` | Assigns `High` / `Medium` / `Low` [`ChangeImportance`](../Models/ChangeImportance.cs) based on change type, access modifiers, and arrow-notation field changes |
| [`Models/ChangeImportance.cs`](../Models/ChangeImportance.cs) | Change importance enum | `Low=0`, `Medium=1`, `High=2`; used by `MemberChangeEntry.Importance` and report display |
| [`Services/ChangeTagClassifier.cs`](../Services/ChangeTagClassifier.cs) | Heuristic change-pattern classifier | Infers [`ChangeTag`](../Models/ChangeTag.cs) labels (Extract, Inline, Move, Rename, Signature, Access, BodyEdit, DepUpdate, +Method, -Method, +Type, -Type) from semantic analysis and dependency data; called by [`FileDiffService`](../Services/FileDiffService.cs) after semantic/dependency analysis |
| [`Models/ChangeTag.cs`](../Models/ChangeTag.cs) | Change tag enum | 12 values representing estimated change patterns; displayed in "Estimated Change" report column |
| [`Services/DotNetDisassembleService.cs`](../Services/DotNetDisassembleService.cs) | Tool probing, disassembly execution, cache hit/store tracking, blacklist handling | Central tool boundary; delegates prefetch to [`ILCachePrefetcher`](../Services/ILCachePrefetcher.cs) |
| [`Services/ILCachePrefetcher.cs`](../Services/ILCachePrefetcher.cs) | IL-cache prefetch (pre-hit verification for all candidate command/arg patterns) | Extracted from [`DotNetDisassembleService`](../Services/DotNetDisassembleService.cs); owns its own hit counter |
| [`Services/DisassemblerHelper.cs`](../Services/DisassemblerHelper.cs) | Shared static helpers: command identification, candidate enumeration, executable path resolution, availability probing | Used by both [`DotNetDisassembleService`](../Services/DotNetDisassembleService.cs) and [`ILCachePrefetcher`](../Services/ILCachePrefetcher.cs); `ProbeAllCandidates()` returns [`DisassemblerProbeResult`](../Models/DisassemblerProbeResult.cs) list for report header; no instance state |
| [`Models/DisassemblerProbeResult.cs`](../Models/DisassemblerProbeResult.cs) | Disassembler availability probe result record | `ToolName`, `Available`, `Version`, `Path`; stored in [`FileDiffResultLists.DisassemblerAvailability`](../Models/FileDiffResultLists.cs) |
| [`Services/DisassemblerBlacklist.cs`](../Services/DisassemblerBlacklist.cs) | Per-tool fail-count tracking and configurable TTL blacklist | Thread-safe [`ConcurrentDictionary`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2?view=net-8.0); TTL defaults to [`DisassemblerBlacklistTtlMinutes`](../Models/ConfigSettings.cs) from config |
| [`Services/Caching/ILCache.cs`](../Services/Caching/ILCache.cs) | Public cache facade and coordinator for IL artifacts | Delegates memory/disk details to focused cache components |
| [`Services/Caching/ILMemoryCache.cs`](../Services/Caching/ILMemoryCache.cs) | In-memory IL/SHA256 cache with LRU and TTL | Owns transient retention policy |
| [`Services/Caching/ILDiskCache.cs`](../Services/Caching/ILDiskCache.cs) | Disk persistence and quota enforcement for IL cache files | Owns cache-file I/O and trimming |
| [`Services/Caching/DotNetDisassemblerCache.cs`](../Services/Caching/DotNetDisassemblerCache.cs) | Disassembler version string cache | Avoids repeated process-launch overhead for version queries |
| [`Services/Caching/TimestampCache.cs`](../Services/Caching/TimestampCache.cs) | In-memory file last-write timestamp cache | Static; cleared per run cycle to reduce I/O |
| [`Services/ReportGenerationContext.cs`](../Services/ReportGenerationContext.cs) | Immutable parameter bag for report generation services | Eliminates parameter duplication at `ProgramRunner` boundary |
| [`Services/ReportGenerateService.cs`](../Services/ReportGenerateService.cs) | Markdown report generation | Reads [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) only; iterates `_sectionWriters` via [`IReportSectionWriter`](../Services/IReportSectionWriter.cs) |
| [`Services/IReportSectionWriter.cs`](../Services/IReportSectionWriter.cs) + [`Services/ReportWriteContext.cs`](../Services/ReportWriteContext.cs) | Per-section report writing interface and context bag | 10 private nested implementations inside [`ReportGenerateService`](../Services/ReportGenerateService.cs) |
| [`Services/HtmlReportGenerateService.cs`](../Services/HtmlReportGenerateService.cs) | Interactive HTML review report generation | Reads [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) only; produces a self-contained [`diff_report.html`](samples/diff_report.html) with checkboxes, text inputs, localStorage auto-save, and download function; uses CSS custom properties (`var(--color-*)`) and utility classes instead of inline styles for theme-aware rendering; supports automatic dark mode via `prefers-color-scheme`; "Download as reviewed" computes SHA256 of the reviewed HTML via Web Crypto API, embeds the hash for self-verification, downloads a companion `.sha256` verification file, and adds a "Verify integrity" button to the reviewed banner; skipped when [`ShouldGenerateHtmlReport`](../Models/ConfigSettings.cs) is `false` |
| [`Services/AuditLogGenerateService.cs`](../Services/AuditLogGenerateService.cs) | Structured JSON audit log generation | Reads [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) and computes SHA256 integrity hashes of `diff_report.md` / `diff_report.html`; produces [`audit_log.json`](samples/audit_log.json); skipped when [`ShouldGenerateAuditLog`](../Models/ConfigSettings.cs) is `false` |
| [`Services/SbomGenerateService.cs`](../Services/SbomGenerateService.cs) | SBOM (Software Bill of Materials) generation | Extracts component list from [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) with SHA256 hashes and diff status; outputs CycloneDX 1.5 JSON (`sbom.cdx.json`) or SPDX 2.3 JSON (`sbom.spdx.json`); skipped when [`ShouldGenerateSbom`](../Models/ConfigSettings.cs) is `false` |
| [`Models/AuditLogEntry.cs`](../Models/AuditLogEntry.cs) | Audit log data models | [`AuditLogRecord`](../Models/AuditLogEntry.cs) (top-level), [`AuditLogFileEntry`](../Models/AuditLogEntry.cs) (per-file), [`AuditLogSummary`](../Models/AuditLogEntry.cs) (counts) |
| [`Models/SbomModels.cs`](../Models/SbomModels.cs) | SBOM data models | CycloneDX 1.5 models ([`CycloneDxBom`](../Models/SbomModels.cs), [`CycloneDxComponent`](../Models/SbomModels.cs)), SPDX 2.3 models ([`SpdxDocument`](../Models/SbomModels.cs), [`SpdxPackage`](../Models/SbomModels.cs)), [`SbomFormat`](../Models/SbomModels.cs) enum |
| [`Models/FileDiffResultLists.cs`](../Models/FileDiffResultLists.cs) | Thread-safe run results and metadata | Shared aggregation object; split into partial files: [`FileDiffResultLists.ComparisonResults.cs`](../Models/FileDiffResultLists.ComparisonResults.cs) (diff details, disassembler labels, ignored files), [`FileDiffResultLists.Metadata.cs`](../Models/FileDiffResultLists.Metadata.cs) (semantic changes, dependency changes, warnings, disassembler info) |

<a id="guide-en-comparison-pipeline"></a>
## Comparison Pipeline

### Folder-level routing

```mermaid
flowchart TD
    A["Enumerate old/new files"] --> B["Filter IgnoredExtensions"]
    B --> C["Build union count for progress"]
    C --> D{"old-side relative path exists in new?"}
    D -- "No" --> E["Record Removed"]
    D -- "Yes" --> F["Run FilesAreEqualAsync(relativePath)"]
    F --> G{"Equal?"}
    G -- "Yes" --> H["Record Unchanged"]
    G -- "No" --> I["Record Modified"]
    E --> J{"More old files?"}
    H --> J
    I --> J
    J -- "Yes" --> D
    J -- "No" --> K["Record remaining new-only files as Added"]
```

Implementation notes:
- [`FolderDiffService.ExecuteFolderDiffAsync()`](../Services/FolderDiffService.cs) clears run-scoped aggregates, then asks [`FolderDiffExecutionStrategy`](../Services/FolderDiffExecutionStrategy.cs) to enumerate old/new files with [`IgnoredExtensions`](../Models/ConfigSettings.cs) already applied and to compute progress from the union of relative paths.
- Discovery now flows through [`IFileSystemService.EnumerateFiles(...)`](../Services/IFileSystemService.cs), so ignored extensions are filtered while entries are streamed instead of first materializing the entire directory tree into an array.
- `PrecomputeIlCachesAsync()` runs before per-file classification so disassembler/cache warm-up does not distort the later decision path. It now streams distinct old/new absolute paths in configurable batches instead of building one extra all-files list first, which reduces peak memory pressure on very large trees.
- The old side is the driving set. Missing matches in `new` become `Removed`, while leftovers in `remainingNewFilesAbsolutePathHashSet` become `Added` after old-side traversal completes.
- Parallel mode only changes processing order. Because each relative path is removed from the remaining-new set before the expensive compare starts, the final classification rules are the same as in sequential execution.
- `Unchanged` versus `Modified` is decided only from the boolean returned by `FilesAreEqualAsync(relativePath, maxParallel)`. The detail reason is recorded separately in [`FileDiffResultLists`](../Models/FileDiffResultLists.cs).

### Per-file decision tree

```mermaid
flowchart TD
    A["Matched relative path"] --> B{"SHA256 equal?"}
    B -- "Yes" --> C["Record SHA256Match and return true"]
    B -- "No" --> D{"Old file is .NET executable?"}
    D -- "Yes" --> E["Disassemble old/new with same tool identity"]
    E --> F{"Filtered IL equal?"}
    F -- "Yes" --> G["Record ILMatch and return true"]
    F -- "No" --> H["Record ILMismatch and return false"]
    D -- "No" --> I{"Extension is text?"}
    I -- "Yes" --> J["Run text compare"]
    J --> K{"Text equal?"}
    K -- "Yes" --> L["Record TextMatch and return true"]
    K -- "No" --> M["Record TextMismatch and return false"]
    I -- "No" --> N["Record SHA256Mismatch and return false"]
```

Rules that are easy to break:
- The first successful classification for a file is the final classification for that file.
- IL comparison is only attempted after SHA256 mismatch and only for files detected as .NET executables.
- IL comparison ignores lines starting with [`Constants.IL_MVID_LINE_PREFIX`](../Common/Constants.cs) (`// MVID:`) unconditionally because they are disassembler-emitted Module Version ID metadata and can change on rebuild without reflecting an executable IL change.
- Additional IL ignore rules are substring-based and case-sensitive (`StringComparison.Ordinal`).
- IL comparison must use the same disassembler identity and version label for old/new.
- Text comparison can fall back from chunk-parallel mode to sequential mode on error, but only because chunk-parallel exceptions are allowed to bubble to `FilesAreEqualAsync(...)`.

Per-file mechanics:
- [`FileDiffService.FilesAreEqualAsync(...)`](../Services/FileDiffService.cs) uses the old-side absolute path for `.NET executable` detection, file extension lookup, and threshold decisions.
- In normal execution, `.NET executable` detection, SHA256/text comparison, file length lookup, and chunk reads all go through [`IFileComparisonService`](../Services/IFileComparisonService.cs). This keeps [`FileDiffService`](../Services/FileDiffService.cs) from depending directly on the concrete comparison implementation and lets tests replace [`IFileComparisonService`](../Services/IFileComparisonService.cs) with a mock or stub. The default implementation, [`FileComparisonService`](../Services/FileComparisonService.cs), delegates those operations to [`DotNetDetector`](../FolderDiffIL4DotNet.Core/Diagnostics/DotNetDetector.cs) and [`FileComparer`](../FolderDiffIL4DotNet.Core/IO/FileComparer.cs).
- [`DotNetDetector.DetectDotNetExecutable(...)`](../FolderDiffIL4DotNet.Core/Diagnostics/DotNetDetector.cs) distinguishes `NotDotNetExecutable` from `Failed`; [`FileDiffService`](../Services/FileDiffService.cs) logs a warning on `Failed` before skipping the IL path.
- Once SHA256 matches, the code records `SHA256Match` and returns immediately; no IL comparison or text comparison runs after that.
- The IL path delegates to [`ILOutputService.DiffDotNetAssembliesAsync(...)`](../Services/ILOutputService.cs), which disassembles old/new via `DisassemblePairWithSameDisassemblerAsync(...)`, normalizes the comparison label, filters lines, optionally writes filtered IL text, and returns both equality and the disassembler label.
- [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) covers this boundary with the preferred tool path: it builds the same tiny class library twice with `Deterministic=false`, confirms the DLL bytes differ, and then verifies that [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) still returns `ILMatch` after filtering.
- `BuildComparisonDisassemblerLabel(...)` is part of correctness. If old/new produce different tool identities or version labels, the code rejects that comparison and raises [`InvalidOperationException`](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception?view=net-8.0).
- `ShouldExcludeIlLine(...)` always strips lines starting with [`Constants.IL_MVID_LINE_PREFIX`](../Common/Constants.cs) (`// MVID:`). If [`ShouldIgnoreILLinesContainingConfiguredStrings`](../Models/ConfigSettings.cs) is `true`, it also strips any substring from [`ILIgnoreLineContainingStrings`](../Models/ConfigSettings.cs) after trimming and deduplicating the configured values, using `StringComparison.Ordinal`.
- Files that are not handled by IL comparison and whose extension is included in [`TextFileExtensions`](../Models/ConfigSettings.cs) are compared as text files. At that point, the code converts [`TextDiffParallelThresholdKilobytes`](../Models/ConfigSettings.cs) and [`TextDiffChunkSizeKilobytes`](../Models/ConfigSettings.cs) into effective byte counts and uses those values to choose the comparison method.
- If [`OptimizeForNetworkShares`](../Models/ConfigSettings.cs) is enabled, the code avoids chunk-parallel reads on remote storage and always uses sequential `DiffTextFilesAsync(...)`, regardless of file size. In local-optimized mode, it uses the old-side file size: below [`TextDiffParallelThresholdKilobytes`](../Models/ConfigSettings.cs) it stays sequential, and at or above the threshold it splits the file into fixed-size chunks based on [`TextDiffChunkSizeKilobytes`](../Models/ConfigSettings.cs) and runs `DiffTextFilesParallelAsync(...)`.
- If [`TextDiffParallelMemoryLimitMegabytes`](../Models/ConfigSettings.cs) is greater than `0`, [`FileDiffService`](../Services/FileDiffService.cs) treats it as an additional buffer budget for chunk-parallel text diff, logs the current managed-heap size, and reduces the effective worker count or falls back to sequential comparison when that budget cannot cover the requested parallelism.
- If chunk-parallel text comparison throws [`ArgumentOutOfRangeException`](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception?view=net-8.0), [`IOException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.ioexception?view=net-8.0), [`UnauthorizedAccessException`](https://learn.microsoft.com/en-us/dotnet/api/system.unauthorizedaccessexception?view=net-8.0), or [`NotSupportedException`](https://learn.microsoft.com/en-us/dotnet/api/system.notsupportedexception?view=net-8.0), the code logs a warning and falls back to sequential `DiffTextFilesAsync(...)`. Because of that fallback, `DiffTextFilesParallelAsync(...)` must not swallow those exceptions and replace them with `false`.
- Files that are neither IL-comparison targets nor text-comparison targets end at `SHA256Mismatch` when SHA256 differs. `SHA256Mismatch` is also part of the aggregated end-of-run warnings, and the report writes that warning in the final `Warnings` section, with its detail table (`[ ! ] Modified Files — SHA256Mismatch (Manual Review Recommended)`) placed immediately below the warning message. Each warning message in the `Warnings` section is immediately followed by its corresponding detail table (interleaved layout). There is no deeper generic binary diff step today.
- For files classified as **modified**, if [`ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp`](../Models/ConfigSettings.cs) is `true` and the new-side last-modified time is older than the old-side last-modified time, the code records a timestamp-regression warning. The check is performed only after `FilesAreEqualAsync` returns `false`; unchanged files are never evaluated. That warning is emitted in the aggregated console output at the end of the run and also written after the `SHA256Mismatch` warning in the report's final `Warnings` section as a list of files with regressed timestamps.

Failure handling:
- [`InvalidOperationException`](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception?view=net-8.0) thrown during IL comparison is logged and intentionally rethrown. This treats IL tool mismatches or setup problems as fatal exceptions and stops the whole run.
- Failures from [`DotNetDetector.DetectDotNetExecutable(...)`](../FolderDiffIL4DotNet.Core/Diagnostics/DotNetDetector.cs) are not treated as fatal exceptions. The code logs a warning, skips IL comparison only, and then continues into text comparison or `SHA256Mismatch` handling.
- [`FileNotFoundException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.filenotfoundexception?view=net-8.0) thrown by `FilesAreEqualAsync(...)` is caught in [`FolderDiffService`](../Services/FolderDiffService.cs) when a new-side file is deleted after enumeration but before comparison. The file is classified as `Removed`, a warning is logged, and traversal continues. This is distinct from [`IOException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.ioexception?view=net-8.0) thrown during enumeration (for example a symlink loop), which is rethrown and stops the entire run.
- `FilesAreEqualAsync(...)` also treats [`DirectoryNotFoundException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.directorynotfoundexception?view=net-8.0), [`IOException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.ioexception?view=net-8.0), [`UnauthorizedAccessException`](https://learn.microsoft.com/en-us/dotnet/api/system.unauthorizedaccessexception?view=net-8.0), and [`NotSupportedException`](https://learn.microsoft.com/en-us/dotnet/api/system.notsupportedexception?view=net-8.0) as expected runtime failures: it logs them with both old/new absolute paths and rethrows without changing the exception type.
- Other unexpected exceptions are logged from inside `FilesAreEqualAsync(...)` with separate "unexpected error" wording and then rethrown to the caller.
- `PrecomputeIlCachesAsync()`, disk-cache eviction cleanup, and post-write read-only protection are best-effort operations. They log warnings and continue because the main comparison result or already-written report remains usable.
- **Use exception filters to consolidate identical catch blocks** — When multiple `catch` blocks perform the same action (e.g. all call `CreateFailureResult` or all log the same warning), merge them with an exception filter (`catch (Exception ex) when (ex is X or Y or Z)`). This reduces code duplication without changing runtime semantics.

- Even when you need to add more context, do not wrap the original exception in a new generic [`Exception`](https://learn.microsoft.com/en-us/dotnet/api/system.exception?view=net-8.0). Log the original exception and use `throw;` so the original exception type and stack trace are preserved.

Avoid:

```csharp
catch (Exception ex)
{
    throw new Exception($"Failed while diffing '{fileRelativePath}'.", ex);
}
```

Prefer:

```csharp
catch (Exception ex)
{
    _logger.LogMessage(
        AppLogLevel.Error,
        $"An error occurred while diffing '{file1AbsolutePath}' and '{file2AbsolutePath}'.",
        shouldOutputMessageToConsole: true,
        ex);
    throw;
}
```

- The per-file detail recorded in [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) and the bool returned from `FilesAreEqualAsync(...)` must describe the same outcome. [`FolderDiffService`](../Services/FolderDiffService.cs) uses the bool return value to classify the file as `Unchanged` or `Modified`, while the report uses the detail result to show whether the reason was `SHA256Match`, `ILMismatch`, `TextMatch`, and so on. If code records `ILMismatch` but returns `true`, for example, the file would be listed under `Unchanged` while the detailed reason says mismatch, which makes the result internally inconsistent.

## Result Model and Reporting Specification

[`FileDiffResultLists`](../Models/FileDiffResultLists.cs) stores:
- Discovery lists for old/new files
- Final buckets for `Unchanged`, `Added`, `Removed`, and `Modified`
- Per-file detail results: `SHA256Match`, `ILMatch`, `TextMatch`, `SHA256Mismatch`, `ILMismatch`, `TextMismatch`
- Ignored file locations
- Timestamp-regression warnings for files whose `new` last-modified time is older than `old`
- Disassembler labels used during IL comparison
- Disassembler availability probe results (`DisassemblerAvailability`) for the report header

**Disassembler Availability table — edge cases:**
`DisassemblerHelper.ProbeAllCandidates()` is called **unconditionally** in [`DiffPipelineExecutor.ExecuteScopedRunAsync()`](../Runner/DiffPipelineExecutor.cs) before any file comparison begins, regardless of file types or the `SkipIL` setting. The probed results are stored in `FileDiffResultLists.DisassemblerAvailability` and used by both report generators.

| Scenario | Probe runs? | Table shown? | Content |
| --- | --- | --- | --- |
| Normal run with .NET assemblies | Yes | Yes | Each tool shows Yes/No + version |
| All files are text (no .dll/.exe) | Yes | Yes | Table still appears; IL comparison is simply not attempted for any file |
| `SkipIL = true` | Yes | Yes | Table still appears; IL comparison is bypassed during diff |
| No disassembler tools available | Yes | Yes | All tools show "No" (red) and "N/A" for version |
| `DisassemblerAvailability` is null or empty | N/A | No | Guard check `if (probeResults == null \|\| probeResults.Count == 0) return;` suppresses output |

In practice, `ProbeAllCandidates()` always returns a non-empty list because the candidate set is hard-coded. The null/empty guard exists for defensive safety and is covered by tests (`GenerateDiffReport_HeaderOmitsAvailabilityTable_WhenProbeResultsAreNull` / `GenerateDiffReportHtml_HeaderOmitsAvailabilityTable_WhenProbeResultsAreNull`).

The nested [`DiffSummaryStatistics`](../Models/FileDiffResultLists.cs) sealed record (`AddedCount`, `RemovedCount`, `ModifiedCount`, `UnchangedCount`, `IgnoredCount`) and the `SummaryStatistics` computed property provide a single consistent snapshot of the five bucket counts. [`ReportGenerateService`](../Services/ReportGenerateService.cs) reads `SummaryStatistics` once per report to write the summary section, so callers do not need to access each collection individually.

[`ReportGenerateService`](../Services/ReportGenerateService.cs) depends on these assumptions:
- `ResetAll()` must happen before any new run populates the instance.
- The detail-result [`Dictionary`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2?view=net-8.0) must not contain stale entries left over from a previous run.
- IL tool labels are only present for IL-based comparisons.
- Report generation reads execution results only and must not start new comparisons.
- **Table sort order**: Unchanged Files rows are sorted by diff-detail result (`SHA256Match` → `ILMatch` → `TextMatch`), then by File Path ascending. Modified Files rows (and the Timestamps Regressed warning table) are sorted by diff-detail result (`TextMismatch` → `ILMismatch` → `SHA256Mismatch`), then by Change Importance (`High` → `Medium` → `Low`), then by File Path ascending. The SHA256Mismatch warning table lists files alphabetically by path. This applies to both Markdown and HTML reports.
- **Per-section column visibility (Markdown vs HTML)**: In the Markdown report, unnecessary columns are removed outright (e.g. Added/Removed tables have 3 columns: Status, File Path, Timestamp; Ignored/SHA256Mismatch/Timestamps Regressed tables have 4 columns without Disassembler). In the HTML report, all tables retain all 8 columns in the DOM to keep cross-table column-width synchronization stable — [`syncTableWidths()`](../Services/HtmlReport/diff_report.js) calculates each table's total width from its `<colgroup>` `<col>` elements, and the resize-handle drag logic updates CSS custom properties shared across tables. Columns that should be visually hidden are marked via CSS classes on the `<table>` element (`hide-disasm`, `hide-col6`), which set `width: 0`, `visibility: hidden`, and `border-color: transparent` on the corresponding `<col>`, `<th>` (`.col-diff-hd` / `.col-disasm-hd`), and `<td>` (`.col-diff` / `.col-disasm`) elements. `syncTableWidths()` skips hidden columns when summing widths so that hidden-column tables are correctly narrower. This approach avoids the instability caused by different tables having different numbers of `<col>` elements, different `colspan` values for inline-diff rows, and conditional rendering logic in the helper methods.

<a id="guide-en-config-runtime"></a>
## Configuration and Runtime Modes

[`ConfigSettings`](../Models/ConfigSettings.cs) is the single source of truth for defaults. [`config.json`](../config.json) is an override file, so omitted keys keep the defaults defined in code, and `null` collection/path values are normalized back to those defaults. After loading, [`ConfigSettings.Validate()`](../Models/ConfigSettings.cs) checks every setting for range constraints; if any fail, [`ConfigService`](../Services/ConfigService.cs) throws [`InvalidDataException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.invaliddataexception?view=net-8.0) with a message that lists each invalid setting, and the run exits with code `3`. Validated constraints: [`MaxLogGenerations`](../Models/ConfigSettings.cs) >= `1`; [`TextDiffParallelThresholdKilobytes`](../Models/ConfigSettings.cs) >= `1`; [`TextDiffChunkSizeKilobytes`](../Models/ConfigSettings.cs) >= `1`; [`TextDiffChunkSizeKilobytes`](../Models/ConfigSettings.cs) < [`TextDiffParallelThresholdKilobytes`](../Models/ConfigSettings.cs); and [`SpinnerFrames`](../Models/ConfigSettings.cs) must contain at least one element. For key-by-key descriptions, use the [README configuration table](../Models/ConfigSettings.cs).

**JSON syntax errors** (e.g. a trailing comma after the last property or array element — a common mistake) are caught by [`ConfigService`](../Services/ConfigService.cs) before validation runs. The error is logged to the run log file and printed to the console in red, including the line number and byte position from the underlying [`JsonException`](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonexception?view=net-8.0) and a trailing-comma hint. Standard JSON does not allow trailing commas: `"Key": "value",}` is invalid — remove the final comma. The run exits with code `3`.

### Configuration groups

| Group | Keys | Purpose |
| --- | --- | --- |
| Inclusion and report shape | [`IgnoredExtensions`](../Models/ConfigSettings.cs), [`TextFileExtensions`](../Models/ConfigSettings.cs), [`ShouldIncludeUnchangedFiles`](../Models/ConfigSettings.cs), [`ShouldIncludeIgnoredFiles`](../Models/ConfigSettings.cs), [`ShouldIncludeILCacheStatsInReport`](../Models/ConfigSettings.cs), [`ShouldOutputFileTimestamps`](../Models/ConfigSettings.cs), [`ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp`](../Models/ConfigSettings.cs) | Controls scope, report verbosity, and timestamp-regression warnings. Note: [`ShouldOutputFileTimestamps`](../Models/ConfigSettings.cs) is purely supplementary — timestamps are never used in comparison logic; results (Unchanged / Modified / etc.) are determined solely by file content. |
| IL behavior | [`ShouldOutputILText`](../Models/ConfigSettings.cs), [`ShouldIgnoreILLinesContainingConfiguredStrings`](../Models/ConfigSettings.cs), [`ILIgnoreLineContainingStrings`](../Models/ConfigSettings.cs), [`SkipIL`](../Models/ConfigSettings.cs), [`DisassemblerBlacklistTtlMinutes`](../Models/ConfigSettings.cs) | Controls IL normalization, artifact output, and disassembler reliability (blacklist TTL) |
| Inline diff | [`EnableInlineDiff`](../Models/ConfigSettings.cs), [`InlineDiffContextLines`](../Models/ConfigSettings.cs), [`InlineDiffMaxDiffLines`](../Models/ConfigSettings.cs), [`InlineDiffMaxOutputLines`](../Models/ConfigSettings.cs), [`InlineDiffMaxEditDistance`](../Models/ConfigSettings.cs), [`InlineDiffLazyRender`](../Models/ConfigSettings.cs) | Controls inline diff rendering in the HTML report |
| Parallelism | [`MaxParallelism`](../Models/ConfigSettings.cs), [`TextDiffParallelThresholdKilobytes`](../Models/ConfigSettings.cs), [`TextDiffChunkSizeKilobytes`](../Models/ConfigSettings.cs), [`TextDiffParallelMemoryLimitMegabytes`](../Models/ConfigSettings.cs) | Controls CPU usage, chunk sizing, and optional memory budget for large-text comparison |
| Cache | [`EnableILCache`](../Models/ConfigSettings.cs), [`ILCacheDirectoryAbsolutePath`](../Models/ConfigSettings.cs), [`ILCacheStatsLogIntervalSeconds`](../Models/ConfigSettings.cs), [`ILCacheMaxDiskFileCount`](../Models/ConfigSettings.cs), [`ILCacheMaxDiskMegabytes`](../Models/ConfigSettings.cs), [`ILPrecomputeBatchSize`](../Models/ConfigSettings.cs) | Controls IL cache lifetime, storage, and large-tree precompute batching |
| Network-share mode | [`OptimizeForNetworkShares`](../Models/ConfigSettings.cs), [`AutoDetectNetworkShares`](../Models/ConfigSettings.cs) | Prevents high-I/O behavior on slower remote storage |
| Report output | [`ShouldGenerateHtmlReport`](../Models/ConfigSettings.cs) | Controls whether the interactive HTML review report is generated alongside the Markdown report |
| Audit log | [`ShouldGenerateAuditLog`](../Models/ConfigSettings.cs) | Controls whether a structured JSON audit log with integrity hashes is generated for tamper detection |
| Logging / UX | [`MaxLogGenerations`](../Models/ConfigSettings.cs), [`SpinnerFrames`](../Models/ConfigSettings.cs) | Controls log file retention and the console spinner animation |

Additional internal defaults:
- [`ProgramRunner`](../ProgramRunner.cs) currently applies non-configurable IL cache defaults from [`Common/Constants.cs`](../Common/Constants.cs): [`Constants.IL_CACHE_MAX_MEMORY_ENTRIES_DEFAULT`](../Common/Constants.cs) (`2000` memory entries), [`Constants.IL_CACHE_TIME_TO_LIVE_DEFAULT_HOURS`](../Common/Constants.cs) (`12` hours TTL), and [`Constants.IL_CACHE_STATS_LOG_INTERVAL_DEFAULT_SECONDS`](../Common/Constants.cs) (`60` seconds for internal stats logs). Cross-project byte/timestamp literals reused by both projects live in [`FolderDiffIL4DotNet.Core/Common/CoreConstants.cs`](../FolderDiffIL4DotNet.Core/Common/CoreConstants.cs).
- Those values are intentionally documented in code because they trade off same-day rerun reuse against unbounded memory or log growth in a short-lived console process.

### Myers diff algorithm

[`TextDiffer`](../FolderDiffIL4DotNet.Core/Text/TextDiffer.cs) implements the Myers diff algorithm (O(D² + N + M) time, O(D²) space) instead of the classical O(N×M) LCS approach. For a comprehensive explanation — including edit-graph diagrams, worked examples, complexity analysis, and implementation details — see **[Myers Diff Algorithm Guide](MYERS_DIFF_ALGORITHM.md)**.

### Inline diff skip behaviour

The inline diff can be suppressed in three ways, each producing a visible `diff-skipped` notice in the HTML report (no expand arrow):

| Trigger | Setting | Condition | Message shown |
| --- | --- | --- | --- |
| Edit distance too large | [`InlineDiffMaxEditDistance`](../Models/ConfigSettings.cs) (default `4000`) | `D` > [`InlineDiffMaxEditDistance`](../Models/ConfigSettings.cs) — too many insertions/deletions | `#N Inline diff skipped: edit distance too large (>M insertions/deletions in X vs Y lines). Increase InlineDiffMaxEditDistance in config to raise the limit.` |
| Output lines capped mid-compute | [`InlineDiffMaxOutputLines`](../Models/ConfigSettings.cs) (default `10000`) | [`TextDiffer.Compute`](../FolderDiffIL4DotNet.Core/Text/TextDiffer.cs) reached the output-line budget; a `Truncated` row is appended and the partial diff is shown | `... (diff output truncated — increase InlineDiffMaxOutputLines to see more)` |
| Diff result too large | [`InlineDiffMaxDiffLines`](../Models/ConfigSettings.cs) (default `10000`) | Total diff output (including hunk headers) exceeds the threshold *after* compute | `#N Inline diff skipped: diff too large (N diff lines; limit is M). Increase InlineDiffMaxDiffLines in config to enable.` |

The edit-distance-exceeded and single-Truncated cases both render as a plain row (no `<details>` element), so the notice is visible without any click. The [`InlineDiffMaxOutputLines`](../Models/ConfigSettings.cs) truncation renders *inside* the `<details>` block after a partial diff.

> **ILMismatch entries** also require `ShouldOutputILText: true` (the default). [`HtmlReportGenerateService`](../Services/HtmlReportGenerateService.cs) reads IL text directly from the `*_IL.txt` files produced by [`ILTextOutputService`](../Services/ILOutput/ILTextOutputService.cs) (under `Reports/<label>/IL/old` and `Reports/<label>/IL/new`). If [`ShouldOutputILText`](../Models/ConfigSettings.cs) is `false`, those files are not written and the inline diff is silently omitted — no `diff-skipped` notice is shown.

### Runtime mode resolution

```mermaid
flowchart TD
    A["Load config"] --> B{"OptimizeForNetworkShares?"}
    B -- "Yes" --> E["Network-optimized mode"]
    B -- "No" --> C{"AutoDetectNetworkShares enabled?"}
    C -- "No" --> D["Local-optimized mode"]
    C -- "Yes" --> F{"old or new looks like network path?"}
    F -- "Yes" --> E
    F -- "No" --> D
```

Network path detection is implemented in [`NetworkPathDetector`](../FolderDiffIL4DotNet.Core/IO/NetworkPathDetector.cs) (extracted from `FileSystemUtility`). It recognizes `\\`-prefixed UNC paths, `\\?\UNC\`-prefixed device paths, and `//`-prefixed forward-slash UNC paths (including IP-based forms such as `//192.168.1.1/share`). `FileSystemUtility.IsLikelyNetworkPath` delegates to `NetworkPathDetector.IsLikelyNetworkPath`.

Practical effect of network-optimized mode:
- Skip IL cache precompute and prefetch.
- Cap auto-selected parallelism at `min(logicalProcessorCount, 8)`.
- Avoid parallel text chunk reads and prefer sequential text comparison.
- Preserve behavior correctness while reducing remote I/O amplification.

<a id="guide-en-performance-runtime"></a>
## Performance and Runtime Modes

Key performance features:
- Parallel file comparison in [`FolderDiffService`](../Services/FolderDiffService.cs)
- Optional IL cache warmup and disk persistence
- Chunk-parallel text comparison for large local text files
- Optional memory-budget-aware throttling for chunk-parallel text comparison
- Batched IL precompute target enumeration for very large folder trees
- Tool failure blacklist inside disassembler flow
- Progress keep-alive while long-running precompute is in flight

When to be careful:
- Changing default parallelism changes both throughput and I/O pressure.
- Cache key shape must remain stable across tool-version changes.
- Over-eager prefetching can regress NAS/SMB scenarios.
- Large text-file behavior depends on threshold, chunk size, and optional memory budget; they should be tuned together.

<a id="guide-en-doc-site"></a>
## Documentation Site and API Reference

DocFX is used as the API-reference generator and site builder.

Inputs:
- XML documentation comments emitted during `dotnet build`
- [`README.md`](../README.md), this guide, and [`doc/TESTING_GUIDE.md`](TESTING_GUIDE.md)
- [`docfx.json`](../docfx.json), [`index.md`](../index.md), [`toc.yml`](../toc.yml), and [`api/index.md`](../api/index.md)

Outputs:
- `_site/`: generated documentation site
- `api/*.yml` and [`api/toc.yml`](../api/toc.yml): generated API metadata consumed by the site build

Expected refresh sequence:
1. Build the solution so the latest XML documentation file exists.
2. Run `docfx metadata docfx.json`.
3. Run `docfx build docfx.json`.
4. Inspect `_site/index.html` or the CI artifact before merging larger API changes.

Guardrails:
- If you rename public namespaces or move public types, regenerate DocFX output in the same change.
- If you add public surface area, keep XML comments current so the generated API reference stays useful.
- `_site/` and generated `api/*.yml` files are build outputs and should not be committed.

<a id="guide-en-ci-release"></a>
## CI and Release Notes

### Workflow Overview

```
On pull_request:
  ├─ dotnet.yml (build)             → Build + Test + Coverage enforcement
  ├─ dotnet.yml (mutation-testing)  → Stryker mutation testing
  ├─ dotnet.yml (test-windows)     → Windows cross-platform verification
  ├─ benchmark-regression.yml       → Performance regression detection
  └─ codeql.yml                     → Security static analysis (C# + Actions)

On push to main:
  ├─ dotnet.yml (build)             → Build + Test + Coverage enforcement
  ├─ dotnet.yml (test-windows)     → Windows cross-platform verification
  ├─ benchmark-regression.yml       → Performance regression detection + baseline update
  └─ codeql.yml                     → Security static analysis

On v* tag push:
  └─ release.yml                    → Build + Test + Publish + GitHub Release creation
```

Quality is guarded across six axes: **correctness** (tests), **coverage** (line/branch thresholds), **detection strength** (mutation testing), **performance** (benchmark regression), **security** (CodeQL), and **compatibility** (Windows).

Workflow/config files:
- [.github/workflows/dotnet.yml](../.github/workflows/dotnet.yml)
- [.github/workflows/release.yml](../.github/workflows/release.yml)
- [.github/workflows/codeql.yml](../.github/workflows/codeql.yml)
- [.github/workflows/benchmark-regression.yml](../.github/workflows/benchmark-regression.yml)
- [.github/dependabot.yml](../.github/dependabot.yml)

Current CI behavior (`build` job — Ubuntu):
- Runs on `push` and `pull_request` targeting `main`, plus `workflow_dispatch`
- Uses [`global.json`](../global.json) through `actions/setup-dotnet`
- Restores and builds `FolderDiffIL4DotNet.sln`
- Installs DocFX, generates the documentation site, and uploads it as `DocumentationSite`
- Installs a real [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) tool and runs tests with `DOTNET_ROLL_FORWARD=Major` so the preferred disassembler path is exercised in CI as well
- Runs tests and coverage only when the test project exists
- Generates coverage summary with `reportgenerator`
- Enforces total coverage thresholds of `80%` line and `75%` branch from the generated Cobertura XML, plus per-class thresholds of `90%` line and `85%` branch for core diff classes (`FileDiffService`, `FolderDiffService`, `FileComparisonService`)
- Publishes build output and uploads it as `FolderDiffIL4DotNet`
- Uploads TRX and coverage files as `TestAndCoverage`

`test-windows` job — Windows:
- Runs in parallel with `build` on `windows-latest`
- Restores, builds, installs [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/), and runs the full test suite with `DOTNET_ROLL_FORWARD=Major`
- Ensures Windows-specific code paths are exercised under CI as well

`mutation-testing` job — Stryker:
- Runs on `pull_request` and `workflow_dispatch` only (not on push to `main`)
- Uses [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) to inject mutations into production code and verify tests detect them
- Configuration is in [`stryker-config.json`](../stryker-config.json)
- Posts the mutation score to the GitHub Actions job summary
- Uploads the full Stryker HTML/JSON report as `StrykerReport`
- Break threshold is `50%` — the job fails if the mutation score falls below this

`benchmark` job (manual only):
- Runs only on `workflow_dispatch`
- Executes [BenchmarkDotNet](https://benchmarkdotnet.org/) benchmarks from `FolderDiffIL4DotNet.Benchmarks` and uploads results as `BenchmarkResults`
- Exports JSON and GitHub-flavored results for manual comparison

Release automation:
- [`.github/workflows/release.yml`](../.github/workflows/release.yml) runs for pushed `v*` tags and manual dispatch with an explicit existing tag input
- Rebuilds, reruns coverage-gated tests, regenerates DocFX output, publishes the app, and removes `*.pdb`
- Creates zipped publish/docs artifacts plus SHA-256 checksum files
- Creates a GitHub Release from the existing tag with generated release notes

Security automation:
- [`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml) analyzes both `csharp` and `actions` on `push`, `pull_request`, weekly schedule, and `workflow_dispatch`
- The Checkout step uses `fetch-depth: 0` so [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) can compute version height from the full commit graph during the `csharp` autobuild
- The Analyze step uses `continue-on-error: true` to tolerate the SARIF upload rejection that occurs when the repository's GitHub Default Setup code scanning is also active for the `actions` language
- [`.github/dependabot.yml`](../.github/dependabot.yml) opens weekly update PRs for both `nuget` dependencies and GitHub Actions
- [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) protects the expected CI/release/security file presence and key settings from accidental removal

Performance regression detection:
- [`.github/workflows/benchmark-regression.yml`](../.github/workflows/benchmark-regression.yml) runs BenchmarkDotNet on every `pull_request` and `push` to `main`, plus `workflow_dispatch`
- Combines JSON results from all benchmark classes into a single report and compares against the stored baseline in the `gh-benchmarks` branch using [`benchmark-action/github-action-benchmark@v1`](https://github.com/benchmark-action/github-action-benchmark)
- Alert threshold is `150%` (50% degradation causes failure); PR comments are posted on regression
- On push to `main`, results are auto-pushed to `gh-benchmarks` as the new baseline
- Benchmark artifacts are always uploaded as `BenchmarkResults`

Versioning:
- [`version.json`](../version.json) uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)
- Informational version is embedded and later included in the generated report

<a id="guide-en-skipped-tests"></a>
## Skipped Tests in Local Runs

Some tests report as **Skipped** when run locally. This is intentional and does not indicate a bug.

Which tests skip and why:
- **[`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs)** (six tests) — these exercise fallback and blacklist logic using fake `#!/bin/sh` shell scripts created by [`WriteExecutable`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs). [`File.SetUnixFileMode`](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.setunixfilemode?view=net-8.0) and shell script execution are not available on Windows, so the tests call `Skip.If(OperatingSystem.IsWindows(), ...)` and report Skipped there.
- **[`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs)** (one test) — this builds the same tiny class library twice with `Deterministic=false` and verifies that [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) produces `ILMatch` after MVID filtering. It calls `Skip.IfNot(IsE2EEnabled(), ...)` and `Skip.If(!CanRunDotNetIldasm(), ...)`, so it reports Skipped unless both `FOLDERDIFF_RUN_E2E=true` and a working [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) (or [`dotnet ildasm`](https://www.nuget.org/packages/dotnet-ildasm/)) are available.

Why this is safe:
- CI runs on both Linux (`build` job) and Windows (`test-windows` job), and both install a real [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) before the test step. That ensures the preferred disassembler path and Windows-specific code paths are exercised in CI. The real-disassembler E2E test still additionally requires `FOLDERDIFF_RUN_E2E=true`, so a local Skipped result can mean either a missing prerequisite or an opt-in that was not enabled.
- The skippable tests use [`[SkippableFact]`](https://github.com/AArnott/Xunit.SkippableFact) from [`Xunit.SkippableFact`](https://www.nuget.org/packages/Xunit.SkippableFact/), so the runner counts them as Skipped rather than Passed, making the distinction visible.
- If a previously Skipped test appears as **Failed**, that is a real issue and should be investigated. Skipped and Failed are distinct outcomes.

For the complete list of affected tests and the `Skip.If` pattern, see [doc/TESTING_GUIDE.md](TESTING_GUIDE.md#testing-en-isolation).

## Extension Points

Typical safe extension points:
- Add new text extensions in [`TextFileExtensions`](../Models/ConfigSettings.cs)
- Introduce new report metadata in [`ReportGenerateService`](../Services/ReportGenerateService.cs)
- Add logging around orchestration boundaries
- Add new tests by substituting [`IFileSystemService`](../Services/IFileSystemService.cs), [`IFolderDiffExecutionStrategy`](../Services/IFolderDiffExecutionStrategy.cs), [`IFileComparisonService`](../Services/IFileComparisonService.cs), [`IFileDiffService`](../Services/IFileDiffService.cs), [`IILOutputService`](../Services/IILOutputService.cs), or [`IDotNetDisassembleService`](../Services/IDotNetDisassembleService.cs)

Higher-risk changes:
- Altering the order `SHA256 -> IL -> text`
- Reusing run-scoped state across executions
- Moving path decisions out of [`DiffExecutionContext`](../Services/DiffExecutionContext.cs)
- Mixing tool identities during IL comparison
- Introducing static mutable caches without isolation

<a id="guide-en-change-checklist"></a>
## Change Checklist

Before merging behavior changes, check:
1. Does [`Program.cs`](../Program.cs) remain thin, with orchestration still in [`ProgramRunner`](../ProgramRunner.cs) or lower services?
2. Does each run still get a fresh [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) and [`FileDiffResultLists`](../Models/FileDiffResultLists.cs)?
3. Are new collaborators injected rather than created ad hoc inside core services?
4. Does [`FolderDiffService`](../Services/FolderDiffService.cs) still call `ResetAll()` before enumeration and classification?
5. Is the reporting specification still consistent with the contents of [`FileDiffResultLists`](../Models/FileDiffResultLists.cs)?
6. If IL behavior changed, are same-tool enforcement and ignore-line semantics still explicit?
7. If performance behavior changed, have local and network-share modes both been considered?
8. Did [`README.md`](../README.md), this guide, and [`doc/TESTING_GUIDE.md`](TESTING_GUIDE.md) stay in sync with user-visible behavior?
9. Were tests added or updated for the changed execution path?
10. If CI, release, or security assumptions changed, were [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml), [`.github/workflows/release.yml`](../.github/workflows/release.yml), [`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml), [`.github/dependabot.yml`](../.github/dependabot.yml), and [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) updated together?

## Cross-Platform Pitfalls

This project runs CI on both Linux and Windows. The following patterns have caused real CI failures and should be kept in mind when writing production code or tests.

### Path separator consistency

On Windows, `Path.GetRelativePath` normalizes output to `\`, but `Path.Combine` does **not** normalize the second argument's separators. This means a round-trip like:

```csharp
var rel = Path.GetRelativePath(baseDir, absolutePath);   // "sub\file.txt" on Windows
var rebuilt = Path.Combine(otherBase, rel);               // "/other\sub\file.txt"
```

produces a different string from the original `Path.Combine(otherBase, "sub/file.txt")` → `"/other\sub/file.txt"` (mixed separators). `OrdinalIgnoreCase` string comparison treats `\` and `/` as different characters, so `HashSet<string>` lookups fail silently.

**Rule**: When constructing relative paths that contain subdirectory separators, always use `Path.Combine("sub", "file.txt")` instead of `"sub/file.txt"`. In production code, avoid comparing raw `Path.Combine` output against `Path.GetRelativePath` output without normalization.

### Timer resolution and timing-sensitive tests

On Windows, `DateTime.UtcNow` and `Thread.Sleep` interact with the OS timer resolution (~15.6ms by default). A test that sets a TTL of 1ms and sleeps 20ms can fail because:

1. `RegisterFailure()` records `DateTime.UtcNow` at time T.
2. The test calls `Assert.True(IsBlacklisted(...))` — but if the code path from `RegisterFailure` to `IsBlacklisted` takes > 1ms (easily possible on a loaded CI runner), the TTL has already expired and the assertion fails.

**Rule**: Use TTL values of at least 500ms and sleep durations of at least 1.4× the TTL in timing-sensitive tests. Avoid sub-millisecond TTLs entirely.

### `WebUtility.HtmlEncode` does not encode backticks

`System.Net.WebUtility.HtmlEncode` encodes `&`, `<`, `>`, `"`, `'` but does **not** encode backtick (`` ` ``). Since the HTML report embeds file paths in JavaScript contexts, backticks must be explicitly encoded to prevent template-literal injection. The `HtmlEncode()` helper in `HtmlReportGenerateService.Helpers.cs` adds `.Replace("`", "&#96;")` as a post-processing step.

### Local tool versions (`dotnet-stryker`, etc.)

The CI workflow runs `dotnet tool restore` using [`.config/dotnet-tools.json`](../.config/dotnet-tools.json). If a pinned version is removed from NuGet, CI fails at the restore step. Always verify that tool versions exist on NuGet before updating the manifest.

### Thread safety in test fakes

When a test fake (mock service) records method calls in a collection (e.g. `ReadChunkCalls.Add(...)`), use `ConcurrentBag<T>` or `ConcurrentQueue<T>` instead of `List<T>` if the fake is invoked from `Parallel.ForEachAsync` or other parallel contexts. A non-thread-safe `List.Add` under concurrency can throw exceptions that are silently caught by production error-handling code, causing the test to follow an unexpected fallback path and fail intermittently.

### `coverlet.collector` and `coverlet.runsettings` compatibility

- `coverlet.collector` 6.0.3+ has a [regression](https://github.com/coverlet-coverage/coverlet/issues/1726) where `<Exclude>` / `<Include>` filters in `coverlet.runsettings` cause the `coverage.cobertura.xml` file to not be generated. Use version 6.0.2 until a fix is released.
- The `opencover` format does not support `<DeterministicReport>true</DeterministicReport>`. If deterministic reports are needed, use `cobertura` only.

## Debugging Tips

- Start with `Logs/log_YYYYMMDD.log` for the exact failure point.
- If the run stops during IL comparison, inspect the chosen disassembler label in logs and report output.
- For unexpected network-mode behavior, verify both config flags and detected path classification.
- When a result bucket looks wrong, inspect [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) population order before touching report formatting.
- If a test becomes order-dependent, suspect leaked run-scoped state first.
- If the banner or any console output shows `?` characters on Windows, the process is using the OEM code page. [`Program.cs`](../Program.cs) sets [`Console.OutputEncoding`](https://learn.microsoft.com/en-us/DOTNET/api/system.console.outputencoding?view=net-8.0) = `Encoding.UTF8` at the very start of `Main()` — before any output — to override this. On Linux and macOS the console is already UTF-8, so the assignment is effectively a no-op on those platforms.

## HTML Report: Integrity Verification Technical Notes

### Dual-hash placeholder approach

The "Download as reviewed" workflow embeds **two** SHA256 hashes inside the reviewed HTML file using a placeholder technique. This solves a circular dependency: the hash of the file cannot be known until the file is complete, but the hash must be embedded inside the file.

| Constant | Placeholder | Purpose |
| --- | --- | --- |
| `__reviewedSha256__` | 64 zeros (`000...0`) | Intermediate hash — hash of the HTML with this field set to the placeholder. Used internally during the hashing process. |
| `__finalSha256__` | 64 f's (`fff...f`) | Final hash — hash of the HTML with `__reviewedSha256__` already embedded and this field set to the placeholder. Matches the companion `.sha256` file exactly. |

The two-step process in `downloadReviewed()`:
1. Replace `__reviewedSha256__` placeholder with zeros → compute SHA256 → replace zeros with actual hash (first hash embedded).
2. Replace `__finalSha256__` placeholder with f's → compute SHA256 → replace f's with actual hash (second hash embedded). This final hash is also written to the companion `.sha256` file.

### Verify integrity: `.sha256`-only verification

`verifyIntegrity()` only accepts `.sha256` files. The reviewed HTML is "self" — it already has its own final hash embedded in `__finalSha256__`, so no HTML file selection is needed. The function reads the `.sha256` file, extracts the hash, and compares it directly against the embedded `__finalSha256__` constant.

### Browser quirk: `input.accept` on dynamically created elements

Some browsers (notably macOS Safari) ignore the `accept` attribute on `<input type="file">` elements that are created dynamically and clicked immediately. The file picker opens with no filter, allowing all files to be selected.

**Workaround**: Pre-create the hidden `<input type="file" accept=".sha256">` element during `DOMContentLoaded` initialization and reuse it in `verifyIntegrity()`. By the time the user clicks "Verify integrity", the input element has been in the DOM long enough for the browser to recognize and apply the `accept` filter. An `onchange` guard (`file.name.endsWith('.sha256')`) is also present as a fallback for browsers that still bypass the filter.

### Type name format in semantic changes

[`SimpleSignatureTypeProvider`](../Services/AssemblyMethodAnalyzer.cs) always outputs **fully qualified .NET type names** (e.g. `System.String`, `System.Int32`, `System.Void`), never C# aliases (`string`, `int`, `void`). Generic type parameters are resolved to their declared names (e.g. `T`, `TKey`, `TValue`) via [`GenericContext`](../Services/AssemblyMethodAnalyzer.MetadataHelpers.cs), which reads parameter names from `TypeDefinition.GetGenericParameters()` and `MethodDefinition.GetGenericParameters()`. Function pointer signatures are expanded as `delegate*<ParamTypes, ReturnType>`, and custom modifiers are preserved as `modreq()`/`modopt()` annotations. The `MemberType`, `ReturnType`, and `Parameters` fields in [`MemberChangeEntry`](../Models/MemberChangeEntry.cs) follow this convention. Sample HTML base64 blocks must use fully qualified names to match.

---

# 開発者ガイド

このガイドは、実行時挙動の変更、差分パイプラインの拡張、CI とテストの整合維持を行うメンテナ向けの資料です。

関連ドキュメント:
- [README.md](../README.md#readme-ja-doc-map): 製品概要、導入、使い方、設定リファレンス
- [doc/TESTING_GUIDE.md](TESTING_GUIDE.md#testing-ja-run-tests): テスト戦略、ローカル実行コマンド、分離ルール
- [api/index.md](../api/index.md): 自動生成 API リファレンスの入口
- [docfx.json](../docfx.json): DocFX のメタデータ/ビルド設定
- [.github/workflows/dotnet.yml](../.github/workflows/dotnet.yml): CI パイプライン定義
- [SECURITY.md](../SECURITY.md): 脅威モデル、STRIDE 分析、セキュリティ対策
- [doc/PERFORMANCE_GUIDE.md](PERFORMANCE_GUIDE.md#perf-ja-memory): メモリ管理、ベンチマークベースライン、チューニング推奨

<a id="guide-ja-map"></a>
## ドキュメントの見取り図

| やりたいこと | 最初に見る場所 |
| --- | --- |
| 実行全体の流れを把握したい | [実行ライフサイクル](#guide-ja-execution-lifecycle) |
| サービス境界や DI スコープを追いたい | [Dependency Injection 構成](#guide-ja-di-layout) |
| ファイル判定ロジックを変更したい | [比較パイプライン](#guide-ja-comparison-pipeline) |
| 設定キーや実行モード判定を理解したい | [設定と実行モード](#guide-ja-config-runtime) |
| 性能やネットワーク共有向け挙動を調整したい | [性能と実行モード](#guide-ja-performance-runtime) |
| 自動生成 API リファレンスを更新したい | [ドキュメントサイトと API リファレンス](#guide-ja-doc-site) |
| ビルド・テスト・成果物の流れを変えたい | [CI とリリースまわり](#guide-ja-ci-release) |
| 安全に機能追加したい | [変更時チェックリスト](#guide-ja-change-checklist) |

## ローカル開発

前提:
- [`.NET SDK 8.x`](https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0)（使用バージョンは [`global.json`](../global.json) で固定）
- `PATH` 上で利用可能な IL 逆アセンブラ
  - 優先は [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) または [`dotnet ildasm`](https://www.nuget.org/packages/dotnet-ildasm/)
  - フォールバックとして [`ilspycmd`](https://www.nuget.org/packages/ilspycmd/) をサポート

よく使うコマンド:

```bash
dotnet restore FolderDiffIL4DotNet.sln
dotnet build FolderDiffIL4DotNet.sln --configuration Release
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false
```

ドキュメントサイトのローカル更新:

```bash
dotnet tool update --global docfx --version '2.*'
export PATH="$PATH:$HOME/.dotnet/tools"
docfx metadata docfx.json
docfx build docfx.json
```

ローカル実行例:

```bash
dotnet run -- "/absolute/path/to/old" "/absolute/path/to/new" "dev-run" --no-pause

# ヘルプ / バージョン確認
dotnet run -- --help
dotnet run -- --version

# 有効な設定を JSON で確認（環境変数オーバーライド適用済み）
dotnet run -- --print-config
dotnet run -- --print-config --config /etc/cfg.json

# スレッド数指定・IL スキップ・カスタム設定ファイル
dotnet run -- "/path/old" "/path/new" "label" --threads 4 --skip-il --config /etc/cfg.json --no-pause
```

実行時に生成される主な成果物は [README § 生成物](../README.md#readme-ja-generated-artifacts) を参照してください。上記レポートファイルに加え、[`EnableILCache`](../Models/ConfigSettings.cs) が `true` かつ [`ILCacheDirectoryAbsolutePath`](../Models/ConfigSettings.cs) 未指定時は OS 標準のユーザーローカルデータディレクトリ配下に `ILCache/` ディレクトリが作成されます（Windows: `%LOCALAPPDATA%\FolderDiffIL4DotNet\ILCache`、macOS/Linux: `~/.local/share/FolderDiffIL4DotNet/ILCache`）。

## Partial Class ファイル構成

大規模なサービスクラスを partial class ファイルに分割し、各ファイルを単一責務にまとめています。クラス名・名前空間は変更なし — ファイル配置のみが異なります。

| クラス | メインファイル | Partial ファイル |
| --- | --- | --- |
| `ProgramRunner` | [`ProgramRunner.cs`](../ProgramRunner.cs) | [`Runner/ProgramRunner.Types.cs`](../Runner/ProgramRunner.Types.cs)（ネスト型: `RunArguments`, `RunCompletionState`, `ProgramExitCode`, `ProgramRunResult`, `StepResult<T>`）、[`Runner/ProgramRunner.HelpText.cs`](../Runner/ProgramRunner.HelpText.cs)（CLI ヘルプメッセージ）、[`Runner/ProgramRunner.Config.cs`](../Runner/ProgramRunner.Config.cs)（設定読込・バリデーション・CLI オーバーライド） |
| `ConfigSettings` | [`Models/ConfigSettings.cs`](../Models/ConfigSettings.cs) | [`Models/ConfigSettings.ReportSettings.cs`](../Models/ConfigSettings.ReportSettings.cs)（レポート出力制御）、[`Models/ConfigSettings.ILSettings.cs`](../Models/ConfigSettings.ILSettings.cs)（IL 比較・キャッシュ・逆アセンブラ）、[`Models/ConfigSettings.DiffSettings.cs`](../Models/ConfigSettings.DiffSettings.cs)（並列処理・ネットワーク・インライン差分） |
| `HtmlReportGenerateService` | [`Services/HtmlReportGenerateService.cs`](../Services/HtmlReportGenerateService.cs) | [`Services/HtmlReport/HtmlReportGenerateService.Sections.cs`](../Services/HtmlReport/HtmlReportGenerateService.Sections.cs)（レポートセクション生成）、[`…DetailRows.cs`](../Services/HtmlReport/HtmlReportGenerateService.DetailRows.cs)（インライン差分、セマンティック変更、依存関係変更の詳細行）、[`…Helpers.cs`](../Services/HtmlReport/HtmlReportGenerateService.Helpers.cs), [`…Css.cs`](../Services/HtmlReport/HtmlReportGenerateService.Css.cs) ([`diff_report.css`](../Services/HtmlReport/diff_report.css) 埋め込みリソースを読み込み), [`…Js.cs`](../Services/HtmlReport/HtmlReportGenerateService.Js.cs) ([`Services/HtmlReport/js/`](../Services/HtmlReport/js/) の8つの JS モジュールを結合し、プレースホルダー置換して読み込み) |
| `FolderDiffService` | [`Services/FolderDiffService.cs`](../Services/FolderDiffService.cs) | [`Services/FolderDiffService.ILPrecompute.cs`](../Services/FolderDiffService.ILPrecompute.cs), [`…DiffClassification.cs`](../Services/FolderDiffService.DiffClassification.cs) |
| `ReportGenerateService` | [`Services/ReportGenerateService.cs`](../Services/ReportGenerateService.cs) | [`Services/SectionWriters/HeaderSectionWriter.cs`](../Services/SectionWriters/HeaderSectionWriter.cs), [`…LegendSectionWriter.cs`](../Services/SectionWriters/LegendSectionWriter.cs), [`…IgnoredFilesSectionWriter.cs`](../Services/SectionWriters/IgnoredFilesSectionWriter.cs), [`…UnchangedFilesSectionWriter.cs`](../Services/SectionWriters/UnchangedFilesSectionWriter.cs), [`…AddedFilesSectionWriter.cs`](../Services/SectionWriters/AddedFilesSectionWriter.cs), [`…RemovedFilesSectionWriter.cs`](../Services/SectionWriters/RemovedFilesSectionWriter.cs), [`…ModifiedFilesSectionWriter.cs`](../Services/SectionWriters/ModifiedFilesSectionWriter.cs), [`…SummarySectionWriter.cs`](../Services/SectionWriters/SummarySectionWriter.cs), [`…ILCacheStatsSectionWriter.cs`](../Services/SectionWriters/ILCacheStatsSectionWriter.cs), [`…WarningsSectionWriter.cs`](../Services/SectionWriters/WarningsSectionWriter.cs) |
| `AssemblyMethodAnalyzer` | [`Services/AssemblyMethodAnalyzer.cs`](../Services/AssemblyMethodAnalyzer.cs) | [`Services/AssemblyMethodAnalyzer.Comparers.cs`](../Services/AssemblyMethodAnalyzer.Comparers.cs)（型/メソッド/プロパティ/フィールド比較）、[`…MetadataHelpers.cs`](../Services/AssemblyMethodAnalyzer.MetadataHelpers.cs)（スナップショット構築、シグネチャ構築）、[`…AccessHelpers.cs`](../Services/AssemblyMethodAnalyzer.AccessHelpers.cs)（アクセス修飾子抽出、型種別判定、IL バイト読み取り）、[`…SignatureProvider.cs`](../Services/AssemblyMethodAnalyzer.SignatureProvider.cs)（ジェネリックコンテキスト、シグネチャ型プロバイダ） |
| `DepsJsonAnalyzer` | [`Services/DepsJsonAnalyzer.cs`](../Services/DepsJsonAnalyzer.cs) | （単一ファイル）`.deps.json` ファイルの構造化された依存関係変更分析 |
| `NuGetVulnerabilityService` | [`Services/NuGetVulnerabilityService.cs`](../Services/NuGetVulnerabilityService.cs) | （単一ファイル）NuGet V3 脆弱性データの取得とパッケージバージョンの既知アドバイザリ突合 |
| `NuGetVersionRange` | [`Services/NuGetVersionRange.cs`](../Services/NuGetVersionRange.cs) | （単一ファイル）NuGet バージョン範囲の区間記法パーサ |
| `FileDiffService` | [`Services/FileDiffService.cs`](../Services/FileDiffService.cs) | [`Services/FileDiffService.TextComparison.cs`](../Services/FileDiffService.TextComparison.cs)（逐次/チャンク並列テキスト比較、メモリ予算考慮の並列度制御） |
| `DotNetDisassembleService` | [`Services/DotNetDisassembleService.cs`](../Services/DotNetDisassembleService.cs) | [`Services/DotNetDisassembleService.VersionLabel.cs`](../Services/DotNetDisassembleService.VersionLabel.cs)（バージョン/ラベル管理、ツールフィンガープリント、プロセス実行、使用記録）, [`Services/DotNetDisassembleService.Streaming.cs`](../Services/DotNetDisassembleService.Streaming.cs)（行単位ストリーミング逆アセンブル、LOH 文字列割り当て回避） |

## Nullable 参照型

[`FolderDiffIL4DotNet.csproj`](../FolderDiffIL4DotNet.csproj) と [`FolderDiffIL4DotNet.Core.csproj`](../FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj) の両方で `<Nullable>enable</Nullable>` と `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` を有効にしています。nullable 警告（CS8600–8604, CS8618, CS8625）はすべて強制されており、`<NoWarn>` による抑制はありません。

### アノテーション規約

| パターン | 使用場面 | 例 |
| --- | --- | --- |
| `string?` 戻り値型 | ミス/失敗時に `null` を返すメソッド | `string? TryGetPathRoot(...)` |
| `string? param = null` | 省略可能なパラメータ | `ValidateFolderNameOrThrow(string folderName, string? paramName = null)` |
| `out string? param` | 失敗パスで `null` が代入される `out` パラメータ | `TryGetFileSystemInfoOnMac(string path, out string? fsType, out uint flags)` |
| `TValue?` ジェネリック型のプロパティ | `IsSuccess` が false のとき `default` になる値 | `StepResult<TValue>.Value` |
| `= null!`（`init` プロパティ） | コンパイラが初期化を検証できないコンテキスト/DTO の必須 init プロパティ | `ReportWriteContext.OldFolderAbsolutePath { get; init; } = null!;` |
| `ILCache?` フィールド / パラメータ | DI 経由で注入される nullable サービス（機能無効時に null） | `private readonly ILCache? _ilCache;` |

### 新規コードのガイドライン

- **必ずアノテーションすること** — nullable コード向けの新しい `<NoWarn>` を追加しないでください。コンパイラが警告を出す場合は、アノテーションを修正するか null チェックを追加してください。
- **`null!` より `?` を優先** — `null!` はオブジェクト初期化子で必ず設定される `init` 専用プロパティにのみ使用します。それ以外は `?` で null 可能性を正直に表現してください。
- **`ArgumentNullException.ThrowIfNull()`** を使用 — public/internal API 境界で非 null 必須のパラメータに適用します。
- **参照前にガード** — `T?` を返す `Try*` メソッドの呼び出し後は、結果を使用する前に `null` チェックしてください。
- **テストプロジェクトは対象外** — [`FolderDiffIL4DotNet.Tests.csproj`](../FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj) では `<Nullable>` を有効にしていません。テストダブルやモックセットアップに過度なアノテーションが必要になり、安全性の利点が小さいためです。

## Railway 指向実行パイプライン

`ProgramRunner.RunWithResultAsync` は [`StepResult<T>`](../Runner/ProgramRunner.Types.cs) 型を基盤とした Railway 指向パイプラインを使用しています。各実行フェーズは `StepResult` を返し、`Bind`（同期）と `BindAsync`（非同期）でチェーンされます。失敗時は後続のステップが自動的にショートサーキットされ、明示的な `if (!IsSuccess) return Failure` チェックは不要です。

```
TryValidateAndBuildRunArguments
  .Bind → TryPrepareReportsDirectory
  .BindAsync → TryLoadConfigBuilderAsync
    .Bind → ApplyCliOverrides + TryBuildConfig
  .BindAsync → TryExecuteRunAsync
```

新しい実行フェーズを追加する場合は、成功時に `StepResult<T>.FromValue(value)` でラップし、失敗時は `StepResult<T>.FromFailure(ProgramRunResult.Failure(exitCode))` を返してから `.Bind()`/`.BindAsync()` でチェーンしてください。

## XML ドキュメント強制

[`FolderDiffIL4DotNet.csproj`](../FolderDiffIL4DotNet.csproj) と [`FolderDiffIL4DotNet.Core.csproj`](../FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj) の両方で `<GenerateDocumentationFile>true</GenerateDocumentationFile>` と `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` を有効にしています。CS1591（XML コメント欠落）と CS1573（param タグ不一致）の抑制は削除されたため、`<summary>` / `<param>` / `<returns>` タグのない新しい public 型・メソッド・プロパティ・パラメータはビルド失敗となります。バイリンガル XML ドキュメントコメント（英語が先、次に日本語）がプロジェクトの規約です。

## HTML レポートセキュリティ

生成される `diff_report.html` は 2 層の XSS 緩和策を適用しています:

1. **HTML エンコーディング** — すべてのユーザー提供データ（ファイルパス、タイムスタンプ、バージョン文字列、逆アセンブラ出力）は [`HtmlReportGenerateService.Helpers.cs`](../Services/HtmlReport/HtmlReportGenerateService.Helpers.cs) の `System.Net.WebUtility.HtmlEncode` でエンコードされ、さらにバッククォートの明示的な置換（`` ` `` → `&#96;`）が行われます。`WebUtility.HtmlEncode` はバッククォートをエンコードしないため、埋め込み JavaScript コンテキストでのテンプレートリテラル注入を防ぐ追加ステップです。

2. **Content-Security-Policy** — `<head>` 内の `<meta http-equiv="Content-Security-Policy">` タグが実行環境を制限: `default-src 'none'` ですべてをブロックし、`style-src 'unsafe-inline'` と `script-src 'unsafe-inline'` でレポート自身のインラインスタイル/スクリプトのみを許可、`img-src 'self'` で同一オリジン画像のみを許可。外部スクリプト・スタイルシート・フォント・フレーム・フォームターゲットの読み込みを防止。

HTML レポート出力を変更する際は、新しい動的データを必ず `HtmlEncode()` で処理し、[`HtmlReportGenerateService.cs`](../Services/HtmlReportGenerateService.cs) の `AppendHtmlHead()` に CSP メタタグが残っていることを確認してください。[`doc/samples/diff_report.html`](samples/diff_report.html) のサンプルレポートも同期を維持する必要があります。

## HTML レポートフィルタリング

HTML レポートには、複数の条件でファイル行を絞り込めるクライアントサイドフィルターゾーンが含まれています。フィルターゾーンは reviewed HTML にも引き継がれ、レビュアーもフィルタ可能です。

### サーバーサイド（C#）

- [`AppendFileRow()`](../Services/HtmlReport/HtmlReportGenerateService.Helpers.cs) が各 `<tr>` に `data-section`、`data-diff`、（該当する場合）`data-importance` 属性を出力。
- **ボタン行**（Download as reviewed、Fold all details、Reset filters、Clear all）は [`HtmlReportGenerateService.cs`](../Services/HtmlReportGenerateService.cs) の `<!--CTRL-->...<!--/CTRL-->` マーカー内にあり、`downloadReviewed()` で reviewed バナーに置換される。reviewed バナーには Verify integrity、Download as Excel-compatible HTML、Fold all details、Reset filters ボタンが含まれる。
- **フィルターゾーン**（Diff Detail、Change Importance、Unchecked only、Search）は CTRL マーカーの**外**に配置され reviewed HTML にも残る。

### CSS（[`diff_report.css`](../Services/HtmlReport/diff_report.css)）

- **CSS カスタムプロパティ（`:root` 変数）** — 60 以上のカラー/サーフェス/ボーダートークンを `:root` に定義。スタイルシート全体でハードコード hex 値の代わりに `var(--color-*)` を使用。テーマ切替を可能にし、パレットを一箇所に集約。
- **ダークモード** — `@media (prefers-color-scheme: dark)` で `:root` 変数を GitHub ダークテーマ風パレット（`#0d1117` 背景、`#e6edf3` テキスト等）に上書き。ブラウザ/OS のカラースキームに応じて自動で切り替わる。コントロールバーの手動トグルボタン（Light / Dark / System）で `html[data-theme="light"]` または `html[data-theme="dark"]` 属性セレクタを適用し `@media` より優先させることも可能。設定はレポートごとの localStorage キー（`{storageKey}-theme`）に保存される。
- **ユーティリティクラス** — C# ジェネレータがインライン `style` 属性の代わりに使うテーマ対応クラス: `.imp-high`、`.imp-medium`（重要度ラベル）、`.status-available`、`.status-unavailable`（逆アセンブラ状態）、`.vuln-new`、`.vuln-resolved`（脆弱性バッジ）、`.vuln-new-count`、`.vuln-resolved-count`（脆弱性カウント）、`.warn-danger`、`.warn-caution`（警告バナー）。
- `tr.filter-hidden` / `tr.diff-row.filter-hidden-parent` — `display: none !important` で行を非表示。
- `.filter-table-dbl` — Change Importance テーブルクラス。`tbody td` の高さを `calc(var(--ft-row-h) * 2)` で Diff Detail と行揃え。
- `.filter-search-wrap` — 検索入力のラッパー。ボーダーとクリアボタンを含む。

### JavaScript（[`Services/HtmlReport/js/`](../Services/HtmlReport/js/)）

JavaScript は `Services/HtmlReport/js/` 配下の9つのモジュールファイルに分割され、`HtmlReportGenerateService.Js.cs` が生成時に結合します：

| モジュール | 責務 |
| --- | --- |
| `diff_report_state.js` | 定数、テンプレートプレースホルダー、`formatTs`、`readSavedStateFromStorage`、`collectState`、`autoSave`、`updateProgress` |
| `diff_report_export.js` | `downloadReviewed`、`verifyIntegrity`、`collapseAll`、`clearAll` |
| `diff_report_diffview.js` | `decodeDiffHtml`、`toggleDiffView`（サイドバイサイド差分） |
| `diff_report_lazy.js` | `setupLazyDiff`、`setupLazySection`、`forceDecodeLazySections` |
| `diff_report_layout.js` | `syncTableWidths`、`syncScTableWidths`、`initColResizeSingle`、`syncFilterRowHeight`、`wrapInputWithClear`、`initClearButtons`、`initColResize` |
| `diff_report_filter.js` | `applyFilters`、`resetFilters`、`copyPath` |
| `diff_report_excel.js` | `downloadAsPdf`、`downloadExcelCompatibleHtml`、`buildExcelRow`、`esc` |
| `diff_report_theme.js` | `initTheme`、`cycleTheme`、`applyTheme`、`getStoredTheme`（Light/Dark/System トグル） |
| `diff_report_init.js` | `DOMContentLoaded` ハンドラー、キーボードナビゲーション |

- `applyFilters()` — すべてのフィルタコントロールを読み取り、行に `filter-hidden` / `filter-hidden-parent` CSS クラスを適用。
- `resetFilters()` — すべてのチェックボックスを復元し、検索ボックスをクリア。
- `__filterIds__` — `collectState()` / localStorage 自動保存から除外されるフィルタ入力 ID の配列。
- `syncFilterRowHeight()` — Diff Detail 行の高さを測定し `--ft-row-h` CSS 変数を設定。Change Importance 行が正確に 2 倍の高さになる。
- `wrapInputWithClear(inp)` — 検索入力をクリアボタン（⊗）付きラッパーで囲む。`input` と `change` の両イベントを発火。
- `downloadReviewed()` — `filter-hidden` / `filter-hidden-parent` クラスとテーブルの inline width をクリアしてから `outerHTML` をキャプチャし、その後 `syncTableWidths()` でライブページの状態を復元。
- `downloadExcelCompatibleHtml()` — Excel XML 名前空間宣言を含む簡素な HTML `<table>` を生成し、`diff_report_YYYYMMDD_reviewed_Excel-compatible.html` としてダウンロード。テーブルにはヘッダーメタデータ、色分けされたセクションタイトル付きの全ファイルセクション、ファイルごとのレビュー状態、凡例、サマリーが含まれる。reviewed HTML のみで利用可能。
- `buildExcelRow(tr)` — ファイル `<tr>` 行からセルデータを抽出し、11列の Excel 互換テーブル行を構築。

### 設計判断

1. フィルタ状態は `collectState()` と localStorage から意図的に除外。ページリロード時にリセットされ、reviewed HTML の状態には保存されない。
2. フィルターゾーンは `<!--CTRL-->` マーカーの**外**に配置し、reviewed HTML でもフィルタ機能を完全に維持。フィルタチェックボックスと検索入力は reviewed モードの読み取り専用化から除外（`__filterIds__` チェック）。
3. importance フィルタは `data-importance` 属性を持つ行にのみ適用。importance なしの行（例: "No structural changes detected"）はフィルタを通過。
4. 検索入力のクリアボタンには `btn-input-clear` CSS クラスを使用。`btn-clear` は**使わないこと** — このクラスはツールバーボタン（Fold all details、Reset filters、Clear all）で既に使用されており、衝突するとツールバーボタンが完全に非表示になる。
5. `downloadReviewed()` は `outerHTML` キャプチャ前にテーブルの inline `style="width:..."` をクリアすること。そうしないと `syncTableWidths()` が設定した古いピクセル幅が reviewed HTML に焼き込まれ、reviewed ロード時に列幅の不一致が発生する。

## パフォーマンスベンチマーク

[`FolderDiffIL4DotNet.Benchmarks`](../FolderDiffIL4DotNet.Benchmarks/) プロジェクトで [BenchmarkDotNet](https://www.nuget.org/packages/BenchmarkDotNet/) を使用してパフォーマンスを計測します。

```bash
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks -- --filter *TextDiffer*
```

ベンチマーククラス:
- [`TextDifferBenchmarks`](../FolderDiffIL4DotNet.Benchmarks/TextDifferBenchmarks.cs): 小規模（100 行）・中規模（10K 行）・大規模（1M 行）の IL 風テキスト差分。
- [`FolderDiffBenchmarks`](../FolderDiffIL4DotNet.Benchmarks/FolderDiffBenchmarks.cs): ファイル列挙（100 / 1K / 10K ファイル）と SHA256 ハッシュ比較。

**CI 統合:** [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml) の `benchmark` ジョブは `workflow_dispatch` 時にすべてのベンチマークを実行し、JSON および GitHub エクスポーター付きの `BenchmarkDotNet.Artifacts/` を CI アーティファクトとしてアップロードします。

**リグレッション検知:** [`.github/workflows/benchmark-regression.yml`](../.github/workflows/benchmark-regression.yml) ワークフローは `main` への PR および `push` のたびに自動実行されます。全ベンチマーククラスの JSON 結果を単一レポートに統合し、[`benchmark-action/github-action-benchmark@v1`](https://github.com/benchmark-action/github-action-benchmark) を使用して `gh-benchmarks` ブランチに保存されたベースラインと比較します。いずれかのベンチマークが 50% 以上劣化した場合（閾値 `150%`）、ジョブが失敗し PR コメントが投稿されます。`main` への push 時には結果が新しいベースラインとして保存されます。

### SHA256 ハッシュのプリシード

IL キャッシュが有効な場合、`FileDiffService` は初回ハッシュ比較後に計算済み SHA256 ハッシュを `PreSeedFileHash` 経由で `ILMemoryCache` にシード登録します。これにより、IL キャッシュキー生成（`BuildILCacheKey`）時にファイルを再読み込みして SHA256 を再計算することを回避します。

### IL 行分割・フィルタの最適化

`ILOutputService.SplitAndFilterIlLines` は `Split('\n')` と `Where(filter)` の処理を 1 パスに統合し、4 つの中間リストの代わりに 1 つの `List<string>` を直接生成します。

## ソースコードのスタイル方針

文字列整形や構造化は、まず局所性と読みやすさを優先します。
- 固定書式で単発利用のメッセージは、[`string.Format(...)`](https://learn.microsoft.com/ja-jp/dotnet/api/system.string.format?view=net-8.0) より補間文字列を優先します。
- 同じ文言テンプレートを複数箇所で意図的に共有する場合のみ、共通の書式定数やヘルパーを残します。
- ドメイン非依存の helper は [`FolderDiffIL4DotNet.Core/`](../FolderDiffIL4DotNet.Core/) へ置き、[`FolderDiffIL4DotNet/Services`](../Services/) はフォルダ差分の振る舞いに集中させてください。
- プロジェクト横断で使うバイト換算値や日時フォーマットは [`FolderDiffIL4DotNet.Core/Common/CoreConstants.cs`](../FolderDiffIL4DotNet.Core/Common/CoreConstants.cs) に集約し、アプリ固有の定数は [`Common/Constants.cs`](../Common/Constants.cs) で管理してください。[`Constants.IL_MVID_LINE_PREFIX`](../Common/Constants.cs) のような IL ドメイン固有の文字列定数は [`Common/Constants.cs`](../Common/Constants.cs) に置き、複数のサービスファイルに重複定義しないようにしてください。
- `#region` は、ファイル構成や命名だけでは読みづらい具体的な事情がある場合に限って追加してください。

## アーキテクチャ概要

```mermaid
flowchart TD
    A["Program.cs"] --> B["ProgramRunner"]
    B --> C["ConfigService"]
    B --> T["DiffPipelineExecutor"]
    T --> D["DiffExecutionContext"]
    T --> E["実行単位 ServiceProvider"]
    E --> F["FolderDiffService"]
    E --> Q["FolderDiffExecutionStrategy"]
    E --> G["ReportGenerateService"]
    E --> H["ProgressReportService"]
    F --> I["IFileSystemService"]
    F --> Q
    F --> J["FileDiffService"]
    J --> K["IFileComparisonService"]
    J --> L["ILOutputService"]
    L --> M["DotNetDisassembleService"]
    M --> R["ILCachePrefetcher"]
    M --> S["DisassemblerHelper"]
    R --> S
    L --> N["ILCache"]
    L --> O["ILTextOutputService"]
    F --> P["FileDiffResultLists"]
    G --> P
    click P href "../Models/FileDiffResultLists.cs" "Open FileDiffResultLists"
```

設計意図:
- [`Program.cs`](../Program.cs) は最小限に保ち、アプリ全体の起点だけを担います。
- [`ProgramRunner`](../ProgramRunner.cs) は 1 回のコンソール実行における CLI 分岐・引数検証・設定読込・終了コード写像の境界です。
- [`DiffPipelineExecutor`](../Runner/DiffPipelineExecutor.cs) は差分実行パイプラインを担当し、スコープ付き DI コンテナ構築・差分実行・全レポート生成を行います。
- [`DryRunExecutor`](../Runner/DryRunExecutor.cs) は `--dry-run` プレビューを担当し、ファイル列挙と統計表示のみを行い比較やレポート生成は行いません。
- [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) は実行固有のパスとモード判定を不変オブジェクトとして保持します。
- [`FolderDiffIL4DotNet.Core`](../FolderDiffIL4DotNet.Core/) は、フォルダ差分ドメインに依存しない console / diagnostics / I/O / text helper を収める再利用境界です。
- コアサービスは、静的可変状態や場当たり的な `new` ではなく、コンストラクタ注入とインターフェースで接続されます。
- [`IFileSystemService`](../Services/IFileSystemService.cs) と [`IFileComparisonService`](../Services/IFileComparisonService.cs) が、列挙/比較 I/O を切り出す最下層の差し替えポイントです。特に [`IFileSystemService.EnumerateFiles(...)`](../Services/IFileSystemService.cs) は、巨大なフォルダでもフィルタ前に `string[]` を丸ごと確保しない遅延列挙を維持します。
- [`FolderDiffExecutionStrategy`](../Services/FolderDiffExecutionStrategy.cs) は、比較対象への取り込み条件、無視ファイル記録、自動並列度の決定を集約し、[`FolderDiffService`](../Services/FolderDiffService.cs) へポリシー知識が広がりすぎないようにします。
- [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) は、差分処理とレポート生成が共有する実行単位の集約ハブです。
- [`DotNetDisassembleService`](../Services/DotNetDisassembleService.cs) は逆アセンブル実行とキャッシュヒット/ストア追跡を担い、IL キャッシュのプリフェッチは [`ILCachePrefetcher`](../Services/ILCachePrefetcher.cs) へ委譲します。コマンド判定・候補列挙・実行ファイルパス解決の共有静的ロジックは [`DisassemblerHelper`](../Services/DisassemblerHelper.cs) に集約し、両クラス間の重複を排除しています。
- [`FolderDiffService`](../Services/FolderDiffService.cs) はプリコンピュート中のキープアライブスピナーを専用の `CreateKeepAliveTask()` に分離し、`PrecomputeIlCachesAsync()` が調停ロジックに集中できるようにしています。

<a id="guide-ja-execution-lifecycle"></a>
## 実行ライフサイクル

### 起動シーケンス

```mermaid
sequenceDiagram
    participant CLI as CLI
    participant Program as Program.cs
    participant Runner as ProgramRunner
    participant Config as ConfigService
    participant Scope as 実行スコープ
    participant Diff as FolderDiffService
    participant Report as ReportGenerateService

    CLI->>Program: Main(args)
    Program->>Runner: RunAsync(args)
    Runner->>Runner: ロガー初期化とバージョン表示
    Runner->>Runner: 引数検証と Reports/<label> 作成
    Runner->>Config: LoadConfigAsync()
    Runner->>Runner: DiffExecutionContext 構築
    Runner->>Scope: BuildRunServiceProvider()
    Runner->>Diff: ExecuteFolderDiffAsync()
    Diff-->>Runner: 集約済みの差分結果を返す
    Runner->>Report: GenerateDiffReport(...)
    Runner->>Runner: 完了時警告の集約出力
    Runner->>Runner: 完了サマリーチャートの出力
    Runner-->>CLI: 型付き終了コード (0/2/3/4/1)
```

差分フェーズは [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) を返し、その内容を使ってレポート生成と完了時の警告出力を行います。

### `RunAsync` の中で起きること

1. CLI オプション（`--help`、`--version`、`--print-config`、`--no-pause`、`--config`、`--threads`、`--no-il-cache`、`--skip-il`、`--no-timestamp-warnings`、`--coffee`、`--beer`、`--matcha`、`--bell`）を解析します。
2. `--help` または `--version` がある場合は、ロガー初期化を一切行わずに即座に出力してコード `0` で終了します。
2a. `--print-config` がある場合（`--config <path>` との併用可）、有効な設定 — [`config.json`](../config.json) のデシリアライズ結果にすべての `FOLDERDIFF_*` 環境変数オーバーライドを適用したもの — をインデント付き JSON として標準出力に書き出し、終了コード `0` で終了します。設定読込エラーは終了コード `3` です。
3. ログを初期化し、アプリのバージョンを表示します。
4. `old`、`new`、`reportLabel` 引数を検証します。未知の CLI フラグはここで終了コード `2` として検出されます。
5. `Reports/<label>` を早い段階で作成し、同名が既にある場合は失敗させます。
6. `--config` で指定されたパス（未指定なら [`AppContext.BaseDirectory`](https://learn.microsoft.com/ja-jp/dotNet/API/system.appcontext.basedirectory?view=net-8.0)）から設定ファイルを読み込み、ミュータブルな [`ConfigSettingsBuilder`](../Models/ConfigSettingsBuilder.cs) へデシリアライズします。デシリアライズ直後に [`ConfigService.ApplyEnvironmentVariableOverrides`](../Services/ConfigService.cs) が `FOLDERDIFF_<PROPERTYNAME>` 環境変数オーバーライド（例: `FOLDERDIFF_MAXPARALLELISM=4`）をビルダーに適用します。
7. CLI オプションをビルダーに上書き適用します。`--threads` → [`MaxParallelism`](../Models/ConfigSettingsBuilder.cs)、`--no-il-cache` → [`EnableILCache`](../Models/ConfigSettingsBuilder.cs) `= false`、`--skip-il` → [`SkipIL`](../Models/ConfigSettingsBuilder.cs) `= true`、`--no-timestamp-warnings` → [`ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp`](../Models/ConfigSettingsBuilder.cs) `= false`。その後 [`ConfigSettingsBuilder.Validate()`](../Models/ConfigSettingsBuilder.cs) を呼び出し、範囲外の値がある場合は終了コード `3` で失敗させます。最後に [`ConfigSettingsBuilder.Build()`](../Models/ConfigSettingsBuilder.cs) がイミュータブルな [`ConfigSettings`](../Models/ConfigSettings.cs) インスタンスを生成し、以降の実行で使用します。
8. [`TimestampCache`](../Services/Caching/TimestampCache.cs) などの一時共有ヘルパーをクリアします。
9. ネットワーク共有判定を含む [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) を組み立てます。
10. 実行単位の DI コンテナを構築します。
11. フォルダ比較を実行し、進捗表示を終了します。
12. 集約結果から [`diff_report.md`](samples/diff_report.md) を生成します。
13. [`ShouldGenerateHtmlReport`](../Models/ConfigSettings.cs) が `true`（既定）のとき、集約結果から [`diff_report.html`](samples/diff_report.html) を生成します。HTML ファイルは localStorage 自動保存およびダウンロード機能を持つ自己完結型インタラクティブレビュードキュメントです。
14. フェーズ結果をプロセス終了コードへ変換します。成功は `0`、CLI/入力パス不正は `2`、設定読込/解析/バリデーション失敗は `3`、差分実行/レポート生成失敗は `4`、分類外の想定外エラーだけを `1` にします。

実装上は、`RunAsync()` 自体を短く保つため、これらを明示的なフェーズとして private helper へ分割しています。

失敗時の扱い:
- [`ProgramRunner`](../ProgramRunner.cs) はアプリ境界で小さな型付き Result を使い、すべての失敗を 1 つの終了コードへ潰さないようにしています。
- 引数検証エラー、未知フラグ、入力パス不足/不正は終了コード `2` です。
- [`ConfigService`](../Services/ConfigService.cs) の [`config.json`](../config.json) 未検出、解析失敗、設定読込 I/O 失敗、または [`ConfigSettings.Validate()`](../Models/ConfigSettings.cs) が失敗した場合は終了コード `3` です。
- 差分実行やレポート生成の失敗、さらに IL 比較由来の致命的な [`InvalidOperationException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.invalidoperationexception?view=net-8.0) は終了コード `4` です。
- 明示分類から漏れた想定外の内部エラーだけを終了コード `1` として扱います。
- IL 比較由来の [`InvalidOperationException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.invalidoperationexception?view=net-8.0) は致命的な例外扱いとし、実行全体を止めるものとします。
- [`FolderDiffService.ExecuteFolderDiffAsync()`](../Services/FolderDiffService.cs) は、パス検証エラーや [`DirectoryNotFoundException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.directorynotfoundexception?view=net-8.0)、[`IOException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.ioexception?view=net-8.0)、[`UnauthorizedAccessException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.unauthorizedaccessexception?view=net-8.0)、[`NotSupportedException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.notsupportedexception?view=net-8.0) などの想定される実行時例外を error として記録して再スローします。本当に想定外の例外だけを別文言の "unexpected error" として記録します。
- プリフライト書込権限チェック（[`CheckReportsParentWritableOrThrow`](../Runner/RunPreflightValidator.cs)）は、[`UnauthorizedAccessException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.unauthorizedaccessexception?view=net-8.0) と [`IOException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.ioexception?view=net-8.0) の両方を原因別メッセージとともにログ出力して再スローします。I/O エラーは一切握りつぶしません。
- 出力ファイルの読み取り専用化はベストエフォートで、失敗しても警告止まりです。

<a id="guide-ja-di-layout"></a>
## Dependency Injection 構成

### ルートコンテナ

[`Program.cs`](../Program.cs) で登録:
- [`ILoggerService`](../Services/ILoggerService.cs) -> [`LoggerService`](../Services/LoggerService.cs)
- [`ConfigService`](../Services/ConfigService.cs)
- [`ProgramRunner`](../ProgramRunner.cs)

このルートコンテナは意図的に小さく保ち、実行固有のサービスを溜め込まないようにしています。

### 実行単位コンテナ

[`RunScopeBuilder.Build(...)`](../Runner/RunScopeBuilder.cs) で登録:
- 実行スコープ内シングルトン
- [`IReadOnlyConfigSettings`](../Models/IReadOnlyConfigSettings.cs)（[`ConfigSettingsBuilder`](../Models/ConfigSettingsBuilder.cs) から構築されたイミュータブルな [`ConfigSettings`](../Models/ConfigSettings.cs)）
- [`DiffExecutionContext`](../Services/DiffExecutionContext.cs)
- [`ILoggerService`](../Services/ILoggerService.cs)（共有ロガー）
- スコープサービス
- [`FileDiffResultLists`](../Models/FileDiffResultLists.cs)
- [`DotNetDisassemblerCache`](../Services/Caching/DotNetDisassemblerCache.cs)
- [`ILCache`](../Services/Caching/ILCache.cs)（無効時は `null`）
- [`ProgressReportService`](../Services/ProgressReportService.cs)
- [`ReportGenerateService`](../Services/ReportGenerateService.cs)
- [`HtmlReportGenerateService`](../Services/HtmlReportGenerateService.cs)
- [`IFileSystemService`](../Services/IFileSystemService.cs) / [`FileSystemService`](../Services/FileSystemService.cs)
- [`IFolderDiffExecutionStrategy`](../Services/IFolderDiffExecutionStrategy.cs) / [`FolderDiffExecutionStrategy`](../Services/FolderDiffExecutionStrategy.cs)
- [`IFileComparisonService`](../Services/IFileComparisonService.cs) / [`FileComparisonService`](../Services/FileComparisonService.cs)
- [`IILTextOutputService`](../Services/ILOutput/IILTextOutputService.cs) / [`ILTextOutputService`](../Services/ILOutput/ILTextOutputService.cs)
- [`IDotNetDisassembleService`](../Services/IDotNetDisassembleService.cs) / [`DotNetDisassembleService`](../Services/DotNetDisassembleService.cs)
- [`IILOutputService`](../Services/IILOutputService.cs) / [`ILOutputService`](../Services/ILOutputService.cs)
- [`IFileDiffService`](../Services/IFileDiffService.cs) / [`FileDiffService`](../Services/FileDiffService.cs)
- [`IFolderDiffService`](../Services/IFolderDiffService.cs) / [`FolderDiffService`](../Services/FolderDiffService.cs)

この構成が重要な理由:
- 実行ごとに、差分結果を保持する [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) と、old/new で同じ逆アセンブラを使うための内部状態やキャッシュは新しく作られ、前回の実行内容を引き継ぎません。
- テストでインターフェース差し替えがしやすくなります。
- 実行時パスやモード判定が明示的で不変になります。

## 主要ファイルの責務

| ファイル | 主な責務 | 補足 |
| --- | --- | --- |
| [`Program.cs`](../Program.cs) | アプリ起動点 | 薄いまま維持する |
| [`ProgramRunner.cs`](../ProgramRunner.cs) | CLI 分岐、引数検証、設定読込、終了コード写像 | ヘルプテキストは [`ProgramRunner.HelpText.cs`](../Runner/ProgramRunner.HelpText.cs)、設定読込/バリデーションは [`ProgramRunner.Config.cs`](../Runner/ProgramRunner.Config.cs) |
| [`Runner/DiffPipelineExecutor.cs`](../Runner/DiffPipelineExecutor.cs) | 差分実行パイプラインとレポート生成 | スコープ付き DI コンテナ構築・差分実行・Markdown/HTML/監査ログの全レポート生成 |
| [`Runner/DryRunExecutor.cs`](../Runner/DryRunExecutor.cs) | `--dry-run` 事前プレビュー | ファイル列挙・ユニオン数/アセンブリ候補数算出・拡張子内訳表示を比較実行なしで行う |
| [`FolderDiffIL4DotNet.Core/`](../FolderDiffIL4DotNet.Core/) | 再利用可能な console / diagnostics / I/O / text helper | フォルダ差分ドメインのポリシーを持たない |
| [`FolderDiffIL4DotNet.Core/Text/EncodingDetector.cs`](../FolderDiffIL4DotNet.Core/Text/EncodingDetector.cs) | ファイルエンコーディング自動検出（BOM・UTF-8 検証・ANSI フォールバック） | インライン差分で非 UTF-8 ファイル（Shift_JIS 等）を正しく読むために使用；`System.Text.Encoding.CodePages` が必要 |
| [`Services/DiffExecutionContext.cs`](../Services/DiffExecutionContext.cs) | 実行固有パスとネットワークモードの保持 | 可変状態を持たない |
| [`Services/FolderDiffService.cs`](../Services/FolderDiffService.cs) | フォルダ差分全体の調停と結果振り分け | 進捗と Added/Removed もここ |
| [`Services/FolderDiffExecutionStrategy.cs`](../Services/FolderDiffExecutionStrategy.cs) | 列挙フィルタと自動並列度ポリシー | 無視拡張子適用とネットワーク考慮の並列度決定を担当 |
| [`Services/IFileSystemService.cs`](../Services/IFileSystemService.cs) + [`Services/FileSystemService.cs`](../Services/FileSystemService.cs) | 列挙/出力系ファイルシステム抽象 | フォルダ単位ユニットテスト向け。遅延列挙もここで扱う |
| [`Services/FileDiffService.cs`](../Services/FileDiffService.cs) | ファイル単位の判定木 | `SHA256 -> IL -> text -> fallback` |
| [`Services/IFileComparisonService.cs`](../Services/IFileComparisonService.cs) + [`Services/FileComparisonService.cs`](../Services/FileComparisonService.cs) | ファイル単位の比較/判定 I/O 抽象 | ファイル単位ユニットテスト向け |
| [`Services/ILOutputService.cs`](../Services/ILOutputService.cs) | IL 比較、行除外、ブロック単位順序非依存比較、任意 IL 出力 | 同一逆アセンブラ制約を保証；行順序が異なる場合はブロック単位マルチセット比較にフォールバック |
| [`Services/ILOutput/ILBlockParser.cs`](../Services/ILOutput/ILBlockParser.cs) | IL 逆アセンブリ出力を論理ブロック（メソッド、クラス、プロパティ）に分割 | `ILOutputService.BlockAwareSequenceEqual` で順序非依存比較に使用 |
| [`Services/AssemblyMethodAnalyzer.cs`](../Services/AssemblyMethodAnalyzer.cs) | `System.Reflection.Metadata` によるメソッドレベル変更検出 | ベストエフォート；失敗時は `null` を返す。型・メソッド・プロパティ・フィールドの追加・削除・変更（アクセス修飾子変更、修飾子変更、型変更、IL ボディ変更）を検出。各エントリは [`ChangeImportanceClassifier`](../Services/ChangeImportanceClassifier.cs) により自動分類 |
| [`Services/ChangeImportanceClassifier.cs`](../Services/ChangeImportanceClassifier.cs) | `MemberChangeEntry` のルールベース重要度分類器 | 変更種別・アクセス修飾子・アロー表記フィールド変更に基づき `High` / `Medium` / `Low` の [`ChangeImportance`](../Models/ChangeImportance.cs) を付与 |
| [`Models/ChangeImportance.cs`](../Models/ChangeImportance.cs) | 変更の重要度列挙型 | `Low=0`, `Medium=1`, `High=2`；`MemberChangeEntry.Importance` およびレポート表示に使用 |
| [`Services/ChangeTagClassifier.cs`](../Services/ChangeTagClassifier.cs) | ヒューリスティック変更パターン分類器 | セマンティック解析と依存関係データから [`ChangeTag`](../Models/ChangeTag.cs) ラベル（Extract、Inline、Move、Rename、Signature、Access、BodyEdit、DepUpdate、+Method、-Method、+Type、-Type）を推定；[`FileDiffService`](../Services/FileDiffService.cs) がセマンティック/依存関係解析後に呼び出す |
| [`Models/ChangeTag.cs`](../Models/ChangeTag.cs) | 変更タグ列挙型 | 推定変更パターンを表す12値；レポートの「Estimated Change」列に表示 |
| [`Services/DotNetDisassembleService.cs`](../Services/DotNetDisassembleService.cs) | ツール探索、逆アセンブル実行、キャッシュヒット/ストア追跡、ブラックリスト | 外部ツール境界；プリフェッチは [`ILCachePrefetcher`](../Services/ILCachePrefetcher.cs) へ委譲 |
| [`Services/ILCachePrefetcher.cs`](../Services/ILCachePrefetcher.cs) | IL キャッシュのプリフェッチ（全候補コマンド×引数パターンの事前ヒット確認） | [`DotNetDisassembleService`](../Services/DotNetDisassembleService.cs) から分離；独自のヒットカウンタを保持 |
| [`Services/DisassemblerHelper.cs`](../Services/DisassemblerHelper.cs) | 共有静的ヘルパー：コマンド判定・候補列挙・実行ファイルパス解決・利用可否プローブ | [`DotNetDisassembleService`](../Services/DotNetDisassembleService.cs) と [`ILCachePrefetcher`](../Services/ILCachePrefetcher.cs) の両方が使用；`ProbeAllCandidates()` はレポートヘッダ用に [`DisassemblerProbeResult`](../Models/DisassemblerProbeResult.cs) リストを返す；インスタンス状態なし |
| [`Models/DisassemblerProbeResult.cs`](../Models/DisassemblerProbeResult.cs) | 逆アセンブラ利用可否プローブ結果レコード | `ToolName`, `Available`, `Version`, `Path`；[`FileDiffResultLists.DisassemblerAvailability`](../Models/FileDiffResultLists.cs) に格納 |
| [`Services/DisassemblerBlacklist.cs`](../Services/DisassemblerBlacklist.cs) | ツール別失敗数管理・設定可能な TTL ブラックリスト | スレッドセーフな [`ConcurrentDictionary`](https://learn.microsoft.com/ja-jp/dotnet/api/system.collections.concurrent.concurrentdictionary-2?view=net-8.0)；TTL は設定値 [`DisassemblerBlacklistTtlMinutes`](../Models/ConfigSettings.cs) を使用 |
| [`Services/Caching/ILCache.cs`](../Services/Caching/ILCache.cs) | IL キャッシュの公開 API と調停 | メモリ/ディスクの詳細は専用コンポーネントへ委譲 |
| [`Services/Caching/ILMemoryCache.cs`](../Services/Caching/ILMemoryCache.cs) | メモリ上の IL / SHA256 キャッシュ | LRU と TTL を担当 |
| [`Services/Caching/ILDiskCache.cs`](../Services/Caching/ILDiskCache.cs) | IL キャッシュのディスク永続化とクォータ制御 | キャッシュファイル I/O とトリミングを担当 |
| [`Services/Caching/DotNetDisassemblerCache.cs`](../Services/Caching/DotNetDisassemblerCache.cs) | 逆アセンブラ バージョン文字列キャッシュ | バージョン取得のプロセス起動コストを回避 |
| [`Services/Caching/TimestampCache.cs`](../Services/Caching/TimestampCache.cs) | メモリ内ファイル最終更新日時キャッシュ | 静的；実行サイクルごとにクリアし I/O を削減 |
| [`Services/ReportGenerationContext.cs`](../Services/ReportGenerationContext.cs) | レポート生成サービス用の不変パラメータバッグ | `ProgramRunner` 境界での引数重複を排除 |
| [`Services/ReportGenerateService.cs`](../Services/ReportGenerateService.cs) | Markdown レポート生成 | [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) を読むだけ；`_sectionWriters` を [`IReportSectionWriter`](../Services/IReportSectionWriter.cs) 経由で反復 |
| [`Services/IReportSectionWriter.cs`](../Services/IReportSectionWriter.cs) + [`Services/ReportWriteContext.cs`](../Services/ReportWriteContext.cs) | セクション単位のレポート書き込みインターフェイスとコンテキスト | [`ReportGenerateService`](../Services/ReportGenerateService.cs) 内に 10 個のプライベートネストクラスで実装 |
| [`Services/HtmlReportGenerateService.cs`](../Services/HtmlReportGenerateService.cs) | インタラクティブ HTML レビューレポート生成 | [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) を読むだけ；チェックボックス・テキスト入力・localStorage 自動保存・ダウンロード機能を持つ自己完結型 [`diff_report.html`](samples/diff_report.html) を生成；インラインスタイルの代わりに CSS カスタムプロパティ（`var(--color-*)`）とユーティリティクラスを使用しテーマ対応レンダリングを実現；`prefers-color-scheme` による自動ダークモード対応；「Download as reviewed」は Web Crypto API でレビュー済み HTML の SHA256 を計算・埋め込み（自己検証用）、コンパニオン `.sha256` 検証ファイルもダウンロードし、レビュー済みバナーに「Verify integrity」ボタンを追加；[`ShouldGenerateHtmlReport`](../Models/ConfigSettings.cs) が `false` のときはスキップ |
| [`Services/AuditLogGenerateService.cs`](../Services/AuditLogGenerateService.cs) | 構造化 JSON 監査ログ生成 | [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) を読み、`diff_report.md` / `diff_report.html` の SHA256 インテグリティハッシュを計算；[`audit_log.json`](samples/audit_log.json) を生成；[`ShouldGenerateAuditLog`](../Models/ConfigSettings.cs) が `false` のときはスキップ |
| [`Services/SbomGenerateService.cs`](../Services/SbomGenerateService.cs) | SBOM（ソフトウェア部品表）生成 | [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) からコンポーネント一覧を SHA256 ハッシュと差分ステータス付きで抽出；CycloneDX 1.5 JSON（`sbom.cdx.json`）または SPDX 2.3 JSON（`sbom.spdx.json`）を出力；[`ShouldGenerateSbom`](../Models/ConfigSettings.cs) が `false` のときはスキップ |
| [`Models/AuditLogEntry.cs`](../Models/AuditLogEntry.cs) | 監査ログデータモデル | [`AuditLogRecord`](../Models/AuditLogEntry.cs)（トップレベル）、[`AuditLogFileEntry`](../Models/AuditLogEntry.cs)（ファイル単位）、[`AuditLogSummary`](../Models/AuditLogEntry.cs)（件数集計） |
| [`Models/SbomModels.cs`](../Models/SbomModels.cs) | SBOM データモデル | CycloneDX 1.5 モデル（[`CycloneDxBom`](../Models/SbomModels.cs)、[`CycloneDxComponent`](../Models/SbomModels.cs)）、SPDX 2.3 モデル（[`SpdxDocument`](../Models/SbomModels.cs)、[`SpdxPackage`](../Models/SbomModels.cs)）、[`SbomFormat`](../Models/SbomModels.cs) 列挙型 |
| [`Models/FileDiffResultLists.cs`](../Models/FileDiffResultLists.cs) | スレッドセーフな結果集約 | 実行単位の共有状態；partial ファイル分割: [`FileDiffResultLists.ComparisonResults.cs`](../Models/FileDiffResultLists.ComparisonResults.cs)（差分詳細、逆アセンブララベル、除外ファイル）、[`FileDiffResultLists.Metadata.cs`](../Models/FileDiffResultLists.Metadata.cs)（セマンティック変更、依存関係変更、警告、逆アセンブラ情報） |

<a id="guide-ja-comparison-pipeline"></a>
## 比較パイプライン

### フォルダ単位のルーティング

```mermaid
flowchart TD
    A["old/new のファイル列挙"] --> B["IgnoredExtensions を除外"]
    B --> C["進捗用の union 件数を算出"]
    C --> D{"old 側の相対パスは new 側にもある?"}
    D -- "いいえ" --> E["Removed を記録"]
    D -- "はい" --> F["FilesAreEqualAsync(relativePath) 実行"]
    F --> G{"一致?"}
    G -- "はい" --> H["Unchanged を記録"]
    G -- "いいえ" --> I["Modified を記録"]
    E --> J{"old 側に未処理あり?"}
    H --> J
    I --> J
    J -- "はい" --> D
    J -- "いいえ" --> K["残った new 側のみのファイルを Added として記録"]
```

実装上の補足:
- [`FolderDiffService.ExecuteFolderDiffAsync()`](../Services/FolderDiffService.cs) は実行単位の集計を初期化し、その後 [`FolderDiffExecutionStrategy`](../Services/FolderDiffExecutionStrategy.cs) へ [`IgnoredExtensions`](../Models/ConfigSettings.cs) 適用済み old/new 一覧の列挙と相対パス和集合件数の算出を委譲します。
- 列挙は [`IFileSystemService.EnumerateFiles(...)`](../Services/IFileSystemService.cs) 経由の遅延列挙で進むため、巨大フォルダでも全件配列化してからフィルタする構造を避けています。
- `PrecomputeIlCachesAsync()` はファイルごとの本判定より前に走り、逆アセンブラや IL キャッシュのウォームアップを先に済ませます。あわせて、大規模ツリーでも old/new 全件の追加リストをもう 1 本作らないよう、重複排除済みパスを設定可能なバッチ単位で流します。
- 走査の主導権は old 側にあります。new 側に対応がなければ `Removed`、最後まで `remainingNewFilesAbsolutePathHashSet` に残ったものが `Added` です。
- 並列実行で変わるのは処理順序だけです。高コストな比較に入る前に new 側の集合から対象の相対パスを外すため、最終的な分類結果のルール自体は逐次実行時と変わりません。
- `Unchanged` と `Modified` は `FilesAreEqualAsync(relativePath, maxParallel)` の bool 戻り値だけで決まり、詳細理由は別途 [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) に記録されます。

### ファイル単位の判定木

```mermaid
flowchart TD
    A["同一相対パスのファイル"] --> B{"SHA256 一致?"}
    B -- "はい" --> C["SHA256Match を記録して true"]
    B -- "いいえ" --> D{"old 側は .NET 実行可能?"}
    D -- "はい" --> E["old/new を同一ツール識別で逆アセンブル"]
    E --> F{"除外後 IL は一致?"}
    F -- "はい" --> G["ILMatch を記録して true"]
    F -- "いいえ" --> H["ILMismatch を記録して false"]
    D -- "いいえ" --> I{"拡張子はテキスト対象?"}
    I -- "はい" --> J["テキスト比較を実行"]
    J --> K{"テキスト一致?"}
    K -- "はい" --> L["TextMatch を記録して true"]
    K -- "いいえ" --> M["TextMismatch を記録して false"]
    I -- "いいえ" --> N["SHA256Mismatch を記録して false"]
```

壊しやすい前提:
- 1 ファイルで最初に確定した分類がそのファイルの最終分類です。
- IL 比較は SHA256 不一致の後、かつ .NET 実行可能ファイルにのみ進みます。
- IL 比較では [`Constants.IL_MVID_LINE_PREFIX`](../Common/Constants.cs)（`// MVID:`）で始まる行を常に無視します。これは逆アセンブラが出力する Module Version ID メタデータで、再ビルドのたびに変わり得るため、アセンブリの中身が実質的に同じでも、この行だけで差分ありと判定されてしまうことがあるためです。
- 追加の IL 行無視は部分一致で、大文字小文字を区別します（`StringComparison.Ordinal`）。
- old/new の IL 比較は、同じ逆アセンブラ識別子とバージョン表記でなければなりません。
- テキスト比較は、並列チャンク経路で例外が出た場合に逐次比較へフォールバックします。この挙動は、並列比較側で例外を握りつぶさず `FilesAreEqualAsync(...)` まで伝播させる前提で成り立っています。

ファイル単位の実装メモ:
- [`FileDiffService.FilesAreEqualAsync(...)`](../Services/FileDiffService.cs) は、`.NET 実行可能か` の判定、拡張子判定、サイズ閾値判定の基準として old 側絶対パスを使います。
- 通常実行時の `.NET 実行可能判定`、SHA256/テキスト比較、サイズ取得、チャンク読み出しは [`IFileComparisonService`](../Services/IFileComparisonService.cs) を通して行われます。これは、[`FileDiffService`](../Services/FileDiffService.cs) が比較処理の具体実装に直接依存せず、テストでは [`IFileComparisonService`](../Services/IFileComparisonService.cs) をモックやスタブに差し替えられるようにするためです。既定実装の [`FileComparisonService`](../Services/FileComparisonService.cs) は、これらの処理を [`DotNetDetector`](../FolderDiffIL4DotNet.Core/Diagnostics/DotNetDetector.cs) と [`FileComparer`](../FolderDiffIL4DotNet.Core/IO/FileComparer.cs) に委譲します。
- [`DotNetDetector.DetectDotNetExecutable(...)`](../FolderDiffIL4DotNet.Core/Diagnostics/DotNetDetector.cs) は `NotDotNetExecutable` と `Failed` を区別します。[`FileDiffService`](../Services/FileDiffService.cs) は `Failed` の場合に warning を出して IL 経路をスキップします。
- SHA256 が一致した時点で `SHA256Match` を記録して即終了し、その後に IL やテキスト比較へは進みません。
- IL 経路は [`ILOutputService.DiffDotNetAssembliesAsync(...)`](../Services/ILOutputService.cs) に委譲され、内部で `DisassemblePairWithSameDisassemblerAsync(...)`、比較用ラベル正規化、行除外、任意の IL テキスト出力までをまとめて担当します。
- [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) では、この境界を推奨ツール経路付きで確認します。`Deterministic=false` の小さなクラスライブラリを 2 回ビルドして DLL バイト列が異なることを確認したうえで、[`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) のフィルタ後 IL では `ILMatch` になることを検証します。
- `BuildComparisonDisassemblerLabel(...)` は正しさの一部です。old/new でツール識別やバージョン表記がずれた場合は、その比較を認めず [`InvalidOperationException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.invalidoperationexception?view=net-8.0) にします。
- `ShouldExcludeIlLine(...)` は [`Constants.IL_MVID_LINE_PREFIX`](../Common/Constants.cs)（`// MVID:`）で始まる行を必ず除外します。さらに [`ShouldIgnoreILLinesContainingConfiguredStrings`](../Models/ConfigSettings.cs) が `true` の場合は、[`ILIgnoreLineContainingStrings`](../Models/ConfigSettings.cs) に設定された文字列を trim・重複排除したうえで、`StringComparison.Ordinal` の部分一致で除外します。
- `.NET` 実行可能として IL 比較の対象にならず、かつ拡張子が [`TextFileExtensions`](../Models/ConfigSettings.cs) に含まれるファイルは、テキストファイルとして比較します。このとき [`TextDiffParallelThresholdKilobytes`](../Models/ConfigSettings.cs) と [`TextDiffChunkSizeKilobytes`](../Models/ConfigSettings.cs) を実効バイト数に変換し、比較方法を決めます。
- [`OptimizeForNetworkShares`](../Models/ConfigSettings.cs) が有効な場合は、ネットワーク共有上でチャンクごとに何度もファイルを開閉するコストを避けるため、ファイルサイズにかかわらず `DiffTextFilesAsync(...)` による逐次比較を使います。ローカル最適化時は old 側ファイルのサイズを基準にし、[`TextDiffParallelThresholdKilobytes`](../Models/ConfigSettings.cs) 未満なら逐次比較、以上なら [`TextDiffChunkSizeKilobytes`](../Models/ConfigSettings.cs) ごとの固定長チャンクに分割して `DiffTextFilesParallelAsync(...)` で並列比較します。
- [`TextDiffParallelMemoryLimitMegabytes`](../Models/ConfigSettings.cs) が `0` より大きい場合、[`FileDiffService`](../Services/FileDiffService.cs) はそれを並列テキスト比較で追加確保してよいバッファ予算として扱い、その時点の managed heap 使用量をログへ残しつつ、実効ワーカー数を下げるか逐次比較へフォールバックします。
- 並列チャンク比較の途中で [`ArgumentOutOfRangeException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.argumentoutofrangeexception?view=net-8.0)、[`IOException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.ioexception?view=net-8.0)、[`UnauthorizedAccessException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.unauthorizedaccessexception?view=net-8.0)、[`NotSupportedException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.notsupportedexception?view=net-8.0) のいずれかが出た場合は、warning を記録したうえで `DiffTextFilesAsync(...)` による逐次比較へフォールバックします。したがって `DiffTextFilesParallelAsync(...)` 側でこれらの例外を `false` に置き換えて握りつぶすと、呼び出し元はフォールバックできません。
- IL 比較対象でもテキスト比較対象でもないファイルは、SHA256 不一致の時点で `SHA256Mismatch` が最終結果です。`SHA256Mismatch` は実行完了時の集約警告の対象でもあり、レポートでは末尾の `Warnings` セクションに出力されます。詳細テーブル（`[ ! ] Modified Files — SHA256Mismatch (Manual Review Recommended)`）は警告メッセージの直下に配置されます。`Warnings` セクションの各警告メッセージは、対応する詳細テーブルの直上に配置されます（インターリーブレイアウト）。現状はその先の汎用バイナリ差分はありません。
- **Modified と判定されたファイル**について、[`ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp`](../Models/ConfigSettings.cs) が `true` かつ new 側の更新日時が old 側より古い場合は、比較結果とは別に更新日時逆転の警告が記録されます。このチェックは `FilesAreEqualAsync` が `false` を返した後にのみ実行されます。Unchanged ファイルは評価されません。この警告は実行完了時にコンソールへ集約出力され、レポートでは `SHA256Mismatch` 警告の後に更新日時が逆転したファイルの一覧として `Warnings` セクションへ出力されます。

失敗時の扱い:
- IL 比較で発生した [`InvalidOperationException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.invalidoperationexception?view=net-8.0) は、ログを出力したうえで意図的に再送出されます。これは IL ツールの不整合やセットアップ不備を致命的な例外として扱い、実行全体を停止させるためです。
- [`DotNetDetector.DetectDotNetExecutable(...)`](../FolderDiffIL4DotNet.Core/Diagnostics/DotNetDetector.cs) の失敗は致命的な例外とは扱いません。警告ログを出力して IL 比較だけをスキップし、その後のテキスト比較または `SHA256Mismatch` 判定へ進みます。
- `FilesAreEqualAsync(...)` が [`FileNotFoundException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.filenotfoundexception?view=net-8.0) をスローした場合は、[`FolderDiffService`](../Services/FolderDiffService.cs) 内でキャッチされます。これは列挙後・比較前に new 側ファイルが削除された場合に発生し、該当ファイルを `Removed` として分類し、警告を記録して走査を継続します。列挙時に発生する [`IOException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.ioexception?view=net-8.0)（シンボリックリンクのループなど）とは異なり、実行全体を停止させません。
- `FilesAreEqualAsync(...)` では、[`DirectoryNotFoundException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.directorynotfoundexception?view=net-8.0)、[`IOException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.ioexception?view=net-8.0)、[`UnauthorizedAccessException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.unauthorizedaccessexception?view=net-8.0)、[`NotSupportedException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.notsupportedexception?view=net-8.0) も想定される実行時失敗として扱い、old/new 両方の絶対パスを含む error ログを出したうえで例外型を変えずに再送出します。
- それ以外の予期しない例外は、`FilesAreEqualAsync(...)` の中で old/new 両方の絶対パスを含む "unexpected error" ログを出力したうえで、呼び出し元へ再送出されます。
- `PrecomputeIlCachesAsync()`、ディスクキャッシュ退避時の削除、書き込み後の読み取り専用化は best-effort です。比較結果や生成済みレポートは利用できるため、warning を記録して継続します。
- 例外に補足情報を付けたい場合も、汎用 [`Exception`](https://learn.microsoft.com/ja-jp/dotnet/api/system.exception?view=net-8.0) へ包み直すのではなく、元の例外をログに出したうえで `throw;` してください。元の例外型とスタックトレースを保つためです。

避けたい例:

```csharp
catch (Exception ex)
{
    throw new Exception($"Failed while diffing '{fileRelativePath}'.", ex);
}
```

推奨例:

```csharp
catch (Exception ex)
{
    _logger.LogMessage(
        AppLogLevel.Error,
        $"An error occurred while diffing '{file1AbsolutePath}' and '{file2AbsolutePath}'.",
        shouldOutputMessageToConsole: true,
        ex);
    throw;
}
```

- [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) に記録する詳細結果と `FilesAreEqualAsync(...)` の戻り値は、同じ判定を表していなければなりません。[`FolderDiffService`](../Services/FolderDiffService.cs) は bool 戻り値で `Unchanged` / `Modified` を決める一方、レポートは詳細結果として `SHA256Match`、`ILMismatch`、`TextMatch` などを表示します。たとえば `ILMismatch` を記録したのに `true` を返すと、一覧では `Unchanged` に入るのに詳細理由は mismatch になり、結果が矛盾します。

## 結果モデルとレポート仕様

[`FileDiffResultLists`](../Models/FileDiffResultLists.cs) が保持するもの:
- old/new の発見済みファイル一覧
- `Unchanged`、`Added`、`Removed`、`Modified` の最終バケット
- `SHA256Match`、`ILMatch`、`TextMatch`、`SHA256Mismatch`、`ILMismatch`、`TextMismatch` の詳細判定
- 無視対象ファイルの所在情報
- Modified と判定されたファイルのうち、`new` 側の更新日時が `old` 側より古いものの警告情報
- IL 比較で使用した逆アセンブラ表示ラベル
- レポートヘッダ用の逆アセンブラ利用可否プローブ結果（`DisassemblerAvailability`）

**Disassembler Availability テーブル — エッジケース:**
`DisassemblerHelper.ProbeAllCandidates()` は [`DiffPipelineExecutor.ExecuteScopedRunAsync()`](../Runner/DiffPipelineExecutor.cs) にてファイル比較の開始前に**無条件で**呼ばれます。ファイル種別や `SkipIL` 設定には依存しません。プローブ結果は `FileDiffResultLists.DisassemblerAvailability` に格納され、両レポート生成で参照されます。

| シナリオ | プローブ実行 | テーブル表示 | 内容 |
| --- | --- | --- | --- |
| .NET アセンブリを含む通常の実行 | はい | はい | 各ツールに Yes/No ＋ バージョンを表示 |
| 全ファイルがテキスト（.dll/.exe なし） | はい | はい | テーブルは表示される。IL 比較はどのファイルにも実行されない |
| `SkipIL = true` | はい | はい | テーブルは表示される。差分処理中の IL 比較はスキップされる |
| 逆アセンブラツールが一切見つからない | はい | はい | 全ツールが "No"（赤）と "N/A" で表示される |
| `DisassemblerAvailability` が null または空 | — | いいえ | ガードチェック `if (probeResults == null \|\| probeResults.Count == 0) return;` により出力を抑制 |

実際には `ProbeAllCandidates()` は候補セットがハードコードされているため常に非空のリストを返します。null/空のガードは防御的安全策として存在し、テスト（`GenerateDiffReport_HeaderOmitsAvailabilityTable_WhenProbeResultsAreNull` / `GenerateDiffReportHtml_HeaderOmitsAvailabilityTable_WhenProbeResultsAreNull`）でカバーされています。

ネストされた [`DiffSummaryStatistics`](../Models/FileDiffResultLists.cs) sealed レコード（`AddedCount`、`RemovedCount`、`ModifiedCount`、`UnchangedCount`、`IgnoredCount`）と `SummaryStatistics` 計算プロパティが、5 つのバケット数を一度に取得できる一貫したスナップショットを提供します。[`ReportGenerateService`](../Services/ReportGenerateService.cs) はレポートのサマリーセクションを書く際に `SummaryStatistics` を一度参照するため、各コレクションを個別に参照する必要はありません。

[`ReportGenerateService`](../Services/ReportGenerateService.cs) が前提としている仕様:
- 新しい実行前に `ResetAll()` が必ず呼ばれていること
- 前回の実行に由来する不要なエントリが詳細結果の [`Dictionary`](https://learn.microsoft.com/ja-jp/dotnet/api/system.collections.generic.dictionary-2?view=net-8.0) に残っていないこと
- IL のラベルは IL 比較時だけ存在すること
- レポート生成は、実行結果の読み取りであり、新しい比較を開始しないこと
- **テーブルのソート順**: Unchanged Files の行は diff-detail 結果（`SHA256Match` → `ILMatch` → `TextMatch`）でソートし、次にファイルパス昇順。Modified Files の行（および Timestamps Regressed 警告テーブル）は diff-detail 結果（`TextMismatch` → `ILMismatch` → `SHA256Mismatch`）でソートし、次に変更の重要度（`High` → `Medium` → `Low`）でソートし、次にファイルパス昇順。SHA256Mismatch 警告テーブルはファイルパスのアルファベット順でソート。Markdown および HTML レポートの両方に適用。
- **セクション別の列表示（Markdown vs HTML）**: Markdown レポートでは不要な列を直接削除する（例: Added/Removed テーブルは Status, File Path, Timestamp の 3 列。Ignored/SHA256Mismatch/Timestamps Regressed テーブルは Disassembler なしの 4 列）。HTML レポートでは、テーブル間の列幅同期の安定性を維持するため、すべてのテーブルが DOM 上に 8 列すべてを保持する。[`syncTableWidths()`](../Services/HtmlReport/diff_report.js) は各テーブルの `<colgroup>` 内 `<col>` 要素から合計幅を計算し、リサイズハンドルのドラッグ操作はテーブル間で共有される CSS カスタムプロパティを更新する。視覚的に非表示にする列は `<table>` 要素の CSS クラス（`hide-disasm`、`hide-col6`）で指定し、対応する `<col>`、`<th>`（`.col-diff-hd` / `.col-disasm-hd`）、`<td>`（`.col-diff` / `.col-disasm`）に `width: 0`、`visibility: hidden`、`border-color: transparent` を適用する。`syncTableWidths()` は非表示列の幅をスキップするため、非表示列を持つテーブルは正しく狭くなる。このアプローチにより、テーブル間で `<col>` 要素数が異なる問題、インライン差分行の `colspan` 値の不整合、ヘルパーメソッドの条件分岐ロジックに起因する不安定性を回避する。

<a id="guide-ja-config-runtime"></a>
## 設定と実行モード

既定値の正本は [`ConfigSettings`](../Models/ConfigSettings.cs) です。[`config.json`](../config.json) は override 用のファイルであり、省略したキーはコード既定値を維持します。`null` を与えたコレクションやキャッシュパスも既定値へ正規化されます。読み込み後、[`ConfigSettings.Validate()`](../Models/ConfigSettings.cs) で各設定値の範囲を検証します。制約違反があれば [`ConfigService`](../Services/ConfigService.cs) が全エラーを列挙した [`InvalidDataException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.invaliddataexception?view=net-8.0) をスローし、終了コード `3` で失敗します。検証対象の制約: [`MaxLogGenerations`](../Models/ConfigSettings.cs) >= `1`、[`TextDiffParallelThresholdKilobytes`](../Models/ConfigSettings.cs) >= `1`、[`TextDiffChunkSizeKilobytes`](../Models/ConfigSettings.cs) >= `1`、[`TextDiffChunkSizeKilobytes`](../Models/ConfigSettings.cs) < [`TextDiffParallelThresholdKilobytes`](../Models/ConfigSettings.cs)、[`SpinnerFrames`](../Models/ConfigSettings.cs) は 1 件以上の要素を含むこと。キーごとの説明は [README の設定表](../Models/ConfigSettings.cs) を参照してください。

**JSON 書式エラー**（最後のプロパティや配列要素の後のトレイリングカンマなど、よくあるミス）はバリデーション実行前に [`ConfigService`](../Services/ConfigService.cs) が検出します。エラーは実行ログへ書き込まれ、コンソールには赤字で行番号・バイト位置（内部の [`JsonException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.text.json.jsonexception?view=net-8.0) から取得）とトレイリングカンマへのヒントを表示します。標準 JSON はトレイリングカンマを許容しないため、`"Key": "value",}` のように末尾にカンマがある場合は削除してください。終了コードは `3` です。

### 設定のまとまり

| グループ | 主なキー | 目的 |
| --- | --- | --- |
| 対象範囲とレポート形状 | [`IgnoredExtensions`](../Models/ConfigSettings.cs), [`TextFileExtensions`](../Models/ConfigSettings.cs), [`ShouldIncludeUnchangedFiles`](../Models/ConfigSettings.cs), [`ShouldIncludeIgnoredFiles`](../Models/ConfigSettings.cs), [`ShouldIncludeILCacheStatsInReport`](../Models/ConfigSettings.cs), [`ShouldOutputFileTimestamps`](../Models/ConfigSettings.cs), [`ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp`](../Models/ConfigSettings.cs) | 比較対象、レポート粒度、更新日時逆転警告の制御。[`ShouldOutputFileTimestamps`](../Models/ConfigSettings.cs) は純粋な補助情報であり、更新日時は比較ロジックには一切使用しない。Unchanged / Modified 等の判定はファイル内容のみで行われる。 |
| IL 関連 | [`ShouldOutputILText`](../Models/ConfigSettings.cs), [`ShouldIgnoreILLinesContainingConfiguredStrings`](../Models/ConfigSettings.cs), [`ILIgnoreLineContainingStrings`](../Models/ConfigSettings.cs), [`SkipIL`](../Models/ConfigSettings.cs), [`DisassemblerBlacklistTtlMinutes`](../Models/ConfigSettings.cs) | IL 正規化・成果物出力・逆アセンブラ信頼性（ブラックリスト TTL）の制御 |
| インライン差分 | [`EnableInlineDiff`](../Models/ConfigSettings.cs), [`InlineDiffContextLines`](../Models/ConfigSettings.cs), [`InlineDiffMaxDiffLines`](../Models/ConfigSettings.cs), [`InlineDiffMaxOutputLines`](../Models/ConfigSettings.cs), [`InlineDiffMaxEditDistance`](../Models/ConfigSettings.cs), [`InlineDiffLazyRender`](../Models/ConfigSettings.cs) | HTML レポートでのインライン差分表示を制御 |
| 並列度 | [`MaxParallelism`](../Models/ConfigSettings.cs), [`TextDiffParallelThresholdKilobytes`](../Models/ConfigSettings.cs), [`TextDiffChunkSizeKilobytes`](../Models/ConfigSettings.cs), [`TextDiffParallelMemoryLimitMegabytes`](../Models/ConfigSettings.cs) | CPU 利用、チャンク粒度、大きいテキスト比較時の任意メモリ予算を制御 |
| キャッシュ | [`EnableILCache`](../Models/ConfigSettings.cs), [`ILCacheDirectoryAbsolutePath`](../Models/ConfigSettings.cs), [`ILCacheStatsLogIntervalSeconds`](../Models/ConfigSettings.cs), [`ILCacheMaxDiskFileCount`](../Models/ConfigSettings.cs), [`ILCacheMaxDiskMegabytes`](../Models/ConfigSettings.cs), [`ILPrecomputeBatchSize`](../Models/ConfigSettings.cs) | IL キャッシュの寿命、保存先、大規模ツリー向け事前計算バッチを制御 |
| ネットワーク共有向け | [`OptimizeForNetworkShares`](../Models/ConfigSettings.cs), [`AutoDetectNetworkShares`](../Models/ConfigSettings.cs) | 遅いストレージでの高 I/O 挙動抑制 |
| レポート出力 | [`ShouldGenerateHtmlReport`](../Models/ConfigSettings.cs) | Markdown レポートと並行してインタラクティブ HTML レビューレポートを生成するかを制御 |
| 監査ログ | [`ShouldGenerateAuditLog`](../Models/ConfigSettings.cs) | 改竄検知用のインテグリティハッシュを含む構造化 JSON 監査ログを生成するかを制御 |
| ログ / UX | [`MaxLogGenerations`](../Models/ConfigSettings.cs), [`SpinnerFrames`](../Models/ConfigSettings.cs) | ログファイルの保持世代数とコンソールスピナーアニメーションを制御 |

補足の内部既定値:
- [`ProgramRunner`](../ProgramRunner.cs) は、[`Common/Constants.cs`](../Common/Constants.cs) で定義した IL キャッシュ内部既定値として、[`Constants.IL_CACHE_MAX_MEMORY_ENTRIES_DEFAULT`](../Common/Constants.cs)（メモリ `2000` 件）、[`Constants.IL_CACHE_TIME_TO_LIVE_DEFAULT_HOURS`](../Common/Constants.cs)（TTL `12` 時間）、[`Constants.IL_CACHE_STATS_LOG_INTERVAL_DEFAULT_SECONDS`](../Common/Constants.cs)（内部統計ログ `60` 秒）を使います。両プロジェクトで共通利用するバイト換算値と日時フォーマットは [`FolderDiffIL4DotNet.Core/Common/CoreConstants.cs`](../FolderDiffIL4DotNet.Core/Common/CoreConstants.cs) にあります。
- これらは同日中の再実行で再利用を効かせつつ、短命なコンソールプロセスとしてメモリ消費やログ肥大を抑えるため、コード側で理由付きの既定値として維持しています。

### Myers diff algorithm

[`TextDiffer`](../FolderDiffIL4DotNet.Core/Text/TextDiffer.cs) は古典的な O(N×M) の LCS アプローチの代わりに Myers diff algorithm（O(D² + N + M) 時間・O(D²) 空間）を実装しています。編集グラフの図解・具体例・計算量分析・実装の詳細については **[Myers Diff Algorithm Guide](MYERS_DIFF_ALGORITHM.md)** を参照してください。

### インライン差分スキップの挙動

インライン差分は 3 通りの条件で抑制されます。いずれの場合も HTML レポートに `diff-skipped` スタイルの通知が直接表示されます（展開矢印なし）。

| トリガー | 設定 | 条件 | 表示メッセージ |
| --- | --- | --- | --- |
| 編集距離が大きすぎる | [`InlineDiffMaxEditDistance`](../Models/ConfigSettings.cs)（既定 `4000`） | `D` > [`InlineDiffMaxEditDistance`](../Models/ConfigSettings.cs) — 挿入・削除行数が多すぎる | `#N Inline diff skipped: edit distance too large (>M insertions/deletions in X vs Y lines). Increase InlineDiffMaxEditDistance in config to raise the limit.` |
| 出力行数が計算途中で上限に達した | [`InlineDiffMaxOutputLines`](../Models/ConfigSettings.cs)（既定 `10000`） | [`TextDiffer.Compute`](../FolderDiffIL4DotNet.Core/Text/TextDiffer.cs) が出力行数予算に達し、`Truncated` 行を末尾に追加して部分差分を返す | `... (diff output truncated — increase InlineDiffMaxOutputLines to see more)` |
| 差分結果が大きすぎる | [`InlineDiffMaxDiffLines`](../Models/ConfigSettings.cs)（既定 `10000`） | 計算後の差分出力行数合計（ハンクヘッダ含む）が閾値を超えた | `#N Inline diff skipped: diff too large (N diff lines; limit is M). Increase InlineDiffMaxDiffLines in config to enable.` |

編集距離超過と単一 Truncated 行のケースはいずれも `<details>` ラッパーなしのプレーン行としてレンダリングされるため、クリック不要でメッセージが見えます。[`InlineDiffMaxOutputLines`](../Models/ConfigSettings.cs) による打ち切りは `<details>` ブロック内に部分差分の末尾として表示されます。

> **ILMismatch エントリ**はさらに `ShouldOutputILText: true`（既定値）が必要です。[`HtmlReportGenerateService`](../Services/HtmlReportGenerateService.cs) は [`ILTextOutputService`](../Services/ILOutput/ILTextOutputService.cs) が `Reports/<label>/IL/old` と `Reports/<label>/IL/new` に書き出した `*_IL.txt` ファイルを直接読み込んでインライン差分を生成します。[`ShouldOutputILText`](../Models/ConfigSettings.cs) が `false` の場合、これらのファイルは生成されずインライン差分はサイレントに省略されます（`diff-skipped` 通知は表示されません）。

### 実行モードの決定

```mermaid
flowchart TD
    A["設定読込"] --> B{"OptimizeForNetworkShares?"}
    B -- "はい" --> E["Network-optimized mode"]
    B -- "いいえ" --> C{"AutoDetectNetworkShares 有効?"}
    C -- "いいえ" --> D["Local-optimized mode"]
    C -- "はい" --> F{"old または new が network path に見える?"}
    F -- "はい" --> E
    F -- "いいえ" --> D
```

ネットワークパスの判定は [`NetworkPathDetector`](../FolderDiffIL4DotNet.Core/IO/NetworkPathDetector.cs)（`FileSystemUtility` から抽出）で行います。`\\` プレフィックスの UNC パス、`\\?\UNC\` プレフィックスのデバイスパス、および `//` プレフィックスのスラッシュ形式 UNC パス（`//192.168.1.1/share` のような IP ベースの形式を含む）を検出します。`FileSystemUtility.IsLikelyNetworkPath` は `NetworkPathDetector.IsLikelyNetworkPath` に委譲します。

ネットワーク最適化モードの実際の影響:
- IL キャッシュの事前計算と先読みをスキップします。
- 自動決定時の並列度を `min(論理 CPU 数, 8)` に抑えます。
- テキスト比較は並列チャンク読みを避け、逐次比較を優先します。
- 正しさを保ったまま、リモート I/O の増幅を抑えます。

<a id="guide-ja-performance-runtime"></a>
## 性能と実行モード

主な性能機能:
- [`FolderDiffService`](../Services/FolderDiffService.cs) によるファイル比較の並列実行
- 任意の IL キャッシュウォームアップとディスク永続化
- ローカルの大きいテキスト向けチャンク並列比較
- 並列テキスト比較に対する任意のメモリ予算ベース抑制
- 大規模ツリー向けの IL 事前計算バッチ化
- 逆アセンブラ失敗時のブラックリスト
- 長い事前計算中でも進捗が止まって見えないようにスピナーを回す

注意が必要な変更:
- 既定並列度の変更はスループットと I/O 圧力の両方に効きます。
- キャッシュキー形状を変えると、ツール更新時の整合性を壊しやすくなります。
- 先読みを増やしすぎると NAS/SMB で退行しやすくなります。
- 大きいテキストファイルの挙動は、閾値・チャンクサイズ・任意メモリ予算の組み合わせで決まります。

<a id="guide-ja-doc-site"></a>
## ドキュメントサイトと API リファレンス

API リファレンス生成とサイト構築には DocFX を使います。

入力:
- `dotnet build` で出力される XML ドキュメントコメント
- [`README.md`](../README.md)、このガイド、[`doc/TESTING_GUIDE.md`](TESTING_GUIDE.md)
- [`docfx.json`](../docfx.json)、[`index.md`](../index.md)、[`toc.yml`](../toc.yml)、[`api/index.md`](../api/index.md)

出力:
- `_site/`: 生成済みドキュメントサイト
- `api/*.yml` と [`api/toc.yml`](../api/toc.yml): サイト構築に使う API メタデータ

更新手順の基本:
1. まずソリューションをビルドして最新の XML ドキュメントを出力します。
2. `docfx metadata docfx.json` を実行します。
3. `docfx build docfx.json` を実行します。
4. 大きめの API 変更では、マージ前に `_site/index.html` または CI artifact を確認します。

運用上の注意:
- 公開 namespace や public 型を移動・改名したら、同じ変更で DocFX 出力も更新してください。
- 公開 API を追加したら、生成結果が読める状態を維持するため XML コメントも必ず更新してください。
- `_site/` と `api/*.yml` はビルド生成物なのでコミットしません。

<a id="guide-ja-ci-release"></a>
## CI とリリースまわり

### ワークフロー概観

```
PR 作成時:
  ├─ dotnet.yml (build)             → ビルド + テスト + カバレッジ検証
  ├─ dotnet.yml (mutation-testing)  → Stryker ミューテーションテスト
  ├─ dotnet.yml (test-windows)     → Windows クロスプラットフォーム検証
  ├─ benchmark-regression.yml       → パフォーマンス回帰検知
  └─ codeql.yml                     → セキュリティ静的解析（C# + Actions）

main push 時:
  ├─ dotnet.yml (build)             → ビルド + テスト + カバレッジ検証
  ├─ dotnet.yml (test-windows)     → Windows クロスプラットフォーム検証
  ├─ benchmark-regression.yml       → パフォーマンス回帰検知 + ベースライン更新
  └─ codeql.yml                     → セキュリティ静的解析

v* タグ push 時:
  └─ release.yml                    → ビルド + テスト + 発行 + GitHub Release 作成
```

品質は6軸で守られています: **正しさ**（テスト）、**網羅性**（行/ブランチカバレッジ閾値）、**検出力**（ミューテーションテスト）、**速度**（ベンチマーク回帰検知）、**安全性**（CodeQL）、**互換性**（Windows）。

ワークフロー/設定:
- [.github/workflows/dotnet.yml](../.github/workflows/dotnet.yml)
- [.github/workflows/release.yml](../.github/workflows/release.yml)
- [.github/workflows/codeql.yml](../.github/workflows/codeql.yml)
- [.github/workflows/benchmark-regression.yml](../.github/workflows/benchmark-regression.yml)
- [.github/dependabot.yml](../.github/dependabot.yml)

現在の CI 挙動（`build` ジョブ — Ubuntu）:
- `main` 向け `push` / `pull_request` と `workflow_dispatch` で実行
- `actions/setup-dotnet` で [`global.json`](../global.json) を利用
- `FolderDiffIL4DotNet.sln` を restore / build
- DocFX を導入し、ドキュメントサイトを生成して `DocumentationSite` artifact としてアップロード
- 実 [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) を入れ、`DOTNET_ROLL_FORWARD=Major` 付きで優先逆アセンブラ経路を CI 上でも検証する
- テストプロジェクトが存在するときだけテストとカバレッジを実行
- `reportgenerator` でカバレッジ要約を生成
- 生成された Cobertura XML から total 行 `80%` / 分岐 `75%` のしきい値を強制する。同時にコア差分クラス（`FileDiffService`、`FolderDiffService`、`FileComparisonService`）のクラス単位しきい値（行 `90%` / 分岐 `85%`）も適用する
- publish 出力を `FolderDiffIL4DotNet` としてアップロード
- TRX とカバレッジ関連を `TestAndCoverage` としてアップロード

`test-windows` ジョブ — Windows:
- `build` ジョブと並行して `windows-latest` 上で実行
- restore / build / [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) インストール後、`DOTNET_ROLL_FORWARD=Major` 付きでフルテストスイートを実行
- Windows 固有のコードパスを CI 上でも検証する

`mutation-testing` ジョブ — Stryker:
- `pull_request` と `workflow_dispatch` でのみ実行（`main` への push では実行されない）
- [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) でプロダクションコードにミューテーションを注入し、テストが検出できるか検証する
- 設定は [`stryker-config.json`](../stryker-config.json) に定義
- ミューテーションスコアを GitHub Actions ジョブサマリに投稿
- 完全な Stryker HTML/JSON レポートを `StrykerReport` としてアップロード
- 閾値は `50%` — ミューテーションスコアがこれを下回るとジョブ失敗

`benchmark` ジョブ（手動のみ）:
- `workflow_dispatch` でのみ実行
- `FolderDiffIL4DotNet.Benchmarks` の [BenchmarkDotNet](https://benchmarkdotnet.org/) ベンチマークを実行し、結果を `BenchmarkResults` としてアップロード
- JSON と GitHub 形式で結果をエクスポートし、手動比較に使用

リリース自動化:
- [`.github/workflows/release.yml`](../.github/workflows/release.yml) は `v*` タグ push と、既存タグを明示指定する `workflow_dispatch` で実行します
- 再ビルド、カバレッジゲート付き再テスト、DocFX 再生成、アプリ publish、`*.pdb` 除去まで行います
- publish 出力 ZIP、ドキュメント ZIP、SHA-256 チェックサムを生成します
- 既存タグから GitHub Release を作成し、自動生成リリースノートを付けます

セキュリティ自動化:
- [`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml) は `csharp` と `actions` を対象に、`push` / `pull_request` / 週次スケジュール / `workflow_dispatch` で解析します
- Checkout ステップでは `fetch-depth: 0` を指定し、`csharp` の autobuild 時に [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) がフルコミット履歴からバージョン計算できるようにします
- Analyze ステップは `continue-on-error: true` を設定し、リポジトリの GitHub Default Setup コードスキャンが有効なとき `actions` 言語の SARIF アップロードが拒否されてもジョブが失敗しないようにします
- [`.github/dependabot.yml`](../.github/dependabot.yml) は `nuget` 依存関係と GitHub Actions の更新 PR を週次で作成します
- [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) で CI / リリース / セキュリティ設定ファイルの存在と主要設定の剥がれを検知します

パフォーマンスリグレッション検知:
- [`.github/workflows/benchmark-regression.yml`](../.github/workflows/benchmark-regression.yml) は `main` 向け `pull_request` と `push`、および `workflow_dispatch` で BenchmarkDotNet を実行します
- 全ベンチマーククラスの JSON 結果を単一レポートに統合し、[`benchmark-action/github-action-benchmark@v1`](https://github.com/benchmark-action/github-action-benchmark) を使用して `gh-benchmarks` ブランチに保存されたベースラインと比較します
- 閾値は `150%`（50% の劣化でジョブ失敗）。リグレッション時に PR コメントを投稿します
- `main` への push 時には結果が `gh-benchmarks` に新しいベースラインとして自動 push されます
- ベンチマーク成果物は常に `BenchmarkResults` としてアップロードされます

バージョニング:
- [`version.json`](../version.json) で [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) を利用
- Informational Version が埋め込まれ、生成レポートにも出力されます

<a id="guide-ja-skipped-tests"></a>
## ローカル実行でのスキップ（Skipped）テスト

ローカルで実行すると一部テストが **Skipped** と表示されることがあります。これは意図した挙動であり、バグではありません。

スキップされるテストとその理由:
- **[`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs)**（6 件）— 偽の `#!/bin/sh` シェルスクリプトを [`WriteExecutable`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) で生成し、フォールバック・ブラックリスト挙動を決定的に検証します。[`File.SetUnixFileMode`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.file.setunixfilemode?view=net-8.0) およびシェルスクリプトの実行は Windows では使えないため、`Skip.If(OperatingSystem.IsWindows(), ...)` を呼び出して Skipped を報告します。
- **[`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs)**（1 件）— `Deterministic=false` で 2 回ビルドした同一クラスライブラリを [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) が MVID 除外後に `ILMatch` と判定することを確認します。`Skip.IfNot(IsE2EEnabled(), ...)` と `Skip.If(!CanRunDotNetIldasm(), ...)` を使っているため、`FOLDERDIFF_RUN_E2E=true` と利用可能な [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/)（または [`dotnet ildasm`](https://www.nuget.org/packages/dotnet-ildasm/)）の両方が揃わない限り Skipped になります。

なぜ安全か:
- CI は Linux（`build` ジョブ）と Windows（`test-windows` ジョブ）の両方で動き、どちらもテストステップの前に実 [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) をインストールします。これにより、優先逆アセンブラ経路と Windows 固有コードパスは CI 上で検証されます。実逆アセンブラ E2E テストはさらに `FOLDERDIFF_RUN_E2E=true` が必要なため、ローカルの Skipped は前提条件不足または opt-in 未設定を示す場合があります。
- スキップ対象のテストは [`Xunit.SkippableFact`](https://www.nuget.org/packages/Xunit.SkippableFact/) の [`[SkippableFact]`](https://github.com/AArnott/Xunit.SkippableFact) を使うため、ランナーは Passed ではなく Skipped として別カウントで表示し、区別が明確になっています。
- これまで Skipped だったテストが **Failed** になった場合は実際の問題であり、調査が必要です。Skipped と Failed は異なる結果です。

スキップ対象テストの一覧と `Skip.If` パターンの詳細は [doc/TESTING_GUIDE.md](TESTING_GUIDE.md#testing-ja-isolation) を参照してください。

## 拡張ポイント

比較的安全に触りやすい場所:
- [`TextFileExtensions`](../Models/ConfigSettings.cs) の値追加
- [`ReportGenerateService`](../Services/ReportGenerateService.cs) へのレポートメタデータ追加
- オーケストレーション境界でのログ追加
- [`IFileSystemService`](../Services/IFileSystemService.cs)、[`IFolderDiffExecutionStrategy`](../Services/IFolderDiffExecutionStrategy.cs)、[`IFileComparisonService`](../Services/IFileComparisonService.cs)、[`IFileDiffService`](../Services/IFileDiffService.cs)、[`IILOutputService`](../Services/IILOutputService.cs)、[`IDotNetDisassembleService`](../Services/IDotNetDisassembleService.cs) を差し替えるテスト追加

高リスクな変更:
- `SHA256 -> IL -> text` の順番変更
- 実行スコープ状態の再利用
- パス判定を [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) から外す変更
- IL 比較で old/new のツール識別を混在させる変更
- 分離されていない静的可変キャッシュの導入

<a id="guide-ja-change-checklist"></a>
## 変更時チェックリスト

振る舞い変更をマージする前に確認:
1. [`Program.cs`](../Program.cs) は薄いままで、調停ロジックが [`ProgramRunner`](../ProgramRunner.cs) か下位サービスに留まっているか。
2. 実行ごとに新しい [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) と [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) が作られているか。
3. 新しい協調オブジェクトは、場当たり的に生成せず注入されているか。
4. [`FolderDiffService`](../Services/FolderDiffService.cs) が列挙や分類の前に `ResetAll()` を呼んでいるか。
5. レポート仕様が [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) の内容と乖離していないか。
6. IL 挙動を変えた場合、同一ツール強制と行除外仕様が明示されたままか。
7. 性能挙動を変えた場合、ローカルモードとネットワーク共有モードの両方を検討したか。
8. [`README.md`](../README.md)、このガイド、[`doc/TESTING_GUIDE.md`](TESTING_GUIDE.md) がユーザー向け挙動と同期しているか。
9. 変更した実行経路に対するテストを追加・更新したか。
10. CI / リリース / セキュリティ前提が変わったなら、[`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml)、[`.github/workflows/release.yml`](../.github/workflows/release.yml)、[`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml)、[`.github/dependabot.yml`](../.github/dependabot.yml)、[`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) をまとめて更新したか。

## クロスプラットフォームの注意点

このプロジェクトは CI を Linux と Windows の両方で実行しています。以下のパターンは実際に CI 失敗を引き起こしたものであり、プロダクションコードやテストを書く際に注意が必要です。

### パスセパレータの一貫性

Windows では `Path.GetRelativePath` が出力を `\` に正規化しますが、`Path.Combine` は第二引数のセパレータを正規化**しません**。そのため、次のようなラウンドトリップ：

```csharp
var rel = Path.GetRelativePath(baseDir, absolutePath);   // Windows では "sub\file.txt"
var rebuilt = Path.Combine(otherBase, rel);               // "/other\sub\file.txt"
```

は、元の `Path.Combine(otherBase, "sub/file.txt")` → `"/other\sub/file.txt"`（混在セパレータ）とは異なる文字列を生成します。`OrdinalIgnoreCase` 文字列比較は `\` と `/` を異なる文字として扱うため、`HashSet<string>` のルックアップがサイレントに失敗します。

**ルール**: サブディレクトリセパレータを含む相対パスを構成する場合は、`"sub/file.txt"` ではなく常に `Path.Combine("sub", "file.txt")` を使用すること。プロダクションコードでは、正規化なしに `Path.Combine` の出力と `Path.GetRelativePath` の出力を直接比較しないこと。

### タイマー解像度とタイミング依存テスト

Windows では `DateTime.UtcNow` と `Thread.Sleep` は OS のタイマー解像度（デフォルトで約 15.6ms）の影響を受けます。TTL を 1ms に設定し 20ms スリープするテストは、以下の理由で失敗する可能性があります：

1. `RegisterFailure()` が時刻 T で `DateTime.UtcNow` を記録する。
2. テストが `Assert.True(IsBlacklisted(...))` を呼ぶ — しかし `RegisterFailure` から `IsBlacklisted` までのコードパスが 1ms 以上かかった場合（負荷のかかった CI ランナーでは容易に起こり得る）、TTL はすでに期限切れとなりアサーションが失敗する。

**ルール**: タイミング依存テストでは TTL を最低 500ms、スリープ時間を TTL の最低 1.4 倍に設定すること。サブミリ秒の TTL は完全に避けること。

### `WebUtility.HtmlEncode` はバッククォートをエンコードしない

`System.Net.WebUtility.HtmlEncode` は `&`、`<`、`>`、`"`、`'` をエンコードしますが、バッククォート（`` ` ``）はエンコード**しません**。HTML レポートはファイルパスを JavaScript コンテキストに埋め込むため、テンプレートリテラル注入を防ぐためにバッククォートを明示的にエンコードする必要があります。`HtmlReportGenerateService.Helpers.cs` の `HtmlEncode()` ヘルパーは後処理ステップとして `.Replace("`", "&#96;")` を追加しています。

### ローカルツールのバージョン（`dotnet-stryker` 等）

CI ワークフローは [`.config/dotnet-tools.json`](../.config/dotnet-tools.json) を使用して `dotnet tool restore` を実行します。固定されたバージョンが NuGet から削除された場合、CI はリストアステップで失敗します。マニフェストを更新する前に、ツールバージョンが NuGet に存在することを必ず確認してください。

### テストフェイクのスレッドセーフティ

テストフェイク（モックサービス）がメソッド呼び出しをコレクションに記録する場合（例: `ReadChunkCalls.Add(...)`）、そのフェイクが `Parallel.ForEachAsync` などの並列コンテキストから呼ばれるなら `List<T>` ではなく `ConcurrentBag<T>` や `ConcurrentQueue<T>` を使用すること。スレッドセーフでない `List.Add` を並列実行すると例外がプロダクションコードのエラーハンドリングに黙殺的に捕捉され、テストが意図しないフォールバックパスに入り断続的に失敗する原因となります。

### `coverlet.collector` と `coverlet.runsettings` の互換性

- `coverlet.collector` 6.0.3 以降に[リグレッション](https://github.com/coverlet-coverage/coverlet/issues/1726)があり、`coverlet.runsettings` の `<Exclude>` / `<Include>` フィルタ使用時に `coverage.cobertura.xml` が生成されません。修正リリースまでバージョン 6.0.2 を使用してください。
- `opencover` フォーマットは `<DeterministicReport>true</DeterministicReport>` をサポートしません。決定論的レポートが必要な場合は `cobertura` のみを使用してください。

## デバッグのコツ

- まず `Logs/log_YYYYMMDD.log` を見て失敗箇所を特定してください。
- IL 比較で停止した場合は、ログやレポートに出る逆アセンブラ表示ラベルを確認してください。
- ネットワーク共有モードが想定外なら、設定フラグと自動判定結果の両方を確認してください。
- バケット分類がおかしい場合は、レポート整形より前に [`FileDiffResultLists`](../Models/FileDiffResultLists.cs) の投入順を追ってください。
- テストが順序依存になったら、まず実行スコープ状態のリークを疑ってください。
- Windows でバナーやコンソール出力が `?` になる場合は、プロセスが OEM コードページ（CP932/CP437 等）を使用しています。[`Program.cs`](../Program.cs) の `Main()` 先頭で [`Console.OutputEncoding`](https://learn.microsoft.com/ja-jp/DOTNET/api/system.console.outputencoding?view=net-8.0) = `Encoding.UTF8` を設定することで回避しています。Linux / macOS ではコンソールがすでに UTF-8 のため、この設定は実質ノーオペレーションです。

## HTML レポート: 整合性検証の技術メモ

### デュアルハッシュ・プレースホルダ方式

「Download as reviewed」ワークフローでは、レビュー済み HTML ファイル内に **2 つ**の SHA256 ハッシュをプレースホルダ方式で埋め込みます。これはファイルのハッシュをファイル自体に埋め込むという循環依存を解決するためです。

| 定数 | プレースホルダ | 用途 |
| --- | --- | --- |
| `__reviewedSha256__` | 64 個のゼロ（`000...0`） | 中間ハッシュ — このフィールドをプレースホルダに置き換えた状態の HTML のハッシュ。ハッシュ処理の内部で使用。 |
| `__finalSha256__` | 64 個の f（`fff...f`） | 最終ハッシュ — `__reviewedSha256__` 埋め込み後、このフィールドをプレースホルダに置き換えた状態の HTML のハッシュ。コンパニオン `.sha256` ファイルと完全に一致。 |

`downloadReviewed()` の 2 段階処理:
1. `__reviewedSha256__` プレースホルダをゼロに置換 → SHA256 計算 → ゼロを実際のハッシュに置換（第 1 ハッシュ埋め込み）。
2. `__finalSha256__` プレースホルダを f に置換 → SHA256 計算 → f を実際のハッシュに置換（第 2 ハッシュ埋め込み）。この最終ハッシュがコンパニオン `.sha256` ファイルにも書き出されます。

### Verify integrity: `.sha256` 専用検証

`verifyIntegrity()` は `.sha256` ファイルのみを受け付けます。レビュー済み HTML は「自分自身」であり、最終ハッシュが `__finalSha256__` に埋め込み済みのため、HTML ファイルの選択は不要です。関数は `.sha256` ファイルを読み取り、ハッシュを抽出して、埋め込み済みの `__finalSha256__` 定数と直接比較します。

### ブラウザの注意点: 動的作成した input 要素の `accept` 属性

一部のブラウザ（特に macOS Safari）は、動的に作成して即座にクリックした `<input type="file">` 要素の `accept` 属性を無視します。ファイルピッカーがフィルタなしで開き、全ファイルが選択可能になります。

**回避策**: `DOMContentLoaded` の初期化時に隠し `<input type="file" accept=".sha256">` 要素を事前作成し、`verifyIntegrity()` ではそれを再利用します。ユーザーが「Verify integrity」をクリックする時点で、input 要素は十分な時間 DOM に存在しているため、ブラウザが `accept` フィルタを認識・適用できます。フィルタをバイパスするブラウザへのフォールバックとして、`onchange` ガード（`file.name.endsWith('.sha256')`）も設けています。

### セマンティック変更の型名フォーマット

[`SimpleSignatureTypeProvider`](../Services/AssemblyMethodAnalyzer.cs) は常に**完全修飾 .NET 型名**（例: `System.String`、`System.Int32`、`System.Void`）を出力し、C# エイリアス（`string`、`int`、`void`）は使用しません。ジェネリック型パラメータは [`GenericContext`](../Services/AssemblyMethodAnalyzer.MetadataHelpers.cs) 経由で宣言名（例: `T`、`TKey`、`TValue`）に解決されます。`GenericContext` は `TypeDefinition.GetGenericParameters()` と `MethodDefinition.GetGenericParameters()` からパラメータ名を読み取ります。関数ポインタシグネチャは `delegate*<ParamTypes, ReturnType>` として展開され、カスタム修飾子は `modreq()`/`modopt()` 注釈として保持されます。[`MemberChangeEntry`](../Models/MemberChangeEntry.cs) の `MemberType`、`ReturnType`、`Parameters` フィールドはこの規約に従います。サンプル HTML の base64 ブロックも一致させる必要があります。
