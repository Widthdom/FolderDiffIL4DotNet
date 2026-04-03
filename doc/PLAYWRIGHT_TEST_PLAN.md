# Playwright Browser Test Implementation Plan

This document describes the plan for adding Playwright-based browser tests to verify the interactive HTML report features of FolderDiffIL4DotNet.

---

## Goal

Test the HTML report's interactive JS features (filtering, keyboard shortcuts, theme, lazy rendering, export, state persistence) with real browser automation, replacing manual QA for these critical reviewer-facing behaviors.

---

## Setup

### NuGet Packages

Add to `FolderDiffIL4DotNet.Tests.csproj`:

```xml
<PackageReference Include="Microsoft.Playwright" Version="1.49.0" />
```

### Playwright Install

After adding the package, run once to install browser binaries:

```bash
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
```

Or on Linux without PowerShell:

```bash
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium
```

### Test Base Class

Create `FolderDiffIL4DotNet.Tests/Browser/PlaywrightFixture.cs`:

- Implement `IAsyncLifetime` for xUnit integration
- In `InitializeAsync()`: launch Chromium (headless), create a `BrowserContext`
- In `DisposeAsync()`: close browser
- Helper method: `OpenReportAsync(string htmlPath)` → opens `file://` URL, returns `IPage`
- Generate a test HTML report by calling `HtmlReportGenerateService` with mock data (reuse `FolderDiffReportE2ETests` setup), OR use `doc/samples/diff_report.html` directly
- Use `[Collection("Playwright")]` to prevent parallel browser launches

### Test Trait

All Playwright tests should use `[Trait("Category", "Browser")]` so they can be filtered:

```bash
dotnet test --filter "Category=Browser"
```

### Skip When Browsers Not Installed

Use `[SkippableFact]` + `Skip.If(!PlaywrightFixture.BrowsersInstalled)` to gracefully skip in environments without Chromium (e.g., CI without browser install step).

---

## Test File Structure

```
FolderDiffIL4DotNet.Tests/
  Browser/
    PlaywrightFixture.cs          -- Shared fixture (browser lifecycle)
    StateManagementTests.cs       -- localStorage, auto-save, progress
    FilterTests.cs                -- Diff type / importance / search filters
    KeyboardNavigationTests.cs    -- j/k/x/?/Escape shortcuts
    ThemeTests.cs                 -- Light/dark/system cycling
    LazyRenderingTests.cs         -- Lazy diff decode, intersection observer
    ExportTests.cs                -- downloadReviewed, SHA256, read-only mode
    IntegrationTests.cs           -- Cross-feature interactions
```

---

## Test Scenarios

### 1. StateManagementTests (8 tests)

| Test Method | What to verify |
|---|---|
| `CheckboxChange_TriggersAutoSave` | Toggle `#cb_mod_0`, check localStorage contains the key with `true` |
| `TextareaChange_TriggersAutoSave` | Type into a reason textarea, check localStorage updated |
| `ProgressBar_UpdatesOnCheckboxToggle` | Toggle checkboxes, verify `#progress-text` shows correct count |
| `ProgressBar_ShowsCompleteAt100Percent` | Check all checkboxes, verify `#progress-bar-fill` has `complete` class |
| `StateRestored_OnPageReload` | Set localStorage, reload, verify checkbox and textarea values restored |
| `StorageUsage_DisplaysCorrectly` | Check `#storage-text` shows non-zero usage after saving state |
| `ClearOldStates_RemovesOldEntries` | Seed localStorage with old `folderdiff-*` keys, trigger clear, verify removed |
| `SaveStatus_ShowsTimestampAfterSave` | After auto-save, verify `#save-status` contains timestamp text |

### 2. FilterTests (10 tests)

| Test Method | What to verify |
|---|---|
| `UncheckSHA256Match_HidesMatchingRows` | Uncheck `#filter-diff-sha256match`, verify `tr[data-diff="SHA256Match"]` gets `filter-hidden` |
| `UncheckILMismatch_HidesMismatchRows` | Uncheck `#filter-diff-ilmismatch`, verify matching rows hidden |
| `ImportanceFilter_HidesLowImportanceRows` | Uncheck `#filter-imp-low`, verify `[data-sc-importance="Low"]` rows hidden inside semantic tables |
| `SearchFilter_FiltersByPath` | Type path substring into `#filter-search`, verify non-matching rows hidden |
| `SearchFilter_CaseInsensitive` | Type uppercase, verify lowercase paths still visible |
| `UncheckedOnly_ShowsOnlyUncheckedRows` | Check some boxes, enable `#filter-unchecked`, verify checked rows hidden |
| `ResetFilters_RestoresAllCheckboxes` | Uncheck several filters, click reset, verify all re-checked |
| `FilterState_PersistedToLocalStorage` | Toggle filter, verify `*-filters` localStorage key updated |
| `FilterState_RestoredOnReload` | Set filter localStorage, reload, verify filter checkboxes match |
| `CopyPath_ShowsCheckmark` | Click copy-path button, verify checkmark icon appears (clipboard API mock needed) |

### 3. KeyboardNavigationTests (8 tests)

| Test Method | What to verify |
|---|---|
| `PressJ_MovesToNextRow` | Press `j`, verify next visible `tr[data-section]` has `kb-focus` class |
| `PressK_MovesToPreviousRow` | Navigate down, press `k`, verify previous row focused |
| `PressX_TogglesCheckbox` | Focus a row with `j`, press `x`, verify checkbox toggled |
| `PressQuestionMark_TogglesHelpOverlay` | Press `?`, verify `#kb-help` has `kb-help-visible` class |
| `PressEscape_ClearsKeyboardFocus` | Focus a row, press `Escape`, verify no `kb-focus` class on any row |
| `PressEscape_ClosesOpenDetails` | Open a `<details>`, press `Escape`, verify it closes |
| `Navigation_SkipsFilterHiddenRows` | Hide some rows via filter, navigate with `j`, verify hidden rows skipped |
| `FirstFocus_PrioritizesAddedSection` | Press `j` on fresh load, verify first focused row is from `data-section="add"` (or first available priority) |

### 4. ThemeTests (5 tests)

| Test Method | What to verify |
|---|---|
| `CycleTheme_SystemToLightToDarkToSystem` | Click `#theme-toggle` 3 times, verify `data-theme` cycles system→light→dark→system |
| `ThemeButton_ShowsCorrectLabel` | After cycling, verify button text matches (⚙ System / ☀ Light / ☾ Dark) |
| `ThemePersisted_InLocalStorage` | Set theme, verify `*-theme` localStorage value |
| `ThemeRestored_OnReload` | Set localStorage, reload, verify `data-theme` matches |
| `DarkTheme_AppliesCorrectColors` | Set dark theme, verify `document.documentElement.dataset.theme === 'dark'` |

### 5. LazyRenderingTests (6 tests)

| Test Method | What to verify |
|---|---|
| `DetailsToggle_DecodesBase64Html` | Click `<summary>` of a `details[data-diff-html]`, verify child table rendered |
| `LazyDecode_RemovesDataAttribute` | After opening, verify `data-diff-html` attribute removed |
| `LazyDecode_WiresAutoSaveOnNewInputs` | Decode lazy section, change new checkbox, verify auto-save triggers |
| `LazySection_DecodesOnToggle` | Open `details[data-lazy-section]`, verify rows appear |
| `ILHighlighting_AppliedOnDecode` | Open IL mismatch detail, verify `.hl-keyword` or `.hl-directive` spans exist |
| `ForceDecodeLazySections_DecodesAll` | Call `forceDecodeLazySections()` via JS eval, verify all `data-diff-html` removed |

### 6. ExportTests (5 tests)

| Test Method | What to verify |
|---|---|
| `DownloadReviewed_GeneratesFile` | Mock download, call `downloadReviewed()`, verify download triggered with correct filename pattern |
| `DownloadReviewed_EmbedsState` | Capture downloaded HTML, verify `__savedState__` is non-null JSON |
| `DownloadReviewed_IsReadOnly` | Load reviewed HTML, verify checkboxes have `pointerEvents=none`, textareas are `readOnly` |
| `CollapseAll_ClosesAllDetails` | Open several details, call `collapseAll()`, verify all `<details>` are closed |
| `ClearAll_ResetsCheckboxesAndTextareas` | Check boxes and type notes, accept confirm dialog, call `clearAll()`, verify all reset |

### 7. IntegrationTests (4 tests)

| Test Method | What to verify |
|---|---|
| `FullWorkflow_CheckFilesAndDownloadReviewed` | Toggle checkboxes, add reasons, download reviewed, load reviewed file, verify state preserved |
| `FilterThenNavigate_OnlyVisibleRowsFocused` | Apply filter, navigate with `j/k`, verify only visible rows receive focus |
| `LazyLoadThenFilter_NewRowsRespectFilters` | Set filter, open lazy detail, verify new rows respect current filter state |
| `ThemeInReviewedReport_StillCyclable` | Load reviewed HTML, verify theme toggle still works |

---

## Implementation Notes

### Generating Test HTML

Two approaches (pick one):

**A) Use the sample report directly:**
```csharp
string htmlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "doc", "samples", "diff_report.html");
```
Pro: No generation needed. Con: Must keep sample up-to-date.

**B) Generate a report in test setup:**
Reuse the approach from `FolderDiffReportE2ETests` — create temp directories with test files, run the diff pipeline, use the generated HTML.
Pro: Always matches current output. Con: Slower, requires disassembler for IL tests.

**Recommendation: Use approach A** (sample report) for most tests, and generate a minimal report for edge-case tests only.

### Mocking Browser APIs

- **clipboard**: `page.Context.GrantPermissionsAsync(new[] { "clipboard-read", "clipboard-write" })` or mock via `page.ExposeFunctionAsync`
- **download**: Use `page.WaitForDownloadAsync()` to capture file downloads
- **confirm dialog**: `page.Dialog += (_, dialog) => dialog.AcceptAsync()`
- **localStorage**: Access via `page.EvaluateAsync("localStorage.getItem('key')")`

### Timeout Handling

Some tests (lazy rendering, intersection observer) may need `page.WaitForSelectorAsync()` with timeouts. Use reasonable defaults (5s) and document why.

### Non-Parallel Execution

Use `[Collection("Playwright")]` on all test classes to prevent parallel browser launches (shared fixture pattern).

---

## Cross-Cutting Consistency Checklist

After implementation, update:

| Artifact | Update |
|---|---|
| `TESTING_GUIDE.md` | Add Browser test section to scope map (EN+JA), update test count |
| `CHANGELOG.md` | Add entry under `[Unreleased]` (EN+JA) |
| `FolderDiffIL4DotNet.Tests.csproj` | Add Playwright NuGet reference |
| `CLAUDE.md` | Consider adding Playwright skip note to test guidelines |

---

## Estimated Test Count

- StateManagement: 8
- Filter: 10
- Keyboard: 8
- Theme: 5
- LazyRendering: 6
- Export: 5
- Integration: 4
- **Total: 46 new tests**

---

## Priority Order

If time is limited, implement in this order:

1. **FilterTests** — Most critical for reviewer workflow
2. **KeyboardNavigationTests** — Essential for efficient 500-file review
3. **StateManagementTests** — Validates auto-save, the core trust feature
4. **ExportTests** — Validates the audit artifact (downloadReviewed)
5. **ThemeTests** — Lower risk, simpler
6. **LazyRenderingTests** — Important but less frequently broken
7. **IntegrationTests** — Highest value but depends on all others
