# CLAUDE.md

This file is read by Claude Code at the start of every session.
It captures the coding standards, workflow rules, and review checklist for this repository.

---

## English

### Project Overview

**What this tool does:**

FolderDiffIL4DotNet compares two folders (typically "old" and "new" builds of the same product) and produces a structured diff report. Its primary use case is **release validation** — confirming exactly what changed between two builds before shipping.

Key differentiator: for .NET assemblies (`.dll`, `.exe`), it compares at the **IL level** rather than binary level, filtering out build-specific noise (MVID, timestamps). This means functionally identical assemblies are reported as "unchanged" even when their binary hashes differ due to non-deterministic builds.

**Outputs:**

- `diff_report.md` — Markdown report for archiving and text-based review
- `diff_report.html` — Interactive single-file HTML report with checkboxes, sign-off workflow, inline diffs, filtering, and tamper-proof integrity verification
- `audit_log.json` — Structured audit log with SHA256 hashes for tamper detection

The HTML report serves as a **sign-off record**: reviewers check each file, write justifications, then download a self-contained reviewed copy with embedded SHA256 integrity verification.

**Technical stack:**

- .NET 8 console app (`global.json`: SDK 8.0.100, rollForward: latestMinor)
- Solution: `FolderDiffIL4DotNet.sln` (main app + `.Core` library + `.Tests` + `.Benchmarks`)
- IL disassembly via external tools (`dotnet-ildasm` preferred, `ilspycmd` fallback)
- Assembly semantic analysis via `System.Reflection.Metadata`
- All user-facing text, code comments, and documentation are **bilingual (English / Japanese)**

### Design Philosophy

This tool is built for **regulated/enterprise release workflows** where the diff report is not just informational — it is an **auditable artifact** that proves what was reviewed before shipping.

**Core principles:**

1. **Signal over noise**: The entire reason IL-level comparison exists is to eliminate false positives. A reviewer should never have to investigate a file that didn't actually change. Every UI and reporting decision should reinforce this — surface what matters, suppress what doesn't.
2. **The reviewed HTML is a legal document**: Once `downloadReviewed()` produces the reviewed copy, it becomes a self-contained compliance record. It must be tamper-proof (SHA256 integrity), fully functional without a server, and preserve all information needed for audit. Design decisions about what goes inside vs. outside `<!--CTRL-->` markers directly affect this.
3. **Reviewer efficiency**: The report will be used by people reviewing hundreds of files under time pressure. Filtering, search, importance levels, fold/unfold, keyboard shortcuts — these aren't nice-to-haves, they are essential for the workflow. UI changes should be evaluated from the perspective of "does this help a reviewer get through 500 files faster and more accurately?"
4. **The header establishes trust context**: The report header (tool version, comparison paths, configuration, disassembler availability) is not just metadata — it tells the reviewer "here is exactly what tool, with what settings, produced this report." This context is critical for audit traceability.
5. **Self-contained single-file output**: The HTML report must work as a single file with no external dependencies. All CSS, JS, and data are embedded. This ensures the report works offline, can be attached to tickets, archived in document management systems, and opened years later.

**When making UI/report changes**, always ask: "Does this help or hinder the reviewer who needs to sign off on 500 files before a release deadline?"

### Build & Test

```bash
dotnet build
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

- CI coverage thresholds: line >= 80%, branch >= 75%
- Core diff class targets (FileDiffService, FolderDiffService, FileComparisonService): line >= 90%, branch >= 85% (CI warns but does not block)
- **Before committing**, always run tests with the Release configuration to match CI: `dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --configuration Release --nologo`. Release builds enable `TreatWarningsAsErrors` and full code analysis rules (e.g. CA1031), which are not enforced in Debug builds. If `dotnet` is unavailable, state this explicitly.

### Bilingual Rule

All documentation and in-code comments MUST be bilingual (English first, then Japanese).
This applies to:

- README.md, DEVELOPER_GUIDE.md, TESTING_GUIDE.md, TROUBLESHOOTING.md, CHANGELOG.md
- XML doc comments (`/// <summary>` blocks)
- Inline code comments (`// English / 日本語`)
- HTML report UI text (English only; `I18n` helper was removed)
- CLAUDE.md itself

### Cross-Cutting Consistency Rule

**Any change to implementation MUST be reflected in ALL related artifacts in the same commit.**
This is the single most important rule in this repository.

#### Checklist for every change

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

### Code Style

#### Method & class size limits

- **Methods**: Keep under ~100 lines. Extract helpers when a method exceeds this.
- **Classes**: Keep under ~500 lines per file. Use `partial class` splits for larger services.
- Already-split examples: `HtmlReportGenerateService` (6 files), `ReportGenerateService` (11 files), `AssemblyMethodAnalyzer` (5 files), `DotNetDisassembleService` (2 files), `FileDiffService` (2 files)

#### Naming conventions

- Private fields: `_camelCase`
- Constants: `UPPER_SNAKE_CASE`
- Config keys: `PascalCase` (matching JSON property names)
- Test methods: `MethodUnderTest_Scenario_ExpectedBehavior`
- Test traits: `[Trait("Category", "Unit")]`, `"Integration"`, `"E2E"`

#### Error handling

- Expected exceptions: log as Error, rethrow to caller
- Best-effort operations (semantic analysis, inline diff): catch-all with `#pragma warning disable CA1031`, log as Warning
- Never silently swallow exceptions in the main diff pipeline

### Test Guidelines

- Every new test class must be added to TESTING_GUIDE.md scope map (both EN and JA tables)
- Test count in TESTING_GUIDE.md must be updated when tests are added
- Use `[SkippableFact]` + `Skip.If(...)` for environment-dependent tests (not silent pass)
- Prefer testing observable behavior over internal implementation
- Use unique temp directories per test class; clean up in `Dispose`

### Report Samples

- `doc/samples/diff_report.md` and `doc/samples/diff_report.html` are **manually maintained** reference samples
- They must match the actual output of `ReportGenerateService` and `HtmlReportGenerateService`
- When updating HTML report features (CSS, JS, data attributes, filter controls), update the sample HTML too
- **Samples must contain realistic, comprehensive examples** — include a variety of file statuses (Added, Removed, Changed, Unchanged), multiple file types (.dll, .exe, .config, .xml, .json, etc.), IL-level comparison results, inline diffs, and sign-off entries. The goal is to help users visualize the tool's actual output before running it.

### CHANGELOG

- Use [Keep a Changelog](https://keepachangelog.com/) format
- Categories: Added, Changed, Fixed, Documentation, Performance
- Each entry should include: feature description, affected files, test class/method names, test count
- Both English and Japanese sections must be updated together
- **Every commit that changes behavior, fixes a bug, or adds a feature MUST have a corresponding CHANGELOG entry.** Do not defer CHANGELOG updates to a later commit — include them in the same commit as the change itself.

### CI / GitHub Actions

- Workflows: `.github/workflows/dotnet.yml` (build+test), `release.yml` (tag-based), `codeql.yml` (security)
- `CiAutomationConfigurationTests` asserts on workflow file existence — update tests when changing CI config
- Versioning: Nerdbank.GitVersioning via `version.json`

### Git & Release Rules

- **Do NOT create git tags** without explicit user permission. Tags trigger the release workflow (`release.yml`) and must be created only when the user explicitly requests it.
- **Commit messages must be written in English.** Keep them concise and descriptive.
- **Version bump procedure** — When bumping the version, perform all three steps in a single commit:
  1. Update `version.json` (`"version"` field)
  2. In `CHANGELOG.md` (both EN and JA sections): move `[Unreleased]` content under a new `[X.Y.Z] - YYYY-MM-DD` heading and add a fresh empty `[Unreleased]` section above it
  3. In `CHANGELOG.md` bottom link references: update `[Unreleased]` compare link to `vX.Y.Z...HEAD`, and add a new `[X.Y.Z]` compare link pointing to `vPREVIOUS...vX.Y.Z`

### Communication

- When chatting with the repository maintainer in the console, **always use Japanese**.

### Common Pitfalls

1. **Markdown in tables**: Don't nest `[link](url)` inside backtick-quoted text in table cells — it renders incorrectly
2. **Help text vs README**: `ProgramRunner.HELP_TEXT` must match README option descriptions
3. **Partial class awareness**: When measuring class size, count ALL partial files together
4. **Test count drift**: The test count in TESTING_GUIDE.md easily drifts — verify after adding tests
5. **Filter state in HTML**: Filter IDs must be excluded from `collectState()` and cleared before `downloadReviewed()`
6. **Thread safety in test fakes**: When a test fake records calls in a collection from `Parallel.ForEachAsync`, use `ConcurrentBag<T>` instead of `List<T>`. A race condition in `List.Add` can throw an exception caught by production error-handling code, causing tests to follow unexpected fallback paths.
7. **Bilingual separation in docs**: Never mix English and Japanese on the same line with `" / "` separators. Each bilingual document has a distinct English section (first half) and Japanese section (second half) — keep them strictly separated.

---

## 日本語

### プロジェクト概要

**このツールの用途：**

FolderDiffIL4DotNet は2つのフォルダ（通常は同一製品の「旧」ビルドと「新」ビルド）を比較し、構造化された差分レポートを生成します。主な用途は**リリース検証** — 出荷前に2つのビルド間で何が変わったかを正確に確認することです。

最大の特徴：.NET アセンブリ（`.dll`、`.exe`）はバイナリレベルではなく **IL レベル**で比較し、ビルド固有のノイズ（MVID、タイムスタンプ）を除外します。これにより、非決定的ビルドでバイナリハッシュが異なっていても、機能的に同一のアセンブリは「変更なし」と判定されます。

**出力物：**

- `diff_report.md` — アーカイブおよびテキストベースレビュー用の Markdown レポート
- `diff_report.html` — チェックボックス、承認ワークフロー、インライン差分、フィルタリング、改竄防止整合性検証を備えたインタラクティブな単一ファイル HTML レポート
- `audit_log.json` — 改竄検知用 SHA256 ハッシュを含む構造化監査ログ

HTML レポートは**承認記録**として機能します：レビュアーが各ファイルをチェックし、理由を記入した後、SHA256 整合性検証が埋め込まれた自己完結型のレビュー済みコピーをダウンロードします。

**技術スタック：**

- .NET 8 コンソールアプリ（`global.json`: SDK 8.0.100, rollForward: latestMinor）
- ソリューション: `FolderDiffIL4DotNet.sln`（メインアプリ + `.Core` ライブラリ + `.Tests` + `.Benchmarks`）
- 外部ツールによる IL 逆アセンブリ（`dotnet-ildasm` 優先、`ilspycmd` フォールバック）
- `System.Reflection.Metadata` によるアセンブリセマンティック分析
- すべてのユーザー向けテキスト、コードコメント、ドキュメントは**英日バイリンガル**

### 設計思想

このツールは**規制・エンタープライズ環境のリリースワークフロー**向けに作られている。差分レポートは単なる情報提供ではなく、出荷前に何をレビューしたかを証明する**監査可能な成果物**である。

**基本原則：**

1. **ノイズよりシグナル**: IL レベル比較が存在する理由は誤検知の排除。レビュアーが実際には変わっていないファイルを調査する必要があってはならない。UI・レポートのあらゆる設計判断はこれを強化すべき — 重要なものを浮かび上がらせ、重要でないものを抑制する。
2. **reviewed HTML は法的文書**: `downloadReviewed()` が生成するレビュー済みコピーは自己完結型のコンプライアンス記録になる。改竄防止（SHA256 整合性）、サーバー不要での完全動作、監査に必要な全情報の保持が必須。`<!--CTRL-->` マーカーの内外に何を配置するかの設計判断はこれに直結する。
3. **レビュアー効率**: レポートは時間的制約の下で数百ファイルをレビューする人が使う。フィルタリング、検索、重要度レベル、折りたたみ、キーボードショートカット — これらは「あれば便利」ではなく、ワークフローに不可欠。UI 変更は「レビュアーが 500 ファイルをより速く正確に処理できるか」の観点で評価すること。
4. **ヘッダーは信頼のコンテキスト**: レポートヘッダー（ツールバージョン、比較パス、設定、逆アセンブラ利用可否）は単なるメタデータではなく、「このツールが、この設定で、このレポートを生成した」ことをレビュアーに伝える。監査追跡性に不可欠。
5. **自己完結型単一ファイル出力**: HTML レポートは外部依存なしの単一ファイルで動作すること。CSS・JS・データはすべて埋め込み。これによりオフライン動作、チケット添付、文書管理システムへのアーカイブ、数年後の閲覧が保証される。

**UI・レポート変更時は常に問うこと：**「リリース期限前に 500 ファイルを承認しなければならないレビュアーにとって、この変更は助けになるか、妨げになるか？」

### ビルド・テスト

```bash
dotnet build
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

- CI カバレッジ閾値: 行 >= 80%、ブランチ >= 75%
- コア差分クラス閾値（FileDiffService、FolderDiffService、FileComparisonService）: 行 >= 90%、ブランチ >= 85%
- **コミット前に**必ず Release 構成でテストを実行し CI と同一条件を確認すること: `dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --configuration Release --nologo`。Release ビルドでは `TreatWarningsAsErrors` と完全なコード解析ルール（CA1031 等）が有効になるため、Debug ビルドでは検出できない問題を事前に捕捉できる。`dotnet` が利用不可なら明記すること。

### 英日併記ルール

すべてのドキュメントとコード内コメントは英日併記（英語が先、日本語が後）。
適用対象：

- README.md、DEVELOPER_GUIDE.md、TESTING_GUIDE.md、TROUBLESHOOTING.md、CHANGELOG.md
- XML doc コメント（`/// <summary>` ブロック）
- インラインコードコメント（`// English / 日本語`）
- HTML レポート UI テキスト（英語のみ; `I18n` ヘルパーは削除済み）
- CLAUDE.md 自体

### 横断的整合性ルール

**実装の変更は、同一コミットで関連する全成果物に反映すること。**
これがこのリポジトリで最も重要なルールです。

#### 変更のたびに確認するチェックリスト

| 変更対象 | 合わせて更新 |
|---|---|
| CLI オプション（`CliParser.cs`、`CliOptions.cs`） | README.md（EN+JA オプション表）、`ProgramRunner.cs` HELP_TEXT、`CliOptionsTests` |
| 設定項目（`ConfigSettings.cs`） | README.md（EN+JA 設定表）、`ConfigSettingsTests`、`config.sample.jsonc` |
| レポート出力ロジック（`ReportGenerateService`、`HtmlReportGenerateService`） | `doc/samples/diff_report.md`、`doc/samples/diff_report.html`、`ReportGenerateServiceTests`、`HtmlReportGenerateServiceTests` |
| 監査ログ構造（`AuditLogGenerateService`） | `doc/samples/audit_log.json`、`AuditLogGenerateServiceTests` |
| 新テストクラス追加 | TESTING_GUIDE.md 範囲マップ表（EN+JA）、テスト件数（EN+JA） |
| サービス/クラスの追加・リネーム | DEVELOPER_GUIDE.md アーキテクチャ/ファイル表（EN+JA） |
| 重要な機能追加・修正 | CHANGELOG.md `[Unreleased]` セクション（EN+JA） |
| HTML レポート JS/CSS | ジェネレータ `.cs` ファイルと `doc/samples/diff_report.html` の両方 |

### コードスタイル

#### メソッド・クラスのサイズ上限

- メソッドは約100行以内。超えたらヘルパーに抽出する。
- クラスは1ファイル約500行以内。大きいサービスは `partial class` で分割する。
- 分割済みの例: `HtmlReportGenerateService`（6 ファイル）、`ReportGenerateService`（11 ファイル）、`AssemblyMethodAnalyzer`（5 ファイル）、`DotNetDisassembleService`（2 ファイル）、`FileDiffService`（2 ファイル）

#### 命名規則

- プライベートフィールド: `_camelCase`
- 定数: `UPPER_SNAKE_CASE`
- 設定キー: `PascalCase`（JSON プロパティ名と一致）
- テストメソッド: `MethodUnderTest_Scenario_ExpectedBehavior`
- テストトレイト: `[Trait("Category", "Unit")]`、`"Integration"`、`"E2E"`

#### エラーハンドリング

- 想定内の例外: Error レベルでログ出力し、呼び出し元に再スロー
- ベストエフォート処理（セマンティック分析、インライン差分）: `#pragma warning disable CA1031` で catch-all、Warning レベルでログ出力
- メイン差分パイプラインで例外を黙殺しないこと

### テストガイドライン

- 新しいテストクラスは TESTING_GUIDE.md の範囲マップ（EN/JA 両方）に追加すること
- テスト追加時は TESTING_GUIDE.md のテスト件数も更新すること
- 環境依存テストには `[SkippableFact]` + `Skip.If(...)` を使用（サイレントパスは不可）
- 内部実装ではなく観測可能な振る舞いをテストすること
- テストクラスごとに固有の一時ディレクトリを使用し、`Dispose` でクリーンアップすること

### サンプルレポート

- `doc/samples/diff_report.md` と `doc/samples/diff_report.html` は**手動メンテナンス**のリファレンスサンプル
- `ReportGenerateService` と `HtmlReportGenerateService` の実際の出力と常に一致させること
- HTML レポート機能（CSS、JS、data 属性、フィルタコントロール）更新時はサンプル HTML も更新すること
- **サンプルには現実的で包括的な例を含めること** — 多様なファイルステータス（Added, Removed, Changed, Unchanged）、複数のファイル種別（.dll, .exe, .config, .xml, .json 等）、IL レベル比較結果、インライン差分、承認エントリを盛り込み、ユーザーが実行前にツール出力をイメージできるようにする。

### 変更履歴

- [Keep a Changelog](https://keepachangelog.com/) 形式を使用
- カテゴリ: Added、Changed、Fixed、Documentation、Performance
- 各エントリには機能説明、影響ファイル、テストクラス/メソッド名、テスト件数を含めること
- 英語・日本語セクションを常に同時に更新すること
- **動作変更・バグ修正・機能追加を行うすべてのコミットで CHANGELOG エントリを記載すること。** 後回しにせず、変更と同一コミットに含めること。

### CI / GitHub Actions

- ワークフロー: `.github/workflows/dotnet.yml`（ビルド+テスト）、`release.yml`（タグベース）、`codeql.yml`（セキュリティ）
- `CiAutomationConfigurationTests` がワークフローファイルの存在をアサート — CI 設定変更時はテストも更新すること
- バージョニング: `version.json` による Nerdbank.GitVersioning

### Git・リリースルール

- **git tag はユーザーの明示的な許可なく作成しないこと。** タグはリリースワークフロー（`release.yml`）を起動するため、ユーザーが明確に指示した場合のみ作成する。
- **コミットメッセージは英語で記述すること。** 簡潔かつ内容が分かるメッセージにする。
- **バージョンアップ手順** — バージョンを上げる際は、以下の3ステップを1コミットで行うこと：
  1. `version.json` の `"version"` フィールドを更新
  2. `CHANGELOG.md`（EN・JA 両セクション）：`[Unreleased]` の内容を新しい `[X.Y.Z] - YYYY-MM-DD` 見出しの下に移動し、その上に空の `[Unreleased]` セクションを追加
  3. `CHANGELOG.md` 末尾のリンク参照：`[Unreleased]` の compare リンクを `vX.Y.Z...HEAD` に更新し、`vPREVIOUS...vX.Y.Z` を指す `[X.Y.Z]` の compare リンクを新規追加

### コミュニケーション

- コンソールでリポジトリメンテナーと会話する際は、**常に日本語を使うこと。**

### よくある落とし穴

1. **テーブル内 Markdown**: テーブルセルのバッククォート引用テキスト内に `[link](url)` をネストしないこと — レンダリングが崩れる
2. **ヘルプテキスト vs README**: `ProgramRunner.HELP_TEXT` は README のオプション説明と一致させること
3. **partial class の認識**: クラスサイズ計測時は全 partial ファイルを合算すること
4. **テスト件数のドリフト**: TESTING_GUIDE.md のテスト件数はずれやすい — テスト追加後に検証すること
5. **HTML のフィルタ状態**: フィルタ ID は `collectState()` から除外し、`downloadReviewed()` 前にクリアすること
6. **テストフェイクのスレッドセーフティ**: テストフェイクが `Parallel.ForEachAsync` からコレクションに呼び出しを記録する場合、`List<T>` ではなく `ConcurrentBag<T>` を使用すること。`List.Add` の競合状態がプロダクションコードのエラーハンドリングに捕捉され、テストが想定外のフォールバックパスを辿る原因になる。
7. **ドキュメントの英日分離**: 同一行に `" / "` 区切りで英語と日本語を混在させないこと。各バイリンガルドキュメントは前半が英語セクション、後半が日本語セクションと明確に分かれており、この構造を厳守すること。
