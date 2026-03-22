# CLAUDE.md

This file is read by Claude Code at the start of every session.
It captures the coding standards, workflow rules, and review checklist for this repository.

このファイルは Claude Code がセッション開始時に読み込むガイドラインです。
このリポジトリのコーディング規約、ワークフロールール、レビューチェックリストを記載しています。

---

## Project Overview / プロジェクト概要

**What this tool does / このツールの用途:**

FolderDiffIL4DotNet compares two folders (typically "old" and "new" builds of the same product) and produces a structured diff report. Its primary use case is **release validation** — confirming exactly what changed between two builds before shipping.

FolderDiffIL4DotNet は2つのフォルダ（通常は同一製品の「旧」ビルドと「新」ビルド）を比較し、構造化された差分レポートを生成します。主な用途は**リリース検証** — 出荷前に2つのビルド間で何が変わったかを正確に確認することです。

Key differentiator: for .NET assemblies (`.dll`, `.exe`), it compares at the **IL level** rather than binary level, filtering out build-specific noise (MVID, timestamps). This means functionally identical assemblies are reported as "unchanged" even when their binary hashes differ due to non-deterministic builds.

最大の特徴：.NET アセンブリ（`.dll`、`.exe`）はバイナリレベルではなく **IL レベル**で比較し、ビルド固有のノイズ（MVID、タイムスタンプ）を除外します。これにより、非決定的ビルドでバイナリハッシュが異なっていても、機能的に同一のアセンブリは「変更なし」と判定されます。

**Outputs / 出力物:**

- `diff_report.md` — Markdown report for archiving and text-based review
- `diff_report.html` — Interactive single-file HTML report with checkboxes, sign-off workflow, inline diffs, filtering, and tamper-proof integrity verification
- `audit_log.json` — Structured audit log with SHA256 hashes for tamper detection

The HTML report serves as a **sign-off record**: reviewers check each file, write justifications, then download a self-contained reviewed copy with embedded SHA256 integrity verification.

HTML レポートは**承認記録**として機能します：レビュアーが各ファイルをチェックし、理由を記入した後、SHA256 整合性検証が埋め込まれた自己完結型のレビュー済みコピーをダウンロードします。

**Technical stack / 技術スタック:**

- .NET 8 console app (`global.json`: SDK 8.0.100, rollForward: latestMinor)
- Solution: `FolderDiffIL4DotNet.sln` (main app + `.Core` library + `.Tests` + `.Benchmarks`)
- IL disassembly via external tools (`dotnet-ildasm` preferred, `ilspycmd` fallback)
- Assembly semantic analysis via `System.Reflection.Metadata`
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
- **Samples must contain realistic, comprehensive examples** — include a variety of file statuses (Added, Removed, Changed, Unchanged), multiple file types (.dll, .exe, .config, .xml, .json, etc.), IL-level comparison results, inline diffs, and sign-off entries. The goal is to help users visualize the tool's actual output before running it.
- サンプルレポートは手動メンテナンス。レポート出力ロジックと常に一致させること。
- **サンプルには現実的で包括的な例を含めること** — 多様なファイルステータス（Added, Removed, Changed, Unchanged）、複数のファイル種別（.dll, .exe, .config, .xml, .json 等）、IL レベル比較結果、インライン差分、承認エントリを盛り込み、ユーザーが実行前にツール出力をイメージできるようにする。

## CHANGELOG / 変更履歴

- Use [Keep a Changelog](https://keepachangelog.com/) format
- Categories: Added, Changed, Fixed, Documentation, Performance
- Each entry should include: feature description, affected files, test class/method names, test count
- Both English and Japanese sections must be updated together
- **Every commit that changes behavior, fixes a bug, or adds a feature MUST have a corresponding CHANGELOG entry.** Do not defer CHANGELOG updates to a later commit — include them in the same commit as the change itself.
- 各エントリには機能説明、影響ファイル、テストクラス/メソッド名、テスト件数を含めること
- **動作変更・バグ修正・機能追加を行うすべてのコミットで CHANGELOG エントリを記載すること。** 後回しにせず、変更と同一コミットに含めること。

## CI / GitHub Actions

- Workflows: `.github/workflows/dotnet.yml` (build+test), `release.yml` (tag-based), `codeql.yml` (security)
- `CiAutomationConfigurationTests` asserts on workflow file existence — update tests when changing CI config
- Versioning: Nerdbank.GitVersioning via `version.json`
- CI ワークフロー変更時は `CiAutomationConfigurationTests` も更新すること

## Git & Release Rules / Git・リリースルール

- **Do NOT create git tags** without explicit user permission. Tags trigger the release workflow (`release.yml`) and must be created only when the user explicitly requests it.
- **git tag はユーザーの明示的な許可なく作成しないこと。** タグはリリースワークフロー（`release.yml`）を起動するため、ユーザーが明確に指示した場合のみ作成する。

## Communication / コミュニケーション

- When chatting with the repository maintainer in the console, **always use Japanese**.
- コンソールでリポジトリメンテナーと会話する際は、**常に日本語を使うこと。**

## Common Pitfalls / よくある落とし穴

1. **Markdown in tables**: Don't nest `[link](url)` inside backtick-quoted text in table cells — it renders incorrectly
2. **Help text vs README**: `ProgramRunner.HELP_TEXT` must match README option descriptions
3. **Partial class awareness**: When measuring class size, count ALL partial files together
4. **Test count drift**: The test count in TESTING_GUIDE.md easily drifts — verify after adding tests
5. **Filter state in HTML**: Filter IDs must be excluded from `collectState()` and cleared before `downloadReviewed()`
