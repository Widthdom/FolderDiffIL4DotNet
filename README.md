# FolderDiffIL4DotNet

`FolderDiffIL4DotNet` is a .NET console application that compares two folders and outputs a Markdown report.
For .NET assemblies, it compares IL while ignoring build-specific information such as `// MVID:` lines, so assemblies whose contents are effectively the same can still be judged equal.

Developer-focused details (architecture, CI, tests, implementation cautions):
- [doc/DEVELOPER_GUIDE.md](doc/DEVELOPER_GUIDE.md)

<a id="readme-en-doc-map"></a>
## Documentation Map

| Need | Document |
| --- | --- |
| Product overview, setup, usage, and configuration | [README.md](README.md#readme-en-usage) |
| Runtime architecture, execution flow, DI scopes, and implementation guardrails | [doc/DEVELOPER_GUIDE.md](doc/DEVELOPER_GUIDE.md#guide-en-map) |
| Test strategy, local test commands, coverage, and isolation rules | [doc/TESTING_GUIDE.md](doc/TESTING_GUIDE.md#testing-en-run-tests) |
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

<a id="readme-en-usage"></a>
## Usage

```
FolderDiffIL4DotNet <oldFolder> <newFolder> <reportLabel> [options]
```

**Arguments:**

| Argument | Description |
|---|---|
| `<oldFolder>` | Absolute path to the baseline (old) folder. |
| `<newFolder>` | Absolute path to the comparison (new) folder. |
| `<reportLabel>` | Label used as the subfolder name under `Reports/`. |

**Options:**

| Option | Description |
|---|---|
| `--help`, `-h` | Show help and exit (code `0`). |
| `--version` | Show the application version and exit (code `0`). |
| `--no-pause` | Skip key-wait at process end. |
| `--config <path>` | Load config from `<path>` instead of the default `<exe>/config.json`. |
| `--threads <N>` | Override `MaxParallelism` for this run (`0` = auto). |
| `--no-il-cache` | Disable the IL cache for this run. |
| `--skip-il` | Skip IL comparison for .NET assemblies entirely. |
| `--no-timestamp-warnings` | Suppress timestamp-regression warnings. |

```bash
dotnet build
dotnet run "/Users/UserA/workspace/old" "/Users/UserA/workspace/new" "YYYYMMDD" --no-pause

# Override threads and skip IL for a quick diff
dotnet run "/path/old" "/path/new" "label" --threads 4 --skip-il --no-pause

# Use a custom config file
dotnet run "/path/old" "/path/new" "label" --config /etc/my-config.json --no-pause
```

Main output:
- `Reports/<label>/diff_report.md`
- Optional IL dumps under `Reports/<label>/IL/old` and `Reports/<label>/IL/new` when [`ShouldOutputILText`](#configuration-table-en) is `true`

Process exit codes:
- `0`: success
- `2`: invalid arguments or input paths (includes preflight failures — see below)
- `3`: configuration load/parse error
- `4`: diff execution or report generation failure
- `1`: unexpected internal error

Before loading configuration, three preflight checks run against the reports output path (all failures produce exit code `2`):
1. **Path length** — the constructed `Reports/<label>` path must not exceed the OS limit (260 chars on Windows without long-path opt-in, 1024 on macOS, 4096 on Linux).
2. **Disk space** — at least 100 MB of free space is required on the drive that will hold the reports folder. The check is best-effort and skips silently when drive information is unavailable (e.g., network shares).
3. **Write permission** — a temporary probe file is created and deleted in the `Reports/` parent directory to verify that the process has write access before any actual output is produced.

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
- Elapsed Time: 0h 0m 1.2s
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

## IL Cache Stats
- Hits    : 42
- Misses  : 8
- Hit Rate: 84.0%
- Stores  : 8
- Evicted : 0
- Expired : 0

## Warnings
- **WARNING:** One or more files were classified as `MD5Mismatch`. Manual review is recommended because only an MD5 hash comparison was possible.
- **WARNING:** One or more files in `new` have older last-modified timestamps than the corresponding files in `old`.
  - payload.bin (updated_old: 2026-03-15 08:59:00.000 +09:00, updated_new: 2026-03-15 08:54:00.000 +09:00)
```

<a id="readme-en-comparison-flow"></a>
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
3. If it is not in the IL path and the extension is listed in [`TextFileExtensions`](#configuration-table-en), compare it as text.
4. If none of the checks say "same", treat it as a normal mismatch.

Important details:
- `Added`, `Removed`, `Unchanged`, and `Modified` are decided by relative path, not by file name alone.
- IL comparison always ignores `// MVID:` lines, so build-specific assembly noise does not create false differences.
- If [`ShouldIgnoreILLinesContainingConfiguredStrings`](#configuration-table-en) is `true`, lines containing any configured ignore string are also skipped during IL comparison.
- Warm-up, cache cleanup, and post-write read-only protection are best-effort paths that log warnings and continue. Folder enumeration, matched-pair comparison, and report writing log and rethrow expected runtime exceptions because they affect correctness or required output.
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
  "ShouldIncludeIgnoredFiles": false,
  "ShouldIncludeILCacheStatsInReport": true
}
```

### Configuration Table EN

<table>
  <thead>
    <tr>
      <th>Key</th>
      <th>Default</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr id="config-en-ignoredextensions">
      <td><code>IgnoredExtensions</code></td>
      <td><code>.cache</code>, <code>.DS_Store</code>, <code>.db</code>, <code>.ilcache</code>, <code>.log</code>, <code>.pdb</code></td>
      <td>Excludes matching extensions from comparison.</td>
    </tr>
    <tr id="config-en-textfileextensions">
      <td><code>TextFileExtensions</code></td>
      <td>Built-in extension list in <a href="Models/ConfigSettings.cs"><code>ConfigSettings</code></a></td>
      <td>Treats matching extensions as text. Include dot (<code>.cs</code>, <code>.json</code>). Matching is case-insensitive.</td>
    </tr>
    <tr id="config-en-maxloggenerations">
      <td><code>MaxLogGenerations</code></td>
      <td><code>5</code></td>
      <td>Number of log files kept in rotation.</td>
    </tr>
    <tr id="config-en-shouldincludeunchangedfiles">
      <td><code>ShouldIncludeUnchangedFiles</code></td>
      <td><code>true</code></td>
      <td>Includes <code>Unchanged</code> section in report.</td>
    </tr>
    <tr id="config-en-shouldincludeignoredfiles">
      <td><code>ShouldIncludeIgnoredFiles</code></td>
      <td><code>true</code></td>
      <td>Includes <code>Ignored Files</code> section before <code>Unchanged</code>.</td>
    </tr>
    <tr id="config-en-shouldincludeilcachestatsInreport">
      <td><code>ShouldIncludeILCacheStatsInReport</code></td>
      <td><code>false</code></td>
      <td>When <code>true</code>, appends an <code>IL Cache Stats</code> section (hits, misses, hit-rate, stores, evicted, expired) between <code>Summary</code> and <code>Warnings</code>. Has no effect when <code>EnableILCache</code> is <code>false</code>.</td>
    </tr>
    <tr id="config-en-shouldoutputiltext">
      <td><code>ShouldOutputILText</code></td>
      <td><code>true</code></td>
      <td>Outputs IL dumps under <code>Reports/&lt;label&gt;/IL/old,new</code>.</td>
    </tr>
    <tr id="config-en-shouldignoreillinescontainingconfiguredstrings">
      <td><code>ShouldIgnoreILLinesContainingConfiguredStrings</code></td>
      <td><code>false</code></td>
      <td>Enables additional IL line-ignore filter by substring.</td>
    </tr>
    <tr id="config-en-ilignorelinecontainingstrings">
      <td><code>ILIgnoreLineContainingStrings</code></td>
      <td><code>[]</code></td>
      <td>String list used by IL substring-ignore filter.</td>
    </tr>
    <tr id="config-en-shouldoutputfiletimestamps">
      <td><code>ShouldOutputFileTimestamps</code></td>
      <td><code>true</code></td>
      <td>Adds last-modified timestamps to report entries.</td>
    </tr>
    <tr id="config-en-shouldwarnwhennewfiletimestampisolderthanoldfiletimestamp">
      <td><code>ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp</code></td>
      <td><code>true</code></td>
      <td>Warns if a file in <code>new</code> has an older last-modified timestamp than the matching file in <code>old</code>, prints the warning at the end of the run, and appends a final <code>Warnings</code> section to <code>diff_report.md</code>.</td>
    </tr>
    <tr id="config-en-maxparallelism">
      <td><code>MaxParallelism</code></td>
      <td><code>0</code></td>
      <td>Max compare parallelism. <code>0</code> or less = auto.</td>
    </tr>
    <tr id="config-en-textdiffparallelthresholdkilobytes">
      <td><code>TextDiffParallelThresholdKilobytes</code></td>
      <td><code>512</code></td>
      <td>Text diff size threshold (KiB) for chunk-parallel mode.</td>
    </tr>
    <tr id="config-en-textdiffchunksizekilobytes">
      <td><code>TextDiffChunkSizeKilobytes</code></td>
      <td><code>64</code></td>
      <td>Chunk size (KiB) for parallel text diff.</td>
    </tr>
    <tr id="config-en-textdiffparallelmemorylimitmegabytes">
      <td><code>TextDiffParallelMemoryLimitMegabytes</code></td>
      <td><code>0</code></td>
      <td>Optional additional buffer budget (MB) for chunk-parallel text diff. <code>&lt;=0</code> means unlimited; otherwise the run reduces worker count or falls back to sequential comparison and logs the current managed-heap size.</td>
    </tr>
    <tr id="config-en-enableilcache">
      <td><code>EnableILCache</code></td>
      <td><code>true</code></td>
      <td>Enables IL cache (memory + optional disk).</td>
    </tr>
    <tr id="config-en-ilcachedirectoryabsolutepath">
      <td><code>ILCacheDirectoryAbsolutePath</code></td>
      <td><code>""</code></td>
      <td>IL cache directory. Empty = <code>&lt;exe&gt;/ILCache</code>.</td>
    </tr>
    <tr id="config-en-ilcachestatslogintervalseconds">
      <td><code>ILCacheStatsLogIntervalSeconds</code></td>
      <td><code>60</code></td>
      <td>IL cache stats log interval. <code>&lt;=0</code> uses default 60s.</td>
    </tr>
    <tr id="config-en-ilcachemaxdiskfilecount">
      <td><code>ILCacheMaxDiskFileCount</code></td>
      <td><code>1000</code></td>
      <td>Disk cache file count cap. <code>&lt;=0</code> means unlimited.</td>
    </tr>
    <tr id="config-en-ilcachemaxdiskmegabytes">
      <td><code>ILCacheMaxDiskMegabytes</code></td>
      <td><code>512</code></td>
      <td>Disk cache size cap (MB). <code>&lt;=0</code> means unlimited.</td>
    </tr>
    <tr id="config-en-ilprecomputebatchsize">
      <td><code>ILPrecomputeBatchSize</code></td>
      <td><code>2048</code></td>
      <td>Batch size for IL-related precompute. <code>&lt;=0</code> uses the default and avoids building one extra all-files list for very large trees.</td>
    </tr>
    <tr id="config-en-optimizefornetworkshares">
      <td><code>OptimizeForNetworkShares</code></td>
      <td><code>false</code></td>
      <td>Enables network-share optimization mode.</td>
    </tr>
    <tr id="config-en-autodetectnetworkshares">
      <td><code>AutoDetectNetworkShares</code></td>
      <td><code>true</code></td>
      <td>Auto-detects network paths and enables optimization mode as needed.</td>
    </tr>
    <tr id="config-en-skipil">
      <td><code>SkipIL</code></td>
      <td><code>false</code></td>
      <td>When <code>true</code>, skips IL decompilation and IL diff for .NET assemblies. MD5-mismatched assemblies are treated as binary diffs. Equivalent to the <code>--skip-il</code> CLI flag.</td>
    </tr>
  </tbody>
</table>

Notes:
- Built-in defaults, including the full [`IgnoredExtensions`](#configuration-table-en) and [`TextFileExtensions`](#configuration-table-en) lists, are defined in [`Models/ConfigSettings.cs`](Models/ConfigSettings.cs).
- Cross-project byte-size and timestamp literals are defined in [`FolderDiffIL4DotNet.Core/Common/CoreConstants.cs`](FolderDiffIL4DotNet.Core/Common/CoreConstants.cs), and app-level constants remain in [`Common/Constants.cs`](Common/Constants.cs), so shared formats do not drift independently across projects.
- After loading `config.json`, settings are validated by [`ConfigSettings.Validate()`](Models/ConfigSettings.cs). If any value is out of range, the run fails immediately with exit code `3` and an error message listing every invalid setting. Validated constraints: `MaxLogGenerations >= 1`; `TextDiffParallelThresholdKilobytes >= 1`; `TextDiffChunkSizeKilobytes >= 1`; and `TextDiffChunkSizeKilobytes` must be less than `TextDiffParallelThresholdKilobytes`.
- Files without extension are still compared.
- If you want extensionless files treated as text, include empty string (`""`) in [`TextFileExtensions`](#configuration-table-en).
- Timestamp-regression warnings are evaluated only for files that exist in both `old` and `new`.
- If any file ends as `MD5Mismatch`, the report writes that warning in the final `Warnings` section before any timestamp-regression entries, and the same message is printed once at run completion.

<a id="readme-en-generated-artifacts"></a>
## Generated Artifacts

- `Reports/<label>/diff_report.md`
- `Logs/log_YYYYMMDD.log`
- Optional: `Reports/<label>/IL/old/*.txt`, `Reports/<label>/IL/new/*.txt`

Report and IL-text creation are treated as required output, so write failures stop the run. After writing, the files are set to read-only when possible, and that protection step remains warning-only.

<a id="readme-en-api-docs"></a>
## API Documentation

API reference pages are generated with DocFX from the XML documentation comments emitted by both [`FolderDiffIL4DotNet.csproj`](FolderDiffIL4DotNet.csproj) and [`FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj`](FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj).

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

<a id="readme-en-ci-automation"></a>
## CI/CD and Security Automation

- [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml) builds, tests, generates DocFX output, uploads artifacts, and already enforces total coverage thresholds of `73%` line and `71%` branch.
- [`.github/workflows/release.yml`](.github/workflows/release.yml) creates a GitHub Release when a `v*` tag is pushed and attaches zipped publish output, zipped documentation, and SHA-256 checksums.
- [`.github/workflows/codeql.yml`](.github/workflows/codeql.yml) runs scheduled and on-change CodeQL analysis for both C# and GitHub Actions workflows.
- [`.github/dependabot.yml`](.github/dependabot.yml) enables weekly update PRs for NuGet packages and GitHub Actions.

## License

- [MIT License](LICENSE)

---

# FolderDiffIL4DotNet（日本語）

`FolderDiffIL4DotNet` は、2つのフォルダを比較して Markdown レポートを出力する .NET コンソールアプリです。
.NET アセンブリは `// MVID:` などのビルド固有情報を除外して IL 比較することで、アセンブリの中身が実質的に同じであれば同一と判断します。

開発者向けの詳細（設計、CI、テスト、実装上の注意点）は以下に分離しました。
- [doc/DEVELOPER_GUIDE.md](doc/DEVELOPER_GUIDE.md)

<a id="readme-ja-doc-map"></a>
## ドキュメントの見取り図

| 見たい内容 | ドキュメント |
| --- | --- |
| 製品概要、導入、使い方、設定 | [README.md](README.md#readme-ja-usage) |
| 実行時アーキテクチャ、実行フロー、DI スコープ、実装上の注意点 | [doc/DEVELOPER_GUIDE.md](doc/DEVELOPER_GUIDE.md#guide-ja-map) |
| テスト戦略、ローカル実行コマンド、カバレッジ、分離ルール | [doc/TESTING_GUIDE.md](doc/TESTING_GUIDE.md#testing-ja-run-tests) |
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

<a id="readme-ja-usage"></a>
## 使い方

```
FolderDiffIL4DotNet <oldFolder> <newFolder> <reportLabel> [options]
```

**引数:**

| 引数 | 説明 |
|---|---|
| `<oldFolder>` | 比較元（旧）フォルダの絶対パス。 |
| `<newFolder>` | 比較先（新）フォルダの絶対パス。 |
| `<reportLabel>` | `Reports/` 配下のサブフォルダ名に使うラベル。 |

**オプション:**

| オプション | 説明 |
|---|---|
| `--help`, `-h` | 使い方を表示してコード `0` で終了します。 |
| `--version` | アプリバージョンを表示してコード `0` で終了します。 |
| `--no-pause` | 終了時のキー待ちをスキップします。 |
| `--config <path>` | デフォルトの `<exe>/config.json` の代わりに `<path>` から設定を読み込みます。 |
| `--threads <N>` | 今回の実行に限り `MaxParallelism` を上書きします（`0` = 自動）。 |
| `--no-il-cache` | 今回の実行に限り IL キャッシュを無効化します。 |
| `--skip-il` | .NET アセンブリの IL 比較をまるごとスキップします。 |
| `--no-timestamp-warnings` | タイムスタンプ逆転警告を抑制します。 |

```bash
dotnet build
dotnet run "/Users/UserA/workspace/old" "/Users/UserA/workspace/new" "YYYYMMDD" --no-pause

# スレッド数指定・IL スキップで高速差分
dotnet run "/path/old" "/path/new" "label" --threads 4 --skip-il --no-pause

# カスタム設定ファイルを指定
dotnet run "/path/old" "/path/new" "label" --config /etc/my-config.json --no-pause
```

主な出力:
- `Reports/<label>/diff_report.md`
- [`ShouldOutputILText`](#configuration-table-ja) が `true` の場合は `Reports/<label>/IL/old` と `Reports/<label>/IL/new` に IL テキスト

プロセス終了コード:
- `0`: 正常終了
- `2`: 引数または入力パス不正（下記プリフライトチェック失敗を含む）
- `3`: 設定ファイルの読込/解析失敗
- `4`: 差分実行またはレポート生成失敗
- `1`: 想定外の内部エラー

設定読み込みの前に、レポート出力パスに対して 3 つのプリフライトチェックを実行します（いずれの失敗も終了コード `2`）:
1. **パス長** — 構築した `Reports/<label>` パスが OS の上限を超えていないこと（Windows 標準は 260 文字、macOS は 1024 文字、Linux は 4096 文字）。
2. **ディスク空き容量** — レポートフォルダを作成するドライブに 100 MB 以上の空き容量があること。ドライブ情報を取得できない場合（ネットワーク共有など）は best-effort でスキップします。
3. **書き込み権限** — `Reports/` 親ディレクトリに一時プローブファイルを作成・削除して、プロセスが書き込み権限を持つことを確認します。

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
- Elapsed Time: 0h 0m 1.2s
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

## IL Cache Stats
- Hits    : 42
- Misses  : 8
- Hit Rate: 84.0%
- Stores  : 8
- Evicted : 0
- Expired : 0

## Warnings
- **WARNING:** One or more files were classified as `MD5Mismatch`. Manual review is recommended because only an MD5 hash comparison was possible.
- **WARNING:** One or more files in `new` have older last-modified timestamps than the corresponding files in `old`.
  - payload.bin (updated_old: 2026-03-15 08:59:00.000 +09:00, updated_new: 2026-03-15 08:54:00.000 +09:00)
```

<a id="readme-ja-comparison-flow"></a>
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
3. IL 経路に入らず、拡張子が [`TextFileExtensions`](#configuration-table-ja) に含まれるなら、テキストとして比較します。
4. どの比較でも「同じ」と言えなければ、通常の不一致として扱います。

重要な点:
- `Added` / `Removed` / `Unchanged` / `Modified` は、ファイル名だけでなく相対パスを基準に決まります。
- [`ShouldIgnoreILLinesContainingConfiguredStrings`](#configuration-table-ja) が `true` の場合は、設定した文字列を含む行も IL 比較から除外します。
- ウォームアップ、キャッシュ削除、書き込み後の読み取り専用化は best-effort として warning を記録して継続します。一方、フォルダ列挙、対応ファイル比較、レポート書き込みは正しさや成果物に直結するため、想定される実行時例外でもログ出力のうえ再スローします。
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
  "ShouldIncludeIgnoredFiles": false,
  "ShouldIncludeILCacheStatsInReport": true
}
```

### Configuration Table JA

<table>
  <thead>
    <tr>
      <th>項目</th>
      <th>既定値</th>
      <th>説明</th>
    </tr>
  </thead>
  <tbody>
    <tr id="config-ja-ignoredextensions">
      <td><code>IgnoredExtensions</code></td>
      <td><code>.cache</code>, <code>.DS_Store</code>, <code>.db</code>, <code>.ilcache</code>, <code>.log</code>, <code>.pdb</code></td>
      <td>指定拡張子を比較対象から除外します。</td>
    </tr>
    <tr id="config-ja-textfileextensions">
      <td><code>TextFileExtensions</code></td>
      <td><a href="Models/ConfigSettings.cs"><code>ConfigSettings</code></a> 内の組み込み拡張子一覧</td>
      <td>指定拡張子をテキスト比較対象にします（<code>.</code> 付き指定、大小無視）。</td>
    </tr>
    <tr id="config-ja-maxloggenerations">
      <td><code>MaxLogGenerations</code></td>
      <td><code>5</code></td>
      <td>ログローテーション世代数。</td>
    </tr>
    <tr id="config-ja-shouldincludeunchangedfiles">
      <td><code>ShouldIncludeUnchangedFiles</code></td>
      <td><code>true</code></td>
      <td>レポートに <code>Unchanged</code> セクションを出力するか。</td>
    </tr>
    <tr id="config-ja-shouldincludeignoredfiles">
      <td><code>ShouldIncludeIgnoredFiles</code></td>
      <td><code>true</code></td>
      <td>レポートに <code>Ignored Files</code> セクションを出力するか。</td>
    </tr>
    <tr id="config-ja-shouldincludeilcachestatsInreport">
      <td><code>ShouldIncludeILCacheStatsInReport</code></td>
      <td><code>false</code></td>
      <td><code>true</code> の場合、<code>Summary</code> と <code>Warnings</code> の間に <code>IL Cache Stats</code> セクション（ヒット数・ミス数・ヒット率・保存数・退避数・期限切れ数）を出力します。<code>EnableILCache</code> が <code>false</code> の場合は本設定が <code>true</code> でも出力されません。</td>
    </tr>
    <tr id="config-ja-shouldoutputiltext">
      <td><code>ShouldOutputILText</code></td>
      <td><code>true</code></td>
      <td><code>Reports/&lt;label&gt;/IL/old,new</code> へ IL を出力するか。</td>
    </tr>
    <tr id="config-ja-shouldignoreillinescontainingconfiguredstrings">
      <td><code>ShouldIgnoreILLinesContainingConfiguredStrings</code></td>
      <td><code>false</code></td>
      <td>IL 比較時の追加行除外（部分一致）を有効化するか。</td>
    </tr>
    <tr id="config-ja-ilignorelinecontainingstrings">
      <td><code>ILIgnoreLineContainingStrings</code></td>
      <td><code>[]</code></td>
      <td>IL 行除外に使う文字列一覧。</td>
    </tr>
    <tr id="config-ja-shouldoutputfiletimestamps">
      <td><code>ShouldOutputFileTimestamps</code></td>
      <td><code>true</code></td>
      <td>レポート各行に更新日時を併記するか。</td>
    </tr>
    <tr id="config-ja-shouldwarnwhennewfiletimestampisolderthanoldfiletimestamp">
      <td><code>ShouldWarnWhenNewFileTimestampIsOlderThanOldFileTimestamp</code></td>
      <td><code>true</code></td>
      <td><code>new</code> 側の更新日時が対応する <code>old</code> 側より古いファイルを検出し、実行終了時のコンソールと <code>diff_report.md</code> 末尾の <code>Warnings</code> セクションへ一覧を出力します。</td>
    </tr>
    <tr id="config-ja-maxparallelism">
      <td><code>MaxParallelism</code></td>
      <td><code>0</code></td>
      <td>比較の最大並列度。<code>0</code> 以下は自動。</td>
    </tr>
    <tr id="config-ja-textdiffparallelthresholdkilobytes">
      <td><code>TextDiffParallelThresholdKilobytes</code></td>
      <td><code>512</code></td>
      <td>並列テキスト比較へ切替える閾値（KiB）。</td>
    </tr>
    <tr id="config-ja-textdiffchunksizekilobytes">
      <td><code>TextDiffChunkSizeKilobytes</code></td>
      <td><code>64</code></td>
      <td>並列テキスト比較のチャンクサイズ（KiB）。</td>
    </tr>
    <tr id="config-ja-textdiffparallelmemorylimitmegabytes">
      <td><code>TextDiffParallelMemoryLimitMegabytes</code></td>
      <td><code>0</code></td>
      <td>並列テキスト比較で追加確保してよいバッファ予算（MB）。<code>&lt;=0</code> は無制限で、それ以外はワーカー数を減らすか逐次比較へ切り替え、その際の managed heap 使用量をログへ出力します。</td>
    </tr>
    <tr id="config-ja-enableilcache">
      <td><code>EnableILCache</code></td>
      <td><code>true</code></td>
      <td>IL キャッシュ（メモリ + 任意ディスク）を有効化するか。</td>
    </tr>
    <tr id="config-ja-ilcachedirectoryabsolutepath">
      <td><code>ILCacheDirectoryAbsolutePath</code></td>
      <td><code>""</code></td>
      <td>IL キャッシュディレクトリ。空なら <code>&lt;exe&gt;/ILCache</code>。</td>
    </tr>
    <tr id="config-ja-ilcachestatslogintervalseconds">
      <td><code>ILCacheStatsLogIntervalSeconds</code></td>
      <td><code>60</code></td>
      <td>IL キャッシュ統計ログ間隔。<code>&lt;=0</code> で既定 60 秒。</td>
    </tr>
    <tr id="config-ja-ilcachemaxdiskfilecount">
      <td><code>ILCacheMaxDiskFileCount</code></td>
      <td><code>1000</code></td>
      <td>ディスクキャッシュ最大ファイル数。<code>&lt;=0</code> で無制限。</td>
    </tr>
    <tr id="config-ja-ilcachemaxdiskmegabytes">
      <td><code>ILCacheMaxDiskMegabytes</code></td>
      <td><code>512</code></td>
      <td>ディスクキャッシュ容量上限（MB）。<code>&lt;=0</code> で無制限。</td>
    </tr>
    <tr id="config-ja-ilprecomputebatchsize">
      <td><code>ILPrecomputeBatchSize</code></td>
      <td><code>2048</code></td>
      <td>IL 関連事前計算のバッチサイズ。<code>&lt;=0</code> で既定値を使い、非常に大きいツリーでも余分な全件リストを追加生成しないようにします。</td>
    </tr>
    <tr id="config-ja-optimizefornetworkshares">
      <td><code>OptimizeForNetworkShares</code></td>
      <td><code>false</code></td>
      <td>ネットワーク共有向け最適化モードを有効化。</td>
    </tr>
    <tr id="config-ja-autodetectnetworkshares">
      <td><code>AutoDetectNetworkShares</code></td>
      <td><code>true</code></td>
      <td>ネットワーク共有を自動検出して最適化モードを必要時に有効化。</td>
    </tr>
    <tr id="config-ja-skipil">
      <td><code>SkipIL</code></td>
      <td><code>false</code></td>
      <td><code>true</code> の場合、.NET アセンブリの IL 逆アセンブルと IL 差分比較をまるごとスキップします。MD5 不一致のアセンブリはバイナリ差分として扱います。CLI フラグ <code>--skip-il</code> と同等。</td>
    </tr>
  </tbody>
</table>

補足:
- [`IgnoredExtensions`](#configuration-table-ja) と [`TextFileExtensions`](#configuration-table-ja) を含む組み込み既定値の全体は [`Models/ConfigSettings.cs`](Models/ConfigSettings.cs) に定義しています。
- プロジェクト横断で使うバイト換算値や日時フォーマットは [`FolderDiffIL4DotNet.Core/Common/CoreConstants.cs`](FolderDiffIL4DotNet.Core/Common/CoreConstants.cs) に置き、アプリ固有の定数は [`Common/Constants.cs`](Common/Constants.cs) で管理しているため、共有書式がプロジェクトごとにずれません。
- `config.json` の読み込み後、[`ConfigSettings.Validate()`](Models/ConfigSettings.cs) で設定値を検証します。範囲外の値がある場合は終了コード `3` で即座に失敗し、全エラーを列挙したエラーメッセージを表示します。検証対象の制約: `MaxLogGenerations >= 1`、`TextDiffParallelThresholdKilobytes >= 1`、`TextDiffChunkSizeKilobytes >= 1`、`TextDiffChunkSizeKilobytes` は `TextDiffParallelThresholdKilobytes` 未満であること。
- 拡張子なしファイルも比較対象です。
- 拡張子なしファイルをテキスト扱いしたい場合は [`TextFileExtensions`](#configuration-table-ja) に空文字（`""`）を含めてください。
- 更新日時逆転の警告は、`old` と `new` の両方に存在する同一相対パスのファイルだけを対象に判定します。
- `MD5Mismatch` が1件でもある場合、その警告はレポート末尾の `Warnings` セクションで更新日時逆転警告より先に出し、同じ文言を実行終了時のコンソールにも1回だけ出力します。

<a id="readme-ja-generated-artifacts"></a>
## 生成物

- `Reports/<label>/diff_report.md`
- `Logs/log_YYYYMMDD.log`
- 任意: `Reports/<label>/IL/old/*.txt`, `Reports/<label>/IL/new/*.txt`

レポート本体と IL テキストの生成は必須成果物として扱うため、書き込み失敗時は実行を停止します。生成後の読み取り専用化は可能な範囲で行い、失敗しても警告のみです。

<a id="readme-ja-api-docs"></a>
## API ドキュメント

API リファレンスは、[`FolderDiffIL4DotNet.csproj`](FolderDiffIL4DotNet.csproj) と [`FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj`](FolderDiffIL4DotNet.Core/FolderDiffIL4DotNet.Core.csproj) が出力する XML ドキュメントコメントを DocFX で収集して生成します。

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

<a id="readme-ja-ci-automation"></a>
## CI/CD とセキュリティ自動化

- [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml) はビルド、テスト、DocFX 生成、artifact 公開を行い、すでに total 行 `73%` / 分岐 `71%` のカバレッジしきい値も強制しています。
- [`.github/workflows/release.yml`](.github/workflows/release.yml) は `v*` タグ push 時に GitHub Release を作成し、publish 出力 ZIP、ドキュメント ZIP、SHA-256 チェックサムを添付します。
- [`.github/workflows/codeql.yml`](.github/workflows/codeql.yml) は C# と GitHub Actions を対象に、変更時と定期実行の CodeQL 解析を行います。
- [`.github/dependabot.yml`](.github/dependabot.yml) は NuGet と GitHub Actions の更新 PR を週次で作成します。

## ライセンス

- [MIT License](LICENSE)
