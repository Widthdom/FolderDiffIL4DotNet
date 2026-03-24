# Testing Guide(English)

This document centralizes the project's testing strategy, execution commands, and practical guardrails for extending tests safely.

Related documents:
- [README.md](../README.md#readme-en-doc-map)
- [doc/DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md#guide-en-map)
- [api/index.md](../api/index.md)

<a id="testing-en-test-stack"></a>
## Test Stack

- Test project: [`FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`](../FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj)
- Framework: [`xUnit` `2.9.3`](https://www.nuget.org/packages/xunit/2.9.3) (`[Fact]` / `[Theory]` / [`[SkippableFact]`](https://github.com/AArnott/Xunit.SkippableFact))
- Runner: [`Microsoft.NET.Test.Sdk` `17.12.0`](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.12.0)
- Coverage collector: [`coverlet.collector` `6.0.4`](https://www.nuget.org/packages/coverlet.collector/6.0.4) (`XPlat Code Coverage`)
- Coverage configuration: [`coverlet.runsettings`](../coverlet.runsettings) (include/exclude filters, deterministic report, branch coverage, multi-format output)
- Mutation testing: [`Stryker.NET`](https://stryker-mutator.io/docs/stryker-net/introduction/) via [`stryker-config.json`](../stryker-config.json) (Standard mutation level, 80/60/50 thresholds)
- Local tool manifest: [`.config/dotnet-tools.json`](../.config/dotnet-tools.json) (`dotnet-reportgenerator-globaltool`, `dotnet-stryker`)
- Dynamic skip support: [`Xunit.SkippableFact` `1.5.23`](https://www.nuget.org/packages/Xunit.SkippableFact/1.5.23) ([`[SkippableFact]`](https://github.com/AArnott/Xunit.SkippableFact) + `Skip.If`)
- Target framework: [`.NET 8` / `net8.0`](https://learn.microsoft.com/en-us/dotnet/standard/frameworks)

<a id="testing-en-scope-map"></a>
## Current Test Scope Map

Current tree has `693` test methods (`656` `[Fact]`/`[SkippableFact]` + `37` `[Theory]` with `126` `[InlineData]` cases) in the latest full run (`dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj -p:UseAppHost=false --nologo`): `1` skipped (E2E test requiring a real disassembler binary).

| Area | Main test classes | What is validated |
| --- | --- | --- |
| Entry and configuration | [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs), [`ProgramRunnerTests`](../FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs), [`CliOptionsTests`](../FolderDiffIL4DotNet.Tests/CliOptionsTests.cs), [`ConfigServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs), [`ConfigSettingsTests`](../FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) | `Main` exit codes, typed [`ProgramRunner`](../ProgramRunner.cs) exit-code mapping for invalid arguments vs. config failures, phase ordering around validation vs. config loading, minimal end-to-end execution, CLI argument parsing via [`CliParser`](../Runner/CliParser.cs) (null/empty/positional-only args, every flag including `--help`/`--version`/`--config`/`--threads`/`--no-il-cache`/`--skip-il`/`--no-timestamp-warnings`/`--print-config`, unknown flag detection, combined flags, `--config`/`--threads` missing-value errors), code-defined config defaults (verified via `ConfigSettings.Default*` named constants) and override behavior, SHA256/timestamp warning console and report output, reflection-backed verification of internal IL cache defaults wired by [`ProgramRunner`](../ProgramRunner.cs); JSON syntax error reporting: trailing comma in object (`{"Key":"v",}`), trailing comma in array (`["a","b",]`), and multiline JSON with line-number verification in the [`InvalidDataException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.invaliddataexception?view=net-8.0) message; preflight write-permission check with fail-fast on [`IOException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.ioexception?view=net-8.0) (no silent swallowing) and cause-specific logging via `ILoggerService`; `--help` text "Tip:" section promoting `--print-config`; stderr `--print-config` hint on configuration error (exit code `3`) |
| Core diff flow | [`FolderDiffExecutionStrategyTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffExecutionStrategyTests.cs), [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs), [`FolderDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs), [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs), [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs), [`FileDiffResultListsTests`](../FolderDiffIL4DotNet.Tests/Models/FileDiffResultListsTests.cs) | Discovery filtering, auto-parallelism policy, classification (`Unchanged/Added/Removed/Modified`), diff detail labels, timestamp-regression detection only for **modified** files (unchanged files with reversed timestamps produce no warning), reset behavior, case-insensitive extension handling, propagated text-diff fallback behavior, permission/I/O failure handling, expected-vs-unexpected exception logging/rethrow behavior, large-batch classification without real disk I/O, IL-precompute batching for large trees, memory-budget-based throttling of large-text chunk comparison, multi-megabyte real-file text comparison, symlink-backed file classification, per-file hash/IL/text error handling without real disk, symlink-loop [`IOException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.ioexception?view=net-8.0) during enumeration (logged and rethrown), [`FileNotFoundException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.filenotfoundexception?view=net-8.0) during per-file comparison classified as `Removed` with a warning (both sequential and parallel modes), `DiffSummaryStatistics`/`SummaryStatistics` snapshot correctness, completion message routed through `ILoggerService` instead of direct `Console.WriteLine`, `CancellationToken` propagation through diff pipeline (pre-cancelled token throws `OperationCanceledException`), best-effort semantic analysis CA1031 fallback (warning logged but diff result unaffected when `AssemblyMethodAnalyzer.Analyze` fails on non-existent paths) |
| IL/disassembler behavior | [`ILOutputServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs), [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), [`DisassemblerBlacklistTests`](../FolderDiffIL4DotNet.Tests/Services/DisassemblerBlacklistTests.cs), [`DisassemblerHelperTests`](../FolderDiffIL4DotNet.Tests/Services/DisassemblerHelperTests.cs), [`DotNetDisassemblerCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/DotNetDisassemblerCacheTests.cs), [`DotNetDetectorTests`](../FolderDiffIL4DotNet.Tests/Core/Diagnostics/DotNetDetectorTests.cs), [`AssemblyMethodAnalyzerTests`](../FolderDiffIL4DotNet.Tests/Services/AssemblyMethodAnalyzerTests.cs), [`AssemblySemanticChangesSummaryTests`](../FolderDiffIL4DotNet.Tests/Models/AssemblySemanticChangesSummaryTests.cs), [`ChangeImportanceClassifierTests`](../FolderDiffIL4DotNet.Tests/Services/ChangeImportanceClassifierTests.cs) | Same-disassembler pairing, fallback behavior, blacklist logic (including TTL-boundary expiry: entry removed and tool retried after the 10-minute blacklist window), independent per-tool blacklist state, null/whitespace safety for `RegisterFailure`/`ResetFailure` (explicit branch coverage for the null guard true-branch), reset of non-existent command, concurrent `RegisterFailure` with 32 threads, concurrent TTL-expiry race with no exceptions, detection and command handling, failure-vs-non-.NET detection semantics; `ResolveExecutablePath` branch coverage for relative paths containing directory separators (found and not found), whitespace-only `PATH` environment variable, `PATH` entries that are empty strings, and commands found via `PATH` search; Windows-only `EnumerateExecutableNames` tests verifying no duplicate `.exe`/`.cmd`/`.bat` extension variants when the command already carries those suffixes; `ProbeAllCandidates` availability probing returns non-empty list with unique tool names including both `dotnet-ildasm` and `ilspycmd`; assembly semantic analysis detects `Modified` entries for method access/modifier changes and property/field type/access/modifier changes; `ChangeImportanceClassifier` correctly classifies all importance levels (High for public/protected removal and access narrowing, Medium for public addition and internal removal, Low for body-only and private additions), `WithClassifiedImportance` preserves all fields; `AssemblySemanticChangesSummary` importance counts, max importance, and entries sorted by importance; CA1031 catch-all fallback paths for corrupt/truncated/empty PE files (returns null instead of throwing), asymmetric valid-vs-corrupt assembly pair; streaming IL comparison (`StreamingFilteredSequenceEqual`) with MVID exclusion, configured ignore strings, empty inputs, length mismatch, all-excluded equality, and behavioral equivalence with legacy `SplitAndFilterIlLines` + `SequenceEqual`; `FilterIlLines` line filtering with and without exclusions; `SplitToLines` line splitting for LF, CRLF, empty, null, and trailing-newline inputs |
| Real disassembler E2E | [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) | Builds the same small class library twice with `Deterministic=false`, confirms the rebuilt DLLs differ by SHA256, and verifies that [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) still classifies them as `ILMatch` after MVID filtering |
| Caching | [`ILCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs), [`ILCachePrefetcherTests`](../FolderDiffIL4DotNet.Tests/Services/ILCachePrefetcherTests.cs), [`ILCacheConcurrencyTests`](../FolderDiffIL4DotNet.Tests/Services/EdgeCases/ILCacheConcurrencyTests.cs), [`ILCacheDiskFailureTests`](../FolderDiffIL4DotNet.Tests/Services/EdgeCases/ILCacheDiskFailureTests.cs) | memory/disk cache semantics, same-key updates at capacity, eviction coordination, keying behavior, concurrent Set/Get under memory and disk modes, LRU eviction under contention, TTL expiry races, disk I/O failure simulation (read-only directory, corrupted files, mid-operation directory deletion), IL-cache prefetch argument validation (null/empty input, invalid maxParallel), cache-disabled and null-cache prefetch handling, cache-hit counter tracking, constructor null-guard checks |
| Edge cases | [`DisassemblerBlacklistTtlRecoveryTests`](../FolderDiffIL4DotNet.Tests/Services/EdgeCases/DisassemblerBlacklistTtlRecoveryTests.cs), [`LargeFileComparisonTests`](../FolderDiffIL4DotNet.Tests/Services/EdgeCases/LargeFileComparisonTests.cs), [`SymlinkAndCircularDirectoryTests`](../FolderDiffIL4DotNet.Tests/Services/EdgeCases/SymlinkAndCircularDirectoryTests.cs), [`FolderDiffConcurrencyStressTests`](../FolderDiffIL4DotNet.Tests/Services/EdgeCases/FolderDiffConcurrencyStressTests.cs) | TTL recovery cycles and concurrent register/check for DisassemblerBlacklist, 4 MiB file chunk-parallel comparison, symlink loop/dangling handling, 500-file parallel classification determinism, simulated latency stress test |
| Golden file snapshots | [`GoldenFileSnapshotTests`](../FolderDiffIL4DotNet.Tests/Services/GoldenFileSnapshotTests.cs) | Structural validation of `doc/samples/diff_report.md` (section existence, section ordering, section-count-vs-summary consistency, disassembler availability table format, importance levels in modified files, legend tables, IL cache stats metrics, header metadata fields, warning types), report generation determinism tests (generate twice from same data, verify identical output for Markdown/HTML), structural verification of generated reports including metadata, file categories, diff detail labels, and importance levels |
| Reporting/logging/progress | [`ReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs), [`HtmlReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs), [`AuditLogGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/AuditLogGenerateServiceTests.cs), [`LoggerServiceTests`](../FolderDiffIL4DotNet.Tests/Services/LoggerServiceTests.cs), [`ProgressReportServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ProgressReportServiceTests.cs) | report sections/summary formatting, report-only warning responsibility, HTML report file creation, interactive checkbox/input element presence, section colour coding, localStorage sentinel, `ShouldGenerateHtmlReport=false` skip, HTML encoding of special characters (backtick/non-ASCII via `WebUtility.HtmlEncode`, Unicode/mixed-script XSS prevention, normal text passthrough), Content-Security-Policy meta tag presence and ordering (between charset and viewport), inline diff summary `#N` numbering aligned with the leftmost table column, log output behavior, shared log-file/date formats, progress reporting lifecycle, Unicode filenames (Japanese/Umlaut/Chinese) in Modified and Unchanged sections, large-file-count (10,500) summary statistics correctness, `InlineDiffMaxDiffLines` suppression (diff computed first; skipped when diff output line count exceeds threshold), legend table format, stat-table column headers and visible borders, `InlineDiffMaxEditDistance` code tag, diff-row background color, clipboard copy button, row hover highlight, table sort order (Unchanged: `SHA256Match` → `ILMatch` → `TextMatch` then path; Modified/Warnings: `TextMismatch` → `ILMismatch` → `SHA256Mismatch` then path), SHA256Mismatch warning detail table (file listing, alphabetical sort, table ordering before Timestamps Regressed, interleaved layout with warning messages immediately followed by detail tables), semantic summary caveat note presence and CSS styling in HTML report, Assembly Semantic Changes section excluded from Markdown report, structured JSON audit log generation (`audit_log.json`) with metadata/summary/file-entries/SHA256 integrity hashes, `ShouldGenerateAuditLog=false` skip, tamper detection via different report content producing different hashes, empty result handling, constructor null checks, reviewed HTML SHA256 integrity verification code presence (Web Crypto API `crypto.subtle.digest`, companion `.sha256` file, `__reviewedSha256__`/`__finalSha256__` sentinels, pre-created file input with `accept='.sha256'`, and `verifyIntegrity` self-verification function), Access/Modifiers `<code>` wrapping with arrow-aware formatting (`CodeWrapArrow`), disassembler availability table in Markdown/HTML report header (shown when probe results exist, omitted when null), `disassemblerAvailability` array in audit log JSON, interactive filter bar presence (importance/unchecked-only/search controls), filter bar placement inside `<!--CTRL-->...<!--/CTRL-->` markers for reviewed-mode stripping, `data-section`/`data-importance` attributes on file rows, `filter-hidden`/`filter-hidden-parent` CSS rules, `applyFilters()`/`resetFilters()` JS functions, `__filterIds__` exclusion from `collectState()`, `downloadReviewed()` filter-hidden class clearing before `outerHTML` capture, `downloadReviewed()` live page filter state restoration via `applyFilters()` after `outerHTML` capture |
| Core utility layer | [`FileComparerTests`](../FolderDiffIL4DotNet.Tests/Core/IO/FileComparerTests.cs), [`FileSystemUtilityTests`](../FolderDiffIL4DotNet.Tests/Core/IO/FileSystemUtilityTests.cs), [`PathValidatorTests`](../FolderDiffIL4DotNet.Tests/Core/IO/PathValidatorTests.cs), [`ProcessHelperTests`](../FolderDiffIL4DotNet.Tests/Core/Diagnostics/ProcessHelperTests.cs), [`SystemInfoTests`](../FolderDiffIL4DotNet.Tests/Core/Diagnostics/SystemInfoTests.cs), [`TextSanitizerTests`](../FolderDiffIL4DotNet.Tests/Core/Text/TextSanitizerTests.cs), [`TextDifferTests`](../FolderDiffIL4DotNet.Tests/Core/Text/TextDifferTests.cs), [`ConsoleRenderCoordinatorTests`](../FolderDiffIL4DotNet.Tests/Core/Console/ConsoleRenderCoordinatorTests.cs) | hashing/text compare, shared report-timestamp formatting, path/network detection (including `//`-prefixed forward-slash UNC and IP-based UNC paths), command tokenization, file-name/path sanitization, computer name and app version retrieval (`SystemInfo`), Myers diff algorithm correctness (identical/empty/added/removed lines, context lines, hunk headers, edit-distance cap, output-line truncation, large-file small-diff efficiency), console render coordinator thread-safety (`RenderSyncRoot`, spinner throttling, `MarkProgressRendered` timing), and the reusable helper contract now housed in `FolderDiffIL4DotNet.Core` |
| HTML report JavaScript | [`diff_report.test.js`](../JsTests/diff_report.test.js) (Jest/jsdom, 26 tests) | `formatTs` date formatting, `collectState` checkbox/text/textarea collection with filter-ID exclusion, `autoSave` localStorage persistence and status display, `applyFilters` importance/unchecked-only/search filtering with associated diff-row hiding, `resetFilters` default state restoration, `decodeDiffHtml` base64-UTF8 decoding (including multibyte), `collapseAll` details-element folding, `clearAll` input reset with confirm guard, DOMContentLoaded state restore from `__savedState__` and localStorage, reviewed-mode read-only enforcement, `verifyIntegrity` null-guard alert, `setupLazyDiff` lazy decode/insert on toggle with no-duplicate-decode guard |
| Architecture boundary | [`CoreSeparationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs), [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) | utility types stay in the `FolderDiffIL4DotNet.Core` assembly, the main assembly no longer defines the legacy `FolderDiffIL4DotNet.Utils` namespace, repository automation keeps coverage gates, release workflow, CodeQL, and Dependabot configured, and documentation coverage thresholds match the CI workflow |

Testability-related structure:
- [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) exercise the thin `Program.Main` entry point, and [`ProgramRunnerTests`](../FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) pin both the phase ordering inside [`ProgramRunner`](../ProgramRunner.cs) and the typed exit-code mapping at the application boundary, which reduces the risk of refactors accidentally loading config before argument validation fails or collapsing distinct failures back into one exit code.
- Diff pipeline services now expose interface seams ([`IFileDiffService`](../Services/IFileDiffService.cs), [`IILOutputService`](../Services/IILOutputService.cs), [`IFolderDiffService`](../Services/IFolderDiffService.cs), [`IDotNetDisassembleService`](../Services/IDotNetDisassembleService.cs), [`IILTextOutputService`](../Services/ILOutput/IILTextOutputService.cs)) so tests can replace collaborators directly.
- [`FolderDiffExecutionStrategy`](../Services/FolderDiffExecutionStrategy.cs) and [`FolderDiffService`](../Services/FolderDiffService.cs) accept [`IFileSystemService`](../Services/IFileSystemService.cs), which lets unit tests simulate enumeration failures, streaming discovery via [`EnumerateFiles(...)`](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.enumeratefiles?view=net-8.0), ignored-file capture, output-directory I/O failures, and large file sets without creating real directories.
- [`FileDiffService`](../Services/FileDiffService.cs) also accepts [`IFileComparisonService`](../Services/IFileComparisonService.cs), which lets unit tests simulate hash permission failures, IL-output write failures, and large-text chunk reads without creating real files.
- [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) carries per-run paths and network-mode flags, which keeps test setup explicit and avoids mutating shared global state.
- Core helper tests now live under [`FolderDiffIL4DotNet.Tests/Core/`](../FolderDiffIL4DotNet.Tests/Core/), and [`CoreSeparationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs) locks the assembly boundary so future refactors do not slide reusable helpers back into the executable project.
- [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) reads the checked-in GitHub workflow/config files directly, so removing coverage gates, tag-based release automation, CodeQL analysis, or Dependabot updates requires an explicit test update in the same change.
- [`AssemblyMethodAnalyzer`](../Services/AssemblyMethodAnalyzer.cs) and [`DotNetDisassembleService`](../Services/DotNetDisassembleService.cs) are split into partial class files (see DEVELOPER_GUIDE.md § Partial Class File Layout). Existing tests cover the same public API — partial class splits do not require new test files.
- [`FolderDiffExecutionStrategyTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffExecutionStrategyTests.cs), [`FolderDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs), and [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs) are marked with `Trait("Category", "Unit")`, the temp-directory-backed [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs) and [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs) are marked with `Trait("Category", "Integration")`, and [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) is marked with `Trait("Category", "E2E")` so the boundary stays explicit. The newest unit additions pin IL-precompute batching, text-diff memory-budget fallback behavior, symlink-loop enumeration errors, file-deleted-during-comparison classification, disassembler blacklist TTL expiry, `//`-prefix UNC detection, `DiffSummaryStatistics`/`SummaryStatistics` snapshot correctness, Unicode filename round-trip in report sections, large-file-count (10,500) summary statistics, and concurrent blacklist access under high contention.

Recommended starting points by change type:
- Entry point, CLI validation, or run orchestration changes: start with [`CliOptionsTests`](../FolderDiffIL4DotNet.Tests/CliOptionsTests.cs), [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs), and [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs).
- Per-file classification changes: start with [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs), then confirm with [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs).
- IL/disassembler or cache changes: start with [`ILOutputServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs), [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), [`DotNetDisassemblerCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/DotNetDisassemblerCacheTests.cs), and [`ILCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs).
- Project-boundary or reusable-helper changes: start with [`CoreSeparationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs) and the relevant tests under [`FolderDiffIL4DotNet.Tests/Core/`](../FolderDiffIL4DotNet.Tests/Core/).
- Report wording or section changes: start with [`ReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs).
- Audit log changes: start with [`AuditLogGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/AuditLogGenerateServiceTests.cs).

<a id="testing-en-run-tests"></a>
## Run Tests Locally

All tests:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo
```

With coverage (Cobertura XML + opencover, using `.runsettings`):

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

Run one class:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --filter "FullyQualifiedName~FolderDiffIL4DotNet.Tests.Services.FolderDiffServiceTests"
```

Run one test method:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --filter "FullyQualifiedName~Main_WithValidArguments_ReturnsSuccessAndGeneratesReport"
```

Run only the lightweight unit tests for folder classification:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --filter "Category=Unit"
```

Run the filesystem-backed integration tests:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --filter "Category=Integration"
```

Run only the real-disassembler end-to-end tests (requires `FOLDERDIFF_RUN_E2E=true`):

```bash
FOLDERDIFF_RUN_E2E=true dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --filter "Category=E2E"
```

Run performance benchmarks (BenchmarkDotNet):

```bash
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks

# Run a specific benchmark class
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks -- --filter *TextDiffer*
```

The `benchmark` CI job (workflow_dispatch only) runs all benchmarks with JSON and GitHub exporters and uploads `BenchmarkDotNet.Artifacts/` as a CI artifact.

CI-parity command (same as GitHub Actions test step):

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --configuration Release --no-build --nologo --settings coverlet.runsettings --logger "trx;LogFileName=test_results.trx" --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

Run mutation testing (Stryker.NET):

```bash
dotnet tool restore
dotnet tool run dotnet-stryker --config-file stryker-config.json
```

<a id="testing-en-coverage"></a>
## Coverage Reporting

After running with coverage, results are created under `TestResults/**/coverage.cobertura.xml`.

Latest full coverage run measured `74.04%` line coverage (`2665/3599`) and `71.63%` branch coverage (`697/973`).
CI fails if total coverage drops below `80%` line or `75%` branch.

Optional local summary generation (uses the local tool manifest):

```bash
dotnet tool restore
dotnet tool run reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:"MarkdownSummaryGithub;Cobertura;HtmlInline_AzurePipelines"
```

The [`coverlet.runsettings`](../coverlet.runsettings) file configures:
- **Include/Exclude filters**: Only `[FolderDiffIL4DotNet]*` and `[FolderDiffIL4DotNet.Core]*` assemblies are measured; test and benchmark assemblies are excluded.
- **Attribute exclusions**: `[ExcludeFromCodeCoverage]`, `[GeneratedCode]`, `[CompilerGenerated]`, and `[Obsolete]` attributed members are excluded.
- **Output formats**: Both `cobertura` (for CI threshold enforcement) and `opencover` (for detailed IDE analysis) are generated.
- **Deterministic report**: `DeterministicReport=true` ensures reproducible output across CI runs.

<a id="testing-en-ci-notes"></a>
## CI Integration Notes

Workflow/config files: [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml), [`.github/workflows/release.yml`](../.github/workflows/release.yml), [`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml), [`.github/dependabot.yml`](../.github/dependabot.yml)

- DocFX site generation runs before tests and publishes `_site/` as the `DocumentationSite` artifact.
- Tests and coverage run only when [`FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`](../FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj) exists.
- CI runs two jobs: the `build` job (Ubuntu) installs a real [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) tool before the test step and runs it with `DOTNET_ROLL_FORWARD=Major` so `Category=E2E` coverage guarantees the preferred disassembler path in GitHub Actions; the `test-windows` job (Windows) runs the same test suite on `windows-latest` — also with `dotnet-ildasm` installed — so the E2E test that was previously always skipped on Windows now executes on every push.
- `TestAndCoverage` artifact includes TRX and coverage outputs.
- `CoverageReport/SummaryGithub.md` is appended to GitHub Step Summary when present.
- A dedicated threshold step parses `coverage.cobertura.xml` and fails the workflow if total coverage falls below `80%` line or `75%` branch.
- [`.github/workflows/release.yml`](../.github/workflows/release.yml) runs on `v*` tags, rebuilds/tests/publishes the app, archives publish/docs output, and creates a GitHub Release from the pushed tag.
- [`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml) runs CodeQL for both `csharp` and `actions` on code changes plus a weekly schedule; uses `fetch-depth: 0` on checkout for [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) and `continue-on-error: true` on the Analyze step to tolerate the Default Setup conflict.
- [`.github/dependabot.yml`](../.github/dependabot.yml) enables weekly update PRs for NuGet and GitHub Actions.
- [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) keeps those repository-automation files under automated regression coverage.

<a id="testing-en-isolation"></a>
## Test Isolation and Environment Notes

- Most tests create unique temporary directories under [`Path.GetTempPath()`](https://learn.microsoft.com/en-us/dotnet/api/system.io.path.gettemppath?view=net-8.0) and clean them up in `Dispose`/`finally`.
- [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) temporarily writes [`config.json`](../config.json) under [`AppContext.BaseDirectory`](https://learn.microsoft.com/en-us/dotNet/API/system.appcontext.basedirectory?view=net-8.0) and restores original content.
- [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) temporarily rewires `PATH`/`HOME` and uses scripted fake tools to test fallback/blacklist logic deterministically; any test that pre-seeds a specific disassembler version into the version cache must also prepend a matching fake tool to `PATH` so that `GetVersionWithFallbacksAsync` finds the fake before the real tool installed on the CI runner, which would otherwise overwrite the seeded entry.
- [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) builds throwaway class libraries under temp directories and pins the E2E assertion to [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/); CI ensures that prerequisite and sets `DOTNET_ROLL_FORWARD=Major` for the test step.
- Some disassembler tests are skipped on Windows using [`[SkippableFact]`](https://github.com/AArnott/Xunit.SkippableFact) + `Skip.If(OperatingSystem.IsWindows(), ...)`, which reports them as **Skipped** in the test runner rather than silently passing. The [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) test is similarly skipped with `Skip.If(!CanRunDotNetIldasm(), ...)` when the tool is unavailable.
- Unit tests do not require globally installed real [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) or [`ilspycmd`](https://www.nuget.org/packages/ilspycmd/) for most scenarios because test doubles are used.
- Avoid adding static mutable test hooks. Prefer constructor injection plus [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) for per-run values.
- The test project has `<Nullable>enable</Nullable>` enabled but suppresses nullable warnings (`CS8600`, `CS8603`, `CS8604`, `CS8605`, `CS8618`, `CS8619`, `CS8620`, `CS8625`) and `xUnit1012` via `<NoWarn>` in the `.csproj`. This is intentional: test code deliberately passes `null` to verify argument validation, null-guard branches, and graceful failure paths. Suppressing these warnings avoids noisy false positives while keeping nullable analysis active for production code.

<a id="testing-en-updating-tests"></a>
## Adding or Updating Tests

- Keep tests deterministic: avoid network dependency, wall-clock assumptions, and global mutable state.
- Use unique temp roots per test class or test case.
- Always restore environment variables and temporary config files changed during tests.
- Prefer asserting observable behavior (result classification/report content/log side-effects) over internal implementation details.
- If test project path/name changes, update [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml) test/coverage conditions and any repository-automation assertions in [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs).
- If release or security automation changes, update [`.github/workflows/release.yml`](../.github/workflows/release.yml), [`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml), [`.github/dependabot.yml`](../.github/dependabot.yml), and [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) in the same change.
- If the public API surface changes, regenerate the DocFX site and make sure XML comments still describe the new members correctly.
- If user-visible execution behavior changes, also update [`README.md`](../README.md) and [`doc/DEVELOPER_GUIDE.md`](DEVELOPER_GUIDE.md) in the same change.
- If the runtime lifecycle or service boundaries change, confirm the terminology in tests still matches the developer guide.

---

# テストガイド（日本語）

このドキュメントは、プロジェクトのテスト戦略、実行手順、拡張時の注意点を集約したものです。

関連ドキュメント:
- [README.md](../README.md#readme-ja-doc-map)
- [doc/DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md#guide-ja-map)
- [api/index.md](../api/index.md)

<a id="testing-ja-test-stack"></a>
## テストスタック

- テストプロジェクト: [`FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`](../FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj)
- フレームワーク: [`xUnit` `2.9.3`](https://www.nuget.org/packages/xunit/2.9.3)（`[Fact]` / `[Theory]` / [`[SkippableFact]`](https://github.com/AArnott/Xunit.SkippableFact)）
- ランナー: [`Microsoft.NET.Test.Sdk` `17.12.0`](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.12.0)
- カバレッジ収集: [`coverlet.collector` `6.0.4`](https://www.nuget.org/packages/coverlet.collector/6.0.4)（`XPlat Code Coverage`）
- カバレッジ設定: [`coverlet.runsettings`](../coverlet.runsettings)（include/exclude フィルタ、決定論的レポート、ブランチカバレッジ、マルチフォーマット出力）
- ミューテーションテスト: [`Stryker.NET`](https://stryker-mutator.io/docs/stryker-net/introduction/)（[`stryker-config.json`](../stryker-config.json) による設定、Standard ミューテーションレベル、80/60/50 閾値）
- ローカルツールマニフェスト: [`.config/dotnet-tools.json`](../.config/dotnet-tools.json)（`dotnet-reportgenerator-globaltool`、`dotnet-stryker`）
- 動的スキップ: [`Xunit.SkippableFact` `1.5.23`](https://www.nuget.org/packages/Xunit.SkippableFact/1.5.23)（[`[SkippableFact]`](https://github.com/AArnott/Xunit.SkippableFact) + `Skip.If`）
- 対象フレームワーク: [`.NET 8` / `net8.0`](https://learn.microsoft.com/ja-jp/dotnet/standard/frameworks)

<a id="testing-ja-scope-map"></a>
## 現在のテスト範囲マップ

直近のフル実行（`dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj -p:UseAppHost=false --nologo`）のテストメソッド数は `694` 件（`657` `[Fact]`/`[SkippableFact]` + `37` `[Theory]`（`126` `[InlineData]` ケース付き））です。スキップ `1`（実際の逆アセンブラバイナリが必要な E2E テスト）。

| 領域 | 主なテストクラス | 主な検証内容 |
| --- | --- | --- |
| エントリーポイント/設定 | [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs), [`ProgramRunnerTests`](../FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs), [`CliOptionsTests`](../FolderDiffIL4DotNet.Tests/CliOptionsTests.cs), [`ConfigServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs), [`ConfigSettingsTests`](../FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) | `Main` の終了コード、引数不正と設定失敗を分ける [`ProgramRunner`](../ProgramRunner.cs) の型付き終了コード分類、引数検証と設定読込の順序、最小構成の実行、[`CliParser`](../Runner/CliParser.cs) による CLI 引数解析（null/空/位置引数のみ、`--help`/`--version`/`--config`/`--threads`/`--no-il-cache`/`--skip-il`/`--no-timestamp-warnings`/`--print-config` 全フラグ、未知フラグ検出、全フラグ組み合わせ、`--config`/`--threads` の値欠落エラー）、コード既定値（`ConfigSettings.Default*` 名前付き定数で検証）と override の設定挙動、更新日時警告のコンソール/レポート出力、[`ProgramRunner`](../ProgramRunner.cs) が内部 IL キャッシュ既定値をどう配線するかの検証、JSON 書式エラーの報告（オブジェクト末尾カンマ `{"Key":"v",}`、配列末尾カンマ `["a","b",]`、複数行 JSON での [`InvalidDataException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.invaliddataexception?view=net-8.0) メッセージへの行番号付与）、プリフライト書込権限チェックにおける [`IOException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.ioexception?view=net-8.0) の fail-fast 処理（サイレントスワロー排除）と `ILoggerService` 経由の原因別ログ出力、`--help` テキストの「Tip:」セクションで `--print-config` を紹介、設定エラー（終了コード `3`）時の stderr への `--print-config` ヒント出力 |
| 差分処理本体 | [`FolderDiffExecutionStrategyTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffExecutionStrategyTests.cs), [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs), [`FolderDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs), [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs), [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs), [`FileDiffResultListsTests`](../FolderDiffIL4DotNet.Tests/Models/FileDiffResultListsTests.cs) | 列挙フィルタ、自動並列度ポリシー、`Unchanged/Added/Removed/Modified` の分類、判定理由、**Modified と判定されたファイルのみ**を対象とした更新日時逆転検出（Unchanged ファイルは更新日時が逆転しても警告対象外）、状態リセット、拡張子大小無視、伝播したテキスト比較例外からのフォールバック、権限エラー/出力先 I/O 失敗、想定例外と想定外例外のログ/再スロー境界、大量ファイルの扱い、大規模ツリー向け IL 事前計算バッチ化、大きいテキスト比較のメモリ予算ベース抑制、複数 MiB の実ファイル比較、シンボリックリンク経由の分類、ファイル単位のハッシュ/IL/テキスト分岐の異常系、列挙時のシンボリックリンクループ [`IOException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.ioexception?view=net-8.0)（ログ出力のうえ再スロー）、比較前ファイル削除時の [`FileNotFoundException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.filenotfoundexception?view=net-8.0) を `Removed` 分類＋警告（逐次・並列両対応）、`DiffSummaryStatistics`/`SummaryStatistics` のスナップショット正確性、完了メッセージが直接 `Console.WriteLine` ではなく `ILoggerService` 経由で出力されることの検証、差分パイプラインへの `CancellationToken` 伝播（キャンセル済みトークンで `OperationCanceledException` がスローされること）、ベストエフォートセマンティック解析の CA1031 フォールバック（`AssemblyMethodAnalyzer.Analyze` が存在しないパスで失敗した場合に警告ログのみで差分結果に影響しないこと） |
| IL/逆アセンブラ | [`ILOutputServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs), [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), [`DisassemblerBlacklistTests`](../FolderDiffIL4DotNet.Tests/Services/DisassemblerBlacklistTests.cs), [`DisassemblerHelperTests`](../FolderDiffIL4DotNet.Tests/Services/DisassemblerHelperTests.cs), [`DotNetDisassemblerCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/DotNetDisassemblerCacheTests.cs), [`DotNetDetectorTests`](../FolderDiffIL4DotNet.Tests/Core/Diagnostics/DotNetDetectorTests.cs), [`AssemblyMethodAnalyzerTests`](../FolderDiffIL4DotNet.Tests/Services/AssemblyMethodAnalyzerTests.cs), [`AssemblySemanticChangesSummaryTests`](../FolderDiffIL4DotNet.Tests/Models/AssemblySemanticChangesSummaryTests.cs), [`ChangeImportanceClassifierTests`](../FolderDiffIL4DotNet.Tests/Services/ChangeImportanceClassifierTests.cs) | 同一逆アセンブラ比較、フォールバック、ブラックリスト（TTL 境界: 10 分のブラックリスト期間経過後にエントリが削除され、ツールが再試行されることを含む）、ツール独立状態、`RegisterFailure`/`ResetFailure` の null/空白文字ガード（null ガードの true 分岐を明示的にカバー）、存在しないコマンドの reset、32 スレッドの並行 `RegisterFailure`、TTL 切れ境界での並行呼び出し（例外なし）、検出・コマンド処理、判定失敗と非 .NET の区別；`ResolveExecutablePath` のブランチ網羅（ディレクトリ区切り文字を含む相対パス（存在あり/なし）、空白のみの `PATH` 環境変数、空文字列の PATH エントリ、PATH 検索によるコマンド発見）；Windows 専用 `EnumerateExecutableNames` テスト（`.exe`/`.cmd`/`.bat` 拡張子を持つコマンドに重複拡張子が追加されないことを検証）；`ProbeAllCandidates` 利用可否プローブが `dotnet-ildasm` と `ilspycmd` の両方を含む一意ツール名の非空リストを返すこと；アセンブリセマンティック解析がメソッドのアクセス/修飾子変更およびプロパティ/フィールドの型/アクセス/修飾子変更を `Modified` エントリとして検出；`ChangeImportanceClassifier` が全重要度レベルを正しく分類（public/protected 削除やアクセス縮小は High、public 追加や internal 削除は Medium、ボディのみ変更や private 追加は Low）、`WithClassifiedImportance` が全フィールドを保持；`AssemblySemanticChangesSummary` の重要度カウント・最大重要度・重要度順ソート、CA1031 catch-all フォールバック：破損/切り詰め/空の PE ファイルで例外ではなく null を返すこと、正常と破損の非対称アセンブリペア；ストリーミング IL 比較（`StreamingFilteredSequenceEqual`）の MVID 除外・設定文字列除外・空入力・長さ不一致・全行除外時の一致・従来の `SplitAndFilterIlLines` + `SequenceEqual` との動作等価性検証；`FilterIlLines` の除外あり/なし行フィルタリング；`SplitToLines` の LF・CRLF・空文字列・null・末尾改行入力の行分割 |
| 実逆アセンブラ E2E | [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) | `Deterministic=false` の同一クラスライブラリを 2 回ビルドし、再ビルド DLL が SHA256 では不一致でも、[`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) では MVID 除外後に `ILMatch` になることを検証 |
| キャッシュ | [`ILCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs)、[`ILCachePrefetcherTests`](../FolderDiffIL4DotNet.Tests/Services/ILCachePrefetcherTests.cs)、[`ILCacheConcurrencyTests`](../FolderDiffIL4DotNet.Tests/Services/EdgeCases/ILCacheConcurrencyTests.cs)、[`ILCacheDiskFailureTests`](../FolderDiffIL4DotNet.Tests/Services/EdgeCases/ILCacheDiskFailureTests.cs) | メモリ/ディスクキャッシュの保持、同一キー再保存、退避時の連動削除、キー生成、メモリ/ディスクモードでの並列 Set/Get、競合下の LRU 退去、TTL 期限切れレース、ディスク I/O 障害シミュレーション（読み取り専用ディレクトリ、破損ファイル、操作中ディレクトリ削除）、IL キャッシュプリフェッチの引数バリデーション（null/空入力、不正 maxParallel）、キャッシュ無効時および null キャッシュ時のプリフェッチ処理、キャッシュヒットカウンタの追跡、コンストラクタ null ガードチェック |
| エッジケース | [`DisassemblerBlacklistTtlRecoveryTests`](../FolderDiffIL4DotNet.Tests/Services/EdgeCases/DisassemblerBlacklistTtlRecoveryTests.cs)、[`LargeFileComparisonTests`](../FolderDiffIL4DotNet.Tests/Services/EdgeCases/LargeFileComparisonTests.cs)、[`SymlinkAndCircularDirectoryTests`](../FolderDiffIL4DotNet.Tests/Services/EdgeCases/SymlinkAndCircularDirectoryTests.cs)、[`FolderDiffConcurrencyStressTests`](../FolderDiffIL4DotNet.Tests/Services/EdgeCases/FolderDiffConcurrencyStressTests.cs) | DisassemblerBlacklist の TTL 復旧サイクルと並列 register/check、4 MiB ファイルのチャンク並列比較、シンボリックリンクループ/ダングリング処理、500 ファイル並列分類の決定論性、レイテンシシミュレーション付きストレステスト |
| ゴールデンファイルスナップショット | [`GoldenFileSnapshotTests`](../FolderDiffIL4DotNet.Tests/Services/GoldenFileSnapshotTests.cs) | `doc/samples/diff_report.md` の構造検証（セクション存在、セクション順序、セクション件数と Summary の整合、逆アセンブラ利用可否テーブル形式、Modified ファイルの重要度レベル、凡例テーブル、IL Cache Stats メトリクス、ヘッダーメタデータフィールド、警告タイプ）、レポート生成の決定論テスト（同一データから2回生成し Markdown/HTML の同一出力を検証）、生成レポートの構造検証（メタデータ、ファイルカテゴリ、差分詳細ラベル、重要度レベル） |
| レポート/ログ/進捗 | [`ReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs)、[`HtmlReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/HtmlReportGenerateServiceTests.cs)、[`AuditLogGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/AuditLogGenerateServiceTests.cs)、[`LoggerServiceTests`](../FolderDiffIL4DotNet.Tests/Services/LoggerServiceTests.cs)、[`ProgressReportServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ProgressReportServiceTests.cs) | レポート出力内容、ログ動作、共有ログ書式、進捗報告ライフサイクル、HTML レポートのファイル生成・チェックボックス/入力要素の存在・セクション色付け・localStorage センチネル・`ShouldGenerateHtmlReport=false` スキップ・特殊文字の HTML エンコード（`WebUtility.HtmlEncode` によるバッククォート・非 ASCII 対応、Unicode/混在スクリプト XSS 防止、通常テキスト保持）、Content-Security-Policy メタタグの存在と配置順序（charset と viewport の間）、インライン差分サマリーの `#N` 番号と左端 `#` 列の整合、Modified・Unchanged セクションでの Unicode ファイル名（日本語/ウムラウト/中国語）のラウンドトリップ、10,500 件の大件数サマリー統計の正確性、`InlineDiffMaxDiffLines` による抑制（差分を計算した後、差分出力行数が閾値を超えた場合にスキップ）、凡例テーブル形式、stat-table 列ヘッダと可視ボーダー、`InlineDiffMaxEditDistance` code タグ、diff-row 背景色、クリップボードコピーボタン、行ホバーハイライト、テーブルソート順（Unchanged: `SHA256Match` → `ILMatch` → `TextMatch` 後にパス昇順、Modified/Warnings: `TextMismatch` → `ILMismatch` → `SHA256Mismatch` 後にパス昇順）、SHA256Mismatch 警告詳細テーブル（ファイル一覧、アルファベット順ソート、Timestamps Regressed テーブルの前に表示、各警告メッセージの直下に詳細テーブルを配置するインターリーブレイアウト）、HTML・Markdown 両レポートにおけるセマンティックサマリー注意書きの存在と CSS スタイル、構造化 JSON 監査ログ生成（`audit_log.json`）―メタデータ/サマリー/ファイルエントリ/SHA256 インテグリティハッシュ、`ShouldGenerateAuditLog=false` スキップ、異なるレポート内容で異なるハッシュが生成される改竄検知、空結果処理、コンストラクタ null チェック、レビュー済み HTML の SHA256 整合性検証コード存在確認（Web Crypto API `crypto.subtle.digest`、コンパニオン `.sha256` ファイル、`__reviewedSha256__`/`__finalSha256__` センチネル、`accept='.sha256'` 付き事前作成ファイル入力、`verifyIntegrity` 自己検証関数）、Access・Modifiers 列の `<code>` ラッピングと矢印対応フォーマット（`CodeWrapArrow`）、Markdown/HTML レポートヘッダの逆アセンブラ利用可否テーブル（プローブ結果がある場合は表示、null の場合は非表示）、監査ログ JSON の `disassemblerAvailability` 配列、インタラクティブフィルターバーの存在（重要度/ファイル種別/未チェックのみ/検索コントロール）、フィルターバーが `<!--CTRL-->...<!--/CTRL-->` マーカー内に配置されレビュー済みモードで除去されること、ファイル行の `data-section`/`data-importance` 属性、`filter-hidden`/`filter-hidden-parent` CSS ルール、`applyFilters()`/`resetFilters()` JS 関数、`__filterIds__` による `collectState()` からの除外、`downloadReviewed()` が `outerHTML` キャプチャ前に filter-hidden クラスをクリアすること、`downloadReviewed()` が `outerHTML` キャプチャ後に `applyFilters()` を呼び出しライブページのフィルタ状態を復元すること |
| Core ユーティリティ層 | [`FileComparerTests`](../FolderDiffIL4DotNet.Tests/Core/IO/FileComparerTests.cs), [`FileSystemUtilityTests`](../FolderDiffIL4DotNet.Tests/Core/IO/FileSystemUtilityTests.cs), [`PathValidatorTests`](../FolderDiffIL4DotNet.Tests/Core/IO/PathValidatorTests.cs), [`ProcessHelperTests`](../FolderDiffIL4DotNet.Tests/Core/Diagnostics/ProcessHelperTests.cs), [`SystemInfoTests`](../FolderDiffIL4DotNet.Tests/Core/Diagnostics/SystemInfoTests.cs), [`TextSanitizerTests`](../FolderDiffIL4DotNet.Tests/Core/Text/TextSanitizerTests.cs), [`TextDifferTests`](../FolderDiffIL4DotNet.Tests/Core/Text/TextDifferTests.cs), [`ConsoleRenderCoordinatorTests`](../FolderDiffIL4DotNet.Tests/Core/Console/ConsoleRenderCoordinatorTests.cs) | ハッシュ/テキスト比較、共有タイムスタンプ書式、パス/ネットワーク判定（`//` プレフィックスのスラッシュ形式 UNC および IP ベース UNC パスを含む）、コマンド分解、ファイル名/パス整形、コンピュータ名・アプリバージョン取得（`SystemInfo`）、Myers diff アルゴリズムの正確性（同一/空/追加/削除行、コンテキスト行、ハンクヘッダ、編集距離上限、出力行数切り詰め、大ファイル小差分の効率性）、コンソールレンダーコーディネータのスレッド安全性（`RenderSyncRoot`、スピナースロットリング、`MarkProgressRendered` タイミング）、`FolderDiffIL4DotNet.Core` に移した再利用 helper の契約確認 |
| HTML レポート JavaScript | [`diff_report.test.js`](../JsTests/diff_report.test.js)（Jest/jsdom、47 テスト） | `formatTs` 日付フォーマット、`collectState` チェックボックス/テキスト/テキストエリア収集とフィルタ ID 除外、`autoSave` localStorage 永続化とステータス表示、`getFileTypeCategory` 拡張子分類（dll/exe/config/resource/other、大文字小文字無視）、`applyFilters` ファイル種別/重要度/未チェックのみ/検索フィルタリングと関連 diff-row の非表示、`resetFilters` デフォルト状態復元、`decodeDiffHtml` base64-UTF8 デコード（マルチバイト文字含む）、`collapseAll` details 要素の折りたたみ、`clearAll` 入力リセットと confirm ガード、DOMContentLoaded 時の `__savedState__` および localStorage からの状態復元、レビュー済みモードの読み取り専用化、`verifyIntegrity` null ガードアラート、`setupLazyDiff` トグル時の遅延デコード/挿入と重複デコード防止 |
| アーキテクチャ境界 | [`CoreSeparationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs), [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) | utility 型が `FolderDiffIL4DotNet.Core` アセンブリに残り、実行ファイル側へ旧 `FolderDiffIL4DotNet.Utils` 名前空間が戻らないこと、カバレッジゲート、リリースワークフロー、CodeQL、Dependabot の設定が維持されること、ドキュメントのカバレッジ閾値が CI ワークフローと一致すること |

テスタビリティに関する構成:
- [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) は薄い `Program.Main` を対象にし、[`ProgramRunnerTests`](../FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) は [`ProgramRunner`](../ProgramRunner.cs) 内のフェーズ順序と型付き終了コード分類を固定します。これにより、引数検証より先に設定読込へ進んでしまう回帰や、異なる失敗理由が再び同じ終了コードへ潰れる回帰を防ぎつつ、静的状態への結合を減らしています。
- 差分パイプラインの主要サービスは [`IFileDiffService`](../Services/IFileDiffService.cs), [`IILOutputService`](../Services/IILOutputService.cs), [`IFolderDiffService`](../Services/IFolderDiffService.cs), [`IDotNetDisassembleService`](../Services/IDotNetDisassembleService.cs), [`IILTextOutputService`](../Services/ILOutput/IILTextOutputService.cs) の差し替えポイントを持ちます。
- [`FolderDiffExecutionStrategy`](../Services/FolderDiffExecutionStrategy.cs) と [`FolderDiffService`](../Services/FolderDiffService.cs) は [`IFileSystemService`](../Services/IFileSystemService.cs) を受け取れるため、ユニットテストでは実ファイルを作らずに列挙失敗・[`EnumerateFiles(...)`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.directory.enumeratefiles?view=net-8.0) ベースの遅延列挙・無視ファイル記録・出力先 I/O 失敗・大量ファイル入力を再現できます。
- [`FileDiffService`](../Services/FileDiffService.cs) は [`IFileComparisonService`](../Services/IFileComparisonService.cs) も受け取れるため、ユニットテストでは実ファイルを作らずにハッシュ権限エラー・IL 出力失敗・大きいテキスト比較のチャンク読み出しを再現できます。
- [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) が実行単位のパスやネットワークモードを保持するため、テストセットアップで共有グローバル状態を書き換える必要がありません。
- Core helper のテストは [`FolderDiffIL4DotNet.Tests/Core/`](../FolderDiffIL4DotNet.Tests/Core/) へまとめ、[`CoreSeparationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs) でアセンブリ境界も固定しています。これにより、再利用 helper が再び実行ファイル側へ混ざる回帰を防ぎます。
- [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) は、コミット済みの GitHub 設定ファイルを直接読んで検証します。これにより、カバレッジゲート、タグ起点のリリース自動化、CodeQL、Dependabot を外す変更は、同じ差分でテスト更新が必要になります。
- [`AssemblyMethodAnalyzer`](../Services/AssemblyMethodAnalyzer.cs) と [`DotNetDisassembleService`](../Services/DotNetDisassembleService.cs) は partial class ファイルに分割されています（DEVELOPER_GUIDE.md § Partial Class ファイル構成を参照）。既存テストは同一の公開 API を検証しており、partial class 分割に伴う新規テストファイルの追加は不要です。
- [`FolderDiffExecutionStrategyTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffExecutionStrategyTests.cs)、[`FolderDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs)、[`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs) には `Trait("Category", "Unit")`、実ディレクトリを使う [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs) と [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs) には `Trait("Category", "Integration")`、実逆アセンブラを使う [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) には `Trait("Category", "E2E")` を付け、境界を明示しています。今回の unit 追加では、IL 事前計算のバッチ化、テキスト比較メモリ予算フォールバック、シンボリックリンクループ列挙エラー、比較前ファイル削除の分類、逆アセンブラ ブラックリスト TTL 境界、`//` プレフィックス UNC 検出、`DiffSummaryStatistics`/`SummaryStatistics` のスナップショット正確性、レポートセクションでの Unicode ファイル名ラウンドトリップ、10,500 件の大件数サマリー統計、高競合下でのブラックリスト並行アクセスも固定しています。

変更種別ごとの出発点:
- エントリーポイント、CLI 引数検証、実行オーケストレーション変更: [`CliOptionsTests`](../FolderDiffIL4DotNet.Tests/CliOptionsTests.cs)、[`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs)、[`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs)
- ファイル単位の分類変更: [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs) を先に見て、最後に [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs)
- IL/逆アセンブラ/キャッシュ変更: [`ILOutputServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs), [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), [`DotNetDisassemblerCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/DotNetDisassemblerCacheTests.cs), [`ILCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs)
- プロジェクト境界や再利用 helper の変更: [`CoreSeparationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs) と [`FolderDiffIL4DotNet.Tests/Core/`](../FolderDiffIL4DotNet.Tests/Core/) 配下の対象テスト
- レポート文言やセクション変更: [`ReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs)
- 監査ログ変更: [`AuditLogGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/AuditLogGenerateServiceTests.cs)

<a id="testing-ja-run-tests"></a>
## ローカルでのテスト実行

全テスト実行:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false
```

カバレッジ付き実行（Cobertura XML + opencover、`.runsettings` 使用）:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

クラス単位実行:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --filter "FullyQualifiedName~FolderDiffIL4DotNet.Tests.Services.FolderDiffServiceTests"
```

メソッド単位実行:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --filter "FullyQualifiedName~Main_WithValidArguments_ReturnsSuccessAndGeneratesReport"
```

[`FolderDiffService`](../Services/FolderDiffService.cs) の軽量ユニットテストだけを回す場合:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --filter "Category=Unit"
```

実ディレクトリを使う統合テストだけを回す場合:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --filter "Category=Integration"
```

実逆アセンブラの E2E テストだけを回す場合（`FOLDERDIFF_RUN_E2E=true` が必要）:

```bash
FOLDERDIFF_RUN_E2E=true dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --filter "Category=E2E"
```

パフォーマンスベンチマーク（BenchmarkDotNet）を実行する場合:

```bash
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks

# 特定のベンチマーククラスだけを実行
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks -- --filter *TextDiffer*
```

`benchmark` CI ジョブ（workflow_dispatch のみ）はすべてのベンチマークを JSON および GitHub エクスポーター付きで実行し、`BenchmarkDotNet.Artifacts/` を CI アーティファクトとしてアップロードします。

CI 同等コマンド（GitHub Actions と同じ test ステップ）:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --configuration Release --no-build --nologo --settings coverlet.runsettings --logger "trx;LogFileName=test_results.trx" --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

ミューテーションテスト実行（Stryker.NET）:

```bash
dotnet tool restore
dotnet tool run dotnet-stryker --config-file stryker-config.json
```

<a id="testing-ja-coverage"></a>
## カバレッジレポート

カバレッジ付き実行後、`TestResults/**/coverage.cobertura.xml` が生成されます。

直近のフルカバレッジ実行では、行カバレッジ `74.04%`（`2665/3599`）、分岐カバレッジ `71.63%`（`697/973`）でした。
CI では total の最小値として、行 `80%` / 分岐 `75%` を下回ると失敗します。

ローカルで要約を作る場合（ローカルツールマニフェストを使用）:

```bash
dotnet tool restore
dotnet tool run reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:"MarkdownSummaryGithub;Cobertura;HtmlInline_AzurePipelines"
```

[`coverlet.runsettings`](../coverlet.runsettings) の設定内容:
- **Include/Exclude フィルタ**: `[FolderDiffIL4DotNet]*` と `[FolderDiffIL4DotNet.Core]*` アセンブリのみ計測対象。テストおよびベンチマークアセンブリは除外。
- **属性除外**: `[ExcludeFromCodeCoverage]`、`[GeneratedCode]`、`[CompilerGenerated]`、`[Obsolete]` 属性付きメンバーを除外。
- **出力フォーマット**: `cobertura`（CI 閾値チェック用）と `opencover`（IDE 詳細分析用）の両方を生成。
- **決定論的レポート**: `DeterministicReport=true` で CI 実行間の再現性を確保。

<a id="testing-ja-ci-notes"></a>
## CI 連携メモ

ワークフロー/設定: [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml), [`.github/workflows/release.yml`](../.github/workflows/release.yml), [`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml), [`.github/dependabot.yml`](../.github/dependabot.yml)

- テスト前に DocFX サイト生成を実行し、`_site/` を `DocumentationSite` artifact として公開します。
- [`FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`](../FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj) が存在する場合のみテスト/カバレッジを実行します。
- CI は 2 つのジョブで構成されます。`build` ジョブ（Ubuntu）はテスト前に実 [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) をインストールし `DOTNET_ROLL_FORWARD=Major` 付きで `Category=E2E` の逆アセンブラ経路も実行します。`test-windows` ジョブ（Windows）は同じテストスイートを `windows-latest` 上で実行し（`dotnet-ildasm` もインストール済み）、これまで Windows では常にスキップされていた E2E テストも push のたびにフルで動作するようになりました。
- `TestAndCoverage` アーティファクトに TRX とカバレッジ関連ファイルを格納します。
- `CoverageReport/SummaryGithub.md` があれば GitHub Step Summary に追記されます。
- 専用のしきい値チェックで `coverage.cobertura.xml` を解析し、total 行 `80%` / 分岐 `75%` を下回るとワークフローを失敗させます。
- [`.github/workflows/release.yml`](../.github/workflows/release.yml) は `v*` タグで実行し、再ビルド/再テスト/publish 後に publish 出力とドキュメントをアーカイブし、push されたタグから GitHub Release を作成します。
- [`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml) は `csharp` と `actions` に対する CodeQL をコード変更時と週次で実行します。[Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) 向けに Checkout で `fetch-depth: 0` を指定し、Default Setup との競合を吸収するため Analyze ステップに `continue-on-error: true` を設定しています。
- [`.github/dependabot.yml`](../.github/dependabot.yml) は NuGet と GitHub Actions の更新 PR を週次で有効化します。
- [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) が、これらの設定ファイルも回帰テスト対象に含めます。

<a id="testing-ja-isolation"></a>
## テスト分離と実行環境の注意

- 多くのテストは [`Path.GetTempPath()`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.path.gettemppath?view=net-8.0) 配下に一意ディレクトリを作成し、`Dispose`/`finally` で後始末します。
- [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) は [`AppContext.BaseDirectory`](https://learn.microsoft.com/ja-jp/dotNet/API/system.appcontext.basedirectory?view=net-8.0) 配下の [`config.json`](../config.json) を一時書き換えし、必ず復元します。
- [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) は `PATH`/`HOME` を一時変更し、擬似ツールスクリプトでフォールバック/ブラックリスト挙動を決定的に検証します。バージョンキャッシュに特定バージョンを事前投入するテストは、`GetVersionWithFallbacksAsync` が実ツールより先に擬似ツールを解決できるよう、同じバージョンを返す偽スクリプトも `PATH` に追加する必要があります（CI ランナーに実ツールがインストールされているため、追加しないとキャッシュ投入値が上書きされます）。
- [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) は temp ディレクトリ上に一時クラスライブラリをビルドし、[`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) 固定で E2E 検証します。CI では [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) のインストールと `DOTNET_ROLL_FORWARD=Major` でこの前提を満たします。
- 逆アセンブラ関連の一部テストは Windows では [`[SkippableFact]`](https://github.com/AArnott/Xunit.SkippableFact) + `Skip.If(OperatingSystem.IsWindows(), ...)` によりスキップされます。これにより、テストが「成功」扱いで素通りするのではなく、テストランナー上で**Skipped（スキップ）**として明示的に報告されます。[`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) も同様に、ツールが存在しない場合は `Skip.If(!CanRunDotNetIldasm(), ...)` でスキップします。
- 多くの単体テストは実ツールのグローバルインストールを不要とします（テストダブル利用）。
- 静的な可変テストフックは追加せず、実行単位の値はコンストラクタ注入と [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) で渡してください。
- テストプロジェクトでは `<Nullable>enable</Nullable>` を有効にしていますが、nullable 警告（`CS8600`、`CS8603`、`CS8604`、`CS8605`、`CS8618`、`CS8619`、`CS8620`、`CS8625`）および `xUnit1012` を `.csproj` の `<NoWarn>` で抑制しています。これは意図的な設計です：テストコードでは引数バリデーション、null ガード分岐、エラー時の正常動作を検証するために意図的に `null` を渡しています。これらの警告を抑制することで、本番コードの nullable 解析を維持しつつ、テストコードでの誤検知ノイズを回避しています。

<a id="testing-ja-updating-tests"></a>
## テスト追加・更新時の方針

- 非決定要因（ネットワーク、時刻依存、グローバル状態依存）を避けて決定的に保ってください。
- テストごとに一意の一時ディレクトリを使って干渉を防いでください。
- 変更した環境変数や一時設定ファイルは必ず復元してください。
- 内部実装より、分類結果・レポート内容・ログ副作用など観測可能な振る舞いを優先して検証してください。
- テストプロジェクトの場所/名称を変更した場合は [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml) の条件とコマンド、および [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) の検証内容を更新してください。
- リリースまたはセキュリティ自動化を変える場合は、[`.github/workflows/release.yml`](../.github/workflows/release.yml)、[`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml)、[`.github/dependabot.yml`](../.github/dependabot.yml)、[`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) を同じ差分で更新してください。
- public API を変更した場合は、DocFX サイトを再生成し、XML コメントが新しいメンバーを正しく説明しているか確認してください。
- ユーザーから見える実行挙動が変わった場合は、[`README.md`](../README.md) と [`doc/DEVELOPER_GUIDE.md`](DEVELOPER_GUIDE.md) も同じ変更で更新してください。
- 実行ライフサイクルやサービス境界を変えた場合は、テスト名や説明に使っている用語も開発者ガイドと揃っているか確認してください。
