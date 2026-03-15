# Changelog

All notable changes to this project will be documented in this file.

The English section comes first, followed by a Japanese translation.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## English

### [Unreleased]

#### Added

- Expanded CLI options: `--help`/`-h` prints usage and exits with code `0` before any logger initialization; `--version` prints the application version and exits with code `0`; `--config <path>` loads a config file from an arbitrary path instead of the default `<exe>/config.json`; `--threads <N>` overrides `MaxParallelism` in [`ConfigSettings`](Models/ConfigSettings.cs) for the current run; `--no-il-cache` forces `EnableILCache = false` for the current run; `--skip-il` skips IL decompilation and IL diff entirely for .NET assemblies (new `SkipIL` property in [`ConfigSettings`](Models/ConfigSettings.cs), also respected by [`FileDiffService`](Services/FileDiffService.cs)); `--no-timestamp-warnings` suppresses timestamp-regression warnings. Unknown flags now produce exit code `2` with a descriptive message instead of silently being ignored. [`ConfigService.LoadConfigAsync()`](Services/ConfigService.cs) now accepts an optional `configFilePath` parameter. Added [`CliOptionsTests`](FolderDiffIL4DotNet.Tests/CliOptionsTests.cs) with 21 parser unit-test cases, and new integration tests in [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) and [`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs).
- Added [`ConfigSettings.Validate()`](Models/ConfigSettings.cs) and the companion `ConfigValidationResult` class; [`ConfigService.LoadConfigAsync()`](Services/ConfigService.cs) now calls `Validate()` immediately after deserialization and throws [`InvalidDataException`](https://learn.microsoft.com/en-us/dotnet/api/system.io.invaliddataexception?view=net-8.0) listing all invalid settings when validation fails, so misconfigured runs are caught at startup with a clear error message instead of failing silently or causing undefined behavior later. Validated constraints: `MaxLogGenerations >= 1`; `TextDiffParallelThresholdKilobytes >= 1`; `TextDiffChunkSizeKilobytes >= 1`; and `TextDiffChunkSizeKilobytes < TextDiffParallelThresholdKilobytes`. Added validation unit tests to [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) (7 cases) and validation integration tests to [`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) (5 cases).

#### Fixed

- Fixed three CI pipeline failures: applied `PATH`/`HOME` isolation to `PrefetchIlCacheAsync_WhenSeededCacheExists_IncrementsHitCounter` in [`DotNetDisassembleServiceTests`](FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) so that the real `dotnet-ildasm` installed on the CI runner no longer overwrites the pre-seeded version cache entry; added `fetch-depth: 0` to the Checkout step in [`.github/workflows/codeql.yml`](.github/workflows/codeql.yml) so Nerdbank.GitVersioning can compute version height from the full commit history during the `csharp` autobuild; and added `continue-on-error: true` to the Analyze step to tolerate the SARIF upload rejection that occurs when the repository's GitHub Default Setup code scanning is also active for the `actions` language.
- Added targeted regression tests in [`FileDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs) and [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) to cover the partial-parallelism-reduction path in `DetermineEffectiveTextDiffParallelism`, the duplicate-path skip in `EnumerateDistinctPrecomputeBatches`, and the zero-batch-size fallback in `GetEffectiveIlPrecomputeBatchSize`; these three branches introduced in commit `e61ba70` were previously untested and caused branch coverage to drop below the `71%` CI threshold enforced in [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml), and refreshed the bilingual docs with the latest passing test count (`251`).

#### Changed

- Changed the elapsed-time display format in [`ProgramRunner.FormatElapsedTime()`](ProgramRunner.cs) from `HH:MM:SS.mmm` (e.g. `00:05:30.123`) to `{h}h {m}m {s.d}s` (e.g. `0h 5m 30.1s`), which disambiguates hours, minutes, and seconds at a glance. Seconds are shown with one decimal place (tenths, truncated). `FormatElapsedTime` is now `internal static` to allow direct unit testing; added 7 parametrized cases to [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs). Updated the elapsed-time example in [README.md](README.md).
- Replaced the Figgle-based banner in [`ConsoleBanner`](FolderDiffIL4DotNet.Core/Console/ConsoleBanner.cs) with a hardcoded ANSI Shadow Unicode block-character string, and removed the `Figgle` NuGet dependency from [`FolderDiffIL4DotNet.Core`](FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj).
- Added `TextDiffParallelMemoryLimitMegabytes` and `ILPrecomputeBatchSize` to [`ConfigSettings`](Models/ConfigSettings.cs), so large local text comparison can clamp chunk-parallel workers based on a configurable buffer budget while logging current managed-heap usage, and IL-related precompute now runs in batches instead of building one extra all-files list for very large trees; added regression coverage in [`FileDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs), [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs), and [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs), and refreshed the bilingual docs with the latest passing test count (`248`).
- Replaced the old top-level catch-all exit-code flattening in [`ProgramRunner`](ProgramRunner.cs) with typed phase results, so invalid arguments/input paths now return `2`, configuration load/parse failures return `3`, diff/report execution failures return `4`, and exit code `1` is reserved for unexpected internal errors; added regression coverage in [`ProgramTests`](FolderDiffIL4DotNet.Tests/ProgramTests.cs) and [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs), and refreshed the bilingual docs accordingly.
- Added repository-level release and security automation with [`.github/workflows/release.yml`](.github/workflows/release.yml), [`.github/workflows/codeql.yml`](.github/workflows/codeql.yml), and [`.github/dependabot.yml`](.github/dependabot.yml); added configuration regression coverage in [`CiAutomationConfigurationTests`](FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs); and refreshed the bilingual docs to distinguish the already-present coverage gate from the newly added GitHub Releases / CodeQL / Dependabot automation.
- Added a real-disassembler E2E test in [`RealDisassemblerE2ETests`](FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs), expanded filesystem-backed coverage for multi-megabyte text comparison and symlinked files in [`FileDiffServiceTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs) and [`FolderDiffServiceTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs), enforced CI total coverage gates at `73%` line / `71%` branch in [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml), and refreshed the bilingual docs with the latest passing test count (`240`) plus measured coverage (`74.04%` line / `71.63%` branch).
- Split the reusable helper layer out of `FolderDiffIL4DotNet` into the new [`FolderDiffIL4DotNet.Core`](FolderDiffIL4DotNet.Core/) project, reorganized the former `Utils` types into `Console` / `Diagnostics` / `IO` / `Text` namespaces, added architecture regression coverage in [`CoreSeparationTests`](FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs), and refreshed the bilingual docs plus latest passing test count (`237`).
- Centralized repeated byte-size and timestamp format literals in [`Common/Constants.cs`](Common/Constants.cs), switched logging/timestamp helpers to the shared definitions, documented the rationale for the internal IL cache defaults used by [`ProgramRunner`](ProgramRunner.cs), and added regression coverage for the shared formats and cache-default wiring.
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
- Replaced one-off `string.Format(...)` usage with interpolated strings, removed broad `#region` usage, and deleted now-unused format/message constants.
- Updated the developer and testing guides to reflect the current source-style expectations and latest passing test count.
- Made `.NET` executable detection distinguish `NotDotNetExecutable` from detection failure, log a warning for non-fatal detection failures, and let chunk-parallel text-diff exceptions bubble to the existing sequential fallback path instead of silently returning `false`.
- Enabled the `CA1031` analyzer for production code so broad exception catches are surfaced during normal builds, while excluding test cleanup code from the warning.
- Removed generic `throw new Exception(..., ex)` wrapping from [`FileSystemUtility`](FolderDiffIL4DotNet.Core/IO/FileSystemUtility.cs), using [`Exception`](https://learn.microsoft.com/en-us/dotnet/api/system.exception?view=net-8.0) only as the referenced outer type name here, so original exception types and stack traces are preserved, and added regression coverage plus bilingual guide updates.
- Moved configuration defaults into [`ConfigSettings`](Models/ConfigSettings.cs), normalized missing or `null` config values back to code-defined defaults, simplified the shipped [`config.json`](config.json) to an override-only shape, and refreshed bilingual docs plus config-focused tests.

### [1.2.2] - 2026-03-14

#### Added

- Added configurable warnings when a file in `new` has an older last-modified timestamp than the matching file in `old`, including console output before exit and a final `Warnings` section in `diff_report.md`.
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

- Added the actual reverse-engineering tool name and version to `diff_report.md`.

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

- Added the executing computer name to `diff_report.md`.

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

- Added a configuration option to include or suppress file timestamps in `diff_report.md`.

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

#### 追加

- CLI オプションを拡充しました。`--help`/`-h` は使い方を表示してロガー初期化前にコード `0` で終了します。`--version` はアプリバージョンを表示してコード `0` で終了します。`--config <path>` はデフォルトの `<exe>/config.json` に代わり任意のパスから設定ファイルを読み込みます。`--threads <N>` は今回の実行に限り [`ConfigSettings`](Models/ConfigSettings.cs) の `MaxParallelism` を上書きします。`--no-il-cache` は今回の実行に限り `EnableILCache = false` に設定します。`--skip-il` は .NET アセンブリの IL 逆アセンブルと IL 差分比較をまるごとスキップします（[`ConfigSettings`](Models/ConfigSettings.cs) に新設した `SkipIL` プロパティとして保持され、[`FileDiffService`](Services/FileDiffService.cs) でも参照します）。`--no-timestamp-warnings` はタイムスタンプ逆転の警告を抑制します。未知のフラグを指定した場合は、これまで黙ってスルーされていた挙動を改め、説明付きで終了コード `2` を返します。[`ConfigService.LoadConfigAsync()`](Services/ConfigService.cs) にオプショナルな `configFilePath` パラメータを追加しました。[`CliOptionsTests`](FolderDiffIL4DotNet.Tests/CliOptionsTests.cs) にパーサー単体テスト 21 件を追加し、[`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) と [`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) にも統合テストを追加しました。
- [`ConfigSettings.Validate()`](Models/ConfigSettings.cs) と `ConfigValidationResult` クラスを追加しました。[`ConfigService.LoadConfigAsync()`](Services/ConfigService.cs) はデシリアライズ直後に `Validate()` を呼び出し、バリデーションが失敗した場合は全エラーを列挙した [`InvalidDataException`](https://learn.microsoft.com/ja-jp/dotnet/api/system.io.invaliddataexception?view=net-8.0) をスローします。これにより、設定不正な実行は後から無言で失敗したり未定義の振る舞いを引き起こしたりする代わりに、起動時に分かりやすいエラーメッセージとして検出されます。検証対象の制約: `MaxLogGenerations >= 1`、`TextDiffParallelThresholdKilobytes >= 1`、`TextDiffChunkSizeKilobytes >= 1`、`TextDiffChunkSizeKilobytes < TextDiffParallelThresholdKilobytes`。あわせて [`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) にバリデーション単体テスト（7 件）、[`ConfigServiceTests`](FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs) にバリデーション統合テスト（5 件）を追加しました。

#### 修正

- CI パイプライン失敗を 3 件修正: [`DotNetDisassembleServiceTests`](FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) の `PrefetchIlCacheAsync_WhenSeededCacheExists_IncrementsHitCounter` に `PATH`/`HOME` 分離を適用し、CI ランナーにインストール済みの実 `dotnet-ildasm` がバージョンキャッシュの事前投入値を上書きする問題を修正; [`.github/workflows/codeql.yml`](.github/workflows/codeql.yml) の Checkout ステップに `fetch-depth: 0` を追加し、`csharp` の autobuild で Nerdbank.GitVersioning がフル履歴からバージョン計算できるよう修正; Analyze ステップに `continue-on-error: true` を追加し、リポジトリの GitHub Default Setup コードスキャンが有効なときに `actions` 言語の SARIF アップロードが拒否されてジョブが失敗する問題を回避。
- [`FileDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs) と [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) に回帰テストを追加し、`DetermineEffectiveTextDiffParallelism` の並列度部分低減経路、`EnumerateDistinctPrecomputeBatches` の重複パスのスキップ経路、`GetEffectiveIlPrecomputeBatchSize` のバッチサイズ 0 時のフォールバック経路をカバーしました。これら 3 つの分岐は commit `e61ba70` で追加されたが未テストのまま残っており、[`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml) で強制している分岐カバレッジ `71%` を下回る原因となっていました。あわせて日英ドキュメントへ最新の通過テスト件数（`251` 件）を反映しました。

#### 変更

- [`ProgramRunner.FormatElapsedTime()`](ProgramRunner.cs) の経過時間表示形式を `HH:MM:SS.mmm`（例: `00:05:30.123`）から `{h}h {m}m {s.d}s`（例: `0h 5m 30.1s`）に変更しました。時・分・秒が単位付きで表示されるため、従来の区切り文字だけでは判別しにくかった曖昧さが解消されます。秒は小数点以下 1 桁（1/10 秒単位、切り捨て）まで表示します。テスト容易性向上のため `FormatElapsedTime` を `internal static` に変更し、[`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) にパラメータ化テスト 7 件を追加しました。あわせて [README.md](README.md) の経過時間サンプル表記を更新しました。
- [`ConsoleBanner`](FolderDiffIL4DotNet.Core/Console/ConsoleBanner.cs) のバナーを Figgle ベースの出力から ANSI Shadow スタイルの Unicode ブロック文字ハードコード文字列に置き換え、[`FolderDiffIL4DotNet.Core`](FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj) から `Figgle` NuGet 依存を削除しました。
- [`ConfigSettings`](Models/ConfigSettings.cs) に `TextDiffParallelMemoryLimitMegabytes` と `ILPrecomputeBatchSize` を追加し、大きいローカルテキスト比較では設定したバッファ予算に応じてチャンク並列ワーカー数を抑えつつ current managed heap 使用量をログできるようにし、IL 関連の事前計算は大規模ツリーでも余分な全件リストを作らずバッチ実行するようにしました。あわせて [`FileDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs)、[`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs)、[`ConfigSettingsTests`](FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) に回帰テストを追加し、日英ドキュメントへ最新の通過テスト件数（`248` 件）を反映しました。
- [`ProgramRunner`](ProgramRunner.cs) のトップレベル `catch` で全失敗を 1 つの終了コードへ潰していた挙動をやめ、フェーズ単位の型付き Result に置き換えました。これにより、引数/入力パス不正は `2`、設定読込/解析失敗は `3`、差分実行/レポート生成失敗は `4`、想定外の内部エラーだけを `1` として返します。あわせて [`ProgramTests`](FolderDiffIL4DotNet.Tests/ProgramTests.cs) と [`ProgramRunnerTests`](FolderDiffIL4DotNet.Tests/ProgramRunnerTests.cs) に回帰テストを追加し、日英ドキュメントも更新しました。
- [`.github/workflows/release.yml`](.github/workflows/release.yml)、[`.github/workflows/codeql.yml`](.github/workflows/codeql.yml)、[`.github/dependabot.yml`](.github/dependabot.yml) を追加して、リポジトリ単位のリリース自動化とセキュリティ自動化を整備しました。あわせて [`CiAutomationConfigurationTests`](FolderDiffIL4DotNet.Tests/Architecture/CiAutomationConfigurationTests.cs) で設定回帰テストを追加し、既存のカバレッジゲートと今回追加した GitHub Releases / CodeQL / Dependabot の役割差分が分かるよう日英ドキュメントを更新しました。
- 実逆アセンブラを使う E2E テスト [`RealDisassemblerE2ETests`](FolderDiffIL4DotNet.Tests/Services/RealDisassemblerE2ETests.cs) を追加し、[`FileDiffServiceTests`](FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs) と [`FolderDiffServiceTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs) では複数 MiB のテキスト比較とシンボリックリンク経由ファイルの実ディレクトリ系カバレッジを拡充しました。あわせて [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml) に total 行 `73%` / 分岐 `71%` の CI カバレッジゲートを追加し、日英ドキュメントへ最新の通過テスト件数（`240` 件）と実測カバレッジ（行 `74.04%` / 分岐 `71.63%`）を反映しました。
- 再利用可能な helper 層を新しい [`FolderDiffIL4DotNet.Core`](FolderDiffIL4DotNet.Core/) プロジェクトへ分離し、従来の `Utils` 型を `Console` / `Diagnostics` / `IO` / `Text` 名前空間へ整理しました。あわせて [`CoreSeparationTests`](FolderDiffIL4DotNet.Tests/Architecture/CoreSeparationTests.cs) でアーキテクチャ境界の回帰テストを追加し、日英ドキュメントと最新の通過テスト件数（`237` 件）を更新しました。
- 繰り返し出ていたバイト換算値と日時フォーマットを [`Common/Constants.cs`](Common/Constants.cs) へ集約し、ログ出力・タイムスタンプ生成をその共有定義へ切り替えました。あわせて [`ProgramRunner`](ProgramRunner.cs) が使う内部 IL キャッシュ既定値の採用理由をコード上に明記し、共有書式と既定値配線を確認する回帰テストを追加しました。
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
- 単発利用の `string.Format(...)` を補間文字列へ置き換え、広範な `#region` 利用をやめ、不要になった書式・メッセージ定数を削除しました。
- 開発ガイドとテストガイドを更新し、現在のソースコード方針と最新の通過テスト件数を反映しました。
- `.NET` 実行可能判定で `NotDotNetExecutable` と判定失敗を区別するようにし、致命ではない判定失敗は warning を残して継続するようにしました。あわせて並列テキスト比較の例外は `false` に潰さず、既存の逐次比較フォールバック経路へ伝播させるようにしました。
- 本体コードで広すぎる例外捕捉を通常ビルド時に検出できるよう、`CA1031` アナライザーを有効化しました。テストの後片付け用 catch は warning 対象から外しています。
- [`FileSystemUtility`](FolderDiffIL4DotNet.Core/IO/FileSystemUtility.cs) での `throw new Exception(..., ex)` 形式の汎用ラップをやめ、ここで言う外側の型名 [`Exception`](https://learn.microsoft.com/ja-jp/dotnet/api/system.exception?view=net-8.0) への包み直しを避けることで、元の例外型とスタックトレースを維持するようにしました。あわせて回帰テストと日英ガイドを更新しました。
- 設定の既定値を [`ConfigSettings`](Models/ConfigSettings.cs) へ集約し、未指定や `null` の設定値をコード既定値へ正規化するようにしました。あわせて配布する [`config.json`](config.json) を override 専用の形に簡素化し、日英ドキュメントと設定まわりのテストを更新しました。

### [1.2.2] - 2026-03-14

#### 追加

- `new` 側ファイルの更新日時が対応する `old` 側より古い場合に、終了前のコンソール警告と `diff_report.md` 末尾の `Warnings` セクションを出す設定付き機能を追加しました。
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

- `diff_report.md` に利用可能な全逆アセンブラではなく、実際に使われた逆アセンブラだけを記録するよう修正しました。
- IL 比較時に複数の逆アセンブラが混在したり、キャッシュが混線したりする可能性を解消しました。
- 小さいテキストファイル比較で逐次比較が二重実行される冗長処理を解消しました。
- IL 比較まわりの回帰対応、レポート文言、終了コード挙動を改善しました。

### [1.1.8] - 2026-01-24

#### 追加

- `diff_report.md` に、実際に使われた逆アセンブルツール名とバージョンを出力するようにしました。

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

- `diff_report.md` に実行コンピュータ名を出力するようにしました。

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

- `diff_report.md` に各ファイルの更新日時を出力するかどうかを切り替える設定を追加しました。

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

[Unreleased]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.2.2...HEAD
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
