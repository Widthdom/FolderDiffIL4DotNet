# Troubleshooting / トラブルシューティング

Common issues and solutions for FolderDiffIL4DotNet.

FolderDiffIL4DotNet のよくある問題と解決策。

---

## English

### "ildasm not found" / IL disassembler unavailable

**Symptom:** The report shows all .NET assemblies compared by `SHA256Mismatch` instead of `ILMatch`/`ILMismatch`. The Disassembler Availability table shows all tools with `Available` = `No`.

**Cause:** No IL disassembler is installed or is not on `PATH`.

**Solution:**

```bash
# Install dotnet-ildasm (preferred)
dotnet tool install --global dotnet-ildasm

# Verify it works
dotnet-ildasm --version

# If "command not found", add the tools directory to PATH:
# macOS/Linux: export PATH="$PATH:$HOME/.dotnet/tools"
# Windows:     add %USERPROFILE%\.dotnet\tools to PATH
```

If `dotnet-ildasm` does not work with your target framework version, try `ilspycmd`:

```bash
dotnet tool install --global ilspycmd
ilspycmd --version
```

**Note:** The tool auto-probes disassemblers per file. If one disassembler fails, it falls back to the next candidate. Check the report's Disassembler Availability table to confirm which tools were detected.

### IL cache disk usage grows too large

**Symptom:** The `ILCache/` directory under the application folder consumes excessive disk space.

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

**Solution:** Free up disk space or redirect reports to a different drive using a full path for the `<reportLabel>` argument or moving the application directory.

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

**症状:** レポートですべての .NET アセンブリが `ILMatch`/`ILMismatch` ではなく `SHA256Mismatch` で比較されている。Disassembler Availability テーブルですべてのツールの `Available` 列が `No` と表示される。

**原因:** IL 逆アセンブラがインストールされていないか、`PATH` に含まれていない。

**解決策:**

```bash
# dotnet-ildasm をインストール（推奨）
dotnet tool install --global dotnet-ildasm

# 動作確認
dotnet-ildasm --version

# 「command not found」の場合、tools ディレクトリを PATH に追加:
# macOS/Linux: export PATH="$PATH:$HOME/.dotnet/tools"
# Windows:     %USERPROFILE%\.dotnet\tools を PATH に追加
```

`dotnet-ildasm` が対象フレームワークバージョンで動作しない場合は `ilspycmd` を試してください:

```bash
dotnet tool install --global ilspycmd
ilspycmd --version
```

**補足:** ツールはファイルごとに逆アセンブラを自動探索します。1 つが失敗すると次の候補にフォールバックします。レポートの Disassembler Availability テーブルで検出されたツールを確認してください。

### IL キャッシュのディスク使用量が大きくなりすぎる

**症状:** アプリケーションフォルダ配下の `ILCache/` ディレクトリが過剰なディスク領域を消費する。

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

**解決策:** ディスク容量を確保するか、レポートを別のドライブにリダイレクト。

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
