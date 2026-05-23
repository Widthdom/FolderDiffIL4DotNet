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
- Core diff classes (`FileDiffService`, `FolderDiffService`, `FileComparisonService`): line >= 85%, branch >= 65%

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

This repository uses `cdidx` for fast code search through the local SQLite index at `.cdidx/codeindex.db`.
Use the globally installed `cdidx` command on `PATH` for repository search.
Query this index instead of using shell search or discovery commands such as `find`, `grep`, `rg`, or `ls -R`.

Prefer `cdidx` whenever you need repeated local-code search, symbol lookup, dependency traversal, impact analysis, or AI-oriented retrieval from this repository.

#### Setup and Availability

First check whether `cdidx` is available:

```bash
cdidx --version
```

If `cdidx` is unavailable, install it with the self-contained installer:

```bash
curl -fsSL https://raw.githubusercontent.com/Widthdom/CodeIndex/main/install.sh | bash
```

Or, when the .NET 8+ SDK is available, install or update the global tool:

```bash
dotnet tool install -g cdidx
dotnet tool update -g cdidx
```

When `cdidx` runs as a distributed (non-development) install, it appends stderr and minimal lifecycle breadcrumbs to a per-user log file so silent hosts still leave traces. Default locations are `%LOCALAPPDATA%\cdidx\logs\` on Windows, `~/Library/Logs/cdidx/` on macOS, and `$XDG_STATE_HOME/cdidx/logs/` (or `~/.local/state/cdidx/logs/`) on Linux. Logs rotate daily and retain the newest 30 files. Set `CDIDX_DISABLE_PERSISTENT_LOG=1` to opt out.

To reinstall or switch to a specific version, use the explicit-version installer form:

```bash
curl -fsSL https://raw.githubusercontent.com/Widthdom/CodeIndex/vX.Y.Z/install.sh | bash -s -- vX.Y.Z
```

Re-running the no-argument one-liner still targets the latest release. The installer resolves the latest tag first, then skips the download only when the current healthy install already matches that latest version. Broken `v0.0.0` installs and same-version installs missing required adjacent assets are treated as reinstall targets. Use the explicit-version form when you need to force a specific version.

If installation fails and `.cdidx/codeindex.db` already exists, `sqlite3` may be used only as a basic fallback for raw text or symbol inspection.
Prefer `cdidx` for call graph queries, freshness metadata, exact-name semantics, scoped snippets, `impact`, `unused`, and `hotspots`.
If neither `cdidx` nor `sqlite3` is available, use the Claude Code built-in `Grep` / `Glob` tools (or your harness's equivalent non-shell search tools).
Do not fall back to shell `rg`, `grep`, `find`, or recursive listing commands, or to a global `cdidx` invocation when the repository's deny list or local-build policy would otherwise block it — bypassing those checks can hide stale-binary bugs.

#### Freshness and Index Updates

Before searching, check whether the index matches the workspace:

```bash
cdidx status --check --json
```

If the command exits `0` and reports `index_matches_workspace: true`, skip reindexing.
Otherwise, update the index before trusting search results:

```bash
cdidx .
```

**Rule: whenever source files are modified, run `cdidx status --check --json` before the next search; if it reports a mismatch, run one of the update commands below.**

```bash
cdidx . --files path/to/changed_file.cs
cdidx . --commits HEAD
cdidx . --commits abc123
cdidx .
```

- Use `--files` for known in-place edits or new files.
- Include old rename/delete paths with `--files` if you need those entries purged.
- Prefer `--commits` after a normal commit because git history carries rename and delete paths.
- After `git reset`, `git rebase`, `git commit --amend`, `git switch`, or `git merge`, prefer `cdidx .` or `cdidx . --json` so stale paths are purged against the current checkout.
- Use `cdidx index --dry-run` to preview indexing changes when needed.
- If a DB has stale folded-name metadata, run `cdidx backfill-fold` or check `status --json` for `fold_ready` before relying on exact-name queries.

#### Query Strategy

Start with freshness and orientation when context matters:

```bash
cdidx status --check --json
cdidx map --path src/ --exclude-tests --json
```

Use the most specific `cdidx` command for the task:

- `map` for language, module, hotspot, and likely-entrypoint orientation
- `inspect` for bundled definition, reference, caller, callee, file, and trust metadata around a candidate symbol
- `symbols` to resolve candidate names before `definition`, `references`, `callers`, `callees`, or `impact`
- `definition` for declaration text, with `--body` when implementation body matters
- `references`, `callers`, and `callees` for symbol-aware graph questions in graph-supported languages
- `impact` for pre-edit ripple checks on callable symbols
- `search` for raw text, comments, strings, punctuation-heavy literals, or languages without useful structured symbols
- `files` to discover candidate paths
- `find` to locate literal text inside known indexed files
- `excerpt` to reconstruct only the needed line range instead of opening a whole file
- `outline` to inspect one file's symbol structure
- `deps` and `deps --reverse` for file-level dependency analysis
- `unused` for bucketed dead-code triage in graph-supported languages
- `hotspots` for central or high-impact symbols
- `validate` for encoding checks
- `languages` for supported language and feature names

Query precision rules:

- Add `--exact` or `--exact-name` once the intended symbol is known, so names such as `Run` do not expand to `RunAsync` or `RunImpact`.
- Use `languages` when you need the canonical `--lang` filter name.
- Use `--path`, repeatable `--exclude-path`, and `--exclude-tests` before broad searches.
- Use `--exact-substring` for case-sensitive literal text, punctuation-heavy strings, operators, or other text where FTS tokenization is a poor fit.
- Use `--fts` only when intentionally writing raw FTS5 syntax such as `NEAR` or `OR`.
- Use `--snippet-lines <n>` and `--max-line-width <n>` to keep search JSON compact for AI or scripted consumers.
- Use `--count` for preflight sizing before fetching full results.
- Use `files --since <datetime>` or `search --since <datetime>` to focus on recently modified code.

Use `--json` whenever results will be consumed by scripts or AI tooling.

Recommended CLI examples:

```bash
cdidx inspect "Authenticate" --lang csharp --exact --exclude-tests
cdidx symbols --lang csharp --name Authenticate --exact-name
cdidx definition "Authenticate" --lang csharp --exact --body
cdidx search "keyword" --path src/ --exclude-tests --snippet-lines 6 --max-line-width 160
cdidx search "Run();" --exact-substring --path src/
cdidx callers "Authenticate" --lang csharp --exact --exclude-tests
cdidx impact "Authenticate" --lang csharp --exact --exclude-tests --json
cdidx deps --path src/Services/AuthService.cs --reverse --json
cdidx hotspots --lang csharp --limit 20 --json
cdidx unused --lang csharp --exclude-tests --json
cdidx find "guard" --path src/app.py --after 2
cdidx excerpt src/app.py --start 10 --end 20
cdidx outline src/app.py --json
cdidx languages --json
```

Markdown files index ATX and setext headings as `heading` symbols and local anchor references as `reference` symbols, so use `symbols` and `outline` to navigate documentation structure.
If `cdidx` itself behaves unexpectedly, file an issue at <https://github.com/Widthdom/CodeIndex/issues> with what happened and what you expected.

#### Direct SQL Fallback

Use direct SQL only when `cdidx` is unavailable and `.cdidx/codeindex.db` already exists.
This requires `sqlite3` and is limited to basic raw text or symbol inspection.

If `sqlite3` is not installed:

- macOS: pre-installed
- Linux: `sudo apt install sqlite3`
- Windows: `winget install SQLite.SQLite` or `scoop install sqlite`

```sql
SELECT f.path, c.start_line, c.content
FROM fts_chunks fc
JOIN chunks c ON c.id = fc.rowid
JOIN files f ON f.id = c.file_id
WHERE fts_chunks MATCH 'keyword'
LIMIT 20;
```

```sql
SELECT f.path, s.name, s.line
FROM symbols s
JOIN files f ON f.id = s.file_id
WHERE s.kind = 'function' AND s.name LIKE '%keyword%';
```

#### Incremental Updates for CI and Hooks

Instead of re-indexing the entire project, automation can update only changed files:

```bash
cdidx . --commits abc123 def456
cdidx . --files src/app.cs src/utils.cs
```

Prefer `--commits` for commit-driven automation because git history also carries rename and delete paths.
Use `--files` for editor / save hooks that touch existing paths or add new files; old rename or delete paths are not purged unless they are listed explicitly.
After `git reset`, `git rebase`, `git commit --amend`, `git switch`, or `git merge`, prefer a full `cdidx . --json` refresh so repo-wide stale paths are purged against the current checkout.

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
- コア差分クラス (`FileDiffService`, `FolderDiffService`, `FileComparisonService`): 行 >= 85%、分岐 >= 65%

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

このリポジトリでは `.cdidx/codeindex.db` のローカル SQLite インデックスを使い、`cdidx` で高速にコード検索します。
リポジトリ検索には、`PATH` 上のグローバル `cdidx` を使ってください。
`find`、`grep`、`rg`、`ls -R` のようなシェル検索・探索コマンドではなく、このインデックスを検索してください。

同じリポジトリを繰り返し検索する場合、シンボル解決、依存関係のたどり、影響分析、AI 向けの取得では `cdidx` を優先してください。

#### セットアップと利用可否

まず `cdidx` が利用可能か確認してください。

```bash
cdidx --version
```

`cdidx` が使えない場合は、self-contained installer でインストールしてください。

```bash
curl -fsSL https://raw.githubusercontent.com/Widthdom/CodeIndex/main/install.sh | bash
```

.NET 8+ SDK がある場合は、グローバルツールとしてインストールまたは更新してもかまいません。

```bash
dotnet tool install -g cdidx
dotnet tool update -g cdidx
```

`cdidx` を distributed（非開発）インストールで動かす場合、stderr とライフサイクル breadcrumb をユーザーごとのログファイルに追記します。サイレントなホストでも痕跡が残るようにするためです。デフォルト保存先は Windows が `%LOCALAPPDATA%\cdidx\logs\`、macOS が `~/Library/Logs/cdidx/`、Linux が `$XDG_STATE_HOME/cdidx/logs/`（または `~/.local/state/cdidx/logs/`）です。日次ローテーションで最新 30 ファイルを保持します。停止したい場合は `CDIDX_DISABLE_PERSISTENT_LOG=1` を設定してください。

特定バージョンへの再インストールや切り替えは、明示バージョン形式を使ってください。

```bash
curl -fsSL https://raw.githubusercontent.com/Widthdom/CodeIndex/vX.Y.Z/install.sh | bash -s -- vX.Y.Z
```

引数なしのワンライナーを再実行した場合も常に latest release を対象にします。インストーラーはまず最新タグを解決し、現在の健全な install がその最新版と一致しているときだけダウンロードを skip します。壊れた `v0.0.0` インストールや、同版でも必須隣接資産が欠けているインストールは再インストール対象です。特定の版を強制したいときは、上の明示バージョン形式を使ってください。

インストールに失敗し、`.cdidx/codeindex.db` が既にある場合に限り、`sqlite3` を raw text や symbol の最低限の確認用フォールバックとして使ってかまいません。
call graph、freshness metadata、exact-name semantics、scoped snippet、`impact`、`unused`、`hotspots` には `cdidx` を優先してください。
`cdidx` も `sqlite3` も利用できない場合は、Claude Code 組み込みの `Grep` / `Glob`（または使用ハーネスの同等の非シェル検索ツール）を使ってください。
shell の `rg`、`grep`、`find`、再帰的な一覧コマンドにはフォールバックしないでください。リポジトリの deny list やローカルビルド方針でブロックされる場合に、グローバル `cdidx` で迂回するのも避けてください。古いバイナリ由来のバグを隠す原因になります。

#### 鮮度確認とインデックス更新

検索前に、インデックスが現在の workspace と一致しているか確認してください。

```bash
cdidx status --check --json
```

終了コード `0` かつ `index_matches_workspace: true` なら再インデックス不要です。
それ以外の場合は、検索結果を信用する前にインデックスを更新してください。

```bash
cdidx .
```

**ルール: ソースファイルを修正したら、次の検索前に `cdidx status --check --json` を実行し、差分が報告された場合は下記のいずれかの更新コマンドを実行すること。**

```bash
cdidx . --files path/to/changed_file.cs
cdidx . --commits HEAD
cdidx . --commits abc123
cdidx .
```

- `--files` は既知の in-place 編集や新規ファイルに使う。
- rename/delete した古い path も purge したい場合は、`--files` にそれらも含める。
- 通常のコミット後は、git 履歴に rename/delete path が含まれるため `--commits` を優先する。
- `git reset`、`git rebase`、`git commit --amend`、`git switch`、`git merge` の後は、現在の checkout に対して stale path を purge するため `cdidx .` または `cdidx . --json` を優先する。
- 必要なら `cdidx index --dry-run` でインデックス変更を事前確認する。
- folded-name metadata が古い DB では、exact-name 検索に頼る前に `cdidx backfill-fold` を実行するか、`status --json` の `fold_ready` を確認する。

#### クエリ戦略

文脈が重要な場合は、鮮度確認と全体把握から始めてください。

```bash
cdidx status --check --json
cdidx map --path src/ --exclude-tests --json
```

用途に応じて最も具体的な `cdidx` コマンドを使ってください。

- `map` は言語、モジュール、hotspot、推定 entrypoint の把握
- `inspect` は候補シンボル周辺の定義、参照、caller、callee、ファイル情報、信頼判断メタデータの一括取得
- `symbols` は `definition`、`references`、`callers`、`callees`、`impact` の前に候補名を固める用途
- `definition` は宣言テキストの取得、本体が必要なら `--body` を付ける
- `references` / `callers` / `callees` は graph-supported language のシンボル-aware なグラフ調査
- `impact` は変更前の callable symbol の波及確認
- `search` は raw text、コメント、文字列、記号を多く含む literal、構造化シンボル抽出が弱い言語の調査
- `files` は候補 path の把握
- `find` は既知のインデックス済みファイル内でのリテラル検索
- `excerpt` はファイル全体ではなく必要な行範囲だけの再構成
- `outline` は 1 ファイルのシンボル構造確認
- `deps` / `deps --reverse` はファイル間依存の分析
- `unused` は graph-supported language の bucket 化されたデッドコード候補調査
- `hotspots` は中心的または影響の大きいシンボル調査
- `validate` はエンコーディング確認
- `languages` は対応言語と機能名の確認

スクリプトや AI ツールに渡す結果では `--json` を使ってください。

検索精度のルール:

- 対象シンボルが分かったら `--exact` または `--exact-name` を付けて、`Run` が `RunAsync` や `RunImpact` に広がらないようにする。
- `--lang` に渡す正式なフィルター名を確認したい場合は `languages` を使う。
- 広い検索の前に `--path`、繰り返し指定できる `--exclude-path`、`--exclude-tests` で範囲を絞る。
- 大文字小文字を区別したリテラル、記号を多く含む文字列、演算子、FTS tokenization と相性の悪い文字列には `--exact-substring` を使う。
- `NEAR` や `OR` のような raw FTS5 構文を意図して書く場合だけ `--fts` を使う。
- AI やスクリプト向けに検索 JSON を小さく保つには `--snippet-lines <n>` と `--max-line-width <n>` を使う。
- 全件取得前の件数確認には `--count` を使う。
- 最近変更されたコードに絞るには `files --since <datetime>` や `search --since <datetime>` を使う。

推奨 CLI 例:

```bash
cdidx inspect "Authenticate" --lang csharp --exact --exclude-tests
cdidx symbols --lang csharp --name Authenticate --exact-name
cdidx definition "Authenticate" --lang csharp --exact --body
cdidx search "keyword" --path src/ --exclude-tests --snippet-lines 6 --max-line-width 160
cdidx search "Run();" --exact-substring --path src/
cdidx callers "Authenticate" --lang csharp --exact --exclude-tests
cdidx impact "Authenticate" --lang csharp --exact --exclude-tests --json
cdidx deps --path src/Services/AuthService.cs --reverse --json
cdidx hotspots --lang csharp --limit 20 --json
cdidx unused --lang csharp --exclude-tests --json
cdidx find "guard" --path src/app.py --after 2
cdidx excerpt src/app.py --start 10 --end 20
cdidx outline src/app.py --json
cdidx languages --json
```

Markdown ファイルは ATX / setext 見出しを `heading` シンボルとして索引し、local anchor 参照も `reference` シンボルとして表面化するため、`symbols` と `outline` でドキュメント構造をたどってください。
cdidx 自体の予期しない挙動を見つけた場合は、発生した事象と期待する動作を <https://github.com/Widthdom/CodeIndex/issues> に報告してください。

#### 直接 SQL フォールバック

直接 SQL は、`cdidx` が使えず `.cdidx/codeindex.db` が既に存在する場合だけ使ってください。
`sqlite3` が必要で、raw text や symbol の最低限の確認に限定します。

`sqlite3` が未インストールの場合の導入手順:

- macOS: プリインストール済み
- Linux: `sudo apt install sqlite3`
- Windows: `winget install SQLite.SQLite` または `scoop install sqlite`

```sql
SELECT f.path, c.start_line, c.content
FROM fts_chunks fc
JOIN chunks c ON c.id = fc.rowid
JOIN files f ON f.id = c.file_id
WHERE fts_chunks MATCH 'キーワード'
LIMIT 20;
```

```sql
SELECT f.path, s.name, s.line
FROM symbols s
JOIN files f ON f.id = s.file_id
WHERE s.kind = 'function' AND s.name LIKE '%キーワード%';
```

#### CI / フック向けインクリメンタル更新

プロジェクト全体を再インデックスする代わりに、自動化では変更のあったファイルだけを更新してください。

```bash
cdidx . --commits abc123 def456
cdidx . --files src/app.cs src/utils.cs
```

通常のコミット直後の自動化では、git 履歴に rename / delete path も含まれるため `--commits` を優先してください。
`--files` は editor / save hook など、既存 path の編集や新規ファイル追加だけを前提とするケース向けです。明示しない限り旧 rename / delete path は purge されません。
`git reset`、`git rebase`、`git commit --amend`、`git switch`、`git merge` の後は、リポジトリ全体の stale path を掃除するため `cdidx . --json` のフル更新を優先してください。

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
