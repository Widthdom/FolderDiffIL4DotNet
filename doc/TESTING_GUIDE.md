# Testing Guide

This document centralizes the project's testing strategy, execution commands, and practical guardrails for extending tests safely.

## Test Stack

- Test project: `FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`
- Framework: `xUnit` (`[Fact]` / `[Theory]`)
- Runner: `Microsoft.NET.Test.Sdk`
- Coverage collector: `coverlet.collector` (`XPlat Code Coverage`)
- Target framework: `net8.0`

## Current Test Scope Map

Current tree has `195` passing tests in the latest full run (`dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`).

| Area | Main test classes | What is validated |
| --- | --- | --- |
| Entry and configuration | `ProgramTests`, `ConfigServiceTests`, `ConfigSettingsTests` | `Main` exit codes, minimal end-to-end execution, config loading/default behavior |
| Core diff flow | `FolderDiffServiceTests`, `FileDiffServiceTests`, `FileDiffResultListsTests` | Classification (`Unchanged/Added/Removed/Modified`), diff detail labels, reset behavior, case-insensitive extension handling |
| IL/disassembler behavior | `ILOutputServiceTests`, `DotNetDisassembleServiceTests`, `DotNetDisassemblerCacheTests`, `DotNetDetectorTests` | Same-disassembler pairing, fallback behavior, blacklist logic, detection and command handling |
| Caching | `ILCacheTests` | memory/disk cache semantics, retention, keying behavior |
| Reporting/logging/progress | `ReportGenerateServiceTests`, `LoggerServiceTests`, `ProgressReportServiceTests` | report sections/summary formatting, log output behavior, progress reporting lifecycle |
| Utility layer | `FileComparerTests`, `FileSystemUtilityTests`, `PathValidatorTests`, `ProcessHelperTests`, `TextSanitizerTests` | hashing/text compare, path/network detection, command tokenization, file-name/path sanitization |

Testability-related structure:
- `ProgramTests` exercise the thin `Program.Main` entry point while the execution orchestration lives in `ProgramRunner`, which reduces static-state coupling.
- Diff pipeline services now expose interface seams (`IFileDiffService`, `IILOutputService`, `IFolderDiffService`, `IDotNetDisassembleService`, `IILTextOutputService`) so tests can replace collaborators directly.
- `DiffExecutionContext` carries per-run paths and network-mode flags, which keeps test setup explicit and avoids mutating shared global state.

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
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo --filter "FullyQualifiedName~FolderDiffIL4DotNet.Tests.Services.FolderDiffServiceTests"
```

Run one test method:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo --filter "FullyQualifiedName~Main_WithValidArguments_ReturnsSuccessAndGeneratesReport"
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

Workflow: `.github/workflows/dotnet.yml`

- Tests and coverage run only when `FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj` exists.
- `TestAndCoverage` artifact includes TRX and coverage outputs.
- `CoverageReport/SummaryGithub.md` is appended to GitHub Step Summary when present.

## Test Isolation and Environment Notes

- Most tests create unique temporary directories under `Path.GetTempPath()` and clean them up in `Dispose`/`finally`.
- `ProgramTests` temporarily writes `config.json` under `AppContext.BaseDirectory` and restores original content.
- `DotNetDisassembleServiceTests` temporarily rewires `PATH`/`HOME` and uses scripted fake tools to test fallback/blacklist logic deterministically.
- Some disassembler tests are skipped on Windows (`OperatingSystem.IsWindows()` guard).
- Unit tests do not require globally installed real `dotnet-ildasm` or `ilspycmd` for most scenarios because test doubles are used.
- Avoid adding static mutable test hooks. Prefer constructor injection plus `DiffExecutionContext` for per-run values.

## Adding or Updating Tests

- Keep tests deterministic: avoid network dependency, wall-clock assumptions, and global mutable state.
- Use unique temp roots per test class or test case.
- Always restore environment variables and temporary config files changed during tests.
- Prefer asserting observable behavior (result classification/report content/log side-effects) over internal implementation details.
- If test project path/name changes, update `.github/workflows/dotnet.yml` test and coverage conditions accordingly.

---

# テストガイド（日本語）

このドキュメントは、プロジェクトのテスト戦略、実行手順、拡張時の注意点を集約したものです。

## テストスタック

- テストプロジェクト: `FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`
- フレームワーク: `xUnit`（`[Fact]` / `[Theory]`）
- ランナー: `Microsoft.NET.Test.Sdk`
- カバレッジ収集: `coverlet.collector`（`XPlat Code Coverage`）
- 対象フレームワーク: `net8.0`

## 現在のテスト範囲マップ

直近のフル実行（`dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj`）では `195` 件が成功しています。

| 領域 | 主なテストクラス | 主な検証内容 |
| --- | --- | --- |
| エントリーポイント/設定 | `ProgramTests`, `ConfigServiceTests`, `ConfigSettingsTests` | `Main` の終了コード、最小構成の実行、設定読込/既定値 |
| 差分処理本体 | `FolderDiffServiceTests`, `FileDiffServiceTests`, `FileDiffResultListsTests` | `Unchanged/Added/Removed/Modified` の分類、判定理由、状態リセット、拡張子大小無視 |
| IL/逆アセンブラ | `ILOutputServiceTests`, `DotNetDisassembleServiceTests`, `DotNetDisassemblerCacheTests`, `DotNetDetectorTests` | 同一逆アセンブラ比較、フォールバック、ブラックリスト、検出・コマンド処理 |
| キャッシュ | `ILCacheTests` | メモリ/ディスクキャッシュの保持、キー生成、削除方針 |
| レポート/ログ/進捗 | `ReportGenerateServiceTests`, `LoggerServiceTests`, `ProgressReportServiceTests` | レポート出力内容、ログ動作、進捗報告ライフサイクル |
| ユーティリティ層 | `FileComparerTests`, `FileSystemUtilityTests`, `PathValidatorTests`, `ProcessHelperTests`, `TextSanitizerTests` | ハッシュ/テキスト比較、パス/ネットワーク判定、コマンド分解、ファイル名/パス整形 |

テスタビリティに関する構成:
- `ProgramTests` は薄い `Program.Main` を対象にしつつ、実行オーケストレーション本体は `ProgramRunner` に分離されています。これにより静的状態への結合を減らしています。
- 差分パイプラインの主要サービスは `IFileDiffService`, `IILOutputService`, `IFolderDiffService`, `IDotNetDisassembleService`, `IILTextOutputService` の差し替えポイントを持ちます。
- `DiffExecutionContext` が実行単位のパスやネットワークモードを保持するため、テストセットアップで共有グローバル状態を書き換える必要がありません。

## ローカルでのテスト実行

全テスト実行:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo
```

カバレッジ付き実行（Cobertura XML）:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

クラス単位実行:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo --filter "FullyQualifiedName~FolderDiffIL4DotNet.Tests.Services.FolderDiffServiceTests"
```

メソッド単位実行:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo --filter "FullyQualifiedName~Main_WithValidArguments_ReturnsSuccessAndGeneratesReport"
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

ワークフロー: `.github/workflows/dotnet.yml`

- `FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj` が存在する場合のみテスト/カバレッジを実行します。
- `TestAndCoverage` アーティファクトに TRX とカバレッジ関連ファイルを格納します。
- `CoverageReport/SummaryGithub.md` があれば GitHub Step Summary に追記されます。

## テスト分離と実行環境の注意

- 多くのテストは `Path.GetTempPath()` 配下に一意ディレクトリを作成し、`Dispose`/`finally` で後始末します。
- `ProgramTests` は `AppContext.BaseDirectory` 配下の `config.json` を一時書き換えし、必ず復元します。
- `DotNetDisassembleServiceTests` は `PATH`/`HOME` を一時変更し、擬似ツールスクリプトでフォールバック/ブラックリスト挙動を決定的に検証します。
- 逆アセンブラ関連の一部テストは Windows ではスキップされます（`OperatingSystem.IsWindows()` ガード）。
- 多くの単体テストは実ツールのグローバルインストールを不要とします（テストダブル利用）。
- 静的な可変テストフックは追加せず、実行単位の値はコンストラクタ注入と `DiffExecutionContext` で渡してください。

## テスト追加・更新時の方針

- 非決定要因（ネットワーク、時刻依存、グローバル状態依存）を避けて決定的に保ってください。
- テストごとに一意の一時ディレクトリを使って干渉を防いでください。
- 変更した環境変数や一時設定ファイルは必ず復元してください。
- 内部実装より、分類結果・レポート内容・ログ副作用など観測可能な振る舞いを優先して検証してください。
- テストプロジェクトの場所/名称を変更した場合は `.github/workflows/dotnet.yml` の条件とコマンドを更新してください。
