# Testing Guide

This document centralizes the project's testing strategy, execution commands, and practical guardrails for extending tests safely.

Related documents:
- [README.md](../README.md)
- [doc/DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md)
- [api/index.md](../api/index.md)

## Test Stack

- Test project: `FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`
- Framework: `xUnit` (`[Fact]` / `[Theory]`)
- Runner: `Microsoft.NET.Test.Sdk`
- Coverage collector: `coverlet.collector` (`XPlat Code Coverage`)
- Target framework: `net8.0`

## Current Test Scope Map

Current tree has `219` passing tests in the latest full run (`dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj -p:UseAppHost=false`).

| Area | Main test classes | What is validated |
| --- | --- | --- |
| Entry and configuration | [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs), [`ConfigServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs), [`ConfigSettingsTests`](../FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) | `Main` exit codes, minimal end-to-end execution, code-defined config defaults and override behavior, MD5/timestamp warning console and report output |
| Core diff flow | [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs), [`FolderDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs), [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs), [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs), [`FileDiffResultListsTests`](../FolderDiffIL4DotNet.Tests/Models/FileDiffResultListsTests.cs) | Classification (`Unchanged/Added/Removed/Modified`), diff detail labels, timestamp-regression detection, reset behavior, case-insensitive extension handling, propagated text-diff fallback behavior, permission/I/O failure handling, large-batch classification without real disk I/O, per-file hash/IL/text error handling without real disk |
| IL/disassembler behavior | [`ILOutputServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs), [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), [`DotNetDisassemblerCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/DotNetDisassemblerCacheTests.cs), [`DotNetDetectorTests`](../FolderDiffIL4DotNet.Tests/Utils/DotNetDetectorTests.cs) | Same-disassembler pairing, fallback behavior, blacklist logic, detection and command handling, failure-vs-non-.NET detection semantics |
| Caching | [`ILCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs) | memory/disk cache semantics, retention, keying behavior |
| Reporting/logging/progress | [`ReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs), [`LoggerServiceTests`](../FolderDiffIL4DotNet.Tests/Services/LoggerServiceTests.cs), [`ProgressReportServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ProgressReportServiceTests.cs) | report sections/summary formatting, report-only warning responsibility, log output behavior, progress reporting lifecycle |
| Utility layer | [`FileComparerTests`](../FolderDiffIL4DotNet.Tests/Utils/FileComparerTests.cs), [`FileSystemUtilityTests`](../FolderDiffIL4DotNet.Tests/Utils/FileSystemUtilityTests.cs), [`PathValidatorTests`](../FolderDiffIL4DotNet.Tests/Utils/PathValidatorTests.cs), [`ProcessHelperTests`](../FolderDiffIL4DotNet.Tests/Utils/ProcessHelperTests.cs), [`TextSanitizerTests`](../FolderDiffIL4DotNet.Tests/Utils/TextSanitizerTests.cs) | hashing/text compare, path/network detection, command tokenization, file-name/path sanitization, original exception-type preservation |

Testability-related structure:
- [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) exercise the thin `Program.Main` entry point while the execution orchestration lives in [`ProgramRunner`](../ProgramRunner.cs), which reduces static-state coupling.
- Diff pipeline services now expose interface seams (`IFileDiffService`, `IILOutputService`, `IFolderDiffService`, `IDotNetDisassembleService`, `IILTextOutputService`) so tests can replace collaborators directly.
- [`FolderDiffService`](../Services/FolderDiffService.cs) also accepts `IFileSystemService`, which lets unit tests simulate enumeration failures, output-directory I/O failures, and large file sets without creating real directories.
- [`FileDiffService`](../Services/FileDiffService.cs) also accepts `IFileComparisonService`, which lets unit tests simulate hash permission failures, IL-output write failures, and large-text chunk reads without creating real files.
- [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) carries per-run paths and network-mode flags, which keeps test setup explicit and avoids mutating shared global state.
- [`FolderDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) and [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs) are marked with `Trait("Category", "Unit")`, while the temp-directory-backed [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs) and [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs) are marked with `Trait("Category", "Integration")` to keep the boundary explicit.

Recommended starting points by change type:
- Entry point, CLI validation, or run orchestration changes: start with [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) and [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs).
- Per-file classification changes: start with [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs), then confirm with [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs).
- IL/disassembler or cache changes: start with [`ILOutputServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs), [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), [`DotNetDisassemblerCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/DotNetDisassemblerCacheTests.cs), and [`ILCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs).
- Report wording or section changes: start with [`ReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs).

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

CI-parity command (same as GitHub Actions test step):

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --configuration Release --no-build --nologo --logger "trx;LogFileName=test_results.trx" --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

## Coverage Reporting

After running with coverage, results are created under `TestResults/**/coverage.cobertura.xml`.

Optional local summary generation (same tool family as CI):

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.*
export PATH="$PATH:$HOME/.dotnet/tools"
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:"MarkdownSummaryGithub;Cobertura;HtmlInline_AzurePipelines"
```

## CI Integration Notes

Workflow: [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml)

- DocFX site generation runs before tests and publishes `_site/` as the `DocumentationSite` artifact.
- Tests and coverage run only when [`FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`](../FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj) exists.
- `TestAndCoverage` artifact includes TRX and coverage outputs.
- `CoverageReport/SummaryGithub.md` is appended to GitHub Step Summary when present.

## Test Isolation and Environment Notes

- Most tests create unique temporary directories under `Path.GetTempPath()` and clean them up in `Dispose`/`finally`.
- [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) temporarily writes `config.json` under `AppContext.BaseDirectory` and restores original content.
- [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) temporarily rewires `PATH`/`HOME` and uses scripted fake tools to test fallback/blacklist logic deterministically.
- Some disassembler tests are skipped on Windows (`OperatingSystem.IsWindows()` guard).
- Unit tests do not require globally installed real `dotnet-ildasm` or `ilspycmd` for most scenarios because test doubles are used.
- Avoid adding static mutable test hooks. Prefer constructor injection plus [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) for per-run values.

## Adding or Updating Tests

- Keep tests deterministic: avoid network dependency, wall-clock assumptions, and global mutable state.
- Use unique temp roots per test class or test case.
- Always restore environment variables and temporary config files changed during tests.
- Prefer asserting observable behavior (result classification/report content/log side-effects) over internal implementation details.
- If test project path/name changes, update [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml) test and coverage conditions accordingly.
- If the public API surface changes, regenerate the DocFX site and make sure XML comments still describe the new members correctly.
- If user-visible execution behavior changes, also update [`README.md`](../README.md) and [`doc/DEVELOPER_GUIDE.md`](DEVELOPER_GUIDE.md) in the same change.
- If the runtime lifecycle or service boundaries change, confirm the terminology in tests still matches the developer guide.

---

# テストガイド（日本語）

このドキュメントは、プロジェクトのテスト戦略、実行手順、拡張時の注意点を集約したものです。

関連ドキュメント:
- [README.md](../README.md)
- [doc/DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md)
- [api/index.md](../api/index.md)

## テストスタック

- テストプロジェクト: `FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`
- フレームワーク: `xUnit`（`[Fact]` / `[Theory]`）
- ランナー: `Microsoft.NET.Test.Sdk`
- カバレッジ収集: `coverlet.collector`（`XPlat Code Coverage`）
- 対象フレームワーク: `net8.0`

## 現在のテスト範囲マップ

直近のフル実行（`dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj -p:UseAppHost=false`）では `219` 件が成功しています。

| 領域 | 主なテストクラス | 主な検証内容 |
| --- | --- | --- |
| エントリーポイント/設定 | [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs), [`ConfigServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ConfigServiceTests.cs), [`ConfigSettingsTests`](../FolderDiffIL4DotNet.Tests/Models/ConfigSettingsTests.cs) | `Main` の終了コード、最小構成の実行、コード既定値と override の設定挙動、更新日時警告のコンソール/レポート出力 |
| 差分処理本体 | [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs), [`FolderDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs), [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs), [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs), [`FileDiffResultListsTests`](../FolderDiffIL4DotNet.Tests/Models/FileDiffResultListsTests.cs) | `Unchanged/Added/Removed/Modified` の分類、判定理由、更新日時逆転検出、状態リセット、拡張子大小無視、伝播したテキスト比較例外からのフォールバック、権限エラー/出力先 I/O 失敗/大量ファイルの扱い、ファイル単位のハッシュ/IL/テキスト分岐の異常系 |
| IL/逆アセンブラ | [`ILOutputServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs), [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), [`DotNetDisassemblerCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/DotNetDisassemblerCacheTests.cs), [`DotNetDetectorTests`](../FolderDiffIL4DotNet.Tests/Utils/DotNetDetectorTests.cs) | 同一逆アセンブラ比較、フォールバック、ブラックリスト、検出・コマンド処理、判定失敗と非 .NET の区別 |
| キャッシュ | [`ILCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs) | メモリ/ディスクキャッシュの保持、キー生成、削除方針 |
| レポート/ログ/進捗 | [`ReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs), [`LoggerServiceTests`](../FolderDiffIL4DotNet.Tests/Services/LoggerServiceTests.cs), [`ProgressReportServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ProgressReportServiceTests.cs) | レポート出力内容、ログ動作、進捗報告ライフサイクル |
| ユーティリティ層 | [`FileComparerTests`](../FolderDiffIL4DotNet.Tests/Utils/FileComparerTests.cs), [`FileSystemUtilityTests`](../FolderDiffIL4DotNet.Tests/Utils/FileSystemUtilityTests.cs), [`PathValidatorTests`](../FolderDiffIL4DotNet.Tests/Utils/PathValidatorTests.cs), [`ProcessHelperTests`](../FolderDiffIL4DotNet.Tests/Utils/ProcessHelperTests.cs), [`TextSanitizerTests`](../FolderDiffIL4DotNet.Tests/Utils/TextSanitizerTests.cs) | ハッシュ/テキスト比較、パス/ネットワーク判定、コマンド分解、ファイル名/パス整形、元例外型の維持 |

テスタビリティに関する構成:
- [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) は薄い `Program.Main` を対象にしつつ、実行オーケストレーション本体は [`ProgramRunner`](../ProgramRunner.cs) に分離されています。これにより静的状態への結合を減らしています。
- 差分パイプラインの主要サービスは `IFileDiffService`, `IILOutputService`, `IFolderDiffService`, `IDotNetDisassembleService`, `IILTextOutputService` の差し替えポイントを持ちます。
- [`FolderDiffService`](../Services/FolderDiffService.cs) は `IFileSystemService` も受け取れるため、ユニットテストでは実ファイルを作らずに列挙失敗・出力先 I/O 失敗・大量ファイル入力を再現できます。
- [`FileDiffService`](../Services/FileDiffService.cs) は `IFileComparisonService` も受け取れるため、ユニットテストでは実ファイルを作らずにハッシュ権限エラー・IL 出力失敗・大きいテキスト比較のチャンク読み出しを再現できます。
- [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) が実行単位のパスやネットワークモードを保持するため、テストセットアップで共有グローバル状態を書き換える必要がありません。
- [`FolderDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) と [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs) には `Trait("Category", "Unit")`、実ディレクトリを使う [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs) と [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs) には `Trait("Category", "Integration")` を付け、境界を明示しています。

変更種別ごとの出発点:
- エントリーポイント、CLI 引数検証、実行オーケストレーション変更: [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) と [`FolderDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceTests.cs)
- ファイル単位の分類変更: [`FileDiffServiceUnitTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceUnitTests.cs) を先に見て、最後に [`FileDiffServiceTests`](../FolderDiffIL4DotNet.Tests/Services/FileDiffServiceTests.cs)
- IL/逆アセンブラ/キャッシュ変更: [`ILOutputServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ILOutputServiceTests.cs), [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs), [`DotNetDisassemblerCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/DotNetDisassemblerCacheTests.cs), [`ILCacheTests`](../FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs)
- レポート文言やセクション変更: [`ReportGenerateServiceTests`](../FolderDiffIL4DotNet.Tests/Services/ReportGenerateServiceTests.cs)

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

CI 同等コマンド（GitHub Actions と同じ test ステップ）:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --configuration Release --no-build --nologo --logger "trx;LogFileName=test_results.trx" --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

## カバレッジレポート

カバレッジ付き実行後、`TestResults/**/coverage.cobertura.xml` が生成されます。

ローカルで要約を作る場合（CI と同系統ツール）:

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.*
export PATH="$PATH:$HOME/.dotnet/tools"
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:"MarkdownSummaryGithub;Cobertura;HtmlInline_AzurePipelines"
```

## CI 連携メモ

ワークフロー: [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml)

- テスト前に DocFX サイト生成を実行し、`_site/` を `DocumentationSite` artifact として公開します。
- [`FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`](../FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj) が存在する場合のみテスト/カバレッジを実行します。
- `TestAndCoverage` アーティファクトに TRX とカバレッジ関連ファイルを格納します。
- `CoverageReport/SummaryGithub.md` があれば GitHub Step Summary に追記されます。

## テスト分離と実行環境の注意

- 多くのテストは `Path.GetTempPath()` 配下に一意ディレクトリを作成し、`Dispose`/`finally` で後始末します。
- [`ProgramTests`](../FolderDiffIL4DotNet.Tests/ProgramTests.cs) は `AppContext.BaseDirectory` 配下の `config.json` を一時書き換えし、必ず復元します。
- [`DotNetDisassembleServiceTests`](../FolderDiffIL4DotNet.Tests/Services/DotNetDisassembleServiceTests.cs) は `PATH`/`HOME` を一時変更し、擬似ツールスクリプトでフォールバック/ブラックリスト挙動を決定的に検証します。
- 逆アセンブラ関連の一部テストは Windows ではスキップされます（`OperatingSystem.IsWindows()` ガード）。
- 多くの単体テストは実ツールのグローバルインストールを不要とします（テストダブル利用）。
- 静的な可変テストフックは追加せず、実行単位の値はコンストラクタ注入と [`DiffExecutionContext`](../Services/DiffExecutionContext.cs) で渡してください。

## テスト追加・更新時の方針

- 非決定要因（ネットワーク、時刻依存、グローバル状態依存）を避けて決定的に保ってください。
- テストごとに一意の一時ディレクトリを使って干渉を防いでください。
- 変更した環境変数や一時設定ファイルは必ず復元してください。
- 内部実装より、分類結果・レポート内容・ログ副作用など観測可能な振る舞いを優先して検証してください。
- テストプロジェクトの場所/名称を変更した場合は [`.github/workflows/dotnet.yml`](../.github/workflows/dotnet.yml) の条件とコマンドを更新してください。
- public API を変更した場合は、DocFX サイトを再生成し、XML コメントが新しいメンバーを正しく説明しているか確認してください。
- ユーザーから見える実行挙動が変わった場合は、[`README.md`](../README.md) と [`doc/DEVELOPER_GUIDE.md`](DEVELOPER_GUIDE.md) も同じ変更で更新してください。
- 実行ライフサイクルやサービス境界を変えた場合は、テスト名や説明に使っている用語も開発者ガイドと揃っているか確認してください。
