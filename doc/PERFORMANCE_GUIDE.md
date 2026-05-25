# Performance Guide (English)

This document describes the memory management architecture, performance configuration, and baseline benchmark metrics for FolderDiffIL4DotNet.

Related documents:
- [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md#guide-en-map)
- [README.md](../README.md#readme-en-doc-map)

<a id="perf-en-memory"></a>
## Memory Management Architecture

FolderDiffIL4DotNet processes potentially tens of thousands of files in parallel. Memory consumption is governed by three independent subsystems, each with its own budget:

### 1. IL Memory Cache (`ILMemoryCache`)

Caches disassembled IL text in memory to avoid redundant subprocess calls.

| Setting | Default | Description |
| --- | --- | --- |
| `ILCacheMaxMemoryMegabytes` | `256` | Memory budget in MB. Set `0` only when you intentionally want unlimited memory mode (entry-count limit only). |
| Max entries (runtime default) | `2000` | Maximum number of cached IL text entries in the default run scope. |
| TTL | Configurable | Expired entries are purged lazily on access. |

**Eviction strategy:** LRU (Least Recently Used). When inserting a new entry would exceed either the entry count or memory budget, the least-recently-accessed entries are evicted until both constraints are satisfied.

**Memory estimation:** Each cached string costs approximately `(length * 2) + 56` bytes (UTF-16 encoding + .NET object overhead). This is tracked atomically via `Interlocked` operations.

**Recommendation for large-scale runs (10,000+ files):** The default `256` MB cap is the safer baseline. Increase `ILCacheMaxMemoryMegabytes` to `512` or `1024` when you want a higher cache hit rate on large-memory machines. Set `0` only when you explicitly accept unlimited memory growth.

### 2. IL Disk Cache (`ILDiskCache`)

Persists IL disassembly results to disk for cross-run reuse.

| Setting | Default | Description |
| --- | --- | --- |
| `ILCacheMaxDiskMegabytes` | `512` | Maximum disk cache size in MB. |
| `ILCacheMaxDiskFileCount` | `1000` | Maximum number of cache files on disk. |
| `ILCacheDirectoryAbsolutePath` | (empty) | Cache directory path. Empty uses the default OS user-local cache path (`%LOCALAPPDATA%\\FolderDiffIL4DotNet\\ILCache` on Windows, `~/.local/share/FolderDiffIL4DotNet/ILCache` on macOS/Linux). |

**Quota enforcement:** Both file count and size limits are enforced at write time. Oldest files are trimmed when either limit is reached.

### 3. Text Diff Memory Budget

Controls memory allocation for parallel chunk-based text comparison of large files.

| Setting | Default | Description |
| --- | --- | --- |
| `TextDiffParallelThresholdKilobytes` | `512` | Files larger than this are compared in parallel chunks. |
| `TextDiffChunkSizeKilobytes` | `64` | Size of each parallel comparison chunk. |
| `TextDiffParallelMemoryLimitMegabytes` | `0` (unlimited) | Memory budget for parallel diff buffers. |

### Overall Memory Profile

| Component | Typical Memory (1,000 files) | Typical Memory (10,000 files) | Bounded? |
| --- | --- | --- | --- |
| IL Memory Cache | 50-200 MB | 50-200 MB (capped at 2,000 entries) | Yes (entry count) |
| File classification queues | < 10 MB | ~50 MB | No (grows with file count) |
| Parallel diff buffers | < 50 MB per thread | < 50 MB per thread | Configurable |
| .NET runtime / GC overhead | ~100 MB | ~150 MB | N/A |
| **Total estimated** | **200-400 MB** | **300-500 MB** | Partially |

**Key insight:** The IL memory cache is the largest memory consumer. With the default 2,000-entry cap and typical IL text sizes (10-200 KB per assembly), peak memory for the cache alone is approximately 100-200 MB. For projects with 10,000+ assemblies, the cache serves as a sliding window over the most recently accessed entries.

<a id="perf-en-parallelism"></a>
## Parallelism Configuration

| Setting | Default | Description |
| --- | --- | --- |
| `MaxParallelism` | `0` (auto) | Number of parallel file comparison threads. `0` = `Environment.ProcessorCount`. |
| Network share cap | `8` | Auto-detected NAS/SMB paths are capped at 8 threads for I/O stability. |

**Guideline:** For local SSDs, the default auto-detection works well. For network shares or spinning disks, explicitly set `MaxParallelism` to `4-8` to avoid I/O saturation.

<a id="perf-en-benchmarks"></a>
## Benchmark Baselines

The following baselines were measured using BenchmarkDotNet on .NET 8.0 with `[MemoryDiagnoser]`. These are reference values for regression detection, not absolute guarantees.

### Folder Enumeration & Hashing

| Benchmark | File Count | File Size | Expected Range | What It Measures |
| --- | --- | --- | --- | --- |
| `EnumerateFiles_100` | 100 | 1 KB each | < 5 ms | Directory scan overhead for small folders |
| `EnumerateFiles_1000` | 1,000 | 4 KB each | < 20 ms | Directory scan for typical projects |
| `EnumerateFiles_10000` | 10,000 | 512 B each | < 200 ms | Directory scan for large monorepos |
| `HashCompare_SmallFile` | 2 | 1 KB | < 1 ms | SHA256 hashing baseline |

### Text Diff Algorithm (Myers)

| Benchmark | Lines | Changes | Expected Range | Memory Alloc |
| --- | --- | --- | --- | --- |
| `SmallFile_5Changes` | 100 | 5 | < 1 ms | < 50 KB |
| `MediumFile_20Changes` | 10,000 | 20 | < 50 ms | < 5 MB |
| `LargeFile_10Changes` | 1,000,000 | 10 | < 500 ms | < 50 MB |

**Diff parameters used:** `contextLines: 3`, `maxEditDistance: 4000`, `maxOutputLines: 10000`.

### Running Benchmarks Locally

```bash
# All benchmarks
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks

# Text diff only
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks -- --filter *TextDiffer*

# Folder enumeration only
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks -- --filter *FolderDiff*
```

### CI Regression Detection

The [`benchmark-regression.yml`](../.github/workflows/benchmark-regression.yml) workflow:
- Stores baseline results in the `gh-benchmarks` branch on push to `main`.
- Compares PR results against the baseline with a **150% threshold** (50% degradation triggers failure).
- Posts PR comments when regressions are detected.

<a id="perf-en-tuning"></a>
## Tuning Recommendations

| Scenario | Recommended Settings |
| --- | --- |
| **Small project (< 500 files)** | Defaults work well. No tuning needed. |
| **Medium project (500-5,000 files)** | The default `ILCacheMaxMemoryMegabytes: 256` is usually sufficient. |
| **Large project (5,000-50,000 files)** | Set `ILCacheMaxMemoryMegabytes: 512-1024`, `MaxParallelism: 8-16`, enable disk cache. |
| **Network share source** | Set `MaxParallelism: 4-8`, enable disk cache for IL reuse across runs. |
| **CI environment (limited RAM)** | Set `ILCacheMaxMemoryMegabytes: 128`, `MaxParallelism: 4`, `--no-il-cache` if memory is critical. |
| **Memory-constrained (< 2 GB RAM)** | Use `--skip-il` to bypass IL comparison entirely, or set `ILCacheMaxMemoryMegabytes: 64`. |

---

# パフォーマンスガイド（日本語）

このドキュメントでは、FolderDiffIL4DotNet のメモリ管理アーキテクチャ、パフォーマンス設定、ベンチマークのベースライン指標を説明します。

関連ドキュメント:
- [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md#guide-ja-map)
- [README.md](../README.md#readme-ja-doc-map)

<a id="perf-ja-memory"></a>
## メモリ管理アーキテクチャ

FolderDiffIL4DotNet は数万ファイルを並列処理する可能性があります。メモリ消費は 3 つの独立したサブシステムで管理され、それぞれ独自のバジェットを持ちます。

### 1. IL メモリキャッシュ（`ILMemoryCache`）

逆アセンブリ済み IL テキストをメモリにキャッシュし、サブプロセスの重複呼び出しを回避します。

| 設定 | デフォルト | 説明 |
| --- | --- | --- |
| `ILCacheMaxMemoryMegabytes` | `256` | メモリバジェット（MB）。無制限（エントリ数制限のみ）に戻したい場合だけ `0` を明示指定。 |
| 最大エントリ数（実行時既定値） | `2000` | 既定の run scope でキャッシュされる IL テキストエントリの最大数。 |
| TTL | 設定可能 | 期限切れエントリはアクセス時に遅延パージされる。 |

**エビクション戦略：** LRU（Least Recently Used）。新しいエントリの挿入でエントリ数またはメモリバジェットを超過する場合、最も最近アクセスされていないエントリから削除。

**メモリ推定：** キャッシュされた文字列 1 件あたり約 `(文字数 * 2) + 56` バイト（UTF-16 + .NET オブジェクトオーバーヘッド）。`Interlocked` 操作でアトミックに追跡。

**大規模実行（10,000+ ファイル）の推奨：** 既定の `256` MB 上限を安全側のベースラインとして使い、より高いヒット率が必要なら `ILCacheMaxMemoryMegabytes` を `512` や `1024` に引き上げてください。`0` は無制限増加を許容すると明示的に判断した場合だけ使ってください。

### 2. IL ディスクキャッシュ（`ILDiskCache`）

IL 逆アセンブリ結果をディスクに永続化し、実行間での再利用を可能にします。

| 設定 | デフォルト | 説明 |
| --- | --- | --- |
| `ILCacheMaxDiskMegabytes` | `512` | ディスクキャッシュの最大サイズ（MB）。 |
| `ILCacheMaxDiskFileCount` | `1000` | ディスク上のキャッシュファイル最大数。 |
| `ILCacheDirectoryAbsolutePath` | （空） | キャッシュディレクトリパス。空の場合は OS 標準のユーザーローカルキャッシュパス（Windows: `%LOCALAPPDATA%\\FolderDiffIL4DotNet\\ILCache`、macOS/Linux: `~/.local/share/FolderDiffIL4DotNet/ILCache`）を使用。 |

**クォータ強制：** ファイル数・サイズの両制限が書き込み時に適用。いずれかの制限に達すると古いファイルから削除。

### 3. テキスト差分メモリバジェット

大きなファイルの並列チャンクベーステキスト比較のメモリ割り当てを制御します。

| 設定 | デフォルト | 説明 |
| --- | --- | --- |
| `TextDiffParallelThresholdKilobytes` | `512` | これより大きいファイルは並列チャンクで比較。 |
| `TextDiffChunkSizeKilobytes` | `64` | 並列比較チャンクのサイズ。 |
| `TextDiffParallelMemoryLimitMegabytes` | `0`（無制限） | 並列差分バッファのメモリバジェット。 |

### 全体のメモリプロファイル

| コンポーネント | 典型的メモリ（1,000 ファイル） | 典型的メモリ（10,000 ファイル） | 制限あり？ |
| --- | --- | --- | --- |
| IL メモリキャッシュ | 50-200 MB | 50-200 MB（2,000 エントリ上限） | はい（エントリ数） |
| ファイル分類キュー | < 10 MB | ~50 MB | いいえ（ファイル数に比例） |
| 並列差分バッファ | < 50 MB/スレッド | < 50 MB/スレッド | 設定可能 |
| .NET ランタイム / GC オーバーヘッド | ~100 MB | ~150 MB | N/A |
| **推定合計** | **200-400 MB** | **300-500 MB** | 部分的 |

**重要な洞察：** IL メモリキャッシュが最大のメモリ消費者。デフォルトの 2,000 エントリ上限と典型的な IL テキストサイズ（アセンブリあたり 10-200 KB）では、キャッシュ単体のピークメモリは約 100-200 MB。10,000+ アセンブリのプロジェクトでは、キャッシュは最近アクセスされたエントリのスライディングウィンドウとして機能。

<a id="perf-ja-parallelism"></a>
## 並列度設定

| 設定 | デフォルト | 説明 |
| --- | --- | --- |
| `MaxParallelism` | `0`（自動） | 並列ファイル比較スレッド数。`0` = `Environment.ProcessorCount`。 |
| ネットワーク共有上限 | `8` | 自動検出された NAS/SMB パスは I/O 安定性のため 8 スレッドに制限。 |

**ガイドライン：** ローカル SSD ではデフォルトの自動検出で十分。ネットワーク共有や HDD では、I/O 飽和を避けるため `MaxParallelism` を `4-8` に明示設定。

<a id="perf-ja-benchmarks"></a>
## ベンチマークベースライン

以下のベースラインは BenchmarkDotNet（.NET 8.0、`[MemoryDiagnoser]` 付き）で計測。回帰検出用の参考値であり、絶対的な保証ではありません。

### フォルダ列挙・ハッシュ

| ベンチマーク | ファイル数 | ファイルサイズ | 期待範囲 | 計測内容 |
| --- | --- | --- | --- | --- |
| `EnumerateFiles_100` | 100 | 各 1 KB | < 5 ms | 小規模フォルダのディレクトリスキャン |
| `EnumerateFiles_1000` | 1,000 | 各 4 KB | < 20 ms | 典型的プロジェクトのスキャン |
| `EnumerateFiles_10000` | 10,000 | 各 512 B | < 200 ms | 大規模モノレポのスキャン |
| `HashCompare_SmallFile` | 2 | 1 KB | < 1 ms | SHA256 ハッシュのベースライン |

### テキスト差分アルゴリズム（Myers）

| ベンチマーク | 行数 | 変更数 | 期待範囲 | メモリ割当 |
| --- | --- | --- | --- | --- |
| `SmallFile_5Changes` | 100 | 5 | < 1 ms | < 50 KB |
| `MediumFile_20Changes` | 10,000 | 20 | < 50 ms | < 5 MB |
| `LargeFile_10Changes` | 1,000,000 | 10 | < 500 ms | < 50 MB |

**使用差分パラメータ：** `contextLines: 3`、`maxEditDistance: 4000`、`maxOutputLines: 10000`。

### ローカルでのベンチマーク実行

```bash
# 全ベンチマーク
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks

# テキスト差分のみ
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks -- --filter *TextDiffer*

# フォルダ列挙のみ
dotnet run -c Release --project FolderDiffIL4DotNet.Benchmarks -- --filter *FolderDiff*
```

### CI 回帰検出

[`benchmark-regression.yml`](../.github/workflows/benchmark-regression.yml) ワークフロー:
- `main` への push 時にベースライン結果を `gh-benchmarks` ブランチに保存。
- PR 結果をベースラインと **150% 閾値**（50% 劣化で失敗）で比較。
- 回帰検出時に PR コメントを投稿。

<a id="perf-ja-tuning"></a>
## チューニング推奨

| シナリオ | 推奨設定 |
| --- | --- |
| **小規模プロジェクト（< 500 ファイル）** | デフォルトで十分。チューニング不要。 |
| **中規模プロジェクト（500-5,000 ファイル）** | 既定の `ILCacheMaxMemoryMegabytes: 256` で十分なことが多い。 |
| **大規模プロジェクト（5,000-50,000 ファイル）** | `ILCacheMaxMemoryMegabytes: 512-1024`、`MaxParallelism: 8-16`、ディスクキャッシュ有効化。 |
| **ネットワーク共有ソース** | `MaxParallelism: 4-8`、実行間の IL 再利用にディスクキャッシュ有効化。 |
| **CI 環境（限定 RAM）** | `ILCacheMaxMemoryMegabytes: 128`、`MaxParallelism: 4`、メモリ重視なら `--no-il-cache`。 |
| **メモリ制約（< 2 GB RAM）** | IL 比較を完全スキップする `--skip-il`、または `ILCacheMaxMemoryMegabytes: 64`。 |
