# AGENT_GUIDE.md

## English

### Project Overview

`FolderDiffIL4DotNet` compares two folders and produces auditable diff artifacts.
It is distributed as the `nildiff` .NET global tool.
For `.dll` and `.exe` files, it compares IL instead of raw bytes so build noise such as MVIDs and timestamps does not create false positives.

The main outputs are:

- `diff_report.md`
- `diff_report.html`
- `audit_log.json`

### Repository Name and Command Mapping

- Repository: `FolderDiffIL4DotNet`
- NuGet package and global tool command: `nildiff`
- Local source checkout: `FolderDiffIL4DotNet.sln`

Use `nildiff` in user-facing examples unless you are intentionally referring to the repository name or solution name.

### Build and Test

```bash
dotnet build
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

Before committing, run the Release test configuration when possible:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --configuration Release --nologo
```

Coverage thresholds:

- Total coverage: line >= 80%, branch >= 75%
- Core diff classes (`FileDiffService`, `FolderDiffService`, `FileComparisonService`): line >= 90%, branch >= 85%

### Documentation and Samples

When implementation changes affect user-visible behavior, update the matching docs and samples in the same change set.

Relevant documents:

- `README.md`
- `doc/DEVELOPER_GUIDE.md`
- `doc/TESTING_GUIDE.md`
- `doc/TROUBLESHOOTING.md`
- `CONTRIBUTING.md`
- `SUPPORT.md`
- `SECURITY.md`
- `CHANGELOG.md`
- `doc/samples/diff_report.md`
- `doc/samples/diff_report.html`
- `doc/samples/audit_log.json`
- `.codex/workflows/README.md`

### Bilingual Documentation Rules

- Keep English and Japanese versions aligned when both are present.
- Do not mix the two languages on the same line with ` / ` separators.
- Code comments, XML docs, and repository documentation should stay bilingual when content is added or changed.

### Changelog Rules

- Use Keep a Changelog format.
- Record behavior changes, fixes, and notable documentation updates in the `[Unreleased]` section.
- Keep the English and Japanese sections in sync.

### Release and Publish Rules

- Do not create tags unless the maintainer explicitly asks.
- Do not publish packages unless the maintainer explicitly asks.
- Do not trigger release workflows unless the user explicitly requests that outcome.

### Git Hygiene

- Add explicit files with `git add <file>`.
- Do not use `git add .` or `git add -A`.
- Do not discard user changes that you did not make.

### Code Search Policy

Use the globally installed `cdidx` command on `PATH` for repository search.
Prefer `cdidx` whenever you need repeated local-code search, symbol lookup, dependency traversal, or AI-oriented retrieval from this repository.

Allowed examples:

```bash
cdidx --help
cdidx .
cdidx search "query"
cdidx definition "SymbolName" --exact-name
```

If `cdidx` is unavailable, install or update it:

```bash
dotnet tool install -g cdidx
# or
dotnet tool update -g cdidx
```

Use the most specific `cdidx` command for the task:

- `search` for full-text queries
- `definition` for declaration lookup
- `references`, `callers`, and `callees` for graph-oriented queries
- `symbols` for name-based symbol lookup
- `files` for indexed file lists
- `find` for literal substring matches inside known indexed files
- `excerpt` for reconstructing a line range
- `map`, `inspect`, and `outline` for orientation and symbol context
- `status` and `validate` for index health and encoding checks
- `impact`, `deps`, `unused`, and `hotspots` for transitive impact and maintenance analysis
- `languages` for supported-language capabilities

Use `--json` when the result will be consumed by scripts or AI tooling.

For query precision:

- Use `--exact-name` for `definition`, `references`, `callers`, `callees`, `symbols`, and `inspect` when you already know the symbol name.
- Use `--exact-substring` for `search` when you need a case-sensitive literal text match.
- Use `--path`, `--exclude-path`, and `--exclude-tests` to scope results before broadening the query.

For incremental index updates:

- Prefer `cdidx index <projectPath> --commits <sha...>` after normal commits, because it captures rename and delete paths from git history.
- Use `cdidx index <projectPath> --files <path...>` only for known in-place edits or new files.
- When using `--files`, include old rename or delete paths as well if you need those entries purged from the index.
- If a DB was created before folded-name metadata was upgraded, use `cdidx backfill-fold` or check `status --json` for `fold_ready` before relying on exact-name queries.

Do not bypass the search policy with shell search or discovery commands.

### Prohibited Shell Search and Discovery Commands

Do not use these commands for repository search:

- `grep`
- `rg`
- `find`
- `fd`
- `locate`
- `git grep`
- `ls -R`
- `Get-ChildItem -Recurse`
- `Select-String`

Also avoid repository-local `cdidx.dll` invocations such as `dotnet ./src/CodeIndex/bin/Debug/net8.0/cdidx.dll`.

### Security and Privacy

- Do not paste private paths, proprietary binaries, customer reports, or exploit details into public issues.
- Prefer private vulnerability reporting if the repository has it enabled.
- Treat generated reports, audit logs, and IL output as sensitive artifacts.

### README Visual Asset Protocol

If a README screenshot or GIF would materially improve the page, ask the maintainer for the asset during the task.

Use exact filenames and capture instructions in the request.
Do not create placeholder assets, broken image links, or a separate screenshot guide document.

### Workflows

Use `.codex/workflows/` for task-specific procedures:

- `issue-fix.md`
- `docs-update.md`
- `precommit.md`
- `pr-finalize.md`
- `adversarial-review.md`

### Common Pitfalls and Validation

- Update all related docs and tests together when behavior changes.
- Keep README, contributor docs, and security guidance aligned with the current command name `nildiff`.
- Use explicit validation commands instead of assuming CI state.
- Check `git status --short` before finalizing work.

## 日本語

### プロジェクト概要

`FolderDiffIL4DotNet` は 2 つのフォルダを比較し、監査可能な差分成果物を生成します。
配布形態は `nildiff` という .NET グローバルツールです。
`.dll` と `.exe` については生バイトではなく IL を比較するため、MVID やタイムスタンプのようなビルドノイズで誤検知しません。

主な出力は次の 3 つです。

- `diff_report.md`
- `diff_report.html`
- `audit_log.json`

### リポジトリ名とコマンド名の対応

- リポジトリ名: `FolderDiffIL4DotNet`
- NuGet パッケージ名とグローバルツールコマンド: `nildiff`
- ローカルソースのソリューション: `FolderDiffIL4DotNet.sln`

ユーザー向けの例では、リポジトリ名ではなく `nildiff` を使ってください。

### ビルドとテスト

```bash
dotnet build
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

コミット前には、可能なら Release 構成でテストを実行してください。

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --configuration Release --nologo
```

カバレッジ閾値:

- 合計カバレッジ: 行 >= 80%、分岐 >= 75%
- コア差分クラス (`FileDiffService`, `FolderDiffService`, `FileComparisonService`): 行 >= 90%、分岐 >= 85%

### ドキュメントとサンプル

実装変更でユーザー向け挙動が変わる場合は、同じ差分で関連ドキュメントとサンプルも更新してください。

関連ドキュメント:

- `README.md`
- `doc/DEVELOPER_GUIDE.md`
- `doc/TESTING_GUIDE.md`
- `doc/TROUBLESHOOTING.md`
- `CONTRIBUTING.md`
- `SUPPORT.md`
- `SECURITY.md`
- `CHANGELOG.md`
- `doc/samples/diff_report.md`
- `doc/samples/diff_report.html`
- `doc/samples/audit_log.json`
- `.codex/workflows/README.md`

### 英日併記ルール

- 英語版と日本語版がある場合は内容を一致させる。
- ` / ` で 1 行に英日を混在させない。
- 追加・変更するコードコメント、XML ドキュメント、リポジトリ文書は併記を保つ。

### CHANGELOG ルール

- Keep a Changelog 形式を使う。
- 挙動変更、修正、重要な文書更新は `[Unreleased]` に記載する。
- 英語版と日本語版を同期させる。

### リリースと公開のルール

- メンテナーから明示的な依頼がない限りタグを作成しない。
- メンテナーから明示的な依頼がない限りパッケージを公開しない。
- ユーザーから明示的に求められない限り、リリースワークフローを起動しない。

### Git の運用

- `git add <file>` のように明示的に追加する。
- `git add .` や `git add -A` は使わない。
- 自分が変更していないユーザー作業は破棄しない。

### コード検索ポリシー

リポジトリ検索には、`PATH` 上のグローバル `cdidx` を使ってください。
同じリポジトリを繰り返し検索する場合、シンボル解決、依存関係のたどり、AI 向けの取得では `cdidx` を優先してください。

許可例:

```bash
cdidx --help
cdidx .
cdidx search "query"
cdidx definition "SymbolName" --exact-name
```

`cdidx` が使えない場合は、インストールまたは更新してください。

```bash
dotnet tool install -g cdidx
# または
dotnet tool update -g cdidx
```

用途に応じて最も具体的な `cdidx` コマンドを使ってください。

- `search` は全文検索
- `definition` は定義・宣言の取得
- `references` / `callers` / `callees` はグラフ系の検索
- `symbols` は名前ベースのシンボル検索
- `files` はインデックス済みファイル一覧
- `find` は既知のインデックス済みファイル内でのリテラル部分一致
- `excerpt` は行範囲の再構成
- `map` / `inspect` / `outline` は全体把握とシンボル文脈の取得
- `status` / `validate` は DB 状態とエンコーディング確認
- `impact` / `deps` / `unused` / `hotspots` は影響範囲と保守分析
- `languages` は対応言語と機能の確認

スクリプトや AI ツールに渡す結果では `--json` を使ってください。

検索精度について:

- シンボル名が分かっている場合は `definition` / `references` / `callers` / `callees` / `symbols` / `inspect` で `--exact-name` を使う
- 大文字小文字を区別したリテラル文字列一致が必要な場合は `search` で `--exact-substring` を使う
- 範囲を絞るときは `--path` / `--exclude-path` / `--exclude-tests` を先に使う

増分更新について:

- 通常のコミット後は、`cdidx index <projectPath> --commits <sha...>` を優先する
- `--files` は、既知のインプレース編集や新規ファイルだけに使う
- `--files` を使う場合、rename/delete した古いパスも消したいならそれらも含める
- 旧 DB で folded-name メタデータが古い場合は、`cdidx backfill-fold` を使うか、`status --json` の `fold_ready` を確認してから exact-name 検索に頼る

シェル検索や探索コマンドでこの方針を回避しないでください。

### 禁止するシェル検索・探索コマンド

リポジトリ検索には次のコマンドを使わないでください。

- `grep`
- `rg`
- `find`
- `fd`
- `locate`
- `git grep`
- `ls -R`
- `Get-ChildItem -Recurse`
- `Select-String`

`dotnet ./src/CodeIndex/bin/Debug/net8.0/cdidx.dll` のようなリポジトリローカル実行も避けてください。

### セキュリティとプライバシー

- 公開 issue に private なパス、プロプライエタリなバイナリ、顧客レポート、攻撃詳細を貼らない。
- 脆弱性報告は、利用可能であれば private な経路を優先する。
- 生成されたレポート、監査ログ、IL 出力は機密情報として扱う。

### README の画像・GIF ポリシー

README にスクリーンショットや GIF が有効な場合は、作業中にメンテナーへ依頼してください。

依頼時は、正確なファイル名と撮影手順を含めてください。
プレースホルダー画像、壊れたリンク、別のスクリーンショットガイド文書は作らないでください。

### ワークフロー

タスク固有の手順は `.codex/workflows/` を使います。

- `issue-fix.md`
- `docs-update.md`
- `precommit.md`
- `pr-finalize.md`
- `adversarial-review.md`

### よくある落とし穴と確認

- 挙動変更時は関連ドキュメントとテストを同時に更新する。
- README、寄稿者向け文書、セキュリティ案内は `nildiff` という実コマンド名と一致させる。
- CI 状態を仮定せず、明示的な確認コマンドを使う。
- 作業完了前に `git status --short` を確認する。
