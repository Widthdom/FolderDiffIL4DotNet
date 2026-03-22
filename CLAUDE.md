# CLAUDE.md

This file is read by Claude Code at the start of every session.
It captures the coding standards, workflow rules, and review checklist for this repository.

このファイルは Claude Code がセッション開始時に読み込むガイドラインです。
このリポジトリのコーディング規約、ワークフロールール、レビューチェックリストを記載しています。

---

## Project Overview / プロジェクト概要

- .NET 8 console app (`global.json`: SDK 8.0.100, rollForward: latestMinor)
- Solution: `FolderDiffIL4DotNet.sln` (main app + `.Core` library + `.Tests` + `.Benchmarks`)
- All user-facing text, code comments, and documentation are **bilingual (English / Japanese)**

## Build & Test / ビルド・テスト

```bash
dotnet build
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

- CI coverage thresholds: line >= 73%, branch >= 71%
- Always run tests locally before pushing. If `dotnet` is unavailable, state this explicitly.
- テストをローカル実行してからプッシュすること。`dotnet` が利用不可なら明記すること。

## Bilingual Rule / 英日併記ルール

All documentation and in-code comments MUST be bilingual (English first, then Japanese).
This applies to:

- README.md, DEVELOPER_GUIDE.md, TESTING_GUIDE.md, TROUBLESHOOTING.md, CHANGELOG.md
- XML doc comments (`/// <summary>` blocks)
- Inline code comments (`// English / 日本語`)
- HTML report UI text (`I18n("English", "日本語")`)
- CLAUDE.md itself

すべてのドキュメントとコード内コメントは英日併記（英語が先、日本語が後）。

## Cross-Cutting Consistency Rule / 横断的整合性ルール

**Any change to implementation MUST be reflected in ALL related artifacts in the same commit.**
This is the single most important rule in this repository.

**実装の変更は、同一コミットで関連する全成果物に反映すること。**
これがこのリポジトリで最も重要なルールです。

### Checklist for every change / 変更のたびに確認するチェックリスト

| When you change... | Also update... |
|---|---|
| CLI options (`CliParser.cs`, `CliOptions.cs`) | README.md (EN+JA options table), `ProgramRunner.cs` HELP_TEXT, `CliOptionsTests` |
| Config settings (`ConfigSettings.cs`) | README.md (EN+JA config table), `ConfigSettingsTests`, `config.sample.jsonc` |
| Report output logic (`ReportGenerateService`, `HtmlReportGenerateService`) | `doc/samples/diff_report.md`, `doc/samples/diff_report.html`, `ReportGenerateServiceTests`, `HtmlReportGenerateServiceTests` |
| Audit log structure (`AuditLogGenerateService`) | `doc/samples/audit_log.json`, `AuditLogGenerateServiceTests` |
| New test class added | TESTING_GUIDE.md scope map table (EN+JA), test count (EN+JA) |
| Service/class added or renamed | DEVELOPER_GUIDE.md architecture/file table (EN+JA) |
| Any significant feature or fix | CHANGELOG.md `[Unreleased]` section (EN+JA) |
| HTML report JS/CSS | Both the generator `.cs` files AND `doc/samples/diff_report.html` |

## Code Style / コードスタイル

### Method & class size limits / メソッド・クラスのサイズ上限

- **Methods**: Keep under ~100 lines. Extract helpers when a method exceeds this.
- **Classes**: Keep under ~500 lines per file. Use `partial class` splits for larger services.
- Already-split examples: `HtmlReportGenerateService` (5 files), `AssemblyMethodAnalyzer` (3 files), `DotNetDisassembleService` (2 files)
- メソッドは約100行以内。超えたらヘルパーに抽出する。
- クラスは1ファイル約500行以内。大きいサービスは `partial class` で分割する。

### Naming conventions / 命名規則

- Private fields: `_camelCase`
- Constants: `UPPER_SNAKE_CASE`
- Config keys: `PascalCase` (matching JSON property names)
- Test methods: `MethodUnderTest_Scenario_ExpectedBehavior`
- Test traits: `[Trait("Category", "Unit")]`, `"Integration"`, `"E2E"`

### Error handling / エラーハンドリング

- Expected exceptions: log as Error, rethrow to caller
- Best-effort operations (semantic analysis, inline diff): catch-all with `#pragma warning disable CA1031`, log as Warning
- Never silently swallow exceptions in the main diff pipeline

## Test Guidelines / テストガイドライン

- Every new test class must be added to TESTING_GUIDE.md scope map (both EN and JA tables)
- Test count in TESTING_GUIDE.md must be updated when tests are added
- Use `[SkippableFact]` + `Skip.If(...)` for environment-dependent tests (not silent pass)
- Prefer testing observable behavior over internal implementation
- Use unique temp directories per test class; clean up in `Dispose`
- 新しいテストクラスは TESTING_GUIDE.md の範囲マップ（EN/JA 両方）に追加すること
- テスト追加時は TESTING_GUIDE.md のテスト件数も更新すること

## Report Samples / サンプルレポート

- `doc/samples/diff_report.md` and `doc/samples/diff_report.html` are **manually maintained** reference samples
- They must match the actual output of `ReportGenerateService` and `HtmlReportGenerateService`
- When updating HTML report features (CSS, JS, data attributes, filter controls), update the sample HTML too
- サンプルレポートは手動メンテナンス。レポート出力ロジックと常に一致させること。

## CHANGELOG / 変更履歴

- Use [Keep a Changelog](https://keepachangelog.com/) format
- Categories: Added, Changed, Fixed, Documentation, Performance
- Each entry should include: feature description, affected files, test class/method names, test count
- Both English and Japanese sections must be updated together
- 各エントリには機能説明、影響ファイル、テストクラス/メソッド名、テスト件数を含めること

## CI / GitHub Actions

- Workflows: `.github/workflows/dotnet.yml` (build+test), `release.yml` (tag-based), `codeql.yml` (security)
- `CiAutomationConfigurationTests` asserts on workflow file existence — update tests when changing CI config
- Versioning: Nerdbank.GitVersioning via `version.json`
- CI ワークフロー変更時は `CiAutomationConfigurationTests` も更新すること

## Common Pitfalls / よくある落とし穴

1. **Markdown in tables**: Don't nest `[link](url)` inside backtick-quoted text in table cells — it renders incorrectly
2. **Help text vs README**: `ProgramRunner.HELP_TEXT` must match README option descriptions
3. **Partial class awareness**: When measuring class size, count ALL partial files together
4. **Test count drift**: The test count in TESTING_GUIDE.md easily drifts — verify after adding tests
5. **Filter state in HTML**: Filter IDs must be excluded from `collectState()` and cleared before `downloadReviewed()`
