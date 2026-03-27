# Troubleshooting / トラブルシューティング

Common issues and solutions for FolderDiffIL4DotNet.

FolderDiffIL4DotNet のよくある問題と解決策。

---

## English

### "ildasm not found" / IL disassembler unavailable

**Symptom:** The report shows all .NET assemblies compared by `SHA256Mismatch` instead of `ILMatch`/`ILMismatch`. The Disassembler Availability table in the report header shows all tools with `Available` = `No`.

**Cause:** No IL disassembler is installed, or the installed tool is not on `PATH`.

#### Step 1: Check prerequisites

An IL disassembler requires the .NET SDK. Verify it is installed:

```bash
dotnet --version
```

If this fails, install the .NET SDK first from <https://dotnet.microsoft.com/download>.

#### Step 2: Install a disassembler

**Option A — `dotnet-ildasm` (preferred):**

```bash
dotnet tool install --global dotnet-ildasm
```

**Option B — `ilspycmd` (fallback):**

If `dotnet-ildasm` does not support your target framework version, or you prefer ILSpy-based output:

```bash
dotnet tool install --global ilspycmd
```

You can install both — the tool will prefer `dotnet-ildasm` and fall back to `ilspycmd` automatically.

#### Step 3: Add the tools directory to PATH

After installing a global tool, the `dotnet tool install` command prints the tools directory path. If your shell cannot find the tool, add it manually:

**macOS / Linux (bash/zsh):**

```bash
# Add to ~/.bashrc, ~/.zshrc, or ~/.profile for persistence
export PATH="$PATH:$HOME/.dotnet/tools"
```

**Windows (PowerShell):**

```powershell
# Temporary (current session only)
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"

# Permanent (requires restart of terminal)
[Environment]::SetEnvironmentVariable("PATH", $env:PATH + ";$env:USERPROFILE\.dotnet\tools", "User")
```

**Windows (Command Prompt):**

```cmd
setx PATH "%PATH%;%USERPROFILE%\.dotnet\tools"
```

#### Step 4: Verify the installation

```bash
# For dotnet-ildasm
dotnet-ildasm --version

# For ilspycmd
ilspycmd --version

# If the standalone command fails, try via the dotnet muxer
dotnet ildasm --version
```

If all three fail with "command not found", verify the tools directory contains the binary:

```bash
# macOS / Linux
ls ~/.dotnet/tools/

# Windows (PowerShell)
dir "$env:USERPROFILE\.dotnet\tools"
```

#### Step 5: Confirm detection in the report

Run FolderDiffIL4DotNet and check the **Disassembler Availability** table in the report header. Each candidate is probed at startup:

| Tool Name | Probed Path | Priority |
|-----------|-------------|----------|
| `dotnet-ildasm` | PATH lookup | 1 (highest) |
| `dotnet-ildasm` | `~/.dotnet/tools/dotnet-ildasm` | 2 |
| `dotnet` (muxer) | PATH lookup (runs as `dotnet ildasm`) | 3 |
| `ilspycmd` | PATH lookup | 4 |
| `ilspycmd` | `~/.dotnet/tools/ilspycmd` | 5 (lowest) |

The tool marked **In Use** in the report is the one that was successfully probed first.

#### How the fallback mechanism works

- The tool probes all 5 candidates at startup and records which are available.
- During comparison, it attempts each available candidate in priority order per file.
- If a tool fails **3 consecutive times**, it is temporarily blacklisted for **10 minutes** (configurable via `DisassemblerBlacklistTtlMinutes` in `config.json`). After the TTL expires, the tool is automatically reinstated.
- If all candidates are exhausted, the file falls back to binary (`SHA256`) comparison and a warning is logged.

#### Common installation issues

| Problem | Cause | Fix |
|---------|-------|-----|
| `dotnet tool install` fails with "NuGet feed" error | No internet or corporate proxy blocking NuGet | Configure NuGet source: `dotnet nuget add source https://api.nuget.org/v3/index.json` or use an offline `.nupkg` |
| Tool installed but "command not found" | `~/.dotnet/tools` not in PATH | See Step 3 above |
| `dotnet-ildasm --version` works but report still shows `SHA256Mismatch` | `SkipIL` is enabled | Check that `--skip-il` is not passed and `SkipIL` is `false` in `config.json` |
| Tool works for some assemblies but fails for others | Framework version mismatch or corrupt assembly | Check the per-file "Disassembler" column in the report; failed files show the fallback tool or `SHA256` |
| `ilspycmd` hangs on certain assemblies | Known issue with some obfuscated assemblies | Set `DisassemblerTimeoutSeconds` in `config.json` (default: 300s) to auto-kill hanging processes |
| Permission denied on macOS/Linux | File not executable | Run `chmod +x ~/.dotnet/tools/dotnet-ildasm` |

### IL cache disk usage grows too large

**Symptom:** The IL cache directory consumes excessive disk space. By default, this is the OS user-local cache path (`%LOCALAPPDATA%\FolderDiffIL4DotNet\ILCache` on Windows, `~/.local/share/FolderDiffIL4DotNet/ILCache` on macOS/Linux) unless `ILCacheDirectoryAbsolutePath` is configured.

**Cause:** Default disk cache limits may be too generous for your use case (default: 1000 files, 512 MiB).

**Solution:** Adjust the cache quotas in `config.json`:

```json
{
  "ILCacheMaxDiskFileCount": 500,
  "ILCacheMaxDiskMegabytes": 256
}
```

To disable disk caching entirely while keeping in-memory caching:

```json
{
  "ILCacheMaxDiskFileCount": 0,
  "ILCacheMaxDiskMegabytes": 0
}
```

To disable IL caching completely (both memory and disk):

```bash
dotnet run -- /path/old /path/new label --no-il-cache --no-pause
```

Or in `config.json`:

```json
{
  "EnableILCache": false
}
```

### Slow performance on network shares

**Symptom:** Comparison runs very slowly when old/new folders are on a network share (NFS, SMB/CIFS).

**Cause:** Parallel I/O and SHA256 precomputation create excessive network round-trips.

**Solution:**

```json
{
  "OptimizeForNetworkShares": true
}
```

This skips SHA256 precomputation and uses sequential I/O instead of parallel chunk comparison. Alternatively, enable auto-detection (default is `true`):

```json
{
  "AutoDetectNetworkShares": true
}
```

### "Configuration load/parse error" (exit code 3)

**Symptom:** The tool exits with code `3` and a configuration error message.

**Cause:** Invalid JSON in `config.json`, or a setting value out of range.

**Solution:**

1. Validate your JSON syntax (check for trailing commas, missing quotes).
2. Use `--print-config` to see what the tool actually reads:

```bash
dotnet run -- --print-config
dotnet run -- --config /path/to/config.json --print-config
```

3. Compare with the annotated sample: [`doc/config.sample.jsonc`](config.sample.jsonc).

### "Path length exceeds OS limit" (exit code 2)

**Symptom:** Preflight check fails with a path-length error.

**Cause:** The constructed `Reports/<label>` path exceeds the OS limit (260 chars on Windows, 1024 on macOS, 4096 on Linux).

**Solution:** Use a shorter report label or move the working directory closer to the filesystem root. On Windows, enable long-path support:

```powershell
# Windows 10/11 — requires admin
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" -Name "LongPathsEnabled" -Value 1 -PropertyType DWORD -Force
```

### "Insufficient disk space" (exit code 2)

**Symptom:** Preflight check fails with a disk-space error.

**Cause:** Less than 100 MiB free on the drive that will hold the `Reports/` folder.

**Solution:** Free up disk space on the drive that contains the application `Reports/` folder, use a shorter report label if path length is also a factor, or move the application/workspace to a drive with more free space. The `<reportLabel>` argument is a folder name, not an output-path override.

### All files reported as "Modified" even though they are functionally identical

**Symptom:** .NET assemblies show `SHA256Mismatch` instead of `ILMatch`.

**Cause:** IL comparison may be skipped (`SkipIL: true`) or no disassembler is available.

**Solution:**

1. Check that `SkipIL` is `false` (default) and not overridden by `--skip-il`.
2. Install an IL disassembler (see "ildasm not found" above).
3. Check the Disassembler Availability table in the report header.

### Memory pressure with very large IL files

**Symptom:** High memory usage or `OutOfMemoryException` when comparing assemblies with very large IL output.

**Solution:** Reduce parallelism to limit concurrent memory usage:

```bash
dotnet run -- /path/old /path/new label --threads 2 --no-pause
```

Or limit the text diff memory budget:

```json
{
  "TextDiffParallelMemoryLimitMegabytes": 256
}
```

---

## 日本語

### 「ildasm が見つからない」/ IL 逆アセンブラが利用不可

**症状:** レポートですべての .NET アセンブリが `ILMatch`/`ILMismatch` ではなく `SHA256Mismatch` で比較されている。レポートヘッダの Disassembler Availability テーブルですべてのツールの `Available` 列が `No` と表示される。

**原因:** IL 逆アセンブラがインストールされていないか、`PATH` に含まれていない。

#### 手順 1: 前提条件の確認

IL 逆アセンブラには .NET SDK が必要です。インストール済みか確認してください:

```bash
dotnet --version
```

失敗する場合は、まず .NET SDK を <https://dotnet.microsoft.com/download> からインストールしてください。

#### 手順 2: 逆アセンブラのインストール

**方法 A — `dotnet-ildasm`（推奨）:**

```bash
dotnet tool install --global dotnet-ildasm
```

**方法 B — `ilspycmd`（フォールバック）:**

`dotnet-ildasm` が対象フレームワークに対応していない場合、または ILSpy ベースの出力を好む場合:

```bash
dotnet tool install --global ilspycmd
```

両方インストールすることも可能です。ツールは `dotnet-ildasm` を優先し、失敗時に `ilspycmd` へ自動フォールバックします。

#### 手順 3: tools ディレクトリを PATH に追加

グローバルツールのインストール後、`dotnet tool install` コマンドが tools ディレクトリのパスを表示します。シェルがツールを見つけられない場合は手動で追加してください:

**macOS / Linux (bash/zsh):**

```bash
# ~/.bashrc、~/.zshrc、または ~/.profile に追記（永続化）
export PATH="$PATH:$HOME/.dotnet/tools"
```

**Windows (PowerShell):**

```powershell
# 一時的（現在のセッションのみ）
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"

# 永続的（ターミナルの再起動が必要）
[Environment]::SetEnvironmentVariable("PATH", $env:PATH + ";$env:USERPROFILE\.dotnet\tools", "User")
```

**Windows (コマンドプロンプト):**

```cmd
setx PATH "%PATH%;%USERPROFILE%\.dotnet\tools"
```

#### 手順 4: インストールの確認

```bash
# dotnet-ildasm の場合
dotnet-ildasm --version

# ilspycmd の場合
ilspycmd --version

# スタンドアロンコマンドが失敗する場合、dotnet マルチプレクサー経由で確認
dotnet ildasm --version
```

3つともすべて「command not found」の場合、tools ディレクトリにバイナリが存在するか確認してください:

```bash
# macOS / Linux
ls ~/.dotnet/tools/

# Windows (PowerShell)
dir "$env:USERPROFILE\.dotnet\tools"
```

#### 手順 5: レポートでの検出確認

FolderDiffIL4DotNet を実行し、レポートヘッダの **Disassembler Availability** テーブルを確認してください。起動時に各候補がプローブされます:

| ツール名 | プローブパス | 優先度 |
|----------|-------------|--------|
| `dotnet-ildasm` | PATH 検索 | 1（最高） |
| `dotnet-ildasm` | `~/.dotnet/tools/dotnet-ildasm` | 2 |
| `dotnet`（マルチプレクサー） | PATH 検索（`dotnet ildasm` として実行） | 3 |
| `ilspycmd` | PATH 検索 | 4 |
| `ilspycmd` | `~/.dotnet/tools/ilspycmd` | 5（最低） |

レポートで **In Use** と表示されているツールが、最初にプローブ成功した候補です。

#### フォールバック機構の動作

- 起動時に 5 つの候補すべてをプローブし、利用可能なものを記録します。
- 比較中は、ファイルごとに優先度順で利用可能な候補を試行します。
- ツールが **3 回連続**で失敗すると、**10 分間**一時的にブラックリスト化されます（`config.json` の `DisassemblerBlacklistTtlMinutes` で設定可能）。TTL 経過後、自動的に復帰します。
- すべての候補が使い尽くされた場合、そのファイルはバイナリ（`SHA256`）比較にフォールバックし、警告がログに記録されます。

#### よくあるインストールの問題

| 問題 | 原因 | 解決策 |
|------|------|--------|
| `dotnet tool install` が「NuGet フィード」エラーで失敗 | インターネット未接続または企業プロキシが NuGet をブロック | NuGet ソースを設定: `dotnet nuget add source https://api.nuget.org/v3/index.json` またはオフライン `.nupkg` を使用 |
| インストール済みだが「command not found」 | `~/.dotnet/tools` が PATH に未追加 | 上記の手順 3 を参照 |
| `dotnet-ildasm --version` は動作するがレポートが `SHA256Mismatch` のまま | `SkipIL` が有効 | `--skip-il` が渡されていないこと、`config.json` の `SkipIL` が `false` であることを確認 |
| 一部のアセンブリでのみツールが失敗する | フレームワークバージョンの不一致またはアセンブリの破損 | レポートのファイルごとの「Disassembler」列を確認。失敗したファイルはフォールバックツールまたは `SHA256` と表示される |
| `ilspycmd` が特定のアセンブリでハングする | 難読化されたアセンブリでの既知の問題 | `config.json` の `DisassemblerTimeoutSeconds` を設定（デフォルト: 300秒）してハングしたプロセスを自動終了 |
| macOS/Linux でパーミッションエラー | ファイルに実行権限がない | `chmod +x ~/.dotnet/tools/dotnet-ildasm` を実行 |

### IL キャッシュのディスク使用量が大きくなりすぎる

**症状:** IL キャッシュディレクトリが過剰なディスク領域を消費する。既定では `ILCacheDirectoryAbsolutePath` 未設定時、OS 標準のユーザーローカルキャッシュパス（Windows: `%LOCALAPPDATA%\FolderDiffIL4DotNet\ILCache`、macOS/Linux: `~/.local/share/FolderDiffIL4DotNet/ILCache`）が使われる。

**原因:** デフォルトのディスクキャッシュ上限が用途に対して大きすぎる（デフォルト: 1000 ファイル、512 MiB）。

**解決策:** `config.json` でキャッシュクォータを調整:

```json
{
  "ILCacheMaxDiskFileCount": 500,
  "ILCacheMaxDiskMegabytes": 256
}
```

メモリキャッシュを維持しつつディスクキャッシュを完全に無効化するには:

```json
{
  "ILCacheMaxDiskFileCount": 0,
  "ILCacheMaxDiskMegabytes": 0
}
```

IL キャッシュを完全に無効化するには（メモリ・ディスク両方）:

```bash
dotnet run -- /path/old /path/new label --no-il-cache --no-pause
```

または `config.json` で:

```json
{
  "EnableILCache": false
}
```

### ネットワーク共有上でパフォーマンスが遅い

**症状:** old/new フォルダがネットワーク共有（NFS、SMB/CIFS）上にある場合、比較実行が非常に遅い。

**原因:** 並列 I/O と SHA256 プリコンピュートがネットワークラウンドトリップを過剰に発生させている。

**解決策:**

```json
{
  "OptimizeForNetworkShares": true
}
```

これにより SHA256 プリコンピュートがスキップされ、並列チャンク比較の代わりに逐次 I/O が使用されます。または、自動検出を有効にしてください（デフォルトは `true`）:

```json
{
  "AutoDetectNetworkShares": true
}
```

### 「設定の読み込み/解析エラー」（終了コード 3）

**症状:** ツールが終了コード `3` と設定エラーメッセージで終了する。

**原因:** `config.json` の JSON 構文が無効、または設定値が範囲外。

**解決策:**

1. JSON 構文を検証（末尾カンマ、引用符の欠落をチェック）。
2. `--print-config` でツールが実際に読み取る内容を確認:

```bash
dotnet run -- --print-config
dotnet run -- --config /path/to/config.json --print-config
```

3. コメント付きサンプルと比較: [`doc/config.sample.jsonc`](config.sample.jsonc)。

### 「パス長が OS 制限を超過」（終了コード 2）

**症状:** プリフライトチェックがパス長エラーで失敗する。

**原因:** 構築された `Reports/<label>` パスが OS 制限を超過（Windows: 260 文字、macOS: 1024、Linux: 4096）。

**解決策:** より短いレポートラベルを使用するか、作業ディレクトリをファイルシステムルートに近い場所に移動。Windows では長いパスのサポートを有効化:

```powershell
# Windows 10/11 — 管理者権限が必要
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" -Name "LongPathsEnabled" -Value 1 -PropertyType DWORD -Force
```

### 「ディスク空き容量不足」（終了コード 2）

**症状:** プリフライトチェックがディスク容量エラーで失敗する。

**原因:** `Reports/` フォルダを保持するドライブの空き容量が 100 MiB 未満。

**解決策:** アプリケーションの `Reports/` フォルダが置かれるドライブの空き容量を増やすか、パス長も問題なら短いレポートラベルを使うか、アプリケーション/ワークスペース自体を空き容量の多いドライブへ移動してください。`<reportLabel>` 引数は出力先パスではなくフォルダ名です。

### 機能的に同一なのにすべてのファイルが「Modified」と報告される

**症状:** .NET アセンブリが `ILMatch` ではなく `SHA256Mismatch` と表示される。

**原因:** IL 比較がスキップされている（`SkipIL: true`）か、逆アセンブラが利用できない。

**解決策:**

1. `SkipIL` が `false`（デフォルト）であり `--skip-il` でオーバーライドされていないことを確認。
2. IL 逆アセンブラをインストール（上記「ildasm が見つからない」を参照）。
3. レポートヘッダの Disassembler Availability テーブルを確認。

### 非常に大きな IL ファイルでのメモリ圧迫

**症状:** 非常に大きな IL 出力を持つアセンブリの比較時にメモリ使用量が高い、または `OutOfMemoryException` が発生する。

**解決策:** 並列度を下げて同時メモリ使用量を制限:

```bash
dotnet run -- /path/old /path/new label --threads 2 --no-pause
```

またはテキスト差分メモリ予算を制限:

```json
{
  "TextDiffParallelMemoryLimitMegabytes": 256
}
```
