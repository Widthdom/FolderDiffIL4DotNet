# Security

## English

This document explains how to report vulnerabilities privately and describes the threat model for FolderDiffIL4DotNet, a .NET 8 release-validation tool that compares two folders at the IL level and produces audit reports.

### Supported Versions

| Version | Supported |
|---|---|
| Latest published release | Yes |
| Current `main` branch | Yes |
| Older releases | No |

Before reporting, confirm whether the vulnerability still affects the latest published release or the current `main` branch.

### Reporting a Vulnerability

Submit suspected vulnerabilities through [GitHub private vulnerability reporting](https://github.com/Widthdom/FolderDiffIL4DotNet/security/advisories/new).
The report and follow-up discussion remain private until disclosure is coordinated.
Do not use a public GitHub Issue to report a vulnerability.

Include the following when available:

- the affected version, commit, or environment
- the security impact and the conditions required to trigger it
- minimal reproduction steps or a proof of concept
- relevant configuration and platform details
- a suggested mitigation or fix, if known
- your preferred contact method and credit preference

Do not include unrelated private paths, customer reports, proprietary binaries, tokens, credentials, or other sensitive artifacts.
If a sensitive artifact is necessary to reproduce the finding, describe it first and coordinate a safe transfer method in the private report.

### Response and Coordinated Disclosure

The maintainers aim to:

- acknowledge a new report within 3 business days
- provide an initial assessment and next steps within 7 business days
- provide a status update at least every 14 calendar days while remediation is in progress

These are response targets rather than guarantees; complex reports may require more time.
The maintainers and reporter will coordinate remediation, advisory publication, and any public disclosure.
Please keep vulnerability details private until a fix or mitigation is available and the agreed disclosure date is reached.
The advisory will credit the reporter when requested and appropriate.

### Threat Model

#### Assets

| Asset | Description | Sensitivity |
|---|---|---|
| Diff report (HTML) | Interactive single-file report with inline diffs, file paths, timestamps, assembly metadata | Medium |
| Diff report (Markdown) | Text-based report for archiving | Medium |
| Audit log (JSON) | Structured log with SHA256 hashes, file inventory, comparison metadata | Medium |
| Reviewed HTML | Legal/compliance artifact with reviewer sign-off data, justifications, and integrity verification | High |
| IL disassembly output | Decompiled .NET assembly IL code, cached during comparison | High |

#### Trust Boundaries

1. User input: CLI arguments, `config.json` / `config.jsonc`, and `FOLDERDIFF_*` environment variables
2. File system: old/new folder contents, report output directory, IL disassembly cache
3. External tools: `dotnet-ildasm`, `ilspycmd`, and the user-selected `dotnet tool update` process
4. Browser: local HTML report rendering
5. Network: trusted nuget.org package-registration metadata used by the console-startup update check

#### Threats and Mitigations

##### Tampering

- SHA256 integrity hashes are recorded in `audit_log.json`
- Reviewed HTML embeds SHA256-based self-verification
- Companion `.sha256` files can be generated
- Report headers include generation timestamp and tool version

##### Information Disclosure

- The HTML report uses a strict Content-Security-Policy meta tag
- No network requests are made from the report
- The report is self-contained and has no external dependencies
- File paths may reveal internal directory structure
- IL disassembly output may contain sensitive intellectual property

##### Injection

- User-supplied data is HTML-encoded before insertion into HTML output
- CSP restricts script execution and external sources
- No form submissions or external data loading are used

##### Denial of Service

- Configurable parallelism limits unbounded thread usage
- IL cache budgets limit growth
- Inline diff cost is capped
- Disassembler timeout is configurable
- Failing disassemblers can be blacklisted
- The startup update request has a two-second timeout, a 20-hour success cache, and a one-hour failure backoff

##### Elevation of Privilege

- The tool runs under the invoking user
- Subprocess execution uses explicit paths
- No privilege escalation mechanisms exist

##### Spoofing

- Report headers include provenance data
- Disassembler availability is reported with tool names and versions
- Reviewed HTML SHA256 verification preserves the trust chain

#### Subprocess Security

- Disassembler commands are hardcoded candidates, not arbitrary user commands
- The update prompt runs only the fixed `dotnet tool update --global nildiff` executable and argument list without a shell
- External tool paths are resolved via `PATH` or configuration
- Each disassembler invocation has a configurable timeout
- Non-ASCII paths are handled via temporary ASCII-safe copies

#### Configuration Security

- Environment overrides are scoped to `FOLDERDIFF_*`
- Configuration is validated at startup
- The tool does not store or require secrets

#### Update-Check Network Security

- The update-check phase requires stdin, stdout, and stderr all to be unredirected, preventing it from consuming piped input, blocking automation, or adding update text to redirected output; see the [user guide](USER_GUIDE.md#guide-en-startup-update-notification) for platform examples and user-facing behavior
- The client requests fixed HTTPS nuget.org registration metadata and follows registration-page links only when they remain on `https://api.nuget.org`
- No compared paths, report data, configuration, or credentials are sent
- Update execution requires an explicit `1. Update now` selection; `2. Skip` and `3. Skip until next version` run no process, and choice 3 only stores the dismissed public package version in the user-local cache
- Network, response, cache, and updater failures are contained and cannot fail the requested command

#### Known Limitations

| Limitation | Rationale |
|---|---|
| CSP uses `unsafe-inline` | Required for the self-contained HTML design |
| File paths may reveal directory structure | Inherent to folder comparison |
| IL disassembly output may contain sensitive code | Necessary for IL-level comparison |

## 日本語

本書は、FolderDiffIL4DotNet（2 つのフォルダを IL レベルで比較し、監査レポートを生成する .NET 8 のリリース検証ツール）の脆弱性を非公開で報告する方法と脅威モデルを説明します。

### サポート対象バージョン

| バージョン | サポート |
|---|---|
| 最新の公開リリース | 対象 |
| 現在の `main` ブランチ | 対象 |
| 過去のリリース | 対象外 |

報告前に、脆弱性が最新の公開リリースまたは現在の `main` ブランチでも再現するか確認してください。

### 脆弱性の報告

脆弱性の疑いは [GitHub private vulnerability reporting](https://github.com/Widthdom/FolderDiffIL4DotNet/security/advisories/new) から報告してください。
報告内容とその後のやり取りは、開示を調整するまで非公開で扱われます。
脆弱性の報告に公開 GitHub Issue を使用しないでください。

可能な範囲で次の情報を含めてください。

- 影響を受けるバージョン、コミット、または環境
- セキュリティ上の影響と、発生に必要な条件
- 最小限の再現手順または概念実証
- 関連する設定とプラットフォームの詳細
- 判明している場合は、推奨する緩和策または修正案
- 希望する連絡方法とクレジット表記

無関係な private パス、顧客レポート、プロプライエタリなバイナリ、トークン、認証情報、その他の機微な成果物は含めないでください。
再現に機微な成果物が必要な場合は、まずその概要を記載し、private report 内で安全な受け渡し方法を調整してください。

### 対応と協調開示

メンテナーは次の対応を目標とします。

- 新規報告を 3 営業日以内に確認する
- 7 営業日以内に初期評価と次の対応を提示する
- 修正対応中は少なくとも 14 暦日ごとに状況を更新する

これらは保証ではなく対応目標であり、複雑な報告では追加の時間が必要になる場合があります。
メンテナーと報告者は、修正、advisory の公開、一般への開示時期を調整します。
修正または緩和策が利用可能になり、合意した開示日を迎えるまで、脆弱性の詳細を非公開に保ってください。
希望があり適切な場合は、advisory に報告者のクレジットを記載します。

### 脅威モデル

#### 資産

| 資産 | 説明 | 機密性 |
|---|---|---|
| 差分レポート（HTML） | インライン差分、ファイルパス、タイムスタンプ、アセンブリメタデータを含む単一ファイルの対話型レポート | 中 |
| 差分レポート（Markdown） | 保管用のテキストレポート | 中 |
| 監査ログ（JSON） | SHA256 ハッシュ、ファイル一覧、比較メタデータを含む構造化ログ | 中 |
| レビュー済み HTML | 承認データ、理由、整合性検証を含む監査成果物 | 高 |
| IL 逆アセンブリ出力 | 比較中にキャッシュされる .NET アセンブリの IL コード | 高 |

#### 信頼境界

1. ユーザー入力: CLI 引数、`config.json` / `config.jsonc`、`FOLDERDIFF_*` 環境変数
2. ファイルシステム: 比較対象の旧/新フォルダ、レポート出力先、IL キャッシュ
3. 外部ツール: `dotnet-ildasm`、`ilspycmd`、ユーザー選択時の `dotnet tool update` プロセス
4. ブラウザ: ローカル HTML レポートのレンダリング
5. ネットワーク: コンソール起動時の更新確認で使う、信頼済み nuget.org パッケージ registration metadata

#### 脅威と緩和策

##### 改竄

- SHA256 整合性ハッシュを `audit_log.json` に記録する
- レビュー済み HTML に SHA256 ベースの自己検証を埋め込む
- 付随する `.sha256` ファイルを生成できる
- レポートヘッダーに生成時刻とツールバージョンを含める

##### 情報漏洩

- HTML レポートは厳格な Content-Security-Policy メタタグを使う
- レポートからネットワークリクエストは発生しない
- レポートは外部依存のない自己完結型
- ファイルパスは内部ディレクトリ構造を露出しうる
- IL 出力には機微な知的財産が含まれうる

##### インジェクション

- ユーザー提供データは HTML 出力前にエンコードする
- CSP でスクリプト実行と外部ソースを制限する
- フォーム送信や外部データ読み込みは使わない

##### サービス拒否

- 設定可能な並列度でスレッド使用を制限する
- IL キャッシュ容量の上限を設定する
- インライン差分の計算コストを制限する
- 逆アセンブラのタイムアウトを設定できる
- 失敗する逆アセンブラはブラックリスト化できる
- 起動時の更新リクエストは 2 秒でタイムアウトし、成功時は 20 時間キャッシュ、失敗時は 1 時間バックオフする

##### 権限昇格

- ツールは呼び出しユーザー権限で動作する
- サブプロセス実行は明示的なパスを使う
- 権限昇格の仕組みはない

##### なりすまし

- レポートヘッダーに provenance 情報を含める
- 逆アセンブラの可用性をツール名とバージョン付きで表示する
- レビュー済み HTML の SHA256 検証で信頼チェーンを維持する

#### サブプロセスセキュリティ

- 逆アセンブラ候補はハードコードされたコマンドであり、任意コマンドではない
- 更新プロンプトは shell を介さず、固定の `dotnet tool update --global nildiff` 実行ファイルと引数だけを起動する
- 外部ツールパスは `PATH` または設定から解決する
- 各逆アセンブラ呼び出しにはタイムアウトがある
- 非 ASCII パスは一時的な ASCII セーフコピーで扱う

#### 設定セキュリティ

- 環境変数オーバーライドは `FOLDERDIFF_*` に限定する
- 設定は起動時に検証する
- ツールはシークレットを保存も要求もしない

#### 更新確認のネットワークセキュリティ

- 更新確認フェーズでは stdin・stdout・stderr のすべてがリダイレクトされていないことを必須とし、パイプ入力の消費、自動処理の入力待ち、リダイレクト出力への更新文言混入を防ぐ。OS 別の例と利用者向けの挙動は[ユーザーガイド](USER_GUIDE.md#guide-ja-startup-update-notification)を参照
- 固定の HTTPS nuget.org registration metadata を要求し、registration page のリンクは `https://api.nuget.org` 上に留まる場合だけ追跡する
- 比較対象パス、レポートデータ、設定、認証情報は送信しない
- 更新処理は `1. Update now` の明示選択時だけ実行し、`2. Skip` と `3. Skip until next version` ではプロセスを起動しない。3 でユーザーローカルキャッシュに保存するのは、通知対象外にした公開パッケージ版だけ
- ネットワーク、レスポンス、キャッシュ、更新処理の失敗は内部で処理し、要求されたコマンドを失敗させない

#### 既知の制限事項

| 制限 | 理由 |
|---|---|
| CSP に `unsafe-inline` を使う | 自己完結型 HTML を維持するため |
| ファイルパスがディレクトリ構造を露出する | フォルダ比較の目的上不可避 |
| IL 出力には機微なコードが含まれうる | IL レベル比較に必要 |
