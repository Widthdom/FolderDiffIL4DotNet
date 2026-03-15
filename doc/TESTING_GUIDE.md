# Testing Guide

This document centralizes the project's testing strategy, execution commands, and practical guardrails for extending tests safely.

Related documents:
- [README.md](../README.md#readme-en-doc-map)
- [doc/DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md#guide-en-map)
- [api/index.md](../api/index.md)

<a id="testing-en-test-stack"></a>
## Test Stack

- Test project: [`FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`](../FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj)
- Framework: [`xUnit` `2.9.3`](https://www.nuget.org/packages/xunit/2.9.3) (`[Fact]` / `[Theory]`)
- Runner: [`Microsoft.NET.Test.Sdk` `17.12.0`](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.12.0)
- Coverage collector: [`coverlet.collector` `6.0.4`](https://www.nuget.org/packages/coverlet.collector/6.0.4) (`XPlat Code Coverage`)
- Target framework: [`.NET 8` / `net8.0`](https://learn.microsoft.com/en-us/dotnet/standard/frameworks)

<a id="testing-en-scope-map"></a>
## Current Test Scope Map

Current tree has `246` passing tests in the latest full run (`dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj -p:UseAppHost=false --nologo`).

| Area | Main test classes | What is validated |
| --- | --- | --- |
| Entry and configuration | [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs), [`ProgramRunnerTests`](../FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs), [`ConfigServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs), [`ConfigSettingsTests`](../FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) | `Main` exit codes, typed `ProgramRunner` exit-code mapping for invalid arguments vs. config failures, phase ordering around validation vs. config loading, minimal end-to-end execution, code-defined config defaults and override behavior, MD5/timestamp warning console and report output, and reflection-backed verification of internal IL cache defaults wired by `ProgramRunner` |
| Core diff flow | [`FolderDiffExecutionStrategyTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffExecutionStrategyTests.cs), [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs), [`FolderDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs), [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs), [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs), [`FileDiffResultListsTests`](../FolderDiffIL4DotNet.Tests/Models/FileDiffResultListsTests.cs) | Discovery filtering, auto-parallelism policy, classification (`Unchanged/Added/Removed/Modified`), diff detail labels, timestamp-regression detection, reset behavior, case-insensitive extension handling, propagated text-diff fallback behavior, permission/I/O failure handling, expected-vs-unexpected exception logging/rethrow behavior, large-batch classification without real disk I/O, multi-megabyte real-file text comparison, symlink-backed file classification, per-file hash/IL/text error handling without real disk |
| IL/disassembler behavior | [`ILOutputServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs), [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), [`DotNetDisassemblerCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/DotNetDisassemblerCacheTests.cs), [`DotNetDetectorTests`](../FolderDiffIL4DotNet.Tests/Core/Diagnostics/DotNetDetectorTests.cs) | Same-disassembler pairing, fallback behavior, blacklist logic, detection and command handling, failure-vs-non-.NET detection semantics |
| Real disassembler E2E | [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) | Builds the same small class library twice with `Deterministic=false`, confirms the rebuilt DLLs differ by MD5, and verifies that `dotnet-ildasm` still classifies them as `ILMatch` after MVID filtering |
| Caching | [`ILCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs) | memory/disk cache semantics, same-key updates at capacity, eviction coordination, keying behavior |
| Reporting/logging/progress | [`ReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs), [`LoggerServiceTests`](../FolderDiffIL4DotNet.Tests/Services/LoggerServiceTests.cs), [`ProgressReportServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ProgressReportServiceTests.cs) | report sections/summary formatting, report-only warning responsibility, log output behavior, shared log-file/date formats, progress reporting lifecycle |
| Core utility layer | [`FileComparerTests`](../FolderDiffIL4DotNet.Tests/Core/IO/FileComparerTests.cs), [`FileSystemUtilityTests`](../FolderDiffIL4DotNet.Tests/Core/IO/FileSystemUtilityTests.cs), [`PathValidatorTests`](../FolderDiffIL4DotNet.Tests/Core/IO/PathValidatorTests.cs), [`ProcessHelperTests`](../FolderDiffIL4DotNet.Tests/Core/Diagnostics/ProcessHelperTests.cs), [`TextSanitizerTests`](../FolderDiffIL4DotNet.Tests/Core/Text/TextSanitizerTests.cs) | hashing/text compare, shared report-timestamp formatting, path/network detection, command tokenization, file-name/path sanitization, and the reusable helper contract now housed in `FolderDiffIL4DotNet.Core` |
| Architecture boundary | [`CoreSeparationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs), [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) | utility types stay in the `FolderDiffIL4DotNet.Core` assembly, the main assembly no longer defines the legacy `FolderDiffIL4DotNet.Utils` namespace, and repository automation keeps coverage gates, release workflow, CodeQL, and Dependabot configured |

Testability-related structure:
- [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) exercise the thin `Program.Main` entry point, and [`ProgramRunnerTests`](../FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) pin both the phase ordering inside [`ProgramRunner`](../ProgramRunner.cs) and the typed exit-code mapping at the application boundary, which reduces the risk of refactors accidentally loading config before argument validation fails or collapsing distinct failures back into one exit code.
- Diff pipeline services now expose interface seams ([`IFileDiffService`](../Services/IFileDiffService.cs), [`IILOutputService`](../Services/IILOutputService.cs), [`IFolderDiffService`](../Services/IFolderDiffService.cs), [`IDotNetDisassembleService`](../Services/IDotNetDisassembleService.cs), [`IILTextOutputService`](../Services/ILOutput/IILTextOutputService.cs)) so tests can replace collaborators directly.
- [`FolderDiffExecutionStrategy`](../Services/FolderDiffExecutionStrategy.cs) and [`FolderDiffService`](../Services/FolderDiffService.cs) accept [`IFileSystemService`](../Services/IFileSystemService.cs), which lets unit tests simulate enumeration failures, streaming discovery via `EnumerateFiles(...)`, ignored-file capture, output-directory I/O failures, and large file sets without creating real directories.
- [`FileDiffService`](../Services/FileDiffService.cs) also accepts [`IFileComparisonService`](../Services/IFileComparisonService.cs), which lets unit tests simulate hash permission failures, IL-output write failures, and large-text chunk reads without creating real files.
- [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) carries per-run paths and network-mode flags, which keeps test setup explicit and avoids mutating shared global state.
- Core helper tests now live under [`FolderDiffIL4DotNet.Tests/Core/`](../FolderDiffIL4DotNet.Tests/Core/), and [`CoreSeparationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs) locks the assembly boundary so future refactors do not slide reusable helpers back into the executable project.
- [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) reads the checked-in GitHub workflow/config files directly, so removing coverage gates, tag-based release automation, CodeQL analysis, or Dependabot updates requires an explicit test update in the same change.
- [`FolderDiffExecutionStrategyTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffExecutionStrategyTests.cs), [`FolderDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs), and [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs) are marked with `Trait("Category", "Unit")`, the temp-directory-backed [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs) and [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs) are marked with `Trait("Category", "Integration")`, and [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) is marked with `Trait("Category", "E2E")` so the boundary stays explicit.

Recommended starting points by change type:
- Entry point, CLI validation, or run orchestration changes: start with [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) and [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs).
- Per-file classification changes: start with [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs), then confirm with [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs).
- IL/disassembler or cache changes: start with [`ILOutputServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs), [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), [`DotNetDisassemblerCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/DotNetDisassemblerCacheTests.cs), and [`ILCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs).
- Project-boundary or reusable-helper changes: start with [`CoreSeparationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs) and the relevant tests under [`FolderDiffIL4DotNet.Tests/Core/`](../FolderDiffIL4DotNet.Tests/Core/).
- Report wording or section changes: start with [`ReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs).

<a id="testing-en-run-tests"></a>
## Run Tests Locally

All tests:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo
```

With coverage (Cobertura XML):

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo --collect:"XPlat Code Coverage" --results-directory ./TestResults
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

Run only the real-disassembler end-to-end tests:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --filter "Category=E2E"
```

CI-parity command (same as GitHub Actions test step):

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --configuration Release --no-build --nologo --logger "trx;LogFileName=test_results.trx" --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

<a id="testing-en-coverage"></a>
## Coverage Reporting

After running with coverage, results are created under `TestResults/**/coverage.cobertura.xml`.

Latest full coverage run measured `74.04%` line coverage (`2665/3599`) and `71.63%` branch coverage (`697/973`).
CI fails if total coverage drops below `73%` line or `71%` branch.

Optional local summary generation (same tool family as CI):

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.*
export PATH="$PATH:$HOME/.dotnet/tools"
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:"MarkdownSummaryGithub;Cobertura;HtmlInline_AzurePipelines"
```

<a id="testing-en-ci-notes"></a>
## CI Integration Notes

Workflow/config files: [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml), [`.github/workflows/release.yml`](../.github/workflows/release.yml), [`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml), [`.github/dependabot.yml`](../.github/dependabot.yml)

- DocFX site generation runs before tests and publishes `_site/` as the `DocumentationSite` artifact.
- Tests and coverage run only when [`FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`](../FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj) exists.
- CI installs a real [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) tool before the test step and runs it with `DOTNET_ROLL_FORWARD=Major` so `Category=E2E` coverage guarantees the preferred disassembler path in GitHub Actions too.
- `TestAndCoverage` artifact includes TRX and coverage outputs.
- `CoverageReport/SummaryGithub.md` is appended to GitHub Step Summary when present.
- A dedicated threshold step parses `coverage.cobertura.xml` and fails the workflow if total coverage falls below `73%` line or `71%` branch.
- `release.yml` runs on `v*` tags, rebuilds/tests/publishes the app, archives publish/docs output, and creates a GitHub Release from the pushed tag.
- `codeql.yml` runs CodeQL for both `csharp` and `actions` on code changes plus a weekly schedule.
- `dependabot.yml` enables weekly update PRs for NuGet and GitHub Actions.
- [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) keeps those repository-automation files under automated regression coverage.

<a id="testing-en-isolation"></a>
## Test Isolation and Environment Notes

- Most tests create unique temporary directories under `Path.GetTempPath()` and clean them up in `Dispose`/`finally`.
- [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) temporarily writes `config.json` under [`AppContext.BaseDirectory`](https://learn.microsoft.com/en-us/dotNet/API/system.appcontext.basedirectory?view=net-8.0) and restores original content.
- [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) temporarily rewires `PATH`/`HOME` and uses scripted fake tools to test fallback/blacklist logic deterministically.
- [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) builds throwaway class libraries under temp directories and pins the E2E assertion to `dotnet-ildasm`; CI ensures that prerequisite and sets `DOTNET_ROLL_FORWARD=Major` for the test step.
- Some disassembler tests are skipped on Windows (`OperatingSystem.IsWindows()` guard).
- Unit tests do not require globally installed real [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) or [`ilspycmd`](https://www.nuget.org/packages/ilspycmd/) for most scenarios because test doubles are used.
- Avoid adding static mutable test hooks. Prefer constructor injection plus [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) for per-run values.

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
- フレームワーク: [`xUnit` `2.9.3`](https://www.nuget.org/packages/xunit/2.9.3)（`[Fact]` / `[Theory]`）
- ランナー: [`Microsoft.NET.Test.Sdk` `17.12.0`](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.12.0)
- カバレッジ収集: [`coverlet.collector` `6.0.4`](https://www.nuget.org/packages/coverlet.collector/6.0.4)（`XPlat Code Coverage`）
- 対象フレームワーク: [`.NET 8` / `net8.0`](https://learn.microsoft.com/ja-jp/dotnet/standard/frameworks)

<a id="testing-ja-scope-map"></a>
## 現在のテスト範囲マップ

直近のフル実行（`dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj -p:UseAppHost=false --nologo`）では `246` 件が成功しています。

| 領域 | 主なテストクラス | 主な検証内容 |
| --- | --- | --- |
| エントリーポイント/設定 | [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs), [`ProgramRunnerTests`](../FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs), [`ConfigServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs), [`ConfigSettingsTests`](../FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) | `Main` の終了コード、引数不正と設定失敗を分ける [`ProgramRunner`](../ProgramRunner.cs) の型付き終了コード分類、引数検証と設定読込の順序、最小構成の実行、コード既定値と override の設定挙動、更新日時警告のコンソール/レポート出力、`ProgramRunner` が内部 IL キャッシュ既定値をどう配線するかの検証 |
| 差分処理本体 | [`FolderDiffExecutionStrategyTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffExecutionStrategyTests.cs), [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs), [`FolderDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs), [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs), [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs), [`FileDiffResultListsTests`](../FolderDiffIL4DotNet.Tests/Models/FileDiffResultListsTests.cs) | 列挙フィルタ、自動並列度ポリシー、`Unchanged/Added/Removed/Modified` の分類、判定理由、更新日時逆転検出、状態リセット、拡張子大小無視、伝播したテキスト比較例外からのフォールバック、権限エラー/出力先 I/O 失敗、想定例外と想定外例外のログ/再スロー境界、大量ファイルの扱い、複数 MiB の実ファイル比較、シンボリックリンク経由の分類、ファイル単位のハッシュ/IL/テキスト分岐の異常系 |
| IL/逆アセンブラ | [`ILOutputServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs), [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), [`DotNetDisassemblerCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/DotNetDisassemblerCacheTests.cs), [`DotNetDetectorTests`](../FolderDiffIL4DotNet.Tests/Core/Diagnostics/DotNetDetectorTests.cs) | 同一逆アセンブラ比較、フォールバック、ブラックリスト、検出・コマンド処理、判定失敗と非 .NET の区別 |
| 実逆アセンブラ E2E | [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) | `Deterministic=false` の同一クラスライブラリを 2 回ビルドし、再ビルド DLL が MD5 では不一致でも、`dotnet-ildasm` では MVID 除外後に `ILMatch` になることを検証 |
| キャッシュ | [`ILCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs) | メモリ/ディスクキャッシュの保持、同一キー再保存、退避時の連動削除、キー生成 |
| レポート/ログ/進捗 | [`ReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs), [`LoggerServiceTests`](../FolderDiffIL4DotNet.Tests/Services/LoggerServiceTests.cs), [`ProgressReportServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ProgressReportServiceTests.cs) | レポート出力内容、ログ動作、共有ログ書式、進捗報告ライフサイクル |
| Core ユーティリティ層 | [`FileComparerTests`](../FolderDiffIL4DotNet.Tests/Core/IO/FileComparerTests.cs), [`FileSystemUtilityTests`](../FolderDiffIL4DotNet.Tests/Core/IO/FileSystemUtilityTests.cs), [`PathValidatorTests`](../FolderDiffIL4DotNet.Tests/Core/IO/PathValidatorTests.cs), [`ProcessHelperTests`](../FolderDiffIL4DotNet.Tests/Core/Diagnostics/ProcessHelperTests.cs), [`TextSanitizerTests`](../FolderDiffIL4DotNet.Tests/Core/Text/TextSanitizerTests.cs) | ハッシュ/テキスト比較、共有タイムスタンプ書式、パス/ネットワーク判定、コマンド分解、ファイル名/パス整形、`FolderDiffIL4DotNet.Core` に移した再利用 helper の契約確認 |
| アーキテクチャ境界 | [`CoreSeparationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs), [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) | utility 型が `FolderDiffIL4DotNet.Core` アセンブリに残り、実行ファイル側へ旧 `FolderDiffIL4DotNet.Utils` 名前空間が戻らないことに加え、カバレッジゲート、リリースワークフロー、CodeQL、Dependabot の設定が維持されること |

テスタビリティに関する構成:
- [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) は薄い `Program.Main` を対象にし、[`ProgramRunnerTests`](../FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) は [`ProgramRunner`](../ProgramRunner.cs) 内のフェーズ順序と型付き終了コード分類を固定します。これにより、引数検証より先に設定読込へ進んでしまう回帰や、異なる失敗理由が再び同じ終了コードへ潰れる回帰を防ぎつつ、静的状態への結合を減らしています。
- 差分パイプラインの主要サービスは [`IFileDiffService`](../Services/IFileDiffService.cs), [`IILOutputService`](../Services/IILOutputService.cs), [`IFolderDiffService`](../Services/IFolderDiffService.cs), [`IDotNetDisassembleService`](../Services/IDotNetDisassembleService.cs), [`IILTextOutputService`](../Services/ILOutput/IILTextOutputService.cs) の差し替えポイントを持ちます。
- [`FolderDiffExecutionStrategy`](../Services/FolderDiffExecutionStrategy.cs) と [`FolderDiffService`](../Services/FolderDiffService.cs) は [`IFileSystemService`](../Services/IFileSystemService.cs) を受け取れるため、ユニットテストでは実ファイルを作らずに列挙失敗・`EnumerateFiles(...)` ベースの遅延列挙・無視ファイル記録・出力先 I/O 失敗・大量ファイル入力を再現できます。
- [`FileDiffService`](../Services/FileDiffService.cs) は [`IFileComparisonService`](../Services/IFileComparisonService.cs) も受け取れるため、ユニットテストでは実ファイルを作らずにハッシュ権限エラー・IL 出力失敗・大きいテキスト比較のチャンク読み出しを再現できます。
- [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) が実行単位のパスやネットワークモードを保持するため、テストセットアップで共有グローバル状態を書き換える必要がありません。
- Core helper のテストは [`FolderDiffIL4DotNet.Tests/Core/`](../FolderDiffIL4DotNet.Tests/Core/) へまとめ、[`CoreSeparationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs) でアセンブリ境界も固定しています。これにより、再利用 helper が再び実行ファイル側へ混ざる回帰を防ぎます。
- [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) は、コミット済みの GitHub 設定ファイルを直接読んで検証します。これにより、カバレッジゲート、タグ起点のリリース自動化、CodeQL、Dependabot を外す変更は、同じ差分でテスト更新が必要になります。
- [`FolderDiffExecutionStrategyTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffExecutionStrategyTests.cs)、[`FolderDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs)、[`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs) には `Trait("Category", "Unit")`、実ディレクトリを使う [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs) と [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs) には `Trait("Category", "Integration")`、実逆アセンブラを使う [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) には `Trait("Category", "E2E")` を付け、境界を明示しています。

変更種別ごとの出発点:
- エントリーポイント、CLI 引数検証、実行オーケストレーション変更: [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) と [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs)
- ファイル単位の分類変更: [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs) を先に見て、最後に [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs)
- IL/逆アセンブラ/キャッシュ変更: [`ILOutputServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs), [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), [`DotNetDisassemblerCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/DotNetDisassemblerCacheTests.cs), [`ILCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs)
- プロジェクト境界や再利用 helper の変更: [`CoreSeparationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs) と [`FolderDiffIL4DotNet.Tests/Core/`](../FolderDiffIL4DotNet.Tests/Core/) 配下の対象テスト
- レポート文言やセクション変更: [`ReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs)

<a id="testing-ja-run-tests"></a>
## ローカルでのテスト実行

全テスト実行:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false
```

カバレッジ付き実行（Cobertura XML）:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --collect:"XPlat Code Coverage" --results-directory ./TestResults
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

実逆アセンブラの E2E テストだけを回す場合:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo -p:UseAppHost=false --filter "Category=E2E"
```

CI 同等コマンド（GitHub Actions と同じ test ステップ）:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --configuration Release --no-build --nologo --logger "trx;LogFileName=test_results.trx" --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

<a id="testing-ja-coverage"></a>
## カバレッジレポート

カバレッジ付き実行後、`TestResults/**/coverage.cobertura.xml` が生成されます。

直近のフルカバレッジ実行では、行カバレッジ `74.04%`（`2665/3599`）、分岐カバレッジ `71.63%`（`697/973`）でした。
CI では total の最小値として、行 `73%` / 分岐 `71%` を下回ると失敗します。

ローカルで要約を作る場合（CI と同系統ツール）:

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.*
export PATH="$PATH:$HOME/.dotnet/tools"
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:"MarkdownSummaryGithub;Cobertura;HtmlInline_AzurePipelines"
```

<a id="testing-ja-ci-notes"></a>
## CI 連携メモ

ワークフロー/設定: [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml), [`.github/workflows/release.yml`](../.github/workflows/release.yml), [`.github/workflows/codeql.yml`](../.github/workflows/codeql.yml), [`.github/dependabot.yml`](../.github/dependabot.yml)

- テスト前に DocFX サイト生成を実行し、`_site/` を `DocumentationSite` artifact として公開します。
- [`FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`](../FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj) が存在する場合のみテスト/カバレッジを実行します。
- GitHub Actions ではテスト前に実 [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) をインストールし、`DOTNET_ROLL_FORWARD=Major` を付けて `Category=E2E` の逆アセンブラ経路も実行します。
- `TestAndCoverage` アーティファクトに TRX とカバレッジ関連ファイルを格納します。
- `CoverageReport/SummaryGithub.md` があれば GitHub Step Summary に追記されます。
- 専用のしきい値チェックで `coverage.cobertura.xml` を解析し、total 行 `73%` / 分岐 `71%` を下回るとワークフローを失敗させます。
- `release.yml` は `v*` タグで実行し、再ビルド/再テスト/publish 後に publish 出力とドキュメントをアーカイブし、push されたタグから GitHub Release を作成します。
- `codeql.yml` は `csharp` と `actions` に対する CodeQL をコード変更時と週次で実行します。
- `dependabot.yml` は NuGet と GitHub Actions の更新 PR を週次で有効化します。
- [`CiAutomationConfigurationTests`](../FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) が、これらの設定ファイルも回帰テスト対象に含めます。

<a id="testing-ja-isolation"></a>
## テスト分離と実行環境の注意

- 多くのテストは `Path.GetTempPath()` 配下に一意ディレクトリを作成し、`Dispose`/`finally` で後始末します。
- [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) は [`AppContext.BaseDirectory`](https://learn.microsoft.com/ja-jp/dotNet/API/system.appcontext.basedirectory?view=net-8.0) 配下の `config.json` を一時書き換えし、必ず復元します。
- [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) は `PATH`/`HOME` を一時変更し、擬似ツールスクリプトでフォールバック/ブラックリスト挙動を決定的に検証します。
- [`RealDisassemblerE2ETests`](../FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) は temp ディレクトリ上に一時クラスライブラリをビルドし、`dotnet-ildasm` 固定で E2E 検証します。CI では `dotnet-ildasm` のインストールと `DOTNET_ROLL_FORWARD=Major` でこの前提を満たします。
- 逆アセンブラ関連の一部テストは Windows ではスキップされます（`OperatingSystem.IsWindows()` ガード）。
- 多くの単体テストは実ツールのグローバルインストールを不要とします（テストダブル利用）。
- 静的な可変テストフックは追加せず、実行単位の値はコンストラクタ注入と [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) で渡してください。

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
