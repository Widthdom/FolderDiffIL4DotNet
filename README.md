# FolderDiffIL4DotNet

`FolderDiffIL4DotNet` is a .NET console application that compares two folders and outputs a Markdown report.
For .NET assemblies, it compares IL while ignoring build-specific information such as `// MVID:` lines, so assemblies whose contents are effectively the same can still be judged equal.

Developer-focused details (architecture, CI, tests, implementation cautions):
- [doc/DEVELOPER_GUIDE.md](doc/DEVELOPER_GUIDE.md)

## Documentation Map

| Need | Document |
| --- | --- |
| Product overview, setup, usage, and configuration | [README.md](README.md) |
| Runtime architecture, execution flow, DI scopes, and implementation guardrails | [doc/DEVELOPER_GUIDE.md](doc/DEVELOPER_GUIDE.md) |
| Test strategy, local test commands, coverage, and isolation rules | [doc/TESTING_GUIDE.md](doc/TESTING_GUIDE.md) |
| Generated API reference from XML documentation comments | [api/index.md](api/index.md) via [docfx.json](docfx.json) |

## Requirements

- [.NET SDK 8.x](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- macOS / Windows / Linux / Unix-like OS
- IL disassembler (auto-probed per file)
  - Preferred: [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) or [`dotnet ildasm`](https://www.nuget.org/packages/dotnet-ildasm/)
  - Fallback: [`ilspycmd`](https://www.nuget.org/packages/ilspycmd/)

[.NET SDK 8.x](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installation examples:

```powershell
# Windows (winget)
winget install Microsoft.DotNet.SDK.8 --source winget
```

```powershell
# Windows (dotnet-install script)
powershell -ExecutionPolicy Bypass -c "& { iwr https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1; .\dotnet-install.ps1 -Channel 8.0 }"
```

```bash
# macOS/Linux/Unix (dotnet-install script)
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0
```

IL disassembler installation examples:

```bash
dotnet tool install --global dotnet-ildasm
# add $HOME/.dotnet/tools (macOS/Linux/Unix) or %USERPROFILE%\.dotnet\tools (Windows) to PATH if needed

# verify installation and version (both commands invoke the same dotnet-ildasm tool)
dotnet-ildasm --version
dotnet ildasm --version
```

```bash
dotnet tool install --global ilspycmd
# add $HOME/.dotnet/tools (macOS/Linux/Unix) or %USERPROFILE%\.dotnet\tools (Windows) to PATH if needed
```

## Usage

1. Place [`config.json`](config.json) next to the executable.
2. Run with arguments:
- old folder absolute path
- new folder absolute path
- report label
3. Add `--no-pause` if you want to skip key-wait at process end.

```bash
dotnet build
dotnet run "/Users/UserA/workspace/old" "/Users/UserA/workspace/new" "YYYYMMDD" --no-pause
```

Main output:
- `Reports/<label>/diff_report.md`
- Optional IL dumps under `Reports/<label>/IL/old` and `Reports/<label>/IL/new` when `ShouldOutputILText=true`

Example `diff_report.md` (trimmed):

```md
# Folder Diff Report
- App Version: FolderDiffIL4DotNet 1.0.0
- Computer: dev-machine
- Old: /Users/UserA/workspace/old
- New: /Users/UserA/workspace/new
- Ignored Extensions: .cache, .DS_Store, .db, .ilcache, .log, .pdb
- Text File Extensions: .asax, .ascx, .asmx, .aspx, .bat, .c, .cmd, .config, .cpp, .cs, .cshtml, .csproj, .csx, .css, .csv, .editorconfig, .env, .fs, .fsi, .fsproj, .fsx, .gitattributes, .gitignore, .gitmodules, .go, .gql, .graphql, .h, .hpp, .htm, .html, .http, .ini, .js, .json, .jsx, .less, .manifest, .md, .mod, .nlog, .nuspec, .plist, .props, .ps1, .psd1, .psm1, .py, .razor, .resx, .rst, .sass, .scss, .sh, .sln, .sql, .sqlproj, .sum, .svg, .targets, .toml, .ts, .tsv, .tsx, .txt, .vb, .vbproj, .vue, .xaml, .xml, .yaml, .yml
- IL Disassembler: dotnet-ildasm (version: 0.12.2)
- Elapsed Time: 00:00:01.234
- Note: When diffing IL, lines starting with "// MVID:" (if present) are ignored because they contain disassembler-emitted Module Version ID metadata that can change on rebuild without meaning the executable IL changed.
- Note: When diffing IL, lines containing any of the configured strings are ignored: "buildserver1_", "buildserver2_".
- Legend:
  - `MD5Match` / `MD5Mismatch`: MD5 hash match / mismatch
  - `ILMatch` / `ILMismatch`: IL(Intermediate Language) match / mismatch
  - `TextMatch` / `TextMismatch`: Text match / mismatch

## [ x ] Ignored Files
- [ x ] bin/MyApp.pdb (old/new) <u>(updated_old: 2026-03-15 08:57:00.000 +09:00, updated_new: 2026-03-15 09:03:00.000 +09:00)</u>

## [ = ] Unchanged Files
- [ = ] appsettings.json <u>(updated: 2026-03-15 09:00:00.000 +09:00)</u> `TextMatch`

## [ + ] Added Files
- [ + ] /Users/UserA/workspace/new/docs/guide.md <u>(updated: 2026-03-15 09:01:00.000 +09:00)</u>

## [ - ] Removed Files
- [ - ] /Users/UserA/workspace/old/legacy/old-tool.txt <u>(updated: 2026-03-15 08:55:00.000 +09:00)</u>

## [ * ] Modified Files
- [ * ] src/MyApp.dll <u>(updated_old: 2026-03-15 08:58:00.000 +09:00, updated_new: 2026-03-15 09:02:00.000 +09:00)</u> `ILMismatch` `dotnet-ildasm (version: 0.12.2)`
- [ * ] payload.bin <u>(updated_old: 2026-03-15 08:59:00.000 +09:00, updated_new: 2026-03-15 08:54:00.000 +09:00)</u> `MD5Mismatch`

## Summary
- Ignored   : 1
- Unchanged : 1
- Added     : 1
- Removed   : 1
- Modified  : 2
- Compared  : 5 (Old) vs 5 (New)

## Warnings
- **WARNING:** One or more files were classified as `MD5Mismatch`. Manual review is recommended because only an MD5 hash comparison was possible.
- **WARNING:** One or more files in `new` have older last-modified timestamps than the corresponding files in `old`.
  - payload.bin (updated_old: 2026-03-15 08:59:00.000 +09:00, updated_new: 2026-03-15 08:54:00.000 +09:00)
```

## Runtime Composition

- [`Program.cs`](Program.cs) is intentionally thin and only resolves [`ProgramRunner`](ProgramRunner.cs).
- [`ProgramRunner`](ProgramRunner.cs) validates arguments, loads [`config.json`](config.json), builds a per-run DI container, and executes the diff/report pipeline.
- [`ProgramRunner`](ProgramRunner.cs) also owns aggregated end-of-run console warnings such as `MD5Mismatch` and timestamp-regression notices.
- [`DiffExecutionContext`](Services/DiffExecutionContext.cs) carries run-specific paths and network-mode decisions.
- [`FolderDiffService`](Services/FolderDiffService.cs) uses [`IFileSystemService`](Services/IFileSystemService.cs) for discovery/output I/O and [`FileDiffService`](Services/FileDiffService.cs) uses [`IFileComparisonService`](Services/IFileComparisonService.cs) for hash, text, and chunk-read operations, which keeps permission and disk-failure paths unit-testable without changing runtime behavior.
- Core pipeline services ([`FolderDiffService`](Services/FolderDiffService.cs), [`FileDiffService`](Services/FileDiffService.cs), [`ILOutputService`](Services/ILOutputService.cs)) depend on interfaces and injected context rather than static fields or `ActivatorUtilities.CreateInstance`, which keeps behavior stable while improving test substitution.

## Comparison Flow

At a high level, the tool first matches files by relative path, then decides whether each matched pair is effectively the same.

```mermaid
flowchart TD
    A["Start folder diff"] --> B["List files under old and new"]
    B --> C{"Same relative path exists on both sides?"}
    C -- "No, only old" --> D["Classify as Removed"]
    C -- "No, only new" --> E["Classify as Added"]
    C -- "Yes" --> F["Compare the matched pair"]
    F --> G{"Effectively the same?"}
    G -- "Yes" --> H["Classify as Unchanged"]
    G -- "No" --> I["Classify as Modified"]
```

For one matched pair, the decision order is:

1. Try an exact byte-level match with MD5.
2. If MD5 differs and the old-side file is a .NET executable, compare filtered IL instead of raw bytes.
3. If it is not in the IL path and the extension is listed in `TextFileExtensions`, compare it as text.
4. If none of the checks say "same", treat it as a normal mismatch.

Important details:
- `Added`, `Removed`, `Unchanged`, and `Modified` are decided by relative path, not by file name alone.
- IL comparison always ignores `// MVID:` lines, so build-specific assembly noise does not create false differences.
- If `ShouldIgnoreILLinesContainingConfiguredStrings=true`, lines containing any configured ignore string are also skipped during IL comparison.
- Text files may use different internal strategies depending on size and runtime mode. If chunk-parallel comparison for a large local file throws, the run logs a warning and retries with sequential text comparison.
- If IL comparison itself fails, the run stops instead of silently falling back to a weaker comparison.

## Configuration ([`config.json`](config.json))

Place [`config.json`](config.json) next to the executable. All keys are optional; omitted keys use the code-defined defaults in [`ConfigSettings`](Models/ConfigSettings.cs). If the defaults are acceptable, this file can be just:

```json
{}
```

Override only the settings you want to change. For example:

```json
{
  "ShouldIgnoreILLinesContainingConfiguredStrings": true,
  "ILIgnoreLineContainingStrings": ["buildserver1_", "buildserver2_"],
  "ShouldOutputFileTimestamps": false,
  "ShouldOutputILText": false,
  "ShouldIncludeIgnoredFiles": false
}
```

| Key | Default | Description |
| --- | --- | --- |
| `IgnoredExtensions` | `.cache`, `.DS_Store`, `.db`, `.ilcache`, `.log`, `.pdb` | Excludes matching extensions from comparison. |
| `TextFileExtensions` | Built-in extension list in [`ConfigSettings`](Models/ConfigSettings.cs) | Treats matching extensions as text. Include dot (`.cs`, `.json`). Matching is case-insensitive. |
| `MaxLogGenerations` | `5` | Number of log files kept in rotation. |
| `ShouldIncludeUnchangedFiles` | `true` | Includes `Unchanged` section in report. |
| `ShouldIncludeIgnoredFiles` | `true` | Includes `Ignored Files` section before `Unchanged`. |
| `ShouldOutputILText` | `true` | Outputs IL dumps under `Reports/<label>/IL/old,new`. |
| `ShouldIgnoreILLinesContainingConfiguredStrings` | `false` | Enables additional IL line-ignore filter by substring. |
| `ILIgnoreLineContainingStrings` | `[]` | String list used by IL substring-ignore filter. |
| `ShouldOutputFileTimestamps` | `true` | Adds last-modified timestamps to report entries. |
| `ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp` | `true` | Warns if a file in `new` has an older last-modified timestamp than the matching file in `old`, prints the warning at the end of the run, and appends a final `Warnings` section to `diff_report.md`. |
| `MaxParallelism` | `0` | Max compare parallelism. `0` or less = auto. |
| `TextDiffParallelThresholdKilobytes` | `512` | Text diff size threshold (KiB) for chunk-parallel mode. |
| `TextDiffChunkSizeKilobytes` | `64` | Chunk size (KiB) for parallel text diff. |
| `EnableILCache` | `true` | Enables IL cache (memory + optional disk). |
| `ILCacheDirectoryAbsolutePath` | `""` | IL cache directory. Empty = `<exe>/ILCache`. |
| `ILCacheStatsLogIntervalSeconds` | `60` | IL cache stats log interval. `<=0` uses default 60s. |
| `ILCacheMaxDiskFileCount` | `1000` | Disk cache file count cap. `<=0` means unlimited. |
| `ILCacheMaxDiskMegabytes` | `512` | Disk cache size cap (MB). `<=0` means unlimited. |
| `OptimizeForNetworkShares` | `false` | Enables network-share optimization mode. |
| `AutoDetectNetworkShares` | `true` | Auto-detects network paths and enables optimization mode as needed. |

Notes:
- Built-in defaults, including the full `IgnoredExtensions` and `TextFileExtensions` lists, are defined in [`Models/ConfigSettings.cs`](Models/ConfigSettings.cs).
- Files without extension are still compared.
- If you want extensionless files treated as text, include empty string (`""`) in `TextFileExtensions`.
- Timestamp-regression warnings are evaluated only for files that exist in both `old` and `new`.
- If any file ends as `MD5Mismatch`, the report writes that warning in the final `Warnings` section before any timestamp-regression entries, and the same message is printed once at run completion.

## Generated Artifacts

- `Reports/<label>/diff_report.md`
- `Logs/log_YYYYMMDD.log`
- Optional: `Reports/<label>/IL/old/*.txt`, `Reports/<label>/IL/new/*.txt`

After writing, report/IL files are set to read-only when possible (failures are warning-only).

## API Documentation

API reference pages are generated with DocFX from the XML documentation comments already maintained in the source code.

Local refresh:

```bash
dotnet build FolderDiffIL4DotNet.sln --configuration Release
dotnet tool update --global docfx --version '2.*'
export PATH="$PATH:$HOME/.dotnet/tools"
docfx metadata docfx.json
docfx build docfx.json
```

Generated outputs:
- Site root: `_site/index.html`
- API metadata intermediate files: `api/*.yml`

CI also generates the same site and uploads it as the `DocumentationSite` artifact.

## License

- [MIT License](LICENSE)

---

# FolderDiffIL4DotNet（日本語）

`FolderDiffIL4DotNet` は、2つのフォルダを比較して Markdown レポートを出力する .NET コンソールアプリです。
.NET アセンブリは `// MVID:` などのビルド固有情報を除外して IL 比較することで、アセンブリの中身が実質的に同じであれば同一と判断します。

開発者向けの詳細（設計、CI、テスト、実装上の注意点）は以下に分離しました。
- [doc/DEVELOPER_GUIDE.md](doc/DEVELOPER_GUIDE.md)

## ドキュメントの見取り図

| 見たい内容 | ドキュメント |
| --- | --- |
| 製品概要、導入、使い方、設定 | [README.md](README.md) |
| 実行時アーキテクチャ、実行フロー、DI スコープ、実装上の注意点 | [doc/DEVELOPER_GUIDE.md](doc/DEVELOPER_GUIDE.md) |
| テスト戦略、ローカル実行コマンド、カバレッジ、分離ルール | [doc/TESTING_GUIDE.md](doc/TESTING_GUIDE.md) |
| XML ドキュメントコメントから生成する API リファレンス | [api/index.md](api/index.md) と [docfx.json](docfx.json) |

## 必要環境

- [.NET SDK 8.x](https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0)
- macOS / Windows / Linux / Unix 系 OS
- IL 逆アセンブラ（ファイルごとに自動判定）
  - 優先: [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) または [`dotnet ildasm`](https://www.nuget.org/packages/dotnet-ildasm/)
  - 代替: [`ilspycmd`](https://www.nuget.org/packages/ilspycmd/)

[.NET SDK 8.x](https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0) のインストール例:

```powershell
# Windows (winget)
winget install Microsoft.DotNet.SDK.8 --source winget
```

```powershell
# Windows (dotnet-install スクリプト)
powershell -ExecutionPolicy Bypass -c "& { iwr https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1; .\dotnet-install.ps1 -Channel 8.0 }"
```

```bash
# macOS/Linux/Unix (dotnet-install スクリプト)
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0
```

IL 逆アセンブラのインストール例:

```bash
dotnet tool install --global dotnet-ildasm
# 必要に応じて PATH へ追加
# macOS/Linux/Unix: $HOME/.dotnet/tools
# Windows: %USERPROFILE%\.dotnet\tools

# インストール確認とバージョン確認（どちらも同じ dotnet-ildasm を実行）
dotnet-ildasm --version
dotnet ildasm --version
```

```bash
dotnet tool install --global ilspycmd
# 必要に応じて PATH へ追加
# macOS/Linux/Unix: $HOME/.dotnet/tools
# Windows: %USERPROFILE%\.dotnet\tools
```

## 使い方

1. 実行ファイルと同じ場所に [`config.json`](config.json) を配置します。
2. 次の引数で実行します。
- 旧フォルダ（比較元）の絶対パス
- 新フォルダ（比較先）の絶対パス
- レポートラベル
3. 終了時のキー待ちを省略する場合は `--no-pause` を付けます。

```bash
dotnet build
dotnet run "/Users/UserA/workspace/old" "/Users/UserA/workspace/new" "YYYYMMDD" --no-pause
```

主な出力:
- `Reports/<label>/diff_report.md`
- `ShouldOutputILText=true` の場合は `Reports/<label>/IL/old` と `Reports/<label>/IL/new` に IL テキスト

`diff_report.md` の簡単な例:

```md
# Folder Diff Report
- App Version: FolderDiffIL4DotNet 1.0.0
- Computer: dev-machine
- Old: /Users/UserA/workspace/old
- New: /Users/UserA/workspace/new
- Ignored Extensions: .cache, .DS_Store, .db, .ilcache, .log, .pdb
- Text File Extensions: .asax, .ascx, .asmx, .aspx, .bat, .c, .cmd, .config, .cpp, .cs, .cshtml, .csproj, .csx, .css, .csv, .editorconfig, .env, .fs, .fsi, .fsproj, .fsx, .gitattributes, .gitignore, .gitmodules, .go, .gql, .graphql, .h, .hpp, .htm, .html, .http, .ini, .js, .json, .jsx, .less, .manifest, .md, .mod, .nlog, .nuspec, .plist, .props, .ps1, .psd1, .psm1, .py, .razor, .resx, .rst, .sass, .scss, .sh, .sln, .sql, .sqlproj, .sum, .svg, .targets, .toml, .ts, .tsv, .tsx, .txt, .vb, .vbproj, .vue, .xaml, .xml, .yaml, .yml
- IL Disassembler: dotnet-ildasm (version: 0.12.2)
- Elapsed Time: 00:00:01.234
- Note: When diffing IL, lines starting with "// MVID:" (if present) are ignored because they contain disassembler-emitted Module Version ID metadata that can change on rebuild without meaning the executable IL changed.
- Note: When diffing IL, lines containing any of the configured strings are ignored: "buildserver1_", "buildserver2_".
- Legend:
  - `MD5Match` / `MD5Mismatch`: MD5 hash match / mismatch
  - `ILMatch` / `ILMismatch`: IL(Intermediate Language) match / mismatch
  - `TextMatch` / `TextMismatch`: Text match / mismatch

## [ x ] Ignored Files
- [ x ] bin/MyApp.pdb (old/new) <u>(updated_old: 2026-03-15 08:57:00.000 +09:00, updated_new: 2026-03-15 09:03:00.000 +09:00)</u>

## [ = ] Unchanged Files
- [ = ] appsettings.json <u>(updated: 2026-03-15 09:00:00.000 +09:00)</u> `TextMatch`

## [ + ] Added Files
- [ + ] /Users/UserA/workspace/new/docs/guide.md <u>(updated: 2026-03-15 09:01:00.000 +09:00)</u>

## [ - ] Removed Files
- [ - ] /Users/UserA/workspace/old/legacy/old-tool.txt <u>(updated: 2026-03-15 08:55:00.000 +09:00)</u>

## [ * ] Modified Files
- [ * ] src/MyApp.dll <u>(updated_old: 2026-03-15 08:58:00.000 +09:00, updated_new: 2026-03-15 09:02:00.000 +09:00)</u> `ILMismatch` `dotnet-ildasm (version: 0.12.2)`
- [ * ] payload.bin <u>(updated_old: 2026-03-15 08:59:00.000 +09:00, updated_new: 2026-03-15 08:54:00.000 +09:00)</u> `MD5Mismatch`

## Summary
- Ignored   : 1
- Unchanged : 1
- Added     : 1
- Removed   : 1
- Modified  : 2
- Compared  : 5 (Old) vs 5 (New)

## Warnings
- **WARNING:** One or more files were classified as `MD5Mismatch`. Manual review is recommended because only an MD5 hash comparison was possible.
- **WARNING:** One or more files in `new` have older last-modified timestamps than the corresponding files in `old`.
  - payload.bin (updated_old: 2026-03-15 08:59:00.000 +09:00, updated_new: 2026-03-15 08:54:00.000 +09:00)
```

## 実行時構成

- [`Program.cs`](Program.cs) は薄いエントリーポイントで、[`ProgramRunner`](ProgramRunner.cs) の解決だけを行います。
- [`ProgramRunner`](ProgramRunner.cs) が引数検証、[`config.json`](config.json) 読込、実行単位 DI コンテナ生成、差分/レポート処理の実行を担います。
- [`ProgramRunner`](ProgramRunner.cs) は `MD5Mismatch` や更新日時逆転のような集約後の終了時コンソール警告も担当します。
- [`DiffExecutionContext`](Services/DiffExecutionContext.cs) が実行ごとのパスやネットワークモード判定を保持します。
- [`FolderDiffService`](Services/FolderDiffService.cs) は列挙/出力系 I/O を [`IFileSystemService`](Services/IFileSystemService.cs)、[`FileDiffService`](Services/FileDiffService.cs) はハッシュ/テキスト/チャンク読み出し系 I/O を [`IFileComparisonService`](Services/IFileComparisonService.cs) に委譲しており、権限エラーやディスク系失敗の経路も実ファイルなしでユニットテストできます。
- 主要パイプラインサービス（[`FolderDiffService`](Services/FolderDiffService.cs), [`FileDiffService`](Services/FileDiffService.cs), [`ILOutputService`](Services/ILOutputService.cs)）は、静的フィールドや `ActivatorUtilities.CreateInstance` ではなく、インターフェースとコンテキスト注入に依存します。これにより既存動作を維持したままテスト差し替え性を高めています。

## 比較フロー

大まかには、まず相対パスでファイルを突き合わせてから、両側に存在するファイルが「実質同じか」を判定します。

```mermaid
flowchart TD
    A["開始: フォルダ比較"] --> B["old/new のファイルを列挙"]
    B --> C{"両側に同じ相対パスがある?"}
    C -- "いいえ、old のみ" --> D["Removed に分類"]
    C -- "いいえ、new のみ" --> E["Added に分類"]
    C -- "はい" --> F["その 1 組を比較"]
    F --> G{"実質同じ?"}
    G -- "はい" --> H["Unchanged に分類"]
    G -- "いいえ" --> I["Modified に分類"]
```

同じ相対パスの 1 組に対しては、次の順番で判定します。

1. まず MD5 で完全一致かを確認します。
2. MD5 が不一致で、old 側ファイルが .NET 実行可能なら、バイト列ではなく IL を比較します。
3. IL 経路に入らず、拡張子が `TextFileExtensions` に含まれるなら、テキストとして比較します。
4. どの比較でも「同じ」と言えなければ、通常の不一致として扱います。

重要な点:
- `Added` / `Removed` / `Unchanged` / `Modified` は、ファイル名だけでなく相対パスを基準に決まります。
- `ShouldIgnoreILLinesContainingConfiguredStrings=true` の場合は、設定した文字列を含む行も IL 比較から除外します。
- テキスト比較の内部実装はファイルサイズや実行モードで変わることがあります。大きいローカルファイルの並列比較で例外が出た場合は warning を記録し、逐次比較へフォールバックします。
- IL 比較そのものに失敗した場合は、弱い比較へ黙って落とさず、その実行全体を停止します。

## 設定（[`config.json`](config.json)）

実行ファイルと同じディレクトリに配置します。全項目省略可能で、未指定の項目は [`ConfigSettings`](Models/ConfigSettings.cs) に定義されたコード既定値を使います。既定値のままでよければ、次のように空オブジェクトだけで構いません。

```json
{}
```

変更したい項目だけを書けば十分です。例:

```json
{
  "ShouldIgnoreILLinesContainingConfiguredStrings": true,
  "ILIgnoreLineContainingStrings": ["buildserver1_", "buildserver2_"],
  "ShouldOutputFileTimestamps": false,
  "ShouldOutputILText": false,
  "ShouldIncludeIgnoredFiles": false
}
```

| 項目 | 既定値 | 説明 |
| --- | --- | --- |
| `IgnoredExtensions` | `.cache`, `.DS_Store`, `.db`, `.ilcache`, `.log`, `.pdb` | 指定拡張子を比較対象から除外します。 |
| `TextFileExtensions` | [`ConfigSettings`](Models/ConfigSettings.cs) 内の組み込み拡張子一覧 | 指定拡張子をテキスト比較対象にします（`.` 付き指定、大小無視）。 |
| `MaxLogGenerations` | `5` | ログローテーション世代数。 |
| `ShouldIncludeUnchangedFiles` | `true` | レポートに `Unchanged` セクションを出力するか。 |
| `ShouldIncludeIgnoredFiles` | `true` | レポートに `Ignored Files` セクションを出力するか。 |
| `ShouldOutputILText` | `true` | `Reports/<label>/IL/old,new` へ IL を出力するか。 |
| `ShouldIgnoreILLinesContainingConfiguredStrings` | `false` | IL 比較時の追加行除外（部分一致）を有効化するか。 |
| `ILIgnoreLineContainingStrings` | `[]` | IL 行除外に使う文字列一覧。 |
| `ShouldOutputFileTimestamps` | `true` | レポート各行に更新日時を併記するか。 |
| `ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp` | `true` | `new` 側の更新日時が対応する `old` 側より古いファイルを検出し、実行終了時のコンソールと `diff_report.md` 末尾の `Warnings` セクションへ一覧を出力します。 |
| `MaxParallelism` | `0` | 比較の最大並列度。`0` 以下は自動。 |
| `TextDiffParallelThresholdKilobytes` | `512` | 並列テキスト比較へ切替える閾値（KiB）。 |
| `TextDiffChunkSizeKilobytes` | `64` | 並列テキスト比較のチャンクサイズ（KiB）。 |
| `EnableILCache` | `true` | IL キャッシュ（メモリ + 任意ディスク）を有効化するか。 |
| `ILCacheDirectoryAbsolutePath` | `""` | IL キャッシュディレクトリ。空なら `<exe>/ILCache`。 |
| `ILCacheStatsLogIntervalSeconds` | `60` | IL キャッシュ統計ログ間隔。`<=0` で既定 60 秒。 |
| `ILCacheMaxDiskFileCount` | `1000` | ディスクキャッシュ最大ファイル数。`<=0` で無制限。 |
| `ILCacheMaxDiskMegabytes` | `512` | ディスクキャッシュ容量上限（MB）。`<=0` で無制限。 |
| `OptimizeForNetworkShares` | `false` | ネットワーク共有向け最適化モードを有効化。 |
| `AutoDetectNetworkShares` | `true` | ネットワーク共有を自動検出して最適化モードを必要時に有効化。 |

補足:
- `IgnoredExtensions` と `TextFileExtensions` を含む組み込み既定値の全体は [`Models/ConfigSettings.cs`](Models/ConfigSettings.cs) に定義しています。
- 拡張子なしファイルも比較対象です。
- 拡張子なしファイルをテキスト扱いしたい場合は `TextFileExtensions` に空文字（`""`）を含めてください。
- 更新日時逆転の警告は、`old` と `new` の両方に存在する同一相対パスのファイルだけを対象に判定します。
- `MD5Mismatch` が1件でもある場合、その警告はレポート末尾の `Warnings` セクションで更新日時逆転警告より先に出し、同じ文言を実行終了時のコンソールにも1回だけ出力します。

## 生成物

- `Reports/<label>/diff_report.md`
- `Logs/log_YYYYMMDD.log`
- 任意: `Reports/<label>/IL/old/*.txt`, `Reports/<label>/IL/new/*.txt`

レポート/IL 出力ファイルは可能な範囲で読み取り専用化されます（失敗時は警告のみ）。

## API ドキュメント

API リファレンスは、ソースコード内で維持している XML ドキュメントコメントを DocFX で収集して生成します。

ローカル更新手順:

```bash
dotnet build FolderDiffIL4DotNet.sln --configuration Release
dotnet tool update --global docfx --version '2.*'
export PATH="$PATH:$HOME/.dotnet/tools"
docfx metadata docfx.json
docfx build docfx.json
```

生成物:
- サイト本体: `_site/index.html`
- API メタデータ中間生成物: `api/*.yml`

CI でも同じサイトを生成し、`DocumentationSite` artifact としてアップロードします。

## ライセンス

- [MIT License](LICENSE)
