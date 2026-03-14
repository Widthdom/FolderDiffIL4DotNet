# Developer Guide

This document contains developer-focused information extracted from `README.md`: architecture, CI, and implementation cautions.

## Local Development

Build:

```bash
dotnet restore FolderDiffIL4DotNet.sln
dotnet build FolderDiffIL4DotNet.sln --configuration Release
```

Testing details:
- [doc/TESTING_GUIDE.md](TESTING_GUIDE.md)

## Runtime Architecture

Main components:
- `Program.cs`: thin entry point that creates the root `ServiceCollection` and delegates to `ProgramRunner`.
- `ProgramRunner.cs`: validates arguments, loads config, creates per-run DI scope, and orchestrates end-to-end execution without static mutable run state.
- `Services/DiffExecutionContext.cs`: run-scoped immutable context for old/new/report paths, IL output paths, and network-optimization decisions.
- `Services/FolderDiffService.cs`: orchestrates end-to-end diff execution, file discovery, parallel scheduling, and result aggregation.
- `Services/FileDiffService.cs`: performs per-file classification (`MD5Match`, `ILMatch`, `TextMatch`, etc.).
- `Services/ILOutputService.cs`: facade for IL compare flow and optional IL text output.
- `Services/DotNetDisassembleService.cs`: executes IL tools, caches versions/fingerprints, applies tool failure blacklist.
- `Services/Caching/ILCache.cs`: memory + optional disk cache keyed by file hash and tool identity.
- `Services/ReportGenerateService.cs`: writes `diff_report.md` sections and summary.
- `Models/FileDiffResultLists.cs`: thread-safe run-scoped shared state (`ConcurrentQueue` / `ConcurrentDictionary`).

Dependency injection notes:
- `Program.cs` owns only application-root services (`ILoggerService`, `ConfigService`, `ProgramRunner`).
- `ProgramRunner` creates a per-run service provider containing `ConfigSettings`, `DiffExecutionContext`, `FileDiffResultLists`, cache/disassembler services, and the diff/report pipeline.
- `FileDiffService`, `ILOutputService`, `FolderDiffService`, `DotNetDisassembleService`, and `ILTextOutputService` now depend on interfaces or run context rather than constructing collaborators via `ActivatorUtilities.CreateInstance`.
- This layout is intentional for testability: test code can replace `IFileDiffService`, `IILOutputService`, `IFolderDiffService`, `IDotNetDisassembleService`, or `IILTextOutputService` without mutating static fields.

Comparison pipeline:
1. MD5 compare first.
2. If both files are .NET assemblies (PE/CLR detection), compare IL.
3. If extension is in `TextFileExtensions`, do line-based text compare.
4. Otherwise classify as `MD5Mismatch`.

## CI (GitHub Actions)

Workflow file:
- `.github/workflows/dotnet.yml`

Current behavior:
- Trigger: `push` and `pull_request` on `main`, plus `workflow_dispatch`.
- Checkout: `fetch-depth: 0` for Nerdbank.GitVersioning compatibility.
- Build: `dotnet restore` + `dotnet build` (Release).
- Test: runs only if `FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj` exists.
- Coverage: `coverlet.collector` (`XPlat Code Coverage`) + `reportgenerator`.
- Artifacts: `FolderDiffIL4DotNet` (publish output), `TestAndCoverage` (TRX + coverage files).

## Testing

- Detailed test strategy, run commands, coverage, and per-class scope are documented in [doc/TESTING_GUIDE.md](TESTING_GUIDE.md).

## Performance Features

- Parallel file comparison controlled by `MaxParallelism`.
- IL cache with memory LRU behavior and optional disk persistence.
- Optional MD5 precompute and IL cache prefetch.
- Parallel text compare by chunk for large text files.
- Tool failure blacklist to avoid repeated expensive disassembler launches.
- Network-share optimization mode (`OptimizeForNetworkShares` and `AutoDetectNetworkShares`).

## Versioning

- Uses Nerdbank.GitVersioning (`version.json`).
- `AssemblyInformationalVersion` is embedded and reported in generated diff reports.

## Implementation Cautions

- Keep `Program.cs` thin. Add orchestration logic to `ProgramRunner` or lower services, not back into static entry-point state.
- Preserve the contract that each execution gets a fresh `DiffExecutionContext` and `FileDiffResultLists`. Reusing them across runs would reintroduce cross-run contamination and make tests order-dependent.
- Avoid reintroducing `ActivatorUtilities.CreateInstance` or service-side `new` calls for core collaborators when constructor injection is feasible. That would weaken substitution in tests.
- Always keep `FolderDiffService` run initialization behavior intact: `FileDiffResultLists.ResetAll()` must run before enumeration/comparison to avoid cross-run contamination.
- Do not mix disassemblers across old/new assembly pair comparison. `ILOutputService` expects the same tool/version identity and throws on mismatch.
- `ILIgnoreLineContainingStrings` filtering is substring-based and case-sensitive (`StringComparison.Ordinal`). Treat this as a compatibility behavior when changing filtering.
- Preserve the fallback policy for network path detection: recoverable detection errors must degrade to local-path handling (`false`) and continue diffing.
- Keep report and IL write-protection logic best-effort. `TrySetReadOnly` failures should remain warning-only, not fatal.
- When adjusting parallelism defaults, retain network optimization cap semantics (`Math.Min(Environment.ProcessorCount, 8)` when network mode is active and `MaxParallelism <= 0`).
- Keep IL cache key identity stable (`file hash + tool identity/version or fingerprint fallback`) so cache entries do not cross-contaminate across tool upgrades.
- Maintain CI conditionals that skip test/coverage steps when test project is absent, so early-stage branches remain buildable.

---

# 開発者ガイド(日本語)

このドキュメントは `README.md` から開発者向け情報を切り出したものです。対象は設計、CI、実装上の注意点です。

## ローカル開発

ビルド:

```bash
dotnet restore FolderDiffIL4DotNet.sln
dotnet build FolderDiffIL4DotNet.sln --configuration Release
```

テスト詳細:
- [doc/TESTING_GUIDE.md](TESTING_GUIDE.md)

## 実行時アーキテクチャ

主要コンポーネント:
- `Program.cs`: ルート DI を組み立てて `ProgramRunner` に委譲する薄いエントリーポイント。
- `ProgramRunner.cs`: 引数検証、設定読込、実行単位 DI スコープ生成、差分実行全体のオーケストレーションを担当。実行中の静的可変状態は持ちません。
- `Services/DiffExecutionContext.cs`: old/new/report パス、IL 出力パス、ネットワーク最適化判定をまとめた実行単位の不変コンテキスト。
- `Services/FolderDiffService.cs`: ファイル列挙、並列実行、集計を含む全体オーケストレーション。
- `Services/FileDiffService.cs`: ファイル単位の判定（`MD5Match` / `ILMatch` / `TextMatch` など）。
- `Services/ILOutputService.cs`: IL 比較フローと IL 出力のファサード。
- `Services/DotNetDisassembleService.cs`: 逆アセンブラ実行、バージョン/フィンガープリント管理、失敗ブラックリスト。
- `Services/Caching/ILCache.cs`: `ファイルハッシュ + ツール識別子` ベースのメモリ/ディスクキャッシュ。
- `Services/ReportGenerateService.cs`: `diff_report.md` の各セクションとサマリー出力。
- `Models/FileDiffResultLists.cs`: 実行単位のスレッドセーフ共有状態。

DI 構成メモ:
- `Program.cs` ではアプリ全体のルートサービス（`ILoggerService`, `ConfigService`, `ProgramRunner`）だけを登録します。
- `ProgramRunner` が実行ごとの `ServiceProvider` を作成し、`ConfigSettings`, `DiffExecutionContext`, `FileDiffResultLists`, キャッシュ系サービス、差分/レポート系サービスをスコープ化します。
- `FileDiffService`, `ILOutputService`, `FolderDiffService`, `DotNetDisassembleService`, `ILTextOutputService` は、`ActivatorUtilities.CreateInstance` に頼らず、コンストラクタ注入された依存関係または `DiffExecutionContext` を使います。
- この構成により、テストでは `IFileDiffService`, `IILOutputService`, `IFolderDiffService`, `IDotNetDisassembleService`, `IILTextOutputService` を差し替え可能です。

比較パイプライン:
1. まず MD5 比較。
2. 両方が .NET アセンブリ（PE/CLR 判定）なら IL 比較。
3. `TextFileExtensions` 対象はテキスト行比較。
4. それ以外は `MD5Mismatch` 判定。

## CI（GitHub Actions）

ワークフロー:
- `.github/workflows/dotnet.yml`

現在の動作:
- トリガー: `main` 向け `push` / `pull_request`、および `workflow_dispatch`。
- Checkout: `fetch-depth: 0`（Nerdbank.GitVersioning 対応）。
- ビルド: `dotnet restore` + Release `dotnet build`。
- テスト: `FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj` がある場合のみ実行。
- カバレッジ: `coverlet.collector` + `reportgenerator`。
- 成果物: `FolderDiffIL4DotNet`（publish 出力）、`TestAndCoverage`（TRX / coverage）。

## テスト

- テスト戦略、実行コマンド、カバレッジ、クラス別の検証範囲は [doc/TESTING_GUIDE.md](TESTING_GUIDE.md) を参照してください。

## パフォーマンス機能

- `MaxParallelism` によるファイル比較並列化。
- IL キャッシュ（メモリ LRU 相当 + 任意ディスク永続化）。
- MD5 先行計算と IL キャッシュ先読み。
- 大きいテキスト向けチャンク並列比較。
- 逆アセンブラ連続失敗時のブラックリスト。
- ネットワーク共有最適化（`OptimizeForNetworkShares` / `AutoDetectNetworkShares`）。

## バージョニング

- Nerdbank.GitVersioning（`version.json`）を利用。
- 生成された `AssemblyInformationalVersion` は diff レポートへ記録。

## 実装上の注意点

- `Program.cs` は薄いまま維持してください。起動フローの追加ロジックは `ProgramRunner` か下位サービスへ置き、静的状態へ戻さないでください。
- 実行ごとに新しい `DiffExecutionContext` と `FileDiffResultLists` を使う前提を崩さないでください。使い回すと、前回実行の状態混入やテスト順依存を再導入します。
- コアな協調サービスに対して、可能な箇所で `ActivatorUtilities.CreateInstance` やサービス内 `new` を再導入しないでください。テスト時の差し替え性が落ちます。
- `FolderDiffService` の実行初期化で `FileDiffResultLists.ResetAll()` を必ず維持してください。これがないと同一プロセス内で前回結果が混入します。
- old/new の IL 比較で逆アセンブラ識別子を混在させないでください。`ILOutputService` は同一識別子前提で、不一致時は例外にします。
- `ILIgnoreLineContainingStrings` の判定は「部分一致 + 大小区別あり（`StringComparison.Ordinal`）」です。互換性に影響するため変更時は注意してください。
- ネットワークパス自動検出の回復可能エラーは `false` へフォールバックし、比較処理自体は継続する方針を維持してください。
- レポート/IL 出力の読み取り専用化はベストエフォートです。`TrySetReadOnly` 失敗を致命扱いにしないでください。
- 既定並列度を変更する場合、ネットワーク最適化時の上限（`MaxParallelism <= 0` なら `min(論理コア数, 8)`）を維持してください。
- IL キャッシュキー（`ファイルハッシュ + ツール識別子`、バージョン取得失敗時はフィンガープリント代替）は安定性を壊さないように扱ってください。
- CI の「テストプロジェクト未導入でもビルドを通す」条件分岐は維持してください。
