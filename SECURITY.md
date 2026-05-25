# Security

## English

This document describes the threat model and security considerations for FolderDiffIL4DotNet, a .NET 8 release-validation tool that compares two folders at the IL level and produces audit reports.

### Reporting Security Issues

If the repository supports private vulnerability reporting, use that path first.
Otherwise, open a minimal public issue and ask for a private channel.

Do not include exploit details, private paths, customer reports, proprietary binaries, tokens, or other sensitive report artifacts in a public issue.

### Threat Model

The existing threat model remains unchanged:

- HTML diff reports and reviewed HTML may expose internal paths and code changes.
- Audit logs and IL output are sensitive artifacts.
- The HTML report must remain self-contained and must not load external resources.

Refer to the rest of this file for the preserved STRIDE analysis, mitigations, and known limitations.

## Threat Model

### Assets

| Asset | Description | Sensitivity |
|---|---|---|
| Diff report (HTML) | Interactive single-file report with inline diffs, file paths, timestamps, assembly metadata | Medium |
| Diff report (Markdown) | Text-based report for archiving | Medium |
| Audit log (JSON) | Structured log with SHA256 hashes, file inventory, comparison metadata | Medium |
| Reviewed HTML | Legal/compliance artifact with reviewer sign-off data, justifications, and integrity verification | High |
| IL disassembly output | Decompiled .NET assembly IL code, cached during comparison | High |

### Trust Boundaries

1. User input: CLI arguments, `config.json` / `config.jsonc`, and `FOLDERDIFF_*` environment variables
2. File system: old/new folder contents, report output directory, IL disassembly cache
3. External tools: `dotnet-ildasm` and `ilspycmd`
4. Browser: local HTML report rendering

### Threats & Mitigations

#### Tampering

- SHA256 integrity hashes are recorded in `audit_log.json`
- Reviewed HTML embeds SHA256-based self-verification
- Companion `.sha256` files can be generated
- Report headers include generation timestamp and tool version

#### Information Disclosure

- The HTML report uses a strict Content-Security-Policy meta tag
- No network requests are made from the report
- The report is self-contained and has no external dependencies
- File paths may reveal internal directory structure
- IL disassembly output may contain sensitive intellectual property

#### Injection

- User-supplied data is HTML-encoded before insertion into HTML output
- CSP restricts script execution and external sources
- No form submissions or external data loading are used

#### Denial of Service

- Configurable parallelism limits unbounded thread usage
- IL cache budgets limit growth
- Inline diff cost is capped
- Disassembler timeout is configurable
- Failing disassemblers can be blacklisted

#### Elevation of Privilege

- The tool runs under the invoking user
- Subprocess execution uses explicit paths
- No privilege escalation mechanisms exist

#### Spoofing

- Report headers include provenance data
- Disassembler availability is reported with tool names and versions
- Reviewed HTML SHA256 verification preserves the trust chain

### Subprocess Security

- Disassembler commands are hardcoded candidates, not arbitrary user commands
- External tool paths are resolved via `PATH` or configuration
- Each disassembler invocation has a configurable timeout
- Non-ASCII paths are handled via temporary ASCII-safe copies

### Configuration Security

- Environment overrides are scoped to `FOLDERDIFF_*`
- Configuration is validated at startup
- The tool does not store or require secrets

### Known Limitations

| Limitation | Rationale |
|---|---|
| CSP uses `unsafe-inline` | Required for the self-contained HTML design |
| File paths may reveal directory structure | Inherent to folder comparison |
| IL disassembly output may contain sensitive code | Necessary for IL-level comparison |

## セキュリティ

### 資産

| 資産 | 説明 | 機密性 |
|---|---|---|
| 差分レポート（HTML） | インライン差分、ファイルパス、タイムスタンプ、アセンブリメタデータを含む単一ファイルの対話型レポート | 中 |
| 差分レポート（Markdown） | 保管用のテキストレポート | 中 |
| 監査ログ（JSON） | SHA256 ハッシュ、ファイル一覧、比較メタデータを含む構造化ログ | 中 |
| レビュー済み HTML | 承認データ、理由、整合性検証を含む監査成果物 | 高 |
| IL 逆アセンブリ出力 | 比較中にキャッシュされる .NET アセンブリの IL コード | 高 |

### 信頼境界

1. ユーザー入力: CLI 引数、`config.json` / `config.jsonc`、`FOLDERDIFF_*` 環境変数
2. ファイルシステム: 比較対象の旧/新フォルダ、レポート出力先、IL キャッシュ
3. 外部ツール: `dotnet-ildasm` と `ilspycmd`
4. ブラウザ: ローカル HTML レポートのレンダリング

### 脅威と緩和策

#### 改竄

- SHA256 整合性ハッシュを `audit_log.json` に記録する
- レビュー済み HTML に SHA256 ベースの自己検証を埋め込む
- 付随する `.sha256` ファイルを生成できる
- レポートヘッダーに生成時刻とツールバージョンを含める

#### 情報漏洩

- HTML レポートは厳格な Content-Security-Policy メタタグを使う
- レポートからネットワークリクエストは発生しない
- レポートは外部依存のない自己完結型
- ファイルパスは内部ディレクトリ構造を露出しうる
- IL 出力には機微な知的財産が含まれうる

#### インジェクション

- ユーザー提供データは HTML 出力前にエンコードする
- CSP でスクリプト実行と外部ソースを制限する
- フォーム送信や外部データ読み込みは使わない

#### サービス拒否

- 設定可能な並列度でスレッド使用を制限する
- IL キャッシュ容量の上限を設定する
- インライン差分の計算コストを制限する
- 逆アセンブラのタイムアウトを設定できる
- 失敗する逆アセンブラはブラックリスト化できる

#### 権限昇格

- ツールは呼び出しユーザー権限で動作する
- サブプロセス実行は明示的なパスを使う
- 権限昇格の仕組みはない

#### なりすまし

- レポートヘッダーに provenance 情報を含める
- 逆アセンブラの可用性をツール名とバージョン付きで表示する
- レビュー済み HTML の SHA256 検証で信頼チェーンを維持する

### サブプロセスセキュリティ

- 逆アセンブラ候補はハードコードされたコマンドであり、任意コマンドではない
- 外部ツールパスは `PATH` または設定から解決する
- 各逆アセンブラ呼び出しにはタイムアウトがある
- 非 ASCII パスは一時的な ASCII セーフコピーで扱う

### 設定セキュリティ

- 環境変数オーバーライドは `FOLDERDIFF_*` に限定する
- 設定は起動時に検証する
- ツールはシークレットを保存も要求もしない

### 既知の制限事項

| 制限 | 理由 |
|---|---|
| CSP に `unsafe-inline` を使う | 自己完結型 HTML を維持するため |
| ファイルパスがディレクトリ構造を露出する | フォルダ比較の目的上不可避 |
| IL 出力には機微なコードが含まれうる | IL レベル比較に必要 |

## 日本語

本書は、FolderDiffIL4DotNet（.NET 8 のリリース検証ツール。2 つのフォルダを IL レベルで比較し、監査レポートを生成する）の脅威モデルとセキュリティ上の注意点を説明します。

### 脆弱性報告

リポジトリで private vulnerability reporting が利用できる場合は、まずそちらを使ってください。
利用できない場合は、最小限の public issue を作成して private な連絡経路を依頼してください。

public issue には、攻撃手順、private なパス、顧客レポート、プロプライエタリなバイナリ、トークン、その他の機微な成果物を含めないでください。

### 脅威モデル

既存の脅威モデルは維持します。

- HTML 差分レポートと reviewed HTML には内部パスやコード変更が含まれる可能性があります。
- 監査ログと IL 出力は機微な成果物です。
- HTML レポートは自己完結型を維持し、外部リソースを読み込んではいけません。

この後半には、既存の STRIDE 分析、緩和策、既知の制限事項をそのまま残します。
